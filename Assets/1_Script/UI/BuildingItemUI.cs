using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// BuildModeUI의 건물 버튼 프리팹에 붙이는 스크립트.
// 프리팹 구조 예시:
//   Root (Image, Button, BuildingItemUI)
//   ├── Icon  (Image)
//   ├── NameLabel  (TextMeshProUGUI)
//   └── CostLabel  (TextMeshProUGUI, 지금은 안 쓴다 — 아래)
//
// **타일에는 그림과 이름만 둔다.** 비용도 잠김 사유도 한때 타일에 적혀 있었는데, 92픽셀
// 폭에 `[잠김] 목공술 연구 필요`를 넣으면 **글자가 잘려 무엇이 필요한지 못 읽는다.** 게다가
// 목록을 훑을 때 알고 싶은 것은 *무엇이 있나*지 *얼마인가*가 아니다.
//
// 그래서 **자세한 것은 마우스를 올렸을 때**(`HoverTooltipUI` — 스탯·특성이 쓰는 그 창)로
// 옮겼다. 고를 때 한 번만 보면 되는 정보는 늘 떠 있을 이유가 없다.
public class BuildingItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image           background;
    [SerializeField] Image           icon;
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI costLabel;

    string              _tipTitle;
    System.Func<string> _tipBody;
    bool                _hovering;

    // **설명은 글자가 아니라 함수로 받는다.** 거기엔 *지금 가진 재료*가 들어가는데, 목록을
    // 지을 때 한 번 구워 두면 창고가 바뀌어도 안 따라온다 — 올릴 때마다 새로 짓는다.
    // **지금 무슨 이름인가** — 같은 것을 매 프레임 다시 걸지 않으려고 부르는 쪽이 본다.
    // `Setup`은 툴팁 델리게이트까지 새로 거는 자리라 값이 싸지 않다.
    public string CurrentName { get; private set; }

    // 이름은 같은데 **누를 때 부르는 곳이 달라졌을 때** 쓴다(모드 전환).
    public void ForgetName() => CurrentName = null;

    public void Setup(string name, System.Func<string> tooltip, System.Action onClick)
    {
        CurrentName = name;
        if (nameLabel != null) nameLabel.text = name;

        _tipTitle = name;
        _tipBody  = tooltip;

        // 비용 칸은 **끈다** — 프리팹에는 남겨 두되(자리를 되살릴 수 있게) 안 쓴다.
        if (costLabel != null) costLabel.gameObject.SetActive(false);

        var btn = GetComponent<Button>();
        if (btn == null) return;

        btn.onClick.RemoveAllListeners();

        // **`onClick`이 없으면 리스너를 안 건다.** 잠긴 건물이 그렇다 —
        // 그냥 `() => onClick()`으로 감싸면 누르는 순간 널 참조로 터진다.
        //
        // `interactable = false`로 막지 않는 이유: 버튼의 색 전환이 배경을 덮어써
        // `SetColor`로 칠한 잠김 색이 안 보이게 된다. 안 눌리는 것으로 충분하다.
        if (onClick != null) btn.onClick.AddListener(() => onClick());
    }

    // **이름 글자색.** 배경이 꺼진 타일에서 *지금 고른 것*을 말할 유일한 자리다 —
    // 아이콘을 물들이면 나중에 진짜 그림이 들어왔을 때 그 그림에 색이 곱해진다.
    public void SetLabelColor(Color color)
    {
        if (nameLabel != null) nameLabel.color = color;
    }

    // BuildModeUI가 매 프레임 색을 직접 설정
    public void SetColor(Color color)
    {
        if (background != null) background.color = color;
    }

    public void OnPointerEnter(PointerEventData _)
    {
        _hovering = true;
        HoverTooltipUI.Show(_tipTitle, _tipBody != null ? _tipBody() : null);
    }

    public void OnPointerExit(PointerEventData _)
    {
        _hovering = false;
        HoverTooltipUI.Hide();
    }

    // **`OnPointerExit`는 오브젝트가 파괴되면 안 온다.** 갈래를 바꾸면 타일이 통째로 다시
    // 지어지므로, 커서를 올려 둔 채로 갈래를 누르면 설명만 화면에 남는다
    // (`ItemInfoUI`가 겪은 그 자리).
    void OnDisable()
    {
        if (!_hovering) return;
        _hovering = false;
        HoverTooltipUI.Hide();
    }
}
