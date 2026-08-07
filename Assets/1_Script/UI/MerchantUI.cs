using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 상점 창. **어느 상점을 여느냐와 누가 값을 치르느냐는 밖에서 정해 준다** —
// 필드맵 상인은 `Merchant.OnSelected`로, 월드맵 마을은 `WorldMapView`가 `OpenFor`로 연다.
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

    [Tooltip("목록 보기 항목 (가로로 긴 행)")]
    [SerializeField] MerchantStockEntryUI entryPrefab;

    [Tooltip("격자 보기 항목 (정사각 타일). 비우면 격자 보기가 꺼진다 — " +
             "DungeonColony → Create Merchant Tile Entry 로 만든 프리팹을 연결할 것.")]
    [SerializeField] MerchantStockEntryUI tileEntryPrefab;

    [Tooltip("목록↔격자 전환 버튼 (씬의 StockLabel 우측). 글자는 런타임에 갈아 끼운다.")]
    [SerializeField] Button viewToggleButton;
    [SerializeField] Button               buyButton;
    [Tooltip("한 번에 살 개수. 비우면 1개씩")]
    [SerializeField] TMP_InputField       buyQuantity;
    [Tooltip("살 수 있는 최대치를 수량 칸에 넣는 버튼")]
    [SerializeField] Button               buyMaxButton;

    [Header("연동")]
    [SerializeField] InventoryUI inventoryPanel;


    // ── 보기 상태 ────────────────────────────────────────────────────────────
    // **기본은 격자.** 재고는 훑어보는 것이지 읽는 것이 아니라, 아이콘이 늘어선 편이 빠르다
    // (배낭·창고도 격자라 눈이 같은 방식으로 움직인다). 이름·가격을 나란히 비교하고 싶을 때만
    // 목록으로 간다.
    //
    // **static이다.** 상점을 닫았다 열 때마다 기본으로 돌아가면, 보기를 바꾼 사람은
    // 상인을 만날 때마다 다시 눌러야 한다. 고른 보기는 그 사람의 취향이지 상점의 성질이 아니다.
    // 타일 프리팹이 없으면 `CanUseGrid`가 목록으로 되돌리므로 배선 전에도 안전하다.
    static bool _gridView = true;

    TextMeshProUGUI _viewToggleLabel;

    bool                                _didForceOpenInventory;
    Shop                                _shop;
    TradeAccount                        _account;
    IReadOnlyList<UnitBrain>            _traders;      // 원정대 거래일 때만. null이면 고른 폰
    int                                 _traderIndex;
    MerchantStockEntry                  _selectedBuy;
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
        Merchant.OnSelected              += OpenForMerchant;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
        GameModeManager.OnModeChanged    += OnGameModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    // 필드에서 상점을 연 채로 M을 누르면 창이 지도 위에 남는다 — 거래 상대가 화면에서
    // 사라졌는데 창만 살아 있으면 창고 돈으로 지도에서 물건을 사게 된다.
    void OnGameModeChanged(GameMode mode) => Close();

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
        GameModeManager.OnModeChanged    -= OnGameModeChanged;
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
            Close();
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

        Open(merchant.Shop, merchant.Account, null, title);
        _merchant = merchant;            // `Open`이 비우므로 그 뒤에 건다
        _openedForMerchant = true;
    }

    // 지금 창을 연 상인(있으면). 월드맵 시장은 파는 폰이 없어 null이다.
    Merchant _merchant;
    bool     _openedForMerchant;   // 필드 상인으로 열었는가 — 파괴 판정과 월드 시장을 가른다

    void Open(Shop shop, TradeAccount account, IReadOnlyList<UnitBrain> traders, string title)
    {
        if (shop == null || account == null) return;

        _shop          = shop;
        _account       = account;
        _merchant          = null;   // 월드맵 시장 등 파는 폰이 없는 진입점의 기본값
        _openedForMerchant = false;
        _traders       = (traders != null && traders.Count > 0) ? traders : null;
        ActiveShop     = shop;
        _selectedBuy   = null;
        _selectedBuyUI = null;

        if (panel)            panel.SetActive(true);
        if (merchantNameText) merchantNameText.text = title ?? shop.DisplayName;

        ApplyContainerLayout();
        RebuildBuyList();
        RefreshGold();

        // **거래할 폰이 없으면 인벤토리를 안 연다.** 팔 사람이 없는데 빈 배낭 창을 띄우면
        // 상점만 보려고 눌렀을 때 화면이 둘로 갈라진다. 판매는 폰을 고르고 나서 하는 일이다.
        bool hasPawn = TraderInventory() != null;

        _didForceOpenInventory = hasPawn && inventoryPanel != null && !inventoryPanel.IsPanelOpen;
        if (hasPawn)
        {
            inventoryPanel?.ForceOpen();
            inventoryPanel?.SetSellMode(shop);

            // 원정대면 **누구 배낭을 볼지 고르는 일**을 배낭 창에 넘긴다 — 무게·짐내림·잠금이
            // 이미 거기 있으므로 폰을 고르는 자리도 같은 줄에 있어야 한다.
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
        _openedForMerchant = false;
        ActiveShop = null;
        if (panel) panel.SetActive(false);
        inventoryPanel?.ClearSellMode();
        inventoryPanel?.ClearPawnCycle();
        if (_didForceOpenInventory) inventoryPanel?.ForceClose();
    }

    // ── 보기 전환 (목록 ↔ 격자) ──────────────────────────────────────────────

    // 타일 프리팹이 없으면 격자 보기는 아예 없는 것으로 친다 — 버튼도 안 만든다.
    // 누를 수는 있는데 아무 일도 안 일어나는 버튼이 제일 나쁘다.
    bool CanUseGrid => tileEntryPrefab != null;

    // 버튼은 **씬에 있다**(`StockLabel` 우측 = 재고 목록 바로 위). 런타임에 만들지 않는다 —
    // 모양·자리는 씬이 정본이고, 코드가 만들면 인스펙터에서 옮길 수가 없다.
    //
    // 제목줄에 두면 골드 표시(우측 130px)와 겹친다. 보기 전환은 아래 목록을 다루는 조작이라
    // 그 목록 바로 위가 자리로도 맞다.
    void SetupViewToggle()
    {
        if (viewToggleButton == null)
        {
            if (CanUseGrid)
                Debug.LogWarning("[MerchantUI] View Toggle Button이 연결되지 않아 " +
                                 "목록↔격자 전환을 쓸 수 없습니다.");
            return;
        }

        // 타일 프리팹이 없으면 격자 보기가 아예 없는 것이니 버튼도 숨긴다 —
        // 누를 수는 있는데 아무 일도 안 일어나는 버튼이 제일 나쁘다.
        viewToggleButton.gameObject.SetActive(CanUseGrid);
        if (!CanUseGrid) return;

        _viewToggleLabel = viewToggleButton.GetComponentInChildren<TextMeshProUGUI>();
        viewToggleButton.onClick.AddListener(ToggleView);
        RefreshViewToggleLabel();
    }

    void ToggleView()
    {
        _gridView = !_gridView;
        RefreshViewToggleLabel();
        ApplyContainerLayout();
        RebuildBuyList();
    }

    // 버튼에는 **지금 상태가 아니라 누르면 갈 곳**을 적는다 — 토글 버튼의 글자가
    // 현재 상태면 "격자라고 적혀 있는데 목록이네"가 되어 매번 눌러 봐야 안다.
    void RefreshViewToggleLabel()
    {
        if (_viewToggleLabel != null) _viewToggleLabel.text = _gridView ? "목록" : "격자";
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

        bool grid    = _gridView && CanUseGrid;
        var  current = stockContainer.GetComponent<LayoutGroup>();

        if (grid  && current is GridLayoutGroup)     return;   // 이미 맞다
        if (!grid && current is VerticalLayoutGroup) return;

        if (current != null) DestroyImmediate(current);

        if (grid)
        {
            var g = stockContainer.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize       = new Vector2(MerchantStockEntryUI.TileWidth,
                                           MerchantStockEntryUI.TileHeight);
            g.spacing        = new Vector2(4f, 4f);
            g.padding        = _listPadding ?? new RectOffset(8, 8, 4, 4);
            g.childAlignment = TextAnchor.UpperLeft;
            g.constraint     = GridLayoutGroup.Constraint.Flexible;
        }
        else
        {
            var v = stockContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            if (_listCaptured)
            {
                v.padding                = _listPadding;
                v.spacing                = _listSpacing;
                v.childAlignment         = _listAlign;
                v.childControlWidth      = _listControlW;
                v.childControlHeight     = _listControlH;
                v.childForceExpandWidth  = _listExpandW;
                v.childForceExpandHeight = _listExpandH;
            }
        }
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
        foreach (Transform t in stockContainer) Destroy(t.gameObject);
        _buyEntryUIs.Clear();
        _selectedBuy   = null;
        _selectedBuyUI = null;
        RefreshBuyButton();

        // 보기에 따라 **어느 템플릿을 복제할지**만 갈린다. 뒤 동작(선택·더블클릭·툴팁)은
        // 같은 `MerchantStockEntryUI` 컴포넌트라 한 벌로 끝난다.
        var template = (_gridView && CanUseGrid) ? tileEntryPrefab : entryPrefab;
        if (_shop == null || template == null) return;

        foreach (var entry in _shop.Stock)
        {
            var ui         = Instantiate(template, stockContainer);
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

    void OnBuyClicked() => ExecuteBuy();

    void ExecuteBuy()
    {
        if (_shop == null || _selectedBuy == null) return;

        int want = ReadCount(buyQuantity, MaxBuyable());

        int bought = 0;
        // 거래 폰이 있으면 그 배낭으로. 없거나 꽉 차면 `TryBuy`가 계정으로 돌린다
        // (거점이면 창고, 원정대면 다른 멤버 배낭).
        for (int i = 0; i < want; i++)
        {
            if (!_shop.TryBuy(_selectedBuy, _account, TraderInventory())) break;
            bought++;
        }

        if (bought == 0)
        {
            // 아무것도 못 샀는데 화면이 그대로면 "왜 안 사지지"가 된다. 돈은 위에서
            // 이미 걸렀으니 여기까지 온 실패는 **넣을 자리가 없다**는 뜻이다.
            EventNotificationUI.Notify("구매 실패",
                $"{_selectedBuy.Item?.ItemName}을(를) 받을 자리가 없습니다. 배낭이나 창고를 비워 주세요.");
            return;
        }

        RefreshGold();
        _selectedBuyUI?.RefreshStock();
        RefreshBuyButton();
    }

    // 살 수 있는 최대 = **재고와 지갑 중 적은 쪽.** 수량 칸에 아무 수나 적어도 이걸 못 넘는다.
    int MaxBuyable()
    {
        if (_selectedBuy == null || _account == null) return 0;
        int afford = _selectedBuy.Price > 0 ? _account.GoldAmount / _selectedBuy.Price
                                            : _selectedBuy.Stock;
        return Mathf.Max(0, Mathf.Min(_selectedBuy.Stock, afford));
    }

    void FillMaxBuyQuantity()
    {
        if (buyQuantity != null) buyQuantity.text = Mathf.Max(1, MaxBuyable()).ToString();
    }

    void RefreshBuyButton()
    {
        if (buyButton == null) return;
        if (_selectedBuy == null || _account == null || _shop?.Data?.GoldItem == null)
        { buyButton.interactable = false; return; }
        buyButton.interactable = _selectedBuy.Stock > 0 && _account.GoldAmount >= _selectedBuy.Price;
    }

    internal bool ExecuteSell(ItemInstance inst, int count = 1)
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

        var inv = TraderInventory();
        if (inv == null) return false;
        if (_shop.TrySell(inst, inv, _account, count) <= 0) return false;
        RefreshGold();
        RefreshBuyButton();     // 판 돈으로 살 수 있게 된 것이 있다
        return true;
    }

    // 사고파는 **배낭**. 원정대 거래면 배낭 창이 지금 보여주는 멤버, 필드면 지금 고른 폰.
    //
    // 원정대 쪽은 **배낭 창에 묻는다** — 넘겨보기를 거기가 갖고 있으므로, 여기서 따로 세면
    // 보이는 배낭과 값이 오가는 배낭이 어긋날 수 있다.
    Inventory TraderInventory()
    {
        if (_traders != null)
        {
            var pawn = inventoryPanel != null ? inventoryPanel.BoundPawn : null;
            if (pawn == null) pawn = _traders[0];    // 아직 배낭 창이 안 열렸을 때
            return pawn != null ? pawn.Inventory : null;
        }

        if (_selection == null) _selection = Object.FindFirstObjectByType<SelectionManager>();
        if (_selection == null || _selection.Selected.Count == 0) return null;
        return _selection.Selected[0].GetComponent<Inventory>();
    }

    // 지갑의 주인을 같이 적는다 — 창고 돈과 원정대 돈은 액수만 봐선 구분이 안 된다.
    void RefreshGold()
    {
        if (goldText == null || _account == null) return;
        goldText.text = $"{_account.Label} 골드: {_account.GoldAmount} G";
    }
}
