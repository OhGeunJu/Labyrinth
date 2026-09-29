using UnityEngine;

// **거둔 것은 그 자리에 떨어진다**(2026-09-05, 사용자: *"인벤토리에 바로 가지 말고 이것도
// 그 장소에서 템이 드랍되게 할까? 그게 직관적이잖아"*).
//
// 그전에는 거두는 폰이 **배낭에 담고 창고까지 지고 갔다**(2단계 일감). 그래서 같은 물건이
// 두 길로 창고에 들어갔다 — 캔 돌은 바닥에 떨어져 운반이 나르고, 거둔 밀은 농부가 직접
// 날랐다. **화면에서 그 둘은 구별되지 않는데** 규칙만 달랐다.
//
// 이제 **거두는 일은 거기서 끝난다.** 떨어진 것은 운반이 나른다(`HaulAllowed`를 켜 두므로
// 사람이 따로 안 찍어도 된다 — `MineJob`과 같은 자리). 딸려 오는 것 셋:
//
//  · **일감이 한 단계다** — 창고를 찾고, 못 찾으면 소지하고, 도착해서 잔량을 되돌리는
//    사슬이 통째로 없어졌다(그 사슬은 `Storage.Deliver`가 이미 운반 쪽에서 돈다).
//  · **배낭이 찼는지를 안 본다** — 짐이 가득한 농부도 거둘 수 있다.
//  · **거두는 사람과 나르는 사람이 갈린다** — 농부는 밭에 남고, 짐은 짐꾼이 옮긴다.
public class FarmHarvestJob : JobBase
{
    public override JobCategory Category => JobCategory.Farming;
    public override Component Site => _plot;   // 배치 칸이 거른다(`WorkPosts`)
    public override WorkAnim    Anim     => WorkAnim.Harvesting;   // 밭에서 거둔다 — 채집과 다른 자리다

    // 밭에서 거두기만 한다 — **손만 닿으면 된다.**
    public override float Reach => HandReach;

    FarmPlot _plot;
    SeedData _seed;

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

        // **작물이 자란 자리에 떨군다** — 폰이 선 자리가 아니다. 밭 여러 칸을 거두면
        // 어느 밭에서 나온 것인지가 바닥에 그대로 남아야 한다.
        var at = _plot.transform.position + Vector3.up * 0.3f;

        // **원예가 수확량을 정한다**(2026-08-26). 그전까지 이 스탯은 이름과 설명
        // (*"씨를 뿌리고 거둔다. 수확량에도 걸린다"*)만 있고 **코드 어디에서도 안 읽혔다**.
        int amount = Farming.HarvestAmount(_seed.HarvestAmount, unit);
        if (amount > 0)
            DroppedItem.Spawn(_seed.HarvestItem, amount, at)?.SetHaulAllowed(true);

        // **씨앗도 같이 나온다**(눈금은 `Farming.SeedReturn`이 든다). 심을 때 한 알을 쓰므로
        // 밭이 저절로 이어지고 천천히 넓어진다.
        int seeds = Farming.SeedReturn(unit);
        if (seeds > 0)
            DroppedItem.Spawn(_seed, seeds, at)?.SetHaulAllowed(true);

        _plot.OnHarvested();
        Farming.Grow(unit, Farming.HarvestXp);

        Status = JobStatus.Done;
    }

    protected override void OnCancelled() => _plot?.OnHarvestJobCancelled();
}
