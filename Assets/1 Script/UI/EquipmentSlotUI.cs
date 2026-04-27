using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler
{
    Image           _icon;
    TextMeshProUGUI _label;
    Action          _onClick;

    public void Init(string slotName, ItemData item, Action onClick)
    {
        _icon   = GetComponentInChildren<Image>();
        _label  = GetComponentInChildren<TextMeshProUGUI>();
        _onClick = onClick;

        if (_label != null)
            _label.text = item != null ? $"{slotName}\n{item.ItemName}" : $"{slotName}\n-";

        if (_icon != null)
            _icon.sprite = item?.Icon;
    }

    public void OnPointerClick(PointerEventData eventData) => _onClick?.Invoke();
}
