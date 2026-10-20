using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 인벤토리 아이콘. **집기 / 놓기 / 회전**을 든다.
// InventoryGridUI가 아이콘 생성 시 자동 추가.
//
// ── 조작은 "한 번 눌러 집고, 한 번 눌러 놓는다" ──────────────────────────────
//
// 예전엔 드래그였다(누른 채 끌어다 떨구기). 바꾼 이유는 **교체가 그 안에서 성립하지 않아서**다:
// A를 B 위에 놓으면 B가 손에 들려야 하는데, 드래그는 **버튼을 떼는 순간 끝나므로** 이어서
// 들고 있을 방법이 없다. 손에 든 상태를 따로 두면 그 문제가 없고, 덤으로 긴 배낭에서
// 먼 칸까지 버튼을 누른 채 끌고 갈 일도 없어진다.
//
// **놓는 규칙은 그대로다**(`TryPlace`) — 격자·장비칸·상인 창·판매 구역·바닥이 전부 예전
// 그대로 받는다. 바뀐 것은 **집고 놓는 방아쇠**뿐이다.
//
// | | 손이 비었을 때 | 손에 들었을 때 |
// |---|---|---|
// | **좌클릭** | 집는다(Shift=절반 / Ctrl=하나) | **놓는다** — 하나 걸리면 그것이 손에 들린다 |
// | **우클릭** | 이걸 저기로(반대편 통·착용·사고팔기·동전 회수) | **되돌린다** |
// | **Q/E** | — | 돌린다 |
//
// **더블클릭은 없앴다** — 첫 클릭이 이미 집으므로 두 번째를 기다릴 수가 없다. 그것이 하던
// 일(동전 회수·즉시 구매/판매)은 **우클릭이 이미 같은 자리에서 한다.**
public class DraggableItem : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
{
    // **손에 든 것.** 격자는 이것을 안 그리고(커서를 따라가는 아이콘과 겹친다),
    // 장비칸·상점은 "지금 무엇을 들고 있나"를 이걸로 묻는다.
    public static ItemInstance     CarriedInstance { get; private set; }
    public static InventoryGridUI  CarriedSourceGrid { get; private set; }

    // **한 번의 물리적 클릭이 집기와 놓기를 둘 다 하지 않게.** 집기는 `OnPointerDown`,
    // 놓기는 손에 든 아이콘의 `Update`가 받는데 둘의 실행 순서는 정해져 있지 않다 —
    // 같은 프레임이면 나중 것이 물러난다.
    static int _actionFrame = -1;

    // 손에 든 것을 우클릭으로 되돌렸다 — 그 클릭의 **떼는 쪽**을 한 번 삼킨다(`OnPointerClick`).
    static bool _cancelClick;

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

