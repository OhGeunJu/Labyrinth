using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 장비칸 하나. **격자와 같은 조작을 쓴다** — 좌클릭으로 집고, 손에 든 것을 좌클릭으로 찬다.
//
// 예전엔 이 파일이 **제 드래그를 따로 굴렸다**(고스트 만들기·회전·미리보기·격자로 떨구기,
// 200줄 남짓). 손에 드는 상태가 생기면서 그게 전부 `DraggableItem` 하나로 모였다 —
// 여기 남은 것은 **이 칸이 무엇을 받고 무엇을 내놓는가**뿐이다.
//
// | | 손이 비었을 때 | 손에 들었을 때 |
// |---|---|---|
// | **좌클릭** | 차고 있던 것을 **손에 든다** | **찬다** — 원래 차던 것이 손에 들린다 |
// | **우클릭** | 벗어서 배낭으로 | (손에 든 쪽이 되돌린다) |
public class EquipSlotUI : MonoBehaviour,
    IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    // 커서 아래 칸을 찾으려면 목록이 있어야 한다 — 손에 든 아이콘이 `RectTransform`으로 짚는다
    // (`IDropHandler`가 없어졌으므로 유니티가 알려 주지 않는다).
    static readonly List<EquipSlotUI> All = new();

    public enum SlotKind { MainHand, OffHand, Head, Chest, Hands, Legs, Belt, Feet }   // 허리(10-15)·신발(10-18)은 꼬리에

    [SerializeField] Image           bg;
    [SerializeField] Image           icon;
    [SerializeField] TextMeshProUGUI slotLabel;
    [SerializeField] TextMeshProUGUI itemNameText;

    [SerializeField] Color emptyColor    = new(0.14f, 0.14f, 0.16f, 0.95f);
    [SerializeField] Color equippedColor = new(0.18f, 0.28f, 0.18f, 0.95f);

    static readonly string[] KindLabels = { "주손 무기", "보조손", "머리", "몸통", "손", "다리", "허리", "신발" };

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

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

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
        // **이름만 적는다 — 등급은 안 적는다.** 같은 물건을 맞바꿀 때 티가 나게 하려고
        // `[희귀] 철검`처럼 등급을 붙여 봤는데, 좁은 칸에서 이름이 밀리는 값이 그 이득보다
        // 컸다. *바뀌었다*는 것은 **반짝임**(`Flash`)이 말하고, *무엇으로 바뀌었나*는 툴팁이
        // 답한다 — 칸은 무엇을 차고 있는지만 말하면 된다.
        if (itemNameText) itemNameText.text = equipped ? item.ItemName : "";
    }

    // ── 집기 / 차기 ──────────────────────────────────────────────────────────

    // **누르는 순간 집는다** — 격자 아이콘과 같은 시점이라야 `DraggableItem`의 프레임 가드가
    // 둘을 갈라 준다(집기와 놓기가 한 클릭에 겹치지 않게).
    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (_equipment == null) return;

        // 손에 들었으면 여기서 받는 것이 아니다 — 손에 든 아이콘이 `TryEquipCarried`로 온다.
        if (DraggableItem.CarriedInstance != null) return;

        var inst = GetCurrentInstance();
        if (inst == null) return;

        // **되돌릴 곳이 없으면 집지 않는다.** 손에 든 것은 어디에도 등록돼 있지 않아서,
        // 되돌릴 통을 모르면 창을 닫는 순간 갈 곳이 없다 — 벗겨만 놓고 잃는 것이 가장 나쁘다.
        var back = InventoryUI.Instance != null ? InventoryUI.Instance.Grid : null;
        if (back == null || back.Inventory == null) return;

        // **벗기면서 손에 든다.** `TakeOff`는 배낭을 안 거치므로(그게 이 칸의 규약이다)
        // 벗긴 그것이 곧 손에 든 것이 된다 — 벗기와 집기가 한 동작이다.
        // **벗긴 그것을 든다** — `TakeOff`가 돌려주는 것이 정본이다(칸을 다시 물으면 이미 비었다).
        var taken = TakeOff() ?? inst;

        // **못 들면 벗긴 것을 도로 넣는다.** `TakeOff`는 배낭을 안 거치므로(그게 이 칸의
        // 규약이다), `CarryDetached`가 실패하면(캔버스를 못 찾을 때 `null`) 그 장비는
        // **칸에서도 빠지고 배낭에도 없는** 상태가 된다 — 조용히 사라지는 것이 가장 나쁘다.
        //
        // 되돌리는 순서는 **다시 차기 → 배낭 → 발밑**이다(`Storage.Deliver`가 부린 짐을
        // 되돌리는 것과 같은 모양: 뒤로 갈수록 나쁜 쪽).
        if (DraggableItem.CarryDetached(taken, back, e.position) != null) return;

        Debug.LogWarning($"[EquipSlot] {taken.Data.ItemName} — 손에 못 들어 되돌린다(캔버스 없음).");
        if (Wear(taken)) return;
        if (back.Inventory.TryAddLike(taken) >= taken.Quantity) return;

        Debug.LogError($"[EquipSlot] {taken.Data.ItemName}을(를) 되돌릴 곳이 없다 — 잃었다.");
    }

    // **우클릭 = 벗어서 배낭으로.** "우클릭 = 이걸 저기로"라는 이 창의 규칙 그대로다
    // (격자에서 우클릭이 반대편 통으로 보내는 것과 같은 자리).
    public void OnPointerClick(PointerEventData e)
    {
        if (_equipment == null) return;
        if (e.button != PointerEventData.InputButton.Right) return;
        if (DraggableItem.CarriedInstance != null) return;   // 되돌리기는 손에 든 쪽이 한다

        switch (_kind)
        {
            case SlotKind.MainHand: _equipment.Unequip();                     break;
            case SlotKind.OffHand:  _equipment.UnequipOffHand();              break;
            case SlotKind.Head:     _equipment.UnequipArmor(ArmorSlot.Head);  break;
            case SlotKind.Chest:    _equipment.UnequipArmor(ArmorSlot.Chest); break;
            case SlotKind.Hands:    _equipment.UnequipArmor(ArmorSlot.Hands); break;
            case SlotKind.Legs:     _equipment.UnequipArmor(ArmorSlot.Legs);  break;
            case SlotKind.Belt:     _equipment.UnequipArmor(ArmorSlot.Belt);  break;
            case SlotKind.Feet:     _equipment.UnequipArmor(ArmorSlot.Feet);  break;
        }
    }

    // ── 손에 든 것을 여기 찬다 ───────────────────────────────────────────────
    //
    // `DraggableItem.EndCarry`가 격자보다 먼저 물어본다. 규칙은 예전 드롭과 같다:
    //  · **맞는 칸인지 먼저 본다**(`Accepts`) — 못 찰 것을 옮겨 놓으면 시킨 적 없는 이동이 된다.
    //  · **한 개만** 찬다(손에 철검이 셋이어도 차는 건 하나 — 나머지는 손에 남는다).
    //  · **배낭을 안 거친다**(`Wear`) — 배낭이 꽉 차면 빈 장비칸에도 못 넣는 일이 생긴다.
    //  · 원래 차던 것은 **손에 들린다**(격자에서 밀려난 것이 손에 들리는 것과 같은 규칙).
    //    다만 손에 뭉치가 남아 있으면 둘을 같이 들 수 없으므로 그때는 배낭으로 보낸다.
    public static bool TryEquipCarried(DraggableItem carried, Vector2 screenPos)
    {
        var inst = carried != null ? carried.Carried : null;
        if (inst == null) return false;

        // **남의 진열은 여기로 못 들어온다** — 값도 안 치르고 입는 길이 된다.
        if (DraggableItem.CarriedSourceGrid != null && DraggableItem.CarriedSourceGrid.IsShop) return false;

        var slot = Under(screenPos);
        if (slot == null || slot._equipment == null) return false;

        // **왜 안 차지는지는 콘솔에 남긴다.** 거절하는 길이 셋인데(맞는 칸이 아니다 ·
        // 벗기다 막혔다 · 차다 막혔다) 아무 말이 없으면 화면에서는 전부 "안 된다" 하나로 보인다.
        if (!slot.Accepts(inst.Data))
        {
            Debug.Log($"[Equip] {slot.name}: {inst.Data.ItemName}은(는) 이 칸에 안 맞습니다.");
            return false;
        }

        var prev  = slot.GetCurrentInstance();
        // **찰 하나는 열린 것부터**(2026-10-16) — 반쯤 탄 횃불 뭉치를 끌어 차면 타다 만 것이 손에 간다.
        var one   = new ItemInstance(inst.Data, 1, 0, 0, false, inst.Quality) { Locked = inst.Locked, Left = inst.Left };
        bool rest = inst.Quantity > 1;

        if (prev != null) slot.TakeOff();        // 먼저 벗긴다 — `Wear`에 맡기면 배낭으로 보낸다

        if (!slot.Wear(one))
        {
            if (prev != null) slot.Wear(prev);   // 못 찼다 — 원래대로
            Debug.LogWarning($"[Equip] {slot.name}: {inst.Data.ItemName}을(를) 못 찼습니다 " +
                             $"(원래 차던 것: {prev?.Data?.ItemName ?? "없음"}). " +
                             "양손 무기가 보조손을 비우려는데 배낭이 꽉 찼거나, 종류가 안 맞는 경우입니다.");
            return false;
        }

        Flash(slot);   // 바뀌었다는 것을 눈에 보이게 — 같은 물건끼리면 이것 말고는 티가 없다

        // **찬 만큼 손에서 던다 — 안 그러면 물건이 복사된다.**
        //
        // 손에 든 것은 **통에서 빠져 있지 않다**(격자가 안 그릴 뿐이다 — 그래야 놓기에
        // 실패해도 제자리에 그대로 있다). 그래서 여기서 빼 주지 않으면 **장비칸에도 있고
        // 배낭에도 있는** 같은 물건이 둘이 된다(실제로 그랬다).
        var srcInv = DraggableItem.CarriedSourceGrid != null
                   ? DraggableItem.CarriedSourceGrid.Inventory : null;

        if (rest)
        {
            // 손에는 나머지가 남는다. 벗은 것을 같이 들 수는 없으니 배낭으로 보낸다.
            inst.Quantity -= 1;
            inst.Left      = 1f;          // 열린 것은 찬 쪽으로 갔다 — 남은 것은 새것이다
            srcInv?.NotifyChanged();      // 통에 있는 그 뭉치가 곧 손에 든 것이다
            carried.KeepCarryingRest();
            if (prev != null) slot.SendBack(prev, srcInv);
        }
        else
        {
            // 통째로 찼다 — 통에 있던 것이면 거기서 뺀다(손에만 있던 것이면 아무 일도 안 한다).
            srcInv?.RemoveInstance(inst);
            if (prev != null) carried.PickUpNext(prev, DraggableItem.CarriedSourceGrid);
        }

        return true;
    }

    // 커서가 장비칸 위인가 — **거절해도 격자로 흘려보내지 않으려고** 밖에서 묻는다.
    public static bool IsUnder(Vector2 screenPos) => Under(screenPos) != null;

    // ── 여기 놓으면 어떻게 되는지 보여 준다 ──────────────────────────────────
    //
    // 격자에는 놓일 칸이 그려지는데(`InventoryGridUI.ShowPreview`) **장비칸에는 아무 표시도
    // 없었다.** 손에 든 것을 칸 위에 올려도 *"여기 놓으면 장착되는가"*를 알 수가 없고,
    // 이미 차 있는 칸이면 아이콘이 그대로라 **아무 일도 안 일어난 것처럼** 보인다
    // (실제로 *"장착이 안 된다"*고 읽혔다 — 되는데도).
    //
    // **칸 배경(`bg`)을 물들이지 않는다.** 그렇게 해 봤더니 색은 칠해지고 살아 있는데
    // (읽어 보면 그대로다) **화면에는 안 보였다** — 그 위에 아이콘·프레임이 덮여 있어서다.
    // 그래서 **맨 위에 사각형을 얹는다**: 격자 미리보기와 같은 방식이라 가려질 수가 없고,
    // 예전에 장비칸이 제 드래그로 그리던 흰 상자와도 같은 모양이다.
    static EquipSlotUI  _hinted;
    static RectTransform _hintBox;

    static readonly Color HintNoColor = new(0.85f, 0.35f, 0.35f, 0.45f);

    // ── 바뀌었으면 한 번 반짝인다 ────────────────────────────────────────────
    //
    // **같은 물건을 맞바꾸면 티가 안 난다** — 칸의 그림도, 손에 들린 것도 똑같아서 아무 일도
    // 안 일어난 것처럼 보인다(등급만 다를 때가 특히 그렇다). 거절을 흔들어서 말하듯
    // (`DraggableItem.Shake`), **바뀐 것은 반짝여서** 말한다. 알림을 띄우기엔 너무 잦은 일이다.
    //
    // 손에는 벗겨진 것이 들려 있으므로 `Hint`가 계속 돌고, 그 김에 그려진다 — 반짝임만을
    // 위한 `Update`를 열두 칸에 두지 않아도 된다.
    const float FlashSeconds = 0.35f;

    static readonly Color FlashColor = new(1f, 1f, 1f, 0.85f);

    static EquipSlotUI _flashSlot;
    static float       _flashUntil;

    public static void Flash(EquipSlotUI slot)
    {
        _flashSlot  = slot;
        _flashUntil = Time.unscaledTime + FlashSeconds;
    }

    static Color OkColor()
    {
        var grid = InventoryUI.Instance != null ? InventoryUI.Instance.Grid : null;
        return grid != null ? grid.PreviewColor : new Color(0.85f, 0.85f, 0.95f, 0.30f);
    }

    public static void Hint(ItemData item, Vector2 screenPos)
    {
        var slot = item != null ? Under(screenPos) : null;
        if (slot == null) { ClearHint(); return; }

        _hinted = slot;

        if (_hintBox == null)
        {
            var go = new GameObject("EquipHint", typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;          // 커서를 먹으면 제 칸을 못 짚는다

            _hintBox = (RectTransform)go.transform;
            _hintBox.anchorMin = Vector2.zero;
            _hintBox.anchorMax = Vector2.one;
            _hintBox.offsetMin = new Vector2(2, 2);
            _hintBox.offsetMax = new Vector2(-2, -2);
        }

        _hintBox.SetParent(slot.transform, false);
        _hintBox.SetAsLastSibling();            // **맨 위** — 아이콘·프레임에 안 가린다
        _hintBox.gameObject.SetActive(true);

        var box = _hintBox.GetComponent<Image>();
        if (box == null) return;

        var color = slot.Accepts(item) ? OkColor() : HintNoColor;

        // 방금 바뀐 칸이면 밝은 데서 평소 색으로 잦아든다.
        float left = _flashSlot == slot ? _flashUntil - Time.unscaledTime : 0f;
        if (left > 0f) color = Color.Lerp(color, FlashColor, left / FlashSeconds);

        box.color = color;
    }

    public static void ClearHint()
    {
        _hinted = null;
        if (_hintBox != null) _hintBox.gameObject.SetActive(false);
    }

    // 커서 아래의 장비칸.
    static EquipSlotUI Under(Vector2 screenPos)
    {
        foreach (var slot in All)
        {
            if (slot == null || !slot.isActiveAndEnabled) continue;

            var rt     = (RectTransform)slot.transform;
            var canvas = slot.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                       ? canvas.worldCamera : null;

            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam)) return slot;
        }
        return null;
    }

    void SendBack(ItemInstance inst, Inventory source)
    {
        // 남은 양째(`TryAddLike`) — 벗은 반쯤 탄 횃불이 배낭에서 새것이 되면 안 된다.
        if (source != null && source.TryAddLike(inst) > 0) return;

        var backpack = _equipment.GetComponent<Inventory>();
        if (backpack != null && backpack.TryAddLike(inst) > 0) return;

        DroppedItem.Spawn(inst.Data, 1, _equipment.transform.position + Vector3.up * 0.15f,
                          inst.Quality, inst.Left);
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
        SlotKind.Belt     => data is ArmorData a && a.Slot == ArmorSlot.Belt,
        SlotKind.Feet     => data is ArmorData a && a.Slot == ArmorSlot.Feet,
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
        SlotKind.Belt     => _equipment.TakeOff(ArmorSlot.Belt),
        SlotKind.Feet     => _equipment.TakeOff(ArmorSlot.Feet),
        _                 => null,
    };

    // 장비칸끼리 맞바꾼다. **양쪽 다 빼놓고 나서 채운다** — 한쪽씩 처리하면 중간 상태에서
    // `ReturnToInventory`가 끼어들어 물건이 배낭이나 바닥으로 새어 나간다.
    ItemInstance GetCurrentInstance() => _kind switch
    {
        SlotKind.MainHand => _equipment?.MainHandInstance,
        SlotKind.OffHand  => _equipment?.OffHandInstance,
        SlotKind.Head     => _equipment?.ArmorInstance(ArmorSlot.Head),
        SlotKind.Chest    => _equipment?.ArmorInstance(ArmorSlot.Chest),
        SlotKind.Hands    => _equipment?.ArmorInstance(ArmorSlot.Hands),
        SlotKind.Legs     => _equipment?.ArmorInstance(ArmorSlot.Legs),
        SlotKind.Belt     => _equipment?.ArmorInstance(ArmorSlot.Belt),
        SlotKind.Feet     => _equipment?.ArmorInstance(ArmorSlot.Feet),
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
            ItemInfoUI.Show(inst.Data, inst.Quality, -1, null, false, this, inst.Left);   // 반쯤 탄 횃불이면 남은 시간
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
        SlotKind.Belt     => _equipment?.GetArmor(ArmorSlot.Belt),
        SlotKind.Feet     => _equipment?.GetArmor(ArmorSlot.Feet),
        _                 => null,
    };
}
