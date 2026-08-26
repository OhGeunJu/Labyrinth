// 방랑자 BT: **직접 명령** → 업기 → 줍기 → 따라가기 → (도주) → 전투 → 휴식 → 식사 → 작업 → 대기
//
// **소집은 분기가 아니라 게이트다**(2026-08-20). 한동안 맨 위에 `DraftedNode` 분기가 있었는데
// 그것이 `Running`만 돌려줘서 **켜는 순간 아래가 통째로 멎었다** — 이동 명령도, 반격도 안 먹는
// 소집이었다(그래서 부르는 입력도 안 붙어 있었다). 소집은 *무엇을 하는 상태*가 아니라
// **자동 행동을 멈추고 싸울 태세로 두는 상태**라, 트리 자리를 차지할 것이 아니라 아래 노드들이
// 저마다 물어야 하는 것이다(업고 있는 것과 같은 모양 — `WorkNode`·`RestNode`가 그렇게 묻는다).
// 도주는 기본 비활성(성향 Fight) — AI 프로필을 붙이거나 SetStanceOverride로 켤 때만 동작한다.
// 직접 명령(우클릭 이동)이 전투보다 위인 이유: 아래에 두면 FightNode가 매 틱 목적지를
// 적 위치로 덮어써 조작이 안 먹는다. 도착하면 자동 해제되어 평소 행동으로 돌아간다.
public partial class UnitBrain
{
    BTNode BuildWandererTree()
    {
        var tree = new BTSelector()
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
            // **먼저 거는가는 성향이 정한다**(P패널) — 기본값 `반격`이면 안 훑는다. 훑으면
            // 지나가는 적을 보고 저 혼자 달려 나가서, 어디에 세워 둘지가 명령이 아니라 지형의
            // 문제가 된다. 지목한 표적은 그대로 유지된다(`DetectNode`가 기존 `Target`을 안 버린다).
            //
            // **소집하면 오히려 안 건다** — 소집은 *"내가 조종할 때까지 그 자리에 있어라"*다.
            // 다만 맞으면 되받아치고 **곁의 적까지는** 문다(짧은 목줄).
            .Add(new BTSequence()
                .Add(new DetectNode())
                .Add(new FightNode()))
            .Add(new RestNode())
            .Add(new EatNode())
            .Add(new WorkNode())
            .Add(new IdleNode());

        // **역할 기본값은 `반격`이다** — P패널에서 아무것도 안 고른 폰(`자동`)은 지금까지처럼
        // 먼저 걸지 않는다. `교전`으로 두면 **판을 시작하는 순간 모든 폰이 저 혼자 달려 나가서**,
        // 어디에 세워 둘지가 명령이 아니라 지형의 문제가 된다.
        //
        // **반드시 트리를 조립한 *뒤*에 적을 것** — 위의 `PlayerFleeNode()`가 그 안에서
        // `_roleStanceFallback = Fight`로 덮는다(하수인·적과 공용이라 그쪽 기본값을 세운다).
        // 앞에 적으면 조용히 지워져 **`자동`이 교전이 되고, 판이 열리자마자 전원이 달려 나간다.**
        _roleStanceFallback = CombatStance.Retaliate;
        return tree;
    }
}
