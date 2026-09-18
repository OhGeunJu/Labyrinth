using System.Collections.Generic;
using UnityEngine;

// **광역 길찾기 — 칸 하나에 노드 하나가 아니다**(2026-09-05, 설계는 `docs/design_pathfinding_hpa.md`).
//
// 그전의 광역 격자는 셀마다 *"이 안에 걸을 수 있는 데가 있나"*를 쟀다. 그런데 라우팅이
// 알아야 하는 것은 *"이 셀을 **가로질러** 지나갈 수 있나"*다 — **그 물음에 한 번도 답한 적이
// 없었다.** 사용자가 증상을 두 줄로 정리했다:
//
// > 눈금을 작게 하면 좁은 길은 가는데 **너무 얇은 벽도 갈 수 있다고 생각하고 부딪히고**,
// > 눈금을 크게 하면 **벽으로 막혀 있는 길을 갈 수 있다고 생각해서 간다.**
//
// 두 증상이 같은 원인이다 — **칸 하나에 노드 하나**라는 모양에는 *칸을 가르는 벽*을 적을
// 자리가 없다. 그래서 2026-08-31에 하루를 다이얼(문턱 3→2→1 · 표본 3×3→4×4 · 간선 폴백)에
// 썼지만 전부 헛수고였다. **어느 눈금에 둬도 안 된다.**
//
// ## 정답은 HPA\*의 원래 정의다
//
// ```
// 지금:  셀 하나 = 노드 하나
// 정답:  셀 안의 **연결 덩어리마다** 노드 하나
// ```
//
// - 셀 안(6~8칸)을 **1m로 훑어 연결 덩어리를 나눈다.**
// - 경계의 열린 자리(entrance)가 **어느 덩어리와 어느 덩어리를 잇는지**로 간선을 만든다.
// - 그래서:
//   - 셀을 반으로 가르는 **얇은 벽** → 덩어리 둘 → **가로지르는 간선이 없다.**
//   - **한 칸짜리 통로** → 그 덩어리가 그대로 노드 → 안 잃는다.
//   - **눈금 다이얼이 사라진다** — 열림도 연결도 전부 1m 사실에서 나온다.
//
// ## 폰의 이동 규칙을 그대로 쓴다
//
// 8방향이되 **대각선은 직교 이웃 둘이 다 열렸을 때만**(`PathGrid.Enterable`과 같은 규칙).
// 여기서 규칙이 갈리면 그래프가 또 거짓말을 한다.
//
// **턱·벼랑은 안 본다**(`SameLevel`). 광역은 *복도 안내*이고 정밀 차단은 로컬 몫이라는
// 규칙 그대로이고, 옛 광역 격자도 안 봤다. 무엇보다 칸마다 높이를 다시 뽑으면 **굽는 값이
// 두 배**가 된다.
//
// ## 값
//
// 칸마다 `IsWorkable` 한 번(세계 1,024m면 **백만 번**) + 덩어리 나누기 + 경계 훑기.
// 굽는 것은 **판이 열릴 때 한 번**이고, 그 뒤로는 **판 자리 둘레만** 다시 굽는다
// (`RebakeArea`). 담는 것은 칸당 2바이트(걸을 수 있나 · 어느 덩어리냐)라 1,024m에서 **2MB**다.
public class WideGraph
{
    // **셀 하나가 가질 수 있는 덩어리 수 천장 — 셀 크기에서 낸다.**
    //
    // 한동안 **8로 못 박고** *"체커보드면 이론상 서른둘이지만 굴에서는 둘셋"*이라고 적어
    // 두었다. **그 전제가 채굴을 안 세고 있었다**(2026-09-18) — 자연 굴은 이어진 몸이라
    // 둘셋이 맞지만, **찍어 둔 덩이를 폰 여럿이 파기 시작하면** 한 셀(7×7) 안에 **서로
    // 안 이어진 구멍이 스물 몇 개**까지 생긴다.
    //
    // 넘친 덩어리는 **버려지므로 그 칸에는 광역 노드가 아예 없다** → `ComputeCoarseRoute`가
    // 거기로 가는 길을 못 내고 → 로컬 창(40m) 밖이면 **폰이 아예 안 간다.** 화면에는
    // *"파 놓은 자리에 갈 수가 없다 — 벽은 안 보이는데"*로만 보이고, **콜라이더도 통행
    // 판정도 멀쩡하므로 어디를 봐도 이유가 없다.** 그리고 못 가니 못 파고, 못 파니 구멍이
    // 안 이어져 **덩어리가 줄지 않는다** — 스스로 안 풀린다.
    //
    // 그래서 천장을 **셀이 가질 수 있는 최댓값**(체커보드 = 칸 수의 절반)으로 둔다 —
    // 7×7이면 25다. **넘칠 수가 없으면 버릴 일도 없다.** 담는 값은 7칸 셀에서
    // 노드 2MB → 6.5MB로 는다(칸 표 2MB는 그대로).
    //
    // 덩어리 번호는 `_comp`가 **바이트**로 드므로 254를 못 넘는다(`None` = 255).
    readonly int _maxComp;

