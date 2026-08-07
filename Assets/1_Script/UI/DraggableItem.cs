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
            ItemInfoUI.Show(_instance.Data, _instance.Quality, -1, null, _instance.Locked);
    }

    public void OnPointerExit(PointerEventData _) => ItemInfoUI.Hide();

    public void OnPointerClick(PointerEventData e)
    {
        if (e.clickCount == 2)
            InventoryUI.NotifyItemDoubleAction(_instance, _sourceGrid);
        else
            InventoryUI.NotifyItemSelected(_instance, _sourceGrid);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        ItemInfoUI.Hide();
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

        // **같은 것 위에 떨어뜨리면 합친다.** `TryAdd`는 넣을 때 알아서 병합하지만,
        // 이미 칸에 놓인 두 뭉치를 합칠 길이 없었다 — 한 번 나뉘면 영영 나뉜 채였다.
        // 위치 이동보다 **먼저** 본다(같은 칸이면 이동은 어차피 제자리다).
        if (TryMerge(targetGrid, cursorCell)) return true;

        // 같은 그리드 — 위치 이동, 안 되면 **자리 교환**
        if (targetGrid == _sourceGrid)
        {
            var inv = _sourceGrid.Inventory;
            if (inv == null) return false;
            if (inv.MoveItem(_instance, newX, newY, _dragRotated)) return true;

            // 못 옮겼다면 대개 **다른 물건이 그 자리에 있어서**다(합칠 수 없는 것이거나 꽉 찬 것).
            // 그냥 실패시키면 좁은 배낭에서 배치를 바꿀 방법이 없다 — 서로 자리를 바꾼다.
            var other = inv.GetAt(cursorCell.x, cursorCell.y);
            return other != null && inv.SwapItems(_instance, other);
        }

        // 다른 그리드 — 컨테이너 간 이동
        var srcInv = _sourceGrid?.Inventory;
        var dstInv = targetGrid.Inventory;
        if (srcInv == null || dstInv == null) return false;
        if (!targetGrid.CanReceiveItem(_instance.Data)) return false;

        // **딸린 것을 같이 옮긴다.** 옮기는 건 같은 물건이다 — 등급을 안 넘기면
        // 희귀 철검을 끌어다 놓는 것만으로 일반이 된다(예전에 그랬다).
        //
        // 잠금은 **가는 곳에 따라 다르다.** 잠금은 "이건 이 폰 것"이라는 뜻이라
        // 폰 배낭 사이에서는 따라가야 하고(다른 일꾼에게 맡기는 것이지 푸는 게 아니다),
        // 창고·작업대·시체로 내려놓는 건 공용 자리에 내놓는 것이므로 거기서 끝난다.
        bool keepLock = _instance.Locked && IsPawnInventory(dstInv);

        if (!dstInv.TryAddAt(_instance.Data, _instance.Quantity, newX, newY, _dragRotated,
                             _instance.Quality, keepLock)) return false;
        srcInv.RemoveInstance(_instance);
        return true;
    }

    // 그 인벤토리가 **폰의 배낭**인가(창고·작업대·시체가 아니라).
    static bool IsPawnInventory(Inventory inv) =>
        inv != null && inv.GetComponent<UnitBrain>() != null;

    // 커서 아래 칸의 뭉치와 합친다. 합쳤으면 true.
    //
    // 조건: **같은 아이템 · 같은 등급 · 자기 자신이 아닐 것.** 등급을 섞으면 어느 쪽 등급이
    // 되는지 말할 수 없고(`Inventory.TryAdd`도 등급별로만 쌓는다), 자기 자신과 합치면 개수가
    // 두 배가 된다.
    bool TryMerge(InventoryGridUI targetGrid, Vector2Int cursorCell)
    {
        var dstInv = targetGrid.Inventory;
        var srcInv = _sourceGrid?.Inventory;
        if (dstInv == null || srcInv == null) return false;

        var target = dstInv.GetAt(cursorCell.x, cursorCell.y);
        if (target == null || ReferenceEquals(target, _instance)) return false;
        if (target.Data != _instance.Data || target.Quality != _instance.Quality) return false;

        int room = _instance.Data.MaxStack - target.Quantity;
        if (room <= 0) return false;   // 이미 꽉 찼으면 합치지 말고 평소대로 이동시킨다

        // 다른 창고로 합칠 때는 그 창고가 받는 물건인지 본다(필터).
        if (targetGrid != _sourceGrid && !targetGrid.CanReceiveItem(_instance.Data)) return false;

        int move = Mathf.Min(room, _instance.Quantity);
        target.Quantity   += move;
        _instance.Quantity -= move;

        // **남으면 원래 자리에 그대로 둔다.** 다 옮겼을 때만 원본을 치운다 —
        // 안 그러면 넘친 만큼이 증발한다.
        if (_instance.Quantity <= 0) srcInv.RemoveInstance(_instance);   // 여기서 OnChanged가 울린다
        else                         srcInv.NotifyChanged();

        // 받는 쪽은 `Quantity`를 직접 고쳤으므로 따로 알린다(창고 집계가 옛 값을 안 쓰게).
        if (!ReferenceEquals(dstInv, srcInv)) dstInv.NotifyChanged();

        targetGrid.RefreshItems();
        if (targetGrid != _sourceGrid) _sourceGrid.RefreshItems();
        return true;
    }

    // ── 판매 드롭 ─────────────────────────────────────────────────────────────

    bool TrySellDrop(PointerEventData e)
    {
        if (_instance != null && _instance.Locked) return false;   // 잠근 것은 못 판다
        if (MerchantUI.ActiveShop == null || MerchantUI.SellZoneRect == null) return false;
        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(MerchantUI.SellZoneRect, e.position, uiCam))
            return false;
        return MerchantUI.TrySellItem(_instance);
    }

    // ── 월드 드롭 ─────────────────────────────────────────────────────────────

    bool TryWorldDrop(PointerEventData e)
    {
        // **잠근 것은 못 버린다.** 창 밖으로 끌면 그대로 땅에 떨어지는데, 그게 이 조작에서
        // 유일하게 되돌릴 수 없는 것이다 — 잠금이 막아야 할 게 있다면 바로 이것이다.
        // false를 돌려주면 `OnEndDrag`가 미배치로 보고 원래 자리로 되돌린다.
        if (_instance != null && _instance.Locked) return false;

        // 어떤 그리드 위에서 실패한 경우는 월드 드롭 안 함
        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        if (InventoryGridUI.FindUnderCursor(e.position, uiCam) != null) return false;

        var inv = _sourceGrid?.Inventory;
        if (inv == null) return false;

        var dropPos = inv.transform.position + Vector3.up * 0.15f;
        inv.RemoveInstance(_instance);

        // **손으로 내려놓은 것은 자동 운반에서 뺀다.** 그러지 않으면 운반을 켠 폰이 곧바로
        // 주워 창고에 넣어, 내려놓기가 사실상 "창고로 보내기"가 된다 — 어디에 두고 싶어도
        // 둘 수가 없다. 다시 나르게 하려면 그 물건을 우클릭해 [창고로 옮긴다].
        DroppedItem.Spawn(_instance.Data, _instance.Quantity, dropPos, _instance.Quality,
                          noAutoHaul: true);
        return true;
    }
}
