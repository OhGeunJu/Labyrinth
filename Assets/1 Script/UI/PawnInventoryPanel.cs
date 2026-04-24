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
    [SerializeField] GameObject slotPrefab;
    [SerializeField] Button closeButton;
    [SerializeField] SelectionManager selectionManager;

    UnitBrain _current;
    readonly List<GameObject> _slots = new();

    void Start()
    {
        panel.SetActive(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(() => panel.SetActive(false));

        selectionManager.OnSelectionChanged += OnSelectionChanged;
    }

    void OnDestroy()
    {
        selectionManager.OnSelectionChanged -= OnSelectionChanged;
    }

    void Update()
    {
        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            if (panel.activeSelf)
            {
                panel.SetActive(false);
                return;
            }

            if (selectionManager.Selected.Count == 0) return;

            var brain = selectionManager.Selected[0].GetComponent<UnitBrain>();
            if (brain == null) return;

            if (_current != null)
                _current.Inventory.OnChanged -= Refresh;

            _current = brain;
            _current.Inventory.OnChanged += Refresh;

            panel.SetActive(true);
            Refresh();
        }
    }

    void OnSelectionChanged(PawnMover pawn, bool selected)
    {
        if (selectionManager.Selected.Count == 0)
        {
            _current = null;
            panel.SetActive(false);
        }
    }

    void Refresh()
    {
        if (_current == null) return;

        if (titleText != null)
            titleText.text = _current.Data != null ? _current.Data.RaceName : _current.name;

        foreach (var slot in _slots) Destroy(slot);
        _slots.Clear();

        var inv = _current.Inventory;
        foreach (var stack in inv.Slots)
        {
            var go = Instantiate(slotPrefab, slotContainer);
            _slots.Add(go);

            var icon = go.GetComponentInChildren<Image>();
            var qty = icon != null
                ? icon.transform.GetComponentInChildren<TextMeshProUGUI>()
                : null;

            if (icon != null && stack.Item.Icon != null)
                icon.sprite = stack.Item.Icon;

            if (qty != null)
                qty.text = $"x{stack.Quantity}";

            // 슬롯에 아이템 정보 주입 (우클릭 드롭용)
            var slotUI = go.GetComponent<InventorySlotUI>() ?? go.AddComponent<InventorySlotUI>();
            slotUI.Init(stack.Item, stack.Quantity, inv, _current.transform);
        }
    }
}