    // **경계 하나가 이을 수 있는 덩어리 쌍의 수 천장.** 같은 경계에서 서로 다른 덩어리 쌍이
    // 넷을 넘는 일은 굴에서 없다(넘으면 첫 넷만 든다 — 이어진 것은 이미 이어져 있다).
    const int MaxLinks = 4;

    const byte None = 255;

    readonly int _tiles;      // 세계 한 변의 칸 수
    readonly int _cellTiles;  // 셀 한 변의 칸 수
    readonly int _cells;      // 한 변의 셀 수

    readonly byte[] _walk;    // 칸 → 걸을 수 있나(1/0)
    readonly byte[] _comp;    // 칸 → 제 셀 안의 덩어리 번호(None = 없음)

    readonly float[] _nodeX, _nodeZ;   // 덩어리 대표점(무게중심)
    readonly int[]   _nodeTiles;       // 덩어리가 든 칸 수(0이면 그 노드는 없다)

    readonly byte[]  _linkA, _linkB;   // 경계의 k번째 연결 — 양쪽 덩어리 번호
    readonly float[] _linkX, _linkZ;   // 그 통과점(entrance)

    // **막힌 것으로 배운 노드**(`ReportUnreachable`). 셀이 아니라 **덩어리** 단위다 —
    // 셀을 통째로 닫으면 그 셀의 멀쩡한 다른 덩어리까지 같이 닫힌다.
    readonly Dictionary<int, float> _blocked = new();

    MapManager _map;
    bool _warnedComp;

    public float CellSize => _cellTiles;
    public int   Cells    => _cells;

    public WideGraph(int worldTiles, int cellTiles)
    {
        _tiles     = Mathf.Max(1, worldTiles);
        _cellTiles = Mathf.Clamp(cellTiles, 2, 64);
        _cells     = Mathf.Max(1, Mathf.CeilToInt(_tiles / (float)_cellTiles));

        // **체커보드가 최악이고 그게 칸 수의 절반이다.** 셀이 아주 크면 바이트 번호가
        // 먼저 바닥나므로 254에서 자른다(그때는 옛날처럼 남는 덩어리를 버리고 경고한다).
        _maxComp = Mathf.Min(254, (_cellTiles * _cellTiles + 1) / 2);

        _walk = new byte[_tiles * _tiles];
        _comp = new byte[_tiles * _tiles];

        int nodes = _cells * _cells * _maxComp;
        _nodeX     = new float[nodes];
        _nodeZ     = new float[nodes];
        _nodeTiles = new int[nodes];

        int links = _cells * _cells * 2 * MaxLinks;
        _linkA = new byte[links];
        _linkB = new byte[links];
        _linkX = new float[links];
        _linkZ = new float[links];
    }

    // ── 색인 ─────────────────────────────────────────────────────────────

    int  TileIdx(int tx, int tz) => tz * _tiles + tx;
    bool InTiles(int tx, int tz) => tx >= 0 && tz >= 0 && tx < _tiles && tz < _tiles;
    bool InCells(int cx, int cz) => cx >= 0 && cz >= 0 && cx < _cells && cz < _cells;

    int CellOf(int t) => Mathf.Clamp(t / _cellTiles, 0, _cells - 1);
    int NodeId(int cx, int cz, int comp) => ((cz * _cells + cx) * _maxComp) + comp;
    int LinkBase(int cx, int cz, int dir) => (((cz * _cells + cx) * 2) + dir) * MaxLinks;

    bool Walk(int tx, int tz) => InTiles(tx, tz) && _walk[TileIdx(tx, tz)] != 0;

