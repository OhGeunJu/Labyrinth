using UnityEngine;

public struct DamageInfo
{
    public float      Amount;
    public DamageType Type;
    public float      EffectChance;  // 0–1, 상태이상 발동 확률
    public bool       IgnoreArmor;   // true면 방어구 고정 감소 우회 (DoT 등 — 미세 피해가 매 틱 0으로 깎이는 것 방지)
    public GameObject Source;

    public static DamageInfo Make(float amount, DamageType type,
                                  float effectChance = 0f, GameObject src = null) =>
        new DamageInfo { Amount = amount, Type = type, EffectChance = effectChance, Source = src };

    // 물리 전용 진입점. `Make`와 하는 일이 같지만 부르는 쪽이 "물리를 넣는다"를 드러낸다.
    public static DamageInfo MakePhysical(float amount, DamageType type, GameObject src = null) =>
        new DamageInfo { Amount = amount, Type = type, Source = src };
}
