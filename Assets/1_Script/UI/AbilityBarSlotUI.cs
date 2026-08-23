using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 액션 바 한 칸. **데이터 연결만 한다** — 여백·색·크기는 전부 씬 오브젝트가 갖는다.
public class AbilityBarSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image           icon;
    [Tooltip("Image Type=Filled, Fill Method=Radial 360 — 쿨다운이 도는 동안 덮는다")]
    [SerializeField] Image           cooldownMask;
    [SerializeField] TextMeshProUGUI keyLabel;
    [Tooltip("아이콘이 없을 때 대신 뜬다")]
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] Button          button;

    AbilityUser _user;
    AbilityData _ability;

    public void Bind(AbilityUser user, AbilityData ability, int key)
    {
        _user    = user;
        _ability = ability;

        if (keyLabel != null) keyLabel.text = key.ToString();

        // **아이콘이 없어도 칸이 비지 않는다** — 저작이 아직 그림을 안 붙였을 뿐인데 빈
        // 사각형이 뜨면 고장으로 보인다. 이름 글자가 그 자리를 메운다.
        bool hasIcon = ability != null && ability.Icon != null;
        if (icon != null)
        {
            icon.enabled = hasIcon;
            if (hasIcon) icon.sprite = ability.Icon;
        }
        if (nameLabel != null)
        {
            nameLabel.gameObject.SetActive(!hasIcon);
            nameLabel.text = ability != null ? ability.AbilityName : "";
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _user?.TryUse(_ability));
        }
        RefreshCooldown();
    }

    // **쿨다운은 덮개로 보인다** — 숫자만 적으면 훑어볼 때 읽어야 하고, 색만 바꾸면
    // 얼마나 남았는지가 안 보인다.
    public void RefreshCooldown()
    {
        if (_user == null || _ability == null) return;

        float left = _user.CooldownLeft(_ability);
        if (cooldownMask != null)
        {
            cooldownMask.enabled    = left > 0f;
            cooldownMask.fillAmount = _ability.Cooldown > 0f ? left / _ability.Cooldown : 0f;
        }
        if (button != null) button.interactable = _user.CanUse(_ability);
    }

    // 설명은 **올렸을 때** 뜬다(`HoverTooltipUI` — 스탯·특성이 쓰는 그 자리).
    // 칸이 작아 설명을 늘 띄우면 정작 고를 것을 가린다.
    public void OnPointerEnter(PointerEventData _)
    {
        if (_ability == null) return;

        float stat = _user != null && _user.GetComponent<UnitBrain>() is { Stats: not null } b
                   ? b.Stats.Toughness : 0f;

        string body = _ability.Summary(stat);
        if (!string.IsNullOrEmpty(_ability.Description))
            body = _ability.Description + "\n" + body;

        float left = _user != null ? _user.CooldownLeft(_ability) : 0f;
        if (left > 0f) body += $"\n<color=#FF8888>{left:0}초 남음</color>";

        HoverTooltipUI.Show(_ability.AbilityName, body);
    }

    // **파괴될 때도 거둔다** — 칸을 다시 지으면 `OnPointerExit`가 안 와서 설명만 화면에 남는다.
    public void OnPointerExit(PointerEventData _) => HoverTooltipUI.Hide();
    void OnDisable() => HoverTooltipUI.Hide();
}
