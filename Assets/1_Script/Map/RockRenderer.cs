using System.Collections.Generic;
using UnityEngine;

// **바위를 그린다 — 칸 데이터에서 청크 메시로**(2026-08-29).
//
// `FloorRenderer`와 **같은 물건**이다: 칸마다 오브젝트를 세우지 않고, 16×16 청크 하나를
// 메시 한 장으로 굽는다. 바닥에서 이미 겪었다 — 20×20 마당 하나가 GameObject 800개였고,
// 바위는 그보다 훨씬 많다(파내기 전에는 **세계가 통째로 바위**다).
//
// ── 보이는 것만 굽는다 ─────────────────────────────────────────────────────
//
// 세계 전체가 바위라도 **그릴 것은 파낸 공간의 테두리뿐**이다. 바위 속의 바위는 어느
// 면에서도 안 보인다. 그래서:
//
//   · **윗면** — 위에서 내려다보는 화면이라 이건 늘 보인다.
//   · **옆면** — **뚫린 이웃 쪽만.** 바위끼리 맞닿은 면은 두 장이 겹쳐 z-파이팅이 나고,
//     무엇보다 안 보인다.
//
// 파내면 그 칸과 **맞닿은 넷**을 다시 굽는다(옆면이 새로 드러난다). 바닥이 쓰는 규칙 그대로다.
//
// ── 지정된 칸은 서브메시로 가른다 ──────────────────────────────────────────
//
// "캘 것"으로 찍은 칸은 다르게 보여야 한다. 칸마다 표식을 세우면 다시 오브젝트가 늘어나므로,
// **같은 메시의 두 번째 서브메시**로 굽고 머티리얼만 달리 준다 — 지정이 바뀌면 그 청크만
// 다시 구우면 된다.
[DefaultExecutionOrder(-50)]
public class RockRenderer : MonoBehaviour
{
    // 청크 한 변(타일). **`FloorRenderer`와 같은 값**이어야 다시 굽는 단위가 어긋나지 않는다.
    public const int ChunkTiles = 16;

    // ── 윗면과 옆면을 가른다 (2026-08-29) ──────────────────────────────────
    //
    // 참고로 삼은 화면의 규칙이 하나다: **파낸 곳만 보이고 그 밖은 새까맣다.** 위에서
    // 내려다보면 안 판 자리는 **바위 윗면**이므로, 그 면을 검게 칠하면 그대로 그 그림이 된다.
    // 옆면만 밝게 두면 **파낸 방의 테두리가 빛을 받아** 방의 모양이 또렷해진다.
    //
    // **조명으로는 못 가른다** — 주변광을 낮추면 옆면도 같이 죽고, 방향광을 눕히면 한쪽
    // 옆면만 밝다. 면의 역할이 다르므로 **재질이 달라야 한다.**
    [Tooltip("바위 **윗면**. 비우면 거의 검게 만든다 — 안 판 자리가 곧 어둠이다.")]
    [SerializeField] Material rockMaterial;

    [Tooltip("바위 **옆면**(파낸 방의 벽). 비우면 밝은 청회색으로 만든다.")]
    [SerializeField] Material wallMaterial;

    [Tooltip("캘 것으로 찍은 칸. 비우면 바위 재질을 복제해 노랗게 물들인다.")]
    [SerializeField] Material markedMaterial;

    [Tooltip("한 프레임에 다시 굽는 청크 수. 드래그로 넓게 찍으면 여러 장이 한꺼번에 더러워진다.")]
    [SerializeField, Range(1, 8)] int rebuildsPerFrame = 2;

    [Layer]
    [Tooltip("바위가 놓일 레이어. **`Ground`여야 한다** — 커서 레이캐스트가 바위를 짚어야 " +
             "캘 자리를 고를 수 있다.")]
    [SerializeField] int rockLayer = 3;

    class Chunk
    {
        public GameObject Go;
        public Mesh       Mesh;

        // **박스로 막는다 — 메시가 아니다**(2026-08-30). 근거는 `FitBoxes` 위에 적었다.
        public readonly List<BoxCollider> Boxes = new();
    }

    readonly Dictionary<Vector2Int, Chunk> _chunks = new();
    readonly HashSet<Vector2Int>           _dirty  = new();

