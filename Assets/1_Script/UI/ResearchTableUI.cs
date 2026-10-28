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

    // ── 그래프 ────────────────────────────────────────────────────────────
    //
    // **규격은 씬이 갖고 이 코드는 그 안을 채운다.** 창 크기·스크롤 칸·선을 그릴 자리는
    // `DungeonColony → Setup Research Graph`가 씬에 세워 두고, 여기서는 노드를 찍고 선을
    // 그을 뿐이다. 코드가 창을 넓히면 **편집 중에 보이는 모양과 게임의 모양이 갈린다.**
    [Header("그래프")]
    [Tooltip("선을 그리는 판. `Content`의 첫 자식으로 씬에 세워 둔다 (에디터 도구가 만든다)")]
    [SerializeField] ResearchLinkGraphic links;
    [Tooltip("노드 사이 간격 (가로=열, 세로=행). 노드 **크기**는 노드 프리팹이 갖는다")]
    [SerializeField] Vector2 nodeGap = new(64f, 14f);
    [Tooltip("그래프 가장자리 여백")]
    [SerializeField] Vector2 graphPadding = new(18f, 18f);

    bool _warnedNoLinks;

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

    void HandleResearchCompleted(ResearchData _) => RebuildGraph();

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
        RebuildGraph();
    }

    public void Close()
    {
        _currentTable = null;
        WindowRaiser.Close(panel);
    }

    // **노드를 놓고 선을 긋는다.** 자리는 `ResearchGraphLayout`이 한 번 계산하고
    // 노드와 선이 **같은 답을 읽는다** — 각자 재면 선이 노드에 안 붙는다.
    void RebuildGraph()
    {
        if (entryContainer == null || entryPrefab == null || _currentTable == null) return;
        WarnIfUnwired();

        var all     = _currentTable.AllResearch;
        var pending = _currentTable.PendingResearch;

        // ── 씬에 구워 둔 것이 있으면 **그것이 진짜다** ──────────────────────
        //
        // (2026-08-25, 사용자 *"런타임에서 쓰는 걸 굽는게 아니라 그냥 진짜 씬에 배치한걸
        // 런타임에 썼으면 좋겠는데"*.)
        //
        // 전에는 구운 노드를 **오브젝트로만** 쓰고 자리는 매번 다시 계산했다 — 그러면
        // **씬에서 노드를 옮겨 놔도 게임에서는 제자리로 돌아간다.** 굽는 뜻이 절반만 서는 것이고,
        // 무엇보다 *편집 중에 보이는 모양*과 *게임의 모양*이 갈린다.
        //
        // 이제 자리도 선도 **안 건드린다.** 런타임이 하는 일은 두 가지뿐이다:
        // 노드의 글자·색을 지금 상태로 칠하고(`Setup`), 선의 **완료 색만** 다시 칠한다
        // (`RefreshDone` — 그래서 선이 제 양 끝을 기억한다).
        var baked = CollectBaked(all);
        if (baked != null)
        {
            _entries.Clear();
            foreach (var data in all)
            {
                if (data == null || !baked.TryGetValue(data, out var e)) continue;

                // **등급이 모자란 연구는 감춘다**(사용자: *"하위 연구대에서는 상위 연구대의
                // 연구들이 안 보여야 해"*). **구운 노드는 그대로 둔다** — 목록에서 빼면
                // `CollectBaked`가 *낡았다*고 보고 저작한 자리를 통째로 버린다.
                bool shown = _currentTable.HandlesTier(data);
                e.gameObject.SetActive(shown);
                if (!shown) continue;

                e.Setup(data, pending);
                e.OnSelected += SelectResearch;
                _entries.Add(e);
            }

            // **선은 씬에 놓인 노드의 자리에서 다시 긋는다** — 계산해서 긋는 것이 아니라
            // *거기 있는 것*을 읽는 것이라, 씬에서 노드를 끌어 옮기면 **선이 따라온다.**
            // (완료 색도 여기서 같이 정해진다.)
            if (links != null) links.SetLinks(BuildLinksFromScene(baked));

            ScrollToTopLeft();
            _markedResearch = pending;
            return;
        }

        // ── 구운 것이 없으면(또는 낡았으면) 계산해서 찍는다 ────────────────
        //
        // **노드 크기는 프리팹이 든다** — 코드가 정하면 프리팹을 손봐도 그래프는 옛 크기로
        // 자리를 잡아 노드끼리 겹치거나 사이가 벌어진다.
        var graph = ResearchGraphLayout.Build(all, NodeSize(), nodeGap, graphPadding);

        // **선을 그리는 판은 남긴다** — 같이 지우면 `links`가 파괴된 참조가 되어 다음 줄에서
        // 선이 통째로, 그리고 **조용히** 안 그려진다.
        foreach (Transform child in entryContainer)
            if (links == null || child != links.transform) Destroy(child.gameObject);
        _entries.Clear();

        foreach (var data in all)
        {
            if (data == null || !graph.Slots.TryGetValue(data, out var slot)) continue;

            // 계산해서 찍을 때도 같다 — **자리는 전체로 잡고 감추기만 한다**(빼고 잡으면
            // 등급이 오를 때마다 그래프 모양이 통째로 달라진다).
            if (!_currentTable.HandlesTier(data)) continue;

            var entry = Instantiate(entryPrefab, entryContainer);
            entry.Setup(data, pending);
            entry.OnSelected += SelectResearch;
            _entries.Add(entry);

            if (entry.transform is RectTransform rt)
            {
                rt.anchorMin        = new Vector2(0f, 1f);
                rt.anchorMax        = new Vector2(0f, 1f);
                rt.pivot            = new Vector2(0f, 1f);
                rt.sizeDelta        = graph.NodeSize;
                rt.anchoredPosition = slot.TopLeft;
            }
        }

        // 스크롤 범위는 **내용이 정한다** — 그래프가 자라면 스크롤도 같이 자라야 한다.
        if (entryContainer is RectTransform content) content.sizeDelta = graph.Size;
        if (links != null)
        {
            (links.transform as RectTransform).sizeDelta = graph.Size;
            links.SetLinks(BuildLinks(graph));
        }

        ScrollToTopLeft();
        _markedResearch = pending;
    }

    // 열었을 때 **왼쪽 위**를 본다 — 뿌리가 거기 있다. 안 맞추면 지난번 스크롤 자리가
    // 남아 빈 곳을 보게 된다. 꺼진 것까지 찾는다 — 연구가 끝나 목록을 다시 지을 때
    // 창이 닫혀 있을 수 있다.
    void ScrollToTopLeft()
    {
        var scroll = entryContainer.GetComponentInParent<ScrollRect>(true);
        if (scroll == null) return;
        scroll.horizontalNormalizedPosition = 0f;
        scroll.verticalNormalizedPosition   = 1f;
    }

    // 씬에 구워 둔 노드를 모은다. **연구 목록과 정확히 맞을 때만** 쓴다 —
    // 하나라도 모자라거나 남으면 `null`을 돌려주고 부르는 쪽이 통째로 다시 찍는다.
    //
    // 반쯤 맞을 때 섞어 쓰지 않는 이유: 남는 노드를 골라 지우고 없는 것만 새로 찍는 길을
    // 두면 **구운 것이 낡았을 때 화면이 조용히 반만 맞는다.** 다 맞거나 다 새로 찍거나다.
    Dictionary<ResearchData, ResearchEntryUI> CollectBaked(IReadOnlyList<ResearchData> all)
    {
        var found = new Dictionary<ResearchData, ResearchEntryUI>();

        foreach (Transform child in entryContainer)
        {
            if (links != null && child == links.transform) continue;
            if (!child.TryGetComponent<ResearchEntryUI>(out var e) || e.Data == null) return null;
            if (!found.TryAdd(e.Data, e)) return null;   // 같은 연구가 둘
        }

        if (found.Count == 0) return null;

        int need = 0;
        foreach (var d in all) if (d != null) need++;
        if (found.Count != need) { WarnStaleBake(found.Count, need); return null; }

        foreach (var d in all)
            if (d != null && !found.ContainsKey(d)) { WarnStaleBake(found.Count, need); return null; }

        return found;
    }

    bool _warnedStale;

    // **낡은 것을 조용히 다시 찍지 않는다.** 다시 찍으면 게임은 멀쩡한데 **에디터에서 보이는
    // 그래프만 옛것**으로 남아, 자리를 저작하는 사람이 화면을 믿을 수 없게 된다.
    void WarnStaleBake(int baked, int need)
    {
        if (_warnedStale) return;
        _warnedStale = true;
        Debug.LogWarning($"[연구 그래프] 씬에 구워 둔 노드가 {baked}개인데 연구는 {need}개입니다 — "
                       + "런타임에 다시 찍습니다. `DungeonColony → Bake Research Graph`로 다시 구우세요.");
    }

    // 씬에 놓인 노드에서 선을 긋는다. **자리를 계산하지 않는다** — `RectTransform`이 지금
    // 어디 있는지를 읽을 뿐이라, 저작자가 노드를 끌어 옮기면 선이 그대로 따라온다.
    //
    // 좌표 규약은 구울 때와 같다(앵커·피벗이 왼쪽 위) — 그래서 `anchoredPosition`이 곧
    // `Content` 기준 왼쪽 위 모서리다.
    List<ResearchLinkGraphic.Link> BuildLinksFromScene(Dictionary<ResearchData, ResearchEntryUI> nodes)
    {
        var result = new List<ResearchLinkGraphic.Link>();
        var mgr    = ResearchManager.Instance;

        foreach (var kv in nodes)
        {
            var data = kv.Key;
            if (data.Prerequisites == null) continue;
            if (kv.Value.transform is not RectTransform to) continue;

            // **감춘 노드에는 선을 안 잇는다** — 안 그으면 선이 빈 곳으로 뻗어, 없는 노드가
            // 있는 것처럼 보인다(등급이 모자라 감춘 것도 씬에는 그대로 서 있다).
            if (!kv.Value.gameObject.activeSelf) continue;

            foreach (var pre in data.Prerequisites)
            {
                if (pre == null || !nodes.TryGetValue(pre, out var fromEntry)) continue;
                if (!fromEntry.gameObject.activeSelf) continue;
                if (fromEntry.transform is not RectTransform from) continue;

                result.Add(new ResearchLinkGraphic.Link
                {
                    From = from.anchoredPosition + new Vector2(from.rect.width, -from.rect.height * 0.5f),
                    To   = to.anchoredPosition   + new Vector2(0f,              -to.rect.height   * 0.5f),
                    Done = mgr != null && mgr.IsCompleted(pre),
                });
            }
        }
        return result;
    }

    // 선은 **선행의 오른쪽 가장자리에서 나와 뒤 연구의 왼쪽 가장자리로** 들어간다.
    // 중심끼리 이으면 선이 노드 위를 가로질러 글자를 덮는다.
    static List<ResearchLinkGraphic.Link> BuildLinks(ResearchGraphLayout.Result graph)
    {
        var result = new List<ResearchLinkGraphic.Link>();
        var mgr    = ResearchManager.Instance;
        float halfH = graph.NodeSize.y * 0.5f;

        foreach (var kv in graph.Slots)
        {
            var data = kv.Key;
            if (data.Prerequisites == null) continue;

            foreach (var pre in data.Prerequisites)
            {
                if (pre == null || !graph.Slots.TryGetValue(pre, out var from)) continue;

                result.Add(new ResearchLinkGraphic.Link
                {
                    From = from.TopLeft + new Vector2(graph.NodeSize.x, -halfH),
                    To   = kv.Value.TopLeft + new Vector2(0f, -halfH),
                    Done = mgr != null && mgr.IsCompleted(pre),
                });
            }
        }
        return result;
    }

    // 노드 크기 = **프리팹의 크기**. 프리팹이 없거나 0이면 그래프가 한 자리에 쌓이므로
    // 최소값으로 받쳐 둔다.
    Vector2 NodeSize()
    {
        var rt = entryPrefab != null ? entryPrefab.transform as RectTransform : null;
        var size = rt != null ? rt.rect.size : Vector2.zero;
        return new Vector2(size.x > 1f ? size.x : 156f, size.y > 1f ? size.y : 38f);
    }

    // **안 배선된 것을 조용히 넘기지 않는다.** 선을 그릴 판이 없으면 노드만 뜨고 그래프가
    // 그냥 흩어진 상자들로 보이는데, 그 증상만으로는 배선이 빠진 것인지 선행이 없는 것인지
    // 구별이 안 된다. 한 번만 적는다(매 프레임 적으면 콘솔이 묻힌다).
    void WarnIfUnwired()
    {
        if (links != null || _warnedNoLinks) return;
        _warnedNoLinks = true;
        Debug.LogWarning("[연구] 선을 그릴 판이 배선되지 않았습니다 — " +
                         "`DungeonColony → Setup Research Graph`를 한 번 돌리세요.");
    }

    void SelectResearch(ResearchData data)
    {
        _selected = data;
        ShowDetail(data);
    }

    void ShowDetail(ResearchData data)
    {
        WindowRaiser.Open(detailPanel);

        if (titleText        != null) titleText.text        = data.ResearchName;
        if (researchTimeText != null) researchTimeText.text = $"연구 시간: {data.ResearchTime}초";

        // 설명란은 **설명만** 담는다. 전에는 해금 목록을 여기 덧붙였는데, 길어지면
        // 아래 선행 연구 칸을 침범해 글자가 겹쳤다 — 한 칸에 두 가지를 넣은 대가였다.
        if (descriptionText != null) descriptionText.text = data.Description;

        // 선행 연구 + 해금을 **한 블록**으로 모은다. 둘 다 "이 연구의 위치"를 말하는 정보다.
        if (prerequisiteText != null)
        {
            var sb = new StringBuilder();

            // **선행마다 끝났는지를 따로 칠한다** — 둘 중 하나만 남았을 때 어느 쪽인지가
            // 안 보이면 "왜 아직 잠겼지"에 답이 없다.
            if (data.Prerequisites != null && data.Prerequisites.Count > 0)
            {
                sb.Append("선행 연구: ");
                bool first = true;
                foreach (var pre in data.Prerequisites)
                {
                    if (pre == null) continue;
                    if (!first) sb.Append(", ");
                    first = false;

                    bool done = ResearchManager.Instance != null
                             && ResearchManager.Instance.IsCompleted(pre);
                    sb.Append($"<color={(done ? "#88FF88" : "#FF6666")}>{pre.ResearchName}</color>");
                }
                sb.AppendLine();
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

            // **하수인도 적는다**(2026-08-25). `골각 세공`의 설명이 *"던전 핵이 해골을 일으켜
            // 세울 수 있게 된다"*고 말하는데 **해금 목록에는 그 줄이 없었다** — 설명이 약속한
            // 것을 목록이 확인해 주지 않으면, 연구를 고르는 사람은 그 문장을 믿을 수가 없다.
            // (게이트 자체는 `MinionData.RequiredResearch`로 진작 돌고 있었다. 안 보였을 뿐이다.)
            var minions = ResearchManager.Instance != null
                        ? ResearchManager.Instance.GetUnlockedMinions(data)
                        : null;

            if (minions != null && minions.Count > 0)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append("<color=#D8A8FF>소환 — ");
                for (int i = 0; i < minions.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(minions[i].RaceName);
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
            int have = MaterialPool.AvailableHere(ing.Resolve());
            string col = have >= ing.Amount ? "#88FF88" : "#FF6666";
            sb.AppendLine($"<color={col}>{ing.DisplayName}  {have} / {ing.Amount}</color>");
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
