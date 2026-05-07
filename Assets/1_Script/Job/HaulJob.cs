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
            _amount = toTake;
            Debug.Log($"[HaulJob] 수거 {toTake}개 → 인벤 추가 {added}개 → 창고로 이동");

            _phase    = Phase.GoToStorage;
            TileCoord = MapManager.Instance.WorldToTile(_storage.transform.position);
        }
        else
        {
            int removed = unit.Inventory.Remove(_itemData, _amount);
            Debug.Log($"[HaulJob] GoToStorage — 인벤에서 {removed}/{_amount}개 꺼냄");

            if (removed == _amount)
            {
                if (!_storage.TryAdd(_itemData, _amount))
                {
                    var fallback = Storage.FindNearest(unit.transform.position, _itemData);
                    bool stored  = fallback != null && fallback.TryAdd(_itemData, _amount);
                    if (!stored)
                    {
                        int returned = unit.Inventory.TryAdd(_itemData, _amount);
                        if (returned < _amount)
                            DroppedItem.Spawn(_itemData, _amount - returned, unit.transform.position);
                    }
                }
            }
            Status = JobStatus.Done;
            _onDone?.Invoke();
            Debug.Log("[HaulJob] 완료");
        }
    }

    protected override void OnCancelled() => _onDone?.Invoke();
}
