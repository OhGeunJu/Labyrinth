using System;
using UnityEngine;

// **모드 안의 도구 둘 — 바 하나가 셋을 다 든다**(2026-09-02).
//
//   밭 구역 : `새 구역`(또는 `확장`) / `삭제`
//   채굴    : `채굴` / `취소`
//   운반    : `운반` / `취소`
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
    enum Mode { None, Farm, Mine, Haul }

    Mode _shownMode = Mode.None;

    void Awake()
    {
        if (primaryItem == null || cancelItem == null)
        {
            Debug.LogWarning("[도구 바] 타일이 안 물렸다 — `Setup Mode Tool Bar`를 돌릴 것.");
            enabled = false;
        }
    }

    void Update()
    {
        bool on = FarmZoneDesignator.IsActive || MineDesignator.IsActive || HaulDesignator.IsActive;
        if (panel != null && panel.activeSelf != on) panel.SetActive(on);
        if (!on) { _shownMode = Mode.None; return; }

        var mode = FarmZoneDesignator.IsActive ? Mode.Farm
                 : MineDesignator.IsActive     ? Mode.Mine
                                               : Mode.Haul;

        // 모드가 바뀌면 이름이 같아도 다시 건다.
        if (mode != _shownMode)
        {
            _shownMode = mode;
            primaryItem?.ForgetName();
            extraItem?.ForgetName();
            cancelItem?.ForgetName();
        }

        if (mode == Mode.Farm) Farm();
        else if (mode == Mode.Mine) Simple(
            "채굴", "끌어서 **캘 칸을 찍는다.**\n바위만 찍힌다.",
            "취소", "끌어서 **찍어 둔 것을 무른다.**\n우클릭은 한 칸씩이다.",
            MineDesignator.Current == MineDesignator.Tool.Designate,
            () => MineDesignator.Use(MineDesignator.Tool.Designate),
            () => MineDesignator.Use(MineDesignator.Tool.Cancel));
        else Simple(
            "운반", "끌어서 **실어 갈 물건을 찍는다.**\n안 찍은 것은 아무도 안 건드린다.",
            "취소", "끌어서 **찍어 둔 것을 무른다.**\n우클릭은 하나씩이다.",
            HaulDesignator.Current == HaulDesignator.Tool.Designate,
            () => HaulDesignator.Use(HaulDesignator.Tool.Designate),
            () => HaulDesignator.Use(HaulDesignator.Tool.Cancel));
    }

    // 밭 구역은 도구가 셋이다 — **`새 구역`과 `확장`을 가른다.** 하나로 두고 *"고른 게
    // 없으면 새 구역"*으로 했더니 **무엇이 될지가 고른 상태에 숨어 있어** 누르기 전에는
    // 알 수 없었다(사용자).
    void Farm()
    {
        var cur = FarmZoneDesignator.Current;

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

    void Simple(string aName, string aTip, string bName, string bTip,
                bool primaryOn, Action onPrimary, Action onCancel)
    {
        if (extraItem != null && extraItem.gameObject.activeSelf) extraItem.gameObject.SetActive(false);

        Bind(primaryItem, aName, aTip, primaryOn,  onPrimary);
        Bind(cancelItem,  bName, bTip, !primaryOn, onCancel);
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
