public enum StatusEffectType
{
    Burning,        // 원소 불: DoT
    HealReduction,  // 암흑 불: 힐량 감소
    Slowed,         // 암흑 얼음: 공격속도/이동속도 감소
    Weakened,       // 암흑 전기: 공격/방어 확률 감소
    // 암흑 독(CorruptionGauge)은 StatusEffectSystem에서 별도 게이지로 관리

    Shielded,       // 신성 얼음: 피해 감소 + 상태이상 저항
    Hasted,         // 신성 전기: 공격속도/이동속도 증가
}
