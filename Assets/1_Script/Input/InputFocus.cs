using UnityEngine.EventSystems;

// **글자를 치는 중인가 — 답은 여기 한 벌이다**(2026-09-09).
//
// 단축키를 받는 곳마다(소집 `R` · 진형 `F1~F5` · 능력 `1~4` · 지도 `M`) 같은 물음을 제 손으로
// 적고 있었고 **넷 중 하나는 옛 `InputField`를 안 봤다** — 그 창에서만 이름 칸에 `r`을 치면
// 소집이 걸렸다. 새 단축키를 붙일 때는 이걸 부르고 다시 적지 말 것.
public static class InputFocus
{
    public static bool IsTyping()
    {
        var es  = EventSystem.current;
        var sel = es != null ? es.currentSelectedGameObject : null;
        if (sel == null) return false;

        return sel.GetComponent<TMPro.TMP_InputField>() != null
            || sel.GetComponent<UnityEngine.UI.InputField>() != null;
    }
}
