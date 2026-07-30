// 하수인 BT: 직접 명령 → (도주) → 감지+반격 → 휴식 → 식사 → 순찰 → 작업 → 대기
// 도주는 기본 비활성(성향 Fight) — AI 프로필을 붙이거나 SetStanceOverride로 켤 때만 동작한다.
//
// **직접 명령(`ManualOrderNode`)이 맨 위에 있는 이유**: 원정 편성 시 하수인도 집결 지점까지
// 걸어가야 하는데, 이 갈래가 없으면 `SetMoveTarget`을 걸어도 다음 틱에 작업/순찰이 목적지를
// 덮어써 제자리로 돌아간다. 우클릭 이동은 여전히 방랑자 전용이므로(`MoveController`)
// 여기에 `ManualMove`를 세우는 건 원정 편성뿐이다.
public partial class UnitBrain
{
    BTNode BuildMinionTree()
    {
        return new BTSelector()
            .Add(new ManualOrderNode())
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
