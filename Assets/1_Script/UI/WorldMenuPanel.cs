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
public class WorldMenuPanel : MonoBehaviour
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
        if (panel == null) { Debug.LogWarning("[WorldMenuPanel] panel 미연결"); enabled = false; return; }

        // 템플릿은 원본이라 화면에 보이면 안 된다 — 껐다 켜는 건 복제본만.
        if (optionTemplate != null) optionTemplate.gameObject.SetActive(false);

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
        panel.SetActive(true);
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
        foreach (var b in _spawned) if (b != null) Destroy(b.gameObject);
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
    }
}
