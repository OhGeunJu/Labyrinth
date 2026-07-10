using System.Collections.Generic;
using UnityEngine;

// 유닛 중심 1m 로컬 윈도우 길찾기. 윈도우 그리드 1개를 풀링해 모든 유닛이 공용.
public class LocalGridManager
{
    LayerMask _obstacleLayer;

    // 풀링된 윈도우 그리드 (모든 유닛 공용 — 길찾기는 동기라 안전).
    PathGrid _windowGrid;

    public void Initialize(LayerMask obstacleLayer) => _obstacleLayer = obstacleLayer;

    // 유닛 중심 윈도우 그리드를 해당 위치로 재배치·재베이크해 target까지 Theta* 경로 반환.
    // 매번 재베이크하므로 런타임에 생긴 벽(Wall 콜라이더)도 항상 반영된다.
    // target이 윈도우 밖이면 경계 안쪽으로 클램프해 점진 이동을 유도한다.
    public List<Vector3> FindWindowPath(Vector3 center, Vector3 target, int size, float clearance)
    {
        var origin = new Vector3(center.x - size * 0.5f, center.y, center.z - size * 0.5f);
        if (_windowGrid == null || _windowGrid.Width != size)
            _windowGrid = new PathGrid(size, size, 1f, origin);
        else
            _windowGrid.Rebase(origin);

        BakeGrid(_windowGrid, size, clearance);
        Vector3 clamped = ClampInsideGrid(_windowGrid, target, 1.5f);
        return _windowGrid.FindPath(center, clamped);
    }

    static Vector3 ClampInsideGrid(PathGrid grid, Vector3 p, float margin)
    {
        float minX = grid.Origin.x + margin * grid.TileSize;
        float maxX = grid.Origin.x + (grid.Width  - margin) * grid.TileSize;
        float minZ = grid.Origin.z + margin * grid.TileSize;
        float maxZ = grid.Origin.z + (grid.Height - margin) * grid.TileSize;
        return new Vector3(Mathf.Clamp(p.x, minX, maxX), p.y, Mathf.Clamp(p.z, minZ, maxZ));
    }

    // clearance = 에이전트 반경. 장애물을 이만큼 부풀려 좁은 틈은 막고
    // 넓은 길은 벽에서 떨어져 지나가게 한다(콜라이더 끼임 방지).
    void BakeGrid(PathGrid grid, int size, float clearance)
    {
        float half = 0.45f + clearance;
        var halfBox = new Vector3(half, 2f, half);
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            var worldPos = grid.CellToWorld(new Vector2Int(x, y));
            bool blocked = Physics.CheckBox(worldPos, halfBox, Quaternion.identity, _obstacleLayer);
            grid.SetWalkable(x, y, !blocked);
        }
    }
}
