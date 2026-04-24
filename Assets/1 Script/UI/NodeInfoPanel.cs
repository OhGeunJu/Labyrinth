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
    [SerializeField] TextMeshProUGUI inventoryText;
    [SerializeField] Button transferButton;

    ResourceNode _current;
    readonly List<GameObject> _entries = new();

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
    }

    void Close()
    {
        _current = null;
        panel.SetActive(false);
    }

    void OnNodeSelected(ResourceNode node)
    {
        _current = node;
        panel.SetActive(node != null);
        RefreshList();
    }

    void Update()
    {
        if (_current == null) { panel.SetActive(false); return; }

        if (titleText != null)
            titleText.text = _current.Data != null
                ? _current.Data.ResourceType.ToString()
                : "자원 노드";

        if (gaugeText != null)
            gaugeText.text = $"진행도: {_current.GaugeProgress * 100f:F0}%";

        if (inventoryText != null)
            inventoryText.text = BuildInventoryText(_current.Inventory);
    }

    string BuildInventoryText(Inventory inv)
    {
        if (inv == null || inv.Slots.Count == 0)
            return "보관 중: 없음";

        var sb = new System.Text.StringBuilder("보관 중:\n");
        foreach (var slot in inv.Slots)
            sb.AppendLine($"  {slot.Item.ItemName} x{slot.Quantity}");
        return sb.ToString().TrimEnd();
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
        Debug.Log($"[NodeInfoPanel] {brain.name}에게 노드 아이템 이전 완료");
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
}
