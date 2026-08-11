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
        JobCategory.Construction,
        JobCategory.Farming,
        JobCategory.Crafting,
        JobCategory.Research,
        JobCategory.Hauling,
        // 맨 뒤 — 남의 일감이 없을 때 자기 짐을 내린다.
        JobCategory.Unload,
    };

    // 큐를 타지 않고 플레이어가 대상에 폰을 직접 붙이는 카테고리.
    public static readonly JobCategory[] DirectAssign =
    {
        JobCategory.Mining,
        JobCategory.Logging,
        JobCategory.Guard,
    };

    public static bool IsQueued(JobCategory category)
        => Array.IndexOf(QueueOrder, category) >= 0;

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
        _                        => "-",
    };
}
