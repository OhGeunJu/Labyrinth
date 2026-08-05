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

    // **누가 집을 수 있는가.** `IsReady`(일감 자체가 준비됐나)와 다른 축이다 —
    // 준비는 됐는데 **이 폰은 안 되는** 경우가 있다(지정 제작자, 만들던 사람이 이어서 해야 하는 장비).
    public virtual bool CanAssignTo(UnitBrain unit) => true;

    // ── 작업 현장(건물) ────────────────────────────────────────────────
    // 이 작업이 매달린 건물. 핵 영역 밖으로 나가 휴면 중이면 배정하지 않는다.
    // 취소가 아니라 대기 — 영역이 복구되면 주문/일감이 그대로 재개된다(재료 부족 대기와 같은 정책).
    BuildingDormancy _siteDormancy;

    protected void SetSite(Component site)
    {
        _siteDormancy = site != null ? site.GetComponentInParent<BuildingDormancy>() : null;

        // **목표 칸도 여기서 정한다.** 전에는 각 Job이 `WorldToTile(건물 위치)`를 직접 넣었는데,
        // 그 칸은 대개 통행 불가라 폰이 도착 판정에 못 걸렸다. `WorkSpot`이 저작된 자리 또는
        // 주변의 설 수 있는 칸을 돌려주므로, 한 곳에서 고치면 모든 작업이 같이 고쳐진다.
        if (site != null) TileCoord = WorkSpot.TileFor(site);
    }

    public bool IsSiteUsable => _siteDormancy == null || !_siteDormancy.IsDormant;

    // ── 예약 ──────────────────────────────────────────────────────────
    public bool Reserve(UnitBrain unit)
    {
        if (Status != JobStatus.Available) return false;
        ReservedBy = unit;
        Status = JobStatus.Reserved;
        OnReserved(unit);
        return true;
    }

    // **누가 할지 정해지는 순간.** 폰마다 다른 값으로 일감을 다시 재야 할 때 쓴다 —
    // 등록 시점엔 누가 올지 모르므로 넉넉히 잡아 두고, 여기서 그 폰에 맞춰 줄인다
    // (운반이 실을 양을 폰의 적재 한도로 자르는 데 쓴다).
    protected virtual void OnReserved(UnitBrain unit) { }

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