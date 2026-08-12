using System;
using UnityEngine;

// BT 타겟 공용 유틸
public static class BTTargetUtil
{
    // 살아있는 공격 대상인지 — 파괴됨/비활성/시체/기절 제외.
    // 기절 유닛을 제외해야 KO가 사형선고가 아니라 회복 가능한 상태가 된다 (켄시식).
    public static bool IsAttackable(GameObject go) => IsAttackable(go, null);

    // `bb`를 주면 그 폰이 **콕 집어 마무리하라고 지목한 대상**(`FinishTarget`)은 기절해 있어도
    // 통과한다. 지목은 우클릭 [마무리한다]에서만 붙으므로 자동 경로는 예전 그대로다 —
    // 탐지·반격은 `bb` 없이 물어서 쓰러진 놈을 계속 건너뛴다.
    public static bool IsAttackable(GameObject go, UnitBlackboard bb)
    {
        if (go == null || !go.activeInHierarchy) return false;
        if (go.GetComponent<Corpse>() != null) return false;   // 죽었으면 더 때릴 것이 없다

        var board = go.GetComponent<UnitBrain>()?.Blackboard;

        // **업힌 사람은 지목해도 못 친다.** 어깨 위에 있어 사거리가 업은 폰과 같아서,
        // 노린 것이 짐인지 사람인지 구별이 안 된다.
        if (board != null && board.CarriedBy != null) return false;

        // **갇힌 사람과 떠나는 사람은 쓰러진 사람과 같은 규칙**이다 — 자동으로는 아무도
        // 안 건드리고, 콕 집어 지목했을 때만 통과한다(`FinishTarget`).
        //
        // 떠나는 쪽을 안 빼면 **놓아 주는 것이 성립하지 않는다**: 푸는 순간 우리 폰이
        // 자동으로 탐지해 쫓아가 죽인다. 저쪽은 도주 성향이라 안 덤비므로 일방적인 사냥이다.
        // 마음이 바뀌면 지목해서 치면 된다(그때는 `LeaveMapNode`가 꺼지고 저쪽도 되받아친다).
        bool down  = board?.IsKnockedOut ?? false;
        bool held  = board?.IsPrisoner   ?? false;
        bool going = board?.LeavingMap   ?? false;
        return !(down || held || going) || (bb != null && bb.FinishTarget == go);
    }
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
//
// **`retaliateOnly`면 먼저 걸지 않는다** — 나를 때린 상대만 표적으로 잡는다.
//
// 방랑자가 이쪽이다. 하수인·적처럼 범위 안의 적대를 모두 훑으면 **직접 제어가 성립하지
// 않는다**: 지나가는 적을 보고 저 혼자 달려 나가면, 어디에 세워 둘지가 명령이 아니라 지형의
// 문제가 된다. 그렇다고 아무것도 안 하면 **맞으면서 가만히 서 있는다**(실제로 그랬다 —
// 방랑자 트리에 `DetectNode`가 없어 `bb.Target`이 저 혼자 붙는 길이 아예 없었다).
//
// 그래서 **반격만** 한다. 규칙을 새로 만들지 않고 여기 스위치를 두는 이유는, 반격 대상을
// 고르는 규칙(15초 기억 · 감지 범위 2배 · 쓰러진 놈 제외)이 이미 아래에 있어서다 —
// 노드를 따로 만들면 그 셋이 두 벌이 된다.
public class DetectNode : BTNode
{
    readonly bool _retaliateOnly;

    public DetectNode(bool retaliateOnly = false) => _retaliateOnly = retaliateOnly;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        Vector3 selfPos = bb.Self.transform.position;

        // 이미 잡은 타겟이 유효하면 유지하되, 훨씬 가까운 적이 생기면 갈아탄다.
        // (옆에 적을 두고 멀리 있는 상대를 패러 가는 것 방지 — 단 매 틱 흔들리지 않게 여유를 둔다)
        // 들고 있던 타겟은 `bb`를 같이 물어본다(지목한 마무리 대상이면 기절해도 유지).
        // 아래 새 후보 스캔·반격은 `bb` 없이 물어 **자동으로는 쓰러진 놈을 안 고른다.**
        GameObject current = bb.Target != null && BTTargetUtil.IsAttackable(bb.Target, bb) ? bb.Target : null;
        float currentDistSqr = current != null ? SqrDist(selfPos, current) : float.MaxValue;

        var myFaction = bb.Faction;

