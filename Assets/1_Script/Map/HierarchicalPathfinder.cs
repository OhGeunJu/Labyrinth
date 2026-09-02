using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 계층형 길찾기 관리자 — **HPA\*의 abstract level 구조**(Botea 외, 2004).
//
// ── 격자 셋의 이름 (2026-08-30에 통일했다) ──────────────────────────────────
//
//   `_wide`   100m  세계 규모 폴백. 코스 격자 밖으로 나가는 먼 길에만 쓴다.
//   `_coarse`   2m  **실제로 쓰는 성긴 격자.** 핵 둘레를 덮고 복도를 안내한다.
//   `_local`    1m  유닛 둘레 창. 정밀 회피.
//
// 전에는 `_l2`·`_meso`·`_l1`이었다. **"meso"는 길찾기 용어가 아니다** — 기상학의
// micro/meso/macro 스케일을 빌려 온 말이라 이 분야 문헌에 없다. 그리고 **공개 API가
// `ComputeCoarseRoute`인데 그것을 만드는 격자 이름이 `_meso`**라 이름이 물음에 답하지 않았다.
// 게다가 `_l2`가 *가장 성긴 것*인데 숫자는 *가장 큰 것*이라 읽는 순서까지 헷갈렸다.
//
// **씬 값은 `FormerlySerializedAs`가 지킨다** — 직렬화 필드는 이름을 바꾸는 순간 값이
// 코드 기본값으로 되돌아간다(이 저장소가 여러 번 겪은 *"코드 기본값은 씬이 값을 들면 아무
// 힘이 없다"*의 뒤집힌 짝이다).
// PawnMover가 ComputeCoarseRoute(코스/L2 코스) +
// FindLocalPath(유닛 중심 1m 윈도우)로 이용한다.
[DefaultExecutionOrder(-100)]
public class HierarchicalPathfinder : MonoBehaviour
{
    public static HierarchicalPathfinder Instance { get; private set; }

    [Header("그리드 설정")]
    [FormerlySerializedAs("l2TileSize")]
    [SerializeField] float wideTileSize  = 100f;

    [Header("로컬 윈도우 (유닛 중심 정밀 회피)")]
    [Tooltip("유닛 주변 1m 격자 윈도우 한 변 크기(m). 클수록 멀리 보지만 베이크 비용↑")]
    [SerializeField] int localWindowSize = 40;

    [Header("코스 격자 (베이스 주변 중간 해상도 — 긴 벽 코스 회피)")]
    [Tooltip("코스 셀 크기(m). 작을수록 정밀(좁은 문도 통과)하지만 셀 수↑")]
    [FormerlySerializedAs("mesoTileSize")]
    [SerializeField] float coarseTileSize = 2f;
    [Tooltip("던전 코어 중심으로 덮는 영역 한 변(m)")]
    [FormerlySerializedAs("mesoAreaSize")]
    [SerializeField] float coarseAreaSize = 160f;

    [Header("에이전트")]
    [Tooltip("유닛 반경(m). 장애물을 이만큼 부풀려 베이크 → 좁은 틈은 못 지나가게/벽에 안 끼이게. CharacterController 반경에 맞춤")]
    [SerializeField] float agentRadius = 0.5f;

    [Header("장애물 레이어")]
    [SerializeField] LayerMask obstacleLayer = ~0;

    // 무엇이 벽으로 취급되는가 — 자기 콜라이더가 여기 걸리는지 알아야 하는 쪽(핵 등)이 읽는다.
    public LayerMask ObstacleLayer => obstacleLayer;

    [Header("지형 비용")]
    [SerializeField] float slopeCostMult   = 0.05f;  // 경사도 1도당 추가 비용 배율
    [SerializeField] float maxWalkableSlope = 45f;    // 이 이상이면 불가

    PathGrid        _wide;
    PathGrid        _coarse;
    Vector3         _coarseCenter;
    LocalGridManager _local;

    Terrain _terrain;

    // 코스 재중심 점검 주기
    const float CoarseRecenterInterval = 3f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _terrain = Terrain.activeTerrain;
        InitGrids();
        SubscribeEvents();
        StartCoroutine(CoarseRecenterLoop());
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
        _local.Initialize(obstacleLayer);
        var corePos = Anchor();

        // 코스 격자 (베이스 주변 중간 해상도)
        BuildCoarse(corePos);

        Debug.Log($"[HierarchicalPathfinder] 초기화 완료 — " +
                  $"광역:{_wide.Width}×{_wide.Height} 코스:{_coarse.Width}×{_coarse.Height}");
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
    // 3×3로 성기게 찍는다: 코스와 같은 원칙(*"하나라도 지날 수 있으면 열어 둔다 — 정밀
    // 차단은 로컬 몫"*)이고, 놓친 좁은 굴은 로컬이 찾아 준다.
    const int   WideMaxCells = 160;   // 한 변의 셀 수 천장
    const float WideMinCell  = 4f;    // 셀이 이보다 잘면 굽는 값만 늘고 얻는 것이 없다
    const int   WideSamples  = 3;     // 셀 하나를 몇 × 몇으로 찍나(값과 '설 수 있나'용)

    float _wideCell;

