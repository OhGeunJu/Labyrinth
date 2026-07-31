using UnityEngine;

public class ResearchJob : JobBase
{
    readonly ResearchTable _table;
    readonly ResearchData  _data;
    float _endTime;
    bool  _consumed;
    bool  _timerStarted;

    public override JobCategory Category => JobCategory.Research;

    // ── 진행 상황 (UI 게이지용) ────────────────────────────────────────────
    public ResearchData Data => _data;

    // 폰이 연구대에 붙어 타이머가 도는 동안에만 true. 걸어오는 중엔 false.
    public bool IsRunning => _timerStarted && Status == JobStatus.InProgress;

    public float Progress
    {
        get
        {
            if (!_timerStarted || _data == null) return 0f;
            float total = Mathf.Max(0.01f, _data.ResearchTime);
            return Mathf.Clamp01(1f - (_endTime - Time.time) / total);
        }
    }

    public ResearchJob(ResearchTable table, ResearchData data)
    {
        _table   = table;
        _data    = data;
        Priority = 1;
        SetSite(table);

        if (MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(table.transform.position);
    }

    protected override void OnStart()
    {
        if (_data == null || _table == null) { Status = JobStatus.Done; return; }

        // 재료 확인
        foreach (var ing in _data.Ingredients)
        {
            if (Storage.GetTotalByItem(ing.Item) < ing.Amount)
            {
                Debug.Log($"[ResearchJob] 재료 부족 — {ing.Item.ItemName} ×{ing.Amount} 필요");
                Status = JobStatus.Done;
                return;
            }
        }

        // 재료 즉시 소비
        foreach (var ing in _data.Ingredients)
            Storage.ConsumeByItem(ing.Item, ing.Amount);
        _consumed = true;

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

        if (_consumed)
        {
            _consumed = false;
            Vector3 pos = _table != null ? _table.transform.position : Vector3.zero;
            foreach (var ing in _data.Ingredients)
                DroppedItem.Spawn(ing.Item, ing.Amount, pos + Vector3.up * 0.3f);
        }

        _table?.QueueResearch(_data);
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_data == null || _table == null) { Status = JobStatus.Done; return; }

        // 폰이 연구대에 도착한 첫 틱에서 타이머 시작 + 연구대에 "지금 이걸 하는 중"을 알린다.
        if (!_timerStarted)
        {
            _timerStarted = true;
            _endTime      = Time.time + _data.ResearchTime;
            _table.SetActiveJob(this);
            Debug.Log($"[ResearchJob] 연구 시작 — {_data.ResearchName} ({_data.ResearchTime}초)");
        }
        if (Time.time < _endTime) return;

        if (_data.SkillGrowthAmount > 0f)
        {
            float xp = _data.SkillGrowthAmount * CombatXp.Sol(unit.Stats.Research);
            unit.Stats.GrowStat(StatName.Research, xp);
        }

        ResearchManager.Instance?.Complete(_data);
        Debug.Log($"[ResearchJob] 연구 완료 — {_data.ResearchName}");
        _table.ClearActiveJob(this);
        Status = JobStatus.Done;
    }
}
