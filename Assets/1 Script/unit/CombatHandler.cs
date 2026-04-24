using System;
using UnityEngine;

public class CombatHandler : MonoBehaviour
{
    public static event Action<CombatHandler, float> OnDamageDealt;

    [Header("공격 설정")]
    [SerializeField] float baseAttackInterval = 2f;  // 무기 미장착 시 폴백
    [SerializeField] float growthRate = 0.1f;

    float _attackTimer;
    UnitStats _stats;
    UnitBrain _brain;
    BodyPartSystem _bodyParts;
    Equipment _equipment;

    public bool IsInCombat { get; private set; }

    void Start()
    {
        _brain     = GetComponent<UnitBrain>();
        _stats     = _brain.Stats;
        _bodyParts = GetComponent<BodyPartSystem>();
        _equipment = GetComponent<Equipment>();
    }

    void Update()
    {
        if (!IsInCombat) return;

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= CalcAttackInterval())
        {
            _attackTimer = 0f;
            WeaponData weapon = _equipment?.MainHand;
            if (weapon != null && weapon.IsRanged)
                TryFireRanged(_brain.Blackboard.Target);
            else
                TryAttack();
        }
    }

    public void EnterCombat() => IsInCombat = true;

    public void ExitCombat()
    {
        IsInCombat = false;
        _attackTimer = 0f;
    }

    // ── 근접 공격 ────────────────────────────────────────────────────
    void TryAttack()
    {
        var target = _brain.Blackboard.Target;
        if (target == null) return;

        var targetCombat     = target.GetComponent<CombatHandler>();
        var targetBrain      = target.GetComponent<UnitBrain>();
        var targetBodySystem = target.GetComponent<BodyPartSystem>();
        if (targetBrain == null) return;

        WeaponType weaponType = GetWeaponType();
        bool unarmed          = weaponType == WeaponType.Unarmed;
        float weaponSkill     = _stats.GetSkillForWeapon(weaponType);
        float attackSkill     = _stats.GetAttackCombatSkill(unarmed);

        if (targetCombat != null && TryBlock(targetCombat, attackSkill))
        {
            GrowDefenseStat(targetCombat);
            Debug.Log($"[Combat] {target.name} 방어 성공");
            return;
        }

        if (targetCombat != null && TryDodge(targetCombat, attackSkill))
        {
            targetCombat._stats.Evasion = Mathf.Min(100f, targetCombat._stats.Evasion + growthRate);
            Debug.Log($"[Combat] {target.name} 회피 성공");
            return;
        }

        float damage = CalcMeleeDamage(weaponType, weaponSkill, unarmed);

        if (targetBodySystem != null)
            targetBodySystem.TakeDamage(damage);
        else
            targetBrain.TakeDamage(damage);

        // 무기 숙련도 + 근접공격 스킬 동시 성장
        _stats.GrowSkillForWeapon(weaponType, growthRate);
        if (!unarmed)
            _stats.MeleeAttack = Mathf.Min(100f, _stats.MeleeAttack + growthRate * 0.5f);

        OnDamageDealt?.Invoke(this, damage);
        Debug.Log($"[Combat] {gameObject.name} → {target.name} {damage:F1} 데미지 ({weaponType})");
    }

    float CalcMeleeDamage(WeaponType weaponType, float weaponSkill, bool unarmed)
    {
        float statusCorrection = CalcStatusCorrection();
        float weightCorrection = CalcWeightCorrection();
        float baseDamage       = _brain.Blackboard.AttackDamage;

        float damage = baseDamage * (1f + weaponSkill / 100f);

        // 절단 무기(검/도끼) → 손재주 보정
        WeaponData weapon = _equipment?.MainHand;
        if (weapon != null && weapon.IsBladed)
            damage *= 1f + _stats.Dexterity / 100f * 0.5f;

        // 맨손 → 근력 보정
        if (unarmed)
            damage *= 1f + _stats.Strength / 100f * 0.5f;

        return damage * statusCorrection * weightCorrection;
    }

    // ── 방어/회피 ────────────────────────────────────────────────────
    bool TryBlock(CombatHandler defender, float attackerAttackSkill)
    {
        bool defUnarmed    = defender.GetWeaponType() == WeaponType.Unarmed;
        float defenseSkill = defender._stats.GetDefenseCombatSkill(defUnarmed);
        float blockChance  = defenseSkill / (attackerAttackSkill + defenseSkill);
        return UnityEngine.Random.value < blockChance;
    }

    bool TryDodge(CombatHandler defender, float attackerAttackSkill)
    {
        float evasion     = defender._stats.Evasion;
        float dodgeChance = evasion / (attackerAttackSkill + evasion);
        return UnityEngine.Random.value < dodgeChance;
    }

    void GrowDefenseStat(CombatHandler defender)
    {
        bool defUnarmed = defender.GetWeaponType() == WeaponType.Unarmed;
        if (defUnarmed)
            defender._stats.MartialArts  = Mathf.Min(100f, defender._stats.MartialArts  + growthRate);
        else
            defender._stats.MeleeDefense = Mathf.Min(100f, defender._stats.MeleeDefense + growthRate);
    }

    // ── 원거리/마법 ──────────────────────────────────────────────────
    public void TryFireRanged(GameObject intendedTarget)
    {
        float distance        = Vector3.Distance(transform.position, intendedTarget.transform.position);
        float effectiveRange  = _brain.Blackboard.AttackRange;
        float distancePenalty = Mathf.Clamp01((distance - effectiveRange) / effectiveRange);
        float accuracyChance  = (_stats.Cognition / 100f) * (1f - distancePenalty);

        if (UnityEngine.Random.value > accuracyChance)
        {
            Debug.Log($"[Combat] {gameObject.name} → 허공 (빗나감)");
            return;
        }

        FireRanged(intendedTarget);
    }

    public void FireRanged(GameObject intendedTarget)
    {
        float hitChance = 0.6f + (_stats.PrecisionShooting / 100f) * 0.4f;
        _stats.PrecisionShooting = Mathf.Min(100f, _stats.PrecisionShooting + growthRate);

        if (UnityEngine.Random.value < hitChance)
            DealRangedDamage(intendedTarget, false);
        else
        {
            GameObject ally = FindAllyInLineOfFire(intendedTarget);
            if (ally != null) DealRangedDamage(ally, true);
            else Debug.Log($"[Combat] {gameObject.name} → 허공 (동료 없음)");
        }
    }

    void DealRangedDamage(GameObject target, bool friendly)
    {
        WeaponType weaponType  = GetWeaponType();
        float weaponSkill      = _stats.GetSkillForWeapon(weaponType);
        float statusCorrection = CalcStatusCorrection();
        float damage = _brain.Blackboard.AttackDamage
                     * (1f + weaponSkill / 100f)
                     * statusCorrection;

        var bodySystem = target.GetComponent<BodyPartSystem>();
        var brain      = target.GetComponent<UnitBrain>();
        if (bodySystem != null) bodySystem.TakeDamage(damage);
        else if (brain != null) brain.TakeDamage(damage);

        _stats.GrowSkillForWeapon(weaponType, growthRate);
        OnDamageDealt?.Invoke(this, damage);
        Debug.Log($"[Combat] {gameObject.name} 원거리 → {target.name} {damage:F1}{(friendly ? " (동료!)" : "")}");
    }

    GameObject FindAllyInLineOfFire(GameObject intendedTarget)
    {
        var myFaction = GetComponent<Faction>();
        if (myFaction == null) return null;

        Vector3 origin    = transform.position;
        Vector3 direction = (intendedTarget.transform.position - origin).normalized;
        float   distance  = Vector3.Distance(origin, intendedTarget.transform.position);

        foreach (var hit in Physics.RaycastAll(origin, direction, distance))
        {
            if (hit.collider.gameObject == gameObject)           continue;
            if (hit.collider.gameObject == intendedTarget)       continue;
            var f = hit.collider.GetComponent<Faction>();
            if (f == null || myFaction.IsHostileTo(f)) continue;
            return hit.collider.gameObject;
        }
        return null;
    }

    // ── 공격 속도 ────────────────────────────────────────────────────
    float CalcAttackInterval()
    {
        WeaponData weapon  = _equipment?.MainHand;
        float interval     = weapon != null ? weapon.BaseAttackInterval : baseAttackInterval;

        float dexBonus      = 1f + _stats.Dexterity / 100f;
        float strengthBonus = (weapon != null && weapon.IsLargeWeapon)
            ? 1f + _stats.Strength / 100f * 0.5f
            : 1f;
        float statusCorrection = Mathf.Max(0.1f, CalcStatusCorrection());
        float weightCorrection = CalcWeightCorrection();

        return interval / (dexBonus * strengthBonus * statusCorrection * weightCorrection);
    }

    // 무게 보정: 최대 수용 무게(근력×5) 초과 시 1/비율로 감소
    float CalcWeightCorrection()
    {
        float maxWeight     = _stats.Strength * 5f;
        float currentWeight = _brain.Inventory.GetTotalWeight();
        if (maxWeight <= 0f || currentWeight <= maxWeight) return 1f;
        return maxWeight / currentWeight;
    }

    // ── 상태 보정 ─────────────────────────────────────────────────────
    float CalcStatusCorrection()
    {
        if (_bodyParts == null) return 1f;

        bool rightArm = !_bodyParts.GetPart(BodyPartType.RightArm).IsDisabled;
        bool leftArm  = !_bodyParts.GetPart(BodyPartType.LeftArm).IsDisabled;
        bool rightLeg = !_bodyParts.GetPart(BodyPartType.RightLeg).IsDisabled;
        bool leftLeg  = !_bodyParts.GetPart(BodyPartType.LeftLeg).IsDisabled;

        if (!rightArm && !leftArm) return 0f;
        if (!rightLeg && !leftLeg) return 0f;

        return (rightArm ? 0.5f : 0f)
             + (leftArm  ? 0.3f : 0f)
             + (rightLeg ? 0.1f : 0f)
             + (leftLeg  ? 0.1f : 0f);
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    WeaponType GetWeaponType() => _equipment?.MainHand?.Type ?? WeaponType.Unarmed;
}
