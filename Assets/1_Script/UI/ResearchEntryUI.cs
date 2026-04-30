using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(LayoutElement))]
public class ResearchEntryUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] float           preferredHeight = 40f;

    public event Action<ResearchData> OnSelected;

    ResearchData _data;

    void Awake()
    {
        var le = GetComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.minHeight       = preferredHeight;
    }

    public void Setup(ResearchData data)
    {
        _data = data;

        var rm = ResearchManager.Instance;
        bool completed   = rm != null && rm.IsCompleted(data);
        bool canResearch = rm == null  || rm.CanResearch(data);

        string label = data.ResearchName;
        Color  color;

        if (completed)
        {
            label = $"[완료] {label}";
            color = new Color(0.4f, 0.8f, 0.4f, 1f);
        }
        else if (!canResearch)
        {
            label = $"[잠김] {label}";
            color = new Color(0.4f, 0.4f, 0.4f, 1f);
        }
        else
        {
            color = StorageHasIngredients(data) ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
        }

        if (nameText != null) { nameText.text = label; nameText.color = color; }

        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.interactable = !completed && canResearch;
            if (btn.interactable) btn.onClick.AddListener(() => OnSelected?.Invoke(_data));
        }
    }

    static bool StorageHasIngredients(ResearchData data)
    {
        foreach (var ing in data.Ingredients)
            if (Storage.GetTotalByItem(ing.Item) < ing.Amount) return false;
        return true;
    }
}
