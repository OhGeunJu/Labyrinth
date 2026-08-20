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

    // **누구의 가게인가** — 세력 우호도가 **진열 종류 수**에 얹히는 통로다
    // (`FactionRelations.SlotBonus`). 값에는 안 얹는다: 상대별 배율은 그 상인의 모든 품목을
    // 같은 방향으로 밀어 "어디로 가나"를 순위로 만들어 버린다(`Trading` 주석에 그 판단이).
    //
    // `MerchantData`가 아니라 `Shop`이 드는 이유: 같은 가격표를 마을 사람 상인도 변방민
    // 상인도 쓸 수 있고(`ExtraPools`로 겹쳐 쓰는 것이 그 전제다), 그때 갈리는 것은
    // 파는 물건이 아니라 **파는 사람**이다.
    //
    // 비어 있으면 저작한 진열 수 그대로다.
    public FactionData Faction { get; set; }

    // **지금 흥정하고 있는 폰** — 값 배율이 여기서 나온다(`Trading`). 상점의 성질이 아니라
    // 거래하는 쪽의 상태라 밖에서 물려 준다(`MerchantUI`가 창을 열 때와 배낭 창에서 폰을
    // 바꿀 때 갱신한다).
    //
    // **`Shop`이 드는 이유는 `Price`를 하나로 두기 위해서다.** 배율을 읽는 쪽마다 곱하면
    // 화면에 뜬 값과 실제로 빠지는 값이 갈린다 — 이 프로젝트에서 가장 나쁜 종류의 어긋남이다.
    public UnitBrain Haggler { get; set; }

    public readonly List<MerchantStockEntry> Stock = new();

    // **이 상인이 지금 가진 돈 — 이만큼만 사 준다.**
    //
    // `MerchantData`가 아니라 `Shop`이 든다. 재고와 같은 이유다: `MerchantData`는 "무엇을 얼마에
    // 파는가"(가격표)이고, **오늘 주머니에 얼마가 남았는지는 그 상인의 것**이다. 같은 가격표를
    // 쓰는 상인이 둘이어도 한쪽에 다 팔았다고 다른 쪽이 빈털터리가 되면 안 된다.
    //
    // **0이면 무한**(`MerchantData.Gold`를 0으로 저작한 경우) — 안 채운 상인이 조용히 거래
    // 불가가 되는 것보다, 예전처럼 도는 편이 낫다.
    public int Gold { get; private set; }

    public bool HasInfiniteGold => Data == null || Data.Gold <= 0;

    public Shop(MerchantData data, FactionData faction = null)
    {
        Data    = data;
        Faction = faction;
        Refresh();
    }

    public string DisplayName => Data != null ? Data.MerchantType.DisplayName() : "상인";

    // ── 좌판 = 진짜 격자 ─────────────────────────────────────────────────────
    //
    // **진열대는 자리가 있는 물건이다.** 예전엔 목록뿐이라 상인에게 무한정 팔 수 있었고
    // (한도는 주머니 하나였다) 판 물건은 어디로 갔는지 아무 데도 안 남았다.
    //
    // 자리를 두면 셋이 한꺼번에 선다:
    //   · **칸이 차면 더 못 판다** — 한 마을에 다 쏟아붓지 못하고 돌아다닐 이유가 생긴다
    //   · **판 물건이 좌판에 오른다** — 내가 넘긴 검을 도로 살 수 있다(더 비싸게)
    //   · **좌판을 내 물건으로 채우면 그 상인의 새 물건이 적게 들어온다**(아래 `Refresh`)
    //
    // **한도는 `Shop`이 든다 — 화면이 아니라.** 한때 격자를 상점 창이 만들었는데, 그러면
    // "몇 개까지 팔리나"를 창이 정하는 꼴이라 창을 안 열고 파는 길이 생기면 규칙이 샌다.
    Inventory _stall;

    public Vector2Int GridSize
    {
        get
        {
            // **기본값은 폰 배낭과 같은 8×10**(`Inventory`의 저작 기본값). 상인마다 따로
            // 저작할 수 있게 열어 둔 것이지 **배낭과 달라야 한다는 뜻이 아니다** — 두 격자가
            // 나란히 떠 있어서 눈금이 같아야 "이게 저기 들어가나"를 눈으로 잴 수 있다.
            var g = Data != null ? Data.StockGrid : default;
            return g.x > 0 && g.y > 0 ? g : new Vector2Int(8, 10);
        }
    }

    // 좌판. **꺼진 GameObject 하나가 든다** — `Inventory`가 MonoBehaviour라 어딘가엔 붙어야
    // 하는데, 이건 세상에 있는 통이 아니라 규칙과 화면에만 있는 것이다.
    public Inventory Stall
    {
        get
        {
            if (_stall == null)
            {
                var go = new GameObject("_Stall") { hideFlags = HideFlags.HideInHierarchy };
                _stall = go.AddComponent<Inventory>();
                _stall.Resize(GridSize.x, GridSize.y);
                go.SetActive(false);
                SyncStall();
            }
            return _stall;
        }
    }

    // 이 물건을 더 받아 줄 자리가 있나. **`CanFit`이 `TryAdd`와 같은 규칙**이라 화면에 뜨는
    // 자리와 실제로 들어가는 자리가 갈리지 않는다(스택도 같이 본다 — 화살 한 칸에 더 얹는 것은
    // 새 자리를 안 먹는다).
    public bool HasRoomFor(ItemData item, int count, ItemQuality quality)
        => item != null && Stall.CanFit(item, count, quality);

    // **목록을 좌판에 그대로 옮겨 담는다.** 좌판이 물리적 진실이고 목록은 값표다 — 둘이
    // 어긋나면 "화면엔 있는데 못 산다"가 되므로, 안 들어간 것은 **목록에서도 뺀다.**
    void SyncStall()
    {
        if (_stall == null) return;
        _stall.Clear();

        for (int i = 0; i < Stock.Count; )
        {
            var e = Stock[i];
            if (e.Item == null || e.Stock <= 0) { Stock.RemoveAt(i); continue; }

            int added = _stall.TryAdd(e.Item, e.Stock, e.Quality);
            if (added <= 0) { Stock.RemoveAt(i); continue; }   // 자리가 없으면 안 내놓는다

            e.Stock = added;                                   // 일부만 들어갔으면 그만큼만
            i++;
        }
    }

    // 진열 갱신 — 가중치 추첨으로 `DailySlotCount`종을 뽑는다. 하루가 바뀔 때 호출.
    public void Refresh()
    {
        // **플레이어가 판 물건은 기한까지 남는다.** 상인의 제 물건만 갈린다 —
        // 판 것까지 매일 아침 사라지면 자리 한도가 아무것도 막지 못하고(좌판이 늘 비어 있다)
        // 어제 판 것을 되사러 갈 수도 없다.
        var kept = new List<MerchantStockEntry>();
        foreach (var e in Stock)
            if (e.FromPlayer && e.Stock > 0 && GameTimeManager.CurrentDay < e.ExpireDay)
                kept.Add(e);

        Stock.Clear();

        if (Data == null) { Stock.AddRange(kept); SyncStall(); return; }

        // **주머니도 하루마다 채워진다.** 진열과 같은 주기다 — 어제 다 팔아 치웠으면 오늘
        // 다시 올 이유가 생기고, 그래야 "이 마을은 한 번에 이만큼만 받아 준다"가 규칙이 된다.
        // 부분 회복이 아니라 통째로 되돌린다: 남은 돈을 이월하면 며칠 안 들르는 것만으로
        // 무한 지갑이 되고, 그러면 애초에 한도를 둔 뜻이 사라진다.
        Gold = Data.Gold;

        // 자기 목록 + 얹은 묶음(`ExtraPools`) — 고블린 방어구 상인이 기본 방어구도 진열한다.
        var pool = Data.CollectPool();

        // 팔 물건이 하나도 저작 안 됐어도 **내가 맡긴 것은 그대로 있다** — 그냥 돌아가면
        // 그것까지 없어진다.
        if (pool.Count == 0) { Stock.AddRange(kept); SyncStall(); return; }

        // **우호도가 진열 종류 수를 정한다**(`FactionRelations.SlotBonus`, −2 ~ +2).
        //
        // 값이 아니라 여기 얹는 이유: 값 배율은 그 상인의 모든 품목을 같은 방향으로 밀어
        // **어디로 갈지를 순위로** 만들지만, 진열 수는 쏠림을 안 만든다 — 관계 좋은 마을에서
        // 고를 것이 많아도 광물을 사려면 여전히 동굴에 가야 한다. 지역차가 "어디로 가나"를
        // 온전히 도맡는다(X4·엘리트의 "평판이 살 수 있는 것을 언락한다"와 같은 자리).
        //
        // **하나는 남긴다** — 0이 되면 진열대가 텅 비어 사실상 거래 불가가 되는데, 그건
        // 태도(적대)가 이미 하는 일이고 여기서 또 하면 왜 못 사는지가 두 갈래가 된다.
        int slots = Mathf.Clamp(Data.DailySlotCount + FactionRelations.SlotBonus(Faction),
                                1, Data.DailySlotCount + FactionRelations.SlotSwing);
        slots = Mathf.Min(slots, pool.Count);

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
                Item      = item,
                ListPrice = Data.CalcPrice(picked),
                Stock     = Random.Range(picked.MinStock, picked.MaxStock + 1),
                Quality   = picked.Quality,
            });
        }

        // **자기 물건을 먼저 놓고, 내가 판 것은 남은 자리에.**
        //
        // 순서가 곧 우선권이다(`SyncStall`이 목록 순서대로 놓고 자리가 없으면 뺀다).
        // 반대로 두면 **좌판을 내 물건으로 채우는 것만으로 그 상인의 새 물건이 줄어든다** —
        // 무기 상인에게 빵을 팔았다고 다음 날 검이 덜 들어오는 꼴이라, 상인의 성격이 내가
        // 뭘 떠넘겼느냐로 흔들린다. 좌판은 **그 사람의 가게**다.
        //
        // 그래서 **밀려난 것은 사라진다**(상인이 어딘가로 넘겼다고 보면 설명이 끝난다).
        // 값은 이미 받았으므로 잃는 것은 *되살 기회*뿐이고, 그것이 쏟아붓기의 대가다 —
        // 한 상인에게 몰아 팔면 그중 상당수는 다음 날 되찾을 수 없다.
        Stock.AddRange(kept);

        Reprice();
        SyncStall();   // 좌판에 실제로 놓아 본다 — 자리가 없는 것은 목록에서도 빠진다
    }

    // **흥정을 값에 얹는다 — 진열을 다시 뽑지 않고.**
    //
    // `Price`를 읽는 다섯 곳에서 각자 곱하면(진열 UI·툴팁·구매·환불·살 수 있는 개수)
    // **화면에 뜬 값과 실제로 빠지는 값이 갈린다** — 가장 나쁜 종류의 어긋남이다. 그래서
    // 숫자를 하나로 두고 **흥정하는 폰이 정해지거나 바뀔 때 다시 매긴다**(`MerchantUI`).
    //
    // 원정대에서 폰을 바꿔 가며 볼 수 있으므로(`SetPawnCycle`) 이건 창을 여는 순간 한 번이
    // 아니다 — **사교 좋은 폰으로 넘기면 값이 그 자리에서 내려가는 것이 보여야** 누구를
    // 흥정에 보낼지가 선택이 된다.
    //
    // 재고(`Stock`)는 안 건드린다 — 여닫으며 진열을 새로 뽑는 것을 막는 규칙 그대로다.
    public void Reprice()
    {
        float f = Trading.BuyFactor(Haggler);
        foreach (var e in Stock)
        {
            // **내가 판 물건은 안 건드린다 — 값이 판 순간에 얼어붙어 있다**(`AddPlayerStock`).
            // 여기서 흥정을 곱하면 **판 값보다 싸게 되사게 되어 돈이 찍힌다**(사교 130이면
            // 팔 때 +18% 살 때 −18%라 왕복에 3할이 남고, 무한히 반복할 수 있다).
            if (e.FromPlayer) continue;

            // 옛 저장분·손으로 만든 항목처럼 기준가가 없으면 지금 값을 기준으로 삼는다.
            if (e.ListPrice <= 0) e.ListPrice = e.Price;
            e.Price = Mathf.Max(1, Mathf.RoundToInt(e.ListPrice * f));
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
    //
    // `at`은 **끌어다 놓은 칸**이다(드래그 구매). 거기 안 들어가면 평소대로 빈 자리를 찾는다 —
    // 자리가 안 맞는다고 구매 자체를 거절하면 "왜 안 사지지"가 되고, 손으로 정한 자리를 아예
    // 안 보면 "여기에 놓았다"가 거짓말이 된다.
    public bool TryBuy(MerchantStockEntry entry, TradeAccount account, Inventory buyer = null,
                       Vector2Int? at = null, bool rotated = false, ItemInstance fromStack = null)
    {
        if (Data == null || account == null) return false;
        if (entry == null || entry.Stock <= 0) return false;
        if (account.TakeGold(entry.Price) < entry.Price) return false;

        // **살 폰이 정해져 있으면 그 배낭에만 넣는다 — 꽉 찼으면 못 산다.**
        //
        // 전엔 배낭이 차면 창고로 흘려보냈다. 그러면 "누가 사러 왔나"가 뜻을 잃는다:
        // 배낭을 비워 두고 사러 온 것과 꽉 채워 온 것이 같은 결과가 되고, 산 물건이 어디
        // 있는지가 그때그때 달라진다(배낭에 있을 수도 창고에 있을 수도 있다).
        // 못 사면 화면이 그렇게 말해 준다(`MerchantUI`의 구매 실패 알림).
        //
        // 살 폰이 없을 때만(월드맵 마을 — 거래 창을 폰 없이 연다) 계정이 받는다.
        int stored = buyer != null
                   ? PlaceInto(buyer, entry, at, rotated)
                   : account.Receive(entry.Item, 1, entry.Quality);

        if (stored == 0)
        {
            // 방금 뺀 자리로 그대로 돌아간다 — 뺀 만큼의 자리는 비어 있다.
            account.GiveGold(entry.Price);
            return false;
        }

        entry.Stock--;

        TakeFromStall(entry.Item, entry.Quality, fromStack);   // 좌판에서도 하나 내린다

        // **판 값은 상인 주머니로 들어간다.** 대칭이라 규칙을 따로 설명할 것이 없고, 덤으로
        // "먼저 사 주면 그만큼 더 받아 준다"가 성립한다 — 빈털터리 상인에게 물건을 팔고 싶으면
        // 그에게서 뭔가 사면 된다. 일부러 만든 것이 아니라 대칭에서 저절로 나온 길이다.
        if (!HasInfiniteGold) Gold += entry.Price;

        return true;
    }

    // **한 번에 여러 개 사기.** 실제로 산 개수를 돌려준다.
    //
    // 반복을 `Shop`이 들면 파는 쪽(`TrySell`)과 모양이 같아지고, 성장을 줄 자리가 한 곳으로
    // 모인다 — 부르는 쪽이 `TryBuy`를 수량만큼 돌리던 시절엔 성장 규약이 사고팔기에서 갈렸다.
    //
    // **성장에 넘기는 것은 개수가 아니라 오간 돈이다**(`Trading.GrowFromTrade`) — 쪼개 사도
    // 총액이 같으면 같이 배운다.
    // 놓은 칸(`at`)은 **첫 개에만** 쓴다 — 나머지는 그 뭉치에 저절로 쌓인다(`TryAdd`가 같은
    // 물건·같은 등급을 합친다). 개마다 자리를 정하려 들면 두 번째부터 갈 곳이 없다.
    static int PlaceInto(Inventory buyer, MerchantStockEntry e, Vector2Int? at, bool rotated)
    {
        if (at.HasValue &&
            buyer.TryAddAt(e.Item, 1, at.Value.x, at.Value.y, rotated, e.Quality))
            return 1;

        return buyer.TryAdd(e.Item, 1, e.Quality);
    }

    // `fromStack`은 **좌판에서 누른 그 뭉치**다. 값표 한 줄이 여러 뭉치를 덮을 수 있으므로
    // (손으로 갈라 놓았을 때) 그 줄의 아무 뭉치가 아니라 **가리킨 것**부터 줄어야 한다 —
    // 3개짜리를 눌렀는데 옆의 4개짜리가 줄면 무엇을 산 것인지 알 수 없다.
    public int TryBuy(MerchantStockEntry entry, TradeAccount account, Inventory buyer, int count,
                      Vector2Int? at = null, bool rotated = false, ItemInstance fromStack = null)
    {
        int bought = 0;
        int paid   = 0;

        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            // **값은 개당으로 모은다** — `entry.Price`는 흥정 폰이 바뀌면 다시 매겨지므로
            // 나중에 `bought × Price`로 되짚으면 실제로 치른 것과 어긋날 수 있다.
            int price = entry != null ? entry.Price : 0;
            if (!TryBuy(entry, account, buyer, i == 0 ? at : null, rotated, fromStack)) break;

            bought++;
            paid += price;
        }

        // **되사기에는 성장이 없다.** 값이 판 순간에 얼어 있어(`AddPlayerStock`) 흥정할 것이
        // 없다. 그리고 **내준 돈이 그만큼 돌아왔다고 적는다**(`ReturnGold`) — 그래야
        // "팔고 되사고 다시 팔기"가 두 번째부터 아무것도 안 준다(위 `TeachableGold` 주석).
        // **매수도 같은 셈을 탄다**(`TeachableGold`). 매도만 netting하면 두 상인 사이로 물건
        // 하나를 돌리는 고리가 **매수 쪽에서 계속 배운다** — 값의 차이만 물고 스탯을 얻는다.
        // 같은 돈으로 두 번 배우지 않는다는 규칙은 방향을 안 가린다.
        if (entry != null && entry.FromPlayer) ReturnGold(paid);
        else                                   Trading.GrowFromTrade(Haggler, TeachableGold(paid));

        return bought;
    }

    // 판매 시도. **실제로 판 개수를 돌려준다**(0 = 실패).
    // 한 번에 하나씩만 팔리면 스택 아이템을 비우는 데 클릭이 수십 번 든다.
    //
    // 한 개씩 도는 이유: 대금이 안 들어가면(창고 만적·배낭 만적) 거기서 멈춰야 하는데,
    // 한꺼번에 계산하면 "절반만 팔린" 상태를 되돌릴 방법이 없다. 개당 원자성을 지킨다.
    public int TrySell(ItemInstance inst, Inventory sourceInv, TradeAccount account, int count = 1)
    {
        if (Data == null || inst == null || sourceInv == null || account == null) return 0;

        int price = GetSellPrice(inst);
        if (price <= 0) return 0;

        int sold = 0;
        int want = Mathf.Clamp(count, 1, inst.Quantity);

        for (int i = 0; i < want; i++)
        {
            // **상인 주머니가 바닥나면 거기서 멈춘다.** 개당으로 재는 덕에 "50개 중 12개까지만
            // 사 줬다"가 저절로 나온다 — 한꺼번에 계산하면 부족할 때 통째로 거절하게 되고,
            // 그러면 "얼마까지 되는지"를 손으로 세어 봐야 한다.
            if (!HasInfiniteGold && Gold < price) break;

            // **좌판에 실제로 놓아 보는 것이 자리 판정이다.** 주머니와 나란한 둘째 한도이고,
            // 개당으로 재므로 여기서도 "50개 중 12개까지만"이 저절로 나온다(스택으로 얹히는
            // 것은 새 칸을 안 먹으니 화살 한 칸에 더 쌓는 것은 늘 된다).
            //
            // **`HasRoomFor`(=`CanFit`)로 묻고 말면 안 된다** — 그건 빈 칸을 **넓이로만** 세서
            // 조각남을 못 본다(스스로 "후하게 나올 수 있다"고 적어 둔 판정이다). 빈 칸 여덟이
            // 뿔뿔이 흩어져 있으면 2×4 갑옷에게 "된다"고 답하고, 그 말을 믿고 값을 치른 뒤
            // `TryAdd`가 조용히 실패해 **판 물건이 세상에서 사라진다.** 넣어 보고 그 답을 쓰면
            // 어긋날 데가 없다.
            if (Stall.TryAdd(inst.Data, 1, inst.Quality) < 1) break;

            // **되돌릴 때 뺄 그 뭉치를 잡아 둔다.** 안 잡으면 롤백이 `prefer` 없이 같은 줄의
            // **아무 뭉치**(대개 상인의 제 물건)를 빼서, 좌판은 줄었는데 `Stock`은 안 줄어
            // 값표와 좌판이 어긋난다.
            var added = Stall.FindStack(inst.Data, inst.Quality);

            // 대금 입금을 먼저 확정한 뒤 물건을 뺀다 — 순서를 뒤집으면 받을 곳이 꽉 찼을 때
            // 물건만 사라진다. **틀어지면 좌판에 얹은 것도 도로 내린다.**
            int paid = account.GiveGold(price);
            if (paid < price)
            {
                if (paid > 0) account.TakeGold(paid);
                TakeFromStall(inst.Data, inst.Quality, added);
                break;
            }

            if (!sourceInv.RemoveOne(inst))
            {
                account.TakeGold(price);
                TakeFromStall(inst.Data, inst.Quality, added);
                break;
            }

            // **거래가 확정된 뒤에 뺀다** — 물건을 못 뺐을 때(위 되돌림) 상인 돈까지
            // 되돌릴 일이 없게. 되돌리는 자리가 적을수록 어긋날 데도 적다.
            if (!HasInfiniteGold) Gold -= price;
            sold++;
        }

        // 목록(값표)에도 올린다 — 좌판에 실제로 놓인 만큼만. **받은 값 그대로** 되판다.
        if (sold > 0) AddPlayerStock(inst.Data, inst.Quality, sold, price);

        // **오간 돈만큼 배운다** — 개수도 건수도 아니다(`Trading.GrowFromTrade` 주석).
        Trading.GrowFromTrade(Haggler, TeachableGold(sold * price));
        return sold;
    }

    // ── 같은 돈으로 두 번 배우지 않는다 ──────────────────────────────────────
    //
    // **팔았다 되사고 다시 파는 고리를 막는다.** 되사기가 판 값 그대로라(`AddPlayerStock`)
    // 왕복 손익이 0인데 파는 쪽 성장은 팔 때마다 붙는다 — **밑천 없이 사교를 올리는 길**이
    // 열린다(되사기 자체에 성장을 안 붙인 것만으론 고리가 안 끊긴다).
    //
    // **상한은 안 둔다.** 하루에 얼마까지라고 못 박으면 그 위로는 아무리 팔아도 안 늘어
    // "돈을 쏟아부으면 오른다"는 이 축의 뜻이 사라진다(`Trading.GoldPerPoint` 주석의 그 판단).
    //
    // 대신 **이 상인이 나에게 *순수하게* 내준 돈**만 센다: 팔면 늘고 되사면 그만큼 돌아간다.
    // 이미 배움으로 친 액수(`_taught`)를 넘어선 만큼에만 성장이 붙으므로
    //
    //   · 팔고 되사고 다시 팔기 → 순액이 제자리라 **두 번째부터 아무것도 안 준다**
    //   · 새 물건을 계속 팔기   → 순액이 계속 늘어 **천장 없이 배운다**
    //
    // 물건에 "되산 것" 표식을 다는 길도 있었는데, 그러면 뭉치가 합쳐지고 쪼개질 때마다 그
    // 표식이 어디로 가는지를 정해야 한다 — 축이 하나 는다. **돈은 합쳐지고 쪼개져도 액수가
    // 그대로**라 셈이 흔들리지 않는다.
    //
    // 되산 뒤에 새 물건을 팔면 옛 순액을 회복할 때까지는 안 배우는데, 그건 틀린 게 아니다 —
    // 같은 돈이 오갔을 뿐이고 그 돈으로는 이미 배웠다.
    int _netPaid;   // 이 상인이 내게 내주고 아직 안 돌려받은 돈
    int _taught;    // 그중 이미 배움으로 친 액수 (줄지 않는다)

    int TeachableGold(int gross)
    {
        if (gross <= 0) return 0;

        _netPaid += gross;

        int eligible = Mathf.Clamp(_netPaid - _taught, 0, gross);
        _taught += eligible;
        return eligible;
    }

    // 되샀다 — 그만큼은 이 상인이 도로 가져갔다.
    void ReturnGold(int amount) => _netPaid = Mathf.Max(0, _netPaid - amount);

    // 좌판에서 하나 내린다.
    //
    // **가리킨 뭉치가 있으면 거기서 먼저 뺀다**(`prefer`) — 같은 물건·같은 등급이라도 손으로
    // 갈라 놓으면 뭉치가 여럿이고, 그때 아무거나 줄이면 **내가 누른 것과 줄어드는 것이 다르다.**
    // 그 뭉치가 이미 비었으면 같은 줄의 다른 뭉치에서 뺀다(값표는 줄 단위라 총량만 맞으면 된다).
    void TakeFromStall(ItemData item, ItemQuality quality, ItemInstance prefer = null)
    {
        if (prefer != null && prefer.Data == item && prefer.Quality == quality &&
            prefer.Quantity > 0 && Stall.RemoveOne(prefer)) return;

        foreach (var inst in Stall.Items)
            if (inst.Data == item && inst.Quality == quality) { Stall.RemoveOne(inst); return; }
    }

    // **내가 판 물건을 값표에 올린다 — 되사는 값은 판 값 그대로.**
    //
    // 좌판(물리)에는 이미 놓았고 여기는 값과 기한을 붙이는 자리다 — 그래서 `SyncStall`을
    // 부르지 않는다(부르면 방금 놓은 것을 지웠다 다시 놓는다).
    //
    // ── 왜 판 값 그대로인가 ──────────────────────────────────────────────────
    //
    // 한때 상인이 되파는 값(`BaseValue × 등급 × 지역배율`)을 붙였다. 그러면 방금 판 검을
    // 도로 사는 데 **네 배 가까이** 든다 — 잘못 판 것을 무를 길이 사실상 없고, 판 것을 다시
    // 사는 일이 늘 손해라 좌판에 올라간다는 사실 자체가 아무 뜻이 없다.
    //
    // **값은 판 순간에 얼어붙는다**(`paidPrice`). 이게 규칙의 전부인 동시에 안전장치다 —
    // 되살 때 흥정 배율을 다시 곱하면(`Reprice`) **판 값보다 싸게 사지므로 돈이 찍힌다**:
    // 사교 130이면 팔 때 +18%, 살 때 −18%라 왕복 한 번에 3할 넘게 남고, 그 짓을 무한히
    // 반복할 수 있다. 그래서 `Reprice`는 이 항목을 건드리지 않는다.
    //
    // **상인의 제 물건과는 안 합친다.** 합치면 그 뭉치가 하루 뒤 굴림표와 함께 사라지면서
    // 내 물건까지 데려가고, 값도 상인 값으로 덮인다. 같은 칸에 나란히 쌓이는 것은 좌판이
    // 알아서 한다(같은 물건·같은 등급이면 한 뭉치) — 보이는 것은 하나여도 값표는 둘이고,
    // `EntryFor`가 재고 남은 앞줄부터 고르므로 사는 쪽에서도 어긋나지 않는다.
    void AddPlayerStock(ItemData item, ItemQuality quality, int count, int paidPrice)
    {
        if (Data == null || item == null || count <= 0) return;

        int expire = GameTimeManager.CurrentDay + Mathf.Max(1, Data.PlayerStockDays);
        int price  = Mathf.Max(1, paidPrice);

        foreach (var e in Stock)
            if (e.FromPlayer && e.Item == item && e.Quality == quality)
            {
                e.Stock    += count;
                e.ExpireDay = expire;   // 더 얹으면 기한도 새로 — 뭉치 하나에 기한이 둘일 순 없다

                // **값은 처음 값을 지킨다.** 한 뭉치에 값이 둘일 수는 없는데, 나중 값으로
                // 덮으면 사교가 자란 뒤 하나만 더 팔아 **먼저 판 것의 되사기 값까지 올릴** 수
                // 있다(반대로도 마찬가지). 먼저 정해진 값이 그대로 가는 편이 예측된다.
                return;
            }

        Stock.Add(new MerchantStockEntry
        {
            Item       = item,
            Quality    = quality,
            Stock      = count,
            ListPrice  = price,
            Price      = price,   // 흥정을 다시 곱하지 않는다 — 위 주석 참고
            FromPlayer = true,
            ExpireDay  = expire,
        });
    }

    // **매입가는 여기 하나다** — `TrySell`도 UI도 이걸 부른다. 전엔 둘이 각자
    // `Data.CalcSellPrice`를 불렀는데, 흥정 배율이 한쪽에만 붙으면 "보이는 값과 들어오는
    // 값이 다르다"가 된다.
    //
    // 구매가와 달리 **읽을 때마다 낸다** — 파는 값은 진열처럼 미리 붙여 둔 것이 아니라
    // 내가 내미는 물건마다 그 자리에서 나오는 값이라, 굳혀 둘 자리가 없다.
    public int GetSellPrice(ItemInstance inst)
    {
        if (inst == null || Data == null) return 0;

        int price = Data.CalcSellPrice(inst.Data, inst.Quality);
        if (price <= 0) return 0;

        return Mathf.Max(1, Mathf.RoundToInt(price * Trading.SellFactor(Haggler)));
    }
}
