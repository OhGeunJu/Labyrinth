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
