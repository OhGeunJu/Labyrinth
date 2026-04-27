using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class PawnInventoryPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] Transform slotContainer;
    [SerializeField] Transform equipmentContainer;
    [SerializeField] GameObject slotPrefab;
    [SerializeField] Button closeButton;
    [SerializeField] SelectionManager selectionManager;

    UnitBrain _current;
    Corpse    _currentCorpse;
    readonly List<GameObject> _slots     = new();
    readonly List<GameObject> _equipSlots = new();

    void Start()
    {
        panel.SetActive(false);
if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        selectionManager.OnSelectionChanged += OnSelectionChanged;
        SelectionManager.OnCorpseSelected   += OpenForCorpse;
        Equipment.OnEquipChanged            += OnEquipChanged;
    }

    void OnDestroy()
    {
        selectionManager.OnSelectionChanged -= OnSelectionChanged;
        SelectionManager.OnCorpseSelected   -= OpenForCorpse;
        Equipment.OnEquipChanged            -= OnEquipChanged;

        if (_current != null)
            _current.Inventory.OnChanged -= Refresh;
    }

    void Update()
    {
        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            if (_currentCorpse != null) { Close(); return; }

            if (panel.activeSelf) { Close(); return; }

            if (selectionManager.Selected.Count == 0) return;

            var brain = selectionManager.Selected[0].GetComponent<UnitBrain>();
            if (brain == null) return;

            SetUnit(brain);
            panel.SetActive(true);
            Refresh();
        }
    }

    void OnSelectionChanged(PawnMover pawn, bool selected)
    {
        if (selectionManager.Selected.Count == 0)
            Close();
    }

    // ── 유닛 인벤토리 ──────────────────────────────────────────────────
    void SetUnit(UnitBrain brain)
    {
        if (_current != null)
            _current.Inventory.OnChanged -= Refresh;

        _currentCorpse = null;
        _current       = brain;
        _current.Inventory.OnChanged += Refresh;

        if (titleText != null)
            titleText.text = _current.Data != null ? _current.Data.RaceName : _current.name;
    }

    void Refresh()
    {
        ClearSlots();
        if (_current == null) return;

        foreach (var stack in _current.Inventory.Slots)
        {
            var go = Instantiate(slotPrefab, slotContainer);
            _slots.Add(go);
            SetupSlotVisual(go, stack.Item, stack.Quantity);

            var slotUI = go.GetComponent<InventorySlotUI>() ?? go.AddComponent<InventorySlotUI>();
            slotUI.Init(stack.Item, stack.Quantity, _current.Inventory, _current.transform);

            var item = stack.Item;
            if      (item is WeaponData w) slotUI.SetClickHandler(() => TryEquipWeapon(w));
            else if (item is ArmorData  a) slotUI.SetClickHandler(() => TryEquipArmor(a));
        }

        RefreshEquipment();
    }

    void RefreshEquipment()
    {
        foreach (var go in _equipSlots) Destroy(go);
        _equipSlots.Clear();

        if (_current == null || equipmentContainer == null || slotPrefab == null) return;

        var eq = _current.Equipment;
        AddEquipSlot("주손",   eq.MainHand,               () => eq.Unequip());
        AddEquipSlot("보조손", eq.OffHand,                 () => eq.UnequipOffHand());
        AddEquipSlot("머리",   eq.GetArmor(ArmorSlot.Head),  () => eq.UnequipArmor(ArmorSlot.Head));
        AddEquipSlot("몸통",   eq.GetArmor(ArmorSlot.Chest), () => eq.UnequipArmor(ArmorSlot.Chest));
        AddEquipSlot("손",     eq.GetArmor(ArmorSlot.Hands), () => eq.UnequipArmor(ArmorSlot.Hands));
        AddEquipSlot("다리",   eq.GetArmor(ArmorSlot.Legs),  () => eq.UnequipArmor(ArmorSlot.Legs));
    }

    void AddEquipSlot(string label, ItemData item, System.Action onUnequip)
    {
        var go = Instantiate(slotPrefab, equipmentContainer);
        _equipSlots.Add(go);
        SetupSlotVisual(go, item, item != null ? 1 : 0);

        var slotUI = go.GetComponent<EquipmentSlotUI>() ?? go.AddComponent<EquipmentSlotUI>();
        slotUI.Init(label, item, item != null ? onUnequip : null);
    }

    void OnEquipChanged(Equipment eq)
    {
        if (_current == null || eq.gameObject != _current.gameObject) return;
        Refresh();
    }

    void TryEquipWeapon(WeaponData weapon)
    {
        if (_current == null) return;
        var eq = _current.Equipment;
        if (weapon.Type == WeaponType.Shield) eq.EquipOffHand(weapon);
        else                                  eq.Equip(weapon);
    }

    void TryEquipArmor(ArmorData armor) => _current?.Equipment.EquipArmor(armor);

    // ── 시체 루팅 ──────────────────────────────────────────────────────
    void OpenForCorpse(Corpse corpse)
    {
        if (_current != null)
        {
            _current.Inventory.OnChanged -= Refresh;
            _current = null;
        }

        if (_currentCorpse != null)
            _currentCorpse.Inventory.OnChanged -= RefreshCorpse;

        _currentCorpse = corpse;
        _currentCorpse.Inventory.OnChanged += RefreshCorpse;

        if (titleText != null)
            titleText.text = $"{corpse.UnitName} (시체)";

        panel.SetActive(true);
        RefreshCorpse();
    }

    void RefreshCorpse()
    {
        ClearSlots();
        if (_currentCorpse == null) return;

        foreach (var stack in _currentCorpse.Inventory.Slots)
        {
            var go = Instantiate(slotPrefab, slotContainer);
            _slots.Add(go);
            SetupSlotVisual(go, stack.Item, stack.Quantity);

            var slotUI = go.GetComponent<InventorySlotUI>() ?? go.AddComponent<InventorySlotUI>();
            slotUI.Init(stack.Item, stack.Quantity, _currentCorpse.Inventory, _currentCorpse.transform);

            var item = stack.Item;
            var qty  = stack.Quantity;
            slotUI.SetClickHandler(() => TakeItem(item, qty));
        }
    }

    void TakeItem(ItemData item, int qty)
    {
        if (_currentCorpse == null) return;
        if (selectionManager.Selected.Count == 0) return;

        var brain = selectionManager.Selected[0].GetComponent<UnitBrain>();
        if (brain == null) return;

        if (_currentCorpse.Inventory.Consume(item, qty))
            brain.Inventory.Add(item, qty);
    }

    // ── 공통 ──────────────────────────────────────────────────────────
    void Close()
    {
        panel.SetActive(false);

        if (_current != null)
        {
            _current.Inventory.OnChanged -= Refresh;
            _current = null;
        }

        if (_currentCorpse != null)
        {
            _currentCorpse.Inventory.OnChanged -= RefreshCorpse;
            _currentCorpse = null;
        }
    }

    void ClearSlots()
    {
        foreach (var slot in _slots)      Destroy(slot);
        foreach (var slot in _equipSlots) Destroy(slot);
        _slots.Clear();
        _equipSlots.Clear();
    }

    static void SetupSlotVisual(GameObject go, ItemData item, int quantity)
    {
        var icon = go.GetComponentInChildren<Image>();
        var qty  = icon != null ? icon.transform.GetComponentInChildren<TextMeshProUGUI>() : null;

        if (icon != null && item?.Icon != null)
            icon.sprite = item.Icon;

        if (qty != null)
            qty.text = quantity > 1 ? $"x{quantity}" : "";
    }
}
