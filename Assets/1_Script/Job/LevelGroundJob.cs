using UnityEngine;

// **땅 정점 하나를 목표 높이로 옮긴다**(2026-09-24 · 09-25에 칸에서 정점으로).
//
// `MineJob`과 **짝이고 모양이 같다**: 찍어 둔 것이 큐로 흐르고, 표시가 원본이며 일감은 거기서
// 파생된다(`LevelJobBoard`). 갈리는 것은 둘뿐이다 —
//
//   · 채굴은 **바위 칸**을 겨누지만 여기는 **땅의 높이 정점**을 겨눈다. **폰은 그 정점이 움직이는
//     땅 밖에 선다** — 근거는 `StandTile`.
//   · 채굴은 파면 칸이 없어지지만 여기는 **높이만 바뀐다.** 그래서 끝났는지를
//     **`GroundLevel.IsSettled`**가 답한다.
//
// **정점인 이유는 `GroundLevel` 머리 주석** — 땅 높이가 2m 정점으로만 있어서 칸 단위로는
// 찍은 것과 움직인 것이 안 맞았다.
//
// **내리면 돌이 나오고 올리면 돌을 먹는다.** 올릴 재료는 **창고에서 바로 뺀다**(`MaterialPool` —
// 화장터·작업대 등급 올리기와 같은 길: 청사진처럼 납품을 기다리게 하면 흙 한 삽에 운반이 딸린다).
public class LevelGroundJob : JobBase
{
    public override JobCategory Category => JobCategory.Leveling;
    public override WorkAnim    Anim     => WorkAnim.Mining;   // 삽질 클립이 없다 — 곡괭이를 쓴다

    // **정해 준 칸에 선다** — 그 칸이 곧 *안 움직이는 땅*이라 옆으로 비키면 규칙이 풀린다.
    public override float Reach => StandReach;

    readonly Vector2Int _vertex;
    readonly float      _target;

    float _work;

    public LevelGroundJob(Vector2Int vertex, float targetY)
    {
        _vertex = vertex;
        _target = targetY;

        TileCoord = StandTile(vertex);
        Priority  = 0;
    }

    public Vector2Int Vertex => _vertex;

