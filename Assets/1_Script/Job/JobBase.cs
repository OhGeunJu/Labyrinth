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

    // ���� ���� ��������������������������������������������������������������������������������
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

    // ���� ���� ��������������������������������������������������������������������������������
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

    // ���� ���� Ŭ�������� ���� ��������������������������������������������������
    protected abstract void Execute(UnitBrain unit);
    protected virtual void OnStart() { }
    protected virtual void OnCancelled() { }
}