using System.Collections.Generic;
using UnityEngine;

// L1 (1×1 타일) 그리드 관리.
// - 공유 그리드: DungeonCore 기준 80×80 타일
// - 클러스터 그리드: 공유 범위 밖 유닛 그룹별 40×40 타일 (그룹 평균 위치 기준)
public class LocalGridManager
{
    const int   SharedSize         = 80;
    const int   ClusterSize        = 40;
    const float ClusterGroupRadius = 20f;

    PathGrid  _sharedGrid;
    Vector3   _sharedCenter;
    LayerMask _obstacleLayer;

    readonly List<(PathGrid grid, Vector3 center)> _clusterGrids = new();

    public void Initialize(Vector3 dungeonCorePos, LayerMask obstacleLayer)
    {
        _obstacleLayer = obstacleLayer;
        _sharedCenter  = dungeonCorePos;
        _sharedGrid    = CreateGrid(SharedSize, dungeonCorePos);
        BakeGrid(_sharedGrid, SharedSize);
    }

    public PathGrid GetGridFor(Vector3 worldPos)
    {
        if (IsInsideGrid(_sharedGrid, worldPos)) return _sharedGrid;
        foreach (var (grid, _) in _clusterGrids)
            if (IsInsideGrid(grid, worldPos)) return grid;
        return null;
    }

    public void RecenterShared(Vector3 newCenter)
    {
        if (Vector3.Distance(newCenter, _sharedCenter) < SharedSize * 0.25f) return;
        _sharedCenter = newCenter;
        _sharedGrid   = CreateGrid(SharedSize, newCenter);
        BakeGrid(_sharedGrid, SharedSize);
    }

    public void UpdateClusters(List<Vector3> outOfRangePositions)
    {
        _clusterGrids.Clear();
        if (outOfRangePositions.Count == 0) return;

        var remaining = new List<Vector3>(outOfRangePositions);
        while (remaining.Count > 0)
        {
            var seed  = remaining[0];
            var group = new List<Vector3>();
            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(remaining[i], seed) <= ClusterGroupRadius)
                { group.Add(remaining[i]); remaining.RemoveAt(i); }
            }

            Vector3 avg = Vector3.zero;
            foreach (var p in group) avg += p;
            avg /= group.Count;

            var grid = CreateGrid(ClusterSize, avg);
            BakeGrid(grid, ClusterSize);
            _clusterGrids.Add((grid, avg));
        }
    }

    public void SetWalkable(Vector3 worldPos, bool walkable)
    {
        UpdateTileInGrid(_sharedGrid, worldPos, walkable);
        foreach (var (grid, _) in _clusterGrids)
            UpdateTileInGrid(grid, worldPos, walkable);
    }

    // ── 내부 헬퍼 ──────────────────────────────────────────────────────

    PathGrid CreateGrid(int size, Vector3 center)
    {
        var origin = new Vector3(center.x - size * 0.5f, center.y, center.z - size * 0.5f);
        return new PathGrid(size, size, 1f, origin);
    }

    void BakeGrid(PathGrid grid, int size)
    {
        var halfBox = new Vector3(0.45f, 2f, 0.45f);
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            var worldPos = grid.CellToWorld(new Vector2Int(x, y));
            bool blocked = Physics.CheckBox(worldPos, halfBox, Quaternion.identity, _obstacleLayer);
            grid.SetWalkable(x, y, !blocked);
        }
    }

    bool IsInsideGrid(PathGrid grid, Vector3 pos)
    {
        float minX = grid.Origin.x, maxX = grid.Origin.x + grid.Width  * grid.TileSize;
        float minZ = grid.Origin.z, maxZ = grid.Origin.z + grid.Height * grid.TileSize;
        return pos.x >= minX && pos.x < maxX && pos.z >= minZ && pos.z < maxZ;
    }

    void UpdateTileInGrid(PathGrid grid, Vector3 worldPos, bool walkable)
    {
        if (!IsInsideGrid(grid, worldPos)) return;
        var c = grid.WorldToCell(worldPos);
        grid.SetWalkable(c.x, c.y, walkable);
    }
}
