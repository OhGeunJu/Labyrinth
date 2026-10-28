using UnityEngine.EventSystems;

// **글자를 치는 중인가 — 답은 여기 한 벌이다**(2026-09-09).
//
// 단축키를 받는 곳마다(소집 `R` · 진형 `F1~F5` · 능력 `1~4` · 지도 `M`) 같은 물음을 제 손으로
// 적고 있었고 **넷 중 하나는 옛 `InputField`를 안 봤다** — 그 창에서만 이름 칸에 `r`을 치면
// 소집이 걸렸다. 새 단축키를 붙일 때는 이걸 부르고 다시 적지 말 것.
public static class InputFocus
{
    // **모달 화면이 열려 있다**(2026-09-20, 캐릭터 생성 화면) — 그 동안은 글자를 안 치고 있어도 단축키를 받으면 안 된다
    // (이름 칸에 `k`를 치면 운반 명령이 열리고, 스페이스가 먹으면 **만드는 중에 시간이 흐른다**). 연 쪽이 켜고 닫는다.
    public static bool ModalOpen;

    // **단축키를 받으면 안 되는가 — 글자 단축키를 받는 곳은 전부 이것을 묻는다.** 2026-09-20에 세어 보니
    // 서른둘 중 넷만 `IsTyping`을 물었다(나머지는 이름 칸에서도 단축키가 먹었다). Esc·Shift·Ctrl은 글자가 아니라 안 묻는다.
    public static bool KeysBlocked() => ModalOpen || IsTyping();

    public static bool IsTyping()
    {
        var es  = EventSystem.current;
        var sel = es != null ? es.currentSelectedGameObject : null;
        if (sel == null) return false;

        return sel.GetComponent<TMPro.TMP_InputField>() != null
            || sel.GetComponent<UnityEngine.UI.InputField>() != null;
    }
}
