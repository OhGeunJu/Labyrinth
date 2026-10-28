using System.Collections.Generic;
using UnityEngine;

// 유닛 중심 1m 로컬 윈도우 길찾기. 윈도우 그리드 1개를 풀링해 모든 유닛이 공용.
public class LocalGridManager
{
    LayerMask _obstacleLayer;
    HierarchicalPathfinder _wide;   // 1m 걸음 표의 주인(층마다 그래프 — 아래 `BakeGrid` 설명)

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

    // 벽에 닿은 칸의 값. **한 칸 비켜 가는 값(약 1.4)보다 작아야 한다** — 크면 *"한 칸이라도
    // 안쪽으로"*가 쌓여 경로가 통째로 부푼다.
    const float WallHugCost = 1.25f;

    // 풀링된 윈도우 그리드 (모든 유닛 공용 — 길찾기는 동기라 안전).
    PathGrid _windowGrid;

    // ── 벽이 어디 있는지부터 성기게 묻는다 ──────────────────────────────────
    //
    // **칸마다 `Physics.CheckBox`를 쏘고 있었다** — 창 하나에 열린 칸 수만큼(수백 번)이고,
    // 폰이 늘면 그만큼 곱해진다. 프로파일러의 물리 쿼리 5,809건이 그것이다.
    //
    // 그런데 **지하에서 이 검사는 거의 언제나 헛돈다.** 장애물 레이어는 `Wall` 하나이고
    // 바위는 `Ground`(`RockRenderer.rockLayer`)라 **바위는 여기 안 걸린다** — 지하에서
    // 벽 콜라이더가 있는 곳은 저작한 오두막 둘레뿐이다. 굴 한복판에서 수백 번을 쏘고
    // 전부 "없음"을 받는다.
    //
    // **없다는 것은 넓게 한 번에 물을 수 있다.** 창 전체를 감싸는 상자 하나가 비면 그 안의
    // 어떤 칸 상자도 비어 있다(칸 상자들의 합집합이 그 안에 든다) — 400번이 1번이 된다.
    // 벽이 있으면 블록(8칸)으로 한 겹 좁혀, **벽이 있는 블록만** 칸마다 묻는다.
    //
    // **보수적이어야 한다** — 블록 상자에 칸 상자의 여유(`half`)를 더해 **반드시 덮게** 한다.
    // 안 더하면 블록 경계에 걸친 벽을 놓치고, 그러면 **길찾기가 벽을 통과한다**(빠뜨리는 쪽의
    // 값이 훨씬 비싸다).
    const int ObstacleBlock = 8;

    bool[] _blockHasWall;
    int    _blocksPerAxis;

    public void Initialize(LayerMask obstacleLayer, HierarchicalPathfinder wide)
    {
        _obstacleLayer = obstacleLayer;
        _wide          = wide;
    }

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

    // 창 안 어느 블록에 벽이 있는지 성기게 찍는다 — 근거는 위 `ObstacleBlock` 설명.
    //
    // **모든 셀이 같은 Y에 있다**(`PathGrid.CellToWorld`가 `Origin.y`를 쓴다) — 그래서
    // 세로 여유는 칸 상자와 **같은 2m**면 정확히 덮는다.
    void ScanWalls(PathGrid grid, int size, float half)
    {
        _blocksPerAxis = Mathf.CeilToInt(size / (float)ObstacleBlock);
        int n = _blocksPerAxis * _blocksPerAxis;
        if (_blockHasWall == null || _blockHasWall.Length != n) _blockHasWall = new bool[n];

        float ts = grid.TileSize;
        float wHalf = size * ts * 0.5f + half;
        var   wc    = new Vector3(grid.Origin.x + size * ts * 0.5f, grid.Origin.y,
                                  grid.Origin.z + size * ts * 0.5f);

        // 창 전체가 비면 블록도 안 묻는다 — 지하에서는 이 한 번으로 끝나는 것이 보통이다.
        if (!Physics.CheckBox(wc, new Vector3(wHalf, 2f, wHalf), Quaternion.identity, _obstacleLayer))
        {
            System.Array.Clear(_blockHasWall, 0, n);
            return;
        }

        for (int by = 0; by < _blocksPerAxis; by++)
        for (int bx = 0; bx < _blocksPerAxis; bx++)
        {
            int x0 = bx * ObstacleBlock, y0 = by * ObstacleBlock;
            int w  = Mathf.Min(ObstacleBlock, size - x0);
            int h  = Mathf.Min(ObstacleBlock, size - y0);

            var c = new Vector3(grid.Origin.x + (x0 + w * 0.5f) * ts, grid.Origin.y,
                                grid.Origin.z + (y0 + h * 0.5f) * ts);
            var e = new Vector3(w * ts * 0.5f + half, 2f, h * ts * 0.5f + half);

            _blockHasWall[by * _blocksPerAxis + bx] =
                Physics.CheckBox(c, e, Quaternion.identity, _obstacleLayer);
        }
    }

