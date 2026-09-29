using System;
using System.Collections.Generic;
using UnityEngine;

public partial class UnitBrain : MonoBehaviour
{
    public static event Action<UnitBrain> OnUnitSpawned;
    public static event Action<UnitBrain> OnUnitDied;

    // **편이 바뀌었다.** 태어나는 것과 다른 축이라 따로 알린다 — `OnUnitSpawned`는 `Start`에서
    // 한 번 뜨고 마는데, 회유로 넘어온 포로나 합류한 떠돌이는 **그 문을 안 지난다.**
    // 그래서 명부(`ColonyUI`)·원정 편성이 새 식구를 영영 못 봤다(2026-08-14 발견).
    //
    // 듣는 쪽은 `OnUnitSpawned`와 **같이** 받아야 한다 — 한쪽만 달면 어느 경로로 들어왔느냐로
    // 화면이 갈린다.
    public static event Action<UnitBrain> OnFactionChanged;
    public event Action<float> OnDamaged;

    // **살아 있는 뇌 전부.** 폰을 훑어야 할 때 `FindObjectsByType<UnitBrain>`을 도는 대신 쓴다
    // (`Faction.All`과 같은 자리) — 여덟 곳이 각자 씬을 훑고 있었다(2026-09-09).
    // `OnDisable`로 빠지므로 **시체**(`BecomeCorpse`가 끈다)와 꺼진 폰(원정)은 여기 없다.
    // 씬 훑기는 꺼진 컴포넌트도 돌려줘서, 핵 창의 하수인 수에 시체와 **적 고블린**까지 섞였다.
    static readonly List<UnitBrain> _all = new();
    public static IReadOnlyList<UnitBrain> All => _all;

    void OnEnable()  => _all.Add(this);
    void OnDisable() => _all.Remove(this);

    [Header("틱 주기")]
    [SerializeField] float tickInterval = 0.1f;

    // 한 프레임에 몰아서 돌 수 있는 틱 수의 천장. **10배·30fps까지는 여기 안 닿는다**
    // (0.333초 / 0.1초 = 3.3틱) — 닿는 것은 프레임이 튀었을 때뿐이고, 그때는 조각을
    // 늘려 시간을 지킨다. `PawnMover.MaxBakesPerFrame`·`StepMove`의 천장과 같은 자리.
    const int MaxTicksPerFrame = 4;

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

    // **성별은 몸이 답한다**(`PawnBody`) — 몸이 없는 폰(늑대·골렘·블록 인형)은 `null`이다.
    // 명세의 성별(`PawnSpec.Gender`)은 몸을 고르는 데 쓰였을 뿐이라 여기서 다시 안 읽는다.
    public Gender? Gender
    {
        get
        {
            var body = GetComponent<PawnBody>();
            return body != null ? body.Gender : (Gender?)null;
        }
    }

    // 이 폰의 **역할**(없으면 null = 역할 없는 전투/노동 유닛).
    public NpcData Npc => npcData;

    // **이 폰을 창에 뭐라고 적나 — 한 곳.**
    //
    // `이름 (딸림말)` 꼴이다. 이름만 띄우면 고블린이 셋일 때 누구를 눌렀는지는 알아도 **무엇인지**를
    // 모르고, 딸림말만 띄우면 셋 다 "고블린"이라 개체가 구별되지 않는다.
    //
    // **딸림말은 "무엇을 하는 사람인가 · 어느 편인가"** 둘이다:
    //   ① **역할**(`NpcData`)이 있으면 그것 — 대화에서 알고 싶은 것은 대개 이쪽이다
    //      (`던컨 (떠돌이 · 변방민)`이 `던컨 (인간)`보다 쓸모 있다).
    //      **역할이 없으면 종족이 그 자리를 대신한다** — 늑대를 `(야수)`라고만 하면
    //      곰과 구별이 안 된다.
    //   ② **세력** — 소속은 늘 붙인다. 누가 적이고 누가 내 편인지가 곧 그 사람이다.
    //      **플레이어 세력만 뺀다**: 내 폰에게 "(플레이어)"는 아무것도 안 알려 준다.
    //
    // **같은 말이 두 번 나오면 하나로 줄인다** — 고블린 적은 종족도 세력도 "고블린"이라
    // 안 줄이면 `그르낙 (고블린 · 고블린)`이 된다.
    //
    // 창마다 따로 만들면 **같은 폰이 창마다 다르게 불린다**(우클릭에선 `(인간)`, 대화에선
    // `(떠돌이)`). 그래서 여기 하나뿐이고 `MoveController`·`Conversation`이 같이 쓴다.
    public string DisplayTitle
    {
        get
        {
            var tags = new List<string>(2);

            string role = npcData != null ? npcData.DisplayName : null;
            if (!string.IsNullOrWhiteSpace(role)) tags.Add(role);
            else if (unitData != null && !string.IsNullOrWhiteSpace(unitData.RaceName))
                tags.Add(unitData.RaceName);

            var fd = GetComponent<Faction>()?.Data;
            if (fd != null && !fd.PlayerControlled && !string.IsNullOrWhiteSpace(fd.FactionName)
                && !tags.Contains(fd.FactionName))
                tags.Add(fd.FactionName);

            string name = UnitName;
            bool hasName = !string.IsNullOrWhiteSpace(name);

            if (tags.Count == 0) return hasName ? name : gameObject.name;

            string tag = string.Join(" · ", tags);
            if (hasName && name != tag) return $"{name} ({tag})";
            return hasName ? name : tag;
        }
    }

    // 역할을 벗는다 — **합류할 때** 쓴다(떠돌이를 고용하면 더는 떠돌이가 아니다).
    //
    // 역할이 남아 있으면 `BuildTree`가 세력과 무관하게 **NPC 트리를 다시 고른다**: 우리 편이
    // 됐는데 명령을 안 듣고 저 혼자 배회하다 맵을 나간다. 세력을 바꾸는 것(`ChangeFaction`)과
    // **짝으로** 부를 것 — 트리를 다시 짓는 것은 그쪽이 한다.
    public void ClearNpcRole() => npcData = null;
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

        // **몸과 명세가 성별로 어긋나면 스폰하는 자리가 `PawnSpawn`을 안 거친 것이다** — 여자 몸에 남자 이름이
        // 붙는다. 조용히 넘기면 *"이름이 이상하다"*로만 보여서 알린다.
        var g = Gender;
        if (g.HasValue && spec.Gender.HasValue && g.Value != spec.Gender.Value)
            Debug.LogWarning($"[몸] {name}: 몸은 {g.Value.DisplayName()}인데 명세는 {spec.Gender.Value.DisplayName()}이다 — " +
                             "이 폰을 세운 자리가 `PawnSpawn`을 거치지 않았다.", this);

        Rename(spec.Name);

        if (Stats != null)
            foreach (var kv in spec.Grades)
                Stats.SetStat(kv.Key, kv.Value.ToValue());

        // **특성은 스탯을 덮어쓴 뒤에 얹는다.** 순서가 뒤집히면 명세의 등급이 특성 보정을
        // 지워 버려, 특성이 붙어 있는데 아무 효과도 없는 폰이 된다.
        SetTraits(spec.Traits);
        RememberBackstories(spec.Childhood, spec.Adulthood);
        SetBackstories(spec.Childhood, spec.Adulthood);
        Stats?.SetPassions(spec.Passions);

