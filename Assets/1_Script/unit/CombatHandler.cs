using System;
using UnityEngine;

public class CombatHandler : MonoBehaviour
{
    public static event Action<CombatHandler, float> OnDamageDealt;

    // **휘둘렀다·쏘았다** — 맞았는지와 무관하게 몸이 움직인 순간. 애니메이션(`PawnAnimator`)이
    // 듣는다. 결과 이벤트(`OnDamageDealt`)에 매달면 막힌 공격은 팔이 안 움직인다.
    public event Action OnStrike;

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

    // ── 원거리 스탯 셋 — 하는 일이 하나씩이다 ────────────────────────────
    //
    // **2026-08-26, 사용자 결정.** 그전에는 `정밀사격` 하나가 명중을 다 정하고, 무기 숙련은
    // 피해에 얹히고, `인지력`은 (거리·움직임 감점을 지운 뒤로) **전투에서 아무것도 안 했다.**
    //
    //   · **인지력**   — 맞나. 대립 굴림의 공격자 쪽 + **움직이는 표적**을 읽는다 + **사거리**
    //   · **정밀사격** — **아군 오사 확률**. 그것 하나다
    //   · **무기 숙련** — **재장전**. 원거리 피해에서는 빠졌다(재장전이 곧 DPS다)
    //
    // **사거리를 인지력에 둔 이유**: 명중과 사거리는 *"얼마나 멀리 있는 것을 맞히나"*라는
    // 한 물음의 두 면이다. 지운 거리 감점이 하려던 일을 **확률이 아니라 경계로** 표현한 것이라
    // 화면에 보인다 — 인지력이 낮은 폰은 **가까이 붙어야 쏜다**(`FightNode`가 사거리 밖에서는
    // 안 쏘므로 저절로 걸어 들어간다). 그리고 사거리는 **폰의 성질**이라 무기를 바꿔도
    // 따라온다: 무기 숙련에 몰아넣으면 그건 이미 재장전이 하는 말이다.
    //
    // **상수다** — `[SerializeField]`로 두면 프리팹 스물여섯이 저마다 값을 들고, 코드 기본값을
    // 고쳐도 **이미 있는 프리팹에는 아무 힘이 없다**(`EventManager.checkInterval`이 그랬다).
    // 이건 밸런스 손잡이가 아니라 식의 모양이다.
    // **폭은 2.5배다**(2026-08-26 검증 뒤 0.5 → 1.5). 1.5배였을 때는 D(35)와 SSS+(130)가
    // 석궁으로 15.9m ↔ 21m라 **눈으로 구별이 안 됐다.** 지금은 19.7m ↔ 35m다.
    const float RangeCognitionGain = 1.5f;   // 인지력 130 → 사거리 2.5배 (석궁 14m → 35m)
    const float ReloadSkillGain    = 1.5f;   // 무기 숙련 130 → 재장전 2.5배 빠름 (3.4s → 1.36s)
    const float MovingTargetKeep   = 0.6f;   // 움직이는 표적: 인지력 0이면 명중 솜씨가 이만큼만 남는다
    const float MaxStrayChance     = 0.4f;   // 정밀사격 0이면 오사 확률이 이만큼
    const float StrayConeAngle     = 20f;    // 사수 앞 부채꼴의 **반각**(도)

    // 사거리 배수. **`UnitBlackboard.AttackRange`가 이걸 부른다** — 사거리를 묻는 곳이
    // 여덟이라 거기 한 곳에서 곱해야 어느 하나가 맨 무기 값을 쓰는 일이 안 생긴다.
    public static float RangeMult(UnitStats stats)
        => stats == null ? 1f
         : 1f + stats.Cognition / StatGradeExt.MaxStatValue * RangeCognitionGain;

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
            OnStrike?.Invoke();
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

