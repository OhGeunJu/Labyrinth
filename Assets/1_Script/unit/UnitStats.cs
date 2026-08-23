using System;
using UnityEngine;

[System.Serializable]
public class UnitStats
{
    [Header("속성 (0-130)")]
    public float Strength  = 10f;
    public float Toughness = 10f;
    public float Dexterity = 10f;
    public float Cognition = 10f;

    [Header("전투 기술 (0-130)")]
    public float MeleeAttack       = 1f;
    public float Defense      = 1f;
    public float PrecisionShooting = 1f;

    // **방패**. `Defense`(막나)와 다른 축 — 이건 *방패가 얼마나 일하나*라
    // `BlockBonus`에 곱해진다. 방패를 안 들면 0이 곱해지므로 아무것도 안 한다.
    public float Shield            = 1f;

    [Header("근접 무기 (0-130)")]
    public float OneHandSword = 1f;
    public float OneHandAxe   = 1f;
    public float OneHandMace  = 1f;
    public float TwoHandSword = 1f;
    public float TwoHandAxe   = 1f;
    public float TwoHandMace  = 1f;
    public float Polearm      = 1f;

    [Header("원거리 무기 (0-130)")]
    public float Bow       = 1f;
    public float Crossbow  = 1f;
    public float Javelin   = 1f;

    [Header("마법 원소 (0-130)")]
    public float FireMagic      = 1f;
    public float WaterMagic     = 1f;
    public float EarthMagic     = 1f;
    public float LightningMagic = 1f;
    public float ArcaneMagic    = 1f;
    public float DarkMagic      = 1f;
    public float HolyMagic      = 1f;

    [Header("신체 기술 (0-130)")]
    public float MartialArts = 1f;
    public float Evasion     = 1f;
    public float Athletics   = 10f;
    public float Swimming    = 10f;

    [Header("노동 (0-130)")]
    public float Labor         = 1f;
    public float Mining        = 1f;
    public float Growing       = 1f;
    public float Cutting       = 1f;
    public float Taming        = 1f;
    public float Cooking       = 1f;
    public float Medicine      = 1f;
    public float Research      = 1f;
    public float Social        = 1f;
    public float WeaponCrafting = 1f;
    public float ArmorCrafting  = 1f;
    public float Alchemy        = 1f;
    public float Smelting       = 1f;
    public float Stonecutting   = 1f;
    public float Woodworking    = 1f;
    public float Gemcutting     = 1f;

    public float GetAttackCombatSkill(bool unarmed)  => unarmed ? MartialArts : MeleeAttack;
    public float GetDefenseCombatSkill(bool unarmed) => unarmed ? MartialArts : Defense;

    // **무기가 어느 스탯을 쓰나 — 여기 하나가 답한다**(2026-08-23).
    //
    // 지팡이는 `WeaponType`만 봐서는 못 가른다(전부 `Staff`다) — 무엇을 쏘느냐(`DamageType`)가
    // 스탯을 정한다. 부르는 쪽이 `Type`으로 물으면 불 지팡이든 암흑 지팡이든 **같은 스탯이
    // 자란다.** 그래서 무기 자체를 받는다.
    public static StatName StatFor(WeaponData weapon)
        => weapon == null                      ? StatName.MartialArts
         : weapon.Type == WeaponType.Staff     ? MagicStatFor(weapon.DamageType)
         :                                       WeaponToStat(weapon.Type);

    public float GetSkillFor(WeaponData weapon) => GetStat(StatFor(weapon));
    public void  GrowSkillFor(WeaponData weapon, float amount) => GrowStat(StatFor(weapon), amount);

    public float GetCraftSkill(CraftSkill type) => GetStat(CraftToStat(type));

    public void GrowCraftSkill(CraftSkill type, float amount)
        => GrowStat(CraftToStat(type), amount);

    static StatName WeaponToStat(WeaponType type) => type switch
    {
        WeaponType.OneHandSword       => StatName.OneHandSword,
        WeaponType.OneHandAxe         => StatName.OneHandAxe,
        WeaponType.OneHandMace        => StatName.OneHandMace,
        WeaponType.TwoHandSword       => StatName.TwoHandSword,
        WeaponType.TwoHandAxe         => StatName.TwoHandAxe,
        WeaponType.TwoHandMace        => StatName.TwoHandMace,
        WeaponType.Polearm            => StatName.Polearm,
        WeaponType.Bow                => StatName.Bow,
        WeaponType.Crossbow           => StatName.Crossbow,
        WeaponType.Javelin            => StatName.Javelin,
        // **방패를 폴백에 두지 않는다** — 남겨 두면 방패를 주손에 든 폰이 조용히 무술로
        // 계산된다(2026-08-23까지 실제로 그랬고, 그래서 방패가 자라지 않았다).
        WeaponType.Shield             => StatName.Shield,
        _                             => StatName.MartialArts,
    };

