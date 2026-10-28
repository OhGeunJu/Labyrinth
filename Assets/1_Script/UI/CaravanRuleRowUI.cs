using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상단 창의 규칙 한 줄 — 물건 · (판매만) 방식 · 수 · 지우기(`CaravanUI`). 모양은 씬의 템플릿이 든다.
public class CaravanRuleRowUI : MonoBehaviour
{
    [SerializeField] TMP_Dropdown   itemDropdown;
    [SerializeField] TMP_Dropdown   modeDropdown;   // 판매 줄만 — 없으면 숨긴다
    [SerializeField] TMP_InputField amountInput;
    [SerializeField] Button         removeButton;

    List<ItemData> _items;
    Action<ItemData> _onItem;
    Action<int>      _onMode;
    Action<int>      _onAmount;

    public void Bind(List<ItemData> items, List<string> labels, ItemData current, int mode, int amount,
                     Action<ItemData> onItem, Action<int> onMode, Action<int> onAmount, Action onRemove)
    {
        _items = items; _onItem = onItem; _onMode = onMode; _onAmount = onAmount;

        itemDropdown.onValueChanged.RemoveAllListeners();
        itemDropdown.ClearOptions();
        itemDropdown.AddOptions(labels);
        int i = current != null ? items.IndexOf(current) : -1;
        itemDropdown.SetValueWithoutNotify(Mathf.Max(0, i));
        itemDropdown.RefreshShownValue();
        itemDropdown.onValueChanged.AddListener(v => _onItem?.Invoke(v >= 0 && v < _items.Count ? _items[v] : null));

        if (modeDropdown != null)
        {
            modeDropdown.gameObject.SetActive(onMode != null);
            modeDropdown.onValueChanged.RemoveAllListeners();
            if (onMode != null)
            {
                modeDropdown.ClearOptions();
                modeDropdown.AddOptions(new List<string> { "초과분", "정량" });
                modeDropdown.SetValueWithoutNotify(mode);
                modeDropdown.RefreshShownValue();
                modeDropdown.onValueChanged.AddListener(v => _onMode?.Invoke(v));
            }
        }

        amountInput.onEndEdit.RemoveAllListeners();
        amountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        amountInput.SetTextWithoutNotify(amount.ToString());
        amountInput.onEndEdit.AddListener(t => _onAmount?.Invoke(int.TryParse(t, out var n) ? Mathf.Max(0, n) : 0));

        removeButton.onClick.RemoveAllListeners();
        removeButton.onClick.AddListener(() => onRemove?.Invoke());
    }
}
