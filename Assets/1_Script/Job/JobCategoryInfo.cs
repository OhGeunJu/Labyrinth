using System;

// 작업 카테고리 메타 — 표시명, 큐 편입 여부, 전역 우선순위 순서를 한 곳에서 정의한다.
//
// 이 게임의 작업은 두 종류다.
//  - **큐 작업**: 건물/농지/작업대 등이 JobManager에 일감을 등록하고, 폰이 알아서 집어간다.
//    → 폰마다 "한다/안 한다"만 정하고, 무엇을 먼저 할지는 아래 QueueOrder(전역 고정)로 정한다.
//  - **직접 배정**: 자원 노드(채굴/벌목)·순찰 구역처럼 플레이어가 폰을 콕 집어 붙이는 작업.
//    → 켜고 끌 대상이 아니므로 무리 관리 패널에서도 토글이 아니라 현재 배정 상태로만 보여준다.
//
// 새 Job 클래스를 추가할 때 그 카테고리가 QueueOrder에 없으면 **아무도 그 일감을 집지 않는다.**
// 큐를 타는 작업이면 반드시 QueueOrder에 넣을 것.
public static class JobCategoryInfo
{
    // 배열 순서 = 전역 우선순위(앞이 먼저). 모든 폰에 공통 적용된다.
    public static readonly JobCategory[] QueueOrder =
    {
        // **치료가 맨 앞이다.** 다른 일은 늦어도 되지만 피 흘리는 사람은 기다릴 수 없고,
        // 치료 일감은 다친 폰이 침대에 누워 있을 때만 생기므로 늘 있는 일도 아니다.
        JobCategory.Medical,
        // **간수는 치료 바로 뒤다.** 붙잡아 온 적은 **깨어나면 걸어 나간다**(기절 회복은
        // 시간이 정한다) — 자리에 넣는 일이 늦으면 붙잡은 것 자체가 없던 일이 된다.
        // 설득 회차도 여기 있다: 게이지가 도중에 줄지는 않지만, 우리 한 칸이 그동안 묶여 있다.
        JobCategory.Warden,
        // **채굴이 건설보다 먼저다**(2026-08-29, 지하로 엎으면서). 지하에서는 **팔 때까지
        // 지을 자리 자체가 없다** — 청사진을 놓아도 그 칸이 바위면 폰이 갈 수가 없다.
        // 파는 일이 밀리면 그 뒤의 모든 일이 같이 밀린다.
        //
        // **채굴이 큐에 들어온 것이 이번 변화다.** 전에는 `DirectAssign`에만 있었고
        // (자원 노드에 폰을 콕 집어 붙인다), 일감을 안 만들었다. 이제 **칸을 찍어 파는 길**이
        // 생기면서 둘 다가 됐다 — 노드는 여전히 직접 배정, 칸은 큐다. **큐에 안 넣으면
        // `Weight`가 0이라 찍어 놓고 아무도 안 간다**(찍기는 되는데 아무 일도 안 일어난다).
        JobCategory.Mining,
        JobCategory.Construction,
        JobCategory.Farming,
        JobCategory.Crafting,
        // **도축은 제작 뒤, 운반 앞이다.** 시체는 삭으므로(신선 2일 → 썩음 4일 → 뼈) 늦으면
        // 나올 것이 줄어든다 — 운반보다는 급하고, 사람이 걸어 둔 제작 주문보다는 덜하다.
        //
        // 한동안 **큐를 안 탔다**: 자동으로 돌리면 시키지도 않았는데 동료 시신을 손질하기
        // 때문이었다. 그 걱정을 없앤 것은 자동화가 아니라 **도축대의 규칙**이다 —
        // 무엇을 가를지 사람이 미리 정해 두므로, 폰이 알아서 집어가도 동의 없이 벌어지는 일이
        // 아니다. 우클릭 `[도축한다]`는 그대로 손으로 시키는 길로 남는다.
        JobCategory.Butcher,
        JobCategory.Research,
        JobCategory.Hauling,
        // 맨 뒤 — 남의 일감이 없을 때 자기 짐을 내린다.
        JobCategory.Unload,
    };

    // 큐를 타지 않고 플레이어가 대상에 폰을 직접 붙이는 카테고리.
    //
    // **채굴은 둘 다다**(2026-08-29) — 자원 노드는 여기(폰을 콕 집어 붙인다), **칸을 찍어
    // 파는 것은 큐**다(`MineJob`). 지하에서 파는 칸은 수백이라 하나씩 붙일 수가 없다.
    // 한쪽만 남기면 다른 쪽이 통째로 죽으므로 **두 목록에 다 있는 것이 맞다.**
    public static readonly JobCategory[] DirectAssign =
    {
        JobCategory.Mining,
        JobCategory.Logging,
        JobCategory.Guard,
    };

    public static bool IsQueued(JobCategory category)
        => Array.IndexOf(QueueOrder, category) >= 0;

    // **폰마다 켜고 끌 수 있는 작업인가** — `IsQueued`와 **다른 물음이다.**
    //
    // 오래도록 둘이 같았다(큐를 타는 것만 껐다 켰다 할 수 있었다). 채굴·벌목에 자율 배정이
    // 생기면서 갈렸다(`ResourceNode.ClaimFreeFor`): 그 둘은 **일감을 만들지 않으므로**
    // 큐에는 못 들어가지만, *할 일이 없을 때 빈 광맥을 잡을까*는 사람이 정해야 한다.
    //
    // **안 가르면 스위치가 조용히 죽는다** — `WorkSettings.IsEnabled`가 `IsQueued`로 막고
    // 있어서 채굴은 **늘 꺼진 것으로 답했고**, 그러면 자율 배정이 한 번도 안 돈다.
    //
    // **경비는 빠진다** — 순찰 구역에는 자율 배정이 없어 여전히 손으로만 붙인다.
    public static bool IsToggleable(JobCategory category)
        => IsQueued(category)
        || category == JobCategory.Mining
        || category == JobCategory.Logging;

    // 작업 점수용 가중치 — 앞에 있을수록 크다. 큐 밖 카테고리는 0(배정 대상 아님).
    public static int Weight(JobCategory category)
    {
        int index = Array.IndexOf(QueueOrder, category);
        return index < 0 ? 0 : QueueOrder.Length - index;
    }

    public static string DisplayName(JobCategory category) => category switch
    {
        JobCategory.Mining       => "채굴",
        JobCategory.Hauling      => "운반",
        JobCategory.Farming      => "농사",
        JobCategory.Logging      => "벌목",
        JobCategory.Construction => "건설",
        JobCategory.Guard        => "경비",
        JobCategory.Crafting     => "제작",
        JobCategory.Research     => "연구",
        JobCategory.Unload       => "짐내림",
        JobCategory.Medical      => "치료",
        JobCategory.Warden       => "간수",
        JobCategory.Butcher      => "도축",
        _                        => "-",
    };
}
