// 하수인 BT: (도주) → 감지+반격 → 휴식 → 식사 → 충전 → 순찰 → 작업 → 대기
// 도주는 기본 비활성(성향 Fight) — AI 프로필을 붙이거나 SetStanceOverride로 켤 때만 동작한다.
//
// **직접 명령 갈래가 없다**(2026-10-28에 걷어냈다). 하수인은 명령을 안 받는다(`AcceptsOrders`) — 우클릭 이동 ·
// 포탈(`CanObeyNow`) · 상단(하수인은 핵 곁을 안 떠난다)이 다 거른다. 예전에 이 갈래를 세운 이유였던 원정 편성은
// 옛 월드맵과 함께 없어졌다.
public partial class UnitBrain
{
    BTNode BuildMinionTree()
    {
        var tree = new BTSelector();
        tree.Add(PlayerFleeNode());
        // **하수인은 명령을 안 받으므로 사람이 눌러 줄 길이 없다** — `AbilityController`가 `AcceptsOrders`로
        // 거른다. 능동을 스스로 쓰는 자리는 전투 묶음 안이다(`AddFight`).
        AddFight(tree, skipWhenFleeing: false);
        return tree.Add(new RestNode())
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
