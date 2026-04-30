public enum ItemQuality
{
    Normal,     // 일반
    Good,       // 양질
    Excellent,  // 우수
    Masterwork  // 명품
}

public static class ItemQualityExt
{
    public static string DisplayName(this ItemQuality q) => q switch
    {
        ItemQuality.Good       => "양질",
        ItemQuality.Excellent  => "우수",
        ItemQuality.Masterwork => "명품",
        _                      => ""
    };

    // 스탯 배율 (나중에 CombatHandler/BodyPartSystem 연동용)
    public static float StatMultiplier(this ItemQuality q) => q switch
    {
        ItemQuality.Good       => 1.2f,
        ItemQuality.Excellent  => 1.5f,
        ItemQuality.Masterwork => 2.0f,
        _                      => 1.0f,
    };
}
