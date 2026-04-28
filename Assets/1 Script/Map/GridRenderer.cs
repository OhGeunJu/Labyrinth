using System.Collections.Generic;
using UnityEngine;

// 논리 그리드의 시각 표현.
// - Impassable 오버레이: SetTile로 명시적으로 막힌 타일만 표시 (경사 자동 판정 타일 제외)
// - Preview 오버레이: 건설 배치 등에서 ShowPreview/HidePreview로 요청 시에만 표시
public class GridRenderer : MonoBehaviour
{
    [Header("Blocked 오버레이")]
    [SerializeField] Color blockedColor = new Color(1f, 0.2f, 0.2f, 0.35f);

    [Header("Preview 오버레이")]
    [SerializeField] Color previewOkColor  = new Color(0.2f, 1f, 0.3f, 0.4f);
    [SerializeField] Color previewBadColor = new Color(1f, 0.2f, 0.2f, 0.4f);

    readonly Dictionary<Vector2Int, GameObject> _blockOverlays   = new();
    readonly Dictionary<Vector2Int, GameObject> _previewOverlays = new();

    void Start()
    {
        MapManager.OnTileChanged   += OnTileChanged;
        MapManager.OnZoneRecentered += ClearBlockOverlays;
    }

    void OnDestroy()
    {
        MapManager.OnTileChanged   -= OnTileChanged;
        MapManager.OnZoneRecentered -= ClearBlockOverlays;
        HidePreview();
        ClearBlockOverlays();
    }

    void OnTileChanged(Vector2Int pos) => RefreshBlockOverlay(pos);

    void ClearBlockOverlays()
    {
        foreach (var obj in _blockOverlays.Values)
            if (obj != null) Destroy(obj);
        _blockOverlays.Clear();
    }

    void RefreshBlockOverlay(Vector2Int pos)
    {
        var tile = MapManager.Instance.GetTile(pos);
        if (!tile.IsPassable)
        {
            if (_blockOverlays.ContainsKey(pos)) return;
            _blockOverlays[pos] = SpawnOverlay(pos, tile.Height + 0.05f, blockedColor, $"Blocked_{pos.x}_{pos.y}");
        }
        else
        {
            if (!_blockOverlays.TryGetValue(pos, out var obj)) return;
            Destroy(obj);
            _blockOverlays.Remove(pos);
        }
    }

    // ── Preview API ────────────────────────────────────────────────────────────

    // footprint 타일들을 건설 가능 여부에 따라 초록/빨강으로 표시
    public void ShowPreview(List<Vector2Int> footprint, bool buildable)
    {
        HidePreview();
        var color = buildable ? previewOkColor : previewBadColor;

        foreach (var pos in footprint)
        {
            if (!MapManager.Instance.IsInBounds(pos)) continue;
            var tile = MapManager.Instance.GetTile(pos);
            _previewOverlays[pos] = SpawnOverlay(pos, tile.Height + 0.07f, color, $"Preview_{pos.x}_{pos.y}");
        }
    }

    public void HidePreview()
    {
        foreach (var obj in _previewOverlays.Values)
            if (obj != null) Destroy(obj);
        _previewOverlays.Clear();
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────────

    GameObject SpawnOverlay(Vector2Int pos, float worldY, Color color, string objName)
    {
        var map = MapManager.Instance;
        var wp  = map.TileToWorld(pos);

        var obj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        obj.name = objName;
        obj.transform.SetParent(transform);
        obj.transform.position   = new Vector3(wp.x, worldY, wp.z);
        obj.transform.localScale = new Vector3(0.1f, 1f, 0.1f);
        Destroy(obj.GetComponent<Collider>());

        var mat = obj.GetComponent<Renderer>().material;
        mat.color = color;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;

        return obj;
    }
}
