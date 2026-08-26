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
            // **원예가 수확량을 정한다**(2026-08-26). 그전까지 이 스탯은 이름과 설명
            // (*"씨를 뿌리고 거둔다. 수확량에도 걸린다"*)만 있고 **코드 어디에서도 안 읽혔다** —
            // 농지 일감이 스탯을 아예 안 봤다. 화면이 약속한 것을 그대로 코드에 옮긴다.
            //
            // **개수가 정수라 올림이 아니라 반올림한다** — 내림이면 원예 0~64가 전부 같은 값이
            // 되어(2개 작물이면 3개가 되려면 65가 필요하다) 낮은 구간이 통째로 죽는다.
            int amount = Farming.HarvestAmount(_seed.HarvestAmount, unit);

            _harvested       = unit.Inventory.TryAdd(_seed.HarvestItem, amount);
            int leftover     = amount - _harvested;
            if (leftover > 0)
                DroppedItem.Spawn(_seed.HarvestItem, leftover, _plot.transform.position + Vector3.up * 0.3f);

            _plot.OnHarvested();
            Farming.Grow(unit, Farming.HarvestXp);

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
