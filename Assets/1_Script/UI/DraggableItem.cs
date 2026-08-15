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

    // `owner: this` — **아이콘이 사라지면 툴팁도 같이 닫힌다.** 동전을 더블클릭해 잔고로
    // 넣으면 이 아이콘이 파괴되는데, 그때 `OnPointerExit`는 오지 않는다(`ItemInfoUI` 참고).
    public void OnPointerEnter(PointerEventData _)
    {
        if (_instance?.Data == null) return;

        // 진열은 **값까지** 적는다 — 칸에는 아이콘과 개수뿐이라 여기가 아니면 값을 볼 자리가
        // 고른 뒤의 구매 줄밖에 없다.
        int price = _sourceGrid != null && _sourceGrid.IconPrice != null
                  ? _sourceGrid.IconPrice(_instance) : -1;

        ItemInfoUI.Show(_instance.Data, _instance.Quality, price, null, _instance.Locked, this);
    }

    public void OnPointerExit(PointerEventData _) => ItemInfoUI.Hide();

    public void OnPointerClick(PointerEventData e)
    {
        // **우클릭 = 한 번에 보내기.** "저기"가 어디냐는 무엇이 열려 있느냐가 정한다 —
        // **상점이 열려 있으면 저기는 상점이다**(진열에서 누르면 사고, 배낭에서 누르면 판다).
        // 그 규칙의 확장이지 새 조작이 아니다. 수량은 드래그 쪼개기와 **같은 조합키**를 쓴다
        // (그냥=전부 / Shift=절반 / Ctrl=하나) — 같은 창에서 같은 키가 다른 뜻이면 안 된다.
        if (e.button == PointerEventData.InputButton.Right)
        {
            if (MerchantUI.HandleRightClick(_instance, _sourceGrid, ModifierAmount())) return;

            InventoryUI.NotifyItemRightClick(_instance, _sourceGrid);
            return;
        }

        // 진열의 좌클릭은 **고르는 것**이고 더블클릭은 사는 것이다(배낭 쪽과 다른 물음이라
        // `InventoryUI`가 아니라 격자가 들고 있는 콜백으로 간다).
        if (_sourceGrid != null && _sourceGrid.IsShop)
        {
            if (e.clickCount == 2) _sourceGrid.OnIconDoubleClicked?.Invoke(_instance);
            else                   _sourceGrid.OnIconClicked?.Invoke(_instance);
            return;
        }

        // **Shift/Ctrl은 클릭이 아니라 드래그에서 갈린다**(`OnBeginDrag`) — 그냥 누르기만
        // 하면 평소대로 고르거나 더블클릭 동작이 돈다.
        if (e.clickCount == 2)
            InventoryUI.NotifyItemDoubleAction(_instance, _sourceGrid);
        else
            InventoryUI.NotifyItemSelected(_instance, _sourceGrid);
    }

    // ── 얼마를 끌고 갈 것인가 ────────────────────────────────────────────────
    //
    // **Shift = 절반, Ctrl = 하나, 그냥 = 전부.**
    //
    // 쪼개는 시점이 **드래그를 시작할 때**인 것이 요점이다. 클릭으로 그 자리에서 쪼개면
    // "쪼갠 다음 그걸 다시 끌어야" 해서 두 동작이 되고, 무엇보다 **어디에 놓을지가 안 정해진
    // 채로** 격자에 새 뭉치가 생긴다. 시작할 때 갈라 두면 그 뒤로는 평범한 드래그라
    // **놓을 수 있는 곳이 전부 그대로 통한다** — 격자·장비칸·상인 창·바닥까지.
    //
    // (커서에 얹어 두고 다음 클릭에 놓는 마인크래프트식도 생각했지만, 그건 "지금 커서가 무엇을
    //  들고 있나"라는 상태를 새로 만들고 **받는 쪽을 전부 고쳐야** 한다. 여기서는 드래그가
    //  이미 그 상태를 들고 있다.)
    // 조합키가 정하는 개수. **드래그 쪼개기·우클릭 거래·드래그 거래가 이 하나를 같이 쓴다** —
    // 같은 창에서 같은 키가 다른 뜻이면 손이 매번 확인해야 한다.
    // `int.MaxValue`는 "전부"라는 뜻이다(부르는 쪽이 개수로 잘라 쓴다).
    int DragAmount() => ModifierAmount();

    int ModifierAmount()
    {
        var kb = Keyboard.current;
        if (kb == null || _instance == null || _instance.Quantity < 2) return int.MaxValue;

        if (kb.ctrlKey.isPressed)  return 1;
        if (kb.shiftKey.isPressed) return _instance.Quantity / 2;
        return int.MaxValue;   // 전부
    }

    // **집어 든 쪽이 새 뭉치다 — 남는 쪽이 원래 자리를 지킨다.**
    //
    // 처음엔 반대로 했다(끌던 인스턴스를 남기고 나머지를 떼어 냄). 그러면 **안 집은 절반이
    // 첫 빈 칸으로 튀어 간다** — 20개를 정리해 둔 자리에서 절반만 집었을 뿐인데 남은 것이
    // 배낭 맨 앞으로 옮겨 앉는다. 집어 든 것은 어차피 커서를 따라가므로 **어디에 있든
    // 상관없는 쪽은 그쪽**이다.
    void SplitForDrag()
    {
        _splitPending = false;

        int take = DragAmount();
        if (take == int.MaxValue) return;

        var inv = _sourceGrid != null ? _sourceGrid.Inventory : null;
        if (inv == null) return;

        // **알리지 않고 쪼갠다** — 지금 알리면 격자가 아이콘을 다시 만들면서 **끌고 있는
        // 이것**이 파괴된다. `OnBeginDrag` 끝(캔버스로 옮긴 뒤)에 알린다.
        //
        // **진열에서는 늘 손에 든다**(아래 `_detached` 갈래). 좌판에 갈라 놓으면 뭉치가 둘이
        // 되는데, 그러면 값표(`Shop.Stock`)의 한 줄이 좌판의 두 뭉치를 가리키게 되어 사는 쪽에서
        // 어느 것을 뺄지가 흐려진다. 손에 들면 좌판에서는 그냥 **그만큼 줄어든 것**이고,
        // 살 때 되돌려 놓은 뒤 평소대로 팔려 나간다(`MerchantUI.HandleDragTrade`).
        var taken = _sourceGrid.IsShop ? null : inv.SplitInstance(_instance, take, notify: false);

        if (taken == null)
        {
            // **자리가 없어도 쪼갠다 — 격자에 놓지 않고 손에 든다.**
            //
            // 꽉 찬 상자에서 절반만 배낭으로 옮기는 것이 가장 흔한 쓰임인데, 그때 **갈라 나온
            // 쪽이 놓일 빈 칸이 그 상자에 없어서** 통째로 끌렸다. 어차피 커서를 따라가는
            // 물건이라 격자에 자리를 잡을 이유가 없다.
            //
            // 대신 **놓기에 실패하면 되돌려야 한다**(`OnEndDrag`) — 격자에 없으므로 그냥 두면
            // 그만큼이 조용히 사라진다.
            _instance.Quantity -= take;
            taken = new ItemInstance(_instance.Data, take,
                                     _instance.GridX, _instance.GridY,
                                     _instance.Rotated, _instance.Quality)
            {
                Locked = _instance.Locked,
            };
            _detached = true;
        }

        _instance     = taken;   // 이제 이 아이콘이 끌고 가는 것은 갈라 나온 쪽이다
        _splitPending = true;
        RefreshCountLabel();
    }

    // 격자에 등록되지 않은 채 끌고 있는가(위의 폴백). 놓기에 실패하면 출처로 되돌린다.
    bool _detached;

    // 쪼갠 직후의 개수를 아이콘에 반영한다 — 안 하면 10을 끌면서 20이라고 적혀 있다.
    void RefreshCountLabel() => ShowCount(_instance.Quantity);

    void ShowCount(int n)
    {
        var label = GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (label != null) label.text = n > 1 ? n.ToString() : "";
    }


    bool _splitPending;

    public void OnBeginDrag(PointerEventData e)
    {
        ItemInfoUI.Hide();
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null || _sourceGrid == null) return;

        // **끌 만큼만 남기고 나머지를 떼어 놓는다**(Shift=절반 / Ctrl=하나). 여기서 갈라 두면
        // 그 뒤로는 평범한 드래그라 놓을 수 있는 곳이 전부 그대로 통한다.
        SplitForDrag();

        DraggedInstance   = _instance;
        DraggedSourceGrid = _sourceGrid;
        _dragging         = this;
        DropHandled       = false;

        _origParent  = transform.parent;
        _dragRotated = _instance.Rotated;

        // **쪼갠 것은 잡은 자리가 없다.** `_instance`가 방금 갈라 나온 새 뭉치라 격자 좌표가
        // 사용자가 누른 칸과 무관하다 — 그 차이를 오프셋으로 쓰면 엉뚱한 칸에 놓인다.
        var clickGrid = _sourceGrid.ScreenToGrid(e.position);
        _clickCellOffset = !_splitPending && clickGrid.x != int.MinValue
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

        // **이제 알린다 — 아이콘을 캔버스로 옮긴 뒤다.**
        //
        // 쪼갤 때 바로 알리면 격자가 아이콘을 전부 다시 만들면서 **지금 끌고 있는 이것**이
        // 파괴된다. 그래서 옮기고 나서 알린다 — `ClearItems`는 격자 안의 것만 지우므로
        // 캔버스로 나온 이 아이콘은 살아남는다.
        //
        // 이 한 줄이 있어야 **남은 절반이 원래 자리에 곧바로 보인다.** 드롭할 때까지 빈칸이면
        // 얼마가 남았는지 알 수 없어서, 반을 집었는지 전부를 집었는지도 확인할 수가 없다.
        if (_splitPending) _sourceGrid.Inventory?.NotifyChanged();
    }

    // **회전은 `OnDrag`가 아니라 여기서 받는다.**
    //
    // `IDragHandler.OnDrag`는 **포인터가 움직인 프레임에만** 불린다. 거기서 Q/E를 읽으면
    // 마우스를 가만히 둔 채 누른 키는 그 프레임에 아무도 안 읽어 **그대로 사라진다**
    // (`wasPressedThisFrame`은 한 프레임짜리다). 증상은 "한 번 돌리고 나면 마우스를 움직여야
    // 다시 돌아간다"로 나타났다 — 실은 움직이는 동안 우연히 겹친 것만 먹힌 것이다.
    //
    // 끌고 있는 **그 아이콘 하나만** 일한다(`_dragging`). 나머지는 비교 한 번에 돌아간다.
    static DraggableItem _dragging;

    void Update()
    {
        if (_dragging != this) return;

        var kb = Keyboard.current;
        if (kb == null) return;
        if (!kb.qKey.wasPressedThisFrame && !kb.eKey.wasPressedThisFrame) return;

        // **Q/E는 건설 모드가 고스트를 돌리는 키와 같다**(`BuildModeManager`) — 같은 뜻이라
        // 손이 한 번 배우면 두 곳에서 통한다. 격자 회전은 상태가 둘뿐이라 Q와 E가 같은 일을
        // 하는데, 어느 쪽을 눌러도 되는 편이 "왼쪽이 어느 쪽이더라"를 안 묻는 것보다 낫다.
        _dragRotated = !_dragRotated;
        UpdateDragVisual();

        // **잡은 지점을 새 모양 안으로 당긴다.** 1×3의 세 번째 칸을 쥐고 3×1로 돌리면 그
        // 오프셋(0,2)이 높이 1짜리 모양 밖을 가리켜, **미리보기가 커서에서 두 칸 위로 튄다.**
        var before = _clickCellOffset;

        int w = _dragRotated ? _instance.Data.Height : _instance.Data.Width;
        int h = _dragRotated ? _instance.Data.Width  : _instance.Data.Height;
        _clickCellOffset = new Vector2Int(Mathf.Clamp(_clickCellOffset.x, 0, w - 1),
                                          Mathf.Clamp(_clickCellOffset.y, 0, h - 1));

        // **손에 든 그림도 그만큼 옮긴다 — 다시 재지 말고 *바뀐 만큼만* 더한다.**
        //
        // 아이콘 자리는 `커서 − _clickPixelOffset`인데, 그 값은 집을 때
        // `cursorCanvas − anchoredPosition`으로 쟀다. `cursorCanvas`는 **캔버스 한가운데** 기준이고
        // `anchoredPosition`은 **앵커(좌상단)** 기준이라, 그 차이(화면 절반!)가 이미 이 값에
        // 녹아 있다. 그걸 모르고 "잡은 칸의 한가운데"로 **새로 재서 덮었더니** 그 절반이
        // 사라져 **아이콘이 화면 밖으로 날아갔다**(Q를 누른 순간 우주로 발사됐다).
        //
        // 잡은 칸이 몇 칸 움직였는지만 더하면 앵커 항은 그대로 남고, 칸 안에서 어디를
        // 쥐었는지(반 칸 단위)도 그대로 간다.
        if (_sourceGrid != null && before != _clickCellOffset)
        {
            float cs = _sourceGrid.CellSize;
            _clickPixelOffset += new Vector2( (_clickCellOffset.x - before.x) * cs,
                                             -(_clickCellOffset.y - before.y) * cs);
        }

        // **그 자리에서 다시 붙인다 — 미리보기만이 아니라 손에 든 것도.**
        //
        // 잡은 지점이 방금 바뀌었으므로 아이콘 자리도 다시 잡아야 한다. 미리보기만 새로
        // 그렸더니 **돌린 첫 프레임에는 둘이 어긋나 보이고 마우스를 한 칸 옮겨야 맞았다** —
        // `OnDrag`가 그제서야 아이콘을 옮겨 주기 때문이다.
        if (Mouse.current != null) FollowCursor(Mouse.current.position.ReadValue());
    }

    public void OnDrag(PointerEventData e) => FollowCursor(e.position);

    // 아이콘을 커서에 붙이고 미리보기를 그린다. **끌 때와 돌릴 때가 같이 쓴다** —
    // 돌리기가 이걸 안 부르면 **그 프레임에는 손에 든 것만 옛 자리에 남아** 미리보기와
    // 어긋나 보이고, 마우스를 한 칸 움직여야 제자리를 찾는다(실제로 그랬다).
    void FollowCursor(Vector2 screenPos)
    {
        if (_canvas == null) return;

        Camera cam = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform, screenPos, cam, out var local))
        {
            _rt.anchoredPosition = local - _clickPixelOffset;
        }

        UpdatePreview(screenPos, cam);
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

        var cell = PlacementCell(hoverGrid, cursorCell);
        bool isExternal = hoverGrid != _sourceGrid;
        hoverGrid.ShowPreview(_instance, cell.x, cell.y, _dragRotated, isExternal);
        _previewGrid = hoverGrid;
    }

    // 놓일 칸은 **격자가 답한다**(`InventoryGridUI.PlacementCell`) — 장비칸 드래그도 같은 답을 쓴다.
    Vector2Int PlacementCell(InventoryGridUI grid, Vector2Int cursorCell)
        => grid.PlacementCell(cursorCell, _instance.Data, _dragRotated, _clickCellOffset);

    void UpdateDragVisual()
    {
        if (_sourceGrid == null) return;
        float cs = _sourceGrid.CellSize;
        int w = _dragRotated ? _instance.Data.Height : _instance.Data.Width;
        int h = _dragRotated ? _instance.Data.Width  : _instance.Data.Height;
        // **크기만 바꾸고 트랜스폼은 안 돌린다.**
        //
        // 전에는 둘을 같이 했다 — 칸 수를 맞바꾼 `sizeDelta`에 `-90°` 회전까지 얹었으니
        // **두 번 돌아** 결국 원래 방향으로 보였고, 게다가 이 아이콘의 피벗이 좌상단이라
        // 회전이 상자를 커서 옆으로 휙 던졌다. 손에 든 것이 놓일 자리와 다른 모양·다른 자리에
        // 있으면 미리보기를 아무리 잘 그려도 소용이 없다.
        //
        // 크기만 맞바꾸면 **손에 든 것이 곧 발자국**이라 미리보기와 정확히 겹친다.
        // 그림은 `preserveAspect`가 비율을 지켜 안에서 맞춰진다.
        _rt.sizeDelta     = new Vector2(w * cs - 4, h * cs - 4);
        _rt.localRotation = Quaternion.identity;
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
        _dragging         = null;
        DropHandled       = false;

        var c = _img.color; c.a = 1f; _img.color = c;
        _img.raycastTarget = true;
        _rt.localRotation = Quaternion.identity;

        // **손에 든 채 갈 곳을 못 찾았으면 출처로 되돌린다.** 격자에 등록돼 있지 않으므로
        // 그냥 두면 그만큼이 조용히 사라진다(개수가 새는 것은 가장 나쁜 종류의 버그다).
        //
        // **넣은 만큼만 세고, 못 넣은 것은 바닥에 놓는다.** 출처가 꽉 차서 갈라진 것이라
        // 되돌릴 자리도 없을 수 있다 — `TryAdd`의 반환값을 안 보면 그 차이만큼이 증발한다.
        // (여기서는 떨구는 것이 맞다: 거절할 조작이 이미 끝났고, 되돌릴 곳이 없다.)
        if (_detached && !placed)
        {
            var back = _sourceGrid != null ? _sourceGrid.Inventory : null;
            int kept  = back != null ? back.TryAdd(_instance.Data, _instance.Quantity, _instance.Quality) : 0;
            int spill = _instance.Quantity - kept;

            if (spill > 0)
            {
                var at = back != null ? back.transform.position : Vector3.zero;
                DroppedItem.Spawn(_instance.Data, spill, at + Vector3.up * 0.15f, _instance.Quality);
            }
        }
        _detached = false;

        var grid = _sourceGrid;
        Destroy(gameObject);

        // **쪼갠 것을 여기서 갚는다.** 시작할 때 `notify: false`로 갈랐으므로(끌던 아이콘이
        // 파괴되지 않게) 떼어 놓은 나머지가 아직 화면에 없다. 놓기에 실패했든 성공했든
        // 다시 그려야 한다 — 실패했으면 갈라진 채로 제자리에 남는 것이 맞다(끌어다 놓기를
        // 무르는 것과 쪼개기를 무르는 것은 다른 일이다).
        if (_splitPending) grid?.Inventory?.NotifyChanged();
        else if (grid != null && !placed) grid.RefreshItems();

        // **놓은 뒤에 다시 그린다 — 성공했어도.**
        //
        // 옮기는 동안 격자는 **끌고 있는 것을 일부러 안 그린다**(`DraggedInstance` 건너뛰기 —
        // 그게 커서를 따라가는 아이콘과 겹치는 것을 막는다). 그런데 `MoveItem`이 자리를 바꾸며
        // 울리는 `OnChanged`는 **아직 끌고 있는 그 프레임에** 도착한다 — 그 다시 그리기는 옮긴
        // 것을 빼고 그리고, 그 뒤로 아무도 다시 안 그렸다. **옮긴 물건이 안 보이다가 다른 것을
        // 건드려야 나타났다.**
        //
        // 여기는 `DraggedInstance`를 이미 비운 뒤라 이제 그려진다. 건너간 경우 받은 쪽도 같이
        // 그린다(사고팔기·통 간 이동).
        if (placed)
        {
            grid?.RefreshItems();
            if (_lastTarget != null && _lastTarget != grid) _lastTarget.RefreshItems();
        }
        _lastTarget = null;

        _splitPending = false;
    }

    // ── 그리드 배치 ───────────────────────────────────────────────────────────

    // 마지막으로 놓은 격자 — `OnEndDrag`가 **놓은 뒤에** 그것도 다시 그린다(위 주석).
    InventoryGridUI _lastTarget;

    bool TryPlace(PointerEventData e)
    {
        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        var targetGrid = InventoryGridUI.FindUnderCursor(e.position, uiCam);
        if (targetGrid == null) return false;

        _lastTarget = targetGrid;

        var cursorCell = targetGrid.ScreenToGrid(e.position);
        if (cursorCell.x == int.MinValue) return false;

        // **미리보기가 가리킨 그 칸**(`PlacementCell`) — 보이는 자리와 놓이는 자리는 같아야 한다.
        var placeAt = PlacementCell(targetGrid, cursorCell);
        int newX = placeAt.x;
        int newY = placeAt.y;

        // **상점 격자를 건너가면 이동이 아니라 거래다.** 같은 격자 안(좌판 정리·배낭 정리)은
        // 평소대로 옮기고, 좌판↔배낭을 건너는 순간 값이 오간다 — 안 가르면 진열대에서
        // 배낭으로 끌어다 놓는 것만으로 공짜가 된다.
        //
        // **합치기(`TryMerge`)보다 먼저 본다** — 배낭의 화살 뭉치를 좌판의 화살 위에 떨어뜨리면
        // 그쪽이 먼저 걸려 값도 안 받고 넘어간다.
        if (targetGrid != _sourceGrid && (targetGrid.IsShop || (_sourceGrid?.IsShop ?? false)))
        {
            // **끌고 있는 뭉치가 곧 개수다** — 다른 격자와 똑같다(조합키가 시작할 때 갈랐다).
            // `_detached`는 그 조각이 아직 좌판 밖에 있다는 뜻이라, 사는 쪽이 되돌려 놓고 판다.
            return MerchantUI.HandleDragTrade(_instance, _sourceGrid, targetGrid, _detached,
                                              new Vector2Int(newX, newY), _dragRotated);
        }

        // **같은 것 위에 떨어뜨리면 합친다.** `TryAdd`는 넣을 때 알아서 병합하지만,
        // 이미 칸에 놓인 두 뭉치를 합칠 길이 없었다 — 한 번 나뉘면 영영 나뉜 채였다.
        // 위치 이동보다 **먼저** 본다(같은 칸이면 이동은 어차피 제자리다).
        if (TryMerge(targetGrid, cursorCell)) return true;

        // 같은 그리드 — 위치 이동, 안 되면 **자리 교환**
        if (targetGrid == _sourceGrid)
        {
            var inv = _sourceGrid.Inventory;
            if (inv == null) return false;

            // **갈라 든 것은 격자에 없다**(꽉 찬 통에서 쪼갠 경우 — `_detached`).
            // `MoveItem`·`SwapItems`는 `_items`에 있는 것만 옮기므로 **늘 실패**하고,
            // 그러면 되돌리기가 원래 뭉치에 도로 합쳐 **쪼갠 것이 없던 일**이 된다
            // (놓은 칸이 아니라 원래 자리로 돌아가서 "왜 안 옮겨지지"가 된다).
            // 격자에 없으니 옮기는 것이 아니라 **놓는 것**이 맞다.
            if (_detached)
                return inv.TryAddAt(_instance.Data, _instance.Quantity, newX, newY,
                                    _dragRotated, _instance.Quality, _instance.Locked);

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

        // **좌판에서 끌어온 것은 파는 것이 아니다** — 안 막으면 상인 창 위에 떨구는 것만으로
        // "판매 실패"가 뜬다(내 배낭에 없는 것을 팔려 드는 셈이다). 되돌리기는 `OnEndDrag`가 한다.
        if (_sourceGrid != null && _sourceGrid.IsShop) return false;

        if (MerchantUI.ActiveShop == null || MerchantUI.SellZoneRect == null) return false;
        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(MerchantUI.SellZoneRect, e.position, uiCam))
            return false;
        // **끌어온 만큼 판다** — 드래그가 이미 개수를 정했다(쪼개 왔으면 그만큼).
        return MerchantUI.TrySellItem(_instance, _instance.Quantity);
    }

    // ── 월드 드롭 ─────────────────────────────────────────────────────────────

    bool TryWorldDrop(PointerEventData e)
    {
        // **잠근 것은 못 버린다.** 창 밖으로 끌면 그대로 땅에 떨어지는데, 그게 이 조작에서
        // 유일하게 되돌릴 수 없는 것이다 — 잠금이 막아야 할 게 있다면 바로 이것이다.
        // false를 돌려주면 `OnEndDrag`가 미배치로 보고 원래 자리로 되돌린다.
        if (_instance != null && _instance.Locked) return false;

        // **남의 진열은 창 밖으로 못 꺼낸다** — 값도 안 치르고 땅에 떨구는 길이 된다.
        if (_sourceGrid != null && _sourceGrid.IsShop) return false;

        // **UI 위에서는 절대 안 버린다 — 창 밖으로 나가야 떨어진다.**
        //
        // 전에는 **격자만** 봤다. 그래서 창 안이어도 격자가 아닌 자리(장비 열, 액션 바,
        // 패널 여백)에 놓으면 물건이 바닥에 떨어졌다 — 아직 배낭 안에서 자리를 옮기던
        // 중인데 밖에 버린 것이 됐다.
        //
        // 끌고 있는 아이콘은 `OnBeginDrag`에서 `raycastTarget`을 껐으므로 자기 자신을
        // 짚지 않는다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return false;

        var inv = _sourceGrid?.Inventory;
        if (inv == null) return false;

        var dropPos = inv.transform.position + Vector3.up * 0.15f;
        inv.RemoveInstance(_instance);

        // **자동 운반은 안 켜진 채로 떨어진다** — 이제 그게 모든 드롭의 기본이라 여기서
        // 따로 끌 것이 없다(예전엔 손 드롭만 예외로 껐다). 나르게 하려면 그 물건을
        // 우클릭해 [창고로 옮긴다].
        DroppedItem.Spawn(_instance.Data, _instance.Quantity, dropPos, _instance.Quality);
        return true;
    }
}
