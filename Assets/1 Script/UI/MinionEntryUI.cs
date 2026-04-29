using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 하수인 소환 목록 한 항목.
// 아이콘, 이름, 소환 비용 표시. 자원 부족 시 빨간색.
public class MinionEntryUI : MonoBehaviour
{
    [SerializeField] Image           icon;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI costText;
    [SerializeField] Button          summonButton;

    public event Action<MinionData> OnSummon;

    MinionData _data;

    public void Setup(MinionData data)
    {
        _data = data;

        if (nameText != null) nameText.text = data.RaceName;
        if (icon != null)
        {
            icon.sprite  = data.Icon;
            icon.enabled = data.Icon != null;
        }

        if (summonButton != null)
        {
            summonButton.onClick.RemoveAllListeners();
            summonButton.onClick.AddListener(() => OnSummon?.Invoke(_data));
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_data == null) return;
        int have    = StorageBuilding.GetTotal(_data.SummonCostType);
        bool canAfford = have >= _data.SummonCostAmount;

        if (costText != null)
        {
            string col = canAfford ? "#88FF88" : "#FF6666";
            costText.text = $"<color={col}>{_data.SummonCostType}  {have} / {_data.SummonCostAmount}</color>";
        }

        if (summonButton != null)
            summonButton.interactable = canAfford;
    }
}
