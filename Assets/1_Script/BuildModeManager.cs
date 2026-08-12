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
    readonly HashSet<PlacedBuilding>    _pending   = new();
    // **청사진도 같은 모드로 지운다** — 지우는 길이 둘이면 하나가 어긋난다. 다만 결과는
    // 다르다: 완성 건물은 부수는 것이라 75%, 청사진은 무효화라 전액 환불(`Cancel`).
    readonly HashSet<BuildingBlueprint> _pendingBp = new();

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
        _lineAnchor   = null;
        gridRenderer.HidePreview();
    }

    // **줄 긋기 기준점은 모드를 벗어나면 버린다.**
    //
    // 안 버리면 기준을 찍어 둔 채 Esc로 나갔다 B로 다시 들어왔을 때, **첫 클릭이 옛 기준부터
    // 지금 커서까지 통째로 짓는다** — 시킨 적 없는 스무 칸이 한 번에 선다. 건설 모드를
    // 껐다 켜는 것·철거로 넘어가는 것·고른 건물을 바꾸는 것 모두 "그 줄은 없던 일"이다.
    void ClearLineAnchor() => _lineAnchor = null;

    // ── 철거 모드 ──────────────────────────────────────────────────────────────

    void ToggleDemolish()
    {
        _demolish = !_demolish;
        ClearPending();
        ClearLineAnchor();
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
        var next   = new HashSet<PlacedBuilding>();
        var nextBp = new HashSet<BuildingBlueprint>();

        if (_dragging)
        {
            float minX = Mathf.Min(_dragStart.x, cur.x), maxX = Mathf.Max(_dragStart.x, cur.x);
            float minY = Mathf.Min(_dragStart.y, cur.y), maxY = Mathf.Max(_dragStart.y, cur.y);

            bool InBox(Vector3 world)
            {
                var sp = _cam.WorldToScreenPoint(world);
                return sp.z >= 0f && sp.x >= minX && sp.x <= maxX && sp.y >= minY && sp.y <= maxY;
            }

            foreach (var pb in PlacedBuilding.All)
                if (pb != null && InBox(pb.transform.position)) next.Add(pb);

            foreach (var bp in BuildingBlueprint.All)
                if (bp != null && InBox(bp.transform.position)) nextBp.Add(bp);
        }
        else
        {
            var hit = PlacedBuilding.PickUnderCursor(_cam, cur);
            if (hit != null) next.Add(hit);

            // **청사진은 콜라이더로 못 집는다** — 고스트가 콜라이더를 다 꺼 두었기 때문이다
            // (청사진은 통행을 안 막고, 켜면 선택·이동 명령의 레이캐스트에 걸린다).
            // 커서 아래 지면 지점을 타일로 바꿔 그 칸의 청사진을 찾는다.
            //
            // 완성 건물이 잡혔으면 안 찾는다 — 다 지어진 건물이 청사진 자리에 겹쳐 있을 리
            // 없지만, 겹쳤다면 눈에 보이는 쪽(실체)이 이기는 게 맞다.
            if (hit == null && TryCursorGround(cur, out var point) && MapManager.Instance != null)
            {
                var bp = BuildingBlueprint.PickAt(MapManager.Instance.WorldToTile(point));
                if (bp != null) nextBp.Add(bp);
            }
        }

        // diff: 빠진 건 해제, 새로 든 건 표시
        foreach (var pb in _pending)
            if (pb != null && !next.Contains(pb)) pb.SetPendingHighlight(false);
        foreach (var pb in next)
            if (!_pending.Contains(pb)) pb.SetPendingHighlight(true);

        foreach (var bp in _pendingBp)
            if (bp != null && !nextBp.Contains(bp)) bp.SetPendingHighlight(false);
        foreach (var bp in nextBp)
            if (!_pendingBp.Contains(bp)) bp.SetPendingHighlight(true);

        _pending.Clear();
        foreach (var pb in next) _pending.Add(pb);

        _pendingBp.Clear();
        foreach (var bp in nextBp) _pendingBp.Add(bp);
    }

    void ExecuteDemolish()
    {
        foreach (var pb in _pending)
            if (pb != null) pb.Demolish(); // 파괴되면서 All/하이라이트도 정리됨
        _pending.Clear();

        foreach (var bp in _pendingBp)
            if (bp != null) bp.Cancel();   // 납품된 자재는 전액 창고로
        _pendingBp.Clear();
    }

    void ClearPending()
    {
        foreach (var pb in _pending)
            if (pb != null) pb.SetPendingHighlight(false);
        _pending.Clear();

        foreach (var bp in _pendingBp)
            if (bp != null) bp.SetPendingHighlight(false);
        _pendingBp.Clear();
    }

    void OnGUI()
    {
        if (!_active) return;

        if (_demolish)
        {
            GUI.color = new Color(1f, 0.7f, 0.7f, 1f);
            GUI.Label(new Rect(10, 10, 600, 24),
                "[철거] 클릭=단일 / 드래그=일괄 (자재 전액 환불)  X=철거 끄기  Esc=나가기");
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
            ClearLineAnchor();
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
        ClearLineGhosts();   // 여벌은 이것의 복제라 원본이 바뀌면 같이 버린다
        ClearLineAnchor();   // 다른 건물을 고른 것은 긋던 줄을 버린다는 뜻이다

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

        ShowWorkSpotOnGhost(model);
    }

    // 고스트에서도 **작업 자리 화살표**를 보여준다.
    //
    // 스크립트를 다 떼기 때문에 `WorkSpot`이 스스로 켤 수 없다 — 그런데 정작 표식이 제일
    // 필요한 순간이 **놓기 직전**이다. 완성된 건물에서만 보이면 "막고 나서 알게 되는" 것과 같다.
    // 그래서 이름으로 찾아 직접 켠다(컴포넌트가 없어도 계층은 그대로 남아 있다).
    static void ShowWorkSpotOnGhost(GameObject model)
    {
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "WorkSpot") continue;
            t.gameObject.SetActive(true);
            foreach (Transform child in t) child.gameObject.SetActive(true);
        }
    }

    void HideGhost()
    {
        if (_ghost != null) _ghost.SetActive(false);
        HideLineGhosts();
    }

    // ── 줄 긋기의 여벌 고스트 ────────────────────────────────────────────────
    //
    // **지어질 자리마다 모델이 서야 한다.** 초록 격자만으로는 몇 칸인지는 알아도 어떤 모양이
    // 어느 방향으로 설지는 안 보인다 — 담을 두르는 일은 그림이 맞는지가 전부다.
    //
    // 커서 쪽 고스트(`_ghost`) 하나를 **복제해 늘려 쓴다.** 프리팹에서 새로 만들면 스크립트를
    // 떼고 콜라이더를 끄는 준비를 매번 다시 해야 하는데, 그 준비가 끝난 것이 이미 있다.
    readonly List<GameObject> _lineGhosts = new();

    void HideLineGhosts()
    {
        foreach (var g in _lineGhosts)
            if (g != null) g.SetActive(false);
    }

    void ShowLineGhosts(List<Vector2Int> tiles, Quaternion rot)
    {
        EnsureGhost();
        if (_ghost == null || tiles == null) { HideLineGhosts(); return; }

        // 첫 칸은 커서 고스트가 맡는다 — 여벌은 그 나머지(단 `MaxLineGhosts`까지).
        int need = Mathf.Clamp(tiles.Count - 1, 0, MaxLineGhosts);

        while (_lineGhosts.Count < need)
        {
            var clone = Instantiate(_ghost, transform);
            clone.name = $"[BuildGhost.Line{_lineGhosts.Count}]";
            _lineGhosts.Add(clone);
        }

        for (int i = 0; i < _lineGhosts.Count; i++)
        {
            var g = _lineGhosts[i];
            if (g == null) continue;

            if (i >= need) { g.SetActive(false); continue; }

            g.SetActive(true);
            var pos = FootprintCenter(GetFootprint(tiles[i + 1]));
            g.transform.SetPositionAndRotation(pos, rot);
            if (_ghostHover <= 0f) SnapOnSurface(g, pos.y);
        }
    }

    // 고른 건물이 바뀌면 여벌도 버린다 — 안 버리면 지난 건물의 모델이 줄에 섞인다.
    void ClearLineGhosts()
    {
        foreach (var g in _lineGhosts)
            if (g != null) Destroy(g);
        _lineGhosts.Clear();
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

    // 커서 아래의 지면 지점. 배치 미리보기와 철거 모드의 청사진 픽킹이 **같은 조준**을 쓴다 —
    // 답이 두 벌이면 미리보기가 가리키는 칸과 실제로 집히는 칸이 어긋난다.
    bool TryCursorGround(Vector2 screenPos, out Vector3 point)
    {
        point = default;
        if (_cam == null) return false;

        Ray ray = _cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
        {
            point = hit.point;
            return true;
        }

        // 공허(던전 씬): 콜라이더가 없어도 y=0 평면 교차로 조준 — 바닥을 공허에 깔 수 있어야 함.
        if (new Plane(Vector3.up, 0f).Raycast(ray, out float enter) && enter <= 500f)
        {
            point = ray.GetPoint(enter);
            return true;
        }
        return false;
    }

    void UpdatePreview()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (_previewShown) { gridRenderer.HidePreview(); _previewShown = false; }
            HideGhost();
            return;
        }

        if (!TryCursorGround(Mouse.current.position.ReadValue(), out var point))
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

    // 초록 격자를 마지막으로 그렸을 때의 조건. **타일만 보면 안 된다** — 발자국은
    // 회전(3×2 → 2×3)과 고른 건물에 따라서도 바뀐다.
    float _previewYaw;
    int   _previewIndex = -1;

    // ── 두 번 클릭으로 줄·면을 한 번에 ────────────────────────────────────────
    //
    // **한 번 눌러 기준을 잡고, 늘였다 줄였다 하다가, 다시 눌러 짓는다.**
    //
    // 처음엔 좌클릭을 누른 채 끄는 페인트였는데, 그건 **커서가 지나간 자리**를 칠할 뿐이라
    // 곧게 긋기가 어렵고 얼마나 지어질지 놓기 전에는 알 수 없다. 담을 두르는 일은 대개
    // "여기서 저기까지"라 **두 점을 찍는 것**이 맞는 조작이다.
    //
    // **벽은 줄, 바닥은 면.** 벽을 사각으로 채우면 속이 꽉 찬 덩어리가 되어 쓸 일이 없고,
    // 바닥을 줄로만 깔면 방 하나에 열 번을 그어야 한다. 줄은 **더 많이 움직인 축**으로만
    // 곧게 간다(대각선은 벽으로 안 쓴다 — 칸이 모서리로만 붙어 사람이 새어 나간다).
    //
    // **저작이 정한다**(`BuildingData.DragBuild`). 크기로 가르면 우리·작업대·서브코어처럼
    // **잇대어 쓰지 않는 한 칸짜리까지** 줄로 그어진다 — "한 칸이냐"와 "잇대어 쓰느냐"는
    // 다른 물음이다. 여러 칸짜리는 발자국이 겹쳐 어디까지 지어질지 예측이 안 되므로 함께 막는다.
    Vector2Int? _lineAnchor;

    bool CanDragBuild => SelectedBuilding != null
                      && SelectedBuilding.DragBuild
                      && SelectedBuilding.Size.x == 1 && SelectedBuilding.Size.y == 1;

    // 바닥은 면, 그 밖(벽·목책·바리케이드)은 줄.
    bool RectDrag => SelectedBuilding != null && SelectedBuilding.IsFloor;

    // **한 번에 그을 수 있는 칸 수.** 넉넉히 둔다 — 진짜 한도는 나중에 맵 크기가 정할 일이고,
    // 여기서 좁게 잡으면 큰 방을 한 번에 못 깐다. 이 값은 **폭주 방지선**이지 규칙이 아니다.
    const int MaxDragTiles = 5000;

    // **비싼 것은 칸이 아니라 고스트다.** 칸 재기는 한 칸에 몇 번의 비교지만, 고스트는
    // 건물 모델을 통째로 `Instantiate`한다 — 5000개면 화면이 멎는다.
    //
    // 그래서 **둘을 따로 자른다**: 초록 격자는 그을 수 있는 만큼 전부 보여 주고(어디까지
    // 지어질지는 그것이 답한다), 모델은 앞쪽 얼마까지만 세운다. 모델의 몫은 "어떤 모양이
    // 어느 방향으로 서나"를 보여 주는 것이라 몇 개만 있어도 그 답이 나온다.
    const int MaxLineGhosts = 200;

    static void LineOrRect(Vector2Int a, Vector2Int b, bool rect, List<Vector2Int> into)
    {
        into.Clear();

        if (rect)
        {
            // 기준에서 가까운 쪽부터 채우도록 범위를 먼저 자른다 — 잘린 뒤에도 **기준이
            // 모서리로 남아야** 어느 방향으로 늘어나는지가 그대로 보인다.
            int spanX = Mathf.Abs(b.x - a.x) + 1;
            int spanY = Mathf.Abs(b.y - a.y) + 1;

            if ((long)spanX * spanY > MaxDragTiles)
            {
                int limX = Mathf.Max(1, Mathf.Min(spanX, Mathf.FloorToInt(Mathf.Sqrt(MaxDragTiles))));
                int limY = Mathf.Max(1, MaxDragTiles / limX);

                b.x = a.x + Mathf.Clamp(b.x - a.x, -(limX - 1), limX - 1);
                b.y = a.y + Mathf.Clamp(b.y - a.y, -(limY - 1), limY - 1);
            }
        }

        if (rect)
        {
            for (int x = Mathf.Min(a.x, b.x); x <= Mathf.Max(a.x, b.x); x++)
            for (int y = Mathf.Min(a.y, b.y); y <= Mathf.Max(a.y, b.y); y++)
                into.Add(new Vector2Int(x, y));
            return;
        }

        // **더 많이 움직인 축으로만** 곧게 간다.
        if (Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.y - a.y))
        {
            b.x = a.x + Mathf.Clamp(b.x - a.x, -(MaxDragTiles - 1), MaxDragTiles - 1);
            for (int x = Mathf.Min(a.x, b.x); x <= Mathf.Max(a.x, b.x); x++)
                into.Add(new Vector2Int(x, a.y));
        }
        else
        {
            b.y = a.y + Mathf.Clamp(b.y - a.y, -(MaxDragTiles - 1), MaxDragTiles - 1);
            for (int y = Mathf.Min(a.y, b.y); y <= Mathf.Max(a.y, b.y); y++)
                into.Add(new Vector2Int(a.x, y));
        }
    }

    readonly List<Vector2Int> _lineTiles = new();
    readonly List<Vector2Int> _oneTile   = new();   // 한 칸씩 물어볼 때 돌려 쓰는 그릇

    void UpdateGridPreview(Vector3 worldPoint)
    {
        var tile = MapManager.Instance.WorldToTile(worldPoint);

        if (CanDragBuild) { UpdateLineBuild(tile); return; }

        // **Q/E로 돌리면 그 자리에서 바로 다시 그린다.** 전엔 타일이 바뀔 때만 그려서,
        // 회전이 고스트 모델에는 즉시 붙는데 초록 격자는 **한 칸 옮겨야** 따라왔다
        // (모델과 격자가 서로 다른 자리를 가리키는 동안 어디에 지어질지 알 수 없다).
        bool stale = tile != _hoveredTile
                  || !_previewShown
                  || !Mathf.Approximately(_previewYaw, _gridYaw)
                  || _previewIndex != _selectedIndex;

        if (stale)
        {
            _hoveredTile   = tile;
            _previewShown  = true;
            _previewYaw    = _gridYaw;
            _previewIndex  = _selectedIndex;
            var fp = GetFootprint(tile);
            gridRenderer.ShowPreview(fp, IsBuildableGrid(fp));
        }

        // 고스트도 차지한 칸 전체의 중앙에 — 미리보기 격자와 모델이 같은 자리를 가리켜야 한다.
        var snapWorld = FootprintCenter(GetFootprint(tile));
        ShowGhost(snapWorld, Quaternion.Euler(0, _gridYaw, 0));

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryPlaceGrid(tile);
    }

    // 한 칸짜리 — 두 번 클릭으로 줄(벽)이나 면(바닥)을 한 번에.
    void UpdateLineBuild(Vector2Int tile)
    {
        // 기준을 안 잡았으면 평소처럼 한 칸 미리보기.
        var from = _lineAnchor ?? tile;
        LineOrRect(from, tile, RectDrag, _lineTiles);

        // **지어질 칸 전체를 미리 보여 준다.** 늘였다 줄였다 하는 동안 몇 칸이 될지가
        // 보이지 않으면 두 번 클릭의 값이 없다(그냥 여러 번 누르는 것과 같다).
        // **리스트를 칸마다 새로 만들지 않는다** — 5000칸이면 한 프레임에 5000개가 GC로 간다.
        bool ok = _lineTiles.Count > 0;
        foreach (var t in _lineTiles)
        {
            _oneTile.Clear();
            _oneTile.Add(t);
            if (!IsBuildableGrid(_oneTile)) { ok = false; break; }
        }

        gridRenderer.ShowPreview(_lineTiles, ok);
        _previewShown = true;
        _hoveredTile  = tile;

        // **지어질 자리마다 모델을 세운다.** 초록 격자만으로는 몇 칸인지는 알아도 어떤 모양이
        // 어느 방향으로 설지 안 보인다 — 담이 그림대로 이어지는지가 놓기 전에 보여야 한다.
        var rot = Quaternion.Euler(0, _gridYaw, 0);
        ShowGhost(FootprintCenter(GetFootprint(_lineTiles.Count > 0 ? _lineTiles[0] : tile)), rot);
        ShowLineGhosts(_lineTiles, rot);

        var mouse = Mouse.current;

        // 우클릭·Esc = 무르기(`Update`의 Esc는 건설 모드를 통째로 닫으므로 여기서 먼저 먹는다).
        if (_lineAnchor.HasValue && mouse.rightButton.wasPressedThisFrame)
        {
            _lineAnchor = null;
            return;
        }

        if (!mouse.leftButton.wasPressedThisFrame) return;

        if (!_lineAnchor.HasValue) { _lineAnchor = tile; return; }   // 첫 클릭 = 기준

        // 두 번째 클릭 = 짓는다. **못 짓는 칸은 건너뛴다** — 스무 칸을 그었는데 한 칸이
        // 막혔다고 전부 취소하면, 어디가 막혔는지도 모른 채 처음부터 다시 그어야 한다.
        foreach (var t in _lineTiles) TryPlaceGrid(t);
        _lineAnchor = null;
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

        Place(FootprintCenter(footprint), Quaternion.Euler(0, _gridYaw, 0), footprint);
        _previewShown = false;
    }

    void TryPlaceFree(Vector3 worldPoint, float yaw)
    {
        Place(worldPoint, Quaternion.Euler(0, yaw, 0));
        _previewShown = false;
    }

    // `footprint`는 격자 배치일 때만 온다(자유 배치는 칸 개념이 없어 중심 한 칸을 쓴다).
    void Place(Vector3 pos, Quaternion rot, List<Vector2Int> footprint = null)
    {
        var data = SelectedBuilding;
        if (data == null) return;

        var prefab = data.Prefab != null ? data.Prefab : fallbackPrefab;
        if (prefab == null) return;

        // UI에서 걸러도 **여기서 한 번 더 막는다** — 목록이 갱신되기 전에 연구가 풀리기 전
        // 상태로 배치가 들어올 수 있고, 배치 경로가 UI 하나뿐이라는 보장도 없다.
        if (!data.IsUnlocked)
        {
            Debug.Log($"[Build] 연구가 필요합니다 — {data.RequiredResearch?.ResearchName}");
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

        // **청사진은 여기서 자재를 안 낸다** — 폰이 나중에 지고 와 납품한다(`ConstructionJob`).
        // 전엔 배치 시점에 한 번 빼고 청사진이 또 걷어 **두 번 냈다.** 즉시 배치가 28종이던
        // 동안엔 비용 있는 건물이 대부분 그 길로 갔고, 청사진 다섯(침대·밭·창고·서브코어·물)에서만
        // 나던 일이라 눈에 덜 띄었다 — 13종을 청사진으로 돌리면서 전면화됐다.
        //
        // 자재가 없어도 놓을 수 있다. 청사진은 **계획**이고, 모자라면 폰이 안 갈 뿐
        // 자재가 들어오는 순간 저절로 재개된다(`BuildingBlueprint.TryRegisterJob`).
        if (data.UsesBlueprint)
        {
            var bp = new GameObject($"[Blueprint] {data.BuildingName}");
            bp.transform.SetPositionAndRotation(pos, rot);
            bp.AddComponent<BuildingBlueprint>().Init(data);
            return;
        }

        // 즉시 배치만 그 자리에서 값을 치른다. 지금 남은 즉시 배치(바닥·돌·광맥 15종)는 전부
        // 비용 0이라 실제로는 안 걸리지만, 비용 있는 건물을 즉시 배치로 되돌려도 맞게 돈다.
        if (data.CostAmount > 0 && !Storage.ConsumeByItem(data.CostItem, data.CostAmount))
        {
            Debug.Log($"[Build] 자원 부족 — {data.CostItem?.ItemName} ×{data.CostAmount}");
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
            placed.OccupyTiles(footprint != null
                ? footprint.ToArray()
                : new[] { MapManager.Instance.WorldToTile(pos) });
        // 벽 등 장애물이면 메소 격자에 반영 (CheckBox가 Wall 콜라이더 감지)
        HierarchicalPathfinder.Instance?.RebakeObstacleAt(pos);
        Debug.Log($"[Build] 배치 완료 — {data.BuildingName}");
    }

    GameObject ResolvePrefab()
    {
        var bd = SelectedBuilding;
        return (bd != null && bd.Prefab != null) ? bd.Prefab : fallbackPrefab;
    }

    // 차지할 칸 목록. **회전하면 가로·세로가 바뀐다.**
    // 홀수 크기는 기준 칸을 중앙에 두고, 짝수 크기는 +방향으로 치우친다(어느 쪽이든 하나로 정해야
    // 미리보기와 실제 배치가 어긋나지 않는다).
    List<Vector2Int> GetFootprint(Vector2Int anchor)
    {
        var size = FootprintSize();

        _footprint.Clear();
        int ox = (size.x - 1) / 2;
        int oy = (size.y - 1) / 2;
        for (int x = 0; x < size.x; x++)
        for (int y = 0; y < size.y; y++)
            _footprint.Add(new Vector2Int(anchor.x - ox + x, anchor.y - oy + y));

        return _footprint;
    }

    // **지금 미리보기가 쓰는 각도.** 자유 모드는 `_freeYaw`로 돌리는데 발자국은 `_gridYaw`만
    // 보고 있었다 — 그래서 자유 모드에서 Q/E로 침대를 돌려도 **밑의 초록색 미리보기가 그대로**였다
    // (1×2가 2×1이 되지 않는다). 바닥은 자유 모드여도 그리드로 강제되므로 그때는 `_gridYaw`다.
    float PreviewYaw =>
        _mode == PlacementMode.Grid || (SelectedBuilding != null && SelectedBuilding.IsFloor)
            ? _gridYaw : _freeYaw;

    Vector2Int FootprintSize()
    {
        var s = SelectedBuilding != null ? SelectedBuilding.Size : Vector2Int.one;
        s = new Vector2Int(Mathf.Max(1, s.x), Mathf.Max(1, s.y));

        // 90°/270° 회전이면 축이 바뀐다. **자유 모드는 15° 단위**라 반올림으로 가장 가까운
        // 직각에 붙인다 — 45° 근처에서는 어느 쪽으로 재도 반 칸씩 어긋나므로, 어긋나더라도
        // 눈에 보이는 모델과 같은 쪽으로 어긋나는 편이 낫다.
        int quarter = Mathf.RoundToInt(PreviewYaw / 90f) & 3;
        return (quarter % 2 == 1) ? new Vector2Int(s.y, s.x) : s;
    }

    // 모델은 **차지한 칸 전체의 중앙**에 놓는다. 기준 칸에 그냥 두면 짝수 크기 건물이
    // 반 칸씩 삐져나온다(미리보기 격자와 모델이 어긋나 보인다).
    Vector3 FootprintCenter(List<Vector2Int> footprint)
    {
        var map = MapManager.Instance;
        if (map == null || footprint.Count == 0) return Vector3.zero;

        var min = footprint[0];
        var max = footprint[0];
        foreach (var t in footprint)
        {
            min = Vector2Int.Min(min, t);
            max = Vector2Int.Max(max, t);
        }

        var a = map.TileToGround(min);
        var b = map.TileToGround(max);
        return new Vector3((a.x + b.x) * 0.5f, Mathf.Max(a.y, b.y), (a.z + b.z) * 0.5f);
    }
}
