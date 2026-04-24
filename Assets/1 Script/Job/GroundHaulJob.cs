// 땅에 떨어진 DroppedItem을 창고로 운반하는 작업.
// ResourceNode 기반 HaulJob과 별도로 관리.
public class GroundHaulJob : JobBase
{
    public override JobCategory Category => JobCategory.Hauling;

    enum Phase { GoToItem, GoToStorage }

    DroppedItem     _droppedItem;
    ItemData        _itemData;
    int             _amount;
    StorageBuilding _storage;
    Phase           _phase = Phase.GoToItem;

    public GroundHaulJob(DroppedItem droppedItem, StorageBuilding storage)
    {
        _droppedItem = droppedItem;
        _itemData    = droppedItem.Item;
        _amount      = droppedItem.Amount;
        _storage     = storage;
        TileCoord    = MapManager.Instance.WorldToTile(droppedItem.transform.position);
        Priority     = 4;  // ResourceNode HaulJob(5)보다 낮음
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_phase == Phase.GoToItem)
        {
            // 이미 다른 폰이 주웠으면 취소
            if (_droppedItem == null) { Status = JobStatus.Done; return; }

            _droppedItem.PickUp(unit.Inventory);
            _phase    = Phase.GoToStorage;
            TileCoord = MapManager.Instance.WorldToTile(_storage.transform.position);
        }
        else
        {
            if (unit.Inventory.Consume(_itemData, _amount))
            {
                if (!_storage.TryAdd(_itemData, _amount))
                {
                    var fallback = StorageBuilding.FindNearest(unit.transform.position, _itemData);
                    if (fallback != null)
                        fallback.TryAdd(_itemData, _amount);
                    else
                        unit.Inventory.Add(_itemData, _amount);
                }
            }
            Status = JobStatus.Done;
        }
    }
}
