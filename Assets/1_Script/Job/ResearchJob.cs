using UnityEngine;

public class ResearchJob : JobBase
{
    readonly ResearchTable _table;
    readonly ResearchData  _data;
    float _endTime;
    float _startTime;
    float _carriedSeconds;   // 이번에 붙기 전까지 이미 해 둔 몫
    bool  _consumed;
    bool  _timerStarted;
    bool  _requeueOnCancel = true;

    // **취소에는 두 종류가 있다.**
    //   ① 폰이 밥 먹으러 가거나 죽어서 끊긴 것 — 다시 큐에 올려야 알아서 이어서 한다.
    //   ② 플레이어가 다른 연구로 갈아탄 것 — 되돌리면 갈아타지지가 않는다.
    // 진행도는 어느 쪽이든 저장되므로, ②로 접어 둔 연구도 나중에 다시 고르면 이어서 한다.
    public void SuppressRequeue() => _requeueOnCancel = false;

    public override JobCategory Category => JobCategory.Research;

    // 이어서 하는 연구인가 — 재료는 처음 착수할 때 한 번만 들어간다.
    bool Resuming => _data != null && ResearchManager.Instance != null
                  && ResearchManager.Instance.GetProgressSeconds(_data) > 0f;

    // **재료가 다 있어야 배정한다** — 없으면 큐에서 기다린다(예약 유지), 입고되면 자동 재개.
    //
    // 전에는 이 게이트가 없어서 재료가 모자란 채로 배정됐고, `OnStart`가 `Status = Done`으로
    // 끝내 버려 **눌러 둔 연구가 조용히 풀렸다.** 게다가 환불이 바닥으로 가던 시절엔
    // "환불 → 창고 집계에서 빠짐 → 다음 배정에서 재료 부족 → 예약 소멸"이 한 고리로 돌았다.
    // `CraftJob`은 처음부터 이 게이트를 갖고 있었다 — 연구만 빠져 있었다.
    public override bool IsReady =>
        _table != null && _data != null && _table.CanResearch(_data);

    // 재료를 **이 폰이** 댈 수 있는가(창고 + 자기 배낭). 남의 배낭 것으로는 못 한다.
    public override bool CanAssignTo(UnitBrain unit)
    {
        if (_data == null || Resuming) return true;
        foreach (var ing in _data.Ingredients)
            if (MaterialPool.Available(ing.Item, unit) < ing.Amount) return false;
        return true;
    }

    // ── 진행 상황 (UI 게이지용) ────────────────────────────────────────────
    public ResearchData Data => _data;

    // 폰이 연구대에 붙어 타이머가 도는 동안에만 true. 걸어오는 중엔 false.
    public bool IsRunning => _timerStarted && Status == JobStatus.InProgress;

    // 이번에 붙어서 진행한 시간(초). 저장할 때와 게이지를 그릴 때 같은 값을 쓴다.
    float ElapsedThisSession => _timerStarted ? Mathf.Max(0f, Time.time - _startTime) : 0f;

    // 0~1. **이전에 해 둔 몫(`_carriedSeconds`)을 포함한다** — 이어서 하는 것이므로
    // 게이지가 0부터 다시 차오르면 안 된다.
    public float Progress
    {
        get
        {
            if (_data == null) return 0f;
            float total = Mathf.Max(0.01f, _data.ResearchTime);
            return Mathf.Clamp01((_carriedSeconds + ElapsedThisSession) / total);
        }
    }

    public ResearchJob(ResearchTable table, ResearchData data)
    {
        _table   = table;
        _data    = data;
        Priority = 1;
        SetSite(table);   // 목표 칸(WorkSpot)도 여기서 정해진다
    }

