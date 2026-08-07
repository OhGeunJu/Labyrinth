using System;
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

        // 생김새 — 지금은 크기 하나뿐이다. 늘어나면 여기에 줄이 는다.
        if (spec.BodyScale > 0f) transform.localScale = Vector3.one * spec.BodyScale;
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
        var corpse = gameObject.AddComponent<Corpse>();
        corpse.Init(UnitName, Inventory);

        enabled = false;
        if (_mover != null) _mover.enabled = false;

        var combat = GetComponent<CombatHandler>();
        if (combat != null) combat.enabled = false;

        // 팩션 제거 — 시체가 적으로 재탐지·재공격되는 것 방지
        var faction = GetComponent<Faction>();
        if (faction != null) Destroy(faction);
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