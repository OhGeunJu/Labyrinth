using System;
using UnityEngine;

public enum BodyPartType
{
    Head,
    Chest,
    Abdomen,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg,
}

[Serializable]
public class BodyPart
{
    public BodyPartType Type;
    public float MaxHP;
    public float CurrentHP;
    public float BleedRate;
    public float HitWeight;       // 피격 확률 가중치 (부위 크기·노출도. 클수록 자주 맞음)
    public float LastWoundTime;   // 마지막 피격 시각 (자연 지혈 계산용)

    public bool IsDisabled  => CurrentHP <= 0f;
    public bool IsDestroyed => CurrentHP <= -100f;

    public BodyPart(BodyPartType type, float maxHP, float bleedRate, float hitWeight = 1f)
    {
        Type      = type;
        MaxHP     = maxHP;
        CurrentHP = maxHP;
        BleedRate = bleedRate;
        HitWeight = hitWeight;
    }

    public void TakeDamage(float damage)
    {
        CurrentHP = Mathf.Max(-100f, CurrentHP - damage);
    }

    public void Heal(float amount)
    {
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }
}
