using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildModeManager : MonoBehaviour
{
    public static BuildModeManager Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance._active;

    [Header("참조")]
    [SerializeField] GridOverlay gridOverlay;
    [SerializeField] GridRenderer gridRenderer;

    [Header("배치")]
    [SerializeField] GameObject fallbackPrefab;
    [SerializeField] ResourceNodeData[] placeableNodes;
    [SerializeField] LayerMask groundMask = ~0;

    bool _active;
    bool _previewShown;
    Vector2Int _hoveredTile;
    int _selectedIndex;
    readonly List<Vector2Int> _footprint = new() { Vector2Int.zero };
    Camera _cam;

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
    }

    void Update()
    {
        if (Keyboard.current.bKey.wasPressedThisFrame)
            SetActive(!_active);

        if (Keyboard.current.escapeKey.wasPressedThisFrame && _active)
            SetActive(false);

        if (_active)
        {
            HandleScroll();
            UpdatePreview();
        }
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
        GUI.Label(new Rect(10, 10, 300, 24), $"[건설 모드] {label}  ({_selectedIndex + 1}/{count})  스크롤로 변경");
    }

    void SetActive(bool active)
    {
        _active = active;
        gridOverlay.SetVisible(active);
        if (!active)
        {
            gridRenderer.HidePreview();
            _previewShown = false;
        }
    }

    void UpdatePreview()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (_previewShown) { gridRenderer.HidePreview(); _previewShown = false; }
            return;
        }

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
        {
            if (_previewShown) { gridRenderer.HidePreview(); _previewShown = false; }
            return;
        }

        var tile = MapManager.Instance.WorldToTile(hit.point);
        if (tile != _hoveredTile || !_previewShown)
        {
            _hoveredTile = tile;
            _previewShown = true;
            var fp = GetFootprint(tile);
            gridRenderer.ShowPreview(fp, IsBuildable(fp));
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryPlace(tile);
    }

    bool IsBuildable(List<Vector2Int> footprint)
    {
        if (!MapManager.Instance.IsBuildable(footprint)) return false;
        var core = DungeonCore.Instance;
        if (core != null)
            foreach (var t in footprint)
                if (!core.IsWithinRange(t)) return false;
        return true;
    }

    void TryPlace(Vector2Int tile)
    {
        var footprint = GetFootprint(tile);
        if (!IsBuildable(footprint)) return;

        var data = SelectedData;
        var prefab = (data != null && data.Prefab != null) ? data.Prefab : fallbackPrefab;
        if (prefab == null) return;

        var worldPos = MapManager.Instance.TileToWorld(tile);
        var go = Instantiate(prefab, worldPos, Quaternion.identity);
        var node = go.GetComponent<ResourceNode>();
        if (node != null && data != null)
            node.Init(data);

        _previewShown = false;
    }

    List<Vector2Int> GetFootprint(Vector2Int center)
    {
        _footprint[0] = center;
        return _footprint;
    }
}
