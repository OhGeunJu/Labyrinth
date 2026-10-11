using UnityEngine;

// **우리에 가둔 짐승을 길들인다**(2026-10-01). 설득(`PersuadeJob`)과 같은 모양이다 — 우리가 일감을 내고(`PrisonCage`),
// 우리 옆에 서서 회차로 끊고, 시계는 **도착해서** 켠다. 다른 것은 분류(조련)와 자라는 스탯(조련)뿐이다.
public class TameJob : JobBase
{
    public TameJob(UnitBrain target, UnitBrain tamer = null)
    {
        _target = target;
        _tamer  = tamer;
        Priority = 4;

        if (target != null && MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(target.transform.position);
    }

    readonly UnitBrain _target;
    readonly UnitBrain _tamer;
    float _endTime;

    public UnitBrain Target => _target;

    public override JobCategory Category => JobCategory.Taming;

    // 창살 너머로 손을 뻗어 먹이를 주고 쓰다듬는다 — 손만 닿으면 된다.
    public override float Reach => HandReach;

    public override bool IsReady => Conversion.CanTame(_target);

    public override bool CanAssignTo(UnitBrain unit)
    {
        if (unit == null || unit == _target) return false;
        if (unit.Blackboard != null && unit.Blackboard.IsKnockedOut) return false;
        if (unit.IsTamedBeast) return false;          // 짐승이 짐승을 길들이지는 않는다
        return _tamer == null || unit == _tamer;
    }

    protected override void OnStart() => _endTime = 0f;

    protected override void Execute(UnitBrain unit)
    {
        if (!Conversion.CanTame(_target)) { Status = JobStatus.Done; return; }

        if (MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(_target.transform.position);

        if (_endTime <= 0f)
        {
            _endTime = Time.time + Conversion.TameSeconds(unit);
            return;
        }
        if (Time.time < _endTime) return;

        Conversion.TameRound(unit, _target);

        // 실패해도 는다(설득과 같은 축).
        if (unit.Stats != null)
            unit.Stats.GrowStat(StatName.Taming, 4f * CombatXp.Sol(unit.Stats.Taming));

        Status = JobStatus.Done;
    }
}
