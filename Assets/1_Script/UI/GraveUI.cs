using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// **무덤 창** — 누가 묻혀 있고, 꺼낼 것인가.
//
// 살펴보기 창(`InspectPanelUI`)은 *고른 것을 보는 자리*라 상태만 적는다. **조작은 여기다** —
// 그것이 이 저장소의 규약이고, 침대에 창이 없는 것은 배정이 곧 "누워라"라 조작이랄 게 없어서지
// 살펴보기 창이 조작을 겸해서가 아니다.
//
// **모양은 씬이 갖는다** — 이 스크립트가 채우는 것은 글자와 눌렀을 때뿐이다.
public class GraveUI : MonoBehaviour
{
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI bodyText;
    [SerializeField] Button          exhumeButton;
    [SerializeField] TextMeshProUGUI exhumeLabel;

    Grave _grave;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (exhumeButton != null) exhumeButton.onClick.AddListener(OnExhume);

        Grave.OnSelected                 += Open;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        Grave.OnSelected                 -= Open;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Open(Grave grave)
    {
        _grave = grave;
        WindowRaiser.Open(panel);
        Refresh();
    }

    public void Close()
    {
        _grave = null;
        WindowRaiser.Close(panel);
    }

    void OnExhume()
    {
        if (_grave == null) return;
        _grave.Exhume();
        Refresh();
    }

    // **매 프레임 안 그린다** — 여기 뜨는 값 중 빠르게 움직이는 것이 없다
    // (묻힌 몸은 시간이 멈춰 있어 아예 안 변한다).
    // **박자는 세계에 하나다**(`UiRefresh`) — 두 박(0.3초).
    int _beat = -1;

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }

        // 무덤이 철거되면 창도 닫는다 — 없는 것을 들여다보고 있을 이유가 없다.
        if (_grave == null) { Close(); return; }

        if (!UiRefresh.Due(ref _beat, 2)) return;
        Refresh();
    }

    void Refresh()
    {
        if (_grave == null) return;

        if (titleText != null) titleText.text = "무덤";

        var buried = _grave.Buried;

        if (bodyText != null)
            bodyText.text = buried == null
                ? "비어 있다.\n\n시체를 우클릭해 <color=#E8C566>[묻는다]</color>를 시키면 " +
                  "폰이 업어 와서 여기 묻는다."
                : $"<b>{buried.UnitName}</b>\n\n" +
                  $"<color=#888>{Butchering.StageName(buried.Stage)}인 채로 멈춰 있다 " +
                  $"({buried.AgeDays:F1}일).</color>\n" +
                  "<color=#888>묻힌 몸은 더 삭지 않는다. 지녔던 것도 함께 묻혀 있다.</color>";

        if (exhumeButton != null) exhumeButton.interactable = buried != null;
        if (exhumeLabel  != null) exhumeLabel.text = buried != null ? "파낸다" : "비어 있다";
    }
}