        // 후보: 감지 범위 내 적대 유닛 + 나를 때린 상대(관계 무관, 감지 범위 2배까지).
        // 훑는 일은 `FactionScan`이 한다 — 도주·수적 보너스와 **같은 한 벌**이고,
        // `Faction.All`을 직접 돌아 매 틱 배열을 만들지 않는다.
        //
        // **반격 전용이면 이 훑기를 건너뛴다** — 먼저 거는 것이 곧 이 스캔이다.
        GameObject best = _retaliateOnly || myFaction == null
            ? null
            : FactionScan.NearestHostile(bb.Self, myFaction, bb.DetectRange, theirsToMine: false);
        float bestDistSqr = best != null ? SqrDist(selfPos, best) : float.MaxValue;

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
// **사람을 업으러 가고, 지정한 곳에 내려놓는다.** 명령 하나에 두 걸음이 들어 있다 —
// 업으라고만 시키고 끝내면 폰이 사람을 진 채로 서 있고, 내려놓으라고만 시키면 어디로
// 데려갈지를 매번 다시 눌러야 한다.
//
// `PickUpNode`와 같은 규약: 목표가 사라지면 **여기서 꺼 준다**(안 꺼 주면 없는 것을 향해
// 영영 Running을 돌려 폰이 아무것도 안 하게 된다).
public class CarryPawnNode : BTNode
{
    const float ReachDistance = 1.4f;   // 사람은 물건보다 크다

    public override BTStatus Tick(UnitBlackboard bb)
    {
        var self = bb.Brain;
        if (self == null) return BTStatus.Failure;

        // ── 1) 업으러 간다 ────────────────────────────────────────────────
        var target = bb.CarryTarget;
        if (target != null && bb.Carrying == null)
        {
            // 그새 깨어났거나 남이 업어 갔으면 명령을 접는다 — 데려가려던 것도 같이 접는다
            // (업지도 못했는데 목적지만 남으면 다음에 다른 사람을 업었을 때 딸려 간다).
            if (!PawnCarry.CanCarry(self, target))
            {
                bb.CarryTarget = null;
                bb.EscortTo    = EscortDest.None;
                return BTStatus.Failure;
            }

            if (Vector3.Distance(bb.Self.transform.position, target.transform.position) > ReachDistance)
            {
                bb.Mover.SetMoveTarget(target.transform.position);
                return BTStatus.Running;
            }

            bb.Mover.StopMovement();
            bb.CarryTarget = null;
            return PawnCarry.TryPickUp(self, target) ? BTStatus.Running : BTStatus.Failure;
        }

        if (bb.Carrying == null) return BTStatus.Failure;

        // ── 2) 자리로 데려간다 (간수) ─────────────────────────────────────
        //
        // 콕 집은 자리가 없을 때만 스스로 정한다 — 손으로 고른 자리가 있으면 그쪽이 이긴다.
        //
        // **매 틱 다시 정한다.** 자리를 미리 못 박으면 걸어가는 사이에 그 칸이 차도 계속
        // 그리로 가고, 도착해서야 "자리 없음"이 뜬다. 종류만 정해 두면 다음 칸으로 갈아탄다.
        if (!bb.HasPutDown)
        {
            bool escorting = bb.EscortTo != EscortDest.None
                          || (PrisonEscort.IsWarden(self) && PrisonEscort.IsCaptive(bb.Carrying));

            if (!escorting) return BTStatus.Failure;   // 간수가 꺼져 있으면 그냥 업고 있는다

            if (!PrisonEscort.Resolve(bb.Carrying, bb.EscortTo, self.transform.position,
                                      out var toBed, out var toCage))
                return BTStatus.Failure;               // 빈 자리가 없다 — 업은 채로 기다린다

            bb.HasPutDown  = true;
            bb.PutDownBed  = toBed;
            bb.PutDownCage = toCage;
            bb.PutDownAt   = toBed != null ? toBed.SleepPosition : toCage.HoldPosition;
        }

        // 목적지가 사라졌으면(침대 철거·우리 파괴) 발밑에 내려놓고 끝낸다 — 업은 채로
        // 영영 서 있는 것보다 낫다.
        Vector3 spot = bb.PutDownBed  != null ? bb.PutDownBed.SleepPosition
                     : bb.PutDownCage != null ? bb.PutDownCage.HoldPosition
                     : bb.PutDownAt;

        if (Vector3.Distance(bb.Self.transform.position, spot) > ReachDistance)
        {
            bb.Mover.SetMoveTarget(spot);
            return BTStatus.Running;
        }

        bb.Mover.StopMovement();

        var carried = bb.Carrying;
        var bed     = bb.PutDownBed;
        var cage    = bb.PutDownCage;

        bb.HasPutDown  = false;
        bb.PutDownBed  = null;
        bb.PutDownCage = null;

        // **자리가 남아 있는지 도착해서 다시 본다.** 선택지를 고를 때는 비어 있었어도, 걸어오는
        // 사이에 다른 폰이 먼저 넣었을 수 있다 — 안 보면 **한 우리에 여럿이 겹쳐 선다**
        // (등록은 하나만 되고 나머지는 갇히지도 않은 채 그 자리에 서 있는다).
        bool cageFull = cage != null && cage.IsOccupied;
        // 자리가 찼거나, **그 자리가 이 사람을 안 받거나**(보통 침대 × 붙잡은 사람).
        bool bedTaken = bed  != null && (bed.IsOccupied || !bed.Accepts(carried));

        if (cageFull || bedTaken)
        {
            // 자리가 없으면 **거기 두지 않는다.** 업은 폰 발밑에 내려놓고 알린다 —
            // 겹쳐 세우면 무슨 일이 일어난 건지 화면에 안 남는다.
            PawnCarry.PutDown(self);
            EventNotificationUI.Notify("자리 없음",
                $"{(cageFull ? "우리" : "죄수 침대")}에 이미 다른 사람이 있어 " +
                $"{carried?.UnitName}을(를) 그 앞에 내려놓았습니다.", holdWhenHidden: true);
            return BTStatus.Failure;
        }

        // 침대에 눕힐 때는 **베개 쪽을 침대가 정한다**(`SleepRotation` — `RestNode`가 제 발로
        // 누울 때 쓰는 것과 같은 값). 안 그러면 발이 베개를 벤다.
        PawnCarry.PutDown(self, spot, bed != null ? bed.SleepRotation : (Quaternion?)null);
        if (carried == null) return BTStatus.Failure;

        // **놓는 자리가 곧 그다음 상태다.** 우리·죄수 침대면 갇히고, 보통 침대면 배정만
        // 해 준다(그러면 `RestNode`가 회복을 맡는다). 맨바닥이면 그냥 누워 있는다.
        // **점유·배정·갇힘을 침대가 한 번에 세운다**(`PlacePrisoner`) — 여기서 셋을 따로
        // 부르면 하나가 빠졌을 때 조용히 어긋난다.
        if (cage != null) cage.TryPut(carried);
        else if (bed != null) bed.PlacePrisoner(carried);

        return BTStatus.Success;
    }
}

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