    protected override void OnStart()
    {
        if (_data == null || _table == null) { Status = JobStatus.Done; return; }

        // **이미 진행 중이던 연구는 재료를 다시 받지 않는다.** 재료는 처음 시작할 때 한 번만 들어간다 —
        // 아니면 중단·재개를 반복하는 것만으로 재료가 계속 빨려 들어간다.
        if (!Resuming)
        {
            // 재료는 **창고 + 이 폰의 배낭**에서 센다(`MaterialPool`).
            foreach (var ing in _data.Ingredients)
            {
                if (MaterialPool.Available(ing.Item, ReservedBy) < ing.Amount)
                {
                    Debug.Log($"[ResearchJob] 재료 부족 — {ing.Item.ItemName} ×{ing.Amount} 필요");
                    // **예약은 잃지 않는다.** 그냥 끝내면 눌러 둔 연구가 조용히 사라진다 —
                    // 큐에 되돌리면 `IsReady`가 재료가 들어올 때까지 배정을 미뤄 준다.
                    _table?.QueueResearch(_data);
                    Status = JobStatus.Done;
                    return;
                }
            }

            foreach (var ing in _data.Ingredients)
                MaterialPool.Consume(ing.Item, ing.Amount, ReservedBy);   // 배낭 먼저
            _consumed = true;
        }

        Debug.Log($"[ResearchJob] 연구 예약 — {_data.ResearchName} ({_data.ResearchTime}초)");
        // 타이머는 여기서 재지 않는다 — 폰이 연구대까지 걸어오는 시간이
        // 연구 시간을 깎아버린다(CraftJob이 같은 이유로 첫 Execute에서 잰다).
    }

    // 완성 전 취소(사망·이동 명령 등) — 차감한 재료를 연구대에 드롭 환불하고 주문을 큐에 되돌린다.
    // 환불이 없으면 취소할 때마다 재료가 증발한다(CraftJob과 같은 정책).
    protected override void OnCancelled()
    {
        _table?.ClearActiveJob(this);   // 게이지가 완료되지 않은 채 남아 있지 않게
        if (_data == null) return;

        // **한 만큼을 저장한다** — 이게 이 클래스의 핵심 변경이다.
        float done = _carriedSeconds + ElapsedThisSession;
        ResearchManager.Instance?.SetProgressSeconds(_data, done);

        // 진행이 있었으면 재료는 **돌려주지 않는다.** 이미 연구에 들어간 것이고,
        // 돌려주면서 진행도까지 남기면 중단·재개를 반복해 재료를 복사할 수 있다.
        // 폰이 도착도 못 한 채 취소됐을 때만 환불한다.
        if (_consumed && ElapsedThisSession <= 0f)
        {
            _consumed = false;
            Vector3 pos = _table != null ? _table.transform.position : Vector3.zero;
            foreach (var ing in _data.Ingredients)
                MaterialPool.Refund(ing.Item, ing.Amount, pos + Vector3.up * 0.3f);
        }

        if (_requeueOnCancel) _table?.QueueResearch(_data);
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_data == null || _table == null) { Status = JobStatus.Done; return; }

        // 폰이 연구대에 도착한 첫 틱에서 타이머 시작 + 연구대에 "지금 이걸 하는 중"을 알린다.
        if (!_timerStarted)
        {
            _timerStarted   = true;
            _startTime      = Time.time;
            _carriedSeconds = ResearchManager.Instance != null
                            ? ResearchManager.Instance.GetProgressSeconds(_data) : 0f;

            // **남은 만큼만** 잰다. 매 틱 누적하지 않는 이유는 BT가 `tickInterval`마다 돌아
            // 프레임 델타를 더하면 시간이 6배 이상 느려지기 때문이다(`docs` 알려진 이슈).
            float remaining = Mathf.Max(0.01f, _data.ResearchTime - _carriedSeconds);
            _endTime = Time.time + remaining;

            _table.SetActiveJob(this);
            string resumed = _carriedSeconds > 0f ? $" (이어서 {Progress * 100f:F0}%)" : "";
            Debug.Log($"[ResearchJob] 연구 시작 — {_data.ResearchName} ({remaining:F0}초 남음){resumed}");
        }
        if (Time.time < _endTime) return;

        if (_data.SkillGrowthAmount > 0f)
        {
            float xp = _data.SkillGrowthAmount * CombatXp.Sol(unit.Stats.Research);
            unit.Stats.GrowStat(StatName.Research, xp);
        }

        ResearchManager.Instance?.ClearProgress(_data);   // 완료됐으니 부분 진행도는 지운다
        ResearchManager.Instance?.Complete(_data);
        Debug.Log($"[ResearchJob] 연구 완료 — {_data.ResearchName}");
        _table.ClearActiveJob(this);
        Status = JobStatus.Done;
    }
}
