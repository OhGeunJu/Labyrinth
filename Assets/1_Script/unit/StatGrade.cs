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
    Smelting, Stonecutting, Woodworking, Gemcutting
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
