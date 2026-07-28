using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 폰 로스터의 한 줄: [초상화][이름][현재 상태]
//
// **모양은 코드가 아니라 씬에 있는 템플릿 오브젝트가 갖는다.**
// 편집 중에 보이는 그 행을 런타임에 그대로 복제해 쓴다 — 미리보기와 실제가 어긋날 수 없고,
// 여백·폰트·색을 인스펙터에서 바꾸면 그대로 게임에 반영된다.
// 이 컴포넌트는 데이터 연결(이름·초상화·상태·클릭)만 담당한다.
public class PawnRosterRowUI : MonoBehaviour
{
    [Header("요소")]
    [SerializeField] Image background;
    [SerializeField] Image portraitImage;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] Button selectButton;

    [Header("색")]
    [SerializeField] Color normalColor     = new Color(1f, 1f, 1f, 0.04f);
    [SerializeField] Color selectedColor   = new Color(0.30f, 0.45f, 0.62f, 0.60f);
    [SerializeField] Color wandererName    = Color.white;
    [SerializeField] Color minionName      = new Color(0.65f, 0.80f, 0.95f);
    [Tooltip("초상화가 없을 때 자리 표시 색")]
    [SerializeField] Color emptyPortrait   = new Color(1f, 1f, 1f, 0.12f);

    UnitBrain _brain;

    public UnitBrain Brain => _brain;

    public void Bind(UnitBrain brain, Action<UnitBrain> onSelect)
    {
        _brain = brain;

        if (nameText != null)
        {
            nameText.text  = brain.UnitName;
            nameText.color = brain.Data is MinionData ? minionName : wandererName;
        }

        if (portraitImage != null)
        {
            var sprite = brain.EnsurePortrait();
            if (sprite == null && brain.Data != null) sprite = brain.Data.Icon;
            portraitImage.sprite = sprite;
            portraitImage.color  = sprite != null ? Color.white : emptyPortrait;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            var captured = brain;
            selectButton.onClick.AddListener(() => onSelect?.Invoke(captured));
        }

        Refresh(false);
    }

    public void Refresh(bool selected)
    {
        if (_brain == null) return;

        if (statusText != null)
        {
            string status = PawnStatusText.Describe(_brain);
            statusText.text  = status;
            statusText.color = PawnStatusText.ColorOf(status);
        }

        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
}
