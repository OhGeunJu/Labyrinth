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

    [Header("진행 게이지")]
    [Tooltip("진행 중일 때만 켜지는 묶음 (게이지 + 텍스트의 부모)")]
    [SerializeField] GameObject      progressRoot;
    [Tooltip("Image Type=Filled, Fill Method=Horizontal 로 설정된 채움 이미지")]
    [SerializeField] Image           progressFill;
    [Tooltip("'제련술  42%' 형태로 표시")]
    [SerializeField] TextMeshProUGUI progressText;

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
        if (panel == null || !panel.activeSelf) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        RefreshProgress();
    }

    // 진행 중인 연구가 있을 때만 게이지를 띄운다. 큐에 예약만 걸린 상태나
    // 폰이 아직 걸어오는 중이면 타이머가 안 돌므로 여기 안 잡힌다.
    void RefreshProgress()
    {
        if (progressRoot == null) return;

        var active = _currentTable != null ? _currentTable.ActiveResearch : null;
        if (active == null) { progressRoot.SetActive(false); return; }

        progressRoot.SetActive(true);
        float p = _currentTable.ActiveProgress;

        if (progressFill != null) progressFill.fillAmount = p;
        if (progressText != null) progressText.text = $"{active.ResearchName}  {p * 100f:F0}%";
    }

    void Open(ResearchTable table)
    {
        _currentTable = table;
        _selected     = null;
        if (panel != null) panel.SetActive(true);
        if (detailPanel != null) detailPanel.SetActive(false);
        RebuildList();
        RefreshProgress();   // 여는 순간 이전 연구대의 상태가 남아 보이지 않게
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

        if (titleText        != null) titleText.text        = data.ResearchName;
        if (researchTimeText != null) researchTimeText.text = $"연구 시간: {data.ResearchTime}초";

        // **이걸 하면 뭐가 열리나** — 연구를 고르는 유일한 근거인데 그동안 안 보였다.
        // 별도 칸을 만들면 씬을 또 고쳐야 해서 설명란 아래에 붙인다.
        if (descriptionText != null)
        {
            var sb = new StringBuilder();
            sb.Append(data.Description);

            if (data.UnlockedRecipes != null && data.UnlockedRecipes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("<color=#8FD8FF>해금 — </color>");
                bool first = true;
                foreach (var r in data.UnlockedRecipes)
                {
                    if (r == null) continue;
                    if (!first) sb.Append("<color=#8FD8FF>, </color>");
                    sb.Append($"<color=#8FD8FF>{r.RecipeName}</color>");
                    first = false;
                }
            }
            descriptionText.text = sb.ToString();
        }

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
