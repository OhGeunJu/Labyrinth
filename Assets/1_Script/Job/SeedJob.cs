using UnityEngine;

public class SeedJob : JobBase
{
    // 심는 것은 즉시라 속도에 걸 자리가 없다 — **자라기만 한다**(거두는 쪽이 값을 낸다).

    public override JobCategory Category => JobCategory.Farming;

    public override Component Site => _plot;   // 배치 칸이 거른다(`WorkPosts`)
    public override WorkAnim    Anim     => _phase == Phase.GoToPlot ? WorkAnim.Seeding : WorkAnim.None;

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
            // **여기서 안 쓴다 — 쓰는 자리는 심는 순간 하나다**(2026-09-02 수정).
            //
            // 전에는 이 갈래에서만 씨앗을 썼는데, `OnStart`가 **배낭에 씨앗이 있으면 이 갈래를
            // 통째로 건너뛰었다.** 그래서 폰이 씨앗 하나를 지니고 있으면 **심어도 줄지 않았다** —
            // 씨앗 한 개로 영원히 농사가 됐다(사용자가 짚었다).
            //
            // 걸어가는 것과 쓰는 것은 다른 일이다. 창고에 들르는 것은 *가지러 가는 그림*이고,
            // **줄어드는 것은 밭에 심을 때**다.
            _phase = Phase.GoToPlot;

            // **밭 타일이 아니라 작업 자리로 간다.** 밭 자기 타일은 설 수 없을 수 있어
            // 도착 판정에 안 걸리고, 그러면 BT의 '못 감' 타이머가 일감을 놓았다가
            // 다시 등록하기를 반복한다 — 겉보기엔 "농사를 안 짓는다"가 된다.
            TileCoord = WorkSpot.TileFor(_plot);
        }
        else
        {
            // **심는 순간 하나를 쓴다.** 배낭이 먼저고 그다음 창고다(`MaterialPool.Consume`).
            // 여기까지 오는 사이 남이 가져갔을 수 있으므로 실패를 정상 경로로 다룬다 —
            // 밭은 비운 채로 두고, 씨앗이 다시 생기면 다음 검사에서 일감이 새로 난다.
            if (!MaterialPool.Consume(_seed, 1, unit))
            {
                Debug.Log($"[SeedJob] 씨앗 없음 — {_seed.ItemName}");
                _plot.OnSeedJobCancelled();
                Status = JobStatus.Done;
                return;
            }

            _plot.OnSeeded();
            Farming.Grow(unit, Farming.SeedXp);
            Status = JobStatus.Done;
        }
    }

    protected override void OnCancelled() => _plot?.OnSeedJobCancelled();
}
