using System.Collections.Generic;
using UnityEngine;

// 계층형 길찾기 관리자 — **HPA\*의 abstract level 구조**(Botea 외, 2004).
//
// ── 층은 둘이다 (2026-09-09) ────────────────────────────────────────────────
//
//   `_graph`   세계 전체. **셀 안의 연결 덩어리마다 노드 하나**(HPA\* entrance) — 복도 안내.
//   `_local`   1m 유닛 둘레 창. 정밀 회피와 마지막 구간.
//
// 그 사이에 **코스 격자(2m · 핵 둘레 160m)가 하나 더 있었다** — 지형이 한 장이던 시절에
// 광역 격자가 죽어 있어서 실제 복도 안내를 맡던 층이다. 광역이 1m 사실로 세계 전체를
// 답하게 된 뒤(09-05, `WideGraph`)로는 **같은 물음에 답하는 두 번째 층**이었고, 남은 것은
// 그 층에만 딸린 장치들이었다: 물리 `CheckBox` 굽기 · 3초 재중심 루프 · 웨이포인트를
// 벽에서 떼는 손질(`OpenSpotNear`) · `Terrain.activeTerrain` 한 장으로 재던 경사(스트리밍에서
// 틀렸다). 재굽기 진입점 셋이 두 구조를 같이 유지해야 했다. 걷어냈다.
//
// `PawnMover`가 `ComputeCoarseRoute`(광역 라우트) + `FindLocalPath`(1m 창)로 쓴다 —
// 이름의 *coarse*는 *로컬이 아닌 쪽*이라는 뜻으로 남는다.
[DefaultExecutionOrder(-100)]
public class HierarchicalPathfinder : MonoBehaviour
{
    public static HierarchicalPathfinder Instance { get; private set; }

    [Header("로컬 윈도우 (유닛 중심 정밀 회피)")]
    [Tooltip("유닛 주변 1m 격자 윈도우 한 변 크기(m). 클수록 멀리 보지만 베이크 비용↑")]
    [SerializeField] int localWindowSize = 40;

    [Header("에이전트")]
    [Tooltip("유닛 반경(m). 장애물을 이만큼 부풀려 베이크 → 좁은 틈은 못 지나가게/벽에 안 끼이게. CharacterController 반경에 맞춤")]
    [SerializeField] float agentRadius = 0.5f;

    [Header("장애물 레이어")]
    [SerializeField] LayerMask obstacleLayer = ~0;

    // 무엇이 벽으로 취급되는가 — 자기 콜라이더가 여기 걸리는지 알아야 하는 쪽(핵 등)이 읽는다.
    public LayerMask ObstacleLayer => obstacleLayer;

