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
                // **하수인은 명령을 안 받으므로 사람이 눌러 줄 길이 없다** —
                // `AbilityController`가 `AcceptsOrders`로 거른다. 여기가 유일한 자리다.
                .Add(new UseAbilityNode())
                .Add(new FightNode()))
            .Add(new RestNode())
            .Add(new EatNode())
            // **몸이 셋이라 채우는 자리도 셋이다** — 골렘은 충전대, 해골은 제단으로
            // 간다(`RechargeNode`). 산 몸에게는 첫 줄에서 실패로 흘러가므로
            // 트리를 몸마다 나누지 않는다. **일감보다 위여야 한다** — 에너지가
            // 바닥난 골렘을 그 자리에 세우는 것이 이 노드이기 때문이다.
            .Add(new RechargeNode())
            .Add(new PatrolNode())
            .Add(new WorkNode())
            .Add(new IdleNode());
    }
}
