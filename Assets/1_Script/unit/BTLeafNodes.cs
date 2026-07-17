using System;
using UnityEngine;

// BT 타겟 공용 유틸
public static class BTTargetUtil
{
    // 살아있는 공격 대상인지 — 파괴됨/비활성/시체 제외
    public static bool IsAttackable(GameObject go) =>
        go != null && go.activeInHierarchy && go.GetComponent<Corpse>() == null;
}

// 조건 체크
public class ConditionNode : BTNode
{
    readonly Func<UnitBlackboard, bool> _condition;
    public ConditionNode(Func<UnitBlackboard, bool> condition) => _condition = condition;
    public override BTStatus Tick(UnitBlackboard bb) =>
        _condition(bb) ? BTStatus.Success : BTStatus.Failure;
}

// 적 감지
public class DetectNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        // 이미 잡은 타겟이 아직 유효한지 확인
        if (bb.Target != null)
        {
            if (BTTargetUtil.IsAttackable(bb.Target)) return BTStatus.Success;
            bb.Target = null;
        }

        // 주변 탐색
        var myFaction = bb.Self.GetComponent<Faction>();
        if (myFaction == null) return BTStatus.Failure;
        Collider[] hits = Physics.OverlapSphere(bb.Self.transform.position, bb.DetectRange);

        foreach (var hit in hits)
        {
            if (hit.gameObject == bb.Self) continue;

            var otherFaction = hit.GetComponent<Faction>();
            if (otherFaction == null) continue;

            if (myFaction.IsHostileTo(otherFaction) && BTTargetUtil.IsAttackable(hit.gameObject))
            {
                bb.Target = hit.gameObject;
                return BTStatus.Success;
            }
        }

        bb.Target = null;
        return BTStatus.Failure;
    }
}

// 소집 상태 (직접 제어)
public class DraftedNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        // 피격 시 반격은 나중에 여기서 처리
        return BTStatus.Running;
    }
}

// 전투
public class FightNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        var combat = bb.Self.GetComponent<CombatHandler>();
        if (combat == null) return BTStatus.Failure;

        // 타겟이 없거나 죽었으면(시체 포함) 전투 종료
        if (!BTTargetUtil.IsAttackable(bb.Target))
        {
            bb.Target = null;
            combat.ExitCombat();
            return BTStatus.Failure;
        }

        float dist = Vector3.Distance(bb.Self.transform.position,
                                       bb.Target.transform.position);

        // 공격 범위 밖이면 이동
        if (dist > bb.AttackRange)
        {
            combat.ExitCombat();
            bb.Mover.SetMoveTarget(bb.Target.transform.position);
            return BTStatus.Running;
        }

        // 공격 범위 안이면 공격
        bb.Mover.StopMovement();
        combat.EnterCombat();
        return BTStatus.Running;
    }
}

