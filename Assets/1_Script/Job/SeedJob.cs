using UnityEngine;

public class SeedJob : JobBase
{
    public override JobCategory Category => JobCategory.Farming;

    enum Phase { GoToStorage, GoToPlot }

    FarmPlot _plot;
    SeedData _seed;
    Phase    _phase = Phase.GoToStorage;

    public SeedJob(FarmPlot plot, SeedData seed)
    {
        _plot    = plot;
        _seed    = seed;
        Priority = 3;
        TileCoord = plot.TileCoord;
        SetSite(plot);
    }

    protected override void OnStart()
    {
        if (_plot == null || _seed == null)
        {
            Status = JobStatus.Done;
            _plot?.OnSeedJobCancelled();
            return;
        }

        if (Storage.GetTotalByItem(_seed) <= 0)
        {
            Debug.Log($"[SeedJob] 씨앗 없음 — {_seed.ItemName}");
            Status = JobStatus.Done;
            _plot.OnSeedJobCancelled();
            return;
        }

        var storage = Storage.FindNearest(_plot.transform.position, _seed);
        if (storage != null)
            TileCoord = MapManager.Instance.WorldToTile(storage.transform.position);
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_plot == null || _seed == null) { Status = JobStatus.Done; return; }

        if (_phase == Phase.GoToStorage)
        {
            if (!Storage.ConsumeByItem(_seed, 1))
            {
                Debug.Log($"[SeedJob] 씨앗 소비 실패 — {_seed.ItemName}");
                _plot.OnSeedJobCancelled();
                Status = JobStatus.Done;
                return;
            }

            _phase    = Phase.GoToPlot;
            TileCoord = _plot.TileCoord;
        }
        else
        {
            _plot.OnSeeded();
            Status = JobStatus.Done;
        }
    }

    protected override void OnCancelled() => _plot?.OnSeedJobCancelled();
}
