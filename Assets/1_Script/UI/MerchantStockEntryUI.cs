using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MerchantStockEntryUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // 격자 보기 칸 크기. **타일 프리팹과 `MerchantUI`의 GridLayoutGroup이 같은 값을 봐야 한다** —
    // 어긋나면 격자가 타일을 늘이거나 줄여 아이콘이 찌그러진다. 그래서 여기 한 곳에만 적는다
    // (생성기도 이 값을 읽어 프리팹을 짓는다).
    public const int TileWidth  = 96;
    public const int TileHeight = 112;

    [SerializeField] Image           bg;
    [SerializeField] Image           icon;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI qualityText;
    [SerializeField] TextMeshProUGUI priceText;
    [SerializeField] TextMeshProUGUI stockText;
    [SerializeField] Button          button;

    [SerializeField] Color normalColor   = new(0.18f, 0.18f, 0.18f, 0.95f);
    [SerializeField] Color selectedColor = new(0.25f, 0.40f, 0.25f, 0.95f);
    [SerializeField] Color soldOutColor  = new(0.35f, 0.20f, 0.20f, 0.95f);

    public event Action<MerchantStockEntry> OnSelected;
    public event Action<MerchantStockEntry> OnDoubleClicked;

    MerchantStockEntry _entry;

    public void Setup(MerchantStockEntry entry)
    {
        _entry = entry;

        if (icon)
        {
            icon.sprite = entry.Item?.Icon;
            icon.color  = entry.Item?.Icon != null ? Color.white : new Color(1, 1, 1, 0);
        }

        if (nameText)    nameText.text    = entry.Item?.ItemName ?? "";
        if (qualityText)
        {
            qualityText.text  = entry.Quality.DisplayName();
            qualityText.color = ItemInfoUI.QualityColor(entry.Quality);
        }
        if (priceText)   priceText.text   = $"{entry.Price} G";

        RefreshStock();

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnSelected?.Invoke(_entry));
        }

        SetSelected(false);
    }

    public void RefreshStock()
    {
        if (stockText) stockText.text = $"x{_entry.Stock}";
        if (bg) bg.color = _entry.Stock <= 0 ? soldOutColor : normalColor;
        if (button) button.interactable = _entry.Stock > 0;
    }

    public void SetSelected(bool selected)
    {
        if (bg && _entry != null)
            bg.color = selected ? selectedColor
                     : _entry.Stock <= 0 ? soldOutColor
                     : normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2 && _entry?.Stock > 0)
            OnDoubleClicked?.Invoke(_entry);
    }

    public void OnPointerEnter(PointerEventData _)
    {
        // `owner: this` — 사서 이 행이 사라지면 툴팁도 같이 닫힌다(`ItemInfoUI` 참고).
        if (_entry?.Item != null)
            ItemInfoUI.Show(_entry.Item, _entry.Quality, _entry.Price, null, false, this);
    }

    public void OnPointerExit(PointerEventData _) => ItemInfoUI.Hide();
}
