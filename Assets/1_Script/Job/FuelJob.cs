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
        TileCoord = MapManager.Instance != null
            ? MapManager.Instance.WorldToTile(table.transform.position)
            : Vector2Int.zero;
    }

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

        _phase    = Phase.GoToStorage;
        TileCoord = MapManager.Instance != null
            ? MapManager.Instance.WorldToTile(_sourceStorage.transform.position)
            : Vector2Int.zero;
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

            _phase    = Phase.GoToFurnace;
            TileCoord = MapManager.Instance != null
                ? MapManager.Instance.WorldToTile(_table.transform.position)
                : Vector2Int.zero;
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
