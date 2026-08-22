using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 우클릭 선택지 한 줄.
//
// **`WorldMenuOption`과 같은 모양이지만 사유를 다르게 보여 준다** — 도착 메뉴는 화면 왼쪽에
// 고정된 큰 판이라 사유를 같은 줄에 흐리게 덧붙일 자리가 있지만, 이 창은 커서 옆에 뜨는 작은
// 팝업이라 줄마다 설명을 달면 창이 부풀어 정작 고를 것을 가린다. **그래서 마우스를 올렸을 때만**
// 띄운다(`HoverTooltipUI`).
//
// **`(문구, 동작)` 튜플에서 암묵 변환된다.** 선택지를 넣는 자리가 서른 곳이 넘는데 사유가 붙는
// 것은 그중 몇뿐이라, 전부 `, null`을 달게 하면 읽는 값 없이 줄만 길어진다.
public struct PawnMenuOption
{
    public string Label;
    public string Note;      // 왜 못 하는가 — 마우스를 올리면 뜬다. 없으면 툴팁도 없다.
    public Action OnClick;   // null이면 눌리지 않는다

    public static implicit operator PawnMenuOption((string label, Action action) t) =>
        new() { Label = t.label, OnClick = t.action };

    // 못 하는 줄. **빼지 않고 흐리게 남기는 이유**: 없는 줄은 "이 상대에겐 원래 없는 기능"과
    // 구별이 안 된다 — 남겨 두면 "되긴 되는데 지금은 아니다"가 보이고, 이유는 올려 보면 나온다.
    public static PawnMenuOption Blocked(string label, string note) =>
        new() { Label = label, Note = note, OnClick = null };
}

// 유닛을 우클릭했을 때 마우스 자리에 뜨는 작은 선택지 창.
//
// **왜 필요했나**: 우클릭이 "적이면 공격"이라는 한 가지 뜻뿐이라, 상인처럼 때릴 일이 없는
// 중립 유닛도 우클릭하면 두들겨 팼다. 중립을 만나는 순간 우클릭은 뜻이 여러 개가 된다 —
// 거래할 수도, 따라갈 수도, (원한다면) 칠 수도 있다. 그 갈림길을 화면에 내놓는다.
//
// **선택지 구성은 여는 쪽(`MoveController`)이 정한다** — 월드맵 도착 메뉴(`WorldMenuUI`)와
// 같은 규칙이다. 여기서 대상 종류를 따지기 시작하면 판단이 두 곳으로 갈라진다.
public class PawnContextMenuUI : MonoBehaviour
{
    public static PawnContextMenuUI Instance { get; private set; }

    [Header("씬 참조")]
    [SerializeField] GameObject      panel;
    [SerializeField] RectTransform   panelRect;
    [SerializeField] TextMeshProUGUI titleText;
    [Tooltip("선택지 줄이 쌓일 부모 (세로 레이아웃)")]
    [SerializeField] Transform       optionContainer;
    [Tooltip("복제해서 쓸 선택지 줄 하나. 씬에 꺼둔 채로 둔다.")]
    [SerializeField] Button          optionTemplate;

    readonly List<GameObject> _rows = new();

    // 못 누르는 줄의 색. 채도를 뺀 어두운 회색 — 옆 줄과 **종류가 달라 보여야** 한다.
    static readonly Color DisabledColor = new(0.45f, 0.45f, 0.48f, 0.7f);

    // **창은 화면이 아니라 월드에 붙는다.** 화면 좌표로 한 번 놓고 말면 카메라를 움직이는 순간
    // 창이 화면에 붙어 따라다녀, 아까 우클릭한 그 자리를 가리키지 않게 된다.
    // 매 프레임 월드 좌표를 화면 좌표로 다시 풀어 붙인다.
    Vector3 _worldAnchor;
    Camera  _cam;

