using System;
using System.Collections.Generic;
using UnityEngine;

public class BodyPartSystem : MonoBehaviour
{
    public static event Action<BodyPartSystem, BodyPartType> OnBodyPartDisabled;
    public static event Action<BodyPartSystem, BodyPartType> OnBodyPartDestroyed;
    public static event Action<BodyPartSystem> OnKnockedOut;
    public static event Action<BodyPartSystem> OnStaggered;
    public static event Action<BodyPartSystem> OnDeath;

    [Header("혈액")]
    [SerializeField] float maxBlood = 100f;
    [SerializeField] float staggerBloodThreshold = 0.3f;  // 혈액 30% 이하 시 비틀거림 판정
    [SerializeField] float baseStaggerDuration = 3f;
    [SerializeField] float healCapDegradationRate = 0.5f; // 힐 1당 캡 감소량 (침대 치료로만 복구)

    public float Blood    { get; private set; }
    public float BloodCap { get; private set; }  // 자연회복 상한 (침대에서 복구)
    public float MaxBlood => maxBlood;

    readonly Dictionary<BodyPartType, BodyPart> _parts = new();

    PawnMover _mover;
    UnitBrain _brain;
    Equipment _equipment;
    Equipment Equip => _equipment ??= GetComponent<Equipment>();
    bool _bloodStaggerTriggered;  // 30% 임계값 중복 방지

    void Awake()
    {
        _mover = GetComponent<PawnMover>();
        _brain = GetComponent<UnitBrain>();
        Blood    = maxBlood;
        BloodCap = maxBlood;
        InitBodyParts();
    }

    void InitBodyParts()
    {
        _parts[BodyPartType.Head]     = new BodyPart(BodyPartType.Head,     30f, 3f);
        _parts[BodyPartType.Chest]    = new BodyPart(BodyPartType.Chest,    50f, 5f);
        _parts[BodyPartType.Abdomen]  = new BodyPart(BodyPartType.Abdomen,  40f, 2f);
        _parts[BodyPartType.LeftArm]  = new BodyPart(BodyPartType.LeftArm,  25f, 1f);
        _parts[BodyPartType.RightArm] = new BodyPart(BodyPartType.RightArm, 25f, 1f);
        _parts[BodyPartType.LeftLeg]  = new BodyPart(BodyPartType.LeftLeg,  25f, 1f);
        _parts[BodyPartType.RightLeg] = new BodyPart(BodyPartType.RightLeg, 25f, 1f);
    }

    void Update()
    {
        UpdateBleed();
        UpdateStagger();
    }

    // ── 피해 처리 ────────────────────────────────────────────────────
    public void TakeDamage(float damage) =>
        TakeDamage(new DamageInfo { Amount = damage, Category = DamageCategory.Physical, PhysicalType = PhysicalType.Blunt });

    public void TakeDamage(DamageInfo info)
    {
        float damage = info.Amount;
        var sfx = GetComponent<StatusEffectSystem>();

        // 타입 배율
        if (info.Category == DamageCategory.Physical && info.PhysicalType == PhysicalType.Blunt)
            damage *= 1.3f;
        if (info.Category == DamageCategory.Elemental && info.Element == DamageElement.Lightning)
            damage *= 1.2f;

        // 보호막 흡수 (타입 배율 이후, 갑옷 이전)
        if (sfx != null && sfx.ShieldPool > 0f)
        {
            damage = sfx.AbsorbShield(damage);
            if (damage <= 0f) return;
        }

        var partType = GetRandomBodyPart();

        // 갑옷 감소 (관통 계열 제외, 등급 배율 반영)
        bool isPiercing = (info.Category == DamageCategory.Physical  && info.PhysicalType == PhysicalType.Pierce)
                       || (info.Category == DamageCategory.Elemental && info.Element      == DamageElement.Ice);
        if (!isPiercing)
        {
            var armorSlot = BodyPartToArmorSlot(partType);
            var armor     = Equip?.GetArmor(armorSlot);
            if (armor != null)
            {
                float qualMult = Equip?.GetArmorQualityMult(armorSlot) ?? 1f;
                damage = Mathf.Max(0f, damage - armor.DamageReduction * qualMult);
            }
        }

        // Shielded 버프: 피해 감소 (%)
        if (sfx != null && sfx.DamageReductionBonus > 0f)
            damage = Mathf.Max(0f, damage * (1f - sfx.DamageReductionBonus));

        // 강인함 저항
        float actualDamage = damage / (1f + _brain.Stats.Toughness / 100f);
        var part     = _parts[partType];

        bool wasDisabled  = part.IsDisabled;
        bool wasDestroyed = part.IsDestroyed;

        part.TakeDamage(actualDamage);

        if (!wasDisabled  && part.IsDisabled)  OnPartDisabledInternal(partType);
        if (!wasDestroyed && part.IsDestroyed) OnPartDestroyedInternal(partType);

        // 베기: 추가 출혈
        if (info.Category == DamageCategory.Physical && info.PhysicalType == PhysicalType.Slash)
        {
            Blood = Mathf.Max(0f, Blood - actualDamage * 0.5f);
            if (Blood <= 0f) { Die(); return; }
        }

        // 상태이상 발동 (Shielded 저항 반영)
        if (info.EffectChance > 0f)
        {
            float resist = sfx != null ? sfx.StatusResistBonus : 0f;
            if (UnityEngine.Random.value < info.EffectChance * (1f - resist))
                TryApplyStatusEffect(info, sfx);
        }
    }

