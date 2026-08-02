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

    public void Setup(ResearchData data, ResearchData activeResearch = null)
    {
        _data = data;
        Refresh(activeResearch);
    }

    // `activeResearch` = 지금 연구대가 맡은 연구. **디테일 창의 게이지가 고른 연구에만 뜨게 되면서
    // 목록이 유일하게 "무엇이 도는 중인가"를 알려 주는 곳이 됐다** — 표식이 없으면 그 정보가
    // 화면 어디에도 남지 않는다.
    public void Refresh(ResearchData activeResearch)
    {
        var data = _data;
        if (data == null) return;

        var rm = ResearchManager.Instance;
        bool completed   = rm != null && rm.IsCompleted(data);
        bool canResearch = rm == null  || rm.CanResearch(data);
        bool researching = activeResearch != null && activeResearch == data;

        string label = data.ResearchName;
        Color  color;

        if (completed)
        {
            label = $"[완료] {label}";
            color = new Color(0.4f, 0.8f, 0.4f, 1f);
        }
        else if (researching)
        {
            // 진행률은 안 적는다 — 목록은 다시 그릴 때만 갱신되므로 숫자를 적으면 멈춘 채로 남는다.
            // 실시간 수치는 게이지가 맡는다.
            label = $"[연구 중] {label}";
            color = new Color(0.55f, 0.8f, 1f, 1f);
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
