using UnityEngine;

public enum StatGrade
{
    FMinus, F, FPlus,
    EMinus, E, EPlus,
    DMinus, D, DPlus,
    CMinus, C, CPlus,
    BMinus, B, BPlus,
    AMinus, A, APlus,
    SMinus, S, SPlus,
    SSMinus, SS, SSPlus,
    SSSMinus, SSS, SSSPlus
}

public enum StatName
{
    Strength, Toughness, Dexterity, Cognition,
    // **방어는 하나다**(2026-08-23). 전에는 `MeleeDefense`·`RangedDefense` 둘이었는데,
    // *"막는 솜씨"*라는 물음은 하나뿐이고 **날아오는 것을 막는 것도 막는 것**이다.
    // 근접·원거리를 가르던 진짜 이유(방패가 둘을 다르게 막는다)는 **방패가 든다** —
    // `WeaponData.BlockBonus` ↔ `RangedBlockBonus`. 스탯을 둘로 두면 같은 것을 두 번
    // 표현하는 셈이고, 실제로 `RangedDefense`는 **저작 에셋에서 한 번도 안 쓰였다**.
    MeleeAttack, Defense, PrecisionShooting,
    OneHandSword, OneHandAxe, OneHandMace,
    TwoHandSword, TwoHandAxe, TwoHandMace, Polearm,
    Bow, Crossbow, Javelin,

    // **마법은 원소 하나로 잰다**(2026-08-23). 전에는 계열 셋(`ElementalMagic`·`DarkMagic`·
    // `HolyMagic`) × 원소 넷 = 열다섯이었는데, **계열 셋은 전투에서 한 번도 안 읽혔고**
    // (`GetMagicCategorySkill`을 부르는 곳이 없었다) 조합 열둘 중 여덟은 무기가 없었다.
    // 피해 축이 `DamageType` 하나로 접히면서 "계열"이라는 층 자체가 사라졌다.
    //
    // **자리를 옮겼으므로 에셋의 정수도 같이 옮겼다** — 실제로 저작에 쓰이던 것은
    // 옛 `DarkMagic`(19) 하나뿐이었고(교단 성장 배경 다섯), 그건 여기 그대로 살아 있다.
    FireMagic, WaterMagic, EarthMagic, LightningMagic, ArcaneMagic, DarkMagic, HolyMagic,

    MartialArts, Evasion, Athletics, Swimming,
    Labor, Mining, Growing, Cutting, Taming, Cooking, Medicine, Research,
    WeaponCrafting, ArmorCrafting, Alchemy,
    Smelting, Stonecutting, Woodworking, Gemcutting,

    // **뒤에 붙일 것** — `UnitData.StatGrades`·`TraitData.StatOffsets`·`PawnSpecData.Grades`가
    // 이 enum을 정수로 직렬화한다. 중간에 끼우면 이미 저작한 에셋이 통째로 밀린다.
    //
    // 사교(2026-08-13에 `Warden`으로 들어왔고 08-14에 이름을 넓혔다).
    //
    // 처음 만든 이유는 이랬다: 작업 카테고리마다 노동 스탯이 하나씩 있는데(치료=의료,
    // 연구=연구, 채굴·벌목·요리…) **간수만 없어서 인지력을 빌려 쓰고 있었다.** 그러면
    // 학자가 곧 간수가 되고, 설득을 시키려고 올린 인지력이 연구 속도까지 딸려 온다.
    //
    // **이름을 넓힌 것은 흥정이 같은 능력이기 때문이다.** 포로를 설득하는 것과 상인과
    // 값을 다투는 것은 둘 다 **말로 상대를 움직이는 일**이라, 스탯을 따로 두면 "이 폰은
    // 설득은 잘하는데 흥정은 못한다"를 설명할 길이 없다. 인지력↔간수를 가른 것과는 다른
    // 경우다 — 연구와 설득은 남남이지만 설득과 흥정은 같은 재주다(림월드의 Social이
    // 포로 회유와 거래가를 함께 정하는 것과 같은 자리).
    //
    // **작업 이름(`JobCategory.Warden`)은 그대로다** — 치료 작업이 의료 스탯을 쓰듯,
    // 작업의 이름과 그 작업이 쓰는 스탯의 이름은 같을 이유가 없다.
    //
    // 이름만 바꿨고 **자리는 그대로 맨 끝이다** — 직렬화가 정수라 순서를 건드리면 이미
    // 저작한 에셋이 통째로 밀린다(에셋에 문자열로 박힌 곳은 없음을 전수 확인).
    Social,

    // **방패**(2026-08-23). 무기는 종류마다 스탯이 있는데 방패만 없었다 — `WeaponToStat`에서
    // `WeaponType.Shield`만 `_ => MartialArts` 폴백에 떨어졌고, 막기 계산은 평탄 `BlockBonus`만
    // 더했다. **그래서 방패는 자라지 않았다**: 천 번을 막아도 나아지지 않고 좋은 방패를 사는
    // 것이 유일한 개선이었다(`Warden`을 만든 그 자리와 같다 — 작업마다 스탯이 있는데 간수만
    // 없어서 인지력을 빌려 쓰고 있었다).
    //
    // **무기와 대칭으로 든다**: 공통 스탯이 굴림(`Defense` = 막나), 종류 스탯이 크기
    // (`Shield` = 방패가 얼마나 일하나). 방패가 없으면 `BlockBonus`가 0이라 이 스탯도 저절로
    // 아무것도 안 한다 — *"방패를 든 폰만 자란다"*가 규칙 없이 나온다.
    Shield
}

