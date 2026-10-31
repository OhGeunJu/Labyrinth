using System.Collections.Generic;
using UnityEngine;

// **절벽의 벽과 안개를 그리고, 자연 다리를 세운다**(2026-09-26 — 판정은 `Chasm`, 저작은 `ChasmData`).
//
// ── 벽 ──────────────────────────────────────────────────────────────────────
//
// 지형은 절벽 덩어리에 **구멍**이 뚫려 있다(`Chasm.ApplyHoles`) — 그 구멍 가장자리를 따라 **수직 면**을
// 가장자리 윗면부터 절벽 바닥(`Chasm.BottomY`)까지 세운다. **드러난 변에만** 세운다(`RockRenderer`와 같은
// 규칙 — 절벽 덩어리끼리 맞닿은 변은 안 보인다). 윗변은 **그 자리 정점의 높이**를 따른다: 가장자리 정점은
// 땅 고르기가 못 움직이므로(`Chasm.IsRimVertex`) 벽과 땅이 어긋날 일이 없다. **벽은 네모난 변이 아니라 흔들린
// 선에서 내려가고, 그 위를 바위 턱이 덮는다**(2026-10-28, `Rim`).
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

            // 벽 — 드러난 변(이웃이 절벽이 아닌 변)에만. 법선은 절벽 안쪽을 본다. 턱(`Rim`)이 같이 선다.
            if (!Chasm.IsChasmBlock(b + Vector2Int.left))  Rim(new Vector2Int(b.x, b.y),         new Vector2Int(b.x, b.y + 1),     Vector3.right,   bottom);
            if (!Chasm.IsChasmBlock(b + Vector2Int.right)) Rim(new Vector2Int(b.x + 1, b.y + 1), new Vector2Int(b.x + 1, b.y),     Vector3.left,    bottom);
            if (!Chasm.IsChasmBlock(b + Vector2Int.down))  Rim(new Vector2Int(b.x + 1, b.y),     new Vector2Int(b.x, b.y),         Vector3.forward, bottom);
            if (!Chasm.IsChasmBlock(b + Vector2Int.up))    Rim(new Vector2Int(b.x, b.y + 1),     new Vector2Int(b.x + 1, b.y + 1), Vector3.back,    bottom);

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

    // ── 가장자리 턱 (2026-10-28) ────────────────────────────────────────────
    //
    // 사용자: *"낭떠러지 부분도 딱 네모낳게 잘라져 있어서 어색함"*. 지형 구멍은 2m 덩어리로만 뚫린다(유니티 지형의
    // 구멍 해상도가 정점 격자다) — 구멍 자체는 둥글게 못 만든다. 그래서 **가장자리를 바위 턱으로 덮고 그 선을
    // 흔든다.** 턱의 바깥선은 땅 위, 안쪽선은 구멍 위에 걸치므로 네모난 구멍 테두리가 턱 밑에 숨는다. 벽은
    // 안쪽선에서 내려간다.
    //
    //   · **두 선은 자리의 함수다**(`LipOut`·`LipIn`) — 같은 점을 어느 덩어리·청크가 그리든 같은 값이라 이음매가 없다.
    //   · **귀퉁이는 둘레 네 덩어리로 정한다**(`Corner`) — 구멍이 튀어나온 귀퉁이는 벽선을 구멍 쪽으로, 땅이
    //     튀어나온 귀퉁이는 턱의 바깥선을 땅 쪽으로 밀어 둥글린다. 계단처럼 꺾인 비스듬한 가장자리가 물결이 된다.
    //     **두 선은 제 쪽으로만 민다** — 바깥선이 구멍으로 넘어가면 귀퉁이에 구멍이 비치고, 벽선이 땅으로 넘어가면
    //     네모난 땅 귀퉁이가 턱 밖으로 드러난다.
    //   · **판정은 그대로다** — 걷는 것·막는 것은 덩어리(`Chasm.Blocks`)가 답한다. 턱은 그림이고 폭이 한 뼘이다.
    const int   RimSteps   = 4;       // 2m 변을 몇 조각으로
    const float CornerPull = 0.6f;    // 귀퉁이를 둥글리는 양(m)
    const float LipLift    = 0.06f;   // 땅 위로 띄운다(지형은 삼각형, 높이 함수는 쌍선형 — 그 차이만큼)

    static float LipOut(Vector2 p) => 0.25f + 0.65f * Mathf.PerlinNoise(p.x * 0.45f + 311.7f, p.y * 0.45f + 57.3f);
    static float LipIn(Vector2 p)  => 0.15f + 0.55f * Mathf.PerlinNoise(p.x * 0.45f + 902.1f, p.y * 0.45f + 448.9f);

    // 정점 v(정점 격자) 둘레의 땅 쪽 방향과 둥글리는 몫. 둘레 네 덩어리 중 땅인 것의 방향을 더한다 —
    // 곧은 변이면 변의 바깥 법선, 귀퉁이면 대각선이 나온다. 대각선으로 엇갈린 둘(안장)은 서로 지워져 0이다.
    static bool Corner(Vector2Int v, out Vector2 dir, out float pullOut, out float pullIn)
    {
        dir = Vector2.zero;
        int holes = 0;
        for (int dz = -1; dz <= 0; dz++)
        for (int dx = -1; dx <= 0; dx++)
        {
            var away = new Vector2(dx + 0.5f, dz + 0.5f);   // 정점에서 그 덩어리 한복판 쪽
            if (Chasm.IsChasmBlock(new Vector2Int(v.x + dx, v.y + dz))) holes++;
            else dir += away;
        }
        pullOut = holes == 3 ? CornerPull : 0f;   // 땅이 튀어나왔다 — 턱을 땅 쪽으로 넓힌다
        pullIn  = holes == 1 ? CornerPull : 0f;   // 구멍이 튀어나왔다 — 벽을 구멍 쪽으로 물린다
        if (dir.sqrMagnitude < 1e-4f) return false;   // 안장 — 변마다 제 법선을 쓴다
        dir.Normalize();
        return true;
    }

    // 변 a→b(정점 격자)의 턱과 벽. `inward`는 절벽 안쪽을 보는 법선.
    void Rim(Vector2Int a, Vector2Int b, Vector3 inward, float bottom)
    {
        int s = TerrainGrid.VertexSpacing;
        var edgeOut = new Vector2(-inward.x, -inward.z);

        int start = _v.Count;
        for (int k = 0; k <= RimSteps; k++)
        {
            float t = k / (float)RimSteps;
            var p = Vector2.Lerp(new Vector2(a.x * s, a.y * s), new Vector2(b.x * s, b.y * s), t);

            Vector2 dir = edgeOut;
            float pullOut = 0f, pullIn = 0f;
            if ((k == 0 || k == RimSteps) && Corner(k == 0 ? a : b, out var d, out pullOut, out pullIn)) dir = d;

            var outer = p + dir * (LipOut(p) + pullOut);
            var inner = p - dir * (LipIn(p)  + pullIn);
            float rimY = TerrainHeight.At(p.x, p.y) + LipLift;

            // 조각마다 넷 — 턱의 바깥 · 턱의 안쪽 · 벽의 위 · 벽의 아래. 안쪽선의 한 점을 턱과 벽이 **따로** 든다
            // (법선이 위와 옆으로 갈린다 — 한 점을 같이 쓰면 벽 윗부분이 위를 보는 빛을 받는다).
            var wallN = new Vector3(-dir.x, 0f, -dir.y);
            _v.Add(new Vector3(outer.x, TerrainHeight.At(outer.x, outer.y) + LipLift, outer.y)); _n.Add(Vector3.up);
            _v.Add(new Vector3(inner.x, rimY,   inner.y)); _n.Add(Vector3.up);
            _v.Add(new Vector3(inner.x, rimY,   inner.y)); _n.Add(wallN);
            _v.Add(new Vector3(inner.x, bottom, inner.y)); _n.Add(wallN);
        }

        // 조각마다 턱(윗면)과 벽. **양면**으로 낸다 — 감김을 한쪽만 맞추면 반대쪽에서 볼 때 사라진다.
        for (int k = 0; k < RimSteps; k++)
        {
            int o0 = start + k * 4, o1 = o0 + 4;
            Both(o0, o0 + 1, o1 + 1, o1);              // 턱: 바깥 → 안쪽
            Both(o0 + 2, o1 + 2, o1 + 3, o0 + 3);      // 벽: 안쪽선 → 바닥
        }
    }

    void Both(int a, int b, int c, int d)
    {
        _iWall.Add(a); _iWall.Add(b); _iWall.Add(c);
        _iWall.Add(a); _iWall.Add(c); _iWall.Add(d);
        _iWall.Add(a); _iWall.Add(c); _iWall.Add(b);
        _iWall.Add(a); _iWall.Add(d); _iWall.Add(c);
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
