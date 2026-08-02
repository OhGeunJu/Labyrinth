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
            _inventory.TryAdd(MainHand, 1, _mainHandInst?.Quality ?? ItemQuality.Common);

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

        _inventory.TryAdd(MainHand, 1, _mainHandInst?.Quality ?? ItemQuality.Common);
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
            _inventory.TryAdd(OffHand, 1, _offHandInst?.Quality ?? ItemQuality.Common);

        OffHand      = item;
        _offHandInst = inst;
        OnEquipChanged?.Invoke(this);
        Debug.Log($"[Equipment] {gameObject.name} 오프핸드 장착: {item.ItemName} [{inst.Quality.DisplayName()}]");
        return true;
    }

    public void UnequipOffHand()
    {
        if (OffHand == null) return;

        _inventory.TryAdd(OffHand, 1, _offHandInst?.Quality ?? ItemQuality.Common);
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
            _inventory.TryAdd(prev, 1, _armorInst.TryGetValue(armor.Slot, out var pi) ? pi.Quality : ItemQuality.Common);

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
        _inventory.TryAdd(armor, 1, inst?.Quality ?? ItemQuality.Common);
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

    ItemInstance FindInInventory(ItemData data)
    {
        foreach (var inst in _inventory.Items)
            if (inst.Data == data) return inst;
        return null;
    }
}