    void TryApplyStatusEffect(DamageInfo info, StatusEffectSystem sfx = null)
    {
        sfx ??= GetComponent<StatusEffectSystem>();
        if (sfx == null) return;

        switch (info.Category)
        {
            case DamageCategory.Elemental:
                if (info.Element == DamageElement.Fire)
                    sfx.ApplyEffect(StatusEffectType.Burning, 5f, 5f);
                // Ice: 방어구 관통 (갑옷 시스템에서 처리)
                if (info.Element == DamageElement.Poison)
                    ApplyPoisonAoE(info.Amount);
                break;

            case DamageCategory.Dark:
                switch (info.Element)
                {
                    case DamageElement.Fire:      sfx.ApplyEffect(StatusEffectType.HealReduction, 10f, 0.5f); break;
                    case DamageElement.Ice:       sfx.ApplyEffect(StatusEffectType.Slowed,         8f, 0.5f); break;
                    case DamageElement.Lightning: sfx.ApplyEffect(StatusEffectType.Weakened,       8f, 0.5f); break;
                    case DamageElement.Poison:    sfx.AddCorruption(info.Amount * 0.3f);                      break;
                }
                break;
        }
    }

    // ── 기능 상실 (0) ────────────────────────────────────────────────
    void OnPartDisabledInternal(BodyPartType type)
    {
        OnBodyPartDisabled?.Invoke(this, type);

        switch (type)
        {
            case BodyPartType.Head:
            case BodyPartType.Chest:
            case BodyPartType.Abdomen:
                TryKnockOut();
                break;

            case BodyPartType.LeftArm:
            case BodyPartType.RightArm:
                UpdateAttackAbility();
                break;

            case BodyPartType.LeftLeg:
            case BodyPartType.RightLeg:
                UpdateMoveAbility();
                break;
        }
    }

    // ── 파괴 (-100) ──────────────────────────────────────────────────
    void OnPartDestroyedInternal(BodyPartType type)
    {
        OnBodyPartDestroyed?.Invoke(this, type);

        switch (type)
        {
            // 핵심 부위 파괴 → 즉사
            case BodyPartType.Head:
            case BodyPartType.Chest:
            case BodyPartType.Abdomen:
                Die();
                break;

            // 팔/다리 파괴 → 기절 (강인함 저항 가능)
            case BodyPartType.LeftArm:
            case BodyPartType.RightArm:
            case BodyPartType.LeftLeg:
            case BodyPartType.RightLeg:
                Debug.Log($"[BodyPart] {gameObject.name} {type} 파괴");
                TryKnockOut();
                break;
        }
    }

    // ── 강인함 판정 ──────────────────────────────────────────────────
    void TryKnockOut()
    {
        float toughness   = _brain.Stats.Toughness;
        float resistChance = toughness / 100f;

        if (UnityEngine.Random.value > resistChance)
            KnockOut();
        else
            Debug.Log($"[BodyPart] {gameObject.name} 기절 저항 (Toughness: {toughness:F1})");
    }

    void TryStagger()
    {
        float toughness   = _brain.Stats.Toughness;
        float resistChance = toughness / 100f;

        if (UnityEngine.Random.value > resistChance)
        {
            float duration = Mathf.Max(0.2f, baseStaggerDuration * (1f - toughness / 100f));
            Stagger(duration);
        }
        else
            Debug.Log($"[BodyPart] {gameObject.name} 비틀거림 저항 (Toughness: {toughness:F1})");
    }

    void KnockOut()
    {
        if (_brain.Blackboard.IsKnockedOut) return;
        _brain.Blackboard.IsKnockedOut = true;
        OnKnockedOut?.Invoke(this);
        Debug.Log($"[BodyPart] {gameObject.name} 기절");
    }

    void Stagger(float duration)
    {
        _brain.Blackboard.IsStaggered  = true;
        _brain.Blackboard.StaggerTimer = duration;
        OnStaggered?.Invoke(this);
        Debug.Log($"[BodyPart] {gameObject.name} 비틀거림 {duration:F1}초");
    }

    void Die()
    {
        OnDeath?.Invoke(this);
        _brain.Die();
    }

    // ── 혈액 ────────────────────────────────────────────────────────
    void UpdateBleed()
    {
        float totalBleed = 0f;
        foreach (var part in _parts.Values)
            if (part.IsDisabled) totalBleed += part.BleedRate;

        if (totalBleed > 0f)
        {
            Blood = Mathf.Max(0f, Blood - totalBleed * Time.deltaTime);
            if (Blood <= 0f) { Die(); return; }
        }

        // 혈액 30% 이하 → 비틀거림 판정 (1회)
        if (!_bloodStaggerTriggered && Blood < maxBlood * staggerBloodThreshold)
        {
            _bloodStaggerTriggered = true;
            TryStagger();
        }

        // 혈액 회복 시 임계값 초기화
        if (_bloodStaggerTriggered && Blood >= maxBlood * staggerBloodThreshold)
            _bloodStaggerTriggered = false;
    }

