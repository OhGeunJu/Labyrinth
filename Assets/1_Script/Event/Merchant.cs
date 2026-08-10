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
        set { _data = value; _shop = new Shop(_data); }
    }

    Shop _shop;
    public Shop Shop => _shop ??= new Shop(_data);

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
        if (_data != null) _shop = new Shop(_data);
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
