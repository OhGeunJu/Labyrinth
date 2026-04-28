using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildModeManager : MonoBehaviour
{
    public static BuildModeManager Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance._active;

    public enum PlacementMode { Grid, Free }

    [Header("참조")]
    [SerializeField] GridOverlay gridOverlay;
    [SerializeField] GridRenderer gridRenderer;

    [Header("배치")]
    [SerializeField] GameObject fallbackPrefab;
    [SerializeField] ResourceNodeData[] placeableNodes;
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

    ResourceNodeData SelectedData => placeableNodes != null && placeableNodes.Length > 0
        ? placeableNodes[_selectedIndex]
        : null;

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

        HandleScroll();
        UpdatePreview();
    }

    void ToggleMode()
    {
        _mode = _mode == PlacementMode.Grid ? PlacementMode.Free : PlacementMode.Grid;
        _previewShown = false;
        gridRenderer.HidePreview();
    }

    void HandleScroll()
    {
        if (placeableNodes == null || placeableNodes.Length == 0) return;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0f) CycleSelection(-1);
        else if (scroll < 0f) CycleSelection(1);
    }

    void CycleSelection(int dir)
    {
        _selectedIndex = (_selectedIndex + dir + placeableNodes.Length) % placeableNodes.Length;
        _previewShown = false;
    }

    void OnGUI()
    {
        if (!_active) return;
        string label = SelectedData != null ? SelectedData.ResourceType.ToString() : "없음";
        int count = placeableNodes != null ? placeableNodes.Length : 0;
        float yaw = _mode == PlacementMode.Grid ? _gridYaw : _freeYaw;
        string modeLabel = $"{(_mode == PlacementMode.Grid ? "그리드" : "자유")} ({yaw:F0}°)";
        GUI.Label(new Rect(10, 10, 400, 24),
            $"[건설] {label} ({_selectedIndex + 1}/{count})  모드: {modeLabel}  Tab=모드 Q/E=회전 휠=선택");
    }

    void SetActive(bool active)
    {
        _active = active;
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
                if (!core.IsWithinRange(t)) return false;
        return true;
    }

    bool IsBuildableFree(Vector3 worldPoint, float yaw)
    {
        var map = MapManager.Instance;
        if (!map.IsInBuildZone(worldPoint)) return false;

        var core = DungeonCore.Instance;
        if (core != null && !core.IsWithinRange(map.WorldToTile(worldPoint))) return false;

        // 충돌 체크: 다른 ResourceNode/건물과 겹치면 불가
        var rotation = Quaternion.Euler(0, yaw, 0);
        var center = worldPoint + Vector3.up * overlapHalfExtents.y;
        var hits = Physics.OverlapBox(center, overlapHalfExtents, rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.GetComponentInParent<ResourceNode>() != null) return false;
            if (h.GetComponentInParent<DungeonCore>() != null) return false;
        }
        return true;
    }

    // ── 배치 ───────────────────────────────────────────────────────────────────

    void TryPlaceGrid(Vector2Int tile)
    {
        var footprint = GetFootprint(tile);
        if (!IsBuildableGrid(footprint)) return;

        var prefab = ResolvePrefab();
        if (prefab == null) return;

        var worldPos = MapManager.Instance.TileToWorld(tile);
        var go = Instantiate(prefab, worldPos, Quaternion.Euler(0, _gridYaw, 0));
        InitNode(go);
        _previewShown = false;
    }

    void TryPlaceFree(Vector3 worldPoint, float yaw)
    {
        var prefab = ResolvePrefab();
        if (prefab == null) return;

        var go = Instantiate(prefab, worldPoint, Quaternion.Euler(0, yaw, 0));
        InitNode(go);
        _previewShown = false;
    }

    GameObject ResolvePrefab()
    {
        var data = SelectedData;
        return (data != null && data.Prefab != null) ? data.Prefab : fallbackPrefab;
    }

    void InitNode(GameObject go)
    {
        var data = SelectedData;
        var node = go.GetComponent<ResourceNode>();
        if (node != null && data != null) node.Init(data);
    }

    List<Vector2Int> GetFootprint(Vector2Int center)
    {
        _footprint[0] = center;
        return _footprint;
    }
}
