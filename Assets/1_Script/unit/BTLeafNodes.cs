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

// 따라가기 — 지정한 대상 곁에 머문다. 우클릭 메뉴의 [따라가기]가 켠다.
//
// **`ManualOrderNode` 바로 아래**에 둔다. 위에 두면 새 이동 명령이 안 먹고(따라가기가 계속 이김),
// 전투·작업보다 아래에 두면 따라가는 도중에 일하러 새 버린다.
public class FollowNode : BTNode
{
    readonly float _stopDistance;

    public FollowNode(float stopDistance = 2.5f) => _stopDistance = stopDistance;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        // 대상이 파괴되면 유니티의 `== null`이 잡는다. **여기서 꺼 주지 않으면**
        // 죽은 대상을 향해 영영 Running을 돌려 폰이 아무것도 안 하게 된다.
        if (bb.FollowTarget == null)
        {
            bb.FollowTarget = null;
            return BTStatus.Failure;
        }

        Vector3 to = bb.FollowTarget.position;
        float dist = Vector3.Distance(bb.Self.transform.position, to);

        // 매 틱 같은 목표를 다시 넣어도 `PawnMover`가 근접 재지정을 스로틀한다.
        if (dist > _stopDistance) bb.Mover.SetMoveTarget(to);
        else                      bb.Mover.StopMovement();

        return BTStatus.Running;
    }
}

// 우클릭 [줍는다] — 지정한 드롭까지 걸어가 배낭에 담는다.
// 큐를 안 타므로 운반 작업을 꺼 둔 폰에게도 시킬 수 있다(직접 명령이므로).
public class PickUpNode : BTNode
{
    const float ReachDistance = 1.2f;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        // 남이 먼저 주웠거나 사라졌으면 유니티의 `== null`이 잡는다. **여기서 꺼 주지 않으면**
        // 없는 것을 향해 영영 Running을 돌려 폰이 아무것도 안 하게 된다(`FollowNode`와 같은 함정).
        var drop = bb.PickUpTarget;
        if (drop == null) { bb.PickUpTarget = null; return BTStatus.Failure; }

        if (Vector3.Distance(bb.Self.transform.position, drop.transform.position) > ReachDistance)
        {
            bb.Mover.SetMoveTarget(drop.transform.position);
            return BTStatus.Running;
        }

        bb.Mover.StopMovement();

        bool ignoreLimit = bb.PickUpIgnoreLimit;
        bool thenEquip   = bb.PickUpThenEquip;
        var  item        = drop.Item;
        bb.PickUpTarget      = null;   // 성공이든 실패든 이 명령은 여기서 끝난다
        bb.PickUpIgnoreLimit = false;
        bb.PickUpThenEquip   = false;

        var brain = bb.Self.GetComponent<UnitBrain>();
        if (brain == null || brain.Inventory == null) return BTStatus.Failure;

        // **한 명만 골라 시켰으면 한도를 넘겨서라도 든다**(`PickUpIgnoreLimit`) — 손으로 배낭에
        // 넣는 것과 같은 일이고, 과적은 막는 대신 속도로 갚는다. 여럿을 골라 시킨 것은 일감
        // 배분에 가까우므로 질 수 있는 만큼만 든다.
        // 어느 쪽이든 **격자 자리는 넘을 수 없다** — 못 실은 잔량은 그 자리에 다시 떨어지고
        // 새 운반 일감이 걸린다(`DroppedItem.PickUp`).
        int max = ignoreLimit ? int.MaxValue : CarryCapacity.RoomFor(brain, drop.Item);
        int got = drop.PickUp(brain.Inventory, max);
        if (got <= 0)
        {
            Debug.Log($"[줍기] {brain.UnitName}: 배낭에 자리가 없어 {item.ItemName}을 들지 못했습니다.");
            return BTStatus.Failure;
        }

        // **줍는 것과 착용은 한 동작이다.** 배낭에 넣었다가 배낭 창을 열어 다시 차게 하면
        // "저걸 입어라"가 두 조작이 된다. 원래 차고 있던 것은 `Equipment`가 배낭으로
        // 돌려주므로(교체가 이미 그렇게 동작한다) 여기서 따로 벗기지 않는다.
        if (thenEquip) EquipPickedUp(brain, item);

