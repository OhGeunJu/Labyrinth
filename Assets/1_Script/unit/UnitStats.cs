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
    public float MeleeDefense      = 1f;
    public float RangedDefense     = 1f;
    public float PrecisionShooting = 1f;

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

    [Header("마법 (0-130)")]
    public float ElementalMagic = 1f;
    public float DarkMagic      = 1f;
    public float HolyMagic      = 1f;

    [Header("마법 원소 (0-130)")]
    public float ElementalFire      = 1f;
    public float ElementalIce       = 1f;
    public float ElementalLightning = 1f;
    public float ElementalPoison    = 1f;
    public float DarkFire           = 1f;
    public float DarkIce            = 1f;
    public float DarkLightning      = 1f;
    public float DarkPoison         = 1f;
    public float HolyFire           = 1f;
    public float HolyIce            = 1f;
    public float HolyLightning      = 1f;
    public float HolyPoison         = 1f;

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
    public float Warden        = 1f;
    public float WeaponCrafting = 1f;
    public float ArmorCrafting  = 1f;
    public float Alchemy        = 1f;
    public float Smelting       = 1f;
    public float Stonecutting   = 1f;
    public float Woodworking    = 1f;
    public float Gemcutting     = 1f;

    public float GetAttackCombatSkill(bool unarmed)  => unarmed ? MartialArts : MeleeAttack;
    public float GetDefenseCombatSkill(bool unarmed) => unarmed ? MartialArts : MeleeDefense;

    public float GetSkillForWeapon(WeaponType type) => GetStat(WeaponToStat(type));

    public float GetMagicCategorySkill(WeaponType type) => type switch
    {
        WeaponType.ElementalFire or WeaponType.ElementalIce
        or WeaponType.ElementalLightning or WeaponType.ElementalPoison => ElementalMagic,
        WeaponType.DarkFire or WeaponType.DarkIce
        or WeaponType.DarkLightning or WeaponType.DarkPoison           => DarkMagic,
        WeaponType.HolyFire or WeaponType.HolyIce
        or WeaponType.HolyLightning or WeaponType.HolyPoison           => HolyMagic,
        _ => 0f,
    };

    public void GrowSkillForWeapon(WeaponType type, float amount)
        => GrowStat(WeaponToStat(type), amount);

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
        WeaponType.ElementalFire      => StatName.ElementalFire,
        WeaponType.ElementalIce       => StatName.ElementalIce,
        WeaponType.ElementalLightning => StatName.ElementalLightning,
        WeaponType.ElementalPoison    => StatName.ElementalPoison,
        WeaponType.DarkFire           => StatName.DarkFire,
        WeaponType.DarkIce            => StatName.DarkIce,
        WeaponType.DarkLightning      => StatName.DarkLightning,
        WeaponType.DarkPoison         => StatName.DarkPoison,
        WeaponType.HolyFire           => StatName.HolyFire,
        WeaponType.HolyIce            => StatName.HolyIce,
        WeaponType.HolyLightning      => StatName.HolyLightning,
        WeaponType.HolyPoison         => StatName.HolyPoison,
        _                             => StatName.MartialArts,
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
        StatName.MeleeDefense      => MeleeDefense,
        StatName.RangedDefense     => RangedDefense,
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
        StatName.ElementalMagic    => ElementalMagic,
        StatName.DarkMagic         => DarkMagic,
        StatName.HolyMagic         => HolyMagic,
        StatName.ElementalFire     => ElementalFire,
        StatName.ElementalIce      => ElementalIce,
        StatName.ElementalLightning=> ElementalLightning,
        StatName.ElementalPoison   => ElementalPoison,
        StatName.DarkFire          => DarkFire,
        StatName.DarkIce           => DarkIce,
        StatName.DarkLightning     => DarkLightning,
        StatName.DarkPoison        => DarkPoison,
        StatName.HolyFire          => HolyFire,
        StatName.HolyIce           => HolyIce,
        StatName.HolyLightning     => HolyLightning,
        StatName.HolyPoison        => HolyPoison,
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
        StatName.Warden            => Warden,
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
            case StatName.MeleeDefense:      MeleeDefense = value; break;
            case StatName.RangedDefense:     RangedDefense = value; break;
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
            case StatName.ElementalMagic:    ElementalMagic = value; break;
            case StatName.DarkMagic:         DarkMagic = value; break;
            case StatName.HolyMagic:         HolyMagic = value; break;
            case StatName.ElementalFire:     ElementalFire = value; break;
            case StatName.ElementalIce:      ElementalIce = value; break;
            case StatName.ElementalLightning:ElementalLightning = value; break;
            case StatName.ElementalPoison:   ElementalPoison = value; break;
            case StatName.DarkFire:          DarkFire = value; break;
            case StatName.DarkIce:           DarkIce = value; break;
            case StatName.DarkLightning:     DarkLightning = value; break;
            case StatName.DarkPoison:        DarkPoison = value; break;
            case StatName.HolyFire:          HolyFire = value; break;
            case StatName.HolyIce:           HolyIce = value; break;
            case StatName.HolyLightning:     HolyLightning = value; break;
            case StatName.HolyPoison:        HolyPoison = value; break;
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
            case StatName.Warden:            Warden = value; break;
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
