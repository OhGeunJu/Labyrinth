using UnityEngine;

public enum JobStatus
{
    Available,
    Reserved,
    InProgress,
    Done,
    Cancelled
}

public abstract class JobBase
{
    public Vector2Int TileCoord { get; protected set; }
    public JobStatus Status { get; protected set; } = JobStatus.Available;
    public UnitBrain ReservedBy { get; private set; }
    public int Priority { get; protected set; } = 0;
    public virtual JobCategory Category => JobCategory.None;

    // 지금 이 작업을 실제로 수행할 수 있는가 (예: 제작 재료 보유).
    // false면 JobManager가 배정을 미루고 큐에 대기 — 조건 충족 시 자동 재개.
    public virtual bool IsReady => true;

    // ── 작업 현장(건물) ────────────────────────────────────────────────
    // 이 작업이 매달린 건물. 핵 영역 밖으로 나가 휴면 중이면 배정하지 않는다.
    // 취소가 아니라 대기 — 영역이 복구되면 주문/일감이 그대로 재개된다(재료 부족 대기와 같은 정책).
    BuildingDormancy _siteDormancy;

    protected void SetSite(Component site)
    {
        _siteDormancy = site != null ? site.GetComponentInParent<BuildingDormancy>() : null;
    }

    public bool IsSiteUsable => _siteDormancy == null || !_siteDormancy.IsDormant;

    // ── 예약 ──────────────────────────────────────────────────────────
    public bool Reserve(UnitBrain unit)
    {
        if (Status != JobStatus.Available) return false;
        ReservedBy = unit;
        Status = JobStatus.Reserved;
        return true;
    }

    public void ReleaseReservation()
    {
        ReservedBy = null;
        Status = JobStatus.Available;
    }

    // ── 진행 ──────────────────────────────────────────────────────────
    public void Start()
    {
        Status = JobStatus.InProgress;
        OnStart();
    }

    public void Tick(UnitBrain unit)
    {
        if (Status != JobStatus.InProgress) return;
        Execute(unit);
    }

    public void Cancel()
    {
        ReleaseReservation();
        Status = JobStatus.Cancelled;
        OnCancelled();
    }

    public bool IsComplete() => Status == JobStatus.Done;

    // ── 파생 클래스 구현 ───────────────────────────────────────────────
    protected abstract void Execute(UnitBrain unit);
    protected virtual void OnStart() { }
    protected virtual void OnCancelled() { }
}