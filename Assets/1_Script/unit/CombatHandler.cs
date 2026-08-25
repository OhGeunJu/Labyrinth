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

    // **천장이 없으면 위의 몫은 파리티에서만 뜻이 있다**(2026-08-24).
    //
    // 위 표는 공격=방어일 때의 값이고, 그 값만 보고 맞췄기에 **격차가 벌어지는 쪽을 아무도
    // 안 쟀다.** 방어 100 대 공격 20이면 총 96%(27:1)였고, 130 대 10에서는 `dodgeWeight`
    // 1.25가 확률을 1 너머로 밀어 `Random.value < 1.25`가 **항상 참** — 회피 100%로 굳었다.
    // **막는 솜씨가 격차를 버는 것은 맞다. 잘못된 것은 그 끝이 없다는 것이다.**
    [Tooltip("막기+회피를 합쳐 이 이상은 못 피한다. 격차가 아무리 나도 이만큼은 맞는다")]
    [SerializeField, Range(0.1f, 0.95f)] float maxAvoidChance = 0.6f;

    [Tooltip("원거리 발사 직전 이만큼은 멈춰 서서 겨눠야 한다. 움직이면 이 구간이 다시 시작된다")]
    [SerializeField] float aimTime = 0.55f;

    float _attackTimer;
    UnitStats _stats;
    UnitBrain _brain;
    PawnMover _mover;
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
        _mover     = GetComponent<PawnMover>();
    }

    void Update()
    {
        if (!IsInCombat) return;

        // **싸우는 동안에는 표적을 본다**(2026-08-24). 폰은 그전까지 아예 안 돌아서 태어난
        // 방향을 보고 섰고, 그래서 **석궁을 옆으로 쏘고 칼을 등 뒤로 휘둘렀다.** 확률을 아무리
        // 맞춰도 화면에서 무슨 일이 벌어지는지 안 보이는 것이 이 자리였다.
        //
        // 도는 것은 `PawnMover`가 한다 — 자세(잠·기절·업힘)를 그쪽이 들고 있어서 여기서
        // `transform.rotation`을 만지면 누운 폰을 세운다.
        var facing = _brain != null ? _brain.Blackboard.Target : null;
        if (facing != null && _mover != null) _mover.FaceTowards(facing.transform.position);

        _attackTimer += Time.deltaTime;

        float interval = CalcAttackInterval();

        // **간격의 마지막 구간은 조준이다 — 그리고 움직이면 풀린다**(2026-08-24).
        //
        // 석궁의 3.4초는 그전까지 **간격일 뿐 화면에 없었다**: 그동안 폰이 아무것도 안 하니
        // *언제 쏘는지*를 예측할 수 없고, 타이머가 차는 순간 갑자기 피가 깎였다.
        //
        // 조준을 **정지로 표현한다** — 게이지를 띄우지 않는 이유는 난전에서 바가 열 개씩
        // 뜨면 화면이 지저분해지고, 무엇보다 **움직임은 그 자체로 읽히기** 때문이다.
        // 딸려 오는 것이 요점이다: **밀고 들어가면 사수가 못 쏜다.** 장전은 다 됐는데 조준만
        // 못 한 상태로 붙들리므로, 거리를 좁히는 것이 그대로 전술이 된다.
        //
        // **되돌리는 것은 조준뿐이다**(`Min`) — 타이머를 0으로 밀면 사수를 슬쩍슬쩍 건드리는
        // 것만으로 영영 못 쏘게 만들 수 있다. 이건 장전을 취소하는 게 아니라 겨냥이 흔들리는 것이다.
        //
        // **근접은 안 건다** — 칼은 걸어 들어가면서 휘두르는 것이고, 걸면 근접전이 통째로 멎는다.
        if (_attackTimer >= interval - aimTime
            && Weapon != null && Weapon.IsRanged
            && _mover != null && _mover.IsMoving())
            _attackTimer = Mathf.Min(_attackTimer, interval - aimTime);

        if (_attackTimer >= interval)
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

        var avoid = Measure(targetCombat, attackSkill, ranged: false);
        if (targetCombat != null && avoid.RollBlock())
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

        if (targetCombat != null && avoid.RollDodge())
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

    // ── 안 맞는 것은 한 물음이다 — 막기와 회피를 같이 잰다 ──────────────────
    //
    // **따로 굴리면 답이 둘 어긋난다**(2026-08-24).
    //
    // ① **총량에 천장을 씌울 자리가 없다.** 막기는 제 확률만 알고 회피도 제 확률만 알아서
    //    *"합쳐서 얼마나 피하나"*에 답하는 곳이 없었다 — 둘 다 멀쩡해 보이는데 합이 96%가 된다.
    //
    // ② **몫이 격차마다 달라진다.** 막기를 먼저 굴리므로 막기가 커질수록 회피에게 남는 자리가
    //    줄어드는데, 뒤집히는 자리가 있다:
    //
    //      `0.75p == (1 − 0.75p) × 1.25p` → **p = 0.533**
    //      (= 방어자의 막는 솜씨가 공격자의 유효 솜씨보다 **2.75배** 높을 때)
    //
    //    원거리는 거리·움직임이 **공격자 쪽만** 깎아 그 배수를 쉽게 넘는다. 그래서 등급이
    //    같아도 멀리서 쏘면 **회피보다 방어가 더 자주 떴다**(사용자 관찰). 0.75/1.25는
    //    *"회피가 더 잦다"*는 뜻으로 저작한 값인데 **파리티 언저리에서만 참**이었다.
    //
    // **총량을 먼저 정하고 몫으로 나눈다** — 그러면 비율이 격차와 무관하게 늘 37.5 : 62.5다.
    readonly struct Avoidance
    {
        public readonly float Block, Dodge;
        public Avoidance(float block, float dodge) { Block = block; Dodge = dodge; }

        // `Dodge`는 **막기가 실패한 다음의** 확률이다 — 호출처는 예전대로 둘을 차례로 굴리면
        // 되고, 몫은 `Measure`가 이미 맞춰 놓았다.
        public bool RollBlock() => UnityEngine.Random.value < Block;
        public bool RollDodge() => UnityEngine.Random.value < Dodge;
    }

    // **막는 솜씨는 하나다** — 근접이든 원거리든 `Defense`(맨손이면 무술)를 굴린다.
    // 갈리는 것은 **방패**뿐이다: `BlockBonus` ↔ `RangedBlockBonus`. 그래서 *"화살은 잘 막는데
    // 칼은 못 막는 방패"*는 그대로 표현되면서, 폰에게는 *"막는 솜씨"*가 하나로 남는다.
    Avoidance Measure(CombatHandler defender, float attackerAttackSkill, bool ranged)
    {
        // **쓰러진 놈은 막지도 피하지도 못한다** — 둘 다 능동적인 동작이다. 회피를 `Evasion`으로
        // 굴리면 회피 높은 폰은 기절해서도 잘 안 맞아 **눕힌 보람이 사라진다.**
        if (defender == null || IsDown(defender)) return default;

        bool  defUnarmed   = defender.GetWeaponType() == WeaponType.Unarmed;
        float defenseSkill = defender._stats.GetDefenseCombatSkill(defUnarmed);
        float shieldBonus  = defender.ShieldBonus(
            (ranged ? defender.Shield?.RangedBlockBonus : defender.Shield?.BlockBonus) ?? 0f);

        float blockRaw = OpposedChance(defenseSkill + shieldBonus, attackerAttackSkill,
                                       defender.defenseExponent, defender.attackerBias) * defender.blockWeight;
        float dodgeRaw = OpposedChance(defender._stats.Evasion, attackerAttackSkill,
                                       defender.defenseExponent, defender.attackerBias) * defender.dodgeWeight;

        float sum = blockRaw + dodgeRaw;
        if (sum <= 0f) return default;

        // ① **몫** — 여기서 정해지고 격차가 얼마든 안 변한다. 방패를 들면 막기 쪽이 커진다.
        float blockShare = blockRaw / sum;

        // ② **총량** — 둘을 독립으로 겹쳐 재고 천장을 씌운다.
        float total = 1f - (1f - Mathf.Clamp01(blockRaw)) * (1f - Mathf.Clamp01(dodgeRaw));
        if (defender._sfx != null) total *= defender._sfx.DefenseChanceMod;
        total = Mathf.Clamp(total, 0f, defender.maxAvoidChance);

        // ③ 총량을 몫대로 갈라 **막기 → (실패하면) 회피** 두 굴림으로 되돌린다.
        float block = total * blockShare;
        float dodge = block >= 1f ? 0f : Mathf.Clamp01((total - block) / (1f - block));
        return new Avoidance(block, dodge);
    }

    // ── 원거리/마법 ──────────────────────────────────────────────────
    //
    // **맞나? 는 한 물음이라 굴림도 하나다**(2026-08-24).
    //
    // 전에는 **셋**이었다: ① 조준(`인지력/100 × 거리 × 움직임`) ② 명중(`0.6 + 정밀/100 × 0.4`)
    // ③ 방어자의 막기·회피. 셋을 곱하면 평범한 상황(인지 50 · 정밀 50 · 방어자 50 · 사거리
    // 절반 · 움직이는 표적)에서 **16%, 여섯 발에 한 대**였고, 방어자가 자란 폰이면 1% 아래로
    // 떨어져 **사실상 안 맞았다.** 근접은 굴림이 하나(대립)뿐인데 원거리만 셋이라, 같은 폰이
    // 화살로는 아무것도 못 했다.
    //
    // **게다가 ①이 `인지력/100`이라 인지력 0이면 정밀사격이 얼마든 명중 0%였다** — 정작
    // 사격 스탯은 ②에만 들었다. 쏘는 솜씨를 정하는 스탯이 명중을 못 정하고 있었다.
    //
    // 지금은 공격자 쪽 요인을 **유효 사격 솜씨 하나로 접어** 대립 굴림에 넘긴다. 거리와
    // 움직임은 그 솜씨를 깎고(멀수록·움직일수록 겨냥이 나빠진다), 인지력은 **움직이는 표적을
    // 읽는 데만** 든다(제 자리를 지키되 명중을 통째로 막지는 않는다).
    public void TryFireRanged(GameObject intendedTarget)
    {
        if (intendedTarget == null) return;

        float distance       = Vector3.Distance(transform.position, intendedTarget.transform.position);
        float effectiveRange = _brain.Blackboard.AttackRange;
        float normalized     = distance / Mathf.Max(effectiveRange, 0.1f);

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

        // **유효 사격 솜씨** — 이것 하나가 대립 굴림의 공격자 쪽이 된다.
        float aimSkill = _stats.PrecisionShooting * (1f - distancePenalty) * (1f - movingPenalty);
        FireRanged(intendedTarget, aimSkill);
    }

    public void FireRanged(GameObject intendedTarget, float aimSkill)
    {
        // PrecisionShooting은 도전 대상이 없어 SOL만 적용. **쏘면 는다** — 맞았는지는 안 본다.
        _stats.GrowStat(StatName.PrecisionShooting, growthRate * CombatXp.Sol(_stats.PrecisionShooting));
        ShotTracer.Show(this, intendedTarget);
        DealRangedDamage(intendedTarget, aimSkill, friendly: false);
    }

    void DealRangedDamage(GameObject target, float aimSkill, bool friendly)
    {
        var targetCombat = target.GetComponent<CombatHandler>();

        WeaponType weaponType  = GetWeaponType();
        float weaponSkill      = _stats.GetSkillFor(Weapon);
        float attackSkill      = aimSkill;
        if (_sfx != null) attackSkill *= _sfx.AttackChanceMod;

        bool defUnarmed      = targetCombat != null && targetCombat.GetWeaponType() == WeaponType.Unarmed;
        float defRangedSkill = targetCombat != null ? targetCombat._stats.GetDefenseCombatSkill(defUnarmed) : 0f;
        StatName defStatName = defUnarmed ? StatName.MartialArts : StatName.Defense;

        var avoid = Measure(targetCombat, attackSkill, ranged: true);
        if (targetCombat != null && avoid.RollBlock())
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
        if (targetCombat != null && avoid.RollDodge())
        {
            GrowXp(targetCombat, StatName.Evasion, targetCombat._stats.Evasion, attackSkill, 1f);
            Debug.Log($"[Combat] {target.name} 원거리 회피 성공");

            // **피한 화살은 사라지지 않는다 — 뒤에 선 동료에게 간다.**
            //
            // 오사가 여기로 옮겨 왔다. 전에는 *공격자가 빗맞은 것*에 붙어 있었는데 그 굴림을
            // 대립 하나로 접으면서 갈 곳이 없어졌다. **회피 쪽이 오히려 더 맞는 자리다** —
            // 몸을 비켜 준 화살이 그대로 날아가는 그림이고, 막은 화살은 거기서 멎는다.
            //
            // **동료가 또 피해도 거기서 끝난다**(`friendly`) — 안 끊으면 한 발이 줄줄이 이어진다.
            if (!friendly)
            {
                var ally = FindAllyInLineOfFire(target);
                if (ally != null)
                {
                    ShotTracer.Show(this, ally, friendly: true);
                    DealRangedDamage(ally, aimSkill, friendly: true);
                }
            }
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

    // **오사는 실제로 일어나야 뜻이 있다**(2026-08-24).
    //
    // 전에는 **발밑에서 발밑으로 두께 0인 선**을 쐈다: 원점이 `transform.position`인데 폰의
    // 피벗은 **발밑**이라(`PawnPlacement`) 레이가 땅바닥을 기었고, 몸통 캡슐(y 0~2)의 맨
    // 아래 모서리만 스쳤다 — 조금만 오르내리막이면 그마저 빗나간다. 게다가 두께가 없어
    // 동료가 **선 위에 정확히 서 있어야만** 걸렸다. 그래서 규칙은 있는데 **한 번도 안 났다**.
    //
    // 지금은 **가슴에서 가슴으로, 몸통 굵기만큼 두껍게** 훑는다. 화살이 지나가는 길은 선이
    // 아니라 **통**이라, 옆에 바짝 붙어 선 동료도 그 길 위에 있다.
    const float ShotRadius = 0.35f;

    GameObject FindAllyInLineOfFire(GameObject intendedTarget)
    {
        var myFaction = GetComponent<Faction>();
        if (myFaction == null) return null;

        Vector3 origin = transform.position + Vector3.up * PawnPlacement.CenterOffset(gameObject);
        Vector3 aim    = intendedTarget.transform.position
                       + Vector3.up * PawnPlacement.CenterOffset(intendedTarget);
        Vector3 delta  = aim - origin;
        float   dist   = delta.magnitude;
        if (dist <= 0.01f) return null;

        // **가장 가까운 동료가 맞는다** — 여럿이 겹쳐 서 있으면 앞사람이 먼저다.
        GameObject best = null;
        float bestDist  = float.MaxValue;
        foreach (var hit in Physics.SphereCastAll(origin, ShotRadius, delta / dist, dist))
        {
            if (hit.collider.gameObject == gameObject)     continue;
            if (hit.collider.gameObject == intendedTarget) continue;

            // 적대 대상이 아니면(아군·중립) 오사 대상 — 중립을 쏴서 원한 사는 것도 이 길이다.
            var f = hit.collider.GetComponent<Faction>();
            if (f == null || myFaction.IsHostileTo(f)) continue;

            // **쓰러진 놈은 안 맞는다** — 바닥에 누운 몸은 화살이 지나가는 높이에 없고,
            // 무엇보다 기절이 회복 가능한 상태라는 규칙을 오사가 뒤로 뚫으면 안 된다.
            var c = hit.collider.GetComponent<CombatHandler>();
            if (IsDown(c)) continue;

            if (hit.distance < bestDist) { bestDist = hit.distance; best = hit.collider.gameObject; }
        }
        return best;
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
