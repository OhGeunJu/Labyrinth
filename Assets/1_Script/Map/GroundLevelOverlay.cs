using System.Collections.Generic;
using UnityEngine;

// **땅 고르기로 찍은 덩어리의 테두리를 그린다**(2026-09-24 · 09-25에 1m 칸에서 2m 덩어리로 — `GroundLevel`).
//
// 채굴 표시는 `RockRenderer`가 바위 메시의 두 번째 서브메시로 그린다 — **땅에는 얹을 메시가
// 없다.** 그래서 `FarmZoneOverlay`와 같은 방식으로 짓는다: 칸마다 네 변을 보고 **이웃이 같이
// 찍히지 않은 변만** 낸다. 둘레를 따라 도는 알고리즘이 필요 없고 구멍 뚫린 구역도 저절로 맞다.
//
// ── 색이 방향을 말한다 ─────────────────────────────────────────────────────
//
// **목표 높이가 화면에 안 적힌다**(창을 안 띄우는 것이 그 설계다 — `LevelDesignator`). 그러면
// 적어도 *깎을 것인지 메울 것인지*는 보여야 한다: 안 보이면 잘못 찍은 것을 **폰이 삽질을
// 끝낸 뒤에야** 안다. 그래서 **내리기는 노랑 · 올리기는 하늘빛**이고, 칸마다 따로 낸다
// (한 구역 안에서 둘이 섞이는 것이 흔하다 — 그게 *고른다*는 말의 뜻이다).
//
// **채굴의 주황과 겹치지 않는다** — 같은 색이면 *"여기를 판다"*와 *"여기를 고른다"*가 화면에서
// 한 물건이 된다.
[DisallowMultipleComponent]
public class GroundLevelOverlay : MonoBehaviour
{
    // 지면에서 띄우는 높이. 깊이 판정을 껐으므로 가려지진 않지만, **0이면 지면과 정확히 같은
    // 평면이라** 비스듬히 볼 때 선이 끊겨 보인다.
    const float Lift = 0.06f;

    static readonly Color Cut  = new(1f,   0.92f, 0.35f);   // 깎는다
    static readonly Color Fill = new(0.45f, 0.8f,  1f);     // 메운다

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SelfStanding.Create<GroundLevelOverlay>("GroundLevelOverlay");
    }

    MeshRenderer _renderer;
    Mesh         _mesh;
    Material     _mat;

    int _built = -1;

    readonly List<Vector3> _verts = new();
    readonly List<Color>   _cols  = new();
    readonly List<int>     _idx   = new();

    static readonly Vector2Int[] Dirs =
        { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

    void Awake()
    {
        var filter = gameObject.AddComponent<MeshFilter>();
        _renderer  = gameObject.AddComponent<MeshRenderer>();

        _mesh = new Mesh { name = "GroundLevelOutline" };
        _mesh.MarkDynamic();
        filter.sharedMesh = _mesh;

        // **`OverlayMaterial`을 쓴다**(건설 격자·영역 원·밭 구역과 한 벌) — 깊이 판정을 꺼서
        // 바닥을 깔아도 그 아래로 안 들어간다.
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
        // **바뀌었을 때만 다시 짓는다** — 찍힌 칸이 수백이면 매 프레임 그만큼이 붙는다.
        if (_built == GroundLevel.Version) return;
        _built = GroundLevel.Version;
        Build();
    }

    // **찍은 2m 덩어리의 둘레를 그린다**(`GroundLevel.MarkedBlocks`). 선은 **각 귀퉁이의 목표 높이**를
    // 잇는다: 그래야 *어디까지 오르내릴 것인가*가 보인다. 선 안이 곧 새 높이이고 둘레 2m가 비탈이다.
    // **건물에 닿아 제자리인 귀퉁이는 지금 높이에** 그린다 — 그 덩어리가 건물 쪽으로 기우는 것이
    // 선에서 그대로 보여야 한다.
    void Build()
    {
        _verts.Clear(); _cols.Clear(); _idx.Clear();

        _tiles.Clear();
        // **할 일이 남은 덩어리만** — 이미 그 높이인 덩어리까지 그리면 평탄화의 시작점처럼
        // 아무것도 안 바뀌는 자리에 선이 남는다(`BlockNeedsWork`).
        foreach (var blk in GroundLevel.MarkedBlocks)
            if (GroundLevel.BlockNeedsWork(blk)) _tiles.Add(blk);

        float s = TerrainGrid.VertexSpacing;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var blk in _tiles)
        {
            // 귀퉁이 넷의 자리(목표 높이).
            Vector3 P(int dx, int dz)
            {
                var v = new Vector2Int(blk.x + dx, blk.y + dz);
                float y = GroundLevel.TryTarget(v, out float t) ? t : GroundLevel.Height(v);
                return new Vector3(v.x * s, y + Lift, v.y * s);
            }
            Vector3 p00 = P(0, 0), p10 = P(1, 0), p01 = P(0, 1), p11 = P(1, 1);

            // 색은 덩어리 전체의 방향 — 귀퉁이 넷의 합이 양수면 내린다.
            float sum = 0f;
            foreach (var v in GroundLevel.CornersOf(blk))
                if (GroundLevel.TryTarget(v, out float t)) sum += GroundLevel.SignedRemaining(v, t);
            var col = sum >= 0f ? Cut : Fill;

            for (int d = 0; d < Dirs.Length; d++)
            {
                if (_tiles.Contains(blk + Dirs[d])) continue;   // 안쪽 변 — 안 그린다

                Vector3 a, b;
                if      (Dirs[d] == Vector2Int.left)  { a = p00; b = p01; }
                else if (Dirs[d] == Vector2Int.right) { a = p10; b = p11; }
                else if (Dirs[d] == Vector2Int.up)    { a = p01; b = p11; }
                else                                  { a = p00; b = p10; }

                Edge(a, b, col);

                minX = Mathf.Min(minX, Mathf.Min(a.x, b.x)); maxX = Mathf.Max(maxX, Mathf.Max(a.x, b.x));
                minZ = Mathf.Min(minZ, Mathf.Min(a.z, b.z)); maxZ = Mathf.Max(maxZ, Mathf.Max(a.z, b.z));
                minY = Mathf.Min(minY, Mathf.Min(a.y, b.y)); maxY = Mathf.Max(maxY, Mathf.Max(a.y, b.y));
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

        // 선은 얇아 자동 경계가 카메라 밖으로 판정되면 통째로 사라진다 — 직접 잡는다.
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

    readonly HashSet<Vector2Int> _tiles = new();
}
