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
                Debug.LogError("[JobManager] 인스턴스가 없습니다.");
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

    // ── 작업 등록/제거 ───────────────────────────────────────────────
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

    // 작업 요청 — 방랑자·하수인 공통.
    // 폰이 그 작업을 하는지 여부는 개체별 WorkSettings(on/off), 무엇을 먼저 할지는
    // JobCategoryInfo.QueueOrder(전역 고정 순서)가 정한다.
    public JobBase RequestJob(UnitBrain unit)
    {
        var settings = unit != null ? unit.Work : null;

        JobBase best      = null;
        int     bestScore = -1;

        foreach (var job in _jobs)
        {
            if (job.Status != JobStatus.Available) continue;
            if (!job.IsReady) continue;   // 재료 부족 등 미준비 작업은 큐에 두고 배정 보류
            if (!job.IsSiteUsable) continue; // 휴면(핵 영역 밖) 건물의 일감도 대기 — 영역 복구 시 재개

            // 큐 밖 카테고리(채굴·벌목 등 직접 배정)는 애초에 여기서 배정되지 않는다.
            int weight = JobCategoryInfo.Weight(job.Category);
            if (weight <= 0) continue;

            if (settings != null && !settings.IsEnabled(job.Category)) continue;

            int score = weight * 100 + job.Priority;
            if (score > bestScore) { bestScore = score; best = job; }
        }

        if (best != null && best.Reserve(unit))
            return best;

        return null;
    }

    // ── 작업 완료 처리 ───────────────────────────────────────────────
    public void CompleteJob(JobBase job)
    {
        _jobs.Remove(job);
        OnJobCompleted?.Invoke(job);
    }
}