// **스탯 이름의 표시명 — 여기 하나뿐이다.**
//
// 원래 `GradeUpNotificationUI` 안에 private으로 들어 있었다. 특성(`TraitData.EffectSummary`)이
// 같은 이름을 필요로 하면서 두 벌이 될 참이었는데, 표시명이 두 곳에 있으면 반드시 어긋난다
// (`ResourceType` enum을 지운 것과 같은 이유 — 한 자원에 이름이 셋이었다).
public static class StatNameExt
{
    public static string ToDisplay(this StatName s) => s switch
    {
        StatName.Strength          => "근력",
        StatName.Toughness         => "강인함",
        StatName.Dexterity         => "손재주",
        StatName.Cognition         => "인지력",
        StatName.MeleeAttack       => "근접공격",
        StatName.Defense           => "방어",
        StatName.PrecisionShooting => "정밀사격",
        StatName.MartialArts       => "무술",
        StatName.Evasion           => "회피",
        StatName.Athletics         => "육상",
        StatName.Swimming          => "수영",
        StatName.Mining            => "채굴",
        StatName.Growing           => "원예",
        StatName.Cutting           => "벌목",
        StatName.Taming            => "조련",
        StatName.Cooking           => "요리",
        StatName.Medicine          => "의료",
        StatName.Research          => "연구",
        StatName.Social            => "사교",
        StatName.WeaponCrafting    => "무기제작",
        StatName.ArmorCrafting     => "갑옷제작",
        StatName.Alchemy           => "연금",
        StatName.Smelting          => "제련",
        StatName.Stonecutting      => "석재가공",
        StatName.Woodworking       => "목공",
        StatName.Gemcutting        => "보석가공",
        StatName.Labor             => "노동",
        StatName.OneHandSword      => "한손검",
        StatName.OneHandAxe        => "한손도끼",
        StatName.OneHandMace       => "한손둔기",
        StatName.TwoHandSword      => "양손검",
        StatName.TwoHandAxe        => "양손도끼",
        StatName.TwoHandMace       => "양손둔기",
        StatName.Polearm           => "장창",
        StatName.Bow               => "활",
        StatName.Crossbow          => "쇠뇌",
        StatName.Javelin           => "투창",
        StatName.Shield            => "방패",
        StatName.FireMagic         => "불 마법",
        StatName.WaterMagic        => "물 마법",
        StatName.EarthMagic        => "땅 마법",
        StatName.LightningMagic    => "전기 마법",
        StatName.ArcaneMagic       => "비전 마법",
        StatName.DarkMagic         => "암흑 마법",
        StatName.HolyMagic         => "신성 마법",
        _                          => s.ToString(),
    };
}

public static class StatGradeExt
{
    public const float MaxStatValue = 130f;
    public const float StepValue = 5f;

    // 최솟값 1 — F-(인덱스 0)도 값 0이 아닌 1에서 시작. 값 0은 방어/회피 대립 굴림에서
    // 무방비(0/0 → 가드로 0%)가 되므로, 등급 스케일 차원에서 바닥을 깐다. FromValue(1)==F- 유지.
    public static float ToValue(this StatGrade g) => Mathf.Max(1f, (int)g * StepValue);

    public static string ToDisplay(this StatGrade g)
    {
        string s = g.ToString();
        if (s.EndsWith("Plus"))  return s.Substring(0, s.Length - 4) + "+";
        if (s.EndsWith("Minus")) return s.Substring(0, s.Length - 5) + "-";
        return s;
    }

    public static StatGrade FromValue(float value)
    {
        int step = Mathf.Clamp(Mathf.FloorToInt(value / StepValue), 0, 26);
        return (StatGrade)step;
    }

    public static StatGrade RandomBetween(StatGrade min, StatGrade max)
    {
        int lo = (int)min;
        int hi = (int)max;
        if (hi < lo) (lo, hi) = (hi, lo);
        return (StatGrade)Random.Range(lo, hi + 1);
    }

    public static Color GetColor(this StatGrade g)
    {
        int tier = (int)g / 3;
        return tier switch
        {
            0 => new Color(0.5f, 0.5f, 0.5f),
            1 => new Color(0.65f, 0.65f, 0.65f),
            2 => new Color(0.85f, 0.85f, 0.85f),
            3 => Color.white,
            4 => new Color(0.4f, 0.9f, 0.4f),
            5 => new Color(0.4f, 0.7f, 1f),
            6 => new Color(0.8f, 0.5f, 1f),
            7 => new Color(1f, 0.8f, 0.2f),
            8 => new Color(1f, 0.4f, 0.4f),
            _ => Color.white
        };
    }
}
