using System.Collections.Generic;
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
    [Tooltip("비우면 버튼의 자식 텍스트를 자동으로 찾는다")]
    [SerializeField] TextMeshProUGUI researchButtonLabel;

    [Header("진행 게이지")]
    [Tooltip("진행 중일 때만 켜지는 묶음 (게이지 + 텍스트의 부모). 런타임에 [연구] 버튼 자리로 옮겨진다")]
    [SerializeField] GameObject      progressRoot;
    [Tooltip("Image Type=Filled, Fill Method=Horizontal 로 설정된 채움 이미지")]
    [SerializeField] Image           progressFill;
    [Tooltip("'제련술  42%' 형태로 표시")]
    [SerializeField] TextMeshProUGUI progressText;

    ResearchTable _currentTable;
    ResearchData  _selected;

    readonly List<ResearchEntryUI> _entries = new();
    ResearchData _markedResearch;   // 목록 표식이 가리키는 연구 — 바뀔 때만 다시 칠한다
    bool         _slotPrepared;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (detailPanel != null) detailPanel.SetActive(false);

        // 게이지를 버튼 자리로 옮기는 건 한 번만 하면 된다. **여기서 미리 해 두는 이유**는
        // 그러지 않으면 첫 선택 전까지 씬에 저작된 자리(패널 아래)에 그대로 떠 있기 때문이다.
        PrepareSlot();
        if (progressRoot != null) progressRoot.SetActive(false);

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

        RefreshActionSlot();
        RefreshEntryMarks();
    }

    // ── [연구] 버튼과 게이지는 **같은 칸을 나눠 쓴다** ─────────────────────
    // 전에는 게이지가 패널 아래에 따로 떠서, 켜지는 순간 원래 그 자리에 있던 것들을 가렸다.
    // 둘은 동시에 필요한 적이 없다 — 지금 이 연구를 하고 있으면 [연구] 버튼은 의미가 없고,
    // 하고 있지 않으면 게이지가 보여줄 게 없다. 그래서 한 칸을 번갈아 쓴다.
    void PrepareSlot()
    {
        if (_slotPrepared || progressRoot == null || researchButton == null) return;
        _slotPrepared = true;

        var btnRt = researchButton.transform as RectTransform;
        var barRt = progressRoot.transform as RectTransform;
        if (btnRt == null || barRt == null || btnRt.parent == null) return;

        barRt.SetParent(btnRt.parent, false);
        barRt.SetSiblingIndex(btnRt.GetSiblingIndex() + 1);

        // 부모가 레이아웃 그룹이면 자리는 `LayoutElement`가, 아니면 앵커·좌표가 정한다.
        // **어느 쪽인지 여기서 알 수 없으므로 둘 다 맞춘다** — 한쪽만 맞추면 나머지 경우에 어긋난다.
        barRt.anchorMin        = btnRt.anchorMin;
        barRt.anchorMax        = btnRt.anchorMax;
        barRt.pivot            = btnRt.pivot;
        barRt.anchoredPosition = btnRt.anchoredPosition;
        barRt.sizeDelta        = btnRt.sizeDelta;

        var barLe = progressRoot.GetComponent<LayoutElement>();
        if (barLe == null) barLe = progressRoot.AddComponent<LayoutElement>();

        var btnLe = researchButton.GetComponent<LayoutElement>();
        if (btnLe != null)
        {
            barLe.preferredWidth = btnLe.preferredWidth;
            barLe.flexibleWidth  = btnLe.flexibleWidth;
        }
    }

    // 게이지 높이를 버튼 높이에 맞춘다. **제 높이가 잡힐 때까지 계속 시도한다** —
    // 레이아웃이 아직 안 돈 프레임에 재면 0이 나오고, 그 0을 그대로 박으면
    // 레이아웃 그룹 아래에서 게이지가 납작하게 찌그러진 채 고정된다.
    void EnsureSlotHeight()
    {
        if (progressRoot == null || researchButton == null) return;

        var barLe = progressRoot.GetComponent<LayoutElement>();
        if (barLe == null || barLe.preferredHeight > 0f) return;

        var btnLe = researchButton.GetComponent<LayoutElement>();
        float h = btnLe != null && btnLe.preferredHeight > 0f
                ? btnLe.preferredHeight
                : (researchButton.transform as RectTransform)?.rect.height ?? 0f;

        if (h <= 0f) return;
        barLe.preferredHeight = h;
        barLe.minHeight       = h;
    }

    // 고른 연구가 **지금 이 연구대가 맡은 것**이면 게이지, 아니면 [연구] 버튼.
    // 버튼 문구가 "시작"인지 "이어서"인지 "전환"인지까지 적는다 — 누르면 무슨 일이 일어나는지
    // 모르면 하던 연구를 잃을까 봐 못 누른다.
    void RefreshActionSlot()
    {
        if (_currentTable == null || _selected == null) return;
        PrepareSlot();
        EnsureSlotHeight();

        var pending = _currentTable.PendingResearch;
        bool showBar = pending != null && pending == _selected;

        if (researchButton != null) researchButton.gameObject.SetActive(!showBar);
        if (progressRoot   != null) progressRoot.SetActive(showBar);

        if (showBar)
        {
            UpdateBar();
            return;
        }

        if (researchButton == null) return;

        var   mgr  = ResearchManager.Instance;
        float done = mgr != null ? mgr.GetProgress01(_selected) : 0f;
        bool  half = done > 0f;

        string label;
        if (mgr != null && mgr.IsCompleted(_selected))
            label = "연구 완료";                       // 게이지가 사라진 자리에 "시작"이 뜨면 다시 할 수 있어 보인다
        else if (pending != null)
            label = half ? $"이 연구로 전환  (이어서 {done * 100f:F0}%)" : "이 연구로 전환";
        else
            label = half ? $"이어서 연구  {done * 100f:F0}%" : "연구 시작";

        var text = ResolveButtonLabel();
        if (text != null) text.text = label;

        researchButton.interactable = _currentTable.CanResearch(_selected);
    }

    void UpdateBar()
    {
        var  mgr     = ResearchManager.Instance;
        bool running = _currentTable.ActiveResearch == _selected;

        // 폰이 붙어 도는 중이면 실시간 값을, 아직 안 왔으면 저장된 몫을 쓴다.
        float p = running
                ? _currentTable.ActiveProgress
                : (mgr != null ? mgr.GetProgress01(_selected) : 0f);

        if (progressFill != null) progressFill.fillAmount = p;
        if (progressText == null) return;

        // **폰을 기다리는 중이라는 것도 상태다.** 이걸 안 적으면 0%에서 안 움직이는 게이지가
        // 고장으로 보인다 — 실제로는 아무도 연구대에 오지 않은 것이다.
        progressText.text = running
            ? $"{_selected.ResearchName}  {p * 100f:F0}%"
            : $"{_selected.ResearchName}  {p * 100f:F0}% <color=#FFAA55>(폰 대기 중)</color>";
    }

    TextMeshProUGUI ResolveButtonLabel()
    {
        if (researchButtonLabel == null && researchButton != null)
            researchButtonLabel = researchButton.GetComponentInChildren<TextMeshProUGUI>(true);
        return researchButtonLabel;
    }

    // 목록의 [연구 중] 표식. **바뀔 때만** 다시 칠한다 — 매 프레임 칠하면 글자 색을
    // 계속 덮어쓰느라 낭비고, 목록을 통째로 다시 지으면 스크롤 위치가 튄다.
    void RefreshEntryMarks()
    {
        var pending = _currentTable != null ? _currentTable.PendingResearch : null;
        if (pending == _markedResearch) return;
        _markedResearch = pending;

        foreach (var e in _entries)
            if (e != null) e.Refresh(pending);
    }

    void Open(ResearchTable table)
    {
        _currentTable = table;
        _selected     = null;
        WindowRaiser.Open(panel);
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
        _entries.Clear();

        var pending = _currentTable.PendingResearch;
        foreach (var data in _currentTable.AllResearch)
        {
            var entry = Instantiate(entryPrefab, entryContainer);
            entry.Setup(data, pending);
            entry.OnSelected += SelectResearch;
            _entries.Add(entry);
        }
        _markedResearch = pending;
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

            // **건물도 연구로 열린다**(2026-08-02). 레시피만 적으면 "목공술을 하면 제재소를
            // 지을 수 있다"가 화면 어디에도 없다 — 그게 그 연구를 하는 제일 큰 이유인데.
            // 목록은 `BuildingData.RequiredResearch`에서 자동으로 모은다(따로 적어 둘 곳 없음).
            var builds = ResearchManager.Instance != null
                       ? ResearchManager.Instance.GetUnlockedBuildings(data)
                       : null;

            if (builds != null && builds.Count > 0)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append("<color=#FFD08F>건물 — ");
                for (int i = 0; i < builds.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(builds[i].BuildingName);
                }
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
        }

        // 버튼을 켤지 게이지를 켤지, 문구가 무엇일지는 여기 한 곳에서만 정한다.
        RefreshActionSlot();
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

    // 하던 연구가 있으면 **접고 갈아탄다**(`StartResearch`). 접힌 쪽의 진행도는 남으므로
    // 다시 고르면 이어서 한다 — 그래서 갈아타는 데 아무 대가가 없다.
    void OnResearchClicked(ResearchData data)
    {
        if (_currentTable == null) return;
        _currentTable.StartResearch(data);
        ShowDetail(data);
        RefreshEntryMarks();
    }
}
