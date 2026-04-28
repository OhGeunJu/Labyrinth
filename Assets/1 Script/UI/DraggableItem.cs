using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 인벤토리 아이콘에 붙어서 드래그/드롭/회전 처리.
// InventoryGridUI가 아이콘 생성 시 자동 추가.
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    ItemInstance     _instance;
    InventoryGridUI  _sourceGrid;
    RectTransform    _rt;
    Image            _img;
    Canvas           _canvas;

    Transform _origParent;
    bool      _dragRotated;

    // 클릭 시점에 아이콘 좌상단 대비 어느 셀을 잡았는지 (셀 단위)
    Vector2Int _clickCellOffset;
    // 캔버스 로컬 공간에서 커서 ↔ 아이콘 좌상단의 픽셀 오프셋
    Vector2    _clickPixelOffset;

    public void Init(ItemInstance instance, InventoryGridUI sourceGrid)
    {
        _instance   = instance;
        _sourceGrid = sourceGrid;
        _rt         = (RectTransform)transform;
        _img        = GetComponent<Image>();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null || _sourceGrid == null) return;

        _origParent  = transform.parent;
        _dragRotated = _instance.Rotated;

        // 클릭 셀 오프셋 저장 (그리드 셀 단위)
        var clickGrid = _sourceGrid.ScreenToGrid(e.position);
        _clickCellOffset = new Vector2Int(clickGrid.x - _instance.GridX,
                                          clickGrid.y - _instance.GridY);

        // 캔버스 좌표로 변환 (재부모 전, 픽셀 오프셋 기록)
        Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        Vector2 cursorCanvas;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)_canvas.transform, e.position, cam, out cursorCanvas);

        // 캔버스 최상단으로 옮김 (월드 위치 유지)
        transform.SetParent(_canvas.transform, true);
        transform.SetAsLastSibling();

        // 재부모 후 아이콘의 현재 anchoredPosition을 좌상단 캔버스 위치로 사용
        _clickPixelOffset = cursorCanvas - _rt.anchoredPosition;

        // 시각 변경
        var c = _img.color; c.a = 0.7f; _img.color = c;
        _img.raycastTarget = false; // 자기가 드롭 레이캐스트를 막지 않도록
    }

    public void OnDrag(PointerEventData e)
    {
        if (_canvas == null) return;

        // R 키로 회전
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            _dragRotated = !_dragRotated;
            UpdateDragSize();
        }

        Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform, e.position, cam, out var local))
        {
            // 클릭 시 잡았던 픽셀 오프셋을 보존해서 따라오게
            _rt.anchoredPosition = local - _clickPixelOffset;
        }
    }

    void UpdateDragSize()
    {
        if (_sourceGrid == null) return;
        float cs = _sourceGrid.CellSize;
        int w = _dragRotated ? _instance.Data.Height : _instance.Data.Width;
        int h = _dragRotated ? _instance.Data.Width  : _instance.Data.Height;
        _rt.sizeDelta = new Vector2(w * cs - 4, h * cs - 4);
    }

    public void OnEndDrag(PointerEventData e)
    {
        bool placed = TryPlace(e);

        var c = _img.color; c.a = 1f; _img.color = c;
        _img.raycastTarget = true;

        var grid = _sourceGrid;
        Destroy(gameObject);
        if (grid != null && !placed) grid.RefreshItems();
    }

    bool TryPlace(PointerEventData e)
    {
        var targetGrid = FindGridUnderCursor(e);
        if (targetGrid == null) return false;
        if (targetGrid != _sourceGrid) return false; // 다중 컨테이너는 단계 6

        // 커서가 가리키는 셀 - 클릭 오프셋 = 아이템 좌상단이 들어갈 셀
        var cursorGrid = targetGrid.ScreenToGrid(e.position);
        int newX = cursorGrid.x - _clickCellOffset.x;
        int newY = cursorGrid.y - _clickCellOffset.y;

        return targetGrid.Inventory != null
            && targetGrid.Inventory.MoveItem(_instance, newX, newY, _dragRotated);
    }

    static InventoryGridUI FindGridUnderCursor(PointerEventData e)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(e, results);
        foreach (var r in results)
        {
            var grid = r.gameObject.GetComponentInParent<InventoryGridUI>();
            if (grid != null) return grid;
        }
        return null;
    }
}
