using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/WeaponData")]
public class WeaponData : ItemData
{
    [Header("무기 분류")]
    public WeaponType Type;
    public bool IsLargeWeapon;  // 양손무기/폴암 → 근력 공격속도 보정
    public bool IsBladed;       // 검/도끼 → 손재주 절단 피해 보정
    public bool IsRanged;

    [Header("전투 스탯")]
    public float BaseDamage         = 10f;
    public float BaseAttackInterval = 2f;
    public float AttackRange        = 1.5f;  // 원거리는 크게 설정

    [Header("방어 보너스 (방패 전용)")]
    public float BlockBonus        = 0f;  // 근접 방어 확률 +
    public float RangedBlockBonus  = 0f;  // 원거리 방어 확률 +

    [Header("피해 유형 (물리/원거리 전용 — 마법은 WeaponType에서 자동 결정)")]
    public DamageCategory DamageCategory = DamageCategory.Physical;
    public PhysicalType   PhysicalType   = PhysicalType.Slash;
    public DamageElement  DamageElement  = DamageElement.None;
    [Range(0f, 1f)]
    public float EffectChance = 0.2f;

    // 실제 사용 값 — 마법 무기는 WeaponType에서 자동 추론
    public DamageCategory EffectiveDamageCategory => IsMagicWeapon ? CategoryFromType(Type) : DamageCategory;
    public DamageElement  EffectiveDamageElement  => IsMagicWeapon ? ElementFromType(Type)  : DamageElement;

    public bool IsMagicWeapon => Type is WeaponType.ElementalFire or WeaponType.ElementalIce
        or WeaponType.ElementalLightning or WeaponType.ElementalPoison
        or WeaponType.DarkFire  or WeaponType.DarkIce  or WeaponType.DarkLightning  or WeaponType.DarkPoison
        or WeaponType.HolyFire  or WeaponType.HolyIce  or WeaponType.HolyLightning  or WeaponType.HolyPoison;

    static DamageCategory CategoryFromType(WeaponType t) => t switch
    {
        WeaponType.ElementalFire or WeaponType.ElementalIce
        or WeaponType.ElementalLightning or WeaponType.ElementalPoison => DamageCategory.Elemental,
        WeaponType.DarkFire  or WeaponType.DarkIce
        or WeaponType.DarkLightning  or WeaponType.DarkPoison          => DamageCategory.Dark,
        WeaponType.HolyFire  or WeaponType.HolyIce
        or WeaponType.HolyLightning  or WeaponType.HolyPoison          => DamageCategory.Holy,
        _                                                               => DamageCategory.Physical,
    };

    static DamageElement ElementFromType(WeaponType t) => t switch
    {
        WeaponType.ElementalFire  or WeaponType.DarkFire  or WeaponType.HolyFire      => DamageElement.Fire,
        WeaponType.ElementalIce   or WeaponType.DarkIce   or WeaponType.HolyIce       => DamageElement.Ice,
        WeaponType.ElementalLightning or WeaponType.DarkLightning or WeaponType.HolyLightning => DamageElement.Lightning,
        WeaponType.ElementalPoison or WeaponType.DarkPoison or WeaponType.HolyPoison  => DamageElement.Poison,
        _                                                                               => DamageElement.None,
    };
}
