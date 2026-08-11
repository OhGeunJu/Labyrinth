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
}
