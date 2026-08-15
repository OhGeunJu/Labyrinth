using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// **화장터 창** — 얼마나 탔고, 왜 안 타고, 무를 것인가.
//
// 무덤 창과 나란한 물건이다. 갈리는 것은 **게이지가 있다는 것** 하나 — 화장은 시간이 걸리는
// 일이라 "지금 어디쯤인가"를 볼 자리가 있어야 하고, 연료가 없어 멈췄을 때 그 이유를 적을
// 자리도 여기다(멈춘 게이지만 보면 고장으로 읽힌다).
public class PyreUI : MonoBehaviour
{
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI bodyText;

    [Header("진행")]
    [SerializeField] Image           progressFill;
    [SerializeField] TextMeshProUGUI progressText;

    [Header("동작")]
    [SerializeField] Button          ejectButton;
    [SerializeField] TextMeshProUGUI ejectLabel;

    static readonly Color Burning = new(0.88f, 0.49f, 0.24f);
    static readonly Color Stalled = new(0.55f, 0.42f, 0.20f);

    Pyre _pyre;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (ejectButton != null) ejectButton.onClick.AddListener(OnEject);

        Pyre.OnSelected                  += Open;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        Pyre.OnSelected                  -= Open;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Open(Pyre pyre)
    {
        _pyre = pyre;
        if (panel != null) panel.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        _pyre = null;
        if (panel != null) panel.SetActive(false);
    }

    void OnEject()
    {
        if (_pyre == null) return;
        _pyre.Eject();
        Refresh();
    }

    // 게이지가 도는 창이라 자주 그린다 — 그래도 매 프레임은 아니다.
    [SerializeField] float refreshInterval = 0.1f;
    float _timer;

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (_pyre == null) { Close(); return; }

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;
        _timer = refreshInterval;
        Refresh();
    }

    void Refresh()
    {
        if (_pyre == null) return;

        if (titleText != null) titleText.text = "화장터";

        var who     = _pyre.Burning;
        bool idle   = who == null;
        bool halted = _pyre.BlockedReason != null;

        if (bodyText != null)
            bodyText.text = idle
                ? "비어 있다.\n\n시체를 우클릭해 <color=#E8C566>[태운다]</color>를 시키면 " +
                  "폰이 업어 와서 여기 올린다.\n" +
                  "<color=#888>유품은 안 태운다 — 발밑에 내려놓는다.</color>"
                : $"<b>{who.UnitName}</b>\n\n" +
                  (halted ? $"<color=#F90>{_pyre.BlockedReason}</color>\n"
                          : "<color=#888>타고 있다. 다 타면 아무것도 남지 않는다.</color>\n") +
                  "<color=#888>되돌릴 수 없다 — 다 타기 전에만 내릴 수 있다.</color>";

        float p = idle ? 0f : _pyre.Progress;
        if (progressFill != null)
        {
            progressFill.fillAmount = p;
            progressFill.color      = halted ? Stalled : Burning;
        }
        if (progressText != null)
            progressText.text = idle ? "" : $"{p * 100f:F0}%";

        if (ejectButton != null) ejectButton.interactable = !idle;
        if (ejectLabel  != null) ejectLabel.text = idle ? "비어 있다" : "내린다";
    }
}
