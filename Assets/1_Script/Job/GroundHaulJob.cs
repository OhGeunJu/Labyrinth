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

    // 질 자리가 없는 폰에게는 주지 않는다(`HaulJob`과 같은 규칙).
    //
    // **무게와 격자를 둘 다 본다.** 무게만 보면 배낭 격자가 꽉 찬 폰이 통과하는데,
    // 그 폰이 오면 `PickUp`의 `TryAdd`가 0을 돌려주고 잔량 **전부**가 그 자리에 다시
    // 떨어진다(드롭 오브젝트를 파괴하고 새로 만든다) → 새 일감 → 여전히 가장 가까운
    // 같은 폰 → 매 틱 프리팹을 다시 굽는 무한 반복이 된다.
    public override bool CanAssignTo(UnitBrain unit)
        => unit != null && unit.Inventory != null
        && CarryCapacity.RoomFor(unit, _itemData) > 0
        && unit.Inventory.CanFit(_itemData, 1, _quality);

    protected override void Execute(UnitBrain unit)
    {
        if (_phase == Phase.GoToItem)
        {
            // 이미 다른 폰이 주웠으면 취소
            if (_droppedItem == null) { Status = JobStatus.Done; return; }

            // **질 수 있는 만큼만** 줍는다. 못 실은 잔량은 그 자리에 다시 떨어지고
            // 새 운반 일감이 걸리므로 방치되지 않는다.
            _amount = _droppedItem.PickUp(unit.Inventory, CarryCapacity.RoomFor(unit, _itemData));
            if (_amount <= 0) { Status = JobStatus.Done; return; }

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
                var fallback = Storage.FindNearest(unit.transform.position, _itemData, leftover, _quality);
                if (fallback != null)
                    leftover -= fallback.TryAdd(_itemData, leftover, _quality);
            }
            if (leftover > 0)
                unit.Inventory.TryAdd(_itemData, leftover, _quality);

            Status = JobStatus.Done;
        }
    }
}
