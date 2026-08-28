using System.Collections.Generic;
using UnityEngine;

// 논리 그리드의 시각 표현.
// - Preview 오버레이: 건설 배치에서 `ShowPreview/HidePreview`로 요청할 때만 — **빨강은 여기 몫이다.**
// - Blocked 오버레이: 통행 불가 타일 표시. **기본은 꺼져 있다**(아래 참조).
public class GridRenderer : MonoBehaviour
{
    [Header("Blocked 오버레이 (진단용)")]
    [Tooltip("통행 불가 타일에 빨간 판을 깐다. **기본 꺼짐** — 켜면 타일을 점유하는 건물마다 " +
             "바닥이 빨개져서, 다 지어 놓은 건물이 잘못된 것처럼 보인다. " +
             "빨강은 '지금 여기 못 짓는다'는 뜻이어야 하고 그건 Preview가 말한다. " +
             "길찾기가 막힌 이유를 눈으로 볼 때만 잠깐 켤 것.")]
    [SerializeField] bool  showBlockedTiles = false;
    [SerializeField] Color blockedColor = new Color(1f, 0.2f, 0.2f, 0.35f);

    [Header("Preview 오버레이")]
    [SerializeField] Color previewOkColor  = new Color(0.2f, 1f, 0.3f, 0.4f);
    [SerializeField] Color previewBadColor = new Color(1f, 0.2f, 0.2f, 0.4f);

    readonly Dictionary<Vector2Int, GameObject> _blockOverlays   = new();
    readonly Dictionary<Vector2Int, GameObject> _previewOverlays = new();

    void Start()
    {
        if (!showBlockedTiles) return;   // 꺼져 있으면 아예 안 듣는다(만들 일도 없다)
        MapManager.OnTileChanged += OnTileChanged;
        // 핵 이동(OnZoneRecentered) 시에도 점유는 절대좌표로 유지되므로 오버레이를 지우지 않는다.
    }

    void OnDestroy()
    {
        MapManager.OnTileChanged -= OnTileChanged;
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
            // 표면 높이 기준 — 바닥이 깔린 타일은 슬래브 윗면 위에 떠야 한다(밑에 깔리지 않게).
            float surfaceY = MapManager.Instance.SurfaceHeight(pos);
            _blockOverlays[pos] = SpawnOverlay(pos, surfaceY + 0.05f, blockedColor, $"Blocked_{pos.x}_{pos.y}");
        }
        else
        {
            if (!_blockOverlays.TryGetValue(pos, out var obj)) return;
            Destroy(obj);
            _blockOverlays.Remove(pos);
        }
    }

    // ── Preview API ────────────────────────────────────────────────────────────

    // footprint 타일들을 건설 가능 여부에 따라 초록/빨강으로 표시.
    //
    // **`atY`는 평탄 기단이 쓴다** — 기단은 지면이 아니라 정해진 한 높이에 깔리므로, 표시를
    // 칸마다의 지면에 그리면 **초록 칸이 기단 고스트 밑에 파묻혀 안 보인다**(어디까지
    // 깔리는지가 화면에서 사라진다). 비우면 지금까지처럼 칸마다 표면을 따른다.
    public void ShowPreview(List<Vector2Int> footprint, bool buildable, float? atY = null) =>
        ShowPreview(footprint, null, buildable, atY);

    // **칸마다 다른 판정으로 그린다** — 줄·면 긋기가 이것을 쓴다.
    //
    // 한 칸이 막혔다고 전체를 빨갛게 칠하면 **화면은 "안 된다"인데 실제로는 지어진다**:
    // 긋기는 못 짓는 칸만 건너뛰고 나머지를 놓기 때문이다(그게 맞다 — 스무 칸을 그었는데
    // 한 칸 막혔다고 전부 취소하면 어디가 막혔는지도 모른 채 처음부터 다시 그어야 한다).
    // 보이는 것과 일어나는 일이 어긋나면 **빨간 표시를 아무도 안 믿게 된다.**
    // `atYs`는 **칸마다 다른 높이**다(계단) — 하나로는 못 적는다. 비우면 `atY`, 그것도
    // 비우면 칸마다의 표면을 따른다.
    public void ShowPreview(List<Vector2Int> footprint, List<bool> perTile, bool fallback,
                            float? atY = null, List<float> atYs = null)
    {
        HidePreview();

        for (int i = 0; i < footprint.Count; i++)
        {
            var pos = footprint[i];
            if (!MapManager.Instance.IsInBounds(pos)) continue;

            bool ok = perTile != null && i < perTile.Count ? perTile[i] : fallback;
            float surfaceY = atYs != null && i < atYs.Count ? atYs[i]
                           : atY ?? MapManager.Instance.SurfaceHeight(pos);
            _previewOverlays[pos] = SpawnOverlay(pos, surfaceY + 0.07f,
                ok ? previewOkColor : previewBadColor, $"Preview_{pos.x}_{pos.y}");
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
