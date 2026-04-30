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
            if (unlocked) entry.OnSelected += SelectRecipe;
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
        if (resultNameText  != null) resultNameText.text  = recipe.Result?.ItemName ?? recipe.RecipeName;
        if (descriptionText != null) descriptionText.text = recipe.Result?.Description ?? "";
        if (craftTimeText   != null) craftTimeText.text   = $"제작 시간: {recipe.CraftTime}s";

        RefreshIngredients(recipe);

        if (craftButton != null)
        {
            craftButton.onClick.RemoveAllListeners();
            craftButton.onClick.AddListener(() => OnCraftClicked(recipe));
            craftButton.interactable = _currentTable != null && _currentTable.CanCraft(recipe);
        }
    }

    void RefreshIngredients(RecipeData recipe)
    {
        if (ingredientsText == null) return;
        var sb = new StringBuilder();
        foreach (var ing in recipe.Ingredients)
        {
            int have   = StorageBuilding.GetTotalByItem(ing.Item);
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
