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

    // **횃불 — 보조손에 드는 불**(2026-10-15). 허리 램프를 얻기 전의 빛이고, 근접으로 칠 때 불을 얹는다
    // (`CombatHandler` — 주 무기 타격 뒤에 한 번 더). 주손에 넣으면 보조손으로 간다(방패와 같다).
    // 타는 시간은 `ItemData.BurnDays` · 남은 시간은 폰이 든다(`Equipment`). **꼬리에 붙인다** — 정수 직렬화.
    Torch,
}

// **무엇을 든 사람이 어떻게 휘두르나**(2026-09-11). 애니메이터의 `Style` 매개변수가 이 값이고
// 공격 상태가 스타일마다 하나씩 있다(`HumanoidClipSet`).
//
// **저작하지 않는다 — 무기 종류에서 파생시킨다**(`WeaponTypeExt.AttackStyle`). `WeaponData`에
// 필드를 두면 같은 물음에 답이 둘이 되고, **새 무기를 넣을 때 빠뜨리면 조용히 맨손으로 휘두른다**
// (그리고 JSON 아이템은 빠뜨리기가 더 쉽다).
//
// **`WeaponType`보다 적다** — 철검과 강철검이 다르게 휘두르지 않는 것과 같은 이유로, 한손 검·
// 도끼·둔기도 같은 팔로 휘두른다. 갈리는 것은 **몸이 쓰는 방식**이다: 한 손이냐 두 손이냐 ·
// 찌르냐 휘두르냐 · 쏘냐 · 거냐.
//
// **값은 꼬리에 붙일 것** — 컨트롤러에 정수 조건으로 구워지므로, 중간에 끼우면 다시 굽기 전까지
// **모든 폰이 엉뚱한 공격을 한다**(`WorkAnim`과 같은 규칙).
public enum AttackStyle
{
    OneHand,   // 한손 검·도끼·둔기 — 한 손으로 휘두른다
    TwoHand,   // 양손 검·도끼·둔기 — 몸을 실어 휘두른다
    Polearm,   // 장병기·투창 — 찌른다
    Bow,       // 활·쇠뇌 — 당겼다 쏜다
    Cast,      // 지팡이 — 건다
    Unarmed,   // 맨손 — 지른다
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
        WeaponType.Torch        => "횃불",
        _                       => t.ToString(),
    };

    // 무기 종류 → 공격 스타일. **방패는 주손에 안 오지만** 오면 맨손으로 본다(때리는 데 쓰는
    // 물건이 아니다 — 방패로 치는 동작을 넣으려면 그때 스타일을 하나 더 낸다).
    public static AttackStyle AttackStyle(this WeaponType t) => t switch
    {
        WeaponType.OneHandSword or WeaponType.OneHandAxe or WeaponType.OneHandMace => global::AttackStyle.OneHand,
        WeaponType.TwoHandSword or WeaponType.TwoHandAxe or WeaponType.TwoHandMace => global::AttackStyle.TwoHand,
        WeaponType.Polearm      or WeaponType.Javelin                              => global::AttackStyle.Polearm,
        WeaponType.Bow          or WeaponType.Crossbow                             => global::AttackStyle.Bow,
        WeaponType.Staff                                                           => global::AttackStyle.Cast,
        _                                                                          => global::AttackStyle.Unarmed,
    };
}
