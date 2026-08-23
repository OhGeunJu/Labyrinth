public enum StatusEffectType
{
    Burning,        // 불: DoT
    HealReduction,  // 암흑: 힐량 감소
    Slowed,         // 전기: 공격속도/이동속도 감소(경직)
    Weakened,       // 공격/방어 확률 감소
    // 암흑의 부패(CorruptionGauge)는 StatusEffectSystem에서 별도 게이지로 관리

    Shielded,       // 피해 감소 + 상태이상 저항
    Hasted,         // 공격속도/이동속도 증가

    // **물이 혼자서는 약해서 있는 것이 아니다** — 젖으면 느려지고 **전기가 더 아프다**.
    // 원소끼리 얽히는 유일한 자리라, 이게 없으면 물은 약한 감속기에 지나지 않는다.
    Wet,
}
