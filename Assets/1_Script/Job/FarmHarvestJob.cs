using UnityEngine;

public class FarmHarvestJob : JobBase
{
    public override JobCategory Category => JobCategory.Farming;

    // 거두고 창고에 부리는 일이라 **손만 닿으면 된다**(운반과 같은 축).
    public override float Reach => HandReach;

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
            TileCoord = WorkSpot.TileFor(_storage);   // 창고 자기 칸이 아니라 폰이 서는 자리
        }
        else
        {
            // 잔량 보존 사슬은 `Storage.Deliver` 한 곳이 돈다(운반과 같은 것).
            //
            // 전에는 여기서 `FindNearest`에 **수량을 안 넘겨** 기본값(1개)으로 물었다 —
            // "한 개는 들어간다"만 확인하고 수확물을 통째로 지고 가는 셈이라, 도착해서 못 넣고
            // 줍고 떨구기를 반복할 수 있었다(CLAUDE.md가 콕 집어 막아 둔 함정).
            int removed = unit.Inventory.Remove(_seed.HarvestItem, _harvested);
            Storage.Deliver(_storage, _seed.HarvestItem, removed, ItemQuality.Common,
                            unit, unit.transform.position);

            Status = JobStatus.Done;
        }
    }

    protected override void OnCancelled() => _plot?.OnHarvestJobCancelled();
}
