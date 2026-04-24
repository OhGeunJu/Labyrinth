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

    public float Blood { get; private set; }

    readonly Dictionary<BodyPartType, BodyPart> _parts = new();

    PawnMover _mover;
    UnitBrain _brain;
    bool _bloodStaggerTriggered;  // 30% 임계값 중복 방지

    void Awake()
    {
        _mover = GetComponent<PawnMover>();
        _brain = GetComponent<UnitBrain>();
        Blood  = maxBlood;
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
    public void TakeDamage(float damage)
    {
        // 강인함 피해 저항: 강인함 0→100%, 50→67%, 100→50%
        float actualDamage = damage / (1f + _brain.Stats.Toughness / 100f);

        var partType = GetRandomBodyPart();
        var part     = _parts[partType];

        bool wasDisabled  = part.IsDisabled;
        bool wasDestroyed = part.IsDestroyed;

        part.TakeDamage(actualDamage);

        if (!wasDisabled  && part.IsDisabled)  OnPartDisabledInternal(partType);
        if (!wasDestroyed && part.IsDestroyed) OnPartDestroyedInternal(partType);
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
}
