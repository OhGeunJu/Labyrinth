using UnityEngine;

[System.Serializable]
public class UnitStats
{
    [Header("속성 (0-100)")]
    public float Strength  = 10f;   // 근력
    public float Toughness = 10f;   // 강인함
    public float Dexterity = 10f;   // 손재주
    public float Cognition = 10f;   // 인지력

    [Header("전투 기술 (0-100)")]
    public float MeleeAttack       = 1f;  // 근접무기: 공격 빈도, 블록 관통
    public float MeleeDefense      = 1f;  // 근접무기: 블록 확률
    public float RangedDefense     = 1f;  // 원거리/투사체: 블록 확률
    public float PrecisionShooting = 1f;  // 원거리/마법: 동료 피격 회피율

    [Header("근접 무기 (0-100)")]
    public float OneHandSword = 1f;
    public float OneHandAxe   = 1f;
    public float OneHandMace  = 1f;
    public float TwoHandSword = 1f;
    public float TwoHandAxe   = 1f;
    public float TwoHandMace  = 1f;
    public float Polearm      = 1f;

    [Header("원거리 무기 (0-100)")]
    public float Bow       = 1f;
    public float Crossbow  = 1f;
    public float Javelin   = 1f;

    [Header("마법 (0-100)")]
    public float ElementalMagic = 1f;  // 원소 마법 전반 (공격 빈도, 정확도)
    public float DarkMagic      = 1f;  // 암흑 마법 전반
    public float HolyMagic      = 1f;  // 신성 마법 전반

    [Header("마법 원소 (0-100)")]
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

    [Header("신체 기술 (0-100)")]
    public float MartialArts = 1f;
    public float Evasion     = 1f;
    public float Athletics   = 10f;
    public float Swimming    = 10f;

    [Header("노동 (0-100)")]
    public float Labor         = 1f;
    public float Mining        = 1f;
    public float Growing       = 1f;
    public float Cutting       = 1f;
    public float Taming        = 1f;
    public float Cooking       = 1f;
    public float Medicine      = 1f;
    public float Research      = 1f;
    public float WeaponCrafting = 1f;
    public float ArmorCrafting  = 1f;
    public float Alchemy        = 1f;
    public float Smelting       = 1f;
    public float Stonecutting   = 1f;
    public float Woodworking    = 1f;
    public float Gemcutting     = 1f;

    // 무장 여부에 따라 전투 기술 반환
    public float GetAttackCombatSkill(bool unarmed)  => unarmed ? MartialArts : MeleeAttack;
    public float GetDefenseCombatSkill(bool unarmed) => unarmed ? MartialArts : MeleeDefense;

    // 무기 타입에 따른 원소 숙련도 반환
    public float GetSkillForWeapon(WeaponType type) => type switch
    {
        WeaponType.OneHandSword      => OneHandSword,
        WeaponType.OneHandAxe        => OneHandAxe,
        WeaponType.OneHandMace       => OneHandMace,
        WeaponType.TwoHandSword      => TwoHandSword,
        WeaponType.TwoHandAxe        => TwoHandAxe,
        WeaponType.TwoHandMace       => TwoHandMace,
        WeaponType.Polearm           => Polearm,
        WeaponType.Bow               => Bow,
        WeaponType.Crossbow          => Crossbow,
        WeaponType.Javelin           => Javelin,
        WeaponType.ElementalFire     => ElementalFire,
        WeaponType.ElementalIce      => ElementalIce,
        WeaponType.ElementalLightning=> ElementalLightning,
        WeaponType.ElementalPoison   => ElementalPoison,
        WeaponType.DarkFire          => DarkFire,
        WeaponType.DarkIce           => DarkIce,
        WeaponType.DarkLightning     => DarkLightning,
        WeaponType.DarkPoison        => DarkPoison,
        WeaponType.HolyFire          => HolyFire,
        WeaponType.HolyIce           => HolyIce,
        WeaponType.HolyLightning     => HolyLightning,
        WeaponType.HolyPoison        => HolyPoison,
        _                            => MartialArts,  // Unarmed
    };

    // 마법 카테고리 스킬 반환 (정확도·빈도 계산용, MeleeAttack 역할)
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

