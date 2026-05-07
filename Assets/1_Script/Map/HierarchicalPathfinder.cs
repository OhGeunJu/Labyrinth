using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 3단계 계층 A* 길찾기 관리자.
// Pathfinder.FindPath()가 이 클래스에 위임함.
[DefaultExecutionOrder(-100)]
public class HierarchicalPathfinder : MonoBehaviour
{
    public static HierarchicalPathfinder Instance { get; private set; }

    [Header("그리드 설정")]
    [SerializeField] float l2TileSize  = 100f;
    [SerializeField] float l3TileSize  = 1000f;

    [Header("장애물 레이어")]
    [SerializeField] LayerMask obstacleLayer = ~0;

    [Header("지형 비용")]
    [SerializeField] float slopeCostMult   = 0.05f;  // 경사도 1도당 추가 비용 배율
    [SerializeField] float maxWalkableSlope = 45f;    // 이 이상이면 불가

    PathGrid        _l3;
    PathGrid        _l2;
    LocalGridManager _l1;

    Terrain _terrain;

    // 클러스터 업데이트 주기
    const float ClusterUpdateInterval = 3f;
    const float PathRecalcInterval    = 2f;

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
        StartCoroutine(ClusterUpdateLoop());
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
            _l3 = new PathGrid(10, 10, l3TileSize, Vector3.zero);
            _l2 = new PathGrid(100, 100, l2TileSize, Vector3.zero);
        }
        else
        {
            var data   = _terrain.terrainData;
            var origin = _terrain.transform.position;
            float w    = data.size.x, h = data.size.z;

            int l3W = Mathf.CeilToInt(w / l3TileSize);
            int l3H = Mathf.CeilToInt(h / l3TileSize);
            _l3 = new PathGrid(l3W, l3H, l3TileSize, origin);

            int l2W = Mathf.CeilToInt(w / l2TileSize);
            int l2H = Mathf.CeilToInt(h / l2TileSize);
            _l2 = new PathGrid(l2W, l2H, l2TileSize, origin);

            BakeFromTerrain(_l3, l3TileSize);
            BakeFromTerrain(_l2, l2TileSize);
        }

        // L1 공유 그리드
        _l1 = new LocalGridManager();
        var corePos = DungeonCore.Instance != null
            ? DungeonCore.Instance.transform.position
            : Vector3.zero;
        _l1.Initialize(corePos, obstacleLayer);

        Debug.Log($"[HierarchicalPathfinder] 초기화 완료 — " +
                  $"L3:{_l3.Width}×{_l3.Height} L2:{_l2.Width}×{_l2.Height}");
    }

    // Terrain 데이터로 그리드 경사도·텍스처 비용 베이킹
    void BakeFromTerrain(PathGrid grid, float tileSize)
    {
        var td     = _terrain.terrainData;
        var origin = _terrain.transform.position;

        for (int x = 0; x < grid.Width; x++)
        for (int y = 0; y < grid.Height; y++)
        {
            var worldCenter = grid.CellToWorld(new Vector2Int(x, y));
            float nx = (worldCenter.x - origin.x) / td.size.x;
            float nz = (worldCenter.z - origin.z) / td.size.z;
            nx = Mathf.Clamp01(nx); nz = Mathf.Clamp01(nz);

            // 경사도 → 이동 불가 / 비용 결정
            float slope = td.GetSteepness(nx, nz);
            if (slope >= maxWalkableSlope)
            {
                grid.SetWalkable(x, y, false);
                continue;
            }
            float cost = 1f + slope * slopeCostMult;

            // 물리 오브젝트 (건물 등) 검사
            var halfBox = new Vector3(tileSize * 0.45f, 2f, tileSize * 0.45f);
            if (Physics.CheckBox(worldCenter, halfBox, Quaternion.identity, obstacleLayer))
            {
                grid.SetWalkable(x, y, false);
                continue;
            }

            grid.SetCost(x, y, cost);
        }
    }

    // ── 공개 길찾기 API ────────────────────────────────────────────────

    // PawnMover에서 호출. 웨이포인트 리스트 반환 (시작 제외).
    public List<Vector3> FindPath(Vector3 from, Vector3 to)
    {
        // L1 범위 안: L1 A*
        var l1Grid = _l1.GetGridFor(from);
        if (l1Grid != null && _l1.GetGridFor(to) == l1Grid)
        {
            var path = l1Grid.FindPath(from, to);
            if (path != null) return path;
        }

        // L2 A*: 전체 경로
        var l2Path = _l2.FindPath(from, to);
        if (l2Path != null)
        {
            // 첫 구간을 L1으로 정밀화
            if (l1Grid != null && l2Path.Count > 0)
            {
                var firstWaypoint = l2Path[0];
                var l1Segment = l1Grid.FindPath(from, firstWaypoint);
                if (l1Segment != null)
                {
                    l1Segment.AddRange(l2Path.GetRange(1, l2Path.Count - 1));
                    return l1Segment;
                }
            }
            return l2Path;
        }

        // 폴백: 직선 이동
        return new List<Vector3> { to };
    }

    // ── 동적 업데이트 ───────────────────────────────────────────────────

    void SubscribeEvents()
    {
        MapManager.OnTileChanged += OnTileChanged;
    }

    void UnsubscribeEvents()
    {
        MapManager.OnTileChanged -= OnTileChanged;
    }

    // 타일 변경 시 L1·L2·L3 순서로 갱신
    void OnTileChanged(Vector2Int tileCoord)
    {
        if (MapManager.Instance == null) return;
        var worldPos  = MapManager.Instance.TileToWorld(tileCoord);
        var tile      = MapManager.Instance.GetTile(tileCoord.x, tileCoord.y);
        bool walkable = tile.IsPassable;

        _l1.SetWalkable(worldPos, walkable);
        UpdateL2At(worldPos, walkable);
        UpdateL3At(worldPos, walkable);
    }

    void UpdateL2At(Vector3 worldPos, bool walkable)
    {
        var c = _l2.WorldToCell(worldPos);
        // L2 타일 안에 하나라도 불가 타일 있으면 L2도 불가 (보수적)
        if (!walkable)
            _l2.SetWalkable(c.x, c.y, false);
        else
            RebakeL2Cell(c);
    }

    void RebakeL2Cell(Vector2Int c)
    {
        var center  = _l2.CellToWorld(c);
        var halfBox = new Vector3(l2TileSize * 0.45f, 2f, l2TileSize * 0.45f);
        bool blocked = Physics.CheckBox(center, halfBox, Quaternion.identity, obstacleLayer);
        _l2.SetWalkable(c.x, c.y, !blocked);
    }

    void UpdateL3At(Vector3 worldPos, bool walkable)
    {
        var c = _l3.WorldToCell(worldPos);
        if (!walkable)
            _l3.SetWalkable(c.x, c.y, false);
        else
        {
            var center  = _l3.CellToWorld(c);
            var halfBox = new Vector3(l3TileSize * 0.45f, 2f, l3TileSize * 0.45f);
            _l3.SetWalkable(c.x, c.y, !Physics.CheckBox(center, halfBox, Quaternion.identity, obstacleLayer));
        }
    }

    // 2~3초마다 공유 그리드 밖 유닛 클러스터 재계산
    IEnumerator ClusterUpdateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(ClusterUpdateInterval);

            var outOfRange = new List<Vector3>();
            foreach (var brain in FindObjectsByType<UnitBrain>(FindObjectsSortMode.None))
            {
                var pos = brain.transform.position;
                if (_l1.GetGridFor(pos) == null)
                    outOfRange.Add(pos);
            }
            _l1.UpdateClusters(outOfRange);

            // DungeonCore 이동 시 공유 그리드 재중심
            if (DungeonCore.Instance != null)
                _l1.RecenterShared(DungeonCore.Instance.transform.position);
        }
    }
}
