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

            // **잠겨도 눌러진다.** 무엇이 열리는지 봐야 그 연구를 할지 정하는데, 버튼을 죽여 두면
            // 이름만 보고 짐작해야 한다(제작 레시피에 08-01에 한 것과 같은 처리).
            // 실제 착수만 막는다 — 디테일 창의 [연구] 버튼이 잠금을 다시 검사한다.
            btn.interactable = !completed;
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