    // **1m 걸음 표를 밖에서 읽는 문** — 가장자리 스폰 자리를 뽑는 BFS(`HierarchicalPathfinder.
    // PickReachableSpread`)가 쓴다. `IsWorkable`을 담아 둔 것이라 같은 답을 다시 뽑을 이유가 없다.
    public bool WalkableTile(int tx, int tz) => Walk(tx, tz);

    // ── 굽기 ─────────────────────────────────────────────────────────────

    // **굽는 값을 재서 적는다.** 칸 백만 개를 훑는 일이라 *"얼마나 걸리나"*가 짐작이 되면
    // 안 된다 — 판이 열릴 때 한 번이지만, 세계를 키우거나 셀을 잘게 잡으면 여기가 먼저
    // 아프다. 덩어리·간선 수도 같이 적는다(그래프가 통째로 비었는지가 한 줄로 보인다).
    public void Build(MapManager map)
    {
        _map = map;
        if (_map == null) return;

        var clock = System.Diagnostics.Stopwatch.StartNew();

        // **걸을 수 있나를 먼저 통째로 담는다.** 덩어리 나누기가 이웃을 여러 번 되묻는데,
        // 그때마다 `IsWorkable`을 부르면 같은 칸을 여덟 번 다시 잰다(칸 백만 개면 팔백만 번).
        for (int tz = 0; tz < _tiles; tz++)
        for (int tx = 0; tx < _tiles; tx++)
            _walk[TileIdx(tx, tz)] = (byte)(map.IsWorkable(new Vector2Int(tx, tz)) ? 1 : 0);

        for (int cz = 0; cz < _cells; cz++)
        for (int cx = 0; cx < _cells; cx++) BuildComponents(cx, cz);

        for (int cz = 0; cz < _cells; cz++)
        for (int cx = 0; cx < _cells; cx++) BuildLinks(cx, cz);

        clock.Stop();

        int nodes = 0, links = 0;
        for (int i = 0; i < _nodeTiles.Length; i++) if (_nodeTiles[i] > 0) nodes++;
        for (int i = 0; i < _linkA.Length; i++)     if (_linkA[i] != None) links++;

        Debug.Log($"[길찾기] 광역 그래프 — {_cells}×{_cells}셀(한 변 {_cellTiles}칸) · "
                + $"덩어리 {nodes}개 · 간선 {links}개 · {clock.ElapsedMilliseconds}ms");

        if (nodes == 0)
            Debug.LogWarning("[길찾기] 광역 그래프에 덩어리가 하나도 없다 — 세계가 통째로 "
                           + "바위이거나 `MapManager.IsWorkable`이 전부 거짓이다. 먼 곳으로는 "
                           + "아무도 못 간다.");
    }

    // **판 자리 둘레만 다시 굽는다.** 칸 하나를 파면 그 칸이 든 셀의 덩어리가 갈라지거나
    // 합쳐지고, **맞닿은 경계 넷**의 연결도 달라진다(동·북은 제 것, 서·남은 이웃 것) —
    // 안 하면 판 길이 먼 경로에 영영 안 보인다.
    public void RebakeArea(Vector3 center, float radius)
    {
        if (_map == null) return;

        var c = _map.WorldToTile(center);
        int r = Mathf.Max(1, Mathf.CeilToInt(radius));

        int cx0 = CellOf(c.x - r), cx1 = CellOf(c.x + r);
        int cz0 = CellOf(c.y - r), cz1 = CellOf(c.y + r);

        for (int cz = cz0; cz <= cz1; cz++)
        for (int cx = cx0; cx <= cx1; cx++)
        {
            RefreshWalk(cx, cz);
            BuildComponents(cx, cz);
        }

        // 경계는 한 칸 넓게 — 서·남 이웃이 제 동·북 경계를 든다.
        for (int cz = cz0 - 1; cz <= cz1; cz++)
        for (int cx = cx0 - 1; cx <= cx1; cx++)
            if (InCells(cx, cz)) BuildLinks(cx, cz);

        // **다시 나눈 셀의 "배운 것"은 버린다**(2026-09-05). 덩어리 번호는 `BuildComponents`가
        // **매번 0부터 다시 매기므로**, 남겨 두면 20초짜리 차단이 **엉뚱한 덩어리에 옮겨
        // 붙는다** — 하필 그것이 그 셀의 주 통로면 멀쩡한 길이 조용히 끊긴다.
        for (int cz = cz0; cz <= cz1; cz++)
        for (int cx = cx0; cx <= cx1; cx++)
        for (int k = 0; k < _maxComp; k++)
            _blocked.Remove(NodeId(cx, cz, k));
    }

