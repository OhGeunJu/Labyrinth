using UnityEngine;
using UnityEngine.InputSystem;

// **능동 능력 단축키(1~4).** 고른 폰들이 그 칸의 능력을 쓴다.
//
// 무엇을 하는지는 `AbilityUser.TryUse`가 답한다 — 여기는 **누가·어느 칸**만 정한다
// (`DraftController`가 소집의 뜻을 `UnitBrain.Draft`에 맡기고 대상만 고르는 것과 같은 모양).
//
// **씬 배선이 없다** — 아무 상태도 안 들고 저작할 것도 없는 순수 입력기라
// `HaulDesignator`·`DraftController`와 같은 자리다.
public class AbilityController : MonoBehaviour
{
    // 액션 바 칸 수. **늘리려면 화면 칸도 같이 늘려야 한다** — 누를 수는 있는데 안 보이는
    // 칸이 생기면 그건 기능이 아니라 숨은 조작이다.
    public const int SlotCount = 4;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SelfStanding.Create<AbilityController>("AbilityController");
    }

    SelectionManager _selection;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 글자를 치는 중이거나 지도를 보는 중이면 명령이 아니다 — `DraftController`와 같은 규칙.
        if (InputFocus.KeysBlocked() || !GameModeManager.IsField) return;

        // **건설·명령·밭 구역이 켜져 있으면 숫자는 그쪽 것이다**(2026-09-28) — 갈래(`BuildModeUI`)와
        // 도구(`CommandMode`)를 고른다. 안 비키면 갈래를 바꾸는 순간 고른 폰이 방패를 든다.
        if (BuildModeManager.IsActive || CommandMode.IsActive || FarmZoneDesignator.IsActive) return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (!kb[Key.Digit1 + i].wasPressedThisFrame) continue;
            UseSlot(i);
            return;
        }
    }

    // **고른 폰 전부가 각자 제 칸의 능력을 쓴다.** 한 명만 쓰게 하면 방패 셋을 들려 놓고도
    // 하나씩 골라 눌러야 하고, 그건 진형을 잡아 놓고 하는 조작이 아니다.
    //
    // **칸은 폰마다 제 목록의 순서다** — 같은 칸이 폰마다 다른 능력일 수 있다. 능력을 전역
    // 목록으로 모으면 방패를 안 든 폰의 칸이 비어 번호가 밀린다.
    void UseSlot(int index)
    {
        if (_selection == null) _selection = FindFirstObjectByType<SelectionManager>();
        if (_selection == null) return;

        foreach (var mover in _selection.Selected)
        {
            if (mover == null) continue;

            var brain = mover.GetComponent<UnitBrain>();
            // **스스로 쓰는 폰과 같은 줄을 본다**(`UnitBrain.AbilityByHand`) — 갈라 적으면
            // 사람도 안 누르고 AI도 안 쓰는 폰이나, 둘 다 쓰는 폰이 생긴다.
            if (brain == null || !brain.AbilityByHand) continue;

            var user = brain.GetComponent<AbilityUser>();
            if (user == null) continue;

            var list = user.Abilities;
            if (index >= list.Count) continue;

            user.TryUse(list[index]);
        }
    }

}
