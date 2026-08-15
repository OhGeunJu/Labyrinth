using System;
using System.Collections.Generic;
using UnityEngine;

// **폰에 이걸 붙이면 상인이 된다.** 어느 종족의 프리팹이든 `Merchant` + `MerchantData`를
// 얹으면 그 물건을 파는 상인이다 — 고블린 무기상인과 엘프 식료상인이 서로 다른 프리팹
// 계보를 만들지 않는다. 역할(`NpcData`)은 상인이 **어떻게 행동하는가**(자리를 지킨다 /
// 들렀다 떠난다)를 정할 뿐, 무엇을 파는지는 여기가 든다.
//
// **재고와 거래 자체는 `Shop`이 한다** — 이 컴포넌트는 그걸 씬에 세우고(클릭 판정·일일 갱신),
// 값을 치를 지갑으로 **거점 창고**(`StorageAccount`)를 물려 주는 역할만 한다.
// 월드맵 마을은 오브젝트가 없어 같은 `Shop`을 `TownMarket`이 든다.
//
// **`Shop`은 컴포넌트마다 하나다.** 같은 `MerchantData`를 쓰는 상인이 둘이어도 재고는
// 따로 돈다 — `MerchantData`는 "무엇을 팔 수 있는가"(후보 목록)이고, 오늘 진열대에 무엇이
// 몇 개 올랐는지는 그 상인의 것이다. 한쪽에서 사도 다른 쪽 재고는 그대로다.
public class Merchant : MonoBehaviour, IWorldClickable, IClickGate
{
    public static event Action<Merchant> OnSelected;

    [Tooltip("이 상인이 파는 것. 비우면 역할 에셋(NpcData.MerchantData)의 기본값을 쓴다.")]
    [SerializeField] MerchantData _data;
    public MerchantData Data
    {
        get => _data;
        // **다시 짓지 않고 버린다** — 다음에 물어볼 때 게터가 짓는다. 여기서 바로 지으면
        // 진열 수가 우호도를 타는데(`FactionRelations.SlotBonus`) 스폰 시점엔 세력 판정이
        // 아직 안 설 수 있다.
        set { _data = value; _shop = null; }
    }

    Shop _shop;

    // **세력은 붙들어 두지 않고 물어볼 때마다 맞춘다** — 폰의 소속은 바뀔 수 있다
    // (회유로 넘어오거나 계약이 끝나거나). `Awake`에서 한 번 물리면 넘어온 상인이
    // 옛 세력의 값으로 장사한다.
    public Shop Shop
    {
        get
        {
            _shop ??= new Shop(_data, MyFaction);
            _shop.Faction = MyFaction;
            return _shop;
        }
    }

    // **파는 사람의 세력** — 우호도가 값에 얹히는 통로다(`Shop.Faction`).
    // 상인은 폰에 붙는 컴포넌트라 소속은 같은 오브젝트의 `Faction`이 이미 알고 있다.
    FactionData MyFaction => GetComponent<Faction>()?.Data;

    // 프리팹/씬에서 **손으로 물려 둔** 상품인가 — 역할 기본값이나 스폰 주입으로 채워진 것과
    // 구별한다. 저작한 것은 밖에서 덮으면 안 된다: "이 사람은 늘 이것만 판다"가 성립해야
    // 고정 상인(고블린 방어구 상인 등)을 프리팹 하나로 세울 수 있다.
    public bool HasAuthoredData { get; private set; }

    public List<MerchantStockEntry> CurrentStock => Shop.Stock;

    // 이 상인의 지갑은 **거점 창고**다. 상인이 선 자리에서 가장 가까운 창고로 넣고 뺀다.
    public TradeAccount Account => new StorageAccount(transform.position);

    void Awake()
    {
        GameTimeManager.OnDayChanged += RefreshStock;
        // 인스펙터에서 직접 Data가 지정된 경우 초기 재고 생성
        HasAuthoredData = _data != null;

        // **여기서 상점을 짓지 않는다.** 진열 종류 수가 세력 우호도를 타는데
        // (`FactionRelations.SlotBonus`), `Awake`는 `Faction.Player`가 아직 `_all`에 안
        // 들어왔을 수 있는 시점이라 보너스가 0으로 굳는다 — 그러면 첫 갱신(자정)까지
        // 진열이 관계를 반영 못 한다. 게터가 필요할 때 짓는다.
    }

    void OnDestroy() => GameTimeManager.OnDayChanged -= RefreshStock;

    // **쓰러진 상인과는 거래하지 않는다.** 기절은 폰의 상태지만 `Merchant`는 그것을 모르는
    // 별개 컴포넌트라, 클릭 라우팅이 그대로 상점을 열어 줬다 — 때려눕히고 물건을 사는 길이
    // 열려 있었다. 시체도 마찬가지다(`BecomeCorpse`는 `Merchant`를 떼지 않는다).
    public bool CanTrade
    {
        get
        {
            if (GetComponent<Corpse>() != null) return false;
            var brain = GetComponent<UnitBrain>();
            return brain == null || brain.Blackboard == null || !brain.Blackboard.IsKnockedOut;
        }
    }

    // 쓰러져 있으면 클릭을 안 받는다 → 상점 대신 폰 정보가 뜬다(`SelectionManager`).
    // 거절을 알림으로 띄우지 않는 이유는 `IClickGate` 주석에.
    public bool CanClick => CanTrade;

    // SelectionManager의 단일 레이캐스트가 이 상인을 클릭 판정 시 호출.
    public void OnWorldClick()
    {
        if (!CanTrade) return;
        OnSelected?.Invoke(this);
    }

    public void RefreshStock() => Shop.Refresh();
}
