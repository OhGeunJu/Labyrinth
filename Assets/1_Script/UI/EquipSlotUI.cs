using UnityEngine;
using UnityEngine.EventSystems;
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

    public void Refresh()
    {
        ItemData item = GetCurrentItem();
        bool equipped = item != null;

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
    public void OnDrop(PointerEventData e)
    {
        if (_equipment == null) return;

        // 장비칸 → 장비칸. **비었으면 옮기고 차 있으면 맞바꾼다.**
        if (DraggedSlot != null && DraggedSlot != this) { SwapWith(DraggedSlot); return; }

        var inst = DraggableItem.DraggedInstance;
        if (inst == null) return;

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
        DraggedSlot = this;
        if (icon) { var c = icon.color; c.a = 0.35f; icon.color = c; }
    }

    // **격자 위에서는 들어갈 자리를 미리 보여 준다** — 배낭 아이콘을 끌 때와 같게.
    //
    // 장비는 2×4처럼 크고, 창고는 필터가 걸려 있으면 **무기·방어구를 아예 안 받는다**
    // (`Storage.CanAccept`). 그걸 놓아 보고 나서야 알면 "왜 안 옮겨지지"가 된다 —
    // `ShowPreview`가 자리와 거절을 그 자리에서 그려 준다.
    InventoryGridUI _previewGrid;

    public void OnDrag(PointerEventData e)
    {
        var inst = GetCurrentInstance();
        var grid = inst?.Data != null ? InventoryGridUI.FindUnderCursor(e.position, UiCam()) : null;

        if (_previewGrid != null && _previewGrid != grid) { _previewGrid.ClearPreview(); _previewGrid = null; }
        if (grid == null) return;

        var cell = grid.ScreenToGrid(e.position);
        if (cell.x == int.MinValue) return;

        // **커서 칸을 왼쪽 위로 잡는다.** 격자 아이콘과 달리 어디를 집었는지가 없다 —
        // 칸 하나에서 끌어온 것이라 잡은 지점이라는 개념이 없다.
        grid.ShowPreview(inst, cell.x, cell.y, false, isExternal: true);
        _previewGrid = grid;
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

        if (DraggedSlot == this) TryDropToGrid(e);

        DraggedSlot = null;
        if (icon) { var c = icon.color; c.a = 1f; icon.color = c; }
        Refresh();
    }

    void TryDropToGrid(PointerEventData e)
    {
        if (_equipment == null) return;

        var grid = InventoryGridUI.FindUnderCursor(e.position, UiCam());
        if (grid == null || grid.Inventory == null) return;

        var inst = GetCurrentInstance();
        if (inst?.Data == null) return;
        if (!grid.CanReceiveItem(inst.Data)) return;   // 창고 필터

        // **자리를 확인하고 나서 벗긴다.** 순서가 뒤집히면 격자가 꽉 찼을 때 벗긴 것이
        // `ReturnToInventory`를 타고 배낭으로 가거나 발밑에 떨어진다 — 사용자는 이 격자에
        // 넣으려 한 것이지 아무 데나 두려던 것이 아니다.
        if (grid.Inventory.TryAdd(inst.Data, 1, inst.Quality) <= 0) return;

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
