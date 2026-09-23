using UnityEngine;

// **땅 한 칸을 목표 높이로 고른다**(2026-09-24).
//
// `MineJob`과 **짝이고 모양이 같다**: 찍어 둔 칸이 큐로 흐르고, 표시가 원본이며 일감은 거기서
// 파생된다(`LevelJobBoard`). 갈리는 것은 둘뿐이다 —
//
//   · 채굴은 **바위 칸**을 겨누므로 그 위에 못 서고 둘러싸야 하는데, 여기는 **걸을 수 있는
//     땅**이라 그 칸에서 해도 된다. 그래도 `HandReach`(2m)를 쓴다: 파는 흙 위에 서 있는
//     그림이 어색하고, 무엇보다 **고르는 도중에 발밑이 오르내린다.**
//   · 채굴은 파면 칸이 없어지지만 여기는 **높이만 바뀐다.** 그래서 끝났는지를 *"아직 바위냐"*가
//     아니라 **`GroundLevel.IsSettled`**가 답한다.
//
// **깎으면 돌이 나오고 메우면 돌을 먹는다** — 대칭이라 *"높은 데를 깎아 낮은 데를 메운다"*가
// 저절로 성립한다. 메울 재료는 **창고에서 바로 뺀다**(`MaterialPool` — 화장터·작업대 등급
// 올리기와 같은 길이다: 청사진처럼 납품을 기다리게 하면 흙 한 삽에 운반 일감이 딸린다).
public class LevelGroundJob : JobBase
{
    public override JobCategory Category => JobCategory.Leveling;
    public override WorkAnim    Anim     => WorkAnim.Mining;   // 삽질 클립이 없다 — 곡괭이를 쓴다

    // **옆에서 한다** — 근거는 위 주석. 채굴의 `NodeReach`와 같은 값이다.
    public override float Reach => 2.0f;

    readonly Vector2Int _tile;
    readonly float      _target;

    float _work;

    public LevelGroundJob(Vector2Int tile, float targetY)
    {
        _tile   = tile;
        _target = targetY;

        TileCoord = tile;
        Priority  = 0;
    }

    public Vector2Int Tile => _tile;

    // 얼마나 고쳐야 하나(m, 부호 있음 — 양수면 깎는다).
    //
    // **붙들지 않고 매번 다시 잰다.** 처음엔 잡을 때 한 번 재 두려 했는데 그러면 **낡는다** —
    // 칸 하나를 덮는 정점이 최대 3×3이라 **이웃 칸을 고르면 이 칸도 같이 맞으므로**, 붙들어
    // 둔 값은 *이미 하지 않아도 되는 일*을 계속 요구한다. 줄어드는 것이 맞는 답이다:
    // 실제로 덜 파도 되니 **작업량도 산출량도 그만큼 줄어야** 한다(안 줄이면 흙을 안 떠내고
    // 돌이 나온다).
    //
    // 값도 싸다 — 정점 아홉을 보는데 그것들은 이미 담겨 있다(`VertexHeightGlobal`).
    float Need => GroundLevel.SignedRemaining(_tile, _target);

    float Required => Mathf.Max(1f, Mathf.Abs(Need) * GroundLevel.WorkPerMeter);

    // **아직 할 일이 있나.** 표시가 살아 있고, 고를 수 있는 자리이고, 아직 안 맞았나.
    //
    // **재료가 없으면 아직 일감이 아니다** — 메우는 칸인데 돌이 없으면 폰이 걸어와서
    // 빈손으로 접고 다시 온다(`IsReady`로 막는 것이 `CanAssignTo`와 같은 규칙이다).
    public override bool IsReady =>
        GroundLevel.IsMarked(_tile)
        && GroundLevel.CanLevel(_tile)
        && !GroundLevel.IsSettled(_tile, _target)
        && (Need > 0f || HasFill());

    // 메울 돌이 있나. **`AvailableNear`가 그 물음의 자리다** — 주석이 *"일감을 등록할지
    // 말지 볼 때 쓴다(그때는 누가 할지 아직 모른다)"*라고 적고 있고, **거점으로 걸러 주므로**
    // 29km 밖 원정대의 배낭을 여기 재료로 세지 않는다.
    bool HasFill()
    {
        var item = GroundLevel.Spoil;
        if (item == null) return true;   // 저작이 비었으면 값을 안 물린다

        var map = MapManager.Instance;
        Vector3 at = map != null ? map.TileToWorld(_tile)
                                 : new Vector3(_tile.x + 0.5f, 0f, _tile.y + 0.5f);
        return MaterialPool.AvailableNear(item, at) >= GroundLevel.SpoilFor(Need);
    }

    protected override void Execute(UnitBrain unit)
    {
        if (!GroundLevel.IsMarked(_tile) || GroundLevel.IsSettled(_tile, _target))
        {
            Status = JobStatus.Done;     // 누가 먼저 했거나 이웃 칸을 고르며 같이 맞았다
            return;
        }

        var stats = unit != null ? unit.Stats : null;
        float power = stats != null ? Mathf.Max(0.1f, stats.Mining) : 1f;

        // **틱 델타를 곱한다** — 안 곱하면 배속이 밸런스를 바꾼다(`ResourceNode.Work`가
        // 시그니처로 못 박은 그 사고).
        float dt = unit != null && unit.Blackboard != null ? unit.Blackboard.TickDelta : Time.deltaTime;
        _work += power * dt;

        stats?.GrowStat(StatName.Mining, dt * 0.05f);

        if (_work < Required) return;

        Finish(unit);
    }

    void Finish(UnitBrain unit)
    {
        float need = Need;
        var   item = GroundLevel.Spoil;
        int   n    = GroundLevel.SpoilFor(need);

        // **메우는 쪽은 재료를 먼저 치른다** — 못 치르면 아무 일도 안 일어나야 한다.
        // 순서를 뒤집으면 **땅은 올라갔는데 돌은 그대로**다.
        if (need < 0f && item != null)
        {
            if (!MaterialPool.Consume(item, n, unit))
            {
                Status = JobStatus.Done;   // 그 사이에 남이 썼다 — 판이 다시 낸다
                return;
            }
        }

        if (!GroundLevel.Apply(_tile, _target))
        {
            Status = JobStatus.Done;
            return;
        }

        // **깎은 흙은 돌로 나온다.** 캔 돌과 같은 규칙으로 **저절로 실려 간다** — 설비가
        // 내놓는 것과 같은 자리라(전리품이 아니라 생산물이다) 사람이 따로 찍어 줄 이유가 없다.
        if (need > 0f && item != null)
        {
            Vector3 drop = unit != null ? unit.transform.position : Vector3.zero;
            DroppedItem.Spawn(item, n, drop)?.SetHaulAllowed(true);
        }

        Status = JobStatus.Done;
    }
}
