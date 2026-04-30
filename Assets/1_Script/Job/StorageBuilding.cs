using System;
using System.Collections.Generic;
using UnityEngine;

public class StorageBuilding : MonoBehaviour
{
    public static readonly List<StorageBuilding> All = new();
    public static event Action OnChanged;

    [Header("허용 아이템 (비어있으면 모두 허용)")]
    [SerializeField] List<ResourceType> allowedTypes = new();

    public Inventory Inventory { get; private set; }

    void Awake()
    {
        Inventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        Inventory.OnChanged += () => OnChanged?.Invoke();
        All.Add(this);
    }

    void OnDestroy() => All.Remove(this);

    // ── 입고 필터 ─────────────────────────────────────────────────────
    public bool CanAccept(ItemData item)
    {
        if (allowedTypes.Count == 0) return true;
        // 자원 아이템만 ResourceType으로 필터링. 무기/방어구는 필터가 비어있을 때만 허용.
        return item is ResourceItemData r && allowedTypes.Contains(r.ResourceType);
    }

    // 필터 통과 시에만 입고. 성공 여부 반환.
    public bool TryAdd(ItemData item, int amount)
    {
        if (!CanAccept(item)) return false;
        return Inventory.TryAdd(item, amount) > 0;
    }

    // ── 정적 집계 (ResourceType 기반) ────────────────────────────────
    public static int GetTotal(ResourceType type)
    {
        int total = 0;
        foreach (var sb in All)
            total += sb.Inventory.GetTotalByType(type);
        return total;
    }

    public static bool Consume(ResourceType type, int amount)
    {
        int remaining = amount;
        foreach (var sb in All)
        {
            if (remaining <= 0) break;
            remaining -= sb.Inventory.RemoveByType(type, remaining);
        }
        return remaining <= 0;
    }

    // ── ItemData 기반 집계 (제작 재료용) ──────────────────────────────
    public static int GetTotalByItem(ItemData item)
    {
        int total = 0;
        foreach (var sb in All)
            total += sb.Inventory.GetTotalQuantity(item);
        return total;
    }

    public static bool ConsumeByItem(ItemData item, int amount)
    {
        if (GetTotalByItem(item) < amount) return false;
        int remaining = amount;
        foreach (var sb in All)
        {
            if (remaining <= 0) break;
            remaining -= sb.Inventory.Remove(item, remaining);
        }
        return remaining <= 0;
    }

    // ── 위치/탐색 유틸 ───────────────────────────────────────────────
    // ResourceType 기반으로 재료가 있는 창고와 해당 ItemData를 반환
    public static (StorageBuilding building, ItemData itemData) FindStorageWithType(ResourceType type)
    {
        foreach (var sb in All)
            foreach (var inst in sb.Inventory.Items)
                if (inst.Data is ResourceItemData r && r.ResourceType == type && inst.Quantity > 0)
                    return (sb, inst.Data);
        return (null, null);
    }

    // item을 받을 수 있는 창고 중 가장 가까운 것 반환
    public static StorageBuilding FindNearest(Vector3 position, ItemData item = null)
    {
        StorageBuilding nearest = null;
        float minDist = float.MaxValue;
        foreach (var sb in All)
        {
            if (item != null && !sb.CanAccept(item)) continue;
            float d = Vector3.Distance(position, sb.transform.position);
            if (d < minDist) { minDist = d; nearest = sb; }
        }
        return nearest;
    }
}
