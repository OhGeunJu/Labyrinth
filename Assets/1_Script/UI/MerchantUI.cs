using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 상점 창. **어느 상점을 여느냐와 누가 값을 치르느냐는 밖에서 정해 준다** —
// 필드맵 상인은 `Merchant.OnSelected`로 연다.
// 그래서 이 창은 "창고 상점"과 "원정대 상점"을 구분하지 않는다(`TradeAccount`가 가른다).
public class MerchantUI : MonoBehaviour
{
    public static Shop          ActiveShop   { get; private set; }
    public static RectTransform SellZoneRect { get; private set; }

    // 월드맵 마을 등 상인 오브젝트가 없는 곳에서 여는 진입점.
    // `traders`가 있으면 그 폰들의 배낭으로 사고판다(없으면 필드처럼 고른 폰을 쓴다).
    public static void OpenFor(Shop shop, TradeAccount account,
                               IReadOnlyList<UnitBrain> traders, string title = null)
        => _instance?.Open(shop, account, traders, title);

    // DraggableItem 드롭 판매용. count는 한 번에 팔 개수.
    public static bool TrySellItem(ItemInstance inst, int count = 1)
        => _instance != null && _instance.ExecuteSell(inst, count);

    // ── 드래그·우클릭으로 사고팔기 ────────────────────────────────────────────
    //
    // **"이걸 저기로"가 상점에서는 곧 거래다.** 배낭↔창고에서 우클릭 한 번이 물건을 건너보내는
    // 규칙(`InventoryGridUI.FindCounterpart`)을 그대로 늘린 것이지 새 조작이 아니다 —
    // 상점이 열려 있으면 "저기"가 상점이고, 건너가는 데 값이 붙을 뿐이다.
    //
    // 진열대는 `FindCounterpart`에서 빠져 있으므로(공짜로 넘어가면 안 된다) 그 갈림길을
    // 여기서 가로챈다.

    // 상점이 열려 있을 때의 우클릭. 처리했으면 true(그러면 평소의 "반대편으로 보내기"는 안 돈다).
    public static bool HandleRightClick(ItemInstance inst, InventoryGridUI from, int count)
    {
        if (_instance == null || ActiveShop == null || inst == null || from == null) return false;

        // **되든 안 되든 "처리했다"고 답한다.** 상점이 열려 있는 동안 우클릭의 뜻은 거래
        // 하나뿐이라, 실패했다고 평소의 "반대편 통으로 보내기"로 흘러가면 **못 판 물건이
        // 창고로 뛰어간다** — 시킨 적 없는 이동이다(사유는 알림이 이미 말해 준다).
        if (from.IsShop) _instance.BuyInstance(inst, count, null);
        else             _instance.ExecuteSell(inst, Mathf.Max(1, count));

        return true;
    }

    // 격자를 건너간 드래그. 좌판 → 배낭이면 사고, 배낭 → 좌판이면 판다.
    //
    // `at`은 **놓은 칸**이다 — 산 물건이 첫 빈 칸으로 튀면 "여기에 놓았다"가 거짓말이 된다.
    // `detached` = 끌고 있는 조각이 **좌판 밖에** 있다(쪼개서 손에 든 상태).
    public static bool HandleCarryTrade(ItemInstance inst, InventoryGridUI from, InventoryGridUI to,
                                       bool detached, Vector2Int at, bool rotated)
    {
        if (_instance == null || ActiveShop == null || inst == null) return false;
        if (from == null || to == null) return false;

        // **사는 쪽은 끌어다 놓은 그 통, 그 칸으로 들어간다** — 어느 배낭에 넣을지도 어디에
        // 놓을지도 손으로 정한 것이므로 창의 "거래 폰"이나 자동 배치보다 그쪽이 이긴다.
        if (from.IsShop && !to.IsShop)
        {
            // **손에 든 조각을 좌판에 되돌려 놓고 판다.** 그래야 사는 길이 하나로 남는다 —
            // "이미 나와 있으니 좌판에서는 빼지 말라"는 예외를 `Shop`에 만들면, 사는 문이
            // 셋인데(버튼·우클릭·드래그) 그중 하나만 다른 규칙을 타게 된다.
            //
            // **되돌리기는 반드시 성공한다**: 갈라져 나온 그 뭉치가 좌판에 그대로 있고 그만큼
            // 자리가 비어 있으므로 `TryAdd`가 거기에 도로 합친다(칸을 새로 안 먹는다).
            if (detached) _instance._shop?.Stall.TryAddLike(inst);

            _instance.BuyInstance(inst, inst.Quantity, to.Inventory, at, rotated);

            // **되돌려 놓은 이상 드래그는 끝난 것으로 친다** — 실패했다고 미배치로 돌리면
            // `OnEndDrag`가 손에 든 것을 **한 번 더** 좌판에 넣는다(개수가 는다).
            return true;
        }

        // 파는 쪽은 **끌어온 통에서** 뺀다(`TrySell`이 출처를 받는다). 여기서 실패하면
        // 미배치가 맞다 — `OnEndDrag`가 손에 든 것을 출처로 되돌린다.
        if (!from.IsShop && to.IsShop) return _instance.ExecuteSell(inst, inst.Quantity, from.Inventory);

        return false;   // 좌판끼리 — 있을 수 없다
    }

    // 수량 칸에 적힌 수를 읽는다. 비었거나 숫자가 아니면 1.
    // **칸이 없으면(생성기를 아직 안 돌렸으면) 1개씩** — 조작이 죽는 것보다 낫다.
    public static int ReadCount(TMP_InputField field, int max)
    {
        if (max < 1) return 0;
        if (field == null || !int.TryParse(field.text, out int n)) n = 1;
        return Mathf.Clamp(n, 1, max);
    }

    static MerchantUI _instance;

    [Header("공통")]
    [SerializeField] GameObject      panel;
    [SerializeField] RectTransform   sellZone;
    [SerializeField] TextMeshProUGUI merchantNameText;
    [SerializeField] TextMeshProUGUI goldText;

    [Header("구매")]
    [SerializeField] Transform            stockContainer;

    [Tooltip("카드 보기 항목 (정사각 타일). 비우면 카드 보기가 꺼진다.")]
    [SerializeField] MerchantStockEntryUI tileEntryPrefab;

    [Tooltip("칸↔카드 전환 버튼 (씬의 StockLabel 우측). 글자는 런타임에 갈아 끼운다.")]
    [SerializeField] Button viewToggleButton;

    [Tooltip("흥정할 폰을 고르는 버튼 (씬의 StockLabel 우측, 보기 전환 왼쪽).")]
    [SerializeField] Button hagglerButton;

    [Tooltip("좌판 칸을 다시 채워 넣는 버튼 (씬의 StockLabel 우측 맨 왼쪽).")]
    [SerializeField] Button tidyButton;

    [Tooltip("재고 목록 위의 제목 줄(씬의 StockLabel/Label). **흥정 상태를 여기 적는다** — " +
             "제목 바(44px)에 두 줄을 넣으면 잘려서 안 보인다. 비우면 그 줄만 안 뜬다.")]
    [SerializeField] TextMeshProUGUI stockLabelText;
    [SerializeField] Button               buyButton;
    [Tooltip("한 번에 살 개수. 비우면 1개씩")]
    [SerializeField] TMP_InputField       buyQuantity;
    [Tooltip("살 수 있는 최대치를 수량 칸에 넣는 버튼")]
    [SerializeField] Button               buyMaxButton;

    [Header("연동")]
    [SerializeField] InventoryUI inventoryPanel;


