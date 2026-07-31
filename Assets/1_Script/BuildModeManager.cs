using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildModeManager : MonoBehaviour
{
    public static BuildModeManager Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance._active;
    public static event Action<bool> OnActiveChanged;

    public enum PlacementMode { Grid, Free }

    [Header("참조")]
    [SerializeField] GridOverlay  gridOverlay;
    [SerializeField] GridRenderer gridRenderer;

    [Header("배치")]
    [SerializeField] GameObject    fallbackPrefab;

    [Tooltip("비우면 Resources/Data/Buildings 의 **전부**를 쓴다(기본). " +
             "채우면 그게 이긴다 — 특정 씬에서 일부만 짓게 하고 싶을 때의 큐레이션용.")]
    [SerializeField] BuildingData[] placeableBuildings;

    [SerializeField] LayerMask     groundMask = ~0;

    [Header("회전")]
    [SerializeField] float freeYawStep = 15f;
    [SerializeField] float gridYawStep = 90f;

    [Header("자유 배치")]
    [SerializeField] Vector3 overlapHalfExtents = new Vector3(0.45f, 0.5f, 0.45f);

    bool          _active;
    bool          _previewShown;
    PlacementMode _mode = PlacementMode.Grid;
    Vector2Int    _hoveredTile;
    int           _selectedIndex;
    float         _freeYaw;
    float         _gridYaw;
    readonly List<Vector2Int> _footprint = new() { Vector2Int.zero };
    Camera _cam;

    // ── 철거 모드 ──────────────────────────────────────────────────────────────
    bool    _demolish;
    bool    _dragging;
    Vector2 _dragStart;
    readonly HashSet<PlacedBuilding> _pending = new();

    GameObject _ghost;
    GameObject _ghostSourcePrefab;
    float      _ghostHover;   // 현재 고스트 프리팹의 저작 부양 높이

    // ── 건설 목록 ─────────────────────────────────────────────────────────────
    // **목록은 레지스트리에서 끌어온다.** 예전엔 이 씬 배열이 정본이었는데 두 가지가 나빴다:
    //   ① 씬에 사는 목록이라 에디터 도구가 등록해도 **씬을 저장 안 하면 조용히 날아갔다**
    //      (2026-07-31에 15건이 그렇게 사라졌다).
    //   ② 모드가 건물을 추가할 진입점이 없었다.
    // `Resources/Data/Buildings/`에 에셋을 두면 그것만으로 목록에 오른다.
    //
    // 인스펙터 배열을 채우면 그게 이긴다 — `ResearchTable.availableResearch`와 같은
    // "데이터가 없으면 기본 동작" 관례(큐레이션용 오버라이드).
    IReadOnlyList<BuildingData> Buildings =>
        (placeableBuildings != null && placeableBuildings.Length > 0)
            ? placeableBuildings
            : DataManager.AllBuildings;

    int TotalCount => Buildings.Count;

    // ── UI용 공개 API ─────────────────────────────────────────────────────────
    public int          TotalBuildingCount => TotalCount;
    public int          SelectedIndex      => _selectedIndex;
    public BuildingData GetBuilding(int i)
    {
        var list = Buildings;
        return (i >= 0 && i < list.Count) ? list[i] : null;
    }

    public void SelectBuilding(int index)
    {
        _selectedIndex = Mathf.Clamp(index, 0, TotalCount - 1);
        _previewShown  = false;
    }

    BuildingData SelectedBuilding => GetBuilding(_selectedIndex);

    string SelectedDisplayName => SelectedBuilding?.BuildingName ?? "?";

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_ghost != null) Destroy(_ghost);
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.bKey.wasPressedThisFrame)
            SetActive(!_active);

        if (Keyboard.current.escapeKey.wasPressedThisFrame && _active)
            SetActive(false);

        if (!_active) return;

        if (Keyboard.current.xKey.wasPressedThisFrame)
            ToggleDemolish();

        if (_demolish) { UpdateDemolish(); return; }

        if (Keyboard.current.tabKey.wasPressedThisFrame)
            ToggleMode();

        if (_mode == PlacementMode.Free)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) _freeYaw -= freeYawStep;
            if (Keyboard.current.eKey.wasPressedThisFrame) _freeYaw += freeYawStep;
        }
        else
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) _gridYaw -= gridYawStep;
            if (Keyboard.current.eKey.wasPressedThisFrame) _gridYaw += gridYawStep;
        }

        UpdatePreview();
    }

    void ToggleMode()
    {
        _mode = _mode == PlacementMode.Grid ? PlacementMode.Free : PlacementMode.Grid;
        _previewShown = false;
        gridRenderer.HidePreview();
    }

    // ── 철거 모드 ──────────────────────────────────────────────────────────────

    void ToggleDemolish()
    {
        _demolish = !_demolish;
        ClearPending();
        _dragging = false;
        HideGhost();
        gridRenderer.HidePreview();
        _previewShown = false;
    }

    void UpdateDemolish()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // UI 위에서는 철거 안 함
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearPending();
            return;
        }

        Vector2 cur = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _dragStart = cur;
            _dragging  = false;
        }

        if (mouse.leftButton.isPressed && !_dragging &&
            Vector2.Distance(_dragStart, cur) > 8f)
            _dragging = true;

        // 예정 표시 갱신: 드래그 중이면 박스 내부, 아니면 커서 위 단일.
        RefreshPending(cur);

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            ExecuteDemolish();
            _dragging = false;
        }
    }

    void RefreshPending(Vector2 cur)
    {
        var next = new HashSet<PlacedBuilding>();

        if (_dragging)
        {
            float minX = Mathf.Min(_dragStart.x, cur.x), maxX = Mathf.Max(_dragStart.x, cur.x);
            float minY = Mathf.Min(_dragStart.y, cur.y), maxY = Mathf.Max(_dragStart.y, cur.y);
            foreach (var pb in PlacedBuilding.All)
            {
                if (pb == null) continue;
                var sp = _cam.WorldToScreenPoint(pb.transform.position);
                if (sp.z < 0f) continue;
                if (sp.x >= minX && sp.x <= maxX && sp.y >= minY && sp.y <= maxY)
                    next.Add(pb);
            }
        }
        else
        {
            var hit = PlacedBuilding.PickUnderCursor(_cam, cur);
            if (hit != null) next.Add(hit);
        }

        // diff: 빠진 건 해제, 새로 든 건 표시
        foreach (var pb in _pending)
            if (pb != null && !next.Contains(pb)) pb.SetPendingHighlight(false);
        foreach (var pb in next)
            if (!_pending.Contains(pb)) pb.SetPendingHighlight(true);

        _pending.Clear();
        foreach (var pb in next) _pending.Add(pb);
    }

    void ExecuteDemolish()
    {
        foreach (var pb in _pending)
            if (pb != null) pb.Demolish(); // 파괴되면서 All/하이라이트도 정리됨
        _pending.Clear();
    }

    void ClearPending()
    {
        foreach (var pb in _pending)
            if (pb != null) pb.SetPendingHighlight(false);
        _pending.Clear();
    }

    void OnGUI()
    {
        if (!_active) return;

        if (_demolish)
        {
            GUI.color = new Color(1f, 0.7f, 0.7f, 1f);
            GUI.Label(new Rect(10, 10, 600, 24),
                "[철거] 클릭=단일 / 드래그=일괄 (건설비 75% 환불)  X=철거 끄기  Esc=나가기");
            GUI.color = Color.white;
            DrawDemolishBox();
            return;
        }

        float yaw = _mode == PlacementMode.Grid ? _gridYaw : _freeYaw;
        string modeLabel = $"{(_mode == PlacementMode.Grid ? "그리드" : "자유")} ({yaw:F0}°)";
        string capInfo   = "";
        var core = DungeonCore.Instance;
        if (core != null)
            capInfo = $"  하수인: {core.CurrentMinionCount}/{core.TotalMinionCap}";
        GUI.Label(new Rect(10, 10, 600, 24),
            $"[건설] {SelectedDisplayName} ({_selectedIndex + 1}/{TotalCount})  모드: {modeLabel}  Tab=모드 Q/E=회전 X=철거{capInfo}");
    }

    void DrawDemolishBox()
    {
        if (!_dragging) return;
        Vector2 cur = Mouse.current.position.ReadValue();
        Vector2 s = new(_dragStart.x, Screen.height - _dragStart.y);
        Vector2 e = new(cur.x,        Screen.height - cur.y);
        Rect rect = new(Mathf.Min(s.x, e.x), Mathf.Min(s.y, e.y),
                        Mathf.Abs(s.x - e.x), Mathf.Abs(s.y - e.y));
        GUI.color = new Color(1f, 0.3f, 0.3f, 0.25f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
    }

    // 외부에서 건설 모드를 강제 종료 (월드맵 전환 등 — 고스트/미리보기가 남지 않게).
    public void Deactivate()
    {
        if (_active) SetActive(false);
    }

    void SetActive(bool active)
    {
        _active = active;
        OnActiveChanged?.Invoke(active);
        gridOverlay.SetVisible(active);
        if (!active)
        {
            gridRenderer.HidePreview();
            _previewShown = false;
            HideGhost();
            ClearPending();
            _demolish = false;
            _dragging = false;
        }
    }

    // ── 표면 스냅 ──────────────────────────────────────────────────────────────

    // 프리팹 피벗이 중앙/임의 위치여도 **렌더러 바닥면**이 표면(surfaceY)에 닿도록 Y를 보정한다.
    // 피벗 규칙을 프리팹마다 맞추지 않아도 되고, 바닥 슬래브 두께도 surfaceY에 이미 반영돼 있음.
    // (surfaceY = MapManager.SurfaceHeight — 바닥이 있으면 그 윗면, 없으면 지형 높이)
    // 프리팹에 저작된 "부양 높이" — 루트 localPosition.y. 서브코어처럼 일부러 띄워 만든 프리팹은
    // 이 값이 양수다. Instantiate(prefab, pos, rot)는 이 Y를 덮어써 버리므로 배치 시 다시 얹어준다.
    public static float AuthoredHover(GameObject prefab) =>
        prefab != null ? Mathf.Max(0f, prefab.transform.localPosition.y) : 0f;

    // 표면 위에 스폰. 저작된 부양 높이가 있으면 그만큼 띄우고(그 프리팹은 바닥 스냅 안 함),
    // 없으면 렌더러 바닥면을 표면에 맞춘다.
    public static GameObject SpawnOnSurface(GameObject prefab, Vector3 surfacePos, Quaternion rot)
    {
        float hover = AuthoredHover(prefab);
        if (hover > 0f)
            return Instantiate(prefab, surfacePos + Vector3.up * hover, rot);

        var go = Instantiate(prefab, surfacePos, rot);
        SnapOnSurface(go, surfacePos.y);
        return go;
    }

    public static void SnapOnSurface(GameObject go, float surfaceY)
    {
        if (go == null) return;

        bool  found = false;
        var   bounds = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            // 파티클/트레일 등은 바운드가 실제 형상과 무관해 제외 — 메시 계열만 사용.
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else        bounds.Encapsulate(r.bounds);
        }
        if (!found) return;   // 렌더러가 없으면 피벗 그대로 (보정 근거 없음)

        // 올리기만 한다 — 메시가 피벗보다 위에 저작된(일부러 띄운) 프리팹을 끌어내리지 않도록.
        float dy = surfaceY - bounds.min.y;
        if (dy > 0.0001f)
            go.transform.position += Vector3.up * dy;
    }

    // ── 고스트 미리보기 ────────────────────────────────────────────────────────

    void EnsureGhost()
    {
        var prefab = ResolvePrefab();
        if (prefab == null) { HideGhost(); return; }
        if (_ghost != null && _ghostSourcePrefab == prefab) return;

        if (_ghost != null) Destroy(_ghost);

        // 비활성 컨테이너 아래에서 인스턴스화 → 자식의 Awake/OnEnable이 유예된다(Unity는 비활성
        // 계층의 스크립트를 깨우지 않음). 이 상태에서 스크립트를 제거하면, 이후 ShowGhost가
        // 활성화해도 창고 등록·이벤트 구독 등 부수효과가 전혀 실행되지 않는 '순수 시각용' 고스트가 된다.
        // (기존엔 활성 프리팹을 Instantiate → Awake/OnEnable이 한 프레임 실행된 뒤에야 비활성화.)
        _ghost = new GameObject("[BuildGhost]");
        _ghost.SetActive(false);

        var model = Instantiate(prefab, _ghost.transform);
        // 저작된 부양 높이를 그대로 재현 — 미리보기와 실제 배치 높이를 일치시킨다.
        _ghostHover = AuthoredHover(prefab);
        model.transform.localPosition = new Vector3(0f, _ghostHover, 0f);
        model.transform.localRotation = Quaternion.identity;
        _ghostSourcePrefab = prefab;

        // 비활성이라 아직 Awake 전 — 지금 제거하면 활성화돼도 영영 실행되지 않는다(렌더러/메시는 남음).
        // 배치 대상 건물 컴포넌트는 상호 RequireComponent가 없어 제거 순서 무관.
        foreach (var b in model.GetComponentsInChildren<MonoBehaviour>(true))
            if (b != null) DestroyImmediate(b);
        foreach (var c in model.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
    }

    void HideGhost()
    {
        if (_ghost != null) _ghost.SetActive(false);
    }

    void ShowGhost(Vector3 pos, Quaternion rot)
    {
        EnsureGhost();
        if (_ghost == null) return;
        _ghost.SetActive(true);
        _ghost.transform.SetPositionAndRotation(pos, rot);
        // 부양 프리팹은 이미 모델을 띄워 붙였으므로 스냅하지 않는다(실제 배치와 동일 규칙).
        if (_ghostHover <= 0f) SnapOnSurface(_ghost, pos.y);
    }

    void UpdatePreview()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (_previewShown) { gridRenderer.HidePreview(); _previewShown = false; }
            HideGhost();
            return;
        }

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Vector3 point;
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
            point = hit.point;
        // 공허(던전 씬): 콜라이더가 없어도 y=0 평면 교차로 조준 — 바닥을 공허에 깔 수 있어야 함.
        else if (new Plane(Vector3.up, 0f).Raycast(ray, out float enter) && enter <= 500f)
            point = ray.GetPoint(enter);
        else
        {
            if (_previewShown) { gridRenderer.HidePreview(); _previewShown = false; }
            HideGhost();
            return;
        }

        // 바닥은 타일 정렬이 필수 — 자유 모드여도 그리드 배치로 강제.
        bool forceGrid = SelectedBuilding != null && SelectedBuilding.IsFloor;
        if (_mode == PlacementMode.Grid || forceGrid) UpdateGridPreview(point);
        else                                          UpdateFreePreview(point);
    }

    void UpdateGridPreview(Vector3 worldPoint)
    {
        var tile = MapManager.Instance.WorldToTile(worldPoint);
        if (tile != _hoveredTile || !_previewShown)
        {
            _hoveredTile  = tile;
            _previewShown = true;
            var fp = GetFootprint(tile);
            gridRenderer.ShowPreview(fp, IsBuildableGrid(fp));
        }

        var snapWorld = MapManager.Instance.TileToGround(tile);
        ShowGhost(snapWorld, Quaternion.Euler(0, _gridYaw, 0));

        // 바닥은 드래그 페인트(좌클릭 유지로 연속 배치) — 이미 깔린 타일은 CanPlaceFloor가 거름.
        bool paintDrag = SelectedBuilding != null && SelectedBuilding.IsFloor
                         && Mouse.current.leftButton.isPressed;
        if (Mouse.current.leftButton.wasPressedThisFrame || paintDrag)
            TryPlaceGrid(tile);
    }

    void UpdateFreePreview(Vector3 worldPoint)
    {
        bool placeable = IsBuildableFree(worldPoint, _freeYaw);
        var snapTile   = MapManager.Instance.WorldToTile(worldPoint);
        _hoveredTile   = snapTile;
        _previewShown  = true;
        gridRenderer.ShowPreview(GetFootprint(snapTile), placeable);

        // XZ는 자유, Y는 바닥 윗면으로 — 레이가 어디에 맞았든 슬래브 위에 얹힌다.
        var surfacePoint = OnSurface(worldPoint);
        ShowGhost(surfacePoint, Quaternion.Euler(0, _freeYaw, 0));

        if (Mouse.current.leftButton.wasPressedThisFrame && placeable)
            TryPlaceFree(surfacePoint, _freeYaw);
    }

    // 바닥이 깔린 타일이면 Y를 슬래브 윗면으로 교체. 바닥이 없으면(지형 맵) 레이 높이 그대로.
    Vector3 OnSurface(Vector3 p)
    {
        var map = MapManager.Instance;
        if (map != null && map.TryGetFloorSurface(map.WorldToTile(p), out float y)) p.y = y;
        return p;
    }

    // ── 검증 ───────────────────────────────────────────────────────────────────

    bool IsBuildableGrid(List<Vector2Int> footprint)
    {
        var map = MapManager.Instance;
        var sel = SelectedBuilding;
        // 바닥은 바닥요구를 우회(공허에 깖), 건물은 바닥 위+점유+평탄(IsBuildable로 일원화).
        bool ok = sel != null && sel.IsFloor
            ? map.CanPlaceFloor(footprint)
            : map.IsBuildable(footprint);
        if (!ok) return false;
        foreach (var bp in BuildingBlueprint.All)
            if (footprint.Contains(map.WorldToTile(bp.transform.position)))
                return false;
        // 완성된 건물과도 중복 금지 (점유 플래그 무관 — 창고/침대라도 겹쳐 쌓지 못하게). 바닥은 밑에 깔리므로 제외.
        if (sel == null || !sel.IsFloor)
            foreach (var pb in PlacedBuilding.All)
                if (pb.Data != null && !pb.Data.IsFloor
                    && footprint.Contains(map.WorldToTile(pb.transform.position)))
                    return false;
        return true;
    }

    bool IsBuildableFree(Vector3 worldPoint, float yaw)
    {
        var map = MapManager.Instance;
        if (!map.IsInBuildZone(worldPoint)) return false;

        // 던전형 맵: 자유 배치도 바닥 위에만 (그리드와 동일 게이트).
        if (map.FloorRequired && !map.IsFloored(worldPoint)) return false;

        // 모델 A: 코어 union은 건설을 차단하지 않음(union 밖은 BuildingDormancy가 휴면 처리).

        var rotation = Quaternion.Euler(0, yaw, 0);
        var center   = worldPoint + Vector3.up * overlapHalfExtents.y;
        var hits     = Physics.OverlapBox(center, overlapHalfExtents, rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.GetComponentInParent<ResourceNode>() != null) return false;
            if (h.GetComponentInParent<DungeonCore>()  != null) return false;
        }
        foreach (var bp in BuildingBlueprint.All)
            if (Vector3.Distance(worldPoint, bp.transform.position) < 1f) return false;
        // 완성된 건물과도 중복 금지 (바닥 제외 — 건물은 바닥 위에 배치).
        foreach (var pb in PlacedBuilding.All)
            if (pb.Data != null && !pb.Data.IsFloor
                && Vector3.Distance(worldPoint, pb.transform.position) < 1f) return false;
        return true;
    }

    // ── 배치 ───────────────────────────────────────────────────────────────────

    void TryPlaceGrid(Vector2Int tile)
    {
        var footprint = GetFootprint(tile);
        if (!IsBuildableGrid(footprint)) return;

        Place(MapManager.Instance.TileToGround(tile), Quaternion.Euler(0, _gridYaw, 0));
        _previewShown = false;
    }

    void TryPlaceFree(Vector3 worldPoint, float yaw)
    {
        Place(worldPoint, Quaternion.Euler(0, yaw, 0));
        _previewShown = false;
    }

    void Place(Vector3 pos, Quaternion rot)
    {
        var data = SelectedBuilding;
        if (data == null) return;

        var prefab = data.Prefab != null ? data.Prefab : fallbackPrefab;
        if (prefab == null) return;

        if (data.CostAmount > 0 && !Storage.Consume(data.CostType, data.CostAmount))
        {
            Debug.Log($"[Build] 자원 부족 — {data.CostType} ×{data.CostAmount}");
            return;
        }

        // 바닥(모델 A): 코어 무관 substrate. 휴면/장애물 처리 없음. Floor 컴포넌트가 바닥 자동 등록.
        if (data.IsFloor)
        {
            var floor = SpawnOnSurface(prefab, pos, rot);   // 슬래브 바닥면을 지면에 붙임
            var fc = floor.GetComponent<Floor>();
            if (fc == null) fc = floor.AddComponent<Floor>();   // 프리팹 누락 대비 안전망
            fc.Refresh();   // 스냅으로 Y가 바뀌었으니 등록 높이(윗면) 재계산
            PlacedBuilding.Attach(floor, data);   // 철거 지원(환불 0)
            Debug.Log($"[Build] 바닥 배치 — {data.BuildingName}");
            return;
        }

        if (data.UsesBlueprint)
        {
            var bp = new GameObject($"[Blueprint] {data.BuildingName}");
            bp.transform.SetPositionAndRotation(pos, rot);
            bp.AddComponent<BuildingBlueprint>().Init(data);
            return;
        }

        // 표면 위에 얹기 — 피벗이 중앙이면 바닥면 스냅, 저작된 부양 높이(서브코어 등)가 있으면 그만큼 띄움.
        var instance = SpawnOnSurface(prefab, pos, rot);
        instance.GetComponent<Storage>()?.Init(data);
        instance.GetComponent<Bed>()?.Init(data);
        instance.GetComponent<FarmPlot>()?.Init(data);
        instance.GetComponent<WaterSource>()?.Init(data);
        // 핵 영역 이탈 시 휴면/재가동 (설계: design_core_creep.md step 3)
        BuildingDormancy.Ensure(instance);
        // 철거/환불용 메타 보관
        var placed = PlacedBuilding.Attach(instance, data);
        // 타일 점유(통행 차단) 건물이면 footprint를 통행 불가로 등록 (철거 시 대칭 해제)
        if (data.OccupiesTile && MapManager.Instance != null)
            placed.OccupyTiles(new[] { MapManager.Instance.WorldToTile(pos) });
        // 벽 등 장애물이면 메소 격자에 반영 (CheckBox가 Wall 콜라이더 감지)
        HierarchicalPathfinder.Instance?.RebakeObstacleAt(pos);
        Debug.Log($"[Build] 배치 완료 — {data.BuildingName}");
    }

    GameObject ResolvePrefab()
    {
        var bd = SelectedBuilding;
        return (bd != null && bd.Prefab != null) ? bd.Prefab : fallbackPrefab;
    }

    List<Vector2Int> GetFootprint(Vector2Int center)
    {
        _footprint[0] = center;
        return _footprint;
    }
}