    readonly List<Vector3> _verts = new();
    readonly List<Vector3> _norms = new();
    readonly List<Vector2> _uvs   = new();
    readonly List<int>     _top   = new();   // 윗면 — 검다
    readonly List<int>     _mark  = new();   // 캘 것으로 찍힌 칸

    readonly List<int>     _side  = new();   // 옆면 — 밝다

    Transform _root;
    Material  _topMat, _sideMat, _markMat;

    [Header("캐면 나오는 것")]
    [Tooltip("광맥이 없는 **맨바위**를 캤을 때. `5_Data/Resource/Stone.asset`을 물릴 것 — " +
             "나오는 것·개수·걸리는 시간이 다 그 안에 있다. **비우면 아무것도 안 나온다.**")]
    [SerializeField] ResourceNodeData plainRock;

    [Header("시작 자리")]
    [Tooltip("판이 열릴 때 내 폰 둘레를 이만큼(m) 파 둔다. **0으로 두면 폰이 바위 속에 갇힌다** — " +
             "걸을 수도, 핵을 놓을 수도, 캘 자리를 고를 수도 없다.")]
    [SerializeField, Range(0f, 40f)] float startClearing = 12f;

    [Tooltip("시작 공터에서 **가장 가까운 자연 굴까지** 길을 뚫는다(m). 굴이 그물로 나 있어도 " +
             "공터가 바위 한복판에 떨어지면 첫 화면이 여전히 웅덩이 하나라, 어느 쪽에 길이 " +
             "있는지를 알 방법이 없다. **0이면 안 뚫는다.**")]
    [SerializeField, Range(0f, 200f)] float pathToCave = 90f;

    [Tooltip("그 길의 폭(m). **좁게 둔다** — 넓으면 저작한 굴과 구별이 안 되고, 시작부터 " +
             "파낼 것이 줄어든다.")]
    [SerializeField, Range(1f, 8f)] float pathWidth = 3f;

    // **판이 열릴 때 내 폰 둘레를 연다.** `Start`에서 한다 — `OnEnable`은 폰이 아직 다 서기
    // 전일 수 있고(실행 순서), 그러면 아무도 못 찾아 **아무 데도 안 열린다.**
    void Start()
    {
        if (!RockMap.Enabled || startClearing <= 0f) return;

        int opened = 0;
        foreach (var brain in FindObjectsByType<UnitBrain>(FindObjectsSortMode.None))
        {
            var f = brain.GetComponent<Faction>();
            if (f == null || !f.IsPlayer) continue;

            RockMap.OpenAround(brain.transform.position, startClearing);

            // **첫 한 자리만 굴에 잇는다** — 폰마다 이으면 진입로가 여럿이라 어디로 가야
            // 할지가 다시 흐려지고, 무엇보다 시작 공터끼리 이어져 마당이 통째로 열린다.
            if (opened == 0 && pathToCave > 0f)
                RockMap.OpenPathToCave(brain.transform.position, pathToCave, pathWidth);

            opened++;
        }

        // **아무도 못 찾으면 카메라 앞을 연다** — 폰이 나중에 오는 판(이벤트로 합류)에서도
        // 최소한 볼 것과 누를 것이 있어야 한다.
        if (opened == 0)
        {
            var p = ViewerXZ();
            RockMap.OpenAround(p, startClearing);
            if (pathToCave > 0f) RockMap.OpenPathToCave(p, pathToCave, pathWidth);
        }
    }

    Vector3 ViewerXZ()
    {
        var t = Camera.main != null ? Camera.main.transform : transform;
        var p = t.position;

        // 카메라는 비스듬히 내려다보므로 **제 자리가 아니라 보고 있는 지면**을 연다.
        var map = MapManager.Instance;
        float y = map != null ? map.SampleGroundHeight(p) : 0f;
        float drop = Mathf.Max(0f, p.y - y);
        return p + t.forward * drop;
    }

    void OnEnable()
    {
        // **씬이 든 것을 static으로 밀어 넣는다** — `RockMap`은 정적이라 스스로 저작할 수
        // 없고, `MineJob`은 컴포넌트가 아니라 씬을 못 본다(`TerrainStreamer`가 지형 프로필을
        // 넘기는 것과 같은 자리).
        if (plainRock != null) RockMap.PlainRock = plainRock;
        else Debug.LogWarning("[바위] `Plain Rock`이 비어 있다 — 맨바위를 캐도 아무것도 안 나온다. " +
                              "`DungeonColony → World → 0. Create Rock Materials`가 물려 준다.");

        RockMap.OnChanged        += OnRockChanged;
        TerrainStreamer.ChunkLoaded   += OnTerrainChunk;
        TerrainStreamer.ChunkUnloaded += OnTerrainGone;

        foreach (var c in TerrainStreamer.LiveChunks) OnTerrainChunk(c);
    }

