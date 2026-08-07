using System;
using System.Collections.Generic;
using UnityEngine;

public class Equipment : MonoBehaviour
{
    public static event Action<Equipment> OnEquipChanged;

    public WeaponData MainHand { get; private set; }
    public WeaponData OffHand  { get; private set; }

    public bool OffHandLocked => MainHand != null && MainHand.IsLargeWeapon;

    readonly Dictionary<ArmorSlot, ArmorData>     _armor    = new();
    readonly Dictionary<ArmorSlot, ItemInstance>  _armorInst = new();
    ItemInstance _mainHandInst;
    ItemInstance _offHandInst;

    public ArmorData GetArmor(ArmorSlot slot) =>
        _armor.TryGetValue(slot, out var a) ? a : null;

    // 장착 아이템의 등급 배율 (갑옷 방어력에 곱함)
    public float GetArmorQualityMult(ArmorSlot slot) =>
        _armorInst.TryGetValue(slot, out var inst) ? inst.Quality.StatMultiplier() : 1f;

    Inventory _inventory;
    UnitBlackboard _bb;
    UnitData _unitData;

    void Start()
    {
        _inventory = GetComponent<Inventory>();
        var brain  = GetComponent<UnitBrain>();
        _bb        = brain.Blackboard;
        _unitData  = brain.Data;
    }

    // 인벤토리에서 무기를 꺼내 MainHand에 장착
    public bool Equip(WeaponData weapon)
    {
        if (weapon == null) return false;

        // **방패는 주손에 들지 않는다.** 주손 칸에 떨어뜨려도 보조손으로 보낸다 —
        // 방패를 들고 때릴 수는 없는데, 막지 않으면 주손에 끼워 무기를 못 드는 상태가 된다.
        if (weapon.Type == WeaponType.Shield) return EquipOffHand(weapon);

        var inst = FindInInventory(weapon);
        // 찾은 인스턴스를 정확히 소모 — Remove(data)는 다른 등급 인스턴스를 뺄 수 있음
        if (inst == null || !_inventory.RemoveOne(inst)) return false;

        if (MainHand != null)
            ReturnToInventory(MainHand, _mainHandInst?.Quality ?? ItemQuality.Common);

        MainHand      = weapon;
        _mainHandInst = inst;
        SyncBlackboard();
        OnEquipChanged?.Invoke(this);
        Debug.Log($"[Equipment] {gameObject.name} 장착: {weapon.ItemName} [{inst.Quality.DisplayName()}]");
        return true;
    }

    // MainHand 해제 → 인벤토리로 반환
    public void Unequip()
    {
        if (MainHand == null) return;

        ReturnToInventory(MainHand, _mainHandInst?.Quality ?? ItemQuality.Common);
        Debug.Log($"[Equipment] {gameObject.name} 해제: {MainHand.ItemName}");
        MainHand      = null;
        _mainHandInst = null;
        SyncBlackboard();
        OnEquipChanged?.Invoke(this);
    }

    public bool EquipOffHand(WeaponData item)
    {
        if (item == null) return false;
        if (OffHandLocked) return false;
        var inst = FindInInventory(item);
        if (inst == null || !_inventory.RemoveOne(inst)) return false;

        if (OffHand != null)
            ReturnToInventory(OffHand, _offHandInst?.Quality ?? ItemQuality.Common);

        OffHand      = item;
        _offHandInst = inst;
        OnEquipChanged?.Invoke(this);
        Debug.Log($"[Equipment] {gameObject.name} 오프핸드 장착: {item.ItemName} [{inst.Quality.DisplayName()}]");
        return true;
    }

    public void UnequipOffHand()
    {
        if (OffHand == null) return;

        ReturnToInventory(OffHand, _offHandInst?.Quality ?? ItemQuality.Common);
        Debug.Log($"[Equipment] {gameObject.name} 오프핸드 해제: {OffHand.ItemName}");
        OffHand      = null;
        _offHandInst = null;
        OnEquipChanged?.Invoke(this);
    }

    public bool EquipArmor(ArmorData armor)
    {
        if (armor == null) return false;
        var inst = FindInInventory(armor);
        if (inst == null || !_inventory.RemoveOne(inst)) return false;

        if (_armor.TryGetValue(armor.Slot, out var prev))
            ReturnToInventory(prev, _armorInst.TryGetValue(armor.Slot, out var pi) ? pi.Quality : ItemQuality.Common);

        _armor[armor.Slot]    = armor;
        _armorInst[armor.Slot] = inst;
        OnEquipChanged?.Invoke(this);
        Debug.Log($"[Equipment] {gameObject.name} 갑옷 장착: {armor.ItemName} ({armor.Slot}) [{inst.Quality.DisplayName()}]");
        return true;
    }

    public void UnequipArmor(ArmorSlot slot)
    {
        if (!_armor.TryGetValue(slot, out var armor)) return;

        _armorInst.TryGetValue(slot, out var inst);
        ReturnToInventory(armor, inst?.Quality ?? ItemQuality.Common);
        _armor.Remove(slot);
        _armorInst.Remove(slot);
        OnEquipChanged?.Invoke(this);
        Debug.Log($"[Equipment] {gameObject.name} 갑옷 해제: {armor.ItemName} ({slot})");
    }

    // Blackboard 상태를 장착 상태에 맞게 갱신 (무기 등급 배율 반영)
    void SyncBlackboard()
    {
        _bb.IsUnarmed = MainHand == null;
        float qualMult = _mainHandInst?.Quality.StatMultiplier() ?? 1f;
        _bb.AttackDamage = MainHand != null
            ? MainHand.BaseDamage * qualMult
            : (_unitData != null ? _unitData.AttackDamage : 10f);
        _bb.AttackRange = MainHand != null
            ? MainHand.AttackRange
            : (_unitData != null ? _unitData.AttackRange : 1.5f);
    }

    // 벗은 것을 배낭에 돌려준다. **자리가 없으면 발밑에 내려놓는다.**
    //
    // 전에는 `TryAdd`의 반환값을 안 보고 넣어서, 격자가 꽉 차면 **차고 있던 장비가 조용히
    // 사라졌다.** 하나를 빼면 하나가 들어갈 자리가 생길 것 같지만 그렇지 않다 — 새로 차는
    // 물건이 스택 안에 있었으면(철검 3개 중 하나) 빼도 칸이 안 비고, 벗은 것은 새 칸을 찾는다.
    //
    // 교체를 거절하는 길도 있었지만, 그러면 "왜 안 입지"가 되고 원인이 화면에 안 남는다.
    // 내려놓으면 눈에 보이고 운반이 알아서 창고로 옮긴다 — 이 프로젝트의 증발 방지 관례다.
    void ReturnToInventory(ItemData item, ItemQuality quality)
    {
        if (item == null) return;
        if (_inventory != null && _inventory.TryAdd(item, 1, quality) > 0) return;

        DroppedItem.Spawn(item, 1, transform.position + Vector3.up * 0.15f, quality);
        Debug.Log($"[Equipment] {gameObject.name}: 배낭에 자리가 없어 {item.ItemName}을 발밑에 내려놓았습니다.");
    }

    ItemInstance FindInInventory(ItemData data)
    {
        foreach (var inst in _inventory.Items)
            if (inst.Data == data) return inst;
        return null;
    }
}
