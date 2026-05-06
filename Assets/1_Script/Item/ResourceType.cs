public enum ResourceType
{
    // 석재
    Stone,
    CutStone,    // 다듬은 돌 (석재 가공대 산출물)

    // 원석
    CopperOre,
    TinOre,
    IronOre,
    Coal,
    SilverOre,
    GoldOre,
    MagicStone,  // 원소 계열 마법 촉매
    Etherstone,  // 신성 계열 마법 촉매 (구 Diamond)
    Voidstone,   // 어둠 계열 마법 촉매 (구 Amethyst)

    // 보석
    Ruby,
    Sapphire,
    Topaz,
    Emerald,

    // 주괴
    Bronze,
    Steel,
    SilverIngot,
    GoldIngot,

    // 목재
    Wood,
    Lumber,      // 목재 판자 (제재소 산출물)
}

public static class ResourceTypeExt
{
    public static string DisplayName(this ResourceType t) => t switch
    {
        ResourceType.Stone       => "돌",
        ResourceType.CutStone    => "다듬은 돌",
        ResourceType.CopperOre   => "구리 광석",
        ResourceType.TinOre      => "주석 광석",
        ResourceType.IronOre     => "철 광석",
        ResourceType.Coal        => "석탄",
        ResourceType.SilverOre   => "은 광석",
        ResourceType.GoldOre     => "금 광석",
        ResourceType.MagicStone  => "마력석",
        ResourceType.Etherstone  => "에테르석",
        ResourceType.Voidstone   => "공허석",
        ResourceType.Ruby        => "루비",
        ResourceType.Sapphire    => "사파이어",
        ResourceType.Topaz       => "토파즈",
        ResourceType.Emerald     => "에메랄드",
        ResourceType.Bronze      => "청동 주괴",
        ResourceType.Steel       => "강철 주괴",
        ResourceType.SilverIngot => "은 주괴",
        ResourceType.GoldIngot   => "금 주괴",
        ResourceType.Wood        => "목재",
        ResourceType.Lumber      => "목재 판자",
        _                        => t.ToString(),
    };
}
