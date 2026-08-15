public enum JobCategory
{
    None,
    Mining,
    Hauling,
    Farming,
    Logging,
    Construction,
    Guard,
    Crafting,
    Research,
    Unload,
    // **뒤에 붙일 것** — 직렬화가 정수라 중간에 끼우면 `MinionData.AllowedJobs`가 통째로 밀린다.
    Medical,
    Warden,
    // 도축 — **큐를 안 탄다**(`JobCategoryInfo.QueueOrder`에 없다). 손으로 시킬 때만 도는
    // 일감이라 작업 설정에도 안 뜬다: 시체를 자동으로 가르기 시작하면 시키지도 않았는데
    // 동료 시신을 손질하는 일이 생긴다.
    Butcher,
}
