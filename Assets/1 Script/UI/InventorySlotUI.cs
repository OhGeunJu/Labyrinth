using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 인벤토리 슬롯 드래그 드롭.
// UI 안에서 손 놓으면 취소, UI 밖(게임 월드)에서 손 놓으면 해당 위치에 드롭.
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ItemData Item   { get; private set; }
    public int      Amount { get; private set; }

    Inventory _inventory;
    Transform _unitTransform;
    Canvas    _rootCanvas;
    GameObject _dragGhost;

    public void Init(ItemData item, int amount, Inventory inventory, Transform unitTransform)
    {
        Item           = item;
        Amount         = amount;
        _inventory     = inventory;
        _unitTransform = unitTransform;

        var canvas = GetComponentInParent<Canvas>();
        _rootCanvas = canvas != null ? canvas.rootCanvas : null;
    }

    // ── 드래그 시작 ───────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_rootCanvas == null) return;

        _dragGhost = new GameObject("DragGhost");
        _dragGhost.transform.SetParent(_rootCanvas.transform, false);
        _dragGhost.transform.SetAsLastSibling();

        var img = _dragGhost.AddComponent<Image>();
        img.sprite        = Item.Icon;
        img.raycastTarget = false;  // 고스트가 레이캐스트 막지 않도록

        var rect = _dragGhost.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(50f, 50f);

        MoveGhostTo(eventData);
    }

    // ── 드래그 중 ─────────────────────────────────────────────────────
    public void OnDrag(PointerEventData eventData)
    {
        if (_dragGhost == null) return;
        MoveGhostTo(eventData);
    }

    // ── 드래그 끝 ─────────────────────────────────────────────────────
    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragGhost != null)
        {
            Destroy(_dragGhost);
            _dragGhost = null;
        }

        // UI 위에서 손 놓으면 취소
        if (eventData.pointerCurrentRaycast.gameObject != null) return;

        // UI 밖 → 월드에 드롭
        DropToWorld(eventData.position);
    }

    // ── 내부 ─────────────────────────────────────────────────────────
    void MoveGhostTo(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPos);

        (_dragGhost.transform as RectTransform).localPosition = localPos;
    }

    void DropToWorld(Vector2 screenPos)
    {
        if (_inventory == null || Item == null) return;

        // 스크린 좌표 → 지면 좌표 변환
        Vector3 dropPos = _unitTransform.position + _unitTransform.forward * 0.8f;
        if (Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
                dropPos = ray.GetPoint(dist);
        }

        if (_inventory.Consume(Item, Amount))
            DroppedItem.Spawn(Item, Amount, dropPos);
    }
}
