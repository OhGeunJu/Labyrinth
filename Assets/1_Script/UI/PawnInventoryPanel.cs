using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// I 키로 토글되는 인벤토리 패널 컨트롤러.
// 상인 열림 → SetSellMode / 루팅 열림 → SetLootMode 로 하단 액션 버튼 전환.
public class PawnInventoryPanel : MonoBehaviour
{
    public enum ActionMode { None, Sell, Loot }

    // DraggableItem → 패널에 아이템 선택/더블클릭 알림
    public static void NotifyItemSelected(ItemInstance inst, InventoryGridUI sourceGrid)
        => _instance?.OnItemSelected(inst, sourceGrid);

    public static void NotifyItemDoubleAction(ItemInstance inst, InventoryGridUI sourceGrid)
        => _instance?.OnItemDoubleAction(inst, sourceGrid);

    static PawnInventoryPanel _instance;

    [Header("씬 참조")]
    [SerializeField] GameObject      panel;
    [SerializeField] RectTransform   gridContainer;
    [SerializeField] EquipSlotUI     mainHandSlot;
    [SerializeField] EquipSlotUI     offHandSlot;
    [SerializeField] EquipSlotUI     headSlot;
    [SerializeField] EquipSlotUI     chestSlot;
    [SerializeField] EquipSlotUI     handsSlot;
    [SerializeField] EquipSlotUI     legsSlot;

    [Header("입력")]
    [SerializeField] Key toggleKey = Key.I;

    [Header("액션 바")]
    [SerializeField] GameObject      actionBar;
    [SerializeField] TextMeshProUGUI actionItemText;
    [SerializeField] TextMeshProUGUI actionButtonLabel;
    [SerializeField] Button          actionButton;

    [Header("판매 수량")]
    [Tooltip("수량 칸 + [전부] 버튼의 부모. 판매 모드에서만 켜진다")]
    [SerializeField] GameObject      sellQuantityGroup;
    [Tooltip("한 번에 팔 개수. 비우면 1개씩")]
    [SerializeField] TMP_InputField  sellQuantity;
    [Tooltip("고른 스택 개수를 수량 칸에 넣는 버튼")]
    [SerializeField] Button          sellMaxButton;

    ItemInstance _lastSellSelection;   // 고른 것이 바뀌면 수량을 1로 되돌리려고 기억한다

    InventoryGridUI  _gridUI;
    SelectionManager _selection;

    ActionMode      _mode;
    ItemInstance    _selectedItem;
    InventoryGridUI _selectedSourceGrid;
    Merchant        _merchant;
    Inventory       _lootInventory;

    // 상인 모드 진입 전 상태 백업 (상인 닫을 때 복원용)
    ActionMode _prevMode;
    Inventory  _prevLootInventory;