    // clearance = 에이전트 반경. 장애물을 이만큼 부풀려 좁은 틈은 막고
    // 넓은 길은 벽에서 떨어져 지나가게 한다(콜라이더 끼임 방지).
    void BakeGrid(PathGrid grid, int size, float clearance)
    {
        var  map = MapManager.Instance;

        // 셀(1m) 안에 장애물이 있는지만 본다 — **타일 단위 판정**.
        // 예전엔 여기에 에이전트 반경을 통째로 더해(0.45+0.5=0.95) 벽에 직교로 붙은 셀이
        // 전부 막혔다. 그러면 걸을 수 있는 한 줄을 얻으려고 복도를 3타일로 파야 한다.
        // 반경 부풀림은 **타일보다 큰 에이전트**에만 남긴다(폰은 타일 안에 들어가야 한다).
        float half = 0.45f + Mathf.Max(0f, clearance - 0.5f);
        var halfBox = new Vector3(half, 2f, half);

        ScanWalls(grid, size, half);
        grid.ClearEdges();

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            var worldPos = grid.CellToWorld(new Vector2Int(x, y));

            // **칸 좌표는 한 번만 낸다** — 아래에서 세 번 물었었다(높이 · 머리 위 · 점유).
            // 1,600칸짜리 창을 폰마다 다시 굽는 자리라 같은 답을 세 번 내는 값이 그대로 붙는다.
            Vector2Int here = default;

            // **딛는 면과 그 칸으로 올라설 수 있는 높이 — 칸을 건너뛰기 전에 굽는다.**
            // 격자는 재사용되므로(`Rebase`) 어느 한 칸이라도 빠지면 **지난번 자리의 높이가
            // 그대로 남고**, 지름길 검사(`ClimbOk`)가 그 헛값을 읽어 벽을 통과하는 직선을
            // 내준다. 바닥이 깔린 칸은 옆면이 수직이라 폰의 `stepOffset`까지만, 자연 지형은
            // 비탈이라 `slopeLimit`이 판단한다.
            if (map != null)
            {
                here = map.WorldToTile(worldPos);

                // **세계 밖으로는 못 나간다**(2026-08-30). 안 막으면 폰이 지도 밖으로 걸어
                // 나가고(로그에 칸 `(79, -2)`가 찍혔다), 거기는 지형도 바위도 함수가 답하는
                // 대로일 뿐이라 **꺼내 주는 그물이 매번 헛돈다.**
                if (!TerrainGrid.TileInWorld(here))
                {
                    grid.SetWalkable(x, y, false);
                    continue;
                }

                grid.SetSurface(x, y, map.SurfaceHeight(here), map.ClimbLimitAt(here));

                // **얇은 벽은 칸이 아니라 변을 막는다**(`EdgeWalls`) — 칸은 그대로 걸을 수 있다.
                if (EdgeWalls.Count > 0) grid.SetEdges(x, y, EdgeWalls.MaskAt(here));

                // **걸을 수 있나는 광역 그래프의 1m 표가 답한다**(2026-09-09). 그 표는
                // `IsWorkable`(점유 아님 · 머리 위 여유 — 바위는 천장이 내려앉은 자리라 여기서
                // 걸린다)을 **담아 둔 것**이고, 점유·바닥·채굴이 바뀔 때마다 그 자리만 다시
                // 굽는다. 전에는 창 1,600칸마다 점유와 머리 위를 각자 다시 물었다 — 같은 1m
                // 사실을 두 곳이 따로 뽑으면 언젠가 한쪽만 고쳐진다(`OnFloorChanged`가 광역만
                // 알던 날이 실제로 있었다). 그래프가 없는 씬은 칸에 직접 묻는다.
                bool walk = _wide != null ? _wide.TileWalkable(here, map) : map.IsWorkable(here);
                if (!walk)
                {
                    grid.SetWalkable(x, y, false);
                    MovementDiagnostics.RecordOccupiedBlock();
                    continue;
                }

                // 최근에 막혔던 자리 — 잠시 피해 간다(다른 폰 등 그래프에 없는 것에 막힌 기억)
                if (PathBlockMemory.IsBlocked(here))
                {
                    grid.SetWalkable(x, y, false);
                    MovementDiagnostics.RecordBlockMemoryHit();
                    continue;
                }
            }

            // **벽이 없는 블록에서는 안 쏜다** — 위 `ScanWalls`가 이미 넓게 물어봤다.
            bool blocked = _blockHasWall[(y / ObstacleBlock) * _blocksPerAxis + (x / ObstacleBlock)]
                        && Physics.CheckBox(worldPos, halfBox, Quaternion.identity, _obstacleLayer);
            grid.SetWalkable(x, y, !blocked);
        }

        // 벼랑에 몸을 문지르며 걷지 않도록 — 막지는 않고 값만 올린다.
        grid.PenalizeLedges(LedgeCost);

        // **벽도 같은 이유로**(2026-08-30). 지하는 길의 거의 전부가 벽에 닿아 있어서, 안
        // 걸면 경로가 모서리에 붙어 꺾이고 폰이 ㄷ자 안쪽에서 벽에 낀다. **순서가 있다** —
        // `PenalizeLedges`가 값을 전부 1로 되돌려 쓰므로 반드시 그 뒤에 곱한다.
        grid.PenalizeWalls(WallHugCost);
    }
}