    void RefreshWalk(int cx, int cz)
    {
        int x0 = cx * _cellTiles, z0 = cz * _cellTiles;
        int x1 = Mathf.Min(x0 + _cellTiles, _tiles), z1 = Mathf.Min(z0 + _cellTiles, _tiles);

        for (int tz = z0; tz < z1; tz++)
        for (int tx = x0; tx < x1; tx++)
            _walk[TileIdx(tx, tz)] = (byte)(_map.IsWorkable(new Vector2Int(tx, tz)) ? 1 : 0);
    }

    // ── 셀 안의 연결 덩어리 ──────────────────────────────────────────────
    //
    // **이 함수가 이 설계의 전부다.** 셀 안을 1m로 훑어 이어진 것끼리 묶는다 — 그래서
    // 셀을 가르는 벽이 **노드 둘**로 나타나고, 가로지르는 간선이 애초에 안 생긴다.

    static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

    readonly Stack<int> _flood = new();

    void BuildComponents(int cx, int cz)
    {
        int x0 = cx * _cellTiles, z0 = cz * _cellTiles;
        int x1 = Mathf.Min(x0 + _cellTiles, _tiles), z1 = Mathf.Min(z0 + _cellTiles, _tiles);

        for (int tz = z0; tz < z1; tz++)
        for (int tx = x0; tx < x1; tx++)
            _comp[TileIdx(tx, tz)] = None;

        for (int k = 0; k < _maxComp; k++)
        {
            int n = NodeId(cx, cz, k);
            _nodeTiles[n] = 0;
        }

        byte next = 0;
        for (int tz = z0; tz < z1; tz++)
        for (int tx = x0; tx < x1; tx++)
        {
            if (!Walk(tx, tz) || _comp[TileIdx(tx, tz)] != None) continue;

            if (next >= _maxComp)
            {
                // **버린다 — 이어 붙이지 않는다.** 없는 길을 있다고 하는 것이 훨씬 나쁘다.
                if (!_warnedComp)
                {
                    _warnedComp = true;
                    Debug.LogWarning($"[길찾기] 셀 ({cx},{cz})의 덩어리가 {_maxComp}개를 넘었다 — "
                                   + "남는 것은 광역에서 안 보인다(로컬이 찾는다). "
                                   + "셀이 너무 넓거나 지형이 유난히 잘게 갈린 자리다.");
                }
                break;
            }

            Fill(tx, tz, x0, z0, x1, z1, next, NodeId(cx, cz, next));
            next++;
        }
    }

    void Fill(int sx, int sz, int x0, int z0, int x1, int z1, byte comp, int node)
    {
        _flood.Clear();
        _flood.Push(TileIdx(sx, sz));
        _comp[TileIdx(sx, sz)] = comp;

        double sumX = 0, sumZ = 0;
        int count = 0;

        while (_flood.Count > 0)
        {
            int idx = _flood.Pop();
            int tx = idx % _tiles, tz = idx / _tiles;

            sumX += tx; sumZ += tz; count++;

            for (int d = 0; d < 8; d++)
            {
                int nx = tx + DX[d], nz = tz + DZ[d];
                if (nx < x0 || nz < z0 || nx >= x1 || nz >= z1) continue;   // 셀 밖은 경계가 잇는다
                if (!Walk(nx, nz)) continue;

                // **대각선은 직교 이웃 둘이 다 열렸을 때만**(`PathGrid.Enterable`과 같은 규칙).
                // 안 지키면 격자에서는 통과인데 **몸이 두 면 사이에 낀다.**
                if (d >= 4 && (!Walk(tx + DX[d], tz) || !Walk(tx, tz + DZ[d]))) continue;

                int ni = TileIdx(nx, nz);
                if (_comp[ni] != None) continue;

                _comp[ni] = comp;
                _flood.Push(ni);
            }
        }

        _nodeTiles[node] = count;
        if (count <= 0) return;

        // 대표점은 무게중심이다. **간선 값과 어림값에만 쓰인다** — 웨이포인트로 나가는 것은
        // 경계 통과점이라, 오목한 덩어리에서 중심이 살짝 벗어나도 길이 틀어지지 않는다.
        var w = _map.TileToWorld(new Vector2Int(Mathf.RoundToInt((float)(sumX / count)),
                                                Mathf.RoundToInt((float)(sumZ / count))));
        _nodeX[node] = w.x;
        _nodeZ[node] = w.z;
    }