    // 무기 타입에 따른 숙련도 성장
    public void GrowSkillForWeapon(WeaponType type, float amount)
    {
        switch (type)
        {
            case WeaponType.OneHandSword:       OneHandSword       = Mathf.Min(100f, OneHandSword       + amount); break;
            case WeaponType.OneHandAxe:         OneHandAxe         = Mathf.Min(100f, OneHandAxe         + amount); break;
            case WeaponType.OneHandMace:        OneHandMace        = Mathf.Min(100f, OneHandMace        + amount); break;
            case WeaponType.TwoHandSword:       TwoHandSword       = Mathf.Min(100f, TwoHandSword       + amount); break;
            case WeaponType.TwoHandAxe:         TwoHandAxe         = Mathf.Min(100f, TwoHandAxe         + amount); break;
            case WeaponType.TwoHandMace:        TwoHandMace        = Mathf.Min(100f, TwoHandMace        + amount); break;
            case WeaponType.Polearm:            Polearm            = Mathf.Min(100f, Polearm            + amount); break;
            case WeaponType.Bow:                Bow                = Mathf.Min(100f, Bow                + amount); break;
            case WeaponType.Crossbow:           Crossbow           = Mathf.Min(100f, Crossbow           + amount); break;
            case WeaponType.Javelin:            Javelin            = Mathf.Min(100f, Javelin            + amount); break;
            case WeaponType.ElementalFire:      ElementalFire      = Mathf.Min(100f, ElementalFire      + amount); break;
            case WeaponType.ElementalIce:       ElementalIce       = Mathf.Min(100f, ElementalIce       + amount); break;
            case WeaponType.ElementalLightning: ElementalLightning = Mathf.Min(100f, ElementalLightning + amount); break;
            case WeaponType.ElementalPoison:    ElementalPoison    = Mathf.Min(100f, ElementalPoison    + amount); break;
            case WeaponType.DarkFire:           DarkFire           = Mathf.Min(100f, DarkFire           + amount); break;
            case WeaponType.DarkIce:            DarkIce            = Mathf.Min(100f, DarkIce            + amount); break;
            case WeaponType.DarkLightning:      DarkLightning      = Mathf.Min(100f, DarkLightning      + amount); break;
            case WeaponType.DarkPoison:         DarkPoison         = Mathf.Min(100f, DarkPoison         + amount); break;
            case WeaponType.HolyFire:           HolyFire           = Mathf.Min(100f, HolyFire           + amount); break;
            case WeaponType.HolyIce:            HolyIce            = Mathf.Min(100f, HolyIce            + amount); break;
            case WeaponType.HolyLightning:      HolyLightning      = Mathf.Min(100f, HolyLightning      + amount); break;
            case WeaponType.HolyPoison:         HolyPoison         = Mathf.Min(100f, HolyPoison         + amount); break;
            default:                            MartialArts        = Mathf.Min(100f, MartialArts        + amount); break;
        }
    }

    // 제작 스킬 반환
    public float GetCraftSkill(CraftSkill type) => type switch
    {
        CraftSkill.WeaponCrafting => WeaponCrafting,
        CraftSkill.ArmorCrafting  => ArmorCrafting,
        CraftSkill.Cooking        => Cooking,
        CraftSkill.Alchemy        => Alchemy,
        CraftSkill.Research       => Research,
        CraftSkill.Smelting       => Smelting,
        CraftSkill.Stonecutting   => Stonecutting,
        CraftSkill.Woodworking    => Woodworking,
        CraftSkill.Gemcutting     => Gemcutting,
        _                         => Labor,
    };

    // 제작 스킬 성장 (상한 100)
    public void GrowCraftSkill(CraftSkill type, float amount)
    {
        switch (type)
        {
            case CraftSkill.WeaponCrafting: WeaponCrafting = Mathf.Min(100f, WeaponCrafting + amount); break;
            case CraftSkill.ArmorCrafting:  ArmorCrafting  = Mathf.Min(100f, ArmorCrafting  + amount); break;
            case CraftSkill.Cooking:        Cooking        = Mathf.Min(100f, Cooking        + amount); break;
            case CraftSkill.Alchemy:        Alchemy        = Mathf.Min(100f, Alchemy        + amount); break;
            case CraftSkill.Research:       Research       = Mathf.Min(100f, Research       + amount); break;
            case CraftSkill.Smelting:       Smelting       = Mathf.Min(100f, Smelting       + amount); break;
            case CraftSkill.Stonecutting:   Stonecutting   = Mathf.Min(100f, Stonecutting   + amount); break;
            case CraftSkill.Woodworking:    Woodworking    = Mathf.Min(100f, Woodworking    + amount); break;
            case CraftSkill.Gemcutting:     Gemcutting     = Mathf.Min(100f, Gemcutting     + amount); break;
            default:                        Labor          = Mathf.Min(100f, Labor          + amount); break;
        }
    }

