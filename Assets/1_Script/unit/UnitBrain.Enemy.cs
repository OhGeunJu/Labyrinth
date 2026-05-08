// 적 BT: 감지+전투 → 대기
public partial class UnitBrain
{
    BTNode BuildEnemyTree()
    {
        return new BTSelector()
            .Add(new BTSequence()
                .Add(new DetectNode())
                .Add(new FightNode()))
            .Add(new IdleNode());
    }
}