    void OnDisable()
    {
        RockMap.OnChanged        -= OnRockChanged;
        TerrainStreamer.ChunkLoaded   -= OnTerrainChunk;
        TerrainStreamer.ChunkUnloaded -= OnTerrainGone;

        foreach (var c in new List<Vector2Int>(_chunks.Keys)) Drop(c);
        if (_root != null) Destroy(_root.gameObject);

        // **런타임에 만든 재질을 버린다** — 인스펙터에 물린 것은 에셋이라 안 건드리고,
        // 비어서 여기가 만든 것만 거둔다. 안 버리면 씬을 다시 열 때마다 쌓인다
        // (`TerrainStreamer`가 `TerrainData`에 대해 지키는 규칙과 같다).
        if (_topMat  != null) { Destroy(_topMat);  _topMat  = null; }
        if (_sideMat != null) { Destroy(_sideMat); _sideMat = null; }
        if (_markMat != null) { Destroy(_markMat); _markMat = null; }


    }

    // **지형 청크를 따라간다.** 바위는 세계 전체에 있으므로 반경을 여기서 다시 재면
    // `TerrainStreamer`와 답이 어긋나 **땅은 있는데 벽이 없는** 칸이 생긴다.
    void OnTerrainChunk(Vector2Int terrainChunk)
    {
        if (!RockMap.Enabled) return;

        // 지형 청크(256m)는 바위 청크(16m) 열여섯 장 × 열여섯 줄이다.
        int per = TerrainGrid.ChunkSize / ChunkTiles;
        var o   = TerrainGrid.OriginOf(terrainChunk);
        var map = MapManager.Instance;
        var baseTile = map != null ? map.WorldToTile(o) : new Vector2Int((int)o.x, (int)o.z);

        for (int y = 0; y < per; y++)
        for (int x = 0; x < per; x++)
        {
            var c = new Vector2Int(baseTile.x / ChunkTiles + x, baseTile.y / ChunkTiles + y);
            _live.Add(c);
            _dirty.Add(c);
        }
    }

    void OnTerrainGone(Vector2Int terrainChunk)
    {
        int per = TerrainGrid.ChunkSize / ChunkTiles;
        var o   = TerrainGrid.OriginOf(terrainChunk);
        var map = MapManager.Instance;
        var baseTile = map != null ? map.WorldToTile(o) : new Vector2Int((int)o.x, (int)o.z);

        for (int y = 0; y < per; y++)
        for (int x = 0; x < per; x++)
        {
            var c = new Vector2Int(baseTile.x / ChunkTiles + x, baseTile.y / ChunkTiles + y);
            _live.Remove(c);
            _dirty.Remove(c);      // **예약도 거둔다** — 안 거두면 내린 청크가 다음 프레임에 되살아난다
            Drop(c);
        }
    }

    // **지형이 서 있는 자리만 그린다**(2026-08-30). 바위 청크가 있는 자리를 정하는 것은
    // `OnTerrainChunk` 하나여야 하는데, 아래 `OnRockChanged`가 그걸 건너뛰고 **아무 칸이나
    // 더럽힐 수 있었다.**
    //
    // 증상: *"처음에는 안 보이는데 땅 없는 곳에 채굴을 대 보면 벽이 갑자기 계속 생긴다"*
    // (사용자). 채굴 지정은 미리보기로 칸을 **찍었다 무르며** `OnChanged`를 터뜨리는데,
    // 그 칸이 **아직 지형이 안 선 자리**여도 그대로 구워졌다 — 그래서 **땅도 없는 허공에
    // 벽만 서고**, 커서를 쓸면 그 벽이 계속 늘어난다.
    //
    // 드래그 박스는 콜라이더가 아니라 **평면**과 교차해 칸을 모으므로(귀퉁이가 허공이어도
    // 답이 나와야 한다) 지형 밖까지 닿는다 — 거기서 막지 말고 **그리는 쪽에서** 막는다.
    readonly HashSet<Vector2Int> _live = new();