    public static UnitStats FromData(UnitData data)
    {
        if (data == null) return new UnitStats();

        return new UnitStats
        {
            Strength  = Random.Range(data.StrengthRange.x,  data.StrengthRange.y),
            Toughness = Random.Range(data.ToughnessRange.x, data.ToughnessRange.y),
            Dexterity = Random.Range(data.DexterityRange.x, data.DexterityRange.y),
            Cognition = Random.Range(data.CognitionRange.x, data.CognitionRange.y),

            MeleeAttack       = Random.Range(data.MeleeAttackRange.x,       data.MeleeAttackRange.y),
            MeleeDefense      = Random.Range(data.MeleeDefenseRange.x,      data.MeleeDefenseRange.y),
            RangedDefense     = Random.Range(data.RangedDefenseRange.x,     data.RangedDefenseRange.y),
            PrecisionShooting = Random.Range(data.PrecisionShootingRange.x, data.PrecisionShootingRange.y),

            OneHandSword = Random.Range(data.OneHandSwordRange.x, data.OneHandSwordRange.y),
            OneHandAxe   = Random.Range(data.OneHandAxeRange.x,   data.OneHandAxeRange.y),
            OneHandMace  = Random.Range(data.OneHandMaceRange.x,  data.OneHandMaceRange.y),
            TwoHandSword = Random.Range(data.TwoHandSwordRange.x, data.TwoHandSwordRange.y),
            TwoHandAxe   = Random.Range(data.TwoHandAxeRange.x,   data.TwoHandAxeRange.y),
            TwoHandMace  = Random.Range(data.TwoHandMaceRange.x,  data.TwoHandMaceRange.y),
            Polearm      = Random.Range(data.PolearmRange.x,      data.PolearmRange.y),

            Bow      = Random.Range(data.BowRange.x,      data.BowRange.y),
            Crossbow = Random.Range(data.CrossbowRange.x, data.CrossbowRange.y),
            Javelin  = Random.Range(data.JavelinRange.x,  data.JavelinRange.y),

            ElementalMagic = Random.Range(data.ElementalMagicRange.x, data.ElementalMagicRange.y),
            DarkMagic      = Random.Range(data.DarkMagicRange.x,      data.DarkMagicRange.y),
            HolyMagic      = Random.Range(data.HolyMagicRange.x,      data.HolyMagicRange.y),

            ElementalFire      = Random.Range(data.ElementalFireRange.x,      data.ElementalFireRange.y),
            ElementalIce       = Random.Range(data.ElementalIceRange.x,       data.ElementalIceRange.y),
            ElementalLightning = Random.Range(data.ElementalLightningRange.x, data.ElementalLightningRange.y),
            ElementalPoison    = Random.Range(data.ElementalPoisonRange.x,    data.ElementalPoisonRange.y),
            DarkFire           = Random.Range(data.DarkFireRange.x,           data.DarkFireRange.y),
            DarkIce            = Random.Range(data.DarkIceRange.x,            data.DarkIceRange.y),
            DarkLightning      = Random.Range(data.DarkLightningRange.x,      data.DarkLightningRange.y),
            DarkPoison         = Random.Range(data.DarkPoisonRange.x,         data.DarkPoisonRange.y),
            HolyFire           = Random.Range(data.HolyFireRange.x,           data.HolyFireRange.y),
            HolyIce            = Random.Range(data.HolyIceRange.x,            data.HolyIceRange.y),
            HolyLightning      = Random.Range(data.HolyLightningRange.x,      data.HolyLightningRange.y),
            HolyPoison         = Random.Range(data.HolyPoisonRange.x,         data.HolyPoisonRange.y),

            MartialArts = Random.Range(data.MartialArtsRange.x, data.MartialArtsRange.y),
            Evasion     = Random.Range(data.EvasionRange.x,     data.EvasionRange.y),
            Athletics   = Random.Range(data.AthleticsRange.x,   data.AthleticsRange.y),
            Swimming    = Random.Range(data.SwimmingRange.x,    data.SwimmingRange.y),

            Labor          = Random.Range(data.LaborRange.x,          data.LaborRange.y),
            Mining         = Random.Range(data.MiningRange.x,         data.MiningRange.y),
            Growing        = Random.Range(data.GrowingRange.x,        data.GrowingRange.y),
            Cutting        = Random.Range(data.CuttingRange.x,        data.CuttingRange.y),
            Taming         = Random.Range(data.TamingRange.x,         data.TamingRange.y),
            Cooking        = Random.Range(data.CookingRange.x,        data.CookingRange.y),
            Medicine       = Random.Range(data.MedicineRange.x,       data.MedicineRange.y),
            Research       = Random.Range(data.ResearchRange.x,       data.ResearchRange.y),
            WeaponCrafting = Random.Range(data.WeaponCraftingRange.x,  data.WeaponCraftingRange.y),
            ArmorCrafting  = Random.Range(data.ArmorCraftingRange.x,   data.ArmorCraftingRange.y),
            Alchemy        = Random.Range(data.AlchemyRange.x,         data.AlchemyRange.y),
        };
    }
}
