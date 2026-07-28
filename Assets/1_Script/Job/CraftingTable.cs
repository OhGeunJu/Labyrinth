using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CraftingTable : MonoBehaviour, IWorldClickable
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

    float _fuelAmount;
    bool  _fuelJobPending;
    float _fuelCheckTimer;

    const float FuelCheckInterval   = 3f;
    const float FuelRefillThreshold = 0.3f;

    public CraftSkill TableType => tableType;

    public bool  RequiresFuel  => _requiresFuel;
    public float FuelAmount    => _fuelAmount;
    public float FuelCapacity  => _fuelCapacity;
    public bool  NeedsFuel     => _requiresFuel && _fuelAmount < _fuelCapacity * FuelRefillThreshold;
    public bool  HasFuel       => !_requiresFuel || _fuelAmount > 0f;
    public IReadOnlyList<FuelEntry> AcceptedFuels => _acceptedFuels;

    public void AddFuel(float amount)
    {
        _fuelAmount = Mathf.Min(_fuelAmount + amount, _fuelCapacity);
    }

    public void OnFuelJobComplete() => _fuelJobPending = false;

    public IReadOnlyList<RecipeData> Recipes =>
        new List<RecipeData>(DataManager.GetRecipesBySkill(tableType));

    public static event Action<CraftingTable> OnSelected;

    void Update()
    {
        UpdateFuel();
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

        if (NeedsFuel && !_fuelJobPending && JobManager.Instance != null)
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
        JobManager.Instance.RegisterJob(new CraftJob(this, recipe));
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
