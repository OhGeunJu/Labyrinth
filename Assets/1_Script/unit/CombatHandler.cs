using System;
using UnityEngine;

public class CombatHandler : MonoBehaviour
{
    public static event Action<CombatHandler, float> OnDamageDealt;

    [Header("공격 설정")]
    [SerializeField] float baseAttackInterval = 2f;  // 무기 미장착 시 폴백
    [SerializeField] float growthRate = 0.1f;

    [Header("방어/회피 밸런스")]
    [Tooltip("대립 굴림 지수 k — 클수록 공격/방어 스킬 격차가 확률에 급격히 반영 (1이면 선형)")]
    [SerializeField] float defenseExponent = 1.5f;
    [Tooltip("공격자 우위 계수 — 클수록 파리티(공격=방어) 기본 방어/회피율이 낮아짐 (1이면 50%)")]
    [SerializeField] float attackerBias = 4f;

    float _attackTimer;
    UnitStats _stats;
    UnitBrain _brain;
    BodyPartSystem _bodyParts;
    Equipment _equipment;
    StatusEffectSystem _sfx;

    public bool IsInCombat { get; private set; }

    bool IsMinion => _brain?.Data is MinionData;

    void Start()
    {
        _brain     = GetComponent<UnitBrain>();
        _stats     = _brain.Stats;
        _bodyParts = GetComponent<BodyPartSystem>();
        _equipment = GetComponent<Equipment>();
        _sfx       = GetComponent<StatusEffectSystem>();
    }

