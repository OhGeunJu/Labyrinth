using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class CraftingTableUI : MonoBehaviour
{
    [Header("씬 참조")]
    [SerializeField] GameObject    panel;
    [SerializeField] Transform     recipeContainer;  // ScrollView의 Content
    [SerializeField] RecipeEntryUI recipeEntryPrefab;
    [SerializeField] TextMeshProUGUI tableTitleText;

    [Header("디테일 패널")]
    [SerializeField] GameObject      detailPanel;
    [SerializeField] Image           resultIcon;
    [SerializeField] TextMeshProUGUI resultNameText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] TextMeshProUGUI ingredientsText;
    [SerializeField] TextMeshProUGUI craftTimeText;
    [SerializeField] Button          craftButton;

    CraftingTable _currentTable;
    RecipeData    _selectedRecipe;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(false);
        CraftingTable.OnSelected         += Open;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        CraftingTable.OnSelected         -= Open;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Update()
    {
        if (panel != null && panel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void Open(CraftingTable table)
    {
        _currentTable   = table;
        _selectedRecipe = null;
        if (panel != null) panel.SetActive(true);
        if (detailPanel != null) detailPanel.SetActive(false);
        if (tableTitleText != null) tableTitleText.text = table.TableType.DisplayName();
        RebuildList();
    }

    public void Close()
    {
        _currentTable = null;
        if (panel != null) panel.SetActive(false);
    }

    void RebuildList()
    {
        if (recipeContainer == null || recipeEntryPrefab == null) return;

        foreach (Transform child in recipeContainer)
            Destroy(child.gameObject);

        if (_currentTable == null) return;

        foreach (var recipe in _currentTable.Recipes)
        {
            bool unlocked = ResearchManager.Instance == null
                         || ResearchManager.Instance.IsUnlocked(recipe);

            var entry = Instantiate(recipeEntryPrefab, recipeContainer);
            entry.Setup(recipe, unlocked);
            entry.OnSelected += SelectRecipe;   // 잠겨도 눌러서 내용은 볼 수 있게
        }
    }

    void SelectRecipe(RecipeData recipe)
    {
        _selectedRecipe = recipe;
        ShowDetail(recipe);
    }

    void ShowDetail(RecipeData recipe)
    {
        if (detailPanel != null) detailPanel.SetActive(true);

        if (resultIcon != null)
        {
            resultIcon.sprite = recipe.Result?.Icon;
            resultIcon.color  = recipe.Result?.Icon != null ? Color.white : new Color(1,1,1,0);
        }
        if (resultNameText != null) resultNameText.text = recipe.Result?.ItemName ?? recipe.RecipeName;
        if (craftTimeText  != null) craftTimeText.text  = $"제작 시간: {recipe.CraftTime}s";

        // 잠긴 레시피는 **왜 잠겼는지**를 설명 맨 위에 붙인다.
        // 목록에서 `[잠김]`만 보면 무슨 연구를 해야 하는지 알 수 없다.
        bool locked = ResearchManager.Instance != null && !ResearchManager.Instance.IsUnlocked(recipe);
        if (descriptionText != null)
        {
            var sb = new StringBuilder();
            if (locked)
            {
                var need = ResearchManager.Instance.GetRequiredResearch(recipe);
                sb.AppendLine($"<color=#FF9060>[잠김] {(need != null ? need.ResearchName : "연구")} 연구가 필요합니다.</color>");
                sb.AppendLine();
            }
            sb.Append(recipe.Result?.Description ?? "");
            descriptionText.text = sb.ToString();
        }

        RefreshIngredients(recipe);

        if (craftButton != null)
        {
            craftButton.onClick.RemoveAllListeners();
            craftButton.onClick.AddListener(() => OnCraftClicked(recipe));
            // 잠겼으면 재료가 충분해도 주문할 수 없다.
            craftButton.interactable = !locked && _currentTable != null && _currentTable.CanCraft(recipe);
        }
    }

    void RefreshIngredients(RecipeData recipe)
    {
        if (ingredientsText == null) return;
        var sb = new StringBuilder();
        foreach (var ing in recipe.Ingredients)
        {
            int have   = Storage.GetTotalByItem(ing.Item);
            string col = have >= ing.Amount ? "#88FF88" : "#FF6666";
            sb.AppendLine($"<color={col}>{ing.Item?.ItemName}  {have} / {ing.Amount}</color>");
        }
        ingredientsText.text = sb.ToString();
    }

    void OnCraftClicked(RecipeData recipe)
    {
        if (_currentTable == null) return;
        _currentTable.QueueOrder(recipe);
        RefreshIngredients(recipe);
        if (craftButton != null)
            craftButton.interactable = _currentTable.CanCraft(recipe);
    }
}
