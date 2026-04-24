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

    public bool IsDisabled  => CurrentHP <= 0f;
    public bool IsDestroyed => CurrentHP <= -100f;

    public BodyPart(BodyPartType type, float maxHP, float bleedRate)
    {
        Type      = type;
        MaxHP     = maxHP;
        CurrentHP = maxHP;
        BleedRate = bleedRate;
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
