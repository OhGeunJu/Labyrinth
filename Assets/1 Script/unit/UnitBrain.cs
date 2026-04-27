using System;
using UnityEngine;

public class UnitBrain : MonoBehaviour
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

    // BodyPartSystem 없는 유닛을 위한 폴백 HP
    float _hp;
    float _maxHp;

    public UnitBlackboard Blackboard => _bb;
    public UnitData Data => unitData;
    public UnitStats Stats { get; private set; }
    public Inventory Inventory { get; private set; }
    public Equipment Equipment { get; private set; }
    public UnitPriorities Priorities { get; private set; }

    void Awake()
    {
        _mover    = GetComponent<PawnMover>();
        Inventory = gameObject.AddComponent<Inventory>();
        Equipment = gameObject.AddComponent<Equipment>();
        gameObject.AddComponent<StatusEffectSystem>();

        Stats = UnitStats.FromData(unitData);

        _maxHp = unitData != null ? unitData.MaxHP : 100f;
        _hp    = _maxHp;

        _bb = new UnitBlackboard
        {
            Self         = gameObject,
            Mover        = _mover,
            DetectRange  = unitData != null ? unitData.DetectRange  : 10f,
            AttackRange  = unitData != null ? unitData.AttackRange  : 1.5f,
            AttackDamage = unitData != null ? unitData.AttackDamage : 10f,
            Mood         = 100f,
        };

        if (unitData != null)
        {
            _mover.Init(unitData);
            _mover.SetStats(Stats, Inventory);
        }

        _root = BuildTree();

        var faction = GetComponent<Faction>();
        bool isEnemy  = faction != null && faction.Type == FactionType.Enemy;
        bool isMinion = unitData is MinionData;
        if (!isEnemy && !isMinion)
            Priorities = gameObject.AddComponent<UnitPriorities>();
    }


    void Start()
    {
        OnUnitSpawned?.Invoke(this);
    }

    void Update()
    {
        if (_bb.IsKnockedOut) return;
        if (_bb.IsStaggered)  return;

        _tickTimer += Time.deltaTime;
        if (_tickTimer < tickInterval) return;
        _tickTimer = 0f;
        _root.Tick(_bb);
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
        corpse.Init(unitData != null ? unitData.RaceName : name, Inventory);

        enabled = false;
        if (_mover != null) _mover.enabled = false;

        var combat = GetComponent<CombatHandler>();
        if (combat != null) combat.enabled = false;
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
        else if (unitData is MinionData)
            return BuildMinionTree();
        else
            return BuildWandererTree();
    }

    // 방랑자: 소집 → 전투 → 작업 → 대기
    BTNode BuildWandererTree()
    {
        return new BTSelector()
            .Add(new BTSequence()
                .Add(new ConditionNode(bb => bb.IsDrafted))
                .Add(new DraftedNode()))
            .Add(new BTSequence()
                .Add(new ConditionNode(bb => bb.Target != null))
                .Add(new FightNode()))
            .Add(new WorkNode())
            .Add(new IdleNode());
    }

    // 하수인: 자동 감지 반격 → 작업 (허용 카테고리만) → 대기
    BTNode BuildMinionTree()
    {
        return new BTSelector()
            .Add(new BTSequence()
                .Add(new DetectNode())
                .Add(new FightNode()))
            .Add(new WorkNode())
            .Add(new IdleNode());
    }

    BTNode BuildEnemyTree()
    {
        return new BTSelector()
            .Add(new BTSequence()
                .Add(new DetectNode())
                .Add(new FightNode()))
            .Add(new IdleNode());
    }
}