    // ── 보기 상태 ────────────────────────────────────────────────────────────
    //
    // **둘을 오간다.** 하나로 못 정한 것은 재고를 보는 목적이 둘이기 때문이다 —
    // *"뭘 파나"* 훑을 때와 *"어떤 물건인가"* 들여다볼 때 원하는 화면이 다르다.
    //
    // | 보기 | 무엇이 보이나 | 언제 |
    // |---|---|---|
    // | **칸** | 배낭과 **똑같은 격자** — 아이콘·개수, 값은 툴팁 | 훑어볼 때. **2×4 갑옷이 자리를 얼마나 먹는지 사기 전에 보인다** |
    // | 카드 | 96×112 타일에 그림·이름·등급·값·재고가 같이 | 값을 견주며 고를 때 |
    //
    // **기본은 칸이다.** 배낭·창고가 이미 격자라 눈이 같은 방식으로 움직이고, 무엇보다
    // **살 물건이 내 배낭에 들어가는지**를 사기 전에 볼 수 있다 — 카드는 그걸 못 보여 준다.
    //
    // **한때 목록(가로로 긴 행) 보기가 셋째로 있었다.** 카드와 같은 것을 좁게 적을 뿐이라
    // 카드가 있으면 볼 이유가 없고, 보기가 셋이면 원하는 화면까지 두 번 눌러야 한다.
    // `entryPrefab` 배선 자리도 같이 걷어냈다 — **아무도 안 읽는 자리가 남으면** 다음 사람이
    // 비었다고 채우려 든다(프리팹 에셋 자체는 남아 있으니 쓸 데가 없으면 지울 것).
    //
    // **static이다.** 상점을 닫았다 열 때마다 기본으로 돌아가면, 보기를 바꾼 사람은
    // 상인을 만날 때마다 다시 눌러야 한다. 고른 보기는 그 사람의 취향이지 상점의 성질이 아니다.
    public enum StockView { Cells, Cards }

    static StockView _view = StockView.Cells;

    // 지금 컨테이너에 실제로 들어 있는 보기. **다를 때만 통째로 비운다** — 같은 보기 안에서는
    // 각자 제 방식으로 갱신하면 되고(칸 보기는 `InventoryGridUI`가 제 자식을 관리한다),
    // 매번 비우면 살 때마다 격자가 통째로 다시 지어진다.
    StockView _builtView = StockView.Cards;
    bool      _builtOnce;

    TextMeshProUGUI _viewToggleLabel;

    bool                                _didForceOpenInventory;
    Shop                                _shop;
    TradeAccount                        _account;
    // 거래하러 온 사람들 — 원정대 멤버이거나(월드맵) 창을 열 때 고른 폰들이다(필드).
    // 여기서 **배낭 주인**(순환으로 고른다)과 **흥정하는 사람**(사교 최고) 둘 다 나온다.
    IReadOnlyList<UnitBrain>            _traders;
    int                                 _traderIndex;
    MerchantStockEntry                  _selectedBuy;

    // **칸 보기에서 고른 그 뭉치.** 값표 한 줄(`_selectedBuy`)이 좌판의 여러 뭉치를 덮을 수
    // 있어서(손으로 갈라 놓았을 때) 줄만으로는 "무엇을 사는가"에 답이 안 된다 — 3개와 4개로
    // 갈라 둔 것을 3개 쪽에서 눌렀으면 살 수 있는 것도 그 3개고, 줄어드는 것도 그 뭉치다.
    // 카드 보기는 줄을 고르는 것이라 여기가 비고, 그때는 줄 전체가 대상이다.
    ItemInstance                        _selectedStack;
    MerchantStockEntryUI                _selectedBuyUI;
    readonly List<MerchantStockEntryUI> _buyEntryUIs = new();
    SelectionManager                    _selection;

    void Awake()
    {
        _instance = this;
        if (panel)    panel.SetActive(false);
        if (sellZone) SellZoneRect = sellZone;
        if (buyButton) { buyButton.interactable = false; buyButton.onClick.AddListener(OnBuyClicked); }
        if (buyMaxButton) buyMaxButton.onClick.AddListener(FillMaxBuyQuantity);
        SetupViewToggle();
        if (hagglerButton) hagglerButton.onClick.AddListener(OpenHagglerPicker);
        if (tidyButton)    tidyButton.onClick.AddListener(TidyStall);
        Merchant.OnSelected              += OpenForMerchant;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;

        // **지갑이 밖에서도 바뀐다.** 사고파는 것 말고도 — 옆에 열린 배낭 창에서 동전 뭉치를
        // 더블클릭하면 잔고가 는다(`InventoryUI.TryClaimCoins`). 구독하지 않으면 `골드: N G`가
        // 옛 액수로 남고, **이제 살 수 있는 물건인데 [구매]가 잠긴 채**로 있다.
        // `InventoryUI`는 같은 이유로 이미 구독하고 있었다.
        Wallet.OnChanged += OnWalletChanged;
    }