    // **맞닿은 넷까지 더럽힌다** — 파내면 이웃 바위의 옆면이 새로 드러난다.
    void OnRockChanged(Vector2Int tile)
    {
        Dirty(ChunkOf(tile));
        Dirty(ChunkOf(tile + Vector2Int.right));
        Dirty(ChunkOf(tile + Vector2Int.left));
        Dirty(ChunkOf(tile + Vector2Int.up));
        Dirty(ChunkOf(tile + Vector2Int.down));
    }

    void Dirty(Vector2Int chunk)
    {
        if (_live.Contains(chunk)) _dirty.Add(chunk);
    }

    static Vector2Int ChunkOf(Vector2Int tile) => new(
        Mathf.FloorToInt(tile.x / (float)ChunkTiles),
        Mathf.FloorToInt(tile.y / (float)ChunkTiles));

    void LateUpdate()
    {

        int budget = rebuildsPerFrame;
        while (budget-- > 0 && _dirty.Count > 0)
        {
            Vector2Int chunk = default;
            foreach (var c in _dirty) { chunk = c; break; }
            _dirty.Remove(chunk);
            Rebuild(chunk);
        }
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    void Rebuild(Vector2Int chunk)
    {
        _verts.Clear(); _norms.Clear(); _uvs.Clear(); _top.Clear(); _side.Clear(); _mark.Clear();

        var map = MapManager.Instance;
        int x0 = chunk.x * ChunkTiles, y0 = chunk.y * ChunkTiles;

        // **도는 김에 콜라이더 쪽이 쓸 것까지 채운다**(2026-09-02). `FitBoxes`가 같은 256칸을
        // 한 번 더 돌며 `IsRock`과 `SurfaceHeight`를 **다시** 물었었다 — 답이 안 바뀌는 물음을
        // 청크 한 장마다 두 벌 낸 셈이다. 여기서 채워 넘기면 그 한 바퀴가 통째로 사라진다.
        float lowest = float.MaxValue, highest = float.MinValue;

        for (int ty = 0; ty < ChunkTiles; ty++)
        for (int tx = 0; tx < ChunkTiles; tx++)
        {
            var tile = new Vector2Int(x0 + tx, y0 + ty);

            bool solid = RockMap.IsRock(tile);
            _solid[tx, ty] = solid;
            _taken[tx, ty] = false;
            if (!solid) continue;

            // **찍힌 칸은 윗면·옆면을 가리지 않고 한 벌로 간다** — 찍혔다는 것이 그 칸의
            // 전부이므로, 거기서 또 갈라 봐야 색만 늘고 읽히지는 않는다.
            bool marked = RockMap.IsMarked(tile);

            float floor = map != null ? map.SurfaceHeight(tile) : 0f;
            float top   = floor + RockMap.Height;

            if (floor < lowest)  lowest  = floor;
            if (floor > highest) highest = floor;

            // 칸의 네 모서리(월드). **격자 위상을 얹는다** — 안 얹으면 벽이 반 칸 어긋난다.
            Vector3 c = map != null ? map.TileToWorld(tile)
                                    : new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);
            float xa = c.x - 0.5f, xb = c.x + 0.5f;
            float za = c.z - 0.5f, zb = c.z + 0.5f;

            // 윗면 — 늘 그린다.
            Quad(marked ? _mark : _top,
                 new Vector3(xa, top, za), new Vector3(xa, top, zb),
                 new Vector3(xb, top, zb), new Vector3(xb, top, za), Vector3.up);

            // 옆면 — **뚫린 이웃 쪽만.**
            var sideList = marked ? _mark : _side;
            Side(sideList, tile + Vector2Int.left,  xa, za, xa, zb, floor, top, Vector3.left);
            Side(sideList, tile + Vector2Int.right, xb, zb, xb, za, floor, top, Vector3.right);
            Side(sideList, tile + Vector2Int.down,  xb, za, xa, za, floor, top, Vector3.back);
            Side(sideList, tile + Vector2Int.up,    xa, zb, xb, zb, floor, top, Vector3.forward);
        }

        if (_top.Count == 0 && _side.Count == 0 && _mark.Count == 0) { Drop(chunk); return; }

        var ch = Ensure(chunk);
        var mesh = ch.Mesh;
        mesh.Clear();
        mesh.indexFormat = _verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(_verts);
        mesh.SetNormals(_norms);
        mesh.SetUVs(0, _uvs);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(_top,  0);
        mesh.SetTriangles(_side, 1);
        mesh.SetTriangles(_mark, 2);
        mesh.RecalculateBounds();

        // **콜라이더를 단다** — 커서가 바위를 짚어야 캘 자리를 고를 수 있고, 몸도 막아야 한다.
        // (천장에 안 다는 것과 정반대다 — 저쪽은 위에 있어서 짚으면 안 되고, 이쪽은 짚어야 한다.)
        FitBoxes(ch, chunk, lowest, highest);
    }

