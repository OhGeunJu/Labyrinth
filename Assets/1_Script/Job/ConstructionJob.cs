using UnityEngine;

// 창고에서 자재 수거 → 설계도에 납품하는 2-phase 작업.
public class ConstructionJob : JobBase
{
    public override JobCategory Category => JobCategory.Construction;

    enum Phase { GoToStorage, GoToBlueprint }

    readonly BuildingBlueprint _blueprint;
    readonly ItemData          _material;
    int             _amount;
    Storage _sourceStorage;
    int             _carriedAmount;
    Phase           _phase = Phase.GoToStorage;

    public ConstructionJob(BuildingBlueprint blueprint, ItemData material, int amount)
    {
        _blueprint = blueprint;
        _material  = material;
        _amount    = amount;
        Priority   = 3;

        if (blueprint != null && MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(blueprint.transform.position);
    }

    protected override void OnStart()
    {
        if (_blueprint == null || _material == null) { Fail(); return; }

        _sourceStorage = Storage.FindStorageWithItem(_material);
        if (_sourceStorage == null)
        {
            Debug.Log($"[ConstructionJob] 창고에 {_material.ItemName} 없음 — 대기");
            Fail();
            return;
        }

        TileCoord = MapManager.Instance.WorldToTile(_sourceStorage.transform.position);
        Debug.Log($"[ConstructionJob] 건설 자재 수거 시작 — {_material.ItemName} ×{_amount}");
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_blueprint == null) { Fail(); return; }

        if (_phase == Phase.GoToStorage)
        {
            int available = _sourceStorage.Inventory.GetTotalQuantity(_material);
            int toTake    = Mathf.Min(_amount, available);
            if (toTake <= 0) { Fail(); return; }

            _sourceStorage.Inventory.Remove(_material, toTake);
            unit.Inventory.TryAdd(_material, toTake);
            _carriedAmount = toTake;

            _phase    = Phase.GoToBlueprint;
            TileCoord = MapManager.Instance.WorldToTile(_blueprint.transform.position);
        }
        else
        {
            unit.Inventory.Remove(_material, _carriedAmount);
            _blueprint.Deliver(_material, _carriedAmount);
            _blueprint.OnJobEnded();
            Status = JobStatus.Done;
        }
    }

    protected override void OnCancelled() => _blueprint?.OnJobEnded();

    void Fail()
    {
        _blueprint?.OnJobEnded();
        Status = JobStatus.Done;
    }
}
