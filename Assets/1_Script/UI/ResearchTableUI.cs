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

    // 게이지는 두 가지를 보여준다:
    //   ① 폰이 붙어 **지금 도는 중** — 실시간으로 차오른다
    //   ② 붙어 있지 않지만 **해 둔 몫이 있는 중단 상태** — 어디까지 왔는지 그대로 보인다
    //
    // 전에는 ①만 있었다. 그런데 진행도가 저장되기 시작한 뒤로는 폰이 자리를 비운 동안
    // "이 연구가 60%까지 와 있다"가 화면 어디에도 없었다 — 그게 제일 알고 싶은 순간인데.
    void RefreshProgress()
    {
        if (progressRoot == null) return;

        var active = _currentTable != null ? _currentTable.ActiveResearch : null;
        if (active != null)
        {
            progressRoot.SetActive(true);
            float p = _currentTable.ActiveProgress;
            if (progressFill != null) progressFill.fillAmount = p;
            if (progressText != null) progressText.text = $"{active.ResearchName}  {p * 100f:F0}%";
            return;
        }

        // 고른 연구에 해 둔 몫이 있으면 그것을 보여준다(멈춰 있는 상태).
        var mgr = ResearchManager.Instance;
        if (_selected != null && mgr != null && mgr.GetProgressSeconds(_selected) > 0f)
        {
            float p = mgr.GetProgress01(_selected);
            progressRoot.SetActive(true);
            if (progressFill != null) progressFill.fillAmount = p;
            if (progressText != null)
                progressText.text = $"{_selected.ResearchName}  {p * 100f:F0}% <color=#FFAA55>(멈춤)</color>";
            return;
        }

        progressRoot.SetActive(false);
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

        // 설명란은 **설명만** 담는다. 전에는 해금 목록을 여기 덧붙였는데, 길어지면
        // 아래 선행 연구 칸을 침범해 글자가 겹쳤다 — 한 칸에 두 가지를 넣은 대가였다.
        if (descriptionText != null) descriptionText.text = data.Description;

        // 선행 연구 + 해금을 **한 블록**으로 모은다. 둘 다 "이 연구의 위치"를 말하는 정보다.
        if (prerequisiteText != null)
        {
            var sb = new StringBuilder();

            if (data.Prerequisite != null)
            {
                bool done = ResearchManager.Instance != null
                         && ResearchManager.Instance.IsCompleted(data.Prerequisite);
                string col = done ? "#88FF88" : "#FF6666";
                sb.AppendLine($"선행 연구: <color={col}>{data.Prerequisite.ResearchName}</color>");
            }

            // **이걸 하면 뭐가 열리나** — 연구를 고르는 유일한 근거다.
            // `ResearchManager`가 양방향(큐레이션 + 레시피가 자기를 가리키는 것)을 다 모아 준다.
            var unlocks = ResearchManager.Instance != null
                        ? ResearchManager.Instance.GetUnlockedRecipes(data)
                        : null;

            if (unlocks != null && unlocks.Count > 0)
            {
                // 선행 연구와 해금은 성격이 달라 한 줄 띄운다 — 붙어 있으면 한 덩어리로 읽힌다.
                if (sb.Length > 0) sb.AppendLine();

                // **개수를 잘라 길이를 못 박는다.** 연구 하나가 레시피 열두 개를 열기도 해서,
                // 다 적으면 글자가 패널을 뚫고 나간다. 여기서 자르지 않으면 패널을 아무리 키워도
                // 콘텐츠가 늘 때마다 같은 일이 반복된다.
                const int MaxNames = 6;
                int shown = Mathf.Min(MaxNames, unlocks.Count);

                // **줄을 바꿔 시작한다.** 선행 연구와 같은 줄에 이어 붙이면 첫 줄에서 자리를 다 쓴다.
                sb.Append("<color=#8FD8FF>해금 — ");
                for (int i = 0; i < shown; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(unlocks[i].RecipeName);
                }
                if (unlocks.Count > shown) sb.Append($" 외 {unlocks.Count - shown}개");
                sb.Append("</color>");
            }

            prerequisiteText.text = sb.ToString();

            // **글자가 칸에 맞춰 줄어들게 한다.** 칸이 한두 줄짜리라 자르기만 하면 한두 개밖에 안 보인다 —
            // 자동 축소를 켜면 같은 칸에 네댓 개가 들어간다. 9pt를 바닥으로 둬서 못 읽을 만큼
            // 작아지지는 않게 하고, 그래도 넘치면 `…`로 더 있다는 것만 알린다.
            prerequisiteText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            prerequisiteText.enableAutoSizing   = true;
            prerequisiteText.fontSizeMin        = 9f;
            prerequisiteText.fontSizeMax        = 14f;
            prerequisiteText.overflowMode       = TMPro.TextOverflowModes.Ellipsis;
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
