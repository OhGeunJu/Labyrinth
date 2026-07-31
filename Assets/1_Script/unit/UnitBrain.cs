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
    [SerializeField] UnitData unitData;

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
        Stats.IsLocked = unitData is MinionData;
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
        if (!UsesEnemyAi && !(unitData is NpcData))
        {
            Work = gameObject.AddComponent<WorkSettings>();
            if (unitData is MinionData minionData)
                Work.InitFromAllowed(minionData.AllowedJobs);
        }
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

        _bb.RestTicked = false;
        _root.Tick(_bb);

        // 전투·소집 등 상위 분기가 휴식을 밀어냈는데 폰이 아직 누워 있으면 일으켜 세운다.
        // (BT 노드에는 중단 콜백이 없어 RestNode가 스스로 정리할 기회를 못 얻는다.)
        if (!_bb.RestTicked && _mover != null && _mover.IsSleeping)
            _mover.SetSleeping(false);
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

        // 적 AI 유닛은 약탈 가능한 시체로 남는다(플레이어 유닛/NPC는 기존대로 제거).
        if (UsesEnemyAi)
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

    // 적 AI(감지→전투) 대상인가 — 플레이어 조작 세력이 아니고, NPC/하수인도 아닌 전투 유닛.
    // 세력이 늘어나도(고블린·산적 등) 플레이어 편만 아니면 자동으로 적 트리를 쓴다.
    public bool UsesEnemyAi
    {
        get
        {
            if (unitData is NpcData || unitData is MinionData) return false;
            var faction = GetComponent<Faction>();
            return faction != null && !faction.IsPlayer;
        }
    }

    BTNode BuildTree()
    {
        if (UsesEnemyAi)
            return BuildEnemyTree();
        if (unitData is NpcData)
            return BuildNpcTree((NpcData)unitData);
        if (unitData is MinionData)
            return BuildMinionTree();
        return BuildWandererTree();
    }
}