        // 생김새 — 크기 · 재질 · 전용 모델. 늘어나면 여기에 줄이 는다.
        if (spec.BodyScale > 0f) transform.localScale = Vector3.one * spec.BodyScale;
        ApplyLook(spec);
    }

    // ── 성장 배경 ─────────────────────────────────────────────────────────────
    //
    // **사람이면 누구나 갖는다** — 내 폰이든 산적이든 마을 사람이든(`UnitData.UsesBackstory`).
    //
    // 한때 방랑자에게만 **얹었다**(굴려는 두고). 근거는 *"읽고 쓸 사람이 있는 폰은 내
    // 방랑자뿐"*이었는데, 세력마다 제 이력을 갖게 되고(`BackstoryGroup`) 나니 그게 뒤집혔다 —
    // **붙잡은 산적을 살펴볼 때 `패거리 두목`이라고 뜨는 것**이야말로 회유할지 몸값을 받을지
    // 정하는 재료다. 안 얹으면 그 줄이 비어서, 세력별 이력을 저작한 것이 화면에 한 번도
    // 안 나타난다.
    //
    // **작업 금지는 저절로 갈린다** — `WorkSettings`는 플레이어 세력 일꾼에만 붙으므로
    // (`ApplyJobBlocks`가 `Work == null`이면 아무 일도 안 한다) 적에게는 걸릴 자리가 없고,
    // 회유해 오는 순간 `ChangeFaction`이 다시 얹는다. 그래서 *"무능력: 운반"*은 적을
    // 살펴볼 때 **앞으로 데려오면 어떻게 되는지**를 미리 알려 주는 줄이 된다.
    public BackstoryData Childhood { get; private set; }
    public BackstoryData Adulthood { get; private set; }

    // 명세가 들고 온 배경으로 **갈아 끼운다**. 특성과 같은 이유로 이전 것을 먼저 되돌린다 —
    // 안 그러면 `ApplySpec`이 두 번 불릴 때 보정도 금지도 두 겹이 된다.
    // **굴려 둔 것과 얹은 것은 다르다.** 적으로 태어난 사람도 이력은 있고(그래야 회유해 왔을
    // 때 그 이력을 들고 온다), 얹는 것은 방랑자가 되는 순간이다.
    BackstoryData _rolledChildhood, _rolledAdulthood;

    // **성년기만 갈아 끼운다**(2026-09-08). 상인은 *무엇을 파느냐*가 곧 *무엇을 하며 살았느냐*라
    // 그 칸이 장사를 따라가야 하는데, **상인 종류는 스폰 뒤에 주입된다**(`EventManager`가
    // `merchant.Data = variant`) — `Awake`의 굴림은 그때 그걸 모른다.
    //
    // **유년기는 안 건드린다** — 상인도 어디선가 자랐고, 그건 세력의 이력이다. 둘 다 갈면
    // *"태어날 때부터 무기 상인"*이 된다.
    //
    // 굴린 것이 없으면(`_rolledAdulthood`가 null) 아무것도 안 한다 — 배경을 안 쓰는 종족이다.
    public void RerollAdulthood(string group)
    {
        if (string.IsNullOrEmpty(group) || _rolledAdulthood == null) return;

        var rng  = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        SetAdulthood(BackstoryPool.Roll(BackstorySlot.Adulthood, rng, group));
    }

    // **성년기를 지정해서 갈아 끼운다.** 굴려서 바꾸는 쪽(`RerollAdulthood`)과 **같은 문을
    // 지난다** — 갈라 두면 한쪽만 `RememberBackstories`를 빠뜨려, 그 뒤에 한 번 더 갈 때
    // **되돌릴 이전 값이 틀린다**(보정이 두 겹으로 남는다).
    //
    // 지금 쓰는 자리는 습격대의 두목이다(`RaidLord.PromoteLeader` — 세게 만들려고 다시
    // 굴리면 *"무리를 이끈"*이 날아간다).
    public void SetAdulthood(BackstoryData story)
    {
        if (story == null || _rolledAdulthood == null) return;   // 배경을 안 쓰는 종족
        if (story == Adulthood) return;

        RememberBackstories(_rolledChildhood, story);
        SetBackstories(_rolledChildhood, story);
    }

    void RememberBackstories(BackstoryData childhood, BackstoryData adulthood)
    {
        _rolledChildhood = childhood;
        _rolledAdulthood = adulthood;
    }

    void SetBackstories(BackstoryData childhood, BackstoryData adulthood)
    {
        BackstoryPool.Remove(Childhood, Stats);
        BackstoryPool.Remove(Adulthood, Stats);
        Work?.ClearBlocked();

        Childhood = childhood;
        Adulthood = adulthood;

        BackstoryPool.Apply(Childhood, Stats);
        BackstoryPool.Apply(Adulthood, Stats);
        ApplyJobBlocks();
    }

    // **못 하는 일을 작업 설정에 새긴다.** `Work`는 나중에 생길 수 있어서(회유로 우리 편이 된
    // 폰은 `ChangeFaction`이 그때 붙인다) 따로 부를 수 있게 뒀다 — 안 그러면 넘어온 사람만
    // 무능력이 조용히 사라진다.
    public void ApplyJobBlocks()
    {
        if (Work == null) return;

        Work.ClearBlocked();
        BlockFromRace();
        BlockFrom(Childhood);
        BlockFrom(Adulthood);
    }

    void BlockFrom(BackstoryData b)
    {
        if (b?.DisabledJobs == null) return;
        foreach (var c in b.DisabledJobs) Work.SetBlocked(c, true);
    }

    // **종족도 못 하는 일을 든다**(2026-08-28) — 골렘에게 요리·연구·의술은 *"서툴다"*가
    // 아니라 **못 하는 것**이다.
    //
    // 등급을 F−로 깔아 두는 것만으로는 부족하다: 그건 *"아주 못한다"*여서 **다른 폰이
    // 없으면 그 폰이 간다.** 화면에도 켤 수 있는 칸으로 뜨고. 성장 배경의 무능력과 **같은
    // 물음**이라 답도 같은 자리에 둔다(`WorkSettings.SetBlocked` — 칸에 `×`로 뜨고 사람이
    // 못 켠다).
    void BlockFromRace()
    {
        if (unitData?.DisabledJobs == null) return;
        foreach (var c in unitData.DisabledJobs) Work.SetBlocked(c, true);
    }

    // ── 특성 ──────────────────────────────────────────────────────────────────
    // **이 폰이 어떤 사람인가.** 스탯에 숫자만 얹으므로(모델 A) 전투·작업 코드는 특성을 모른다.
    readonly List<TraitData> _traits = new();

    public IReadOnlyList<TraitData> Traits => _traits;

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
        // **0이면 조용히 멈춘다 — 한 프레임에 몰아 도는 길이 0으로 나눈다**(밀린 틱 수를
        // 세는 식이라). 전에는 0이면 매 프레임 돌았으니 **틀려도 티가 안 났다**.
        if (tickInterval <= 0f)
        {
            Debug.LogWarning($"[UnitBrain] {name}의 틱 주기가 {tickInterval}초입니다 — " +
                             "0.1초로 되돌립니다(0이면 BT가 아예 안 돕니다).", this);
            tickInterval = 0.1f;
        }

        _mover    = GetComponent<PawnMover>();
        _body     = GetComponent<BodyPartSystem>();
        Inventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        Equipment = GetComponent<Equipment>() ?? gameObject.AddComponent<Equipment>();
        gameObject.AddComponent<StatusEffectSystem>();

        // **능동은 장비가 주므로 그릇은 누구에게나 붙인다.** 세력으로 가르면(플레이어만)
        // 적에게 능동을 쥐여 주려는 순간 그 분기를 도로 찾아야 하고, 장비를 안 찬 폰에게는
        // 어차피 목록이 비어 아무 일도 안 한다(`WorkSettings`가 일꾼에만 붙는 것과 다른 축 —
        // 저쪽은 저작 대상이고 이건 장비에서 파생된다).
        gameObject.AddComponent<AbilityUser>();

        Stats = UnitStats.FromData(unitData);
        // 성장이 잠기는 건 **내가 소환한** 하수인이다. 같은 종족 에셋을 쓰는 야생 개체까지
        // 잠그면 "고블린은 성장하지 않는다"가 되는데, 그건 종족의 성질이 아니다.
        Stats.IsLocked = IsSummoned;
        Stats.Owner    = gameObject;

        _maxHp = unitData != null ? unitData.MaxHP : 100f;
        _hp    = _maxHp;

        _bb = new UnitBlackboard
        {
            Self         = gameObject,
            Mover        = _mover,
            Brain        = this,
            Body         = _body,
            Combat       = GetComponent<CombatHandler>(),
            Faction      = GetComponent<Faction>(),
            DetectRange  = unitData != null ? unitData.DetectRange  : 10f,
            NightVision  = unitData != null ? unitData.NightVision  : 0f,
            AttackRange  = unitData != null ? unitData.AttackRange  : 1.5f,
            AttackDamage = unitData != null ? unitData.AttackDamage : 10f,
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

            // **여기서 다시 새긴다**(2026-08-28). `SetBackstories`는 `Awake`의 앞쪽
            // (`ApplySpec`)에서 도는데 `Work`는 여기서 생긴다 — 그래서 `ApplyJobBlocks`가
            // `Work == null`로 조용히 빠져나갔고, **화면에는 "무능력: 운반"이라고 적혀
            // 있는데 폰은 운반을 했다.** 이 함수의 주석이 이미 그 위험을 적어 뒀는데
            // (*"`Work`는 나중에 생길 수 있어서 따로 부를 수 있게 뒀다"*) 정작 부르는
            // 곳이 `SetBackstories` 하나뿐이었다.
            ApplyJobBlocks();
        }

        HookUnloadSignals();
    }

    void OnDestroy()
    {
        UnhookUnloadSignals();

        // 파괴 경로가 `Die`만은 아니다(원정 정리·씬 언로드) — 어느 쪽으로 사라지든
        // 업고 있던 사람이 부모째 딸려 가면 안 된다.
        PawnCarry.DropOnDestroy(this);
    }

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
            var rng = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            SetTraits(TraitPool.RollForPawn(rng));

            // **명세가 없는 폰도 배경을 굴려 둔다 — 얹지 않더라도.**
            //
            // 이 폰들이 대부분이다(적·NPC·이벤트로 온 사람). 안 굴려 두면 **회유해 온 산적이
            // 배경 없는 사람**이 된다 — 데려오고 나서 굴리면 그건 살아온 이력이 아니라
            // 합류 보너스다. 굴려는 두되 얹는 것은 방랑자가 될 때다(`SetBackstories`).
            // **소환된 것에게는 제 이력이 있다**(2026-09-08, 사용자 결정 — 처음엔 비웠다가
            // *"그냥 배경을 소환한 하수인 뭐 이런 느낌으로 잡아주면 되잖아"*로 바꿨다).
            //
            // 불려 나온 고블린에게 *"굴의 아이"*와 *"약탈꾼"*이 붙으면 **살아온 적 없는 삶이
            // 스탯 창에 적힌다.** 그렇다고 비워 두면 창에 빈칸이 남는데, 이 저장소의 규칙은
            // *"공용이라는 것은 없다 — 모든 배경은 어느 무리의 것이다"*이지 *"어떤 무리는
            // 배경이 없다"*가 아니다. **소환물도 무리다** — `summoned` 묶음이 그 자리다.
            //
            // **묶음을 세력이 못 든다.** 소환물은 **플레이어 세력**이라 `FactionData`로 가르면
            // 내 방랑자까지 같은 묶음을 쓴다 — 여기서 갈라야 한다.
            //
            // **`UsesBackstory`를 안 본다.** 그 스위치는 *"늑대에게 사람 유년기를 주지 마라"*는
            // 뜻인데, 소환물은 **제 무리의 이력**을 받으므로 그 걱정이 아예 없다. 그리고 볼
            // 수도 없다: `Skeleton`은 야생과 **같은 에셋**이라(`Enemy_Skeleton`·`Minion_Skeleton`)
            // 켜는 순간 **야생 해골이 `civilian` 폴백으로 사람 이력을 받는다.**
            if (unitData != null && (IsSummoned || unitData.UsesBackstory))
            {
                var group = IsSummoned ? PawnSpec.SummonedStoryGroup
                                       : GetComponent<Faction>()?.Data?.BackstoryGroup;
                RememberBackstories(BackstoryPool.Roll(BackstorySlot.Childhood, rng, group),
                                    BackstoryPool.Roll(BackstorySlot.Adulthood, rng, group));
                SetBackstories(_rolledChildhood, _rolledAdulthood);

                // **열정은 사람만**(소환물은 성장이 잠겨 있어 붙여 봐야 아무 일도 안 한다 — `IsLocked`).
                if (!IsSummoned) Stats?.SetPassions(Passions.Roll(rng, _rolledChildhood, _rolledAdulthood));
            }
            return;
        }

        var spec = pawnSpec.Build(unitData, Gender);   // 몸이 이미 있다 — 그 성별로 굴린다
        if (!string.IsNullOrWhiteSpace(fixedName)) spec.Name = fixedName;
        ApplySpec(spec);
    }

    string ResolveUnitName()
    {
        // 이름은 몸의 성별을 따른다(`UnitData.NamesFor` — 성별 풀 + 중성 풀).
        var names = unitData != null ? unitData.NamesFor(Gender) : null;
        if (names != null && names.Count > 0)
            return names[UnityEngine.Random.Range(0, names.Count)];
        if (unitData != null && !string.IsNullOrEmpty(unitData.RaceName))
            return unitData.RaceName;
        return gameObject.name;
    }

    void Start()
    {
        // **알리기 전에 입힌다.** 구독자(로스터·UI·`WorldBattle` 전력 계산)가 장비를 읽으므로,
        // 알린 뒤에 채우면 첫 계산이 맨몸 기준으로 나온다.
        ApplyLoadout();

        // **주머니는 장비 다음이다.** `ApplyLoadout`이 배낭을 거쳐 장비를 차므로(그것이 유일한
        // 장착 경로다) 먼저 채우면 그 왕복에 격자를 빼앗아 **무기를 못 차는** 일이 생긴다.
        FillPockets();

        OnUnitSpawned?.Invoke(this);
    }

    void Update()
    {
        // 허기는 기절 중에도 진행 — 쓰러진 채 굶으면 혈액이 계속 빠져 결국 아사(BT 정지와 무관).
        UpdateNeeds();

        if (_bb.IsKnockedOut) return;

        // **업혀 있거나 갇혀 있으면 제 뜻대로 못 움직인다.** 업힌 쪽은 어깨에 붙어 따라가고,
        // 갇힌 쪽은 자리를 지킨다 — BT를 돌리면 깨어나는 순간 걸어 나간다.
        if (_bb.CarriedBy != null) return;
        if (_bb.IsPrisoner) { Imprisonment.Hold(this); return; }

        _tickTimer += Time.deltaTime;
        if (_tickTimer < tickInterval) return;

        // **멀리 있는 남의 폰은 생각하지 않는다**(2026-08-26). 세계가 29km라 전부 돌릴 수
        // 없다 — 플레이어 세력은 언제나 돌고(원정 나간 팀이 얼어붙으면 안 된다) 그 밖은
        // 활성 반경 안에서만 돈다. 절차 지형이 꺼진 씬에서는 이 게이트가 통째로 안 걸린다.
        //
        // **타이머는 흘려보낸 뒤에 판단한다** — 위에서 막으면 `_tickTimer`가 쌓여 있다가
        // 가까이 간 순간 델타가 몇 초짜리로 한 번에 들어온다.
        if (!SimLocality.ShouldTick(this)) { _tickTimer = 0f; return; }

        // **배속에서는 한 프레임이 틱 간격보다 길어진다 — 밀린 만큼 나눠서 돈다**
        // (2026-09-11). `Time.deltaTime`이 `timeScale`을 타므로 **10배·60fps면 한 프레임이
        // 0.167초**인데, 전에는 쌓인 것을 통째로 한 틱에 쏟고 타이머를 비웠다. 그래서 BT가
        // **게임 시간으로 0.167초마다** 돌았다(0.1초가 아니다).
        //
        // 누적하는 값은 `TickDelta`가 정직해서 맞았다(허기·수면·채굴 진행·경험치). 틀린
        // 것은 **판단의 간격**이다 — 적을 알아보는 것·일감을 집는 것·막혔다고 포기하는 것이
        // 배속에 따라 늦어지고, **프레임이 떨어지면 더 늦어진다**(30fps·10배면 0.33초).
        // 배속과 프레임레이트는 화면의 축이지 행동의 축이 아니다.
        //
        // `PawnMover.StepMove`와 **같은 모양**이다: 시간은 그대로 두고 나눠서 민다.
        // 남는 것은 버리지 않고 다음 프레임으로 넘긴다 — 버리면 10배에서 틱이 성긴 채로
        // 그대로다(그것이 고치려던 것이다).
        int steps = (int)(_tickTimer / tickInterval);
        float slice;
        if (steps > MaxTicksPerFrame)
        {
            // **천장은 악순환을 막는 것이다** — 프레임이 한 번 튀면(로드·히치) 수십 번이
            // 밀리는데 그만큼 돌면 그 프레임이 더 길어져 다음 프레임이 더 밀린다.
            // 걸렸을 때 **남은 시간을 나눠 갖는다** — 버리면 배속에서 허기·작업이 느려진다
            // (`TickDelta`의 합이 실제로 흐른 시간이어야 한다).
            steps      = MaxTicksPerFrame;
            slice      = _tickTimer / MaxTicksPerFrame;
            _tickTimer = 0f;
        }
        else
        {
            slice       = tickInterval;
            _tickTimer -= steps * tickInterval;
        }

        for (int i = 0; i < steps; i++)
        {
            _bb.TickDelta = slice;   // 노드가 쓸 실제 경과 시간 (한 프레임이 아니라 틱 간격)

            UpdateUnload();

            _bb.RestTicked = false;
            _bb.Working    = WorkAnim.None;   // 이번 틱에 일하면 `WorkNode`가 다시 적는다
            _root.Tick(_bb);

            // 전투·소집 등 상위 분기가 휴식을 밀어냈는데 폰이 아직 누워 있으면 일으켜 세운다.
            // (BT 노드에는 중단 콜백이 없어 RestNode가 스스로 정리할 기회를 못 얻는다.)
            if (!_bb.RestTicked && _mover != null && _mover.IsSleeping)
                _mover.SetSleeping(false);
        }
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

    // **눈금·임계·바닥 벌칙은 전부 `Needs`가 답한다** — 여기는 시간을 흘려보낼 뿐이다.
    // 축이 둘(허기·피로)이라 값을 여기 적으면 축마다 한 벌씩 생긴다.
    void UpdateNeeds()
    {
        // **누가 이 축을 도는가는 `Needs.Simulates` 하나가 답한다** — 플레이어 세력, 그리고
        // **갇힌 사람은 세력을 안 가린다**(업혀 있는 동안은 아무도 안 돈다).
        //
        // 한동안 갇힌 사람은 아예 굶지 않았다. 갇히면 BT가 안 돌아 `EatNode`도 안 도는데
        // 허기는 BT 밖에서 깎이므로, 우리 폰을 가둬 두면 **먹을 방법이 없는 채로 굶어
        // 죽었다.** 그건 규칙이 아니라 사고였다 — 이제 먹이는 일감(`FeedJob`)이 있다.
        //
        // 되살리면서 **적 포로도 굶게 했다.** 그래야 붙잡아 두는 데 값이 든다 — 예전에는
        // 우리에 넣어 두기만 하면 회유가 저절로 끝났다.
        //
        // 화면의 바(`InspectPanelUI`)도 같은 답을 쓴다. 도는 것과 보이는 것이 갈리면
        // "안 보이는데 죽는" 또는 "안 줄어드는 바"가 생긴다.
        if (!Needs.Simulates(this)) return;

        float dt = Time.deltaTime;

        for (int i = 0; i < Needs.Count; i++)
        {
            var kind = (NeedKind)i;
            // 갇힌 사람은 피로가 안 돌고, **자는 동안에도 피로는 안 준다**(`Needs.Decays`).
            if (!Needs.Decays(this, kind)) continue;

            float rate = Needs.DecayPerSecond(kind) * NeedRateModifier(kind);
            if (rate <= 0f) continue;

            float left = Mathf.Max(0f, _bb.Need(kind) - rate * dt);
            _bb.SetNeed(kind, left);
            if (left > 0f) continue;

            // 바닥 — 신체 부위 시스템이 있으면 **혈액을 깎아** 화면에 보이는 파이프라인으로
            // 기절 → 사망시킨다. 없는 유닛만 숨은 폴백 HP를 깎는다.
            // 피로는 여기서 0을 돌려주므로 몸이 안 상한다(대신 쓰러져 잔다).
            float harm = Needs.StarvePerSecond(kind) * dt;
            if (harm <= 0f) continue;

            if (_body != null) _body.Starve(harm);
            else               TakeDamage(harm);
        }
    }

    // 핵이 허기를 늦추거나 앞당긴다(`DungeonCoreData.HungerRateModifier`).
    // **허기에만 건다** — 그 손잡이는 "이 핵의 하수인은 덜 먹는다"는 뜻으로 저작된 것이라,
    // 피로까지 조용히 따라 움직이면 저작한 사람이 모르는 채로 둘이 바뀐다.
    float NeedRateModifier(NeedKind kind)
    {
        if (kind != NeedKind.Hunger) return 1f;
        var core = DungeonCore.Instance;
        return core != null && core.Data != null
             ? Mathf.Max(0f, 1f + core.Data.HungerRateModifier)
             : 1f;
    }

    // BodyPartSystem 없는 유닛용 폴백
    public void TakeDamage(DamageInfo info) => TakeDamage(info.Amount);

    public void TakeDamage(float damage)
    {
        _hp = Mathf.Max(0f, _hp - damage);
        OnDamaged?.Invoke(damage);
        if (_hp <= 0f) Die();
    }

    // **죽었나.** 시체는 `UnitBrain`을 끄지만 컴포넌트 자체는 남으므로, 이 폰을 참조하고
    // 있던 쪽(침대·우리)에서는 `== null`로 걸러지지 않는다 — 물어볼 자리가 필요하다.
    public bool IsDead => _isDead;

    public void Die()
    {
        if (_isDead) return;
        _isDead = true;

        ReportDeathToFaction();

        // **업고 있던 사람을 먼저 내려놓는다.** 안 내려놓으면 시체의 자식으로 남아 같이
        // 굳고, 시체를 치우면 산 사람이 함께 사라진다. 업혀 있었다면 그 사실도 지운다 —
        // 업은 폰이 계속 무게를 지고 다니게 된다.
        PawnCarry.DropOnDestroy(this);
        if (_bb.CarriedBy != null) PawnCarry.PutDown(_bb.CarriedBy, transform.position);

        // 자리에서 죽으면 그 자리를 비운다 — 안 비우면 다시 못 쓴다.
        //
        // **침대도 같이 비운다.** 예전엔 우리만 비웠는데, `Bed`는 시체를 계속 물고 있어
        // (`_occupant`가 `== null`로 안 걸린다 — 컴포넌트는 남는다) **그 침대를 아무도 못
        // 썼다.** 적 포로가 굶어 죽을 수 있게 되면서(2026-08-12 먹이기) 실제로 밟히는
        // 자리가 됐다.
        //
        // `Imprisonment.Release`를 안 쓰는 이유: 그건 놓아 준 적에게 도주 성향을 씌우고
        // 맵 밖으로 내보낸다 — 시체에게 시킬 일이 아니다. 자리만 비운다.
        PrisonCage.HolderOf(this)?.Release();
        Bed.ReleaseFrom(this);

        // 진행 중인 작업이 있으면 취소해서 pending 플래그 해제 + 노드 배정 정리
        AbandonWork();

        OnUnitDied?.Invoke(this);

        // **누가 죽든 시체로 남는다 — 내 폰도.**
        //
        // 한때 플레이어 세력만 그 자리에서 파괴했다. 그런데 파괴 경로는 `BecomeCorpse`를
        // 안 거치므로 **입고 있던 장비와 배낭 속 물건이 통째로 증발했다** — 원정 나간 폰이
        // 죽으면 지고 있던 짐까지 같이 사라졌다. 죽음이 "장비를 되찾으러 가는 일"이 되려면
        // 몸이 남아야 한다(`Corpse.WasPlayer`는 원래 그걸 상정하고 있던 자리다).
        //
        // 갈랐던 이유였던 "내 폰 시체가 쌓인다"는 **치우는 길**로 풀 일이지, 몸을 없애서
        // 풀 일이 아니다 — 그 길은 셋이다(도축·매장·화장).
        BecomeCorpse();
    }

    // **죽이면 그 세력이 기억한다.**
    //
    // 지금까지 중립을 죽여도 반격하는 것은 **그 개인뿐**이었다(`LastAttacker`, 15초).
    // 그래서 마을 사람을 몰살하고도 그 마을과 계속 거래할 수 있었다 — 세력이 아홉이어도
    // 관계표가 안 움직이면 배경화면이라는 것이 가장 크게 드러나던 자리다.
    //
    // **이미 적대인 상대는 안 센다.** 전쟁 중에 적을 죽이는 것은 사건이 아니라 전쟁이고,
    // 세면 습격 한 번 막을 때마다 관계가 바닥을 치는 무의미한 숫자가 된다.
    //
    // **갇힌 채로 죽으면 처형으로 친다** — 손에 넣어 두고 죽인 것은 싸우다 죽인 것과 다르다
    // (굶겨 죽인 것도 여기 걸린다. 그것도 내가 한 일이다).
    void ReportDeathToFaction()
    {
        var mine = GetComponent<Faction>();
        if (mine == null || mine.Data == null || mine.IsPlayer) return;

        var player = Faction.Player;
        if (player == null) return;

        // 저쪽이 이미 나를 적대하면 셈에서 뺀다.
        if (mine.IsHostileTo(player)) return;

        bool held = _bb != null && _bb.IsPrisoner;
        FactionRelations.Add(mine.Data,
            held ? FactionRelations.ExecutePenalty : FactionRelations.KillPenalty,
            held ? $"{UnitName}을(를) 가둬 두고 죽였다" : $"{UnitName}을(를) 죽였다");
    }

    void BecomeCorpse()
    {
        // **주머니는 여기서 만들지 않는다 — 태어날 때 이미 넣었다**(`FillPockets`).
        //
        // 예전엔 죽는 순간 굴려 시체 배낭에 넣었다. 이유는 *"살아 있는 동안 아무 차이를
        // 안 만든다"*였는데, **그게 사실이 아니었다**: 살아 있는 적을 뒤지면 배낭이 **늘
        // 비어 있었다.** 붙잡아 온 포로도, 쓰러뜨려 놓고 뒤진 산적도 마찬가지다 — 지녔던
        // 것이 죽어야만 생기니 "무엇을 지녔는지 보고 정한다"가 성립하지 않았다.

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

    // ── 일어난다 ─────────────────────────────────────────────────────────────
    //
    // **`BecomeCorpse`의 짝이다.** 저쪽이 끄기만 하고 아무것도 안 지운 덕에(*"`enabled=false`라
    // 되살릴 일이 생기면 그대로 되돌릴 수 있다"*) 여기서 도로 켜면 그 사람이 그대로 선다 —
    // 이름·스탯·특성·차고 있던 장비까지. **그것이 이 기능의 값이다**: 죽은 동료가 내 편이
    // 아닌 것이 되어 내가 입혀 준 갑옷을 입고 걸어온다.
    //
    // **부르는 곳은 `Necromancy` 하나다.** 죽음의 문이 하나면(`Die`) 일어나는 문도 하나여야
    // 한다 — 여기서 되돌리는 것을 부르는 쪽이 각자 챙기면 반드시 하나를 빠뜨린다.
    //
    // 소속은 **일으킨 세력**이 된다. 뼈만 남은 몸은 이 길로 안 온다(모양이 그 사람이 아니다
    // — `Necromancy`가 해골 프리팹을 세운다).
    public bool Rise(FactionData to, float bloodRatio = 1f)
    {
        if (!_isDead || to == null) return false;

        // **시체이기를 그만둔다.** `Corpse`가 남아 있으면 업기·도축·무덤이 산 사람을
        // 시체로 보고(`PawnCarry.IsCorpse`) 업어다 묻는다.
        var corpse = GetComponent<Corpse>();
        if (corpse != null) Destroy(corpse);

        _isDead = false;

        // **역할은 죽음과 함께 끝났다.** 안 지우면 `BuildTree`가 NPC 트리를 골라 일어난
        // 상인이 제자리를 지키고 서 있는다. 장사도 마찬가지라 `Merchant`도 뗀다 —
        // 컴포넌트가 남으면 클릭만으로 죽은 자와 거래가 된다(`Merchant.CanTrade`는
        // 시체인지만 묻는데, 방금 그것을 지웠다).
        npcData = null;
        var shop = GetComponent<Merchant>();
        if (shop != null) Destroy(shop);

        // 세력 — 시체는 컴포넌트째 지워져 있다(`BecomeCorpse`). 다시 붙인다.
        var faction = GetComponent<Faction>();
        if (faction == null) faction = gameObject.AddComponent<Faction>();
        faction.SetData(to);

        // **블랙보드의 캐시도 같이 갈아 끼운다 — 여기가 조용한 함정이다.**
        //
        // `UnitBlackboard.Faction`은 `Awake`에서 **한 번** 잡는 참조인데(매 틱 `GetComponent`를
        // 다시 하지 않으려고 둔 것이다), `BecomeCorpse`는 그 컴포넌트를 **파괴**한다. 그래서
        // 새로 붙이기만 하면 블랙보드는 **파괴된 옛 참조**를 계속 들고 있고, `DetectNode`가
        // `myFaction == null` 가지로 빠져 **적을 훑는 일 자체를 건너뛴다** — 일어난 것이
        // 아무도 안 때리고 그냥 서 있는다(2026-08-19에 그렇게 나타났다).
        //
        // `ChangeFaction`이 이 함정을 안 밟는 것은 **같은 컴포넌트의 값만** 갈기 때문이다
        // (`SetData`). 컴포넌트를 다시 만드는 길은 여기 하나뿐이라 여기서 챙긴다.
        _bb.Faction = faction;

        // 레이어도 새 세력을 따라간다 — `ChangeFaction`과 같은 줄이다. 일으켜 세운 것은
        // 대개 남이 되므로, 안 옮기면 **일어난 마을 사람이 내 폰을 그대로 통과한다.**
        PawnMover.ApplyLayer(gameObject, !UsesEnemyAi);

        // 죽기 전에 들고 있던 것을 끊는다 — `ChangeFaction`과 같은 줄이다.
        _bb.Target       = null;
        _bb.FinishTarget = null;
        _bb.LastAttacker = null;
        _bb.FollowTarget = null;
        _bb.LeavingMap   = false;
        _bb.Routed       = false;
        _bb.IsPrisoner   = false;
        _bb.HoldPosition = false;
        // 일어난 것은 아무것도 필요하지 않다(적 세력은 어차피 이 축이 안 돈다)
        for (int i = 0; i < Needs.Count; i++) _bb.SetNeed((NeedKind)i, Needs.Full);
        ClearStanceOverride();

        // 꺼 둔 것을 도로 켠다 — 끈 순서 그대로.
        enabled = true;
        if (_mover != null) _mover.enabled = true;

        var combat = GetComponent<CombatHandler>();
        if (combat != null) combat.enabled = true;

        var sfx = GetComponent<StatusEffectSystem>();
        if (sfx != null) sfx.enabled = true;

        // 몸 — **켜기만 하면 안 된다.** 혈액이 사망 비율(−30)에 있고 부위가 파괴돼 있어,
        // 그대로 두면 다음 틱에 `CheckBloodState`가 다시 죽인다.
        if (_body != null)
        {
            _body.enabled = true;
            _body.Rise(bloodRatio);
        }

        // BT·성장 잠금은 `ChangeFaction`과 **같은 식**이어야 한다(답이 두 벌이면 어긋난다).
        _root = BuildTree();
        Stats.IsLocked = IsSummoned;

        // **일으켜진 몸은 다시 기절하지 않는다** — 이미 한 번 죽은 것이라 잃을 의식이 없다.
        // 종족이 아니라 여기서 켜는 이유: 좀비의 종족은 여전히 사람이다.
        _risen = true;

        Debug.Log($"[Necromancy] {UnitName}이(가) {to.FactionName}의 것으로 일어났습니다.");
        return true;
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

    // ── 주머니 ───────────────────────────────────────────────────────────────
    //
    // **태어날 때 채운다 — 지녔던 것은 죽어서 생기는 것이 아니다**(2026-08-16).
    //
    // 예전엔 `Loot`를 **죽는 순간** 굴려 시체 배낭에 넣었다. 근거는 *"살아 있는 동안 아무
    // 차이를 안 만드니 미리 지고 다니게 하면 적재 무게·짐내림에 공짜로 걸린다"*였는데,
    // 그 대가로 잃은 것이 훨씬 컸다: **살아 있는 상대의 배낭은 늘 비어 있었다.** 붙잡아 온
    // 포로를 뒤져도, 쓰러뜨려 놓고 뒤져도 아무것도 없고 죽여야만 물건이 생겼다 —
    // *"무엇을 지녔는지 보고 풀어 줄지 정한다"*가 성립할 수가 없었다.
    //
    // 걸린다던 규칙들은 **걸리는 것이 맞다**: 짐을 지면 느려지고, 우리 편이 되면 그것을
    // 창고로 나른다. 둘 다 지고 다니는 물건에 대해 옳은 동작이다.
    //
    // **그래서 주머니에는 "평소 지니고 다닐 만한 것"만 적는다** — 늑대 가죽·고기처럼
    // **몸에서 나오는 것은 종족이 든다**(`UnitData.ButcherYield`/`BoneYield`, 도축으로 나온다).
    // 시점이 갈리는 것이 곧 그 둘을 가르는 축이다: 지니던 것은 죽으면 그 자리에 남고,
    // 몸은 갈라야 나온다.
    void FillPockets()
    {
        if (Inventory == null || loadout == null) return;

        // **동전도 주머니다.** 등급에서 자동 산출하는 길도 있었지만, 그러면 "강한 늑대가
        // 금화를 물고 있다"가 되어 저작으로 막을 방법이 없다.
        var coin = Wallet.Coin;
        if (coin != null) Stow(coin, RollRange(loadout.CoinDropMin, loadout.CoinDropMax));

        if (loadout.Loot == null) return;
        foreach (var entry in loadout.Loot)
        {
            if (entry == null) continue;
            int n = entry.Roll();
            // JSON 아이템은 `Resolve()`로만 나온다.
            if (n > 0) Stow(entry.Resolve(), n);
        }
    }

    // **안 들어가면 떨구지 않고 알린다.** 죽을 때 채우던 시절엔 넘친 것을 발밑에 내려놓았지만,
    // 태어나는 자리는 대개 맵 가장자리라 거기 떨구면 **아무도 안 가는 곳에 물건이 쌓인다.**
    // 못 넣는 것은 저작 실수(장비 잔량이 격자를 다 먹었다)이므로 화면이 아니라 콘솔에 적는다 —
    // 조용히 사라지게 두지 않는 것이 이 저장소의 관례다.
    void Stow(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;

        int left = amount - Inventory.TryAdd(item, amount);
        if (left <= 0) return;

        Debug.LogWarning($"[UnitBrain] {UnitName}: 배낭이 좁아 주머니의 {item.ItemName} " +
                         $"{left}개를 못 넣었습니다(격자 {Inventory.Width}x{Inventory.Height}).", this);
    }

    static int RollRange(int min, int max)
    {
        int lo = Mathf.Max(0, Mathf.Min(min, max));
        int hi = Mathf.Max(0, Mathf.Max(min, max));
        return hi <= 0 ? 0 : UnityEngine.Random.Range(lo, hi + 1);
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

    // ── 하던 일을 놓는다 — 두 갈래다 ─────────────────────────────────────────
    //
    // **큐 일감과 직접 배정은 다른 물건이다.** 일감은 *지금 손에 든 한 건*이고, 자원 노드 배정은
    // **"저 바위는 네 담당"이라는 지속 상태**다. 그래서 잠시 딴 일을 시킬 때 거둘 것은 앞의
    // 하나뿐이다 — 배정까지 거두면 **끝나고 나서 플레이어가 다시 붙여 줘야 한다.**
    //
    // 큐 일감을 안 놓으면 그것대로 사고다: 게이트로 막기만 하면 그 일감이 **예약된 채 얼어붙어
    // 다른 폰도 못 가져간다**(한 명이 멈추는 게 아니라 일감이 통째로 죽는다).

    // 손에 든 일감만 놓는다. **배정은 남는다** — 풀리면 하던 곳으로 돌아간다.
    // (각 Job의 `OnCancelled`가 재료 환불·주문 재등록을 처리한다.)
    public void DropCurrentJob()
    {
        if (_bb?.CurrentJob == null) return;

        JobManager.Instance?.CancelJob(_bb.CurrentJob);
        _bb.CurrentJob = null;
    }

    // 일감도 배정도 거둔다 — **그 자리를 아주 떠날 때**. 사망·직접 이동 명령·원정 집결이 쓴다.
    // (이동 명령에서 배정을 남겨두면 명령한 곳에 도착하자마자 다시 일하러 돌아가 **명령이 안
    // 먹는 것처럼 보인다** — 그쪽은 거두는 것이 맞다.)
    public void AbandonWork()
    {
        if (_bb == null) return;

        DropCurrentJob();
        _bb.AssignedNode?.Unassign(this);
    }

    // ── 명령을 받는가 ────────────────────────────────────────────────────────
    //
    // 묻는 곳이 둘이다(우클릭 `MoveController`, 소집 `DraftController`) — 각자 적으면 한쪽만
    // 하수인을 거르거나 한쪽만 갇힌 폰을 빼서, **명령이 조용히 사라지는 대상**이 생긴다.
    //
    // **둘은 다른 물음이다**: 하수인은 *원래* 명령을 안 받고(그것이 그 종족의 뜻이다), 갇히거나
    // 업힌 폰은 *지금* 못 받을 뿐이다. 부르는 쪽이 그 둘을 갈라 써야 "이 폰은 빠졌습니다"를
    // 알릴지 조용히 건너뛸지 정할 수 있다.
    public bool AcceptsOrders => unitData is not MinionData;

    // **내가 불러낸 것인가**(2026-09-08). 이 물음이 **세 곳에 복사돼 있었다**
    // (`Stats.IsLocked`를 세우는 자리 셋) — 이름을 주고 한 벌로 모은다.
    //
    // **종족만으로는 못 가른다**: 같은 `MinionData` 에셋을 야생 개체도 쓴다(`Enemy_Goblin`과
    // `Minion_Goblin`이 **같은 `Goblin.asset`**이다). 그래서 세력을 같이 봐야 한다 —
    // 이 저장소가 이미 적어 둔 *"`is MinionData`는 '하수인 종족'이지 '내 편'이 아니다"*가
    // 바로 이 자리다.
    //
    // 답하는 것 둘: **성장이 잠기나**(소환물은 안 자란다) · **이력이 있나**(소환된 것에게는
    // 유년기가 없다 — 사용자: *"내가 뽑는 고블린에는 백스토리가 달리는게 이상하지 않아?"*).
    // 둘 다 **종족의 성질이 아니라 어떻게 태어났느냐**의 문제라 같은 물음이다.
    public bool IsSummoned => unitData is MinionData && !UsesEnemyAi;

    // **이 폰의 능동을 사람이 눌러 주나** — 아니면 스스로 써야 한다(`UseAbilityNode`).
    //
    // **`AcceptsOrders` 하나로는 못 가른다**(2026-09-07에 그렇게 짰다가 잡았다). 저건
    // *하수인 종족인가*만 답하는데 **적도 `UnitData`를 쓰므로 참이 된다** — 그것으로 가르면
    // 정작 **적이 능동을 안 쓴다**(만들려던 것이 통째로 안 걸린다). `is MinionData`가
    // *"하수인 종족"이지 "내 편"이 아닌* 것과 **같은 함정**이다.
    //
    // **답은 실제로 사람이 누르는 경로에서 나온다** — `SelectionManager`가 **플레이어 세력만**
    // 고르고, `AbilityController`가 거기서 **명령을 받는 폰만** 남긴다. 그 둘을 이어 붙인 것이
    // 이 물음이고, 그래서 **누르는 쪽과 스스로 쓰는 쪽이 같은 줄을 본다.**
    //
    // 딸려 오는 것 하나: **용병은 스스로 쓴다**(`Faction_Mercenary`는 플레이어 세력이 아니라
    // 고를 수가 없다). 곁에서 알아서 싸우는 것이 그 계약의 뜻이라 그게 맞다.
    public bool AbilityByHand
    {
        get
        {
            if (!AcceptsOrders) return false;
            var f = Blackboard != null ? Blackboard.Faction : null;
            return f != null && f.IsPlayer;   // **모르는 것은 플레이어가 아니다**
        }
    }

    // **도발당한 폰은 명령을 안 듣는다**(2026-09-08, 사용자 결정: *"도발에 걸렸는데 움직이면
    // 안 되지 그냥 내 명령을 들으면 안 돼"*).
    //
    // **`CanObeyNow`에 얹지 않는다** — 그건 `PawnMover.SetMoveTarget`도 보는 물음이라, 거기
    // 걸리면 **BT가 도발한 놈에게 걸어갈 수도 없어져 도발이 통째로 죽는다.** 갈리는 자리가
    // 정확히 이것이다: **사람의 명령은 막고, 트리의 이동은 안 막는다.**
    //
    // 묻는 곳은 둘이다 — `ManualOrderNode`(붙들려 있으면 명령 갈래를 건너뛴다)와
    // `MoveController`(왜 안 되는지 알린다). **조용히 씹으면 조작이 고장 난 것으로 보인다.**
    public bool IsHeldByTaunt => Taunt.HolderOf(Blackboard) != null;

    public bool CanObeyNow
    {
        get
        {
            var bb = Blackboard;
            return bb == null || (!bb.IsPrisoner && bb.CarriedBy == null);
        }
    }

    // ── 소집(Draft) ──────────────────────────────────────────────────────────
    //
    // **일을 놓고 싸울 태세로 선다.** 림월드의 징집이고, 이 게임에서 정확히 뜻하는 것은 셋이다:
    //
    //   ① **자동 행동을 멈춘다** — 일감을 안 집고(`WorkNode`) 눕지 않는다(`RestNode`).
    //      **배정은 남는다**(자원 노드·침대) — 소집을 풀면 캐던 바위로, 제 침대로 돌아간다.
    //      **밥은 그대로 먹는다** — 막으면 소집해 둔 것을 잊었을 때 굶어 죽는다(업고 있을 때
    //      먹는 것만 열어 둔 그 판단이다). 피로는 안 죽으므로 막아도 사고가 안 난다.
    //   ② **먼저 친다** — 평소 방랑자는 반격만 하는데(`DetectNode(retaliateOnly)`) 소집 중에는
    //      다가온 적을 저 혼자 문다. **목줄은 짧다**(공격 사거리 ×3) — 자리를 지키며 싸우라는
    //      뜻이지 표적을 찾아 나서라는 뜻이 아니다.
    //   ③ **도망치지 않는다** — `PlayerFleeNode`가 이미 `!bb.IsDrafted`를 본다.
    //
    // 명령(이동·공격·업기)은 **그대로 받는다.** 한동안 트리 맨 위에 `DraftedNode` 분기가 있어
    // `Running`만 돌려줬는데, 그러면 소집한 순간 **이동조차 안 먹었다** — 그래서 부르는 입력도
    // 안 붙어 있었다(2026-08-20에 게이트로 바꾸면서 그 분기를 지웠다).
    //
    // **하던 일감은 여기서 놓는다**(`DropCurrentJob`). `WorkNode` 게이트만 걸면 그 일감이
    // 예약된 채 얼어붙어 **다른 폰도 못 가져간다** — 업기가 `TryPickUp`에서 놓는 것과 같은 이유다.
    // **다만 배정(자원 노드·침대)은 남긴다** — 풀면 캐던 바위로, 제 침대로 돌아간다.
    public void Draft()
    {
        if (_bb == null || _bb.IsDrafted) return;

        // **일감만 놓고 배정은 남긴다**(`DropCurrentJob`). 소집은 *잠시* 딴 일을 시키는 것이지
        // 담당을 떼는 일이 아니다 — 배정까지 거두면 소집을 풀 때마다 캐던 바위·베던 나무를
        // 손으로 다시 붙여 줘야 한다. 침대 배정을 안 건드리는 것과 **같은 이유**다.
        _bb.IsDrafted = true;
        DropCurrentJob();

        // **선 자리가 곧 자리다.** 이동 명령을 받으면 진형이 덮어쓰지만, 그 전에도
        // `FightNode`가 볼 값이 있어야 한다 — 없으면(0,0,0) 소집하자마자 표적을 문 폰이
        // **세계 원점을 기준으로 목줄을 재서** 그 자리에서 아예 못 움직인다.
        _bb.FormationSpot = transform.position;

        // **침대는 안 건드린다.** 자던 폰을 일으키는 것은 `RestNode`의 게이트가 이미 한다
        // (`bb.IsDrafted`면 `LeaveBed` — 업고 있을 때와 **같은 줄**이고, 그건 자세와 **점유만**
        // 풀고 **배정은 남긴다**).
        //
        // 여기서 `Bed.ReleaseFrom`을 부르면 **배정까지 거둔다** — 소집을 풀고 나서 플레이어가
        // 침대를 다시 붙여 줘야 하고, 그 사이 다른 폰이 그 침대를 가져간다. 소집은 *잠시*
        // 다른 일을 시키는 것이지 **잠자리를 빼앗는 일이 아니다**(업기가 *"배정은 안 거둔다 —
        // 미루기만 하므로 내려놓는 즉시 제 침대로 간다"*고 판단한 그 자리다).
    }

    public void Undraft()
    {
        if (_bb == null) return;
        _bb.IsDrafted = false;
    }

    public bool IsDrafted => _bb != null && _bb.IsDrafted;

    // ── 세력을 바꾼다 (회유) ─────────────────────────────────────────────────
    //
    // **세력에 딸린 것이 넷인데 전부 `Awake`에서 한 번 정해진다.** `Faction.Data`만 갈아
    // 끼우면 **적 트리를 그대로 든 아군**이 된다 — 우리 폰을 적으로 알고 덤빈다.
    // 그래서 세력을 바꾸는 길은 여기 하나뿐이고, 넷을 같이 다시 세운다:
    //
    //  ① **BT** — 적 트리 → (종족에 따라) 하수인/방랑자 트리
    //  ② **성장 잠금** — 하수인 종족이 우리 편이 되면 잠긴다(소환물과 같은 규칙)
    //  ③ **작업 설정** — 플레이어 세력 일꾼만 갖는다. 없으면 만들어 붙인다
    //  ④ **허기** — `UpdateHunger`가 플레이어 세력만 도므로 저절로 시작된다
    //
    // **종족이 무엇이 될지를 정한다**(2026-08-12 결정): 하수인 종족(`MinionData`)이면
    // 회유해도 하수인이다 — 명령 못 받고, 안 자라고, 소환 한도를 차지한다. 사람 종족이면
    // 방랑자가 된다. 그래서 "누구를 붙잡을 것인가"에 뜻이 생긴다.
    public void ChangeFaction(FactionData to)
    {
        var faction = GetComponent<Faction>();
        if (faction == null || to == null || faction.Data == to) return;

        faction.SetData(to);

        // 하던 것을 끊는다 — 적으로서 하던 표적·추격을 들고 넘어오면 안 된다.
        _bb.Target       = null;
        _bb.FinishTarget = null;
        _bb.LastAttacker = null;
        _bb.FollowTarget = null;
        _bb.LeavingMap   = false;
        _bb.Routed       = false;
        ClearStanceOverride();
        GetComponent<CombatHandler>()?.ExitCombat();
        _mover?.StopMovement();

        // 떼의 레일도 내린다 — 떼를 떠난 폰이 몸 없이 걸으면 명령받은 이동이 벽·폰을 뚫는다(`PawnMover.OnRails`).
        if (_mover != null) _mover.OnRails = false;

        // ① BT — `UsesEnemyAi`가 세력을 보므로 다시 지으면 알아서 갈린다.
        _root = BuildTree();

        // ①-b 레이어 — **몸이 누구를 통과하는지**가 여기서 갈린다(`PawnMover.ApplyLayer`).
        // 안 옮기면 회유해 온 사람이 내 폰과 부딪히고, 길찾기는 같은 레이어라 서로를
        // 못 봐서 **밀고 선다.** 세력을 바꾸는 문이 여기 하나이므로 여기서 같이 옮긴다.
        PawnMover.ApplyLayer(gameObject, !UsesEnemyAi);

        // ② 성장 잠금 — `Awake`와 **같은 식**이어야 한다(답이 두 벌이면 어긋난다).
        Stats.IsLocked = IsSummoned;

        // ③ 작업 설정 — 적일 땐 안 붙어 있었다. 붙이면서 짐내림 신호도 같이 건다.
        if (!UsesEnemyAi && npcData == null && Work == null)
        {
            Work = gameObject.AddComponent<WorkSettings>();
            if (unitData is MinionData minionData) Work.InitFromAllowed(minionData.AllowedJobs);
            HookUnloadSignals();

            // **넘어온 사람도 제 배경을 갖는다.** 방랑자가 되는 순간이므로 여기서 얹는다 —
            // 태어날 때는 적이라 `SetBackstories`가 물러났다(방랑자 전용).
            SetBackstories(_rolledChildhood, _rolledAdulthood);
        }

        Debug.Log($"[세력] {UnitName} → {to.FactionName} " +
                  $"({(unitData is MinionData ? "하수인" : "방랑자")}, 성장잠금 {Stats.IsLocked})");

        // **알린다.** 명부·원정 편성은 `OnUnitSpawned`만 듣고 있었는데 그건 `Start`에서 한 번
        // 뜨고 마는 것이라, **이 문으로 들어온 식구는 화면에 영영 안 나타났다.**
        OnFactionChanged?.Invoke(this);
    }

    // ── 기절하지 않는 몸 ─────────────────────────────────────────────────────
    //
    // **"이 폰이 언데드인가"에 답하는 자리 하나.** 둘에서 온다:
    //  ① **종족**(`UnitData.IsUndead` — 해골). 내가 소환한 것이든 야생의 것이든 같다.
    //  ② **일으켜진 몸**(`Rise`). 좀비의 종족은 여전히 사람이라 ①로는 안 걸린다.
    //
    // 묻는 곳이 `BodyPartSystem.KnockOut` 하나뿐인데도 프로퍼티로 둔 이유: 출처가 둘이라
    // 부르는 쪽에서 `unitData.IsUndead || 어딘가의 플래그`를 적기 시작하면 반드시 한쪽이
    // 빠진다(이 저장소가 여러 번 겪은 그것).
    public bool IsUndead => (unitData != null && unitData.IsUndead) || _risen;

    // 일으켜진 몸인가. `Rise`에서만 켜지고 다시 안 꺼진다 — 되돌리는 길은 없다.
    bool _risen;

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