    // ── 무엇을 하면 무엇이 자라나 ────────────────────────────────────
    //
    // **막았으면 방어가, 피했으면 회피가, 맞았으면 강인함이 자란다**(2026-08-26, 사용자 —
    // *"맞는데 왜 숙련도가 올라가 막아야 올라가지"*). 켄시가 그 배치다.
    //
    // 그전에는 **맞을 때 방어가 8배로 자랐다.** 근거는 어디에도 없었다(2026-06-04에 그냥
    // 들어왔고, 08-23에 방패를 붙일 때 적힌 말은 *"방어 스탯이 이미 그 모양이다"* — 베낀 것이다).
    // 셋이 어긋나 있었다:
    //
    //   ① **표 안에서 안 맞았다** — 같은 줄에서 방어는 8배인데 **회피는 2배**였다.
    //   ② **따라잡기가 두 겹이었다** — `CombatXp.ChallengeMult`가 이미 약한 쪽에 최대 6배를
    //      준다. 8배를 또 곱하면 **48배**까지 간다.
    //   ③ **막는 데 성공할수록 손해였다** — 방어를 키우는 최적 전략이 *"안 막는 것"*이 된다.
    //
    // 무엇보다 **방어가 다른 전투 스탯보다 7~15배 빨리 자랐다.** 사건당 기대 성장(파리티:
    // 막기 13.5% · 회피 22.5% · 피격 64%)으로 재면 무기 0.708 · 근접공격 0.354인데 **방어가
    // 5.255**였다. 8배를 뗀 것은 숫자를 내린 것이 아니라 **나란히 맞춘 것**이다.
    //
    // **그냥 떼기만 하면 방어가 안 자란다**(0.135 = 2.6%) — 막기 성공에서만 자라게 되므로
    // 그 배수를 같이 올려야 한다. 지금:
    //
    //   | 사건 | 배우는 것 | 배수 | 기대 성장 |
    //   |---|---|---|---|
    //   | 막았다 | 방어(맨손=무술) · 방패 | **3.0** | 0.405 |
    //   | 피했다 | 회피                   | **1.5** | 0.338 |
    //   | 맞았다 | **강인함**             | **1.0** | 0.640 |
    //
    // 넷이 0.34~0.71 안에 들어온다. 강인함이 조금 높은 것은 맞는 사건이 제일 잦아서인데,
    // **얻어맞는 폰이 단단해지는 것**이라 뜻이 맞는다.
    //
    // **강인함은 이 길이 생기기 전까지 도발을 쓸 때만 자랐다**(`AbilityUser`) — 도발 능력이
    // 붙은 장비를 안 찬 폰은 기절 저항·피해 감소·지도 전력을 정하는 스탯이 **평생 그대로**였다.
    const float BlockXp = 3f;    // 막았다 → 방어 · 방패
    const float DodgeXp = 1.5f;  // 피했다 → 회피
    const float HitXp   = 1f;    // 맞았다 → 강인함

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

