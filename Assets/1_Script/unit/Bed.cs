using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Bed : MonoBehaviour, IWorldClickable
{
    public static event Action<Bed> OnSelected;
    static readonly List<Bed> _all = new();

    public string BedName = "침대";

    [Header("눕는 자세")]
    [Tooltip("있으면 이 지점의 위치·회전으로 눕는다. 비우면 침대 윗면 중앙에 자동 배치.")]
    [SerializeField] Transform sleepAnchor;

    [Tooltip("sleepAnchor가 없을 때 침대 윗면에서 폰을 띄우는 높이.")]
    [SerializeField] float lieHeight = 0.35f;

    [Tooltip("침대 옆 접근 지점을 침대 가장자리에서 얼마나 떨어뜨릴지.")]
    [SerializeField] float approachMargin = 0.8f;

    public void Init(BuildingData data) { if (data != null) BedName = data.BuildingName; }
    public UnitBrain  AssignedUnit { get; private set; }

    // 누울 위치 — 침대 윗면 중앙 위. 렌더러가 없으면 오브젝트 원점.
    public Vector3 SleepPosition
    {
        get
        {
            if (sleepAnchor != null) return sleepAnchor.position;

            var bounds = WorldBounds();
            return new Vector3(bounds.center.x, bounds.max.y + lieHeight, bounds.center.z);
        }
    }

    // 침대 옆에서 설 수 있는 지점 — 이동 목표로 쓴다.
    // 침대 중심을 그대로 목표로 주면 폰이 침대에 정면으로 처박혀 멈춘다(침대는 길찾기 그래프에
    // 없는 장애물이라 경로가 관통해서 나온다). 네 면 중 **설 수 있고 가장 가까운** 쪽을 고른다.
    public Vector3 ApproachPosition(Vector3 from)
    {
        var bounds = WorldBounds();
        float reach = Mathf.Max(bounds.extents.x, bounds.extents.z) + approachMargin;

        Vector3 center = bounds.center;
        Vector3[] sides =
        {
            center + transform.right   * reach,
            center - transform.right   * reach,
            center + transform.forward * reach,
            center - transform.forward * reach,
        };

        // 1차: 설 수 있는 면 중 가장 가까운 쪽
        Vector3 best        = center;
        float   bestDistSqr = float.MaxValue;

        foreach (var side in sides)
        {
            if (!IsOpen(side)) continue;
            float distSqr = (side - from).sqrMagnitude;
            if (distSqr >= bestDistSqr) continue;
            bestDistSqr = distSqr;
            best        = side;
        }
        if (bestDistSqr < float.MaxValue) return best;

        // 2차: 네 면이 다 막혔으면 그냥 가장 가까운 면으로 (그 앞에서 멈추더라도 붙는다)
        foreach (var side in sides)
        {
            float distSqr = (side - from).sqrMagnitude;
            if (distSqr >= bestDistSqr) continue;
            bestDistSqr = distSqr;
            best        = side;
        }

        return best;
    }

    // 그 지점에 설 수 있는가 — 바닥이 있고(던전) 건물에 점유되지 않았는가
    static bool IsOpen(Vector3 world)
    {
        var map = MapManager.GetForPosition(world);
        if (map == null) return true;

        var tile = map.WorldToTile(world);
        return map.IsStandable(tile) && map.GetTile(tile.x, tile.y).IsPassable;
    }

    Bounds WorldBounds()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    // 누운 자세 — 폰이 다가온 면에 맞춰 네 방향 중 하나로 눕는다(머리는 들어온 쪽 반대편).
    // 침대를 어느 쪽에서 접근하든 자연스럽게 보이도록. sleepAnchor가 있으면 그 회전 고정.
    public Quaternion SleepRotationFrom(Vector3 approachPos)
    {
        if (sleepAnchor != null) return sleepAnchor.rotation;

        // 침대 로컬 기준으로 폰이 어느 면에 있는지 → ±X / ±Z 네 면 중 가까운 쪽
        Vector3 local = transform.InverseTransformPoint(approachPos);

        float yawOffset;
        if (Mathf.Abs(local.x) >= Mathf.Abs(local.z)) yawOffset = local.x >= 0f ? 90f : -90f;
        else                                          yawOffset = local.z >= 0f ? 0f  : 180f;

        return Quaternion.Euler(-90f, transform.eulerAngles.y + yawOffset, 0f);
    }

    UnitBrain _occupant;
    public bool IsOccupied => _occupant != null;

    void OnEnable()  => _all.Add(this);
    void OnDisable() => _all.Remove(this);

    // SelectionManager의 단일 레이캐스트가 이 건물을 클릭 판정 시 호출.
    public void OnWorldClick() => OnSelected?.Invoke(this);

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
        // 같은 유닛의 재점유는 허용 — 전투 등으로 잠시 자리를 뜬 뒤 돌아올 때 막히면 안 된다.
        if (_occupant != null && _occupant != unit) return false;
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
