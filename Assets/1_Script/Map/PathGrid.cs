using System.Collections.Generic;
using UnityEngine;

// 단일 A* 격자. L1(tileSize=1), L2(tileSize=100), L3(tileSize=1000) 공용.
public class PathGrid
{
    public readonly int     Width;
    public readonly int     Height;
    public readonly float   TileSize;
    public Vector3 Origin { get; private set; }   // 셀(0,0) 좌하단 월드 좌표

    // 같은 크기 격자를 다른 위치로 재사용(윈도우 풀링용). 이후 재베이크 필요.
    public void Rebase(Vector3 origin) => Origin = origin;

    readonly float[] _cost;       // 이동 비용 배율 (1.0 = 기본)
    readonly bool[]  _walkable;

    // -- 오르막 턱 -----------------------------------------------------------
    //
    // **높이차만으로는 비탈과 턱을 구별할 수 없다.** 20도 비탈도 1m당 0.36m라 기단 옆면과
    // 숫자가 같은데, 폰은 비탈은 걸어 오르고(`CharacterController.slopeLimit` 45도) 수직인
    // 턱은 `stepOffset`(0.3m)까지만 오른다. 그래서 **칸마다 "여기로 올라설 때 허용되는
    // 높이"를 따로 굽는다** — 바닥이 깔린 칸은 옆면이 수직이라 `stepOffset`, 자연 지형은
    // 비탈이라 훨씬 넉넉하다.
    //
    // 안 재면 폰이 기단 가장자리를 향해 걸어가 **벽에 부딪힌 채 영영 밀고 있는다**
    // (길찾기는 높이를 안 보므로 경로는 태연히 기단 위로 난다).
    //
    // **오르는 것과 내려가는 것은 다른 축이다**(2026-08-28). 사람은 무릎까지밖에 못
    // 올라가도 제 키만큼은 뛰어내린다.
    //
    // 두 번 헤맸다. 처음엔 내려가는 것을 **아예 열어** 뒀는데, 그러면 경로가 지형을
    // 무시하고 곧장 나서(*"여기서 뛰어내리면 된다"*) 폰이 그 직선 위의 벽을 밀고 섰다.
    // 그래서 **아예 막았더니** 이번엔 턱 하나 때문에 기단을 통째로 돌아 나갔다.
    //
    // 답은 비대칭이다 — 올라가는 한도는 칸이 들고(`_climb`), 내려가는 한도는 폰이 든다
    // (`MaxDrop`). 자잘한 턱은 그냥 내려가고 진짜 벼랑에서만 계단을 찾는다.
    readonly float[] _height;     // 그 칸의 딛는 면 높이(m)
    readonly float[] _climb;      // 그 칸으로 올라설 때 허용되는 높이차(m)

    public bool UsesHeight;       // 안 구우면 안 잰다(옛 평면 맵)
    public float MaxDrop;         // 뛰어내릴 수 있는 높이(m). 0이면 내려가는 것을 안 막는다

    // ── 지름길은 몸통 두께를 안다 ──────────────────────────────────────────
    //
    // Theta*는 부모에서 곧장 이을 수 있으면 잇는다(any-angle). 그런데 그 판정
    // (`LineOfSight`)은 **선이 지나는 칸만** 본다 — 선이 막힌 칸의 **모서리를 1cm 옆으로
    // 스쳐도** 통과다. 칸으로는 맞지만 **몸은 반지름만큼 두껍다.**
    //
    // 그래서 벽을 끼고 도는 길에서 경로가 모서리에 딱 붙어 꺾이고, 폰은 그 선을 따라가다
    // **모서리에 부딪혀 옆으로 삐져나온다.** 칸은 멀쩡히 이어져 있으니 길찾기 그림에는
    // 안 나오고, 벽을 아무리 얇게 만들어도 안 없어진다(선이 붙어 있는 한 늘 스친다).
    //
    // **부풀림은 지름길에만 건다** — 격자 간선(칸 중심을 따라가는 길)에는 안 건다.
    // 거기까지 부풀리면 **한 칸짜리 통로가 통째로 막힌다.** 벽 옆에서는 지름길을 포기하고
    // 칸을 따라 돌면 되고, 칸 중심은 벽 면에서 이미 반 칸 떨어져 있다.
    bool[] _tight;   // 이 칸이나 맞닿은 칸이 막혔나 — 지름길이 지나가면 안 되는 자리

