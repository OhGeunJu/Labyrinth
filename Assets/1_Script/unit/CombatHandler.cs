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

    // **막기와 회피가 같은 식을 쓰되 몫이 다르다**(2026-08-23).
    //
    // 전에는 둘이 같은 확률(파리티 20%)이었는데, **막기를 먼저 굴리므로** 실제 기여는
    // 막기 20% · 회피 16%가 되어 **막기 쪽으로 기운다.** 막기를 눌러 주고 회피를 올리면
    // *안 맞는 총량은 그대로 두면서* 둘이 비슷해진다:
    //
    //   가중 없음 : 막기 20.0% · 회피 20.0%(기여 16.0%) · **총 36.0%**
    //   0.75/1.25 : 막기 15.0% · 회피 25.0%(기여 21.2%) · **총 36.3%**
    //
    // **총량이 그대로인 것이 요점이다** — 맞는 빈도를 바꾸려던 게 아니라 *무엇으로 안 맞는가*의
    // 비율을 바꾸려던 것이다.
    //
    // **새 필드라 프리팹이 코드 기본값을 그대로 받는다** — 이미 있는 필드였다면 프리팹에 박힌
    // 값이 이겨서 아무 일도 안 일어났을 것이다(`EventManager.checkInterval`이 그랬다).
    [Tooltip("막기 확률에 곱한다. 1 미만이면 막기가 덜 뜬다")]
    [SerializeField] float blockWeight = 0.75f;
    [Tooltip("회피 확률에 곱한다. 1 초과면 회피가 더 뜬다")]
    [SerializeField] float dodgeWeight = 1.25f;

    float _attackTimer;
    UnitStats _stats;
    UnitBrain _brain;
    BodyPartSystem _bodyParts;
    Equipment _equipment;
    StatusEffectSystem _sfx;

    public bool IsInCombat { get; private set; }

    bool IsMinion => _brain?.Data is MinionData;

    // ── 한 손이 막혔을 때 ─────────────────────────────────────────────────
    //
    // **어깨에 사람을 메면 남는 손은 하나다.** 양손 무기(`IsLargeWeapon`)는 휘두를 수 없고
    // 방패도 못 든다 — 그 손으로 사람을 붙들고 있기 때문이다.
    //
    // **장비를 벗기지는 않는다.** 벗기면 내려놓을 때 돌려 입히는 길이 필요하고, 그 사이
    // 배낭이 꽉 차 있으면 물건이 새어 나간다. 차고 있되 **지금은 못 쓰는 것**으로 본다.
    //
    // 무기를 묻는 곳이 여덟 군데라 여기 하나로 모은다 — 한 군데만 빠뜨려도 "업고도
    // 대검을 휘두르는" 구멍이 남고, 그 구멍은 눈에 잘 안 띈다.
    bool OneHanded => _brain?.Blackboard?.Carrying != null;

    WeaponData Weapon
    {
        get
        {
            var w = _equipment?.MainHand;
            return OneHanded && w != null && w.IsLargeWeapon ? null : w;
        }
    }

    WeaponData Shield => OneHanded ? null : _equipment?.OffHand;

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
            WeaponData weapon = Weapon;
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

    // **무기 자체를 받는다** — 지팡이는 종류가 하나라 `WeaponType`으로는 불과 암흑이 갈리지
    // 않는다(`UnitStats.StatFor`).
    void GrowWeaponXp(CombatHandler unit, WeaponData weapon, float mySkill, float oppSkill, float eventMult = 1f)
    {
        if (unit == null) return;
        int enemyCount = CombatXp.CountNearbyEnemies(unit.gameObject);
        float xp = growthRate * eventMult * CombatXp.Calculate(mySkill, oppSkill, enemyCount);
        unit._stats.GrowSkillFor(weapon, xp);
    }

    // **방패 스탯이 방패의 몫을 키운다**(2026-08-23).
    //
    // `Defense`(막나)와 **다른 축이다** — 이건 *방패가 얼마나 일하나*라 `BlockBonus`에
    // 곱해진다. 무기가 `MeleeAttack`(맞히나) × 무기 스탯(얼마나 아픈가)으로 갈린 것과 대칭.
    //
    // **방패를 안 들면 저절로 0이다**(0 × 무엇 = 0) — *"방패를 든 폰만 자란다"*가 규칙을
    // 따로 만들지 않아도 나온다.
    float ShieldBonus(float baseBonus)
        => baseBonus <= 0f ? 0f : baseBonus * (1f + _stats.Shield / 100f);

    bool HasShield => Shield != null && (Shield.BlockBonus > 0f || Shield.RangedBlockBonus > 0f);

    // 방패를 들고 있을 때만 얹는다 — 안 그러면 맨몸으로 맞은 폰의 방패 솜씨가 는다.
    void GrowShieldXp(CombatHandler unit, float oppSkill, float eventMult)
    {
        if (unit == null || !unit.HasShield) return;
        GrowXp(unit, StatName.Shield, unit._stats.Shield, oppSkill, eventMult);
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
        float weaponSkill     = _stats.GetSkillFor(Weapon);
        float attackSkill     = _stats.GetAttackCombatSkill(unarmed);

        // Weakened 디버프: 공격 적중률 감소
        if (_sfx != null) attackSkill *= _sfx.AttackChanceMod;

        bool defUnarmed     = targetCombat != null && targetCombat.GetWeaponType() == WeaponType.Unarmed;
        float defSkill      = targetCombat != null ? targetCombat._stats.GetDefenseCombatSkill(defUnarmed) : 0f;
        StatName defStatName = defUnarmed ? StatName.MartialArts : StatName.Defense;

        if (targetCombat != null && TryBlock(targetCombat, attackSkill))
        {
            // 방어 성공: 적은 XP (baseline 1x)
            GrowXp(targetCombat, defStatName, defSkill, attackSkill, 1f);
            GrowShieldXp(targetCombat, attackSkill, 1f);
            // 공격자: 블록 당함 = 0.5x
            GrowWeaponXp(this, Weapon, weaponSkill, defSkill, 0.5f);
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
        var weapon = Weapon;
        if (weapon != null && weapon.DamageType == DamageType.Holy
            && weapon.EffectChance > 0f && UnityEngine.Random.value < weapon.EffectChance)
            ApplyHolyEffect(weapon, target);

        // 피격 시: 방어자는 방어 실패 = 8x XP, 공격자는 무기/MeleeAttack 정상
        if (targetCombat != null)
        {
            // **맞았을 때가 더 크다** — 방어 스탯이 이미 그 모양이다(1.0x ↔ 8.0x).
            // *두들겨 맞으면서 배운다*는 켄시의 성장이 방패에도 그대로 걸린다.
            GrowXp(targetCombat, defStatName, defSkill, attackSkill, 8f);
            GrowXp(targetCombat, StatName.Evasion, targetCombat._stats.Evasion, attackSkill, 2f);
            GrowShieldXp(targetCombat, attackSkill, 8f);
        }
        GrowWeaponXp(this, Weapon, weaponSkill, defSkill, 1f);
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
        WeaponData weapon = Weapon;
        if (weapon != null && weapon.IsBladed)
            damage *= 1f + _stats.Dexterity / 100f * 0.5f;

        // 맨손 → 근력 보정
        if (unarmed)
            damage *= 1f + _stats.Strength / 100f * 0.5f;

        return damage * statusCorrection * weightCorrection;
    }

    // ── 방어/회피 ────────────────────────────────────────────────────
    //
    // **쓰러진 놈은 막지도 피하지도 못한다.** 방어는 무기를 들어 받는 것이고 회피는 몸을
    // 피하는 것이라 둘 다 **능동적인 동작**이다 — 기절한 사람이 할 수 있는 일이 아니다.
    //
    // 회피를 남겨 둘까 망설였지만("누운 놈을 못 맞출 수도 있지 않나") 그건 **공격자 쪽의
    // 실수**다. 그걸 방어자의 `Evasion`으로 굴리면 회피가 높은 폰은 기절해서도 잘 안 맞는
    // 꼴이 되고, 스탯이 높을수록 마무리가 오래 걸린다 — 눕힌 보람이 사라진다.
    // 빗맞음을 넣고 싶으면 방어자 스탯이 아니라 고정 확률이어야 한다.
    static bool IsDown(CombatHandler c) =>
        c != null && c._brain != null && c._brain.Blackboard != null && c._brain.Blackboard.IsKnockedOut;

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
        if (IsDown(defender)) return false;
        bool defUnarmed    = defender.GetWeaponType() == WeaponType.Unarmed;
        float defenseSkill = defender._stats.GetDefenseCombatSkill(defUnarmed);
        float shieldBonus  = defender.ShieldBonus(defender.Shield?.BlockBonus ?? 0f);
        float effective    = defenseSkill + shieldBonus;
        float blockChance  = OpposedChance(effective, attackerAttackSkill, defender.defenseExponent, defender.attackerBias)
                           * defender.blockWeight;
        if (defender._sfx != null) blockChance *= defender._sfx.DefenseChanceMod;
        return UnityEngine.Random.value < blockChance;
    }

    // **막는 솜씨는 하나다** — 근접이든 원거리든 `Defense`(맨손이면 무술)를 굴린다.
    // 갈리는 것은 **방패**뿐이다: `BlockBonus` ↔ `RangedBlockBonus`. 그래서 *"화살은 잘 막는데
    // 칼은 못 막는 방패"*는 그대로 표현되면서, 폰에게는 *"막는 솜씨"*가 하나로 남는다.
    bool TryBlockRanged(CombatHandler defender, float attackerAttackSkill)
    {
        if (IsDown(defender)) return false;
        bool defUnarmed    = defender.GetWeaponType() == WeaponType.Unarmed;
        float defenseSkill = defender._stats.GetDefenseCombatSkill(defUnarmed);
        float shieldBonus  = defender.ShieldBonus(defender.Shield?.RangedBlockBonus ?? 0f);
        float effective    = defenseSkill + shieldBonus;
        float blockChance  = OpposedChance(effective, attackerAttackSkill, defender.defenseExponent, defender.attackerBias)
                           * defender.blockWeight;
        if (defender._sfx != null) blockChance *= defender._sfx.DefenseChanceMod;
        return UnityEngine.Random.value < blockChance;
    }

    bool TryDodge(CombatHandler defender, float attackerAttackSkill)
    {
        if (IsDown(defender)) return false;
        float evasion     = defender._stats.Evasion;
        float dodgeChance = OpposedChance(evasion, attackerAttackSkill, defender.defenseExponent, defender.attackerBias)
                          * defender.dodgeWeight;
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
        float weaponSkill      = _stats.GetSkillFor(Weapon);
        float attackSkill      = _stats.PrecisionShooting;
        if (_sfx != null) attackSkill *= _sfx.AttackChanceMod;

        bool defUnarmed      = targetCombat != null && targetCombat.GetWeaponType() == WeaponType.Unarmed;
        float defRangedSkill = targetCombat != null ? targetCombat._stats.GetDefenseCombatSkill(defUnarmed) : 0f;
        StatName defStatName = defUnarmed ? StatName.MartialArts : StatName.Defense;

        if (targetCombat != null && TryBlockRanged(targetCombat, attackSkill))
        {
            GrowXp(targetCombat, defStatName, defRangedSkill, attackSkill, 1f);
            GrowShieldXp(targetCombat, attackSkill, 1f);
            GrowWeaponXp(this, Weapon, weaponSkill, defRangedSkill, 0.5f);
            Debug.Log($"[Combat] {target.name} 원거리 방어 성공");
            return;
        }

        // **날아오는 것도 피한다**(2026-08-23). 전에는 원거리에 회피 굴림이 아예 없어서
        // **근접은 막기 → 회피 두 번, 원거리는 막기 한 번**이었다 — 같은 폰이 칼보다 화살에
        // 훨씬 잘 맞았고, `Evasion`의 설명(*"몸으로 피한다"*)과도 어긋났다. 화살을 몸으로
        // 피하는 것이 칼을 피하는 것보다 어려울 이유가 없다.
        if (targetCombat != null && TryDodge(targetCombat, attackSkill))
        {
            GrowXp(targetCombat, StatName.Evasion, targetCombat._stats.Evasion, attackSkill, 1f);
            Debug.Log($"[Combat] {target.name} 원거리 회피 성공");
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
        {
            GrowXp(targetCombat, defStatName, defRangedSkill, attackSkill, 8f);
            GrowShieldXp(targetCombat, attackSkill, 8f);
        }
        GrowWeaponXp(this, Weapon, weaponSkill, defRangedSkill, 1f);
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
            // 적대 대상이 아니면(아군·중립) 오사 회피 대상 — 중립을 쏴서 원한 사는 것도 막는다.
            var f = hit.collider.GetComponent<Faction>();
            if (f == null || myFaction.IsHostileTo(f)) continue;
            return hit.collider.gameObject;
        }
        return null;
    }

    // ── 공격 속도 ────────────────────────────────────────────────────
    float CalcAttackInterval()
    {
        WeaponData weapon  = Weapon;
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

    // 무게 보정: 적재 한도(`CarryCapacity`) 초과 시 1/비율로 감소.
    // **적재도 `CarryCapacity`가 답한다** — 배낭만 세면 어깨에 멘 사람이 빠진다.
    float CalcWeightCorrection()
    {
        float maxWeight     = CarryCapacity.For(_stats);
        float currentWeight = CarryCapacity.Load(_brain);
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
    //
    // **넷이던 것을 하나로 접었다**(2026-08-23). 계열 × 원소 시절에는 신성 불/얼음/전기/독이
    // 저마다 다른 축복을 줬는데, 그 넷을 가르는 축이 **원소였고 그 축이 없어졌다.**
    // 지금 신성이 하는 일은 둘이다: **언데드에게 두 배**(`BodyPartSystem`) + 휘두른 사람을
    // 추스르는 것. 여기는 그 둘째다.
    void ApplyHolyEffect(WeaponData weapon, GameObject target)
    {
        var mySfx  = GetComponent<StatusEffectSystem>();
        var myBody = GetComponent<BodyPartSystem>();

        myBody?.Heal(bloodAmount: 30f, partsAmount: 10f);
        mySfx?.ClearDebuffs();
        Debug.Log($"[Holy] {gameObject.name} 신성: 회복 + 디버프 제거");
    }

    void ExtraHit(GameObject target)
    {
        if (target == null) return;
        var weaponType = GetWeaponType();
        float extraDamage = CalcMeleeDamage(weaponType, _stats.GetSkillFor(Weapon), false) * 0.5f;
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
            // 아군만 치유 — 중립은 대상 아님(관계 3단계로 바뀌며 명시화).
            var faction = col.GetComponent<Faction>();
            if (myFaction != null && faction != null && !myFaction.IsAllyOf(faction)) continue;
            col.GetComponent<BodyPartSystem>()?.Heal(bloodAmount, partsAmount);
        }
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    DamageInfo BuildDamageInfo(float amount)
    {
        WeaponData weapon = Weapon;
        if (weapon == null)
            return DamageInfo.MakePhysical(amount, DamageType.Blunt, gameObject);

        return new DamageInfo
        {
            Amount       = amount,
            Type         = weapon.DamageType,
            EffectChance = weapon.EffectChance,
            Source       = gameObject,
        };
    }

    WeaponType GetWeaponType() => Weapon?.Type ?? WeaponType.Unarmed;
}
