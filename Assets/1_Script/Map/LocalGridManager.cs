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
    public List<Vector3> FindWindowPath(Vector3 center, Vector3 target, int size, float clearance,
                                        PawnMover self = null)
    {
        // 원점을 정수 좌표에 스냅 — 1m 셀이 맵 타일과 정확히 겹쳐 바닥(공허) 게이트가 타일 단위로 맞는다.
        var origin = new Vector3(Mathf.Floor(center.x) - size * 0.5f, center.y, Mathf.Floor(center.z) - size * 0.5f);
        if (_windowGrid == null || _windowGrid.Width != size)
            _windowGrid = new PathGrid(size, size, 1f, origin);
        else
            _windowGrid.Rebase(origin);

        MovementDiagnostics.RecordBake();
        BakeGrid(_windowGrid, size, clearance);
        Vector3 clamped = ClampInsideGrid(_windowGrid, target, 1.5f);
        BakeOtherPawns(_windowGrid, self, center, clamped);
        return _windowGrid.FindPath(center, clamped);
    }

    // 다른 폰을 장애물로 굽는다. 폰이 ㄷ자로 늘어서 길을 막았을 때, 반사적 회피로는
    // 국소 최소점에서 좌우 진동만 하므로 **경로 자체가 그 벽을 피해 나오게** 해야 한다.
    // 매 재계획마다 다시 구우므로 폰이 비키면 곧바로 반영된다.
    //
    // 두 곳은 비워둔다: 자기 자신(과 자기 셀)과 목적지 주변 — 그 셀이 막히면 경로가
    // 아예 안 나와 직선 폴백으로 되돌아간다(= 밀기만 하던 예전 동작).
    static void BakeOtherPawns(PathGrid grid, PawnMover self, Vector3 center, Vector3 target)
    {
        var selfCell   = grid.WorldToCell(center);
        var targetCell = grid.WorldToCell(target);

        foreach (var other in PawnMover.All)
        {
            if (other == null || other == self || other.IsSleeping) continue;

            // 물리적으로 나를 통과시키는 상대(같은 편)는 장애물이 아니다.
            // self를 안 넘긴 호출은 보수적으로 전부 막는다.
            if (self != null && !self.IsBlockedBy(other)) continue;

            // WorldToCell은 경계로 클램프하므로 윈도우 밖 폰을 먼저 걸러야 한다
            // (안 그러면 멀리 있는 폰이 윈도우 가장자리 셀을 막아버린다).
            var pos = other.transform.position;
            if (!grid.InWorldBounds(pos)) continue;

            var cell = grid.WorldToCell(pos);
            if (cell == selfCell) continue;
            if (Mathf.Abs(cell.x - targetCell.x) <= 1 && Mathf.Abs(cell.y - targetCell.y) <= 1) continue;

            grid.SetWalkable(cell.x, cell.y, false);
            MovementDiagnostics.RecordPawnBakeBlock();
        }
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
        // 공허 게이트(던전형 맵): 바닥 없는 셀은 이동 불가 — 경로가 바닥 위로만 나온다.
        var  map      = MapManager.Instance;
        bool voidGate = map != null && map.FloorRequired;

        // 셀(1m) 안에 장애물이 있는지만 본다 — **타일 단위 판정**.
        // 예전엔 여기에 에이전트 반경을 통째로 더해(0.45+0.5=0.95) 벽에 직교로 붙은 셀이
        // 전부 막혔다. 그러면 걸을 수 있는 한 줄을 얻으려고 복도를 3타일로 파야 한다.
        // 반경 부풀림은 **타일보다 큰 에이전트**에만 남긴다(폰은 타일 안에 들어가야 한다).
        float half = 0.45f + Mathf.Max(0f, clearance - 0.5f);
        var halfBox = new Vector3(half, 2f, half);
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            var worldPos = grid.CellToWorld(new Vector2Int(x, y));
            if (voidGate && !map.IsFloored(worldPos))
            {
                grid.SetWalkable(x, y, false);
                continue;
            }

            // 1m 셀은 정수 좌표로 스냅돼 맵 타일과 1:1로 겹친다.
            if (map != null)
            {
                var tile = map.WorldToTile(worldPos);

                // 건물 점유 타일 — 콜라이더 레이어와 무관하게 통행 불가.
                // (이게 빠져 있으면 건물이 U자로 둘러싼 자리를 플래너가 못 보고 직선으로 관통시킨다.)
                if (!map.GetTile(tile.x, tile.y).IsPassable)
                {
                    grid.SetWalkable(x, y, false);
                    MovementDiagnostics.RecordOccupiedBlock();
                    continue;
                }

                // 최근에 막혔던 자리 — 잠시 피해 간다
                if (PathBlockMemory.IsBlocked(tile))
                {
                    grid.SetWalkable(x, y, false);
                    MovementDiagnostics.RecordBlockMemoryHit();
                    continue;
                }
            }

            bool blocked = Physics.CheckBox(worldPos, halfBox, Quaternion.identity, _obstacleLayer);
            grid.SetWalkable(x, y, !blocked);
        }
    }
}
