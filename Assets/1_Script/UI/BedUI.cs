using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class BedUI : MonoBehaviour
{
    [SerializeField] SelectionManager   selectionManager;
    [SerializeField] GameObject         panel;
    [SerializeField] TextMeshProUGUI    titleText;
    [SerializeField] TextMeshProUGUI    assignedUnitText;
    [SerializeField] Button             assignButton;
    [SerializeField] Button             unassignButton;

    Bed _current;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);

        Bed.OnSelected                   += Open;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;

        assignButton?.onClick.AddListener(AssignSelected);
        unassignButton?.onClick.AddListener(Unassign);
    }

    void OnDestroy()
    {
        Bed.OnSelected                   -= Open;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();

        RefreshAssignedLabel();
    }

    void Open(Bed bed)
    {
        _current = bed;
        panel.SetActive(true);
        if (titleText != null) titleText.text = bed.BedName;
        RefreshAssignedLabel();
    }

    public void Close()
    {
        _current = null;
        if (panel != null) panel.SetActive(false);
    }

    void RefreshAssignedLabel()
    {
        if (_current == null || assignedUnitText == null) return;
        var unit = _current.AssignedUnit;
        assignedUnitText.text = unit != null ? unit.gameObject.name : "미배정";
    }

    void AssignSelected()
    {
        if (_current == null || selectionManager == null) return;
        if (selectionManager.Selected.Count == 0) return;

        var brain = selectionManager.Selected[0].GetComponent<UnitBrain>();
        if (brain == null) return;

        _current.Assign(brain);
        RefreshAssignedLabel();
    }

    void Unassign()
    {
        _current?.Unassign();
        RefreshAssignedLabel();
    }
}
