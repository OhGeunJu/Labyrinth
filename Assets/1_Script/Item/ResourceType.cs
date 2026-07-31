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

    // 화폐
    Gold,
}

// 자원 분류. 창고 필터가 22종을 낱개로 늘어놓으면 못 쓰므로 묶음 단위로 다룬다.
// **묶음의 단일 출처는 여기다** — 위 enum의 주석 구분(석재/원석/보석/주괴/목재/화폐)과 같게 유지할 것.
public enum ResourceGroup { Stone, Ore, Gem, Ingot, Wood, Currency }

public static class ResourceGroupExt
{
    public static readonly ResourceGroup[] All =
    {
        ResourceGroup.Stone, ResourceGroup.Ore, ResourceGroup.Gem,
        ResourceGroup.Ingot, ResourceGroup.Wood, ResourceGroup.Currency,
    };

    public static string DisplayName(this ResourceGroup g) => g switch
    {
        ResourceGroup.Stone    => "석재",
        ResourceGroup.Ore      => "원석",
        ResourceGroup.Gem      => "보석",
        ResourceGroup.Ingot    => "주괴",
        ResourceGroup.Wood     => "목재",
        ResourceGroup.Currency => "화폐",
        _                      => g.ToString(),
    };
}

public static class ResourceTypeExt
{
    public static ResourceGroup Group(this ResourceType t) => t switch
    {
        ResourceType.Stone or ResourceType.CutStone => ResourceGroup.Stone,

        ResourceType.CopperOre or ResourceType.TinOre     or ResourceType.IronOre   or
        ResourceType.Coal      or ResourceType.SilverOre  or ResourceType.GoldOre   or
        ResourceType.MagicStone or ResourceType.Etherstone or ResourceType.Voidstone
            => ResourceGroup.Ore,

        ResourceType.Ruby or ResourceType.Sapphire or
        ResourceType.Topaz or ResourceType.Emerald => ResourceGroup.Gem,

        ResourceType.Bronze or ResourceType.Steel or
        ResourceType.SilverIngot or ResourceType.GoldIngot => ResourceGroup.Ingot,

        ResourceType.Wood or ResourceType.Lumber => ResourceGroup.Wood,

        ResourceType.Gold => ResourceGroup.Currency,

        _ => ResourceGroup.Stone,
    };

    // 그 묶음에 속한 자원 전부. 필터를 묶음 단위로 켜고 끌 때 쓴다.
    public static System.Collections.Generic.List<ResourceType> TypesIn(ResourceGroup g)
    {
        var list = new System.Collections.Generic.List<ResourceType>();
        foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
            if (t.Group() == g) list.Add(t);
        return list;
    }

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
        ResourceType.Gold        => "골드",
        _                        => t.ToString(),
    };
}