    // **자기가 서 있는 땅은 파지 않는다**(2026-09-25, 사용자: *"내리다가 자기있던 칸을 먼저 내려서
    // 오도가도 못하는 상황"*). 정점 하나는 둘레 ±2m를 움직이므로, 전에는 정점 바로 옆 칸에 서서
    // **제 발밑을 같이 내렸고** 두 단쯤 내려가면 구덩이 가장자리가 오를 수 없는 비탈이 됐다.
    //
    // 그래서 **그 정점이 움직이는 넓이(칸 `[2V-2, 2V+1]`) 바로 바깥 한 칸 둘레**에서 고른다. 고르는 순서:
    //   ① 바위·점유가 아니고 설 수 있을 것(`IsWorkable`)
    //   ② **다른 표시에도 안 걸린 땅**(`IsStable`) — 옆 정점이 곧 움직일 땅에 서면 같은 일을 남이 한다
    //   ③ 정점에 가까울 것(둘레 스무 칸 중 모서리보다 변 한가운데)
    // 하나도 없으면 정점 자리 칸으로 떨어진다(못 가면 판이 쉬게 하고 다시 낸다).
    static Vector2Int StandTile(Vector2Int v)
    {
        int s   = TerrainGrid.VertexSpacing;
        var map = MapManager.Instance;
        int lo  = v.x * s - s - 1, hi = v.x * s + s;       // x 범위 [2V-3, 2V+2]
        int loZ = v.y * s - s - 1, hiZ = v.y * s + s;

        Vector2Int best = new(v.x * s, v.y * s);
        int bestScore = int.MaxValue;
        var center = new Vector2(v.x * s, v.y * s);

        for (int z = loZ; z <= hiZ; z++)
        for (int x = lo; x <= hi; x++)
        {
            if (x != lo && x != hi && z != loZ && z != hiZ) continue;   // 둘레만
            var t = new Vector2Int(x, z);
            if (!TerrainGrid.TileInWorld(t)) continue;
            if (RockMap.Enabled && RockMap.IsRock(t)) continue;
            if (map != null && !map.IsWorkable(t)) continue;

            int score = Mathf.RoundToInt(Vector2.Distance(center, new Vector2(x + 0.5f, z + 0.5f)) * 10f);
            if (!IsStable(t)) score += 1000;
            if (score < bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

    // **그 칸의 땅이 앞으로 안 움직이나** — 칸 `t`는 정점 칸 `floor(t/2)` 안에 있어 귀퉁이 넷의 영향만
    // 받는다. 그 넷 중 아직 목표에 안 닿은 표시가 있으면 곧 움직일 땅이다.
    static bool IsStable(Vector2Int t)
    {
        int s = TerrainGrid.VertexSpacing;
        int kx = Mathf.FloorToInt(t.x / (float)s), kz = Mathf.FloorToInt(t.y / (float)s);
        for (int dz = 0; dz <= 1; dz++)
        for (int dx = 0; dx <= 1; dx++)
        {
            var c = new Vector2Int(kx + dx, kz + dz);
            if (GroundLevel.TryTarget(c, out float y) && !GroundLevel.IsSettled(c, y)) return false;
        }
        return true;
    }

    // 얼마나 옮겨야 하나(m, 부호 있음 — 양수면 내린다). **매번 다시 잰다** — 이웃이 먼저
    // 움직여도 이 정점은 안 바뀌지만, 같은 정점을 남이 먼저 옮겼으면 0이 된다.
    float Need => GroundLevel.SignedRemaining(_vertex, _target);

    public override bool IsReady =>
        GroundLevel.IsMarked(_vertex)
        && GroundLevel.CanMoveVertex(_vertex)
        && !GroundLevel.IsSettled(_vertex, _target)
        && (Need > 0f || HasFill());

    // 올릴 돌이 있나. **`AvailableNear`가 그 물음의 자리다** — 거점으로 걸러 주므로 멀리 나간
    // 원정대의 배낭을 여기 재료로 세지 않는다.
    bool HasFill()
    {
        var item = GroundLevel.Spoil;
        if (item == null) return true;   // 저작이 비었으면 값을 안 물린다
        return MaterialPool.AvailableNear(item, GroundLevel.VertexWorld(_vertex)) >= GroundLevel.SpoilFor(Need);
    }

    protected override void Execute(UnitBrain unit)
    {
        if (!GroundLevel.IsMarked(_vertex) || GroundLevel.IsSettled(_vertex, _target))
        {
            Status = JobStatus.Done;
            return;
        }

        var stats = unit != null ? unit.Stats : null;
        float power = stats != null ? Mathf.Max(0.1f, stats.Mining) : 1f;

        // **틱 델타를 곱한다** — 안 곱하면 배속이 밸런스를 바꾼다.
        float dt = unit != null && unit.Blackboard != null ? unit.Blackboard.TickDelta : Time.deltaTime;
        _work += power * dt;

        stats?.GrowStat(StatName.Mining, dt * 0.05f);

        if (_work < GroundLevel.WorkFor(Need)) return;

        Finish(unit);
    }

    void Finish(UnitBrain unit)
    {
        float need = Need;
        var   item = GroundLevel.Spoil;
        int   n    = GroundLevel.SpoilFor(need);

        // **올리는 쪽은 재료를 먼저 치른다** — 못 치르면 아무 일도 안 일어나야 한다.
        if (need < 0f && item != null && !MaterialPool.Consume(item, n, unit))
        {
            Status = JobStatus.Done;   // 그 사이에 남이 썼다 — 판이 다시 낸다
            return;
        }

        if (!GroundLevel.Apply(_vertex, _target))
        {
            Status = JobStatus.Done;
            return;
        }

        // **내린 흙은 돌로 나온다** — 캔 돌과 같은 규칙으로 저절로 실려 간다(생산물이다).
        if (need > 0f && item != null)
        {
            Vector3 drop = unit != null ? unit.transform.position : Vector3.zero;
            DroppedItem.Spawn(item, n, drop)?.SetHaulAllowed(true);
        }

        Status = JobStatus.Done;
    }
}