// 작업
public class WorkNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        // 배정된 노드가 있으면 우선 처리
        if (bb.AssignedNode != null)
            return TickAssigned(bb);

        // 기존 JobManager 경로
        if (bb.CurrentJob == null)
        {
            var brain      = bb.Self.GetComponent<UnitBrain>();
            var minionData = brain.Data as MinionData;
            var job = minionData != null
                ? JobManager.Instance.RequestJobFiltered(brain, minionData.AllowedJobs)
                : JobManager.Instance.RequestJob(brain);

            if (job == null) return BTStatus.Failure;
            bb.CurrentJob = job;
            job.Start();
            bb.Mover.SetMoveTarget(TileToWorld(job.TileCoord, bb));
        }

        var currentJob = bb.CurrentJob;
        if (currentJob == null) return BTStatus.Failure;

        var jobCenter = MapManager.Instance.TileToWorld(currentJob.TileCoord);
        var pos       = bb.Self.transform.position;
        float dist    = Vector2.Distance(new Vector2(pos.x, pos.z),
                                         new Vector2(jobCenter.x, jobCenter.z));

        if (dist > 2.0f)
        {
            // 이동 중이 아닐 때만 목표 재설정 (이미 이동 중이면 기다림)
            if (!bb.Mover.HasTarget())
                bb.Mover.SetMoveTarget(TileToWorld(currentJob.TileCoord, bb));
        }
        else
        {
            bb.Mover.StopMovement();
            currentJob.Tick(bb.Self.GetComponent<UnitBrain>());
            if (currentJob.IsComplete())
            {
                JobManager.Instance.CompleteJob(currentJob);
                bb.CurrentJob = null;
                return BTStatus.Success;
            }
        }

        return BTStatus.Running;
    }

    BTStatus TickAssigned(UnitBlackboard bb)
    {
        var node = bb.AssignedNode;

        if (node == null)
        {
            bb.AssignedNode = null;
            return BTStatus.Failure;
        }

        var nodeCenter = MapManager.Instance.TileToWorld(node.TileCoord);
        var pos = bb.Self.transform.position;
        float dist = Vector2.Distance(new Vector2(pos.x, pos.z),
                                      new Vector2(nodeCenter.x, nodeCenter.z));

        if (dist > 2.0f)
        {
            if (!bb.Mover.HasTarget())
                bb.Mover.SetMoveTarget(TileToWorld(node.TileCoord, bb));
        }
        else
        {
            bb.Mover.StopMovement();
            var brain = bb.Self.GetComponent<UnitBrain>();
            bool isCutting = node.Data != null && node.Data.ResourceType == ResourceType.Wood;
            StatName workStat = isCutting ? StatName.Cutting : StatName.Mining;
            float skill = brain.Stats.GetStat(workStat);
            node.Work(skill);

            float required = node.Data != null ? node.Data.RequiredSkill : 0f;
            float xp = 0.05f * Time.deltaTime
                     * CombatXp.ChallengeMult(skill, required)
                     * CombatXp.Sol(skill);
            brain.Stats.GrowStat(workStat, xp);
        }

        return BTStatus.Running;
    }

    Vector3 TileToWorld(Vector2Int tile, UnitBlackboard bb)
    {
        var adjacent = GetAdjacentPassable(tile);
        var world = MapManager.Instance.TileToWorld(adjacent);
        return new Vector3(world.x, bb.Self.transform.position.y, world.z);
    }

    Vector2Int GetAdjacentPassable(Vector2Int tile)
    {
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in dirs)
        {
            var neighbor = tile + dir;
            if (MapManager.Instance.GetTile(neighbor.x, neighbor.y).IsPassable)
                return neighbor;
        }
        return tile;
    }
}

// 순찰: 배정된 PatrolZone이 있으면 웨이포인트 또는 구역 내 랜덤 이동
public class PatrolNode : BTNode
{
    int   _waypointIndex;
    float _waitTimer;
    const float WaitTime    = 2f;
    const float ArrivalDist = 1.5f;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        var zone = bb.AssignedPatrolZone;
        if (zone == null) return BTStatus.Failure;

        // 대기 중
        if (_waitTimer > 0f)
        {
            bb.Mover.StopMovement();
            _waitTimer -= Time.deltaTime;
            return BTStatus.Running;
        }

        switch (zone.Mode)
        {
            case PatrolMode.Waypoint:
            {
                if (zone.WaypointCount == 0) return BTStatus.Failure;
                var target = zone.GetWaypoint(_waypointIndex);
                float dist = Vector3.Distance(bb.Self.transform.position, target);
                if (dist > ArrivalDist)
                    bb.Mover.SetMoveTarget(target);
                else
                {
                    _waypointIndex = (_waypointIndex + 1) % zone.WaypointCount;
                    _waitTimer = WaitTime;
                }
                break;
            }
            case PatrolMode.Area:
            {
                if (!bb.Mover.HasTarget())
                {
                    bb.Mover.SetMoveTarget(zone.GetRandomAreaPoint());
                    _waitTimer = WaitTime;
                }
                break;
            }
            case PatrolMode.Idle:
            {
                var center = zone.transform.position;
                float dist = Vector3.Distance(bb.Self.transform.position, center);
                if (dist > ArrivalDist)
                    bb.Mover.SetMoveTarget(center);
                else
                    bb.Mover.StopMovement();
                break;
            }
        }

        return BTStatus.Running;
    }
}

// 침대 휴식: 혈액 < 60% 이거나 BloodCap이 덜 회복됐으면 가장 가까운 빈 침대로
public class RestNode : BTNode
{
    const float RestThreshold  = 0.6f;  // 혈액 60% 이하 시 휴식 필요
    const float CapThreshold   = 0.95f; // 캡 95% 이상이면 충분히 회복됨
    const float BloodHealRate  = 2f;    // 초당 혈액 회복
    const float CapRestoreRate = 1f;    // 초당 캡 회복
    const float ArrivalDist    = 1.5f;

    Bed _bed;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        var body = bb.Self.GetComponent<BodyPartSystem>();
        if (body == null) return BTStatus.Failure;

        bool needsRest = body.Blood    < body.MaxBlood * RestThreshold
                      || body.BloodCap < body.MaxBlood * CapThreshold;

        if (!needsRest) { LeaveBed(bb); return BTStatus.Failure; }