    public bool IsPanelOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        _instance = this;
        if (gridContainer != null)
        {
            _gridUI = gridContainer.GetComponent<InventoryGridUI>();
            if (_gridUI == null) _gridUI = gridContainer.gameObject.AddComponent<InventoryGridUI>();
        }
        if (panel)     panel.SetActive(false);
        if (actionBar) actionBar.SetActive(false);
        if (actionButton) actionButton.onClick.AddListener(OnActionClicked);
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active && panel != null) panel.SetActive(false); }

    void Start()
    {
        _selection = FindFirstObjectByType<SelectionManager>();

        if (sellMaxButton != null)
            sellMaxButton.onClick.AddListener(() =>
            {
                if (sellQuantity != null && _selectedItem != null)
                    sellQuantity.text = _selectedItem.Quantity.ToString();
            });
    }

    void Update()
    {
        if (panel == null || Keyboard.current == null) return;

        // 수량 칸에 숫자를 치는 중이면 I(토글)가 먹으면 안 된다 — 창이 닫혀 버린다.
        if (sellQuantity != null && sellQuantity.isFocused) return;

        if (Keyboard.current[toggleKey].wasPressedThisFrame) Toggle();

        // 상점이 어떤 길로 닫히든 [판매] 표시가 따라 사라지게 한다.
        // 닫는 쪽마다 정리를 부르게 하면 언젠가 한 곳을 빠뜨린다 — 여기서 한 번 더 본다.
        if (_mode == ActionMode.Sell && MerchantUI.ActiveMerchant == null) RefreshActionBar();
    }

    void Toggle()
    {
        bool open = !panel.activeSelf;
        panel.SetActive(open);
        if (open) BindToSelected();
        else if (_mode == ActionMode.None) ClearActionMode();
    }

    void BindToSelected()
    {
        if (_gridUI == null) return;
        if (_selection == null || _selection.Selected.Count == 0)
        {
            _gridUI.Bind(null);
            BindEquipSlots(null);
            return;
        }
        var pawn = _selection.Selected[0];
        _gridUI.Bind(pawn.GetComponent<Inventory>());
        BindEquipSlots(pawn.GetComponent<Equipment>());
    }

    void BindEquipSlots(Equipment eq)
    {
        mainHandSlot?.Bind(eq);
        offHandSlot?.Bind(eq);
        headSlot?.Bind(eq);
        chestSlot?.Bind(eq);
        handsSlot?.Bind(eq);
        legsSlot?.Bind(eq);
    }

    // ── 외부 진입점 ──────────────────────────────────────────────────────────

    public void ForceOpen()
    {
        if (panel == null || panel.activeSelf) return;
        panel.SetActive(true);
        BindToSelected();
    }

    public void ForceClose()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void SetSellMode(Merchant merchant)
    {
        _prevMode          = _mode;
        _prevLootInventory = _lootInventory;
        _mode              = ActionMode.Sell;
        _merchant          = merchant;
        _selectedItem      = null;
        RefreshActionBar();
    }

    // 상인 닫힐 때 호출 — 루팅 중이었으면 루팅 모드로 복원
    public void ClearSellMode()
    {
        _merchant     = null;
        _selectedItem = null;
        if (_prevMode == ActionMode.Loot && _prevLootInventory != null)
        {
            _mode          = ActionMode.Loot;
            _lootInventory = _prevLootInventory;
        }
        else
        {
            _mode = ActionMode.None;
        }
        _prevMode          = ActionMode.None;
        _prevLootInventory = null;
        RefreshActionBar();
    }

    public void SetLootMode(Inventory lootInventory)
    {
        _mode          = ActionMode.Loot;
        _lootInventory = lootInventory;
        _merchant      = null;
        _selectedItem  = null;
        RefreshActionBar();
    }

    public void ClearActionMode()
    {
        _mode          = ActionMode.None;
        _merchant      = null;
        _lootInventory = null;
        _selectedItem  = null;
        if (actionBar) actionBar.SetActive(false);
    }

    // ── DraggableItem 콜백 ───────────────────────────────────────────────────

    void OnItemSelected(ItemInstance inst, InventoryGridUI sourceGrid)
    {
        if (_mode == ActionMode.None) return;
        _selectedItem       = inst;
        _selectedSourceGrid = sourceGrid;
        RefreshActionBar();
    }

    void OnItemDoubleAction(ItemInstance inst, InventoryGridUI sourceGrid)
    {
        _selectedItem       = inst;
        _selectedSourceGrid = sourceGrid;
        if (_mode == ActionMode.Sell) ExecuteSell();
        else if (_mode == ActionMode.Loot) ExecuteLoot();
    }

    // ── 액션 버튼 ────────────────────────────────────────────────────────────

    void OnActionClicked()
    {
        if (_mode == ActionMode.Sell) ExecuteSell();
        else if (_mode == ActionMode.Loot) ExecuteLoot();
    }

    void ExecuteSell()
    {
        if (_merchant == null || _selectedItem == null) return;

        // 수량은 **입력칸에 적힌 수**를 쓴다. 스택 개수를 넘지 못하게 잘라 준다.
        int want     = MerchantUI.ReadCount(sellQuantity, _selectedItem.Quantity);
        var instance = _selectedItem;
        if (want <= 0) return;

        if (!MerchantUI.TrySellItem(instance, want)) return;

        // 스택이 남았으면 선택을 유지한다 — 열 개씩 팔 때 매번 다시 고르게 하면 안 된다.
        if (instance.Quantity <= 0) _selectedItem = null;
        RefreshActionBar();
    }

    void ExecuteLoot()
    {
        if (_lootInventory == null || _selectedItem == null) return;
        var playerInv = GetPlayerInventory();
        if (playerInv == null) return;

        // 선택 아이템이 플레이어 인벤토리에 있으면 → 루팅 인벤토리로 이동
        // 루팅 인벤토리에 있으면 → 플레이어 인벤토리로 이동
        bool fromPlayer = _selectedSourceGrid == _gridUI;
        var srcInv = fromPlayer ? playerInv : _lootInventory;
        var dstInv = fromPlayer ? _lootInventory : playerInv;

        int added = dstInv.TryAdd(_selectedItem.Data, _selectedItem.Quantity, _selectedItem.Quality);
        if (added <= 0) return;

        if (added >= _selectedItem.Quantity)
            srcInv.RemoveInstance(_selectedItem);
        else
            _selectedItem.Quantity -= added;

        _selectedItem = null;
        RefreshActionBar();
    }

    void RefreshActionBar()
    {
        if (actionBar == null) return;

        // **판매 모드는 상점이 실제로 열려 있을 때만 성립한다.**
        // 상점을 닫는 길이 여럿이라(X 버튼·Esc·건설 모드 진입) 한 군데서 `ClearSellMode`를
        // 빠뜨리면 인벤토리에 [판매]가 그대로 남는다 — 팔 상대가 없는데 파는 버튼이 보인다.
        // 상태를 복구하려 하지 말고 **매번 조건으로 확인**한다.
        if (_mode == ActionMode.Sell && MerchantUI.ActiveMerchant == null)
        {
            ClearActionMode();
            return;
        }

        if (_mode == ActionMode.None)
        {
            actionBar.SetActive(false);
            return;
        }

        actionBar.SetActive(true);

        if (actionButtonLabel)
            actionButtonLabel.text = _mode == ActionMode.Sell ? "판매" : "옮기기";

        // 수량 칸은 **판매할 때만** 보인다. 옮기기는 스택을 통째로 다루므로 적을 것이 없다.
        bool showQty = _mode == ActionMode.Sell;
        if (sellQuantityGroup) sellQuantityGroup.SetActive(showQty);

        // 고를 때마다 1로 되돌린다 — 앞 물건에 적었던 수가 남아 있으면
        // 다음 물건을 그 개수만큼 팔아 버린다(되돌릴 수 없는 사고다).
        if (showQty && sellQuantity && _selectedItem != null && _lastSellSelection != _selectedItem)
        {
            sellQuantity.text  = "1";
            _lastSellSelection = _selectedItem;
        }

        // 선택된 아이템 정보
        if (actionItemText)
        {
            if (_selectedItem == null)
            {
                actionItemText.text = _mode == ActionMode.Sell
                    ? "아이템을 선택하세요"
                    : "아이템을 선택하세요";
            }
            else if (_mode == ActionMode.Sell)
            {
                int price = _merchant?.GetSellPrice(_selectedItem) ?? 0;
                string qc = ColorUtility.ToHtmlStringRGB(ItemInfoPanel.QualityColor(_selectedItem.Quality));
                actionItemText.text =
                    $"<color=#{qc}>[{_selectedItem.Quality.DisplayName()}]</color> {_selectedItem.Data?.ItemName}" +
                    $"  →  {(price > 0 ? $"<color=#FFD94D>{price} G</color>" : "<color=#888>판매 불가</color>")}";
                if (actionButton) actionButton.interactable = price > 0;
            }
            else
            {
                actionItemText.text = $"{_selectedItem.Data?.ItemName} ×{_selectedItem.Quantity}";
                if (actionButton) actionButton.interactable = true;
            }
        }

        if (_selectedItem == null && actionButton)
            actionButton.interactable = false;
    }

    Inventory GetPlayerInventory()
    {
        if (_selection == null) _selection = FindFirstObjectByType<SelectionManager>();
        if (_selection == null || _selection.Selected.Count == 0) return null;
        return _selection.Selected[0].GetComponent<Inventory>();
    }
}