    // **지팡이의 스탯은 무기 종류가 아니라 피해 원소가 정한다**(2026-08-23).
    // 지팡이는 `WeaponType.Staff` 하나뿐이고 무엇을 쏘느냐는 `DamageType`이 든다 —
    // 종류마다 지팡이를 만들면 원소를 하나 더할 때마다 `WeaponType`이 같이 늘어난다.
    public static StatName MagicStatFor(DamageType type) => type switch
    {
        DamageType.Fire      => StatName.FireMagic,
        DamageType.Water     => StatName.WaterMagic,
        DamageType.Earth     => StatName.EarthMagic,
        DamageType.Lightning => StatName.LightningMagic,
        DamageType.Arcane    => StatName.ArcaneMagic,
        DamageType.Dark      => StatName.DarkMagic,
        DamageType.Holy      => StatName.HolyMagic,
        _                    => StatName.ArcaneMagic,
    };

    static StatName CraftToStat(CraftSkill type) => type switch
    {
        CraftSkill.WeaponCrafting => StatName.WeaponCrafting,
        CraftSkill.ArmorCrafting  => StatName.ArmorCrafting,
        CraftSkill.Cooking        => StatName.Cooking,
        CraftSkill.Alchemy        => StatName.Alchemy,
        CraftSkill.Research       => StatName.Research,
        CraftSkill.Smelting       => StatName.Smelting,
        CraftSkill.Stonecutting   => StatName.Stonecutting,
        CraftSkill.Woodworking    => StatName.Woodworking,
        CraftSkill.Gemcutting     => StatName.Gemcutting,
        _                         => StatName.Labor,
    };

    public float GetStat(StatName stat) => stat switch
    {
        StatName.Strength          => Strength,
        StatName.Toughness         => Toughness,
        StatName.Dexterity         => Dexterity,
        StatName.Cognition         => Cognition,
        StatName.MeleeAttack       => MeleeAttack,
        StatName.Defense           => Defense,
        StatName.PrecisionShooting => PrecisionShooting,
        StatName.OneHandSword      => OneHandSword,
        StatName.OneHandAxe        => OneHandAxe,
        StatName.OneHandMace       => OneHandMace,
        StatName.TwoHandSword      => TwoHandSword,
        StatName.TwoHandAxe        => TwoHandAxe,
        StatName.TwoHandMace       => TwoHandMace,
        StatName.Polearm           => Polearm,
        StatName.Bow               => Bow,
        StatName.Crossbow          => Crossbow,
        StatName.Javelin           => Javelin,
        StatName.Shield            => Shield,
        StatName.FireMagic         => FireMagic,
        StatName.WaterMagic        => WaterMagic,
        StatName.EarthMagic        => EarthMagic,
        StatName.LightningMagic    => LightningMagic,
        StatName.ArcaneMagic       => ArcaneMagic,
        StatName.DarkMagic         => DarkMagic,
        StatName.HolyMagic         => HolyMagic,
        StatName.MartialArts       => MartialArts,
        StatName.Evasion           => Evasion,
        StatName.Athletics         => Athletics,
        StatName.Swimming          => Swimming,
        StatName.Labor             => Labor,
        StatName.Mining            => Mining,
        StatName.Growing           => Growing,
        StatName.Cutting           => Cutting,
        StatName.Taming            => Taming,
        StatName.Cooking           => Cooking,
        StatName.Medicine          => Medicine,
        StatName.Research          => Research,
        StatName.Social            => Social,
        StatName.WeaponCrafting    => WeaponCrafting,
        StatName.ArmorCrafting     => ArmorCrafting,
        StatName.Alchemy           => Alchemy,
        StatName.Smelting          => Smelting,
        StatName.Stonecutting      => Stonecutting,
        StatName.Woodworking       => Woodworking,
        StatName.Gemcutting        => Gemcutting,
        _ => 0f,
    };

