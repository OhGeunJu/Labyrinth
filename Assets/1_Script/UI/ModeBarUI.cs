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

    public enum Mode { Build, Mine, Haul }

    [SerializeField] List<Slot> slots = new();

    [Header("색")]
    [SerializeField] Color idle   = new(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] Color active = new(0.78f, 0.62f, 0.20f, 1f);

    // **건설 중에는 숨는다**(2026-08-29, 사용자). 건설 모드는 제 목록 창(`BuildModeUI`)을
    // 통째로 띄우므로 그 위에 모드 바가 겹친다.
    //
    // **오브젝트를 끄지 않는다** — 끄면 `Update`가 안 돌아서 **건설을 끝내도 다시 안 나타난다**
    // (자기가 자기를 못 켠다). `CanvasGroup`으로 투명하게 만들면 계속 돌면서 지켜본다.
    // 알파만 0으로 두면 **안 보이는데 눌리므로** 레이캐스트도 같이 끈다.
    CanvasGroup _group;

    void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

        foreach (var s in slots)
        {
            if (s.Button == null) continue;
            var mode = s.Mode;                     // **지역 변수로 잡는다** — 람다가 루프
            s.Button.onClick.AddListener(() => Toggle(mode));   // 변수를 캡처하면 전부 마지막 것이 된다
        }
    }

    void Update()
    {
        bool hide = BuildModeManager.IsActive;
        if (_group != null)
        {
            _group.alpha          = hide ? 0f : 1f;
            _group.blocksRaycasts = !hide;
            _group.interactable   = !hide;
        }
        if (hide) return;

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
        }
    }
}
