using System.Collections.Generic;
using UnityEngine;

// **구역의 테두리를 그린다**(2026-09-02, 사용자 결정 — *"테두리에 찐한 색이 뜨는 거,
// 폰 클릭했을 때처럼"*).
//
// 처음에는 밭 흙 색에 구역 색을 **곱했는데** 그건 애매했다. 어두운 굴에서 옅은 곱셈은
// *"저 밭이 좀 누런가?"* 정도로만 보이고, 무엇보다 **어디서 끊기는지가 안 보인다** —
// 구역을 표시하는 목적의 절반이 *경계*인데 면을 칠하면 경계가 제일 안 보이는 것이 된다.
//
// **테두리는 반대다.** 안쪽 변은 안 그리고 **바깥 변만** 그리므로, 이어 붙은 밭은 선이
// 저절로 하나로 이어지고 끊긴 곳은 선이 갈린다 — 림월드의 구역이 그 모양이다.
//
// ── 어떻게 짓나 ────────────────────────────────────────────────────────────
//
// 칸마다 네 변을 보고 **이웃이 같은 구역이 아닌 변만** 낸다. 그게 전부다 —
// 둘레를 따라 도는 알고리즘이 필요 없고, 구멍이 뚫린 구역도 저절로 맞는다.
//
// **`OverlayMaterial`을 쓴다**(건설 격자·영역 원과 한 벌) — 깊이 판정을 꺼서 바닥을 깔아도
// 그 아래로 안 들어간다. 선이 얇아 **경계를 손으로 잡는다**(자동 경계가 카메라 밖으로
// 판정되면 통째로 사라진다 — 격자가 먼저 겪었다).
[DisallowMultipleComponent]
public class FarmZoneOverlay : MonoBehaviour
{
    // 지면에서 띄우는 높이. 깊이 판정을 껐으므로 가려지지는 않지만, **0으로 두면 바닥
    // 메시와 정확히 같은 평면이라** 비스듬히 볼 때 선이 끊겨 보인다.
    const float Lift = 0.05f;

    static FarmZoneOverlay _instance;

