using System;
using UnityEngine;

public class HaulJob : JobBase
{
    public override JobCategory Category => JobCategory.Hauling;

    enum Phase { GoToSource, GoToStorage }

    ResourceNode _source;
    ItemData     _itemData;
    int          _amount;
    Storage      _storage;
    Phase        _phase = Phase.GoToSource;
    Action       _onDone;
    UnitBrain    _carrier;   // 짐을 들고 있는 폰 (취소 시 내려놓기용)

    public HaulJob(ResourceNode source, ItemData itemData, int amount, Storage storage, Action onDone = null)
    {
        _source   = source;
        _itemData = itemData;
        _amount   = amount;
        _storage  = storage;
        _onDone   = onDone;
        TileCoord = source.TileCoord;
        Priority  = 5;
    }

    protected override void Execute(UnitBrain unit)
    {
        _carrier = unit;

        if (_phase == Phase.GoToSource)
        {
            int available = _source != null ? _source.Inventory.GetTotalQuantity(_itemData) : 0;
            if (available <= 0)
            {
                Status = JobStatus.Done;
                _onDone?.Invoke();
                return;
            }

            int toTake = _source.Inventory.Remove(_itemData, available);
            int added  = unit.Inventory.TryAdd(_itemData, toTake);
            if (added < toTake)
            {
                // 인벤에 다 안 들어감 — 나머지는 노드에 되돌림 (증발 방지)
                int back = _source.Inventory.TryAdd(_itemData, toTake - added);
                int lost = toTake - added - back;
                if (lost > 0)
                    DroppedItem.Spawn(_itemData, lost, _source.transform.position);
            }
            _amount = added;

            if (_amount <= 0)
            {
                Status = JobStatus.Done;
                _onDone?.Invoke();
                return;
            }

            _phase    = Phase.GoToStorage;
            TileCoord = MapManager.Instance.WorldToTile(_storage.transform.position);
        }
        else
        {
            int removed = unit.Inventory.Remove(_itemData, _amount);
            // 실제 꺼낸 수량 기준으로 입고. 못 들어간 잔량은 인벤 복귀 → 드롭 순으로 보존.
            int leftover = removed;
            if (leftover > 0 && _storage != null && _storage.CanAccept(_itemData))
                leftover -= _storage.Inventory.TryAdd(_itemData, leftover);

            if (leftover > 0)
            {
                var fallback = Storage.FindNearest(unit.transform.position, _itemData);
                if (fallback != null)
                    leftover -= fallback.Inventory.TryAdd(_itemData, leftover);
            }

            if (leftover > 0)
            {
                leftover -= unit.Inventory.TryAdd(_itemData, leftover);
                if (leftover > 0)
                    DroppedItem.Spawn(_itemData, leftover, unit.transform.position);
            }

            Status = JobStatus.Done;
            _onDone?.Invoke();
        }
    }

    // 취소 시 들고 있던 짐을 그 자리에 내려놓는다.
    // 안 그러면 운반 도중 취소된 물건이 폰 인벤토리에 갇힌다(이동 명령 취소가 잦아지며 드러난 경로).
    // 드롭되면 GroundHaulJob이 자동 등록돼 다른 폰이 창고로 옮긴다.
    protected override void OnCancelled()
    {
        _onDone?.Invoke();

        if (_phase != Phase.GoToStorage || _carrier == null || _itemData == null || _amount <= 0) return;

        int removed = _carrier.Inventory.Remove(_itemData, _amount);
        if (removed > 0)
            DroppedItem.Spawn(_itemData, removed, _carrier.transform.position);
    }
}
