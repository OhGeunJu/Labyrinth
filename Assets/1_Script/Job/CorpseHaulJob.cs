using UnityEngine;

// **시체를 저장 구역으로 업어 옮긴다**(2026-10-06, `StockZones`).
//
// 두 단계다 — 업으러 가서(`Fetch`), 쥐어 둔 구역 칸에 내려놓는다(`Carry`). 도축대로 업어 가는 일(`ButcherJob`)과
// 같은 모양이고, 업고 내리는 것은 같은 문(`PawnCarry`)을 지난다.
//
// **자리는 일감을 만들 때 이미 쥐었다**(`CorpseHauling`이 `StockZones.TryClaimFree`로) — 빈 칸이 없으면 일감이 안
// 난다. 가는 사이에 그 칸이 못 쓰게 됐으면(메워졌다 · 영역이 줄었다 · 지워졌다) 다른 빈 칸을 다시 쥐고, 그것도
// 없으면 **선 자리에 내려놓는다** — 업은 채 서 있으면 그 폰이 다음 일감을 못 집는다.
public class CorpseHaulJob : JobBase
{
    readonly GameObject _target;
    Vector2Int _dest;

    enum Phase { Fetch, Carry }
    Phase _phase = Phase.Fetch;

    CorpseHaulJob(GameObject target)
    {
        _target = target;
        SetSite(target != null ? target.transform : null);   // 먼저 가는 곳은 시체
        Priority = 4;   // 바닥 물건 운반(`GroundHaulJob`)과 같은 자리
    }

    // **빈 칸을 쥘 수 있을 때만 일감이 난다** — 쥐는 것은 일감이 있어야 하고(누가 쥐었나를 적는다) 목적지는 쥐어 봐야
    // 안다. 그래서 만드는 문을 하나로 묶는다.
    public static CorpseHaulJob TryCreate(Corpse corpse)
    {
        if (corpse == null) return null;
        var job = new CorpseHaulJob(corpse.gameObject);
        if (!StockZones.TryClaimFree(corpse.transform.position, job, out job._dest)) return null;
        return job;
    }

    public override JobCategory Category => JobCategory.Hauling;

    // 집어 들고 내려놓는 일이라 손만 닿으면 된다(`HaulJob`과 같은 규약).
    public override float Reach => HandReach;

    public GameObject Target => _target;

    UnitBrain Body => _target != null ? _target.GetComponent<UnitBrain>() : null;

    public override bool IsReady => _target != null;

    // **어깨는 하나다** — 이미 남을 업은 폰에게는 안 준다. 가서야 못 업는 것이 드러나면 일감이 죽고 다시 걸리고 같은
    // 폰이 또 집어 가는 고리가 된다(`ButcherJob.CanAssignTo`가 먼저 겪었다).
    public override bool CanAssignTo(UnitBrain unit)
    {
        var body = Body;
        if (body == null || unit == null || unit == body) return false;
        var mine = body.Blackboard != null && body.Blackboard.CarriedBy == unit;
        return mine || PawnCarry.CanCarry(unit, body);
    }

    protected override void OnCancelled() => StockZones.Release(this);

    protected override void Execute(UnitBrain unit)
    {
        var body = Body;
        if (body == null) { Finish(); return; }

        if (_phase == Phase.Fetch)
        {
            bool mine = body.Blackboard != null && body.Blackboard.CarriedBy == unit;

            // **이 일감이 시킨 업기다** — `keepCurrentJob`을 안 넘기면 `TryPickUp`이 이 일감 자신을 취소한다.
            if (!mine && !PawnCarry.TryPickUp(unit, body, keepCurrentJob: true)) { Finish(); return; }

            _phase    = Phase.Carry;
            TileCoord = _dest;
            return;
        }

        // ── 닿았다 — 내려놓는다 ──────────────────────────────────────────────
        var bb = body.Blackboard;
        if (bb == null || bb.CarriedBy != unit) { Finish(); return; }   // 그새 남이 받아 갔다

        var map = MapManager.Instance;
        if (!StockZones.Contains(_dest))
        {
            // 쥔 칸이 못 쓰게 됐다 — 다른 빈 칸으로 돌린다. 이번 걸음은 거기까지 이어서 간다.
            StockZones.Release(this);
            if (StockZones.TryClaimFree(unit.transform.position, this, out var again))
            {
                _dest = again;
                TileCoord = again;
                return;
            }
            PawnCarry.PutDown(unit);
            Finish();
            return;
        }

        PawnCarry.PutDown(unit, map != null ? map.TileToGround(_dest) : (Vector3?)null);
        Finish();
    }

    void Finish()
    {
        StockZones.Release(this);
        Status = JobStatus.Done;
    }
}