        var brain = bb.Brain;
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
        var combat = bb.Combat;
        if (combat == null) return BTStatus.Failure;

        // 타겟이 없거나 죽었으면(시체 포함) 전투 종료.
        // **지목한 마무리 대상은 기절해 있어도 통과한다** — `bb`를 같이 넘기는 이유다.
        if (!BTTargetUtil.IsAttackable(bb.Target, bb))
        {
            bb.Target       = null;
            bb.FinishTarget = null;   // 끝났으면 예외도 같이 거둔다(다음 대상에 새지 않게)
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
    // **얼마나 가까이 가야 하는지는 일감이 정한다**(`JobBase.Reach`) — 작업대는 저작된 자리에
    // 서야 하지만 창고는 손만 닿으면 된다. 여기 상수로 못 박아 두었더니 **모든 건물 일감이
    // 한 자리에 붙어야** 했고, 창고를 여럿이 쓸 때 서로 비켜서느라 오래 걸렸다.
    const float ReachRadius   = JobBase.HandReach;  // 더는 못 좁힐 때 받아들이는 한계
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
            var brain = bb.Brain;
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

        if (!Arrived(dist, currentJob.TileCoord, currentJob.Reach))
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
            currentJob.Tick(bb.Brain);
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
            var brain = bb.Brain;
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

    // 일감이 요구하는 거리 안에 들었거나(`JobBase.Reach`), 가까이 와서 더는 못 좁히거나
    // (`ReachRadius` + 정체). 후자는 **저작된 자리에 서야 하는 일**에만 뜻이 있다 —
    // 손만 닿으면 되는 일(`HandReach`)은 이미 첫 줄에서 통과한다.
    bool Arrived(float dist, Vector2Int tile, float reach)
    {
        if (dist <= reach) return true;
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
        var brain    = bb.Brain;
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
            bb.Mover.SetSleeping(true, _bed.SleepPosition, _bed.SleepRotation);
        }

        bb.RestTicked = true;   // 이번 틱에 쉬고 있음 (상위 분기로 넘어가면 UnitBrain이 깨운다)

        // 누워 있는 동안 회복 (캡 감소 없이, 부위 HP 포함)
        var body = bb.Body;
        if (body != null)
        {
            body.HealBlood(BloodHealRate * bb.TickDelta, degradeCap: false);
            body.RestoreBloodCap(CapRestoreRate * bb.TickDelta);
            body.HealBodyParts(PartHealRate * bb.TickDelta);

            SelfTend(brain, bb, body);
        }

        // 회복이 끝나도 일어나지 않는다 — 배정이 곧 "쉬는 중"이라는 명령.
        return BTStatus.Running;
    }

