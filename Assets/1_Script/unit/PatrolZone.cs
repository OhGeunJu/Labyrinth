using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PatrolMode { Waypoint, Area, Idle }

public class PatrolZone : MonoBehaviour
{
    public static event Action<PatrolZone> OnSelected;

    public string     ZoneName = "순찰 구역";
    public PatrolMode Mode     = PatrolMode.Waypoint;

    [Header("웨이포인트 모드")]
    public List<Transform> Waypoints = new();

    [Header("구역 모드")]
    public float Radius = 10f;

    readonly List<UnitBrain> _assigned = new();
    Camera _cam;

    public IReadOnlyList<UnitBrain> Assigned => _assigned;

    void OnEnable()  => _cam = Camera.main;

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit) && hit.collider.gameObject == gameObject)
            OnSelected?.Invoke(this);
    }

    public void Assign(UnitBrain unit)
    {
        if (unit == null || _assigned.Contains(unit)) return;

        // 기존 배정 구역에서 해제
        unit.Blackboard.AssignedPatrolZone?.Unassign(unit);

        _assigned.Add(unit);
        unit.Blackboard.AssignedPatrolZone = this;
    }

    public void Unassign(UnitBrain unit)
    {
        if (_assigned.Remove(unit))
            unit.Blackboard.AssignedPatrolZone = null;
    }

    // 웨이포인트 모드: index 순환
    public Vector3 GetWaypoint(int index)
    {
        if (Waypoints.Count == 0) return transform.position;
        var wp = Waypoints[index % Waypoints.Count];
        return wp != null ? wp.position : transform.position;
    }

    public int WaypointCount => Waypoints.Count;

    // 구역 모드: 반경 내 랜덤 위치
    public Vector3 GetRandomAreaPoint()
    {
        float angle = UnityEngine.Random.value * Mathf.PI * 2f;
        float dist  = UnityEngine.Random.value * Radius;
        return transform.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
    }

    void OnDrawGizmosSelected()
    {
        if (Mode == PatrolMode.Area)
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, Radius);
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
        else
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < Waypoints.Count; i++)
            {
                if (Waypoints[i] == null) continue;
                Gizmos.DrawSphere(Waypoints[i].position, 0.3f);
                if (i + 1 < Waypoints.Count && Waypoints[i + 1] != null)
                    Gizmos.DrawLine(Waypoints[i].position, Waypoints[i + 1].position);
            }
            if (Waypoints.Count > 1 && Waypoints[0] != null && Waypoints[^1] != null)
                Gizmos.DrawLine(Waypoints[^1].position, Waypoints[0].position);
        }
    }
}
