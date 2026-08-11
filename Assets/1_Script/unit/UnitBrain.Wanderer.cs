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
            // **업기는 줍기보다 위다.** 둘 다 손으로 콕 집어 시킨 일인데, 사람을 업으라는
            // 명령은 대개 전투 직후 급한 것이다(피 흘리는 동료를 침대로). 물건은 기다린다.
            .Add(new CarryPawnNode())
            .Add(new PickUpNode())
            .Add(new FollowNode())
            .Add(PlayerFleeNode())
            // **맞으면 되받아친다 — 먼저 걸지는 않는다.**
            //
            // 전엔 `bb.Target != null`만 봤는데, 방랑자 트리에는 `DetectNode`가 없어서
            // **`Target`이 저 혼자 붙는 길이 아예 없었다** — 우클릭으로 지목하기 전까지는
            // 두들겨 맞으면서 가만히 서서 일했다.
            //
            // `retaliateOnly`라 범위 안의 적대를 훑지 않는다. 훑으면 지나가는 적을 보고 저
            // 혼자 달려 나가서, 어디에 세워 둘지가 명령이 아니라 지형의 문제가 된다.
            // 지목한 표적은 그대로 유지된다(`DetectNode`가 기존 `Target`을 안 버린다).
            .Add(new BTSequence()
                .Add(new DetectNode(retaliateOnly: true))
                .Add(new FightNode()))
            .Add(new RestNode())
            .Add(new EatNode())
            .Add(new WorkNode())
            .Add(new IdleNode());
    }
}
