using UnityEngine;
using UnityEngine.InputSystem;

// **진형을 고른다 — `F1`~`F5`**(2026-09-08, 사용자 결정. `F5`(사각형)는 2026-09-09에 붙었다).
//
// 한 키로 **순환**하게 만들었다가 바꿨다. 순환은 *지금 무엇인지*를 알아야 다음이 뭔지
// 알 수 있어서, 화면에 상태가 안 보이면 **누를 때마다 무엇이 나올지 모른다.** 번호 키는
// 누르는 것이 곧 고르는 것이라 **지금 무엇인지 몰라도 된다.**
//
// `DraftController`·`HaulDesignator`와 **같은 자리**다: 아무 상태도 안 들고 저작할 것도
// 없는 순수 입력기라 **씬 배선 없이 스스로 선다**(맵마다 배선을 반복할 이유가 없다).
//
// **모드가 아니다** — 드래그의 뜻을 안 바꾸므로 `ExclusiveMode`에 넣지 않는다. 진형은
// *다음 이동 명령이 어떤 모양으로 설 것인가*이고, 그건 상태가 아니라 **설정 하나**다.
//
// **답하는 자리는 진형 바다**(`FormationBarUI` — 소집 중에만 뜬다). 알림은 **그 바가 안
// 떠 있을 때만** 띄운다: 바가 이미 답하고 있으면 알림은 겹치는 말이고, 안 떠 있으면
// (아직 소집을 안 했으면) **알림이 유일한 답**이다. 둘 다 없으면 고른 진형이 화면 어디에도
// 안 보여 **찾을 수 없는 스위치**가 된다.
[DefaultExecutionOrder(-40)]
public class FormationController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SelfStanding.Create<FormationController>("FormationController");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Formation.Shape? pick =
            kb.f1Key.wasPressedThisFrame ? Formation.Shape.Box
          : kb.f2Key.wasPressedThisFrame ? Formation.Shape.Line
          : kb.f3Key.wasPressedThisFrame ? Formation.Shape.Column
          : kb.f4Key.wasPressedThisFrame ? Formation.Shape.Wedge
          : kb.f5Key.wasPressedThisFrame ? Formation.Shape.Ring
          : null;
        if (pick == null) return;

        // 글자를 치는 중이면 단축키가 아니다(`DraftController`와 같은 규칙).
        if (InputFocus.KeysBlocked()) return;

        Formation.Current = pick.Value;

        // 바가 떠 있으면 그쪽이 이미 답한다 — 여기서 또 띄우면 누를 때마다 알림이 쌓인다.
        if (!FormationBarUI.IsShowing)
            EventNotificationUI.Notify("진형",
                $"{Formation.NameOf(Formation.Current)} — 소집(R)한 뒤 우클릭으로 끌어 세운다");
    }

}
