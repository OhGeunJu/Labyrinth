public enum ItemQuality
{
    Common,     // 일반
    Uncommon,   // 고급
    Rare,       // 희귀
    Unique,     // 유니크
    Epic,       // 에픽
    Legendary   // 전설
}

public static class ItemQualityExt
{
    public static string DisplayName(this ItemQuality q) => q switch
    {
        ItemQuality.Common    => "일반",
        ItemQuality.Uncommon  => "고급",
        ItemQuality.Rare      => "희귀",
        ItemQuality.Unique    => "유니크",
        ItemQuality.Epic      => "에픽",
        ItemQuality.Legendary => "전설",
        _                     => "일반"
    };

    // 스탯 배율 (CombatHandler/BodyPartSystem 연동용)
    public static float StatMultiplier(this ItemQuality q) => q switch
    {
        ItemQuality.Uncommon  => 1.2f,
        ItemQuality.Rare      => 1.5f,
        ItemQuality.Unique    => 2.0f,
        ItemQuality.Epic      => 2.5f,
        ItemQuality.Legendary => 3.0f,
        _                     => 1.0f,
    };
}