    void Update()
    {
        if (!IsInCombat) return;

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= CalcAttackInterval())
        {
            _attackTimer = 0f;
            WeaponData weapon = _equipment?.MainHand;
            var target = _brain.Blackboard.Target;
            if (weapon != null && weapon.IsRanged)
            {
                // BT 틱(0.1s) 사이에 타겟이 파괴될 수 있음 — 매 프레임 도는 여기서도 확인
                if (target != null) TryFireRanged(target);
            }
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

    // ── 켄시식 XP 성장 헬퍼 ─────────────────────────────────────────
    void GrowXp(CombatHandler unit, StatName stat, float mySkill, float oppSkill, float eventMult = 1f)
    {
        if (unit == null) return;
        int enemyCount = CombatXp.CountNearbyEnemies(unit.gameObject);
        float xp = growthRate * eventMult * CombatXp.Calculate(mySkill, oppSkill, enemyCount);
        unit._stats.GrowStat(stat, xp);
    }

    void GrowWeaponXp(CombatHandler unit, WeaponType type, float mySkill, float oppSkill, float eventMult = 1f)
    {
        if (unit == null) return;
        int enemyCount = CombatXp.CountNearbyEnemies(unit.gameObject);
        float xp = growthRate * eventMult * CombatXp.Calculate(mySkill, oppSkill, enemyCount);
        unit._stats.GrowSkillForWeapon(type, xp);
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

        // Weakened 디버프: 공격 적중률 감소
        if (_sfx != null) attackSkill *= _sfx.AttackChanceMod;

        bool defUnarmed     = targetCombat != null && targetCombat.GetWeaponType() == WeaponType.Unarmed;
        float defSkill      = targetCombat != null ? targetCombat._stats.GetDefenseCombatSkill(defUnarmed) : 0f;
        StatName defStatName = defUnarmed ? StatName.MartialArts : StatName.MeleeDefense;

        if (targetCombat != null && TryBlock(targetCombat, attackSkill))
        {
            // 방어 성공: 적은 XP (baseline 1x)
            GrowXp(targetCombat, defStatName, defSkill, attackSkill, 1f);
            // 공격자: 블록 당함 = 0.5x
            GrowWeaponXp(this, weaponType, weaponSkill, defSkill, 0.5f);
            if (!unarmed) GrowXp(this, StatName.MeleeAttack, attackSkill, defSkill, 0.25f);
            Debug.Log($"[Combat] {target.name} 방어 성공");
            return;
        }

        if (targetCombat != null && TryDodge(targetCombat, attackSkill))
        {
            // 회피 성공: Evasion baseline XP, 공격자는 히트박스 미접촉 → 0
            GrowXp(targetCombat, StatName.Evasion, targetCombat._stats.Evasion, attackSkill, 1f);
            Debug.Log($"[Combat] {target.name} 회피 성공");
            return;
        }

        float damage = CalcMeleeDamage(weaponType, weaponSkill, unarmed);
        DamageInfo dmgInfo = BuildDamageInfo(damage);

        if (targetBodySystem != null)
            targetBodySystem.TakeDamage(dmgInfo);
        else
            targetBrain.TakeDamage(dmgInfo);

        // 신성 무기: 공격 성공 시 힐/버프 발동
        var weapon = _equipment?.MainHand;
        if (weapon != null && weapon.EffectiveDamageCategory == DamageCategory.Holy
            && weapon.EffectChance > 0f && UnityEngine.Random.value < weapon.EffectChance)
            ApplyHolyEffect(weapon, target);

        // 피격 시: 방어자는 방어 실패 = 8x XP, 공격자는 무기/MeleeAttack 정상
        if (targetCombat != null)
        {
            GrowXp(targetCombat, defStatName, defSkill, attackSkill, 8f);
            GrowXp(targetCombat, StatName.Evasion, targetCombat._stats.Evasion, attackSkill, 2f);
        }
        GrowWeaponXp(this, weaponType, weaponSkill, defSkill, 1f);
        if (!unarmed) GrowXp(this, StatName.MeleeAttack, attackSkill, defSkill, 0.5f);

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
    // 대립 굴림: def^k / (atk^k × bias + def^k)
    //  · k(지수)   — 스킬 격차를 확률에 얼마나 급격히 반영할지
    //  · bias(우위) — 공격자 쪽 가중. 파리티에서 기본 확률 = 1 / (1 + bias)
    static float OpposedChance(float defense, float attack, float exponent, float bias)
    {
        if (defense <= 0f) return 0f;
        float d = Mathf.Pow(defense, exponent);
        float a = Mathf.Pow(Mathf.Max(0f, attack), exponent) * bias;
        return d / (a + d);
    }

    bool TryBlock(CombatHandler defender, float attackerAttackSkill)
    {
        bool defUnarmed    = defender.GetWeaponType() == WeaponType.Unarmed;
        float defenseSkill = defender._stats.GetDefenseCombatSkill(defUnarmed);
        float shieldBonus  = defender._equipment?.OffHand?.BlockBonus ?? 0f;
        float effective    = defenseSkill + shieldBonus;
        float blockChance  = OpposedChance(effective, attackerAttackSkill, defender.defenseExponent, defender.attackerBias);
        if (defender._sfx != null) blockChance *= defender._sfx.DefenseChanceMod;
        return UnityEngine.Random.value < blockChance;
    }

    bool TryBlockRanged(CombatHandler defender, float attackerAttackSkill)
    {
        float defenseSkill = defender._stats.RangedDefense;
        float shieldBonus  = defender._equipment?.OffHand?.RangedBlockBonus ?? 0f;
        float effective    = defenseSkill + shieldBonus;
        float blockChance  = OpposedChance(effective, attackerAttackSkill, defender.defenseExponent, defender.attackerBias);
        if (defender._sfx != null) blockChance *= defender._sfx.DefenseChanceMod;
        return UnityEngine.Random.value < blockChance;
    }

    bool TryDodge(CombatHandler defender, float attackerAttackSkill)
    {
        float evasion     = defender._stats.Evasion;
        float dodgeChance = OpposedChance(evasion, attackerAttackSkill, defender.defenseExponent, defender.attackerBias);
        if (defender._sfx != null) dodgeChance *= defender._sfx.DefenseChanceMod;
        return UnityEngine.Random.value < dodgeChance;
    }

    // ── 원거리/마법 ──────────────────────────────────────────────────
    public void TryFireRanged(GameObject intendedTarget)
    {
        if (intendedTarget == null) return;
        float distance       = Vector3.Distance(transform.position, intendedTarget.transform.position);
        float effectiveRange = _brain.Blackboard.AttackRange;
        float normalized     = distance / Mathf.Max(effectiveRange, 0.1f);

        // TODO: 거리 페널티 수치 추후 조정 필요
        // 사거리 내: 0~40%, 사거리 초과: 40%~100%
        float distancePenalty = normalized <= 1f
            ? normalized * 0.4f
            : 0.4f + (normalized - 1f) * 0.6f;
        distancePenalty = Mathf.Clamp01(distancePenalty);

        // 이동 타겟 페널티: Cognition이 높을수록 감소
        var targetMover     = intendedTarget.GetComponent<PawnMover>();
        float movingPenalty = (targetMover != null && targetMover.HasTarget())
            ? Mathf.Lerp(0.4f, 0f, _stats.Cognition / 100f)
            : 0f;

        float accuracyChance = (_stats.Cognition / 100f) * (1f - distancePenalty) * (1f - movingPenalty);

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
        // PrecisionShooting은 도전 대상이 없어 SOL만 적용
        _stats.GrowStat(StatName.PrecisionShooting, growthRate * CombatXp.Sol(_stats.PrecisionShooting));

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
        var targetCombat = target.GetComponent<CombatHandler>();

        WeaponType weaponType  = GetWeaponType();
        float weaponSkill      = _stats.GetSkillForWeapon(weaponType);
        float attackSkill      = _stats.PrecisionShooting;
        if (_sfx != null) attackSkill *= _sfx.AttackChanceMod;

        float defRangedSkill = targetCombat != null ? targetCombat._stats.RangedDefense : 0f;

        if (targetCombat != null && TryBlockRanged(targetCombat, attackSkill))
        {
            GrowXp(targetCombat, StatName.RangedDefense, defRangedSkill, attackSkill, 1f);
            GrowWeaponXp(this, weaponType, weaponSkill, defRangedSkill, 0.5f);
            Debug.Log($"[Combat] {target.name} 원거리 방어 성공");
            return;
        }

        float statusCorrection = CalcStatusCorrection();
        float damage = _brain.Blackboard.AttackDamage
                     * (1f + weaponSkill / 100f)
                     * statusCorrection;

        DamageInfo dmgInfo = BuildDamageInfo(damage);
        var bodySystem = target.GetComponent<BodyPartSystem>();
        var brain      = target.GetComponent<UnitBrain>();
        if (bodySystem != null) bodySystem.TakeDamage(dmgInfo);
        else if (brain != null) brain.TakeDamage(dmgInfo);

        if (targetCombat != null)
            GrowXp(targetCombat, StatName.RangedDefense, defRangedSkill, attackSkill, 8f);
        GrowWeaponXp(this, weaponType, weaponSkill, defRangedSkill, 1f);
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

        float sfxMult = _sfx != null ? _sfx.AttackIntervalMult : 1f;
        return interval / (dexBonus * strengthBonus * statusCorrection * weightCorrection) * sfxMult;
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

    // ── 신성 효과 ────────────────────────────────────────────────────
    void ApplyHolyEffect(WeaponData weapon, GameObject target)
    {
        var mySfx  = GetComponent<StatusEffectSystem>();
        var myBody = GetComponent<BodyPartSystem>();

        switch (weapon.EffectiveDamageElement)
        {
            case DamageElement.Fire:
                // 큰 단일 회복 (혈액 + 신체 부위) + 디버프 제거
                myBody?.Heal(bloodAmount: 30f, partsAmount: 10f);
                mySfx?.ClearDebuffs();
                Debug.Log($"[Holy] {gameObject.name} 신성 불: 회복 + 디버프 제거");
                break;

            case DamageElement.Ice:
                // 보호막 (별도 체력) + 피해 감소 + 상태이상 저항
                mySfx?.AddShield(40f);
                mySfx?.ApplyEffect(StatusEffectType.Shielded, 8f, 0.3f);
                Debug.Log($"[Holy] {gameObject.name} 신성 얼음: 보호막 + 피해감소");
                break;

            case DamageElement.Lightning:
                // 공격속도/이동속도 증가 + 추가 타격
                mySfx?.ApplyEffect(StatusEffectType.Hasted, 6f, 0.5f);
                ExtraHit(target);
                Debug.Log($"[Holy] {gameObject.name} 신성 전기: 가속 + 추가 타격");
                break;

            case DamageElement.Poison:
                // 광역 회복 (혈액 + 신체) + 부패 게이지 제거
                HolyAoEHeal(bloodAmount: 10f, partsAmount: 3f);
                mySfx?.ClearCorruption();
                Debug.Log($"[Holy] {gameObject.name} 신성 독: 광역 회복 + 부패 제거");
                break;
        }
    }

    void ExtraHit(GameObject target)
    {
        if (target == null) return;
        var weaponType = GetWeaponType();
        float extraDamage = CalcMeleeDamage(weaponType, _stats.GetSkillForWeapon(weaponType), false) * 0.5f;
        var dmg = BuildDamageInfo(extraDamage);
        var body = target.GetComponent<BodyPartSystem>();
        if (body != null) body.TakeDamage(dmg);
        else target.GetComponent<UnitBrain>()?.TakeDamage(dmg);
    }

    void HolyAoEHeal(float bloodAmount, float partsAmount)
    {
        var myFaction = GetComponent<Faction>();
        float range   = _brain.Blackboard.AttackRange + 2f;

        foreach (var col in Physics.OverlapSphere(transform.position, range))
        {
            if (col.gameObject == gameObject) continue;
            var faction = col.GetComponent<Faction>();
            if (myFaction != null && faction != null && myFaction.IsHostileTo(faction)) continue;
            col.GetComponent<BodyPartSystem>()?.Heal(bloodAmount, partsAmount);
        }
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    DamageInfo BuildDamageInfo(float amount)
    {
        WeaponData weapon = _equipment?.MainHand;
        if (weapon == null)
            return DamageInfo.MakePhysical(amount, PhysicalType.Blunt, gameObject);

        return new DamageInfo
        {
            Amount       = amount,
            Category     = weapon.EffectiveDamageCategory,
            PhysicalType = weapon.PhysicalType,
            Element      = weapon.EffectiveDamageElement,
            EffectChance = weapon.EffectChance,
            Source       = gameObject,
        };
    }

    WeaponType GetWeaponType() => _equipment?.MainHand?.Type ?? WeaponType.Unarmed;
}
