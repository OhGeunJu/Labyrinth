using UnityEngine;
using UnityEngine.EventSystems;

// 헤더 오브젝트에 붙이고 Target에 이동시킬 패널 RectTransform을 연결하세요.
// Target을 비워두면 자기 자신을 이동시킵니다.
[RequireComponent(typeof(RectTransform))]
public class DraggablePanel : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [SerializeField] RectTransform target;

    RectTransform _target;

    void Start()
    {
        _target = target != null ? target : GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData) => _target?.SetAsLastSibling();

    public void OnDrag(PointerEventData eventData)
    {
        if (_target == null) return;
        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        _target.anchoredPosition += eventData.delta / scale;
    }
}
