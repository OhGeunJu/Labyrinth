using UnityEngine;

public class FarmHarvestJob : JobBase
{
    public override JobCategory Category => JobCategory.Farming;

    enum Phase { GoToPlot, GoToStorage }

    FarmPlot _plot;
    SeedData _seed;
    Storage  _storage;
    int      _harvested;
    Phase    _phase = Phase.GoToPlot;

    public FarmHarvestJob(FarmPlot plot, SeedData seed)
    {
        _plot     = plot;
        _seed     = seed;
        Priority  = 5;
        TileCoord = plot.TileCoord;
        SetSite(plot);
    }

    protected override void OnStart()
    {
        if (_plot == null || _seed?.HarvestItem == null)
        {
            Status = JobStatus.Done;
            _plot?.OnHarvestJobCancelled();
        }
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_plot == null || _seed == null) { Status = JobStatus.Done; return; }

        if (_phase == Phase.GoToPlot)
        {
            // 수확 후 인벤토리에 담기
            _harvested       = unit.Inventory.TryAdd(_seed.HarvestItem, _seed.HarvestAmount);
            int leftover     = _seed.HarvestAmount - _harvested;
            if (leftover > 0)
                DroppedItem.Spawn(_seed.HarvestItem, leftover, _plot.transform.position + Vector3.up * 0.3f);

            _plot.OnHarvested();

            if (_harvested <= 0)
            {
                Status = JobStatus.Done;
                return;
            }

            _storage = Storage.FindNearest(_plot.transform.position, _seed.HarvestItem);
            if (_storage == null)
            {
                // 창고 없으면 그대로 소지
                Status = JobStatus.Done;
                return;
            }

            _phase    = Phase.GoToStorage;
            TileCoord = MapManager.Instance.WorldToTile(_storage.transform.position);
        }
        else
        {
            int removed = unit.Inventory.Remove(_seed.HarvestItem, _harvested);
            if (removed > 0)
            {
                int leftover = removed - _storage.TryAdd(_seed.HarvestItem, removed);

                // 부분 입고 시 남은 양을 다른 창고로, 그래도 남으면 땅에 드롭 → 증발 방지.
                if (leftover > 0)
                {
                    var fallback = Storage.FindNearest(unit.transform.position, _seed.HarvestItem);
                    if (fallback != null)
                        leftover -= fallback.TryAdd(_seed.HarvestItem, leftover);
                }
                if (leftover > 0)
                    DroppedItem.Spawn(_seed.HarvestItem, leftover, unit.transform.position);
            }
            Status = JobStatus.Done;
        }
    }

    protected override void OnCancelled() => _plot?.OnHarvestJobCancelled();
}
