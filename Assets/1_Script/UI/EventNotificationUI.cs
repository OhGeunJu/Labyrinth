using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 이벤트 발생 시 화면 상단에 팝업으로 알림 표시.
// 인스펙터에서 panel, iconImage, titleText, descText 연결.
//
// **코루틴을 쓰지 않는다.** 구독은 `Awake`에서 하는 static 이벤트라 이 GameObject가 꺼져 있어도
// `Show`가 불리는데, 꺼진 오브젝트에서는 `StartCoroutine`이 예외를 던진다
// ("Coroutine couldn't be started because the game object is inactive").
// (옛 3D 월드맵 시절) 지도를 보는 동안 필드 UI가 통째로 꺼지고
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
    // 이벤트 데이터가 아닌 **완성된 문장**으로 밀린 알림(스폰 방향 등). 데이터보다 자세하므로
    // 둘 다 밀려 있으면 이쪽이 이긴다 — 원정 중 습격이야말로 방향이 가장 아쉬운 경우다.
    string _pendingTitle, _pendingText;
    float _closeAt;

    // 게임 이벤트가 아닌 **조작 거절**도 여기로 알린다(잠긴 물건을 팔려 할 때 등).
    // 알림 창을 새로 만들 이유가 없다 — 플레이어는 이미 여기를 본다.
    //
    // `Awake`에서 잡아 두고 **찾지 않는다.** 이 컴포넌트는 늘 켜져 있는 오브젝트에 살고
    // (꺼지는 건 자식 `panel`이다) 알림은 자주 불릴 수 있는 경로다.
    static EventNotificationUI _instance;

    // `holdWhenHidden` — 띄울 곳이 없을 때(월드맵이라 필드 UI가 통째로 꺼진 동안) 들고
    // 기다릴 것인가. **거점에서 벌어진 사건은 참, 조작에 대한 대답은 거짓이다.**
    // "골드가 모자랍니다"는 그 순간의 대답이라 한참 뒤 거점에 돌아왔을 때 뜨면 영문 모를
    // 소음이 되지만, 원정 중에 온 습격은 놓치면 "왜 거점이 털렸지"가 된다.
    public static void Notify(string title, string message, bool holdWhenHidden = false)
    {
        if (_instance != null && _instance.isActiveAndEnabled)
        {
            _instance.Display(title, message, null);
            return;
        }

        if (holdWhenHidden && _instance != null)
        {
            _instance._pendingTitle = title;
            _instance._pendingText  = message;
        }
        Debug.Log($"[알림] {title} — {message}");   // 못 띄웠어도 흔적은 남긴다
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

    // 필드로 돌아와 다시 켜지는 순간 밀린 알림을 띄운다. 문장이 밀려 있으면 그쪽이 자세하다.
    void OnEnable()
    {
        if (_pendingText != null) Display(_pendingTitle, _pendingText, null);
        else if (_pending != null) Display(_pending);
    }

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
        _pending     = null;
        _pendingText = null;
        _pendingTitle = null;
        if (panel == null) return;

        // **할 말이 없으면 안 띄운다.** 제목도 설명도 비면 배경만 남아 화면에 **작은 검은 상자**가
        // 뜬다 — 무슨 일이 일어났는지도, 왜 떴는지도 알 수 없다. 실제로 이벤트 에셋이 빈 채로
        // 발동해 그 상자만 보였다(2026-08-06). 데이터를 채워도 언젠가 또 빈 것이 들어오므로
        // 창 쪽에서 막고, 대신 **무엇이 비었는지 콘솔에 남긴다**.
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(desc))
        {
            Debug.LogWarning("[알림] 제목도 설명도 비어 있어 띄우지 않습니다 — " +
                             "`GameEventData`의 EventName/Description을 채우세요.");
            return;
        }

        if (iconImage != null) { iconImage.sprite = icon; iconImage.enabled = icon != null; }
        if (titleText != null) titleText.text = Josa.Fix(title);
        if (descText  != null) descText.text  = Josa.Fix(desc);

        WindowRaiser.Open(panel);
        // 배속(`Time.timeScale`)을 타지 않는 실제 시간. 10배속에서 0.5초 만에 사라지거나
        // 일시정지 중에 영영 안 닫히는 일이 없게.
        _closeAt = Time.unscaledTime + displayDuration;
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf || Time.unscaledTime < _closeAt) return;

        // **닫기를 한 번만 건다.** 흐려지는 동안 `panel`은 아직 켜져 있어서, 안 막으면 매
        // 프레임 새 페이드가 시작되어 **알파가 제자리걸음하며 영영 안 닫힌다.**
        _closeAt = float.PositiveInfinity;
        WindowRaiser.Close(panel);
    }

    public void Close()
    {
        _pending      = null;
        _pendingText  = null;
        _pendingTitle = null;
        _closeAt      = float.PositiveInfinity;
        WindowRaiser.Close(panel);
    }
}
