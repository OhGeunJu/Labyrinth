using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// **지금 무슨 모드인가 — 화면에 띄우고 눌러서 바꾼다**(2026-08-29).
//
// 모드가 셋이 됐다(건설 `B` · 채굴 `K` · 운반 `H`). 키만 있으면 **있는 줄도 모른다** —
// 특히 채굴은 지하에서 가장 자주 쓰는 조작인데 아무 표시가 없었다.
//
// ── 켜졌는지가 보여야 한다 ─────────────────────────────────────────────────
//
// 이 셋은 **껐다 켜는 상태**라(누르면 끝나는 명령이 아니다), 지금 어느 것이 켜져 있는지가
// 화면에 없으면 **드래그가 왜 다르게 동작하는지**를 알 수 없다. 버튼 색이 그 답을 한다.
//
// **상태는 여기서 안 든다** — 각 모드가 이미 제 `IsActive`를 들고 있고, 키로도 바뀐다.
// 여기서 따로 기억하면 **키로 바꿨을 때 버튼만 옛 상태로 남는다.** 매 프레임 물어본다.
public class ModeBarUI : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public Button Button;
        public Mode   Mode;
    }

    // **값은 꼬리에 붙인다** — 씬이 정수로 들고 있어, 중간에 끼우면 저작해 둔 버튼이 밀린다.
    // `Command`(2026-09-19) — 채굴·채집·운반을 한 버튼으로. 켜면 도구 바에 셋이 같이 뜬다(`ModeToolBarUI`).
    // `Mine`·`Haul`은 **지우지 않는다** — 정수로 저작돼 있어 지우면 뒤의 값이 밀린다(씬은 도구가 옮긴다).
    public enum Mode { Build, Mine, Haul, FarmZone, Command }

    [SerializeField] List<Slot> slots = new();

    [Header("색")]
    [SerializeField] Color idle   = new(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] Color active = new(0.78f, 0.62f, 0.20f, 1f);

    // **건설 중에도 뜬다**(2026-09-02, 사용자 결정 — 전에는 숨겼다).
    //
    // 숨긴 이유는 *"건설 목록이 그 위에 겹친다"*였는데, **겹치는 것은 자리를 옮기면 되는
    // 일**이고 숨기면 **건설에서 채굴로 바로 못 넘어간다**(끄고, 찾고, 다시 켠다). 모드 바가
    // 있는 이유가 *모드끼리 바로 옮겨 다니는 것*이라 숨기면 그 이유가 사라진다.
    //
    // 창 규약대로 `panel`은 자식을 가리킨다(`<기준>_UI` = 스크립트만 · `<기준>_Panel` =
    // 보였다 사라지는 자식) — 지금은 늘 켜 두지만, 숨길 일이 생기면 여기서 끈다.
    [SerializeField] GameObject panel;

    void Awake()
    {
        foreach (var s in slots)
        {
            if (s.Button == null) continue;
            var mode = s.Mode;                     // **지역 변수로 잡는다** — 람다가 루프
            s.Button.onClick.AddListener(() => Toggle(mode));   // 변수를 캡처하면 전부 마지막 것이 된다
        }
    }

    void Update()
    {
        if (panel != null && !panel.activeSelf) panel.SetActive(true);

        foreach (var s in slots)
        {
            if (s.Button == null) continue;
            var img = s.Button.targetGraphic as Image;
            if (img != null) img.color = IsOn(s.Mode) ? active : idle;
        }
    }

    // **모드마다 켜는 길이 다르다** — 건설은 매니저가, 나머지는 지정기가 든다.
    // 여기서 새 스위치를 만들지 않는다: 키로 켠 것과 버튼으로 켠 것이 **같은 상태**여야 한다.
    static bool IsOn(Mode m) => m switch
    {
        Mode.Build => BuildModeManager.IsActive,
        Mode.Mine  => MineDesignator.IsActive,
        Mode.Haul  => HaulDesignator.IsActive,
        Mode.FarmZone => FarmZoneDesignator.IsActive,
        Mode.Command  => CommandMode.IsActive,
        _          => false,
    };

    // **키를 흉내 낸다.** 각 모드가 제 키를 스스로 듣고 있으므로, 버튼은 그 키와 같은 문을
    // 두드려야 한다 — 따로 켜면 *"버튼으로는 켜지는데 키로는 안 꺼지는"* 상태가 생긴다.
    void Toggle(Mode m)
    {
        switch (m)
        {
            case Mode.Build: BuildModeManager.Instance?.ToggleBuildMode(); break;
            case Mode.Mine:  MineDesignator.Toggle();                      break;
            case Mode.Haul:  HaulDesignator.Toggle();                      break;
            case Mode.FarmZone: FarmZoneDesignator.Toggle();               break;
            // 명령: 켜져 있으면 끄고, 꺼져 있으면 **채굴로** 연다(도구 바에서 채집·운반으로 바꾼다).
            case Mode.Command:
                if (CommandMode.IsActive) CommandMode.Close();
                else                      CommandMode.Use(CommandMode.Tool.Mine);
                break;
        }
    }
}
