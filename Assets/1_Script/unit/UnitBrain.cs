using System;
using System.Collections.Generic;
using UnityEngine;

public partial class UnitBrain : MonoBehaviour
{
    public static event Action<UnitBrain> OnUnitSpawned;
    public static event Action<UnitBrain> OnUnitDied;
    public event Action<float> OnDamaged;

    [Header("틱 주기")]
    [SerializeField] float tickInterval = 0.1f;

    [Header("유닛 데이터")]
    [Tooltip("**종족** — 스탯 범위·이름 풀·기본 능력치. 무엇을 하는 사람인지는 여기 없다.")]
    [SerializeField] UnitData unitData;

    [Tooltip("**역할**(선택) — 주민·경비·상인. 채우면 NPC 트리를 탄다. " +
             "종족과 따로 두므로 인간 상인·엘프 상인이 **같은 역할 에셋**을 쓴다.")]
    [SerializeField] NpcData npcData;

    [Tooltip("AI 프로필 — 도주/교전 성향과 유휴 배회 설정. 비우면 타입별 기본값 " +
             "(일반·경비 = 교전+배회 / NPC 주민 = 도주+배회 / 상점 상인 = 도주+제자리).")]
    [SerializeField] AiProfileData aiProfile;

    [Tooltip("**지니는 것**(선택) — 장비·소지품·주머니. 종족과 따로 두므로 인간 산적과 " +
             "인간 마을 사람이 **같은 `Human`을 쓰면서 다른 것을 지닌다**.\n" +
             "종족이 든 것(`UnitData.Loot` = 몸에서 나오는 가죽·이빨)과 **겹치지 않고 더해진다** " +
             "— 다른 물음에 답하기 때문이다.")]
    [SerializeField] LoadoutData loadout;

    // ── 고정 개체 (선택) ──────────────────────────────────────────────────────
    // **이름 있는 동료는 매번 다른 사람이면 안 된다.** 기본값은 `Awake`가 이름도 스탯도
    // 제 마음대로 굴리는 것이라, 같은 프리팹을 두 번 찍으면 다른 사람이 나온다.
    [Header("고정 개체 (선택)")]
    [Tooltip("이름만 못 박는다. 스탯·생김새까지 정하려면 아래 `pawnSpec`을 쓸 것.")]
    [SerializeField] string fixedName;

    [Tooltip("저작한 개체 명세 — 스탯을 **원하는 등급으로 콕 집어** 정하고 전용 생김새도 준다. " +
             "채우면 위 `fixedName`보다 이쪽이 이긴다.")]
    [SerializeField] PawnSpecData pawnSpec;

    PawnMover _mover;
    UnitBlackboard _bb;
    BTNode _root;
    float _tickTimer;
    bool _isDead;

    // 신체 부위 시스템(있으면 혈액/부위가 진짜 체력). 없는 유닛은 아래 폴백 HP 사용.
    BodyPartSystem _body;

    // BodyPartSystem 없는 유닛을 위한 폴백 HP
    float _hp;
    float _maxHp;

    public UnitBlackboard Blackboard => _bb;
    public UnitData Data => unitData;

    // 이 폰의 **역할**(없으면 null = 역할 없는 전투/노동 유닛).
    public NpcData Npc => npcData;
    public AiProfileData AiProfile => aiProfile;

    // ── 전투 성향 (런타임 조정 가능) ─────────────────────────────────────
    // NPC/적은 프로필 에셋으로 고정 운용하지만, 플레이어 유닛은 상황에 따라 바꾸고 싶다.
    // 우선순위: 런타임 오버라이드 > AI 프로필 > 역할 기본값(트리 조립 시 지정).
    CombatStance? _stanceOverride;
    CombatStance  _roleStanceFallback = CombatStance.Fight;

    public CombatStance EffectiveStance =>
        _stanceOverride ?? (aiProfile != null ? aiProfile.Stance : _roleStanceFallback);

    public bool HasStanceOverride => _stanceOverride.HasValue;

    // 이 유닛만 성향을 바꾼다(프로필 에셋은 건드리지 않음). BT 재조립 불필요 — 매 틱 읽는다.
    public void SetStanceOverride(CombatStance stance) => _stanceOverride = stance;
    public void ClearStanceOverride()                  => _stanceOverride = null;

