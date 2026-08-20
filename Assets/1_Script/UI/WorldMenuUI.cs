using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도착 메뉴의 선택지 한 줄.
// **판단은 여기서 하지 않는다** — 무엇이 가능한지는 `WorldMapView`가 정해서 넘긴다(패널은 그리기만).
public struct WorldMenuOption
{
    public string Label;              // "시장에 간다"
    public bool   Enabled;
    public string Note;               // 비활성 사유("준비 중") — 같은 줄에 흐리게 덧붙는다
    public System.Action OnClick;

    public static WorldMenuOption Ready(string label, System.Action onClick, string note = null) =>
        new() { Label = label, Enabled = true, Note = note, OnClick = onClick };

    public static WorldMenuOption Blocked(string label, string note) =>
        new() { Label = label, Enabled = false, Note = note, OnClick = null };
}

// 마운트앤블레이드식 **도착 메뉴** — 월드맵에서 내용물이 있는 칸에 도착하면 자동으로 열린다.
//
// 배너로드식 좌우 분할: 왼쪽에 지역 그림, 오른쪽에 지역명 + 정보 + 세로 선택지 목록.
// 지도에 붙어 따라다니는 팝업이 아니라 **화면 왼쪽에 고정된 메뉴 화면**이다 — 위치 계산이 없다.
// (오른쪽을 비워 두는 건 메뉴를 띄운 채로 지도를 계속 볼 수 있게 하려는 것.)
//
// **선택지의 내용과 가능 여부는 `WorldMapView`가 정한다.** 여기는 받은 목록을 씬의 템플릿 줄로
// 복제해 그릴 뿐이다(→ `docs/editor_creators.md` "모양은 씬이, 코드는 데이터만").
// 그래서 나중에 마을 방문·소굴 습격·자원 채집이 붙어도 이 스크립트는 손대지 않는다.
//
// 캔버스는 필드 UI와 **같은 것 하나**를 쓰되, 캔버스 직속의 `WorldUI` 묶음 아래에 있어야 한다 —
// `GameModeManager`가 캔버스가 아니라 그 자식들을 토글하면서 이 묶음만 반대로 켠다.
public class WorldMenuUI : MonoBehaviour
{
    [SerializeField] GameObject      panel;           // 메뉴 루트 (닫힘 = SetActive(false))
    [Tooltip("왼쪽 지역 그림. 스프라이트가 없으면 바이옴 색 단색으로 채운다.")]
    [SerializeField] Image           illustration;
    [SerializeField] TextMeshProUGUI titleText;       // "숲 마을"
    [SerializeField] TextMeshProUGUI infoText;        // "숲 · 주민 320 · 우호적"
    [Tooltip("선택지 한 줄의 씬 템플릿. 비활성으로 두면 런타임에 이걸 복제해 목록을 채운다.")]
    [SerializeField] Button          optionTemplate;

    readonly List<Button> _spawned = new();

    public bool IsOpen { get; private set; }

    // 닫혔을 때 — 헤더의 X(공용 `CloseButton`)로 닫힌 경우도 포함한다.
    public event System.Action Closed;

    void Awake()
    {
        if (panel == null) { Debug.LogWarning("[WorldMenuUI] panel 미연결"); enabled = false; return; }

        // 템플릿은 원본이라 화면에 보이면 안 된다 — 껐다 켜는 건 복제본만.
        if (optionTemplate != null) optionTemplate.gameObject.SetActive(false);

        var pr = panel.transform as RectTransform;
        if (pr != null) _authoredPanelH = pr.sizeDelta.y;

        panel.SetActive(false);
    }

    // 공용 `CloseButton`은 패널을 SetActive(false)할 뿐이라(패널 스크립트에 close를 따로 두지 않는 규약),
    // 그걸로 닫힌 걸 여기서 알아채야 IsOpen과 지도 쪽 선택 강조가 같이 정리된다.
    void Update()
    {
        if (IsOpen && (panel == null || !panel.activeSelf)) Close();
    }

    // art가 null이면 tint로 단색 — 아트가 아직 없어도 메뉴가 성립해야 한다.
    public void Open(string title, string info, Sprite art, Color tint, IList<WorldMenuOption> options)
    {
        if (panel == null) return;

        if (titleText != null) titleText.text = title;
        if (infoText  != null) infoText.text  = info;

        if (illustration != null)
        {
            illustration.sprite = art;
            illustration.color  = art != null ? Color.white : tint;
            // 스프라이트가 없을 때 Image는 흰 사각형을 그린다 — 그게 곧 단색 판이 된다.
            illustration.preserveAspect = art != null;
        }

        BuildOptions(options);

        IsOpen = true;
        WindowRaiser.Open(panel);
    }

    public void Close()
    {
        bool wasOpen = IsOpen;
        IsOpen = false;
        if (panel != null) panel.SetActive(false);

        // `Closed` 구독자가 다시 Close를 부를 수 있으므로(월드맵 쪽 정리 경로) 열려 있었을 때만 알린다.
        if (wasOpen) Closed?.Invoke();
    }

