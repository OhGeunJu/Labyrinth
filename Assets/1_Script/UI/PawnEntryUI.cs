using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 자원 노드 배정 목록의 폰 엔트리. 초상화 + 개체명 + 해제 버튼.
public class PawnEntryUI : MonoBehaviour
{
    [SerializeField] Image portraitImage;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] Button unassignButton;

    public void Setup(UnitBrain brain, ResourceNode node, Action onChanged)
    {
        if (nameText != null)
            nameText.text = brain.UnitName;

        if (portraitImage != null)
        {
            var sprite = brain.EnsurePortrait();
            if (sprite == null && brain.Data != null) sprite = brain.Data.Icon;
            portraitImage.sprite  = sprite;
            portraitImage.enabled = sprite != null;
            portraitImage.preserveAspect = true;
        }

        if (unassignButton != null)
        {
            unassignButton.onClick.RemoveAllListeners();
            unassignButton.onClick.AddListener(() =>
            {
                node.Unassign(brain);
                onChanged?.Invoke();
            });
        }
    }
}