    // 지금 도망쳐야 하는가 — 성향과 현재 상태로 매 틱 판단.
    public bool ShouldFleeNow()
    {
        switch (EffectiveStance)
        {
            case CombatStance.Flee:
                return true;
            case CombatStance.FightWhileHealthy:
                return IsBloodBelow(aiProfile != null ? aiProfile.FleeBloodRatio : 0.4f);
            default:
                return false;
        }
    }
    public string UnitName { get; private set; }

    // 이름을 밖에서 정해 준다. 기본은 `Awake`가 `NamePool`에서 아무거나 고르는 것인데,
    // **미리 이름을 보여 주고 데려오는 경우**(마을 고용)엔 메뉴에서 본 사람과 합류한 사람이
    // 달라지면 안 된다. 빈 이름은 무시한다 — 지우는 길은 만들지 않는다.
    public void Rename(string name)
    {
        if (!string.IsNullOrWhiteSpace(name)) UnitName = name;
    }

    // 태어나기 전에 적어 둔 명세를 입힌다(`PawnSpec`).
    //
    // `Awake`는 이름과 스탯을 제 마음대로 굴린다 — 그게 기본값이다. 하지만 **보고 나서 데려오는
    // 경우**(마을 고용)엔 메뉴에 뜬 사람과 합류한 사람이 같아야 하므로 그 결과를 덮는다.
    // 나중에 외모·몸 크기를 사람이 정하게 되어도 **이 통로 하나**로 들어온다.
    public PawnSpec Spec { get; private set; }

    public void ApplySpec(PawnSpec spec)
    {
        if (spec == null) return;
        Spec = spec;

        Rename(spec.Name);

        if (Stats != null)
            foreach (var kv in spec.Grades)
                Stats.SetStat(kv.Key, kv.Value.ToValue());

        // **특성은 스탯을 덮어쓴 뒤에 얹는다.** 순서가 뒤집히면 명세의 등급이 특성 보정을
        // 지워 버려, 특성이 붙어 있는데 아무 효과도 없는 폰이 된다.
        SetTraits(spec.Traits);

        // 생김새 — 크기 · 재질 · 전용 모델. 늘어나면 여기에 줄이 는다.
        if (spec.BodyScale > 0f) transform.localScale = Vector3.one * spec.BodyScale;
        ApplyLook(spec);
    }

    // ── 특성 ──────────────────────────────────────────────────────────────────
    // **이 폰이 어떤 사람인가.** 스탯에 숫자만 얹으므로(모델 A) 전투·작업 코드는 특성을 모른다.
    readonly List<TraitData> _traits = new();

    public IReadOnlyList<TraitData> Traits => _traits;

    public bool HasTrait(TraitData trait) => trait != null && _traits.Contains(trait);

    // 명세가 들고 온 특성으로 **갈아 끼운다**(스폰 시 한 번). 이미 얹은 것이 있으면 되돌린다 —
    // 안 그러면 `ApplySpec`이 두 번 불릴 때 보정이 두 겹으로 쌓인다.
    void SetTraits(IEnumerable<TraitData> traits)
    {
        foreach (var t in _traits) TraitPool.Remove(t, Stats);
        _traits.Clear();

        if (traits == null) return;
        foreach (var t in traits) AddTrait(t);
    }

    // **나중에 붙일 수도 있다** — 부상 후 트라우마, 사건으로 얻는 별명 같은 것들의 진입점이다.
    // 기본은 스폰 고정이지만 통로를 막아 두면 나중에 그 기능이 `ApplySpec`을 다시 뜯게 된다.
    public bool AddTrait(TraitData trait)
    {
        if (trait == null || _traits.Contains(trait)) return false;

        // 배타 묶음이 겹치면 안 붙인다 — `용감`한 폰이 `겁쟁이`가 되려면 먼저 떼야 한다.
        if (!string.IsNullOrWhiteSpace(trait.ExclusiveGroup))
            foreach (var t in _traits)
                if (t != null && t.ExclusiveGroup == trait.ExclusiveGroup) return false;

        _traits.Add(trait);
        TraitPool.Apply(trait, Stats);
        return true;
    }

    public bool RemoveTrait(TraitData trait)
    {
        if (!_traits.Remove(trait)) return false;
        TraitPool.Remove(trait, Stats);
        return true;
    }