        // **나를 노린 것은 막아도 안다**(2026-08-26). `RecordAttacker`가 `BodyPartSystem`
        // 안에만 있어서 **피해가 실제로 들어갈 때만** 가해자가 잡혔다 — 막거나 피한 공격은
        // 아무 기록도 안 남아, **잘 막는 폰일수록 반격이 늦었다**(최대 60%까지 안 맞으므로
        // 연달아 흘리면 그동안 가만히 서 있는다). 맞았는지는 아픔이 알려 주지만, **누가
        // 나를 치려 했는지는 막아도 안다.**
        //
        // 굴림보다 **먼저** 적는다 — 결과와 무관해야 하기 때문이다.
        targetBrain.Blackboard?.RecordAttacker(gameObject);

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
            GrowXp(targetCombat, defStatName, defSkill, attackSkill, BlockXp);
            GrowShieldXp(targetCombat, attackSkill, BlockXp);
            // 공격자: 블록 당함 = 0.5x
            GrowWeaponXp(this, Weapon, weaponSkill, defSkill, 0.5f);
            if (!unarmed) GrowXp(this, StatName.MeleeAttack, attackSkill, defSkill, 0.25f);
            GrowXp(this, StatName.Dexterity, _stats.Dexterity, defSkill, 0.25f);
            Debug.Log($"[Combat] {target.name} 방어 성공");
            return;
        }

        if (targetCombat != null && avoid.RollDodge())
        {
            // 공격자는 히트박스 미접촉 → 0
            GrowXp(targetCombat, StatName.Evasion, targetCombat._stats.Evasion, attackSkill, DodgeXp);
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

        // **맞았으면 강인함만 자란다** — 못 막고 못 피한 것이라 그 솜씨가 늘 이유가 없다.
        // 배우는 것은 *아픔을 견디는 법*이다.
        if (targetCombat != null)
            GrowXp(targetCombat, StatName.Toughness, targetCombat._stats.Toughness, attackSkill, HitXp);
        GrowWeaponXp(this, Weapon, weaponSkill, defSkill, 1f);
        if (!unarmed) GrowXp(this, StatName.MeleeAttack, attackSkill, defSkill, 0.5f);

        // **손재주는 휘두를수록 는다**(2026-08-26). 그전까지 자라는 길이 **하나도 없어**
        // 태어난 값 그대로 평생 갔다 — 근력(짐)·강인함(맞기)·인지력(원거리 명중)이 저마다
        // 길을 가진 것과 어긋난다. 이 스탯이 실제로 하는 일이 **근접 간격과 베기 피해**이므로
        // 근접 공격이 그 자리다. **맨손도 는다** — 간격 보정은 무기를 안 들어도 걸린다.
        GrowXp(this, StatName.Dexterity, _stats.Dexterity, defSkill, 0.5f);

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
    // **거리와 움직임은 명중에 안 걸린다**(2026-08-26, 사용자 결정 — *"지워버려"*).
    //
    // `distancePenalty`(사거리 내 0~40%)와 `movingPenalty`(0~40%, 인지력이 깎음)는 **첫
    // 커밋(04-25)부터 있던 것인데 결정으로 논의된 적이 한 번도 없다** — 워크로그가 생기기
    // 한 달 전 뼈대 코드에 그냥 들어와 있었고, 08-24에 식을 다시 짤 때도 08-26에 천장 밖으로
    // 뺄 때도 *이미 있는 것*으로 전제하고 지나갔다.
    //
    // 남길 값이 없었다: **솜씨로 어쩔 수 없는 벌칙**이라 사거리 끝에서 명사수와 초짜가 똑같이
    // 헛나갔고, 그러면 그건 활 쏘는 솜씨가 없는 것과 같다.
    //
    // **거리는 사거리가 든다** — `FightNode`가 사거리 밖에서는 아예 안 쏜다. *맞느냐*가
    // 아니라 *쏘느냐*를 정하는 자리이고, 그 하나로 족하다.
    public void TryFireRanged(GameObject intendedTarget)
    {
        if (intendedTarget == null) return;

        // PrecisionShooting은 도전 대상이 없어 SOL만 적용. **쏘면 는다** — 맞았는지는 안 본다.
        _stats.GrowStat(StatName.PrecisionShooting, growthRate * CombatXp.Sol(_stats.PrecisionShooting));

        // **빗맞음을 먼저 굴린다** — 겨냥이 흔들려 엉뚱한 데로 간 화살이라 겨눈 쪽은
        // 관여하지 않는다. 표적은 못 맞힌다(그 발을 버린다).
        var strayed = RollStray(intendedTarget);
        if (strayed != null)
        {
            ShotTracer.Show(this, strayed, stray: true);
            Debug.Log($"[Combat] {gameObject.name} 빗맞음 → {strayed.name}");
            DealRangedDamage(strayed, stray: true);
            return;
        }

        ShotTracer.Show(this, intendedTarget);
        DealRangedDamage(intendedTarget, stray: false);
    }

    // ── 빗맞음 — `정밀사격`이 답하는 것은 이것 하나다 ──────────────────────
    //
    // **겨눈 것을 맞히나**를 정한다. 빗나가면 화살이 사라지는 게 아니라 **부채꼴 안의 다른
    // 누군가가 맞는다** — 동료면 손해고, 옆에 선 다른 적이면 그냥 다른 놈이 맞는 것이다.
    //
    // **세력을 안 가린다**(2026-08-26, 사용자 결정). 한때 비적대만 훑었는데, 그러면 *"적들만
    // 있는 난전에 대고 쏘면 정밀사격이 아무 일도 안 한다"*가 되어 **겨눈 놈을 마무리하려는
    // 바로 그 순간에** 그 스탯이 사라졌다. 지금은 **표적을 고르는 스탯**이다.
    //
    // **부채꼴이 비어 있으면 굴림 자체가 없다.** 매 발 굴리면 정밀사격이 결국 명중도 깎아
    // (그 발을 버리므로) **인지력에 준 축을 나눠 갖는다.** 앞에 누가 있을 때만 물으면 스탯
    // 셋이 하는 일이 정확히 하나씩으로 갈린다 — 허허벌판의 한 놈은 늘 겨눈 대로 맞는다.
    //
    // **선이 아니라 부채꼴이다** — 표적을 향한 방향으로 반각 `StrayConeAngle`, **표적까지**.
    // 사거리 끝까지 재지 않는 이유는 눈으로 재는 것과 같아야 하기 때문이다: 플레이어가 보는
    // 것은 *사수와 적을 잇는 쐐기*이고, 적 너머에 선 사람은 *"내 앞"*으로 안 읽힌다.
    //
    // 덤으로 규칙이 하나 저절로 생긴다 — **멀리서 쏠수록 쐐기가 길고 넓어져** 앞에 끼는
    // 사람이 늘어난다. 붙어서 쏘면 좁다. 그래서 **난전에 대고 쏘는 것**이 가장 위험하다.
    //
    // **빗맞음이 나는 자리는 여기 하나다.** 한때 *표적이 회피하면 화살이 뒤로 지나가 동료에게
    // 간다*는 길이 따로 있었는데(08-23~08-26), 그건 **방어자의 솜씨가 우리 편을 다치게 하는
    // 것**이라 플레이어가 손댈 자리가 없었다.
    GameObject RollStray(GameObject intendedTarget)
    {
        float chance = (1f - _stats.PrecisionShooting / StatGradeExt.MaxStatValue) * MaxStrayChance;
        if (chance <= 0f) return null;

        Vector3 toTarget = intendedTarget.transform.position - transform.position;
        var other = FactionScan.NearestPawnInCone(
            gameObject, toTarget, toTarget.magnitude,
            Mathf.Cos(StrayConeAngle * Mathf.Deg2Rad), exclude: intendedTarget);

        // **아무도 없으면 안 굴린다** — 굴려 놓고 버리면 같은 뜻인데, 안 굴리는 편이
        // *"앞이 비어 있으면 정밀사격은 아무 일도 안 한다"*를 코드에 그대로 적는다.
        if (other == null) return null;

        return UnityEngine.Random.value < chance ? other : null;
    }

    void DealRangedDamage(GameObject target, bool stray)
    {
        var targetCombat = target.GetComponent<CombatHandler>();

        // 막아도 안다 — 근접과 같은 규칙(위 `TryAttack` 주석).
        target.GetComponent<UnitBrain>()?.Blackboard?.RecordAttacker(gameObject);

        WeaponType weaponType  = GetWeaponType();
        float weaponSkill      = _stats.GetSkillFor(Weapon);

        // **맞나는 인지력이 답한다**(2026-08-26). 움직이는 표적이면 명중 솜씨가 깎이는데,
        // **그 깎임을 지우는 것도 인지력이다** — 그래서 키우면 이긴다.
        //
        // 지운 `movingPenalty`와 형태는 같지만 **결정적으로 다르다**: 그때는 감점을 깎는
        // 스탯(인지력)과 명중을 정하는 스탯(정밀사격)이 **달라서 솜씨로 못 이겼다.**
        float attackSkill = _stats.Cognition;
        var targetMover   = target.GetComponent<PawnMover>();
        if (targetMover != null && targetMover.HasTarget())
            attackSkill *= Mathf.Lerp(MovingTargetKeep, 1f,
                                      _stats.Cognition / StatGradeExt.MaxStatValue);
        if (_sfx != null) attackSkill *= _sfx.AttackChanceMod;

        bool defUnarmed      = targetCombat != null && targetCombat.GetWeaponType() == WeaponType.Unarmed;
        float defRangedSkill = targetCombat != null ? targetCombat._stats.GetDefenseCombatSkill(defUnarmed) : 0f;
        StatName defStatName = defUnarmed ? StatName.MartialArts : StatName.Defense;

        var avoid = Measure(targetCombat, attackSkill, ranged: true);
        if (targetCombat != null && avoid.RollBlock())
        {
            GrowXp(targetCombat, defStatName, defRangedSkill, attackSkill, BlockXp);
            GrowShieldXp(targetCombat, attackSkill, BlockXp);
            GrowWeaponXp(this, Weapon, weaponSkill, defRangedSkill, 0.5f);
            GrowXp(this, StatName.Cognition, _stats.Cognition, defRangedSkill, 0.25f);
            Debug.Log($"[Combat] {target.name} 원거리 방어 성공");
            return;
        }

        // **날아오는 것도 피한다**(2026-08-23). 전에는 원거리에 회피 굴림이 아예 없어서
        // **근접은 막기 → 회피 두 번, 원거리는 막기 한 번**이었다 — 같은 폰이 칼보다 화살에
        // 훨씬 잘 맞았고, `Evasion`의 설명(*"몸으로 피한다"*)과도 어긋났다. 화살을 몸으로
        // 피하는 것이 칼을 피하는 것보다 어려울 이유가 없다.
        if (targetCombat != null && avoid.RollDodge())
        {
            GrowXp(targetCombat, StatName.Evasion, targetCombat._stats.Evasion, attackSkill, DodgeXp);
            Debug.Log($"[Combat] {target.name} 원거리 회피 성공");
            return;
        }

        // **무기 숙련은 원거리 피해에 안 얹힌다**(2026-08-26) — 그 스탯이 맡은 것은 재장전이고,
        // **재장전이 곧 DPS다.** 둘 다 주면 숙련 1점이 두 번 값을 하고, 그러면 무기를 오래 쓴
        // 폰과 아닌 폰의 차이가 *다른 모든 선택을 덮는다.* (근접은 그대로 피해에 얹힌다.)
        float statusCorrection = CalcStatusCorrection();
        float damage = _brain.Blackboard.AttackDamage * statusCorrection;

        DamageInfo dmgInfo = BuildDamageInfo(damage);
        var bodySystem = target.GetComponent<BodyPartSystem>();
        var brain      = target.GetComponent<UnitBrain>();
        if (bodySystem != null) bodySystem.TakeDamage(dmgInfo);
        else if (brain != null) brain.TakeDamage(dmgInfo);

        // 맞았으면 강인함만 (근접과 같은 규칙)
        if (targetCombat != null)
            GrowXp(targetCombat, StatName.Toughness, targetCombat._stats.Toughness, attackSkill, HitXp);
        GrowWeaponXp(this, Weapon, weaponSkill, defRangedSkill, 1f);
        GrowXp(this, StatName.Cognition, _stats.Cognition, defRangedSkill, 0.5f);
        OnDamageDealt?.Invoke(this, damage);
        Debug.Log($"[Combat] {gameObject.name} 원거리 → {target.name} {damage:F1}{(stray ? " (빗맞음!)" : "")}");
    }

    // ── 공격 속도 ────────────────────────────────────────────────────
    float CalcAttackInterval()
    {
        WeaponData weapon  = Weapon;
        float interval     = weapon != null ? weapon.BaseAttackInterval : baseAttackInterval;

        // **원거리 재장전은 무기 숙련이 줄인다**(2026-08-26) — 손재주·근력은 **근접 몫으로
        // 물러났다.** 한 손잡이를 두 스탯이 잡으면 어느 쪽을 키워야 빨라지는지가 안 읽히고,
        // 손재주는 제 설명대로(*"제작 속도와 결과물 등급"*) 제작 스탯으로 남는 편이 낫다.
        float skillBonus;
        if (weapon != null && weapon.IsRanged)
            skillBonus = 1f + _stats.GetSkillFor(weapon) / StatGradeExt.MaxStatValue * ReloadSkillGain;
        else
            skillBonus = (1f + _stats.Dexterity / 100f)
                       * ((weapon != null && weapon.IsLargeWeapon)
                            ? 1f + _stats.Strength / 100f * 0.5f
                            : 1f);

        float statusCorrection = Mathf.Max(0.1f, CalcStatusCorrection());
        float weightCorrection = CalcWeightCorrection();

        float sfxMult = _sfx != null ? _sfx.AttackIntervalMult : 1f;
        return interval / (skillBonus * statusCorrection * weightCorrection) * sfxMult;
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
