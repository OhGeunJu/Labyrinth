using UnityEngine;
using UnityEngine.EventSystems;

// 지도 이미지 위의 클릭을 **정규화 좌표**로 바꿔 `WorldMapPanel`에 넘긴다.
//
// 패널이 직접 받지 않는 이유: 클릭을 받아야 하는 것은 **지도 그림 그 자체**이고, 패널에는
// 테두리·제목처럼 눌러도 아무 일이 없어야 하는 자리가 있다. 그림에만 붙인다.
//
// **올리면 거기가 어디인지 말한다**(2026-09-06, 사용자: *"균사림이 맵에 없는데 어케봐"*).
// 실은 지도의 23%가 균사림이었고 시작 지점 바로 옆에도 있었다 — **색은 칠해져 있는데
// 이름이 아무 데도 안 적혀 있었다.** 색으로 바이옴 여덟을 외우게 하는 것은 지도가 아니라
// 퀴즈다. **범례를 따로 안 만든다** — 커서가 짚은 자리 하나만 답하면 되고, 그 답을 띄우는
// 창은 이미 있다(`HoverTooltipUI`).
[RequireComponent(typeof(RectTransform))]
public class WorldMapClickArea : MonoBehaviour,
    IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
{
    [SerializeField] WorldMapPanel panel;

    public void OnPointerClick(PointerEventData e)
    {
        if (!Normalized(e, out var uv) || panel == null) return;
        panel.LookAt(uv);
    }

    public void OnPointerMove(PointerEventData e)
    {
        if (!Normalized(e, out var uv) || panel == null) { HoverTooltipUI.Hide(); return; }
        panel.Describe(uv);
    }

    // **떠날 때 거둔다** — 안 거두면 지도를 닫아도 설명만 화면에 남는다(줄이 파괴되면
    // `OnPointerExit`가 안 오던 그 자리와 같은 규칙).
    public void OnPointerExit(PointerEventData e) => HoverTooltipUI.Hide();

    void OnDisable() => HoverTooltipUI.Hide();

    // 피벗이 어디든 같은 답이 나오게 `rect`를 기준으로 잰다.
    bool Normalized(PointerEventData e, out Vector2 uv)
    {
        uv = default;

        var rt = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, e.position, e.pressEventCamera, out var local)) return false;

        var r = rt.rect;
        uv = new Vector2(Mathf.Clamp01((local.x - r.xMin) / r.width),
                         Mathf.Clamp01((local.y - r.yMin) / r.height));
        return true;
    }
}
