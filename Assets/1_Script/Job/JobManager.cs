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
        float   bestDist  = float.MaxValue;

        // 동점일 때 **가까운 것**을 고른다.
        // 전에는 점수만 봤는데 같은 카테고리·같은 Priority면 점수가 **완전히 같아서**
        // 항상 `_jobs`에 먼저 들어간 것이 이겼다. 그래서 계속 일감을 뿜는 노드 하나가
        // 뒤에 등록된 노드의 운반을 영구히 굶겼다 — 폰을 늘려도 전부 같은 곳으로 갔다.
        Vector3 from = unit != null ? unit.transform.position : Vector3.zero;

        foreach (var job in _jobs)
        {
            if (job.Status != JobStatus.Available) continue;
            if (!job.IsReady) continue;   // 재료 부족 등 미준비 작업은 큐에 두고 배정 보류
            if (!job.IsSiteUsable) continue; // 휴면(핵 영역 밖) 건물의 일감도 대기 — 영역 복구 시 재개

            // 큐 밖 카테고리(채굴·벌목 등 직접 배정)는 애초에 여기서 배정되지 않는다.
            int weight = JobCategoryInfo.Weight(job.Category);
            if (weight <= 0) continue;

            if (settings != null && !settings.IsEnabled(job.Category)) continue;
            if (!job.CanAssignTo(unit)) continue;   // 지정 제작자 / 만들던 사람만 이어받는 장비

            int   score = weight * 100 + job.Priority;
            float dist  = MapManager.Instance != null
                        ? Vector3.SqrMagnitude(MapManager.Instance.TileToWorld(job.TileCoord) - from)
                        : 0f;

            if (score > bestScore || (score == bestScore && dist < bestDist))
            {
                bestScore = score;
                bestDist  = dist;
                best      = job;
            }
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
