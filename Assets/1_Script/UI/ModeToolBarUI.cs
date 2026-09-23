using System;
using UnityEngine;

// **모드 안의 도구 둘 — 바 하나가 셋을 다 든다**(2026-09-02).
//
//   밭 구역 : `새 구역`(또는 `확장`) / `삭제`
//   명령    : `채굴` / `채집` / `운반` / `취소`(2026-09-19 — 채굴·운반 모드를 하나로, `CommandMode`)
//
// 셋 다 **같은 물음에 답한다**: *"지금 이 드래그가 무엇을 하는가."* 그래서 바를 셋 만들지
// 않는다 — 만들면 씬에서 자리를 세 번 맞춰야 하고, 셋 중 하나만 옮겨 두면 모드를 바꿀 때마다
// **도구가 화면에서 뛴다.** 하나가 켜진 모드에 맞춰 이름만 바꾼다.
//
// **모드 바와는 다른 자리다** — 모드 바는 *어느 모드인가*를, 이 바는 *그 모드 안에서 무엇을
// 하는가*를 답한다. 섞으면 **누르면 모드가 바뀌는 것과 도구가 바뀌는 것이 구별되지 않는다.**
//
// **상태는 여기서 안 든다** — 지정기들이 이미 제 `Current`를 들고 있고, 매 프레임 물어본다.
// 따로 기억하면 다른 길로 바뀌었을 때 버튼만 옛 상태로 남는다.
public class ModeToolBarUI : MonoBehaviour
{
    [Tooltip("씬에 놓인 타일. **코드가 만들지 않는다** — 여기 물린 것을 그대로 쓴다.")]
    [SerializeField] BuildingItemUI primaryItem;
    [Tooltip("밭 구역에만 쓰는 셋째 타일(`확장`). 다른 모드에서는 꺼진다.")]
    [SerializeField] BuildingItemUI extraItem;
    [SerializeField] BuildingItemUI cancelItem;
    [Tooltip("명령에만 쓰는 넷째 타일(`땅고르기`). 밭 구역에서는 꺼진다. `UI → Setup Command Bar`가 세운다.")]
    [SerializeField] BuildingItemUI fourthItem;
    // **다음에 도구가 하나 더 늘면 배열로 옮길 것**(2026-09-24) — 서수 필드는 늘어날수록
    // *"어느 칸이 무엇인가"*가 이름에서 안 읽히고, `Farm`이 남는 칸을 끄는 줄도 같이 는다.
    // 옮기는 방법은 이미 선례가 있다: `BuildingData.Costs`가 **옛 한 칸을 이름을 지켜
    // 흡수했다**(직렬화 키가 필드 이름이라 이름을 바꾸면 구운 씬의 배선이 통째로 날아간다).
    [Tooltip("명령에만 쓰는 다섯째 타일(`취소`). 밭 구역에서는 꺼진다.")]
    [SerializeField] BuildingItemUI fifthItem;

    [Tooltip("보였다 사라지는 자식(창 규약). 뿌리를 끄면 `Update`가 안 돌아 다시 못 켠다.")]
    [SerializeField] GameObject panel;

    [Header("색")]
    [SerializeField] Color idle   = new(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] Color active = new(1.00f, 0.80f, 0.30f, 1f);

    // 지금 보여 주고 있는 것 — 바뀔 때만 다시 적는다(`Setup`은 툴팁 델리게이트까지 새로 건다).
    //
    // **모드도 같이 기억한다**(2026-09-02 버그). 이름만 보고 다시 걸었더니 `운반 → 채굴`처럼
    // **취소 쪽 이름이 그대로인 전환**에서 다시 안 걸렸고, 그 타일이 **운반 지정기에 묶인 채**
    // 남아 **채굴에서 취소가 안 먹었다**(사용자가 짚었다). 이름은 같아도 **누를 때 부르는
    // 곳이 다르다** — 다시 거는 조건은 *무슨 글자인가*가 아니라 *어느 모드인가*다.
    enum Mode { None, Farm, Command }

    Mode _shownMode = Mode.None;

    void Awake()
    {
        if (primaryItem == null || cancelItem == null)
        {
            Debug.LogWarning("[도구 바] 타일이 안 물렸다 — `UI → Setup Command Bar`를 돌릴 것.");
            enabled = false;
        }
    }

    void Update()
    {
        bool on = FarmZoneDesignator.IsActive || CommandMode.IsActive;
        if (panel != null && panel.activeSelf != on) panel.SetActive(on);
        if (!on) { _shownMode = Mode.None; return; }

        var mode = FarmZoneDesignator.IsActive ? Mode.Farm : Mode.Command;

        // 모드가 바뀌면 이름이 같아도 다시 건다.
        if (mode != _shownMode)
        {
            _shownMode = mode;
            primaryItem?.ForgetName();
            extraItem?.ForgetName();
            cancelItem?.ForgetName();
            fourthItem?.ForgetName();
            fifthItem?.ForgetName();
        }

        if (mode == Mode.Farm) Farm();
        else Command();
    }

