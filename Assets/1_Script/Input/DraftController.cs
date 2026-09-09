using UnityEngine;
using UnityEngine.InputSystem;

// **소집 토글(R).** 고른 폰들을 싸울 태세로 붙들거나 풀어 준다.
//
// 소집이 무엇을 뜻하는지는 `UnitBrain.Draft`가 답한다 — 여기는 **누구에게 걸 것인가**만 정한다.
//
// ── 누구에게 ──────────────────────────────────────────────────────────────────
//
// 고른 폰 중 **명령을 받고, 지금 받을 수 있는** 것들(`UnitBrain.AcceptsOrders`/`CanObeyNow` —
// 우클릭 명령이 쓰는 것과 **같은 물음**이라 명령 대상이 한 벌이다). 하수인은 자동 AI라 빠지고,
// 갇히거나 업힌 폰도 빠진다 — 걸어 봐야 BT가 안 돌아 조용히 아무 일도 안 일어난다.
//
// ── 전부 켜거나 전부 끈다 ─────────────────────────────────────────────────────
//
// 개체마다 뒤집으면 셋 중 하나만 소집된 상태에서 R을 눌렀을 때 **둘은 켜지고 하나는 꺼져**
// 매번 화면을 확인해야 한다. **하나라도 안 켜져 있으면 전부 켜고, 전부 켜져 있을 때만 전부
// 끈다** — 누르는 사람이 "이번엔 어느 쪽이 될까"를 안 세도 된다.
//
// ── 씬 배선이 필요 없다 ───────────────────────────────────────────────────────
//
// `HaulDesignator`와 같은 자리 — **아무 상태도 안 들고 저작할 것도 없는 순수 입력기**라
// 씬에 둘 이유가 없다(정착지가 여럿 생겨도 맵마다 배선을 반복하지 않는다).
public class DraftController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SelfStanding.Create<DraftController>("DraftController");
    }

    SelectionManager _selection;

    void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.rKey.wasPressedThisFrame) return;

        // **글자를 치는 중이면 명령이 아니다** — 수량 칸에서 R을 누른 것까지 소집으로 받으면
        // 창을 열어 둔 채로는 타자를 칠 수가 없다.
        //
        // **"고른 것이 있나"로 물으면 안 된다** — uGUI는 누른 **버튼**도 다음 클릭까지 고른
        // 채로 둔다. 그러면 창고 필터나 작업 설정을 한 번 누른 뒤로는 R이 조용히 안 먹는다.
        // 글자를 치는 칸인지를 물어야 한다.
        if (InputFocus.IsTyping()) return;

        // **월드맵에서는 안 먹는다** — 거기서 R은 아무 뜻이 없고, 지도에 나가 있는 사이에
        // 필드의 폰들이 소집되면 돌아왔을 때 아무도 일을 안 하고 있다.
        if (!GameModeManager.IsField) return;

        Toggle();
    }

    void Toggle()
    {
        // 씬 오브젝트라 매번 찾지 않고 들고 있는다(정착지를 갈아타면 다시 찾는다).
        if (_selection == null) _selection = FindFirstObjectByType<SelectionManager>();
        if (_selection == null) return;

        // **셀 때와 걸 때가 같은 목록이어야 한다** — 하수인이 섞여 있다고 "전부 켜져 있나"의
        // 답이 흔들리면, 누를 때마다 결과가 달라진다.
        bool any = false, allDrafted = true;
        foreach (var pawn in _selection.Selected)
        {
            var brain = Target(pawn);
            if (brain == null) continue;
            any = true;
            if (!brain.IsDrafted) allDrafted = false;
        }
        if (!any) return;

        bool draft = !allDrafted;
        foreach (var pawn in _selection.Selected)
        {
            var brain = Target(pawn);
            if (brain == null) continue;
            if (draft) brain.Draft(); else brain.Undraft();
        }
    }

    static UnitBrain Target(PawnMover pawn)
    {
        if (pawn == null) return null;   // 고른 뒤 파괴된 폰
        var brain = pawn.GetComponent<UnitBrain>();
        return brain != null && brain.AcceptsOrders && brain.CanObeyNow ? brain : null;
    }
}