    // ── 경계 — HPA\*가 entrance라고 부르는 것 ─────────────────────────────
    //
    // 맞닿은 선을 **1m로 훑어** 양쪽 칸이 둘 다 걸을 수 있는 자리를 찾고, 그 자리가 잇는
    // **덩어리 쌍**을 적는다. 그래서 *"셀이 열렸나"*가 아니라 *"이 덩어리에서 저 덩어리로
    // 건너갈 수 있나"*에 답한다.

    void BuildLinks(int cx, int cz)
    {
        WriteLinks(cx, cz, dir: 0);   // 동
        WriteLinks(cx, cz, dir: 1);   // 북
    }

    // **통과점은 열린 구간의 한가운데다**(2026-09-05에 바로잡았다).
    //
    // 처음에는 **처음 열린 칸**을 통과점으로 삼고 같은 덩어리 쌍이면 건너뛰었다. 그러면
    // 통로가 경계를 다섯 칸 폭으로 가로지를 때 **그 다섯 중 첫 칸** — 즉 **한쪽 벽에 붙은
    // 자리**가 통과점이 된다. 화면에서는 *"노란 공이 가운데가 아니라 벽 근처에 선다"*로
    // 보이고, 폰은 그 점을 향해 걸으므로 **넓은 굴에서도 벽을 끼고 다닌다.**
    //
    // HPA\*가 entrance를 다루는 방식 그대로 **이어진 구간(run)을 찾아 그 한가운데**를 쓴다.
    // 그러면 통과점이 저절로 **통로 폭의 복판**에 선다 — 벽에서 떼는 손질(`PushOffWalls`)이
    // 필요 없어지는 것도 이 때문이다.
    //
    // **구간마다 하나씩 적는다 — 덩어리 쌍마다가 아니다.** 같은 두 덩어리가 **서로 다른
    // 문 둘**로 이어질 수 있고(굴이 갈라졌다 만나는 자리), 쌍으로 묶으면 그중 하나만 남아
    // 라우트가 **먼 문으로 돌아간다.** 자리가 모자라면 **넓은 문부터** 남긴다(좁은 문은
    // 어차피 로컬이 찾는다).
    void WriteLinks(int cx, int cz, int dir)
    {
        int b = LinkBase(cx, cz, dir);
        for (int k = 0; k < MaxLinks; k++) { _linkA[b + k] = None; _linkB[b + k] = None; }

        int ncx = dir == 0 ? cx + 1 : cx;
        int ncz = dir == 0 ? cz : cz + 1;
        if (!InCells(ncx, ncz)) return;

        int x0 = cx * _cellTiles, z0 = cz * _cellTiles;
        int x1 = Mathf.Min(x0 + _cellTiles, _tiles), z1 = Mathf.Min(z0 + _cellTiles, _tiles);

        int lo = dir == 0 ? z0 : x0;
        int hi = dir == 0 ? z1 : x1;

        _runs.Clear();

        int  runStart = -1;
        byte runA = None, runB = None;

        for (int t = lo; t <= hi; t++)
        {
            byte ca = None, cb = None;

            if (t < hi)
            {
                int ax = dir == 0 ? x1 - 1 : t;
                int az = dir == 0 ? t      : z1 - 1;
                int bx = dir == 0 ? x1     : t;
                int bz = dir == 0 ? t      : z1;

                if (InTiles(bx, bz) && Walk(ax, az) && Walk(bx, bz))
                {
                    ca = _comp[TileIdx(ax, az)];
                    cb = _comp[TileIdx(bx, bz)];
                    if (ca == None || cb == None) { ca = None; cb = None; }
                }
            }

            bool same = ca != None && ca == runA && cb == runB;
            if (same) continue;

            // 구간이 끝났다 — 닫는다.
            if (runStart >= 0) _runs.Add(new Run { Lo = runStart, Hi = t - 1, A = runA, B = runB });

            runStart = ca != None ? t : -1;
            runA = ca; runB = cb;
        }

        // **넓은 문부터.** 자리가 넷뿐이라 좁은 것을 먼저 넣으면 진짜 통로가 밀려난다.
        _runs.Sort((p, q) => (q.Hi - q.Lo).CompareTo(p.Hi - p.Lo));

        int written = 0;
        for (int i = 0; i < _runs.Count && written < MaxLinks; i++)
        {
            var run = _runs[i];
            int mid = (run.Lo + run.Hi) / 2;

            int ax = dir == 0 ? x1 - 1 : mid;
            int az = dir == 0 ? mid    : z1 - 1;
            int bx = dir == 0 ? x1     : mid;
            int bz = dir == 0 ? mid    : z1;

            var pa = _map.TileToWorld(new Vector2Int(ax, az));
            var pb = _map.TileToWorld(new Vector2Int(bx, bz));

            _linkA[b + written] = run.A;
            _linkB[b + written] = run.B;
            _linkX[b + written] = (pa.x + pb.x) * 0.5f;
            _linkZ[b + written] = (pa.z + pb.z) * 0.5f;
            written++;
        }
    }

