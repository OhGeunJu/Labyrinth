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

    protected override void OnStart()
    {
        Debug.Log($"[HaulJob] 시작 — 운반자:{ReservedBy?.name} 소스:{_source?.name} 아이템:{_itemData?.ItemName}");
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_phase == Phase.GoToSource)
        {
            int available = _source != null ? _source.Inventory.GetTotalQuantity(_itemData) : 0;
            Debug.Log($"[HaulJob] GoToSource — 노드 재고:{available} 운반자:{unit.name}");

            if (available <= 0)
            {
                Debug.Log("[HaulJob] 노드에 아이템 없음 → 완료");
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
            Debug.Log($"[HaulJob] 수거 {toTake}개 → 인벤 추가 {added}개 → 창고로 이동");

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
            Debug.Log($"[HaulJob] GoToStorage — 인벤에서 {removed}/{_amount}개 꺼냄");

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
            Debug.Log("[HaulJob] 완료");
        }
    }

    protected override void OnCancelled() => _onDone?.Invoke();
}
