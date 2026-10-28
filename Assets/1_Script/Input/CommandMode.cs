using UnityEngine;
using UnityEngine.InputSystem;

// **명령 — 채굴 · 채집 · 운반 · 취소를 한 모드로**(2026-09-19, 사용자 결정).
//
// 채굴(`MineDesignator`)과 채집·운반(`HaulDesignator`)은 **지정기가 둘**이지만 플레이어에게는 *"무엇을
// 시킬까"*를 고르는 **한 모드**다. 이 파일이 그 한 모드의 답을 든다: 지금 어느 도구인가 · 도구를 바꾸는
// 문 · 단축키. 지정기들은 제 드래그만 알고 서로를 모른다(`ExclusiveMode`가 하나만 켠다).
//
// ## 어기면 조용히 깨지는 것
//
// - **카메라는 안 건드린다**(2026-09-19, 사용자: *"명령 화면 조정하는 거 그냥 없애자"*). 한때 채굴이 시점을
//   위로 돌렸고(채굴만 그래서 통일성이 없었다), 명령 전체로 옮겼다가 통째로 뺐다.
// - **단축키는 여기 하나다** — `C`가 명령을 열고 닫고, 열려 있을 때 숫자가 도구를 고른다(`Order` 순서 —
//   2026-09-28 사용자 결정. 전에는 도구마다 글자 하나였다). 지정기마다 제 키를 들면 *"이 키는 어느 도구를
//   여나"*가 파일 둘에 흩어진다.
// - **취소는 무엇이든 무른다** — 채굴 표시든 채집·운반 지정이든(사용자: *"다 호환되게"*). 취소 도구는
//   채굴 지정기의 `Cancel`로 돌고, 무르는 김에 채집·운반 쪽도 같이 무른다(`HaulDesignator.UndesignateRect`).
public class CommandMode : MonoBehaviour
{
    // **값은 꼬리에 붙인다** — `ModeBarUI`가 순서로 버튼을 짓는다.
    public enum Tool { Mine, Gather, Haul, Cancel, Raise, Lower, Flatten }

    public static bool IsActive =>
        MineDesignator.IsActive || HaulDesignator.IsActive || LevelDesignator.IsActive;

    public static Tool Current =>
        LevelDesignator.IsActive
            ? (LevelDesignator.Current == LevelDesignator.Tool.Lower   ? Tool.Lower
             : LevelDesignator.Current == LevelDesignator.Tool.Flatten ? Tool.Flatten : Tool.Raise)
      : MineDesignator.IsActive
            ? (MineDesignator.Current == MineDesignator.Tool.Cancel ? Tool.Cancel : Tool.Mine)
            : HaulDesignator.Kind == HaulDesignator.Target.Gather ? Tool.Gather : Tool.Haul;

    // 도구를 바꾼다(꺼져 있으면 켠다). 버튼·단축키가 다 여기로 온다.
    public static void Use(Tool t)
    {
        switch (t)
        {
            case Tool.Mine:   MineDesignator.Open(MineDesignator.Tool.Designate);   break;
            case Tool.Cancel: MineDesignator.Open(MineDesignator.Tool.Cancel);      break;
            case Tool.Gather: HaulDesignator.Open(HaulDesignator.Target.Gather);    break;
            case Tool.Haul:   HaulDesignator.Open(HaulDesignator.Target.Haul);      break;
            case Tool.Raise:  LevelDesignator.Open(LevelDesignator.Tool.Raise);     break;
            case Tool.Lower:  LevelDesignator.Open(LevelDesignator.Tool.Lower);     break;
            case Tool.Flatten: LevelDesignator.Open(LevelDesignator.Tool.Flatten); break;
        }
    }

    public static string NameOf(Tool t) => t switch
    {
        Tool.Mine   => "채굴",
        Tool.Gather => "채집",
        Tool.Haul   => "운반",
        Tool.Raise  => "올리기",
        Tool.Lower  => "내리기",
        Tool.Flatten => "평탄화",
        _           => "취소",
    };

    // 그 도구가 이미 켜져 있으면 명령을 끄고, 아니면 그 도구로 연다(키를 두 번 누르면 나간다).
    public static void Toggle(Tool t)
    {
        if (IsActive && Current == t) Close();
        else Use(t);
    }

    public static void Close()
    {
        MineDesignator.Close();
        HaulDesignator.Close();
        LevelDesignator.Close();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => SelfStanding.Create<CommandMode>("CommandMode");

    // **화면 순서 = 숫자 순서**(2026-09-28, 사용자: *"명령은 N키로 바꾸고 명령 열었을 때도 1234로"*).
    // 도구 바(`ModeToolBarUI.Command`)가 이 배열을 그대로 늘어놓는다 — 둘이 따로 적으면 `3`을
    // 눌렀는데 세 번째 칸이 아닌 것이 켜진다.
    public static readonly Tool[] Order =
        { Tool.Cancel, Tool.Mine, Tool.Gather, Tool.Haul, Tool.Raise, Tool.Lower, Tool.Flatten };

    // 도구 바가 이름 옆에 적는 키. `Order`의 자리 + 1.
    public static int KeyOf(Tool t) => System.Array.IndexOf(Order, t) + 1;

    // 명령을 켜고 끈다 — **`C`와 모드 바 버튼이 같은 문이다.** 켤 때는 채굴로 연다.
    public static void ToggleMode()
    {
        if (IsActive) Close();
        else          Use(Tool.Mine);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || InputFocus.KeysBlocked()) return;

        // **`C`가 명령 모드를 연다**(2026-09-28 — 전에는 도구마다 글자 하나: H·J·K·C·V·N·T).
        // 모드 바가 키보드 줄과 같은 순서다: `C` 명령 · `V` 밭 구역 · `B` 건설.
        if (kb.cKey.wasPressedThisFrame) { ToggleMode(); return; }

        // **열려 있을 때만 숫자가 도구다** — 닫혀 있으면 1~4는 능동 능력이다(`AbilityController`가 양보한다).
        if (!IsActive) return;
        for (int i = 0; i < Order.Length && i < 9; i++)
            if (kb[Key.Digit1 + i].wasPressedThisFrame) { Use(Order[i]); return; }
    }
}