        // 배정된 침대만 사용
        if (_bed == null)
        {
            var brain = bb.Self.GetComponent<UnitBrain>();
            _bed = Bed.FindAssigned(brain);
            if (_bed == null) return BTStatus.Failure;

            if (!_bed.TryOccupy(brain)) { _bed = null; return BTStatus.Failure; }

            bb.Mover.SetMoveTarget(_bed.transform.position);
        }

        // 이동 중
        float dist = Vector3.Distance(bb.Self.transform.position, _bed.transform.position);
        if (dist > ArrivalDist) return BTStatus.Running;

        // 침대에서 회복 (캡 감소 없이)
        bb.Mover.StopMovement();
        body.HealBlood(BloodHealRate * Time.deltaTime, degradeCap: false);
        body.RestoreBloodCap(CapRestoreRate * Time.deltaTime);

        if (body.Blood >= body.BloodCap && body.BloodCap >= body.MaxBlood * CapThreshold)
        {
            LeaveBed(bb);
            return BTStatus.Success;
        }

        return BTStatus.Running;
    }

    void LeaveBed(UnitBlackboard bb)
    {
        if (_bed == null) return;
        _bed.Vacate(bb.Self.GetComponent<UnitBrain>());
        _bed = null;
    }
}

// 허기: 허기가 낮으면 음식을 찾아 먹는다
public class EatNode : BTNode
{
    const float HungerThreshold = 60f;  // 이 이하이면 먹으러 감
    const float ArrivalDist     = 1.5f;

    FoodData _food;
    Storage  _sourceStorage;
    float    _eatTimer;
    bool     _hasFood; // true = 인벤에 음식 있음, 먹는 단계

    public override BTStatus Tick(UnitBlackboard bb)
    {
        if (bb.Hunger >= HungerThreshold) { Reset(); return BTStatus.Failure; }

        var brain = bb.Self.GetComponent<UnitBrain>();

        if (!_hasFood)
        {
            // 1) 인벤토리에 음식 있으면 바로 먹기
            var invFood = FindFoodIn(brain.Inventory);
            if (invFood != null)
            {
                _food    = invFood;
                _hasFood = true;
                bb.Mover.StopMovement();
            }
            else
            {
                // 2) 창고에서 음식 찾기
                if (_sourceStorage == null)
                {
                    (_sourceStorage, _food) = FindFoodInStorage(bb.Self.transform.position);
                    if (_sourceStorage == null) return BTStatus.Failure;
                    bb.Mover.SetMoveTarget(_sourceStorage.transform.position);
                }

                float dist = Vector3.Distance(bb.Self.transform.position,
                                              _sourceStorage.transform.position);
                if (dist > ArrivalDist) return BTStatus.Running;

                // 창고 도착 → 음식 꺼내기
                int picked = _sourceStorage.Inventory.Remove(_food, 1);
                if (picked <= 0) { Reset(); return BTStatus.Failure; }
                if (brain.Inventory.TryAdd(_food, 1) <= 0)
                {
                    // 인벤 만석 — 꺼낸 음식을 창고에 되돌리고 포기 (증발 방지)
                    _sourceStorage.Inventory.TryAdd(_food, 1);
                    Reset();
                    return BTStatus.Failure;
                }
                _hasFood = true;
                bb.Mover.StopMovement();
            }
        }

        // 먹는 중
        _eatTimer += Time.deltaTime;
        if (_eatTimer < _food.EatTime) return BTStatus.Running;

        brain.Inventory.Remove(_food, 1);
        bb.Hunger = Mathf.Min(100f, bb.Hunger + _food.NutritionValue);
        Debug.Log($"[EatNode] {bb.Self.name} 섭취 완료 — {_food.ItemName} (허기 {bb.Hunger:F0}/100)");
        Reset();
        return BTStatus.Success;
    }

    FoodData FindFoodIn(Inventory inv)
    {
        foreach (var inst in inv.Items)
            if (inst.Data is FoodData f && inst.Quantity > 0) return f;
        return null;
    }

    (Storage storage, FoodData food) FindFoodInStorage(Vector3 pos)
    {
        Storage  nearest = null;
        FoodData food    = null;
        float    minDist = float.MaxValue;
        foreach (var sb in Storage.All)
        {
            foreach (var inst in sb.Inventory.Items)
            {
                if (inst.Data is FoodData f && inst.Quantity > 0)
                {
                    float d = Vector3.Distance(pos, sb.transform.position);
                    if (d < minDist) { minDist = d; nearest = sb; food = f; }
                    break;
                }
            }
        }
        return (nearest, food);
    }

    void Reset()
    {
        _food = null; _sourceStorage = null; _eatTimer = 0f; _hasFood = false;
    }
}

// 대기
public class IdleNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        return BTStatus.Running;
    }
}