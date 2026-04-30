using UnityEngine;

public class ResearchJob : JobBase
{
    readonly ResearchTable _table;
    readonly ResearchData  _data;
    float _endTime;

    public override JobCategory Category => JobCategory.Research;

    public ResearchJob(ResearchTable table, ResearchData data)
    {
        _table   = table;
        _data    = data;
        Priority = 1;

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

        Debug.Log($"[ResearchJob] 연구 시작 — {_data.ResearchName} ({_data.ResearchTime}초)");
        _endTime = Time.time + _data.ResearchTime;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_data == null || _table == null) { Status = JobStatus.Done; return; }
        if (Time.time < _endTime) return;

        // 연구 스킬 성장
        unit.Stats.Research = Mathf.Min(100f, unit.Stats.Research + 1f);

        ResearchManager.Instance?.Complete(_data);
        Debug.Log($"[ResearchJob] 연구 완료 — {_data.ResearchName}");
        Status = JobStatus.Done;
    }
}
