using System.Collections.Generic;
using UnityEngine;

public class StatusEffectSystem : MonoBehaviour
{
    // 외부에서 읽어가는 보정값 (1f = 정상)
    public float AttackIntervalMult  { get; private set; } = 1f;  // >1 느려짐, <1 빨라짐
    public float MoveSpeedMult       { get; private set; } = 1f;  // <1 느려짐, >1 빨라짐
    public float HealMult            { get; private set; } = 1f;  // <1 = 힐량 감소
    public float AttackChanceMod     { get; private set; } = 1f;  // <1 = 공격 적중 감소
    public float DefenseChanceMod    { get; private set; } = 1f;  // <1 = 방어/회피 감소
    public float DamageReductionBonus{ get; private set; } = 0f;  // >0 = 피해 감소 (0–1)
    public float StatusResistBonus   { get; private set; } = 0f;  // >0 = 상태이상 저항 (0–1)

    // 보호막 (신성 얼음): 기본 체력과 별도로 피해를 먼저 흡수
    public float ShieldPool { get; private set; } = 0f;

    // 암흑 독: 회복되지 않는 2차 피통
    public float CorruptionGauge { get; private set; }
    public const float MaxCorruption = 100f;

    readonly List<StatusEffect> _active = new();
    BodyPartSystem _bodyParts;

    void Start()
    {
        _bodyParts = GetComponent<BodyPartSystem>();
    }

    void Update()
    {
        TickDurations();
        RecalculateMods();
        ApplyBurningDoT();
    }

    public void ApplyEffect(StatusEffectType type, float duration, float strength)
    {
        for (int i = 0; i < _active.Count; i++)
        {
            if (_active[i].Type != type) continue;
            var e = _active[i];
            e.Duration = Mathf.Max(e.Duration, duration);
            e.Strength = Mathf.Max(e.Strength, strength);
            _active[i] = e;
            return;
        }
        _active.Add(new StatusEffect { Type = type, Duration = duration, Strength = strength });
        Debug.Log($"[Status] {gameObject.name} {type} 적용 (duration:{duration}s, strength:{strength})");
    }

    public void AddCorruption(float amount)
    {
        CorruptionGauge = Mathf.Min(MaxCorruption, CorruptionGauge + amount);
        Debug.Log($"[Status] {gameObject.name} 부패 게이지 +{amount:F1} ({CorruptionGauge:F1}/{MaxCorruption})");
    }

    public void AddShield(float amount)
    {
        ShieldPool += amount;
        Debug.Log($"[Holy] {gameObject.name} 보호막 +{amount:F1} (총 {ShieldPool:F1})");
    }

    // 피해를 보호막으로 흡수. 반환값 = 보호막을 뚫고 남은 피해량
    public float AbsorbShield(float damage)
    {
        float absorbed = Mathf.Min(ShieldPool, damage);
        ShieldPool -= absorbed;
        return damage - absorbed;
    }

    public void ClearDebuffs()
    {
        _active.RemoveAll(e => e.Type == StatusEffectType.Burning
                            || e.Type == StatusEffectType.HealReduction
                            || e.Type == StatusEffectType.Slowed
                            || e.Type == StatusEffectType.Weakened
                            || e.Type == StatusEffectType.Wet);
        Debug.Log($"[Status] {gameObject.name} 디버프 제거");
    }

    public void ClearCorruption()
    {
        CorruptionGauge = 0f;
        Debug.Log($"[Status] {gameObject.name} 부패 게이지 초기화");
    }

    void TickDurations()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var e = _active[i];
            e.Duration -= Time.deltaTime;
            if (e.Duration <= 0f) { _active.RemoveAt(i); continue; }
            _active[i] = e;
        }
    }

    void RecalculateMods()
    {
        AttackIntervalMult   = 1f;
        MoveSpeedMult        = 1f;
        HealMult             = 1f;
        AttackChanceMod      = 1f;
        DefenseChanceMod     = 1f;
        DamageReductionBonus = 0f;
        StatusResistBonus    = 0f;

        foreach (var e in _active)
        {
            switch (e.Type)
            {
                case StatusEffectType.Slowed:
                    AttackIntervalMult = Mathf.Max(AttackIntervalMult, 1f + e.Strength);
                    MoveSpeedMult      = Mathf.Min(MoveSpeedMult,      1f - e.Strength * 0.5f);
                    break;
                case StatusEffectType.HealReduction:
                    HealMult = Mathf.Min(HealMult, 1f - e.Strength);
                    break;
                case StatusEffectType.Weakened:
                    AttackChanceMod  = Mathf.Min(AttackChanceMod,  1f - e.Strength);
                    DefenseChanceMod = Mathf.Min(DefenseChanceMod, 1f - e.Strength);
                    break;
                case StatusEffectType.Shielded:
                    DamageReductionBonus = Mathf.Max(DamageReductionBonus, e.Strength);
                    StatusResistBonus    = Mathf.Max(StatusResistBonus,    e.Strength * 0.5f);
                    break;
                case StatusEffectType.Hasted:
                    AttackIntervalMult = Mathf.Min(AttackIntervalMult, 1f - e.Strength * 0.5f);
                    MoveSpeedMult      = Mathf.Max(MoveSpeedMult,      1f + e.Strength * 0.5f);
                    break;
                // 젖으면 무거워 느려진다. 감속은 얕고(0.25×) **본체는 전기 증폭**이다
                // (`BodyPartSystem`이 `IsWet`을 본다) — 물 하나로 눕히는 것이 아니라
                // 다음 한 방을 아프게 하는 원소다.
                case StatusEffectType.Wet:
                    MoveSpeedMult = Mathf.Min(MoveSpeedMult, 1f - e.Strength * 0.25f);
                    break;
            }
        }
    }

    // **젖어 있나** — 전기 증폭(`BodyPartSystem`)이 묻는다.
    public bool IsWet
    {
        get
        {
            foreach (var e in _active)
                if (e.Type == StatusEffectType.Wet) return true;
            return false;
        }
    }

    // **불이 물을 말린다.** 안 그러면 젖은 채로 계속 타는 그림이 되고, 무엇보다 물을 끼얹어
    // 두면 불·전기가 **둘 다** 잘 드는 공짜 조합이 된다(물의 값은 전기 하나여야 한다).
    public void DryOff() => _active.RemoveAll(e => e.Type == StatusEffectType.Wet);

    void ApplyBurningDoT()
    {
        if (_bodyParts == null) return;

        foreach (var e in _active)
        {
            if (e.Type != StatusEffectType.Burning) continue;
            _bodyParts.TakeDamage(new DamageInfo
            {
                Amount       = e.Strength * Time.deltaTime,
                Type         = DamageType.Fire,
                EffectChance = 0f,   // DoT는 상태이상 연쇄 없음
                IgnoreArmor  = true, // 매 틱 미세 피해가 방어구 고정 감소로 0이 되던 것 방지 (화상이 방어구로 무효화되던 버그)
            });
        }
    }
}
