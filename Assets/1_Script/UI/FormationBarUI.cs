using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// **지금 무슨 진형인가 - 화면에 띄우고 눌러서 바꾼다**(2026-09-08).
//
// `ModeBarUI`와 **같은 물건**이다: 상태를 안 들고 매 프레임 물어본다. 여기서 따로 기억하면
// **키(`F1`~`F5`)로 바꿨을 때 버튼만 옛 상태로 남는다.**
//
// == 소집 중에만 뜬다 =======================================================
//
// 진형은 소집의 것이다(`MoveController`가 소집 안 한 무리에게는 밀집을 쓴다). 늘 띄워 두면
// **눌러도 아무 일도 안 일어나는 버튼들**이 화면에 상주하고, 그건 *진형이 안 먹는다*는
// 오해를 만든다. 소집한 순간 나타나는 편이 **언제 쓰는 것인지를 그 자체로 말한다.**
//
// 재는 것은 **고른 폰 중에 소집된 것이 있나**다 - 그게 곧 *다음 우클릭이 진형을 쓸 조건*이라,
// 뜨는 조건과 먹는 조건이 같아진다(갈리면 *바는 떴는데 안 먹는* 자리가 생긴다).
public class FormationBarUI : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public Button Button;
        public Formation.Shape Shape;
    }

    [SerializeField] List<Slot> slots = new();

    // 창 규약: `<기준>_UI`는 스크립트만 든 늘 켜진 뿌리, 보였다 사라지는 그림은 이 자식이다.
    [SerializeField] GameObject panel;

    [Header("색")]
    [SerializeField] Color idle   = new(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] Color active = new(0.78f, 0.62f, 0.20f, 1f);

    // **알림을 띄울지 여기서 답한다** - 바가 이미 답하고 있으면 알림은 겹치는 말이고,
    // 안 떠 있으면 알림이 **유일한 답**이다(`FormationController`가 이걸 본다).
    public static bool IsShowing { get; private set; }

    SelectionManager _selection;

    void OnDisable() => IsShowing = false;

    void Awake()
    {
        foreach (var s in slots)
        {
            if (s.Button == null) continue;
            var shape = s.Shape;                    // **지역 변수로 잡는다** - 람다가 루프
            s.Button.onClick.AddListener(() => Formation.Current = shape);   // 변수를 캡처하면
        }                                                                    // 전부 마지막 것이 된다
    }

    void Update()
    {
        bool show = AnyDrafted();
        IsShowing = show;

        if (panel != null && panel.activeSelf != show) panel.SetActive(show);
        if (!show) return;

        foreach (var s in slots)
        {
            if (s.Button == null) continue;
            var img = s.Button.targetGraphic as Image;
            if (img != null) img.color = Formation.Current == s.Shape ? active : idle;
        }
    }

    bool AnyDrafted()
    {
        // 씬 오브젝트라 매번 찾지 않고 들고 있는다(정착지를 갈아타면 다시 찾는다).
        if (_selection == null) _selection = FindFirstObjectByType<SelectionManager>();
        if (_selection == null) return false;

        foreach (var pawn in _selection.Selected)
        {
            if (pawn == null) continue;             // 고른 뒤 파괴된 폰
            var brain = pawn.GetComponent<UnitBrain>();
            if (brain != null && brain.IsDrafted) return true;
        }
        return false;
    }
}
