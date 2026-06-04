using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class SellItemEntryUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] Image           bg;
    [SerializeField] Image           icon;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI qualityText;
    [SerializeField] TextMeshProUGUI sellPriceText;
    [SerializeField] TextMeshProUGUI qtyText;
    [SerializeField] Button          button;

    [SerializeField] Color normalColor   = new(0.18f, 0.18f, 0.18f, 0.95f);
    [SerializeField] Color selectedColor = new(0.25f, 0.40f, 0.25f, 0.95f);
    [SerializeField] Color noValueColor  = new(0.22f, 0.22f, 0.22f, 0.60f);

    public event Action<ItemInstance> OnSelected;
    public event Action<ItemInstance> OnDoubleClicked;

    ItemInstance _inst;
    int          _sellPrice;

    public void Setup(ItemInstance inst, int sellPrice)
    {
        _inst      = inst;
        _sellPrice = sellPrice;

        if (icon)
        {
            icon.sprite = inst.Data?.Icon;
            icon.color  = inst.Data?.Icon != null ? Color.white : new Color(1, 1, 1, 0);
        }
        if (nameText)    nameText.text    = inst.Data?.ItemName ?? "";
        if (qualityText)
        {
            qualityText.text  = inst.Quality.DisplayName();
            qualityText.color = ItemInfoPanel.QualityColor(inst.Quality);
        }
        if (sellPriceText)
        {
            bool canSell = sellPrice > 0;
            sellPriceText.text  = canSell ? $"{sellPrice} G" : "판매 불가";
            sellPriceText.color = canSell ? new Color(1f, 0.85f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
        }
        if (qtyText)
        {
            bool showQty = inst.Quantity > 1;
            qtyText.gameObject.SetActive(showQty);
            if (showQty) qtyText.text = $"x{inst.Quantity}";
        }

        if (button)
        {
            button.interactable = sellPrice > 0;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnSelected?.Invoke(_inst));
        }

        if (bg) bg.color = sellPrice > 0 ? normalColor : noValueColor;
    }

    public void SetSelected(bool selected)
    {
        if (bg) bg.color = selected ? selectedColor
                         : _sellPrice > 0 ? normalColor : noValueColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2 && _sellPrice > 0)
            OnDoubleClicked?.Invoke(_inst);
    }

    public void OnPointerEnter(PointerEventData _)
    {
        if (_inst?.Data != null)
            ItemInfoPanel.Show(_inst.Data, _inst.Quality, _sellPrice > 0 ? _sellPrice : -1, "판매가");
    }

    public void OnPointerExit(PointerEventData _) => ItemInfoPanel.Hide();
}