    // **화면에 붙는 경우도 있다** — UI에서 연 목록(상점 창의 "누가 흥정하나")은 가리킬 월드
    // 지점이 없다. 그때는 놓은 자리에 그대로 둔다: 붙어 있어야 할 것(누른 그 글자)이 이미
    // 화면 붙박이라, 카메라를 따라 움직이면 오히려 어긋난다.
    bool _screenAnchored;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;
        if (panel != null) panel.SetActive(false);
        if (optionTemplate != null) optionTemplate.gameObject.SetActive(false);
    }

    // 카메라가 움직인 **뒤에** 따라붙어야 한 프레임 늦게 흔들리지 않는다.
    void LateUpdate()
    {
        if (!IsOpen || _screenAnchored) return;

        // **그리던 카메라가 꺼지면 닫는다.** 필드↔월드를 오갈 때 반대편 카메라가 꺼지는데,
        // 그대로 두면 없어진 시점 기준으로 좌표를 풀어 창이 엉뚱한 자리에 붙는다.
        if (_cam != null && !_cam.isActiveAndEnabled) { Close(); return; }
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 sp = _cam.WorldToScreenPoint(_worldAnchor);

        // 카메라 뒤로 넘어가면 화면에 그릴 자리가 없다(뒤집힌 좌표가 나온다) → 닫는다.
        if (sp.z <= 0f) { Close(); return; }

        Place(sp);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // **연 그 프레임의 클릭은 무시한다.**
    //
    // 이 창은 우클릭으로 열리는데, 아래 "밖을 누르면 닫는다"가 같은 프레임의 그 우클릭을
    // 그대로 집어 즉시 닫아 버린다. 필드에서 멀쩡했던 건 `MoveController`와 실행 순서가
    // 우연히 맞아서였다 — 창이 먼저 돌아 아직 안 열린 상태로 클릭을 흘려보냈다.
    // `WorldMapView`는 -40이라 순서가 뒤집혀 그 우연이 깨진다. 순서에 기대지 않고 못 박는다.
    int _openedFrame = -1;

    void Update()
    {
        if (!IsOpen || Time.frameCount == _openedFrame) return;

        // Esc 또는 **메뉴 밖 클릭**으로 닫는다. 좌우 어느 버튼이든 닫는다 —
        // 열어 둔 채로 다른 조작을 하면 그 조작이 메뉴 뒤에서 일어나 혼란스럽다.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }

        bool clicked = (Mouse.current.leftButton.wasPressedThisFrame ||
                        Mouse.current.rightButton.wasPressedThisFrame);
        if (clicked && !PointerOverMenu()) Close();
    }

    // 메뉴 위에서 눌렀는지. UI 위가 아니면 무조건 밖이고, UI 위라면 그게 이 메뉴인지 본다
    // (다른 패널 위를 눌렀는데 메뉴가 안 닫히면 그것도 이상하다).
    bool PointerOverMenu()
    {
        if (EventSystem.current == null || panelRect == null) return false;
        if (!EventSystem.current.IsPointerOverGameObject()) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(
            panelRect, Mouse.current.position.ReadValue(), null);
    }

    // title = 무엇에 대한 명령인가. options = (문구, 눌렀을 때).
    // worldAnchor = **우클릭한 월드 지점.** 화면 좌표가 아니다 — 카메라를 움직여도 그 자리에 남는다.
    //
    // `cam`은 그 월드 지점을 보는 카메라. **월드맵도 이 창을 쓴다** — 거기선 `Camera.main`이
    // 필드 카메라(꺼져 있다)라 그대로 두면 좌표를 못 푼다. 비우면 `Camera.main`.
    public void Open(string title, List<PawnMenuOption> options, Vector3 worldAnchor,
                     Camera cam = null)
    {
        if (!Build(title, options)) return;

        _screenAnchored = false;
        _worldAnchor    = worldAnchor;

        // **매번 다시 정한다 — 비워서 부르면 `Camera.main`으로 되돌린다.**
        //
        // 전에는 `if (cam != null)`이라 **월드맵이 한 번 쓰고 나면 그 카메라가 눌러앉았다.**
        // 필드로 돌아와 우클릭하면(`MoveController`는 `cam`을 안 넘긴다) 꺼진 월드 카메라로
        // 자리를 풀고, `LateUpdate`가 "그리던 카메라가 꺼졌다"며 **연 그 프레임에 닫았다** —
        // 그 뒤로 세션 내내 우클릭 창이 안 떴다.
        _cam = cam != null ? cam : Camera.main;
        Show();

        if (_cam == null) _cam = Camera.main;
        if (_cam != null) Place(_cam.WorldToScreenPoint(worldAnchor));
    }

    // **화면 좌표에 그대로 놓는 진입점.** UI에서 연 목록용 — 가리킬 월드 지점이 없다.
    //
    // 창을 새로 만들지 않고 이걸 쓰는 이유: 줄 모양·못 누르는 줄·설명 툴팁·밖을 누르면
    // 닫힘이 이미 여기 다 있다. 목록을 띄우는 규칙이 두 벌이 되면 한쪽이 반드시 뒤처진다.
    public void OpenAtScreen(string title, List<PawnMenuOption> options, Vector2 screenPos)
    {
        if (!Build(title, options)) return;

        _screenAnchored = true;
        _cam            = null;
        Show();
        Place(screenPos);
    }

    void Show()
    {
        _openedFrame = Time.frameCount;
        WindowRaiser.Open(panel);

        // **맨 앞으로 올린다** — 상점 창처럼 나중에 그려지는 패널 위에서 열릴 수 있는데,
        // 형제 순서가 뒤면 목록이 그 뒤에 숨어 "눌러도 아무 일이 없다"가 된다.
        panel.transform.SetAsLastSibling();

        // 크기는 여는 순간 한 번만 확정하면 된다(줄 수가 안 바뀐다).
        // 매 프레임 다시 계산하면 붙어 다니는 내내 레이아웃을 통째로 다시 짓는 셈이다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    // 줄을 짓는다. 띄울 것이 없으면 false(그리고 닫는다).
    bool Build(string title, List<PawnMenuOption> options)
    {
        if (panel == null || optionContainer == null || optionTemplate == null) return false;
        if (options == null || options.Count == 0) { Close(); return false; }

        foreach (var r in _rows) if (r != null) Destroy(r);
        _rows.Clear();

        if (titleText != null) titleText.text = Josa.Fix(title);

        foreach (var opt in options)
        {
            var row = Instantiate(optionTemplate, optionContainer);
            row.gameObject.SetActive(true);

            var txt = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt != null) txt.text = Josa.Fix(opt.Label);

            // **사유가 있으면 마우스를 올렸을 때 뜬다.** 되는 줄에도 붙을 수 있다 — 지금은
            // 못 하는 줄에만 쓰지만, "왜 이걸 고르나"를 적을 자리가 같은 곳이어야 나중에
            // 설명이 두 벌로 갈리지 않는다.
            if (!string.IsNullOrEmpty(opt.Note))
                row.gameObject.AddComponent<MenuRowTooltip>().Set(opt.Label, opt.Note);

            // **동작이 없는 줄은 눌리지 않는다.** 지금 못 하는 일을 알려 주는 자리다
            // (`설득한다 (40% — 12초 뒤)`) — 빼 버리면 왜 안 되는지가 화면에서 사라지고,
            // 그대로 두면 눌러도 아무 일이 없는 줄이 된다.
            if (opt.OnClick == null)
            {
                row.interactable = false;

                // **투명도만 낮추면 "글자가 좀 흐리네"로 읽힌다.** 못 누르는 줄이라는 것이
                // 한눈에 와야 하므로 **색까지 죽인다** — 채도를 뺀 회색으로 눌러 두면
                // 옆 줄과 나란히 놓였을 때 종류가 다른 것으로 보인다.
                if (txt != null) txt.color = DisabledColor;

                _rows.Add(row.gameObject);
                continue;
            }

            var captured = opt.OnClick;   // 클로저 캡처 — 루프 변수를 그대로 쓰면 전부 마지막 것이 된다
            row.onClick.RemoveAllListeners();
            row.onClick.AddListener(() => { Close(); captured?.Invoke(); });

            _rows.Add(row.gameObject);
        }

        return true;
    }

    // 마우스 자리에 놓되 **화면 밖으로 안 나가게** 민다. 화면 가장자리에서 유닛을 우클릭하면
    // 메뉴가 반쯤 잘려 선택지를 못 누른다.
    void Place(Vector2 screenPos)
    {
        if (panelRect == null) return;

        var canvas = panelRect.GetComponentInParent<Canvas>();
        var parent = panelRect.parent as RectTransform;
        if (canvas == null || parent == null) return;

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, cam, out var local))
            return;

        // 피벗이 좌상단이라 기본은 커서 오른쪽-아래로 펼쳐진다.
        Vector2 size = panelRect.rect.size;
        Rect    area = parent.rect;

        if (local.x + size.x > area.xMax) local.x -= size.x;   // 오른쪽이 모자라면 왼쪽으로
        if (local.y - size.y < area.yMin) local.y += size.y;   // 아래가 모자라면 위로

        // **`anchoredPosition`은 부모 로컬 좌표가 아니라 앵커점 기준이다.**
        // 부모 로컬은 부모 피벗(대개 한가운데)이 원점이고, 이 창의 앵커는 좌상단이라
        // 변환 없이 넣으면 화면 절반만큼 어긋난다.
        Vector2 anchorRef = new Vector2(
            Mathf.Lerp(area.xMin, area.xMax, panelRect.anchorMin.x),
            Mathf.Lerp(area.yMin, area.yMax, panelRect.anchorMin.y));

        panelRect.anchoredPosition = local - anchorRef;
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);

        // **닫을 때 툴팁도 거둔다.** 줄이 사라지면 `OnPointerExit`는 **오지 않는다** —
        // 커서를 올린 채 고르면 창은 닫히는데 설명만 화면에 남는다(`ItemInfoUI`가 격자
        // 아이콘이 파괴될 때 정확히 그래서 툴팁이 영영 남았다).
        HoverTooltipUI.Hide();
    }
}

// 선택지 줄 위에 커서가 올라오면 사유를 띄운다.
//
// **`Button.interactable`이 꺼져 있어도 포인터 이벤트는 온다** — 그게 이 컴포넌트가 성립하는
// 이유다. `interactable`은 버튼 제 클릭·상태 전환만 막지, 같은 오브젝트의 다른 핸들러까지
// 막지는 않는다. 그래서 **못 누르는 줄에서 오히려 설명이 필요한** 이 경우가 그대로 된다.
public class MenuRowTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    string _title, _body;

    public void Set(string title, string body) { _title = title; _body = body; }

    public void OnPointerEnter(PointerEventData e) => HoverTooltipUI.Show(_title, _body);
    public void OnPointerExit(PointerEventData e)  => HoverTooltipUI.Hide();

    // 줄이 파괴되면 `OnPointerExit`가 안 온다 — 창이 닫히는 길이 `Close()`뿐이 아니므로
    // (선택지를 고르면 줄이 통째로 다시 만들어진다) 여기서도 거둔다.
    void OnDisable() => HoverTooltipUI.Hide();
}
