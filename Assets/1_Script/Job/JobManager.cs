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

        // **손으로 맡긴 일감이 먼저다.** 플레이어가 우클릭으로 "저 사람을 치료해라"라고 하면
        // 그 일감을 이 폰 이름으로 미리 예약해 둔다(`ReserveFor`). 그러면 아래 추첨은
        // `Available`만 보므로 영영 안 걸리는데, 그건 명령을 무시하는 것이다.
        //
        // **작업 설정(on/off)도 안 본다** — 손으로 시킨 일은 "이 작업을 맡길까"의 결과가
        // 아니라 지금 이 순간의 지시다(자원 노드 직접 배정과 같은 규칙).
        PruneDeadTreatJobs();

        var mine = FindReserved(unit);
        if (mine != null) return mine;

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

    // **환자가 사라진 치료 일감을 걷어낸다.** `IsReady`가 false라 배정은 안 되지만, 그대로 두면
    // 큐에 영원히 남아 매 요청마다 훑히고 `HasTreatJobFor`의 판단도 흐린다.
    // (다른 일감은 매달린 건물이 사라지면 그쪽에서 지운다 — 치료는 대상이 폰이라 주인이 없다.)
    void PruneDeadTreatJobs()
    {
        for (int i = _jobs.Count - 1; i >= 0; i--)
            if (_jobs[i] is TreatJob t && t.Patient == null) _jobs.RemoveAt(i);
    }

    // 이 폰 앞으로 잡아 둔 일감. 없으면 null.
    JobBase FindReserved(UnitBrain unit)
    {
        if (unit == null) return null;

        foreach (var job in _jobs)
        {
            if (job.Status != JobStatus.Reserved || job.ReservedBy != unit) continue;
            if (!job.IsReady || !job.IsSiteUsable) continue;   // 아직이면 다음 틱에 다시 본다
            return job;
        }
        return null;
    }

    // 이 환자를 두고 이미 도는 치료 일감이 있는가. **같은 명령을 두 번 걸지 않으려고** 쓴다 —
    // 침대 쪽은 `Bed`가 제 일감을 하나만 들고 있어 저절로 막히지만, 침대 없는 환자는 우클릭할
    // 때마다 새로 만들어 큐에 쌓인다(환자가 먼저 나으면 `IsReady == false`인 채 영영 남는다).
    public bool HasTreatJobFor(UnitBrain patient)
    {
        if (patient == null) return false;

        foreach (var job in _jobs)
            if (job is TreatJob t && t.Patient == patient) return true;
        return false;
    }

    // **폰을 콕 집어 맡긴다.** 우클릭 명령이 쓰는 길 — 등록과 예약을 한 번에 한다.
    // 큐에 올려 두는 이유는 그 폰이 죽거나 그만두면 `ReleaseReservation`으로 남에게
    // 넘어가야 하기 때문이다(등록 없이 들려 주면 그 자리에서 사라진다).
    public void ReserveFor(JobBase job, UnitBrain unit)
    {
        if (job == null || unit == null) return;
        RegisterJob(job);
        job.Reserve(unit);
    }

    // ── 작업 완료 처리 ───────────────────────────────────────────────
    public void CompleteJob(JobBase job)
    {
        _jobs.Remove(job);
        OnJobCompleted?.Invoke(job);
    }
}