        ItemInfoUI.Show(_instance.Data, _instance.Quality, price, null, _instance.Locked, this, _instance.Left);
    }

    public void OnPointerExit(PointerEventData _) => ItemInfoUI.Hide();

    // ── 집기 ─────────────────────────────────────────────────────────────────
    //
    // **누르는 순간 집는다**(`OnPointerClick`이 아니라 `OnPointerDown`). 놓기도 누르는
    // 순간이라, 둘을 같은 시점에 두어야 `_actionFrame` 한 줄로 겹침을 막을 수 있다.
    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (CarriedInstance != null) return;        // 놓는 중이다 — 손에 든 쪽이 받는다
        if (Time.frameCount == _actionFrame) return;

        _actionFrame = Time.frameCount;
        BeginCarry(e.position);
    }

    public void OnPointerClick(PointerEventData e)
    {
        // **우클릭 = 한 번에 보내기.** "저기"가 어디냐는 무엇이 열려 있느냐가 정한다 —
        // **상점이 열려 있으면 저기는 상점이다**(진열에서 누르면 사고, 배낭에서 누르면 판다).
        // 그 규칙의 확장이지 새 조작이 아니다. 수량은 드래그 쪼개기와 **같은 조합키**를 쓴다
        // (그냥=전부 / Shift=절반 / Ctrl=하나) — 같은 창에서 같은 키가 다른 뜻이면 안 된다.
        if (e.button == PointerEventData.InputButton.Right)
        {
            // 손에 들었으면 우클릭은 **되돌리기**다 — 그 처리는 손에 든 아이콘이 이미 했다
            // (`Update`). 여기서 또 하면 밑에 있던 물건이 엉뚱하게 실려 간다.
            //
            // **`_actionFrame`으로는 못 막는다.** 되돌리기는 **누르는 프레임**에 끝나는데
            // `OnPointerClick`은 **떼는 프레임**에 온다 — 그 사이에 `CarriedInstance`는 이미
            // 비었고 프레임 번호도 달라져, 가드 둘이 **전부 통과한다.** 그래서 손에 든 것을
            // 우클릭으로 되돌리면 **커서 밑의 물건까지 팔리거나 반대편 통으로 갔다.**
            // 되돌린 그 클릭은 **떼는 것까지 한 번**이므로, 눌렀을 때 표시해 두고 여기서 먹는다.
            if (_cancelClick) { _cancelClick = false; return; }
            if (CarriedInstance != null || Time.frameCount == _actionFrame) return;

            if (MerchantUI.HandleRightClick(_instance, _sourceGrid, ModifierAmount())) return;

            InventoryUI.NotifyItemRightClick(_instance, _sourceGrid, this);
            return;
        }

        // 좌클릭은 **누를 때 이미 집었다**(`OnPointerDown`). 여기서는 아무것도 안 한다 —
        // 고르기는 집으면서 같이 알린다(`BeginCarry`).
    }

    // ── 얼마를 끌고 갈 것인가 ────────────────────────────────────────────────
    //
    // **Shift = 절반, Ctrl = 하나, 그냥 = 전부.**
    //
    // 쪼개는 시점이 **집을 때**인 것이 요점이다. 그 자리에서 쪼개면 "쪼갠 다음 그걸 다시
    // 집어야" 해서 두 동작이 되고, 무엇보다 **어디에 놓을지가 안 정해진 채로** 격자에 새
    // 뭉치가 생긴다. 집을 때 갈라 두면 그 뒤로는 평범한 손에 든 것이라 **놓을 수 있는 곳이
    // 전부 그대로 통한다** — 격자·장비칸·상인 창·바닥까지.
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
        // 이것**이 파괴된다. `BeginCarry` 끝(캔버스로 옮긴 뒤)에 알린다.
        //
        // **진열에서는 늘 손에 든다**(아래 `_detached` 갈래). 좌판에 갈라 놓으면 뭉치가 둘이
        // 되는데, 그러면 값표(`Shop.Stock`)의 한 줄이 좌판의 두 뭉치를 가리키게 되어 사는 쪽에서
        // 어느 것을 뺄지가 흐려진다. 손에 들면 좌판에서는 그냥 **그만큼 줄어든 것**이고,
        // 살 때 되돌려 놓은 뒤 평소대로 팔려 나간다(`MerchantUI.HandleCarryTrade`).
        var taken = _sourceGrid.IsShop ? null : inv.SplitInstance(_instance, take, notify: false);

        if (taken == null)
        {
            // **자리가 없어도 쪼갠다 — 격자에 놓지 않고 손에 든다.**
            //
            // 꽉 찬 상자에서 절반만 배낭으로 옮기는 것이 가장 흔한 쓰임인데, 그때 **갈라 나온
            // 쪽이 놓일 빈 칸이 그 상자에 없어서** 통째로 끌렸다. 어차피 커서를 따라가는
            // 물건이라 격자에 자리를 잡을 이유가 없다.
            //
            // 대신 **놓기에 실패하면 되돌려야 한다**(`EndCarry`) — 격자에 없으므로 그냥 두면
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

    // 쪼갠 직후의 개수를 아이콘에 반영한다 — 안 하면 10을 손에 들고 20이라고 적혀 있다.
    // **격자와 같은 곳에서 붙인다**(`Decorate`) — 개수만 고치려고 첫 글자 자식을 집으면
    // 그림 없는 물건의 **이름**을 덮어쓴다.
    void RefreshCountLabel() => _sourceGrid?.Decorate(transform, _instance);


    bool _splitPending;

    void BeginCarry(Vector2 screenPos)
    {
        ItemInfoUI.Hide();
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null || _sourceGrid == null) return;

        // **잠금 모드에서는 클릭이 자물쇠다** — 집기보다 먼저 물어본다(안 물으면 잠그려다
        // 물건이 손에 들린다).
        if (!_sourceGrid.IsShop && InventoryUI.ClickConsumed(_instance, _sourceGrid)) return;

        // **끌 만큼만 남기고 나머지를 떼어 놓는다**(Shift=절반 / Ctrl=하나). 여기서 갈라 두면
        // 그 뒤로는 평범한 드래그라 놓을 수 있는 곳이 전부 그대로 통한다.
        SplitForDrag();

        CarriedInstance   = _instance;
        CarriedSourceGrid = _sourceGrid;
        _carrying         = this;

        _origParent  = transform.parent;
        _dragRotated = _instance.Rotated;

        transform.SetParent(_canvas.transform, true);
        transform.SetAsLastSibling();

        // **어디를 눌렀든 한가운데를 쥔다** — 집는 길이 셋인데(격자·장비칸·밀려난 것) 누른
        // 자리를 그대로 쓰면 그중 둘은 쥔 자리가 없어 규칙이 갈린다.
        CenterAnchor();
        GrabAtCenter();

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

        // **집는 것이 곧 고르는 것이다** — 액션 바의 [판매]·[줍기]가 가리키는 것과 손에 든
        // 것은 같아야 한다(예전엔 클릭이 고르기, 드래그가 집기라 둘이 갈렸다).
        //
        // **여기서 알린다 — 아이콘을 캔버스로 옮긴 뒤다.** 위 `_splitPending`과 같은 이유다:
        // 고르기가 화면을 다시 그리면 격자가 아이콘을 전부 새로 만드는데, 그전에 알리면
        // **방금 집은 이것이 그 자리에서 파괴된다**(격자 안에 있는 동안은 격자의 것이다).
        if (_sourceGrid.IsShop) _sourceGrid.OnIconClicked?.Invoke(_instance);
        else                    InventoryUI.NotifyItemSelected(_instance, _sourceGrid);
    }

    // **회전·따라가기·놓기를 여기서 받는다.**
    //
    // (드래그 시절의 `IDragHandler.OnDrag`는 **포인터가 움직인 프레임에만** 불렸다. 거기서 Q/E를 읽으면
    // 마우스를 가만히 둔 채 누른 키는 그 프레임에 아무도 안 읽어 **그대로 사라진다**
    // (`wasPressedThisFrame`은 한 프레임짜리다). 증상은 "한 번 돌리고 나면 마우스를 움직여야
    // 다시 돌아간다"로 나타났다 — 실은 움직이는 동안 우연히 겹친 것만 먹힌 것이다.
    // 이제는 손에 든 동안 매 프레임 도므로 그 함정이 아예 없다.)
    //
    // 손에 든 **그 아이콘 하나만** 일한다(`_carrying`). 나머지는 비교 한 번에 돌아간다.
    static DraggableItem _carrying;

    // 지금 손에 든 아이콘. **`WindowRaiser`가 창을 올린 뒤 이것을 다시 맨 위로 올린다** —
    // 커서를 따라다니는 것이 창 뒤로 숨으면 무엇을 들고 있는지도, 거절 흔들림도 안 보인다.
    public static DraggableItem CarriedIcon => _carrying;

    void Update()
    {
        TickGridShake();
        if (_carrying != this) return;

        var mouse = Mouse.current;
        if (mouse != null) FollowCursor(mouse.position.ReadValue());

        // **창이 닫히면 손에 든 것을 되돌린다.** 안 그러면 격자에서도 빠지고 화면에도 없는
        // 물건이 남는다 — 개수가 새는 것은 가장 나쁜 종류의 버그다.
        if (_sourceGrid == null || !_sourceGrid.isActiveAndEnabled)
        {
            EndCarry(mouse != null ? mouse.position.ReadValue() : Vector2.zero, cancel: true);
            return;
        }

        if (mouse != null && Time.frameCount != _actionFrame)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _actionFrame = Time.frameCount;
                EndCarry(mouse.position.ReadValue(), cancel: false);
                return;
            }
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _actionFrame  = Time.frameCount;
                _cancelClick  = true;   // 이 클릭의 **놓는 쪽**까지 삼킨다(아래)
                EndCarry(mouse.position.ReadValue(), cancel: true);
                return;
            }
        }

        var kb = Keyboard.current;
        if (kb == null) return;
        if (!kb.qKey.wasPressedThisFrame && !kb.eKey.wasPressedThisFrame) return;

        // **Q/E는 건설 모드가 고스트를 돌리는 키와 같다**(`BuildModeManager`) — 같은 뜻이라
        // 손이 한 번 배우면 두 곳에서 통한다. 격자 회전은 상태가 둘뿐이라 Q와 E가 같은 일을
        // 하는데, 어느 쪽을 눌러도 되는 편이 "왼쪽이 어느 쪽이더라"를 안 묻는 것보다 낫다.
        _dragRotated = !_dragRotated;
        UpdateDragVisual();

        // **돌린 모양의 한가운데를 다시 쥔다.** 예전엔 잡은 칸을 새 모양 안으로 당기고
        // 픽셀 오프셋을 *바뀐 만큼만* 더했는데(앵커 항이 그 값에 녹아 있어 다시 재면 안 됐다),
        // 앵커를 한가운데로 맞춘 지금은 그 항이 없어서 **그냥 다시 내면 된다.**
        GrabAtCenter();

        // **그 자리에서 다시 붙인다 — 미리보기만이 아니라 손에 든 것도.**
        //
        // 잡은 지점이 방금 바뀌었으므로 아이콘 자리도 다시 잡아야 한다. 미리보기만 새로
        // 그렸더니 **돌린 첫 프레임에는 둘이 어긋나 보이고 마우스를 한 칸 옮겨야 맞았다** —
        // 아이콘을 옮기는 것이 한 박자 늦기 때문이다.
        if (Mouse.current != null) FollowCursor(Mouse.current.position.ReadValue());
    }

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
            // 거절 흔들림 — 남은 시간이 줄면서 진폭도 준다(툭 끊기지 않게).
            float left = _shakeUntil - Time.unscaledTime;
            if (left > 0f)
                local.x += Mathf.Sin(left * ShakeFreq) * ShakeAmp * (left / ShakeSeconds);

            _rt.anchoredPosition = local - _clickPixelOffset;
        }

        UpdatePreview(screenPos, cam);
    }

    void UpdatePreview(Vector2 screenPos, Camera uiCam)
    {
        // **장비칸도 미리보기가 있어야 한다** — 격자만 칸을 그려 주고 장비칸은 아무 말이
        // 없어서, 손에 든 것을 칸 위에 올려도 *"여기 놓으면 장착되는가"*를 알 수가 없었다
        // (차 있는 칸에서는 아예 *"장착이 안 된다"*로 읽혔다 — 되는데도).
        EquipSlotUI.Hint(_instance?.Data, screenPos);

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

    // 놓는다. `cancel`이면 아무 데도 안 놓고 **되돌린다**(우클릭·창 닫힘).
    void EndCarry(Vector2 screenPos, bool cancel)
    {
        _previewGrid?.ClearPreview();
        _previewGrid = null;
        EquipSlotUI.ClearHint();

        bool placed = false;
        if (!cancel)
        {
            // **장비칸을 격자보다 먼저 묻는다.** 배낭 격자의 `RectTransform`이 **장비 열까지
            // 덮고 있어서**, 격자를 먼저 물으면 장비칸 위에 놓아도 배낭에 떨어진다 — 착용이
            // 아예 안 된다(드래그 시절엔 `IDropHandler`가 먼저 받아 이 순서가 안 드러났다).
            placed = EquipSlotUI.TryEquipCarried(this, screenPos);

            // **장비칸 위에서 거절당하면 거기서 끝이다.** 아래로 흘려보내면 **배낭 격자의
            // `RectTransform`이 장비 열까지 덮고 있어서**(그래서 장비칸을 먼저 묻는다) 못 찬
            // 물건이 배낭에 툭 떨어진다 — 사용자가 본 것은 *"그 칸에 안 놓인다"*지만
            // 실제로 일어난 일은 **엉뚱한 데로 갔다**였다. 못 차면 손에 든 채 흔든다.
            if (!placed && !EquipSlotUI.IsUnder(screenPos))
            {
                placed = TryPlace(screenPos);
                if (!placed) placed = TrySellDrop(screenPos);
                if (!placed) placed = TryWorldDrop(screenPos);
            }
        }

        // **하나만 내주고 나머지는 계속 든다**(장비칸에 한 개를 찬 경우).
        if (_keepCarrying)
        {
            _keepCarrying = false;
            _sourceGrid?.Decorate(transform, _instance);
            UpdateDragVisual();
            if (Mouse.current != null) FollowCursor(Mouse.current.position.ReadValue());
            _sourceGrid?.RefreshItems();
            return;
        }

        // **못 놓았으면 계속 들고 있는다 — 떨구지 않는다.**
        //
        // 예전엔 여기서 손을 털었다(아이콘을 없애고 물건은 제자리로, 갈라 든 것은 바닥으로).
        // 그런데 놓기가 실패하는 흔한 이유는 **둘에 걸쳤다**거나 **자리가 없다**는 것이라,
        // 사람은 **조금 옮겨서 다시 누르려는 참**이다 — 그때 물건이 손을 떠나면 다시 집어야
        // 하고, 바닥에 떨어지기라도 하면 주우러 가야 한다.
        //
        // **거절은 흔들어서 말한다** — 알림을 띄우면 창이 하나 더 뜨고, 아무 표시도 없으면
        // 클릭이 먹혔는지 알 수가 없다.
        if (!cancel && !placed)
        {
            Shake();
            if (Mouse.current != null) FollowCursor(Mouse.current.position.ReadValue());
            return;
        }

        // **밀어낸 것이 있으면 손에 이어서 든다** — 이 시스템의 요점이다.
        // 아이콘을 새로 만들지 않고 **이것을 그대로 쓴다**(그림·개수·크기만 갈아 끼운다).
        if (_pendingPickup != null)
        {
            var next     = _pendingPickup;
            var nextGrid = _pendingPickupGrid;
            _pendingPickup     = null;
            _pendingPickupGrid = null;

            var oldGrid = _sourceGrid;
            CarryInstead(next, nextGrid);

            oldGrid?.RefreshItems();
            if (nextGrid != null && nextGrid != oldGrid) nextGrid.RefreshItems();
            return;
        }

        CarriedInstance   = null;
        CarriedSourceGrid = null;
        _carrying         = null;

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
            // 되돌릴 통 — 출처가 사라졌으면 배낭으로. **바닥에 떨구는 것은 마지막 수단이고,
            // 그때도 자리를 아는 통이 있어야 한다**(없으면 원점에 떨어진다).
            var back = _sourceGrid != null ? _sourceGrid.Inventory : null;
            if (back == null && InventoryUI.Instance != null && InventoryUI.Instance.Grid != null)
                back = InventoryUI.Instance.Grid.Inventory;
            // 남은 양째 — 열린 것이 먼저 들어간다(`TryAddLike`). 하나도 못 넣었으면 떨구는 쪽에 열린 것이 있다.
            int kept  = back != null ? back.TryAddLike(_instance) : 0;
            int spill = _instance.Quantity - kept;

            if (spill > 0 && back != null)
            {
                var at = back.transform.position;
                DroppedItem.Spawn(_instance.Data, spill, at + Vector3.up * 0.15f, _instance.Quality,
                                  kept == 0 ? _instance.Left : 1f);
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
        // 옮기는 동안 격자는 **끌고 있는 것을 일부러 안 그린다**(`CarriedInstance` 건너뛰기 —
        // 그게 커서를 따라가는 아이콘과 겹치는 것을 막는다). 그런데 `MoveItem`이 자리를 바꾸며
        // 울리는 `OnChanged`는 **아직 끌고 있는 그 프레임에** 도착한다 — 그 다시 그리기는 옮긴
        // 것을 빼고 그리고, 그 뒤로 아무도 다시 안 그렸다. **옮긴 물건이 안 보이다가 다른 것을
        // 건드려야 나타났다.**
        //
        // 여기는 `CarriedInstance`를 이미 비운 뒤라 이제 그려진다. 건너간 경우 받은 쪽도 같이
        // 그린다(사고팔기·통 간 이동).
        if (placed)
        {
            grid?.RefreshItems();
            if (_lastTarget != null && _lastTarget != grid) _lastTarget.RefreshItems();
        }
        _lastTarget = null;

        _splitPending = false;
    }

    // ── 커서에 붙이는 값 ─────────────────────────────────────────────────────
    //
    // **손에 드는 순간 앵커를 캔버스 한가운데로 옮긴다**(`CenterAnchor`).
    //
    // `FollowCursor`는 `anchoredPosition = 커서로컬 − _clickPixelOffset`으로 놓는데,
    // `커서로컬`은 `ScreenPointToLocalPointInRectangle`이 주는 **캔버스 피벗(한가운데)** 기준이다.
    // 아이콘의 앵커가 격자에서 쓰던 **좌상단** 그대로면 두 값의 기준이 달라, 그 차이
    // (화면 절반!)를 `_clickPixelOffset`이 늘 이고 다녀야 한다 — 그 항을 빠뜨리면 아이콘이
    // 구석으로 밀리고(0으로 뒀다가 그랬다), 반대로 모르고 덮어쓰면 화면 밖으로 날아간다
    // (Q를 누르면 우주로 발사되던 그것).
    //
    // **기준을 맞춰 버리면 그 항이 아예 없어진다.** 손에 든 동안 아이콘은 격자의 자식이
    // 아니므로 앵커가 좌상단일 이유도 없다. 그러면 `_clickPixelOffset`이 뜻하는 것은
    // **"아이콘 안에서 커서가 쥔 자리" 하나**뿐이라 계산이 한 줄로 준다.
    void CenterAnchor()
    {
        if (_rt == null) return;
        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
    }

    // **커서는 물건의 한가운데를 쥔다.**
    //
    // 좌상단을 쥐면 물건이 커서의 오른쪽 아래에 통째로 깔려, **커서와 (나중에 붙일) 설명이
    // 그림을 가린다.** 한가운데면 어느 쪽으로도 절반씩만 나가므로 덜 가리고, 놓을 자리도
    // 커서 둘레로 퍼져 겨냥이 쉽다.
    //
    // **칸 오프셋과 픽셀 오프셋이 같은 곳을 가리켜야 한다** — 아니면 손에 든 그림과
    // 미리보기가 어긋난다. 짝수 폭은 한가운데가 칸 경계에 떨어지는데, 그때는 오른쪽·아래
    // 칸으로 친다(`w / 2`가 곧 그 값이다).
    void GrabAtCenter()
    {
        int w = _dragRotated ? _instance.Data.Height : _instance.Data.Width;
        int h = _dragRotated ? _instance.Data.Width  : _instance.Data.Height;
        float cs = _sourceGrid != null ? _sourceGrid.CellSize : 64f;

        _clickCellOffset  = new Vector2Int(w / 2, h / 2);
        _clickPixelOffset = new Vector2(w * cs * 0.5f, -h * cs * 0.5f);
    }

    // ── 밖에서 손에 쥐여 주기 ────────────────────────────────────────────────
    //
    // 격자가 아닌 자리(장비칸)에서 집을 때. **아이콘을 새로 하나 만든다** — 그 자리에는
    // 끌 아이콘이 없기 때문이다. 만들고 나면 그다음은 격자에서 집은 것과 똑같다
    // (회전·미리보기·놓기가 전부 이 클래스 하나에 있다).
    //
    // `returnGrid`는 **되돌릴 곳**이다(우클릭·창 닫힘). 장비칸에서 집었으면 배낭이 그곳이다.
    public static DraggableItem CarryDetached(ItemInstance inst, InventoryGridUI returnGrid,
                                              Vector2 screenPos)
    {
        if (inst?.Data == null) return null;

        var canvas = returnGrid != null ? returnGrid.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return null;

        var go = new GameObject($"Carried_{inst.Data.ItemName}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;          // 손에 든 것이 제 밑을 가리면 놓을 곳을 못 짚는다
        if (inst.Data.Icon != null) { img.sprite = inst.Data.Icon; img.preserveAspect = true; }
        else                        img.color  = new Color(0.25f, 0.35f, 0.25f, 0.85f);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);   // 커서로컬과 같은 기준
        rt.pivot     = new Vector2(0, 1);                        // 크기·미리보기는 좌상단 기준

        var drag = go.AddComponent<DraggableItem>();
        drag.Init(inst, returnGrid);
        drag._canvas = canvas;
        drag.CarryInstead(inst, returnGrid);

        // **집힌 것이 손에 들렸다는 신호**(2026-08-29). 집기·놓기는 드래그가 아니라 **한 번 눌러
        // 집고 한 번 눌러 놓는** 조작이라, 집힌 순간에 아무 변화가 없으면 *"눌렸나?"*가 된다 —
        // 격자에서 아이콘이 사라지는 것만으로는 커서 밑을 안 보고 있으면 놓친다.
        UiTween.Pop(go.transform);

        _actionFrame = Time.frameCount;     // 집은 그 클릭이 곧바로 놓지 않게
        return drag;
    }

    // 지금 손에 든 것(장비칸이 개수를 본다).
    public ItemInstance Carried => _instance;

    // 밀려난 것을 **손에 이어서 들려 준다**(장비칸에서 벗겨진 것).
    public void PickUpNext(ItemInstance inst, InventoryGridUI grid)
    {
        _pendingPickup     = inst;
        _pendingPickupGrid = grid;
    }

    // 하나만 가져갔다 — 손에는 나머지가 남는다(장비칸에 한 개만 찬 경우).
    bool _keepCarrying;
    public void KeepCarryingRest() => _keepCarrying = true;

    // ── 거절은 흔들어서 말한다 ───────────────────────────────────────────────
    //
    // 못 놓은 클릭에 아무 일도 안 일어나면 **눌리기는 한 건지**를 알 수가 없다. 알림을 띄우는
    // 것은 과하다(이유가 뻔하고, 자주 있는 일이다). 짧게 흔드는 것으로 족하다.
    //
    // **`unscaledTime`으로 잰다** — 배속·일시정지는 화면 조작과 상관이 없다
    // (`CameraController`가 `unscaledDeltaTime`을 쓰는 것과 같은 자리).
    const float ShakeSeconds = 0.18f;
    const float ShakeAmp     = 7f;    // 픽셀
    const float ShakeFreq    = 46f;

    float _shakeUntil;

    void Shake() => _shakeUntil = Time.unscaledTime + ShakeSeconds;

    // ── 격자에 놓인 아이콘의 반응 ─────────────────────────────────────────────
    //
    // 위 `Shake`는 **손에 든** 아이콘 몫이다 — 커서를 따라가는 `FollowCursor`가 흔들림을
    // 얹으므로, 격자에 가만히 있는 아이콘은 그 경로를 아예 안 탄다.
    //
    // **왜 격자 안에서 답하나**: 배낭에서 밥을 먹는 것은 **인벤토리에서 하는 조작**이라,
    // 답도 그 자리에서 와야 "내가 이걸 눌렀다"가 된다. 폰 머리 위에 띄워 봤더니
    // (`GradeUpNotificationUI`) 손은 창을 보고 있는데 답은 창 밖에서 나서, 조작이 아니라
    // **어딘가에서 일어난 일**처럼 보였다.
    float _gridShakeUntil;
    Vector2 _gridShakeHome;

    // **못 썼다** — 그 자리에서 흔든다. 아무것도 안 바뀌었으므로 이 아이콘은 살아 있다.
    public void FlashRefused()
    {
        if (_rt == null) _rt = (RectTransform)transform;
        if (_gridShakeUntil <= Time.unscaledTime) _gridShakeHome = _rt.anchoredPosition;
        _gridShakeUntil = Time.unscaledTime + ShakeSeconds;
    }

    void TickGridShake()
    {
        if (_gridShakeUntil <= 0f || _carrying == this) return;
        if (_rt == null) return;

        float left = _gridShakeUntil - Time.unscaledTime;
        if (left <= 0f)
        {
            _rt.anchoredPosition = _gridShakeHome;
            _gridShakeUntil = 0f;
            return;
        }

        _rt.anchoredPosition = _gridShakeHome +
            new Vector2(Mathf.Sin(left * ShakeFreq) * ShakeAmp * (left / ShakeSeconds), 0f);
    }

    // **썼다** — 줄어들며 사라지는 잔상을 남긴다.
    //
    // **이 아이콘을 직접 애니메이션할 수가 없다**: 개수가 줄면 `Inventory.OnChanged`가 돌고
    // `InventoryGridUI.Refresh`가 **아이콘을 전부 파괴하고 다시 만든다**(그래서 코루틴도 같이
    // 죽는다). 그러니 격자 바깥에 잔상을 하나 띄우고 그것이 스스로 사라지게 둔다 — 격자가
    // 몇 번을 다시 그려도 상관이 없다.
    public void FlashUsed() => ItemUsePulse.Spawn(_rt != null ? _rt : (RectTransform)transform);

    // ── 이어서 들기 ───────────────────────────────────────────────────────────
    //
    // 놓은 자리에 있던 것을 **밀어내고 그것을 손에 든다.** `TryPlace`가 여기 적어 두면
    // `EndCarry`가 아이콘을 버리지 않고 갈아 끼운다.
    ItemInstance    _pendingPickup;
    InventoryGridUI _pendingPickupGrid;

    // 밀려난 것은 **통에서 이미 빠졌다**(`Inventory.TryPlaceOverAt`) — 그래서 `_detached`다.
    // 다음에 놓기가 실패하거나 되돌리면 그 갈래가 출처로 돌려놓는다.
    void CarryInstead(ItemInstance inst, InventoryGridUI grid)
    {
        _instance   = inst;
        _sourceGrid = grid != null ? grid : _sourceGrid;
        _detached   = true;
        _splitPending = false;
        _dragRotated  = inst.Rotated;

        CarriedInstance   = _instance;
        CarriedSourceGrid = _sourceGrid;

        // **손에 든 아이콘은 이것이다.** 밀어내기 경로에서는 이미 자기 자신이라 없어도
        // 돌았지만, **장비칸에서 새로 만든 아이콘**(`CarryDetached`)은 여기서 세워 주지
        // 않으면 `Update`가 첫 줄에서 물러난다 — 커서를 안 따라오고 클릭도 안 먹는데
        // `CarriedInstance`는 세워져 있어서, **장비는 벗겨졌는데 손에 든 것은 죽어 있는**
        // 상태가 된다(장비를 뺄 때만 그랬던 이유가 이것이다).
        _carrying = this;

        // 잡은 지점은 **한가운데**다 — 집을 때와 같은 식이라 갈릴 데가 없다.
        CenterAnchor();
        GrabAtCenter();

        if (_img != null)
        {
            _img.sprite        = _instance.Data.Icon;
            _img.preserveAspect = _instance.Data.Icon != null;
        }

        // **글자는 격자가 붙인다**(`Decorate`) — 이름·개수·잠금이 한 곳에서 나온다.
        // 여기서 손으로 만지다가 **그림 없는 물건의 이름을 개수로 덮어썼다**(개수가 1이면
        // 빈 글자라 이름이 통째로 사라졌다 — 무엇을 들고 있는지 알 수가 없었다).
        _sourceGrid?.Decorate(transform, _instance);
        UpdateDragVisual();

        if (Mouse.current != null) FollowCursor(Mouse.current.position.ReadValue());
    }

    // ── 그리드 배치 ───────────────────────────────────────────────────────────

    // 마지막으로 놓은 격자 — `EndCarry`가 **놓은 뒤에** 그것도 다시 그린다(위 주석).
    InventoryGridUI _lastTarget;

    bool TryPlace(Vector2 screenPos)
    {
        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        var targetGrid = InventoryGridUI.FindUnderCursor(screenPos, uiCam);
        if (targetGrid == null) return false;

        _lastTarget = targetGrid;

        var cursorCell = targetGrid.ScreenToGrid(screenPos);
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
            return MerchantUI.HandleCarryTrade(_instance, _sourceGrid, targetGrid, _detached,
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

            // **놓기와 밀어내기는 한 문이다**(`TryPlaceOverAt`) — 빈자리면 놓고, 발자국에
            // 걸리는 것이 **딱 하나면** 그것을 통에서 빼서 **손에 이어서 들려 준다**.
            // **둘 이상이면 아무것도 안 한다**(어느 쪽을 밀어낼지 말할 수 없다).
            //
            // 전엔 `MoveItem` 실패 뒤에 **커서 칸의 물건**과 맞바꿨는데, 3×1로 돌린 검처럼
            // 발자국이 커서에서 비켜난 것은 **커서 칸이 비어 있어도 옆에서 남의 물건에 걸쳤다** —
            // 그 셋째 물건을 아무도 안 봤다.
            // **갈라 든 것도 같은 문으로 들어간다**(`_detached` — 꽉 찬 통에서 쪼갠 경우, 또는
            // 방금 밀어내 손에 든 것). 그건 격자에 없으므로 옮기는 것이 아니라 **꽂는 것**인데,
            // `TryPlaceOverAt`이 "이 통에 있는가"를 스스로 보고 갈라 준다.
            if (!inv.TryPlaceOverAt(_instance, newX, newY, _dragRotated, out var pushed))
                return false;

            _detached = false;            // 이제 격자에 있다
            if (pushed != null) { _pendingPickup = pushed; _pendingPickupGrid = _sourceGrid; }
            return true;
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

        // **건너가는 것도 밀어낼 수 있다** — 규칙이 통마다 다르면 "여기서는 되고 저기서는
        // 안 되는" 것이 된다. 옮길 뭉치를 새로 지어 꽂고, 성공했을 때만 출처에서 뺀다.
        var moving = new ItemInstance(_instance.Data, _instance.Quantity, newX, newY,
                                      _dragRotated, _instance.Quality) { Locked = keepLock, Left = _instance.Left };

        if (!dstInv.TryPlaceOverAt(moving, newX, newY, _dragRotated, out var pushedOut)) return false;

        srcInv.RemoveInstance(_instance);
        _detached = false;
        if (pushedOut != null) { _pendingPickup = pushedOut; _pendingPickupGrid = targetGrid; }
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

        // **열린 것은 끌어 합치지 않는다**(2026-10-16) — 개수만 더하면 남은 양이 사라진다(반쯤 탄 횃불이 새것이 된다).
        // 그냥 옮겨 놓이고, 같은 물건끼리 합치는 것은 넣는 문(`Inventory.TryAddOpened`)이 양을 보존하며 한다.
        if (_instance.IsPartial) return false;

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

    bool TrySellDrop(Vector2 screenPos)
    {
        if (_instance != null && _instance.Locked) return false;   // 잠근 것은 못 판다

        // **좌판에서 끌어온 것은 파는 것이 아니다** — 안 막으면 상인 창 위에 떨구는 것만으로
        // "판매 실패"가 뜬다(내 배낭에 없는 것을 팔려 드는 셈이다). 되돌리기는 `EndCarry`가 한다.
        if (_sourceGrid != null && _sourceGrid.IsShop) return false;

        if (MerchantUI.ActiveShop == null || MerchantUI.SellZoneRect == null) return false;
        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(MerchantUI.SellZoneRect, screenPos, uiCam))
            return false;
        // **끌어온 만큼 판다** — 드래그가 이미 개수를 정했다(쪼개 왔으면 그만큼).
        return MerchantUI.TrySellItem(_instance, _instance.Quantity);
    }

    // ── 월드 드롭 ─────────────────────────────────────────────────────────────

    bool TryWorldDrop(Vector2 screenPos)
    {
        // **잠근 것은 못 버린다.** 창 밖으로 끌면 그대로 땅에 떨어지는데, 그게 이 조작에서
        // 유일하게 되돌릴 수 없는 것이다 — 잠금이 막아야 할 게 있다면 바로 이것이다.
        // false를 돌려주면 `EndCarry`가 미배치로 보고 원래 자리로 되돌린다.
        if (_instance != null && _instance.Locked) return false;

        // **남의 진열은 창 밖으로 못 꺼낸다** — 값도 안 치르고 땅에 떨구는 길이 된다.
        if (_sourceGrid != null && _sourceGrid.IsShop) return false;

        // **UI 위에서는 절대 안 버린다 — 창 밖으로 나가야 떨어진다.**
        //
        // 전에는 **격자만** 봤다. 그래서 창 안이어도 격자가 아닌 자리(장비 열, 액션 바,
        // 패널 여백)에 놓으면 물건이 바닥에 떨어졌다 — 아직 배낭 안에서 자리를 옮기던
        // 중인데 밖에 버린 것이 됐다.
        //
        // 손에 든 아이콘은 `BeginCarry`에서 `raycastTarget`을 껐으므로 자기 자신을
        // 짚지 않는다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return false;

        // **격자 위에서는 무슨 일이 있어도 안 떨군다.**
        //
        // 위 검사는 **레이캐스트에 걸리는 그림이 있어야** 참이다 — 격자의 빈 칸에 배경
        // 그래픽이 없으면 창 한복판인데도 거짓이 나온다. 그러면 *"둘에 걸쳐 못 놓았다"*가
        // 곧바로 **"바닥에 버렸다"**가 되어, 조금 옮겨 다시 누르려던 물건이 땅에 떨어진다.
        // 자리는 사각형으로 다시 물어 확인 사살한다(`FindUnderCursor`는 그림을 안 본다).
        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera : null;
        if (InventoryGridUI.FindUnderCursor(screenPos, uiCam) != null) return false;

        var inv = _sourceGrid?.Inventory;
        if (inv == null) return false;

        var dropPos = inv.transform.position + Vector3.up * 0.15f;
        inv.RemoveInstance(_instance);

        // **자동 운반은 안 켜진 채로 떨어진다** — 이제 그게 모든 드롭의 기본이라 여기서
        // 따로 끌 것이 없다(예전엔 손 드롭만 예외로 껐다). 나르게 하려면 그 물건을
        // 우클릭해 [창고로 옮긴다].
        DroppedItem.Spawn(_instance.Data, _instance.Quantity, dropPos, _instance.Quality, _instance.Left);
        return true;
    }
}

// **"썼다"는 잔상.** 아이콘 그림을 그 자리에 한 장 베껴 두고 줄어들며 사라지게 한다.
//
// 격자 밖(캔버스 바로 아래)에 서므로 `InventoryGridUI.Refresh`가 아이콘을 전부 다시 만들어도
// 죽지 않는다 — 그게 이걸 따로 두는 유일한 이유다.
//
// **글자는 안 베낀다**(개수·잠금) — 사라지는 잔상에 개수를 적으면 *"이만큼이 사라졌다"*로
// 읽힌다. 남는 것이 아니라 **한 개를 썼다**는 뜻이다.
public class ItemUsePulse : MonoBehaviour
{
    // **살짝 커졌다가 줄며 사라진다.** 그냥 줄이기만 하면 0.26초 동안 조금 작아지는 것뿐이라
    // 눈에 안 들어온다 — 처음에 한 번 부풀어야 *"눌렀고, 그래서 이게 쓰였다"*로 읽힌다.
    const float Seconds   = 0.3f;
    const float PopAt     = 0.25f;   // 이 지점까지 커진다(0~1)
    const float PopScale  = 1.18f;
    const float Shrink    = 0.3f;    // 끝에 이만큼까지 줄어든다

    public static void Spawn(RectTransform icon)
    {
        if (icon == null) return;

        var canvas = icon.GetComponentInParent<Canvas>();
        var src    = icon.GetComponent<Image>();
        if (canvas == null || src == null) return;

        var go = new GameObject("~ItemUsePulse", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, worldPositionStays: false);
        rt.SetAsLastSibling();

        rt.pivot     = icon.pivot;
        rt.sizeDelta = icon.rect.size;
        rt.position  = icon.position;
        rt.rotation  = icon.rotation;

        // **캔버스 바로 아래로 옮기면 중간 부모의 스케일이 사라진다** — 격자가 조금이라도
        // 축소돼 있으면 잔상만 원래 크기로 튀어나온다. 겉보기 크기를 그대로 물려받는다.
        var cs = canvas.transform.lossyScale;
        rt.localScale = new Vector3(
            cs.x != 0f ? icon.lossyScale.x / cs.x : 1f,
            cs.y != 0f ? icon.lossyScale.y / cs.y : 1f, 1f);

        var img = go.GetComponent<Image>();
        img.sprite         = src.sprite;
        img.color          = src.color;
        img.preserveAspect = src.preserveAspect;
        img.raycastTarget  = false;   // 사라지는 그림이 클릭을 먹으면 안 된다

        go.AddComponent<ItemUsePulse>();
    }

    RectTransform _rt;
    Image         _img;
    Color         _color0;
    Vector3       _scale0;
    float         _t;

    void Awake()
    {
        _rt     = (RectTransform)transform;
        _img    = GetComponent<Image>();
        _color0 = _img.color;
        _scale0 = _rt.localScale;
    }

    // **`unscaledDeltaTime`으로 잰다** — 배속·일시정지는 화면 조작과 상관이 없다
    // (`DraggableItem`의 흔들림·`CameraController`와 같은 자리).
    void Update()
    {
        _t += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(_t / Seconds);

        float scale = k < PopAt ? Mathf.Lerp(1f, PopScale, k / PopAt)
                                : Mathf.Lerp(PopScale, Shrink, (k - PopAt) / (1f - PopAt));
        _rt.localScale = _scale0 * scale;

        // **부푸는 동안은 또렷하게 두고 끝에서 빠르게 지운다** — 처음부터 선형으로 흐려지면
        // 제일 잘 보여야 할 순간이 이미 반쯤 투명하다.
        _img.color = new Color(_color0.r, _color0.g, _color0.b, _color0.a * (1f - k * k));

        if (k >= 1f) Destroy(gameObject);
    }
}