    struct Run { public int Lo, Hi; public byte A, B; }

    readonly List<Run> _runs = new();

    // ── 노드 찾기 ────────────────────────────────────────────────────────
    //
    // **부풀림이 아니라 `_walk`로 찾는다** — 여기가 묻는 것은 *거기 설 수 있나*다. 못 서는
    // 자리를 겨눴으면 둘레에서 가장 가까운 설 수 있는 칸으로 접는다(안 접으면 벽 옆 칸의
    // 목적지를 통째로 못 잡는다).
    public bool TryNodeAt(Vector3 world, out int node, int maxRadius = 12)
    {
        node = -1;
        if (_map == null) return false;

        var t = _map.WorldToTile(world);
        if (TryNodeAtTile(t.x, t.y, out node)) return true;

        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                if (TryNodeAtTile(t.x + dx, t.y + dz, out node)) return true;
            }
        }
        return false;
    }

    bool TryNodeAtTile(int tx, int tz, out int node)
    {
        node = -1;
        if (!InTiles(tx, tz) || !Walk(tx, tz)) return false;

        byte c = _comp[TileIdx(tx, tz)];
        if (c == None) return false;

        node = NodeId(CellOf(tx), CellOf(tz), c);
        return _nodeTiles[node] > 0;
    }

    // ── 이웃 ─────────────────────────────────────────────────────────────
    //
    // 노드 하나에 닿는 경계는 넷이다 — 제 셀의 동·북, 그리고 서·남 이웃이 든 그쪽 동·북.

    struct Step { public int Node; public Vector3 Gate; }

    readonly List<Step> _steps = new();

    List<Step> Neighbors(int node)
    {
        _steps.Clear();

        int comp = node % _maxComp;
        int cell = node / _maxComp;
        int cx = cell % _cells, cz = cell / _cells;

        Collect(cx, cz, 0, comp, forward: true);    // 제 동쪽 경계 — 나는 A
        Collect(cx, cz, 1, comp, forward: true);    // 제 북쪽 경계 — 나는 A
        Collect(cx - 1, cz, 0, comp, forward: false);   // 서쪽 이웃의 동쪽 경계 — 나는 B
        Collect(cx, cz - 1, 1, comp, forward: false);   // 남쪽 이웃의 북쪽 경계 — 나는 B

        return _steps;
    }

    void Collect(int cx, int cz, int dir, int comp, bool forward)
    {
        if (!InCells(cx, cz)) return;

        int ncx = dir == 0 ? cx + 1 : cx;
        int ncz = dir == 0 ? cz : cz + 1;
        if (!InCells(ncx, ncz)) return;

        int b = LinkBase(cx, cz, dir);
        for (int k = 0; k < MaxLinks; k++)
        {
            byte a = _linkA[b + k], bb = _linkB[b + k];
            if (a == None || bb == None) continue;

            int mine  = forward ? a  : bb;
            int other = forward ? bb : a;
            if (mine != comp) continue;

            int n = forward ? NodeId(ncx, ncz, other) : NodeId(cx, cz, other);
            if (_nodeTiles[n] <= 0) continue;

            _steps.Add(new Step { Node = n, Gate = new Vector3(_linkX[b + k], 0f, _linkZ[b + k]) });
        }
    }

    // ── 배운 것 ──────────────────────────────────────────────────────────
    //
    // **덩어리 하나만 잠깐 닫는다.** 라우트가 틀렸을 때 폰이 배우는 길인데(로컬이 못 뚫으면
    // 광역이 알아야 한다), 영구히 닫으면 **한 폰이 한 번 실패한 것으로 세계가 좁아진다.**
    // **가까이서만 짚는다**(반경 2). 부르는 쪽이 넘기는 것은 *못 간 자리*라 대개 벽 속인데,
    // 기본 반경(12)으로 스냅하면 **벽 건너편이 아니라 폰이 서 있는 쪽**이 잡히기 쉽다 —
    // 그러면 배운다는 것이 **제가 선 자리를 닫는 일**이 된다.
    public void BlockAt(Vector3 world, float seconds)
    {
        if (!TryNodeAt(world, out int node, maxRadius: 2)) return;
        _blocked[node] = Time.time + seconds;
    }

    bool IsBlocked(int node)
    {
        if (!_blocked.TryGetValue(node, out float until)) return false;
        if (Time.time < until) return true;

        _blocked.Remove(node);
        return false;
    }

    // ── A\* ──────────────────────────────────────────────────────────────
    //
    // **부분 경로를 낸다** — 못 가면 `null`이 아니라 *가장 가까이까지*다. `null`을 돌려주면
    // 부르는 쪽이 직선 폴백으로 떨어지고, 지하에서 그 직선은 거의 늘 바위 속이다.
    //
    // **막힌 것으로 배운 노드를 빼고 먼저 푼다.** 그것 때문에 길이 아예 안 나오면 무시하고
    // 한 번 더 푼다 — **끊어 놓고 못 가는 것보다 이어 두고 배우는 편이 낫다.**
    public List<Vector3> FindRoute(Vector3 from, Vector3 to)
    {
        if (_map == null) return null;
        if (!TryNodeAt(from, out int start)) return null;

        // **목적지에 노드가 없어도 포기하지 않는다**(2026-09-05에 고쳤다). 채굴은 **바위 칸
        // 자체를 겨눈다**(`MineJob.TileCoord`) — 큰 덩이의 속이면 12칸 안에 걸을 수 있는 칸이
        // 없어 노드를 못 찾는다. 그때 `null`을 돌려주면 부르는 쪽이 **직선 한 방**으로 떨어지고,
        // 지하에서 그 직선은 거의 늘 바위 속이다(이 파일이 내내 피하려던 그 모양이다).
        //
        // 노드가 없으면 **도달 못 하는 목표(-1)로 두고 풀어** 가장 가까운 데까지의 부분 경로를
        // 낸다 — *"길이 없으면 가장 가까이까지라도 간다"*는 이 저장소의 규칙 그대로다.
        int goal = TryNodeAt(to, out int g) ? g : -1;

        if (goal >= 0 && start == goal) return new List<Vector3> { to };

        var route = Search(start, goal, to, useBlocked: true);
        if (route == null || route.Count == 0) route = Search(start, goal, to, useBlocked: false);
        return route;
    }

    readonly Dictionary<int, float> _g       = new();
    readonly Dictionary<int, int>   _from    = new();
    readonly Dictionary<int, Vector3> _gate  = new();

    // **이진 힙이다 — 목록을 훑지 않는다.** 한 변 147셀에 덩어리가 둘씩만 돌아도 열린 목록이
    // 수천이 되고, 매 번 가장 작은 것을 훑어 찾으면 **경로 하나가 O(n²)**이다. 폰이 여럿이
    // 같은 프레임에 재계획하는 것이 이 저장소가 이미 겪은 사고라(2026-09-02, 프레임 104ms)
    // 여기서 같은 모양을 다시 만들 이유가 없다.
    readonly List<float> _openF = new();
    readonly List<int>   _openN = new();

    void HeapPush(float f, int node)
    {
        _openF.Add(f); _openN.Add(node);
        int i = _openF.Count - 1;
        while (i > 0)
        {
            int p = (i - 1) >> 1;
            if (_openF[p] <= _openF[i]) break;
            (_openF[p], _openF[i]) = (_openF[i], _openF[p]);
            (_openN[p], _openN[i]) = (_openN[i], _openN[p]);
            i = p;
        }
    }

    int HeapPop()
    {
        int top = _openN[0];
        int last = _openF.Count - 1;

        _openF[0] = _openF[last]; _openN[0] = _openN[last];
        _openF.RemoveAt(last);    _openN.RemoveAt(last);

        int i = 0, n = _openF.Count;
        while (true)
        {
            int l = i * 2 + 1, r = l + 1, m = i;
            if (l < n && _openF[l] < _openF[m]) m = l;
            if (r < n && _openF[r] < _openF[m]) m = r;
            if (m == i) break;

            (_openF[m], _openF[i]) = (_openF[i], _openF[m]);
            (_openN[m], _openN[i]) = (_openN[i], _openN[m]);
            i = m;
        }
        return top;
    }

    List<Vector3> Search(int start, int goal, Vector3 goalPos, bool useBlocked)
    {
        _g.Clear(); _from.Clear(); _gate.Clear();
        _openF.Clear(); _openN.Clear();

        _g[start] = 0f;
        HeapPush(Heur(start, goalPos), start);

        int  best     = start;
        float bestH   = Heur(start, goalPos);
        bool  reached = false;

        while (_openF.Count > 0)
        {
            int cur = HeapPop();
            if (cur == goal) { reached = true; break; }

            // **낡은 항목은 버린다** — 값이 더 좋아진 노드를 힙에서 지우는 대신 다시 넣으므로
            // 같은 노드가 여럿 들어 있다(감소키 없는 A\*의 흔한 모양).
            if (!_g.TryGetValue(cur, out float gc)) continue;
            var steps = Neighbors(cur);
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (useBlocked && IsBlocked(s.Node)) continue;

                // 값은 **대표점 → 통과점 → 대표점**이다. 셀 중심끼리 재면 한 칸짜리 통로가
                // 넓은 간선과 같은 값이 된다.
                float step = Dist(cur, s.Gate) + Dist(s.Node, s.Gate);
                float ng   = gc + step;

                if (_g.TryGetValue(s.Node, out float old) && old <= ng) continue;

                _g[s.Node]    = ng;
                _from[s.Node] = cur;
                _gate[s.Node] = s.Gate;

                float h = Heur(s.Node, goalPos);
                if (h < bestH) { bestH = h; best = s.Node; }

                HeapPush(ng + h, s.Node);
            }
        }

        int end = reached ? goal : best;
        if (end == start) return null;

        // 되짚어 통과점만 모은다. **덩어리 대표점은 안 넣는다** — 통과점만으로 이미 복도가
        // 그려지고, 대표점을 끼우면 경로가 셀 한복판으로 끌려가 지그재그가 된다.
        var back = new List<Vector3>();
        int at = end;
        while (at != start && _from.ContainsKey(at))
        {
            back.Add(_gate[at]);
            at = _from[at];
        }
        if (at != start) return null;

        back.Reverse();
        if (reached) back.Add(goalPos);
        return back;
    }

    float Dist(int node, Vector3 p)
    {
        float dx = _nodeX[node] - p.x, dz = _nodeZ[node] - p.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    float Heur(int node, Vector3 goal) => Dist(node, goal);

    // ── 진단 ─────────────────────────────────────────────────────────────
    //
    // **끊긴 자리를 사람이 읽을 수 있어야 한다.** 옛 격자에서는 *"셀 열림/닫힘 · 간선
    // 이음/끊김"*을 찍었는데, 이제는 **그 자리에 덩어리가 몇 개고 이웃이 몇인지**가 답이다.
    public string Describe(Vector3 world)
    {
        if (_map == null) return "격자 없음";

        var t  = _map.WorldToTile(world);
        int cx = CellOf(t.x), cz = CellOf(t.y);

        int comps = 0;
        for (int k = 0; k < _maxComp; k++) if (_nodeTiles[NodeId(cx, cz, k)] > 0) comps++;

        if (!TryNodeAt(world, out int node, maxRadius: 0))
            return $"셀({cx},{cz}) 덩어리{comps} — **그 칸은 못 걷는다**";

        int links = Neighbors(node).Count;
        return $"셀({cx},{cz}) 덩어리{comps} 그중 {node % _maxComp}번(칸 {_nodeTiles[node]}) "
             + $"이웃 {links}{(IsBlocked(node) ? " · **막힘으로 배움**" : "")}";
    }
}
