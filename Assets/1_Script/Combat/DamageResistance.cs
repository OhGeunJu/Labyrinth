using System.Collections.Generic;
using UnityEngine;

// **저항이 없으면 원소는 장식이다**(2026-08-23).
//
// 원소를 일곱으로 늘려 놓고 저항이 없으면 무기를 바꿀 이유가 없다 — 색만 다른 피해가 된다.
// 저항표가 있어야 *"저놈에게는 이걸 쓴다"*가 성립하고, 그것이 원소를 두는 유일한 이유다.
//
// **드는 곳 둘**: `ArmorData`(입은 것) · `UnitData`(타고난 것). 둘은 더해진다.
[System.Serializable]
public struct DamageResistance
{
    public DamageType Type;

    [Tooltip("0.4 = 40% 덜 아프다. **음수는 취약**(-1이면 두 배). 안 적은 타입은 0.")]
    [Range(-2f, 0.9f)]
    public float Amount;
}

public static class DamageResistanceExt
{
    // 최종 배율. **바닥을 0.1로 깐다** — 완전 면역은 표로 만들 수 있어도 화면에서는
    // *"안 맞는다"*로 보여 무엇이 일어나는지 알 수가 없다(0을 적어야 할 만큼 특별한
    // 것이라면 그건 저항이 아니라 규칙이어야 한다).
    public static float Multiplier(this IReadOnlyList<DamageResistance> list, DamageType type)
    {
        if (list == null || list.Count == 0) return 1f;

        float sum = 0f;
        for (int i = 0; i < list.Count; i++)
            if (list[i].Type == type) sum += list[i].Amount;

        return Mathf.Clamp(1f - sum, 0.1f, 3f);
    }
}