    // ── 자가치유 ─────────────────────────────────────────────────────────────
    //
    // **여기 있는 이유**: BT 순서가 `Rest → … → Work`라, 침대에 배정된 폰은 이 노드에서
    // Running으로 멈춰 `WorkNode`까지 가지도 않는다. 자가치유를 일감으로 만들면 **아무도 못
    // 집는 일감**이 된다. 림월드도 자가치유는 누운 채로 한다.
    //
    // **약은 제 배낭에서만 나온다.** 누워 있는 사람이 창고까지 손을 뻗을 수는 없다 —
    // 그게 의사를 부르는 이유의 절반이다(의사는 챙겨서 온다, `TreatJob`).
    // 회복량·속도·약 고르기는 의사와 **같은 규칙**(`Treatment`)을 쓰되 `self: true`가 붙는다.
    float _tendEndTime;
    MedicineData _tending;

    void SelfTend(UnitBrain brain, UnitBlackboard bb, BodyPartSystem body)
    {
        if (brain == null || brain.Work == null || !brain.Work.SelfTend) return;
        if (!body.NeedsTreatment) { _tendEndTime = 0f; return; }

        float skill = brain.Stats != null ? brain.Stats.Medicine : 0f;

        // 한 회차 시작 — 무엇으로 할지는 **시작할 때** 정한다(도중에 바뀌면 걸린 시간과
        // 실제 쓴 약이 어긋난다).
        if (_tendEndTime <= 0f)
        {
            _tending     = Treatment.BestInBackpack(brain);
            _tendEndTime = Time.time + Treatment.Seconds(_tending, skill, self: true);
            return;
        }

        if (Time.time < _tendEndTime) return;

        var used  = Treatment.ConsumeFromBackpack(brain, _tending) ? _tending : null;
        float pow = Treatment.Power(used, skill, self: true);
        Treatment.Apply(body, pow);

        if (brain.Stats != null)
            brain.Stats.GrowStat(StatName.Medicine, 6f * CombatXp.Sol(skill));

        Debug.Log($"[SelfTend] {brain.UnitName} 자가치유 " +
                  $"({(used != null ? used.ItemName : "맨손")}, 배율 {pow:F2})");

        _tendEndTime = 0f;   // 다음 회차
    }

    void LeaveBed(UnitBlackboard bb)
    {
        if (bb.Mover != null) bb.Mover.SetSleeping(false);
        if (_bed == null) return;
        _bed.Vacate(bb.Brain);
        _bed = null;
        _tendEndTime = 0f;   // 일어나면 하던 회차는 없던 일이다
    }
}

// 허기: 허기가 낮으면 음식을 찾아 먹는다
public class EatNode : BTNode
{
    // **창고는 손만 닿으면 된다**(`JobBase.HandReach`) — 일감들과 같은 규약을 쓴다.
    const float ArrivalDist = JobBase.HandReach;

    FoodData _food;
    Storage  _sourceStorage;
    Vector3  _approach;      // 창고 **앞의 설 수 있는 칸**(창고 자기 칸이 아니다)
    Storage  _eatFrom;       // 창고에서 바로 먹는 중이면 그 창고 (비면 배낭에서 먹는다)
    float    _eatTimer;
    bool     _hasFood; // true = 먹을 것을 확보함, 먹는 단계

