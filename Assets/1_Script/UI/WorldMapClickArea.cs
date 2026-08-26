using UnityEngine;
using UnityEngine.EventSystems;

// 지도 이미지 위의 클릭을 **정규화 좌표**로 바꿔 `WorldMapPanel`에 넘긴다.
//
// 패널이 직접 받지 않는 이유: 클릭을 받아야 하는 것은 **지도 그림 그 자체**이고, 패널에는
// 테두리·제목처럼 눌러도 아무 일이 없어야 하는 자리가 있다. 그림에만 붙인다.
[RequireComponent(typeof(RectTransform))]
public class WorldMapClickArea : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] WorldMapPanel panel;

    public void OnPointerClick(PointerEventData e)
    {
        if (panel == null) return;

        var rt = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, e.position, e.pressEventCamera, out var local)) return;

        // 피벗이 어디든 같은 답이 나오게 `rect`를 기준으로 잰다.
        var r = rt.rect;
        float u = Mathf.Clamp01((local.x - r.xMin) / r.width);
        float v = Mathf.Clamp01((local.y - r.yMin) / r.height);
        panel.LookAt(new Vector2(u, v));
    }
}
