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

    [Tooltip("**베개가 어느 쪽인가** — 머리를 둘 방향(침대 기준 각도).\n" +
             "0 = 침대 정면(+Z) 쪽에 머리. 모델의 베개가 반대 끝이면 180, 옆이면 ±90.\n" +
             "폰이 다가온 방향과 무관하게 늘 같은 쪽으로 눕는다.")]
    [SerializeField] float pillowYaw;

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

    // **머리는 베개 쪽이다 — 침대가 정한다.**
    //
    // 전에는 **폰이 다가온 면**을 보고 네 방향 중 하나로 눕혔다. 어느 쪽에서 와도 자연스럽게
    // 보이라는 뜻이었지만, 침대에는 베개가 있다 — 같은 침대인데 걸어온 방향에 따라 머리가
    // 발치에 가고, 두 폰이 번갈아 쓰면 매번 반대로 눕는다.
    //
    // 그래서 **침대의 방향만 본다.** 기본은 침대 정면(+Z) 쪽에 머리를 둔다. 모델의 베개가
    // 다른 끝에 있으면 `pillowYaw`를 180으로(옆이면 ±90) — 프리팹마다 한 번 적으면 끝난다.
    // `sleepAnchor`를 두면 그 회전이 이긴다(가장 세게 저작하는 길).
    public Quaternion SleepRotation
    {
        get
        {
            if (sleepAnchor != null) return sleepAnchor.rotation;
            return Quaternion.Euler(-90f, transform.eulerAngles.y + pillowYaw, 0f);
        }
    }

    UnitBrain _occupant;
    public bool IsOccupied => _occupant != null;

    // 지금 이 침대에 **누워 있는** 폰. 배정(`AssignedUnit`)과 다르다 — 배정은 명령이고
    // 이건 실제로 자리에 있느냐다. 치료는 자리에 있어야 할 수 있다.
    public UnitBrain Occupant => _occupant;

    void OnEnable()  => _all.Add(this);
    void OnDisable() { _all.Remove(this); DropTreatJob(); }

    // ── 치료 일감 ────────────────────────────────────────────────────────────
    //
    // **침대가 일감을 낸다.** 다친 폰 자신이 내면 "누가 등록을 지우는가"가 애매해진다
    // (기절해 있거나 죽으면 스스로 못 지운다). 침대는 자리를 아는 유일한 물건이고,
    // 작업대가 제작 주문을 내는 것과 같은 자리다.
    //
    // **한 회차씩** 건다 — `TreatJob`이 끝나면 아직 나을 것이 남았을 때 다음 것이 걸린다.
    // 그래야 회차마다 약을 하나씩 쓰고, 도중에 물자가 떨어져도 맨손으로 이어진다.
    const float CheckInterval = 1f;

    TreatJob _treatJob;
    float    _nextCheck;

    void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + CheckInterval;

        // 끝났거나 취소된 것은 붙들고 있어 봐야 다음 회차를 막을 뿐이다.
        if (_treatJob != null && _treatJob.Status is JobStatus.Done or JobStatus.Cancelled)
            _treatJob = null;

        var body = _occupant != null ? _occupant.GetComponent<BodyPartSystem>() : null;
        bool wanted = body != null && body.NeedsTreatment;

        if (!wanted) { DropTreatJob(); return; }
        if (_treatJob != null) return;

        // **플레이어 세력만** 돌본다 — 붙잡아 온 적을 우리 침대에 눕힐 길이 아직 없고,
        // 생기면 그때 여기서 갈라야 한다는 것이 드러나야 한다.
        var faction = _occupant.GetComponent<Faction>();
        if (faction == null || !faction.IsPlayer) return;
        if (JobManager.Instance == null) return;

        _treatJob = new TreatJob(this);
        JobManager.Instance.RegisterJob(_treatJob);
    }

    // 지금 이 침대에 치료가 필요한가 — 우클릭 메뉴가 선택지를 낼지 정할 때 쓴다.
    public bool NeedsTreatment
    {
        get
        {
            var body = _occupant != null ? _occupant.GetComponent<BodyPartSystem>() : null;
            return body != null && body.NeedsTreatment;
        }
    }

    // **이 의사에게 맡긴다**(우클릭 [치료한다]).
    //
    // 새로 만들지 않고 **있던 일감을 그대로 넘긴다** — 침대가 일감의 주인이라, 여기서 하나
    // 더 만들면 같은 환자에게 둘이 붙는다. 이미 남이 하고 있으면 뺏지 않는다(약만 버려진다).
    public bool RequestTreatBy(UnitBrain doctor)
    {
        if (doctor == null || doctor == _occupant || !NeedsTreatment) return false;
        if (JobManager.Instance == null) return false;

        if (_treatJob != null && _treatJob.Status == JobStatus.InProgress) return false;

        if (_treatJob == null || _treatJob.Status is JobStatus.Done or JobStatus.Cancelled)
        {
            _treatJob = new TreatJob(this);
            JobManager.Instance.ReserveFor(_treatJob, doctor);
            return true;
        }

        // 아직 아무도 안 집었거나 남이 예약만 해 둔 것 — 이 의사 앞으로 다시 잡는다.
        _treatJob.ReleaseReservation();
        _treatJob.Reserve(doctor);
        return true;
    }

    void DropTreatJob()
    {
        if (_treatJob == null) return;

        // 아직 아무도 안 집었거나 예약만 된 것은 거둔다. 이미 하고 있는 것은 그대로 끝내게
        // 둔다 — 환자가 잠깐 일어나도 하던 손을 놓게 하면 약만 버려진다.
        if (_treatJob.Status is JobStatus.Available or JobStatus.Reserved)
        {
            JobManager.Instance?.RemoveJob(_treatJob);
            _treatJob.Cancel();
        }
        _treatJob = null;
    }

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

    // **이 폰을 침대에서 놓아 준다** — 배정과 점유를 함께 거둔다.
    //
    // 명령을 받으면(이동·공격·따라가기·줍기) 침대를 떠나야 하는데, 그때 **배정만 두면 도착하는
    // 순간 `RestNode`가 도로 눕히고**(배정이 곧 "쉬어라"라는 명령이라 그게 맞다),
    // **점유만 두면 그 침대를 아무도 못 쓴다** — `RestNode`가 자기 `_bed`를 붙들고 있는데
    // 그 노드는 명령이 도는 동안 아예 안 돌아 스스로 비울 기회가 없다.
    //
    // 그 사이 `Bed.Update`는 자리에 없는 사람을 치료하겠다고 일감을 계속 걸었다.
    public static void ReleaseFrom(UnitBrain unit)
    {
        if (unit == null) return;

        foreach (var bed in _all)
        {
            if (bed.AssignedUnit == unit) bed.AssignedUnit = null;
            bed.Vacate(unit);
        }
    }

    // 이 폰이 **지금 누워 있는** 침대. 폰을 직접 눌러 치료를 시킬 때, 침대에 있는 사람이면
    // 침대 쪽 일감을 써야 한다(여기서 새로 만들면 같은 사람에게 둘이 붙는다).
    public static Bed FindOccupiedBy(UnitBrain unit)
    {
        if (unit == null) return null;
        foreach (var bed in _all)
            if (bed._occupant == unit) return bed;
        return null;
    }

    // 해당 유닛에게 배정된 침대 반환
    public static Bed FindAssigned(UnitBrain unit)
    {
        foreach (var bed in _all)
            if (bed.AssignedUnit == unit) return bed;
        return null;
    }
}