    // **광역은 이제 격자가 아니라 그래프다**(2026-09-05, `WideGraph`) — 셀 하나에 노드
    // 하나가 아니라 **셀 안의 연결 덩어리마다 노드 하나**다.
    //
    // **옛 100m 격자(`_wide`)는 2026-09-06에 걷어냈다** — 검증이 끝났으므로. 그때 같이
    // 사라진 것: 표본 문턱(`WideMinOpen`·`WideSamples`) · 경계 간선(`PathGrid`의 링크와
    // `IgnoreLinks`) · 셀 단위 차단(`_wideBlocked`). 전부 **셀 하나에 노드 하나**라는
    // 모양을 다이얼로 떠받치던 장치였고, 덩어리가 노드가 되면서 물음 자체가 없어졌다.
    WideGraph        _graph;
    LocalGridManager _local;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        InitGrids();
        SubscribeEvents();
    }

    void OnDestroy()
    {
        UnsubscribeEvents();
    }

    // ── 초기화 ─────────────────────────────────────────────────────────

    void InitGrids()
    {
        BuildWide();

        // 로컬 윈도우 패스파인더
        _local = new LocalGridManager();
        _local.Initialize(obstacleLayer, _graph);

        string wide = _graph != null
            ? $"그래프 {_graph.Cells}×{_graph.Cells}셀(칸 {_graph.CellSize:0}m)"
            : "**없음**";
        Debug.Log($"[HierarchicalPathfinder] 초기화 완료 — 광역:{wide} · 로컬 창 {localWindowSize}m");
    }

    // ── 광역 격자 — 세계 전체 ───────────────────────────────────────────────
    //
    // **되살렸다**(2026-08-30). 지형이 한 장이던 시절의 코드가 그대로 남아 있어서 스트리밍
    // 세계에서는 **사실상 죽어 있었다**: `Terrain.activeTerrain`이 **청크 하나**(64m)를 집고,
    // 100m 셀로 나누면 **1×1 격자**가 되며, 45m짜리 `CheckBox`는 바위에 반드시 걸려 **그 한
    // 칸마저 막힘**으로 구워졌다. 그러니 `FindPath`가 늘 null이고, 코스 범위 밖을 찍으면
    // 경로가 **직선 한 방**으로 떨어졌다 — 굴에서 그건 곧 벽에 처박는다는 뜻이다.
    //
    // 고친 것 셋:
    //
    //  · **지형 오브젝트가 아니라 칸이 답한다**(`MapManager.IsWorkable`) — 스트리밍이라
    //    `Terrain`은 로드된 것만 있고, 바위는 애초에 `Terrain`이 모른다.
    //  · **세계 전체를 덮는다** — 어디를 찍어도 진짜 경로가 나야 한다.
    //  · **셀 크기를 셀 수에서 낸다** — 세계를 키워도 값이 폭발하지 않는다. 256m면 4m 셀에
    //    64×64, 1,024m면 6.4m 셀에 160×160이다. **세계 크기를 손잡이 하나로 두는 규칙**이
    //    여기에도 걸린다(개수를 밀도로 적은 것과 같은 자리).
    //
    // **셀 안을 전부 훑지 않는다** — 8m 셀이면 64칸이고, 칸마다 여유를 다시 뽑아야 한다.
    // 3×3로 성기게 찍는다: *"하나라도 지날 수 있으면 열어 둔다 — 정밀 차단은 로컬 몫"*이고,
    // 놓친 좁은 굴은 로컬이 찾아 준다.
    const int   WideMaxCells = 160;   // 한 변의 셀 수 천장
    const float WideMinCell  = 4f;    // 셀이 이보다 잘면 굽는 값만 늘고 얻는 것이 없다

    float _wideCell;

    void BuildWide()
    {
        // **`MapManager`가 없으면 광역이 없다**(2026-09-06). 그래프는 *칸이 걸을 수 있나*를
        // 물어서 짓는데 물어볼 데가 없기 때문이다. 그러면 라우트가 *목적지 하나*로 떨어져
        // 로컬 창 밖의 목적지는 직선으로 겨눈다 — 조용히 그러면 원인이 안 보이므로 말한다.
        var map = MapManager.Instance;
        if (map == null)
        {
            Debug.LogWarning("[길찾기] MapManager가 없다 — 광역 그래프를 못 짓는다. " +
                             "로컬 창(40m) 밖으로 나가는 경로는 직선으로 떨어진다.");
            return;
        }

        // **셀은 칸의 정수배다**(2026-09-05). 그래프가 셀 안을 1m로 훑으므로, 셀 한 변이
        // 6.4m 같은 소수면 **셀마다 칸 수가 6이었다 7이었다** 해서 색인이 어긋난다.
        float world = TerrainGrid.WorldSize;
        int cellTiles = Mathf.Max(Mathf.CeilToInt(WideMinCell),
                                  Mathf.CeilToInt(world / WideMaxCells));
        _wideCell = cellTiles;

        _graph = new WideGraph(Mathf.RoundToInt(world), cellTiles);
        _graph.Build(map);
    }

    // ── 광역이 거짓말을 했을 때 ─────────────────────────────────────────────
    //
    // 광역 셀은 **3×3로 성기게 찍고 하나라도 지날 수 있으면 열어 둔다** — 그게 계층을 두는
    // 값이고, *"정밀 차단은 로컬 몫"*이라는 규칙이다. 그런데 그 규칙에는 **짝이 빠져 있었다**:
    // 로컬이 못 뚫었을 때 **광역이 그 사실을 배우는 길**이 없었다.
    //
    // 그래서 이렇게 됐다(2026-08-31): 6.4m 셀 안의 **1m짜리 구석 하나**가 열려 있으면 셀이
    // 열린 것으로 구워지고, 라우트가 그 셀을 지나간다. 폰은 거기까지 **정직하게 걸어가서**
    // 벽 앞에 선다 — 그 자리에서는 갈 수 있는 어떤 칸도 목표에 더 가깝지 않으므로 로컬
    // 경로가 아예 안 나오고, 조용히 포기한다. 사용자의 말 그대로다: *"막혀 있는 길을 가놓고
    // 갈 수 있는 길이라고 착각하고 있다가 막상 앞에 벽을 보니까 멈춘다."*
    //
    // **`PathBlockMemory`로는 안 된다** — 그건 로컬 베이크만 읽는다. 광역이 낸 거짓말은
    // **광역 눈금으로 기억해야** 다음 라우트가 그 셀을 피한다.
    //
    // **잠시만 닫는다**(`PathBlockMemory`와 같은 뜻) — 판 자리가 늘어나면 그 셀이 진짜로
    // 열릴 수 있고, 영구히 닫으면 **한 폰이 한 번 실패한 것으로 세계가 좁아진다.**
    const float WideBlockDuration = 20f;

    // **덩어리 하나만 닫는다** — 셀을 통째로 닫으면 그 셀의 멀쩡한 다른 덩어리까지 같이
    // 닫힌다(옛 격자에는 그 구별이 없어서 그렇게 할 수밖에 없었다).
    public void ReportUnreachable(Vector3 world) => _graph?.BlockAt(world, WideBlockDuration);

    // **광역이 그 칸을 걸을 수 있다고 보나** — 진단이 쓴다(`RockMap.VerifyOpened`). 그래프가
    // 없으면 참으로 답한다(없는 것을 막힌 것으로 치면 없는 사고를 쫓게 된다).
    public bool WideWalkable(Vector2Int tile) => _graph == null || _graph.WalkableTile(tile.x, tile.y);

    // ── 공개 길찾기 API ────────────────────────────────────────────────

    // 광역 라우트 — *어느 복도로 갈지*의 안내. 도착점이 아니다(마지막 구간은 `PawnMover`가
    // `_finalGoal`을 직접 겨누고 로컬 창이 잇는다).
    public List<Vector3> ComputeCoarseRoute(Vector3 from, Vector3 to)
    {
        // **웨이포인트를 벽에서 떼지 않는다.** 그래프의 웨이포인트는 **경계의 통과점**이고 그
        // 자리는 양쪽 칸이 둘 다 걸을 수 있다고 이미 확인된 곳이다 — 벽에서 떼겠다고 밀면
        // **한 칸짜리 통로의 통과점이 벽 속으로 밀려난다**(옛 코스 격자에서는 셀 중심이 벽에
        // 붙을 수 있어 `OpenSpotNear`가 필요했다 — 격자와 함께 지웠다).
        //
        // **간선 무시 재시도도 없다.** 그건 *셀 열림*과 *경계 이음*이 따로 구워져 서로
        // 거짓말을 하던 시절의 장치인데, 이제 **열림과 연결이 같은 1m 사실에서 나온다.**
        // 부분 경로는 `WideGraph`가 스스로 낸다(*갈 수 있는 데까지*).
        if (_graph != null)
        {
            var route = _graph.FindRoute(from, to);
            if (route != null && route.Count > 0)
            {
                var last = route[route.Count - 1];
                float ddx = last.x - to.x, ddz = last.z - to.z;
                if (ddx * ddx + ddz * ddz > _wideCell * _wideCell * 2.25f) WarnPartialWideRoute(last, to);

                return Densify(route, from);
            }

            WarnNoWideRoute(from, to);
            return new List<Vector3> { to };
        }

        // **여기 오면 광역까지 실패한 것이다** — 라우트가 *목적지 하나*로 떨어지고, 그러면
        // 조향 목표가 수백 미터 밖이라 폰이 사실상 못 움직인다. 조용히 넘어가면 그 사실이
        // 진단에 **"라우트 1점"**으로만 남아, 원인이 *격자가 끊겼나*인지 *출발 셀이 닫혔나*
        // 인지를 화면에서 가릴 수가 없다.
        WarnNoWideRoute(from, to);
        return new List<Vector3> { to };
    }

    float _nextWideWarn;
    float _nextPartialWarn;

    // 라우트가 중간에 끝났다 — **그 자리에 덩어리가 몇이고 이웃이 몇인가**를 찍는다
    // (`WideGraph.Describe`). 이웃이 0이면 그 덩어리가 **섬**이고, 그건 진짜 막다른 길이거나
    // 아직 안 판 벽이다. **부분 경로 자체는 정상 동작이다**(*갈 수 있는 데까지 간다*) —
    // 가려야 하는 것은 그것이 **세계의 문제인지 그래프의 문제인지**다.
    //
    // **진단은 남긴다** — 값이 싸고, 08-31에 이 사슬을 푼 것이 이 둘이다.
    void WarnPartialWideRoute(Vector3 end, Vector3 goal)
    {
        if (_graph == null || Time.time < _nextPartialWarn) return;
        _nextPartialWarn = Time.time + 3f;

        Debug.LogWarning(
            $"[길찾기] 광역 라우트가 중간에 끝났다 — 끝 {_graph.Describe(end)} · " +
            $"목표 {_graph.Describe(goal)} · 셀 {_wideCell:0}m");
    }

    void WarnNoWideRoute(Vector3 from, Vector3 to)
    {
        if (_graph == null || Time.time < _nextWideWarn) return;
        _nextWideWarn = Time.time + 3f;

        Debug.LogWarning(
            $"[길찾기] 광역 경로 없음 — 출발 {_graph.Describe(from)} · " +
            $"도착 {_graph.Describe(to)} · 셀 {_wideCell:0}m");
    }

    // ── 웨이포인트 사이는 로컬 창보다 좁아야 한다 ───────────────────────────
    //
    // **Theta\*는 any-angle이라 웨이포인트가 몇 개 안 나온다**(그게 목적이다 — 계단식
    // 경로를 없앤다). 그런데 그 결과 **한 구간이 수백 미터**가 될 수 있고, 그러면
    // `PawnMover`의 조향 목표가 **로컬 창(40m) 밖**에 떨어진다.
    //
    // 창 밖 목표는 `LocalGridManager`가 **창 가장자리로 접어서** 겨눈다 — 목표를 향한
    // *직선 투영*이다. 지하에서 그 직선은 거의 늘 바위 속이라, 창 안에서 그보다 가까워지는
    // 칸이 하나도 없으면 로컬 경로가 통째로 안 나온다. 화면에는 *"길이 뻔히 있는데 폰이
    // 안 간다"*로만 보이고, 진단에는 **`선 자리 통행가능 · 앞을 막은 것 없음 · 로컬경로
    // 없음`**이라는 모순처럼 읽히는 세 줄이 남는다(2026-08-31에 실제로 그렇게 나왔다:
    // 548m 목표에 웨이포인트가 하나였다).
    //
    // **답은 라우트 쪽이다** — 창을 키우면 굽는 값이 제곱으로 늘고, 조향만 손보면 라우트가
    // 여전히 *어느 복도로 갈지*를 말해 주지 못한다. 긴 구간을 잘라 **웨이포인트를 늘 창
    // 안에** 둔다. 자르는 자리는 광역이 이미 걸을 수 있다고 답한 직선 위라 새 정보를
    // 지어내지 않는다.
    const float RouteStepMax = 12f;   // 로컬 창 반쪽(20m)보다 넉넉히 작게

    static List<Vector3> Densify(List<Vector3> route, Vector3 from)
    {
        if (route == null || route.Count == 0) return route;

        var dense = new List<Vector3>(route.Count * 2);
        Vector3 prev = from;

        foreach (var p in route)
        {
            float dx = p.x - prev.x, dz = p.z - prev.z;
            float d  = Mathf.Sqrt(dx * dx + dz * dz);

            int steps = Mathf.FloorToInt(d / RouteStepMax);
            for (int i = 1; i <= steps; i++)
            {
                float t = i * RouteStepMax / d;
                if (t >= 0.999f) break;          // 끝점은 아래에서 그대로 넣는다
                dense.Add(Vector3.Lerp(prev, p, t));
            }

            dense.Add(p);
            prev = p;
        }
        return dense;
    }

    // ── 굴을 따라 가장 먼 곳 ────────────────────────────────────────────────
    //
    // **지하에서 "맵 가장자리"는 가장 긴 굴의 끝이다**(2026-08-30).
    //
    // 밖에서 오는 것들(상인·습격·지원군)은 `EventManager.WalkToEdge`가 정한 자리에서 나오는데,
    // 그건 **직선**으로 걷다 첫 바위에서 멈춘다 — 지상에서는 어느 방향이든 가장자리까지
    // 뚫려 있어 맞았지만, 지하에서는 **모퉁이를 못 돌아** 대부분의 방향이 몇 m 앞에서 끝난다.
    // 그래서 상인이 기지 코앞의 벽에서 튀어나왔다.
    //
    // **광역 그래프의 1m 걸음 표 위에서 훑는다**(2026-09-09 — 그전에는 코스 격자였다).
    // 폰이 실제로 걷는 사실이라 **갈 수 있는 곳임이 보장되고**, 이미 담겨 있어 새로 잴 것이
    // 없다(반경 150m면 7만 칸인데 바이트 배열을 읽는 것뿐이라 몇 ms이고, 스폰은 드물다).
    // 1m라 **뽑힌 칸이 곧 설 수 있는 칸**이다 — 코스 셀은 한복판이 바위일 수 있어 따로 옮겨야 했다.
    //
    // **고르는 것은 "가장 멀리 떨어진 칸"이지 "가장 오래 걸어야 하는 칸"이 아니다** — 걸음
    // 수로 고르면 기지 옆의 구불구불한 막다른 굴이 뽑힌다. 우리가 원하는 것은 *멀리서
    // 온다*이므로 **직선 거리로 재되 후보는 걸어갈 수 있는 칸뿐**이다.
    bool[] _bfsSeen;
    int[]  _bfsQueue;

    // **"가장 먼 곳"이 아니라 "충분히 먼 곳 중 하나"다**(2026-09-08에 바꿨다 — 사용자:
    // *"적이 항상 북동쪽에서만 나와"*).
    //
    // 가장 먼 칸은 **자리가 하나**라, 굴이 안 바뀌는 동안 습격도 상인도 **매번 같은 데서**
    // 나온다. 그러면 *어느 쪽을 막을까*가 결정이 아니게 된다 — 알림이 8방위를 알리는 이유가
    // 바로 그건데, 방향이 상수면 그 한 곳만 지켜 놓고 판이 끝난다.
    //
    // 후보가 하나도 없으면 예전처럼 **가장 먼 칸**으로 떨어진다 — 좁은 굴에서 습격이 통째로
    // 멎는 것이 한쪽에서만 오는 것보다 나쁘다.
    //
    // **한 자리를 묻는 것은 여러 자리를 묻는 것의 특수한 경우다**(2026-09-09) — 아래
    // `PickReachableSpread`가 답하고 한 자리가 필요한 쪽은 `1`을 넘겨 그중 하나를 꺼낸다.
    // 갈라 두면 *상인이 나오는 자리*와 *습격이 나오는 자리*가 두 벌이 되어, 한쪽만 고치는
    // 날이 반드시 온다(한 자리짜리 문은 그래서 없앴다).

    // ── 여러 방향에서 온다 (2026-09-09) ─────────────────────────────────────
    //
    // **포위는 *어디에 서나*가 아니라 *어느 쪽으로 접근하나*다** — 그래서 진형이 아니라
    // 여기서 시작한다. 가장자리를 하나만 고르는 동안에는 습격이 몇 명이든 **한 줄기**라,
    // 방어선을 한 곳에 세워 두면 판이 끝났다.
    //
    // **한 번의 BFS로 8방위를 한꺼번에 뽑는다** — 방향마다 따로 훑으면 굴을 여덟 번 훑게
    // 되고(반경 150m면 그것만으로 프레임을 먹는다), 무엇보다 **같은 굴을 여덟 번 훑어도
    // 답은 한 벌**이다. 훑는 김에 **방위마다 저수지 표본**을 하나씩 든다.
    //
    // **고르는 것은 서로 먼 방위다** — 아무렇게나 `count`개를 뽑으면 북과 북동이 같이
    // 뽑혀 **두 갈래가 나란히 걸어온다**(그건 포위가 아니라 그냥 한 무리다). 처음 하나를
    // 굴리고, 그다음부터는 **이미 고른 것들에서 가장 먼 방위**를 집는다.
    //
    // **모자라면 있는 만큼만 돌려준다** — 굴이 좁아 방위가 하나뿐이면 갈래도 하나다.
    // 여기서 포기하면 *좁은 굴에서는 습격이 안 온다*가 되는데, 그건 한쪽에서만 오는 것보다
    // 훨씬 나쁘다(`TryFindEdgeSpawn`이 직선 방식으로 떨어지는 것과 같은 규칙).
    public int PickReachableSpread(Vector3 from, float maxRadius, float minRadius,
                                   int count, List<Vector3> into)
    {
        if (into == null) return 0;
        into.Clear();
        count = Mathf.Max(1, count);

        var map = MapManager.Instance;
        if (_graph == null || map == null) return 0;

        var origin = map.WorldToTile(from);
        if (!_graph.WalkableTile(origin.x, origin.y)) return 0;

        // 창은 `from` 둘레 (2R+1)² 칸이고 창 좌표로 색인한다 — 세계 전체 크기의 표를 매번
        // 지우지 않기 위해서다.
        int r = Mathf.Max(1, Mathf.CeilToInt(maxRadius));
        int w = r * 2 + 1, n = w * w;
        if (_bfsSeen == null || _bfsSeen.Length != n) { _bfsSeen = new bool[n]; _bfsQueue = new int[n]; }
        System.Array.Clear(_bfsSeen, 0, n);

        int head = 0, tail = 0;
        int si = r * w + r;                        // 창 한복판 = origin
        _bfsSeen[si] = true;
        _bfsQueue[tail++] = si;

        float maxSqr = maxRadius * maxRadius;
        float minSqr = minRadius * minRadius;
        float bestSqr = -1f;
        int   best    = si;

        // 방위마다 저수지 표본 하나. `-1`은 그쪽에 후보가 없다는 뜻이다.
        var sectorPick   = new int[Sectors];
        var sectorSeen   = new int[Sectors];
        for (int s = 0; s < Sectors; s++) sectorPick[s] = -1;

        while (head < tail)
        {
            int i  = _bfsQueue[head++];
            int cx = i % w, cy = i / w;

            var wp = map.TileToWorld(new Vector2Int(origin.x + cx - r, origin.y + cy - r));
            float dx = wp.x - from.x, dz = wp.z - from.z;
            float d  = dx * dx + dz * dz;

            // **반경 밖은 더 안 펼친다** — 여기서 안 끊으면 굴이 이어진 만큼 세계 끝까지 훑는다.
            if (d > maxSqr) continue;
            if (d > bestSqr) { bestSqr = d; best = i; }

            // **훑는 김에 뽑는다** — 몇 개가 나올지 모르는 채로 균등하게 하나를 고르는 방법.
            if (d >= minSqr)
            {
                int sec = SectorOf(dx, dz);
                if (Random.Range(0, ++sectorSeen[sec]) == 0) sectorPick[sec] = i;
            }

            for (int k = 0; k < 4; k++)
            {
                int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0);
                int ny = cy + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= w || ny >= w) continue;
                if (!_graph.WalkableTile(origin.x + nx - r, origin.y + ny - r)) continue;

                int ni = ny * w + nx;
                if (_bfsSeen[ni]) continue;
                _bfsSeen[ni] = true;
                _bfsQueue[tail++] = ni;
            }
        }

        if (bestSqr < 0f) return 0;

        // **최소 거리를 채운 방위가 하나도 없으면 가장 먼 칸이라도 쓴다.**
        int available = 0;
        for (int s = 0; s < Sectors; s++) if (sectorPick[s] >= 0) available++;
        if (available == 0)
        {
            into.Add(Resolve(best, w, r, origin, from.y, map));
            return 1;
        }

        // **서로 먼 방위부터 집는다.** 첫 하나는 굴리고(늘 같은 방위에서 시작하면 갈래가
        // 하나일 때 방향이 다시 상수가 된다), 그다음부터는 이미 고른 것들에서 가장 먼 것.
        var chosen = new List<int>(Mathf.Min(count, available));
        int seed = Random.Range(0, Sectors);
        for (int s = 0; s < Sectors && chosen.Count == 0; s++)
        {
            int cand = (seed + s) % Sectors;
            if (sectorPick[cand] >= 0) chosen.Add(cand);
        }

        while (chosen.Count < count && chosen.Count < available)
        {
            int pickSec = -1, pickGap = -1;
            for (int s = 0; s < Sectors; s++)
            {
                if (sectorPick[s] < 0 || chosen.Contains(s)) continue;

                int gap = Sectors;
                foreach (int c in chosen) gap = Mathf.Min(gap, SectorGap(s, c));
                if (gap > pickGap) { pickGap = gap; pickSec = s; }
            }
            if (pickSec < 0) break;
            chosen.Add(pickSec);
        }

        foreach (int s in chosen) into.Add(Resolve(sectorPick[s], w, r, origin, from.y, map));
        return into.Count;
    }

    // 8방위. `EventManager`가 알림에 쓰는 나눔과 같은 수다 — 갈리면 *두 방향에서 온다*고
    // 알려 놓고 실제로는 같은 방위에서 나오는 일이 생긴다.
    const int Sectors = 8;

    static int SectorOf(float dx, float dz)
    {
        int i = Mathf.RoundToInt(Mathf.Atan2(dz, dx) * Mathf.Rad2Deg / (360f / Sectors));
        return ((i % Sectors) + Sectors) % Sectors;
    }

    // 방위 사이의 거리 — 원형이라 반대로 도는 쪽이 짧을 수 있다.
    static int SectorGap(int a, int b)
    {
        int d = Mathf.Abs(a - b);
        return Mathf.Min(d, Sectors - d);
    }

    // 창 색인 → 월드 좌표. 1m 칸이라 그대로 설 수 있는 자리다.
    static Vector3 Resolve(int index, int w, int r, Vector2Int origin, float y, MapManager map)
    {
        var c = map.TileToWorld(new Vector2Int(origin.x + index % w - r, origin.y + index / w - r));
        return new Vector3(c.x, y, c.z);
    }


    // 로컬 정밀 경로: 유닛 위치 중심 윈도우를 즉석 베이크해 target까지 Theta*.
    // 에이전트 반경만큼 장애물을 부풀려 좁은 틈 끼임을 방지한다.
    public List<Vector3> FindLocalPath(Vector3 unitPos, Vector3 target, PawnMover self = null)
        => _local.FindWindowPath(unitPos, target, localWindowSize, agentRadius, self);

    // 건물 배치·타일 변경·한 칸 채굴 — **그 칸이 든 셀의 덩어리를 다시 나누고 닿은 경계를
    // 다시 잇는다.** 한 칸을 파는 것이 덩어리 둘을 하나로 합칠 수 있으므로 이웃까지 같이
    // 본다. 안 고치면 **판 길이 먼 경로에는 영영 안 보인다.**
    public void RebakeObstacleAt(Vector3 worldPos) => _graph?.RebakeArea(worldPos, 1f);

    // **넓은 자리를 한 번에 다시 굽는다**(2026-08-31).
    //
    // `RebakeObstacleAt`은 **한 칸을 팠을 때**를 위한 것이라 광역 셀 하나만 고친다. 그런데
    // 판이 열릴 때 `RockMap`이 여는 것은 **공터 12m와 굴까지의 길 90m**이고, 그건 칸 하나가
    // 아니다.
    //
    // 안 고치면 이렇게 된다: 격자에는 **기지가 바위에 둘러싸인 섬**으로 남는다 → 광역
    // 길찾기가 아무 경로도 못 내고 → `ComputeCoarseRoute`가 **목적지 하나짜리 라우트**로
    // 떨어지고 → 조향 목표가 수백 미터 밖이라 로컬 창이 그 직선을 창 가장자리(바위)로
    // 접어 겨눈다 → **로컬 경로 없음 → 조용히 정지.** 화면에는 *"길이 있는데 멀리 찍으면
    // 안 간다"*로만 보인다(2026-08-31에 실제로 그랬다).
    //
    // **여는 쪽이 알린다** — 격자가 스스로 알 방법이 없다(함수는 판 것을 모르고, 판 것은
    // `RockMap`이 든다).
    public void RebakeArea(Vector3 center, float radius)
    {
        if (radius > 0f) _graph?.RebakeArea(center, radius);
    }

    // ── 동적 업데이트 ───────────────────────────────────────────────────

    void SubscribeEvents()
    {
        MapManager.OnTileChanged  += OnTileChanged;
        MapManager.OnFloorChanged += OnFloorChanged;
    }

    void UnsubscribeEvents()
    {
        MapManager.OnTileChanged  -= OnTileChanged;
        MapManager.OnFloorChanged -= OnFloorChanged;
    }

    // 바닥 설치/철거. `WideGraph._walk`는 `IsWorkable`을 **담아 둔 것**인데 바닥은 그 답을
    // 바꾼다 — 머리 위 여유를 **깔린 바닥 윗면에서** 재기 때문이다(`MapManager.HasHeadroom`).
    // 안 알리면 기단을 올려 여유가 없어진 칸으로 **먼 경로가 계속 폰을 보내고**, 반대로
    // 바닥이 열어 준 자리는 **영영 안 보인다.**
    void OnFloorChanged(Vector2Int tileCoord)
    {
        if (MapManager.Instance == null) return;
        RebakeObstacleAt(MapManager.Instance.TileToWorld(tileCoord));
    }

    // 점유 변경(건물·자원 노드).
    void OnTileChanged(Vector2Int tileCoord)
    {
        if (MapManager.Instance == null) return;
        RebakeObstacleAt(MapManager.Instance.TileToWorld(tileCoord));
    }
}
