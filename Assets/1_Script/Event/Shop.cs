using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// 상점 하나 — 진열된 재고와 사고파는 절차. **MonoBehaviour가 아니다.**
//
// 필드맵의 상인은 NPC에 붙은 `Merchant`가 이걸 들고, 월드맵 마을은 오브젝트 없이
// `TownMarket`이 칸마다 하나씩 든다. 마을에 폰을 세워 두지 않으려고 재고 추첨과 거래를
// MonoBehaviour에서 떼어냈다 — 지도 위의 마을은 "칸"이지 "씬에 있는 물건"이 아니다.
public class Shop
{
    public MerchantData Data { get; private set; }

    public readonly List<MerchantStockEntry> Stock = new();

    public Shop(MerchantData data)
    {
        Data = data;
        Refresh();
    }

    public string DisplayName => Data != null ? Data.MerchantType.DisplayName() : "상인";

    // 진열 갱신 — 가중치 추첨으로 `DailySlotCount`종을 뽑는다. 하루가 바뀔 때 호출.
    public void Refresh()
    {
        Stock.Clear();
        if (Data == null) return;

        // 자기 목록 + 얹은 묶음(`ExtraPools`) — 고블린 방어구 상인이 기본 방어구도 진열한다.
        var pool = Data.CollectPool();
        if (pool.Count == 0) return;

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
                Debug.LogWarning($"[Shop] 재고 항목을 못 찾았습니다 — ItemId '{picked.ItemId}'. 진열에서 뺍니다.");
                i--;   // 이 칸은 다른 것으로 채운다
                if (pool.Count == 0) break;
                continue;
            }

            Stock.Add(new MerchantStockEntry
            {
                Item    = item,
                Price   = Data.CalcPrice(picked),
                Stock   = Random.Range(picked.MinStock, picked.MaxStock + 1),
                Quality = picked.Quality,
            });
        }
    }

    // 구매 시도. 골드 부족, 받을 곳 없음이면 false.
    //
    // `buyer`가 있으면 **거기 먼저 넣는다** — 산 물건을 바로 들고 다니는 게 자연스럽고,
    // 창고로 직행하면 방금 산 것을 다시 꺼내러 가야 한다. 배낭이 꽉 차면 계정이 받는다
    // (거점이면 창고로, 원정대면 다른 멤버 배낭으로).
    //
    // **돈을 먼저 뺀다.** 예전엔 물건을 먼저 넣었는데, 그건 "골드만 사라지는" 사고를 피하려던
    // 것이고 이제 계정에 되돌리는 길(`GiveGold`)이 있다. 순서를 이렇게 두면 물건을 넣은 뒤
    // 값을 못 치르는 경우(= 되돌릴 곳을 모르는 경우)가 아예 생기지 않는다.
    public bool TryBuy(MerchantStockEntry entry, TradeAccount account, Inventory buyer = null)
    {
        if (Data == null || account == null) return false;
        if (entry == null || entry.Stock <= 0) return false;
        if (account.TakeGold(entry.Price) < entry.Price) return false;

        int stored = buyer != null ? buyer.TryAdd(entry.Item, 1, entry.Quality) : 0;
        if (stored == 0) stored = account.Receive(entry.Item, 1, entry.Quality);

        if (stored == 0)
        {
            // 방금 뺀 자리로 그대로 돌아간다 — 뺀 만큼의 자리는 비어 있다.
            account.GiveGold(entry.Price);
            return false;
        }

        entry.Stock--;
        return true;
    }

    // 판매 시도. **실제로 판 개수를 돌려준다**(0 = 실패).
    // 한 번에 하나씩만 팔리면 스택 아이템을 비우는 데 클릭이 수십 번 든다.
    //
    // 한 개씩 도는 이유: 대금이 안 들어가면(창고 만적·배낭 만적) 거기서 멈춰야 하는데,
    // 한꺼번에 계산하면 "절반만 팔린" 상태를 되돌릴 방법이 없다. 개당 원자성을 지킨다.
    public int TrySell(ItemInstance inst, Inventory sourceInv, TradeAccount account, int count = 1)
    {
        if (Data == null || inst == null || sourceInv == null || account == null) return 0;

        int price = Data.CalcSellPrice(inst.Data, inst.Quality);
        if (price <= 0) return 0;

        int sold = 0;
        int want = Mathf.Clamp(count, 1, inst.Quantity);

        for (int i = 0; i < want; i++)
        {
            // 대금 입금을 먼저 확정한 뒤 물건을 뺀다 — 순서를 뒤집으면 받을 곳이 꽉 찼을 때
            // 물건만 사라진다.
            int paid = account.GiveGold(price);
            if (paid < price)
            {
                if (paid > 0) account.TakeGold(paid);
                break;
            }

            if (!sourceInv.RemoveOne(inst))
            {
                account.TakeGold(price);
                break;
            }
            sold++;
        }
        return sold;
    }

    public int GetSellPrice(ItemInstance inst)
        => inst != null ? (Data?.CalcSellPrice(inst.Data, inst.Quality) ?? 0) : 0;
}
