using System.Collections.Generic;
using UnityEngine;

// 거래의 **우리 쪽** — 값을 치를 돈이 어디 있고, 산 물건은 어디로 가는가.
//
// 상인이 아는 것은 "무엇을 얼마에 파는가"뿐이다. 지갑이 거점 창고인지 원정대 배낭인지는
// 상인의 성질이 아니라 **누가 사러 왔느냐**의 문제다. 예전엔 `Merchant`가 `Storage`를 직접
// 불러서, 창고가 닿지 않는 곳(월드맵 마을)에서는 같은 상인이 아예 동작할 수 없었다.
//
// **계약 — 돈은 전액 아니면 0이다.** `TakeGold`가 절반만 빼 가면 되돌릴 방법이 없다.
// 물건(`Receive`)은 반대로 부분 입고를 인정한다 — 격자·무게가 막는 건 정상적인 거절이고,
// 실제로 받은 개수를 돌려주면 호출자가 거기까지만 값을 치르면 된다.
public abstract class TradeAccount
{
    // UI에 "누구의 지갑인가"를 적는다 — 창고 돈과 원정대 돈을 헷갈리면 안 산 물건을 샀다고 믿는다.
    public abstract string Label { get; }

    // ── 돈은 하나다 ───────────────────────────────────────────────────────────
    //
    // **누가 사러 왔든 지갑은 같다**(`Wallet`). 예전엔 창고 골드와 배낭 골드가 따로 놀아서
    // 같은 물건을 거점에선 살 수 있고 지도에선 못 사는 일이 났고, 돈을 묻는 곳이 생길 때마다
    // "배낭 먼저, 그다음 창고"를 다시 짜야 했다.
    //
    // **갈리는 것은 돈이 아니라 물건이다** — 산 것이 창고로 가느냐 배낭으로 가느냐는 여전히
    // 누가 사러 왔느냐의 문제다. 그래서 `Receive`만 하위가 정한다.
    public int GoldAmount => Wallet.Balance;

    // 실제로 뺀 금액. 전액을 못 대면 **아무것도 빼지 않고** 0.
    public int TakeGold(int amount)
    {
        if (amount <= 0) return 0;
        return Wallet.TrySpend(amount) ? amount : 0;
    }

    // 실제로 넣은 금액. 잔고는 늘 받는다 — 넣을 자리라는 개념이 없다.
    public int GiveGold(int amount)
    {
        if (amount <= 0) return 0;
        Wallet.Add(amount);
        return amount;
    }

    // 산 물건을 받는다. 실제로 받은 개수(0 = 받을 곳 없음).
    public abstract int Receive(ItemData item, int amount, ItemQuality quality);
}

// 거점 창고 지갑 — 필드맵에 상주하는 상인이 쓰는 계정(기존 동작 그대로).
public class StorageAccount : TradeAccount
{
    // 창고를 고를 기준점. 상인이 서 있는 자리에서 가장 가까운 창고로 넣고 뺀다.
    readonly Vector3 _origin;

    public StorageAccount(Vector3 origin)
    {
        _origin = origin;
    }

    public override string Label => "창고";

    public override int Receive(ItemData item, int amount, ItemQuality quality)
    {
        if (item == null || amount <= 0) return 0;

        // **수량과 등급을 넘긴다.** 기본값(1개·일반)으로 물으면 만적인 창고가 "한 개는 들어간다"고
        // 답하고, 등급이 다르면 스택이 안 합쳐져 결국 못 넣는다.
        var dest = Storage.FindNearest(_origin, item, amount, quality);
        return dest != null ? dest.Inventory.TryAdd(item, amount, quality) : 0;
    }
}
