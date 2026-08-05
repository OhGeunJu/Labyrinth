using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CraftingTable : MonoBehaviour, IWorldClickable, IHaulSource
{
    [Serializable]
    public struct FuelEntry
    {
        public ItemData Item;
        [Tooltip("아이템 1개당 제공하는 연료량")]
        public float FuelValue;
    }

    [SerializeField] CraftSkill tableType = CraftSkill.WeaponCrafting;

    [Header("연료 (용광로 등)")]
    [SerializeField] bool            _requiresFuel;
    [SerializeField] float           _fuelCapacity  = 100f;
    [Tooltip("연료 소모 속도 (단위/초). 용광로가 켜져있는 동안 항상 소모.")]
    [SerializeField] float           _fuelBurnRate  = 2f;
    [SerializeField] List<FuelEntry> _acceptedFuels = new();

    [Header("산출물")]
    [Tooltip("완성품을 폰이 창고로 실어 가게 할지. 끄면 산출물 칸에 쌓아 둔다 " +
             "(작업대 옆에서 바로 집어 쓰거나, 창고를 채우고 싶지 않을 때).")]
    [SerializeField] bool allowHaulOut = true;

    [Tooltip("산출물 칸 크기(가로×세로). 폰이 실어 갈 때까지 버티는 완충 자리라 창고만큼 클 필요는 없다.")]
    [SerializeField] Vector2Int outputSize = new Vector2Int(8, 3);

    float _fuelAmount;
    bool  _fuelJobPending;
    float _fuelCheckTimer;

    HaulDispatcher _haul;
    float _haulCheckTimer;

    const float FuelCheckInterval   = 3f;
    const float FuelRefillThreshold = 0.3f;
    const float HaulCheckInterval   = 2f;

    public CraftSkill TableType => tableType;

    // ── 산출물 (IHaulSource) ─────────────────────────────────────────────────
    // 완성품이 여기 쌓인다. **폰 인벤토리에 바로 넣으면** 그 폰이 물건을 들고 돌아다니게 되고,
    // 넘치는 만큼은 바닥에 떨어져 어디로 갔는지 알 수 없다 — 작업대에 자리를 주는 편이 낫다.
    public Inventory Inventory { get; private set; }

    public bool AllowHaulOut => allowHaulOut;

    // ── 진행 중인 제작 (UI 게이지용) ─────────────────────────────────────────
    // `ResearchTable.SetActiveJob`과 같은 모양 — 폰이 작업대에 붙어 타이머가 도는 동안만 잡힌다.
    CraftJob _activeJob;

    public RecipeData ActiveRecipe  => _activeJob != null && _activeJob.IsRunning ? _activeJob.Recipe : null;
    public float      ActiveProgress => _activeJob != null && _activeJob.IsRunning ? _activeJob.Progress : 0f;

    public void SetActiveJob(CraftJob job) => _activeJob = job;

    public void ClearActiveJob(CraftJob job)
    {
        if (_activeJob == job) _activeJob = null;
    }

    // ── 지정 제작자 ──────────────────────────────────────────────────────────
    // 레시피별로 "이 폰만 만든다"를 정할 수 있다. 비우면 아무나(기본).
    // 등급 있는 장비는 폰의 스킬이 결과 등급을 정하므로, 좋은 대장장이에게 몰아주고 싶어진다.
    readonly Dictionary<RecipeData, UnitBrain> _assigned = new();

    public UnitBrain GetAssignedCrafter(RecipeData recipe)
    {
        if (recipe == null) return null;
        // 죽어서 파괴된 폰이 남아 있으면 그 레시피가 영영 막힌다 — 그때는 지정을 푼다.
        if (_assigned.TryGetValue(recipe, out var u) && u != null) return u;
        _assigned.Remove(recipe);
        return null;
    }

    public void SetAssignedCrafter(RecipeData recipe, UnitBrain unit)
    {
        if (recipe == null) return;
        if (unit == null) _assigned.Remove(recipe);
        else              _assigned[recipe] = unit;
        OnChanged?.Invoke(this);
    }

    // ── 부분 진행 ────────────────────────────────────────────────────────────
    // **폰이 사라져도 만들던 것이 사라지지 않는다.** 전에는 취소될 때마다 재료를 되돌리고
    // 진행을 0으로 버렸다 — 긴 제작은 한 번도 안 끊길 수가 없어서 영영 못 끝냈다.
    //
    // `Owner`는 **등급 있는 장비**일 때만 잡힌다. 등급은 만든 폰의 스킬로 정해지므로
    // 중간에 다른 폰이 이어받으면 누구의 솜씨인지가 뒤섞인다.
    class Partial
    {
        public float     Seconds;
        public UnitBrain Owner;
    }

    readonly Dictionary<RecipeData, Partial> _partial = new();

    public float GetPartialSeconds(RecipeData recipe) =>
        recipe != null && _partial.TryGetValue(recipe, out var p) ? p.Seconds : 0f;

    public UnitBrain GetPartialOwner(RecipeData recipe)
    {
        if (recipe == null || !_partial.TryGetValue(recipe, out var p)) return null;
        if (p.Owner != null) return p.Owner;

        // 만들던 폰이 죽었다. 잠긴 채로 두면 아무도 못 끝내므로 **진행을 버리고 처음부터** 한다.
        _partial.Remove(recipe);
        return null;
    }

    public void SavePartial(RecipeData recipe, float seconds, UnitBrain owner)
    {
        if (recipe == null || seconds <= 0f) return;
        _partial[recipe] = new Partial { Seconds = seconds, Owner = owner };
        OnChanged?.Invoke(this);
    }

    public void ClearPartial(RecipeData recipe)
    {
        if (recipe != null && _partial.Remove(recipe)) OnChanged?.Invoke(this);
    }

    // ── 대기 중인 주문 수 ────────────────────────────────────────────────────
    // 연료는 **주문이 걸려 있을 때만** 채운다. 안 그러면 아무도 안 쓰는 용광로가
    // 석탄을 계속 빨아들인다(연료는 시간에 따라 타 없어지므로 보충이 끝없이 등록된다).
    //
    // **레시피별로 센다.** 합계 하나만 두면 "이 레시피를 몇 개 더 걸 수 있나"를 물었을 때
    // 상관없는 주문까지 깎여 나온다(빵 주문 3개가 철검 수량에서 빠지는 식).
    readonly Dictionary<RecipeData, int> _pendingByRecipe = new();
    int _pendingOrders;

    public bool HasPendingOrder => _pendingOrders > 0;

    public int PendingFor(RecipeData recipe) =>
        recipe != null && _pendingByRecipe.TryGetValue(recipe, out int n) ? n : 0;

    public void OrderFinished(RecipeData recipe)
    {
        _pendingOrders = Mathf.Max(0, _pendingOrders - 1);

        if (recipe == null || !_pendingByRecipe.TryGetValue(recipe, out int n)) return;
        if (n <= 1) _pendingByRecipe.Remove(recipe);
        else        _pendingByRecipe[recipe] = n - 1;
    }

    public void SetAllowHaulOut(bool value)
    {
        if (allowHaulOut == value) return;
        allowHaulOut = value;
        _haulCheckTimer = 0f;            // 켜면 다음 프레임에 바로 등록되게
        OnChanged?.Invoke(this);
    }

    public Vector2Int TileCoord =>
        MapManager.Instance != null ? MapManager.Instance.WorldToTile(transform.position) : default;

    // 연료·산출물이 바뀌면 열려 있는 패널이 따라 갱신되도록.
    public static event Action<CraftingTable> OnChanged;

    void Awake()
    {
        // 프리팹이 이미 Inventory를 들고 있으면 그 크기를 존중한다 — 우리가 만든 것만 크기를 잡는다.
        var existing = GetComponent<Inventory>();
        Inventory = existing ?? gameObject.AddComponent<Inventory>();
        if (existing == null) Inventory.Resize(outputSize.x, outputSize.y);

        Inventory.OnChanged += () => OnChanged?.Invoke(this);
        _haul = new HaulDispatcher(this);
    }

    public bool  RequiresFuel  => _requiresFuel;
    public float FuelAmount    => _fuelAmount;
    public float FuelCapacity  => _fuelCapacity;
    public bool  NeedsFuel     => _requiresFuel && _fuelAmount < _fuelCapacity * FuelRefillThreshold;
    public bool  HasFuel       => !_requiresFuel || _fuelAmount > 0f;
    public IReadOnlyList<FuelEntry> AcceptedFuels => _acceptedFuels;

    public void AddFuel(float amount)
    {
        _fuelAmount = Mathf.Min(_fuelAmount + amount, _fuelCapacity);
        OnChanged?.Invoke(this);
    }

    public void OnFuelJobComplete() => _fuelJobPending = false;

    public IReadOnlyList<RecipeData> Recipes =>
        new List<RecipeData>(DataManager.GetRecipesBySkill(tableType));

    public static event Action<CraftingTable> OnSelected;

    void Update()
    {
        UpdateFuel();
        UpdateHaulOut();
    }

    // 산출물을 창고로 내보낸다. 발행은 `HaulDispatcher`가 맡는다 — 쌓인 재고를 몫으로 나눠
    // 내므로 종류가 여럿이든 한 종류가 많이 쌓였든 여러 폰이 나눠 실어 간다.
    // (전에는 `_haulJobPending` 하나로 "동시에 한 건, 한 종류"라 운반이 늘 한 명이었다.)
    void UpdateHaulOut()
    {
        if (!allowHaulOut || Inventory == null) return;

        _haulCheckTimer -= Time.deltaTime;
        if (_haulCheckTimer > 0f) return;
        _haulCheckTimer = HaulCheckInterval;

        if (Inventory.Items.Count == 0 || JobManager.Instance == null) return;
        if (BuildingDormancy.IsDormantAt(this)) return;   // 휴면 중엔 일감을 만들지 않는다

        // 완성품은 한 개도 놀려 두지 않는다 — 임계값 1.
        _haul.Dispatch(1);
    }

    // SelectionManager의 단일 레이캐스트가 이 작업대를 클릭 판정 시 호출.
    public void OnWorldClick() => OnSelected?.Invoke(this);

    void UpdateFuel()
    {
        if (!_requiresFuel) return;

        // 시간에 따라 연료 소모
        if (_fuelAmount > 0f)
            _fuelAmount = Mathf.Max(0f, _fuelAmount - _fuelBurnRate * Time.deltaTime);

        // 주기적으로 연료 보충 작업 등록
        _fuelCheckTimer -= Time.deltaTime;
        if (_fuelCheckTimer > 0f) return;
        _fuelCheckTimer = FuelCheckInterval;

        // **주문이 걸려 있을 때만 채운다.** 노는 용광로가 석탄을 계속 먹으면
        // 플레이어가 쓰지도 않는 시설에 자원을 빼앗긴다.
        if (HasPendingOrder && NeedsFuel && !_fuelJobPending && JobManager.Instance != null)
            TryRegisterFuelJob();
    }

    void TryRegisterFuelJob()
    {
        foreach (var entry in _acceptedFuels)
        {
            if (entry.Item == null) continue;
            if (Storage.GetTotalByItem(entry.Item) <= 0) continue;
            _fuelJobPending = true;
            JobManager.Instance.RegisterJob(new FuelJob(this, entry));
            return;
        }
    }

    // 재료가 하나도 걸리지 않는 레시피를 한 번에 걸 수 있는 수. 상한이 필요한 이유는
    // `MaxCraftable` 주석 참조.
    const int UnboundedBatch = 99;

    // 재료가 **지금 창고에 있는 것만으로** 몇 개 더 걸 수 있나. 수량 칸의 상한이다.
    //
    // 주문을 걸어도 재료는 폰이 실제로 집을 때 빠진다. 그래서 창고 재고만 보면 [최대]를 두 번
    // 눌렀을 때 재고가 그대로라 두 배가 걸린다 — **이 레시피로 이미 걸어 둔 주문 수**를 뺀다.
    public int MaxCraftable(RecipeData recipe)
    {
        if (recipe == null) return 0;

        int max = int.MaxValue;
        foreach (var ing in recipe.Ingredients)
        {
            if (ing.Item == null || ing.Amount <= 0) continue;
            max = Mathf.Min(max, Storage.GetTotalByItem(ing.Item) / ing.Amount);
        }

        // **셀 재료가 없는 레시피는 "못 만든다"가 아니라 "무엇도 막지 않는다"이다.**
        // 0으로 두면 `CanCraft`(재료를 다 통과)와 답이 갈려 버튼이 영구히 잠긴다 —
        // 지금 에셋엔 그런 레시피가 없지만 JSON·모드가 넣으면 조용히 안 만들어진다.
        // int.MaxValue를 그대로 흘리면 화면에 `최대 2147483647`이 뜨므로 한 번에 걸 만한
        // 수로 자른다(수량 칸의 상한일 뿐, 여러 번 누르는 것은 막지 않는다).
        if (max == int.MaxValue) max = UnboundedBatch;

        return Mathf.Max(0, max - PendingFor(recipe));
    }

    public void QueueOrder(RecipeData recipe) => QueueOrder(recipe, 1);

    // `count`만큼 한 번에 건다. 열 개를 만들려고 버튼을 열 번 누르게 하지 않는다.
    public void QueueOrder(RecipeData recipe, int count)
    {
        if (recipe == null || JobManager.Instance == null) return;
        // 휴면(핵 영역 밖) 작업대에는 주문을 넣을 수 없다 — UI 경로가 막혀도 직접 호출 방어.
        if (BuildingDormancy.IsDormantAt(this)) { Debug.Log("[CraftingTable] 휴면 중 — 주문 불가"); return; }

        count = Mathf.Max(1, count);
        for (int i = 0; i < count; i++)
        {
            _pendingOrders++;
            _pendingByRecipe.TryGetValue(recipe, out int n);
            _pendingByRecipe[recipe] = n + 1;
            JobManager.Instance.RegisterJob(new CraftJob(this, recipe));
        }

        // 주문이 들어온 순간 연료부터 챙긴다. 주기 검사(3초)를 기다리면 그만큼 늦고,
        // 제작 주문은 **연료가 있어야 배정된다**(`CraftJob.IsReady`) — 보충이 먼저 걸려야 한다.
        RequestFuelNow();
    }

    // 연료 보충 작업을 지금 등록해 본다(주기 검사를 기다리지 않는다).
    public void RequestFuelNow()
    {
        _fuelCheckTimer = 0f;
        if (HasPendingOrder && NeedsFuel && !_fuelJobPending && JobManager.Instance != null)
            TryRegisterFuelJob();
    }

    // **재료는 창고에 있어야 주문할 수 있다.** 배낭은 잠시 들고 있는 자리지 창고가 아니다.
    //
    // 한때 `MaterialPool.AvailableAnywhere`(모든 폰 배낭 포함)로 넓게 봤는데, 그러면
    // 화면은 `재료 3/3`이라 하면서 **정작 만들 수 있는 건 그걸 든 폰 하나**가 된다
    // (`CraftJob.CanAssignTo`는 남의 배낭을 안 센다 — 조작한 적 없는 폰의 짐이 줄면 안 되니까).
    // 그 폰이 광맥에 배정돼 있으면 큐를 아예 안 보므로 주문은 영영 안 걸린다 —
    // 화면은 된다는데 아무 일도 안 일어난다.
    //
    // 배낭에 든 재료는 **짐 내리기**로 창고에 들어와야 쓰인다. 그래야 보이는 수량과
    // 실제로 만들 수 있는 양이 같아진다.
    public bool CanCraft(RecipeData recipe)
    {
        if (recipe == null) return false;
        foreach (var ing in recipe.Ingredients)
            if (Storage.GetTotalByItem(ing.Item) < ing.Amount)
                return false;
        return true;
    }
}
