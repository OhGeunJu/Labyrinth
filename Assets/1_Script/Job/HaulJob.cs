using UnityEngine;

public class HaulJob : JobBase
{
    public override JobCategory Category => JobCategory.Hauling;

    enum Phase { GoToSource, GoToStorage }

    ResourceNode    _source;
    ItemData        _itemData;
    int             _amount;
    Storage _storage;
    Phase           _phase = Phase.GoToSource;

    public HaulJob(ResourceNode source, ItemData itemData, int amount, Storage storage)
    {
        _source   = source;
        _itemData = itemData;
        _amount   = amount;
        _storage  = storage;
        TileCoord = source.TileCoord;
        Priority  = 5;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_phase == Phase.GoToSource)
        {
            // 노드 인벤토리에서 아이템 꺼내기
            int available = _source != null ? _source.Inventory.GetTotalQuantity(_itemData) : 0;
            int toTake    = Mathf.Min(_amount, available);

            if (toTake <= 0)
            {
                // 이미 다른 폰이 가져간 경우
                Status = JobStatus.Done;
                return;
            }

            _source.Inventory.Remove(_itemData, toTake);
            unit.Inventory.TryAdd(_itemData, toTake);
            _amount = toTake;

            _phase    = Phase.GoToStorage;
            TileCoord = MapManager.Instance.WorldToTile(_storage.transform.position);
        }
        else
        {
            // 창고에 입금 (필터 재확인)
            if (unit.Inventory.Remove(_itemData, _amount) == _amount)
            {
                if (!_storage.TryAdd(_itemData, _amount))
                {
                    var fallback = Storage.FindNearest(unit.transform.position, _itemData);
                    if (fallback != null)
                        fallback.TryAdd(_itemData, _amount);
                    else
                        unit.Inventory.TryAdd(_itemData, _amount);  // 갈 곳 없으면 반환
                }
            }
            Status = JobStatus.Done;
        }
    }
}
