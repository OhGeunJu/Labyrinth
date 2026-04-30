using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class PriorityPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] SelectionManager selectionManager;
    [SerializeField] List<PriorityRow> rows;

    UnitPriorities _current;

    void Awake()
    {
        foreach (var row in rows)
        {
            var captured = row.category;
            row.minusButton?.onClick.AddListener(() => _current?.Decrease(captured));
            row.plusButton?.onClick.AddListener(() => _current?.Increase(captured));
        }
    }

    void Start()
    {
        panel.SetActive(false);
        selectionManager.OnSelectionChanged += OnSelectionChanged;
        BuildModeManager.OnActiveChanged    += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        selectionManager.OnSelectionChanged -= OnSelectionChanged;
        BuildModeManager.OnActiveChanged    -= OnBuildModeChanged;
        SetTarget(null);
    }

    void OnBuildModeChanged(bool active) { if (active) panel.SetActive(false); }

    void Update()
    {
        if (Keyboard.current.pKey.wasPressedThisFrame)
            panel.SetActive(!panel.activeSelf);
    }

    void OnSelectionChanged(PawnMover pawn, bool selected)
    {
        var selectedList = selectionManager.Selected;
        if (selectedList.Count == 0) { SetTarget(null); return; }

        var brain = selectedList[0].GetComponent<UnitBrain>();
        if (brain == null || brain.Data is MinionData) { SetTarget(null); return; }

        SetTarget(brain.GetComponent<UnitPriorities>());
    }

    void SetTarget(UnitPriorities priorities)
    {
        if (_current != null) _current.OnChanged -= RefreshAll;
        _current = priorities;
        if (_current != null) _current.OnChanged += RefreshAll;
        RefreshAll();
    }

    void RefreshAll()
    {
        foreach (var row in rows)
        {
            int val = _current != null ? _current.Get(row.category) : 3;
            row.valueText.text  = val == 0 ? "OFF" : val.ToString();
            row.valueText.color = val == 0 ? Color.red : Color.white;

            bool interactable = _current != null;
            if (row.minusButton != null) row.minusButton.interactable = interactable;
            if (row.plusButton  != null) row.plusButton.interactable  = interactable;
        }
    }
}

[Serializable]
public class PriorityRow
{
    public JobCategory category;
    public Button minusButton;
    public Button plusButton;
    public TextMeshProUGUI valueText;
}