    void BuildWide()
    {
        var map = MapManager.Instance;
        if (map == null)
        {
            // 절차 지형이 없는 옛 씬 — 예전 방식 그대로.
            if (_terrain == null)
            {
                Debug.LogWarning("[길찾기] Terrain도 MapManager도 없다 — 빈 광역 격자로 둔다.");
                _wideCell = wideTileSize;
                _wide = new PathGrid(100, 100, wideTileSize, Vector3.zero);
            }
            else
            {
                var origin = _terrain.transform.position;
                float w = _terrain.terrainData.size.x, h = _terrain.terrainData.size.z;
                _wideCell = wideTileSize;
                _wide = new PathGrid(Mathf.CeilToInt(w / wideTileSize),
                                     Mathf.CeilToInt(h / wideTileSize), wideTileSize, origin);
                BakeFromTerrain(_wide, wideTileSize);
            }
            return;
        }

        float world = TerrainGrid.WorldSize;
        _wideCell   = Mathf.Max(WideMinCell, world / WideMaxCells);

        int n = Mathf.Max(2, Mathf.CeilToInt(world / _wideCell));
        _wide = new PathGrid(n, n, _wideCell, Vector3.zero);

        // **경계를 먼저 굽는다** — 셀의 열림이 경계에서 나오므로 순서가 뒤집히면
        // 첫 굽기가 통째로 폴백(표본)으로 떨어진다.
        _wide.EnsureLinks();
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++) BakeWideLinks(x, y);

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++) BakeWideCell(x, y);

        _wide.BuildClearance();
    }

    // ── 경계가 이어져 있나 — HPA\*의 entrance ───────────────────────────────
    //
    // 두 셀이 맞닿은 **선**을 1m 간격으로 훑어, 그 선을 사이에 둔 **양쪽 칸이 둘 다 걸을 수
    // 있는** 자리가 하나라도 있으면 이어진 것으로 본다.
    //
    // **셀 안을 훑는 것과는 다른 물음이다** — 셀이 아무리 넓게 열려 있어도 경계가 벽이면
    // 못 건너가고, 반대로 셀이 대부분 바위여도 **한 칸짜리 틈**이 경계에 있으면 지나갈 수
    // 있다. 후자를 막지 않는 것이 이 저장소의 규칙이다(*"한 칸짜리 통로를 막지 마라"*).
    //
    // 값은 셀당 경계 둘(동·북)이고 경계마다 표본 일곱쯤이라, 굽는 비용이 셀 표본과 같은
    // 자릿수다. **세계를 키워도 셀 수가 천장에 걸려 있으므로**(160×160) 여기도 안 폭발한다.
    void BakeWideLinks(int x, int y)
    {
        var map = MapManager.Instance;
        if (map == null || _wide == null) return;

        var c    = _wide.CellToWorld(new Vector2Int(x, y));
        float h  = _wideCell * 0.5f;

        _wide.SetLinkEast (x, y, CrossOpen(map, c, h, east: true));
        _wide.SetLinkNorth(x, y, CrossOpen(map, c, h, east: false));
    }

    // 경계선을 1m로 훑는다. **양쪽 칸을 같이 본다** — 선 위의 한 점만 보면 그 점이 어느 칸에
    // 속하는지에 따라 답이 갈린다(격자 위상이 정수가 아니다).
    static bool CrossOpen(MapManager map, Vector3 center, float half, bool east)
    {
        float b = east ? center.x + half : center.z + half;   // 경계선의 자리
        float lo = (east ? center.z : center.x) - half;

        int steps = Mathf.Max(2, Mathf.CeilToInt(half * 2f));
        for (int i = 0; i < steps; i++)
        {
            float t = lo + i + 0.5f;
            var a = east ? new Vector3(b - 0.5f, 0f, t) : new Vector3(t, 0f, b - 0.5f);
            var d = east ? new Vector3(b + 0.5f, 0f, t) : new Vector3(t, 0f, b + 0.5f);
            if (map.IsWorkable(map.WorldToTile(a)) && map.IsWorkable(map.WorldToTile(d))) return true;
        }
        return false;
    }

    // 셀 하나를 다시 구웠으면 **그 셀에 닿은 경계 넷**도 다시 굽는다 — 동·북은 제 것이고
    // 서·남은 이웃이 든다. 안 하면 판 자리가 셀은 열렸는데 **경계는 닫힌 채**로 남는다.
    void BakeWideLinksAround(int x, int y)
    {
        BakeWideLinks(x, y);
        BakeWideLinks(x - 1, y);
        BakeWideLinks(x, y - 1);
    }

    // **광역에서도 지름길은 두께를 안다**(2026-08-31). `BuildClearance`를 안 구우면 Theta\*의
    // 가시선이 **막힌 셀의 모서리를 스치며** 직선을 긋는다 — 6.4m 눈금에서 그 "스침"은
    // 실제로 **수 미터의 바위**다. 그게 라우트가 굴을 안 따라가고 어둠을 가로지르던 그림의
    // 나머지 절반이다(로컬 창이 `_windowGrid`에 이미 같은 것을 굽고 있었다).
    //
    // 굴이 6.4m 셀로 한두 칸 폭이라 대개 가시선이 안 열리고, 그러면 Theta\*가 **평범한 A\***로
    // 떨어져 **칸을 따라 굴을 돈다.** 그것이 광역이 해야 할 일이다 — 매끄러움은 로컬 몫이다.
    void RebuildWideClearance() => _wide?.BuildClearance();

    // 셀 하나 — **하나라도 설 수 있으면 열어 둔다.**
    // **한 점만 열려서는 안 된다**(2026-08-31). 전에는 표본 아홉 중 **하나라도** 열리면 셀을
    // 열어 두었다 — *"하나라도 지날 수 있으면 열어 둔다"*는 규칙 그대로다. 그런데 6.4m 셀에
    // 표본이 2.1m 간격이라, **굴을 살짝 스치기만 한 셀**도 열린 것이 됐다. Theta\*는 열린
    // 셀 위로 직선을 그으므로 라우트가 **바위를 가로질러 뻗었고**, 폰은 거기까지 걸어가
    // 벽 앞에서 섰다(씬 뷰의 노란 점들이 굴을 안 따라가고 어둠을 가로지른 그림).
    //
    // **문턱으로 푸는 길은 접었다**(2026-08-31).
    //
    // 표본 문턱을 올려 *좁은 셀*을 닫아 보려 했는데, 3으로 올리자 **세계가 통째로 끊겼고**
    // (*"이제 걍 멀면 안 가는데?"*) 2로 내리자 **얇은 벽을 여전히 뚫었다.** 당연하다 —
    // 셀 안에 열린 칸이 몇이냐는 **"이 셀을 지나갈 수 있나"에 답하지 못한다.** 벽은 칸 안이
    // 아니라 **칸과 칸 사이**에 서기 때문이다.
    //
    // 그래서 규칙을 원래대로 되돌리고(*하나라도 지날 수 있으면 열어 둔다*), 빠져 있던
    // 절반을 따로 굽는다 — **경계가 이어져 있나**(`BakeWideLinks`). 셀의 열림 비율은
    // 이제 *"설 수 있나"*와 **값**만 정한다.
    const int WideMinOpen = 1;

    void BakeWideCell(int x, int y)
    {
        var map = MapManager.Instance;
        if (map == null || _wide == null) return;

        var c    = _wide.CellToWorld(new Vector2Int(x, y));
        float h  = _wideCell * 0.5f;
        int  hit = 0;

        for (int sy = 0; sy < WideSamples; sy++)
        for (int sx = 0; sx < WideSamples; sx++)
        {
            float fx = (sx + 0.5f) / WideSamples, fz = (sy + 0.5f) / WideSamples;
            var p = new Vector3(c.x - h + fx * _wideCell, 0f, c.z - h + fz * _wideCell);
            if (map.IsWorkable(map.WorldToTile(p))) hit++;
        }

        // **걸을 수 있나는 경계가 답한다**(2026-08-31에 바로잡았다).
        //
        // 표본만으로 정하던 때는 **셀이 닫혔는데 그 경계는 이어진** 모순이 나왔다(진단이
        // 실제로 `북 닫힘/이음`을 찍었다). 당연하다 — 표본은 2.1m 간격이라 **1~2m짜리
        // 통로가 통째로 안 걸린다.** 그런데 경계 검사는 **1m로 훑으므로** 그 통로를 본다.
        //
        // 그래서 **셀의 열림을 경계에서 낸다**: 사방 중 하나라도 건널 수 있으면 그 셀에는
        // 걸을 수 있는 칸이 있다는 뜻이고, **넷 다 못 건너면 들어갈 수도 나올 수도 없어서**
        // 라우팅에는 없는 것과 같다. 표본은 이제 **값(비용)만** 정한다.
        bool open = _wide.LinkEast(x, y) || _wide.LinkEast(x - 1, y)
                 || _wide.LinkNorth(x, y) || _wide.LinkNorth(x, y - 1)
                 || hit >= WideMinOpen;   // 간선을 아직 안 구웠을 때의 폴백

        // **로컬이 못 뚫었다고 알려 온 셀은 닫아 둔다**(아래 `ReportUnreachable`).
        if (open && _wideBlocked.TryGetValue(new Vector2Int(x, y), out float until))
        {
            if (Time.time < until) open = false;
            else _wideBlocked.Remove(new Vector2Int(x, y));
        }

        _wide.SetWalkable(x, y, open);

        // **좁은 셀은 비싸다.** 지날 수는 있게 두되, 넉넉한 굴이 있으면 그쪽으로 돌게 한다 —
        // 열림/막힘 둘로만 두면 *간신히 지나는 길*과 *넓은 간선*이 값이 같아 라우트가
        // 바위를 스치는 쪽을 고른다.
        int total = WideSamples * WideSamples;
        _wide.SetCost(x, y, 1f + (1f - hit / (float)total) * 2f);
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

    readonly Dictionary<Vector2Int, float> _wideBlocked = new();

    public void ReportUnreachable(Vector3 world)
    {
        if (_wide == null || !_wide.InWorldBounds(world)) return;

        var c = _wide.WorldToCell(world);
        _wideBlocked[c] = Time.time + WideBlockDuration;
        BakeWideCell(c.x, c.y);
        RebuildWideClearance();
    }

    // 던전 코어 중심으로 코스 격자를 생성·베이크. 코어 이동 시 재중심.
    void BuildCoarse(Vector3 center)
    {
        int n = Mathf.Max(2, Mathf.CeilToInt(coarseAreaSize / coarseTileSize));
        var origin = new Vector3(center.x - n * coarseTileSize * 0.5f,
                                 center.y,
                                 center.z - n * coarseTileSize * 0.5f);
        _coarse = new PathGrid(n, n, coarseTileSize, origin);
        _coarseCenter = center;
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
            BakeCell(_coarse, x, y, coarseTileSize, floorGate: true);
    }

    void BakeFromTerrain(PathGrid grid, float tileSize)
    {
        for (int x = 0; x < grid.Width; x++)
        for (int y = 0; y < grid.Height; y++)
            BakeCell(grid, x, y, tileSize);
    }

    // 셀 하나 베이크: 지형 경사(있으면) + 공허 게이트(floorGate) + 장애물 CheckBox(에이전트 반경 부풀림).
    // 재베이크에도 쓰이므로 walkable을 항상 마지막에 확정한다.
    // floorGate: 던전형 맵(FloorRequired)에서 셀이 덮는 타일에 바닥이 하나도 없으면 이동 불가.
    // 관대한 판정(하나라도 있으면 통과) — 좁은 바닥 다리 코스를 살리고, 정밀 차단은 L1이 담당.
    // 코스 전용(L2 100m 셀은 타일 스캔이 무의미하게 비쌈).
    void BakeCell(PathGrid grid, int x, int y, float tileSize, bool floorGate = false)
    {
        var worldCenter = grid.CellToWorld(new Vector2Int(x, y));

        if (_terrain != null)
        {
            var td     = _terrain.terrainData;
            var origin = _terrain.transform.position;
            float nx = Mathf.Clamp01((worldCenter.x - origin.x) / td.size.x);
            float nz = Mathf.Clamp01((worldCenter.z - origin.z) / td.size.z);
            float slope = td.GetSteepness(nx, nz);
            if (slope >= maxWalkableSlope) { grid.SetWalkable(x, y, false); return; }
            grid.SetCost(x, y, 1f + slope * slopeCostMult);
        }
        else grid.SetCost(x, y, 1f);

        // ── 셀이 타일보다 조금 클 때(코스 2m)는 **타일이 답한다** ──────────────
        //
        // 여기서 콜라이더로 재면 **한 칸짜리 문이 코스 단계에서 사라진다**(2026-08-28).
        // 2m 셀에 벽이 한 조각만 닿아도 셀 전체가 막히는데, 7×6 오두막은 3.5×3셀이라
        // **안쪽까지 통째로 막혔다** — 그 안에 선 폰은 코스 경로가 아예 안 나오고,
        // 조향 목표가 최종 목적지로 떨어져 **벽을 향해 곧장 걸었다.**
        //
        // 원칙은 이미 적혀 있었다(*"정밀 차단은 L1 몫"*) — 점유 판정만 관대하고
        // 콜라이더 판정이 그 원칙을 도로 깨고 있었다. **코스는 복도 안내다.**
        if (floorGate)
        {
            if (!AnyFlooredInCell(worldCenter, tileSize)) { grid.SetWalkable(x, y, false); return; }

            // 하나라도 지날 수 있으면 열어 둔다. 좁은 문은 L1(1m)이 정확히 찾는다.
            grid.SetWalkable(x, y, AnyPassableInCell(worldCenter, tileSize));
            return;
        }

        // 타일을 훑기엔 너무 큰 셀(L2 100m) — 콜라이더로 어림잡는다. 반경 부풀림은
        // **셀 반쪽보다 큰 에이전트**에만 적용한다.
        float half = tileSize * 0.45f + Mathf.Max(0f, agentRadius - tileSize * 0.5f);
        var halfBox = new Vector3(half, 2f, half);
        bool blocked = Physics.CheckBox(worldCenter, halfBox, Quaternion.identity, obstacleLayer);
        grid.SetWalkable(x, y, !blocked);
    }

    // 셀이 덮는 타일 범위. **`WorldToTile`이 답한다** — 월드 좌표를 바로 `Floor`하면
    // 격자 위상(08-27에 첫 조작이 정하게 됐다)을 빠뜨려 한 칸씩 어긋난 범위를 훑는다.
    // 모서리에서 반 칸 안으로 들어와 묻는다(경계에 정확히 걸린 값이 옆 칸에 떨어지지 않게).
    static bool CellTiles(Vector3 c, float tileSize, out Vector2Int lo, out Vector2Int hi)
    {
        var map = MapManager.Instance;
        lo = hi = default;
        if (map == null) return false;

        float h = tileSize * 0.5f;
        lo = map.WorldToTile(new Vector3(c.x - h + 0.5f, 0f, c.z - h + 0.5f));
        hi = map.WorldToTile(new Vector3(c.x + h - 0.5f, 0f, c.z + h - 0.5f));
        return true;
    }

    // 셀 범위와 겹치는 맵 타일 중 바닥이 하나라도 있는가. FloorRequired가 아니면 항상 통과.
    static bool AnyFlooredInCell(Vector3 cellCenter, float tileSize)
    {
        var map = MapManager.Instance;
        if (map == null || !map.FloorRequired) return true;
        if (!CellTiles(cellCenter, tileSize, out var lo, out var hi)) return true;

        for (int tx = lo.x; tx <= hi.x; tx++)
        for (int tz = lo.y; tz <= hi.y; tz++)
            if (map.IsFloored(new Vector2Int(tx, tz))) return true;
        return false;
    }

    // 셀 범위에 지나갈 수 있는 타일이 하나라도 있는가 — **코스 단계의 관대한 판정**이다.
    static bool AnyPassableInCell(Vector3 cellCenter, float tileSize)
    {
        var map = MapManager.Instance;
        if (map == null) return true;
        if (!CellTiles(cellCenter, tileSize, out var lo, out var hi)) return true;

        // **`IsWorkable`이다 — `IsPassable`이 아니다**(2026-08-29).
        //
        // `IsPassable`은 **점유(`_occupied`)만** 본다. 지하로 오면서 통행을 막는 것이 하나 더
        // 생겼는데(바위) 그건 `IsStandable`이 든다 — 여기서 안 보면 **코스 경로가 통짜 바위를
        // 가로질러 난다.** 그러면 폰은 그 복도 안내를 따라 벽으로 걸어가고, 로컬 경로가 안
        // 나와 **직선 폴백으로 벽을 밀다가 안으로 처박힌다.**
        //
        // `IsWorkable` = 통행 가능 + 설 수 있음. **둘을 여기서 다시 조합하지 않는다** —
        // 조합하는 자리가 둘이면 언젠가 한쪽만 고친다.
        for (int tx = lo.x; tx <= hi.x; tx++)
        for (int tz = lo.y; tz <= hi.y; tz++)
            if (map.IsWorkable(new Vector2Int(tx, tz))) return true;
        return false;
    }

    // ── 공개 길찾기 API ────────────────────────────────────────────────

    // 코스 라우트: from이 베이스(코스) 안이면 코스(중간 해상도)로 라우팅 →
    // 윈도우보다 긴 벽도 우회. to가 코스 밖이면 경계(게이트 방향)까지 코스로
    // 안내 후 to를 직선으로 잇는다. from이 코스 밖이면 장거리 L2(100m).
    public List<Vector3> ComputeCoarseRoute(Vector3 from, Vector3 to)
    {
        // **둘 다 코스 안일 때만 코스로 간다**(2026-08-30).
        //
        // 전에는 출발만 안에 있으면 코스로 풀고, 목적지가 밖이면 **경계까지 간 뒤 목적지를
        // 그냥 덧붙였다** — 그 마지막 구간이 **직선 한 방**이다. 지상에서는 견딜 만했지만
        // 굴에서는 곧 벽에 처박는다는 뜻이고, 광역 격자가 죽어 있어서 대안도 없었다.
        if (_coarse != null && _coarse.InWorldBounds(from) && _coarse.InWorldBounds(to))
        {
            var r = _coarse.FindPath(from, to);
            if (r != null && r.Count > 0) { PushOffWalls(r, PushRadius); return Densify(r, from); }
        }

        // 멀리는 광역이 안내한다. **셀이 성기므로 벽에서 떼는 반경도 그만큼 크다.**
        if (_wide != null)
        {
            var wide = _wide.FindPath(from, to);

            // **아예 못 냈으면 간선부터 의심한다** — 출발 셀이 간선으로 고립되면 여기가
            // `null`이 되고, 그러면 라우트가 *목적지 하나*로 떨어져 폰이 통째로 못 움직인다.
            if (wide == null || wide.Count == 0)
            {
                _wide.IgnoreLinks = true;
                wide = _wide.FindPath(from, to);
                _wide.IgnoreLinks = false;
            }

            if (wide != null && wide.Count > 0)
            {
                // **여기까지밖에 못 갔다면 그 이유를 말한다.** 부분 경로는 정상 동작이지만
                // (*갈 수 있는 데까지 간다*), 그것이 **진짜 막다른 길**인지 **간선 판정이
                // 너무 짠 것**인지는 화면에서 못 가린다 — 끝난 칸의 이웃과 간선을 찍는다.
                // **끊긴 답이 나오면 간선을 무시하고 한 번 더 푼다.**
                //
                // 간선 판정(경계가 이어졌나)은 셀 열림보다 정확하지만 **완벽하지 않다** —
                // 틀리면 *갈 수 있는 길이 끊긴 것*으로 보이고, 그건 라우트가 조금 나쁜 것보다
                // 훨씬 나쁘다(폰이 아예 못 간다. 사용자: *"실제 길은 이어져 있어"*).
                //
                // 그래서 **정확한 답을 먼저 쓰되 그것이 세계를 끊으면 이어 둔다.** 그 라우트가
                // 얇은 벽을 뚫는 것이면 폰이 걸어가다 알아채고 `ReportUnreachable`로 배운다 —
                // **끊어 놓고 못 가는 것보다 이어 두고 배우는 편이 낫다.**
                var last = wide[wide.Count - 1];
                float dx = last.x - to.x, dz = last.z - to.z;
                if (dx * dx + dz * dz > _wideCell * _wideCell * 2.25f)
                {
                    WarnPartialWideRoute(last, to);

                    // **검증된 앞부분은 버리지 않는다**(2026-08-31에 한 번 되돌렸다).
                    //
                    // 처음에는 *부분 경로면 통째로 갈아탄다*로 넣었는데, 그러면 조금만 모자라도
                    // **처음부터 벽 뚫는 라우트**가 되어 고치기 전으로 돌아간다(사용자:
                    // *"아까까지 잘 됐는데 다시 그때 경로로 돌아왔어"*).
                    //
                    // 간선을 지킨 앞부분은 **믿을 수 있는 안내**다. 그러니 **거기까지는 그대로
                    // 쓰고, 그 끝에서부터만** 간선을 무시하고 이어 붙인다 — 미심쩍은 구간이
                    // 꼬리에만 남고, 거기서 벽을 만나면 `ReportUnreachable`이 배운다.
                    _wide.IgnoreLinks = true;
                    var tail = _wide.FindPath(last, to);
                    _wide.IgnoreLinks = false;

                    if (tail != null && tail.Count > 0)
                    {
                        var tend = tail[tail.Count - 1];
                        float tdx = tend.x - to.x, tdz = tend.z - to.z;
                        if (tdx * tdx + tdz * tdz < dx * dx + dz * dz) wide.AddRange(tail);
                    }
                }

                PushOffWalls(wide, Mathf.CeilToInt(_wideCell * 0.5f));
                return Densify(wide, from);
            }
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

    // 라우트가 중간에 끝났다 — **끝난 칸의 사방**을 찍는다. `열림`은 셀이 걸을 수 있나이고
    // `이음`은 그 방향으로 **건너갈 수 있나**다. 둘은 다른 물음이라 따로 봐야 한다:
    // *"이웃이 열려 있는데 이음이 닫혔다"*면 간선 판정이 짠 것이고, *"이웃이 닫혔다"*면
    // 진짜 벽이다.
    void WarnPartialWideRoute(Vector3 end, Vector3 goal)
    {
        if (_wide == null || Time.time < _nextPartialWarn) return;
        _nextPartialWarn = Time.time + 3f;

        var c = _wide.WorldToCell(end);
        var g = _wide.WorldToCell(goal);

        string Side(int dx, int dy, bool link)
        {
            int nx = c.x + dx, ny = c.y + dy;
            if (!_wide.InBounds(nx, ny)) return "밖";
            return $"{(_wide.IsWalkable(nx, ny) ? "열림" : "닫힘")}/{(link ? "이음" : "**끊김**")}";
        }

        // 간선은 `PathGrid`가 들고 있으므로 여기서는 **같은 검사를 다시 해서** 보여 준다
        // (판정과 화면이 같은 함수를 봐야 그림이 거짓말을 안 한다).
        var map = MapManager.Instance;
        var cc  = _wide.CellToWorld(c);
        float h = _wideCell * 0.5f;
        bool e  = map != null && CrossOpen(map, cc, h, east: true);
        bool n  = map != null && CrossOpen(map, cc, h, east: false);
        var wc  = _wide.CellToWorld(new Vector2Int(c.x - 1, c.y));
        var sc  = _wide.CellToWorld(new Vector2Int(c.x, c.y - 1));
        bool w  = map != null && CrossOpen(map, wc, h, east: true);
        bool sv = map != null && CrossOpen(map, sc, h, east: false);

        Debug.LogWarning(
            $"[길찾기] 광역 라우트가 중간에 끝났다 — 끝 셀 ({c.x},{c.y}) 표본 {OpenSamples(c.x, c.y)}/" +
            $"{WideSamples * WideSamples}, 목표 셀 ({g.x},{g.y}) 표본 {OpenSamples(g.x, g.y)}/" +
            $"{WideSamples * WideSamples}\n" +
            $"  동 {Side(1, 0, e)} · 서 {Side(-1, 0, w)} · 북 {Side(0, 1, n)} · 남 {Side(0, -1, sv)}");
    }

    void WarnNoWideRoute(Vector3 from, Vector3 to)
    {
        if (_wide == null || Time.time < _nextWideWarn) return;
        _nextWideWarn = Time.time + 3f;

        var a = _wide.WorldToCell(from);
        var b = _wide.WorldToCell(to);
        Debug.LogWarning(
            $"[길찾기] 광역 경로 없음 — 출발 셀 ({a.x},{a.y}) {(_wide.IsWalkable(a.x, a.y) ? "열림" : "**닫힘**")}" +
            $"(표본 {OpenSamples(a.x, a.y)}/{WideSamples * WideSamples}) · " +
            $"도착 셀 ({b.x},{b.y}) {(_wide.IsWalkable(b.x, b.y) ? "열림" : "**닫힘**")}" +
            $"(표본 {OpenSamples(b.x, b.y)}/{WideSamples * WideSamples}) · " +
            $"셀 {_wideCell:0.0}m · 격자 {_wide.Width}×{_wide.Height}");
    }

    // 그 셀에서 실제로 몇 표본이 열려 있나 — 문턱이 맞는지를 숫자로 본다.
    int OpenSamples(int x, int y)
    {
        var map = MapManager.Instance;
        if (map == null || _wide == null || !_wide.InBounds(x, y)) return -1;

        var c   = _wide.CellToWorld(new Vector2Int(x, y));
        float h = _wideCell * 0.5f;
        int hit = 0;

        for (int sy = 0; sy < WideSamples; sy++)
        for (int sx = 0; sx < WideSamples; sx++)
        {
            float fx = (sx + 0.5f) / WideSamples, fz = (sy + 0.5f) / WideSamples;
            var p = new Vector3(c.x - h + fx * _wideCell, 0f, c.z - h + fz * _wideCell);
            if (map.IsWorkable(map.WorldToTile(p))) hit++;
        }
        return hit;
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
    // **코스 격자를 그대로 쓴다** — 폰이 실제로 걷는 그래프라 **갈 수 있는 곳임이 보장되고**,
    // 이미 구워져 있어 새로 잴 것이 없다(1m 칸으로 직접 훑으면 반경 150m에 7만 칸이고
    // 칸마다 여유를 다시 뽑아야 한다).
    //
    // **고르는 것은 "가장 멀리 떨어진 칸"이지 "가장 오래 걸어야 하는 칸"이 아니다** — 걸음
    // 수로 고르면 기지 옆의 구불구불한 막다른 굴이 뽑힌다. 우리가 원하는 것은 *멀리서
    // 온다*이므로 **직선 거리로 재되 후보는 걸어갈 수 있는 칸뿐**이다.
    bool[] _bfsSeen;
    int[]  _bfsQueue;

    public bool FarthestReachable(Vector3 from, float maxRadius, out Vector3 found)
    {
        found = from;
        if (_coarse == null || !_coarse.InWorldBounds(from)) return false;

        var start = _coarse.WorldToCell(from);
        if (!_coarse.IsWalkable(start.x, start.y)) return false;

        int w = _coarse.Width, h = _coarse.Height, n = w * h;
        if (_bfsSeen == null || _bfsSeen.Length != n) { _bfsSeen = new bool[n]; _bfsQueue = new int[n]; }
        System.Array.Clear(_bfsSeen, 0, n);

        int head = 0, tail = 0;
        int si = start.y * w + start.x;
        _bfsSeen[si] = true;
        _bfsQueue[tail++] = si;

        float maxSqr = maxRadius * maxRadius;
        float bestSqr = -1f;
        int   best    = si;

        while (head < tail)
        {
            int i  = _bfsQueue[head++];
            int cx = i % w, cy = i / w;

            var wp = _coarse.CellToWorld(new Vector2Int(cx, cy));
            float dx = wp.x - from.x, dz = wp.z - from.z;
            float d  = dx * dx + dz * dz;

            // **반경 밖은 더 안 펼친다** — 여기서 안 끊으면 굴이 이어진 만큼 세계 끝까지 훑는다.
            if (d > maxSqr) continue;
            if (d > bestSqr) { bestSqr = d; best = i; }

            for (int k = 0; k < 4; k++)
            {
                int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0);
                int ny = cy + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (!_coarse.IsWalkable(nx, ny)) continue;

                int ni = ny * w + nx;
                if (_bfsSeen[ni]) continue;
                _bfsSeen[ni] = true;
                _bfsQueue[tail++] = ni;
            }
        }

        if (bestSqr < 0f) return false;

        var c   = _coarse.CellToWorld(new Vector2Int(best % w, best / w));
        var map = MapManager.Instance;

        // **셀 한복판이 바위일 수 있다**(셀은 한 칸이라도 열리면 열린 것으로 친다) —
        // 코스 waypoint와 같은 이유로 설 수 있는 자리로 옮긴다.
        found = map != null ? OpenSpotNear(map, new Vector3(c.x, from.y, c.z))
                            : new Vector3(c.x, from.y, c.z);
        return true;
    }

    // ── 코스 waypoint를 벽에서 뗀다 ─────────────────────────────────────────
    //
    // **코스 셀은 한 칸이라도 지날 수 있으면 열린 것으로 친다**(`AnyPassableInCell` — 그래야
    // 한 칸짜리 문이 코스 단계에서 안 사라진다). 그런데 경로가 돌려주는 것은 그 셀의
    // **기하학적 중심**이라, 셀의 4분의 3이 바위인 자리에서는 **중심이 바위 속**이다.
    //
    // 폰은 그 점을 향해 걷는다 — 즉 **벽을 향해 걷는다.** 도착 판정 안으로 못 들어가니
    // 다음 waypoint로 넘어가지도 못하고, 벽에 몸을 댄 채 좌우로 오간다(사용자가 씬 뷰에서
    // *"노란 공이 벽에 살짝 박혀 있다"*로 잡아냈다).
    //
    // **"가까이 가면 넘어간다"로 풀지 않는다** — 그러면 도착 판정이 waypoint마다 달라지고,
    // 진짜로 좁은 통로에서 **아직 안 지난 모퉁이를 지난 것으로 친다.** 점을 **설 수 있는
    // 자리로 옮기는 것**이 옳다.
    //
    // **마지막 점은 안 건드린다** — 그건 목적지이고, 목적지를 옮기면 *"거기 가라"*가 거짓이
    // 된다(마지막 구간은 `PawnMover`가 `_finalGoal`을 직접 겨눈다).
    void PushOffWalls(List<Vector3> route, int radius)
    {
        var map = MapManager.Instance;
        if (map == null) return;

        for (int i = 0; i < route.Count - 1; i++)
            route[i] = OpenSpotNear(map, route[i], radius);
    }

    // 그 자리에서 가장 **벽에서 먼** 칸의 한복판. 후보는 둘레 두 칸까지.
    //
    // **트인 정도로 고르고 거리로 가른다** — 가까운 것부터 고르면 벽에 붙은 칸이 그대로
    // 뽑힌다(그게 지금 문제다). 둘레 여덟 칸이 다 트인 자리를 먼저 찾고, 없으면 차선.
    const int PushRadius = 2;

    static Vector3 OpenSpotNear(MapManager map, Vector3 p, int radius = PushRadius)
    {
        var here = map.WorldToTile(p);

        int  bestOpen = -1;
        float bestDist = float.MaxValue;
        Vector2Int best = here;

        radius = Mathf.Clamp(radius, 1, 8);

        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            var t = new Vector2Int(here.x + dx, here.y + dy);
            if (!map.IsWorkable(t)) continue;

            int open = 0;
            for (int ny = -1; ny <= 1; ny++)
            for (int nx = -1; nx <= 1; nx++)
            {
                if (nx == 0 && ny == 0) continue;
                if (map.IsWorkable(new Vector2Int(t.x + nx, t.y + ny))) open++;
            }

            float d = dx * dx + dy * dy;
            if (open > bestOpen || (open == bestOpen && d < bestDist))
            {
                bestOpen = open; bestDist = d; best = t;
            }
        }

        if (bestOpen < 0) return p;   // 둘레가 통째로 바위 — 손댈 것이 없다

        var c = map.TileToWorld(best);
        return new Vector3(c.x, p.y, c.z);
    }


    // 로컬 정밀 경로: 유닛 위치 중심 윈도우를 즉석 베이크해 target까지 Theta*.
    // 에이전트 반경만큼 장애물을 부풀려 좁은 틈 끼임을 방지한다.
    public List<Vector3> FindLocalPath(Vector3 unitPos, Vector3 target, PawnMover self = null)
        => _local.FindWindowPath(unitPos, target, localWindowSize, agentRadius, self);

    public int LocalWindowSize => localWindowSize;

    // 건물 배치/타일 변경 시 호출. 코스 격자의 해당 영역만 CheckBox 재베이크.
    // (L2 전체 셀을 막던 과다차단 없이 정확히 갱신)
    public void RebakeObstacleAt(Vector3 worldPos)
    {
        // **광역도 같이 고친다** — 통째로 바위이던 셀을 한 칸 파면 그 셀이 열려야 한다.
        // 안 고치면 **판 길이 먼 경로에는 영영 안 보인다.**
        if (_wide != null && _wide.InWorldBounds(worldPos))
        {
            var wc = _wide.WorldToCell(worldPos);

            // **경계 먼저, 셀 나중** — 그리고 경계가 바뀌면 **이웃 셀의 열림도 바뀐다.**
            BakeWideLinksAround(wc.x, wc.y);
            BakeWideLinksAround(wc.x + 1, wc.y);
            BakeWideLinksAround(wc.x, wc.y + 1);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (_wide.InBounds(wc.x + dx, wc.y + dy)) BakeWideCell(wc.x + dx, wc.y + dy);

            RebuildWideClearance();
        }

        if (_coarse == null || !_coarse.InWorldBounds(worldPos)) return;
        var c = _coarse.WorldToCell(worldPos);
        int r = Mathf.CeilToInt((agentRadius + 2f) / coarseTileSize) + 1;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
            if (_coarse.InBounds(c.x + dx, c.y + dy))
                BakeCell(_coarse, c.x + dx, c.y + dy, coarseTileSize, floorGate: true);
    }

    // **넓은 자리를 한 번에 다시 굽는다**(2026-08-31).
    //
    // `RebakeObstacleAt`은 **한 칸을 팠을 때**를 위한 것이라 광역 셀 하나와 코스 몇 칸만
    // 고친다. 그런데 판이 열릴 때 `RockMap`이 여는 것은 **공터 12m와 굴까지의 길 90m**이고,
    // 그건 칸 하나가 아니다.
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
        if (radius <= 0f) return;

        if (_wide != null)
        {
            var c = _wide.WorldToCell(center);
            int r = Mathf.CeilToInt(radius / Mathf.Max(0.01f, _wideCell)) + 1;
            // **경계 먼저, 셀 나중** — 셀의 열림이 경계에서 나온다.
            for (int dy = -r - 1; dy <= r + 1; dy++)
            for (int dx = -r - 1; dx <= r + 1; dx++)
                if (_wide.InBounds(c.x + dx, c.y + dy)) BakeWideLinksAround(c.x + dx, c.y + dy);

            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (_wide.InBounds(c.x + dx, c.y + dy)) BakeWideCell(c.x + dx, c.y + dy);

            RebuildWideClearance();
        }

        if (_coarse != null)
        {
            var c = _coarse.WorldToCell(center);
            int r = Mathf.CeilToInt(radius / Mathf.Max(0.01f, coarseTileSize)) + 1;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (_coarse.InBounds(c.x + dx, c.y + dy))
                    BakeCell(_coarse, c.x + dx, c.y + dy, coarseTileSize, floorGate: true);
        }
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

    // 바닥 설치/철거 시 그 타일이 속한 코스 셀만 재베이크.
    // 장애물과 달리 반경 부풀림이 없으므로 단일 셀이면 충분 — 스타터 바닥(400타일) 일괄 등록도 감당.
    void OnFloorChanged(Vector2Int tileCoord)
    {
        if (_coarse == null || MapManager.Instance == null) return;
        var worldPos = MapManager.Instance.TileToWorld(tileCoord);
        if (!_coarse.InWorldBounds(worldPos)) return;
        var c = _coarse.WorldToCell(worldPos);
        BakeCell(_coarse, c.x, c.y, coarseTileSize, floorGate: true);
    }

    // 타일 변경(자원 노드 등) 시 코스 격자의 해당 영역만 CheckBox 재베이크.
    void OnTileChanged(Vector2Int tileCoord)
    {
        if (MapManager.Instance == null) return;
        var worldPos = MapManager.Instance.TileToWorld(tileCoord);
        RebakeObstacleAt(worldPos);
    }

    // 코어 이동 시 코스 재중심 (베이스가 따라가게).
    IEnumerator CoarseRecenterLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(CoarseRecenterInterval);

            var anchor = Anchor();
            if ((anchor - _coarseCenter).sqrMagnitude > (coarseAreaSize * 0.25f) * (coarseAreaSize * 0.25f))
                BuildCoarse(anchor);
        }
    }

    // **코스 격자의 중심은 무엇인가**(2026-08-27). 원래는 핵이었다 — 거점 둘레가 곧 폰이
    // 오가는 데였으니까. 그런데 **판이 열릴 때 핵을 안 주기로 하면서**(사용자 결정) 핵이
    // 없는 시간이 생겼고, 그때 중심이 원점으로 떨어졌다. 세계가 8km인데 폰은 아무 데나
    // 있으므로, **중간 해상도 격자가 폰이 없는 데에 서고 이동이 통째로 로컬로 떨어진다.**
    //
    // 그래서 **핵이 없으면 내 폰들이 중심이다.** 평균을 쓴다 — 하나만 짚으면 그 폰이
    // 죽거나 꺼질 때마다 중심이 튄다.
    static Vector3 Anchor()
    {
        var core = DungeonCore.Instance;
        if (core != null) return core.transform.position;

        var all = Faction.All;
        Vector3 sum = Vector3.zero;
        int n = 0;
        for (int i = 0; i < all.Count; i++)
        {
            var f = all[i];
            if (f == null || !f.IsPlayer) continue;
            sum += f.transform.position;
            n++;
        }
        return n > 0 ? sum / n : Vector3.zero;
    }
}
