using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 이벤트 발생 시 화면 상단에 팝업으로 알림 표시.
// 인스펙터에서 panel, iconImage, titleText, descText 연결.
//
// **코루틴을 쓰지 않는다.** 구독은 `Awake`에서 하는 static 이벤트라 이 GameObject가 꺼져 있어도
// `Show`가 불리는데, 꺼진 오브젝트에서는 `StartCoroutine`이 예외를 던진다
// ("Coroutine couldn't be started because the game object is inactive").
// 월드맵을 보는 동안 필드 UI가 통째로 꺼지고(`GameModeManager.ApplyFieldUi`)
// 이벤트는 그때도 계속 돌기 때문에 정상적으로 벌어지는 상황이다.
public class EventNotificationUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI descText;
    [Tooltip("자동으로 닫히기까지 대기 시간(초). 배속과 무관한 실제 시간.")]
    [SerializeField] float displayDuration = 5f;

    // 꺼져 있는 동안 들어온 알림. 돌아왔을 때 띄운다 —
    // 원정 중 습격을 놓치면 "왜 거점이 털렸지"가 된다.
    GameEventData _pending;
    float _closeAt;

    // 게임 이벤트가 아닌 **조작 거절**도 여기로 알린다(잠긴 물건을 팔려 할 때 등).
    // 알림 창을 새로 만들 이유가 없다 — 플레이어는 이미 여기를 본다.
    //
    // `Awake`에서 잡아 두고 **찾지 않는다.** 이 컴포넌트는 늘 켜져 있는 오브젝트에 살고
    // (꺼지는 건 자식 `panel`이다) 알림은 자주 불릴 수 있는 경로다.
    static EventNotificationUI _instance;

    public static void Notify(string title, string message)
    {
        if (_instance != null && _instance.isActiveAndEnabled)
        {
            _instance.Display(title, message, null);
            return;
        }

        // 띄울 곳이 없으면(월드맵이라 필드 UI가 통째로 꺼진 동안 등) 흔적은 남긴다.
        Debug.Log($"[알림] {title} — {message}");
    }

    void Awake()
    {
        _instance = this;
        if (panel != null) panel.SetActive(false);
        EventManager.OnEventTriggered += Show;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        EventManager.OnEventTriggered -= Show;
    }

    // 필드로 돌아와 다시 켜지는 순간 밀린 알림을 띄운다.
    void OnEnable() { if (_pending != null) Display(_pending); }

    void Show(GameEventData data)
    {
        if (data == null) return;

        // 꺼져 있으면 지금 띄울 수 없다 — 최신 것 하나만 들고 기다린다.
        // (여러 개를 쌓아 두면 돌아오자마자 팝업이 줄줄이 지나간다)
        if (!isActiveAndEnabled) { _pending = data; return; }

        Display(data);
    }

    void Display(GameEventData data)
        => Display(data.EventName, data.Description, data.Icon);

    void Display(string title, string desc, Sprite icon)
    {
        _pending = null;
        if (panel == null) return;

        if (iconImage != null) { iconImage.sprite = icon; iconImage.enabled = icon != null; }
        if (titleText != null) titleText.text = title;
        if (descText  != null) descText.text  = desc;

        panel.SetActive(true);
        // 배속(`Time.timeScale`)을 타지 않는 실제 시간. 10배속에서 0.5초 만에 사라지거나
        // 일시정지 중에 영영 안 닫히는 일이 없게.
        _closeAt = Time.unscaledTime + displayDuration;
    }

    void Update()
    {
        if (panel != null && panel.activeSelf && Time.unscaledTime >= _closeAt)
            panel.SetActive(false);
    }

    public void Close()
    {
        _pending = null;
        if (panel != null) panel.SetActive(false);
    }
}
