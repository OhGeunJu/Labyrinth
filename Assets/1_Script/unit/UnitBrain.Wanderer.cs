// 방랑자 BT: 소집 → 전투 → 휴식 → 식사 → 작업 → 대기
public partial class UnitBrain
{
    BTNode BuildWandererTree()
    {
        return new BTSelector()
            .Add(new BTSequence()
                .Add(new ConditionNode(bb => bb.IsDrafted))
                .Add(new DraftedNode()))
            .Add(new BTSequence()
                .Add(new ConditionNode(bb => bb.Target != null))
                .Add(new FightNode()))
            .Add(new RestNode())
            .Add(new EatNode())
            .Add(new WorkNode())
            .Add(new IdleNode());
    }
}
