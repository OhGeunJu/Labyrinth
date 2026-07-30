using System;
using UnityEngine;

// BT 타겟 공용 유틸
public static class BTTargetUtil
{
    // 살아있는 공격 대상인지 — 파괴됨/비활성/시체/기절 제외.
    // 기절 유닛을 제외해야 KO가 사형선고가 아니라 회복 가능한 상태가 된다 (켄시식).
    public static bool IsAttackable(GameObject go) =>
        go != null && go.activeInHierarchy && go.GetComponent<Corpse>() == null
        && !(go.GetComponent<UnitBrain>()?.Blackboard?.IsKnockedOut ?? false);
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
        Vector3 selfPos = bb.Self.transform.position;

        // 이미 잡은 타겟이 유효하면 유지하되, 훨씬 가까운 적이 생기면 갈아탄다.
        // (옆에 적을 두고 멀리 있는 상대를 패러 가는 것 방지 — 단 매 틱 흔들리지 않게 여유를 둔다)
        GameObject current = bb.Target != null && BTTargetUtil.IsAttackable(bb.Target) ? bb.Target : null;
        float currentDistSqr = current != null ? SqrDist(selfPos, current) : float.MaxValue;

        var myFaction = bb.Self.GetComponent<Faction>();
        if (myFaction == null) { bb.Target = current; return current != null ? BTStatus.Success : BTStatus.Failure; }

        // 후보: 감지 범위 내 적대 유닛 + 나를 때린 상대(관계 무관, 감지 범위 2배까지).
        GameObject best = null;
        float bestDistSqr = float.MaxValue;

        foreach (var hit in Physics.OverlapSphere(selfPos, bb.DetectRange))
        {
            if (hit.gameObject == bb.Self) continue;

            var otherFaction = hit.GetComponent<Faction>();
            if (otherFaction == null) continue;
            if (!myFaction.IsHostileTo(otherFaction)) continue;
            if (!BTTargetUtil.IsAttackable(hit.gameObject)) continue;

            float d = SqrDist(selfPos, hit.gameObject);
            if (d < bestDistSqr) { bestDistSqr = d; best = hit.gameObject; }
        }

        // 반격 대상 — 중립이라 위 스캔에 안 걸리지만 대응해야 한다. 거리 비교에 함께 넣는다.
        if (bb.HasFreshAttacker && BTTargetUtil.IsAttackable(bb.LastAttacker))
        {
            float d = SqrDist(selfPos, bb.LastAttacker);
            float limit = bb.DetectRange * 2f;   // 원거리 저격수를 맵 끝까지 쫓지는 않는다
            if (d <= limit * limit && d < bestDistSqr) { bestDistSqr = d; best = bb.LastAttacker; }
        }

        if (best == null)
        {
            bb.Target = current;   // 새 후보가 없으면 기존 타겟 유지(추격 중 놓치지 않게)
            return current != null ? BTStatus.Success : BTStatus.Failure;
        }

        // 기존 타겟보다 확실히 가까울 때만 교체 (SwitchMargin 배 이내면 그대로 — 타겟 흔들림 방지)
        bb.Target = (current == null || bestDistSqr * SwitchMargin < currentDistSqr) ? best : current;
        return BTStatus.Success;
    }

    const float SwitchMargin = 1.5f;   // 새 후보가 기존 타겟보다 이 배수만큼 가까워야 갈아탄다

    static float SqrDist(Vector3 from, GameObject go) =>
        (go.transform.position - from).sqrMagnitude;
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

// 플레이어 직접 이동 명령 — 도착할 때까지 자동 행동(전투/휴식/작업)을 막는다.
// 소집(Draft) 바로 아래에 두어 "내가 시킨 게 최우선"이 되게 한다.
public class ManualOrderNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        // 제자리 대기 명령(원정 집결 등)은 목적지가 없어도 계속 붙든다.
        // 아래 자동 해제만 있으면 "도착했지만 계속 대기"를 표현할 수 없다 — 도착하는 순간 일하러 간다.
        if (bb.HoldPosition) return BTStatus.Running;

        if (!bb.ManualMove) return BTStatus.Failure;

        // 도착했거나(목표 소진) 길찾기가 포기해 멈췄으면 명령 종료 → 평소 행동으로 복귀.
        // 이 해제 조건이 없으면 한 번 명령한 폰이 영영 일을 안 한다.
        if (bb.Mover == null || !bb.Mover.HasTarget())
        {
            bb.ManualMove = false;
            return BTStatus.Failure;
        }

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
            var brain = bb.Self.GetComponent<UnitBrain>();
            var job   = JobManager.Instance.RequestJob(brain);

            if (job == null) return BTStatus.Failure;
            bb.CurrentJob = job;
            job.Start();
            // OnStart이 즉시 종료(예: 재료 부족으로 주문 재큐)한 경우 헛걸음 방지 — 바로 놓는다.
            if (job.IsComplete())
            {
                JobManager.Instance.CompleteJob(job);
                bb.CurrentJob = null;
                return BTStatus.Failure;
            }
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
            float xp = 0.05f * bb.TickDelta
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
            if (MapManager.Instance.GetTile(neighbor.x, neighbor.y).IsPassable
                && MapManager.Instance.IsStandable(neighbor))   // 공허 게이트: 바닥 밖 접근 타일 제외
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
            _waitTimer -= bb.TickDelta;
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

