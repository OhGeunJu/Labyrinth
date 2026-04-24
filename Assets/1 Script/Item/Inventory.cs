using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    readonly Dictionary<ItemData, int> _stockMap = new();
    readonly List<ItemStack> _slots = new();

    public IReadOnlyList<ItemStack> Slots => _slots;
    public event Action OnChanged;

    public void Add(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;

        if (_stockMap.ContainsKey(item))
            _stockMap[item] += amount;
        else
            _stockMap[item] = amount;

        RebuildSlots();
        OnChanged?.Invoke();
        Debug.Log($"[Inventory] {item.ItemName} +{amount} (합계 {_stockMap[item]})");
    }

    public bool Consume(ItemData item, int amount)
    {
        if (!_stockMap.TryGetValue(item, out int current) || current < amount)
            return false;

        current -= amount;
        if (current == 0) _stockMap.Remove(item);
        else _stockMap[item] = current;

        RebuildSlots();
        OnChanged?.Invoke();
        return true;
    }

    public int GetQuantity(ItemData item) =>
        _stockMap.TryGetValue(item, out int q) ? q : 0;

    public float GetTotalWeight()
    {
        float total = 0f;
        foreach (var kv in _stockMap)
            total += kv.Key.Weight * kv.Value;
        return total;
    }

    // ResourceType 기반 집계 (StorageBuilding 통합용)
    public int GetTotalByType(ResourceType type)
    {
        int total = 0;
        foreach (var kv in _stockMap)
            if (kv.Key.ResourceType == type) total += kv.Value;
        return total;
    }

    public int ConsumeByType(ResourceType type, int amount)
    {
        // 먼저 소비할 목록을 수집한 뒤 실행 (이터레이션 중 수정 방지)
        var toConsume = new System.Collections.Generic.List<(ItemData item, int count)>();
        int remaining = amount;

        foreach (var kv in _stockMap)
        {
            if (kv.Key.ResourceType != type) continue;
            int take = System.Math.Min(remaining, kv.Value);
            toConsume.Add((kv.Key, take));
            remaining -= take;
            if (remaining <= 0) break;
        }

        foreach (var (item, count) in toConsume)
            Consume(item, count);

        return amount - remaining;  // 실제 소비량 반환
    }

    public void Clear()
    {
        _stockMap.Clear();
        _slots.Clear();
        OnChanged?.Invoke();
    }

    void RebuildSlots()
    {
        _slots.Clear();
        foreach (var kv in _stockMap)
            _slots.Add(new ItemStack(kv.Key, kv.Value));
    }
}
