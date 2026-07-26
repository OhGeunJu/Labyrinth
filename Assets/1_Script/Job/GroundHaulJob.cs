// 땅에 떨어진 DroppedItem을 창고로 운반하는 작업.
// ResourceNode 기반 HaulJob과 별도로 관리.
public class GroundHaulJob : JobBase
{
    public override JobCategory Category => JobCategory.Hauling;

    enum Phase { GoToItem, GoToStorage }

    DroppedItem     _droppedItem;
    ItemData        _itemData;
    int             _amount;
    ItemQuality     _quality;
    Storage _storage;
    Phase           _phase = Phase.GoToItem;

    public GroundHaulJob(DroppedItem droppedItem, Storage storage)
    {
        _droppedItem = droppedItem;
        _itemData    = droppedItem.Item;
        _amount      = droppedItem.Amount;
        _quality     = droppedItem.Quality;
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
            int removed  = unit.Inventory.Remove(_itemData, _amount);
            int leftover = removed - _storage.TryAdd(_itemData, removed, _quality);

            // 부분 입고 시 남은 양을 다른 창고로, 그래도 남으면 인벤 복귀(방금 뺀 공간이라 반드시 수용) → 증발 방지.
            if (leftover > 0)
            {
                var fallback = Storage.FindNearest(unit.transform.position, _itemData);
                if (fallback != null)
                    leftover -= fallback.TryAdd(_itemData, leftover, _quality);
            }
            if (leftover > 0)
                unit.Inventory.TryAdd(_itemData, leftover, _quality);

            Status = JobStatus.Done;
        }
    }
}