// 침대 휴식: **배정된 침대가 있으면 그 침대로 가서 눕는다.** 누워 있는 동안은 일하지 않고 회복만 한다.
// 배정 해제 = 다시 일하러 감. 배고프면(EatNode 임계) 일어나 먹고 돌아온다 — 누운 채 아사하지 않게.
// 전투/소집 등 상위 분기가 이 노드를 밀어내면 UnitBrain이 자동으로 깨운다(bb.RestTicked).
public class RestNode : BTNode
{
    const float BloodHealRate  = 2f;    // 초당 혈액 회복
    const float CapRestoreRate = 1f;    // 초당 캡 회복
    const float PartHealRate   = 1f;    // 초당 부위 HP 회복 (자연 회복에 추가로 얹힘)
    const float ArrivalDist    = 1.0f;  // 접근 지점(침대 옆) 기준
    const float HungerLeave    = 60f;   // EatNode 임계와 동일

    Bed     _bed;
    Vector3 _approach;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        var brain    = bb.Self.GetComponent<UnitBrain>();
        var assigned = Bed.FindAssigned(brain);

        // 배정 해제됐거나 배가 고프면 일어난다
        if (assigned == null || bb.Hunger <= HungerLeave) { LeaveBed(bb); return BTStatus.Failure; }

        var pos = bb.Self.transform.position;

        if (_bed == null)
        {
            if (!assigned.TryOccupy(brain)) return BTStatus.Failure;   // 다른 폰이 쓰는 중
            _bed = assigned;

            // 침대 중심이 아니라 **설 수 있는 옆면**으로 간다. 중심을 목표로 주면
            // 침대에 정면으로 처박혀(길찾기 그래프에 없는 장애물) 그대로 굳는다.
            _approach = _bed.ApproachPosition(pos);
            bb.Mover.SetMoveTarget(_approach);
        }
        else if (_bed != assigned)   // 침대를 옮겨 배정한 경우
        {
            LeaveBed(bb);
            return BTStatus.Running;
        }

        // 이동 중 — 도착 못 하고 이동도 끝났으면(막힘) 접근 면을 다시 고른다
        if (!bb.Mover.IsSleeping)
        {
            if (Vector3.Distance(pos, _approach) > ArrivalDist)
            {
                if (!bb.Mover.HasTarget())
                {
                    _approach = _bed.ApproachPosition(pos);
                    bb.Mover.SetMoveTarget(_approach);
                }
                return BTStatus.Running;
            }

            // 도착 — 다가온 면에 맞춰 눕는다
            bb.Mover.SetSleeping(true, _bed.SleepPosition, _bed.SleepRotationFrom(pos));
        }

        bb.RestTicked = true;   // 이번 틱에 쉬고 있음 (상위 분기로 넘어가면 UnitBrain이 깨운다)

        // 누워 있는 동안 회복 (캡 감소 없이, 부위 HP 포함)
        var body = bb.Self.GetComponent<BodyPartSystem>();
        if (body != null)
        {
            body.HealBlood(BloodHealRate * bb.TickDelta, degradeCap: false);
            body.RestoreBloodCap(CapRestoreRate * bb.TickDelta);
            body.HealBodyParts(PartHealRate * bb.TickDelta);
        }

        // 회복이 끝나도 일어나지 않는다 — 배정이 곧 "쉬는 중"이라는 명령.
        return BTStatus.Running;
    }

    void LeaveBed(UnitBlackboard bb)
    {
        if (bb.Mover != null) bb.Mover.SetSleeping(false);
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
        _eatTimer += bb.TickDelta;
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