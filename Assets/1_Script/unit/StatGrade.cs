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
    MeleeAttack, MeleeDefense, RangedDefense, PrecisionShooting,
    OneHandSword, OneHandAxe, OneHandMace,
    TwoHandSword, TwoHandAxe, TwoHandMace, Polearm,
    Bow, Crossbow, Javelin,
    ElementalMagic, DarkMagic, HolyMagic,
    ElementalFire, ElementalIce, ElementalLightning, ElementalPoison,
    DarkFire, DarkIce, DarkLightning, DarkPoison,
    HolyFire, HolyIce, HolyLightning, HolyPoison,
    MartialArts, Evasion, Athletics, Swimming,
    Labor, Mining, Growing, Cutting, Taming, Cooking, Medicine, Research,
    WeaponCrafting, ArmorCrafting, Alchemy,
    Smelting, Stonecutting, Woodworking, Gemcutting,

    // **뒤에 붙일 것** — `UnitData.StatGrades`·`TraitData.StatOffsets`·`PawnSpecData.Grades`가
    // 이 enum을 정수로 직렬화한다. 중간에 끼우면 이미 저작한 에셋이 통째로 밀린다.
    //
    // 간수(2026-08-13). 작업 카테고리마다 노동 스탯이 하나씩 있는데(치료=의료, 연구=연구,
    // 채굴·벌목·요리…) **간수만 없어서 인지력을 빌려 쓰고 있었다.** 그러면 학자가 곧 간수가
    // 되고, 설득을 시키려고 인지력을 올리면 연구 속도까지 딸려 온다.
    Warden
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
        StatName.MeleeDefense      => "근접방어",
        StatName.RangedDefense     => "원거리방어",
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
        StatName.Warden            => "간수",
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
        StatName.ElementalMagic    => "원소마법",
        StatName.DarkMagic         => "암흑마법",
        StatName.HolyMagic         => "신성마법",
        StatName.ElementalFire     => "원소·불",
        StatName.ElementalIce      => "원소·얼음",
        StatName.ElementalLightning=> "원소·전기",
        StatName.ElementalPoison   => "원소·독",
        StatName.DarkFire          => "암흑·불",
        StatName.DarkIce           => "암흑·얼음",
        StatName.DarkLightning     => "암흑·전기",
        StatName.DarkPoison        => "암흑·독",
        StatName.HolyFire          => "신성·불",
        StatName.HolyIce           => "신성·얼음",
        StatName.HolyLightning     => "신성·전기",
        StatName.HolyPoison        => "신성·독",
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
