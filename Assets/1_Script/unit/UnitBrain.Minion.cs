// 하수인 BT: 감지+반격 → 휴식 → 식사 → 순찰 → 작업 → 대기
public partial class UnitBrain
{
    BTNode BuildMinionTree()
    {
        return new BTSelector()
            .Add(new BTSequence()
                .Add(new DetectNode())
                .Add(new FightNode()))
            .Add(new RestNode())
            .Add(new EatNode())
            .Add(new PatrolNode())
            .Add(new WorkNode())
            .Add(new IdleNode());
    }
}
