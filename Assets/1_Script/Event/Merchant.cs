using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

// 마을 상주 상인 NPC에 부착. 매일 자정에 재고 갱신.
public class Merchant : MonoBehaviour, IWorldClickable
{
    public static event Action<Merchant> OnSelected;

    [SerializeField] MerchantData _data;
    public MerchantData Data
    {
        get => _data;
        set { _data = value; RefreshStock(); }
    }

    public List<MerchantStockEntry> CurrentStock { get; private set; } = new();

    void Awake()
    {
        GameTimeManager.OnDayChanged += RefreshStock;
        // 인스펙터에서 직접 Data가 지정된 경우 초기 재고 생성
        if (_data != null) RefreshStock();
    }

    void OnDestroy() => GameTimeManager.OnDayChanged -= RefreshStock;

    // SelectionManager의 단일 레이캐스트가 이 상인을 클릭 판정 시 호출.
    public void OnWorldClick() => OnSelected?.Invoke(this);

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

            pool.Remove(picked);

            // 못 찾은 항목은 **뽑기에서 빼고 넘어간다**(위에서 이미 pool에서 제거했다).
            // 진열대에 빈 줄로 올리면 클릭했을 때 아무 일도 안 일어나 원인을 알 수 없다.
            var item = picked.Resolve();
            if (item == null)
            {
                Debug.LogWarning($"[Merchant] 재고 항목을 못 찾았습니다 — ItemId '{picked.ItemId}'. 진열에서 뺍니다.");
                i--;   // 이 칸은 다른 것으로 채운다
                if (pool.Count == 0) break;
                continue;
            }

            CurrentStock.Add(new MerchantStockEntry
            {
                Item    = item,
                Price   = Data.CalcPrice(picked),
                Stock   = Random.Range(picked.MinStock, picked.MaxStock + 1),
                Quality = picked.Quality,
            });
        }
    }

    // 구매 시도. 골드 부족, 받을 곳 없음, 자리 없음이면 false.
    //
    // `buyer`가 있으면 **거기 먼저 넣는다** — 산 물건을 바로 들고 다니는 게 자연스럽고,
    // 창고로 직행하면 방금 산 것을 다시 꺼내러 가야 한다. 폰이 없거나 배낭이 꽉 차면 창고로.
    public bool TryBuy(MerchantStockEntry entry, Inventory buyer = null)
    {
        if (Data?.GoldItem == null || entry.Stock <= 0) return false;
        if (Storage.GetTotalByItem(Data.GoldItem) < entry.Price) return false;

        // **물건 입고를 먼저 확정한다** — 골드부터 빼면 넣을 자리가 없을 때 돈만 사라진다.
        bool stored = buyer != null && buyer.TryAdd(entry.Item, 1, entry.Quality) > 0;

        if (!stored)
        {
            var dest = Storage.FindNearest(transform.position, entry.Item);
            if (dest == null) return false;
            stored = dest.Inventory.TryAdd(entry.Item, 1, entry.Quality) > 0;
        }
        if (!stored) return false;

        Storage.ConsumeByItem(Data.GoldItem, entry.Price); // 위에서 총량 확인됨
        entry.Stock--;
        return true;
    }

    // 판매 시도. **실제로 판 개수를 돌려준다**(0 = 실패).
    // 한 번에 하나씩만 팔리면 스택 아이템을 비우는 데 클릭이 수십 번 든다.
    //
    // 한 개씩 도는 이유: 골드가 창고에 안 들어가면(만적) 거기서 멈춰야 하는데,
    // 한꺼번에 계산하면 "절반만 팔린" 상태를 되돌릴 방법이 없다. 개당 원자성을 지킨다.
    public int TrySell(ItemInstance inst, Inventory sourceInv, int count = 1)
    {
        if (Data?.GoldItem == null || inst == null || sourceInv == null) return 0;

        int price = Data.CalcSellPrice(inst.Data, inst.Quality);
        if (price <= 0) return 0;

        var goldDest = Storage.FindNearest(transform.position, Data.GoldItem);
        if (goldDest == null) return 0;

        int sold = 0;
        int want = Mathf.Clamp(count, 1, inst.Quantity);

        for (int i = 0; i < want; i++)
        {
            // 골드 입고를 먼저 확정한 뒤 물건을 뺀다 — 순서를 뒤집으면 창고가 꽉 찼을 때
            // 물건만 사라진다.
            int addedGold = goldDest.Inventory.TryAdd(Data.GoldItem, price);
            if (addedGold < price)
            {
                if (addedGold > 0) goldDest.Inventory.Remove(Data.GoldItem, addedGold);
                break;
            }

            if (!sourceInv.RemoveOne(inst))
            {
                goldDest.Inventory.Remove(Data.GoldItem, price);
                break;
            }
            sold++;
        }
        return sold;
    }

    public int GetSellPrice(ItemInstance inst)
        => Data?.CalcSellPrice(inst.Data, inst.Quality) ?? 0;
}
