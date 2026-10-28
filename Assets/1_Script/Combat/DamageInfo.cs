using UnityEngine;

public struct DamageInfo
{
    public float      Amount;
    public DamageType Type;
    public float      EffectChance;  // 0–1, 상태이상 발동 확률
    public bool       IgnoreArmor;   // true면 방어구 고정 감소 우회 (DoT 등 — 미세 피해가 매 틱 0으로 깎이는 것 방지)
    public GameObject Source;

    // 물리 피해 진입점 — 상태이상 확률은 원소 피해가 제 손으로 채운다.
    public static DamageInfo MakePhysical(float amount, DamageType type, GameObject src = null) =>
        new DamageInfo { Amount = amount, Type = type, Source = src };
}