    // ── 비틀거림 타이머 ─────────────────────────────────────────────
    void UpdateStagger()
    {
        if (!_brain.Blackboard.IsStaggered) return;

        _brain.Blackboard.StaggerTimer -= Time.deltaTime;
        if (_brain.Blackboard.StaggerTimer <= 0f)
            _brain.Blackboard.IsStaggered = false;
    }

    // ── 팔/다리 기능 상실 처리 ──────────────────────────────────────
    void UpdateAttackAbility()
    {
        bool leftArm  = _parts[BodyPartType.LeftArm].IsDisabled;
        bool rightArm = _parts[BodyPartType.RightArm].IsDisabled;

        if (leftArm && rightArm)
            Debug.Log($"[BodyPart] {gameObject.name} 팔 양쪽 기능 상실 → 공격 불가");
        else if (leftArm || rightArm)
            Debug.Log($"[BodyPart] {gameObject.name} 팔 한쪽 기능 상실 → 공격력 감소");
    }

    void UpdateMoveAbility()
    {
        bool leftLeg  = _parts[BodyPartType.LeftLeg].IsDisabled;
        bool rightLeg = _parts[BodyPartType.RightLeg].IsDisabled;

        if (leftLeg && rightLeg)
        {
            _mover.SetCrawling(true);
            Debug.Log($"[BodyPart] {gameObject.name} 다리 양쪽 기능 상실 → 기기");
        }
        else if (leftLeg || rightLeg)
        {
            _mover.SetLimping(true);
            Debug.Log($"[BodyPart] {gameObject.name} 다리 한쪽 기능 상실 → 절뚝거림");
        }
    }

    // ── 유틸 ────────────────────────────────────────────────────────
    BodyPartType GetRandomBodyPart()
    {
        var types = (BodyPartType[])Enum.GetValues(typeof(BodyPartType));
        return types[UnityEngine.Random.Range(0, types.Length)];
    }

    public BodyPart GetPart(BodyPartType type) => _parts[type];

    // degradeCap: 포션/마법 힐은 true, 침대 치료는 false
    public void HealBlood(float amount, bool degradeCap = true)
    {
        var sfx    = GetComponent<StatusEffectSystem>();
        float mult = sfx != null ? sfx.HealMult : 1f;
        float actual = Mathf.Min(amount * mult, BloodCap - Blood);
        if (actual <= 0f) return;

        Blood += actual;
        if (degradeCap)
            BloodCap = Mathf.Max(0f, BloodCap - actual * healCapDegradationRate);
    }

    public void RestoreBloodCap(float amount)
    {
        BloodCap = Mathf.Min(maxBlood, BloodCap + amount);
    }

    public void HealBodyParts(float amount)
    {
        foreach (var part in _parts.Values)
        {
            if (part.IsDestroyed) continue;
            bool wasDisabled = part.IsDisabled;
            part.Heal(amount);
            // 기능 상실 → 회복 시 상태 재갱신
            if (wasDisabled && !part.IsDisabled)
            {
                if (part.Type == BodyPartType.LeftLeg || part.Type == BodyPartType.RightLeg)
                    UpdateMoveAbility();
                else if (part.Type == BodyPartType.LeftArm || part.Type == BodyPartType.RightArm)
                    UpdateAttackAbility();
            }
        }
    }

    // 혈액 + 신체 부위 동시 회복 (힐 계열 공통)
    public void Heal(float bloodAmount, float partsAmount)
    {
        HealBlood(bloodAmount);
        HealBodyParts(partsAmount);
    }

    void ApplyPoisonAoE(float originalDamage)
    {
        float aoeDamage = originalDamage * 0.5f;
        foreach (var col in Physics.OverlapSphere(transform.position, 3f))
        {
            if (col.gameObject == gameObject) continue;
            col.GetComponent<BodyPartSystem>()?.TakeDamage(new DamageInfo
            {
                Amount       = aoeDamage,
                Category     = DamageCategory.Elemental,
                Element      = DamageElement.Poison,
                EffectChance = 0f,  // AoE 연쇄 없음
            });
        }
        Debug.Log($"[Poison] {gameObject.name} 주변 독 AoE {aoeDamage:F1} 피해");
    }

    static ArmorSlot BodyPartToArmorSlot(BodyPartType type) => type switch
    {
        BodyPartType.Head                                  => ArmorSlot.Head,
        BodyPartType.Chest or BodyPartType.Abdomen         => ArmorSlot.Chest,
        BodyPartType.LeftArm or BodyPartType.RightArm      => ArmorSlot.Hands,
        BodyPartType.LeftLeg or BodyPartType.RightLeg      => ArmorSlot.Legs,
        _                                                   => ArmorSlot.Chest,
    };
}
