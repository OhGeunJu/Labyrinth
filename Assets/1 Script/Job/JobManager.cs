using System;
using System.Collections.Generic;
using UnityEngine;

public class JobManager : MonoBehaviour
{
    public static JobManager Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogError("[JobManager] �ν��Ͻ��� �����ϴ�.");
            return _instance;
        }
    }
    static JobManager _instance;

    public static event Action<JobBase> OnJobRegistered;
    public static event Action<JobBase> OnJobCompleted;
    public static event Action<JobBase> OnJobCancelled;

    readonly List<JobBase> _jobs = new();

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
    }

    // ���� ���/���� ������������������������������������������������������������������������
    public void RegisterJob(JobBase job)
    {
        _jobs.Add(job);
        OnJobRegistered?.Invoke(job);
    }

    public void RemoveJob(JobBase job)
    {
        _jobs.Remove(job);
    }

    public void CancelJob(JobBase job)
    {
        job.Cancel();
        _jobs.Remove(job);
        OnJobCancelled?.Invoke(job);
    }

    // 작업 요청 — 방랑자용 (유닛 개인 우선순위 사용)
    public JobBase RequestJob(UnitBrain unit)
    {
        var unitPriorities = unit.GetComponent<UnitPriorities>();
        return RequestJobInternal(unit, null, unitPriorities);
    }

    // 작업 요청 — 하수인용 (카테고리 필터 + 글로벌 우선순위)
    public JobBase RequestJobFiltered(UnitBrain unit, System.Collections.Generic.List<JobCategory> allowed)
        => RequestJobInternal(unit, allowed, null);

    JobBase RequestJobInternal(UnitBrain unit, System.Collections.Generic.List<JobCategory> allowed, UnitPriorities unitPriorities)
    {
        JobBase best      = null;
        int     bestScore = -1;

        foreach (var job in _jobs)
        {
            if (job.Status != JobStatus.Available) continue;
            if (allowed != null && allowed.Count > 0 && !allowed.Contains(job.Category)) continue;

            int priority = unitPriorities != null
                ? unitPriorities.Get(job.Category)
                : (PriorityManager.Instance != null ? PriorityManager.Instance.Get(job.Category) : 3);
            if (priority <= 0) continue;

            int score = priority * 100 + job.Priority;
            if (score > bestScore) { bestScore = score; best = job; }
        }

        if (best != null && best.Reserve(unit))
            return best;

        return null;
    }

    // ���� �Ϸ� ó�� ������������������������������������������������������������������������
    public void CompleteJob(JobBase job)
    {
        _jobs.Remove(job);
        OnJobCompleted?.Invoke(job);
    }
}
