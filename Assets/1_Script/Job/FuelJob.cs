using UnityEngine;

// 창고에서 연료 아이템을 가져와 용광로(CraftingTable)에 보충하는 작업.
public class FuelJob : JobBase
{
    public override JobCategory Category => JobCategory.Crafting;

    enum Phase { GoToStorage, GoToFurnace }

    readonly CraftingTable            _table;
    readonly CraftingTable.FuelEntry  _fuel;

    Storage _sourceStorage;
    int     _carryAmount;
    Phase   _phase;

    public FuelJob(CraftingTable table, CraftingTable.FuelEntry fuel)
    {
        _table    = table;
        _fuel     = fuel;
        Priority  = 3;
        SetSite(table);   // 목표 칸(WorkSpot)도 여기서 정해진다
    }

    // **넣고 꺼내기만 하는 일이라 손만 닿으면 된다** — 창고에서 석탄을 집는 것도, 용광로
    // 아궁이에 붓는 것도 그 자리에 서서 하는 작업이 아니다(제작은 `CraftJob`이 따로 한다).
    public override float Reach => HandReach;

    protected override void OnStart()
    {
        _sourceStorage = null;
        foreach (var sb in Storage.All)
        {
            if (sb.Inventory.GetTotalQuantity(_fuel.Item) > 0)
            { _sourceStorage = sb; break; }
        }

        if (_sourceStorage == null)
        {
            Debug.Log($"[FuelJob] 연료 없음 — {_fuel.Item.ItemName}을 창고에 보충하세요");
            _table.OnFuelJobComplete();
            Status = JobStatus.Done;
            return;
        }

        // 창고 자기 칸이 아니라 **폰이 서는 자리**(`WorkSpot`) — 창고는 칸을 막으므로
        // 자기 칸은 설 수 없는 자리다.
        _phase    = Phase.GoToStorage;
        TileCoord = WorkSpot.TileFor(_sourceStorage);
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_table == null || _fuel.Item == null) { Cleanup(); return; }

        if (_phase == Phase.GoToStorage)
        {
            int need = Mathf.CeilToInt((_table.FuelCapacity - _table.FuelAmount) / _fuel.FuelValue);
            need = Mathf.Clamp(need, 1, 20);

            int available = _sourceStorage != null ? _sourceStorage.Inventory.GetTotalQuantity(_fuel.Item) : 0;
            int toTake    = Mathf.Min(need, available);

            if (toTake <= 0) { Cleanup(); return; }

            _sourceStorage.Inventory.Remove(_fuel.Item, toTake);
            unit.Inventory.TryAdd(_fuel.Item, toTake);
            _carryAmount = toTake;

            // **작업대 칸은 원래부터 못 서는 자리다**(`OccupiesTile: true`). 그런데도 여기서
            // 자기 칸을 겨누고 있었다 — 도착 판정이 늘 건물 한복판까지의 거리로 재어졌다는 뜻이다.
            _phase    = Phase.GoToFurnace;
            TileCoord = WorkSpot.TileFor(_table);
        }
        else
        {
            int removed = unit.Inventory.Remove(_fuel.Item, _carryAmount);
            _table.AddFuel(removed * _fuel.FuelValue);
            Debug.Log($"[FuelJob] 연료 보충 — {_fuel.Item.ItemName} ×{removed} (+{removed * _fuel.FuelValue:F0})  잔량: {_table.FuelAmount:F0}/{_table.FuelCapacity:F0}");
            Cleanup();
        }
    }

    protected override void OnCancelled() => _table?.OnFuelJobComplete();

    void Cleanup()
    {
        _table?.OnFuelJobComplete();
        Status = JobStatus.Done;
    }
}
