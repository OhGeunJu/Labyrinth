using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 장착 슬롯 1칸. Init으로 종류 설정, Bind로 Equipment 교체.
public class EquipSlotUI : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IDropHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // **지금 끌고 있는 장비칸.** 격자 아이콘의 `DraggableItem`과 나란한 채널이다 —
    // 장비칸에는 끌 아이콘 오브젝트가 없으므로(칸 자체가 그림이다) 따로 둔다.
    public static EquipSlotUI DraggedSlot { get; private set; }

    public enum SlotKind { MainHand, OffHand, Head, Chest, Hands, Legs }

    [SerializeField] Image           bg;
    [SerializeField] Image           icon;
    [SerializeField] TextMeshProUGUI slotLabel;
    [SerializeField] TextMeshProUGUI itemNameText;

    [SerializeField] Color emptyColor    = new(0.14f, 0.14f, 0.16f, 0.95f);
    [SerializeField] Color equippedColor = new(0.18f, 0.28f, 0.18f, 0.95f);

    static readonly string[] KindLabels = { "주손", "보조", "머리", "몸통", "손", "다리" };

    // **반드시 직렬화한다.** 예전엔 private 필드였고 `Init()`을 부르는 곳도 없어서
    // 6칸 전부 기본값(`MainHand`)으로 남았다 — 그래서 어느 칸을 봐도 주손 아이템이 떴다
    // ("인벤토리 모든 칸에 착용한 것처럼 보인다"의 정체).
    [Tooltip("이 칸이 맡는 부위. 씬에서 칸마다 다르게 지정해야 한다.")]
    [SerializeField] SlotKind _kind;
    Equipment _equipment;

    public void Init(SlotKind kind)
    {
        _kind = kind;
        if (slotLabel) slotLabel.text = KindLabels[(int)kind];
    }

    public void Bind(Equipment equipment)
    {
        if (_equipment != null) Equipment.OnEquipChanged -= OnEquipChanged;
        _equipment = equipment;
        if (_equipment != null) Equipment.OnEquipChanged += OnEquipChanged;
        Refresh();
    }

    void OnDestroy()
    {
        if (_equipment != null) Equipment.OnEquipChanged -= OnEquipChanged;
    }

    void OnEquipChanged(Equipment eq)
    {
        if (eq == _equipment) Refresh();
    }

    // 양손 무기가 이 칸을 잡아먹고 있을 때의 색 — 빈 칸도 찬 칸도 아니라 따로 둔다.
    [SerializeField] Color twoHandedColor = new(0.24f, 0.22f, 0.14f, 0.95f);

    public void Refresh()
    {
        ItemData item = GetCurrentItem();
        bool equipped = item != null;

        // ── 양손 무기가 보조손을 쓰고 있다 ──────────────────────────────────
        //
        // **비어 보이면 안 된다.** 대검을 들면 보조손 칸이 그냥 빈 칸으로 보여서, 방패를
        // 끼우려다 "왜 안 껴지지"가 됐다(이제는 대검이 벗겨지므로 더 나쁘다 — 왜 벗겨졌는지
        // 모른 채 무기가 배낭으로 간다). **무엇이 그 자리를 쓰고 있는지**를 적는다.
        //
        // 아이콘은 주손 무기 것을 흐리게 빌려 쓴다 — 그림이 곧 "저것이 두 손을 쓴다"는 말이다.
        bool twoHanded = _kind == SlotKind.OffHand && _equipment != null && _equipment.OffHandLocked;

        if (twoHanded)
        {
            var main = _equipment.MainHand;
            if (bg) bg.color = twoHandedColor;
            if (icon)
            {
                icon.sprite = main?.Icon;
                icon.color  = main?.Icon != null ? new Color(1f, 1f, 1f, 0.35f) : new Color(1, 1, 1, 0);
            }
            if (itemNameText) itemNameText.text = "양손 사용 중";
            return;
        }

        if (bg)   bg.color = equipped ? equippedColor : emptyColor;
        if (icon)
        {
            icon.sprite = item?.Icon;
            icon.color  = item?.Icon != null ? Color.white : new Color(1, 1, 1, 0);
        }
        if (itemNameText) itemNameText.text = equipped ? item.ItemName : "";
    }

    // ── 클릭 → 해제 ──────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData e)
    {
        if (_equipment == null) return;

        // **끌었다 제자리에 놓은 것은 클릭이 아니다.**
        //
        // uGUI는 누른 곳과 놓은 곳이 같으면 **드래그가 끝난 뒤에도** 클릭을 한 번 더 보낸다.
        // 그래서 장비를 집어 끌었다가 도로 그 칸에 놓으면 **여기가 벗겨 버렸다** — 다른 칸에
        // 놓을 때만 멀쩡했던 이유가 이것이다(놓은 곳이 달라 클릭이 안 온다).
        //
        // 순서는 로그로 확인했다: `OnBeginDrag` → **`OnPointerClick`** → `OnEndDrag`.
        // 그래서 이 시점엔 `DraggedSlot`이 아직 나를 가리키고 있어 그것만 보면 된다.
        if (DraggedSlot != null) return;

        switch (_kind)
        {
            case SlotKind.MainHand: _equipment.Unequip();                     break;
            case SlotKind.OffHand:  _equipment.UnequipOffHand();              break;
            case SlotKind.Head:     _equipment.UnequipArmor(ArmorSlot.Head);  break;
            case SlotKind.Chest:    _equipment.UnequipArmor(ArmorSlot.Chest); break;
            case SlotKind.Hands:    _equipment.UnequipArmor(ArmorSlot.Hands); break;
            case SlotKind.Legs:     _equipment.UnequipArmor(ArmorSlot.Legs);  break;
        }
    }

    // ── 드롭 → 장착 ──────────────────────────────────────────────────────────

    // ── 드롭 → 장착 ─────────────────────────────────────────────────────────
    //
    // **장비칸을 컨테이너처럼 다룬다.** 받는 곳이 셋, 오는 곳도 셋이다:
    // 격자(배낭·상자·시체·작업대)에서 오거나, 다른 장비칸에서 온다.
    //
    // **배낭을 안 거친다**(`Equipment.Wear*`). 전엔 `Equip*`이 배낭에서 꺼내 차는 설계라
    // 밖에서 끌어온 것을 못 찾고 조용히 실패했고, 배낭을 경유하게 고쳤더니 이번엔
    // **배낭이 꽉 차면 빈 칸에도 못 넣었다** — "칸은 비었는데 왜 안 들어가지"가 된다.
    // 벗겨지는 쪽만 배낭으로 가고(자리 없으면 발밑), 들어오는 쪽은 자리를 안 본다.
    // **장비칸 위에 놓았다** — 그러면 격자로 벗기는 길(`TryDropToGrid`)이 또 돌면 안 된다.
    //
    // 배낭 격자의 `RectTransform`이 장비 열까지 덮고 있어서, **제자리에 도로 놓기만 해도**
    // `FindUnderCursor`가 "격자 위"라고 답해 **장비가 벗겨졌다.** 다른 칸으로 맞바꾼 뒤에도
    // 같은 길이 한 번 더 돌 뻔했다. `DraggableItem.DropHandled`와 같은 자리의 깃발이다.
    static bool _slotDropHandled;

    public void OnDrop(PointerEventData e)
    {
        if (_equipment == null) return;

        if (DraggedSlot != null) _slotDropHandled = true;

        // 장비칸 → 장비칸. **비었으면 옮기고 차 있으면 맞바꾼다.**
        if (DraggedSlot != null && DraggedSlot != this) { SwapWith(DraggedSlot); return; }

        var inst = DraggableItem.DraggedInstance;
        if (inst == null) return;

        // **남의 진열은 여기로 못 들어온다** — 값도 안 치르고 입는 길이 된다.
        // 사는 것은 배낭으로 끌어다 놓고 나서 할 일이다(`MerchantUI.HandleDragTrade`).
        var src = DraggableItem.DraggedSourceGrid;
        if (src != null && src.IsShop) return;

        // **맞는 칸인지 먼저 본다.** 옮기고 나서 못 차면 물건만 남의 상자에서 건너온 채가
        // 되는데, 그건 시킨 적 없는 일이다.
        if (!Accepts(inst.Data)) return;

        var sourceInv = DraggableItem.DraggedSourceGrid != null
            ? DraggableItem.DraggedSourceGrid.Inventory : null;
        if (sourceInv == null) return;

        // **차고 있던 것은 온 곳으로 돌아간다 — 맞바꾸기다.**
        //
        // 전에는 `Equipment.ReturnToInventory`에 맡겨 **폰의 배낭으로** 갔다. 그러면 상자에서
        // 갑옷을 끌어다 입을 때마다 벗은 갑옷이 배낭에 쌓이고, 사용자는 그걸 다시 상자로
        // 옮겨야 한다 — **한 동작으로 보이는 일이 두 동작**이 된다. 장비칸끼리 맞바꾸는 것과
        // 같은 규칙으로 맞춘다.
        var prev = GetCurrentInstance();

        // **한 개만 빼낸다.** 상자에 철검이 셋 있어도 차는 것은 하나다.
        if (!sourceInv.RemoveOne(inst)) return;
        var single = new ItemInstance(inst.Data, 1, 0, 0, false, inst.Quality);

        // 벗기는 것이 먼저 — `Wear`에 맡기면 그 안에서 배낭으로 보내 버린다.
        if (prev != null) TakeOff();

        if (!Wear(single))
        {
            // 못 찼다 — 둘 다 원래대로 되돌린다(빼놓고 실패하면 어디에도 없다).
            if (prev != null) Wear(prev);
            sourceInv.TryAdd(single.Data, 1, single.Quality);
            return;
        }

        // 벗은 것을 **온 곳으로**. 못 받으면(꽉 찼거나 필터에 걸리면) 배낭, 그것도 안 되면 발밑 —
        // 이 프로젝트의 증발 방지 순서 그대로다.
        if (prev != null) SendBack(prev, sourceInv);

        DraggableItem.DropHandled = true;
    }

    void SendBack(ItemInstance inst, Inventory source)
    {
        if (source != null && source.TryAdd(inst.Data, 1, inst.Quality) > 0) return;

        var backpack = _equipment.GetComponent<Inventory>();
        if (backpack != null && backpack.TryAdd(inst.Data, 1, inst.Quality) > 0) return;

        DroppedItem.Spawn(inst.Data, 1, _equipment.transform.position + Vector3.up * 0.15f,
                          inst.Quality);
    }

    // 이 칸이 받는 물건인가. **판정을 한 곳에 모은다** — 전엔 `switch` 안에 섞여 있어서
    // 옮기기 전에 물어볼 방법이 없었다.
    bool Accepts(ItemData data) => _kind switch
    {
        SlotKind.MainHand => data is WeaponData,

        // **보조손은 양손 무기를 아예 안 받는다.** `Equipment.OffHandLocked`는 "지금 주손에
        // 양손 무기를 들었나"라 **주손이 비면 풀린다** — 주손 칸을 보조손 칸으로 끌면
        // `TakeOff`가 주손을 비운 직후라 잠금이 사라져 **대검이 보조손에 들어갔다**
        // (그리고 주손에 다른 무기를 또 차면 양손 무기 + 무기가 되고, 방패처럼
        // `OffHand.BlockBonus`까지 읽힌다). 상태를 안 보는 규칙으로 못 박는다.
        SlotKind.OffHand  => data is WeaponData w && !w.IsLargeWeapon,
        SlotKind.Head     => data is ArmorData a && a.Slot == ArmorSlot.Head,
        SlotKind.Chest    => data is ArmorData a && a.Slot == ArmorSlot.Chest,
        SlotKind.Hands    => data is ArmorData a && a.Slot == ArmorSlot.Hands,
        SlotKind.Legs     => data is ArmorData a && a.Slot == ArmorSlot.Legs,
        _                 => false,
    };

    bool Wear(ItemInstance inst) => _kind switch
    {
        SlotKind.MainHand => _equipment.WearMainHand(inst),
        SlotKind.OffHand  => _equipment.WearOffHand(inst),
        _                 => _equipment.WearArmor(inst),
    };

    // 이 칸의 것을 **배낭에 안 돌려주고** 빼낸다(교환용).
    ItemInstance TakeOff() => _kind switch
    {
        SlotKind.MainHand => _equipment.TakeOffMainHand(),
        SlotKind.OffHand  => _equipment.TakeOffOffHand(),
        SlotKind.Head     => _equipment.TakeOff(ArmorSlot.Head),
        SlotKind.Chest    => _equipment.TakeOff(ArmorSlot.Chest),
        SlotKind.Hands    => _equipment.TakeOff(ArmorSlot.Hands),
        SlotKind.Legs     => _equipment.TakeOff(ArmorSlot.Legs),
        _                 => null,
    };

    // 장비칸끼리 맞바꾼다. **양쪽 다 빼놓고 나서 채운다** — 한쪽씩 처리하면 중간 상태에서
    // `ReturnToInventory`가 끼어들어 물건이 배낭이나 바닥으로 새어 나간다.
    void SwapWith(EquipSlotUI from)
    {
        var mine  = GetCurrentInstance();
        var yours = from.GetCurrentInstance();
        if (yours == null) return;

        // **서로 받을 수 있는지 먼저 본다.** 한쪽만 되면 교환이 아니라 강탈이 된다.
        if (!Accepts(yours.Data)) return;
        if (mine != null && !from.Accepts(mine.Data)) return;

        from.TakeOff();
        TakeOff();

        // **못 차면 원래 자리로 되돌린다.** `Accepts`가 거를 것은 이미 걸렀지만, 빼놓고 나서
        // 실패하면 그 물건은 **어디에도 없다**(칸에도 배낭에도) — 안전망 없이 "실패할 수 없다"에
        // 기대면 나중에 조건이 하나 늘 때 조용히 증발한다.
        if (!Wear(yours))
        {
            from.Wear(yours);
            if (mine != null) Wear(mine);
            return;
        }
        if (mine != null && !from.Wear(mine)) Wear(mine);
    }

    // ── 드래그 출발 ──────────────────────────────────────────────────────────
    //
    // 칸 자체가 그림이라 **끌 아이콘을 따로 만들지 않는다.** 아이콘을 복제해 커서에 붙이면
    // 격자 쪽 `DraggableItem`과 두 벌이 되고, 회전·프리뷰 같은 것을 여기서 또 구현하게 된다.
    // 대신 **끌리는 동안 칸을 흐리게** 해서 무엇을 들고 있는지 보인다.
    public void OnBeginDrag(PointerEventData e)
    {
        if (_equipment == null || GetCurrentItem() == null) return;

        ItemInfoUI.Hide();
        DraggedSlot  = this;
        _dragRotated = false;
        if (icon) { var c = icon.color; c.a = 0.35f; icon.color = c; }

        MakeGhost();
    }

    // **손에 든 것을 보여 준다.**
    //
    // 한때 "칸 자체가 그림이라 끌 아이콘을 안 만든다"로 두고 칸만 흐리게 했다. 그런데 격자
    // 드래그에는 손에 든 그림이 있어서, **같은 조작이 어디서 시작했느냐로 다르게 보였다** —
    // 무엇보다 2×4 갑옷을 끌 때 *얼마나 큰 것을 들고 있는지*가 안 보인다.
    //
    // **그림만 만든다**(놓는 규칙은 그대로 `TryDropToGrid`가 든다) — 그때 걱정한 "두 벌"은
    // 드롭 처리를 말한 것이고, 커서를 따라다니는 사각형 하나는 그 축이 아니다.
    RectTransform _ghost;
    bool          _dragRotated;

    void MakeGhost()
    {
        var canvas = GetComponentInParent<Canvas>();
        var inst   = GetCurrentInstance();
        if (canvas == null || inst?.Data == null) return;

        var go = new GameObject("_EquipDragGhost", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color         = new Color(1f, 1f, 1f, 0.7f);

        if (inst.Data.Icon != null) { img.sprite = inst.Data.Icon; img.preserveAspect = true; }
        else
        {
            // **그림이 없으면 이름을 적는다** — 격자 아이콘과 같은 규칙(`InventoryGridUI`).
            // 안 적으면 손에 초록 사각형만 들려 무엇을 끌고 있는지 알 수가 없다.
            img.color = new Color(0.25f, 0.35f, 0.25f, 0.7f);

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);

            var txt = label.AddComponent<TextMeshProUGUI>();
            txt.text          = inst.Data.ItemName;
            txt.fontSize      = 12;
            txt.alignment     = TextAlignmentOptions.Center;
            txt.color         = Color.white;
            txt.raycastTarget = false;

            var lrt = txt.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(2, 2);
            lrt.offsetMax = new Vector2(-2, -2);
        }

        _ghost = (RectTransform)go.transform;
        _ghost.pivot = new Vector2(0f, 1f);   // 격자 아이콘과 같은 기준(좌상단 = 잡은 지점)

        SizeGhost();
        if (Mouse.current != null) MoveGhost(Mouse.current.position.ReadValue());

    }

    // 칸 크기는 **지금 얹혀 있는 격자에서 베낀다** — 손에 든 것이 놓일 자리와 같은 크기여야
    // 뜻이 있다. 격자 밖이면 배낭 것을 쓴다(거기로 갈 일이 가장 잦다).
    void SizeGhost(float cellSize = 0f)
    {
        if (_ghost == null) return;

        var inst = GetCurrentInstance();
        if (inst?.Data == null) return;

        float cs = cellSize > 0f ? cellSize
                 : InventoryUI.Instance != null && InventoryUI.Instance.Grid != null
                 ? InventoryUI.Instance.Grid.CellSize : 64f;

        int w = _dragRotated ? inst.Data.Height : inst.Data.Width;
        int h = _dragRotated ? inst.Data.Width  : inst.Data.Height;

        _ghost.sizeDelta = new Vector2(w * cs - 4, h * cs - 4);
    }

    // **격자 위에서는 들어갈 자리를 미리 보여 준다** — 배낭 아이콘을 끌 때와 같게.
    //
    // 장비는 2×4처럼 크고, 창고는 필터가 걸려 있으면 **무기·방어구를 아예 안 받는다**
    // (`Storage.CanAccept`). 그걸 놓아 보고 나서야 알면 "왜 안 옮겨지지"가 된다 —
    // `ShowPreview`가 자리와 거절을 그 자리에서 그려 준다.
    InventoryGridUI _previewGrid;

    public void OnDrag(PointerEventData e) => RefreshDrag(e.position);

    // **회전과 따라다니기는 `Update`가 받는다.**
    //
    // `OnDrag`는 **포인터가 움직인 프레임에만** 불린다 — 거기서 Q/E를 읽으면 마우스를 가만히
    // 둔 채 누른 키가 그대로 사라진다(격자 드래그가 겪은 그것).
    void Update()
    {
        if (DraggedSlot != this) return;

        var kb = Keyboard.current;
        if (kb != null && (kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
        {
            _dragRotated = !_dragRotated;
            SizeGhost();
        }

        if (Mouse.current != null) RefreshDrag(Mouse.current.position.ReadValue());
    }

    void RefreshDrag(Vector2 screenPos)
    {
        MoveGhost(screenPos);

        var inst = GetCurrentInstance();
        var grid = inst?.Data != null ? InventoryGridUI.FindUnderCursor(screenPos, UiCam()) : null;

        if (_previewGrid != null && _previewGrid != grid) { _previewGrid.ClearPreview(); _previewGrid = null; }
        if (grid == null) { SizeGhost(); return; }

        SizeGhost(grid.CellSize);   // 얹힌 격자의 눈금으로

        var cursor = grid.ScreenToGrid(screenPos);
        if (cursor.x == int.MinValue) return;

        // **커서 칸을 왼쪽 위로 잡는다.** 격자 아이콘과 달리 어디를 집었는지가 없다 —
        // 칸 하나에서 끌어온 것이라 잡은 지점이라는 개념이 없다. 격자 밖으로 나가지 않게
        // 당기는 것은 `PlacementCell`이 한다(격자 드래그와 같은 답).
        var cell = grid.PlacementCell(cursor, inst.Data, _dragRotated, Vector2Int.zero);

        grid.ShowPreview(inst, cell.x, cell.y, _dragRotated, isExternal: true);
        _previewGrid = grid;
    }

    // **앵커에 기대지 않고 월드 좌표로 놓는다.**
    //
    // `anchoredPosition`은 앵커점 기준이라 앵커를 잘못 잡으면 화면 절반만큼 밀린다 — 실제로
    // 그림이 화면 구석으로 날아갔다. `ScreenPointToWorldPointInRectangle`은 **캔버스 모드와
    // 앵커를 안 가리고** 그 화면 지점의 월드 좌표를 그대로 주므로 어긋날 자리가 없다.
    void MoveGhost(Vector2 screenPos)
    {
        if (_ghost == null) return;

        var canvas = _ghost.parent as RectTransform;
        if (canvas == null) return;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvas, screenPos, UiCam(), out var world))
            _ghost.position = world;
    }

    Camera UiCam()
    {
        var canvas = GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
             ? canvas.worldCamera : null;
    }

    // 놓는 자리가 **장비칸이면 그쪽 `OnDrop`이 이미 처리했고**(교환), 격자면 여기서 벗긴다.
    //
    // 격자 쪽은 `IDropHandler`가 없어(아이콘 단위로 `DraggableItem`이 받는다) 받아 줄 사람이
    // 없다 — 그래서 장비칸을 끌어 배낭에 놓아도 **아무 일도 안 일어났다.** 벗기는 길이
    // 클릭뿐이라 "드래그로 빼던 것이 안 된다"가 됐다.
    public void OnEndDrag(PointerEventData e)
    {
        _previewGrid?.ClearPreview();
        _previewGrid = null;

        if (_ghost != null) { Destroy(_ghost.gameObject); _ghost = null; }

        // 장비칸 위에 놓았으면 그쪽이 이미 처리했다(제자리 놓기 포함 — 위 `_slotDropHandled`).
        if (DraggedSlot == this && !_slotDropHandled) TryDropToGrid(e);
        _slotDropHandled = false;

        DraggedSlot = null;
        if (icon) { var c = icon.color; c.a = 1f; icon.color = c; }
        Refresh();
    }

    void TryDropToGrid(PointerEventData e)
    {
        if (_equipment == null) return;

        // **장비칸 위에 놓았으면 벗기지 않는다 — 칸 사각형을 직접 잰다.**
        //
        // 배낭 격자의 `RectTransform`이 장비 열까지 덮고 있어서 `FindUnderCursor`는 "격자 위"라고
        // 답한다. 그래서 **제자리에 도로 놓기만 해도 장비가 벗겨졌다.**
        //
        // 앞서 두 번 빗나갔다: `_slotDropHandled`는 *"`OnDrop`이 `OnEndDrag`보다 먼저 온다"*는
        // 순서에, `pointerCurrentRaycast`는 *"놓는 프레임까지 그 값이 남아 있다"*는 가정에
        // 기댔는데 **제자리 놓기에서는 둘 다 안 맞았다.** 사각형 안에 커서가 있느냐는
        // 그런 가정이 없다 — 물어보는 그 순간의 사실이다.
        if (RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)transform, e.position, UiCam())) return;

        var grid = InventoryGridUI.FindUnderCursor(e.position, UiCam());
        if (grid == null || grid.Inventory == null) return;

        var inst = GetCurrentInstance();
        if (inst?.Data == null) return;
        if (!grid.CanReceiveItem(inst.Data)) return;   // 창고 필터

        // **미리보기가 가리킨 그 칸에 놓는다.** 전에는 `TryAdd`(빈 자리 자동 탐색)라 밝은 칸을
        // 보고 놓아도 **첫 빈 칸으로 튀었다** — 미리보기가 거짓말이 된다.
        // 그 칸이 안 되면(다른 것이 있으면) 평소대로 빈 자리를 찾는다: 자리 때문에 벗기기
        // 자체를 거절하면 "왜 안 옮겨지지"가 된다.
        //
        // **자리를 확인하고 나서 벗긴다.** 순서가 뒤집히면 격자가 꽉 찼을 때 벗긴 것이
        // `ReturnToInventory`를 타고 배낭으로 가거나 발밑에 떨어진다 — 사용자는 이 격자에
        // 넣으려 한 것이지 아무 데나 두려던 것이 아니다.
        var cursor = grid.ScreenToGrid(e.position);
        bool placed = false;

        if (cursor.x != int.MinValue)
        {
            var cell = grid.PlacementCell(cursor, inst.Data, _dragRotated, Vector2Int.zero);
            placed = grid.Inventory.TryAddAt(inst.Data, 1, cell.x, cell.y, _dragRotated, inst.Quality);
        }

        if (!placed && grid.Inventory.TryAdd(inst.Data, 1, inst.Quality) <= 0) return;

        TakeOff();          // 배낭에 안 돌려준다 — 이미 위에서 넣었다
        grid.RefreshItems();
    }

    // 지금 이 칸에 든 인스턴스(등급을 들고 있다). 교환은 **`ItemData`가 아니라 이걸** 옮겨야
    // 한다 — 데이터만 넘기면 희귀 철검이 맞바꾸는 것만으로 일반이 된다.
    ItemInstance GetCurrentInstance() => _kind switch
    {
        SlotKind.MainHand => _equipment?.MainHandInstance,
        SlotKind.OffHand  => _equipment?.OffHandInstance,
        SlotKind.Head     => _equipment?.ArmorInstance(ArmorSlot.Head),
        SlotKind.Chest    => _equipment?.ArmorInstance(ArmorSlot.Chest),
        SlotKind.Hands    => _equipment?.ArmorInstance(ArmorSlot.Hands),
        SlotKind.Legs     => _equipment?.ArmorInstance(ArmorSlot.Legs),
        _                 => null,
    };

    // ── 툴팁 ─────────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _)
    {
        // `owner: this` — 장비칸은 사라지지 않지만, 창이 닫히면(꺼지면) 툴팁도 같이 닫힌다.
        //
        // **등급은 인스턴스에서 읽는다.** 전엔 `ItemQuality.Common`을 박아 넘겨서, 희귀 철검을
        // 차고 있어도 툴팁이 `일반 · 한손 검`이라고 했다 — 격자에서 보던 등급과 어긋난다.
        var inst = GetCurrentInstance();
        if (inst?.Data != null)
            ItemInfoUI.Show(inst.Data, inst.Quality, -1, null, false, this);
    }

    public void OnPointerExit(PointerEventData _) => ItemInfoUI.Hide();

    ItemData GetCurrentItem() => _kind switch
    {
        SlotKind.MainHand => _equipment?.MainHand,
        SlotKind.OffHand  => _equipment?.OffHand,
        SlotKind.Head     => _equipment?.GetArmor(ArmorSlot.Head),
        SlotKind.Chest    => _equipment?.GetArmor(ArmorSlot.Chest),
        SlotKind.Hands    => _equipment?.GetArmor(ArmorSlot.Hands),
        SlotKind.Legs     => _equipment?.GetArmor(ArmorSlot.Legs),
        _                 => null,
    };
}
