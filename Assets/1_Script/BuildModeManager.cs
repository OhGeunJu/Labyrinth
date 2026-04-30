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
    [SerializeField] GridOverlay gridOverlay;
    [SerializeField] GridRenderer gridRenderer;

    [Header("배치")]
    [SerializeField] GameObject fallbackPrefab;
    [SerializeField] ResourceNodeData[]    placeableNodes;
    [SerializeField] SubCoreData[]         placeableSubCores;
    [SerializeField] StorageBuildingData[] placeableStorages;
    [SerializeField] LayerMask groundMask = ~0;

    [Header("회전")]
    [SerializeField] float freeYawStep = 15f;
    [SerializeField] float gridYawStep = 90f;

    [Header("자유 배치")]
    [SerializeField] Vector3 overlapHalfExtents = new Vector3(0.45f, 0.5f, 0.45f);

    bool _active;
    bool _previewShown;
    PlacementMode _mode = PlacementMode.Grid;
    Vector2Int _hoveredTile;
    int _selectedIndex;
    float _freeYaw;
    float _gridYaw;
    readonly List<Vector2Int> _footprint = new() { Vector2Int.zero };
    Camera _cam;

    GameObject _ghost;
    GameObject _ghostSourcePrefab;

    int NodeCount    => placeableNodes    != null ? placeableNodes.Length    : 0;
    int SubCoreCount => placeableSubCores != null ? placeableSubCores.Length : 0;
    int StorageCount => placeableStorages != null ? placeableStorages.Length : 0;
    int TotalCount   => NodeCount + SubCoreCount + StorageCount;

    // ── UI용 공개 API ─────────────────────────────────────────────────────────
    public int TotalBuildingCount => TotalCount;
    public int NodeBuildingCount  => NodeCount;
    public int SelectedIndex      => _selectedIndex;
    public ResourceNodeData GetNode(int i)    => placeableNodes[i];
    public SubCoreData      GetSubCore(int i) => placeableSubCores[i];
    public void SelectBuilding(int index)
    {
        _selectedIndex = Mathf.Clamp(index, 0, TotalCount - 1);
        _previewShown  = false;
    }

    bool IsSubCoreSelected => _selectedIndex >= NodeCount && _selectedIndex < NodeCount + SubCoreCount;
    bool IsStorageSelected => _selectedIndex >= NodeCount + SubCoreCount;

    ResourceNodeData SelectedNode =>
        _selectedIndex < NodeCount && NodeCount > 0 ? placeableNodes[_selectedIndex] : null;

    SubCoreData SelectedSubCore =>
        IsSubCoreSelected && SubCoreCount > 0 ? placeableSubCores[_selectedIndex - NodeCount] : null;

    StorageBuildingData SelectedStorage =>
        IsStorageSelected && StorageCount > 0 ? placeableStorages[_selectedIndex - NodeCount - SubCoreCount] : null;

    string SelectedDisplayName =>
        IsStorageSelected ? (SelectedStorage != null ? SelectedStorage.BuildingName : "?")
        : IsSubCoreSelected ? (SelectedSubCore != null ? SelectedSubCore.CoreName : "?")
                            : (SelectedNode    != null ? SelectedNode.ResourceType.ToString() : "없음");

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
        if (Keyboard.current.bKey.wasPressedThisFrame)
            SetActive(!_active);

        if (Keyboard.current.escapeKey.wasPressedThisFrame && _active)
            SetActive(false);

        if (!_active) return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
            ToggleMode();

        if (_mode == PlacementMode.Free)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) _freeYaw -= freeYawStep;
            if (Keyboard.current.eKey.wasPressedThisFrame) _freeYaw += freeYawStep;
        }
        else // Grid
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

    void OnGUI()
    {
        if (!_active) return;
        float yaw = _mode == PlacementMode.Grid ? _gridYaw : _freeYaw;
        string modeLabel = $"{(_mode == PlacementMode.Grid ? "그리드" : "자유")} ({yaw:F0}°)";
        string capInfo = "";
        var core = DungeonCore.Instance;
        if (core != null)
            capInfo = $"  하수인: {core.CurrentMinionCount}/{core.TotalMinionCap}";
        GUI.Label(new Rect(10, 10, 500, 24),
            $"[건설] {SelectedDisplayName} ({_selectedIndex + 1}/{TotalCount})  모드: {modeLabel}  Tab=모드 Q/E=회전 휠=선택{capInfo}");
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

        // 충돌/스크립트 비활성 (시각만 사용)
        foreach (var c in _ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
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
            _hoveredTile = tile;
            _previewShown = true;
            var fp = GetFootprint(tile);
            gridRenderer.ShowPreview(fp, IsBuildableGrid(fp));
        }

        var snapWorld = MapManager.Instance.TileToWorld(tile);
        ShowGhost(snapWorld, Quaternion.Euler(0, _gridYaw, 0));

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryPlaceGrid(tile);
    }

    void UpdateFreePreview(Vector3 worldPoint)
    {
        bool placeable = IsBuildableFree(worldPoint, _freeYaw);

        // 색 피드백은 커서 아래 타일에 — 위치/회전은 고스트가 보여줌
        var snapTile = MapManager.Instance.WorldToTile(worldPoint);
        _hoveredTile = snapTile;
        _previewShown = true;
        gridRenderer.ShowPreview(GetFootprint(snapTile), placeable);

        ShowGhost(worldPoint, Quaternion.Euler(0, _freeYaw, 0));

        if (Mouse.current.leftButton.wasPressedThisFrame && placeable)
            TryPlaceFree(worldPoint, _freeYaw);
    }

    // ── 검증 ───────────────────────────────────────────────────────────────────

    bool IsBuildableGrid(List<Vector2Int> footprint)
    {
        if (!MapManager.Instance.IsBuildable(footprint)) return false;
        var core = DungeonCore.Instance;
        if (core != null)
            foreach (var t in footprint)
                if (!core.IsWithinAnyRange(t)) return false;
        foreach (var bp in BuildingBlueprint.All)
            if (footprint.Contains(MapManager.Instance.WorldToTile(bp.transform.position)))
                return false;
        return true;
    }

    bool IsBuildableFree(Vector3 worldPoint, float yaw)
    {
        var map = MapManager.Instance;
        if (!map.IsInBuildZone(worldPoint)) return false;

        var core = DungeonCore.Instance;
        if (core != null && !core.IsWithinAnyRangeWorld(worldPoint)) return false;

        var rotation = Quaternion.Euler(0, yaw, 0);
        var center = worldPoint + Vector3.up * overlapHalfExtents.y;
        var hits = Physics.OverlapBox(center, overlapHalfExtents, rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.GetComponentInParent<ResourceNode>() != null) return false;
            if (h.GetComponentInParent<DungeonCore>() != null) return false;
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

        var worldPos = MapManager.Instance.TileToWorld(tile);
        var rot      = Quaternion.Euler(0, _gridYaw, 0);

        if (IsStorageSelected)  PlaceStorage(worldPos, rot);
        else if (IsSubCoreSelected) PlaceBlueprint(worldPos, rot);
        else                   PlaceNode(worldPos, rot);

        _previewShown = false;
    }

    void TryPlaceFree(Vector3 worldPoint, float yaw)
    {
        var rot = Quaternion.Euler(0, yaw, 0);

        if (IsStorageSelected)  PlaceStorage(worldPoint, rot);
        else if (IsSubCoreSelected) PlaceBlueprint(worldPoint, rot);
        else                   PlaceNode(worldPoint, rot);

        _previewShown = false;
    }

    // 서브코어 예비 설계 배치 (자재는 하수인이 운반)
    void PlaceBlueprint(Vector3 worldPos, Quaternion rot)
    {
        var sc = SelectedSubCore;
        if (sc == null) return;
        var go = new GameObject($"[Blueprint] {sc.CoreName}");
        go.transform.SetPositionAndRotation(worldPos, rot);
        go.AddComponent<BuildingBlueprint>().Init(sc);
    }

    // ResourceNode 즉시 배치
    void PlaceNode(Vector3 worldPos, Quaternion rot)
    {
        var prefab = SelectedNode != null && SelectedNode.Prefab != null
            ? SelectedNode.Prefab : fallbackPrefab;
        if (prefab == null) return;
        var go   = Instantiate(prefab, worldPos, rot);
        var node = go.GetComponent<ResourceNode>();
        if (node != null && SelectedNode != null) node.Init(SelectedNode);
    }

    void PlaceStorage(Vector3 worldPos, Quaternion rot)
    {
        var data = SelectedStorage;
        if (data == null) return;

        // 자원 소비
        if (data.CostAmount > 0 && !Storage.Consume(data.CostType, data.CostAmount))
        {
            Debug.Log($"[Build] 창고 건설 자원 부족 — {data.CostType} ×{data.CostAmount}");
            return;
        }

        var prefab = data.Prefab != null ? data.Prefab : fallbackPrefab;
        if (prefab == null) return;

        var go      = Instantiate(prefab, worldPos, rot);
        var storage = go.GetComponent<Storage>();
        if (storage == null)
        {
            Debug.LogWarning($"[Build] 창고 프리팹 '{prefab.name}'에 Storage 컴포넌트가 없습니다.");
            Destroy(go);
            return;
        }
        storage.Init(data);
        Debug.Log($"[Build] 창고 배치 — {data.BuildingName}");
    }

    GameObject ResolvePrefab()
    {
        if (IsStorageSelected)
        {
            var sd = SelectedStorage;
            return (sd != null && sd.Prefab != null) ? sd.Prefab : fallbackPrefab;
        }
        if (IsSubCoreSelected)
        {
            var sc = SelectedSubCore;
            return (sc != null && sc.Prefab != null) ? sc.Prefab : fallbackPrefab;
        }
        var data = SelectedNode;
        return (data != null && data.Prefab != null) ? data.Prefab : fallbackPrefab;
    }

    List<Vector2Int> GetFootprint(Vector2Int center)
    {
        _footprint[0] = center;
        return _footprint;
    }
}