    // ── 막는 것은 박스다 ────────────────────────────────────────────────────
    //
    // **콜라이더로 이 메시를 쓰면 안 된다**(2026-08-30). 이 메시는 *드러난 면만* 굽는다 —
    // 즉 **속이 빈 껍데기**다. 캡슐이 어쩌다 한 번 안으로 새면 **밀어낼 면이 안에 없어서**
    // 그대로 벽 속에 선다. ㄷ자 안쪽처럼 두 면에 동시에 눌리는 자리, 순간이동으로 자리를
    // 옮긴 직후, 프레임이 튄 순간 — 새어 들어가는 길이 여럿이고 **하나씩 막는 방식으로는
    // 안 끝난다.**
    //
    // **박스는 속이 찼다.** 유니티가 겹침을 풀 때 언제나 바깥으로 밀어내므로 *"들어간 채로
    // 서 있는"* 상태 자체가 안 생긴다.
    //
    // **칸마다 하나씩 달면 안 된다** — 청크 하나가 256개다. 이어진 바위를 **직사각형으로
    // 묶는다**(탐욕적으로 오른쪽 → 아래로 넓힌다). 통짜 바위 청크는 박스 **하나**가 되고,
    // 굴이 난 곳도 대개 열 몇 개다. `FloorRenderer`가 면을 묶는 것과 같은 결의 최적화다.
    //
    // **높이는 넉넉히 잡는다** — 바닥이 칸마다 조금씩 다르므로 가장 낮은 데서 가장 높은
    // 데까지 덮는다. 넘치는 쪽은 땅속이라 아무도 안 만난다.
    static readonly bool[,] _solid = new bool[ChunkTiles, ChunkTiles];
    static readonly bool[,] _taken = new bool[ChunkTiles, ChunkTiles];

    // `_solid`·`_taken`·최저/최고는 **`Rebuild`가 채워 넘긴다** — 같은 256칸을 두 번 돌지
    // 않으려는 것이고, 그래서 **`Rebuild` 말고 다른 데서 부르면 안 된다**(빈 배열로 돈다).
    void FitBoxes(Chunk ch, Vector2Int chunk, float lowest, float highest)
    {
        var map = MapManager.Instance;
        int x0 = chunk.x * ChunkTiles, y0 = chunk.y * ChunkTiles;

        if (lowest > highest) { SetBoxCount(ch, 0); return; }   // 바위가 없다

        float bottom = lowest - 2f;                 // 조금 파묻는다 — 발밑으로 새지 않게

        float top    = highest + RockMap.Height;
        float cy     = (bottom + top) * 0.5f;
        float sy     = top - bottom;

        int used = 0;
        for (int ty = 0; ty < ChunkTiles; ty++)
        for (int tx = 0; tx < ChunkTiles; tx++)
        {
            if (!_solid[tx, ty] || _taken[tx, ty]) continue;

            // 오른쪽으로 최대한.
            int w = 1;
            while (tx + w < ChunkTiles && _solid[tx + w, ty] && !_taken[tx + w, ty]) w++;

            // 그 폭 그대로 아래로 최대한.
            int h = 1;
            while (ty + h < ChunkTiles)
            {
                bool ok = true;
                for (int k = 0; k < w; k++)
                    if (!_solid[tx + k, ty + h] || _taken[tx + k, ty + h]) { ok = false; break; }
                if (!ok) break;
                h++;
            }

            for (int j = 0; j < h; j++)
            for (int k = 0; k < w; k++) _taken[tx + k, ty + j] = true;

            // 칸 범위 → 월드. **격자 위상을 얹는다**(그리는 쪽과 같은 식이어야 한다).
            Vector3 a = map != null ? map.TileToWorld(new Vector2Int(x0 + tx, y0 + ty))
                                    : new Vector3(x0 + tx + 0.5f, 0f, y0 + ty + 0.5f);

            var box = BoxAt(ch, used++);
            box.center = new Vector3(a.x - 0.5f + w * 0.5f, cy, a.z - 0.5f + h * 0.5f);
            box.size   = new Vector3(w, sy, h);
        }

        SetBoxCount(ch, used);
    }

