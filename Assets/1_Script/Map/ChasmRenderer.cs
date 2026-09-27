using System.Collections.Generic;
using UnityEngine;

// **절벽의 벽과 안개를 그리고, 자연 다리를 세운다**(2026-09-26 — 판정은 `Chasm`, 저작은 `ChasmData`).
//
// ── 벽 ──────────────────────────────────────────────────────────────────────
//
// 지형은 절벽 덩어리에 **구멍**이 뚫려 있다(`Chasm.ApplyHoles`) — 그 구멍 가장자리를 따라 **수직 면**을
// 가장자리 윗면부터 절벽 바닥(`Chasm.BottomY`)까지 세운다. **드러난 변에만** 세운다(`RockRenderer`와 같은
// 규칙 — 절벽 덩어리끼리 맞닿은 변은 안 보인다). 윗변은 **그 자리 정점의 높이**를 따른다: 가장자리 정점은
// 땅 고르기가 못 움직이므로(`Chasm.IsRimVertex`) 벽과 땅이 어긋날 일이 없다.
//
// ── 안개 ────────────────────────────────────────────────────────────────────
//
// **밑이 안 보이게 한다**(사용자: *"그 사이를 안개로 매꿔서 밑에를 볼 수 없게"*). 셰이더를 새로 쓰지 않고
// **반투명 판을 깊이마다 겹친다** — 내려갈수록 판이 쌓여 벽이 안개에 녹아든다. 맨 밑은 불투명 판이다.
// 새 셰이더는 빌드에서 잘릴 길이 하나 더 생기는 것이라(`RuntimeShaders`) 있는 URP 재질로 푼다.
// **전장의 안개와 다른 물건이다** — 이건 지형이 원래 그렇게 생긴 것이라 가 본 곳이어도 늘 가려져 있다.
//
// ── 재질은 `Resources/World/`의 에셋이다 ────────────────────────────────────
//
// `World → J. Create Bridges`가 만든다(`ChasmWall` · `ChasmFog` · `ChasmFloor`). 에셋이라 빌드에 들어간다.
// 없으면 URP 재질을 만들어 쓴다(그래도 절벽이 통째로 안 보이지는 않는다).
//
// ── 청크 단위로 짓는다 ──────────────────────────────────────────────────────
//
// 지형이 선 청크만 그린다(`TerrainStreamer.ChunkLoaded`) — 보이지 않는 곳의 벽은 짓지 않는다.
public class ChasmRenderer : MonoBehaviour
{
    const int FogLayers = 6;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!Chasm.Any) return;   // 절벽이 없는 판에는 아무것도 안 세운다
        SelfStanding.Create<ChasmRenderer>("ChasmRenderer");
    }

    readonly Dictionary<Vector2Int, GameObject> _chunks = new();
    Material _wall, _fog, _floor;
    int _builtVersion = -1;

    void OnEnable()
    {
        TerrainStreamer.ChunkLoaded   += OnChunk;
        TerrainStreamer.ChunkUnloaded += OnGone;
    }

    void OnDisable()
    {
        TerrainStreamer.ChunkLoaded   -= OnChunk;
        TerrainStreamer.ChunkUnloaded -= OnGone;
    }

    void Start()
    {
        _wall  = Resources.Load<Material>("World/ChasmWall")  ?? Make(false, new Color(0.18f, 0.17f, 0.16f));
        _fog   = Resources.Load<Material>("World/ChasmFog")   ?? Make(true,  new Color(0.02f, 0.02f, 0.03f, 0.35f));
        _floor = Resources.Load<Material>("World/ChasmFloor") ?? Make(false, new Color(0.01f, 0.01f, 0.015f), unlit: true);

        foreach (var c in TerrainStreamer.LiveChunks) OnChunk(c);
        _builtVersion = Chasm.Version;

        SpawnNatural();
    }

    // 저작 도구가 판 도중에 칠을 바꿨으면 선 청크를 전부 다시 짓는다(보통은 판 밖에서 칠한다).
    void Update()
    {
        if (_builtVersion == Chasm.Version) return;
        _builtVersion = Chasm.Version;
        foreach (var c in new List<Vector2Int>(_chunks.Keys)) OnChunk(c);
    }

    static Material Make(bool transparent, Color c, bool unlit = false)
    {
        var sh = RuntimeShaders.Find(unlit || transparent ? RuntimeShaders.Unlit : RuntimeShaders.Lit);
        if (sh == null) return null;
        var m = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        if (transparent) FadeMaterial.Setup(m);
        FadeMaterial.SetColor(m, c);
        return m;
    }

    // ── 청크 ────────────────────────────────────────────────────────────────

    void OnGone(Vector2Int chunk)
    {
        if (_chunks.TryGetValue(chunk, out var go)) { if (go != null) Destroy(go); _chunks.Remove(chunk); }
    }

    readonly List<Vector3> _v = new();
    readonly List<Vector3> _n = new();
    readonly List<int>     _iWall = new(), _iFog = new(), _iFloor = new();

    void OnChunk(Vector2Int chunk)
    {
        OnGone(chunk);

        int s = TerrainGrid.VertexSpacing;
        var origin = TerrainGrid.OriginOf(chunk);
        int bx0 = Mathf.FloorToInt(origin.x / s), bz0 = Mathf.FloorToInt(origin.z / s);
        int per = TerrainGrid.ChunkSize / s;

        _v.Clear(); _n.Clear(); _iWall.Clear(); _iFog.Clear(); _iFloor.Clear();
        float bottom = Chasm.BottomY;

        for (int z = 0; z < per; z++)
        for (int x = 0; x < per; x++)
        {
            var b = new Vector2Int(bx0 + x, bz0 + z);
            if (!Chasm.IsChasmBlock(b)) continue;

            // 귀퉁이 넷 — 월드 자리와 윗면(정점 높이).
            Vector3 P(int dx, int dz)
            {
                var v = new Vector2Int(b.x + dx, b.y + dz);
                return new Vector3(v.x * s, TerrainHeight.VertexHeightGlobal(v.x, v.y) + 0.02f, v.y * s);
            }
            Vector3 p00 = P(0, 0), p10 = P(1, 0), p01 = P(0, 1), p11 = P(1, 1);

            // 벽 — 드러난 변(이웃이 절벽이 아닌 변)에만. 법선은 절벽 안쪽을 본다.
            if (!Chasm.IsChasmBlock(b + Vector2Int.left))  Wall(p00, p01, Vector3.right,   bottom);
            if (!Chasm.IsChasmBlock(b + Vector2Int.right)) Wall(p11, p10, Vector3.left,    bottom);
            if (!Chasm.IsChasmBlock(b + Vector2Int.down))  Wall(p10, p00, Vector3.forward, bottom);
            if (!Chasm.IsChasmBlock(b + Vector2Int.up))    Wall(p01, p11, Vector3.back,    bottom);

            // 안개 — 이 덩어리에서 가장 낮은 가장자리보다 `FogStart` 아래부터 바닥까지 고르게.
            float top = Mathf.Min(Mathf.Min(p00.y, p10.y), Mathf.Min(p01.y, p11.y)) - Chasm.FogStart;
            float x0 = b.x * s, x1 = x0 + s, z0 = b.y * s, z1 = z0 + s;
            if (top > bottom)
                for (int k = 0; k < FogLayers; k++)
                    Quad(x0, x1, z0, z1, Mathf.Lerp(top, bottom, k / (float)FogLayers), _iFog);
            Quad(x0, x1, z0, z1, bottom + 0.05f, _iFloor);
        }

        if (_v.Count == 0) return;

        var go = new GameObject($"Chasm_{TerrainGrid.KeyOf(chunk)}") { hideFlags = SelfStanding.RuntimeObject };
        go.transform.SetParent(transform, false);

        var mesh = new Mesh { name = go.name };
        if (_v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(_v);
        mesh.SetNormals(_n);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(_iWall, 0);
        mesh.SetTriangles(_iFog, 1);
        mesh.SetTriangles(_iFloor, 2);
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = new[] { _wall, _fog, _floor };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _chunks[chunk] = go;
    }

    // 윗변 a→b(월드), 아래로 bottom까지. **양면**으로 낸다 — 감김을 한쪽만 맞추면 반대쪽에서 볼 때 사라진다.
    void Wall(Vector3 a, Vector3 b, Vector3 normal, float bottom)
    {
        var a2 = new Vector3(a.x, bottom, a.z);
        var b2 = new Vector3(b.x, bottom, b.z);
        int i = _v.Count;
        _v.Add(a); _v.Add(b); _v.Add(b2); _v.Add(a2);
        for (int k = 0; k < 4; k++) _n.Add(normal);
        _iWall.Add(i); _iWall.Add(i + 1); _iWall.Add(i + 2);
        _iWall.Add(i); _iWall.Add(i + 2); _iWall.Add(i + 3);
        _iWall.Add(i); _iWall.Add(i + 2); _iWall.Add(i + 1);
        _iWall.Add(i); _iWall.Add(i + 3); _iWall.Add(i + 2);
    }

    void Quad(float x0, float x1, float z0, float z1, float y, List<int> into)
    {
        int i = _v.Count;
        _v.Add(new Vector3(x0, y, z0)); _v.Add(new Vector3(x0, y, z1));
        _v.Add(new Vector3(x1, y, z1)); _v.Add(new Vector3(x1, y, z0));
        for (int k = 0; k < 4; k++) _n.Add(Vector3.up);
        into.Add(i); into.Add(i + 1); into.Add(i + 2);
        into.Add(i); into.Add(i + 2); into.Add(i + 3);
    }

    // ── 자연 다리 ───────────────────────────────────────────────────────────
    //
    // **지을 수 있는 다리와 같은 프리팹을 세운다** — 자연 다리는 *처음부터 지어져 있는 다리*일 뿐이라 규칙이
    // 한 벌이다(`Bridge`가 윗면을 등록한다). 무너진 다리는 장식이라 모양만 세운다.
    // **판 전체에 한 번에 세운다** — 몇 개 안 되고, 먼 폰의 길찾기도 다리 윗면을 알아야 한다.
    void SpawnNatural()
    {
        var data = Chasm.Data;
        if (data == null) return;

        int made = 0;
        foreach (var spot in data.Bridges)
        {
            if (spot?.Kind == null || spot.Kind.Prefab == null) continue;
            var go = Instantiate(spot.Kind.Prefab, spot.Position, Quaternion.Euler(0f, spot.Yaw, 0f));
            go.name = $"Bridge_{spot.Kind.name}_Natural";
            if (go.TryGetComponent<Bridge>(out var br)) br.MarkNatural();
            made++;
        }

        foreach (var ruin in data.Ruins)
        {
            if (ruin?.Prefab == null) continue;
            var go = Instantiate(ruin.Prefab, ruin.Position, Quaternion.Euler(0f, ruin.Yaw, 0f));
            var map = MapManager.Instance;
            var p = go.transform.position;
            if (map != null) p.y = map.SampleGroundHeight(p);
            go.transform.position = p;
        }

        if (made > 0 || data.Ruins.Count > 0)
            Debug.Log($"[절벽] 덩어리 {System.Linq.Enumerable.Count(Chasm.AllBlocks)} · 자연 다리 {made} · 무너진 다리 {data.Ruins.Count}");
    }
}
