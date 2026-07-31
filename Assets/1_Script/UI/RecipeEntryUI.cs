using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 레시피 목록 한 항목. 이름 텍스트 표시.
// 재료 부족 시 빨간색, 충분하면 흰색.
[RequireComponent(typeof(LayoutElement))]
public class RecipeEntryUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] float           preferredHeight = 40f;

    public event Action<RecipeData> OnSelected;

    RecipeData _recipe;

    void Awake()
    {
        var le = GetComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.minHeight       = preferredHeight;
    }

    // 잠긴 레시피도 **누를 수 있다.** 무엇을 만들 수 있게 되는지 미리 봐야
    // 그 연구를 할지 정한다 — 눌러지지도 않으면 이름만 보고 짐작해야 한다.
    // 잠김은 흐린 글씨와 `[잠김]` 표시로 알리고, 실제 제작만 디테일 쪽에서 막는다.
    public void Setup(RecipeData recipe, bool unlocked = true)
    {
        _recipe = recipe;

        string label = recipe.RecipeName;
        if (!unlocked) label = $"[잠김] {label}";
        if (nameText != null) nameText.text = label;

        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.interactable = true;                     // 잠겨도 눌러서 볼 수 있게
            btn.onClick.AddListener(() => OnSelected?.Invoke(_recipe));
        }

        if (unlocked) Refresh();
        else if (nameText != null) nameText.color = new Color(0.45f, 0.45f, 0.48f, 1f);
    }

    public void Refresh()
    {
        if (nameText == null || _recipe == null) return;
        bool canCraft = true;
        foreach (var ing in _recipe.Ingredients)
            if (Storage.GetTotalByItem(ing.Item) < ing.Amount)
            { canCraft = false; break; }

        nameText.color = canCraft ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
    }
}
