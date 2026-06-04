using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 인벤토리 아이콘에 붙어서 드래그/드롭/회전 처리.
// InventoryGridUI가 아이콘 생성 시 자동 추가.
public class DraggableItem : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // EquipSlotUI.OnDrop에서 참조
    public static ItemInstance     DraggedInstance { get; private set; }
    public static InventoryGridUI  DraggedSourceGrid { get; private set; }
    public static bool             DropHandled;

    ItemInstance     _instance;
    InventoryGridUI  _sourceGrid;
    RectTransform    _rt;
    Image            _img;
    Canvas           _canvas;

    Transform        _origParent;
    bool             _dragRotated;
    InventoryGridUI  _previewGrid; // 현재 프리뷰가 표시된 그리드

    Vector2Int _clickCellOffset;
    Vector2    _clickPixelOffset;

    public void Init(ItemInstance instance, InventoryGridUI sourceGrid)
    {
        _instance   = instance;
        _sourceGrid = sourceGrid;
        _rt         = (RectTransform)transform;
        _img        = GetComponent<Image>();
    }

    public void OnPointerEnter(PointerEventData _)
    {
        if (_instance?.Data != null)
            ItemInfoPanel.Show(_instance.Data, _instance.Quality);
    }

    public void OnPointerExit(PointerEventData _) => ItemInfoPanel.Hide();

    public void OnPointerClick(PointerEventData e)
    {
        if (e.clickCount == 2)
            PawnInventoryPanel.NotifyItemDoubleAction(_instance, _sourceGrid);
        else
            PawnInventoryPanel.NotifyItemSelected(_instance, _sourceGrid);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        ItemInfoPanel.Hide();
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null || _sourceGrid == null) return;

        DraggedInstance   = _instance;
        DraggedSourceGrid = _sourceGrid;
        DropHandled       = false;

        _origParent  = transform.parent;
        _dragRotated = _instance.Rotated;

        var clickGrid = _sourceGrid.ScreenToGrid(e.position);
        _clickCellOffset = clickGrid.x != int.MinValue
            ? new Vector2Int(clickGrid.x - _instance.GridX, clickGrid.y - _instance.GridY)
            : Vector2Int.zero;

        Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)_canvas.transform, e.position, cam, out var cursorCanvas);

        transform.SetParent(_canvas.transform, true);
        transform.SetAsLastSibling();

        _clickPixelOffset = cursorCanvas - _rt.anchoredPosition;

        var c = _img.color; c.a = 0.7f; _img.color = c;
        _img.raycastTarget = false;
    }

    public void OnDrag(PointerEventData e)
    {
        if (_canvas == null) return;

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            _dragRotated = !_dragRotated;
            UpdateDragVisual();
        }

        Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform, e.position, cam, out var local))
        {
            _rt.anchoredPosition = local - _clickPixelOffset;
        }

        UpdatePreview(e.position, cam);
    }

    void UpdatePreview(Vector2 screenPos, Camera uiCam)
    {
        var hoverGrid = InventoryGridUI.FindUnderCursor(screenPos, uiCam);

        // 그리드가 바뀌면 이전 프리뷰 제거
        if (_previewGrid != null && _previewGrid != hoverGrid)
        {
            _previewGrid.ClearPreview();
            _previewGrid = null;
        }

        if (hoverGrid == null) return;

        var cursorCell = hoverGrid.ScreenToGrid(screenPos);
        if (cursorCell.x == int.MinValue) return;

        int nx = cursorCell.x - _clickCellOffset.x;
        int ny = cursorCell.y - _clickCellOffset.y;
        bool isExternal = hoverGrid != _sourceGrid;
        hoverGrid.ShowPreview(_instance, nx, ny, _dragRotated, isExternal);
        _previewGrid = hoverGrid;
    }

    void UpdateDragVisual()
    {
        if (_sourceGrid == null) return;
        float cs = _sourceGrid.CellSize;
        int w = _dragRotated ? _instance.Data.Height : _instance.Data.Width;
        int h = _dragRotated ? _instance.Data.Width  : _instance.Data.Height;
        _rt.sizeDelta     = new Vector2(w * cs - 4, h * cs - 4);
        _rt.localRotation = _dragRotated
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.identity;
    }

    public void OnEndDrag(PointerEventData e)
    {
        _previewGrid?.ClearPreview();
        _previewGrid = null;

        bool placed = TryPlace(e);
        if (!placed) placed = TrySellDrop(e);
        if (!placed) placed = DropHandled;   // EquipSlotUI.OnDrop이 처리한 경우
        if (!placed) placed = TryWorldDrop(e);

        DraggedInstance   = null;
        DraggedSourceGrid = null;
        DropHandled       = false;

        var c = _img.color; c.a = 1f; _img.color = c;
        _img.raycastTarget = true;
        _rt.localRotation = Quaternion.identity;

        var grid = _sourceGrid;
        Destroy(gameObject);
        if (grid != null && !placed) grid.RefreshItems();
    }

    // ── 그리드 배치 ───────────────────────────────────────────────────────────

    bool TryPlace(PointerEventData e)
    {
        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        var targetGrid = InventoryGridUI.FindUnderCursor(e.position, uiCam);
        if (targetGrid == null) return false;

        var cursorCell = targetGrid.ScreenToGrid(e.position);
        if (cursorCell.x == int.MinValue) return false;

        int newX = cursorCell.x - _clickCellOffset.x;
        int newY = cursorCell.y - _clickCellOffset.y;

        // 같은 그리드 — 위치 이동
        if (targetGrid == _sourceGrid)
            return _sourceGrid.Inventory != null
                && _sourceGrid.Inventory.MoveItem(_instance, newX, newY, _dragRotated);

        // 다른 그리드 — 컨테이너 간 이동
        var srcInv = _sourceGrid?.Inventory;
        var dstInv = targetGrid.Inventory;
        if (srcInv == null || dstInv == null) return false;
        if (!targetGrid.CanReceiveItem(_instance.Data)) return false;
        if (!dstInv.TryAddAt(_instance.Data, _instance.Quantity, newX, newY, _dragRotated)) return false;
        srcInv.RemoveInstance(_instance);
        return true;
    }

    // ── 판매 드롭 ─────────────────────────────────────────────────────────────

    bool TrySellDrop(PointerEventData e)
    {
        if (MerchantUI.ActiveMerchant == null || MerchantUI.SellZoneRect == null) return false;
        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(MerchantUI.SellZoneRect, e.position, uiCam))
            return false;
        return MerchantUI.TrySellItem(_instance);
    }

    // ── 월드 드롭 ─────────────────────────────────────────────────────────────

    bool TryWorldDrop(PointerEventData e)
    {
        // 어떤 그리드 위에서 실패한 경우는 월드 드롭 안 함
        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        if (InventoryGridUI.FindUnderCursor(e.position, uiCam) != null) return false;

        var inv = _sourceGrid?.Inventory;
        if (inv == null) return false;

        var dropPos = inv.transform.position + Vector3.up * 0.15f;
        inv.RemoveInstance(_instance);
        DroppedItem.Spawn(_instance.Data, _instance.Quantity, dropPos);
        return true;
    }
}
