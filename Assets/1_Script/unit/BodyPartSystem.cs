using System;
using System.Collections.Generic;
using UnityEngine;

public class BodyPartSystem : MonoBehaviour
{
    public static event Action<BodyPartSystem, BodyPartType> OnBodyPartDisabled;
    public static event Action<BodyPartSystem, BodyPartType> OnBodyPartDestroyed;
    public static event Action<BodyPartSystem> OnKnockedOut;
    public static event Action<BodyPartSystem> OnRecovered;
    public static event Action<BodyPartSystem> OnDeath;

    [Header("혈액")]
    [SerializeField] float maxBlood = 100f;
    [SerializeField] float healCapDegradationRate = 0.5f; // 힐 1당 캡 감소량 (침대 치료로만 복구)

    [Header("출혈·자연 회복 (켄시식)")]
    [Tooltip("부상 후 이 시간(초)에 걸쳐 출혈이 서서히 멎음. 재피격 시 리셋")]
    [SerializeField] float clotSeconds      = 30f;
    [Tooltip("혈액 초당 자연 회복량 (BloodCap까지)")]
    [SerializeField] float bloodRegenPerSec = 0.2f;
    [Tooltip("부위 HP 초당 자연 회복량 (파괴 부위 제외)")]
    [SerializeField] float partRegenPerSec  = 0.25f;

    [Header("피격 혈액 손실 (모든 피해가 혈액을 깎음)")]
    [Tooltip("베기 피해 → 혈액 손실 배율")]
    [SerializeField] float slashBloodFactor = 0.5f;
    [Tooltip("둔기 피해 → 혈액 손실 배율")]
    [SerializeField] float bluntBloodFactor = 0.1f;
    [Tooltip("그 외(찌르기·원소 등) 피해 → 혈액 손실 배율")]
    [SerializeField] float otherBloodFactor = 0.25f;
    [Tooltip("혈액이 이 비율 밑으로 떨어지면 기절 (과다출혈 — 강인함 무관). 0 = 피가 바닥나면 기절")]
    [SerializeField] float koBloodThreshold = 0f;
    [Tooltip("코어 부위(머리/가슴/복부) HP가 이 깊이 이하 음수면 무조건 기절 (강인함 무관)")]
    [SerializeField] float coreKoDepth      = 10f;
    [Tooltip("사망 혈액 비율 (음수). 켄시식 — 혈액은 0 밑으로 내려가며, 이 비율에 닿아야 사망. 기절을 건너뛰는 즉사 방지")]
    [SerializeField] float deathBloodRatio  = -0.3f;

    [Header("기절 회복")]
    [Tooltip("기상 판정을 시작하기까지의 최소 기절 시간(초)")]
    [SerializeField] float minKnockOutSeconds = 20f;
    [Tooltip("기상에 필요한 혈액 비율 (0~1). 기절 임계보다 높아야 기상 직후 재기절이 없다")]
    [SerializeField] float wakeBloodRatio     = 0.25f;

    public float Blood    { get; private set; }
    public float BloodCap { get; private set; }  // 자연회복 상한 (침대에서 복구)
    public float MaxBlood => maxBlood;

    readonly Dictionary<BodyPartType, BodyPart> _parts = new();

