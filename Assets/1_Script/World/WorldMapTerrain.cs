using UnityEngine;
using UnityEngine.Rendering;

// 월드맵 지형 — **맵 전체가 이어진 메시 한 장**이다(칸별 타일이 아니다).
//
// 저작한 격자(`WorldMapData`의 바이옴 배치)를 재료로 삼되, 칸 경계에서 끊기지 않는다:
//   높이·색·기복 성격을 이웃 칸끼리 **이중선형으로 섞어** 샘플하므로,
//   숲에서 산지로 넘어가면 능선이 서서히 험해지고 경계선은 보이지 않는다.
//   → 여러 칸에 걸친 산맥이 자연스럽게 생긴다(칸마다 따로 만들면 절대 안 나오는 것).
//
// 지형을 직접 만들어 붙이고 싶으면 이 컴포넌트를 빼면 된다 —
// `WorldMapGrid`는 무엇이 됐든 **표면에 레이캐스트**할 뿐이라 그대로 동작한다.
[DefaultExecutionOrder(-46)]   // WorldMapGrid(-45)가 표면을 읽기 전에 지어져야 한다
public class WorldMapTerrain : MonoBehaviour
{
    [Header("해상도")]
    [Tooltip("칸 하나를 몇 조각으로 나눠 정점을 만들지. 클수록 곱지만 무겁다.")]
    [SerializeField, Range(1, 12)] int subdivisions = 4;

    [Header("모양")]
    [Tooltip("기복의 기본 주기(칸 단위). 작을수록 넓고 완만한 굴곡이 나온다.")]
    [SerializeField] float baseFrequency = 0.35f;
    [SerializeField, Range(1, 5)] int octaves = 3;
    [Tooltip("같은 값이면 항상 같은 지형이 나온다. 바꾸면 배치는 그대로 두고 능선만 다시 뽑힌다.")]
    [SerializeField] float seed = 1234f;

    [Header("색")]
    [Tooltip("바이옴 색을 칸 해상도 텍스처로 굽고 Bilinear로 늘린다 → 경계가 번져 지도처럼 보인다.")]
    [SerializeField] bool paintBiomeColors = true;

    GameObject _go;
    Texture2D  _tex;
    Material   _mat;
    float      _spacing = 4f;


    // `WorldMapGrid`가 자기 칸 크기를 넘겨준다 — 지형과 격자가 같은 축척을 써야 한다.
    public void Build(float nodeSpacing)
    {
        var wm = WorldMap.Instance;
        if (wm == null) { Debug.LogWarning("[WorldMapTerrain] WorldMap.Instance 없음"); return; }

        _spacing = nodeSpacing;
        Clear();

        int vx = wm.Width  * subdivisions + 1;
        int vz = wm.Height * subdivisions + 1;

        // 면마다 정점을 분리해 **플랫 셰이딩**(각진 로우폴리) — 텍스처 없이 조명만으로 형태가 읽힌다.
        var verts = new System.Collections.Generic.List<Vector3>(vx * vz * 6);
        var uvs   = new System.Collections.Generic.List<Vector2>(vx * vz * 6);
        var tris  = new System.Collections.Generic.List<int>(vx * vz * 6);

        // 격자점 높이를 먼저 뽑아두고 삼각형을 만든다(같은 점을 여러 번 계산하지 않게).
        var h = new float[vx, vz];
        for (int j = 0; j < vz; j++)
        for (int i = 0; i < vx; i++)
            h[i, j] = HeightAt((float)i / subdivisions, (float)j / subdivisions);

        for (int j = 0; j < vz - 1; j++)
        for (int i = 0; i < vx - 1; i++)
        {
            var p00 = Point(i,     j,     h[i,     j],     wm);
            var p10 = Point(i + 1, j,     h[i + 1, j],     wm);
            var p01 = Point(i,     j + 1, h[i,     j + 1], wm);
            var p11 = Point(i + 1, j + 1, h[i + 1, j + 1], wm);

            AddTri(verts, uvs, tris, p00, p01, p10);
            AddTri(verts, uvs, tris, p10, p01, p11);
        }

        var mesh = new Mesh { name = "WorldTerrain" };
        mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        _go = new GameObject("Terrain");
        _go.transform.SetParent(transform, false);
        _go.transform.localPosition = Vector3.zero;
        _go.AddComponent<MeshFilter>().sharedMesh = mesh;
        _go.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(wm);
        _go.AddComponent<MeshCollider>().sharedMesh = mesh;   // 표면 샘플 + 클릭 판정
    }

    public void Clear()
    {
        if (_go != null) Destroy(_go);
        _go = null;
    }

    // ── 높이 ─────────────────────────────────────────────────────────────────

