using UnityEngine;

public struct DamageInfo
{
    public float          Amount;
    public DamageCategory Category;
    public PhysicalType   PhysicalType;  // Category == Physical 일 때 사용
    public DamageElement  Element;       // Category != Physical 일 때 사용
    public float          EffectChance;  // 0–1, 상태이상 발동 확률
    public GameObject     Source;

    public static DamageInfo MakePhysical(float amount, PhysicalType type, GameObject src = null) =>
        new DamageInfo { Amount = amount, Category = DamageCategory.Physical, PhysicalType = type, Source = src };

    public static DamageInfo MakeMagic(float amount, DamageCategory cat, DamageElement elem,
                                       float effectChance, GameObject src = null) =>
        new DamageInfo { Amount = amount, Category = cat, Element = elem, EffectChance = effectChance, Source = src };
}
