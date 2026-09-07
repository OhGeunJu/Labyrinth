using UnityEngine;

// 채집 — **가서, 따고, 끝난다.** 게이지도 배정도 없다(`Forageable`의 표를 볼 것).
//
// **큐 작업이다.** 채굴·벌목처럼 직접 배정으로 두면 *"이 폰을 저 버섯에 붙인다"*가 되는데,
// 한 번이면 끝나는 일에 배정은 값이 크기만 하다(붙이고 떼는 조작이 딸 때마다 든다).
// 큐에 두면 **지나가다 집는다** — 그게 사용자가 말한 *"열매채집 같은 느낌"*이다.
//
// **`HandReach`다** — 거기 서 있을 이유가 없고 손만 닿으면 된다(운반·수확과 같은 자리).
public class ForageJob : JobBase
{
    public override JobCategory Category => JobCategory.Foraging;
    public override float       Reach    => HandReach;

    // **없어졌으면 일감이 아니다** — 다른 폰이 먼저 땄거나 청크가 내려갔다.
    public override bool IsReady => _target != null;

    readonly Forageable _target;

    public ForageJob(Forageable target)
    {
        _target = target;

        // **자리는 그 자리다** — `WorkSpot.TileFor`를 안 쓴다. 버섯은 칸을 안 막으므로
        // (콜라이더가 트리거다) 폰이 그 칸에 그대로 설 수 있고, 2m 안이면 손이 닿는다.
        Priority = 3;
        if (target != null && MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(target.transform.position);
    }

    protected override void OnStart()
    {
        if (_target == null) Status = JobStatus.Done;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_target == null) { Status = JobStatus.Done; return; }

        _target.Harvest();

        // **원예를 태운다** — 밭을 거두는 것과 같은 일이다(`Farming.HarvestXp`).
        // 여기만 다른 스탯을 태우면 *"버섯만 따는 폰"*이 밭에서 쓸모없어진다.
        Farming.Grow(unit, Farming.HarvestXp);

        Status = JobStatus.Done;
    }

    // **놓으면 다시 걸 수 있어야 한다** — 안 되돌리면 그 버섯은 영영 일감이 안 난다.
    protected override void OnCancelled() => _target?.OnJobCancelled();
}
