using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// I 키로 토글되는 인벤토리 패널 컨트롤러.
// 상인 열림 → SetSellMode / 루팅 열림 → SetLootMode 로 하단 액션 버튼 전환.
public class InventoryUI : MonoBehaviour
{
    public enum ActionMode { None, Sell, Loot }

    // DraggableItem → 패널에 아이템 선택/더블클릭 알림
    public static void NotifyItemSelected(ItemInstance inst, InventoryGridUI sourceGrid)
        => _instance?.OnItemSelected(inst, sourceGrid);

    public static void NotifyItemDoubleAction(ItemInstance inst, InventoryGridUI sourceGrid)
        => _instance?.OnItemDoubleAction(inst, sourceGrid);

    static InventoryUI _instance;

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

    [Header("적재 바 (액션 바 위 한 줄) — DungeonColony → Create Carry Bar 로 생성")]
    [Tooltip("`무게 12/15kg`. 과적이면 붉게 물든다")]
    [SerializeField] TextMeshProUGUI weightText;

    [Tooltip("이 폰이 남는 짐을 창고로 나를까 (P키 무리 관리와 같은 설정)")]
    [SerializeField] Button          unloadButton;
    [SerializeField] TextMeshProUGUI unloadLabel;

    [Tooltip("켜고 아이템을 누르면 그 뭉치가 잠긴다 — 자동 운반·재료·판매·버리기에서 빠진다")]
    [SerializeField] Button          lockButton;
    [SerializeField] TextMeshProUGUI lockLabel;

    [Tooltip("적재 바 오른쪽 끝의 폰 넘겨보기 묶음. 원정대 거래일 때만 켜진다")]
    [SerializeField] GameObject      traderGroup;
    [SerializeField] TextMeshProUGUI traderNameText;
    [SerializeField] Button          traderPrevButton;
    [SerializeField] Button          traderNextButton;

    ItemInstance _lastSellSelection;   // 고른 것이 바뀌면 수량을 1로 되돌리려고 기억한다

    InventoryGridUI  _gridUI;
    SelectionManager _selection;

    ActionMode      _mode;
    ItemInstance    _selectedItem;
    InventoryGridUI _selectedSourceGrid;
    Shop            _shop;
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
        if (unloadButton) unloadButton.onClick.AddListener(ToggleUnload);
        if (lockButton)   lockButton.onClick.AddListener(ToggleLockMode);
        if (traderPrevButton) traderPrevButton.onClick.AddListener(() => StepPawn(-1));
        if (traderNextButton) traderNextButton.onClick.AddListener(() => StepPawn(+1));
        if (traderGroup)  traderGroup.SetActive(false);
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;

        RefreshLockToggle();
        RefreshUnloadToggle();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
        if (_weightInventory != null) _weightInventory.OnChanged -= RefreshWeight;
        if (_walletHooked) Wallet.OnChanged -= OnWalletChanged;
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

        // 월드맵에서 I를 누르면 **아무도 안 고른 빈 배낭**이 뜬다 — 거기선 선택이 팀 단위라
        // 고른 폰이라는 게 없다. 거래로 폰이 지정돼 있을 때만 연다.
        if (Keyboard.current[toggleKey].wasPressedThisFrame &&
            (GameModeManager.IsField || _boundPawn != null))
            Toggle();