    // **박스를 지웠다 다시 만들지 않는다** — 청크는 파낼 때마다 다시 구워지므로, 그때마다
    // 컴포넌트를 붙였다 떼면 물리 엔진이 매번 브로드페이즈를 다시 짠다. 남는 것은 꺼 둔다.
    BoxCollider BoxAt(Chunk ch, int i)
    {
        while (ch.Boxes.Count <= i) ch.Boxes.Add(ch.Go.AddComponent<BoxCollider>());
        var b = ch.Boxes[i];
        b.enabled = true;
        return b;
    }

    static void SetBoxCount(Chunk ch, int used)
    {
        for (int i = used; i < ch.Boxes.Count; i++)
            if (ch.Boxes[i] != null) ch.Boxes[i].enabled = false;
    }

    void Side(List<int> list, Vector2Int neighbor,
              float x1, float z1, float x2, float z2, float floor, float top, Vector3 n)
    {
        if (RockMap.IsRock(neighbor)) return;   // 맞닿은 바위 — 안 보인다

        // 이웃 바닥이 더 낮으면 거기까지 내려 세운다. 안 내리면 **벽 밑이 뚫려 보인다.**
        var map = MapManager.Instance;
        float bottom = map != null ? Mathf.Min(floor, map.SurfaceHeight(neighbor)) : floor;

        Quad(list,
             new Vector3(x1, bottom, z1), new Vector3(x1, top, z1),
             new Vector3(x2, top, z2),    new Vector3(x2, bottom, z2), n);
    }

    // 텍스처가 몇 미터에 한 번 반복하나. **칸 크기(1m)와 안 떨어지는 값**으로 둔다 —
    // 정수로 두면 칸마다 같은 자리에서 무늬가 시작해 **격자가 눈에 보인다.**
    const float UvScale = 1f / 2.7f;

