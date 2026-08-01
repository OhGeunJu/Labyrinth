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

    bool  _haulJobPending;
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
    int _pendingOrders;
    public bool HasPendingOrder => _pendingOrders > 0;

    public void OrderFinished() => _pendingOrders = Mathf.Max(0, _pendingOrders - 1);

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

    // 산출물을 창고로 내보낸다. **한 번에 한 종류씩** — 폰 하나가 다녀오는 동안
    // 같은 짐에 대해 잡이 중복 등록되지 않게 `_haulJobPending`으로 막는다(연료와 같은 방식).
    void UpdateHaulOut()
    {
        if (!allowHaulOut || Inventory == null || _haulJobPending) return;

        _haulCheckTimer -= Time.deltaTime;
        if (_haulCheckTimer > 0f) return;
        _haulCheckTimer = HaulCheckInterval;

        if (Inventory.Items.Count == 0 || JobManager.Instance == null) return;
        if (BuildingDormancy.IsDormantAt(this)) return;   // 휴면 중엔 일감을 만들지 않는다

        var item = Inventory.Items[0].Data;
        if (item == null) return;

        var storage = Storage.FindNearest(transform.position, item);
        if (storage == null) return;      // 받아 줄 창고가 없으면 그대로 쌓아 둔다

        _haulJobPending = true;
        JobManager.Instance.RegisterJob(
            new HaulJob(this, item, Inventory.GetTotalQuantity(item), storage,
                        () => _haulJobPending = false));
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

    public void QueueOrder(RecipeData recipe)
    {
        if (recipe == null || JobManager.Instance == null) return;
        // 휴면(핵 영역 밖) 작업대에는 주문을 넣을 수 없다 — UI 경로가 막혀도 직접 호출 방어.
        if (BuildingDormancy.IsDormantAt(this)) { Debug.Log("[CraftingTable] 휴면 중 — 주문 불가"); return; }
        _pendingOrders++;
        JobManager.Instance.RegisterJob(new CraftJob(this, recipe));

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

    public bool CanCraft(RecipeData recipe)
    {
        if (recipe == null) return false;
        foreach (var ing in recipe.Ingredients)
            if (Storage.GetTotalByItem(ing.Item) < ing.Amount)
                return false;
        return true;
    }
}
