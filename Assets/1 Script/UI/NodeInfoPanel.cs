using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NodeInfoPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI gaugeText;
    [SerializeField] Button assignButton;
    [SerializeField] Transform assignedList;
    [SerializeField] GameObject pawnEntryPrefab;
    [SerializeField] Button closeButton;
    [SerializeField] SelectionManager selectionManager;
    [SerializeField] Transform slotContainer;
    [SerializeField] GameObject slotPrefab;
    [SerializeField] Button transferButton;

    ResourceNode _current;
    readonly List<GameObject> _entries = new();
    readonly List<GameObject> _slots   = new();

    string _lastTitle;
    float  _lastGauge = -1f;

    void Start()
    {
        SelectionManager.OnNodeSelected += OnNodeSelected;
        panel.SetActive(false);
        assignButton.onClick.AddListener(AssignSelected);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (transferButton != null)
            transferButton.onClick.AddListener(TransferToSelected);
    }

    void OnDestroy()
    {
        SelectionManager.OnNodeSelected -= OnNodeSelected;
        UnsubscribeInventory();
    }

    void Close()
    {
        UnsubscribeInventory();
        _current = null;
        panel.SetActive(false);
    }

    void OnNodeSelected(ResourceNode node)
    {
        UnsubscribeInventory();
        _current = node;

        if (_current != null)
            _current.Inventory.OnChanged += RefreshSlots;

        // 새 노드 선택 → 캐시 초기화
        _lastTitle = null;
        _lastGauge = -1f;

        panel.SetActive(node != null);
        RefreshList();
        RefreshSlots();
    }

    void Update()
    {
        if (_current == null) { panel.SetActive(false); return; }

        if (titleText != null)
        {
            string newTitle = _current.Data != null
                ? _current.Data.ResourceType.ToString()
                : "자원 노드";
            if (newTitle != _lastTitle)
            {
                titleText.text = newTitle;
                _lastTitle = newTitle;
            }
        }

        if (gaugeText != null)
        {
            float gauge = Mathf.Floor(_current.GaugeProgress * 100f);
            if (!Mathf.Approximately(gauge, _lastGauge))
            {
                gaugeText.text = $"진행도: {gauge:F0}%";
                _lastGauge = gauge;
            }
        }
    }

    void RefreshSlots()
    {
        foreach (var s in _slots) Destroy(s);
        _slots.Clear();

        if (_current == null || slotContainer == null || slotPrefab == null) return;

        foreach (var stack in _current.Inventory.Slots)
        {
            var go = Instantiate(slotPrefab, slotContainer);
            _slots.Add(go);

            var icon = go.GetComponentInChildren<Image>();
            var qty  = icon != null ? icon.transform.GetComponentInChildren<TextMeshProUGUI>() : null;

            if (icon != null && stack.Item.Icon != null)
                icon.sprite = stack.Item.Icon;
            if (qty != null)
                qty.text = $"x{stack.Quantity}";

            var slotUI = go.GetComponent<InventorySlotUI>() ?? go.AddComponent<InventorySlotUI>();
            slotUI.Init(stack.Item, stack.Quantity, _current.Inventory, _current.transform);
        }
    }

    void TransferToSelected()
    {
        if (_current == null || selectionManager == null) return;
        if (selectionManager.Selected.Count == 0) return;

        var brain = selectionManager.Selected[0].GetComponent<UnitBrain>();
        if (brain == null) return;

        var nodeInv = _current.Inventory;
        var pawnInv = brain.Inventory;

        foreach (var slot in nodeInv.Slots)
            pawnInv.Add(slot.Item, slot.Quantity);

        nodeInv.Clear();
    }

    void AssignSelected()
    {
        if (_current == null || selectionManager == null) return;

        foreach (var pawn in selectionManager.Selected)
        {
            var brain = pawn.GetComponent<UnitBrain>();
            if (brain != null)
                _current.Assign(brain);
        }
        RefreshList();
    }

    void RefreshList()
    {
        foreach (var e in _entries) Destroy(e);
        _entries.Clear();

        if (_current == null) return;

        foreach (var brain in _current.AssignedPawns)
        {
            var entry = Instantiate(pawnEntryPrefab, assignedList);
            var entryUI = entry.GetComponent<PawnEntryUI>();
            entryUI.Setup(brain, _current, RefreshList);
            _entries.Add(entry);
        }
    }

    void UnsubscribeInventory()
    {
        if (_current != null)
            _current.Inventory.OnChanged -= RefreshSlots;
    }
}
