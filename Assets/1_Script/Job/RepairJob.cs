using System.Collections.Generic;
using UnityEngine;

// **상한 건물을 고친다**(2026-09-30, 사용자: *"폰이 자동으로 고친다"*).
//
// ## 재료는 상한 만큼
//
// 다 부서진 것(0)을 끝까지 고치는 데 **짓는 비용의 25%**가 든다(`MaterialShare`). 절반 상했으면 그 절반.
// 시작할 때 한 번 낸다(`MaterialPool.Consume` — 배낭 먼저, 그다음 창고, **폰이 나르지 않는다**: 제작과 같은 길이다).
// 망치질을 시작하기 전에 취소되면 **뺀 것 그대로** 돌려준다(`MaterialPool.Refund(taken)`). 비용이 없는 건물은 공짜다.
//
// ## 빠르기
//
// 초당 `BaseRate` × (0.5 + 노동/100). 노동이 는다(제작·건설과 같은 축).
public class RepairJob : JobBase
{
    public const float MaterialShare = 0.25f;
    const float BaseRate = 8f;

    public override JobCategory Category => JobCategory.Construction;
    public override WorkAnim Anim => WorkAnim.HammerWall;
    public override Component Site => _hp != null ? _hp.Building : null;

    readonly BuildingHealth _hp;
    readonly List<(ItemData item, int amount)> _need = new();
    readonly List<(ItemData item, int amount)> _taken = new();
    bool  _paid;
    float _repaired;

    public BuildingHealth Target => _hp;

    public RepairJob(BuildingHealth hp)
    {
        _hp = hp;
        Priority = 2;
        SetSite(hp != null ? (Component)hp.Building ?? hp : null);
    }

    // 상한 만큼의 재료 — 지금 체력에서 잰다(기다리는 동안 더 상했을 수 있다).
    void MeasureNeed()
    {
        _need.Clear();
        var data = _hp != null && _hp.Building != null ? _hp.Building.Data : null;
        if (data == null || _hp.Max <= 0f) return;

        float missing = Mathf.Clamp01(1f - _hp.Current / _hp.Max);
        foreach (var c in data.Costs)
        {
            if (c.Item == null || c.Amount <= 0) continue;
            int n = Mathf.CeilToInt(c.Amount * MaterialShare * missing);
            if (n > 0) _need.Add((c.Item, n));
        }
    }

    Vector3 SitePos => _hp != null ? _hp.transform.position : Vector3.zero;

    public override bool IsReady
    {
        get
        {
            if (_hp == null || !_hp.Damaged || !IsSiteUsable) return false;
            MeasureNeed();
            foreach (var (item, n) in _need)
                if (MaterialPool.AvailableNear(item, SitePos) < n) return false;
            return true;
        }
    }

    public override bool CanAssignTo(UnitBrain unit)
    {
        MeasureNeed();
        foreach (var (item, n) in _need)
            if (MaterialPool.Available(item, unit) < n) return false;
        return true;
    }

    protected override void OnStart()
    {
        if (_hp == null || !_hp.Damaged) { Status = JobStatus.Done; return; }

        MeasureNeed();
        _taken.Clear();
        foreach (var (item, n) in _need)
        {
            if (MaterialPool.Available(item, ReservedBy) >= n) continue;
            Status = JobStatus.Done;   // 그새 누가 썼다 — 다음 검사에서 다시 걸린다(`RepairDispatcher`)
            return;
        }
        foreach (var (item, n) in _need)
            MaterialPool.Consume(item, n, ReservedBy, _taken);
        _paid = true;
        _repaired = 0f;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_hp == null) { Status = JobStatus.Done; return; }
        if (!_hp.Damaged) { Status = JobStatus.Done; return; }

        float dt    = unit != null && unit.Blackboard != null ? unit.Blackboard.TickDelta : Time.deltaTime;
        float skill = unit != null && unit.Stats != null ? unit.Stats.Labor : 0f;
        float amt   = BaseRate * (0.5f + skill / 100f) * dt;

        _hp.Repair(amt);
        _repaired += amt;
        unit?.Stats?.GrowStat(StatName.Labor, 0.02f * dt * CombatXp.Sol(skill));

        if (!_hp.Damaged) Status = JobStatus.Done;
    }

    // 망치질 전에 취소됐으면 뺀 것 그대로 돌려준다 — 한 번이라도 고쳤으면 재료는 이미 벽에 들어갔다.
    protected override void OnCancelled()
    {
        if (_paid && _repaired <= 0f) MaterialPool.Refund(_taken, SitePos + Vector3.up * 0.3f);
        _paid = false;
    }
}
