public enum ResourceType
{
    // 석재
    Stone,

    // 원석
    CopperOre,
    TinOre,
    IronOre,
    Coal,
    SilverOre,
    GoldOre,
    MagicStone,

    // 보석
    Ruby,
    Sapphire,
    Topaz,
    Emerald,
    Diamond,
    Amethyst,

    // 주괴
    Bronze,
    Steel,
    SilverIngot,
    GoldIngot,

    // 목재
    Wood,
}

public static class ResourceTypeExt
{
    public static string DisplayName(this ResourceType t) => t switch
    {
        ResourceType.Stone       => "돌",
        ResourceType.CopperOre   => "구리 광석",
        ResourceType.TinOre      => "주석 광석",
        ResourceType.IronOre     => "철 광석",
        ResourceType.Coal        => "석탄",
        ResourceType.SilverOre   => "은 광석",
        ResourceType.GoldOre     => "금 광석",
        ResourceType.MagicStone  => "마력석",
        ResourceType.Ruby        => "루비",
        ResourceType.Sapphire    => "사파이어",
        ResourceType.Topaz       => "토파즈",
        ResourceType.Emerald     => "에메랄드",
        ResourceType.Diamond     => "다이아몬드",
        ResourceType.Amethyst    => "자수정",
        ResourceType.Bronze      => "청동 주괴",
        ResourceType.Steel       => "강철 주괴",
        ResourceType.SilverIngot => "은 주괴",
        ResourceType.GoldIngot   => "금 주괴",
        ResourceType.Wood        => "목재",
        _                        => t.ToString(),
    };
}
