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
    public override BTStatus Tick(UnitBlackboard bb)
    {
        Vector3 selfPos = bb.Self.transform.position;

        // **도발이 표적 선정을 덮어쓴다.** 새 전투 규칙이 아니라 **이 한 곳의 답을 갈아
        // 끼우는 것**이다(`FinishTarget`이 자동 표적에 예외를 뚫은 것과 같은 자리).
        //
        // **맨 위여야 한다** — 아래의 "더 가까운 적으로 갈아탄다"보다 뒤에 두면, 탱커가
        // 도발한 순간 그 옆의 다른 적이 더 가깝다는 이유로 **한 틱 만에 풀린다.** 붙들어
        // 두는 것이 도발의 전부라 그러면 능력이 아무것도 안 한 것이 된다.
        //
        // 만료·죽음·기절은 `Taunt.HolderOf`가 한꺼번에 본다.
        var holder = Taunt.HolderOf(bb);
        if (holder != null)
        {
            bb.Target = holder;
            return BTStatus.Success;
        }

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
        //
        // **소집하면 반격 전용이라도 먼저 친다.** 소집은 "싸울 태세로 서 있어라"라는 명령이라
        // 맞을 때까지 기다리는 것은 그 뜻이 아니다 — 이 한 줄이 없으면 소집한 폰과 안 한 폰이
        // 전투에서 똑같이 군다(`DraftedNode`가 `Running`만 돌려 **반격조차 안 하던** 옛 구조의
        // 반대편 실수다).
        //
        // **대신 목줄이 짧다**(`DraftLeash`). 감지 범위(10m)를 그대로 쓰면 세워 둔 폰이 표적을
        // 찾아 걸어 나가서 **진형을 잡으려고 소집한 뜻이 사라진다** — 소집은 *자리를 지키며
        // 싸우라*는 뜻이다. 다가온 것만 친다.
        //
        // **어두우면 덜 훑는다**(`Vision`) — 밤에는 적이 코앞까지 와야 보인다. 밤눈이 있는
        // 종족은 그만큼 덜 줄어든다.
        // **먼저 거는가는 성향이 답한다**(2026-08-26, 사용자 결정). 전에는 트리마다 박아 둔
        // `retaliateOnly` 플래그가 정했는데, 그래서 **P패널의 성향이 방랑자에게 안 먹혔다** —
        // `교전`을 골라도 `자동`과 똑같이 반격만 했다(넷 중 `도주`만 `PlayerFleeNode`를 통해
        // 따로 먹혔다). 플래그를 지우고 성향 하나로 모은다.
        bool initiates = bb.Brain != null
                      && bb.Brain.EffectiveStance != CombatStance.Retaliate
                      && bb.Brain.EffectiveStance != CombatStance.Flee;

        // **소집은 반대다 — 자리를 지키며 반격만 한다**(2026-08-26, 사용자 결정으로 뒤집었다).
        //
        // 08-20에는 *"소집은 싸울 태세라 먼저 친다"*로 두었는데, 그러면 **세워 둔 폰이 표적을
        // 찾아 걸어 나가** 진형을 잡으려던 뜻이 사라진다. 소집의 뜻은 *"내가 조종할 때까지
        // 그 자리에 있어라"*다 — 먼저 거는 것은 성향이 할 일이고, 소집은 그것을 **끈다.**
        if (bb.IsDrafted) initiates = false;

        // **맞은 뒤에는 성향도 소집도 훑기를 막지 않는다.** 이미 붙은 싸움에서 손을 놓는 것은
        // 어느 성향의 뜻도 아니다 — 그전에는 때린 놈이 쓰러지는 순간 `Target`이 비고 새 후보를
        // 안 훑어서, **옆에 적을 두고 멍하니 서서 맞았다**(사용자 검증에서 드러났다).
        //
        // **그리고 15초로는 모자랐다.** `HasFreshAttacker`만 보면 때린 놈을 눕히고 15초가
        // 지나는 순간 다시 손을 놓는다 — 그건 *맞은 기억*의 길이지 **싸움의 길이가 아니다.**
        // `Engaged`가 붙는 순간부터 **주위가 빌 때까지** 이어진다.
        //
        // **드는 길이 둘이다 — 맞았거나, 치고 있거나**(2026-08-26). 한동안 *맞았을 때*만
        // 적어 두었는데, 그러면 **내가 먼저 친 싸움이 교전으로 안 쳐진다**: 우클릭으로 지목해
        // 한 놈을 눕힌 폰이 (한 대도 안 맞았으면) 그 자리에서 손을 놓고 일하러 갔다. 진단
        // 로그가 정확히 그 모양이었다 — `맞은놈=없음 Engaged=False`인데 적이 1.2m 앞에 있었다.
        //
        // **표적을 들고 있다는 것 자체가 교전이다** — 플레이어가 지목했든 반격으로 붙었든,
        // 그 폰은 이미 싸우는 중이다.
        if (bb.HasFreshAttacker || current != null) bb.Engaged = true;

        bool scan  = (initiates || bb.Engaged) && myFaction != null;

        // **소집 중에는 목줄이 짧다** — 다가온 것과 그 곁만 문다. 자리를 지키라는 명령이므로
        // 반격으로 붙은 싸움이어도 표적을 찾아 멀리 나가면 안 된다.
        float range = Mathf.Min(Vision.RangeFor(bb),
                                bb.IsDrafted ? bb.AttackRange * DraftLeash : float.MaxValue);

        GameObject best = scan
            ? FactionScan.NearestHostile(bb.Self, myFaction, range, theirsToMine: false)
            : null;
        float bestDistSqr = best != null ? SqrDist(selfPos, best) : float.MaxValue;

        // 반격 대상 — 중립이라 위 스캔에 안 걸리지만 대응해야 한다. 거리 비교에 함께 넣는다.
        if (bb.HasFreshAttacker && BTTargetUtil.IsAttackable(bb.LastAttacker))
        {
            float d = SqrDist(selfPos, bb.LastAttacker);
            // **반격 거리는 밤에도 안 줄인다** — 맞은 것을 아는 것은 눈이 아니라 아픔이다.
            // 여기까지 `Vision`을 걸면 어두울 때 등 뒤에서 맞고도 누가 때렸는지 모르는 채로
            // 서 있게 되는데, 그건 밤이 위험한 것이 아니라 폰이 고장 난 것으로 보인다.
            float limit = bb.DetectRange * 2f;   // 원거리 저격수를 맵 끝까지 쫓지는 않는다
            if (d <= limit * limit && d < bestDistSqr) { bestDistSqr = d; best = bb.LastAttacker; }
        }

        if (best == null)
        {
            bb.Target = current;   // 새 후보가 없으면 기존 타겟 유지(추격 중 놓치지 않게)

            // **훑어 봤는데 아무도 없으면 싸움이 끝난 것이다.** `scan`이 꺼져 있을 때는
            // 훑지를 않았으므로 *없다*고 말할 근거가 없다 — 그대로 둔다.
            if (scan && current == null) bb.Engaged = false;

            return current != null ? BTStatus.Success : BTStatus.Failure;
        }

        // 기존 타겟보다 확실히 가까울 때만 교체 (SwitchMargin 배 이내면 그대로 — 타겟 흔들림 방지)
        bb.Target = (current == null || bestDistSqr * SwitchMargin < currentDistSqr) ? best : current;
        return BTStatus.Success;
    }

    const float SwitchMargin = 1.5f;   // 새 후보가 기존 타겟보다 이 배수만큼 가까워야 갈아탄다

    // 소집한 폰이 저 혼자 붙는 거리 — 공격 사거리의 이 배수. 근접(1.5m)이면 4.5m라
    // **한두 걸음**이다. 키우면 소집이 "돌격"이 되고, 1이면 사실상 반격과 같아진다.
    // **소집 중의 목줄.** 감지에도 쓰고 `FightNode`의 접근에도 쓴다 — **한 값이어야 한다**:
    // 갈리면 *볼 수는 있는데 못 가는* 또는 *안 보이는데 쫓아가는* 폰이 생긴다.
    public const float DraftLeash = 3f;

    static float SqrDist(Vector3 from, GameObject go) =>
        (go.transform.position - from).sqrMagnitude;
}

