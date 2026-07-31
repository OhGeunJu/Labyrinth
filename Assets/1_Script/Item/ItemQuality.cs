using UnityEngine;

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

    // 등급 색 — **여기가 단일 출처다.** 툴팁 글자색과 바닥에 떨어진 아이템 색이
    // 같아야 "저 파란 게 아까 그 희귀템"이 성립한다. UI 쪽 `ItemInfoPanel.QualityColor`가
    // 이걸 가져다 쓴다(예전엔 거기에만 있어서 UI 밖에서는 쓸 수 없었다).
    public static Color Color(this ItemQuality q) => q switch
    {
        ItemQuality.Common    => new Color(0.80f, 0.80f, 0.80f),
        ItemQuality.Uncommon  => new Color(0.20f, 0.80f, 0.20f),
        ItemQuality.Rare      => new Color(0.20f, 0.55f, 1.00f),
        ItemQuality.Unique    => new Color(0.65f, 0.20f, 0.90f),
        ItemQuality.Epic      => new Color(1.00f, 0.50f, 0.10f),
        ItemQuality.Legendary => new Color(1.00f, 0.85f, 0.00f),
        _                     => UnityEngine.Color.white,
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