    // 밭이 하나라도 서면 스스로 선다 — **씬 배선이 없다**(지정기들과 같은 자리).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        _instance = SelfStanding.Create<FarmZoneOverlay>("FarmZoneOverlay");
    }

    MeshRenderer _renderer;
    Mesh         _mesh;
    Material     _mat;

    int _builtVersion = -1;
    int _builtPreview = -1;

    readonly List<Vector3> _verts = new();
    readonly List<Color>   _cols  = new();
    readonly List<int>     _idx   = new();

    static readonly Vector2Int[] Dirs =
        { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

    void Awake()
    {
        var filter = gameObject.AddComponent<MeshFilter>();
        _renderer  = gameObject.AddComponent<MeshRenderer>();

        _mesh = new Mesh { name = "FarmZoneOutline" };
        _mesh.MarkDynamic();
        filter.sharedMesh = _mesh;

        _mat = OverlayMaterial.Create();
        _renderer.sharedMaterial    = _mat;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows    = false;
    }

    void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_mat  != null) Destroy(_mat);
    }

    void LateUpdate()
    {
        int preview = FarmZoneDesignator.PreviewVersion;

        // **바뀌었을 때만 다시 짓는다** — 정점을 매 프레임 쌓으면 밭 수만큼이 프레임에 붙는다.
        if (_builtVersion == FarmZones.Version && _builtPreview == preview) return;
        _builtVersion = FarmZones.Version;
        _builtPreview = preview;

        Build();
    }

    void Build()
    {
        _verts.Clear(); _cols.Clear(); _idx.Clear();

        var map = MapManager.Instance;
        var seen = new HashSet<FarmPlot>();

        float minY = float.MaxValue, maxY = float.MinValue;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var plot in FarmPlot.All)
        {
            if (plot == null || !seen.Add(plot)) continue;

            var zone = FarmZones.Of(plot);
            var col  = Strong(FarmZones.ColorOf(plot));

            // 이 구역의 칸을 모아 둔다 — 변마다 *이웃이 같은 구역인가*를 물어야 한다.
            var tiles = new HashSet<Vector2Int>();
            foreach (var p in zone) if (p != null) { tiles.Add(p.TileCoord); seen.Add(p); }

            foreach (var p in zone)
            {
                if (p == null) continue;
                var t = p.TileCoord;
                float y = (map != null ? map.SurfaceHeight(t) : p.transform.position.y) + Lift;

                // 칸의 네 모서리(월드). 격자 위상을 얹는다 — 안 얹으면 선이 반 칸 어긋난다.
                var c  = map != null ? map.TileToWorld(t)
                                     : new Vector3(t.x + 0.5f, 0f, t.y + 0.5f);
                float xa = c.x - 0.5f, xb = c.x + 0.5f;
                float za = c.z - 0.5f, zb = c.z + 0.5f;

                for (int d = 0; d < Dirs.Length; d++)
                {
                    if (tiles.Contains(t + Dirs[d])) continue;   // 안쪽 변 — 안 그린다

                    Vector3 a, b;
                    if      (Dirs[d] == Vector2Int.left)  { a = new(xa, y, za); b = new(xa, y, zb); }
                    else if (Dirs[d] == Vector2Int.right) { a = new(xb, y, za); b = new(xb, y, zb); }
                    else if (Dirs[d] == Vector2Int.up)    { a = new(xa, y, zb); b = new(xb, y, zb); }
                    else                                  { a = new(xa, y, za); b = new(xb, y, za); }

                    Edge(a, b, col);

                    if (a.x < minX) minX = a.x; if (b.x > maxX) maxX = b.x;
                    if (a.z < minZ) minZ = a.z; if (b.z > maxZ) maxZ = b.z;
                    if (y < minY) minY = y;     if (y > maxY) maxY = y;
                }
            }
        }

        // **끌고 있는 것은 흰 선으로 위에 얹는다** — 아직 구역이 아니라 *묶으려는 것*이라
        // 구역 색을 쓰면 이미 그렇게 된 줄로 보인다. 흰색은 어느 구역 색과도 안 겹친다.
        var prev = FarmZoneDesignator.Preview;
        if (prev.Count > 0)
        {
            var tiles = new HashSet<Vector2Int>();
            foreach (var p in prev) if (p != null) tiles.Add(p.TileCoord);

            foreach (var p in prev)
            {
                if (p == null) continue;
                var t = p.TileCoord;
                float y = (map != null ? map.SurfaceHeight(t) : p.transform.position.y) + Lift * 2f;
                var c  = map != null ? map.TileToWorld(t)
                                     : new Vector3(t.x + 0.5f, 0f, t.y + 0.5f);
                float xa = c.x - 0.5f, xb = c.x + 0.5f;
                float za = c.z - 0.5f, zb = c.z + 0.5f;

                for (int d = 0; d < Dirs.Length; d++)
                {
                    if (tiles.Contains(t + Dirs[d])) continue;

                    Vector3 a, b;
                    if      (Dirs[d] == Vector2Int.left)  { a = new(xa, y, za); b = new(xa, y, zb); }
                    else if (Dirs[d] == Vector2Int.right) { a = new(xb, y, za); b = new(xb, y, zb); }
                    else if (Dirs[d] == Vector2Int.up)    { a = new(xa, y, zb); b = new(xb, y, zb); }
                    else                                  { a = new(xa, y, za); b = new(xb, y, za); }

                    Edge(a, b, Color.white);

                    if (a.x < minX) minX = a.x; if (b.x > maxX) maxX = b.x;
                    if (a.z < minZ) minZ = a.z; if (b.z > maxZ) maxZ = b.z;
                    if (y < minY) minY = y;     if (y > maxY) maxY = y;
                }
            }
        }

        _mesh.Clear();
        if (_verts.Count == 0) { _renderer.enabled = false; return; }

        _mesh.indexFormat = _verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        _mesh.SetVertices(_verts);
        _mesh.SetColors(_cols);
        _mesh.SetIndices(_idx, MeshTopology.Lines, 0);

        // 선은 얇아서 자동 경계가 카메라 밖으로 판정되면 통째로 사라진다 — 직접 잡는다.
        _mesh.bounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f),
            new Vector3(Mathf.Max(maxX - minX, 1f), Mathf.Max(maxY - minY, 1f), Mathf.Max(maxZ - minZ, 1f)));

        _renderer.enabled = true;
    }

    void Edge(Vector3 a, Vector3 b, Color c)
    {
        _idx.Add(_verts.Count); _verts.Add(a); _cols.Add(c);
        _idx.Add(_verts.Count); _verts.Add(b); _cols.Add(c);
    }

    // **진하게, 다만 살짝 하얗게**(사용자). `FarmZones`가 드는 색은 밭 색에 곱하려고 1에
    // 가깝게 눌러 둔 값이라 선으로 쓰면 흐리다 — 여기서 되돌려 진하게 쓰고, 흰색을 조금
    // 섞어 어두운 굴에서도 뜨게 한다.
    static Color Strong(Color tint)
    {
        Color.RGBToHSV(tint, out float h, out _, out _);
        var c = Color.HSVToRGB(h, 0.8f, 1f);
        return Color.Lerp(c, Color.white, 0.25f);
    }
}