    // 칸 단위 실수 좌표(u,v)의 표고. 바이옴 성격을 이웃끼리 섞은 뒤 노이즈를 태운다.
    // **칸 경계에서 값이 튀지 않는 게 핵심** — 그래야 지형이 한 장으로 이어진다.
    float HeightAt(float u, float v)
    {
        BlendProfile(u, v, out var p);
        float n = BiomeTerrain.FBm(u * baseFrequency * p.Roughness,
                                   v * baseFrequency * p.Roughness, octaves, seed);
        return p.Base + n * p.Amplitude;
    }

    // 노드 중심(i+0.5)에 정의된 바이옴 성격을 이중선형으로 섞는다.
    void BlendProfile(float u, float v, out BiomeTerrainProfile p)
    {
        var wm = WorldMap.Instance;
        float fx = Mathf.Clamp(u - 0.5f, 0f, wm.Width  - 1f);
        float fy = Mathf.Clamp(v - 0.5f, 0f, wm.Height - 1f);

        int x0 = Mathf.FloorToInt(fx), x1 = Mathf.Min(x0 + 1, wm.Width  - 1);
        int y0 = Mathf.FloorToInt(fy), y1 = Mathf.Min(y0 + 1, wm.Height - 1);
        float tx = fx - x0, ty = fy - y0;

        var a = BiomeTerrain.Of(wm.GetTile(new Vector2Int(x0, y0)).Biome);
        var b = BiomeTerrain.Of(wm.GetTile(new Vector2Int(x1, y0)).Biome);
        var c = BiomeTerrain.Of(wm.GetTile(new Vector2Int(x0, y1)).Biome);
        var d = BiomeTerrain.Of(wm.GetTile(new Vector2Int(x1, y1)).Biome);

        p = new BiomeTerrainProfile
        {
            Base      = Bi(a.Base,      b.Base,      c.Base,      d.Base,      tx, ty),
            Amplitude = Bi(a.Amplitude, b.Amplitude, c.Amplitude, d.Amplitude, tx, ty),
            Roughness = Mathf.Max(0.05f, Bi(a.Roughness, b.Roughness, c.Roughness, d.Roughness, tx, ty)),
        };
    }

    static float Bi(float a, float b, float c, float d, float tx, float ty) =>
        Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);

    // ── 메시 조립 ────────────────────────────────────────────────────────────

    // 정점 격자 인덱스 → 로컬 위치 + UV(텍셀 중심이 노드 중심에 오도록 정렬).
    (Vector3 pos, Vector2 uv) Point(int i, int j, float y, WorldMap wm)
    {
        float u = (float)i / subdivisions;
        float v = (float)j / subdivisions;
        return (new Vector3(u * _spacing, y, v * _spacing),
                new Vector2(u / wm.Width, v / wm.Height));
    }

    static void AddTri(System.Collections.Generic.List<Vector3> verts,
                       System.Collections.Generic.List<Vector2> uvs,
                       System.Collections.Generic.List<int> tris,
                       (Vector3 pos, Vector2 uv) a,
                       (Vector3 pos, Vector2 uv) b,
                       (Vector3 pos, Vector2 uv) c)
    {
        int i = verts.Count;
        verts.Add(a.pos); verts.Add(b.pos); verts.Add(c.pos);
        uvs.Add(a.uv);    uvs.Add(b.uv);    uvs.Add(c.uv);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }

    Material MakeMaterial(WorldMap wm)
    {
        var sh = RuntimeShaders.Find(RuntimeShaders.Lit, RuntimeShaders.Standard,
                                     RuntimeShaders.Unlit);
        _mat = new Material(sh) { name = "WorldTerrainMat" };
        if (_mat.HasProperty("_Smoothness")) _mat.SetFloat("_Smoothness", 0f);
        if (_mat.HasProperty("_Glossiness")) _mat.SetFloat("_Glossiness", 0f);

        if (!paintBiomeColors) return _mat;

        _tex = new Texture2D(wm.Width, wm.Height, TextureFormat.RGBA32, false)
        {
            name       = "WorldBiomes",
            filterMode = FilterMode.Bilinear,   // 칸 경계가 번진다 → 바둑판이 아니라 지도
            wrapMode   = TextureWrapMode.Clamp,
        };
        for (int x = 0; x < wm.Width;  x++)
        for (int y = 0; y < wm.Height; y++)
            _tex.SetPixel(x, y, wm.GetTile(new Vector2Int(x, y)).Biome.Color());
        _tex.Apply();

        if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", _tex);
        _mat.mainTexture = _tex;
        return _mat;
    }

    // 내용물만 바뀐 칸의 색 갱신 — 지형 형태는 그대로 둔다(고도는 런타임에 안 바뀐다).
    public void RefreshColor(Vector2Int node)
    {
        if (_tex == null || WorldMap.Instance == null || !WorldMap.Instance.InBounds(node)) return;
        _tex.SetPixel(node.x, node.y, WorldMap.Instance.GetTile(node).Biome.Color());
        _tex.Apply();
    }
}