    void OnWalletChanged(int _)
    {
        if (panel == null || !panel.activeSelf) return;
        RefreshGold();
        RefreshBuyButton();
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Start()
    {
        _selection = Object.FindFirstObjectByType<SelectionManager>();
        if (inventoryPanel == null) inventoryPanel = Object.FindFirstObjectByType<InventoryUI>();
    }

    void OnDestroy()
    {
        _instance = null;
        Merchant.OnSelected              -= OpenForMerchant;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
        Wallet.OnChanged                 -= OnWalletChanged;
    }

    void Update()
    {
        // 닫기 버튼은 공용 `CloseButton`이 **패널을 그냥 끄기만** 한다(UI 규약).
        // 그러면 여기서 해야 할 뒷정리 — 판매 모드 해제·인벤토리 닫기·`ActiveShop` 비우기 —
        // 가 안 돈다. 그대로 두면 창을 닫아도 폰 인벤토리가 판매 모드에 남고,
        // 드래그 판매가 닫힌 상점을 상대로 계속 먹는다.
        // **이 컴포넌트는 루트에 있어 패널이 꺼져도 Update가 돌므로** 뒤늦게라도 정리할 수 있다.
        if (_shop != null && panel != null && !panel.activeSelf) { Close(); return; }

        // **거래 중에 상인이 사라지거나 쓰러지면 창을 닫는다.** 열 때만 막으면 창을 띄워 둔 채
        // 때려눕히고 계속 사는 길이 남고, **행상인은 머물 시간이 끝나면 스스로 소멸하는데**
        // `Shop`은 MonoBehaviour가 아니라서 그 뒤에도 창이 멀쩡히 살아 거래가 계속된다.
        // 그래서 "필드 상인으로 열었다"를 따로 기억한다 — 파괴된 참조는 `_merchant != null`이
        // 거짓이 되어 그것만으로는 구분이 안 된다(월드맵 시장은 애초에 상인이 없다).
        if (_openedForMerchant && (_merchant == null || !_merchant.CanTrade)) { Close(); return; }

        if (panel && panel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        // **흥정하는 사람이 바뀌었는지 지켜본다.** 필드에서는 맵 전체에서 고르므로
        // (`Trading.BestOnField`) 그 사람이 쓰러지거나 죽거나 원정을 떠나면 다음 사람으로
        // 넘어가고, 값이 그에 따라 달라져야 한다. 바뀌었을 때만 일하므로(`SyncHaggler`의
        // 조기 반환) 매 프레임 도는 값은 비교 하나다.
        if (_shop != null) SyncHaggler();

        // 필드 상인을 폰 없이 열어 뒀다면, 폰을 고르는 순간 받아들인다(위 주석 참고).
        // 월드맵 시장은 명단을 밖에서 받으므로 여기 안 걸린다.
        if (_openedForMerchant && _traders == null) AdoptSelection();
    }

    // 필드맵 상인 클릭 — 지갑은 거점 창고, 거래 폰은 지금 고른 폰.
    void OpenForMerchant(Merchant merchant)
    {
        if (merchant == null) return;

        // **누구의 가게인지 제목에 적는다.** 재고는 상인마다 따로 도는데(각자 자기 `Shop`),
        // 제목이 종류 이름뿐이면 무기 상인이 둘 있을 때 어느 쪽 창인지 알 수 없다.
        var brain  = merchant.GetComponent<UnitBrain>();
        string who = brain != null ? brain.UnitName : null;
        string title = string.IsNullOrWhiteSpace(who)
            ? merchant.Shop.DisplayName
            : $"{who} ({merchant.Shop.DisplayName})";

        // **고른 폰들을 통째로 넘긴다** — 배낭 순환과 흥정 둘 다 여기서 나온다.
        // 전엔 null을 넘기고 `Selected[0]`을 그때그때 읽었는데, 그러면 ① 여럿을 고른 채
        // 열었을 때 **첫 번째로 잡힌 폰의 배낭에 갇히고**(그 "첫 번째"는 레이캐스트 순서라
        // 고른 사람이 정한 것도 아니다) ② 흥정할 사람을 고를 길이 없다.
        //
        // **창을 열 때 데리고 있던 사람들로 못 박는다** — 창이 떠 있는 동안 뒤의 땅을
        // 잘못 눌러 선택이 바뀌어도 거래 중이던 배낭이 발밑에서 갈리지 않는다.
        // **누가 연 창인지는 `Open` 안에서 정해져야 한다.** 전엔 `Open`이 끝난 뒤에 걸었는데,
        // `Open`이 그 안에서 이미 `SyncHaggler(force)`를 부르므로 **첫 값이 필드 규칙(맵 최고)이
        // 아니라 원정대 규칙(`_traders` 중 최고)으로 매겨졌다** — 다음 프레임에 `Update`가
        // 바로잡느라 창이 뜨자마자 값과 흥정 줄이 한 번 바뀌었다. 방금 없앤 "문에 따라 값이
        // 갈린다"가 창 뜨는 순간에만 남아 있던 셈이다.
        Open(merchant.Shop, merchant.Account, SelectedPawns(), title, merchant);
    }

    // 재고 줄의 고정 부분. 씬의 `StockLabel/Label`에 적힌 것과 같은 말이라 여기 한 번 적고
    // 런타임에 갈아 끼운다 — 흥정 줄이 그 뒤에 붙는다.
    const string StockLabel = "상인 재고";

    string    _title;        // 흥정 줄을 덧붙이기 전의 제목
    UnitBrain _lastHaggler;  // 흥정하는 사람이 바뀌었는지 재는 기준

    // **흥정하는 사람이 바뀌면 값이 따라 움직인다.**
    //
    // 데리고 온 사람들이 그대로면 흥정하는 사람도 그대로라 대개 창을 열 때 한 번이면 되지만,
    // **그 사람이 쓰러지거나 사라지면 값이 달라져야 한다**(`Trading.BestOf`가 다음 사람을
    // 고른다). 바뀌었을 때만 일하므로 매 프레임 도는 값은 비교 하나다 — 매 프레임 목록을
    // 다시 그리면 클릭이 씹힌다.
    void SyncHaggler(bool force = false)
    {
        if (_shop == null) return;

        var pawn = HagglerPawn();
        if (!force && pawn == _lastHaggler) return;

        _lastHaggler  = pawn;
        _shop.Haggler = pawn;
        _shop.Reprice();

        RefreshTitle();

        // **왜 이 값인지를 재고 줄에 적는다.** 값이 조용히 달라지면 상인이 원래 싼 것과 이 폰이
        // 잘 깎은 것이 화면에서 구별되지 않아, 사교를 올린 보람이 어디에도 안 남는다.
        // **배낭 주인과 다를 수 있으므로 이름을 적는 것이 특히 중요하다.**
        //
        // 처음엔 제목에 두 줄로 붙였다가 옮겼다 — **제목 바가 44px이라 둘째 줄이 잘려서 한 번도
        // 안 보였다.** 여기는 목록 위 전체 폭 줄(420px)이라 한 줄로 들어가고, 값 이야기가
        // 값 목록 바로 위에 붙는 것이 자리로도 맞다.
        //
        // **아무도 안 데려왔으면 그렇게 적는다.** 폰을 안 고르고 상인을 누르면 배낭도 안 열리고
        // 흥정도 없는데, 아무 말이 없으면 그것이 고장인지 규칙인지 알 길이 없다.
        RefreshHagglerLine();
        RebuildBuyList();
    }

    // 재고 줄 = **"누가 흥정하고 있고, 그래서 값이 얼마나 달라지나" + 바꾸는 문.**
    void RefreshHagglerLine()
    {
        if (stockLabelText == null) return;

        var pawn = _shop != null ? _shop.Haggler : null;

        string note = pawn == null ? "거래할 폰을 고르세요" : Trading.Note(pawn);

        // **바꾸는 길은 옆의 버튼이 든다**(`hagglerButton`, 씬의 `StockLabel/HagglerButton`).
        // 한때 이 글자 자체를 눌러 열었는데 둘이 나빴다: 글자는 **눌리는 것처럼 안 보여서**
        // 있는 줄도 모르는 기능이 되고, 그렇다고 `[바꾼다]` 같은 꼬리표를 붙이면 이 줄이
        // 길어질수록 그게 먼저 잘려 나간다. 버튼은 자리를 차지하는 대신 눌린다는 것이 보인다.
        // **좌판이 얼마나 찼는지 적는다.** 자리가 판매 한도인데(`Shop.HasRoomFor`) 그 숫자가
        // 화면에 없으면 "좌판에 자리가 없다"가 갑자기 튀어나온 것처럼 보이고, 반대로 **꽉 찬 줄
        // 알았는데 계속 팔리는 것**도 설명이 안 된다(스택으로 얹히는 것은 새 칸을 안 먹는다 —
        // 아이콘이 격자를 덮고 있어도 실제로는 몇 칸 안 쓰는 일이 흔하다).
        string room = "";
        if (_shop != null)
        {
            var stall = _shop.Stall;
            room = $"  <size=85%><color=#9AB>좌판 {stall.UsedCellCount}/{stall.Width * stall.Height}</color></size>";
        }

        stockLabelText.text = $"{StockLabel}{room}   <size=85%><color=#9AB>· {note}</color></size>";
    }

    // ── 누가 흥정할지 고르기 ──────────────────────────────────────────────────
    //
    // **기본은 그 자리 사교 최고**고(자동), 눌러서 사람을 지목할 수 있다.
    //
    // 자동만 있으면 값은 늘 최선이지만 **흥정을 배울 사람이 영영 한 명**이다 — 사교는 쓰는
    // 사람만 자라는 스탯이라(`Trading.GrowFromTrade`) 둘째 협상가를 키울 길이 없어진다.
    // 반대로 매번 고르게 하면 가장 잦은 조작(물건 사기)에 한 단계가 얹힌다. 그래서 **기본값은
    // 자동, 바꾸는 것은 선택**이다.
    //
    // **목록 창을 새로 안 짓는다** — 우클릭 창(`PawnContextMenuUI`)이 이미 목록 그릇이다.
    // 줄 모양·못 누르는 줄·설명 툴팁·밖을 누르면 닫힘이 전부 거기 있고, 새로 지으면 그 규칙이
    // 두 벌이 된다. **버튼만 씬에 있다**(모양·자리의 정본은 씬이다).
    UnitBrain _hagglerOverride;   // 사람이 지목한 폰. null이면 자동

    // **흥정을 맡길 수 있는 사람들 — 고르는 목록과 자동으로 뽑는 목록이 같은 함수다.**
    //
    // 지도 위에서는 **원정대가 곧 "그 자리에 있는 사람들"**이다(거점에 남은 폰은 지도에 없고,
    // 나간 폰은 GameObject가 꺼져 있어 `Faction.All`에도 없다). 필드에서는 맵 전체다.
    List<UnitBrain> HagglerPool()
        => (!_openedForMerchant && _traders != null)
         ? new List<UnitBrain>(_traders)
         : Trading.CandidatesOnField();

    // 이 폰이 지금 그 목록 안에 있나. **매 프레임 도는 자리라 목록을 안 만든다.**
    bool InPool(UnitBrain pawn)
    {
        if (pawn == null) return false;

        if (!_openedForMerchant && _traders != null)
        {
            foreach (var m in _traders) if (m == pawn) return true;
            return false;
        }

        // 필드 — `Faction.All`이 이미 "지금 살아 있고 켜져 있는 것"만 든다(원정 나간 폰은
        // 꺼져 있어 저절로 빠진다). 그래서 여기 있는지만 보면 된다.
        foreach (var f in Faction.All)
            if (f != null && f.IsPlayer && f.gameObject == pawn.gameObject) return true;

        return false;
    }

    void OpenHagglerPicker()
    {
        var menu = PawnContextMenuUI.Instance;
        if (menu == null || _shop == null) return;

        var pool = HagglerPool();

        pool.Sort((a, b) =>
        {
            float sa = a != null && a.Stats != null ? a.Stats.Social : -1f;
            float sb = b != null && b.Stats != null ? b.Stats.Social : -1f;
            return sb.CompareTo(sa);        // 잘하는 사람부터 — 고를 일이 가장 잦다
        });

        var auto = Trading.BestOf(pool);
        var opts = new List<PawnMenuOption>
        {
            // **자동으로 되돌리는 줄이 맨 위다.** 지목해 놓고 되돌릴 길이 없으면 한 번 누른
            // 것이 그 판 내내 남는다.
            _hagglerOverride == null
                ? PawnMenuOption.Blocked($"자동 — 사교 최고 ({Name(auto)})", "이미 자동입니다")
                : ($"자동 — 사교 최고 ({Name(auto)})", (System.Action)(() => SetHaggler(null))),
        };

        foreach (var p in pool)
        {
            if (p == null || p.Stats == null) continue;

            string label = $"{p.UnitName}  (사교 {p.Stats.Social:0})";

            if (!Trading.CanHaggle(p))
            {
                opts.Add(PawnMenuOption.Blocked(label, "말을 할 수 있는 상태가 아니다"));
                continue;
            }

            // **얼마나 달라지는지 줄마다 적는다** — 이름과 스탯만으로는 그 차이가 값에서
            // 얼마인지 알 수 없어, 고르는 것이 짐작이 된다.
            int buy = Mathf.RoundToInt((Trading.BuyFactor(p) - 1f) * 100f);
            label  += $"  <size=85%><color=#9AB>구매 {buy:+0;-0;0}%</color></size>";

            if (p == _shop.Haggler) label = $"<color=#FFD94D>{label}</color>";

            var captured = p;
            opts.Add((label, (System.Action)(() => SetHaggler(captured))));
        }

        // 누른 그 버튼 옆에 띄운다. 화면 좌표라 `OpenAtScreen`.
        var rt  = hagglerButton != null ? (RectTransform)hagglerButton.transform
                                        : stockLabelText.rectTransform;
        var cnv = rt.GetComponentInParent<Canvas>();
        Camera cam = cnv != null && cnv.renderMode != RenderMode.ScreenSpaceOverlay
                   ? cnv.worldCamera : null;

        Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
        menu.OpenAtScreen("누가 흥정하나", opts, sp);
    }

    static string Name(UnitBrain p) => p != null ? p.UnitName : "없음";

    void SetHaggler(UnitBrain pawn)
    {
        _hagglerOverride = pawn;
        SyncHaggler(force: true);
    }

    // 지금 창을 연 상인(있으면). 월드맵 시장은 파는 폰이 없어 null이다.
    Merchant _merchant;
    bool     _openedForMerchant;   // 필드 상인으로 열었는가 — 파괴 판정과 월드 시장을 가른다

    // `merchant`는 **필드 상인으로 열었을 때만** 들어온다(월드맵 시장은 파는 폰이 없다).
    // 흥정하는 사람을 고르는 규칙이 그것으로 갈리므로(`HagglerPawn`) **여는 시점에 같이
    // 받아야 한다** — 창을 연 뒤에 걸면 첫 값이 다른 규칙으로 매겨진다.
    void Open(Shop shop, TradeAccount account, IReadOnlyList<UnitBrain> traders, string title,
              Merchant merchant = null)
    {
        if (shop == null || account == null) return;

        _shop          = shop;
        _account       = account;
        _merchant          = merchant;          // 월드맵 시장은 null
        _openedForMerchant = merchant != null;
        _traders       = (traders != null && traders.Count > 0) ? traders : null;
        ActiveShop     = shop;
        _selectedBuy   = null;
        _selectedStack = null;
        _selectedBuyUI = null;

        WindowRaiser.Open(panel);

        ApplyContainerLayout();

        // **여는 순간은 반드시 다시 매긴다**(`force`) — 지난번에 흥정한 폰이 그대로면
        // 폰 비교가 같다고 나와 제목도 값도 옛것으로 남는다. 목록 만들기도 여기서 한다
        // (`SyncHaggler`가 `RebuildBuyList`를 부른다 — 두 번 부르면 만들자마자 버린다).
        _title = title ?? shop.DisplayName;
        SyncHaggler(force: true);

        RefreshGold();

        // **거래할 폰이 없으면 인벤토리를 안 연다.** 팔 사람이 없는데 빈 배낭 창을 띄우면
        // 상점만 보려고 눌렀을 때 화면이 둘로 갈라진다. 판매는 폰을 고르고 나서 하는 일이다.
        bool hasPawn = TraderInventory() != null;

        _didForceOpenInventory = hasPawn && inventoryPanel != null && !inventoryPanel.IsPanelOpen;
        if (hasPawn)
        {
            inventoryPanel?.ForceOpen();
            inventoryPanel?.SetSellMode(shop);

            // **누구 배낭을 볼지 고르는 일은 배낭 창에 넘긴다** — 무게·짐내림·잠금이 이미
            // 거기 있으므로 폰을 고르는 자리도 같은 줄에 있어야 한다. 원정대든 필드에서
            // 여럿을 고른 것이든 같다(혼자면 `RefreshTraderBar`가 버튼만 숨긴다).
            if (_traders != null) inventoryPanel?.SetPawnCycle(_traders);
        }
    }

    public void Close()
    {
        ItemInfoUI.Hide();
        _shop      = null;
        _account   = null;
        _traders   = null;
        _merchant  = null;
        _lastHaggler = null;
        _openedForMerchant = false;
        ActiveShop = null;

        // **흥정 폰 목록을 열어 뒀으면 같이 닫는다** — 그 목록은 이 창에 대한 것이라, 남으면
        // 없어진 상점의 값을 고르는 창이 화면에 떠 있게 된다.
        //
        // **지목(`_hagglerOverride`)은 안 지운다.** 사교는 쓰는 사람만 자라는 스탯이라,
        // 키우려고 지목한 것이 상점 하나 닫을 때마다 풀리면 가게마다 다시 골라야 한다.
        // 그 폰이 못 하게 되면 `HagglerPawn`이 알아서 자동으로 돌린다.
        if (PawnContextMenuUI.Instance != null && PawnContextMenuUI.Instance.IsOpen)
            PawnContextMenuUI.Instance.Close();

        WindowRaiser.Close(panel);
        inventoryPanel?.ClearSellMode();
        inventoryPanel?.ClearPawnCycle();
        if (_didForceOpenInventory) inventoryPanel?.ForceClose();
    }

    // ── 보기 전환 (칸 ↔ 목록) ────────────────────────────────────────────────

    // 버튼은 **씬에 있다**(`StockLabel` 우측 = 재고 목록 바로 위). 런타임에 만들지 않는다 —
    // 모양·자리는 씬이 정본이고, 코드가 만들면 인스펙터에서 옮길 수가 없다.
    //
    // 제목줄에 두면 골드 표시(우측 130px)와 겹친다. 보기 전환은 아래 목록을 다루는 조작이라
    // 그 목록 바로 위가 자리로도 맞다.
    void SetupViewToggle()
    {
        if (viewToggleButton == null)
        {
            Debug.LogWarning("[MerchantUI] View Toggle Button이 연결되지 않아 " +
                             "칸↔목록 전환을 쓸 수 없습니다.");
            return;
        }

        // **두 보기 다 프리팹을 안 가린다** — 칸은 `InventoryGridUI`가 런타임에 그리고
        // 목록은 `entryPrefab`을 쓴다. 그래서 버튼은 늘 뜬다.
        viewToggleButton.gameObject.SetActive(true);

        _viewToggleLabel = viewToggleButton.GetComponentInChildren<TextMeshProUGUI>();
        viewToggleButton.onClick.AddListener(ToggleView);
        RefreshViewToggleLabel();
    }

    void ToggleView()
    {
        _view = NextView(_view);
        RefreshViewToggleLabel();
        ApplyContainerLayout();
        RebuildBuyList();
    }

    // 타일 프리팹이 없으면 카드는 **아예 없는 보기**라 칸에 머문다.
    StockView NextView(StockView v)
        => v == StockView.Cells && tileEntryPrefab != null ? StockView.Cards : StockView.Cells;

    static string ViewName(StockView v) => v == StockView.Cells ? "칸" : "카드";

    // 버튼에는 **지금 상태가 아니라 누르면 갈 곳**을 적는다 — 토글 버튼의 글자가
    // 현재 상태면 "격자라고 적혀 있는데 목록이네"가 되어 매번 눌러 봐야 안다.
    void RefreshViewToggleLabel()
    {
        if (_viewToggleLabel != null) _viewToggleLabel.text = ViewName(NextView(_view));
    }

    // 컨테이너의 레이아웃을 보기에 맞춰 **갈아 끼운다.**
    //
    // 켜고 끄기로 못 한다 — Unity는 한 오브젝트에 `LayoutGroup`을 둘 붙이지 못하게 막는다
    // (`DisallowMultipleComponent`). 그래서 지우고 새로 붙여야 하는데, 그냥 지우면 씬에서
    // **저작한 여백·간격이 사라진다.** 처음 한 번 값을 베껴 두고 돌아올 때 되살린다.
    //
    // `DestroyImmediate`인 이유: `Destroy`는 프레임 끝에 처리돼서, 바로 뒤의 `AddComponent`가
    // 아직 남아 있는 옛 컴포넌트에 막힌다.
    void ApplyContainerLayout()
    {
        if (stockContainer == null) return;
        CaptureListLayout();

        bool cells   = _view == StockView.Cells;
        var  current = stockContainer.GetComponent<LayoutGroup>();

        // **칸 보기는 레이아웃 그룹이 없어야 한다** — `InventoryGridUI`가 칸을 절대 좌표로
        // 놓기 때문에, 레이아웃 그룹이 남아 있으면 그것이 자식을 다시 줄 세워 격자가 무너진다.
        // 크기도 스스로 정해야 해서 `ContentSizeFitter`도 재운다.
        var fitter = stockContainer.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = !cells;

        if (cells)
        {
            if (current != null) DestroyImmediate(current);
            return;
        }

        if (current is GridLayoutGroup) return;   // 이미 맞다
        if (current != null) DestroyImmediate(current);

        var g = stockContainer.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize       = new Vector2(MerchantStockEntryUI.TileWidth,
                                       MerchantStockEntryUI.TileHeight);
        g.spacing        = new Vector2(4f, 4f);
        g.padding        = _listPadding ?? new RectOffset(8, 8, 4, 4);
        g.childAlignment = TextAnchor.UpperLeft;
        g.constraint     = GridLayoutGroup.Constraint.Flexible;
    }

    // 씬에 저작된 세로 레이아웃 값을 한 번만 베낀다. 이걸 안 해 두면 격자를 한 번 켠 뒤
    // 목록으로 돌아왔을 때 여백 0·간격 0짜리 기본값이 되어 화면이 달라진다.
    RectOffset _listPadding;
    float      _listSpacing;
    TextAnchor _listAlign;
    bool       _listControlW, _listControlH, _listExpandW, _listExpandH;
    bool       _listCaptured;

    void CaptureListLayout()
    {
        if (_listCaptured || stockContainer == null) return;

        var v = stockContainer.GetComponent<VerticalLayoutGroup>();
        if (v == null) return;

        // `RectOffset`은 참조형이라 컴포넌트를 지우면 같이 죽는다 — 값을 복사해 둔다.
        _listPadding  = new RectOffset(v.padding.left, v.padding.right, v.padding.top, v.padding.bottom);
        _listSpacing  = v.spacing;
        _listAlign    = v.childAlignment;
        _listControlW = v.childControlWidth;
        _listControlH = v.childControlHeight;
        _listExpandW  = v.childForceExpandWidth;
        _listExpandH  = v.childForceExpandHeight;
        _listCaptured = true;
    }

    // ── 구매 ─────────────────────────────────────────────────────────────────

    void RebuildBuyList()
    {
        if (stockContainer == null) return;

        // **보기가 바뀔 때만 통째로 비운다.** 같은 보기 안에서는 각자 갱신한다 —
        // 칸 보기의 격자는 제 자식을 스스로 관리하므로(`InventoryGridUI.Bind`), 여기서 매번
        // 비우면 물건 하나 살 때마다 칸 배경까지 다 다시 짓는다.
        if (!_builtOnce || _builtView != _view)
        {
            // **격자를 떼고 나서 비운다.** `ClearStock`은 `stockContainer`의 자식을 전부
            // 지우는데, 칸 보기를 쓰고 나면 거기 `InventoryGridUI`의 제 루트(`_Cells`·`_Items`)가
            // 들어 있다. 순서를 뒤집으면 카드→칸 전환에서 **좌판이 빈 채로 남는다**:
            // `Destroy`가 프레임 끝이라 `BuildCellGrid`/`EnsureItemsRoot`가 *"이미 있다"*고
            // 보고 죽을 오브젝트를 재사용하고, 새로 만든 아이콘이 그것과 함께 파괴된다
            // (다음 `Inventory.OnChanged`까지 안 돌아온다 — 뭘 사거나 팔아야 보였다).
            DetachCellsView();
            ClearStock();
            _builtView = _view;
            _builtOnce = true;
        }

        _selectedBuy   = null;
        _selectedStack = null;
        _selectedBuyUI = null;
        RefreshBuyButton();

        if (_shop == null) return;

        if (_view == StockView.Cells) { BuildCellsView(); return; }

        DetachCellsView();
        if (tileEntryPrefab == null) return;

        ClearStock();   // 카드는 매번 다시 찍는다(재고·값이 바뀐다)

        foreach (var entry in _shop.Stock)
        {
            var ui         = Instantiate(tileEntryPrefab, stockContainer);
            var capturedUI = ui;
            ui.Setup(entry);
            ui.OnSelected += e =>
            {
                if (_selectedBuyUI != null) _selectedBuyUI.SetSelected(false);
                _selectedBuy   = e;
                _selectedBuyUI = capturedUI;
                capturedUI.SetSelected(true);
                RefreshBuyButton();
            };
            ui.OnDoubleClicked += _ => ExecuteBuy();
            _buyEntryUIs.Add(ui);
        }
    }

    void ClearStock()
    {
        for (int i = stockContainer.childCount - 1; i >= 0; i--)
        {
            var c = stockContainer.GetChild(i);
            c.SetParent(null);                 // 즉시 떼어 낸다 — `Destroy`는 프레임 끝이라
            Destroy(c.gameObject);             // 새로 만든 것과 한 프레임 겹쳐 보인다
        }
        _buyEntryUIs.Clear();
    }

    // ── 칸 보기 (배낭과 같은 격자) ────────────────────────────────────────────
    //
    // **좌판(`Shop.Stall`)을 그대로 그린다.** 흉내 내지 않는 이유는 그래야 **같은 것이 같게
    // 보이기** 때문이다 — 2×4 갑옷이 칸을 얼마나 먹는지가 배낭에서와 똑같이 나온다.
    //
    // 한때 이 창이 재고 목록을 보고 **제 통을 따로 만들어** 그렸다. 그때는 자리가 화면의
    // 일이었지만, **자리가 곧 판매 한도가 되면서 규칙이 됐다** — 규칙을 창이 들면 창을 안 열고
    // 파는 길이 생겼을 때 조용히 새므로, 통은 `Shop`이 들고 여기는 **보여 주기만** 한다.
    InventoryGridUI _cellsGrid;

    void BuildCellsView()
    {
        EnsureCellsGrid();
        if (_cellsGrid == null || _shop == null) return;

        var stall = _shop.Stall;
        var rect  = (RectTransform)stockContainer;

        rect.sizeDelta = new Vector2(rect.sizeDelta.x, stall.Height * _cellsGrid.CellSize);
        _cellsGrid.Bind(stall);
    }

    void EnsureCellsGrid()
    {
        if (_cellsGrid == null)
        {
            _cellsGrid = stockContainer.GetComponent<InventoryGridUI>()
                      ?? stockContainer.gameObject.AddComponent<InventoryGridUI>();

            _cellsGrid.IsShop    = true;   // 격자를 건너가면 이동이 아니라 거래다
            _cellsGrid.IconPrice = inst => EntryFor(inst)?.Price ?? 0;

            _cellsGrid.OnIconClicked += inst =>
            {
                SelectStack(inst);
                _cellsGrid.HighlightInstance(inst);
                RefreshBuyButton();
            };
        }

        // **칸 크기(픽셀)는 배낭에서 베낀다 — 열 때마다 다시 맞춘다.**
        //
        // 만들 때 한 번만 하면 그 순간 배낭 격자가 아직 안 잡혀 있을 때 48로 굳는다. 값이
        // 같으면 `CopyCellSize`가 곧바로 돌아오므로 매번 물어도 값이 안 든다.
        if (inventoryPanel != null && inventoryPanel.Grid != null)
            _cellsGrid.CopyCellSize(inventoryPanel.Grid.CellSize);

        _cellsGrid.enabled = true;
    }

    // **좌판을 다시 채워 넣는다**(`Inventory.Compact`). 격자는 쓰다 보면 반드시 조각나는데,
    // 여기서는 그게 **판매 한도로 직결된다** — 칸 수로는 넉넉한데 2×4 갑옷이 들어갈 연속된
    // 자리가 없어 거절당하는 일이 생긴다. 좌판은 내가 끌어 옮길 수 없으니(진열은 못 끈다)
    // 정리해 줄 길이 없으면 그 상인은 영영 그 상태로 남는다.
    //
    // 다시 그리는 것은 `Inventory.OnChanged`를 격자가 듣고 있어 저절로 된다.
    void TidyStall()
    {
        if (_shop == null) return;

        _shop.Stall.Compact();
        RefreshHagglerLine();   // 쓰는 칸 수가 그대로여도 조각남이 풀리므로 다시 적는다
    }

    // 칸 보기를 접는다 — 격자를 지우지 않고 **재운다**(다시 켤 때 통째로 다시 짓지 않게).
    void DetachCellsView()
    {
        if (_cellsGrid == null) return;
        _cellsGrid.Bind(null);
        _cellsGrid.enabled = false;
    }

    // **창 크기는 씬이 정한다.** 한때 격자에 맞춰 코드가 창을 늘렸는데, 모양·자리의 정본은
    // 씬이라는 규약을 코드가 덮어쓰는 꼴이었다 — 인스펙터에서 아무리 맞춰도 실행하면 딴 크기가
    // 된다. 진열대(8×6 × 64px = 512)가 들어가도록 `Merchant_Panel`을 580×620으로 저작했다.
    // 진열대를 더 키우면(`MerchantData.StockGrid`) 창도 같이 키울 것.

    // 이 뭉치가 어느 재고 줄인가. **표를 안 만들고 그때그때 찾는다** — 같은 물건·같은 등급이
    // 두 줄이면 통에서 한 뭉치로 합쳐지는데, 표로 굳혀 두면 그중 하나가 미아가 된다.
    // 재고가 남은 첫 줄을 고르므로 앞줄이 다 팔리면 저절로 다음 줄로 넘어간다.
    MerchantStockEntry EntryFor(ItemInstance inst)
    {
        if (_shop == null || inst == null) return null;

        foreach (var e in _shop.Stock)
            if (e.Item == inst.Data && e.Quality == inst.Quality && e.Stock > 0) return e;

        return null;
    }

    // **좌판의 이 뭉치를 산다** — 드래그로 끌어냈거나 우클릭했을 때.
    //
    // 사는 개수는 밖에서 정하지만(`DragAmount`) **살 수 있는 만큼으로 잘린다**(재고·지갑).
    // `into`가 있으면 그 통으로, 없으면 거래 폰의 배낭으로.
    internal bool BuyInstance(ItemInstance inst, int count, Inventory into,
                              Vector2Int? at = null, bool rotated = false)
    {
        if (_shop == null || _account == null || inst == null) return false;

        var entry = EntryFor(inst);
        if (entry == null) return false;

        // 고른 것으로 잡아 둔다 — 아래 구매 줄이 무엇을 샀는지 보여 주고,
        // `MaxBuyable`·`ExecuteBuy`가 같은 줄·같은 뭉치를 본다.
        SelectStack(inst);

        // **누른 그 뭉치를 넘지 않는다.** 값표 한 줄(`entry`)이 좌판의 **여러 뭉치**를 덮을 수
        // 있어서다 — 3개와 4개로 갈라 둔 것을 3개 쪽에서 우클릭하면 줄 재고(7)만큼 팔려 나갔다.
        // 산다는 것은 늘 **이것**을 산다는 뜻이다(`MaxBuyable`도 같은 규칙을 쓴다).
        int want = Mathf.Clamp(count, 1, inst.Quantity);

        int max = MaxBuyable();                       // 지갑·줄 재고·고른 뭉치
        if (max > 0) want = Mathf.Min(want, max);

        var inv = into != null ? into : TraderInventory();

        int bought = _shop.TryBuy(entry, _account, inv, want, at, rotated, inst);
        if (bought == 0)
        {
            EventNotificationUI.Notify("구매 실패",
                $"{entry.Item?.ItemName} — 돈이 모자라거나 받을 자리가 없습니다.");
            return false;
        }

        AfterBuy();
        return true;
    }

    void OnBuyClicked() => ExecuteBuy();

    void ExecuteBuy()
    {
        if (_shop == null || _selectedBuy == null) return;

        int want = ReadCount(buyQuantity, MaxBuyable());

        // 거래 폰이 있으면 그 배낭으로. 없거나 꽉 차면 `TryBuy`가 계정으로 돌린다
        // (거점이면 창고, 원정대면 다른 멤버 배낭).
        //
        // **반복은 `Shop`이 든다** — 여기서 돌리면 사교 성장이 개수만큼 붙는다(파는 쪽과
        // 규약이 갈린다).
        int bought = _shop.TryBuy(_selectedBuy, _account, TraderInventory(), want,
                                  fromStack: _selectedStack);

        if (bought == 0)
        {
            // 아무것도 못 샀는데 화면이 그대로면 "왜 안 사지지"가 된다. 돈은 위에서
            // 이미 걸렀으니 여기까지 온 실패는 **넣을 자리가 없다**는 뜻이다.
            EventNotificationUI.Notify("구매 실패",
                $"{_selectedBuy.Item?.ItemName}을(를) 받을 자리가 없습니다. 배낭이나 창고를 비워 주세요.");
            return;
        }

        AfterBuy();
    }

    // 산 뒤의 뒷정리 — **사는 길이 셋이라**(구매 버튼·더블클릭·드래그/우클릭) 한 곳에 모은다.
    void AfterBuy()
    {
        RefreshGold();
        _selectedBuyUI?.RefreshStock();

        // **칸 보기는 좌판이 스스로 다시 그린다** — `Inventory.OnChanged`를 격자가 듣고 있어서
        // 개수가 줄고 다 팔린 물건이 칸에서 사라지는 것이 저절로 나온다(배낭과 같은 모습).
        // 고른 것은 **재고가 남아 있을 때만** 붙들어 둔다: 다 팔았으면 칸 자체가 없으니
        // 고른 상태로 두면 없는 것을 가리킨다.
        if (_view == StockView.Cells && _shop != null && _cellsGrid != null)
        {
            if (_selectedBuy != null && _selectedBuy.Stock <= 0) _selectedBuy = null;
            if (_selectedStack != null && _selectedStack.Quantity <= 0) _selectedStack = null;

            if (_selectedBuy != null)
                foreach (var inst in _shop.Stall.Items)
                    if (EntryFor(inst) == _selectedBuy) { _cellsGrid.HighlightInstance(inst); break; }
        }

        RefreshBuyButton();
    }

    // 고른 것을 기억한다 — 줄(값표)과 뭉치(좌판)를 같이. 둘 다 있어야 "무엇을 사는가"에 답한다.
    void SelectStack(ItemInstance inst)
    {
        _selectedBuy   = EntryFor(inst);
        _selectedStack = inst;
    }

    // 살 수 있는 최대 = **재고와 지갑 중 적은 쪽.** 수량 칸에 아무 수나 적어도 이걸 못 넘는다.
    //
    // **뭉치를 골랐으면 그 뭉치를 못 넘는다.** 값표 한 줄이 좌판의 여러 뭉치를 덮을 수 있어서,
    // 줄 재고로만 재면 1개짜리를 눌러 놓고 [최대]를 누를 때 옆의 5개까지 딸려 나간다 —
    // **누른 것을 산다**가 우클릭·드래그·버튼에서 같아야 한다.
    int MaxBuyable()
    {
        if (_selectedBuy == null || _account == null) return 0;

        int afford = _selectedBuy.Price > 0 ? _account.GoldAmount / _selectedBuy.Price
                                            : _selectedBuy.Stock;

        int stock = _selectedStack != null
                  ? Mathf.Min(_selectedBuy.Stock, _selectedStack.Quantity)
                  : _selectedBuy.Stock;

        return Mathf.Max(0, Mathf.Min(stock, afford));
    }

    void FillMaxBuyQuantity()
    {
        if (buyQuantity != null) buyQuantity.text = Mathf.Max(1, MaxBuyable()).ToString();
    }

    void RefreshBuyButton()
    {
        if (buyButton == null) return;
        if (_selectedBuy == null || _account == null || _shop?.Data == null)
        { buyButton.interactable = false; return; }
        buyButton.interactable = _selectedBuy.Stock > 0 && _account.GoldAmount >= _selectedBuy.Price;
    }

    // `source`를 주면 **그 통에서** 뺀다(끌어온 격자). 안 주면 거래 폰의 배낭.
    internal bool ExecuteSell(ItemInstance inst, int count = 1, Inventory source = null)
    {
        if (_shop == null || inst == null) return false;

        // **잠근 것은 못 판다.** 판매로 들어오는 길이 셋(버튼·더블클릭·드래그)이라 각 입구를
        // 막으면 언젠가 하나를 빠뜨린다 — 실제로 돈이 오가는 이 한 곳에서 막는다.
        if (inst.Locked)
        {
            EventNotificationUI.Notify(
                "잠긴 물건",
                $"{inst.Data?.ItemName}은(는) 잠겨 있어 팔 수 없습니다. 배낭에서 [잠금]으로 풀어 주세요.");
            return false;
        }

        var inv = source != null ? source : TraderInventory();
        if (inv == null) return false;
        if (_shop.TrySell(inst, inv, _account, count) <= 0)
        {
            // **하나도 못 팔았으면 이유를 말한다.** 드래그 판매·더블클릭 판매는 배낭 창의
            // 판매 줄을 안 거치므로(거기 적힌 사유를 못 본다), 물건이 제자리로 돌아가는 것만
            // 보이면 "왜 안 되지"가 된다. 구매 실패가 알림을 띄우는 것과 같은 자리.
            string why = !_shop.HasRoomFor(inst.Data, 1, inst.Quality)
                       ? "상인의 좌판에 자리가 없습니다. 다른 상인을 찾아보세요."
                       : "상인이 살 돈이 모자랍니다. 내일 다시 오거나 그에게서 무언가 사 보세요.";

            EventNotificationUI.Notify("판매 실패", $"{inst.Data?.ItemName} — {why}");
            return false;
        }
        RefreshGold();
        RefreshBuyButton();                  // 판 돈으로 살 수 있게 된 것이 있다
        inventoryPanel?.RefreshSellState();  // 상인 주머니·자리가 줄었다 — 판매 줄이 그걸 알아야 한다

        // **판 물건이 좌판에 올랐으니 진열도 다시 그린다.** 칸 보기는 좌판(`Inventory`)을
        // 직접 보고 있어 저절로 되지만, 카드 보기는 값표에서 찍으므로 여기서 말해 줘야 한다.
        if (_view == StockView.Cards) RebuildBuyList();
        return true;
    }

    // 사고파는 **배낭**. 원정대 거래면 배낭 창이 지금 보여주는 멤버, 필드면 지금 고른 폰.
    //
    // 원정대 쪽은 **배낭 창에 묻는다** — 넘겨보기를 거기가 갖고 있으므로, 여기서 따로 세면
    // 보이는 배낭과 값이 오가는 배낭이 어긋날 수 있다.
    // **배낭 주인과 흥정하는 사람은 다를 수 있다.**
    //
    // 물건이 오가는 것은 **고른 폰의 배낭**이고(순환 버튼으로 갈아탄다), 값을 다투는 것은
    // **그 자리에서 제일 말 잘하는 사람**이다 — "값은 우리 협상가가 부르고 물건은 내가
    // 지고 간다". 하나로 묶으면 둘 중 하나가 어긋난다: 사교 최고를 자동으로 잡으면 내가
    // 보려던 배낭이 아닌 것이 열리고, 고른 폰으로 흥정하면 몸값·고용(`Trading.BestOf`)과
    // 규칙이 달라진다.
    //
    // 창에 이름이 둘 뜨는 값은 치른다. 대신 흥정하는 쪽 이름을 제목 밑에 적고
    // (`Trading.Note`), 왜 그런지는 사교 스탯 툴팁(`StatInfo`)이 답한다.
    // 지금 고른 폰들. 필드 상인은 이걸로 거래한다 — 원정대가 `_traders`를 받는 자리와 같다.
    List<UnitBrain> SelectedPawns()
    {
        if (_selection == null) _selection = Object.FindFirstObjectByType<SelectionManager>();
        if (_selection == null) return null;

        var list = new List<UnitBrain>();
        foreach (var m in _selection.Selected)
        {
            var brain = m != null ? m.GetComponent<UnitBrain>() : null;
            if (brain != null) list.Add(brain);
        }
        return list;
    }

    UnitBrain TraderPawn()
    {
        if (_traders != null)
        {
            var pawn = inventoryPanel != null ? inventoryPanel.BoundPawn : null;
            return pawn != null ? pawn : _traders[0];   // 아직 배낭 창이 안 열렸을 때
        }

        // **못 박은 명단이 없으면 지금 고른 폰을 본다.** 여기가 비면 `Shop.TryBuy`의 `buyer`가
        // null이 되어 **산 물건이 배낭이 아니라 창고로 간다** — 방금 산 것을 다시 꺼내러 가야
        // 하는 꼴이다. 그 길은 파는 폰이 아예 없는 월드맵 마을 몫이지 필드 상인 몫이 아니다.
        var live = SelectedPawns();
        return (live != null && live.Count > 0) ? live[0] : null;
    }

    // **창을 열 때 아무도 안 골랐어도 그 뒤에 고르면 받아들인다.**
    //
    // 명단을 창 열 때 못 박은 것은 "거래 중에 뒤의 땅을 잘못 눌러 배낭이 갈리는 것"을 막으려던
    // 것인데, **처음부터 비어 있던 경우까지 얼려 버렸다** — 상인을 먼저 눌러 구경하다 폰을
    // 고르는 순서가 막힌다. 비어 있을 때만 다시 보므로 원래 막으려던 것은 그대로 막힌다.
    void AdoptSelection()
    {
        var live = SelectedPawns();
        if (live == null || live.Count == 0) return;

        _traders = live;

        // **내가 연 것은 내가 닫는다** — 안 적어 두면 `Close`가 배낭 창을 열어 둔 채 끝난다
        // (`Open`의 같은 자리와 짝).
        _didForceOpenInventory = inventoryPanel != null && !inventoryPanel.IsPanelOpen;
        inventoryPanel?.ForceOpen();
        inventoryPanel?.SetSellMode(_shop);
        inventoryPanel?.SetPawnCycle(_traders);
        SyncHaggler(force: true);   // 흥정 줄도 그제야 설 수 있다
        RefreshGold();
    }

    // 값을 다투는 사람 — **이 맵에 있는 우리 편 중 사교 최고**(`Trading.BestOnField`).
    //
    // **데리고 온 사람들 중에서 고르지 않는다.** 그러면 흥정을 잘 하려고 매번 "그 폰을
    // 골랐던가"를 신경 써야 하는데, 그건 선택이 아니라 잡일이다. 협상은 우리 쪽에서 제일
    // 말 잘하는 사람이 맡고, 그 사람이 누구인지는 재고 줄이 적어 준다.
    //
    // 원정대(월드맵 시장)는 `_traders`가 곧 "그 자리에 있는 사람들"이라 그쪽에서 고른다 —
    // 거점에 남은 폰은 지도 위에 없다.
    UnitBrain HagglerPawn()
    {
        // **지목한 사람이 있으면 그 사람.** 다만 **조용히 자동으로 돌아가는 조건**을 둔다 —
        // 쓰러지거나 갇히거나 죽거나 그 자리에 없으면 그 지목은 지킬 수 없다. 그때 값을 0으로
        // 떨어뜨리는 대신 자동으로 넘기고, 바뀐 것은 재고 줄이 적는다.
        //
        // **"그 자리에 있나"는 목록(`InPool`)에게 묻는다.** 처음엔 `isActiveAndEnabled`로 물었는데
        // 그것이 **월드맵에서 지목을 통째로 막았다** — 원정 나간 폰은 GameObject가 꺼져 있어서
        // (그게 원정의 표현이다) 매 프레임 자동으로 되돌아갔다. 필드의 "원정을 떠나면 손을 뗀다"와
        // 지도의 "원정대가 곧 그 자리에 있는 사람들"은 **같은 물음의 반대편**이라, 고를 때 쓰는
        // 목록에 물으면 양쪽이 저절로 맞는다.
        if (_hagglerOverride != null && InPool(_hagglerOverride)
                                     && Trading.CanHaggle(_hagglerOverride))
            return _hagglerOverride;

        // **월드맵 원정대는 그 명단이 곧 "그 자리에 있는 사람들"이다** — 거점에 남은 폰은
        // 지도 위에 없다.
        if (!_openedForMerchant && _traders != null) return Trading.BestOf(_traders);

        // **필드는 맵 전체.** `_traders`(창 열 때 고른 폰들)로 고르면 **같은 상인의 값이
        // 문에 따라 갈린다** — 좌클릭으로 열면 고른 폰 중 최고, 대화창 `[물건을 보여 주시오]`로
        // 열면(`traders: null`) 맵 최고가 됐다. 배낭 주인은 `_traders`가 정하고 값은 여기가
        // 정한다(둘이 다른 물음이라는 것이 이 창의 규약이다).
        return Trading.BestOnField();
    }

    Inventory TraderInventory()
    {
        var pawn = TraderPawn();
        return pawn != null ? pawn.Inventory : null;
    }

    // 지갑의 주인을 같이 적는다 — 창고 돈과 원정대 돈은 액수만 봐선 구분이 안 된다.
    void RefreshGold()
    {
        if (goldText == null || _account == null) return;
        goldText.text = $"{_account.Label} 골드: {_account.GoldAmount} G";

        RefreshTitle();
        RefreshHagglerLine();   // 좌판 사용량이 방금 바뀌었을 수 있다(사고팔면 늘거나 준다)
    }

    // **제목은 여기 한 곳에서 짓는다.** 흥정하는 사람이 바뀔 때(`SyncHaggler`)와 돈이 오갈
    // 때(`RefreshGold`) 둘 다 다시 그려야 하는데, 각자 적으면 한쪽이 주머니를 빠뜨려
    // **거래 한 번에 액수가 사라진다.**
    //
    // **상인 주머니를 제목에 붙이는 이유**: 얼마까지 사 주는지 모르면 그건 규칙이 아니라
    // 사고다 — 한창 팔다가 갑자기 안 팔리는데 이유가 화면에 없다. 골드 줄(`goldText`)은
    // 130px이라 둘이 못 들어가고 재고 줄은 흥정 이야기로 이미 길다. 제목은 246px이 남는다.
    void RefreshTitle()
    {
        if (merchantNameText == null) return;

        merchantNameText.text = _shop != null && !_shop.HasInfiniteGold
            ? $"{_title}  <size=80%><color=#FFD94D>{_shop.Gold:N0} G</color></size>"
            : _title;
    }
}
