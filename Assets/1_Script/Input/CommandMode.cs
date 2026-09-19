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
// - **단축키는 여기 하나다**(H 채굴 · J 채집 · K 운반 · C 취소 — 사용자가 정했다). 지정기마다 제 키를
//   들면 *"이 키는 어느 도구를 여나"*가 파일 둘에 흩어진다. **같은 키를 다시 누르면 명령을 끈다.**
// - **취소는 무엇이든 무른다** — 채굴 표시든 채집·운반 지정이든(사용자: *"다 호환되게"*). 취소 도구는
//   채굴 지정기의 `Cancel`로 돌고, 무르는 김에 채집·운반 쪽도 같이 무른다(`HaulDesignator.UndesignateRect`).
public class CommandMode : MonoBehaviour
{
    public enum Tool { Mine, Gather, Haul, Cancel }

    public static bool IsActive => MineDesignator.IsActive || HaulDesignator.IsActive;

    public static Tool Current =>
        MineDesignator.IsActive
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
        }
    }

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
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => SelfStanding.Create<CommandMode>("CommandMode");

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && GameModeManager.IsField && !InputFocus.KeysBlocked())
        {
            if      (kb.hKey.wasPressedThisFrame) Toggle(Tool.Mine);
            else if (kb.jKey.wasPressedThisFrame) Toggle(Tool.Gather);
            else if (kb.kKey.wasPressedThisFrame) Toggle(Tool.Haul);
            else if (kb.cKey.wasPressedThisFrame) Toggle(Tool.Cancel);
        }
    }
}
