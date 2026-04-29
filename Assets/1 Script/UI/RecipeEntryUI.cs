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

    public void Setup(RecipeData recipe)
    {
        _recipe = recipe;

        if (nameText != null) nameText.text = recipe.RecipeName;

        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnSelected?.Invoke(_recipe));
        }

        Refresh();
    }

    public void Refresh()
    {
        if (nameText == null || _recipe == null) return;
        bool canCraft = true;
        foreach (var ing in _recipe.Ingredients)
            if (StorageBuilding.GetTotalByItem(ing.Item) < ing.Amount)
            { canCraft = false; break; }

        nameText.color = canCraft ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
    }
}
