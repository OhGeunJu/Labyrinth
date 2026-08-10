using System;
using UnityEngine;

// **우리 돈. 전역 잔고 하나.**
//
// 돈은 창고에도 배낭에도 나뉘어 있지 않다 — 얼마를 가졌는지 묻는 곳이 하나뿐이라
// 거래·고용·몸값이 전부 같은 숫자를 본다. 예전엔 창고 골드와 배낭 골드가 따로 놀아서
// "원정대에 돈이 없어서 못 산다"가 자주 났고, 새 기능마다 배낭+창고 순서를 다시 짜야 했다.
//
// **그런데 아이템이 사라진 것은 아니다.** 동전은 여전히 세상에 떨어지고 시체에 남는다 —
// 다만 **쓰는 물건**이라서, 배낭이든 시체든 창고든 **누르면** 잔고가 된다
// (`InventoryUI.TryClaimCoins`). 그래서 "시체를 뒤져 돈을 챙긴다"가 그대로 살아 있다.
//
// **자동으로 빨아들이지 않는 것이 요점이다.** 들어오는 족족 흡수하게 만들면
// "어느 인벤토리가 우리 것인가"를 가리는 스위치가 필요해지고(적 주머니·시체는 빼야 한다),
// 시작할 때 이미 놓여 있던 동전을 걷어들이는 전환도 있어야 한다. **누르는 사람이 곧 임자**로
// 두면 둘 다 필요 없다 — 남의 시체에 든 동전은 내가 누르기 전까지 그냥 남의 것이다.
//
// 동전은 무게가 0이다(`Gold.asset`). 어차피 눌러서 없앨 물건이라 지고 다니는 값을
// 물릴 이유가 없고, 무게가 있으면 전투 뒤 주운 동전 때문에 과적이 걸린다.
public static class Wallet
{
    public static int Balance { get; private set; }

    // 잔고가 바뀌었다 — 상점·몸값 창이 즉시 따라가게.
    public static event Action<int> OnChanged;

    public static bool CanAfford(int amount) => amount <= Balance;

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        Balance += amount;
        OnChanged?.Invoke(Balance);
    }

    // **전액 아니면 0.** 절반만 빼면 되돌릴 방법이 없다(`TradeAccount`가 돈을 다루는 계약 그대로).
    public static bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (amount > Balance) return false;

        Balance -= amount;
        OnChanged?.Invoke(Balance);
        return true;
    }

    // 씬을 다시 시작해도 static은 살아남는다(도메인 리로드를 끈 경우) —
    // 지난 플레이의 잔고를 물고 시작하면 무엇이 정상인지 알 수 없다.
    public static void Reset()
    {
        Balance = 0;
        OnChanged?.Invoke(Balance);
    }

    // 잔고는 실행 사이에 남으면 안 된다(도메인 리로드를 끄면 static이 살아남는다).
    // **씬 배선이 필요 없다** — `Wallet`을 들 오브젝트를 만들면 그걸 빠뜨린 씬에서 돈이 사라진다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void OnPlayStart()
    {
        Reset();
        // 화폐 아이템 캐시도 같이 버린다 — 지난 실행의 `ItemData`를 물고 있으면
        // 레지스트리를 다시 지은 뒤 같은 골드인데 다른 객체가 되어 비교가 어긋난다.
        _coin     = null;
        _searched = false;
    }

    // ── 무엇이 돈인가 ─────────────────────────────────────────────────────────
    //
    // **레지스트리가 답한다** — `ResourceGroup.Currency` 자원이 곧 돈이다
    // (`EventManager`가 발견 이벤트에서 화폐를 걸러낼 때 쓰는 것과 같은 판정).
    // 씬·프리팹 배열에 적어 두지 않는 이유는 이 저장소에서 네 번 겪은 그것이다:
    // 목록을 배선으로 두면 런타임 생성물(JSON·모드 아이템)에서 반드시 빈다.
    static ItemData _coin;
    static bool     _searched;

    public static ItemData Coin
    {
        get
        {
            if (_searched) return _coin;
            _searched = true;

            foreach (var kv in DataManager.AllItems)
                if (kv.Value is ResourceItemData r && r.Group == ResourceGroup.Currency)
                {
                    _coin = r;
                    break;
                }

            if (_coin == null)
                Debug.LogWarning("[Wallet] 화폐(ResourceGroup.Currency) 아이템이 없습니다 — " +
                                 "시체·바닥에 동전이 떨어지지 않고 상점 값도 못 매깁니다.");
            return _coin;
        }
    }

    public static bool IsCurrency(ItemData item) =>
        item is ResourceItemData r && r.Group == ResourceGroup.Currency;
}
