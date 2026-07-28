// 하수인 BT: (도주) → 감지+반격 → 휴식 → 식사 → 순찰 → 작업 → 대기
// 도주는 기본 비활성(성향 Fight) — AI 프로필을 붙이거나 SetStanceOverride로 켤 때만 동작한다.
public partial class UnitBrain
{
    BTNode BuildMinionTree()
    {
        return new BTSelector()
            .Add(PlayerFleeNode())
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
