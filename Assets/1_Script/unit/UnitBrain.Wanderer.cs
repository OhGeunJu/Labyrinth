// 방랑자 BT: 소집 → **직접 명령** → 줍기 → 따라가기 → (도주) → 전투 → 휴식 → 식사 → 작업 → 대기
// 도주는 기본 비활성(성향 Fight) — AI 프로필을 붙이거나 SetStanceOverride로 켤 때만 동작한다.
// 직접 명령(우클릭 이동)이 전투보다 위인 이유: 아래에 두면 FightNode가 매 틱 목적지를
// 적 위치로 덮어써 조작이 안 먹는다. 도착하면 자동 해제되어 평소 행동으로 돌아간다.
public partial class UnitBrain
{
    BTNode BuildWandererTree()
    {
        return new BTSelector()
            .Add(new BTSequence()
                .Add(new ConditionNode(bb => bb.IsDrafted))
                .Add(new DraftedNode()))
            .Add(new ManualOrderNode())
            .Add(new PickUpNode())
            .Add(new FollowNode())
            .Add(PlayerFleeNode())
            .Add(new BTSequence()
                .Add(new ConditionNode(bb => bb.Target != null))
                .Add(new FightNode()))
            .Add(new RestNode())
            .Add(new EatNode())
            .Add(new WorkNode())
            .Add(new IdleNode());
    }
}
