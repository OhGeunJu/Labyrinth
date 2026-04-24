using System;
using UnityEngine;

public class Equipment : MonoBehaviour
{
    public static event Action<Equipment> OnEquipChanged;

    public WeaponData MainHand { get; private set; }

    // 양손 무기가 장착되면 OffHand 슬롯은 잠김
    public bool OffHandLocked => MainHand != null && MainHand.IsLargeWeapon;

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
