public enum WeaponType
{
    Unarmed,

    // 방어구
    Shield,

    // 근접
    OneHandSword,
    OneHandAxe,
    OneHandMace,
    TwoHandSword,
    TwoHandAxe,
    TwoHandMace,
    Polearm,

    // 원거리
    Bow,
    Crossbow,
    Javelin,

    // **마법 — 지팡이 하나다**(2026-08-23).
    //
    // 전에는 계열 셋 × 원소 넷 = 열두 종이었다. 무엇을 쏘느냐는 이제 `WeaponData.DamageType`이
    // 들므로, 종류로도 가르면 **원소를 하나 더할 때마다 `WeaponType`이 같이 늘어난다**
    // (게다가 툴팁은 열둘을 전부 "지팡이"라고 부르고 있었다 — 갈라 둔 것이 화면에 나타난
    // 적이 없다).
    Staff,
}

// 화면에 보이는 분류 이름. 툴팁이 등급 옆에 붙인다.
// 마법 지팡이 여덟은 **속성별로 나누지 않는다** — 툴팁에 이미 피해 속성이 따로 나오고,
// 여기서까지 갈라 적으면 같은 말이 두 줄이 된다.
public static class WeaponTypeExt
{
    public static string DisplayName(this WeaponType t) => t switch
    {
        WeaponType.Unarmed      => "맨손",
        WeaponType.Shield       => "방패",
        WeaponType.OneHandSword => "한손 검",
        WeaponType.OneHandAxe   => "한손 도끼",
        WeaponType.OneHandMace  => "한손 둔기",
        WeaponType.TwoHandSword => "양손 검",
        WeaponType.TwoHandAxe   => "양손 도끼",
        WeaponType.TwoHandMace  => "양손 둔기",
        WeaponType.Polearm      => "장병기",
        WeaponType.Bow          => "활",
        WeaponType.Crossbow     => "쇠뇌",
        WeaponType.Javelin      => "투창",
        WeaponType.Staff        => "지팡이",
        _                       => t.ToString(),
    };
}
