using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// 시체를 뒤지는 창. **통을 여는 창이기도 하다** — 아래 `ShowContainer`.
public class CorpseInventoryUI : MonoBehaviour
{
    public static CorpseInventoryUI Instance { get; private set; }

    [SerializeField] GameObject         panel;
    [SerializeField] RectTransform      gridContainer;
    [SerializeField] TextMeshProUGUI    titleText;
    [SerializeField] UnityEngine.UI.Button closeButton;
    [SerializeField] EquipSlotUI        mainHandSlot;
    [SerializeField] EquipSlotUI        offHandSlot;
    [SerializeField] EquipSlotUI        headSlot;
    [SerializeField] EquipSlotUI        chestSlot;
    [SerializeField] EquipSlotUI        handsSlot;
    [SerializeField] EquipSlotUI        legsSlot;
    [SerializeField] InventoryUI playerInventoryPanel;

    InventoryGridUI _gridUI;
    bool            _didForceOpenInventory;

    void Awake()
    {
        Instance = this;
        if (gridContainer != null)
        {
            _gridUI = gridContainer.GetComponent<InventoryGridUI>();
            if (_gridUI == null) _gridUI = gridContainer.gameObject.AddComponent<InventoryGridUI>();
        }
        if (panel != null) panel.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        SelectionManager.OnCorpseSelected    += OnCorpseSelected;
        BuildModeManager.OnActiveChanged     += OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Start()
    {
        if (playerInventoryPanel == null)
            playerInventoryPanel = FindFirstObjectByType<InventoryUI>();
    }

    void OnDestroy()
    {
        SelectionManager.OnCorpseSelected -= OnCorpseSelected;
        BuildModeManager.OnActiveChanged  -= OnBuildModeChanged;
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

    void Update()
    {
        if (panel && panel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void OnCorpseSelected(Corpse corpse)
        => Show(corpse.Inventory, $"{corpse.UnitName}의 시체",
                corpse.GetComponent<Equipment>());

    // **장비 열이 없는 통을 여는 길**(도축대 산출물 등).
    //
    // 창을 새로 안 짓는다 — 하는 일이 글자 그대로 같다: 저쪽 통을 띄우고, 내 배낭을 열고,
    // 사이에서 물건을 옮긴다. 다른 것은 **장비 열이 있느냐**뿐이라 그 줄만 껐다 켠다
    // (`InventoryGridUI`를 새 패널에 또 배선하면 우클릭 보내기·드래그 규칙이 두 벌이 된다).
    public void ShowContainer(Inventory inv, string title) => Show(inv, title, null);

    void Show(Inventory inv, string title, Equipment equip)
    {
        if (_gridUI == null || inv == null) return;

        _gridUI.Bind(inv);
        BindEquipSlots(equip);
        SetEquipColumnVisible(equip != null);

        if (titleText) titleText.text = title;
        WindowRaiser.Open(panel);

        _didForceOpenInventory = playerInventoryPanel != null && !playerInventoryPanel.IsPanelOpen;
        playerInventoryPanel?.ForceOpen();
        playerInventoryPanel?.SetLootMode(inv);
    }

    void SetEquipColumnVisible(bool on)
    {
        foreach (var slot in new[] { mainHandSlot, offHandSlot, headSlot,
                                     chestSlot, handsSlot, legsSlot })
            if (slot != null) slot.gameObject.SetActive(on);
    }

    public void Close()
    {
        _gridUI?.Bind(null);
        BindEquipSlots(null);
        SetEquipColumnVisible(true);   // 다음에 시체를 열 때 장비 열이 사라져 있으면 안 된다
        WindowRaiser.Close(panel);
        playerInventoryPanel?.ClearActionMode();
        if (_didForceOpenInventory) playerInventoryPanel?.ForceClose();
        _didForceOpenInventory = false;
    }
}