// 플레이어 직접 이동 명령 — 도착할 때까지 자동 행동(전투/휴식/작업)을 막는다.
// 소집(Draft) 바로 아래에 두어 "내가 시킨 게 최우선"이 되게 한다.
public class ManualOrderNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        // **도발이 명령보다 위다**(2026-09-08). 여기서 실패를 돌려주면 아래 전투 갈래가 받아
        // 붙든 놈에게 간다. **`ManualMove`도 끈다** — 안 끄면 도발이 풀리는 순간 **8초 전에
        // 시킨 곳으로 저 혼자 걸어간다**(명령이 살아 있던 것과 같다).
        if (bb.Brain != null && bb.Brain.IsHeldByTaunt)
        {
            bb.ManualMove   = false;
            bb.HoldPosition = false;
            return BTStatus.Failure;
        }

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
                                      out var toSite))
                return BTStatus.Failure;               // 빈 자리가 없다 — 업은 채로 기다린다

            bb.HasPutDown  = true;
            bb.PutDownSite = toSite;
            bb.PutDownAt   = toSite.PutPosition;
        }

        // 목적지가 사라졌으면(침대 철거·우리 파괴) 발밑에 내려놓고 끝낸다 — 업은 채로
        // 영영 서 있는 것보다 낫다. **유니티의 `== null`을 태우려면 인터페이스가 아니라
        // `Object`로 물어야 한다**(파괴된 컴포넌트는 인터페이스 참조로는 null이 아니다).
        var site = SiteOrNull(bb.PutDownSite);
        Vector3 spot = site != null ? site.PutPosition : bb.PutDownAt;

        if (Vector3.Distance(bb.Self.transform.position, spot) > ReachDistance)
        {
            bb.Mover.SetMoveTarget(spot);
            return BTStatus.Running;
        }

        bb.Mover.StopMovement();

        var carried = bb.Carrying;

        bb.HasPutDown  = false;
        bb.PutDownSite = null;

        // **자리가 남아 있는지 도착해서 다시 본다.** 선택지를 고를 때는 비어 있었어도, 걸어오는
        // 사이에 다른 폰이 먼저 넣었을 수 있다 — 안 보면 **한 우리에 여럿이 겹쳐 선다**
        // (등록은 하나만 되고 나머지는 갇히지도 않은 채 그 자리에 서 있는다).
        // 자리가 찼거나, **그 자리가 이 사람을 안 받거나**(보통 침대 × 붙잡은 사람).
        if (site != null && (site.IsFull || !site.Accepts(carried)))
        {
            // 자리가 없으면 **거기 두지 않는다.** 업은 폰 발밑에 내려놓고 알린다 —
            // 겹쳐 세우면 무슨 일이 일어난 건지 화면에 안 남는다.
            PawnCarry.PutDown(self);
            EventNotificationUI.Notify("자리 없음",
                $"{site.SiteName}이(가) 이미 차 있어 " +
                $"{carried?.UnitName}을(를) 그 앞에 내려놓았습니다.", holdWhenHidden: true);
            return BTStatus.Failure;
        }

        // 어느 쪽을 보게 놓을지는 **자리가 정한다**(침대의 `SleepRotation` — 안 정하면 발이
        // 베개를 벤다). 맨바닥이면 `null`이라 업은 폰이 보던 방향 그대로다.
        PawnCarry.PutDown(self, spot, site?.PutRotation);
        if (carried == null) return BTStatus.Failure;

        // **놓는 자리가 곧 그다음 상태다.** 우리·죄수 침대면 갇히고, 보통 침대면 배정만
        // 해 준다(그러면 `RestNode`가 회복을 맡는다). 무덤이면 묻힌다. 맨바닥이면 그냥
        // 누워 있는다. **점유·배정·갇힘을 자리가 한 번에 세운다** — 여기서 따로 부르면
        // 자리 종류가 늘 때 하나가 빠진다.
        site?.Put(carried);

        return BTStatus.Success;
    }

    // 유니티의 `== null`은 **인터페이스 참조로는 안 통한다** — 파괴된 컴포넌트도 인터페이스로
    // 들고 있으면 `null`이 아니다(`MissingReferenceException`은 만질 때 터진다). `Object`로
    // 되돌려 물어야 한다.
    static ICarrySite SiteOrNull(ICarrySite site)
        => site is UnityEngine.Object o && o == null ? null : site;
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
// **스스로 도는 폰이 능동을 쓰는 자리**(2026-09-07 — `docs/design_combat_rework.md`의 마지막
// 미결이 여기였다: *"적도 능동을 쓰나"*).
//
// **그릇은 처음부터 다 있었다** — `AbilityUser`를 `UnitBrain.Awake`가 세력을 안 가리고 모든
// 폰에 붙이고, 적 로드아웃 여섯이 이미 도발 방패를 든다. **없던 것은 판단 하나**였다.
//
// **가르는 축은 `UnitBrain.AbilityByHand` 하나다 — 사람이 눌러 주는 폰인가.**
//   · 눌러 주는 폰(내 방랑자)은 **여기서 손대지 않는다.** AI가 대신 쓰면 쿨다운을 태워
//     **누르려던 순간에 회색**이고, 그건 직접 제어가 아니다.
//   · 나머지(적·경비·용병·**하수인**)는 **스스로 쓴다.** 하수인이 여기 걸리는 것이 특히
//     중요하다 — `AbilityController`가 걸러서 **사람이 눌러 줄 길이 아예 없다.**
//
// **`AcceptsOrders`로 가르면 안 된다**(2026-09-07에 그렇게 짰다가 잡았다) — 그건 *하수인
// 종족인가*라서 **적도 참이고**, 그러면 만들려던 것이 통째로 안 걸린다.
//
// **늘 성공을 돌려준다.** 시퀀스 안에서 전투 **앞**에 서기 때문이다 — 실패를 돌려주면
// *능력이 없다*는 이유로 시퀀스가 끊겨 **싸움이 통째로 멎는다.** 하는 일이 있으면 하고
// 없으면 비켜 주는, 곁다리 노드다.
public class UseAbilityNode : BTNode
{
    AbilityUser _user;
    bool        _looked;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        if (bb == null || bb.Self == null) return BTStatus.Success;