    // **명령 — 채굴 · 채집 · 운반 · 취소**(2026-09-19, 사용자: *"채굴 채집 운반 세 버튼을 명령으로 합쳐서
    // 한번에 뜨게"* · *"원래 있던 취소 버튼도 달아 대신 다 호환되게"*).
    //
    // 전에는 채굴·운반이 모드 바의 **따로 된 버튼**이고 여기에는 그 모드의 `찍기/취소`가 떴다. 채집은 운반
    // 모드가 같이 찍고 있었는데 **이름이 `운반`이라 아무도 몰랐다.** 이제 넷이 **한 줄에 같이** 뜬다 —
    // 무엇이 켜졌나는 `CommandMode.Current` 하나가 답한다. **취소는 도구를 안 가린다**(무엇이든 무른다).
    // 설명은 **무엇을 시키나 한 줄**이다(사용자: *"딱 뭐를 시킨다 만 적어"*). 키는 이름에 붙인다.
    //
    // 타일 이름(`cancelItem`)은 씬 배선이라 그대로 두고 **셋째 칸(운반)**으로 쓴다 — 넷째는 `fourthItem`.
    void Command()
    {
        var cur = CommandMode.Current;
        Bind(primaryItem, "채굴 (H)", "벽을 캐게 한다.",               cur == CommandMode.Tool.Mine,   () => CommandMode.Use(CommandMode.Tool.Mine));
        Bind(extraItem,   "채집 (J)", "버섯과 채집물을 따게 한다.",     cur == CommandMode.Tool.Gather, () => CommandMode.Use(CommandMode.Tool.Gather));
        Bind(cancelItem,  "운반 (K)", "바닥 물건을 창고로 옮기게 한다.", cur == CommandMode.Tool.Haul,   () => CommandMode.Use(CommandMode.Tool.Haul));
        Bind(fourthItem,  "땅고르기 (V)", "땅을 처음 누른 칸의 높이로 깎거나 메우게 한다.",
             cur == CommandMode.Tool.Level,  () => CommandMode.Use(CommandMode.Tool.Level));
        Bind(fifthItem,   "취소 (C)", "찍어 둔 명령을 무른다.",          cur == CommandMode.Tool.Cancel, () => CommandMode.Use(CommandMode.Tool.Cancel));
    }

    // 밭 구역은 도구가 셋이다 — **`새 구역`과 `확장`을 가른다.** 하나로 두고 *"고른 게
    // 없으면 새 구역"*으로 했더니 **무엇이 될지가 고른 상태에 숨어 있어** 누르기 전에는
    // 알 수 없었다(사용자).
    void Farm()
    {
        var cur = FarmZoneDesignator.Current;
        if (fourthItem != null && fourthItem.gameObject.activeSelf) fourthItem.gameObject.SetActive(false);
        if (fifthItem  != null && fifthItem.gameObject.activeSelf)  fifthItem.gameObject.SetActive(false);

        Bind(primaryItem, "새 구역", "끌어서 **새 구역**을 만든다.\n붙어 있어도 다른 구역이 된다.",
             cur == FarmZoneDesignator.Tool.New,
             () => FarmZoneDesignator.Use(FarmZoneDesignator.Tool.New));

        Bind(extraItem, "확장", "**처음 누른 칸의 구역**이 커진다.\n빈 땅에서 시작하면 처음 걸린 구역이다.",
             cur == FarmZoneDesignator.Tool.Extend,
             () => FarmZoneDesignator.Use(FarmZoneDesignator.Tool.Extend));

        Bind(cancelItem, "삭제", "끌어서 **구역에서 뺀다.**\n뺀 밭은 저 혼자가 된다.",
             cur == FarmZoneDesignator.Tool.Erase,
             () => FarmZoneDesignator.Use(FarmZoneDesignator.Tool.Erase));
    }

    // **이름이 바뀔 때 설명도 같이 간다** — 하는 일이 달라졌는데 설명만 옛것이면
    // 그 설명이 곧 거짓말이다. **다시 거는 조건은 이름이 아니라 모드다**(위 `_shownMode` 참고).
    void Bind(BuildingItemUI item, string name, string tip, bool on, Action click)
    {
        if (item == null) return;
        if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);

        if (item.CurrentName != name) item.Setup(name, () => tip, click);
        item.SetLabelColor(on ? active : idle);
    }
}
