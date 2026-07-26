using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class MerchantUI : MonoBehaviour
{
    public static Merchant      ActiveMerchant { get; private set; }
    public static RectTransform SellZoneRect   { get; private set; }

    // DraggableItem 드롭 판매용
    public static bool TrySellItem(ItemInstance inst)
        => _instance != null && _instance.ExecuteSell(inst);

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
        _didForceOpenInventory = inventoryPanel != null && !inventoryPanel.IsPanelOpen;
        inventoryPanel?.ForceOpen();
        inventoryPanel?.SetSellMode(merchant);
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
        if (!_merchant.TryBuy(_selectedBuy)) return;
        RefreshGold();
        _selectedBuyUI?.RefreshStock();
        RefreshBuyButton();
    }

    void RefreshBuyButton()
    {
        if (buyButton == null) return;
        if (_selectedBuy == null || _merchant?.Data?.GoldItem == null)
        { buyButton.interactable = false; return; }
        int gold = Storage.GetTotalByItem(_merchant.Data.GoldItem);
        buyButton.interactable = _selectedBuy.Stock > 0 && gold >= _selectedBuy.Price;
    }

    internal bool ExecuteSell(ItemInstance inst)
    {
        if (_merchant == null || inst == null) return false;
        var inv = GetPlayerInventory();
        if (inv == null) return false;
        if (!_merchant.TrySell(inst, inv)) return false;
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
