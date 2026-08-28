using System.Collections.Generic;
using UnityEngine;

// 유닛 중심 1m 로컬 윈도우 길찾기. 윈도우 그리드 1개를 풀링해 모든 유닛이 공용.
public class LocalGridManager
{
    LayerMask _obstacleLayer;

    // 벼랑 옆 칸의 이동 비용 배율.
    //
    // **1.6으로 뒀던 것이 잘못이었다**(2026-08-28). 한 칸 비켜 가는 값(약 1.4)보다 크게
    // 잡으면 *"한 칸이라도 벼랑에서 떨어져라"*가 되어, 기단 가장자리를 따라가야 하는
    // 길에서 **경로가 안쪽으로 크게 부푼다** — 모서리를 도는데 마당 한복판까지 나갔다
    // 오는 그림이 그것이었다. 가장자리 칸 하나마다 한 칸씩 우회를 사 준 셈이다.
    //
    // 이건 **선호지 회피가 아니다.** 같은 길이면 안쪽으로 가고, 돌아가야 하면 그냥
    // 가장자리로 간다. 몸이 얇아진 뒤로(`PawnMover.BodyRadius` 0.35) 가장자리에서
    // 문지를 일도 줄어 큰 값을 물릴 이유가 없다.
    const float LedgeCost = 1.15f;

    // 풀링된 윈도우 그리드 (모든 유닛 공용 — 길찾기는 동기라 안전).
    PathGrid _windowGrid;

    public void Initialize(LayerMask obstacleLayer) => _obstacleLayer = obstacleLayer;

    // 유닛 중심 윈도우 그리드를 해당 위치로 재배치·재베이크해 target까지 Theta* 경로 반환.
    // 매번 재베이크하므로 런타임에 생긴 벽(Wall 콜라이더)도 항상 반영된다.
    // target이 윈도우 밖이면 경계 안쪽으로 클램프해 점진 이동을 유도한다.
    public List<Vector3> FindWindowPath(Vector3 center, Vector3 target, int size, float clearance,
                                        PawnMover self = null)
    {
        // **셀을 맵 타일에 정확히 포갠다** — 정수 좌표에 스냅하면 안 된다.
        //
        // 08-27에 격자의 위상이 첫 조작으로 옮겨 간 뒤로 타일 경계는 `정수 + 위상`이다.
        // 정수에 스냅하면 셀 하나가 **타일 둘에 걸치고**, 그러면 셀 중심에서 재는 것이
        // 전부 반 칸씩 어긋난다 — 점유·바닥·높이 조회도, 무엇보다 장애물 검사(반경 0.45)가
        // **벽 칸에 반 칸 걸쳐** 벽 옆 칸을 통째로 막는다. 오두막 문간이 안 열리던 것이
        // 이것이었다(문 앞 칸이 벽에 걸려 막혔다).
        var map0 = MapManager.Instance;
        var ph   = map0 != null ? map0.GridPhase : Vector2.zero;
        var ct   = map0 != null
            ? map0.WorldToTile(center)
            : new Vector2Int(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z));

        int half   = size / 2;
        var origin = new Vector3(ct.x - half + ph.x, center.y, ct.y - half + ph.y);
        if (_windowGrid == null || _windowGrid.Width != size)
            _windowGrid = new PathGrid(size, size, 1f, origin);
        else
            _windowGrid.Rebase(origin);

        MovementDiagnostics.RecordBake();
        _windowGrid.UsesHeight = MapManager.Instance != null;
        _windowGrid.MaxDrop    = PawnMover.DropDown;
        BakeGrid(_windowGrid, size, clearance);
        Vector3 clamped = ClampInsideGrid(_windowGrid, target, 1.5f);
        BakeOtherPawns(_windowGrid, self, center, clamped);

        // **다른 폰까지 구운 뒤에 잰다** — 지름길이 스치면 안 되는 것에는 폰도 든다.
        // 격자 간선은 그대로 두고 직선 판정에만 여유를 준다.
        _windowGrid.BuildClearance();

        return _windowGrid.FindPath(center, clamped);
    }

    // 다른 폰을 장애물로 굽는다. 폰이 ㄷ자로 늘어서 길을 막았을 때, 반사적 회피로는
    // 국소 최소점에서 좌우 진동만 하므로 **경로 자체가 그 벽을 피해 나오게** 해야 한다.
    // 매 재계획마다 다시 구우므로 폰이 비키면 곧바로 반영된다.
    //
    // 두 곳은 비워둔다: **자기 자신(과 자기 셀)**과 **목적지 칸**.
    //
    // 목적지 쪽은 한때 **둘레 한 칸까지** 비워 뒀다. 그 셀이 막히면 경로가 아예 안 나오고
    // 직선 폴백으로 되돌아갔기 때문이다(= 밀기만 하던 예전 동작). 그런데 그 여유가
    // **목적지 옆에 선 NPC를 통째로 안 보이게 만들었다** — 떨어진 물건 옆에 상인이 서
    // 있으면 폰이 그를 향해 곧장 걸어가 밀고 있었다.
    //
    // 이제 안 비워도 된다. `FindPath`가 **도착 셀이 막히면 가까운 걷는 칸으로 스냅**하고,
    // 그래도 못 닿으면 **가장 가까이까지의 부분 경로**를 낸다(2026-08-28). 직선 폴백으로
    // 떨어지는 길이 이미 막혀 있다. 목적지 칸 하나만 남긴다 — *그 폰에게 다가가는 것*이
    // 목적일 때(따라가기·말 걸기) 그 칸이 막히면 아무 데도 못 간다.
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
            if (cell == selfCell || cell == targetCell) continue;

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

            // **딛는 면과 그 칸으로 올라설 수 있는 높이 — 칸을 건너뛰기 전에 굽는다.**
            // 격자는 재사용되므로(`Rebase`) 어느 한 칸이라도 빠지면 **지난번 자리의 높이가
            // 그대로 남고**, 지름길 검사(`ClimbOk`)가 그 헛값을 읽어 벽을 통과하는 직선을
            // 내준다. 바닥이 깔린 칸은 옆면이 수직이라 폰의 `stepOffset`까지만, 자연 지형은
            // 비탈이라 `slopeLimit`이 판단한다.
            if (map != null)
            {
                var here = map.WorldToTile(worldPos);
                grid.SetSurface(x, y, map.SurfaceHeight(here), map.ClimbLimitAt(here));
            }

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

        // 벼랑에 몸을 문지르며 걷지 않도록 — 막지는 않고 값만 올린다.
        grid.PenalizeLedges(LedgeCost);
    }
}
