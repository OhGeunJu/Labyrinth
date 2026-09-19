using UnityEngine;

// **야생 자원을 찍어서 가져온다**(2026-09-06).
//
// 지은 노드는 *배정*이라 폰이 거기 매이지만(`ResourceNode.Assign`), 야생은 **찍으면 큐에
// 올라가고 아무나 온다**(`MineJob`이 벽에 대해 하는 것과 같은 자리). 그래서 이 일감이 있다.
//
// ## `MineJob`과 무엇이 같고 무엇이 다른가
//
// 같다: 찍어서 큐에 올라가고 · **둘러서서 한다**(`NodeReach`) · 다 하면 그 자리에서 없어진다.
// 다르다: 벽은 **한 번에 사라지지만** 야생 노드는 **몇 번을 캐야 없어진다**(`TotalYields`) —
// 그래서 한 폰이 붙어 게이지를 채우는 동안 일감이 살아 있어야 한다.
//
// ## 범주는 노드가 답한다
//
// 나무면 벌목, 아니면 채굴(`ResourceNode.NeededCategory`). 여기서 `IsTree`를 또 물으면
// 답이 두 벌이 되고, 그러면 *"작업 설정에서 벌목을 껐는데 나무를 벤다"*가 생긴다.
public class GatherNodeJob : JobBase
{
    readonly ResourceNode _node;

    public override JobCategory Category => _node != null ? _node.NeededCategory : JobCategory.Mining;
    public override WorkAnim    Anim     =>
        _node != null && _node.Data != null && _node.Data.IsTree ? WorkAnim.Chopping : WorkAnim.Mining;

    // **둘러서서 캔다** — 노드 위에 설 수는 없고, `MaxWorkers`만큼 여럿이 붙는다.
    public override float Reach => NodeReach;

    // **몸에서 재고 · 폰 쪽 둘레로 가고 · 몸을 본다** — 배정으로 캐는 쪽과 같은 답(`ResourceNode.ReachFrom`).
    public override float    DistanceFrom(Vector3 pos)   => _node != null ? _node.ReachFrom(pos) : base.DistanceFrom(pos);
    public override Vector3? StandPointFor(Vector3 from) => _node != null ? _node.StandPointFor(from) : null;
    public override Vector3  FacePoint => _node != null ? _node.transform.position : base.FacePoint;

    // **없어졌거나 다 캤으면 일감이 아니다.**
    public override bool IsReady => _node != null && !_node.Depleted;

    public GatherNodeJob(ResourceNode node)
    {
        _node = node;

        // **노드 자리를 그대로 겨눈다** — `WorkSpot.TileFor`는 *저작된 설 자리*를 찾는데
        // 바위·나무에는 그런 자리가 없다(`TickAssigned`가 같은 판단을 한다).
        Priority = 4;
        if (node != null) TileCoord = node.TileCoord;
    }

    protected override void OnStart()
    {
        if (_node == null || _node.Depleted) Status = JobStatus.Done;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_node == null) { Status = JobStatus.Done; return; }

        // **한 틱 캐는 답은 한 벌이다**(`NodeWork.Tick`) — 배정으로 캐는 쪽(`WorkNode`)과
        // 여기가 각자 적으면 **스킬이 자라는 속도가 갈린다.**
        NodeWork.Tick(_node, unit, unit.Blackboard != null ? unit.Blackboard.TickDelta : Time.deltaTime);

        if (_node == null || _node.Depleted) Status = JobStatus.Done;
    }

    // **놓으면 다시 걸 수 있어야 한다** — 안 되돌리면 그 노드는 영영 일감이 안 난다.
    protected override void OnCancelled() => _node?.OnJobCancelled();
}

// **한 틱 캔다 — 부르는 곳이 둘이다**(배정 BT · 찍어서 나간 일감).
//
// 캐는 일에는 세 가지가 같이 붙어 있다: 어떤 스탯을 쓰나 · 게이지를 얼마나 채우나 · 경험을
// 얼마나 주나. 그 셋을 두 곳에 적으면 **하나만 고치는 날**이 오고, 그러면 *"찍어서 캘 때가
// 배정해서 캘 때보다 빠르다"*가 된다(플레이어는 그걸 규칙으로 읽는다).
public static class NodeWork
{
    public static void Tick(ResourceNode node, UnitBrain brain, float deltaTime)
    {
        if (node == null || brain == null || brain.Stats == null) return;

        bool cutting = node.Data != null && node.Data.IsTree;
        var  stat    = cutting ? StatName.Cutting : StatName.Mining;

        float skill = brain.Stats.GetStat(stat);
        node.Work(skill, deltaTime);

        float required = node.Data != null ? node.Data.RequiredSkill : 0f;
        brain.Stats.GrowStat(stat, 0.05f * deltaTime
                                   * CombatXp.ChallengeMult(skill, required)
                                   * CombatXp.Sol(skill));
    }
}