    // **UV를 월드 좌표에서 뽑는다**(평면 투영). 면마다 로컬 UV를 매기면 칸 하나가 텍스처
    // 한 장을 통째로 쓰게 되어, 1m 칸에 2m짜리 바위 무늬가 **통째로 눌려 들어간다** —
    // 벽이 모자이크처럼 보인다. 월드로 뽑으면 무늬가 칸을 가로질러 이어진다.
    //
    // **면의 방향에 따라 축을 고른다** — 윗면은 XZ, 옆면은 (X 또는 Z)와 Y다. 안 고르면
    // 옆면의 UV가 한 축으로 눌려 **세로로 늘어난 줄무늬**가 된다.
    void Quad(List<int> list, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
    {
        int i = _verts.Count;
        _verts.Add(a); _verts.Add(b); _verts.Add(c); _verts.Add(d);
        _norms.Add(n); _norms.Add(n); _norms.Add(n); _norms.Add(n);
        Uv(a, n); Uv(b, n); Uv(c, n); Uv(d, n);
        list.Add(i); list.Add(i + 1); list.Add(i + 2);
        list.Add(i); list.Add(i + 2); list.Add(i + 3);
    }

    void Uv(Vector3 p, Vector3 n)
    {
        Vector2 uv;
        if (Mathf.Abs(n.y) > 0.5f)      uv = new Vector2(p.x, p.z);   // 윗면
        else if (Mathf.Abs(n.x) > 0.5f) uv = new Vector2(p.z, p.y);   // 동·서 벽
        else                            uv = new Vector2(p.x, p.y);   // 남·북 벽
        _uvs.Add(uv * UvScale);
    }

    // ── 그릇 ────────────────────────────────────────────────────────────────

    // **바위 청크의 부모.** 커서 레이가 *"이건 바위다"*를 물을 자리다 — 바위는 `Ground`
    // 레이어라(짚어야 캘 수 있다) 레이어로는 바닥과 못 가른다.
    public static Transform ChunkRoot { get; private set; }

    public static bool IsRockCollider(Component c) =>
        c != null && ChunkRoot != null && c.transform.parent == ChunkRoot;

    Chunk Ensure(Vector2Int chunk)
    {
        if (_chunks.TryGetValue(chunk, out var c) && c.Go != null) return c;

        if (_root == null)
        {
            _root = new GameObject("RockChunks").transform;
            _root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ChunkRoot = _root;
        }

        var go = new GameObject($"Rock_{chunk.x}_{chunk.y}");
        go.transform.SetParent(_root, false);
        go.layer = Mathf.Clamp(rockLayer, 0, 31);

        var mesh = new Mesh { name = go.name };
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { TopMat(), SideMat(), MarkMat() };

        c = new Chunk { Go = go, Mesh = mesh };
        _chunks[chunk] = c;
        return c;
    }

    void Drop(Vector2Int chunk)
    {
        if (!_chunks.TryGetValue(chunk, out var c)) return;
        _chunks.Remove(chunk);
        if (c.Go != null) Destroy(c.Go);
        if (c.Mesh != null) Destroy(c.Mesh);   // **메시도 같이 버린다** — 안 버리면 샌다
    }

    // **안 판 자리는 어둠이다.** 완전한 0으로는 안 내린다 — 그러면 벽 윗면과 화면 배경이
    // 구별이 안 가 **세계의 경계가 어디인지**를 못 읽는다. 아주 어둡게 두면 안개와 섞여
    // *"저기까지는 바위다"*가 보인다.
    // **안 판 자리는 어둠이다.** 완전한 0으로는 안 내린다 — 그러면 벽 윗면과 화면 배경이
    // 구별이 안 가 **세계의 경계가 어디인지**를 못 읽는다.
    Material TopMat()
    {
        if (rockMaterial != null) return rockMaterial;
        if (_topMat != null) return _topMat;

        _topMat = Lit("Rock top (runtime)", new Color(0.05f, 0.06f, 0.07f));
        return _topMat;
    }

    // **파낸 방의 벽.** 참고 화면의 그 밝은 청회색 — 빛을 받는 면이라 방의 모양을 그린다.
    Material SideMat()
    {
        if (wallMaterial != null) return wallMaterial;
        if (_sideMat != null) return _sideMat;

        _sideMat = Lit("Rock wall (runtime)", new Color(0.52f, 0.57f, 0.60f));
        return _sideMat;
    }

    Material MarkMat()
    {
        if (markedMaterial != null) return markedMaterial;
        if (_markMat != null) return _markMat;

        _markMat = Lit("Rock marked (runtime)", new Color(1.00f, 0.74f, 0.24f));

        // **스스로 빛나게 한다** — 어두운 굴에서 색만 바꾸면 주변광에 묻혀 티가 안 난다.
        // **키워드를 같이 켜야** URP가 그 패스를 돈다(색만 넣으면 아무 일도 안 일어난다).
        _markMat.EnableKeyword("_EMISSION");
        _markMat.SetColor("_EmissionColor", new Color(0.95f, 0.55f, 0.08f));
        _markMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return _markMat;
    }

    static Material Lit(string name, Color color)
    {
        var sh = RuntimeShaders.Find(RuntimeShaders.Lit, RuntimeShaders.Standard);
        var m = new Material(sh) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color",     color);   // 빌트인으로 떨어졌을 때
        m.SetFloat("_Smoothness", 0.02f);  // 젖은 광택이 나면 저폴리 느낌이 깨진다
        m.SetFloat("_Metallic",   0f);

        // **양면으로 그린다**(2026-08-29). 벽이 **투명하게 보이던 원인**이다 — 면마다 네
        // 모서리를 손으로 적었는데 방향에 따라 감김이 뒤집혀서, 뒤집힌 면은 뒷면 컬링에
        // 걸려 **통째로 안 그려졌다.** 그 자리는 벽이 아니라 구멍으로 보인다.
        //
        // 감김을 하나씩 맞추는 길도 있지만, **면이 여섯 방향이고 새 면을 추가할 때마다 또
        // 틀린다.** 바위는 속이 꽉 찬 물건이라 양면으로 그려도 잃는 것이 없다.
        m.SetFloat("_Cull", 0f);                    // 0 = Off (양면)
        m.doubleSidedGI = true;
        return m;
    }
}
