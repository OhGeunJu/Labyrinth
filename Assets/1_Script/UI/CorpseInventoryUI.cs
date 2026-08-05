using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class CorpseInventoryUI : MonoBehaviour
{
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
    {
        if (_gridUI == null) return;

        _gridUI.Bind(corpse.Inventory);
        BindEquipSlots(corpse.GetComponent<Equipment>());
        if (titleText) titleText.text = $"{corpse.UnitName}의 시체";
        if (panel) panel.SetActive(true);

        _didForceOpenInventory = playerInventoryPanel != null && !playerInventoryPanel.IsPanelOpen;
        playerInventoryPanel?.ForceOpen();
        playerInventoryPanel?.SetLootMode(corpse.Inventory);
    }

    public void Close()
    {
        _gridUI?.Bind(null);
        BindEquipSlots(null);
        if (panel) panel.SetActive(false);
        playerInventoryPanel?.ClearActionMode();
        if (_didForceOpenInventory) playerInventoryPanel?.ForceClose();
        _didForceOpenInventory = false;
    }
}
