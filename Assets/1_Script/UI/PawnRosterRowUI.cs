using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 폰 로스터의 한 줄: [초상화][이름][현재 상태]
//
// **모양은 코드가 아니라 씬에 있는 템플릿 오브젝트가 갖는다.**
// 편집 중에 보이는 그 행을 런타임에 그대로 복제해 쓴다 — 미리보기와 실제가 어긋날 수 없고,
// 여백·폰트·색을 인스펙터에서 바꾸면 그대로 게임에 반영된다.
// 이 컴포넌트는 데이터 연결(이름·초상화·상태·클릭)만 담당한다.
public class PawnRosterRowUI : MonoBehaviour,
    IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler
{
    [Header("요소")]
    [SerializeField] Image background;
    [SerializeField] Image portraitImage;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] Button selectButton;

    [Header("색")]
    [SerializeField] Color normalColor     = new Color(1f, 1f, 1f, 0.04f);
    [SerializeField] Color selectedColor   = new Color(0.30f, 0.45f, 0.62f, 0.60f);
    [SerializeField] Color wandererName    = Color.white;
    [SerializeField] Color minionName      = new Color(0.65f, 0.80f, 0.95f);
    [Tooltip("초상화가 없을 때 자리 표시 색")]
    [SerializeField] Color emptyPortrait   = new Color(1f, 1f, 1f, 0.12f);

    UnitBrain _brain;

    public UnitBrain Brain => _brain;

    // ── 포인터 이벤트 ─────────────────────────────────────────────────────
    // 선택 방식(단일/Ctrl 추가/드래그 범위)을 정하는 건 **패널**이다.
    // 행은 "눌렸다 / 지나갔다 / 뗐다"만 알린다 — 한 행이 제 이웃을 알 리 없으므로
    // 범위 계산은 목록을 들고 있는 쪽이 해야 한다.
    public event Action<PawnRosterRowUI, bool> RowPointerDown;   // (행, Ctrl 눌림)
    public event Action<PawnRosterRowUI>       RowPointerEnter;
    public event Action                        RowPointerUp;

    // 더블클릭 판정을 **직접 잰다.** `PointerEventData.clickCount`는 입력 모듈에 따라
    // 안 올라오는 경우가 있어(New Input System UI 모듈) 믿을 수 없었다.
    // 행 단위가 아니라 **정적**으로 들고 있어야 한다 — 다른 행을 연달아 눌렀을 때
    // 더블클릭으로 오인하지 않으려면 "직전에 누른 행"까지 같이 봐야 하기 때문이다.
    const float DoubleClickSeconds = 0.35f;
    static PawnRosterRowUI _lastClicked;
    static float           _lastClickTime;

    public void OnPointerDown(PointerEventData e)
    {
        bool doubleClick = _lastClicked == this &&
                           Time.unscaledTime - _lastClickTime <= DoubleClickSeconds;
        _lastClicked   = this;
        _lastClickTime = Time.unscaledTime;

        // **더블클릭 = 그 폰을 화면에 비춘다.** 목록에서 골라도 화면 밖에 있으면
        // 어디 있는지 알 수가 없다. 첫 클릭은 평소대로 선택되므로 "고르면서 찾아간다"가 된다.
        if (doubleClick) { FocusCamera(); return; }

        bool ctrl = Keyboard.current != null &&
                    (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
        RowPointerDown?.Invoke(this, ctrl);
    }

    void FocusCamera()
    {
        if (_brain == null) return;

        // 켜져 있는 카메라를 고른다 — 씬에 필드·월드 둘이 있다.
        var cam = CameraController.Active;
        if (cam == null)
        {
            // 조용히 아무 일도 안 일어나면 "더블클릭이 안 먹는다"로 보인다 — 이유를 남긴다.
            Debug.LogWarning("[PawnRoster] 켜져 있는 CameraController가 없어 폰을 비추지 못했습니다.");
            return;
        }
        cam.FocusOn(_brain.transform.position);
    }

    public void OnPointerEnter(PointerEventData e) => RowPointerEnter?.Invoke(this);
    public void OnPointerUp(PointerEventData e)    => RowPointerUp?.Invoke();

    // 드래그를 **여기서 붙잡는다.** 안 그러면 목록을 감싼 ScrollRect가 드래그를 가져가
    // 범위 선택 대신 목록이 스크롤된다(내용이 커서 밑에서 움직여 엉뚱한 행이 잡힌다).
    // 실제 범위 계산은 PointerEnter가 하므로 여기서는 할 일이 없다 — 소유권만 가져온다.
    // 스크롤은 휠로 한다.
    public void OnBeginDrag(PointerEventData e) { }
    public void OnDrag(PointerEventData e)      { }

    public void Bind(UnitBrain brain)
    {
        _brain = brain;

        if (nameText != null)
        {
            nameText.text  = brain.UnitName;
            nameText.color = brain.Data is MinionData ? minionName : wandererName;
        }

        if (portraitImage != null)
        {
            var sprite = brain.EnsurePortrait();
            if (sprite == null && brain.Data != null) sprite = brain.Data.Icon;
            portraitImage.sprite = sprite;
            portraitImage.color  = sprite != null ? Color.white : emptyPortrait;
        }

        // 선택은 위의 포인터 이벤트가 담당한다. 버튼이 남아 있으면 클릭이 **두 번** 처리되고
        // (Button이 pointerDown을 먼저 먹어) 행 쪽 핸들러가 아예 안 불리기도 한다.
        // 강조 색은 어차피 Refresh가 직접 칠하므로 버튼의 시각 효과도 필요 없다.
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.enabled = false;
        }

        Refresh(false);
    }

    public void Refresh(bool selected)
    {
        if (_brain == null) return;

        if (statusText != null)
        {
            string status = PawnStatusText.Describe(_brain);
            statusText.text  = status;
            statusText.color = PawnStatusText.ColorOf(status);
        }

        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
}