    PawnMover _mover;
    UnitBrain _brain;
    Equipment _equipment;
    Equipment Equip => _equipment ??= GetComponent<Equipment>();

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
        // BleedRate는 지혈(clotSeconds) 전까지의 초당 출혈 — 단일 부상은 출혈만으로 죽지 않는 페이싱.
        // HitWeight는 피격 확률 가중치 — 균등(각 14%)이 아니라 부위 크기·노출도 반영:
        // 몸통(가슴30+복부20=50%) 우세, 사지 각 10%, 머리 10%(작은 표적). 합 100.
        _parts[BodyPartType.Head]     = new BodyPart(BodyPartType.Head,     30f, 0.5f, 10f);
        _parts[BodyPartType.Chest]    = new BodyPart(BodyPartType.Chest,    50f, 0.8f, 30f);
        _parts[BodyPartType.Abdomen]  = new BodyPart(BodyPartType.Abdomen,  40f, 0.4f, 20f);
        _parts[BodyPartType.LeftArm]  = new BodyPart(BodyPartType.LeftArm,  25f, 0.2f, 10f);
        _parts[BodyPartType.RightArm] = new BodyPart(BodyPartType.RightArm, 25f, 0.2f, 10f);
        _parts[BodyPartType.LeftLeg]  = new BodyPart(BodyPartType.LeftLeg,  25f, 0.2f, 10f);
        _parts[BodyPartType.RightLeg] = new BodyPart(BodyPartType.RightLeg, 25f, 0.2f, 10f);
    }

    void Update()
    {
        UpdateBleed();
        UpdateRegen();
        UpdateKnockOutRecovery();
    }

    // ── 피해 처리 ────────────────────────────────────────────────────
    public void TakeDamage(float damage) =>
        TakeDamage(new DamageInfo { Amount = damage, Category = DamageCategory.Physical, PhysicalType = PhysicalType.Blunt });

    public void TakeDamage(DamageInfo info)
    {
        float damage = info.Amount;
        var sfx = GetComponent<StatusEffectSystem>();

        // 가해자 기록 — 중립 세력도 선공당하면 반격/도망할 수 있게(관계와 무관한 축).
        // 같은 편의 오사(사격 관통)는 원한으로 치지 않는다.
        if (info.Source != null && _brain != null && _brain.Blackboard != null)
        {
            var mine  = GetComponent<Faction>();
            var their = info.Source.GetComponent<Faction>();
            if (mine == null || their == null || !mine.IsAllyOf(their))
                _brain.Blackboard.RecordAttacker(info.Source);
        }

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

        // 갑옷 감소 (관통 계열·DoT 우회 제외, 등급 배율 반영)
        bool isPiercing = (info.Category == DamageCategory.Physical  && info.PhysicalType == PhysicalType.Pierce)
                       || (info.Category == DamageCategory.Elemental && info.Element      == DamageElement.Ice);
        if (!isPiercing && !info.IgnoreArmor)
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
        part.LastWoundTime = Time.time;   // 지혈 타이머 리셋

        if (!wasDisabled  && part.IsDisabled)  OnPartDisabledInternal(partType);
        if (!wasDestroyed && part.IsDestroyed) OnPartDestroyedInternal(partType);

        // 코어 부위 손상 상태 판정 — 강인함 굴림은 0 근처에서 버티는 용도.
        // 깊은 음수까지 내려가면(coreKoDepth) 무조건 쓰러진다 (스탯 0~130이라 굴림만으론 면역 가능).
        if (IsCorePart(partType) && !part.IsDestroyed)
        {
            if (part.CurrentHP <= -coreKoDepth) KnockOut();
            else if (wasDisabled)               TryKnockOut();   // 기능상실 상태 추가 피격 — 재판정
        }

        // 모든 피해는 혈액도 깎는다 — 베기는 출혈이 심하고 둔기는 덜함
        float bloodFactor = otherBloodFactor;
        if (info.Category == DamageCategory.Physical)
        {
            if      (info.PhysicalType == PhysicalType.Slash) bloodFactor = slashBloodFactor;
            else if (info.PhysicalType == PhysicalType.Blunt) bloodFactor = bluntBloodFactor;
        }
        Blood -= actualDamage * bloodFactor;   // 음수 허용 (기절→사망 사이 구간)
        if (CheckBloodState()) return;

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

    // 혈액 공통 판정 (켄시식) — 기절 임계 미만이면 쓰러지고, 음수로 더 내려가 사망 비율에
    // 닿아야 죽는다. 강한 한 방이 기절 구간을 건너뛰고 즉사시키는 일이 없다. 혈액 감소 직후 호출.
    // 반환: 사망 여부.
    bool CheckBloodState()
    {
        if (Blood <= maxBlood * deathBloodRatio) { Die(); return true; }
        if (Blood < maxBlood * koBloodThreshold) KnockOut();
        return false;
    }

    static bool IsCorePart(BodyPartType type) =>
        type == BodyPartType.Head || type == BodyPartType.Chest || type == BodyPartType.Abdomen;

    // ── 강인함 판정 ──────────────────────────────────────────────────
    void TryKnockOut()
    {
        float toughness   = _brain.Stats.Toughness;
        // 스탯 범위가 0~130이라 상한 없이는 100+에서 완전 면역 — 최대 90%까지만 저항
        float resistChance = Mathf.Min(0.9f, toughness / 100f);

        if (UnityEngine.Random.value > resistChance)
            KnockOut();
        else
            Debug.Log($"[BodyPart] {gameObject.name} 기절 저항 (Toughness: {toughness:F1})");
    }

    void KnockOut()
    {
        if (_brain.Blackboard.IsKnockedOut) return;
        _brain.Blackboard.IsKnockedOut = true;
        _koTimer = 0f;

        // BT가 정지하므로 전투/이동은 여기서 직접 꺼야 함 (기절 중 공격 지속 방지)
        GetComponent<CombatHandler>()?.ExitCombat();
        _mover?.StopMovement();

        // 눕는 포즈 — CharacterController 캡슐은 회전을 안 따라가므로 충돌은 그대로
        _preKoRotation = transform.rotation;
        transform.rotation = _preKoRotation * Quaternion.Euler(0f, 0f, 90f);

        OnKnockedOut?.Invoke(this);
        Debug.Log($"[BodyPart] {gameObject.name} 기절");
    }

    // ── 기절 회복 (켄시식) ──────────────────────────────────────────
    // 최소 기절 시간 경과 + 코어 부위(머리/가슴/복부) 전부 회복 + 혈액이 기준 이상이면 기상.
    float _koTimer;
    Quaternion _preKoRotation;

    void UpdateKnockOutRecovery()
    {
        if (!_brain.Blackboard.IsKnockedOut) return;

        _koTimer += Time.deltaTime;
        if (_koTimer < minKnockOutSeconds) return;
        // BloodCap이 기준 밑까지 깎인 유닛은 캡까지 찼으면 기상 허용 (기절 중엔 침대를 못 가므로)
        if (Blood < maxBlood * wakeBloodRatio && Blood < BloodCap) return;
        if (_parts[BodyPartType.Head].IsDisabled
         || _parts[BodyPartType.Chest].IsDisabled
         || _parts[BodyPartType.Abdomen].IsDisabled) return;

        _brain.Blackboard.IsKnockedOut = false;

        // ── 기절 자세를 되돌린다 — **다만 지금 누울 자리가 있으면 아니다** ──────────
        //
        // 깨어나는 것과 일어서는 것은 다른 일이다. 침대에 눕혀 둔 폰은 다 나아도 **누워
        // 있어야** 한다(배정이 곧 "쉬어라"라는 명령이고, 포로는 자리가 자세를 정한다).
        //
        // 전에는 무조건 되돌렸고, 우리 폰은 다음 틱에 `RestNode`가 다시 눕혀서 가려졌다 —
        // **한 프레임 벌떡 섰다가 도로 눕는** 것을 아무도 못 본 것뿐이다. 포로는 BT가 안 돌아
        // 그 복구가 없으니 선 채로 남았고, 그제야 드러났다.
        //
        // 고치는 자리는 여기다. 누운 상태를 아는 것이 둘(`PawnMover.IsSleeping` = 침대,
        // `IsPrisoner` = 갇힌 자리)뿐이므로 여기서 한 번 묻고 만다 — 되돌린 뒤에 누가
        // 다시 눕혀 주기를 기대하면 그 "누가"가 없는 경로에서 반드시 드러난다.
        //
        // **업혀 있는 것도 그 자리다.** 어깨 위에서 자세를 되돌리면 업힌 사람이 **어깨 위에
        // 벌떡 선다** — 업기는 90도 눕힌 자세를 부모 기준으로 잡아 두는데(`PawnCarry`)
        // 여기서 세워 버리면 그 자세가 깨지고, 내려놓기 전까지 되돌려 줄 사람도 없다
        // (침대·우리와 정확히 같은 이유다 — "깨어나는 것과 일어서는 것은 다른 일이다").
        bool lyingSomewhere = (_mover != null && _mover.IsSleeping)
                           || _brain.Blackboard.IsPrisoner
                           || _brain.Blackboard.CarriedBy != null;
        if (!lyingSomewhere) transform.rotation = _preKoRotation;
        OnRecovered?.Invoke(this);
        Debug.Log($"[BodyPart] {gameObject.name} 기상 (기절 {_koTimer:F0}초)");
    }

    void Die()
    {
        OnDeath?.Invoke(this);
        _brain.Die();
    }

    // ── 혈액 ────────────────────────────────────────────────────────
    void UpdateBleed()
    {
        // 자연 지혈: 부상 직후 BleedRate로 시작해 clotSeconds에 걸쳐 선형으로 멎는다.
        // 재피격 시 LastWoundTime이 리셋돼 다시 출혈 — 연타당할 때만 출혈이 위험해진다.
        float totalBleed = 0f;
        foreach (var part in _parts.Values)
        {
            if (!part.IsDisabled) continue;
            float clot = clotSeconds > 0f ? 1f - (Time.time - part.LastWoundTime) / clotSeconds : 0f;
            if (clot > 0f) totalBleed += part.BleedRate * clot;
        }

        if (totalBleed > 0f)
        {
            Blood -= totalBleed * Time.deltaTime;
            if (CheckBloodState()) return;
        }
    }

    // ── 자연 회복 ───────────────────────────────────────────────────
    // 혈액은 BloodCap까지, 부위 HP는 파괴 부위 제외 서서히 회복.
    // 침대 휴식(RestNode)은 여기에 추가로 더 빠른 회복을 얹는다.
    void UpdateRegen()
    {
        if (bloodRegenPerSec > 0f && Blood < BloodCap)
            Blood = Mathf.Min(BloodCap, Blood + bloodRegenPerSec * Time.deltaTime);

        if (partRegenPerSec > 0f)
            HealBodyParts(partRegenPerSec * Time.deltaTime);
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
    // 부위별 HitWeight로 가중치 추첨 — 몸통이 자주, 머리가 드물게 맞는다(균등 아님).
    BodyPartType GetRandomBodyPart()
    {
        float total = 0f;
        foreach (var part in _parts.Values) total += part.HitWeight;
        if (total <= 0f) return BodyPartType.Chest;

        float roll = UnityEngine.Random.value * total;
        foreach (var part in _parts.Values)
        {
            roll -= part.HitWeight;
            if (roll <= 0f) return part.Type;
        }
        return BodyPartType.Chest;   // 부동소수 오차 폴백
    }

    public BodyPart GetPart(BodyPartType type) => _parts[type];

    // 파괴는 아니지만 기능 상실 상태인(= 회복 가능한) 부위가 있는가 — 침대 휴식 판단용.
    // 파괴 부위는 영구라 여기 포함하면 침대에서 영영 못 나온다.
    public bool HasRecoverableDisabledPart
    {
        get
        {
            foreach (var part in _parts.Values)
                if (part.IsDisabled && !part.IsDestroyed) return true;
            return false;
        }
    }

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

    // ── 치료 ────────────────────────────────────────────────────────────────
    //
    // **자연 회복·휴식이 못 하는 몫까지 끌어올린다.** 누워 있으면 저절로 낫긴 하지만
    // (`RestNode`), 그건 시간이 지나는 것이지 누가 무엇을 한 것이 아니다. 치료는 사람이
    // 개입하는 자리라 **한 번에 크게, 그리고 즉시 지혈**한다.

    // 손댈 것이 남았는가. **파괴된 부위는 안 센다** — `HealBodyParts`가 건너뛰므로
    // 세어 봐야 영원히 끝나지 않는 일감이 된다(의족은 후유증 축의 일이다).
    public bool NeedsTreatment
    {
        get
        {
            if (BloodCap < maxBlood - 0.5f) return true;
            foreach (var part in _parts.Values)
                if (!part.IsDestroyed && part.CurrentHP < part.MaxHP - 0.5f) return true;
            return false;
        }
    }

    // 치료 한 회차. `TreatJob`이 약을 하나 소모하면서 부른다.
    //
    // **혈액은 캡을 안 깎는다**(`degradeCap: false`) — 캡이 주는 것은 "억지로 끌어올린 값"이라는
    // 뜻인데, 치료는 그 캡 자체를 되돌리는 일이다. 깎으면서 되돌리면 제자리걸음이 된다.
    public void Treat(float partHeal, float capHeal)
    {
        HealBodyParts(partHeal);
        RestoreBloodCap(capHeal);
        HealBlood(partHeal, degradeCap: false);
        StopBleeding();
    }

    // **즉시 지혈.** 출혈은 `LastWoundTime`부터 `clotSeconds`에 걸쳐 저절로 멎는데,
    // 그 시각을 과거로 밀어 지금 멎게 한다. 휴식은 못 하고 치료만 하는 일이다 —
    // 연타로 계속 터지는 출혈을 끊는 것이 치료의 가장 급한 몫이라서.
    public void StopBleeding()
    {
        foreach (var part in _parts.Values)
            part.LastWoundTime = Time.time - clotSeconds;
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

    // 아사(허기) — 무작위 부위/갑옷과 무관한 전신 쇠약. 혈액을 직접 깎아
    // 기존 혈액 파이프라인(기절→사망)으로 서서히 쓰러지게 한다.
    // InspectPanelUI가 Blood를 표시하므로 아사 진행이 눈에 보인다(숨은 _hp 폴백과 대조).
    public void Starve(float amount)
    {
        if (amount <= 0f) return;
        Blood -= amount;   // 음수 허용 — CheckBloodState가 기절/사망까지 처리
        CheckBloodState();
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