        return BTStatus.Success;
    }

    static void EquipPickedUp(UnitBrain brain, ItemData item)
    {
        var equipment = brain.Equipment;
        if (equipment == null) return;

        bool ok = item switch
        {
            WeaponData weapon => equipment.Equip(weapon),   // 방패면 Equip이 보조손으로 보낸다
            ArmorData  armor  => equipment.EquipArmor(armor),
            _                 => false,
        };
        if (!ok)
            Debug.Log($"[착용] {brain.UnitName}: {item.ItemName}을 착용하지 못했습니다 " +
                      "(양손 무기를 든 채 보조손 등).");
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
    // ── 못 가는 일감을 놓는 장치 ──────────────────────────────────────────
    // 일감 지점까지 **길이 없으면** 폰은 도착 판정(2.0m)에 영영 안 걸리고, 그 자리에서 얼어붙는다.
    // 로스터에는 "운반"이라 떠 있는데 아무 일도 일어나지 않고, 그 일감은 예약된 채라
    // **다른 폰도 못 가져간다** — 한 명이 멈추는 게 아니라 그 일감이 통째로 죽는다.
    //
    // 길이 막히는 이유는 여러 가지다(노드가 사방으로 둘러싸임, 바닥 밖, 철거로 지형 변화…).
    // 원인을 다 막기보다 **진행이 없으면 놓는다**로 처리한다 —
    // `ExpeditionFormation.NudgeStragglers`가 집결에서 쓰는 것과 같은 모양.
    const float StuckSeconds  = 6f;     // 이만큼 가까워지지 않으면 포기
    const float ProgressEpsilon = 0.35f; // 이보다 줄어야 "가고 있다"고 본다

    // ── 어디까지 가면 "도착"인가 ──────────────────────────────────────────
    // **저작한 자리에 실제로 선다**(`WorkSpot`). 칸 하나가 1m이므로 그 칸 위에 서 있다는 것은
    // 중심에서 반대각선(≈0.71) 안쪽이라는 뜻이다 — 0.9면 넉넉하다.
    //
    // 예전엔 이 값이 **2.0**뿐이었다. 그러면 폰이 작업대에서 두 칸 떨어져 서서 일하고,
    // 자리를 저작한 뜻이 사라진다(멀리서 허공에 대고 제작하는 것처럼 보인다).
    //
    // 그렇다고 0.9만 두면 **못 서는 상황이 곧 교착**이다(그 칸에 다른 폰이 서 있거나 반 칸
    // 어긋난 건물이 물고 있을 때). 그래서 **가까이 와서 더는 못 좁히면 거기서라도 한다** —
    // 일이 아예 안 되는 것보다 한 발 떨어져 하는 편이 낫다. 완전히 못 가면 6초 뒤 포기는 그대로다.
    const float WorkRadius    = 0.9f;   // 저작된 자리에 선다
    const float ReachRadius   = 2.0f;   // 더는 못 좁힐 때 받아들이는 한계
    const float SettleSeconds = 2f;     // 이만큼 못 좁히면 "더는 못 간다"로 본다

    // **자원 노드는 이 규약을 안 쓴다.** 바위·나무에는 저작된 자리가 없고 `MaxWorkers`만큼
    // 여럿이 둘러서서 캔다 — 한 칸에 붙이면 서로 비켜서다 아무도 못 캔다. 넉넉히 둔다.
    const float NodeReach = 2.0f;

    float _stuckTimer;
    float _bestDist = float.MaxValue;

    void ResetProgress()
    {
        _stuckTimer = 0f;
        _bestDist   = float.MaxValue;
    }

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
            ResetProgress();   // 새 일감 — 진행 측정을 처음부터
        }

        var currentJob = bb.CurrentJob;
        if (currentJob == null) return BTStatus.Failure;

        // **밖에서 취소된 일감을 계속 붙들고 있으면 폰이 그 자리에서 언다.**
        // `JobBase.Tick`은 InProgress일 때만 돌고 `IsComplete()`는 Done일 때만 참이라,
        // 취소된 작업은 어느 쪽에도 안 걸려 영원히 손에 남는다 — 도착해서 아무것도 안 하고
        // 서 있게 된다. 연구를 다른 것으로 갈아타면 실제로 이 경로가 만들어진다.
        // 되돌리기(`OnCancelled`)는 취소한 쪽에서 이미 돌았으므로 여기선 놓기만 한다.
        if (currentJob.Status == JobStatus.Cancelled)
        {
            bb.CurrentJob = null;
            bb.Mover.StopMovement();
            ResetProgress();
            return BTStatus.Failure;
        }

        var jobCenter = MapManager.Instance.TileToWorld(currentJob.TileCoord);
        var pos       = bb.Self.transform.position;
        float dist    = Vector2.Distance(new Vector2(pos.x, pos.z),
                                         new Vector2(jobCenter.x, jobCenter.z));

        if (!Arrived(dist, currentJob.TileCoord))
        {
            // 이동 중이 아닐 때만 목표 재설정 (이미 이동 중이면 기다림)
            if (!bb.Mover.HasTarget())
                bb.Mover.SetMoveTarget(TileToWorld(currentJob.TileCoord, bb));

            // 가까워지고 있나? 아니면 얼마나 오래 제자리인가.
            if (dist < _bestDist - ProgressEpsilon) { _bestDist = dist; _stuckTimer = 0f; }
            else                                     _stuckTimer += bb.TickDelta;

            if (_stuckTimer >= StuckSeconds)
            {
                Debug.LogWarning($"[Work] {bb.Self.name}: '{JobCategoryInfo.DisplayName(currentJob.Category)}' " +
                                 $"일감 지점 {currentJob.TileCoord}까지 갈 수 없어 놓습니다 " +
                                 "(길이 막혔거나 바닥 밖일 수 있습니다).");

                // **왜 못 갔는지까지 남긴다.** 좌표만 찍으면 "길은 열려 보이는데 왜?"에서 멈춘다 —
                // 목표 칸이 점유됐는지·바닥이 있는지·차단 기억에 걸렸는지, 그리고 로컬 경로가
                // 아예 안 나온 것인지(직선 폴백으로 처박은 것인지)가 원인을 가른다.
                // `PawnMover`의 포기(4단계)가 쓰던 진단을 일감 쪽에서도 그대로 쓴다.
                MovementDiagnostics.LogGiveUp(bb.Self.name, pos, jobCenter,
                                              bb.Mover.HasLocalPath, bb.Self);

                // 놓는 건 **취소**로 한다 — 각 Job이 OnCancelled에 "들고 있던 것 되돌리기"를
                // 이미 구현해 두었다(운반 짐 드롭·제작 재료 환불). 예약만 풀면 그게 안 돈다.
                if (JobManager.Instance != null) JobManager.Instance.CancelJob(currentJob);
                bb.CurrentJob = null;
                bb.Mover.StopMovement();
                ResetProgress();
                return BTStatus.Failure;   // 이번 틱은 다른 걸 하게
            }
        }
        else
        {
            ResetProgress();
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

        // **자원 노드는 둘러서서 캔다 — 한 자리에 붙이지 않는다.**
        // 작업대와 다른 축이다: 작업대는 `WorkSpot`으로 **설 자리를 저작**하지만, 바위·나무는
        // 그런 자리가 없고 `MaxWorkers`만큼 여럿이 붙는다. 좁게 잡으면 한 칸을 두고 서로
        // 비켜서다 아무도 못 캔다. 넉넉히 잡아 각자 닿는 데서 캐게 둔다.
        if (dist > NodeReach)
        {
            if (!bb.Mover.HasTarget())
                bb.Mover.SetMoveTarget(TileToWorld(node.TileCoord, bb));
        }
        else
        {
            bb.Mover.StopMovement();
            var brain = bb.Self.GetComponent<UnitBrain>();
            bool isCutting = node.Data != null && node.Data.IsTree;
            StatName workStat = isCutting ? StatName.Cutting : StatName.Mining;
            float skill = brain.Stats.GetStat(workStat);
            node.Work(skill, bb.TickDelta);

            float required = node.Data != null ? node.Data.RequiredSkill : 0f;
            float xp = 0.05f * bb.TickDelta
                     * CombatXp.ChallengeMult(skill, required)
                     * CombatXp.Sol(skill);
            brain.Stats.GrowStat(workStat, xp);
        }

        return BTStatus.Running;
    }

    // "여기서 하기로 했다"를 **붙들어 둔다.** 일을 시작하면 `ResetProgress`가 정체 타이머를
    // 지우는데, 그걸 매 틱 다시 재면 다음 틱에 "아직 안 왔다"가 되어 **일한다 ↔ 다시 걷는다**가
    // 진동한다. 목표 칸이 바뀌면(운반 2단계처럼) 저절로 풀리도록 어느 칸이었는지도 같이 든다.
    bool       _settled;
    Vector2Int _settledFor;

    // 저작된 자리에 섰거나(WorkRadius), 가까이 와서 더는 못 좁히거나(ReachRadius + 정체).
    bool Arrived(float dist, Vector2Int tile)
    {
        if (dist <= WorkRadius) return true;
        if (dist > ReachRadius) return false;

        if (_settled && _settledFor == tile) return true;
        if (_stuckTimer < SettleSeconds) return false;

        _settled    = true;
        _settledFor = tile;
        return true;
    }

    Vector3 TileToWorld(Vector2Int tile, UnitBlackboard bb)
    {
        var adjacent = GetAdjacentPassable(tile);
        var world = MapManager.Instance.TileToWorld(adjacent);
        return new Vector3(world.x, bb.Self.transform.position.y, world.z);
    }

    // 목표 칸이 이미 설 수 있으면 그대로 간다(`WorkSpot`이 정해 준 자리가 대개 그렇다).
    // 아니면 **대각선과 반경 2까지** 넓혀 찾는다 — 상하좌우 4칸만 보면 건물이 몰린 곳에서
    // 전부 막혀 "막힌 칸 자신"을 목적지로 돌려주고, 그러면 폰이 영영 도착하지 못한다.
    Vector2Int GetAdjacentPassable(Vector2Int tile) =>
        MapManager.Instance.FindWorkableNear(tile);
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