    public void SetStat(StatName stat, float value)
    {
        value = Mathf.Clamp(value, 0f, StatGradeExt.MaxStatValue);
        switch (stat)
        {
            case StatName.Strength:          Strength = value; break;
            case StatName.Toughness:         Toughness = value; break;
            case StatName.Dexterity:         Dexterity = value; break;
            case StatName.Cognition:         Cognition = value; break;
            case StatName.MeleeAttack:       MeleeAttack = value; break;
            case StatName.Defense:           Defense = value; break;
            case StatName.PrecisionShooting: PrecisionShooting = value; break;
            case StatName.OneHandSword:      OneHandSword = value; break;
            case StatName.OneHandAxe:        OneHandAxe = value; break;
            case StatName.OneHandMace:       OneHandMace = value; break;
            case StatName.TwoHandSword:      TwoHandSword = value; break;
            case StatName.TwoHandAxe:        TwoHandAxe = value; break;
            case StatName.TwoHandMace:       TwoHandMace = value; break;
            case StatName.Polearm:           Polearm = value; break;
            case StatName.Bow:               Bow = value; break;
            case StatName.Crossbow:          Crossbow = value; break;
            case StatName.Javelin:           Javelin = value; break;
            case StatName.Shield:            Shield = value; break;
            case StatName.FireMagic:         FireMagic = value; break;
            case StatName.WaterMagic:        WaterMagic = value; break;
            case StatName.EarthMagic:        EarthMagic = value; break;
            case StatName.LightningMagic:    LightningMagic = value; break;
            case StatName.ArcaneMagic:       ArcaneMagic = value; break;
            case StatName.DarkMagic:         DarkMagic = value; break;
            case StatName.HolyMagic:         HolyMagic = value; break;
            case StatName.MartialArts:       MartialArts = value; break;
            case StatName.Evasion:           Evasion = value; break;
            case StatName.Athletics:         Athletics = value; break;
            case StatName.Swimming:          Swimming = value; break;
            case StatName.Labor:             Labor = value; break;
            case StatName.Mining:            Mining = value; break;
            case StatName.Growing:           Growing = value; break;
            case StatName.Cutting:           Cutting = value; break;
            case StatName.Taming:            Taming = value; break;
            case StatName.Cooking:           Cooking = value; break;
            case StatName.Medicine:          Medicine = value; break;
            case StatName.Research:          Research = value; break;
            case StatName.Social:            Social = value; break;
            case StatName.WeaponCrafting:    WeaponCrafting = value; break;
            case StatName.ArmorCrafting:     ArmorCrafting = value; break;
            case StatName.Alchemy:           Alchemy = value; break;
            case StatName.Smelting:          Smelting = value; break;
            case StatName.Stonecutting:      Stonecutting = value; break;
            case StatName.Woodworking:       Woodworking = value; break;
            case StatName.Gemcutting:        Gemcutting = value; break;
        }
    }

    [System.NonSerialized] public bool IsLocked = false;
    [System.NonSerialized] public System.Action<UnitStats, StatName, StatGrade> OnGradeUp;
    [System.NonSerialized] public GameObject Owner;
    [System.NonSerialized] public UnitData SourceData;
    public static event System.Action<UnitStats, StatName, StatGrade> OnAnyGradeUp;

    public void GrowStat(StatName stat, float amount)
    {
        if (IsLocked || amount <= 0f) return;
        if (SourceData != null) amount *= SourceData.GetGrowthRate(stat);
        if (amount <= 0f) return;
        float oldVal = GetStat(stat);
        StatGrade oldGrade = StatGradeExt.FromValue(oldVal);
        SetStat(stat, oldVal + amount);
        StatGrade newGrade = StatGradeExt.FromValue(GetStat(stat));
        if ((int)newGrade > (int)oldGrade)
        {
            OnGradeUp?.Invoke(this, stat, newGrade);
            OnAnyGradeUp?.Invoke(this, stat, newGrade);
        }
    }

    public StatGrade GetGrade(StatName stat) => StatGradeExt.FromValue(GetStat(stat));

    public static UnitStats FromData(UnitData data)
    {
        var s = new UnitStats();
        if (data == null) return s;
        s.SourceData = data;

        foreach (StatName stat in Enum.GetValues(typeof(StatName)))
        {
            StatGrade grade = data.RollGrade(stat);
            s.SetStat(stat, grade.ToValue());
        }
        return s;
    }
}
