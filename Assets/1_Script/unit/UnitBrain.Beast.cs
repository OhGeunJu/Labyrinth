// **가축 — 길들여 우리 편이 된 짐승**(2026-10-01, `Conversion.CanTame` · 사용자: *"개미 같은 동물한테 운반할 수
// 있게 마차 같은거를 붙여"*).
//
// 방랑자 트리를 그대로 태우면 늑대가 작업대 앞에 서고 곰이 연구를 한다. 짐승이 하는 일은 셋이다: **짐을 나르고**
// (운반 — `BeastJobs`) · **따라다니고**(따라가기 명령) · **덤비면 문다**(반격). 사람과 같은 노드를 쓰되 줄이 짧다.
//
// **명령은 수레를 단 동안만 받는다**(`AcceptsOrders` · `PawnCart`) — 맨몸의 짐승은 저 알아서 짐을 나르고, 수레를 달면
// 짐마차가 되어 사람이 끈다(사용자: *"마차에 착용되면 직접조종"*).
public partial class UnitBrain
{
    // 가축인가 — 길들일 수 있는 종족 · 우리 편 · 역할 없음. **세력이 답한다**(같은 늑대 에셋을 야생도 쓴다 —
    // `IsSummoned`가 하수인 종족에 대해 적어 둔 그 규칙).
    public bool IsTamedBeast =>
        unitData != null && unitData.Tameable && unitData is not MinionData && npcData == null && !UsesEnemyAi;

    // 가축이 할 줄 아는 일. 운반 하나 — 수레가 그 값을 키운다(`CarryCapacity.For`).
    static readonly JobCategory[] BeastJobs = { JobCategory.Hauling };

    BTNode BuildBeastTree()
    {
        var tree = new BTSelector()
            .Add(new ManualOrderNode())
            .Add(new FollowNode())
            .Add(PlayerFleeNode())
            .Add(new BTSequence()
                .Add(new DetectNode())
                .Add(new FightNode()))
            .Add(new RestNode())
            .Add(new EatNode())
            .Add(new WorkNode())
            .Add(new IdleNode());
        _roleStanceFallback = CombatStance.Retaliate;   // 먼저 덤비지 않는다 — 물리면 문다
        return tree;
    }
}
