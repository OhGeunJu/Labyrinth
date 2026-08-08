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

        // **건물 자리가 아니라 폰이 서는 자리다.** 전에는 청사진 중심 칸을 그대로 목표로 삼았는데,
        // 청사진은 칸을 안 막으므로 그 칸에 설 수 있었다 — 폰이 3×2 건물 **한복판**에 서서 짓고,
        // 완성되는 순간 발밑이 통행 불가가 되어 그대로 갇혔다.
        SetSite(blueprint);
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

        // 창고도 자기 칸은 통행 불가다 — `WorkSpot`이 그 앞의 설 수 있는 칸을 준다.
        TileCoord = WorkSpot.TileFor(_sourceStorage);
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
            TileCoord = WorkSpot.TileFor(_blueprint);   // 발자국 밖 — 다 지어져도 갇히지 않는다
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
