using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ResearchTable : MonoBehaviour, IWorldClickable, IUpgradableBench
{
    [Tooltip("비우면 DataManager에 등록된 연구 **전부**를 쓴다. " +
             "특정 연구대만 일부를 다루게 하고 싶을 때만 채운다.")]
    [SerializeField] List<ResearchData> availableResearch = new();

    // **등급 — 작업대와 같은 축이다**(`BenchTier`). 개량 연구대는 연구가 1.5배 빠르다.
    // 0으로 남은 프리팹은 `Tier`가 1로 받으므로 **이미 놓인 연구대는 안 느려진다.**
    [Tooltip("연구대 등급. 1 = 기본, 2 = 개량(연구가 빠르다). 비워 두면 1이다.")]
    [SerializeField] int tier = BenchTier.Min;

    public int   Tier  => BenchTier.Clamp(tier);
    public float Speed => BenchTier.SpeedOf(Tier);

    // **올리는 문은 `BenchUpgrade` 하나다.**
    public void SetTier(int value)
    {
        tier = BenchTier.Clamp(value);
        BenchVisual.Ensure(this)?.Apply();
    }

    public string BenchName => BenchTier.BaseNameOf(this);

    // **비어 있으면 레지스트리에서 끌어온다.**
    // 인스턴스마다 손으로 채우는 방식이면 런타임에 지은 연구대는 목록이 텅 빈 채로 나온다
    // — 씬에 미리 놓아둔 것에만 데이터가 들어가는 구조가 되기 때문이다.
    // `CraftingTable`이 `DataManager.GetRecipesBySkill`로 끌어오는 것과 같은 규칙으로 맞췄다.
    // 목록을 채우면 그게 이긴다(큐레이션용) — "데이터가 없으면 기본 동작"이라는 이 프로젝트의 관례.
    // **이 목록은 저작된 그래프다 — 여기서 거르면 안 된다**(2026-09-17에 되돌렸다).
    //
    // 등급 필터를 처음엔 여기 걸었는데, 그러면 **씬에 구워 둔 노드 마흔넷과 목록 열다섯이
    // 어긋나** `ResearchTableUI`가 *"구운 것이 낡았다"*고 경고하며 **매번 런타임에 그래프를
    // 다시 찍는다**(저작한 자리가 통째로 버려진다). 다시 구워도 소용이 없다 — 구울 때는
    // 마흔넷, 열 때는 열다섯이라 **영원히 안 맞는다.**
    //
    // *"거르는 자리는 목록 하나"*라고 적었던 판단이 틀렸다. 이 목록이 답하는 물음은
    // **"이 그래프에 무엇이 있나"**이지 *"지금 무엇을 할 수 있나"*가 아니다. 등급은
    // **`CanResearch`(할 수 있나)와 화면(보이나)**이 묻는다.
    public IReadOnlyList<ResearchData> AllResearch
    {
        get
        {
            if (availableResearch.Count > 0) return availableResearch;

            _fallback.Clear();
            foreach (var kv in DataManager.AllResearch) _fallback.Add(kv.Value);
            return _fallback;
        }
    }

    // **이 연구대에서 다룰 수 있는 등급인가.** 화면도 이걸 보고 노드를 감춘다 —
    // 답이 한 곳에 있어야 *보이는데 못 하는* 연구가 안 생긴다.
    public bool HandlesTier(ResearchData data) =>
        data != null && data.RequiredBenchTier <= Tier;

    // 매번 새 리스트를 만들면 UI가 열릴 때마다 쓰레기가 생긴다 — 한 벌만 돌려 쓴다.
    readonly List<ResearchData> _fallback = new();

    // ── 진행 중인 연구 ────────────────────────────────────────────────────
    // 폰이 실제로 연구대에 붙어 타이머를 돌리기 시작한 작업 하나. 큐에 쌓인 예약은
    // 여기 안 잡힌다 — UI 게이지는 "지금 진행 중"만 보여야 하기 때문이다.
    ResearchJob _activeJob;

    public ResearchData ActiveResearch =>
        _activeJob != null && _activeJob.IsRunning ? _activeJob.Data : null;

    public float ActiveProgress =>
        _activeJob != null && _activeJob.IsRunning ? _activeJob.Progress : 0f;

    public void SetActiveJob(ResearchJob job) => _activeJob = job;

    // ── 이 연구대가 맡은 연구 ─────────────────────────────────────────────
    // **한 연구대는 한 번에 하나만 맡는다.** 전에는 UI가 누를 때마다 작업을 하나씩 더 쌓아서,
    // 제련술을 하는 중에 석공술을 누르면 갈아타는 게 아니라 둘 다 큐에 남았다 — 그러면
    // 무엇이 먼저 될지 플레이어가 알 수 없고, 화면에 뜬 게이지와도 어긋난다.
    readonly List<ResearchJob> _jobs = new();

    // 폰이 아직 안 왔어도 잡혀 있는 연구. UI가 [연구] 버튼과 게이지를 가르는 기준이다
    // (`ActiveResearch`는 폰이 붙어 타이머가 도는 동안만 잡히므로 그것만으로는 부족하다).
    public ResearchData PendingResearch
    {
        get
        {
            Prune();
            return _jobs.Count > 0 ? _jobs[0].Data : null;
        }
    }

    void Prune()
    {
        for (int i = _jobs.Count - 1; i >= 0; i--)
        {
            var j = _jobs[i];
            if (j == null || j.Status == JobStatus.Done || j.Status == JobStatus.Cancelled)
                _jobs.RemoveAt(i);
        }
    }

    // UI의 [연구] 버튼이 부르는 유일한 진입점. **하던 것을 접고 이걸로 갈아탄다.**
    // 접힌 연구의 진행도는 `ResearchJob.OnCancelled`가 저장하므로, 나중에 다시 고르면
    // 0부터가 아니라 하던 데서 이어서 한다.
    public void StartResearch(ResearchData data)
    {
        if (data == null) return;
        Prune();

        bool already = false;
        for (int i = _jobs.Count - 1; i >= 0; i--)
        {
            var j = _jobs[i];
            if (j.Data == data) { already = true; continue; }

            // 되돌리지 않게 막고 접는다 — 안 막으면 `OnCancelled`가 곧바로 큐에 다시 올려
            // 갈아타기가 무효가 된다.
            j.SuppressRequeue();
            JobManager.Instance?.CancelJob(j);
            _jobs.RemoveAt(i);
            Debug.Log($"[ResearchTable] 연구 전환 — {j.Data?.ResearchName} 접고 {data.ResearchName}(으)로");
        }

        // 하던 것을 다시 누른 경우다. 새로 걸면 진행이 초기화되는 게 아니라 작업만 둘이 된다.
        if (already) return;

        QueueResearch(data);
    }

    // 완료·취소 모두 여기로 온다. 다른 작업이 이미 자리를 차지했으면 건드리지 않는다.
    public void ClearActiveJob(ResearchJob job)
    {
        if (_activeJob == job) _activeJob = null;
    }

    public static event Action<ResearchTable> OnSelected;

    // SelectionManager의 단일 레이캐스트가 이 연구대를 클릭 판정 시 호출.
    public void OnWorldClick() => OnSelected?.Invoke(this);

    public void QueueResearch(ResearchData data)
    {
        if (data == null || JobManager.Instance == null) return;
        // 휴면(핵 영역 밖) 연구대는 사용 불가 — UI 경로가 막혀도 직접 호출 방어.
        if (BuildingDormancy.IsDormantAt(this)) { Debug.Log("[ResearchTable] 휴면 중 — 연구 불가"); return; }

        var job = new ResearchJob(this, data);
        _jobs.Add(job);
        JobManager.Instance.RegisterJob(job);
    }

    public bool CanResearch(ResearchData data)
    {
        if (data == null) return false;

        // **등급이 모자라면 못 한다** — 목록에서 거르는 대신 여기서 막는다(위 주석 참고).
        if (!HandlesTier(data)) return false;

        if (ResearchManager.Instance != null && !ResearchManager.Instance.CanResearch(data)) return false;

        // **이어서 하는 연구는 재료를 다시 요구하지 않는다.** 재료는 처음 착수할 때 한 번만
        // 들어가는데(`ResearchJob.OnStart`가 그렇게 한다), 여기서 또 검사하면 갈아탔다가
        // 돌아온 연구는 재료가 이미 없어서 영영 재개할 수 없다 — 진행도를 저장하는 의미가 사라진다.
        if (ResearchManager.Instance != null && ResearchManager.Instance.GetProgressSeconds(data) > 0f)
            return true;

        foreach (var ing in data.Ingredients)
            if (Storage.GetTotalByItem(ing.Resolve(), transform.position) < ing.Amount) return false;
        return true;
    }
}
