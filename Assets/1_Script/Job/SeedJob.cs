using UnityEngine;

public class SeedJob : JobBase
{
    // 심는 것은 즉시라 속도에 걸 자리가 없다 — **자라기만 한다**(거두는 쪽이 값을 낸다).

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

    // 씨앗을 **이 폰이** 댈 수 있어야 배정한다(배낭 또는 창고).
    // 남이 들고 있는 씨앗으로는 이 폰이 심을 수 없다.
    public override bool CanAssignTo(UnitBrain unit)
        => _seed == null || MaterialPool.Available(_seed, unit) > 0;

    protected override void OnStart()
    {
        if (_plot == null || _seed == null)
        {
            Status = JobStatus.Done;
            _plot?.OnSeedJobCancelled();
            return;
        }

        if (MaterialPool.Available(_seed, ReservedBy) <= 0)
        {
            Debug.Log($"[SeedJob] 씨앗 없음 — {_seed.ItemName}");
            Status = JobStatus.Done;
            _plot.OnSeedJobCancelled();
            return;
        }

        // **이미 들고 있으면 창고에 안 들른다.** 배낭에 씨앗이 있는데 창고까지 다녀오면
        // 사 오자마자 심으려던 동작이 먼 길이 된다.
        if (ReservedBy != null && ReservedBy.Inventory != null &&
            ReservedBy.Inventory.GetTotalQuantity(_seed) > 0)
        {
            _phase    = Phase.GoToPlot;
            TileCoord = WorkSpot.TileFor(_plot);
            return;
        }

        // **가지러 가는 것**이므로 "받아 줄 곳"이 아니라 "들어 있는 곳"을 찾는다.
        // `FindNearest`는 이제 남은 자리까지 보므로(입고용), 여기 쓰면 씨앗이 가득 든 창고가
        // 오히려 탈락한다 — 정확히 가야 할 곳이 걸러지는 셈이다.
        var origin = ReservedBy != null ? ReservedBy.transform.position : _plot.transform.position;
        var storage = Storage.FindStorageWithItem(_seed, origin);
        if (storage != null)
            TileCoord = WorkSpot.TileFor(storage);   // 창고 자기 칸이 아니라 폰이 서는 자리
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_plot == null || _seed == null) { Status = JobStatus.Done; return; }

        if (_phase == Phase.GoToStorage)
        {
            if (!MaterialPool.Consume(_seed, 1, unit))
            {
                Debug.Log($"[SeedJob] 씨앗 소비 실패 — {_seed.ItemName}");
                _plot.OnSeedJobCancelled();
                Status = JobStatus.Done;
                return;
            }

            _phase = Phase.GoToPlot;

            // **밭 타일이 아니라 작업 자리로 간다.** 밭 자기 타일은 설 수 없을 수 있어
            // 도착 판정에 안 걸리고, 그러면 BT의 '못 감' 타이머가 일감을 놓았다가
            // 다시 등록하기를 반복한다 — 겉보기엔 "농사를 안 짓는다"가 된다.
            TileCoord = WorkSpot.TileFor(_plot);
        }
        else
        {
            _plot.OnSeeded();
            Farming.Grow(unit, Farming.SeedXp);
            Status = JobStatus.Done;
        }
    }

    protected override void OnCancelled() => _plot?.OnSeedJobCancelled();
}
