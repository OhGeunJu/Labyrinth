using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

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

    // **창은 화면이 아니라 월드에 붙는다.** 화면 좌표로 한 번 놓고 말면 카메라를 움직이는 순간
    // 창이 화면에 붙어 따라다녀, 아까 우클릭한 그 자리를 가리키지 않게 된다.
    // 매 프레임 월드 좌표를 화면 좌표로 다시 풀어 붙인다.
    Vector3 _worldAnchor;
    Camera  _cam;

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
        if (!IsOpen) return;

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
    public void Open(string title, List<(string label, Action action)> options, Vector3 worldAnchor,
                     Camera cam = null)
    {
        if (panel == null || optionContainer == null || optionTemplate == null) return;
        if (options == null || options.Count == 0) { Close(); return; }

        foreach (var r in _rows) if (r != null) Destroy(r);
        _rows.Clear();

        if (titleText != null) titleText.text = title;

        foreach (var (label, action) in options)
        {
            var row = Instantiate(optionTemplate, optionContainer);
            row.gameObject.SetActive(true);

            var txt = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt != null) txt.text = label;

            // **동작이 없는 줄은 눌리지 않는다.** 지금 못 하는 일을 알려 주는 자리다
            // (`설득한다 (40% — 12초 뒤)`) — 빼 버리면 왜 안 되는지가 화면에서 사라지고,
            // 그대로 두면 눌러도 아무 일이 없는 줄이 된다.
            if (action == null)
            {
                row.interactable = false;
                if (txt != null) txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, 0.45f);
                _rows.Add(row.gameObject);
                continue;
            }

            var captured = action;   // 클로저 캡처 — 루프 변수를 그대로 쓰면 전부 마지막 것이 된다
            row.onClick.RemoveAllListeners();
            row.onClick.AddListener(() => { Close(); captured?.Invoke(); });

            _rows.Add(row.gameObject);
        }

        _worldAnchor = worldAnchor;
        _openedFrame = Time.frameCount;

        // **매번 다시 정한다 — 비워서 부르면 `Camera.main`으로 되돌린다.**
        //
        // 전에는 `if (cam != null)`이라 **월드맵이 한 번 쓰고 나면 그 카메라가 눌러앉았다.**
        // 필드로 돌아와 우클릭하면(`MoveController`는 `cam`을 안 넘긴다) 꺼진 월드 카메라로
        // 자리를 풀고, 아래 `LateUpdate`가 "그리던 카메라가 꺼졌다"며 **연 그 프레임에 닫았다** —
        // 그 뒤로 세션 내내 우클릭 창이 안 떴다.
        _cam = cam != null ? cam : Camera.main;
        panel.SetActive(true);

        // 크기는 여는 순간 한 번만 확정하면 된다(줄 수가 안 바뀐다).
        // 매 프레임 다시 계산하면 붙어 다니는 내내 레이아웃을 통째로 다시 짓는 셈이다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

        if (_cam == null) _cam = Camera.main;
        if (_cam != null) Place(_cam.WorldToScreenPoint(worldAnchor));
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
    }
}
