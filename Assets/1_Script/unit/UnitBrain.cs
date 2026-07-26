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
    public UnitPriorities Priorities { get; private set; }

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

        var faction = GetComponent<Faction>();
        bool isEnemy  = faction != null && faction.Type == FactionType.Enemy;
        bool isMinion = unitData is MinionData;
        if (!isEnemy && !isMinion)
            Priorities = gameObject.AddComponent<UnitPriorities>();
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
        _tickTimer = 0f;
        _root.Tick(_bb);
    }

    const float BaseHungerDecayRate = 0.5f; // 초당 감소량 (100 기준 약 3.3분에 고갈)
    const float StarveBloodPerSec   = 2f;   // 허기 0 시 초당 혈액 손실 (아사)

    void UpdateHunger()
    {
        // 플레이어 팩션(방랑자/하수인)만 허기 적용
        var faction = GetComponent<Faction>();
        if (faction == null || faction.Type != FactionType.Player) return;

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

        // 진행 중인 작업이 있으면 취소해서 pending 플래그 해제
        if (_bb?.CurrentJob != null)
        {
            JobManager.Instance?.CancelJob(_bb.CurrentJob);
            _bb.CurrentJob = null;
        }

        OnUnitDied?.Invoke(this);

        var faction = GetComponent<Faction>();
        if (faction != null && faction.Type == FactionType.Enemy)
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

    public void Draft()
    {
        _bb.IsDrafted = true;
        _mover.StopMovement();
    }

    public void Undraft()
    {
        _bb.IsDrafted = false;
    }

    BTNode BuildTree()
    {
        var faction = GetComponent<Faction>();

        if (faction != null && faction.Type == FactionType.Enemy)
            return BuildEnemyTree();
        if (unitData is NpcData)
            return BuildNpcTree((NpcData)unitData);
        if (unitData is MinionData)
            return BuildMinionTree();
        return BuildWandererTree();
    }
}