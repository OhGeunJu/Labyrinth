using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class ResearchTableUI : MonoBehaviour
{
    [Header("씬 참조")]
    [SerializeField] GameObject      panel;
    [SerializeField] Transform       entryContainer;
    [SerializeField] ResearchEntryUI entryPrefab;

    [Header("디테일 패널")]
    [SerializeField] GameObject      detailPanel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] TextMeshProUGUI ingredientsText;
    [SerializeField] TextMeshProUGUI researchTimeText;
    [SerializeField] TextMeshProUGUI prerequisiteText;
    [SerializeField] Button          researchButton;

    ResearchTable _currentTable;
    ResearchData  _selected;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(false);
        ResearchTable.OnSelected         += Open;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
        ResearchManager.OnResearchCompleted += HandleResearchCompleted;
    }

    void OnDestroy()
    {
        ResearchTable.OnSelected            -= Open;
        BuildModeManager.OnActiveChanged    -= OnBuildModeChanged;
        ResearchManager.OnResearchCompleted -= HandleResearchCompleted;
    }

    void HandleResearchCompleted(ResearchData _) => RebuildList();

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Update()
    {
        if (panel != null && panel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void Open(ResearchTable table)
    {
        _currentTable = table;
        _selected     = null;
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
        if (entryContainer == null || entryPrefab == null || _currentTable == null) return;

        foreach (Transform child in entryContainer)
            Destroy(child.gameObject);

        foreach (var data in _currentTable.AllResearch)
        {
            var entry = Instantiate(entryPrefab, entryContainer);
            entry.Setup(data);
            entry.OnSelected += SelectResearch;
        }
    }

    void SelectResearch(ResearchData data)
    {
        _selected = data;
        ShowDetail(data);
    }

    void ShowDetail(ResearchData data)
    {
        if (detailPanel != null) detailPanel.SetActive(true);

        if (titleText       != null) titleText.text       = data.ResearchName;
        if (descriptionText != null) descriptionText.text = data.Description;
        if (researchTimeText != null) researchTimeText.text = $"연구 시간: {data.ResearchTime}초";

        if (prerequisiteText != null)
        {
            if (data.Prerequisite == null)
                prerequisiteText.text = "";
            else
            {
                bool done = ResearchManager.Instance != null && ResearchManager.Instance.IsCompleted(data.Prerequisite);
                string col = done ? "#88FF88" : "#FF6666";
                prerequisiteText.text = $"선행 연구: <color={col}>{data.Prerequisite.ResearchName}</color>";
            }
        }

        RefreshIngredients(data);

        if (researchButton != null)
        {
            researchButton.onClick.RemoveAllListeners();
            researchButton.onClick.AddListener(() => OnResearchClicked(data));
            researchButton.interactable = _currentTable != null && _currentTable.CanResearch(data);
        }
    }

    void RefreshIngredients(ResearchData data)
    {
        if (ingredientsText == null) return;
        if (data.Ingredients.Count == 0) { ingredientsText.text = "재료 없음"; return; }

        var sb = new StringBuilder();
        foreach (var ing in data.Ingredients)
        {
            int have = Storage.GetTotalByItem(ing.Item);
            string col = have >= ing.Amount ? "#88FF88" : "#FF6666";
            sb.AppendLine($"<color={col}>{ing.Item?.ItemName}  {have} / {ing.Amount}</color>");
        }
        ingredientsText.text = sb.ToString();
    }

    void OnResearchClicked(ResearchData data)
    {
        if (_currentTable == null) return;
        _currentTable.QueueResearch(data);
        ShowDetail(data);
    }
}
