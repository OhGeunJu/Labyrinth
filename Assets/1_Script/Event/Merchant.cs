using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

// 마을 상주 상인 NPC에 부착. 매일 자정에 재고 갱신.
public class Merchant : MonoBehaviour
{
    public static event Action<Merchant> OnSelected;

    [SerializeField] MerchantData _data;
    public MerchantData Data
    {
        get => _data;
        set { _data = value; RefreshStock(); }
    }

    public List<MerchantStockEntry> CurrentStock { get; private set; } = new();

    Camera _cam;

    void Awake()
    {
        _cam = Camera.main;
        GameTimeManager.OnDayChanged += RefreshStock;
        // 인스펙터에서 직접 Data가 지정된 경우 초기 재고 생성
        if (_data != null) RefreshStock();
    }

    void OnDestroy() => GameTimeManager.OnDayChanged -= RefreshStock;

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit) && hit.collider.gameObject == gameObject)
            OnSelected?.Invoke(this);
    }

    public void RefreshStock()
    {
        CurrentStock.Clear();
        if (Data == null || Data.ItemPool.Count == 0) return;

        var pool  = new List<MerchantPoolEntry>(Data.ItemPool);
        int slots = Mathf.Min(Data.DailySlotCount, pool.Count);

        for (int i = 0; i < slots; i++)
        {
            float total = 0f;
            foreach (var e in pool) total += e.Weight;

            float roll = Random.value * total;
            float acc  = 0f;
            MerchantPoolEntry picked = pool[pool.Count - 1];
            foreach (var e in pool) { acc += e.Weight; if (roll <= acc) { picked = e; break; } }

            CurrentStock.Add(new MerchantStockEntry
            {
                Item    = picked.Item,
                Price   = Data.CalcPrice(picked),
                Stock   = Random.Range(picked.MinStock, picked.MaxStock + 1),
                Quality = picked.Quality,
            });
            pool.Remove(picked);
        }
    }

    // 구매 시도. Storage 골드 부족, 보관 창고 없음, 창고 만석이면 false.
    public bool TryBuy(MerchantStockEntry entry)
    {
        if (Data?.GoldItem == null || entry.Stock <= 0) return false;
        if (Storage.GetTotalByItem(Data.GoldItem) < entry.Price) return false;

        var dest = Storage.FindNearest(transform.position, entry.Item);
        if (dest == null) return false;

        // 아이템 입고(품질 유지)를 먼저 확정 → 실패 시 골드 손실 없음
        if (dest.Inventory.TryAdd(entry.Item, 1, entry.Quality) <= 0) return false;

        Storage.ConsumeByItem(Data.GoldItem, entry.Price); // 위에서 총량 확인됨
        entry.Stock--;
        return true;
    }

    // 판매 시도. 골드 입고를 먼저 확정한 뒤 아이템을 제거 — 실패 시 양쪽 모두 원복.
    public bool TrySell(ItemInstance inst, Inventory sourceInv)
    {
        if (Data?.GoldItem == null || inst == null || sourceInv == null) return false;
        int price = Data.CalcSellPrice(inst.Data, inst.Quality);
        if (price <= 0) return false;

        var goldDest = Storage.FindNearest(transform.position, Data.GoldItem);
        if (goldDest == null) return false;

        int addedGold = goldDest.Inventory.TryAdd(Data.GoldItem, price);
        if (addedGold < price)
        {
            if (addedGold > 0) goldDest.Inventory.Remove(Data.GoldItem, addedGold);
            return false;
        }

        if (!sourceInv.RemoveOne(inst))
        {
            goldDest.Inventory.Remove(Data.GoldItem, price);
            return false;
        }
        return true;
    }

    public int GetSellPrice(ItemInstance inst)
        => Data?.CalcSellPrice(inst.Data, inst.Quality) ?? 0;
}