    // 굽기가 끝난 뒤 한 번 부른다.
    public void BuildClearance()
    {
        _tight ??= new bool[Width * Height];

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width;  x++)
            _tight[y * Width + x] =
                !_walkable[y * Width + x] ||
                !IsWalkable(x + 1, y) || !IsWalkable(x - 1, y) ||
                !IsWalkable(x, y + 1) || !IsWalkable(x, y - 1);
    }

    // 지름길이 지나도 되는 칸인가. 부풀림을 안 구웠으면 옛 판정 그대로.
    bool LosOpen(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        return _tight == null ? _walkable[y * Width + x] : !_tight[y * Width + x];
    }

    // 계단 한 단은 **한도와 정확히 같게** 저작된다(`PawnMover.StepUp`) — 부동소수 오차로
    // 그 한 단이 막히면 계단이 통째로 못 쓰는 물건이 된다.
    const float ClimbSlack = 0.005f;

    // **벼랑 옆 칸은 비싸게 매긴다.**
    //
    // 폰의 캡슐 반경이 0.5인데 칸 중심에서 옆면까지도 0.5다 — 벽 옆 칸에 서면 몸이 정확히
    // 면에 닿아, 조향이 조금만 흔들려도 벽을 문지르며 느려진다. **막을 일은 아니다**(한 칸
    // 폭 통로는 지날 수 있어야 한다) — 다른 길이 있으면 그쪽으로 가게 **값만 올린다.**
    //
    // **오를 수 있는 턱은 벼랑이 아니다** — 계단 한 단은 정확히 한도만큼이라 여기 안 걸린다
    // (걸리면 계단이 통째로 비싸져서 폰이 계단을 피한다). 자연 비탈도 마찬가지다.
    //
    // **값은 매번 전부 다시 쓴다** — 격자를 자리만 옮겨 재사용하므로(`Rebase`), 안 쓰면
    // 지난번 자리의 벼랑이 여기 남는다.
    public void PenalizeLedges(float mult)
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width;  x++)
        {
            int i = y * Width + x;
            _cost[i] = UsesHeight && IsLedge(x, y, i) ? mult : 1f;
        }
    }

    // **벽에 몸을 문지르며 걷지 않게 한다**(2026-08-30).
    //
    // 지하에서는 길의 거의 전부가 벽에 닿아 있어서, 격자가 *벽 옆 칸*과 *한 칸 안쪽*을
    // 똑같이 치면 경로가 **모서리에 딱 붙어 꺾인다.** 폰은 그 선을 따라 걷다 ㄷ자 안쪽
    // 모서리에서 **면 사이로 끼어 들어간다**(바위 콜라이더는 드러난 면만 있는 속 빈
    // 껍데기라, 한 번 들어가면 밀어내는 힘이 없다 — `EscapeRock`이 꺼내도 같은 길로 다시
    // 들어가 무한히 되풀이된다).
    //
    // **막지 않고 값만 올린다** — 막으면 **한 칸짜리 통로를 못 지난다**(지름길 부풀림을
    // 격자 간선에 안 거는 것과 같은 이유). 그리고 좁은 굴에서는 **모든 칸이 벽에 닿아
    // 배율이 고르게 걸리므로 그 안의 선택은 하나도 안 바뀐다** — 넓은 데서만 안쪽으로 붙는다.
    //
    // **작게 잡는다** — 크게 잡으면 *"한 칸이라도 안쪽으로"*가 쌓여 경로가 통째로 부푼다
    // (`LedgeCost`를 1.6에서 1.15로 내린 그 사고).
    public void PenalizeWalls(float mult)
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width;  x++)
        {
            int i = y * Width + x;
            if (!_walkable[i]) continue;
            if (Open(x + 1, y) && Open(x - 1, y) && Open(x, y + 1) && Open(x, y - 1)) continue;

            // **곱한다** — 벼랑 값을 덮어쓰면 벼랑이면서 벽인 칸이 둘 중 하나만 반영된다.
            _cost[i] *= mult;
        }
    }

    // 격자 밖은 **열린 것으로 친다** — 창 가장자리를 벽으로 치면 로컬 격자의 테두리가
    // 통째로 비싸져서, 창을 옮길 때마다 경로가 흔들린다.
    bool Open(int x, int y) => !InBounds(x, y) || _walkable[y * Width + x];

    bool IsLedge(int x, int y, int i)
    {
        return Face(i, x + 1, y) || Face(i, x - 1, y) || Face(i, x, y + 1) || Face(i, x, y - 1);
    }

    // 이 경계에 **넘을 수 없는 수직 면**이 있나 — 올려다보든 내려다보든.
    bool Face(int i, int nx, int ny)
    {
        if (!InBounds(nx, ny)) return false;
        int j = ny * Width + nx;
        return _height[j] - _height[i] > _climb[j] + ClimbSlack
            || _height[i] - _height[j] > _climb[i] + ClimbSlack;
    }

    // `from`에서 `to`로 한 걸음 들어갈 수 있는가 — 막힘과 오르막을 **한 자리에서** 묻는다.
    // 이 물음이 두 군데로 갈리면 대각선 모서리 검사만 옛 규칙으로 남는다.
    // ── 칸과 칸 **사이** — 이어져 있나 ─────────────────────────────────────
    //
    // **벽은 칸 안이 아니라 칸 사이에 선다**(2026-08-31). 칸 하나의 열림만 구우면 A\*는
    // *"이웃한 열린 칸은 이어져 있다"*고 가정하는데, 셀이 크면 그 가정이 자주 거짓이다 —
    // 두 셀이 다 열려 있어도 **맞닿은 경계가 통째로 벽**일 수 있다.
    //
    // HPA\*에서 이걸 **entrance(transition)**라고 부른다: 클러스터마다 이웃과 맞닿은
    // 경계에서 *열린 구간*을 찾아 잇는 것이 알고리즘의 절반이다. 그 절반이 빠져 있었다.
    //
    // **안 구우면 예전 그대로다** — 로컬·코스 격자는 칸이 1~2m라 이 문제가 거의 없어서
    // 지금은 광역만 굽는다.
    bool[] _linkE;   // (x,y) ↔ (x+1,y)
    bool[] _linkN;   // (x,y) ↔ (x,y+1)

    public void EnsureLinks()
    {
        int n = Width * Height;
        if (_linkE == null || _linkE.Length != n) { _linkE = new bool[n]; _linkN = new bool[n]; }
    }

    public void SetLinkEast (int x, int y, bool open) { if (_linkE != null && InBounds(x, y)) _linkE[y * Width + x] = open; }
    public void SetLinkNorth(int x, int y, bool open) { if (_linkN != null && InBounds(x, y)) _linkN[y * Width + x] = open; }

    public bool LinkEast (int x, int y) => _linkE != null && InBounds(x, y) && _linkE[y * Width + x];
    public bool LinkNorth(int x, int y) => _linkN != null && InBounds(x, y) && _linkN[y * Width + x];

    // **대각선은 여기서 안 본다** — `FindPath`가 직교 두 이웃(`side1`·`side2`)을 따로
    // 검사하므로, 직교만 막아 두면 대각선도 따라 막힌다.
    // **간선을 무시하고 한 번 더 풀 수 있다**(2026-08-31). 간선 판정은 *더 정확한 답*이지만
    // **완벽하지는 않다** — 틀리면 갈 수 있는 길이 끊긴 것으로 보이고, 그건 라우트가 조금
    // 나쁜 것보다 훨씬 나쁘다(폰이 아예 못 간다). 그래서 **끊긴 답이 나오면 무시하고 다시
    // 풀어 본다**: 정확한 답을 먼저 쓰되, 그것이 세계를 끊으면 이어 두고 **폰이 걸어가며
    // 배우게 한다**(`ReportUnreachable`).
    public bool IgnoreLinks;

    bool Linked(int from, int to)
    {
        if (_linkE == null || IgnoreLinks) return true;

        int fx = from % Width, fy = from / Width;
        int tx = to   % Width, ty = to   / Width;
        int dx = tx - fx,      dy = ty - fy;

        if (dy == 0 && dx ==  1) return _linkE[from];
        if (dy == 0 && dx == -1) return _linkE[to];
        if (dx == 0 && dy ==  1) return _linkN[from];
        if (dx == 0 && dy == -1) return _linkN[to];
        return true;
    }

    bool Enterable(int from, int to)
    {
        if (!_walkable[to]) return false;
        if (!Linked(from, to)) return false;
        if (!UsesHeight) return true;

        // 올라갈 때는 **가는 칸**의 한도, 내려갈 때는 **떠나는 칸**의 한도(되돌아 올라올
        // 수 있어야 한다). `MapManager.SameLevel`과 같은 식이다 — 갈리면 *"경로는 있는데
        // 서 있을 자리가 없다"*가 된다.
        float d = _height[to] - _height[from];
        if (d > 0f) return  d <= _climb[to] + ClimbSlack;          // 오른다 — 칸이 정한다
        return MaxDrop <= 0f || -d <= MaxDrop + ClimbSlack;        // 내려간다 — 폰이 정한다
    }

    // 경로 계산 시 재사용 버퍼 (GC 절약)
    float[] _g, _f;
    int[]   _parent;
    bool[]  _inClose;

    // 이진 최소 힙 (open set). decrease-key 지원 인덱스 힙.
    int[] _heap;      // 힙 슬롯 → 노드 인덱스
    int[] _heapPos;   // 노드 인덱스 → 힙 슬롯 (-1 = 힙에 없음)
    int   _heapCount;

    static readonly (int dx, int dy)[] Dirs8 =
    {
        ( 1, 0), (-1, 0), ( 0, 1), ( 0,-1),
        ( 1, 1), ( 1,-1), (-1, 1), (-1,-1),
    };

    public PathGrid(int width, int height, float tileSize, Vector3 origin)
    {
        Width = width; Height = height; TileSize = tileSize; Origin = origin;
        int n    = width * height;
        _cost    = new float[n];
        _walkable = new bool[n];
        _height  = new float[n];
        _climb   = new float[n];
        _g       = new float[n];
        _f       = new float[n];
        _parent  = new int[n];
        _inClose = new bool[n];
        _heap    = new int[n];
        _heapPos = new int[n];
        for (int i = 0; i < n; i++) { _cost[i] = 1f; _walkable[i] = true; }
    }

    // ── 좌표 변환 ──────────────────────────────────────────────────────
    public Vector2Int WorldToCell(Vector3 w) =>
        new(Mathf.Clamp(Mathf.FloorToInt((w.x - Origin.x) / TileSize), 0, Width  - 1),
            Mathf.Clamp(Mathf.FloorToInt((w.z - Origin.z) / TileSize), 0, Height - 1));

    public Vector3 CellToWorld(Vector2Int c) =>
        new(Origin.x + (c.x + 0.5f) * TileSize,
            Origin.y,
            Origin.z + (c.y + 0.5f) * TileSize);

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public void SetSurface(int x, int y, float height, float climb)
    {
        int i = y * Width + x;
        _height[i] = height;
        _climb[i]  = climb;
    }

    public bool InWorldBounds(Vector3 w) =>
        w.x >= Origin.x && w.x < Origin.x + Width  * TileSize &&
        w.z >= Origin.z && w.z < Origin.z + Height * TileSize;

    // ── 타일 데이터 ─────────────────────────────────────────────────────
    public void  SetWalkable(int x, int y, bool w)     { if (InBounds(x,y)) _walkable[y*Width+x] = w; }
    public void  SetCost    (int x, int y, float cost) { if (InBounds(x,y)) _cost    [y*Width+x] = Mathf.Max(0.1f, cost); }
    public bool  IsWalkable (int x, int y) => InBounds(x,y) && _walkable[y*Width+x];
    public float GetCost    (int x, int y) => InBounds(x,y) ? _cost[y*Width+x] : float.MaxValue;

    // ── Theta* (any-angle) ─────────────────────────────────────────────
    // 표준 A*와 달리 가시선(LineOfSight)이 확보되면 부모를 건너뛰어 직선 경로를
    // 만든다. 결과는 계단식이 아닌 매끄러운 any-angle 웨이포인트.
    // 반환: 월드 좌표 웨이포인트 리스트 (시작점 제외, toWorld 포함). 경로 없으면 null.
    public List<Vector3> FindPath(Vector3 fromWorld, Vector3 toWorld)
    {
        var start = WorldToCell(fromWorld);
        var end   = WorldToCell(toWorld);
        if (start == end) return new List<Vector3> { toWorld };
        // 시작 셀이 막혀도(반경 부풀림으로 벽에 붙은 경우 등) 탈출은 허용. 도착 셀만 검사.
        // 도착 셀이 막힘(공허/벽 부풀림) — 근처 walkable 셀로 스냅해 접근 경로를 만든다.
        // 코스 웨이포인트(2m 셀 중심)가 좁은 바닥 다리 옆 공허 타일에 앉는 경우가 대표 사례.
        if (!IsWalkable(end.x, end.y))
        {
            var snapped = FindNearestWalkable(end, 4);
            if (snapped != null)
            {
                end     = snapped.Value;
                toWorld = CellToWorld(end);
                if (start == end) return new List<Vector3> { toWorld };
            }
            // **못 찾아도 여기서 포기하지 않는다**(2026-08-31). 전에는 `null`이었고, 그것이
            // **08-28에 세운 규칙("길이 없으면 가장 가까이까지라도 간다 — `null`은 한 발짝도
            // 못 갈 때만")을 어기고 남아 있던 자리**다.
            //
            // 어떻게 드러났나: 로컬 윈도우(40m)는 **먼 목표를 창 가장자리로 접어서**
            // 겨눈다(`ClampInsideGrid`). 지하에서 그 직선 투영은 **거의 늘 바위 속**이고,
            // 둘레 네 칸도 바위면 여기서 `null`이 나왔다. 그러면 `ReplanLocal`이
            // **조향 목표를 제자리로** 두고 `AdvanceWaypoint`가 조용히 멈춘다 —
            // 화면에는 *"길이 뻔히 있는데 가다 말고 서고, 다시 찍어도 꿈쩍 안 하고,
            // 포기 메시지도 안 뜬다"*로만 보인다(노란 공이 떴다가 그 프레임에 사라진다).
            //
            // 못 걷는 칸을 그대로 목표로 두면 `ei`는 영영 안 뽑히고, 탐색이 끝난 뒤
            // **가장 가까이 닿은 칸까지의 부분 경로**가 나온다. 값은 격자를 한 번 다 뒤지는
            // 것인데, 그건 *갈 수 없는 곳을 겨눴을 때만* 치른다.
        }

        int n = Width * Height;

        // 버퍼 초기화
        for (int i = 0; i < n; i++)
        {
            _g[i] = float.MaxValue; _parent[i] = -1;
            _heapPos[i] = -1; _inClose[i] = false;
        }
        _heapCount = 0;

        int si = start.y * Width + start.x;
        int ei = end.y   * Width + end.x;

        // **못 닿으면 가장 가까이까지라도 간다.**
        //
        // 전에는 `null`을 돌려줬고, 부르는 쪽(`PawnMover.ReplanLocal`)이 그걸 받으면
        // **조향 목표로 직선 폴백**을 했다 — 길이 없는데 곧장 걸어가는 것이라, 기단 위를
        // 클릭하면 폰이 **옆면에 붙어 영영 벽을 밀고 있었다.** 갈 수 있는 데까지 가서
        // 서는 편이 언제나 낫다(계단 앞까지는 간다).
        int best  = si;
        float bestH = Heuristic(start, end);

        _g[si]      = 0f;
        _f[si]      = Heuristic(start, end);
        _parent[si] = si;            // 시작점의 부모는 자기 자신
        HeapPush(si);

        while (_heapCount > 0)
        {
            int cur = HeapPopMin();
            _inClose[cur] = true;

            if (cur == ei) return BuildPath(ei, toWorld);

            float h = Heuristic(new Vector2Int(cur % Width, cur / Width), end);
            if (h < bestH) { bestH = h; best = cur; }

            int cx = cur % Width, cy = cur / Width;

            foreach (var (dx, dy) in Dirs8)
            {
                int nx = cx + dx, ny = cy + dy;
                if (!InBounds(nx, ny)) continue;
                int ni = ny * Width + nx;
                if (_inClose[ni] || !Enterable(cur, ni)) continue;

                // **대각선은 두 직교 이웃이 다 열려 있을 때만 간다.**
                //
                // 안 그러면 격자에서는 통과인데 **몸은 두 면 사이에 낀다.** 기단 옆면과
                // 계단이 만나는 사거리가 딱 그 자리다 — 옆에서 계단으로 붙을 때 폰이
                // 모서리에 걸려 엉거주춤한 것이 이것이었다. 벽은 반경 부풀림(`clearance`)이
                // 막아 주지만 **높이로 막은 면은 부풀림이 없어** 여기서 걸러야 한다.
                if (dx != 0 && dy != 0)
                {
                    int side1 = cy * Width + (cx + dx);
                    int side2 = (cy + dy) * Width + cx;
                    if (!Enterable(cur, side1) || !Enterable(cur, side2)) continue;
                }

                UpdateVertex(cur, ni, nx, ny, end);
            }
        }

        // 다 뒤졌는데 못 닿았다 — 가장 가까이 간 자리까지의 경로를 준다.
        // **시작 자리면 아무것도 안 준다**(그건 한 발짝도 못 간다는 뜻이고, 그때
        // 직선 폴백으로 흘려보내면 다시 벽을 민다).
        return best != si ? BuildPath(best, CellToWorld(new Vector2Int(best % Width, best / Width)))
                          : null;
    }

    // Theta* 정점 완화. cur의 부모에서 ni가 보이면 부모를 직접 연결(Path 2),
    // 아니면 표준 A* 간선(Path 1).
    void UpdateVertex(int cur, int ni, int nx, int ny, Vector2Int end)
    {
        int par  = _parent[cur];
        int px   = par % Width, py = par / Width;

        float ng;
        int   nparent;

        // Path 2: 부모 → ni 직선 가시선 확보 시
        if (LineOfSight(px, py, nx, ny) && ClimbOk(px, py, nx, ny))
        {
            ng      = _g[par] + EuclidWorld(px, py, nx, ny) * _cost[ni];
            nparent = par;
        }
        else // Path 1: 표준 격자 간선
        {
            int cx = cur % Width, cy = cur / Width;
            ng      = _g[cur] + EuclidWorld(cx, cy, nx, ny) * _cost[ni];
            nparent = cur;
        }

        if (ng >= _g[ni]) return;

        _g[ni]      = ng;
        _f[ni]      = ng + Heuristic(new Vector2Int(nx, ny), end);
        _parent[ni] = nparent;

        // g가 감소했으므로 힙에 있으면 sift-up(decrease-key), 없으면 새로 push.
        if (_heapPos[ni] >= 0) HeapSiftUp(_heapPos[ni]);
        else                   HeapPush(ni);
    }

    // ── 이진 최소 힙 (키 = _f) ──────────────────────────────────────────
    void HeapPush(int node)
    {
        int i = _heapCount++;
        _heap[i] = node;
        _heapPos[node] = i;
        HeapSiftUp(i);
    }

    int HeapPopMin()
    {
        int root = _heap[0];
        _heapPos[root] = -1;
        int last = --_heapCount;
        if (last > 0)
        {
            int moved = _heap[last];
            _heap[0] = moved;
            _heapPos[moved] = 0;
            HeapSiftDown(0);
        }
        return root;
    }

    void HeapSiftUp(int i)
    {
        int node = _heap[i];
        float f  = _f[node];
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            int pNode  = _heap[parent];
            if (_f[pNode] <= f) break;
            _heap[i] = pNode; _heapPos[pNode] = i;
            i = parent;
        }
        _heap[i] = node; _heapPos[node] = i;
    }

    void HeapSiftDown(int i)
    {
        int node = _heap[i];
        float f  = _f[node];
        int half = _heapCount >> 1;   // 마지막 내부 노드까지
        while (i < half)
        {
            int child  = (i << 1) + 1;
            int right  = child + 1;
            int cNode  = _heap[child];
            if (right < _heapCount && _f[_heap[right]] < _f[cNode])
            { child = right; cNode = _heap[right]; }
            if (_f[cNode] >= f) break;
            _heap[i] = cNode; _heapPos[cNode] = i;
            i = child;
        }
        _heap[i] = node; _heapPos[node] = i;
    }

    // 두 셀 중심 사이 월드 거리.
    float EuclidWorld(int ax, int ay, int bx, int by)
    {
        float dx = (ax - bx) * TileSize;
        float dy = (ay - by) * TileSize;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // Bresenham 기반 가시선 검사. 두 셀을 잇는 선이 통과하는 모든 셀이
    // 이동 가능하면 true. (Theta* 표준 LineOfSight)
    // 선분이 오르막 턱을 넘지 않는가. **`LineOfSight`와 따로 훑는다** — 저쪽은 슈퍼커버라
    // 이웃 칸을 겹쳐 보는데, 높이는 **지나가는 순서**가 있어야 잰다.
    //
    // 여기서 막히면 지름길만 없어지고 표준 A* 간선으로 떨어진다 — 그쪽이 칸 단위로 같은
    // 규칙을 다시 재므로 **보수적으로 굴어도 길을 잃지 않는다.**
    bool ClimbOk(int x0, int y0, int x1, int y1)
    {
        if (!UsesHeight) return true;

        // **한 축씩 밟는다**(맨해튼) — 대각선으로 건너뛰면 모서리 칸을 안 보고 지나가서,
        // 격자 간선에서 막은 그 사거리를 **지름길이 그대로 통과해 버린다.**
        int steps = Mathf.Abs(x1 - x0) + Mathf.Abs(y1 - y0);
        if (steps == 0) return true;

        float prev = _height[y0 * Width + x0];
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            int ci = cy * Width + cx;

            float d = _height[ci] - prev;
            if (d > 0f)
            {
                if (d > _climb[ci] + ClimbSlack) return false;
            }
            else if (MaxDrop > 0f && -d > MaxDrop + ClimbSlack) return false;

            prev = _height[ci];
        }
        return true;
    }

    bool LineOfSight(int x0, int y0, int x1, int y1)
    {
        int dx = x1 - x0, dy = y1 - y0;
        int sx, sy, f = 0;
        if (dy < 0) { dy = -dy; sy = -1; } else sy = 1;
        if (dx < 0) { dx = -dx; sx = -1; } else sx = 1;

        int ox = (sx - 1) / 2;   // sx=1→0, sx=-1→-1
        int oy = (sy - 1) / 2;

        if (dx >= dy)
        {
            while (x0 != x1)
            {
                f += dy;
                if (f >= dx)
                {
                    if (!LosOpen(x0 + ox, y0 + oy)) return false;
                    y0 += sy;
                    f  -= dx;
                }
                if (f != 0 && !LosOpen(x0 + ox, y0 + oy)) return false;
                if (dy == 0 && !LosOpen(x0 + ox, y0) && !LosOpen(x0 + ox, y0 - 1)) return false;
                x0 += sx;
            }
        }
        else
        {
            while (y0 != y1)
            {
                f += dx;
                if (f >= dy)
                {
                    if (!LosOpen(x0 + ox, y0 + oy)) return false;
                    x0 += sx;
                    f  -= dy;
                }
                if (f != 0 && !LosOpen(x0 + ox, y0 + oy)) return false;
                if (dx == 0 && !LosOpen(x0, y0 + oy) && !LosOpen(x0 - 1, y0 + oy)) return false;
                y0 += sy;
            }
        }
        return true;
    }

    // 막힌 셀 주변을 링(체비쇼프 거리)으로 확장하며 가장 가까운 walkable 셀 탐색.
    //
    // **부풀림(`LosOpen`)이 아니라 `IsWalkable`로 고른다**(2026-08-31에 바로잡았다).
    // 부풀림은 **지름길이 지나도 되는가**에 답하는 값이고, 이 함수가 묻는 것은 **거기 설 수
    // 있는가**다. 부풀림으로 고르면 **벽 옆 칸이 전부 후보에서 빠져** 한 칸짜리 통로 안의
    // 목적지를 영영 못 잡는다 — *"격자 간선에는 부풀림을 절대 안 건다"*는 규칙과 같은 자리다.
    Vector2Int? FindNearestWalkable(Vector2Int c, int maxRadius)
    {
        for (int r = 1; r <= maxRadius; r++)
        {
            Vector2Int? best  = null;
            int         bestD = int.MaxValue;
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;   // 링 껍질만
                if (!IsWalkable(c.x + dx, c.y + dy)) continue;
                int d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = new Vector2Int(c.x + dx, c.y + dy); }
            }
            if (best != null) return best;
        }
        return null;
    }

    List<Vector3> BuildPath(int endIdx, Vector3 exactEnd)
    {
        var result = new List<Vector3>();
        int cur = endIdx;
        // 시작점은 부모가 자기 자신. 시작점에 도달하면 멈춰 시작점은 제외한다.
        while (cur != _parent[cur])
        {
            result.Add(CellToWorld(new Vector2Int(cur % Width, cur / Width)));
            cur = _parent[cur];
        }
        result.Reverse();
        if (result.Count > 0) result[result.Count - 1] = exactEnd;
        else result.Add(exactEnd);
        return result;
    }

    float Heuristic(Vector2Int a, Vector2Int b)
    {
        // any-angle(Theta*)에서는 직선거리가 최단이므로 유클리드가 admissible.
        float dx = (a.x - b.x) * TileSize;
        float dy = (a.y - b.y) * TileSize;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
