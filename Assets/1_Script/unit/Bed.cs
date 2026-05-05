using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Bed : MonoBehaviour
{
    public static event Action<Bed> OnSelected;
    static readonly List<Bed> _all = new();

    public string BedName = "침대";

    public void Init(BuildingData data) { if (data != null) BedName = data.BuildingName; }
    public UnitBrain  AssignedUnit { get; private set; }

    UnitBrain _occupant;
    Camera    _cam;
    public bool IsOccupied => _occupant != null;

    void OnEnable()  { _all.Add(this);    _cam = Camera.main; }
    void OnDisable() => _all.Remove(this);

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit) && hit.collider.gameObject == gameObject)
            OnSelected?.Invoke(this);
    }

    public void Assign(UnitBrain unit)
    {
        // 기존 배정 유닛의 침대를 빼앗는 경우 해제
        if (unit != null)
            foreach (var bed in _all)
                if (bed != this && bed.AssignedUnit == unit)
                    bed.AssignedUnit = null;

        AssignedUnit = unit;
    }

    public void Unassign() => AssignedUnit = null;

    public bool TryOccupy(UnitBrain unit)
    {
        if (_occupant != null) return false;
        _occupant = unit;
        return true;
    }

    public void Vacate(UnitBrain unit)
    {
        if (_occupant == unit) _occupant = null;
    }

    // 해당 유닛에게 배정된 침대 반환
    public static Bed FindAssigned(UnitBrain unit)
    {
        foreach (var bed in _all)
            if (bed.AssignedUnit == unit) return bed;
        return null;
    }
}
