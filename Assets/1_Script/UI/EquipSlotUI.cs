using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 장착 슬롯 1칸. Init으로 종류 설정, Bind로 Equipment 교체.
public class EquipSlotUI : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IDropHandler
{
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

    public void OnDrop(PointerEventData e)
    {
        var inst = DraggableItem.DraggedInstance;
        if (inst == null || _equipment == null) return;

        bool ok = false;
        switch (_kind)
        {
            case SlotKind.MainHand:
                if (inst.Data is WeaponData mw) ok = _equipment.Equip(mw);          break;
            case SlotKind.OffHand:
                if (inst.Data is WeaponData ow) ok = _equipment.EquipOffHand(ow);   break;
            case SlotKind.Head:
                if (inst.Data is ArmorData ha && ha.Slot == ArmorSlot.Head)
                    ok = _equipment.EquipArmor(ha);  break;
            case SlotKind.Chest:
                if (inst.Data is ArmorData ca && ca.Slot == ArmorSlot.Chest)
                    ok = _equipment.EquipArmor(ca);  break;
            case SlotKind.Hands:
                if (inst.Data is ArmorData na && na.Slot == ArmorSlot.Hands)
                    ok = _equipment.EquipArmor(na);  break;
            case SlotKind.Legs:
                if (inst.Data is ArmorData la && la.Slot == ArmorSlot.Legs)
                    ok = _equipment.EquipArmor(la);  break;
        }

        if (ok) DraggableItem.DropHandled = true;
    }

    // ── 툴팁 ─────────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _)
    {
        var item = GetCurrentItem();
        if (item != null) ItemInfoPanel.Show(item, ItemQuality.Common);
    }

    public void OnPointerExit(PointerEventData _) => ItemInfoPanel.Hide();

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