        // 상점이 어떤 길로 닫히든 [판매] 표시가 따라 사라지게 한다.
        // 닫는 쪽마다 정리를 부르게 하면 언젠가 한 곳을 빠뜨린다 — 여기서 한 번 더 본다.
        if (_mode == ActionMode.Sell && MerchantUI.ActiveShop == null) RefreshActionBar();
    }

    void Toggle()
    {
        bool open = !panel.activeSelf;
        panel.SetActive(open);

        // **닫으면 남의 배낭에서 손을 뗀다.** 포로 소지품처럼 밖에서 지정해 연 것은
        // **한 번 보는 것**이라, 안 풀면 다음에 I를 눌렀을 때 내 폰 대신 그 포로의 배낭이
        // 열린다 — 왜 내 물건이 안 보이는지 알 방법이 없다.
        //
        // **원정 배낭 돌려보기(`_cycle`)는 예외다** — 거기서는 지정이 곧 화면의 상태이고
        // [이전]/[다음]으로 계속 갈아탄다. 그쪽은 `ClearPawnCycle`이 따로 거둔다.
        if (!open && _cycle == null) _boundPawn = null;

        if (open) BindToSelected();
        else if (_mode == ActionMode.None) ClearActionMode();
    }

    // **밖에서 지정한 폰**이 있으면 선택보다 그게 이긴다. 월드맵 마을에서 거래할 때 쓴다 —
    // 원정 나간 폰은 `SelectionManager`에 잡히지 않으므로(GameObject가 꺼져 있다) 선택을
    // 통해서는 배낭을 열 길이 아예 없다.
    UnitBrain _boundPawn;

    public UnitBrain BoundPawn => _boundPawn;

    public void BindTo(UnitBrain pawn)
    {
        _boundPawn = pawn;
        BindToSelected();
    }

    public void ClearBoundPawn()
    {
        if (_boundPawn == null) return;
        _boundPawn = null;
        BindToSelected();
    }

    // ── 폰 넘겨보기 (원정대) ─────────────────────────────────────────────────
    //
    // **"누구의 배낭인가"는 배낭 창의 정보다.** 상점 창에 달면 상점을 안 열었을 땐 넘겨볼
    // 수단이 없고, 무게·짐내림·잠금이 이미 "이 폰"의 것인데 폰을 고르는 자리만 딴 창에 있게 된다.
    //
    // 월드맵에서만 쓴다 — 필드에서는 폰을 클릭해 고르는 방식이 이미 있고, 같은 일을 하는
    // 조작이 둘이면 어느 쪽이 이기는지 헷갈린다. 그래서 목록이 없으면 줄 자체가 꺼진다.
    IReadOnlyList<UnitBrain> _cycle;
    int                      _cycleIndex;

    public void SetPawnCycle(IReadOnlyList<UnitBrain> pawns)
    {
        _cycle      = (pawns != null && pawns.Count > 0) ? pawns : null;
        _cycleIndex = 0;
        BindTo(_cycle != null ? _cycle[0] : null);
        RefreshTraderBar();
    }

    public void ClearPawnCycle()
    {
        _cycle = null;
        ClearBoundPawn();
        RefreshTraderBar();
    }

    void StepPawn(int delta)
    {
        if (_cycle == null || _cycle.Count == 0) return;
        _cycleIndex = (_cycleIndex + delta + _cycle.Count) % _cycle.Count;
        BindTo(_cycle[_cycleIndex]);
        RefreshTraderBar();
    }

    void RefreshTraderBar()
    {
        if (traderGroup == null) return;

        bool show = _cycle != null;
        traderGroup.SetActive(show);
        if (!show) return;

        var pawn = _cycle[Mathf.Clamp(_cycleIndex, 0, _cycle.Count - 1)];
        if (traderNameText != null)
            traderNameText.text = _cycle.Count > 1
                ? $"{pawn?.UnitName ?? "?"} ({_cycleIndex + 1}/{_cycle.Count})"
                : pawn?.UnitName ?? "?";

        // 혼자면 넘길 곳이 없다 — 눌러도 아무 일 없는 버튼을 남기지 않는다.
        if (traderPrevButton != null) traderPrevButton.gameObject.SetActive(_cycle.Count > 1);
        if (traderNextButton != null) traderNextButton.gameObject.SetActive(_cycle.Count > 1);
    }

    void BindToSelected()
    {
        if (_gridUI == null) return;

        var pawn = _boundPawn != null
                 ? _boundPawn.transform
                 : (_selection != null && _selection.Selected.Count > 0
                        ? _selection.Selected[0].transform
                        : null);

        if (pawn == null)
        {
            _gridUI.Bind(null);
            BindEquipSlots(null);
            BindWeight(null);
            return;
        }

        _gridUI.Bind(pawn.GetComponent<Inventory>());
        BindEquipSlots(pawn.GetComponent<Equipment>());
        BindWeight(pawn.GetComponent<UnitBrain>());
    }

    // ── 적재 무게 · 짐내림 · 잠금 ────────────────────────────────────────────
    // 한도를 넘겨도 **넣는 건 막지 않고 느려지게** 했으므로(자동 운반만 한도를 지킨다),
    // 지금 얼마나 졌는지가 안 보이면 "왜 이 폰만 느리지"를 설명할 길이 없다.
    //
    // 셋 다 **씬 오브젝트다**(`Canvas/.../CarryBar`). 한때 런타임에 만들었는데, 그러면
    // 편집 중에는 없는 것이 되어 자리·크기를 눈으로 맞출 수가 없고 (실제로 잠금 버튼이
    // 화면에 안 잡혔다) 인스펙터에서 옮길 수도 없다 — "모양은 씬이, 코드는 데이터만".
    UnitBrain _weightPawn;
    Inventory _weightInventory;

    bool _walletHooked;

    void BindWeight(UnitBrain brain)
    {
        // 인벤토리가 바뀔 때만 다시 그린다 — 매 프레임 무게를 합산할 이유가 없다.
        if (_weightInventory != null) _weightInventory.OnChanged -= RefreshWeight;

        _weightPawn      = brain;
        _weightInventory = brain != null ? brain.Inventory : null;
        if (_weightInventory != null) _weightInventory.OnChanged += RefreshWeight;

        // 잔고는 배낭과 무관하게 움직인다(시체 루팅·거래·몸값) — 따로 구독해야 같은 줄이
        // 뒤처지지 않는다. **한 번만 건다**: 폰을 바꿀 때마다 걸면 중복으로 쌓인다.
        if (!_walletHooked)
        {
            Wallet.OnChanged += OnWalletChanged;
            _walletHooked = true;
        }

        RefreshWeight();
    }

    void OnWalletChanged(int _) => RefreshWeight();

    // ── 짐내림 토글 ──────────────────────────────────────────────────────────
    // P키 무리 관리에도 있지만, **짐을 보면서 정하는 설정**이라 배낭 창에도 둔다.
    // "이 폰 배낭에 뭐가 들었나"를 보다가 바로 "이건 창고로 보내라"를 정할 수 있어야 한다.
    void ToggleUnload()
    {
        var work = _weightPawn != null ? _weightPawn.Work : null;
        if (work == null) return;
        work.Toggle(JobCategory.Unload);      // P키 패널과 **같은 설정**을 건드린다
        RefreshUnloadToggle();
    }

    void RefreshUnloadToggle()
    {
        if (unloadButton == null) return;

        var work = _weightPawn != null ? _weightPawn.Work : null;
        unloadButton.gameObject.SetActive(work != null);   // 적·NPC는 작업 설정이 없다
        if (work == null) return;

        bool on = work.IsEnabled(JobCategory.Unload);
        if (unloadLabel != null)
            unloadLabel.color = on ? Color.white : OffLabelColor;
        SetButtonColor(unloadButton, on ? OnBgColor : OffBgColor);
    }

    // ── 잠금 모드 ────────────────────────────────────────────────────────────
    // **무엇을 남길지는 종류가 아니라 플레이어가 정한다.** [잠금]을 켜고 아이템을 누르면
    // 그 뭉치가 잠긴다. 다시 누르면 풀린다.
    //
    // 모드로 만든 이유: 마우스 버튼 하나로는 UI 버튼을 누른 채 다른 것을 클릭할 수 없다.
    // 켜고 → 여러 개 찍고 → 끄는 편이 한 개씩 보조키를 짚는 것보다 손이 덜 간다.
    //
    // 잠기면 **자동 운반·재료 집계·판매·버리기**에서 빠진다(`ItemInstance.Locked` 주석).
    // 드래그로 옮기고 장착하는 것은 그대로 된다.
    bool _lockMode;

    static readonly Color OnBgColor      = new Color(0.20f, 0.50f, 0.28f);
    static readonly Color LockOnBgColor  = new Color(0.55f, 0.42f, 0.16f);
    static readonly Color OffBgColor     = new Color(0.24f, 0.24f, 0.28f);
    static readonly Color OffLabelColor  = new Color(0.55f, 0.55f, 0.58f);

    static void SetButtonColor(Button btn, Color c)
    {
        var img = btn != null ? btn.GetComponent<Image>() : null;
        if (img != null) img.color = c;
    }

    void ToggleLockMode()
    {
        _lockMode = !_lockMode;
        RefreshLockToggle();
    }

    void RefreshLockToggle()
    {
        if (lockButton == null) return;

        if (lockLabel != null)
            lockLabel.color = _lockMode ? Color.white : OffLabelColor;
        SetButtonColor(lockButton, _lockMode ? LockOnBgColor : OffBgColor);
    }

    // 반환: 잠금을 실제로 건드렸나(= 클릭을 이 모드가 삼켰나).
    bool ToggleItemLock(ItemInstance inst, InventoryGridUI sourceGrid)
    {
        if (inst == null) return false;

        inst.Locked = !inst.Locked;

        // 표시를 다시 그린다. `Locked`는 인벤토리가 모르는 값이라 `OnChanged`가 안 울린다 —
        // 그리드에 직접 말해야 잠금 표시가 바뀐다.
        sourceGrid?.RefreshItems();
        return true;
    }

    void RefreshWeight()
    {
        RefreshUnloadToggle();
        RefreshLockToggle();

        if (weightText == null) return;

        if (_weightPawn == null) { weightText.text = ""; return; }

        float load = CarryCapacity.Load(_weightPawn);
        float cap  = CarryCapacity.For(_weightPawn);

        // **잔고를 같은 줄에 붙인다.** 돈이 아이템이던 시절엔 배낭 격자에서 눈으로 셌는데,
        // 전역 잔고가 되면서 상시로 볼 데가 사라졌다. 새 칸을 만들려면 씬을 고쳐야 하고,
        // 무게와 돈은 둘 다 "지금 얼마나 지고 다니나"라 같은 줄에 있어도 어색하지 않다.
        weightText.text  = $"무게 {load:0.#}/{cap:0.#}kg   골드 {Wallet.Balance:N0}";
        // 과적은 색으로 알린다 — 숫자만으로는 넘겼는지 눈에 안 들어온다.
        weightText.color = load > cap
            ? new Color(0.95f, 0.55f, 0.40f)
            : new Color(0.82f, 0.82f, 0.86f);
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

    public void SetSellMode(Shop shop)
    {
        _prevMode          = _mode;
        _prevLootInventory = _lootInventory;
        _mode              = ActionMode.Sell;
        _shop          = shop;
        _selectedItem      = null;
        RefreshActionBar();
    }

    // 상인 닫힐 때 호출 — 루팅 중이었으면 루팅 모드로 복원
    public void ClearSellMode()
    {
        _shop     = null;
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
        _shop      = null;
        _selectedItem  = null;
        RefreshActionBar();
    }

    public void ClearActionMode()
    {
        _mode          = ActionMode.None;
        _shop      = null;
        _lootInventory = null;
        _selectedItem  = null;
        if (actionBar) actionBar.SetActive(false);
    }

    // ── DraggableItem 콜백 ───────────────────────────────────────────────────

    void OnItemSelected(ItemInstance inst, InventoryGridUI sourceGrid)
    {
        // 잠금 모드에서는 클릭이 **잠금 토글**이다. 판매·루팅 선택으로 넘기지 않는다 —
        // 한 클릭이 두 가지를 뜻하면 어느 쪽이 일어났는지 알 수 없다.
        if (_lockMode && ToggleItemLock(inst, sourceGrid)) return;

        if (_mode == ActionMode.None) return;
        _selectedItem       = inst;
        _selectedSourceGrid = sourceGrid;
        RefreshActionBar();
    }

    void OnItemDoubleAction(ItemInstance inst, InventoryGridUI sourceGrid)
    {
        // 더블클릭은 단클릭 두 번으로도 온다 — 잠금 모드에서 즉시 판매/루팅이 튀어나오면
        // 잠그려다 물건을 팔아 버린다.
        if (_lockMode) return;

        // **동전은 쓰는 물건이다.** 어느 모드든 여기서 먼저 잡는다 —
        // 판매 모드에서 동전을 팔거나, 루팅 모드에서 동전을 배낭 칸으로 옮기는 것은
        // 둘 다 할 이유가 없는 짓이다. 시체에서 바로 챙기는 것도 이 길로 된다.
        if (TryClaimCoins(inst, sourceGrid)) return;

        _selectedItem       = inst;
        _selectedSourceGrid = sourceGrid;
        if (_mode == ActionMode.Sell) ExecuteSell();
        else if (_mode == ActionMode.Loot) ExecuteLoot();
    }

    // 동전 뭉치를 잔고로 바꾼다. 아이템이 아니었으면 거짓.
    //
    // **자동으로 빨아들이지 않는다**(사용자 결정 2026-08-10): 동전은 세상에 아이템으로 남고,
    // 눌러야 우리 것이 된다. 그래서 시작할 때 이미 놓여 있던 동전을 걷어들이는 전환도,
    // "어느 인벤토리가 우리 것인가"를 가리는 스위치도 필요 없다 — **누르는 사람이 곧 임자다.**
    bool TryClaimCoins(ItemInstance inst, InventoryGridUI sourceGrid)
    {
        if (inst == null || !Wallet.IsCurrency(inst.Data)) return false;

        var inv = sourceGrid != null ? sourceGrid.Inventory : null;
        if (inv == null) return false;

        int amount = inst.Quantity;
        if (amount <= 0) return false;

        inv.RemoveInstance(inst);
        Wallet.Add(amount);

        // 고른 것이 사라졌으므로 액션 바를 다시 그린다 — 안 그러면 없는 물건에 [판매]가 남는다.
        if (_selectedItem == inst) _selectedItem = null;
        RefreshActionBar();
        return true;
    }

    // ── 액션 버튼 ────────────────────────────────────────────────────────────

    void OnActionClicked()
    {
        if (_mode == ActionMode.Sell) ExecuteSell();
        else if (_mode == ActionMode.Loot) ExecuteLoot();
    }

    void ExecuteSell()
    {
        if (_shop == null || _selectedItem == null) return;

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
        if (_mode == ActionMode.Sell && MerchantUI.ActiveShop == null)
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
                int price = _shop?.GetSellPrice(_selectedItem) ?? 0;
                string qc = ColorUtility.ToHtmlStringRGB(ItemInfoUI.QualityColor(_selectedItem.Quality));
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
