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

    int TotalCount => placeableBuildings != null ? placeableBuildings.Length : 0;

    // ── UI용 공개 API ─────────────────────────────────────────────────────────
    public int          TotalBuildingCount => TotalCount;
    public int          SelectedIndex      => _selectedIndex;
    public BuildingData GetBuilding(int i) =>
        (placeableBuildings != null && i >= 0 && i < placeableBuildings.Length)
            ? placeableBuildings[i] : null;

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

    // ── 고스트 미리보기 ────────────────────────────────────────────────────────

    void EnsureGhost()
    {
        var prefab = ResolvePrefab();
        if (prefab == null) { HideGhost(); return; }
        if (_ghost != null && _ghostSourcePrefab == prefab) return;

        if (_ghost != null) Destroy(_ghost);
        _ghost = Instantiate(prefab);
        _ghostSourcePrefab = prefab;
        _ghost.name = "[BuildGhost]";

        foreach (var c in _ghost.GetComponentsInChildren<Collider>())     c.enabled = false;
        foreach (var b in _ghost.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
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
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
        {
            if (_previewShown) { gridRenderer.HidePreview(); _previewShown = false; }
            HideGhost();
            return;
        }

        if (_mode == PlacementMode.Grid) UpdateGridPreview(hit.point);
        else                              UpdateFreePreview(hit.point);
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

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryPlaceGrid(tile);
    }

    void UpdateFreePreview(Vector3 worldPoint)
    {
        bool placeable = IsBuildableFree(worldPoint, _freeYaw);
        var snapTile   = MapManager.Instance.WorldToTile(worldPoint);
        _hoveredTile   = snapTile;
        _previewShown  = true;
        gridRenderer.ShowPreview(GetFootprint(snapTile), placeable);

        ShowGhost(worldPoint, Quaternion.Euler(0, _freeYaw, 0));

        if (Mouse.current.leftButton.wasPressedThisFrame && placeable)
            TryPlaceFree(worldPoint, _freeYaw);
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
        return true;
    }

    bool IsBuildableFree(Vector3 worldPoint, float yaw)
    {
        var map = MapManager.Instance;
        if (!map.IsInBuildZone(worldPoint)) return false;

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
            var floor = Instantiate(prefab, pos, rot);
            if (floor.GetComponent<Floor>() == null) floor.AddComponent<Floor>(); // 프리팹 누락 대비 안전망
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

        var instance = Instantiate(prefab, pos, rot);
        instance.GetComponent<Storage>()?.Init(data);
        instance.GetComponent<Bed>()?.Init(data);
        instance.GetComponent<FarmPlot>()?.Init(data);
        instance.GetComponent<WaterSource>()?.Init(data);
        // 핵 영역 이탈 시 휴면/재가동 (설계: design_core_creep.md step 3)
        BuildingDormancy.Ensure(instance);
        // 철거/환불용 메타 보관
        PlacedBuilding.Attach(instance, data);
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
