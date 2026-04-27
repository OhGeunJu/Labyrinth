using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 슬롯 간 드래그 → 인벤토리 이동
// UI 밖으로 드래그 → 월드 드롭
public class InventorySlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerClickHandler
{
    public ItemData Item   { get; private set; }
    public int      Amount { get; private set; }

    Inventory  _inventory;
    Transform  _unitTransform;
    Canvas     _rootCanvas;
    GameObject _dragGhost;
    Action     _onClicked;

    // OnDrop → OnEndDrag 순으로 실행되므로 플래그로 전달
    bool _dropHandled;

    public void Init(ItemData item, int amount, Inventory inventory, Transform unitTransform)
    {
        Item           = item;
        Amount         = amount;
        _inventory     = inventory;
        _unitTransform = unitTransform;

        var canvas = GetComponentInParent<Canvas>();
        _rootCanvas = canvas != null ? canvas.rootCanvas : null;
    }

    public void SetClickHandler(Action onClicked) => _onClicked = onClicked;

    public void OnPointerClick(PointerEventData eventData) => _onClicked?.Invoke();

    // ── 드래그 시작 ───────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_rootCanvas == null) return;

        _dragGhost = new GameObject("DragGhost");
        _dragGhost.transform.SetParent(_rootCanvas.transform, false);
        _dragGhost.transform.SetAsLastSibling();

        var img = _dragGhost.AddComponent<Image>();
        img.sprite        = Item.Icon;
        img.raycastTarget = false;

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

    // ── 다른 슬롯 위에 드롭 → 인벤토리 이동 ─────────────────────────
    public void OnDrop(PointerEventData eventData)
    {
        var source = eventData.pointerDrag?.GetComponent<InventorySlotUI>();
        if (source == null || source == this) return;
        if (source._inventory == null || _inventory == null) return;
        if (source._inventory == _inventory) return;

        // Consume 먼저 (성공 시에만 Add) → 아이템 손실 방지
        if (source._inventory.Consume(source.Item, source.Amount))
        {
            _inventory.Add(source.Item, source.Amount);
            source._dropHandled = true;
        }
    }

    // ── 드래그 끝 ─────────────────────────────────────────────────────
    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragGhost != null)
        {
            Destroy(_dragGhost);
            _dragGhost = null;
        }

        // OnDrop에서 이미 처리됨
        if (_dropHandled) { _dropHandled = false; return; }

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
