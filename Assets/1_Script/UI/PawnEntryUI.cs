using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PawnEntryUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] Button unassignButton;

    public void Setup(UnitBrain brain, ResourceNode node, Action onChanged)
    {
        if (nameText != null)
            nameText.text = brain.Data != null ? brain.Data.RaceName : brain.name;

        unassignButton.onClick.AddListener(() =>
        {
            node.Unassign(brain);
            onChanged?.Invoke();
        });
    }
}