        var brain = bb.Brain;
        if (brain == null || brain.AbilityByHand) return BTStatus.Success;

        // `AbilityUser`는 `Awake`가 붙이므로 틱이 도는 시점엔 반드시 있다. 한 번만 찾는다.
        if (!_looked) { _looked = true; _user = bb.Self.GetComponent<AbilityUser>(); }

        // 쓸 수 있고 쓸 만한 것이 있으면 쓴다. **무엇이 쓸 만한가는 능력의 주인이 답한다**
        // (`Taunt.Worth`) — 여기서 물으면 능력이 늘 때마다 BT가 전투 규칙을 알게 된다.
        if (_user != null) _user.TryUseAny();

        return BTStatus.Success;
    }
}

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
            bb.Target        = null;
            bb.FinishTarget  = null;   // 끝났으면 예외도 같이 거둔다(다음 대상에 새지 않게)
            bb.OrderedTarget = null;   // 명령도 그 표적과 함께 끝난다
            combat.ExitCombat();
            return BTStatus.Failure;
        }

        float dist = Vector3.Distance(bb.Self.transform.position,
                                       bb.Target.transform.position);

        // 공격 범위 밖이면 이동
        if (dist > bb.AttackRange)
        {
            combat.ExitCombat();
            bb.Mover.SetMoveTarget(Approach(bb));
            return BTStatus.Running;
        }

        // 공격 범위 안이면 공격
        bb.Mover.StopMovement();
        combat.EnterCombat();
        return BTStatus.Running;
    }

    // **어디까지 다가가나 — 소집 중에는 자리를 지킨다**(2026-09-08).
    //
    // 진형이 자리를 나눠 주기만 하고 지키게 하지 않으면 **첫 접촉에서 대형이 녹는다**:
    // 표적을 문 폰이 저마다 그것을 쫓아 걸어 나가므로, 대형은 *출발 직전의 그림*이 된다.
    //
    // **`DetectNode.DraftLeash`와 같은 값을 쓴다** — 감지가 이미 *다가온 것만 문다*로
    // 잘라 두었으므로, 접근도 같은 눈금이어야 **볼 수 있는 것에는 닿는다**가 성립한다.
    // 갈리면 *보이는데 못 가는* 표적이 생겨 폰이 그 자리에서 헛돈다.
    //
    // **소집이 아니면 한 줄도 안 바뀐다** — 평소 전투는 예전 그대로 표적까지 간다.
    static Vector3 Approach(UnitBlackboard bb)
    {
        var to = bb.Target.transform.position;
        if (!bb.IsDrafted) return to;

        // **사람이 지목한 표적은 목줄을 안 탄다** — 명령이 진형보다 위다. 안 그러면
        // 소집해 둔 폰에게 20m 밖의 적을 치라고 시켜도 **몇 걸음 나가다 서서** 명령이
        // 안 먹는 것처럼 보인다.
        if (bb.OrderedTarget != null && bb.OrderedTarget == bb.Target) return to;

        // 자리를 모르면(소집 밖에서 켜진 표적 등) 지금 선 자리가 곧 자리다.
        var spot = bb.FormationSpot;
        if (spot == Vector3.zero) spot = bb.Self.transform.position;

        var d = to - spot;
        d.y = 0f;

        float leash = bb.AttackRange * DetectNode.DraftLeash;
        return d.magnitude <= leash ? to : spot + d.normalized * leash;
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
    const float StuckSeconds  = 6f;     // 이만큼 **경로가 안 줄면** 포기
    const float ProgressEpsilon = 0.5f; // 이만큼 줄어야 "가고 있다"고 본다

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
    // **`JobBase.NodeReach`가 답한다** — 배정으로 캐는 여기와 찍어서 캐는 일감이 같은
    // 거리에서 서야 한다.
    const float NodeReach = JobBase.NodeReach;

    float      _stuckTimer;
    float      _bestLeft = float.MaxValue;
    Vector2Int _progressFor = new Vector2Int(int.MinValue, int.MinValue);

    // **어느 길을 재고 있었나**(2026-09-06). 길이 바뀌면 기록도 버린다 — 아래 `Tick` 참조.
    int        _progressRoute = int.MinValue;

    void ResetProgress()
    {
        _stuckTimer    = 0f;
        _bestLeft      = float.MaxValue;
        _progressRoute = int.MinValue;
    }

    public override BTStatus Tick(UnitBlackboard bb)
    {
        // **업고 있으면 일을 안 한다.**
        //
        // 한 손이 사람으로 막혀 있고(양손 무기·방패를 못 드는 것과 **같은 축**이다), 무엇보다
        // 어깨에 시신을 얹은 채 광맥을 캐러 맵을 가로지르는 그림이 된다. 업기는 그 자체로
        // 명령이라, 업고 있는 동안 그 폰이 하는 일은 **옮기는 것**이다.
        //
        // 여기까지 흘러오는 이유: `CarryPawnNode`는 **갈 곳이 없으면 Failure**를 돌려준다
        // (*"간수가 꺼져 있으면 그냥 업고 있는다"*). 그 뒤로 트리가 그대로 Work까지 흘렀다.
        //
        // **하던 일감은 여기서 안 놓는다** — 놓는 자리는 업기가 시작되는 문 하나뿐이다
        // (`PawnCarry.TryPickUp`). 자리로 데려가는 동안에는 `CarryPawnNode`가 Running을 물고
        // 있어 이 노드가 **아예 안 돌므로**, 여기서만 놓으면 예약된 일감이 **다른 폰도 못
        // 가져가는 채로** 남는다.
        //
        // **배정된 자원 노드는 거둔다**(`AbandonWork`) — 한때 여기 *"안 거둔다"*고 적혀
        // 있었지만 코드는 늘 거두고 있었고, **거두는 쪽이 맞다**: 배정도 플레이어가 붙인
        // 명령이라 **새 명령이 옛 명령을 덮어쓴다**(이동 명령이 `PutDownSite`·`FollowTarget`을
        // 거두는 것과 같은 자리). *"업고 나면 하던 바위로 돌아간다"*를 만들면, 심부름을 시킬
        // 때마다 그 폰이 무엇을 하던 중이었는지를 기억하고 있어야 한다.
        //
        // **소집만 예외다** — 그건 명령이 아니라 **상태**(켜고 끄는 스위치)라, 끄면 이전으로
        // 돌아가는 것이 그 스위치의 뜻이다. 그래서 소집은 `DropCurrentJob`을 쓴다.
        // **소집 중에는 일을 안 집는다** — 소집이 곧 "지금은 그것 말고 싸울 준비"라는 명령이다
        // (업고 있을 때와 **같은 자리·같은 이유**: 그 폰이 지금 하는 일은 그것이 아니다).
        // 하던 일감은 `UnitBrain.Draft()`가 `DropCurrentJob`으로 놓는다 — 여기서 게이트만
        // 걸면 그 일감이 예약된 채 얼어붙어 **다른 폰도 못 가져간다.** 배정은 남는다.
        //
        // **다만 지금 든 일감이 그 업기를 시킨 것이면 계속 돈다.** 업는 순간 하던 일감은
        // 놓이므로(`PawnCarry.TryPickUp`) 업고 있는데 `CurrentJob`이 남아 있다는 것은
        // **그 일감이 업으라고 시켰다**는 뜻뿐이다(`ButcherJob`이 시체를 도축대로 업어 온다).
        // 여기서 막으면 그 일감은 다시 못 돌고, `CarryPawnNode`는 갈 곳이 없어 Failure를
        // 돌려주므로 **폰이 시체를 든 채 영영 서 있는다.**
        if (bb.IsDrafted) return BTStatus.Failure;
        if (bb.Carrying != null && bb.CurrentJob == null) return BTStatus.Failure;

        // 배정된 노드가 있으면 우선 처리.
        //
        // **저 혼자 잡은 것만 작업 설정을 따른다.** 손으로 붙인 것은 *"너는 저기를 캐라"*라는
        // 명령이라 P패널 on/off를 안 본다(`[치료한다]`가 설정을 안 보는 것과 같은 규칙).
        // 자율 배정된 것까지 안 따르면 **끄는 스위치가 거짓말이 된다** — 껐는데도 계속 캔다.
        if (bb.AssignedNode != null)
        {
            if (bb.NodeAutoClaimed && !StillAllowed(bb))
            {
                bb.AssignedNode.Unassign(bb.Brain);
                bb.NodeAutoClaimed = false;
                return BTStatus.Failure;
            }
            return TickAssigned(bb);
        }

        // 기존 JobManager 경로
        if (bb.CurrentJob == null)
        {
            var brain = bb.Brain;
            var job   = JobManager.Instance.RequestJob(brain);

            // **큐가 비면 빈 광맥을 잡는다** — 자율 배정(`ResourceNode.ClaimFreeFor`).
            //
            // 여기가 곧 "우선순위 맨 끝"이다. `QueueOrder`에는 안 넣는다 — 채굴·벌목은
            // **일감을 만들지 않으므로**(`TickAssigned`가 노드를 직접 캔다) 그 배열에 적으면
            // *"큐에서 배정되는 카테고리"*라는 그 배열의 뜻이 거짓이 된다. 큐를 먼저 비우고
            // 나서 도는 것만으로 "캐 놓은 것을 나르는 일이 캐는 일보다 앞선다"가 성립한다.
            if (job == null)
            {
                if (ResourceNode.ClaimFreeFor(brain) != null)
                {
                    ResetProgress();
                    return BTStatus.Running;   // 다음 틱부터 `TickAssigned`가 데려간다
                }
                return BTStatus.Failure;
            }
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

        // **목표 칸이 바뀌면 진행 기록을 버린다**(2026-09-05).
        //
        // 운반은 **한 일감 안에서 목표를 바꾼다** — 짐을 집으면 `TileCoord`가 창고로 간다
        // (`HaulJob` 2단계). 그런데 진행 기록(`_bestLeft`)은 **새 일감일 때만** 지워졌다:
        // 물건 앞에 도착해 기록이 0에 가까워진 채로 목표가 50m 밖 창고로 바뀌니, 그 뒤로는
        // **어떤 값도 기록보다 작을 수 없어** 6초 뒤 반드시 *"갈 수 없다"*가 됐다.
        //
        // 그리고 놓는 것은 **취소**라 `OnCancelled`가 짐을 바닥에 떨군다 — 그래서 짐꾼이
        // **집었다 떨구고 다시 집기를** 되풀이했다(사용자가 본 그 그림이다). 6초라는 주기가
        // 그대로 로그에 찍혀 있었다.
        //
        // `_settled`는 같은 이유로 이미 `_settledFor`에 칸을 들고 있었다 — **그 짝을 진행
        // 기록에는 안 달아 둔 것**이 이 사고의 전부다.
        if (_progressFor != currentJob.TileCoord)
        {
            ResetProgress();
            _progressFor = currentJob.TileCoord;
        }

        var jobCenter = MapManager.Instance.TileToWorld(currentJob.TileCoord);
        var pos       = bb.Self.transform.position;
        float dist    = Vector2.Distance(new Vector2(pos.x, pos.z),
                                         new Vector2(jobCenter.x, jobCenter.z));

        if (!Arrived(dist, currentJob.TileCoord, currentJob.Reach))
        {
            // 이동 중이 아닐 때만 목표 재설정 (이미 이동 중이면 기다림)
            bool moving = bb.Mover.HasTarget();
            if (!moving)
                bb.Mover.SetMoveTarget(TileToWorld(currentJob.TileCoord, bb));

            // **직선이 아니라 그은 선으로 잰다**(2026-09-05, 사용자: *"직선말고 그은 선으로
            // 구분하면 안돼?"*).
            //
            // 전에는 *일감까지의 직선거리*가 줄어야 "가고 있다"로 쳤다. 지상에서는 대개
            // 맞았지만 **굴에서는 틀린 눈금이다** — 폰과 일감 사이가 통째로 바위면 경로가
            // 수십 미터를 **돌아서** 가고, 그동안 직선거리는 줄지 않거나 늘어난다. 그래서
            // 멀쩡히 걷고 있는 폰이 6초마다 *"갈 수 없다"*며 일감을 놓았다(진단에
            // **`로컬경로 있음` · `앞을 막은 것: 없음`**이 같이 찍혀 있었다 — 그 둘이 같이
            // 뜨면 못 가는 것이 아니라 **재는 쪽이 틀린 것**이다).
            //
            // **답은 `PawnMover.RouteRemaining` 하나다** — 라우트를 든 것은 그쪽이고, 여기서
            // 다시 그으면 답이 두 벌이 된다. 라우트가 없는 짧은 이동에서는 그 값이 직선으로
            // 떨어지는데, 그때는 돌아갈 것이 없으므로 **직선이 곧 그은 선이다.**
            //
            // *"움직이기만 하면 통과"*로 두지 않는 이유: 그러면 **제자리에서 맴도는 폰**이
            // 영원히 일감을 붙들고 있는다. 남은 경로는 맴돌면 안 줄어든다.
            //
            // **다시 낸 길이 더 길어도 괜찮다** — 값이 위로 튈 뿐이고, 여기서는 *6초 안에
            // 한 번이라도 줄었나*만 보므로 걷는 동안 저절로 줄어든다.
            // **목표가 없는 틱의 값은 안 믿는다.** 그때 `RouteRemaining`은 0인데, 그것을
            // 최고 기록으로 삼으면 **다시 목표를 준 뒤 어떤 값도 그보다 작을 수 없어**
            // 걷고 있어도 6초 뒤 반드시 놓는다. 시계는 그대로 돌린다 — 목표를 주기만 하고
            // 아무 데도 못 가는 상태가 이어지면 그것도 놓아야 하는 상태다.
            if (!moving) _stuckTimer += bb.TickDelta;
            else
            {
                // **길을 다시 내면 눈금도 다시 낸다**(2026-09-06, 채굴 로그가 답했다).
                //
                // 로그에 `남은경로 110.8m (최고 56.8m)`가 열여섯 폰에게 한꺼번에 찍혔다 —
                // 같은 창의 이동 요약은 `막힘 1~2회 · 포기 0`이었으니 **폰은 멀쩡히 걷고
                // 있었다.** 남은 길이 두 배로 뛴 것이다: `ReportUnreachable`은 광역 덩어리를
                // **20초 닫으므로**(한 폰이 벽 앞에서 배운 것이 그 순간 모두의 라우트를
                // 바꾼다) 다음 라우트가 통째로 우회로 나온다. 그러면 **옛 기록은 다시는 못
                // 깨는 값**이고, 6초 뒤 *반드시* "갈 수 없다"가 된다 — 걸어서 되돌리려면
                // 뛴 만큼(50m 넘게)을 시계 안에 걷어야 하는데 그럴 수가 없다.
                //
                // *"다시 낸 길이 더 길어도 괜찮다 — 걷는 동안 저절로 줄어든다"*라고 적어
                // 두었던 것이 여기서 틀렸다. **줄기는 하는데 기록까지 못 내려간다.**
                //
                // `PawnMover`는 제 시계에 이미 이 짝을 달아 두었다(`TrackGoalGain`의
                // *"새 길에는 새 시계를 준다"*). **밖에서 재는 쪽도 같아야 한다** —
                // `_progressFor`(목표 칸이 바뀌면 버린다)와 정확히 같은 모양이다.
                int   routeNow = bb.Mover.RouteVersion;
                float left     = bb.Mover.RouteRemaining;

                if (routeNow != _progressRoute)
                {
                    // **`MaxValue`로 두지 않는다** — 그러면 다음 틱이 공짜로 기록을 깨서
                    // 시계가 한 번 더 되돌아간다(`PawnMover.ResetProgress`가 같은 이유로
                    // 지금 거리에서 시작한다).
                    _progressRoute = routeNow;
                    _bestLeft      = left;
                    _stuckTimer    = 0f;
                }
                else if (left < _bestLeft - ProgressEpsilon) { _bestLeft = left; _stuckTimer = 0f; }
                else                                          _stuckTimer += bb.TickDelta;
            }

            if (_stuckTimer >= StuckSeconds)
            {
                // **왜 시계가 찼는지를 같이 찍는다**(2026-09-05). *"갈 수 없다"*만으로는
                // **이동이 멎은 것인지**(목표 없음) **가고는 있는데 안 줄어든 것인지**를
                // 가릴 수가 없고, 그 둘은 고쳐야 할 자리가 전혀 다르다. 그리고 **일감 지점과
                // 이동 목표는 다른 값이다**(`SnapToStandable`이 접는다) — 채굴은 일감 지점이
                // *바위 칸 그 자체*라 특히 갈린다.
                var mg = bb.Mover.Goal;
                Debug.LogWarning($"[Work] {bb.Self.name}: '{JobCategoryInfo.DisplayName(currentJob.Category)}' " +
                                 $"일감 지점 {currentJob.TileCoord}까지 갈 수 없어 놓습니다 — " +
                                 $"이동목표 {(bb.Mover.HasTarget() ? "있음" : "**없음**")} " +
                                 $"({mg.x:F1}, {mg.z:F1}) 라우트 {bb.Mover.RoutePoints}점" +
                                 $"(#{bb.Mover.RouteVersion}) · " +
                                 $"남은경로 {bb.Mover.RouteRemaining:F1}m (최고 " +
                                 $"{(_bestLeft == float.MaxValue ? "없음" : _bestLeft.ToString("F1") + "m")}) · " +
                                 $"일감까지 직선 {dist:F1}m");

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

            // **일하는 쪽을 본다**(2026-09-10, 사용자: *"돌을 캐야하는데 허공을 캐"*). 걷는 동안은
            // `PawnMover`가 가는 쪽을 보게 하는데, 멈춰 서서 일할 때는 **아무도 안 돌려 준다** —
            // 마지막으로 걷던 방향 그대로 곡괭이를 휘두른다. 겨누는 문은 `FaceTowards` 하나다
            // (싸우는 중에는 표적이 이기는데, 일하는 폰은 멈춰 있어 다툴 일이 없다).
            bb.Mover.FaceTowards(jobCenter);

            bb.Working = currentJob.Anim;
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

    // 저 혼자 잡은 배정이 아직 유효한가 — 그 작업이 켜져 있고 막히지 않았나.
    // 노드가 답을 든다(`NeededCategory`) — 여기서 `IsTree`를 또 물으면 답이 두 벌이 된다.
    static bool StillAllowed(UnitBlackboard bb)
    {
        var work = bb.Brain != null ? bb.Brain.Work : null;
        if (work == null) return true;   // 설정이 없으면 막을 근거도 없다

        var need = bb.AssignedNode.NeededCategory;
        return work.IsEnabled(need) && !work.IsBlocked(need);
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
            bb.Mover.FaceTowards(nodeCenter);   // 캐는 쪽을 본다 — 일감 쪽과 같은 규칙

            var brain = bb.Brain;
            bool isCutting = node.Data != null && node.Data.IsTree;
            bb.Working = isCutting ? WorkAnim.Chopping : WorkAnim.Mining;
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
    // **부위 회복만 몸의 눈금과 같이 올렸다**(평균 부위 HP 31→100, `BodyPartSystem`) — 안
    // 올리면 회복이 세 배 느려져 한 번 다치면 하루를 누워 있어야 한다. **혈액은 100 그대로라
    // 예전 값 그대로다.**
    const float BloodHealRate  = 2f;    // 초당 혈액 회복
    const float CapRestoreRate = 1f;    // 초당 캡 회복
    const float PartHealRate   = 3f;    // 초당 부위 HP 회복 (자연 회복에 추가로 얹힘)
    const float ArrivalDist    = 1.0f;  // 접근 지점(침대 옆) 기준

    Bed     _bed;
    Vector3 _approach;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        var brain    = bb.Brain;

        // **업고 있으면 눕지 않는다** — 사람을 어깨에 얹은 채 잠자리에 드는 그림이 된다
        // (`WorkNode`가 일감을 막는 것과 **같은 축**이다: 업기는 그 자체로 명령이고, 업고
        // 있는 동안 그 폰이 하는 일은 옮기는 것이다).
        //
        // **배정은 안 거둔다** — 거두면 내려놓고 나서 플레이어가 침대를 다시 붙여 줘야 한다.
        // 미루기만 하므로 **내려놓는 즉시 제 침대로 간다**(배정된 자원 노드를 안 거두는 것과
        // 같은 판단). 자고 있던 폰이 무언가를 업게 되면 **일어나야 한다** — 그래서 `LeaveBed`로
        // 나간다: 누운 자세와 점유를 안 풀면 **업은 채로 침대에 굳고 그 침대를 아무도 못 쓴다.**
        // **소집 중에는 눕지 않는다** — 업고 있을 때와 같은 자리. 자다 깨어 보니 진형이
        // 흐트러져 있으면 소집한 뜻이 없다. **밥은 그대로 먹는다**(`EatNode`는 안 막았다) —
        // 막으면 소집해 둔 것을 잊었을 때 굶어 죽는다(업기가 먹는 것만 열어 둔 그 판단이다).
        // 피로는 안 죽이므로(쓰러질 뿐이다) 막아도 사고가 안 난다.
        if (bb.IsDrafted || bb.Carrying != null) { LeaveBed(bb); return BTStatus.Failure; }

        // **배고프면 일어난다** — 임계는 `Needs`가 답한다(`EatNode`와 같은 값). 여기 숫자를
        // 따로 적으면 "일어나긴 하는데 안 먹는" 또는 그 반대가 생긴다.
        //
        // **잠보다 먼저다.** 굶은 채로 자면 자는 동안 허기가 바닥을 치는데, 자는 폰은 스스로
        // 못 일어나 그대로 죽는다.
        if (Feeding.IsHungry(brain)) { LeaveBed(bb); return BTStatus.Failure; }

        // ── 누울 이유가 둘이다 ───────────────────────────────────────────
        //
        // **졸려서, 그리고 다쳐서.** 배정만으로 눕히던 시절에는 배정이 곧 "지금 누워라"라는
        // 명령이었는데(다친 폰을 눕히는 길), 피로가 생기면서 *언제 눕나*를 몸이 정하게 됐다.
        // 둘을 같이 두지 않으면 회귀한다: 피로만 보면 **다친 폰을 눕힐 길이 사라지고**,
        // 배정만 보면 침대를 준 폰이 하루 종일 누워 있는다.
        //
        bool sleepy = Needs.IsLow(brain, NeedKind.Rest);
        bool hurt   = bb.Body != null && bb.Body.NeedsTreatment;

        var assigned = Bed.FindAssigned(brain);

        // **졸린데 제 침대가 없으면 빈 것을 잡는다.** 사람마다 손으로 배정하게 두면 폰이
        // 늘 때마다 잡일이 늘고, 잊으면 그 폰만 조용히 바닥에서 잔다.
        if (assigned == null && sleepy) assigned = Bed.ClaimFreeFor(brain);

        // **배정이 풀렸으면 먼저 일어난다.** `Bed.Unassign`(우클릭 `[일어난다]`·작업 패널)은
        // **배정만** 거두고 점유도 자세도 안 푼다 — 그건 원래 여기서 `LeaveBed`가 하던 일인데,
        // 피로를 붙이면서 이 줄이 `CollapseIfExhausted`로 바뀌며 사라졌다. 그러면 자던 폰이
        // **영영 그 침대에 누워 있고**(`IsSleeping`이라 아래 첫 가드에 안 걸린다) 점유가 안
        // 풀려 **그 침대를 아무도 못 쓴다** — 게다가 `Bed.Update`는 배정도 없는 그 폰에게
        // 치료 일감을 계속 건다.
        //
        // **`_bed`가 있을 때만 부른다** — 바닥에서 쓰러져 자는 폰도 배정이 없으므로, 조건
        // 없이 부르면 `LeaveBed`의 `SetSleeping(false)`가 매 틱 깨우고 아래에서 다시 눕혀
        // **틱마다 떤다**.
        if (assigned == null)
        {
            if (_bed != null) LeaveBed(bb);
            return CollapseIfExhausted(bb);
        }

        // **자리를 잃었으면 누우러 간 것이 아니다.** 명령을 받아 일어날 때 배정은 남기고
        // 점유만 비우므로(`Bed.RouseFrom`) `_bed`가 남아 있는데, 그대로 두면 아래 `stay`가
        // *"이미 누우러 갔다"*로 읽어 **가득 찰 때까지**를 기준으로 삼는다 — 안 졸린 폰이
        // 명령을 마치자마자 침대로 돌아간다. 놓으면 다시 `sleepy`가 기준이 되고, 필요하면
        // 아래에서 다시 점유한다.
        if (_bed != null && _bed.Occupant != brain) _bed = null;

        // **손으로 붙인 배정은 "가서 쉬어라"라는 명령이다**(`Bed.AutoClaimed`) — 안 졸려도 눕고
        // 뗄 때까지 눕는다. 그것이 다친 동료를 눕히는 길이고, 피로만 보게 뒀더니 **배정해도
        // 안 눕는** 것이 됐다(2026-08-20).
        //
        // **저 혼자 잡은 잠자리는 다르다** — 졸려서 갔으니 가득 채우면 일어나 일하러 간다.
        // 그때 **가는 문턱과 나오는 문턱이 달라야 한다**(`Needs`의 히스테리시스): 하나로 두면
        // 문턱 위에서 **떤다**(자러 가서 조금 차면 "이제 안 졸리다"가 되어 일어나고, 나가면
        // 다시 졸려서 돌아온다 — 실제로 그랬다). 그래서 **이미 누우러 간 뒤라면**(`_bed != null`)
        // 가득 찰 때까지 눕는다.
        //
        // **누워 있을 것인가를 정하는 자리는 여기 하나뿐이다** — 아래에서 또 물으면 두 문턱
        // 중 낮은 쪽이 늘 이겨서 히스테리시스가 도로 사라진다.
        bool ordered = !assigned.AutoClaimed;
        bool stay    = ordered || (_bed != null ? bb.Rest < Needs.WakeAt : sleepy);

        if (!stay && !hurt) { LeaveBed(bb); return BTStatus.Failure; }

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

        // **자면 피로가 찬다** — 얼마나 빨리 차는지는 **그 잠자리가 답한다**
        // (`Bed.RestQuality`: 보통 1.0 · 좋은 침대 1.4 · 바닥은 `Needs.FloorQuality` 0.5).
        // 일어나는 판단은 **위 `stay` 하나**가 한다(다음 틱에 걸린다) — 여기서 또 물으면
        // 문턱이 두 곳이 된다.
        Needs.Fill(bb, NeedKind.Rest, Needs.SleepPerSecond(_bed.RestQuality) * bb.TickDelta);

        // 누워 있는 동안 회복 (캡 감소 없이, 부위 HP 포함)
        var body = bb.Body;
        if (body != null)
        {
            body.HealBlood(BloodHealRate * bb.TickDelta, degradeCap: false);
            body.RestoreBloodCap(CapRestoreRate * bb.TickDelta);
            body.HealBodyParts(PartHealRate * bb.TickDelta);

            SelfTend(brain, bb, body);
        }

        // 회복이 끝나도 여기서는 안 일어난다 — 나오는 판단은 위 `stay` 하나가 한다.
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

    // ── 침대가 없을 때 ───────────────────────────────────────────────────
    //
    // **바닥에 닿으면 그 자리에 쓰러진다.** 피로는 안 죽이는 대신 **못 쓰게 만든다** — 습격
    // 한복판이든 광산 바닥이든 몸이 먼저 무너지고, 그동안 그 폰은 무방비다. 이게 침대를
    // 짓지 않은 값이고, 밤을 새우게 한 값이다.
    //
    // 바닥잠은 회복이 절반이라 **밤새 자도 다 못 채운다**(`Needs.SleepPerSecond`) — 그래서
    // 다음 날 낮에 또 졸리고, 침대가 그 고리를 끊는다.
    BTStatus CollapseIfExhausted(UnitBlackboard bb)
    {
        // **`Mover`를 안 물어보면 그 폰의 BT가 통째로 멎는다** — `LeaveBed`가 바로 아래에서
        // 같은 것을 가드하고 있다(널 참조가 `UnitBrain.Update`까지 올라가는 그 사고).
        if (bb.Mover == null) return BTStatus.Failure;
        if (bb.Rest > Needs.CollapseAt && !bb.Mover.IsSleeping) return BTStatus.Failure;

        // 다 채웠으면 일어나 하던 일로.
        if (bb.Rest >= Needs.WakeAt) { LeaveBed(bb); return BTStatus.Failure; }

        // **선 자리에 그대로 눕는다** — 침대처럼 자리를 고르지 않는다(고를 힘이 남아
        // 있었으면 침대로 갔다). 자세는 침대와 같은 문을 쓴다(`SetSleeping`).
        if (!bb.Mover.IsSleeping)
        {
            bb.Mover.StopMovement();
            bb.Mover.SetSleeping(true, bb.Self.transform.position, bb.Self.transform.rotation);
        }

        Needs.Fill(bb, NeedKind.Rest, Needs.SleepPerSecond(Needs.FloorQuality) * bb.TickDelta);
        bb.RestTicked = true;
        return BTStatus.Running;
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
        float have = bb.Hunger;

        if (!_hasFood)
        {
            // 1) 인벤토리에 음식 있으면 바로 먹기
            var invFood = Feeding.BestInBackpack(brain, have);
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
                    _food = Feeding.BestInStorage(have, bb.Self.transform.position,
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
        Debug.Log($"[EatNode] {bb.Self.name} 섭취 완료 — {_food.ItemName} " +
                  $"(허기 {bb.Hunger:F0}/100)");
        Reset();
        return BTStatus.Success;
    }

    void Reset()
    {
        _food = null; _sourceStorage = null; _eatFrom = null;
        _approach = Vector3.zero; _eatTimer = 0f; _hasFood = false;
    }
}

// **충전 — 골렘은 에너지를, 해골은 사기를 채운다**(2026-09-04).
//
// `EatNode`의 짝이고 자리도 그 옆이다. 갈리는 것은 **아무 데서나 먹을 수 없다**는 것 하나다:
// 밥은 배낭에서도 창고 앞에서도 먹지만, 충전은 **그 건물 앞에 서야 한다.**
//
// ## 산 몸에게는 실패로 흘려보낸다
//
// 트리는 하나이고 몸이 셋이라, 이 노드는 **대부분의 폰에게 첫 줄에서 끝난다**
// (`Recharge.TryAxisOf`). 몸마다 트리를 나누지 않는 이유는 그러면 `BuildMinionTree`가
// 셋이 되고, 전투·일감·순찰이 그 셋에 복사되기 때문이다.
//
// ## 재료가 없으면 — 멈춘 골렘은 서서 기다린다
//
// 임계 아래인데 재료가 없으면 보통은 **실패**로 넘어가 하던 일을 계속한다(유지비를 못 대는
// 것이 곧 정지는 아니다). 다만 **바닥에 닿았으면 `Running`으로 붙든다** — 그것이 "그 자리에
// 멈춘다"의 구현이고, **BT를 끄지 않으므로** 재료가 생기는 순간 제 발로 걸어간다.
// (끄는 길로 만들면 재료가 생겨도 영영 못 가는 데드락이다.)
public class RechargeNode : BTNode
{
    // **손만 닿으면 된다**(`JobBase.HandReach`) — 일감들과 같은 규약. 창고 앞에 서는
    // `EatNode`와 같은 자리다.
    const float ArrivalDist = JobBase.HandReach;

    Recharger _site;
    Vector3   _approach;
    float     _timer;
    bool      _arrived;
    bool      _targetSet;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        var brain = bb.Brain;
        if (!Recharge.TryAxisOf(brain, out var kind)) { Reset(); return BTStatus.Failure; }

        bool stalled = Recharge.Stalled(brain, kind);
        if (!Recharge.IsLow(brain, kind)) { Reset(); return BTStatus.Failure; }

        // **자리를 먼저 고른다** — 없으면 멈춘 폰만 붙든다.
        if (_site == null || !_site.isActiveAndEnabled)
        {
            _site = Recharge.Nearest(kind, bb.Self.transform.position);
            if (_site == null) return Wait(bb, stalled);

            // **건물 자기 칸이 아니라 폰이 서는 자리로 간다**(`WorkSpot`) — 충전대가 제 칸을
            // 막으면 중심을 겨눈 폰은 영영 못 닿는다(`EatNode`가 창고에서 겪은 그 자리).
            _approach = MapManager.Instance != null
                      ? MapManager.Instance.TileToWorld(WorkSpot.TileFor(_site))
                      : _site.transform.position;
            _arrived   = false;
            _targetSet = false;
            _timer     = 0f;
        }

        // **재료를 먼저 보고, 그다음에 목표를 준다**(2026-09-05에 순서를 바로잡았다).
        //
        // 반대로 두었더니 재료가 없는 동안 **매 틱**(10Hz) 자리를 다시 고르고 목표를 준 뒤
        // 곧바로 `Reset`으로 버렸다 — 폰은 아래 `WorkNode`로 내려가 일을 집는데 **이동 목표만
        // 충전대 쪽으로 덮여** 일터가 아니라 충전대로 끌려갔고, 그 사이 광역 A\*가 초당 열 번
        // 돌았다. 재료가 없는 것은 이 기능이 **늘 있으리라 예상하는 상태**라 더 나빴다.
        //
        // **가는 동안에도 다시 본다** — 남이 써 버릴 수 있고, 안 보면 빈 충전대 앞에서
        // 6초를 서 있다 실패한다.
        if (!Recharge.HasFuel(_site, brain)) { Reset(); return Wait(bb, stalled); }

        // **평면 거리로 잰다** — `TileToWorld`는 Y=0(길찾기용)이라 폰의 키가 그대로 얹힌다.
        var self = bb.Self.transform.position;
        float dist = Vector2.Distance(new Vector2(self.x, self.z),
                                      new Vector2(_approach.x, _approach.z));
        if (dist > ArrivalDist)
        {
            // 도착했다가 밀려났으면 처음부터 — 원격으로 충전하지 않는다.
            if (_arrived) { Reset(); return BTStatus.Running; }

            // **목표를 잃었으면 다시 준다**(2026-09-05). 목표를 자리 고를 때 한 번만 주었더니,
            // 위 분기가 이 노드를 밀어냈다 돌아오면(`FightNode`가 `StopMovement`를 부른다)
            // **`_site`가 그대로라 다시 안 주고** `Running`만 돌려줬다 — 에너지는 안 죽이므로
            // 깨울 시계도 없어서 **그 폰이 영원히 선 채로 아래 분기까지 막았다.**
            // (`RestNode`가 침대로 가다 막혔을 때 접근 면을 다시 고르는 것과 같은 자리.)
            if (!_targetSet || !bb.Mover.HasTarget())
            {
                bb.Mover.SetMoveTarget(_approach);
                _targetSet = true;
            }
            return BTStatus.Running;
        }

        if (!_arrived)
        {
            _arrived = true;
            bb.Mover.StopMovement();
        }

        _timer += bb.TickDelta;
        if (_timer < Recharge.Seconds) return BTStatus.Running;

        // **빼는 것과 채우는 것은 붙어 있어야 한다** — 사이에 한 틱이라도 있으면 재료만
        // 사라지고 안 차는 창이 생긴다.
        if (!Recharge.Consume(_site, brain)) { Reset(); return Wait(bb, stalled); }

        Recharge.Fill(brain, kind);
        Debug.Log($"[충전] {bb.Self.name} {Needs.Name(kind)} 충전 완료 — {_site.name}");
        Reset();
        return BTStatus.Success;
    }

    // 채울 수 없을 때. **멈춘 폰은 여기서 붙들린다**(아래 분기가 안 돈다) — 살아 있는
    // 폰이면 그냥 실패시켜 하던 일을 계속하게 둔다.
    BTStatus Wait(UnitBlackboard bb, bool stalled)
    {
        if (!stalled) return BTStatus.Failure;

        bb.Mover?.StopMovement();
        return BTStatus.Running;
    }

    void Reset()
    {
        _site = null; _approach = Vector3.zero; _timer = 0f; _arrived = false; _targetSet = false;
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