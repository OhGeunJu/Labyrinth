using UnityEngine;

public class ResearchJob : JobBase
{
    readonly ResearchTable _table;
    readonly ResearchData  _data;
    float _endTime;
    bool  _consumed;

    public override JobCategory Category => JobCategory.Research;

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

        Debug.Log($"[ResearchJob] 연구 시작 — {_data.ResearchName} ({_data.ResearchTime}초)");
        _endTime = Time.time + _data.ResearchTime;
    }

    // 완성 전 취소(사망·이동 명령 등) — 차감한 재료를 연구대에 드롭 환불하고 주문을 큐에 되돌린다.
    // 환불이 없으면 취소할 때마다 재료가 증발한다(CraftJob과 같은 정책).
    protected override void OnCancelled()
    {
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
        if (Time.time < _endTime) return;

        if (_data.SkillGrowthAmount > 0f)
        {
            float xp = _data.SkillGrowthAmount * CombatXp.Sol(unit.Stats.Research);
            unit.Stats.GrowStat(StatName.Research, xp);
        }

        ResearchManager.Instance?.Complete(_data);
        Debug.Log($"[ResearchJob] 연구 완료 — {_data.ResearchName}");
        Status = JobStatus.Done;
    }
}
