using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MerchantStockEntryUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
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
            qualityText.color = ItemInfoPanel.QualityColor(entry.Quality);
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
        if (_entry?.Item != null)
            ItemInfoPanel.Show(_entry.Item, _entry.Quality, _entry.Price);
    }

    public void OnPointerExit(PointerEventData _) => ItemInfoPanel.Hide();
}