    public override BTStatus Tick(UnitBlackboard bb)
    {
        // **임계도 고르는 규칙도 `Feeding`이 답한다** — 먹는 길이 둘이라(제 발로 먹는 것과
        // 남이 먹여 주는 것) 여기 따로 적으면 "포로만 다른 것을 먹는" 꼴이 된다.
        if (!Feeding.IsHungry(bb.Brain)) { Reset(); return BTStatus.Failure; }

        var brain = bb.Brain;

        if (!_hasFood)
        {
            // 1) 인벤토리에 음식 있으면 바로 먹기
            var invFood = Feeding.BestInBackpack(brain, bb.Hunger);
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
                    _food = Feeding.BestInStorage(bb.Hunger, bb.Self.transform.position,
                                                  out _sourceStorage);
                    if (_sourceStorage == null) return BTStatus.Failure;

                    // **창고 자기 칸이 아니라 폰이 서는 자리로 간다**(`WorkSpot`) — 창고는
                    // 자기 칸을 막으므로 중심을 겨누면 영영 못 닿는다. 2×2 창고는 가장 가까운
                    // 설 수 있는 칸까지가 1.5m를 넘어서, 그 앞에 서 놓고도 굶었다.
                    // 모든 일감이 이미 이 규약을 쓴다.
                    _approach = MapManager.Instance != null
                              ? MapManager.Instance.TileToWorld(WorkSpot.TileFor(_sourceStorage))
                              : _sourceStorage.transform.position;
                    bb.Mover.SetMoveTarget(_approach);
                }

                // **평면 거리로 잰다** — `TileToWorld`는 Y=0(길찾기용)이라 폰의 키가
                // 그대로 거리에 얹힌다.
                var self = bb.Self.transform.position;
                float dist = Vector2.Distance(new Vector2(self.x, self.z),
                                              new Vector2(_approach.x, _approach.z));
                if (dist > ArrivalDist) return BTStatus.Running;

                // 창고 도착 → **여기서 바로 먹는다. 배낭에 넣지 않는다.**
                //
                // 폰은 창고 앞에 서 있다 — 한 끼를 배낭에 넣었다 그 자리에서 도로 꺼내는
                // 것은 아무것도 아닌 일이고, 그 왕복이 사고를 셋 만든다:
                //  ① 자리가 없으면 꺼냈다 도로 넣기를 **BT 틱마다** 되풀이한다(전엔 그랬다).
                //  ② 배낭이 바뀌므로 `UpdateUnload`가 **방금 먹으려고 꺼낸 밥**을 창고로
                //     되돌릴 일감을 건다.
                //  ③ 먹다 말고 명령을 받으면 밥 한 개가 배낭에 남아 따로 정리해야 한다.
                //
                // 창고에 그대로 두면 셋 다 없어진다. 먹는 순간에 빠지므로(`ConsumeFrom`)
                // 그새 남이 가져갔으면 그때 접고 다시 고른다.
                _eatFrom = _sourceStorage;
                _hasFood = true;
                bb.Mover.StopMovement();
            }
        }

        // **창고에서 바로 먹는 중이면 계속 그 앞에 있어야 한다.**
        //
        // 도착 판정은 `_hasFood`가 거짓일 때만 돌므로, 한 번 먹기 시작하면 거리를 다시 안 봤다.
        // 배낭에서 먹을 때는 무해했지만(음식이 몸에 있다) 창고에서 먹게 되면서 길이 생겼다 —
        // 명령을 받아 걸어간 폰이 다음에 `EatNode`로 돌아왔을 때 **맵 반대편 창고에서 원격으로
        // 먹는다**(상위 분기가 도는 동안 이 노드는 `Reset`될 기회가 없다).
        if (_eatFrom != null)
        {
            var here = bb.Self.transform.position;
            float away = Vector2.Distance(new Vector2(here.x, here.z),
                                          new Vector2(_approach.x, _approach.z));
            if (away > ArrivalDist) { Reset(); return BTStatus.Failure; }
        }

        // 먹는 중
        _eatTimer += bb.TickDelta;
        if (_eatTimer < _food.EatTime) return BTStatus.Running;

        // 빼는 것도 허기를 채우는 것도 `Feeding`이 한 곳에서 한다.
        // **먹은 자리에서 뺀다** — 배낭에 못 넣었으면 창고에서 바로 먹은 것이다.
        var from = _eatFrom != null ? _eatFrom.Inventory : brain.Inventory;
        if (!Feeding.ConsumeFrom(from, _food)) { Reset(); return BTStatus.Failure; }

        Feeding.Eat(brain, _food);
        Debug.Log($"[EatNode] {bb.Self.name} 섭취 완료 — {_food.ItemName} (허기 {bb.Hunger:F0}/100)");
        Reset();
        return BTStatus.Success;
    }

    void Reset()
    {
        _food = null; _sourceStorage = null; _eatFrom = null;
        _approach = Vector3.zero; _eatTimer = 0f; _hasFood = false;
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