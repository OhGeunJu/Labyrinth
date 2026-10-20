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
    [SerializeField] float partRegenPerSec  = 0.8f;

    // ── 피격 혈액 손실 ───────────────────────────────────────────────────────
    //
    // **부위 HP를 켄시 값(100 균일)으로 올리면서 절반으로 줄였다**(0.5/0.1/0.25 → 0.25/0.05/0.125).
    // 혈액은 100 그대로이므로, 안 줄이면 **모든 전투가 출혈 KO로 끝난다**:
    //
    // | | 출혈 KO | 가슴 무력화(기대) |
    // |---|---|---|
    // | 예전(가슴 50) | 7.7대 | 6.4대 |
    // | 부위 100 · 계수 그대로 | **7.7대** | 12.8대 ← 출혈이 늘 먼저 |
    // | 부위 100 · 계수 절반 | 15.4대 | **12.8대** ← 부위가 먼저 |
    //
    // (강철 대검 26 기준. 무력화는 가중치 30%를 감안한 **총** 타격 수다.)
    // 부위가 먼저 나가고 출혈이 그 뒤를 받치는 것이 예전 모양이고 켄시의 모양이다.
    //
    // **부위별 지속 출혈(`BleedRate`)은 안 줄였다** — 그건 상처가 시간에 걸쳐 피를 흘리는 것이고
    // 지혈(`clotSeconds` 30초)이 끊으므로, 혈액이 100 그대로인 이상 예전 페이싱이 그대로 맞다.
    // 줄이면 단일 부상이 아무것도 아니게 된다.
    [Header("피격 혈액 손실 (모든 피해가 혈액을 깎음)")]
    [Tooltip("베기 피해 → 혈액 손실 배율")]
    [SerializeField] float slashBloodFactor = 0.25f;
    [Tooltip("둔기 피해 → 혈액 손실 배율")]
    [SerializeField] float bluntBloodFactor = 0.05f;
    [Tooltip("그 외(찌르기·원소 등) 피해 → 혈액 손실 배율")]
    [SerializeField] float otherBloodFactor = 0.125f;
    [Tooltip("혈액이 이 비율 밑으로 떨어지면 기절 (과다출혈 — 강인함 무관). 0 = 피가 바닥나면 기절")]
    [SerializeField] float koBloodThreshold = 0f;
    [Tooltip("코어 부위(머리/가슴/복부) HP가 이 깊이 이하 음수면 무조건 기절 (강인함 무관)")]
    [SerializeField] float coreKoDepth      = 30f;
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
        // ── 부위 HP는 켄시 값이다 — **전부 100 균일** ────────────────────────
        //
        // 켄시의 인간은 머리도 가슴도 팔다리도 **다 100**이다. 부위의 무게는 HP가 아니라
        // **어디를 맞느냐(`HitWeight`)와 그 부위가 무엇을 하느냐**가 정한다 — 가슴·배·머리는
        // 코어라 나가면 쓰러지고(`IsCorePart`), 팔이 나가면 못 때리고 다리가 나가면 못 걷는다.
        // **HP로 또 가르면 같은 것을 두 번 표현하는 셈**이고, 실제로 그러면 "가슴은 두 배로
        // 질기고 게다가 제일 자주 맞는다"가 되어 다른 부위가 뜻을 잃는다.
        //
        // **파괴 문턱이 −100 고정**이므로 그 관계도 켄시 그대로다 — 무력화(0)에서 **꼭 그만큼
        // 더** 맞아야 부위가 떨어진다(1:2).
        //
        // 전엔 30/50/40/25였다. 눈금이 1/3이라 **팔다리가 한 방에 무력화됐고**(25 HP ↔ 강철
        // 대검 26), 파괴 문턱만 −100 고정이라 *무력화는 한 대, 절단은 다섯 대*라는 이상한
        // 사이가 생겼다.
        //
        // **피해는 안 올렸다** — 무기·방어구·상태이상은 그대로다. 그래서 **전투가 약 두 배
        // 길어진다**(의도한 것: 켄시식 난전). **혈액도 켄시대로 100 그대로 둔다** — 대신
        // **타격당 혈액 손실(`slashBloodFactor` 등)을 절반으로 줄였다**. 안 줄이면 부위만
        // 질겨져 **모든 전투가 출혈 KO로 끝나고 절단을 영영 못 본다**(위 표).
        //
        // BleedRate는 지혈(clotSeconds) 전까지의 초당 출혈 — 단일 부상은 출혈만으로 죽지 않는
        // 페이싱. **혈액이 100 그대로이므로 이 값도 예전 그대로다.**
        // ── HitWeight — HP가 균일해진 지금 **부위를 가르는 축은 이것 하나다** ─────
        //
        // 균등(각 14%)이 아니라 부위 크기·노출도를 반영한다. **머리 10 · 가슴 18 · 배 16 ·
        // 팔 각 13 · 다리 각 15**(몸통 34% · 사지 56% · 머리 10%, 합 100).
        //
        // 전엔 몸통이 50%(가슴 30 + 배 20)였다. 실루엣으로는 말이 되는데 **결과가 안 됐다** —
        // 쓰러뜨리는 것은 **코어(머리·가슴·배)뿐이라**(`IsCorePart`) 몸통 비중이 곧 전투 길이고,
        // 그 길이 안에서 사지는 거의 안 상한다. 부위 HP를 100 균일로 올리고 나니 그 틈이
        // 벌어져서, **팔 하나 무력화에 총 38대인데 전투는 13대에 끝났다** — 켄시의 상징인
        // 절단·의족을 **구조적으로 못 보는** 배치다.
        //
        // 몸통을 34%로 낮추니 사지가 실제로 상한다 — **다리 25.6대 · 팔 29.6대**(전엔 둘 다
        // 38대였고 전투는 13대에 끝났다). 한 판에 늘 벌어지진 않아도 여러 번 싸우거나
        // 여럿에게 둘러싸이면 **다리가 먼저 나가 못 걷는** 일이 실제로 생긴다.
        //
        // **다리를 팔보다 크게 잡는다**(15 ↔ 13) — 표적이 크고, 나갔을 때의 뜻도 다르다
        // (팔은 못 때리고 다리는 못 걷는다 — 도망도 못 간다). 부위별 총 타격 수는
        // 가슴 21.4 · 배 24 · 다리 25.6 · 팔 29.6 · 머리 38.5로, **어느 하나가 압도하지 않는다**.
        //
        // ── 그래서 쓰러지는 것은 대개 **출혈**이다(15.4대 ↔ 가슴 21.4대) ────────────
        //
        // 몸통이 34%면 코어로 눕히는 데 21대가 걸리므로 **혈액이 먼저 마른다**. 그게 나쁜
        // 배치는 아니다 — 켄시도 난전에서 쓰러지는 이유는 대개 실혈이고, 부위 파괴는 그
        // 뒤에 오래 두들겨 맞을 때 벌어진다. 다만 **무기가 이 축을 가른다**: 둔기는 혈액 계수가
        // 0.05뿐이라 **부위로 눕히고**(뼈 몽둥이의 *"죽이기보다 눕히기에 낫다"*가 수치로 참이다),
        // 베기는 0.25라 **피로 눕힌다**.
        //
        // 부위 쪽을 먼저 오게 하고 싶으면 **`slashBloodFactor`를 더 내린다**(0.25 → 0.18이면
        // 출혈 21.4대로 가슴과 나란해진다) — `HitWeight`를 도로 올리면 사지가 다시 안 상한다.
        //
        // **혈액은 강인함을 통과한 뒤의 피해에서 뺀다**(`actualDamage * bloodFactor`) — 그래서
        // 강인함이 높아도 부위와 혈액이 **같은 비율로** 늦춰진다(어느 한쪽만 질겨지지 않는다).
        _parts[BodyPartType.Head]     = new BodyPart(BodyPartType.Head,     100f, 0.5f, 10f);
        _parts[BodyPartType.Chest]    = new BodyPart(BodyPartType.Chest,    100f, 0.8f, 18f);
        _parts[BodyPartType.Abdomen]  = new BodyPart(BodyPartType.Abdomen,  100f, 0.4f, 16f);
        _parts[BodyPartType.LeftArm]  = new BodyPart(BodyPartType.LeftArm,  100f, 0.2f, 13f);
        _parts[BodyPartType.RightArm] = new BodyPart(BodyPartType.RightArm, 100f, 0.2f, 13f);
        _parts[BodyPartType.LeftLeg]  = new BodyPart(BodyPartType.LeftLeg,  100f, 0.2f, 15f);
        _parts[BodyPartType.RightLeg] = new BodyPart(BodyPartType.RightLeg, 100f, 0.2f, 15f);
    }

    void Update()
    {
        UpdateBleed();
        UpdateRegen();
        UpdateKnockOutRecovery();
    }

    // ── 피해 처리 ────────────────────────────────────────────────────
    public void TakeDamage(float damage) =>
        TakeDamage(new DamageInfo { Amount = damage, Type = DamageType.Blunt });

    public void TakeDamage(DamageInfo info)
    {
        float damage = info.Amount;
        var sfx = GetComponent<StatusEffectSystem>();

        // 가해자 기록 — 중립 세력도 선공당하면 반격/도망할 수 있게(관계와 무관한 축).
        // **같은 편 오사를 거르는 것은 `RecordAttacker`가 한다** — 부르는 곳이 둘이라
        // (여기와 `CombatHandler`의 공격 시도) 각자 적으면 한쪽이 반드시 빠진다.
        if (info.Source != null && _brain != null && _brain.Blackboard != null)
            _brain.Blackboard.RecordAttacker(info.Source);

        // 타입 배율
        if (info.Type == DamageType.Blunt)     damage *= 1.3f;
        if (info.Type == DamageType.Lightning) damage *= 1.2f;

        // **젖으면 전기가 더 아프다** — 물이 혼자서는 약한 감속기인데, 이 한 줄이 물을
        // *다음 한 방을 아프게 하는 원소*로 만든다. 원소끼리 얽히는 유일한 자리다.
        if (info.Type == DamageType.Lightning && sfx != null && sfx.IsWet)
            damage *= 1.5f;

        // **언데드에게 신성은 두 배.** 저항표가 아니라 여기서 묻는 이유: `UnitBrain.IsUndead`는
        // 해골 종족 **∥ 일으켜진 몸**을 함께 답하는데, 일어난 동료는 사람 종족이라
        // `UnitData.Resistances`에 적을 자리가 없다 — 표에만 적으면 **성물이 걔들에게 안 듣는다.**
        // 시체가 일어나는 게임에서 그건 신성을 넣은 뜻을 통째로 지운다.
        if (info.Type == DamageType.Holy && _brain != null && _brain.IsUndead)
            damage *= 2f;

        // 보호막 흡수 (타입 배율 이후, 갑옷 이전)
        if (sfx != null && sfx.ShieldPool > 0f)
        {
            damage = sfx.AbsorbShield(damage);
            if (damage <= 0f) return;
        }

        var partType = GetRandomBodyPart();

        // **막는 것이 갈린다: 물리는 갑옷 두께, 원소는 저항.**
        //
        // `Arcane`은 둘 다 지나간다 — 그게 비전의 일이고, 그 대신 계수가 낮다.
        // 저항표가 아무리 두꺼워도 뚫는 답이 하나는 있어야 한다.
        var  armorSlot = BodyPartToArmorSlot(partType);
        var  armor     = Equip?.GetArmor(armorSlot);
        bool ignoresDefense = info.Type == DamageType.Arcane;

        if (!ignoresDefense && !info.IgnoreArmor)
        {
            if (info.Type.IsPhysical())
            {
                // 관통은 두께를 지나간다(예전 `Pierce` 그대로).
                if (info.Type != DamageType.Pierce && armor != null)
                {
                    float qualMult = Equip?.GetArmorQualityMult(armorSlot) ?? 1f;
                    damage = Mathf.Max(0f, damage - armor.DamageReduction * qualMult);
                }
                // **신발도 다리를 막는다**(2026-10-18) — 몸에 발이 없어 다리 갑옷 위에 얹는다. 물리만.
                if (info.Type != DamageType.Pierce && armorSlot == ArmorSlot.Legs && Equip != null &&
                    Equip.GetArmor(ArmorSlot.Feet) is { } feet)
                    damage = Mathf.Max(0f, damage - feet.DamageReduction * Equip.GetArmorQualityMult(ArmorSlot.Feet));
            }
            else
            {
                damage *= ResistanceMult(info.Type, armor);
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
        float bloodFactor = info.Type switch
        {
            DamageType.Slash => slashBloodFactor,
            DamageType.Blunt => bluntBloodFactor,
            _                => otherBloodFactor,
        };
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

        // **원소마다 하는 일이 하나씩이다.** 겹치면 색만 다른 피해가 된다.
        switch (info.Type)
        {
            case DamageType.Fire:
                sfx.ApplyEffect(StatusEffectType.Burning, 5f, 5f);
                sfx.DryOff();   // 불이 물을 말린다 — 안 그러면 불·전기가 둘 다 잘 드는 공짜 조합
                break;

            case DamageType.Water:
                sfx.ApplyEffect(StatusEffectType.Wet, 8f, 0.4f);
                break;

            case DamageType.Earth:
                // **켄시식 KO 체계에 직접 얹힌다** — 땅의 일은 피해가 아니라 *넘어뜨리는 것*이다.
                TryKnockOut(0.35f);
                break;

            case DamageType.Lightning:
                sfx.ApplyEffect(StatusEffectType.Slowed, 8f, 0.5f);
                break;

            // 비전은 상태이상이 없다 — 갑옷·저항을 지나가는 것이 그 일이다.
            case DamageType.Arcane:
                break;

            case DamageType.Dark:
                sfx.ApplyEffect(StatusEffectType.HealReduction, 10f, 0.5f);
                sfx.AddCorruption(info.Amount * 0.3f);
                break;

            // 신성은 언데드 특효가 그 일(위 타입 배율). 산 사람에게는 그냥 피해다.
            case DamageType.Holy:
                break;
        }
    }

    // **저항은 종족 + 갑옷이다.** 답을 한 곳에 둔다 — 부르는 데가 늘 때 한쪽만 세면
    // "갑옷을 벗었는데 저항이 그대로"가 된다.
    float ResistanceMult(DamageType type, ArmorData armor)
    {
        float mult = 1f;

        var race = _brain != null ? _brain.Data : null;
        if (race != null) mult *= race.Resistances.Multiplier(type);
        if (armor != null) mult *= armor.Resistances.Multiplier(type);

        return mult;
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
    // `bonus`는 **넘어뜨리는 쪽의 보정**(땅 원소가 0.35를 넣는다). 저항에서 곧장 뺀다 —
    // 강인함을 깎는 식으로 넣으면 같은 값이 다른 판정(사망 문턱 등)에도 새어 나간다.
    void TryKnockOut(float bonus = 0f)
    {
        float toughness   = _brain.Stats.Toughness;
        // 스탯 범위가 0~130이라 상한 없이는 100+에서 완전 면역 — 최대 90%까지만 저항
        float resistChance = Mathf.Clamp01(Mathf.Min(0.9f, toughness / 100f) - bonus);

        if (UnityEngine.Random.value > resistChance)
            KnockOut();
        else
            Debug.Log($"[BodyPart] {gameObject.name} 기절 저항 (Toughness: {toughness:F1})");
    }

    void KnockOut()
    {
        // **언데드는 쓰러지지 않고 부서진다.** 기절은 *의식을 잃는 것*이라 해골에게도,
        // 이미 한 번 죽어 일어난 몸에게도 성립하지 않는다(`UnitBrain.IsUndead`).
        //
        // **갈리는 자리를 여기 하나로 둔다.** 기절로 오는 길이 셋인데(혈액 임계 ·
        // 코어 부위 깊이 · 강인함 굴림) 각자 앞에서 물으면 하나가 반드시 빠져 *가끔*
        // 기절하는 해골이 남는다. 대신 여기서 갈리므로 **언데드는 혈액 0에서 부서진다** —
        // 사람이 −30까지 버티는 그 구간이 통째로 없다.
        if (_brain != null && _brain.IsUndead) { Die(); return; }

        if (_brain.Blackboard.IsKnockedOut) return;
        _brain.Blackboard.IsKnockedOut = true;
        _koTimer = 0f;

        // BT가 정지하므로 전투/이동은 여기서 직접 꺼야 함 (기절 중 공격 지속 방지)
        if (TryGetComponent(out CombatHandler combat)) combat.ExitCombat();
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

    // 붙들려 있었나 — **놓이는 순간**을 잡는다(한 프레임짜리 사건이라 상태로 기억해야 한다).
    bool _wasHeld;

    // 몸이 깨어날 준비가 됐나 — **시간을 빼고** 피와 코어만 본다. 두 곳이 묻는다(놓이는 순간·
    // 시계가 다 찼을 때)라 답이 한 벌이어야 한다.
    //
    // `BloodCap`이 기준 밑까지 깎인 몸은 **캡까지 찼으면** 깨어난다 — 기절 중엔 침대를 못 가서
    // 그 위로는 영영 안 차기 때문이다.
    bool CanWakeNow =>
        (Blood >= maxBlood * wakeBloodRatio || Blood >= BloodCap)
        && !_parts[BodyPartType.Head].IsDisabled
        && !_parts[BodyPartType.Chest].IsDisabled
        && !_parts[BodyPartType.Abdomen].IsDisabled;

    void UpdateKnockOutRecovery()
    {
        if (!_brain.Blackboard.IsKnockedOut) return;

        // **붙들려 있는 동안은 회복이 아니다**(2026-09-11) — 어깨에 메였거나 도축대 상판에
        // 묶인 몸은 쉬는 중이 아니다. **시계를 멈춘다**(넘어가게 두면 그 시간이 회복으로
        // 쳐져, 놓는 순간 이미 다 찬 시계가 곧바로 깨운다).
        //
        // 없을 때 실제로 이랬다: 때려눕힌 짐승을 도축대까지 업고 가는 데 십수 초가 걸리고,
        // 상판에 눕혀 죽이는 데 7초가 더 걸린다 — 최소 기절 20초가 **그 사이에 지나가서**
        // 사냥한 것이 도마 위에서 깨어나 걸어 나갔다(*"눕히는 순간 적 폰이 일어나"*).
        // `ButcherJob`은 깨어난 것을 보고 *"가는 사이에 깨어났으면 그만둔다"*는 규칙대로
        // 물러났으므로, **두 규칙이 각자 옳은데 겹쳐서 사냥이 성립하지 않았다.**
        //
        // **침대는 안 걸린다** — 눕혀 두는 것이 곧 회복이라 시계가 가야 한다. 둘 다
        // `SetSleeping`을 쓰므로 **자세로는 구별할 수 없어** 붙드는 쪽이 적어 준다.
        if (_brain.Blackboard.CarriedBy != null || _brain.Blackboard.Restrained)
        {
            _wasHeld = true;
            return;
        }

        // **놓이면 곧바로 깨어난다 — 몸이 나았다면**(2026-09-12, 사용자 결정).
        //
        // 붙들린 동안 시계를 멈춘 것만으로는 **놓은 뒤가 너무 느렸다**: 쓰러뜨리자마자 업었으면
        // 시계가 2초에서 멈춰 있어 내려놓고도 18초를 더 서 있었다. 20초라는 바닥은 *맞자마자
        // 벌떡 일어나지 않는다*는 뜻이지 **놓인 뒤에 그만큼 더 누워 있으라는 뜻이 아니다** —
        // 업혀 다니는 동안 정신을 차릴 시간은 이미 지났다. 그래서 **시계를 통째로 없앤다.**
        //
        // **몸이 이미 나았을 때만이다** — 피가 모자라거나 코어가 망가진 몸은 제 시계를 그대로
        // 쓴다(그건 *붙들려서 못 일어나는 것*이 아니라 *아직 못 일어나는 것*이다). 그래서
        // 놓아 주는 쪽에는 손잡이가 하나 남는다: **덜 나은 채로 놓으면 그만큼 더 누워 있는다.**
        if (_wasHeld)
        {
            _wasHeld = false;
            if (CanWakeNow) _koTimer = minKnockOutSeconds;   // 아래에서 이번 틱에 깨어난다
        }

        _koTimer += Time.deltaTime;
        if (_koTimer < minKnockOutSeconds) return;
        if (!CanWakeNow) return;

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
        // **두 번 죽지 않는다.** `UnitBrain.Die`는 스스로 막지만 `OnDeath`는 안 막혀서,
        // 한 번의 피해가 `CheckBloodState`를 여러 번 거치면(독 번짐 등) 구독자가 죽음을
        // 두 번 듣는다. 언데드가 기절 대신 이 문으로 오게 되면서 그 길이 짧아졌다.
        if (_brain == null || _brain.IsDead) return;

        OnDeath?.Invoke(this);
        _brain.Die();
    }

    // ── 혈액 ────────────────────────────────────────────────────────

    // **지금 초당 얼마나 흘리나 — 한 벌**(2026-09-19). 흘리는 쪽(`UpdateBleed`)과 화면(`PawnSheetUI`의 *출혈*)이 같이 묻는다.
    // **`BodyPart.BleedRate`는 "지금 피가 나는가"가 아니다** — 그 부위가 다쳤을 때 흘리는 **빠르기(상수)**라 멀쩡한
    // 몸에서도 0보다 크다. 그걸로 물었더니 상태표가 **늘 *출혈***이었다(사용자: *"상태가 계속 출혈이야"*).
    // 실제로 흘리는 것은 **망가진 부위가 · 맞은 지 `clotSeconds` 안에 · 산 몸일 때**뿐이다.
    public float CurrentBleed
    {
        get
        {
            // **산 몸만 흘린다**(아래 `UpdateBleed` 참고).
            if (_brain != null && Needs.BodyOf(_brain) != BodyKind.Living) return 0f;

            // 자연 지혈: 부상 직후 BleedRate로 시작해 clotSeconds에 걸쳐 선형으로 멎는다.
            // 재피격 시 LastWoundTime이 리셋돼 다시 출혈 — 연타당할 때만 출혈이 위험해진다.
            float total = 0f;
            foreach (var part in _parts.Values)
            {
                if (!part.IsDisabled) continue;
                float clot = clotSeconds > 0f ? 1f - (Time.time - part.LastWoundTime) / clotSeconds : 0f;
                if (clot > 0f) total += part.BleedRate * clot;
            }
            return total;
        }
    }

    void UpdateBleed()
    {
        // **산 몸만 흘린다**(2026-09-08). 지속 출혈은 *상처가 시간에 걸쳐 피를 흘리는 것*이라
        // 뼈에도 돌덩이에도 성립하지 않는다 — 그런데 부위가 부서지면 종류를 안 가리고 돌아서,
        // **가만히 서 있는 해골이 저 혼자 무너졌다.**
        //
        // 없앤 것은 *시간에 걸친 감소*뿐이다: 맞으면 여전히 줄고(`Blood -= actualDamage`),
        // 0에서 부서지는 것도 그대로다. **언데드는 맞은 만큼만 무너진다.**
        float totalBleed = CurrentBleed;
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

    // **한 회차(`Treat(partHeal, capHeal)`) 중 얼마가 필요한가**(0~1, 2026-10-16) — 약을 치료한 만큼만 쓰려고
    // (`Treatment.Dose`). 가장 많이 빈 곳이 답한다: 부위 하나 · 혈액 상한 · 혈액. 회차가 그 셋을 한꺼번에 채우므로
    // 셋 중 가장 큰 몫만큼 쓰면 전부 찬다.
    public float TreatNeed(float partHeal, float capHeal)
    {
        float need = 0f;
        if (partHeal > 0f)
        {
            foreach (var part in _parts.Values)
                if (!part.IsDestroyed) need = Mathf.Max(need, (part.MaxHP - part.CurrentHP) / partHeal);
            need = Mathf.Max(need, (BloodCap - Blood) / partHeal);
        }
        if (capHeal > 0f) need = Mathf.Max(need, (maxBlood - BloodCap) / capHeal);
        return Mathf.Clamp01(need);
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

    // ── 일어난다 ─────────────────────────────────────────────────────────────
    //
    // **`BecomeCorpse`의 짝**(`UnitBrain.Rise`가 부른다). 죽음이 남긴 것을 전부 되돌린다 —
    // 혈액은 음수(사망 비율 −30)에 있고, 부위는 망가졌거나 **파괴**돼 있으며, 기절 자세로
    // 누워 있다. 하나라도 남기면 일어나자마자 다시 쓰러진다.
    //
    // **파괴된 부위도 되돌린다.** `HealBodyParts`는 그것을 건너뛰는데(의족은 후유증 축의
    // 일이다) 여기서 건너뛰면 머리가 터져 죽은 몸은 일어나는 즉시 기절해 선 자리에서 다시
    // 눕는다 — 일으키는 쪽은 치료가 아니라 죽음을 되돌리는 일이다.
    //
    // **자세는 `forward`로 세운다.** 기절 자세가 **로컬 z축**(=정면) 둘레의 90° 회전이라
    // 누워 있어도 정면은 수평 그대로다 — `_preKoRotation`을 쓰면 기절을 거치지 않고 죽은
    // 몸에서 그 값이 비어 있다(`default(Quaternion)`는 회전이 아니다).
    public void Rise(float bloodRatio = 1f)
    {
        foreach (var part in _parts.Values)
        {
            part.CurrentHP     = part.MaxHP;
            part.LastWoundTime = Time.time - clotSeconds;   // 출혈 흔적도 지운다
        }

        BloodCap = maxBlood;
        Blood    = Mathf.Clamp(maxBlood * Mathf.Clamp01(bloodRatio), 1f, maxBlood);

        _koTimer = 0f;
        _brain.Blackboard.IsKnockedOut = false;

        var fwd = transform.forward;
        fwd.y = 0f;
        transform.rotation = fwd.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(fwd)
            : Quaternion.identity;
        _preKoRotation = transform.rotation;

        // 부위가 다 찼으므로 팔다리 기능도 다시 본다 — 안 부르면 다리가 성한데도 못 걷는다.
        UpdateMoveAbility();
        UpdateAttackAbility();
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

    static ArmorSlot BodyPartToArmorSlot(BodyPartType type) => type switch
    {
        BodyPartType.Head                                  => ArmorSlot.Head,
        BodyPartType.Chest or BodyPartType.Abdomen         => ArmorSlot.Chest,
        BodyPartType.LeftArm or BodyPartType.RightArm      => ArmorSlot.Hands,
        BodyPartType.LeftLeg or BodyPartType.RightLeg      => ArmorSlot.Legs,
        _                                                   => ArmorSlot.Chest,
    };
}
