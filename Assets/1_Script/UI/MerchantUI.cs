using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class MerchantUI : MonoBehaviour
{
    public static Merchant      ActiveMerchant { get; private set; }
    public static RectTransform SellZoneRect   { get; private set; }

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
    [SerializeField] MerchantStockEntryUI entryPrefab;
    [SerializeField] Button               buyButton;
    [Tooltip("한 번에 살 개수. 비우면 1개씩")]
    [SerializeField] TMP_InputField       buyQuantity;
    [Tooltip("살 수 있는 최대치를 수량 칸에 넣는 버튼")]
    [SerializeField] Button               buyMaxButton;

    [Header("연동")]
    [SerializeField] PawnInventoryPanel inventoryPanel;

    bool                                _didForceOpenInventory;
    Merchant                            _merchant;
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
        Merchant.OnSelected              += Open;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Start()
    {
        _selection = Object.FindFirstObjectByType<SelectionManager>();
        if (inventoryPanel == null) inventoryPanel = Object.FindFirstObjectByType<PawnInventoryPanel>();
    }

    void OnDestroy()
    {
        _instance = null;
        Merchant.OnSelected              -= Open;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void Update()
    {
        // 닫기 버튼은 공용 `CloseButton`이 **패널을 그냥 끄기만** 한다(UI 규약).
        // 그러면 여기서 해야 할 뒷정리 — 판매 모드 해제·인벤토리 닫기·`ActiveMerchant` 비우기 —
        // 가 안 돈다. 그대로 두면 창을 닫아도 폰 인벤토리가 판매 모드에 남고,
        // 드래그 판매가 닫힌 상점을 상대로 계속 먹는다.
        // **이 컴포넌트는 루트에 있어 패널이 꺼져도 Update가 돌므로** 뒤늦게라도 정리할 수 있다.
        if (_merchant != null && panel != null && !panel.activeSelf) { Close(); return; }

        if (panel && panel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void Open(Merchant merchant)
    {
        _merchant      = merchant;
        ActiveMerchant = merchant;
        _selectedBuy   = null;
        _selectedBuyUI = null;

        if (panel)            panel.SetActive(true);
        if (merchantNameText) merchantNameText.text = merchant.Data?.MerchantType.DisplayName() ?? "상인";

        RebuildBuyList();
        RefreshGold();

        // **고른 폰이 없으면 인벤토리를 안 연다.** 팔 사람이 없는데 빈 배낭 창을 띄우면
        // 상점만 보려고 눌렀을 때 화면이 둘로 갈라진다. 판매는 폰을 고르고 나서 하는 일이다.
        bool hasPawn = GetPlayerInventory() != null;

        _didForceOpenInventory = hasPawn && inventoryPanel != null && !inventoryPanel.IsPanelOpen;
        if (hasPawn)
        {
            inventoryPanel?.ForceOpen();
            inventoryPanel?.SetSellMode(merchant);
        }
    }

    public void Close()
    {
        ItemInfoPanel.Hide();
        _merchant      = null;
        ActiveMerchant = null;
        if (panel) panel.SetActive(false);
        inventoryPanel?.ClearSellMode();
        if (_didForceOpenInventory) inventoryPanel?.ForceClose();
    }

    // ── 구매 ─────────────────────────────────────────────────────────────────

    void RebuildBuyList()
    {
        foreach (Transform t in stockContainer) Destroy(t.gameObject);
        _buyEntryUIs.Clear();
        if (_merchant == null || entryPrefab == null) return;

        foreach (var entry in _merchant.CurrentStock)
        {
            var ui         = Instantiate(entryPrefab, stockContainer);
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
        if (_merchant == null || _selectedBuy == null) return;

        int want = ReadCount(buyQuantity, MaxBuyable());

        int bought = 0;
        // 고른 폰이 있으면 그 배낭으로. 없거나 꽉 찼으면 `TryBuy`가 창고로 돌린다.
        for (int i = 0; i < want; i++)
        {
            if (!_merchant.TryBuy(_selectedBuy, GetPlayerInventory())) break;
            bought++;
        }
        if (bought == 0) return;

        RefreshGold();
        _selectedBuyUI?.RefreshStock();
        RefreshBuyButton();
    }

    // 살 수 있는 최대 = **재고와 지갑 중 적은 쪽.** 수량 칸에 아무 수나 적어도 이걸 못 넘는다.
    int MaxBuyable()
    {
        if (_selectedBuy == null) return 0;
        var goldItem = _merchant?.Data?.GoldItem;
        int gold     = goldItem != null ? Storage.GetTotalByItem(goldItem) : 0;
        int afford   = _selectedBuy.Price > 0 ? gold / _selectedBuy.Price : _selectedBuy.Stock;
        return Mathf.Max(0, Mathf.Min(_selectedBuy.Stock, afford));
    }

    void FillMaxBuyQuantity()
    {
        if (buyQuantity != null) buyQuantity.text = Mathf.Max(1, MaxBuyable()).ToString();
    }

    void RefreshBuyButton()
    {
        if (buyButton == null) return;
        if (_selectedBuy == null || _merchant?.Data?.GoldItem == null)
        { buyButton.interactable = false; return; }
        int gold = Storage.GetTotalByItem(_merchant.Data.GoldItem);
        buyButton.interactable = _selectedBuy.Stock > 0 && gold >= _selectedBuy.Price;
    }

    internal bool ExecuteSell(ItemInstance inst, int count = 1)
    {
        if (_merchant == null || inst == null) return false;
        var inv = GetPlayerInventory();
        if (inv == null) return false;
        if (_merchant.TrySell(inst, inv, count) <= 0) return false;
        RefreshGold();
        return true;
    }

    Inventory GetPlayerInventory()
    {
        if (_selection == null) _selection = Object.FindFirstObjectByType<SelectionManager>();
        if (_selection == null || _selection.Selected.Count == 0) return null;
        return _selection.Selected[0].GetComponent<Inventory>();
    }

    void RefreshGold()
    {
        if (goldText == null || _merchant?.Data?.GoldItem == null) return;
        goldText.text = $"골드: {Storage.GetTotalByItem(_merchant.Data.GoldItem)} G";
    }
}
