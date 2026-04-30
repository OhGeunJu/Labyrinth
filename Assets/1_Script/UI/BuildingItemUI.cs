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
        if (btn != null) btn.onClick.AddListener(() => onClick());
    }

    // BuildModeUI가 매 프레임 색을 직접 설정
    public void SetColor(Color color)
    {
        if (background != null) background.color = color;
    }
}
