using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// **글자 위에 뜨는 설명 하나.** 스탯 창에서 스탯·특성에 마우스를 올리면 이게 뜬다.
//
// **`ItemInfoUI`와 같은 구조를 쓴다** — 이 컴포넌트는 **늘 켜져 있는 부모**에 살고, 보였다
// 사라지는 건 자식인 `panel`이다. 컴포넌트를 꺼지는 오브젝트에 붙이면 유니티가 그 `Awake`를
// 부르지 않아 `Instance`가 영영 null이 되고, **툴팁이 한 번도 안 뜬다**(그쪽에서 실제로
// 겪은 사고라 주석까지 남아 있다).
//
// `ItemInfoUI`를 그대로 쓰지 않은 이유는 그쪽이 **아이템 전용**이기 때문이다(아이콘·등급·
// 가격 줄). 스탯 설명을 거기 끼우면 아이템이 아닌 것에 아이템 창을 여는 꼴이 된다.
//
// 씬 생성: `DungeonColony → Create Hover Tooltip UI`
public class HoverTooltipUI : MonoBehaviour
{
    public static HoverTooltipUI Instance { get; private set; }

    [Header("씬 참조")]
    [Tooltip("실제로 보였다 사라지는 상자. 이 컴포넌트가 붙은 오브젝트가 아니라 **자식**이어야 한다")]
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI bodyText;

    [SerializeField] Vector2 cursorOffset = new(18f, -18f);

    RectTransform _rt;
    Canvas        _canvas;

    void Awake()
    {
        Instance = this;
        _canvas  = GetComponentInParent<Canvas>();

        if (panel == null)
        {
            Debug.LogError($"[HoverTooltipUI] {name}: panel이 비어 있어 툴팁이 뜨지 않습니다. " +
                           "DungeonColony → Create Hover Tooltip UI 를 실행하세요.");
            return;
        }

        _rt = panel.GetComponent<RectTransform>();
        panel.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ── 표시 ──────────────────────────────────────────────────────────────────

    public static void Show(string title, string body)
    {
        if (Instance != null) Instance.ShowInternal(title, body);
    }

    public static void Hide()
    {
        if (Instance != null && Instance.panel != null) Instance.panel.SetActive(false);
    }

    void ShowInternal(string title, string body)
    {
        if (panel == null) return;
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body)) { Hide(); return; }

        if (titleText != null)
        {
            titleText.text = title ?? "";
            titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(title));
        }
        if (bodyText != null)
        {
            bodyText.text = body ?? "";
            bodyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(body));
        }

        WindowRaiser.Open(panel);
        FollowCursor();
    }

    void LateUpdate()
    {
        if (panel != null && panel.activeSelf) FollowCursor();
    }

    // 커서 옆에 두되 **화면 밖으로는 안 나간다** — 끝에서 잘리면 설명이 반만 보인다.
    // 계산은 `ItemInfoUI.FollowCursor`와 같다(툴팁이 둘인데 위치 규칙이 다르면 안 된다).
    void FollowCursor()
    {
        if (_rt == null || _canvas == null || Mouse.current == null) return;

        Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        var    cRt = (RectTransform)_canvas.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cRt, Mouse.current.position.ReadValue(), cam, out var local)) return;

        Vector2 pos   = local + cursorOffset;
        Vector2 cSize = cRt.sizeDelta;
        Vector2 pSize = _rt.sizeDelta;
        pos.x = Mathf.Clamp(pos.x, -cSize.x * 0.5f, cSize.x * 0.5f - pSize.x);
        pos.y = Mathf.Clamp(pos.y, -cSize.y * 0.5f + pSize.y, cSize.y * 0.5f);
        _rt.anchoredPosition = pos;
    }
}
