using UnityEngine;

public class WaterJob : JobBase
{
    public override JobCategory Category => JobCategory.Farming;

    enum Phase { GoToWell, GoToPlot }

    FarmPlot    _plot;
    WaterSource _well;
    Phase       _phase;
    bool        _phaseInit;

    public WaterJob(FarmPlot plot)
    {
        _plot     = plot;
        Priority  = 4;
        TileCoord = plot.TileCoord;
        SetSite(plot);
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_plot == null) { Status = JobStatus.Done; return; }

        // 첫 Execute에서 물 공급 방식 결정 (unit 정보 필요)
        if (!_phaseInit)
        {
            _phaseInit = true;
            bool canSelfWater = unit.Data is MinionData m && m.CanProvideWater;

            if (canSelfWater)
            {
                _phase    = Phase.GoToPlot;
                TileCoord = _plot.TileCoord;
                // 이미 밭에 도착해 있으므로 바로 물 주기
            }
            else
            {
                _well = WaterSource.FindNearest(_plot.transform.position);
                if (_well == null)
                {
                    Debug.Log("[WaterJob] 우물 없음 — 우물을 건설하거나 물 공급 하수인을 배치하세요");
                    _plot.OnWaterJobCancelled();
                    Status = JobStatus.Done;
                    return;
                }
                _phase    = Phase.GoToWell;
                TileCoord = MapManager.Instance.WorldToTile(_well.transform.position);
                return; // 우물로 먼저 이동
            }
        }

        if (_phase == Phase.GoToWell)
        {
            // 우물 도착 → 밭으로 이동
            _phase    = Phase.GoToPlot;
            TileCoord = _plot.TileCoord;
            return;
        }

        // 밭 도착 → 물 주기
        _plot.OnWatered();
        Status = JobStatus.Done;
    }

    protected override void OnCancelled() => _plot?.OnWaterJobCancelled();
}