    // **저작한 모습을 입힌다.** 굴려서는 안 나오고 명세에 적어야만 붙는다 —
    // 이름 있는 동료가 남들과 똑같이 생기면 이름을 붙인 뜻이 절반 사라진다.
    void ApplyLook(PawnSpec spec)
    {
        // 전용 모델이 있으면 **기본 렌더러를 끄고** 자식으로 붙인다. 지우지 않고 끄는 이유는
        // 초상화(`PortraitCapture`)·선택 표식처럼 렌더러를 훑는 것들이 이미 있어서,
        // 구조를 바꾸는 것보다 보이는 것만 바꾸는 편이 파장이 작기 때문이다.
        if (spec.ModelOverride != null)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = false;

            var model = Instantiate(spec.ModelOverride, transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // 붙인 모델의 콜라이더는 뗀다 — 폰의 `CharacterController`와 겹쳐
            // 자기 자신에 걸리거나 클릭 판정이 둘로 갈린다(`DroppedItem`과 같은 처리).
            foreach (var c in model.GetComponentsInChildren<Collider>(true))
                Destroy(c);
            return;
        }

        if (spec.MaterialOverride == null) return;

        // 재질만 갈아 끼우는 경우. `sharedMaterial`을 쓴다 — `material`은 개체마다 사본을
        // 만들어 배칭이 깨지고 누수가 된다(`BuildingDormancy` 틴트와 같은 이유).
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.sharedMaterial = spec.MaterialOverride;
    }

    Sprite _portrait;
    bool   _portraitTried;

    // 로스터 등에서 표시 직전 호출. 최초 1회만 3D 모델을 렌더해 캐싱.
    public Sprite EnsurePortrait()
    {
        if (!_portraitTried)
        {
            _portraitTried = true;
            _portrait = PortraitCapture.Capture(gameObject);
        }
        return _portrait;
    }
    public UnitStats Stats { get; private set; }
    public Inventory Inventory { get; private set; }
    public Equipment Equipment { get; private set; }
    public WorkSettings Work { get; private set; }

    void Awake()
    {
        _mover    = GetComponent<PawnMover>();
        _body     = GetComponent<BodyPartSystem>();
        Inventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        Equipment = GetComponent<Equipment>() ?? gameObject.AddComponent<Equipment>();
        gameObject.AddComponent<StatusEffectSystem>();

        Stats = UnitStats.FromData(unitData);
        // 성장이 잠기는 건 **내가 소환한** 하수인이다. 같은 종족 에셋을 쓰는 야생 개체까지
        // 잠그면 "고블린은 성장하지 않는다"가 되는데, 그건 종족의 성질이 아니다.
        Stats.IsLocked = unitData is MinionData && !UsesEnemyAi;
        Stats.Owner    = gameObject;

        _maxHp = unitData != null ? unitData.MaxHP : 100f;
        _hp    = _maxHp;

        _bb = new UnitBlackboard
        {
            Self         = gameObject,
            Mover        = _mover,
            DetectRange  = unitData != null ? unitData.DetectRange  : 10f,
            AttackRange  = unitData != null ? unitData.AttackRange  : 1.5f,
            AttackDamage = unitData != null ? unitData.AttackDamage : 10f,
            Hunger       = 100f,
        };

        if (unitData != null)
        {
            _mover.Init(unitData);
            _mover.SetStats(Stats, Inventory);
        }

        UnitName = ResolveUnitName();
        ApplyAuthoredSpec();

        _root = BuildTree();

        // 작업 설정은 플레이어 세력 일꾼(방랑자·하수인)만 갖는다. 적/NPC는 작업 큐를 쓰지 않는다.
        // 하수인은 MinionData.AllowedJobs를 초기값으로 받되, 이후 조정은 개체별로 적용된다.
        if (!UsesEnemyAi && npcData == null)
        {
            Work = gameObject.AddComponent<WorkSettings>();
            if (unitData is MinionData minionData)
                Work.InitFromAllowed(minionData.AllowedJobs);
        }

        HookUnloadSignals();
    }

    void OnDestroy() => UnhookUnloadSignals();

