using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// PawnRosterPanel의 개별 폰 엔트리.
// 초상화(1회 캡처 스냅샷) + 개체 이름 + 선택 버튼.
public class PawnRosterEntryUI : MonoBehaviour
{
    [SerializeField] Image portraitImage;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] Button selectButton;

    UnitBrain _brain;

    public UnitBrain Brain => _brain;

    public void Setup(UnitBrain brain, Action<UnitBrain> onSelect)
    {
        _brain = brain;

        if (nameText != null)
            nameText.text = brain.UnitName;

        if (portraitImage != null)
        {
            var sprite = brain.EnsurePortrait();
            if (sprite == null && brain.Data != null) sprite = brain.Data.Icon;
            portraitImage.sprite = sprite;
            portraitImage.enabled = sprite != null;
            portraitImage.preserveAspect = true;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            var captured = brain;
            selectButton.onClick.AddListener(() => onSelect?.Invoke(captured));
        }
    }
}
