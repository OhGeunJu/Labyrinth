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

    // **당장 안 끈다 — 프레임 끝에 판정한다**(2026-08-29). 근거는 `HoverHold`에 있다:
    // 곧장 옆 줄로 옮기면 Enter와 Exit이 같은 프레임에 오는데 순서가 보장되지 않아, 즉시
    // 끄면 **방금 띄운 옆 줄의 툴팁을 지나온 줄이 꺼 버린다.**
    public static void Hide()
    {
        if (Instance != null) Instance._hold.HideAsked();
    }

    HoverHold _hold;

    void ShowInternal(string title, string body)
    {
        if (panel == null) return;
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body)) { Hide(); return; }

        if (titleText != null)
        {
            titleText.text = Josa.Fix(title) ?? "";
            titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(title));
        }
        if (bodyText != null)
        {
            bodyText.text = Josa.Fix(body) ?? "";
            bodyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(body));
        }

        WindowRaiser.Open(panel);
        _hold.Shown();
        FollowCursor();
    }

    void LateUpdate()
    {
        // **끄는 판정은 여기 한 번뿐이다** — 포인터 이벤트는 이 시점보다 먼저 다 왔으므로,
        // 이번 프레임에 띄운 적이 있는지를 그때 알 수 있다.
        if (_hold.ShouldHide()) WindowRaiser.Close(panel);

        if (panel != null && panel.activeSelf) FollowCursor();
    }

    // 커서 옆에 두되 화면 밖으로는 안 나간다 — **자리 계산은 `CursorTooltip` 한 벌이다.**
    //
    // 여기에 따로 적어 두었더니 *"`ItemInfoUI`와 같다"*고 주석까지 달아 놓고도 **뒤집기가
    // 빠져 있었다**: 화면 아래·오른쪽 끝에서 툴팁이 안으로 밀려 들어와 가리키던 것을 덮었다
    // (건설 목록 오른쪽 타일에 올리면 그 줄이 통째로 가려졌다).
    void FollowCursor() => CursorTooltip.Place(_rt, _canvas, cursorOffset);
}
