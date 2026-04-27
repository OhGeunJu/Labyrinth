using System;
using System.Collections.Generic;
using UnityEngine;

public class Equipment : MonoBehaviour
{
    public static event Action<Equipment> OnEquipChanged;

    public WeaponData MainHand { get; private set; }
    public WeaponData OffHand  { get; private set; }

    public bool OffHandLocked => MainHand != null && MainHand.IsLargeWeapon;

    readonly Dictionary<ArmorSlot, ArmorData> _armor = new();

    public ArmorData GetArmor(ArmorSlot slot) =>
        _armor.TryGetValue(slot, out var a) ? a : null;

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
        if (!_inventory.Consume(weapon, 1)) return false;

        // 기존 장착 무기를 인벤토리로 반환
        if (MainHand != null)
            _inventory.Add(MainHand, 1);

        MainHand = weapon;
        SyncBlackboard();
        OnEquipChanged?.Invoke(this);
        Debug.Log($"[Equipment] {gameObject.name} 장착: {weapon.ItemName}");
        return true;
    }

    // MainHand 해제 → 인벤토리로 반환
    public void Unequip()
    {
        if (MainHand == null) return;

        _inventory.Add(MainHand, 1);
        Debug.Log($"[Equipment] {gameObject.name} 해제: {MainHand.ItemName}");
        MainHand = null;
        SyncBlackboard();
        OnEquipChanged?.Invoke(this);
    }

    public bool EquipOffHand(WeaponData item)
    {
        if (item == null) return false;
        if (OffHandLocked) return false;
        if (!_inventory.Consume(item, 1)) return false;

        if (OffHand != null)
            _inventory.Add(OffHand, 1);

        OffHand = item;
        OnEquipChanged?.Invoke(this);
        Debug.Log($"[Equipment] {gameObject.name} 오프핸드 장착: {item.ItemName}");
        return true;
    }

    public void UnequipOffHand()
    {
        if (OffHand == null) return;

        _inventory.Add(OffHand, 1);
        Debug.Log($"[Equipment] {gameObject.name} 오프핸드 해제: {OffHand.ItemName}");
        OffHand = null;
        OnEquipChanged?.Invoke(this);
    }

    public bool EquipArmor(ArmorData armor)
    {
        if (armor == null) return false;
        if (!_inventory.Consume(armor, 1)) return false;

        if (_armor.TryGetValue(armor.Slot, out var prev))
            _inventory.Add(prev, 1);

        _armor[armor.Slot] = armor;
        OnEquipChanged?.Invoke(this);
        Debug.Log($"[Equipment] {gameObject.name} 갑옷 장착: {armor.ItemName} ({armor.Slot})");
        return true;
    }

    public void UnequipArmor(ArmorSlot slot)
    {
        if (!_armor.TryGetValue(slot, out var armor)) return;

        _inventory.Add(armor, 1);
        _armor.Remove(slot);
        OnEquipChanged?.Invoke(this);
        Debug.Log($"[Equipment] {gameObject.name} 갑옷 해제: {armor.ItemName} ({slot})");
    }

    // Blackboard 상태를 장착 상태에 맞게 갱신
    void SyncBlackboard()
    {
        _bb.IsUnarmed    = MainHand == null;
        _bb.AttackDamage = MainHand != null
            ? MainHand.BaseDamage
            : (_unitData != null ? _unitData.AttackDamage : 10f);
        _bb.AttackRange  = MainHand != null
            ? MainHand.AttackRange
            : (_unitData != null ? _unitData.AttackRange : 1.5f);
    }
}
