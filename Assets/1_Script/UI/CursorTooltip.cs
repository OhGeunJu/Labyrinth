using UnityEngine;
using UnityEngine.InputSystem;

// **커서 옆에 뜨는 창을 어디에 놓을 것인가 — 답은 한 벌이다.**
//
// 커서 툴팁이 둘이다(`ItemInfoUI`·`HoverTooltipUI`). 둘 다 *커서 옆에 두되 화면 밖으로는
// 안 나간다*는 같은 규칙을 따르는데, 계산이 **두 벌**이었다 — `HoverTooltipUI`에는
// *"계산은 `ItemInfoUI.FollowCursor`와 같다(툴팁이 둘인데 위치 규칙이 다르면 안 된다)"*고
// **적혀만 있고**, 실제로는 뒤집기가 빠져 있었다.
//
// 그래서 아래쪽·오른쪽 끝에서 갈렸다: 아이템 툴팁은 반대편으로 뒤집혀 피해 갔는데, 스탯·건물
// 툴팁은 **화면 안으로 밀려 들어와 정작 가리키던 것을 덮었다**(건설 목록에서 오른쪽 타일에
// 마우스를 올리면 그 줄이 통째로 가려졌다).
//
// ## 밀어 넣지 말고 뒤집는다
//
// 넘칠 때 화면 안으로 미는 것이 자연스러워 보이지만, 그러면 툴팁이 **커서 밑으로 기어들어와**
// 가리키는 것을 덮는다 — 설명이 설명을 방해한다. 반대편에 놓으면 커서와의 거리는 그대로이고
// 덮는 것은 *지나온 쪽*이 된다.
//
// 그래도 넘치면(툴팁이 화면보다 클 때) 그때는 민다 — 잘려서 반만 보이는 것보다 낫다.
//
// **피벗은 좌상단을 가정한다**(`anchoredPosition`이 왼쪽 위 모서리). 두 툴팁 다 그렇게 지어져
// 있고, 여기가 그 약속을 아는 유일한 자리다.
public static class CursorTooltip
{
    // 커서와 툴팁 사이. **딱 붙지만 않으면 된다** — 멀리 떨어뜨리면 무엇에 딸린 설명인지가
    // 흐려지고 눈이 그만큼 더 움직인다.
    public const float Gap = 12f;

    public static void Place(RectTransform panel, Canvas canvas, Vector2 cursorOffset)
    {
        if (panel == null || canvas == null || Mouse.current == null) return;

        Camera cam = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var    cRt = (RectTransform)canvas.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cRt, Mouse.current.position.ReadValue(), cam, out var local)) return;

        Vector2 cSize = cRt.sizeDelta;
        Vector2 pSize = panel.sizeDelta;

        // **커서에서 한 뼘 떨어뜨린다.** 손에 든 물건이 커서 한가운데에 붙으므로
        // (`DraggableItem.GrabAtCenter`) 딱 붙이면 그 물건 위에 겹치고, 가리키기만 해도
        // 그 칸의 그림을 덮는다.
        Vector2 pos = local + cursorOffset + new Vector2(Gap, -Gap);

        if (pos.x + pSize.x >  cSize.x * 0.5f) pos.x = local.x + cursorOffset.x - Gap - pSize.x;
        if (pos.y - pSize.y < -cSize.y * 0.5f) pos.y = local.y + cursorOffset.y + Gap + pSize.y;

        pos.x = Mathf.Clamp(pos.x, -cSize.x * 0.5f, cSize.x * 0.5f - pSize.x);
        pos.y = Mathf.Clamp(pos.y, -cSize.y * 0.5f + pSize.y, cSize.y * 0.5f);

        panel.anchoredPosition = pos;
    }
}

// **툴팁이 언제 사라지나 — 이것도 답이 한 벌이어야 한다**(2026-08-29).
//
// 커서를 A에서 B로 **곧장** 옮기면 유니티는 `OnPointerEnter(B)`와 `OnPointerExit(A)`를 같은
// 프레임에 보내는데 **순서를 보장하지 않는다.** Exit이 나중에 오면 방금 띄운 B의 툴팁을
// A가 꺼 버린다 — 화면에는 *"빈 곳을 거쳐 가면 둘 다 보이는데, 곧장 옮기면 안 보인다"*로
// 나타난다(스탯 창에서 근접공격 → 방어로 바로 옮길 때 실제로 그랬다).
//
// **그래서 끄는 것을 프레임 끝까지 미룬다.** 그 프레임에 누가 띄웠으면 안 끈다 — 순서를 안
// 봐도 되고, **띄운 주인을 인자로 넘기게 하지 않아도 된다**(부르는 곳이 여섯이라 인자를 하나
// 늘리면 그중 하나는 반드시 안 넘기고, 그 경로에서만 툴팁이 남는다).
//
// **커서 툴팁 둘이 같은 모양이라 같은 버그를 갖고 있었다**(`ItemInfoUI`·`HoverTooltipUI`) —
// 자리 계산(`CursorTooltip.Place`)을 여기 모은 것과 같은 이유로 이 판정도 여기 둔다.
public struct HoverHold
{
    // **0이 "없음"이다.** 구조체 기본값이 0이고 `Time.frameCount`는 UI가 도는 시점엔
    // 이미 0보다 크므로, 따로 초기화할 자리가 없어도 된다.
    int _shown, _hide;

    public void Shown()     => _shown = Time.frameCount;
    public void HideAsked() => _hide  = Time.frameCount;

    // 이번 프레임에 정말 감출 것인가. **묻는 순간 소모된다** — 프레임마다 한 번만 참이다.
    public bool ShouldHide()
    {
        bool hide = _hide != 0 && _shown < _hide;
        _hide = 0;
        return hide;
    }
}
