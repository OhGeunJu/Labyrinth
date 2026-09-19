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

    // **클릭이 집기가 아닌 때가 있다.** 잠금 모드에서는 한 번 누르는 것이 자물쇠를 거는
    // 일이라, 집어 버리면 잠그려다 물건이 손에 들린다 — 집기 전에 먼저 물어본다.
    // 참을 돌려주면 그 클릭은 여기서 끝난 것이다(`DraggableItem.BeginCarry`).
    public static bool ClickConsumed(ItemInstance inst, InventoryGridUI sourceGrid)
        => _instance != null && _instance.ConsumeModalClick(inst, sourceGrid);

    // **아이콘도 같이 받는다** — 답을 그 아이콘 자리에서 돌려주려고(먹기의 잔상·거절 흔들림).
    public static void NotifyItemRightClick(ItemInstance inst, InventoryGridUI sourceGrid,
                                            DraggableItem icon = null)
        => _instance?.OnItemRightClick(inst, sourceGrid, icon);

    static InventoryUI _instance;

    // 배낭 창 하나. **장비칸 드래그가 칸 크기를 여기서 베낀다**(`Grid.CellSize`) — 손에 든
    // 그림이 놓일 자리와 같은 크기여야 얼마나 큰 것을 들고 있는지가 보인다.
    public static InventoryUI Instance => _instance;

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

    [Tooltip("칸을 다시 채워 넣는다 — 같은 것끼리 합치고 큰 것부터 빈틈없이")]
    [SerializeField] Button          tidyButton;

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

    // 폰 배낭 격자. **상점 진열이 이걸 베낀다**(칸 크기·격자 크기) — 나란히 떠 있는 두 격자가
    // 다른 눈금을 쓰면 같은 물건이 다른 크기로 보인다.
    public InventoryGridUI Grid => _gridUI;

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
        if (tidyButton)   tidyButton.onClick.AddListener(TidyGrid);
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
        if (Keyboard.current[toggleKey].wasPressedThisFrame && !InputFocus.KeysBlocked() &&
            (GameModeManager.IsField || _boundPawn != null))
            Toggle();

        // 상점이 어떤 길로 닫히든 [판매] 표시가 따라 사라지게 한다.
        // 닫는 쪽마다 정리를 부르게 하면 언젠가 한 곳을 빠뜨린다 — 여기서 한 번 더 본다.
        if (_mode == ActionMode.Sell && MerchantUI.ActiveShop == null) RefreshActionBar();
    }

    void Toggle()
    {
        bool open = !panel.activeSelf;
        if (open) WindowRaiser.Open(panel);
        else      WindowRaiser.Close(panel);

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
    // **필드에서도 쓴다**(2026-08-14). 한동안 월드맵 전용이었다 — "필드에서는 폰을 클릭해
    // 고르는 방식이 이미 있고, 같은 일을 하는 조작이 둘이면 어느 쪽이 이기는지 헷갈린다"가
    // 그 이유였다. 그런데 **둘은 같은 일이 아니다**: 선택은 *누구를 데리고 왔나*이고 순환은
    // *그중 누구 배낭을 보나*다. 순환이 도는 범위가 선택의 부분집합이라 겨룰 데가 없고,
    // 여럿을 고른 채 상점을 열면 오히려 순환이 없이는 **첫 번째로 잡힌 폰의 배낭에 갇힌다**
    // (그 "첫 번째"는 레이캐스트 순서라 고른 사람이 정한 것도 아니다).
    //
    // 목록이 없으면 줄 자체가 꺼지고, 혼자면 버튼만 숨는다.
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

    // **칸을 다시 채워 넣는다**(`Inventory.Compact`).
    //
    // 격자 배낭은 쓰다 보면 반드시 조각난다 — 가운데 것을 꺼내면 그 자리가 구멍으로 남고,
    // 칸 수로는 넉넉한데 **2×4 갑옷이 들어갈 연속된 자리가 없어** 거절당한다. 그때 물건을
    // 하나씩 끌어 옮기는 것은 놀이가 아니라 잡일이다.
    //
    // **지금 보고 있는 통을 정리한다** — 배낭이든 창고든 시체든, 이 창이 그리고 있는 그것.
    // 격자가 `OnChanged`를 듣고 있어 다시 그리는 것은 저절로 된다.
    void TidyGrid() => _gridUI?.Inventory?.Compact();

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
        // **못 끼는 폰에게는 칸을 안 보여 준다**(`UnitData.CanEquip`) — 골렘·짐승.
        //
        // 막기만 하고 칸을 남기면 **넣어도 아무 일이 안 일어나는 칸**이 생긴다. 그건
        // "안 되는 것"이 아니라 "고장 난 것"으로 읽힌다 — 이 저장소가 못 누르는 줄을
        // 지우는 대신 흐리게 두고 이유를 띄우는 것과 같은 축이고, 여기서는 *"이 종족은
        // 아예 장비를 안 쓴다"*라 칸 자체가 없는 것이 맞다.
        bool show = eq == null || eq.CanEquip;

        Show(mainHandSlot, eq, show);
        Show(offHandSlot,  eq, show);
        Show(headSlot,     eq, show);
        Show(chestSlot,    eq, show);
        Show(handsSlot,    eq, show);
        Show(legsSlot,     eq, show);
    }

    static void Show(EquipSlotUI slot, Equipment eq, bool show)
    {
        if (slot == null) return;
        slot.gameObject.SetActive(show);
        if (show) slot.Bind(eq);
    }

    // ── 외부 진입점 ──────────────────────────────────────────────────────────

    public void ForceOpen()
    {
        if (panel == null || panel.activeSelf) return;
        WindowRaiser.Open(panel);
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
        // 잠금 모드의 클릭은 **집기보다 먼저** 처리된다(`ConsumeModalClick`) — 여기까지
        // 오지 않는다.

        if (_mode == ActionMode.None) return;
        _selectedItem       = inst;
        _selectedSourceGrid = sourceGrid;
        RefreshActionBar();
    }

    // 잠금 모드의 클릭 — 집기보다 먼저 돈다(`ClickConsumed`).
    bool ConsumeModalClick(ItemInstance inst, InventoryGridUI sourceGrid)
        => _lockMode && ToggleItemLock(inst, sourceGrid);

    // ── 우클릭: 한 번에 보내기 ────────────────────────────────────────────────
    //
    // **뜻은 무엇이 열려 있느냐가 정한다.**
    //   · 반대편 통이 열려 있으면 → **그리로 옮긴다**(배낭 ↔ 창고·작업대·시체).
    //   · 배낭만 열려 있으면 → **장비는 입고, 음식은 먹는다.**
    //
    // 둘을 다른 조작으로 가르지 않는 이유: 우클릭으로 하고 싶은 일은 늘 **"이걸 저기로"**
    // 하나인데, "저기"가 상황마다 다를 뿐이다. 통이 열려 있는데 옷을 입혀 버리면 옮기려던
    // 사람이 놀라고, 통이 없는데 아무 일도 안 하면 배낭에서 장비를 차는 길이 드래그뿐이 된다.
    //
    // **음식의 "저기"는 입이다**(2026-08-20) — 동전의 저기가 지갑이고 장비의 저기가 몸인 것과
    // 같은 자리다. 그래서 새 조작을 만들지 않았다: 규칙이 하나 더 생기면 그것만 따로 외워야 한다.
    //
    // **통이 열려 있으면 옮기는 쪽이 이긴다** — 창고를 열어 둔 채 밥을 옮기려던 사람이
    // 그것을 먹어 버리면 되돌릴 수가 없다(옮기기는 무를 수 있지만 먹은 것은 못 무른다).
    void OnItemRightClick(ItemInstance inst, InventoryGridUI sourceGrid, DraggableItem icon)
    {
        // 잠금 모드에서는 누르는 일이 잠그는 것뿐이다 — 잠그려다 물건을 보내면 안 된다.
        if (_lockMode) return;
        if (inst?.Data == null || sourceGrid == null || sourceGrid.Inventory == null) return;

        // 동전은 어느 모드에서든 잔고로 — 더블클릭과 같은 자리(옮겨 봐야 할 일이 없다).
        if (TryClaimCoins(inst, sourceGrid)) return;

        var target = InventoryGridUI.FindCounterpart(sourceGrid, inst.Data);
        if (target != null) { QuickMove(inst, sourceGrid, target); return; }

        if (QuickEat(inst, sourceGrid, icon)) return;

        QuickEquip(inst, sourceGrid);
    }

    // ── 그 자리에서 먹는다 ────────────────────────────────────────────────────
    //
    // **먹는 것은 원래 폰이 알아서 한다**(`EatNode`) — 임계 아래로 내려가면 배낭이든 창고든
    // 찾아 먹는다. 그런데 그건 *배가 고파졌을 때*의 일이라, **지금 채워 두고 싶을 때**의 길이
    // 없었다: 싸우러 나가기 전, 원정을 떠나기 전, 임계 바로 위에서 어정거릴 때.
    //
    // **누른 그 뭉치를 먹는다** — `Feeding.ConsumeFrom`은 같은 종류를 아무거나 빼므로 여기서는
    // 안 쓴다(`QuickEquip`이 인스턴스 경로로 가는 것과 **같은 이유**: 누른 것과 없어지는 것이
    // 달라 보이면 안 된다).
    //
    // **즉시 먹는다.** `EatTime`(2~4초)을 기다리게 하려면 BT에 상태를 하나 더 만들어야 하는데,
    // 인벤토리에서 하는 일은 전부 즉시다(착용도, 동전 회수도) — 여기만 시간이 흐르면 그것도
    // 따로 외울 규칙이 된다.
    //
    // **답은 그 아이콘 자리에서 돌려준다** — 먹으면 줄어들며 사라지는 잔상, 못 먹으면 흔들림
    // (`DraggableItem.FlashUsed`/`FlashRefused`). 폰 머리 위에 띄워 봤더니
    // (`GradeUpNotificationUI`) **손은 창을 보고 있는데 답은 창 밖에서 나서**, 내가 조작한
    // 결과가 아니라 어딘가에서 일어난 일처럼 보였다. 배낭에서 하는 일의 답은 배낭에 있어야 한다.
    //
    // 돌려주는 값은 *"이건 먹는 일이었다"*지 *"먹었다"*가 아니다 — 거절한 경우에도 참을
    // 돌려줘야 아래 `QuickEquip`으로 흘러가지 않는다(음식은 장비가 아니라 어차피 아무 일도
    // 안 일어나지만, 이유를 남기고 끝내는 편이 낫다).
    bool QuickEat(ItemInstance inst, InventoryGridUI sourceGrid, DraggableItem icon)
    {
        if (inst.Data is not FoodData food) return false;

        // **배낭의 밥만 먹는다** — 창고·작업대의 밥을 눌러 봐야 **누가 먹는지 말할 수 없다**
        // (창고의 검을 눌러 입지 않는 것과 같은 규칙). 폰이 먹으러 가는 것은 `EatNode` 몫이다.
        var src   = sourceGrid.Inventory;
        var eater = src.GetComponent<UnitBrain>();
        var bb    = eater != null ? eater.Blackboard : null;
        if (bb == null) return false;

        // **갇힌 사람은 제 손으로 못 먹는다** — 그게 간수를 두는 이유이고(`FeedJob`), 여기서
        // 뚫리면 포로 소지품 창을 열어 공짜로 먹이는 길이 된다. 붙잡아 두는 값이 사라진다.
        // **죽은 사람·업힌 사람·남의 폰은 못 먹는다.** 누가 이 축을 도는지는 `Needs.Simulates`
        // 하나가 답한다 — 여기서 따로 세면 어긋난다. 특히 **시체가 그 구멍이었다**:
        // `BecomeCorpse`는 `UnitBrain`을 **끄기만** 하므로 `GetComponent`로는 산 사람과
        // 구별이 안 되고, 시체 배낭에서 밥을 우클릭하면 **그 밥이 사라졌다**(시체는 배가
        // 안 고프니 아무 일도 안 일어난 채로).
        // **축까지 물어야 한다** — 몸이 셋이 되면서 `Simulates(brain)`은 *무엇이든 요구하는
        // 폰인가*가 됐다. 골렘은 에너지를 요구하지 허기를 요구하지 않으므로, 축을 안 물으면
        // **돌덩이에게 빵을 먹일 수 있다**(먹여도 안 차니 밥만 사라진다).
        if (!Feeding.Simulates(eater))
            return Refuse(icon, $"{eater.UnitName}은(는) 지금 먹을 수 없다");

        // **원정 나간 폰은 못 먹는다.** 그 폰들은 GameObject가 꺼져 있어(`ExpeditionParty`)
        // **허기가 아예 안 돈다** — 채워 봐야 줄지 않으니 밥만 사라진다. 띄울 자리도 없다
        // (팝업은 폰 머리 위를 따라가는데 그 폰은 지도 밖의 옛 자리에 서 있다).
        if (!eater.gameObject.activeInHierarchy)
        {
            Debug.Log($"[Eat] {eater.UnitName}은(는) 원정 중이라 여기서 못 먹는다(허기가 안 돈다).");
            return true;
        }

        if (bb.IsPrisoner)   return Refuse(icon, $"{eater.UnitName}은(는) 갇혀 있어 제 손으로 못 먹는다");
        // 기절한 폰은 남도 자기도 못 먹인다(치료·설득과 같은 규칙).
        if (bb.IsKnockedOut) return Refuse(icon, $"{eater.UnitName}은(는) 쓰러져 있어 못 먹는다");

        // **배가 찼으면 안 먹는다** — 한 끼가 통째로 사라진다. 그 아래로는 막지 않는다:
        // 넘치는 만큼 버려지는 것은 손으로 시킨 사람의 몫이고(`Feeding.Better`는 *자동*이
        // 낭비하지 않게 있는 규칙이다), 싸우기 전에 채워 두는 것이 그 값을 치를 만한 일이다.
        //
        // **딱 100일 때만 막으면 그 순간은 한 프레임뿐이다** — 허기는 매 프레임 줄어서
        // (`UpdateNeeds`) 먹자마자 99.99가 된다. 화면에는 `100/100`이라 적혀 있는데 계속
        // 먹히는 것이 그래서였다. 여백을 둔다.
        float room = Needs.Room(bb.Hunger);
        if (room <= FullMargin)
            return Refuse(icon, $"{eater.UnitName}은(는) 배가 불러 못 먹는다 (허기 {bb.Hunger:F0}/100)");

        // **잠근 뭉치도 먹는다** — 잠금은 *자동 판단*이 조용히 축내는 것을 막으려고 있고
        // (`Feeding.BestIn`의 규칙), 이건 그 뭉치를 콕 집어 누른 사람의 지시다.
        float before = bb.Hunger;

        // **개수를 건드리기 전에 잔상을 띄운다.** 개수가 줄면 `Inventory.OnChanged`가 돌고
        // `InventoryGridUI.Refresh`가 아이콘을 다시 만드는데, 그때 **옛 아이콘을 그 자리에서
        // 즉시 부모에서 뗀다**(`child.SetParent(null)` — 새 아이콘과 겹치지 않게). 그러면
        // `GetComponentInParent<Canvas>()`가 **null**이 되어 잔상이 설 자리를 못 찾는다.
        //
        // `Destroy`가 프레임 끝에 도는 것만 믿고 뒤에 뒀더니 정확히 그래서 **아무것도 안
        // 보였다** — 파괴가 늦는 것과 부모에서 떨어지는 것은 다른 시각이다.
        icon?.FlashUsed();

        inst.Quantity--;
        if (inst.Quantity <= 0) src.RemoveInstance(inst);
        else                    src.NotifyChanged();
        if (_selectedItem == inst && inst.Quantity <= 0) _selectedItem = null;

        Feeding.Eat(eater, food);

        // **수치는 콘솔에만 적는다.** 화면에서 답하는 것은 잔상이고(*"한 개 썼다"*), 얼마나
        // 찼는지는 살펴보기 창의 허기 바가 실시간으로 말한다 — 같은 것을 세 군데서 말할
        // 이유가 없다. **버린 양**은 손으로 먹일 때 유일하게 손해 보는 자리라 남긴다.
        float gain   = bb.Hunger - before;
        float wasted = Mathf.Max(0f, food.NutritionValue - room);
        Debug.Log($"[Eat] {eater.UnitName} — {food.ItemName} (허기 +{gain:F0}" +
                  (wasted > 0f ? $", {wasted:F0} 버림)" : ")"));

        RefreshActionBar();
        return true;
    }

    // **딱 100일 때만 막으면 그 순간이 한 프레임뿐이라** 사실상 안 막힌다(위 주석).
    const float FullMargin = 1f;

    // 못 먹는 이유 — **아이콘을 흔들고** 콘솔에 적는다.
    //
    // 흔드는 것은 이 창에서 이미 *"안 된다"*를 뜻한다(`DraggableItem.EndCarry`가 못 놓은
    // 물건을 흔든다) — 새 낱말을 만들지 않는다. 알림을 띄우기엔 너무 잦고, 아무 일도 안
    // 일어나면 **눌리기는 한 건지**를 알 수가 없다.
    //
    // 돌려주는 값은 *"이건 먹는 일이었다"*지 *"먹었다"*가 아니다(위 주석).
    static bool Refuse(DraggableItem icon, string why)
    {
        icon?.FlashRefused();
        Debug.Log($"[Eat] {why}");
        return true;
    }

    // 뭉치째 반대편으로. **들어간 만큼만 뺀다** — 자리가 모자라면 나머지는 그대로 남는다
    // (전부 못 가면 아무것도 안 보내는 것보다, 갈 수 있는 만큼 가는 편이 낫다).
    void QuickMove(ItemInstance inst, InventoryGridUI from, InventoryGridUI to)
    {
        var src = from.Inventory;
        var dst = to.Inventory;

        int moved = dst.TryAdd(inst.Data, inst.Quantity, inst.Quality);
        if (moved <= 0) return;

        inst.Quantity -= moved;
        if (inst.Quantity <= 0) src.RemoveInstance(inst);
        else                    src.NotifyChanged();

        if (_selectedItem == inst && inst.Quantity <= 0) _selectedItem = null;
        RefreshActionBar();
    }

    // 장비면 그 자리에서 입는다. **창고의 검을 눌러 입지는 않는다** — 어느 폰이 입는지
    // 말할 수 없다(배낭은 그 폰의 것이라 답이 하나다).
    //
    // ── 누른 **그것**을 입고, 벗은 것은 **그 자리**에 놓는다 ─────────────────
    //
    // `Equipment.Equip*`(배낭 경로)를 쓰면 안 된다 — 그쪽은 `ItemData`로 찾으므로 **배낭에서
    // 처음 걸리는 같은 종류**를 집는다. 일반 천갑옷을 눌렀는데 위 칸의 희귀 천갑옷이 입혀졌다.
    // 등급이 다르면 다른 물건이다(값도 성능도 다르다) — 누른 것을 입어야 한다.
    //
    // 그래서 **인스턴스 경로**(`Wear*`)로 간다. 대신 배낭에서 빼는 것과 벗은 것을 놓는 것을
    // 여기서 직접 해야 한다:
    //   · 누른 것을 배낭에서 빼고(칸이 빈다)
    //   · 차고 있던 것을 `TakeOff`로 **배낭을 안 거치고** 빼내서
    //   · **빈 그 칸에** 놓는다
    //
    // 벗은 것이 첫 빈 칸으로 튀면 정리해 둔 배낭이 누를 때마다 흐트러진다 — 갈아입는 것은
    // **자리를 맞바꾸는 일**이지 새 물건이 생기는 일이 아니다.
    void QuickEquip(ItemInstance inst, InventoryGridUI sourceGrid)
    {
        var src   = sourceGrid.Inventory;
        var equip = src.GetComponent<UnitBrain>()?.Equipment;
        if (equip == null) return;

        // 어느 칸에 차는 물건인가 — 벗을 때도 채울 때도 같은 칸이다.
        ArmorSlot? armorSlot = inst.Data is ArmorData a ? a.Slot : null;
        bool offHand  = inst.Data is WeaponData sh && sh.Type == WeaponType.Shield;
        bool mainHand = inst.Data is WeaponData && !offHand;
        if (armorSlot == null && !offHand && !mainHand) return;   // 장비가 아니다

        ItemInstance TakeOffCurrent() =>
            armorSlot.HasValue ? equip.TakeOff(armorSlot.Value)
            : offHand          ? equip.TakeOffOffHand()
                               : equip.TakeOffMainHand();

        bool Wear(ItemInstance it) =>
            armorSlot.HasValue ? equip.WearArmor(it)
            : offHand          ? equip.WearOffHand(it)
                               : equip.WearMainHand(it);

        // 누른 칸 — 벗은 것이 돌아갈 자리.
        int  x = inst.GridX, y = inst.GridY;
        bool rotated = inst.Rotated;

        // 뭉치에서 하나만 입는다(장비는 한 칸에 하나지만, 그렇다고 나머지를 잃을 수는 없다).
        var  toWear    = inst;
        bool wholeCell = inst.Quantity <= 1;

        if (wholeCell) src.RemoveInstance(inst);
        else
        {
            inst.Quantity--;
            toWear = new ItemInstance(inst.Data, 1, x, y, rotated, inst.Quality) { Locked = inst.Locked };
            src.NotifyChanged();
        }

        void PutBackClicked()
        {
            if (wholeCell) src.TryAddAt(toWear.Data, 1, x, y, rotated, toWear.Quality, toWear.Locked);
            else         { inst.Quantity++; src.NotifyChanged(); }
        }

        // 차고 있던 것을 먼저 빼낸다 — `Wear*`에 맡기면 그쪽이 배낭 첫 빈 칸으로 돌려보낸다.
        var old = TakeOffCurrent();

        // 못 입었다(양손 무기가 오프핸드를 잠갔다든지) — 둘 다 원래대로.
        if (!Wear(toWear))
        {
            if (old != null) Wear(old);
            PutBackClicked();
            return;
        }

        // **벗은 것은 방금 빈 그 칸으로.** 안 되면 배낭 아무 데나.
        if (old?.Data != null)
        {
            bool placed = src.TryAddAt(old.Data, 1, x, y, rotated, old.Quality, old.Locked)
                       || src.TryAdd(old.Data, 1, old.Quality) > 0;

            if (!placed)
            {
                // **발밑에 안 떨군다 — 아예 안 입는다.**
                //
                // 이 프로젝트의 증발 방지 관례는 "떨구면 눈에 보이고 운반이 알아서 창고로
                // 옮긴다"였다. 그 전제가 사라졌다 — **바닥 물건은 이제 저절로 안 실려 간다**
                // (체크한 것만 나른다, 2026-08-13). 떨구면 배낭이 꽉 차서 생긴 일이 **치우는
                // 일**로 바뀐다.
                //
                // 거절이 맞는 자리이기도 하다: 손으로 누른 조작이고, 자리를 비우고 다시
                // 누르면 그만이다. **아무것도 안 잃는다** — 셋 다 원래대로 돌린다.
                TakeOffCurrent();      // 방금 입은 것을 도로 벗고
                Wear(old);             // 원래 것을 다시 채우고
                PutBackClicked();      // 누른 것을 제자리에

                EventNotificationUI.Notify("착용 실패",
                    $"{old.Data.ItemName}을(를) 넣을 자리가 없습니다 — 배낭을 비우고 다시 누르세요.");
                return;
            }
        }

        if (_selectedItem == inst) _selectedItem = null;
        RefreshActionBar();
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

    // **밖에서 판매 줄을 다시 그리게 하는 문.** 상인 주머니가 줄면 같은 물건이 "1,200 G"에서
    // "상인의 돈이 모자란다"로 바뀌어야 하는데, 그 변화는 **배낭이 아니라 상점 쪽에서** 일어난다.
    // 드래그 판매·더블클릭 판매는 이 창을 안 거치므로(`MerchantUI.TrySellItem` 직행) 여기가
    // 없으면 문구가 한 박자 늦는다 — 다음에 뭘 누를 때까지 옛 값이 남는다.
    public void RefreshSellState() => RefreshActionBar();

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

                // **왜 안 팔리는지 적는다.** 값은 멀쩡히 붙어 있는데 눌러도 아무 일이 없으면
                // 그건 고장으로 보인다. 막히는 이유가 셋이고 **할 일이 다 다르다**:
                //   · 값이 0 → 그 물건은 원래 안 팔린다
                //   · 주머니가 빔 → 내일 다시 오거나, 그에게서 뭘 사 주면 된다
                //   · 좌판이 참 → 다른 마을로 가거나, 내가 쌓아 둔 것이 빠지길 기다린다
                bool broke = price > 0 && _shop != null && !_shop.HasInfiniteGold && _shop.Gold < price;
                bool full  = price > 0 && !broke && _shop != null &&
                             !_shop.HasRoomFor(_selectedItem.Data, 1, _selectedItem.Quality);

                string tail = price <= 0 ? "<color=#888>판매 불가</color>"
                            : broke      ? $"<color=#C88>상인의 돈이 모자란다 ({_shop.Gold:N0} G)</color>"
                            : full       ? "<color=#C88>좌판에 자리가 없다</color>"
                            :              $"<color=#FFD94D>{price} G</color>";

                actionItemText.text =
                    $"<color=#{qc}>[{_selectedItem.Quality.DisplayName()}]</color> {_selectedItem.Data?.ItemName}" +
                    $"  →  {tail}";
                if (actionButton) actionButton.interactable = price > 0 && !broke && !full;
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
