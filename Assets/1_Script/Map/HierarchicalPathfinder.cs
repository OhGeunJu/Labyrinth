using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 계층형 길찾기 관리자. PawnMover가 ComputeCoarseRoute(메소/L2 코스) +
// FindLocalPath(유닛 중심 1m 윈도우)로 이용한다.
[DefaultExecutionOrder(-100)]
public class HierarchicalPathfinder : MonoBehaviour
{
    public static HierarchicalPathfinder Instance { get; private set; }

    [Header("그리드 설정")]
    [SerializeField] float l2TileSize  = 100f;

    [Header("로컬 윈도우 (유닛 중심 정밀 회피)")]
    [Tooltip("유닛 주변 1m 격자 윈도우 한 변 크기(m). 클수록 멀리 보지만 베이크 비용↑")]
    [SerializeField] int localWindowSize = 40;

    [Header("메소 격자 (베이스 주변 중간 해상도 — 긴 벽 코스 회피)")]
    [Tooltip("메소 셀 크기(m). 작을수록 정밀(좁은 문도 통과)하지만 셀 수↑")]
    [SerializeField] float mesoTileSize = 2f;
    [Tooltip("던전 코어 중심으로 덮는 영역 한 변(m)")]
    [SerializeField] float mesoAreaSize = 160f;

    [Header("에이전트")]
    [Tooltip("유닛 반경(m). 장애물을 이만큼 부풀려 베이크 → 좁은 틈은 못 지나가게/벽에 안 끼이게. CharacterController 반경에 맞춤")]
    [SerializeField] float agentRadius = 0.5f;

    [Header("장애물 레이어")]
    [SerializeField] LayerMask obstacleLayer = ~0;

    [Header("지형 비용")]
    [SerializeField] float slopeCostMult   = 0.05f;  // 경사도 1도당 추가 비용 배율
    [SerializeField] float maxWalkableSlope = 45f;    // 이 이상이면 불가

    PathGrid        _l2;
    PathGrid        _meso;
    Vector3         _mesoCenter;
    LocalGridManager _l1;

    Terrain _terrain;