    // 인스펙터에 적어 둔 고정값을 입힌다. **밖에서 명세를 주는 경우**(마을 고용)는
    // 스폰 직후 `ApplySpec`이 이 위에 덮으므로 둘이 다투지 않는다 — 저작은 기본값이고
    // 주입이 이긴다(상인 상품과 같은 우선순위).
    void ApplyAuthoredSpec()
    {
        if (pawnSpec == null)
        {
            Rename(fixedName);   // 이름만 못 박는 경우(빈 문자열은 `Rename`이 무시한다)

            // **명세가 없는 폰도 특성을 받는다.** 방랑자든 하수인이든 적이든 마찬가지다 —
            // 특성이 일부에게만 있으면 "이 고블린은 왜 안 쓰러지지"에 답이 없어진다.
            // 명세가 있는 폰은 `ApplySpec`이 시드에서 뽑은 것을 쓴다(메뉴에서 본 그대로).
            SetTraits(TraitPool.RollForPawn(
                new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue))));
            return;
        }

        var spec = pawnSpec.Build(unitData);
        if (!string.IsNullOrWhiteSpace(fixedName)) spec.Name = fixedName;
        ApplySpec(spec);
    }

    string ResolveUnitName()
    {
        if (unitData != null && unitData.NamePool != null && unitData.NamePool.Count > 0)
            return unitData.NamePool[UnityEngine.Random.Range(0, unitData.NamePool.Count)];
        if (unitData != null && !string.IsNullOrEmpty(unitData.RaceName))
            return unitData.RaceName;
        return gameObject.name;
    }

    void Start()
    {
        // **알리기 전에 입힌다.** 구독자(로스터·UI·`WorldBattle` 전력 계산)가 장비를 읽으므로,
        // 알린 뒤에 채우면 첫 계산이 맨몸 기준으로 나온다.
        ApplyLoadout();

        OnUnitSpawned?.Invoke(this);
    }

    void Update()
    {
        // 허기는 기절 중에도 진행 — 쓰러진 채 굶으면 혈액이 계속 빠져 결국 아사(BT 정지와 무관).
        UpdateHunger();

        if (_bb.IsKnockedOut) return;

        _tickTimer += Time.deltaTime;
        if (_tickTimer < tickInterval) return;
        _bb.TickDelta = _tickTimer;   // 노드가 쓸 실제 경과 시간 (한 프레임이 아니라 틱 간격)
        _tickTimer = 0f;

        UpdateUnload();

        _bb.RestTicked = false;
        _root.Tick(_bb);

        // 전투·소집 등 상위 분기가 휴식을 밀어냈는데 폰이 아직 누워 있으면 일으켜 세운다.
        // (BT 노드에는 중단 콜백이 없어 RestNode가 스스로 정리할 기회를 못 얻는다.)
        if (!_bb.RestTicked && _mover != null && _mover.IsSleeping)
            _mover.SetSleeping(false);
    }

    // ── 짐 내리기 ────────────────────────────────────────────────────────────
    // **배낭은 잠시 들고 있는 자리지 창고가 아니다.** 상인에게 산 재료가 배낭에 갇히면
    // 제작 화면(창고 기준)에는 안 보여서 "분명히 샀는데 못 만든다"가 된다.
    //
    // 일감을 폰이 **스스로** 등록한다 — 짐이 그 폰의 배낭에 있으니 남이 대신 못 한다.
    // 큐에는 올라가되 `UnloadJob.CanAssignTo`가 주인만 통과시킨다.
    // **주기로 훑지 않는다 — 바뀌었을 때만 본다.** 배낭이 그대로면 답도 그대로다.
    // 다시 봐야 하는 순간은 셋뿐이다: 배낭이 바뀌었을 때(`Inventory.OnChanged`),
    // 창고가 바뀌었을 때(자리가 없어 못 걸었던 것이 이제 걸릴 수 있다),
    // 작업 설정이 바뀌었을 때(방금 `짐내림`을 켰다).
    bool      _unloadDirty = true;   // 처음 한 번은 본다
    UnloadJob _unloadJob;

    void MarkUnloadDirty() => _unloadDirty = true;

    // **작업 설정이 없는 폰은 구독하지 않는다.** 적·NPC는 `Work`가 아예 안 붙으므로
    // (`Awake`의 `!UsesEnemyAi && npcData == null` 관문) 짐을 내릴 일이 없다 —
    // 그런데도 전부 `Storage.OnChanged`에 매달려 있으면 창고가 바뀔 때마다 습격 온 적
    // 스무 마리까지 깨운다. 짐내림 기본값을 켜 둘 수 있는 것도 이 관문 덕이다.
    void HookUnloadSignals()
    {
        if (Work == null) return;

        if (Inventory != null) Inventory.OnChanged += MarkUnloadDirty;
        Work.OnChanged    += MarkUnloadDirty;
        Storage.OnChanged += MarkUnloadDirty;
    }

    void UnhookUnloadSignals()
    {
        if (Work == null) return;

        if (Inventory != null) Inventory.OnChanged -= MarkUnloadDirty;
        Work.OnChanged    -= MarkUnloadDirty;
        Storage.OnChanged -= MarkUnloadDirty;
    }

    // **무엇을 남길지는 종류가 아니라 플레이어가 정한다.** 한때 무기·갑옷·식량을 종류로
    // 뺐는데, 그 규칙은 반드시 어긋난다 — 만들어 쌓아 둔 검은 창고로 가야 하고 쥐어 준
    // 예비 검은 남아야 하는데, 둘 다 `WeaponData`다.
    //
    // 그래서 **뭉치 단위 잠금**(`ItemInstance.Locked`)으로 바꿨다. 배낭 창의 [잠금]을 켜고
    // 아이템을 누르면 그 뭉치만 자동 운반에서 빠진다. 손으로 하는 일은 잠금과 무관하다.
    void UpdateUnload()
    {
        if (Inventory == null || JobManager.Instance == null) return;

        // 쓸모를 잃은 일감은 **치운다.** 아직 아무도 안 집었는데(`Available`) 조건이 깨졌으면
        // (짐을 이미 썼다·창고가 찼다·짐내림을 껐다) 그냥 두면 배정되지 않는 일감이 큐에
        // 영원히 남고, 아래 "살아 있으면 그만" 관문에 걸려 **이 폰은 다시는 짐을 못 내린다.**
        if (_unloadJob != null)
        {
            bool alive = _unloadJob.Status != JobStatus.Done &&
                         _unloadJob.Status != JobStatus.Cancelled;
            bool stale = _unloadJob.Status == JobStatus.Available &&
                         (!_unloadJob.IsReady || Work == null || !Work.IsEnabled(JobCategory.Unload));

            if (alive && stale)
            {
                JobManager.Instance.CancelJob(_unloadJob);
                _unloadJob = null;
            }
            else if (alive) return;   // 살아 있고 쓸모도 있다 — 겹쳐 걸지 않는다
        }

        if (Work == null || !Work.IsEnabled(JobCategory.Unload)) return;

        // **일하는 중에는 짐을 안 내린다.** 운반 중인 폰의 배낭에는 지금 나르는 물건이 들어
        // 있는데, 그걸 내려놓을 일감을 또 걸면 같은 짐을 두 일감이 다툰다.
        if (_bb.CurrentJob != null) return;

        // **바쁜 동안에는 표시를 지우지 않는다** — 위 관문들보다 뒤에서 내려야
        // 일하느라 못 본 변경이 사라지지 않는다.
        if (!_unloadDirty) return;
        _unloadDirty = false;

        // 받아 줄 창고가 **자리까지 있는** 첫 아이템 하나만 맡는다. 한 번에 한 종류씩이라
        // 오가는 동안 상황이 바뀌어도 어긋난 만큼만 다시 잰다.
        foreach (var inst in Inventory.Items)
        {
            var item = inst.Data;
            if (item == null || inst.Locked) continue;

            // **아이템 하나가 아니라 (아이템, 등급) 하나를 맡는다.** 등급을 안 가리면
            // 희귀 셋과 일반 다섯이 여덟 개 한 덩어리가 되고, 그걸 안 가리고 내려놓으면
            // 희귀가 창고에서 일반이 된다(`HaulJob`과 같은 규칙).
            // 잠긴 뭉치는 수량에서도 뺀다 — 전체를 세어 걸면 도착해서 잠긴 것만큼 어긋난다.
            var quality  = inst.Quality;
            int amount   = Inventory.GetUnlockedQuantity(item, quality);
            if (amount <= 0) continue;

            var storage  = Storage.FindNearest(transform.position, item, amount, quality);
            if (storage == null) continue;

            _unloadJob = new UnloadJob(this, item, quality, amount, storage);
            JobManager.Instance.RegisterJob(_unloadJob);
            return;
        }
    }

    // 하루(1800초)에 두 끼. 100 → 0 이 900초이므로 식사 임계(60)까지는 360초 걸린다.
    // 하루 길이를 바꾸면 여기도 같이 바꿔야 한다 — 기준은 `GameTimeManager.dayDuration`.
    const float BaseHungerDecayRate = 0.111f; // 초당 감소량 (100 기준 900초에 고갈 = 하루 2끼)
    const float StarveBloodPerSec   = 2f;     // 허기 0 시 초당 혈액 손실 (아사)

    void UpdateHunger()
    {
        // 플레이어 세력(방랑자/하수인)만 허기 적용
        var faction = GetComponent<Faction>();
        if (faction == null || !faction.IsPlayer) return;

        float rate = BaseHungerDecayRate;

        // DungeonCore의 HungerRateModifier 반영
        var core = DungeonCore.Instance;
        if (core != null && core.Data != null)
            rate *= Mathf.Max(0f, 1f + core.Data.HungerRateModifier);

        _bb.Hunger = Mathf.Max(0f, _bb.Hunger - rate * Time.deltaTime);

        if (_bb.Hunger <= 0f)
        {
            // 신체 부위 시스템이 있으면 혈액을 깎아(전신 쇠약) UI에 보이는 혈액 파이프라인으로
            // 기절→사망시킨다. 없는 유닛만 숨은 폴백 HP를 깎는다.
            if (_body != null) _body.Starve(StarveBloodPerSec * Time.deltaTime);
            else               TakeDamage(StarveBloodPerSec * Time.deltaTime);
        }
    }

    // BodyPartSystem 없는 유닛용 폴백
    public void TakeDamage(DamageInfo info) => TakeDamage(info.Amount);

    public void TakeDamage(float damage)
    {
        _hp = Mathf.Max(0f, _hp - damage);
        OnDamaged?.Invoke(damage);
        if (_hp <= 0f) Die();
    }

    public void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // 진행 중인 작업이 있으면 취소해서 pending 플래그 해제 + 노드 배정 정리
        AbandonWork();

        OnUnitDied?.Invoke(this);

        // **플레이어 세력이 아니면 시체로 남는다.**
        // 예전엔 `UsesEnemyAi`로 갈랐는데 그건 **AI 역할**(어떻게 싸우나)을 묻는 축이라
        // NPC·하수인을 통째로 제외했다. 그래서 상인이나 마을 사람을 죽이면
        // 시체도 유품도 없이 사라졌다 — 이제 중립을 공격할 수 있으니 그건 맞지 않는다.
        // 약탈 가능 여부는 역할이 아니라 **세력**이 정해야 한다.
        var faction = GetComponent<Faction>();
        if (faction != null && !faction.IsPlayer)
        {
            BecomeCorpse();
            return;
        }

        Destroy(gameObject);
    }

    void BecomeCorpse()
    {
        DropCoins();
        DropLoot();

        // **소속을 먼저 챙긴다.** 아래에서 `Faction` 컴포넌트를 지우는데, 그건 자동 탐지를
        // 막으려는 것이지 "누구의 시체인지" 잊자는 뜻이 아니다.
        var corpseFaction = GetComponent<Faction>();

        var corpse = gameObject.AddComponent<Corpse>();
        corpse.Init(UnitName, Inventory, corpseFaction != null ? corpseFaction.Data : null);

        enabled = false;
        if (_mover != null) _mover.enabled = false;

        var combat = GetComponent<CombatHandler>();
        if (combat != null) combat.enabled = false;

        // **몸이 도는 것도 멈춘다.** `BodyPartSystem.Update`는 출혈·자연회복·기절회복을 돌리는데,
        // 시체에서 그게 계속 돌면 죽은 놈의 혈액이 눈앞에서 줄고(또는 도로 차고) 기절 회복
        // 타이머까지 굴러간다. `StatusEffectSystem`도 같다 — 시체가 계속 불타며 피해를 받는다.
        // `enabled=false`라 되살릴 일이 생기면 그대로 되돌릴 수 있다(휴면 건물과 같은 규칙).
        var body = GetComponent<BodyPartSystem>();
        if (body != null) body.enabled = false;

        var sfx = GetComponent<StatusEffectSystem>();
        if (sfx != null) sfx.enabled = false;

        // 팩션 제거 — 시체가 적으로 재탐지·재공격되는 것 방지.
        // (`enabled = false`로는 못 막는다 — `GetComponent`는 꺼진 것도 돌려준다.)
        // 소속 자체는 위에서 `Corpse`가 받아 갔다.
        if (corpseFaction != null) Destroy(corpseFaction);
    }

    // ── 지니는 것 ────────────────────────────────────────────────────────────
    //
    // **출처는 `LoadoutData` 하나뿐이다**(`loadout`). 종족(`UnitData`)에는 아이템이 없다.
    //
    // 한때 "몸에서 나오는 것(늑대 가죽)은 종족, 지니는 것(무기)은 역할"로 갈라 두 벌을 굴렸다.
    // 서사로는 맞는 구분인데 **기계적으로는 둘 다 "죽으면 나오는 아이템"**이라, "이 적이 뭘
    // 떨구나"에 답하려면 에셋 두 개를 열어 더해야 했다. 답이 두 벌이면 반드시 어긋난다 —
    // `CarryCapacity`·표시명·`MaterialPool`에서 이미 겪은 것과 같다.
    //
    // 늑대 가죽은 `Loadout_Wolf`에 적는다. 종족이 아니라 **그 프리팹이 무엇이냐**의 문제로 두면
    // 같은 `Goblin`을 쓰는 야생 도적과 내 하수인도 저절로 갈린다.

    // **주머니에 든 동전.** 시체를 뒤지면 나온다 — 그게 싸울 이유의 절반이다.
    //
    // 등급으로 자동 산출하는 길도 있었지만, 그러면 "강한 늑대가 금화를 물고 있다"가 되어
    // 저작으로 막을 방법이 없다.
    void DropCoins()
    {
        if (Inventory == null || loadout == null) return;

        var coin = Wallet.Coin;
        if (coin == null) return;

        GiveOrDrop(coin, RollRange(loadout.CoinDropMin, loadout.CoinDropMax));
    }

    // **시체 배낭에 넣되, 안 들어가는 만큼은 발밑에 내려놓는다.**
    //
    // 시체가 이미 뭘 지니고 있으면(`ApplyLoadout`이 못 채워 남긴 장비, 살아서 주운 것)
    // 격자가 찰 수 있다. `TryAdd`의 반환값을 안 보면 **넘친 만큼이 조용히 사라진다** —
    // 그러면 굴림표를 아무리 저작해도 "가끔 안 나오는" 것으로만 보이고 원인이 화면에 안 남는다.
    //
    // 떨구는 쪽을 고른 이유는 `Equipment.ReturnToInventory`와 같다: **눈에 보이고 운반이 알아서
    // 창고로 옮긴다.** 이 프로젝트의 증발 방지 관례다(사건으로 떨어진 것은 `NoAutoHaul`이 아니라
    // 그대로 운반된다 — 아무도 의도해서 거기 둔 것이 아니므로).
    void GiveOrDrop(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;

        int added = Inventory != null ? Inventory.TryAdd(item, amount) : 0;
        int left  = amount - added;
        if (left <= 0) return;

        DroppedItem.Spawn(item, left, transform.position + Vector3.up * 0.15f);
        Debug.Log($"[UnitBrain] {UnitName}: 시체 배낭이 차서 {item.ItemName} {left}개를 발밑에 내려놓았습니다.");
    }

    static int RollRange(int min, int max)
    {
        int lo = Mathf.Max(0, Mathf.Min(min, max));
        int hi = Mathf.Max(0, Mathf.Max(min, max));
        return hi <= 0 ? 0 : UnityEngine.Random.Range(lo, hi + 1);
    }

    // **시체에 남는 물건.** 죽을 때 만드는 이유: 살아 있는 동안 아무 차이를 만들지 않기
    // 때문이다. 미리 지고 다니게 하면 적재 무게·짐내림 같은 **살아 있는 폰의 규칙에 공짜로
    // 걸린다** — 적이 제 배낭이 무겁다고 느려지고, 아군이 되면 그것을 창고로 나르려 든다.
    //
    // 장비는 반대다(`ApplyLoadout`) — 그건 싸우는 동안 실제로 일하므로 태어날 때 있어야 한다.
    void DropLoot()
    {
        if (Inventory == null || loadout == null || loadout.Loot == null) return;

        foreach (var entry in loadout.Loot)
        {
            if (entry == null) continue;
            int n = entry.Roll();
            // JSON 아이템은 `Resolve()`로만 나온다. 넘친 만큼은 발밑에 — 조용히 사라지면 안 된다.
            if (n > 0) GiveOrDrop(entry.Resolve(), n);
        }
    }

    // **태어날 때 차고 나오는 것.**
    //
    // 배낭에 넣고 `Equipment`로 차게 한다 — 바로 꽂는 길은 없고, 있으면 안 된다.
    // `Equipment.Equip*`이 `FindInInventory` + `RemoveOne`으로 도는 배낭 경유 설계라,
    // 다른 경로를 하나 더 내면 벗을 때 돌아갈 자리가 없는 장비가 생긴다.
    //
    // 못 찬 것은 배낭에 남는다 — 그대로 전리품이 된다(양손 무기를 든 채 굴린 방패 등).
    void ApplyLoadout()
    {
        if (Inventory == null || Equipment == null) return;
        if (loadout == null || loadout.Loadout == null) return;

        // **같은 부위를 여럿 적으면 나중 것이 이긴다** — `EquipArmor`가 앞의 것을 배낭으로
        // 돌려보내고, 밀려난 것은 그대로 전리품이 된다.
        foreach (var entry in loadout.Loadout)
        {
            if (entry == null || !entry.Roll()) continue;

            var item = entry.Resolve();   // 장비는 대부분 JSON이라 여기서만 나온다
            if (Inventory.TryAdd(item, 1) <= 0) continue;

            switch (item)
            {
                case WeaponData w: Equipment.Equip(w);      break;
                case ArmorData  a: Equipment.EquipArmor(a); break;
                // 무기도 갑옷도 아니면 배낭에 든 채로 둔다(먹을 것을 지니고 다니는 적).
            }
        }
    }

    // 하던 일을 놓는다 — 큐 일감은 취소(각 Job의 OnCancelled가 재료 환불·주문 재등록을 처리),
    // 노드 직접 배정도 해제. 사망과 플레이어의 직접 이동 명령이 같은 경로를 쓴다.
    // (배정을 남겨두면 명령한 곳에 도착하자마자 다시 일하러 돌아가 명령이 안 먹는 것처럼 보인다.)
    public void AbandonWork()
    {
        if (_bb == null) return;

        if (_bb.CurrentJob != null)
        {
            JobManager.Instance?.CancelJob(_bb.CurrentJob);
            _bb.CurrentJob = null;
        }

        _bb.AssignedNode?.Unassign(this);
    }

    public void Draft()
    {
        _bb.IsDrafted = true;
        _mover.StopMovement();
    }

    public void Undraft()
    {
        _bb.IsDrafted = false;
    }

    // 적 AI(감지→전투) 대상인가 — 역할이 없고(NPC 아님) 플레이어 조작 세력도 아닌 전투 유닛.
    // 세력이 늘어나도(고블린·산적 등) 플레이어 편만 아니면 자동으로 적 트리를 쓴다.
    //
    // **`MinionData`인지는 묻지 않는다.** 전엔 `unitData is MinionData`면 무조건 하수인으로 봤는데,
    // 그건 **종족 데이터의 형태**를 **소속**으로 읽은 것이다 — 고블린은 내가 소환할 수도 있고
    // 야생에 살 수도 있는데, 그 둘을 가르는 건 세력이지 에셋 클래스가 아니다.
    // 실제로 그래서 습격 이벤트에 세운 고블린이 **플레이어 폰으로 태어났다**(2026-08-07).
    public bool UsesEnemyAi
    {
        get
        {
            if (npcData != null) return false;
            var faction = GetComponent<Faction>();
            return faction != null && !faction.IsPlayer;
        }
    }

    BTNode BuildTree()
    {
        if (UsesEnemyAi)
            return BuildEnemyTree();
        if (npcData != null)
            return BuildNpcTree(npcData);
        if (unitData is MinionData)
            return BuildMinionTree();
        return BuildWandererTree();
    }
}