    void BuildOptions(IList<WorldMenuOption> options)
    {
        // **끄고 나서 지운다.** `Destroy`는 프레임 끝에야 반영되므로, 지우기만 하면 아래
        // `FitToContent`가 재는 순간 **지난 메뉴의 줄들이 아직 목록에 붙어 있다** — 그만큼
        // 창이 더 길게 잡히고, 다음 번에야 제 크기로 돌아온다(한 박자 늦게 따라온다).
        // 레이아웃 그룹은 꺼진 자식을 안 세므로 끄는 것만으로 계산에서 빠진다.
        foreach (var b in _spawned)
        {
            if (b == null) continue;
            b.gameObject.SetActive(false);
            Destroy(b.gameObject);
        }
        _spawned.Clear();

        if (optionTemplate == null || options == null) return;

        foreach (var o in options)
        {
            var btn = Instantiate(optionTemplate, optionTemplate.transform.parent);
            btn.gameObject.SetActive(true);
            btn.interactable = o.Enabled;

            // 이름으로 집는다 — 템플릿에 장식용 자식이 더 붙어도(머리표 막대 등)
            // GetComponentInChildren이 엉뚱한 걸 집지 않게. 장식은 코드가 건드리지 않는다.
            var labelTr = btn.transform.Find("Label");
            var label   = labelTr != null ? labelTr.GetComponent<TextMeshProUGUI>()
                                          : btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                // 부연은 같은 줄에 흐리게 — 왜 못 고르는지가 그 줄 안에서 읽혀야 한다.
                label.text = string.IsNullOrEmpty(o.Note)
                    ? o.Label
                    : $"{o.Label}  <color=#FFFFFF80>{o.Note}</color>";
                UIHelper.ApplyKoreanFont(label, transform);
            }

            var cb = o.OnClick;
            if (cb != null) btn.onClick.AddListener(() => cb());

            _spawned.Add(btn);
        }

        FitToContent();
    }

    // ── 글자가 길면 창이 늘어난다 ────────────────────────────────────────────
    //
    // 줄 높이도 패널 높이도 씬에 **고정값**으로 박혀 있었다(760×420). 그래서 두 줄짜리 문구가
    // 들어오면 — 포로 안내가 그랬다("몸값을 받지 않는 자들이다 — 쳐들어가는 수밖에 없다") —
    // 글자가 칸 안에서 잘리고 목록이 패널 밖으로 넘쳤다.
    //
    // **문구를 줄이는 쪽으로 풀지 않는다.** 여기 뜨는 말은 `WorldMapView`가 정하고 앞으로도
    // 늘 텐데, 그때마다 글자 수를 세게 되면 UI가 내용을 검열하는 꼴이 된다.
    //
    // 두 번 재는 이유: 첫 패스에서는 칸 **너비**가 아직 안 잡혀 TMP가 높이를 못 낸다.
    // 레이아웃을 한 번 확정하고 나서 높이를 얹고, 다시 확정한다.
    const float RowPadding = 14f;    // 글자 위아래 여백
    const float MinRowH    = 34f;    // 한 줄짜리는 지금 모양 그대로
    const float PanelPad   = 24f;
    const float MaxPanelH  = 900f;   // 화면을 넘지 않게 — 넘으면 그때 스크롤을 붙인다

    float _authoredPanelH;           // 씬에 저작된 높이. 매번 여기서 다시 잰다

    void FitToContent()
    {
        if (optionTemplate == null || panel == null) return;

        var listRect = optionTemplate.transform.parent as RectTransform;
        if (listRect == null) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);

        foreach (var btn in _spawned)
        {
            if (btn == null) continue;

            var labelTr = btn.transform.Find("Label");
            var label   = labelTr != null ? labelTr.GetComponent<TextMeshProUGUI>()
                                          : btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null) continue;

            float w = ((RectTransform)label.transform).rect.width;
            if (w <= 1f) continue;   // 아직 너비가 없다 — 고정 높이 그대로 둔다

            float need = label.GetPreferredValues(label.text, w, 0f).y + RowPadding;

            var le = btn.GetComponent<LayoutElement>();
            if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = Mathf.Max(MinRowH, need);
        }

        var panelRect = panel.transform as RectTransform;
        if (panelRect == null) return;

        // **저작한 크기에서 다시 잰다.** 늘리기만 하면 긴 메뉴를 한 번 본 뒤로는 선택지가
        // 둘뿐인 칸에서도 창이 계속 크게 남는다 — 창 크기가 지금 내용과 무관해진다.
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, _authoredPanelH);

        LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);

        // 목록 위(제목·구분선·정보)는 그대로 두고 넘치는 만큼만 키운다.
        float above  = -listRect.anchoredPosition.y;
        float needed = above + listRect.rect.height + PanelPad;

        if (needed > _authoredPanelH)
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x,
                                              Mathf.Min(MaxPanelH, needed));
    }
}