    // 메소 재중심 점검 주기
    const float MesoRecenterInterval = 3f;

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
        StartCoroutine(MesoRecenterLoop());
    }

    void OnDestroy()
    {
        UnsubscribeEvents();
    }

    // ── 초기화 ─────────────────────────────────────────────────────────

    void InitGrids()
    {
        if (_terrain == null)
        {
            Debug.LogWarning("[HierarchicalPathfinder] Terrain 없음 — 빈 그리드로 초기화");
            _l2 = new PathGrid(100, 100, l2TileSize, Vector3.zero);
        }
        else
        {
            var data   = _terrain.terrainData;
            var origin = _terrain.transform.position;
            float w    = data.size.x, h = data.size.z;

            int l2W = Mathf.CeilToInt(w / l2TileSize);
            int l2H = Mathf.CeilToInt(h / l2TileSize);
            _l2 = new PathGrid(l2W, l2H, l2TileSize, origin);

            BakeFromTerrain(_l2, l2TileSize);
        }

        // 로컬 윈도우 패스파인더
        _l1 = new LocalGridManager();
        _l1.Initialize(obstacleLayer);
        var corePos = DungeonCore.Instance != null
            ? DungeonCore.Instance.transform.position
            : Vector3.zero;

        // 메소 격자 (베이스 주변 중간 해상도)
        BuildMeso(corePos);

        Debug.Log($"[HierarchicalPathfinder] 초기화 완료 — " +
                  $"L2:{_l2.Width}×{_l2.Height} Meso:{_meso.Width}×{_meso.Height}");
    }

    // 던전 코어 중심으로 메소 격자를 생성·베이크. 코어 이동 시 재중심.
    void BuildMeso(Vector3 center)
    {
        int n = Mathf.Max(2, Mathf.CeilToInt(mesoAreaSize / mesoTileSize));
        var origin = new Vector3(center.x - n * mesoTileSize * 0.5f,
                                 center.y,
                                 center.z - n * mesoTileSize * 0.5f);
        _meso = new PathGrid(n, n, mesoTileSize, origin);
        _mesoCenter = center;
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
            BakeCell(_meso, x, y, mesoTileSize, floorGate: true);
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
    // 메소 전용(L2 100m 셀은 타일 스캔이 무의미하게 비쌈).
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

        if (floorGate && !AnyFlooredInCell(worldCenter, tileSize))
        {
            grid.SetWalkable(x, y, false);
            return;
        }

        // 건물 점유 — 셀이 덮는 타일이 **전부** 막혔을 때만 차단(관대한 판정).
        // 부분 점유는 L1(1m 윈도우)이 정밀하게 처리한다. 얇은 벽까지 여기서 막으면
        // 한 칸짜리 통로가 코스 단계에서 사라져 경로 자체를 못 찾는다.
        if (floorGate && AllOccupiedInCell(worldCenter, tileSize))
        {
            grid.SetWalkable(x, y, false);
            return;
        }

        // 셀 범위 안의 장애물만 본다. 반경 부풀림은 **셀 반쪽보다 큰 에이전트**에만 적용 —
        // 코스 단계에서 벽 주변까지 통째로 막으면 좁은 통로가 사라져 경로 자체가 안 나온다
        // (정밀 차단은 L1 몫이라는 기존 원칙과 같은 방향).
        float half = tileSize * 0.45f + Mathf.Max(0f, agentRadius - tileSize * 0.5f);
        var halfBox = new Vector3(half, 2f, half);
        bool blocked = Physics.CheckBox(worldCenter, halfBox, Quaternion.identity, obstacleLayer);
        grid.SetWalkable(x, y, !blocked);
    }

    // 셀 범위와 겹치는 맵 타일 중 바닥이 하나라도 있는가. FloorRequired가 아니면 항상 통과.
    static bool AnyFlooredInCell(Vector3 cellCenter, float tileSize)
    {
        var map = MapManager.Instance;
        if (map == null || !map.FloorRequired) return true;

        int x0 = Mathf.FloorToInt(cellCenter.x - tileSize * 0.5f);
        int z0 = Mathf.FloorToInt(cellCenter.z - tileSize * 0.5f);
        int x1 = Mathf.CeilToInt (cellCenter.x + tileSize * 0.5f) - 1;
        int z1 = Mathf.CeilToInt (cellCenter.z + tileSize * 0.5f) - 1;
        for (int tx = x0; tx <= x1; tx++)
        for (int tz = z0; tz <= z1; tz++)
            if (map.IsFloored(new Vector2Int(tx, tz))) return true;
        return false;
    }

    // 셀 범위와 겹치는 맵 타일이 전부 건물에 점유돼 있는가.
    static bool AllOccupiedInCell(Vector3 cellCenter, float tileSize)
    {
        var map = MapManager.Instance;
        if (map == null) return false;

        int x0 = Mathf.FloorToInt(cellCenter.x - tileSize * 0.5f);
        int z0 = Mathf.FloorToInt(cellCenter.z - tileSize * 0.5f);
        int x1 = Mathf.CeilToInt (cellCenter.x + tileSize * 0.5f) - 1;
        int z1 = Mathf.CeilToInt (cellCenter.z + tileSize * 0.5f) - 1;
        for (int tx = x0; tx <= x1; tx++)
        for (int tz = z0; tz <= z1; tz++)
            if (map.GetTile(tx, tz).IsPassable) return false;
        return true;
    }

    // ── 공개 길찾기 API ────────────────────────────────────────────────

    // 코스 라우트: from이 베이스(메소) 안이면 메소(중간 해상도)로 라우팅 →
    // 윈도우보다 긴 벽도 우회. to가 메소 밖이면 경계(게이트 방향)까지 메소로
    // 안내 후 to를 직선으로 잇는다. from이 메소 밖이면 장거리 L2(100m).
    public List<Vector3> ComputeCoarseRoute(Vector3 from, Vector3 to)
    {
        if (_meso != null && _meso.InWorldBounds(from))
        {
            bool toInside  = _meso.InWorldBounds(to);
            Vector3 mesoTo = toInside ? to : ClampIntoMeso(to);
            var r = _meso.FindPath(from, mesoTo);
            if (r != null && r.Count > 0)
            {
                if (!toInside) r.Add(to);   // 메소 경계 통과 후 최종 목적지
                return r;
            }
        }

        var l2 = _l2.FindPath(from, to);
        if (l2 != null && l2.Count > 0) return l2;
        return new List<Vector3> { to };
    }

    // 메소 밖 좌표를 메소 경계 안쪽(여유 1.5셀)으로 클램프. to 방향 경계점 산출용.
    Vector3 ClampIntoMeso(Vector3 p)
    {
        float m = mesoTileSize * 1.5f;
        float maxX = _meso.Origin.x + _meso.Width  * mesoTileSize - m;
        float maxZ = _meso.Origin.z + _meso.Height * mesoTileSize - m;
        return new Vector3(Mathf.Clamp(p.x, _meso.Origin.x + m, maxX),
                           p.y,
                           Mathf.Clamp(p.z, _meso.Origin.z + m, maxZ));
    }

    // 로컬 정밀 경로: 유닛 위치 중심 윈도우를 즉석 베이크해 target까지 Theta*.
    // 에이전트 반경만큼 장애물을 부풀려 좁은 틈 끼임을 방지한다.
    public List<Vector3> FindLocalPath(Vector3 unitPos, Vector3 target, PawnMover self = null)
        => _l1.FindWindowPath(unitPos, target, localWindowSize, agentRadius, self);

    public int LocalWindowSize => localWindowSize;

    // 건물 배치/타일 변경 시 호출. 메소 격자의 해당 영역만 CheckBox 재베이크.
    // (L2 전체 셀을 막던 과다차단 없이 정확히 갱신)
    public void RebakeObstacleAt(Vector3 worldPos)
    {
        if (_meso == null || !_meso.InWorldBounds(worldPos)) return;
        var c = _meso.WorldToCell(worldPos);
        int r = Mathf.CeilToInt((agentRadius + 2f) / mesoTileSize) + 1;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
            if (_meso.InBounds(c.x + dx, c.y + dy))
                BakeCell(_meso, c.x + dx, c.y + dy, mesoTileSize, floorGate: true);
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

    // 바닥 설치/철거 시 그 타일이 속한 메소 셀만 재베이크.
    // 장애물과 달리 반경 부풀림이 없으므로 단일 셀이면 충분 — 스타터 바닥(400타일) 일괄 등록도 감당.
    void OnFloorChanged(Vector2Int tileCoord)
    {
        if (_meso == null || MapManager.Instance == null) return;
        var worldPos = MapManager.Instance.TileToWorld(tileCoord);
        if (!_meso.InWorldBounds(worldPos)) return;
        var c = _meso.WorldToCell(worldPos);
        BakeCell(_meso, c.x, c.y, mesoTileSize, floorGate: true);
    }

    // 타일 변경(자원 노드 등) 시 메소 격자의 해당 영역만 CheckBox 재베이크.
    void OnTileChanged(Vector2Int tileCoord)
    {
        if (MapManager.Instance == null) return;
        var worldPos = MapManager.Instance.TileToWorld(tileCoord);
        RebakeObstacleAt(worldPos);
    }

    // 코어 이동 시 메소 재중심 (베이스가 코어 주변을 따라가게).
    IEnumerator MesoRecenterLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(MesoRecenterInterval);

            if (DungeonCore.Instance != null)
            {
                var corePos = DungeonCore.Instance.transform.position;
                if ((corePos - _mesoCenter).sqrMagnitude > (mesoAreaSize * 0.25f) * (mesoAreaSize * 0.25f))
                    BuildMeso(corePos);
            }
        }
    }
}
