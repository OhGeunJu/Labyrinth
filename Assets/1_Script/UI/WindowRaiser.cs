using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// **지금 만지고 있는 창이 앞으로 온다.**
//
// 전에는 순서가 **씬에 적힌 그대로 고정**이었다. 그래서 상자를 열면 무슨 짓을 해도 배낭이
// 위에 있고, 상자를 보려면 배낭을 옆으로 끌어 놓는 수밖에 없었다 — 창이 여럿 뜨는 화면
// (배낭 ↔ 창고·작업대·시체·상점)에서는 **어느 것이 위인가가 곧 조작 가능성**이다.
//
// 헤더를 **끌기 시작할 때**는 이미 올라왔다(`DraggablePanel.OnBeginDrag`). 그런데 창을 만지는
// 일의 대부분은 끄는 것이 아니라 **안을 누르는 것**이다(격자·버튼·탭). 그래서 규칙을 넓힌다:
// **누른 것이 속한 창이 앞으로 온다.**
//
// ── 씬 배선이 필요 없다 ───────────────────────────────────────────────────────
//
// `HaulDesignator`·`DraftController`와 같은 자리 — **아무 상태도 안 들고 저작할 것도 없는 순수
// 입력기**다. 창마다 컴포넌트를 붙이게 하면 열다섯 군데를 배선해야 하고, **나중에 창을 하나
// 더 만들 때 그것만 조용히 안 올라온다.**
//
// ── 왜 `LateUpdate`인가 ───────────────────────────────────────────────────────
//
// 같은 프레임에 `EventSystem`이 눌린 것을 먼저 처리한다 — 격자 아이콘을 집으면 그 아이콘이
// **캔버스로 나와 맨 위에 선다**(`DraggableItem`). `Update`에서 창을 올리면 그 둘의 순서가
// 프레임마다 달라져, 어떤 때는 **손에 든 물건이 창 뒤로 숨는다.** 늦게 돌면 그 경합이 없다.
public class WindowRaiser : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SelfStanding.Create<WindowRaiser>("WindowRaiser");
    }

    // ── 창을 앞으로 ──────────────────────────────────────────────────────────
    //
    // **"창"은 캔버스의 직속 자식이다.** 이 저장소가 이미 그 눈금을 쓴다 — 옛 모드 전환(`GameModeManager`, 10-09에 걷어냄)이
    // 필드/월드를 갈아 끼울 때 캔버스가 아니라 **그 직속 자식들**을 켜고 끈다. 그리는 순서도
    // 형제 순서라, 창 하나를 통째로 올리는 단위가 정확히 그것이다.
    public static void ToFront(Transform inside)
    {
        var window = WindowOf(inside);
        if (window == null) return;

        // **이미 맨 위면 아무것도 안 한다.** 툴팁은 커서가 얹혀 있는 동안 `Show`가 매 프레임
        // 불릴 수 있는데(`HoverTooltipUI`), 그때마다 순서를 다시 매기면 캔버스가 매 프레임
        // 더러워져 **레이아웃이 헛돈다.** 순서를 바꾸는 일은 실제로 바뀔 때만 한다.
        var parent = window.parent;
        if (parent == null || window.GetSiblingIndex() == parent.childCount - 1) return;

        window.SetAsLastSibling();
    }

    // **창을 여는 문.** 켜고 · 맨 앞으로 올리고 · 늘 위에 있어야 하는 것들을 되올린다.
    //
    // 클릭으로 올리는 것(`LateUpdate`)만으로는 모자라다: **상자를 누르면 창이 열리는데**, 그건
    // 월드 클릭이라 UI 레이캐스트에 안 걸린다. 이미 열려 있는 상자를 다시 누르면 창이 그
    // 자리에 그대로 있고 배낭이 그 위를 덮은 채라, *"눌렀는데 아무 일도 안 일어난다"*가 된다.
    //
    // **여는 곳마다 두 줄을 적게 하지 않는다** — `SetActive(true)`를 부르는 곳이 스무 곳이라,
    // 나중에 창을 하나 더 만들면 **그것만 조용히 안 올라온다.** 여는 길을 하나로 두면
    // 붙이는 것을 잊을 자리가 없다.
    public static void Open(GameObject panel)
    {
        if (panel == null) return;

        // **뜨는 모양도 여기 하나가 정한다**(2026-08-29). 여는 길이 이미 하나이므로 `SetActive`를
        // `UiTween.PopIn`으로 바꾸는 것만으로 **창 전부가 따라온다** — 창마다 적으면 그중 하나는
        // 반드시 `SetUpdate(true)`를 빠뜨리고, 그 창만 일시정지 중에 안 열린다.
        UiTween.PopIn(panel);
        ToFront(panel.transform);
        KeepOverlaysOnTop();
    }

    // **창을 닫는 문.** 여는 쪽과 짝이다 — 흐려진 뒤 꺼진다.
    //
    // **`SetActive(false)`를 전부 이걸로 바꾸지는 않는다.** 옛 모드 전환처럼
    // *상태를 갈아 끼우는* 자리는 즉시 꺼져야 한다 — 거기서 0.1초를 끌면 필드 UI가 월드맵
    // 위에 잠깐 겹쳐 보인다. **사람이 닫는 것만** 이 문으로 온다.
    public static void Close(GameObject panel) => UiTween.FadeOut(panel);

    static Transform WindowOf(Transform t)
    {
        var canvas = t != null ? t.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return null;

        // 캔버스 바로 아래에 닿을 때까지 올라간다.
        var cur = t;
        while (cur != null && cur.parent != canvas.transform) cur = cur.parent;
        return cur;
    }

    void LateUpdate()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.leftButton.wasPressedThisFrame && !mouse.rightButton.wasPressedThisFrame) return;

        var es = EventSystem.current;
        if (es == null || !es.IsPointerOverGameObject()) return;

        // **커서 아래 맨 위의 것**을 집는다. `RaycastAll`은 앞에서부터 준다.
        // 버퍼는 들고 있는다 — 누를 때마다 새로 만들 이유가 없다.
        _data ??= new PointerEventData(es);
        _data.position = mouse.position.ReadValue();

        _hits.Clear();
        es.RaycastAll(_data, _hits);
        if (_hits.Count == 0) return;

        ToFront(_hits[0].gameObject.transform);
        KeepOverlaysOnTop();
    }

    // ── 늘 위에 있어야 하는 것들 ─────────────────────────────────────────────
    //
    // 창을 올리면 **그 위에 있던 것이 밑으로 깔린다.** 그런데 밑으로 가면 안 되는 것들이 있다:
    //
    //   · **손에 든 아이콘** — 커서를 따라다니는 것이 창 뒤로 숨으면 무엇을 들고 있는지 안 보이고,
    //     놓기에 실패했을 때(흔들림) 그 흔들림도 안 보인다.
    //   · **툴팁 둘**(`ItemInfoUI`·`HoverTooltipUI`) — 커서 옆에 뜨는 설명이라, 가려지면 정작
    //     설명을 보려고 올린 창이 그 설명을 덮는다.
    //
    // **켜져 있는지 안 묻는다** — 꺼져 있는 것을 맨 위로 올려도 아무 일이 안 일어나고, 물으면
    // *"툴팁이 지금 떠 있나"*를 여기서 또 판정하게 된다(그건 그쪽 창의 몫이다).
    static void KeepOverlaysOnTop()
    {
        // 우클릭 창은 스스로도 올라오지만(`OpenAtScreen`), 그 위에 창을 올리면 덮인다.
        if (PawnContextMenuUI.Instance != null) ToFront(PawnContextMenuUI.Instance.transform);

        if (ItemInfoUI.Instance != null)     ToFront(ItemInfoUI.Instance.transform);
        if (HoverTooltipUI.Instance != null) ToFront(HoverTooltipUI.Instance.transform);

        // **손에 든 것이 가장 위다** — 마지막에 올린다.
        var carried = DraggableItem.CarriedIcon;
        if (carried != null) ToFront(carried.transform);   // 손에 든 것은 이미 캔버스 직속이다
    }

    PointerEventData _data;
    readonly System.Collections.Generic.List<RaycastResult> _hits = new();
}
