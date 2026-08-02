using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BuildModeUI의 건물 버튼 프리팹에 붙이는 스크립트.
// 프리팹 구조 예시:
//   Root (Image, Button, BuildingItemUI)
//   ├── Icon  (Image)
//   ├── NameLabel  (TextMeshProUGUI)
//   └── CostLabel  (TextMeshProUGUI)
public class BuildingItemUI : MonoBehaviour
{
    [SerializeField] Image           background;
    [SerializeField] Image           icon;
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI costLabel;

    public Image Background => background;

    public void Setup(string name, string cost, System.Action onClick)
    {
        if (nameLabel != null) nameLabel.text = name;

        if (costLabel != null)
        {
            costLabel.text = cost;
            costLabel.gameObject.SetActive(!string.IsNullOrEmpty(cost));
        }

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

    // BuildModeUI가 매 프레임 색을 직접 설정
    public void SetColor(Color color)
    {
        if (background != null) background.color = color;
    }
}
