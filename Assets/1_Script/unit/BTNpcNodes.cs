using UnityEngine;

// NPC 전용 BT 노드 모음
// (도주 노드 FleeNode는 NPC 외 일반 유닛도 쓰므로 공용 — 아래 정의)

// 나를 적대하는 상대를 감지하면 반대 방향으로 도주.
// NPC(주민)와 CombatStance.Flee/FightWhileHealthy 유닛이 공유한다.
// shouldFlee: null이면 항상 도주, 지정하면 그 조건일 때만(예: 혈액이 임계 아래).
public class FleeNode : BTNode
{
    readonly float _fleeRange;
    readonly float _fleeDist;
    readonly System.Func<UnitBlackboard, bool> _shouldFlee;

    // 도주를 시작한 뒤에는 이 배수 거리까지 벌어져야 그만둔다(히스테리시스).
    // 없으면 감지 범위 경계에서 "도망→멈춤→다시 감지" 를 반복하며 제자리에 서 있는 것처럼 보인다.
    const float ReleaseMult = 2.5f;
    const float RepathInterval = 0.3f;   // 목적지 갱신 주기(초) — 매 틱 재계획하면 길찾기가 요동친다

    bool  _fleeing;
    float _nextRepath;

    public FleeNode(float detectRange, float fleeDistance = 15f,
                    System.Func<UnitBlackboard, bool> shouldFlee = null)
    {
        _fleeRange  = detectRange;
        _fleeDist   = fleeDistance;
        _shouldFlee = shouldFlee;
    }

    public override BTStatus Tick(UnitBlackboard bb)
    {
        if (_shouldFlee != null && !_shouldFlee(bb)) { _fleeing = false; return BTStatus.Failure; }
        if (bb.Mover == null) { _fleeing = false; return BTStatus.Failure; }

        // 이미 도망 중이면 더 넓은 범위까지 위협을 추적한다 — 쫓아오는 한 계속 달린다.
        //
        // **어두우면 늦게 알아챈다**(`Vision`, 교전 탐지와 **같은 한 벌**) — 도주만 밤에
        // 멀쩡하면 마을 사람이 안 보이는 적을 미리 피해 달아난다. 다만 **이미 도망 중일 때는
        // 안 줄인다**: 쫓기는 사람이 어둡다고 추격자를 놓치면 그 자리에 멈춰 서서 잡힌다.
        float fleeRange   = Vision.RangeFor(_fleeRange, bb.NightVision, bb.Self.transform.position);
        float searchRange = _fleeing ? _fleeRange * ReleaseMult : fleeRange;
        var threat = FindThreat(bb, searchRange);

        if (threat == null)
        {
            if (_fleeing) { _fleeing = false; bb.Mover.StopMovement(); }
            return BTStatus.Failure;
        }

        _fleeing = true;

        // 목적지 갱신: 주기적으로 또는 멈춰 섰을 때(막다른 곳에 도달·경로 실패 직후) 즉시 다시 잡는다.
        if (Time.time >= _nextRepath || !bb.Mover.IsMoving())
        {
            _nextRepath = Time.time + RepathInterval;
            if (TryPickFleeSpot(bb, threat, out Vector3 dest))
                bb.Mover.SetDestination(dest);
        }

        return BTStatus.Running;
    }

    GameObject FindThreat(UnitBlackboard bb, float range)
    {
        Vector3 selfPos = bb.Self.transform.position;

        // 가장 가까운 위협 — **나를 적대하는 쪽**(방향별 관계: 내가 상대를 어떻게 보든 무관).
        // 시체·기절·갇힌 것에게서는 도망치지 않는다(`IsAttackable`이 같이 걸러 준다).
        var threat = FactionScan.NearestHostile(bb.Self, bb.Faction, range, theirsToMine: true);
        float bestDistSqr = threat != null
                          ? (threat.transform.position - selfPos).sqrMagnitude : float.MaxValue;

        // 나를 때린 상대는 관계와 무관하게 위협 — 중립 세력에게 선공당한 경우.
        if (bb.HasFreshAttacker && bb.LastAttacker.activeInHierarchy)
        {
            float d = (bb.LastAttacker.transform.position - selfPos).sqrMagnitude;
            if (d <= range * range && d < bestDistSqr) threat = bb.LastAttacker;
        }

        return threat;
    }

    // 위협 반대 방향이 최선이지만 벽·낭떠러지로 막히면 못 간다.
    // 반대 방향에서 좌우로 부채꼴을 그리며 설 수 있는 지점을 찾고, 그래도 없으면 거리를 줄여 다시 시도한다.
    bool TryPickFleeSpot(UnitBlackboard bb, GameObject threat, out Vector3 dest)
    {
        Vector3 selfPos = bb.Self.transform.position;
        Vector3 away    = selfPos - threat.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitSphere;   // 겹쳐 있으면 아무 방향
        away.Normalize();

        var map = MapManager.Instance;
        float[] angles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 110f, -110f };
        float[] scales = { 1f, 0.6f, 0.3f };

        foreach (float scale in scales)
        foreach (float angle in angles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
            var candidate = selfPos + dir * (_fleeDist * scale);
            if (map == null || map.IsStandable(candidate))
            {
                dest = candidate;
                return true;
            }
        }

        dest = default;
        return false;   // 완전히 몰림 — 이동 명령 없이 그 자리에서 버틴다
    }
}

// 홈포인트 주변 랜덤 배회 — "할 일이 없을 때"의 기본 행동. NPC·적 공용(플레이어 유닛은 작업 BT가 따로 있음).
// 홈이 정해지지 않은 유닛(적 등)은 **처음 배회하는 순간의 위치**를 홈으로 잡는다.
// 침공 유닛처럼 스폰 직후 이동 명령을 받은 경우, 도착해서 멈춘 그 자리가 홈이 된다.
public class WanderNode : BTNode
{
    readonly float _radius;
    readonly float _pauseMin;
    readonly float _pauseMax;

    float _waitTimer;
    float _waitDuration;
    bool  _waiting;

    public WanderNode(float radius, float pauseMin = 2f, float pauseMax = 6f)
    {
        _radius   = radius;
        _pauseMin = pauseMin;
        _pauseMax = Mathf.Max(pauseMin, pauseMax);
    }

    const int PickAttempts = 8;   // 한 틱에 시도할 후보 수 (공허/좁은 바닥에서 한 번만 뽑으면 계속 실패)

    public override BTStatus Tick(UnitBlackboard bb)
    {
        if (bb.Mover == null) return BTStatus.Failure;   // 이동 불가 유닛은 다음 분기로

        if (_waiting)
        {
            _waitTimer += bb.TickDelta;
            if (_waitTimer < _waitDuration) return BTStatus.Running;
            _waiting = false;
        }

        // 이동 중이면(배회 목적지든 외부 명령이든) 끝날 때까지 기다린다 — 명령을 덮어쓰지 않는다.
        if (!bb.Mover.IsMoving())
        {
            if (!bb.HomeSet)   // 홈 미설정 유닛은 지금 서 있는 자리를 기준점으로
            {
                bb.HomePosition = bb.Self.transform.position;
                bb.HomeSet      = true;
            }

            if (!TryPickDestination(bb, out Vector3 dest))
                return BTStatus.Failure;   // 갈 곳이 없으면 실패 → 대기 등 다음 분기로 넘긴다

            bb.Mover.SetDestination(dest);

            _waitTimer    = 0f;
            _waitDuration = Random.Range(_pauseMin, _pauseMax);
            _waiting      = true;
        }
        return BTStatus.Running;
    }

    // 홈 주변에서 설 수 있는 지점을 여러 번 뽑아본다. 던전(바닥 위에만 이동 가능)에서
    // 한 번만 추첨하면 대부분 공허에 떨어져 영영 못 움직이는 문제를 막는다.
    bool TryPickDestination(UnitBlackboard bb, out Vector3 dest)
    {
        var map = MapManager.Instance;

        float radius = bb.WanderRadius > 0f ? bb.WanderRadius : _radius;   // 무리는 좁게(`WildSpawner`)
        for (int i = 0; i < PickAttempts; i++)
        {
            Vector2 rnd = Random.insideUnitCircle * radius;
            dest = bb.HomePosition + new Vector3(rnd.x, 0f, rnd.y);
            if (map == null || map.IsStandable(dest)) return true;
        }

        dest = default;
        return false;
    }
}

// EventMerchant: 목적지까지 이동 → 대기 → 홈으로 귀환 → 소멸
// **풀려난 사람이 맵을 떠난다.** 가장자리로 걸어가 사라진다(행상인의 퇴장과 같은 모양).
//
// 성향만 도주로 바꿔서는 놓아 준 것이 아니다 — 우리 폰을 피해 달아나다 구석에서 서성이고,
// 결국 그 자리에 적이 하나 남는다. **살려 보냈으면 실제로 가야** 그 선택에 값이 붙는다.
//
// ── 때리면 어떻게 하나 ────────────────────────────────────────────────────
//
// **떠돌이가 떠날 때를 정한다.** 머문 시간이 `stay`를 넘으면 `LeavingMap`을 켠다.
//
// 켜기만 하고 **늘 `Failure`를 돌려준다** — 걸어 나가는 일은 바로 아래 `LeaveMapNode`가 한다.
// 한 노드로 묶고 싶어지지만, 그러면 "떠나는 중"이 그 노드 안에만 있게 되어 밖에서 떠나게
// 만드는 길(붙잡혔다 풀려나기 등)과 답이 두 벌이 된다. **`UnitBlackboard.LeavingMap` 하나가
// 그 답이어야 한다.**
//
// 시계는 **`bb.TickDelta`로 잰다** — BT는 `tickInterval`(0.1초)마다 도는데 `Time.deltaTime`을
// 누적하면 시간이 여섯 배 이상 느려진다(이 저장소가 전수 교체한 그 사고).
public class DrifterDepartNode : BTNode
{
    readonly float _stay;
    float _elapsed;

    public DrifterDepartNode(float stay) => _stay = Mathf.Max(1f, stay);

    public override BTStatus Tick(UnitBlackboard bb)
    {
        // 이미 떠나는 중이면 시계는 뜻이 없다 — 아래 `LeaveMapNode`가 받는다.
        if (bb.LeavingMap) return BTStatus.Failure;

        // **싸우는 동안은 시간이 안 간다.** 쫓기다가 시간이 차서 "떠난다"로 넘어가면
        // 도망치던 폰이 갑자기 가장자리로 직진한다 — 쫓기는 것과 떠나는 것은 다른 일이다.
        if (bb.HasFreshAttacker) return BTStatus.Failure;

        _elapsed += bb.TickDelta;
        if (_elapsed < _stay) return BTStatus.Failure;

        // 나갈 길이 없으면 **그냥 더 머문다.** 여기서 사라지게 하면 눈앞에서 증발한다.
        if (!EventManager.TryFindExit(bb.Self.transform.position, out var exit))
            return BTStatus.Failure;

        bb.LeavingMap  = true;
        bb.LeaveTarget = exit;
        return BTStatus.Failure;
    }
}

// **싸운다. 확률로 굴리지 않는다.**
//
// 확률이면 "때렸는데 왜 어떤 놈은 반격하고 어떤 놈은 안 하지"가 되고, 그건 플레이어가
// 통제할 수 없는 눈치 싸움이다. 그리고 **살려 보내는 선택의 값은 "안 때리면 안 싸운다"에
// 있다** — 때리면 싸우는 것은 그 선택을 무르는 것이고, 무를 수 있어야 선택이 진짜다.
//
// 무엇보다 **이미 이 게임의 규칙이다**: 반격은 세력 관계와 무관하게 `LastAttacker`로 돈다
// (중립을 때려도 되받아친다). 풀어 준 포로만 예외로 둘 이유가 없다.
//
// 되돌아가는 것도 공짜다 — 15초(`RetaliationMemory`) 동안 안 맞으면 `HasFreshAttacker`가
// 꺼져 **다시 떠난다.** 확률로는 못 만드는 "때리면 돌아서고, 놓아주면 다시 간다"가 저절로 나온다.
public class LeaveMapNode : BTNode
{
    // **가장자리 칸 위에 올라섰을 때** 사라진다. 넉넉히 잡으면 판이 작을수록 맵 한가운데서
    // 사라지는 꼴이 된다 — 칸 반대각선(≈0.71)을 조금 넘는 정도면 그 칸에 선 것이다.
    const float ArrivalDist   = 0.9f;
    const float RepathSeconds = 2f;    // 길이 막혔을 때 다시 잡아 본다

    // **더는 못 가면 포기한다.** 좁혀지지 않는데 계속 걷게 두면, 나가는 길이 반 칸 모자란
    // 지형에서 폰이 가장자리에 붙어 영원히 서 있는다(`WorkNode`의 정체 처리와 같은 축).
    const float StuckSeconds  = 8f;
    const float ProgressEps   = 0.3f;

    float _nextRepath;
    float _stuckTimer;
    float _bestDist = float.MaxValue;
    int   _routeAt  = -1;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        // **떠나는 중이 아니면 진행 기록을 지운다.** 노드는 트리에 계속 살아 있어서, 안 지우면
        // 다시 붙잡혔다 풀려났을 때 지난번의 `_bestDist`가 남아 **출발하자마자 "더는 못 간다"로
        // 사라진다.**
        if (!bb.LeavingMap)
        {
            _bestDist = float.MaxValue; _stuckTimer = 0f; _routeAt = -1;
            bb.Routed = false;
            return BTStatus.Failure;
        }

        // **맞으면 돌아선다 — 다만 패주는 안 돌아선다**(2026-09-08).
        //
        // 이 갈래는 *놓아주어서 나가는 사람*의 것이다: 나가는 길에 맞으면 싸우는 편이 맞다.
        // 무너진 습격대는 **쫓기며 맞는 중**이라 여기 걸리면 후퇴가 첫 틱에 취소되고,
        // 그러면 `RaidLord`가 세 상태를 다 갖춰 놓고도 **끝나는 방법이 몰살뿐**으로 되돌아간다.
        if (bb.HasFreshAttacker && !bb.Routed)
        {
            bb.LeavingMap = false;
            bb.Brain?.ClearStanceOverride();
            bb.Mover?.StopMovement();
            return BTStatus.Failure;
        }

        if (bb.Mover == null) { bb.LeavingMap = false; return BTStatus.Failure; }

        var pos  = bb.Self.transform.position;
        float d  = Vector3.Distance(new Vector3(pos.x, 0f, pos.z),
                                    new Vector3(bb.LeaveTarget.x, 0f, bb.LeaveTarget.z));

        if (d < ArrivalDist) { Object.Destroy(bb.Self); return BTStatus.Running; }

        // **진행은 경로로 잰다 — 직선거리가 아니다**(2026-09-08).
        //
        // 직선으로 재면 **굴에서 틀린다**: 나가는 길이 바위를 크게 돌아가면 걷는 내내
        // 직선거리가 안 줄고, 8초 뒤 *"더는 못 간다"*가 되어 **멀쩡히 걷던 폰이 그 자리에서
        // 사라진다**(사용자: *"도망치지 않았는데 시간이 지나면 사라져"*). `WorkNode`가 먼저
        // 겪고 고친 그 눈금이다 — 답은 `PawnMover.RouteRemaining` 하나다.
        //
        // **길이 바뀌면 눈금도 다시 낸다**(`RouteVersion`) — 우회로가 더 길게 나오면 옛 기록은
        // **다시는 못 깨는 값**이라, 재계획 한 번이 8초 뒤의 포기를 예약한다.
        if (bb.Mover.RouteVersion != _routeAt)
        {
            _routeAt   = bb.Mover.RouteVersion;
            _bestDist  = float.MaxValue;
            _stuckTimer = 0f;
        }

        float gauge = bb.Mover.RouteRemaining;
        if (gauge <= 0f) gauge = d;          // 라우트가 없는 짧은 이동은 직선이 곧 그은 선이다

        if (gauge < _bestDist - ProgressEps) { _bestDist = gauge; _stuckTimer = 0f; }
        else                                 _stuckTimer += bb.TickDelta;

        if (_stuckTimer >= StuckSeconds)
        {
            // **보고 있는 앞에서는 증발하지 않는다**(2026-09-08). 이 갈래가 있는 이유는
            // *아무도 안 보는 가장자리에서 영원히 서 있는 것*을 치우려는 것인데, 화면 한복판에서
            // 폰이 사라지면 그건 정리가 아니라 **속임수로 보인다.**
            //
            // 보이는 자리에서는 **떠나기를 포기하고 트리에 넘긴다** — 다음 틱에 위의 첫 줄이
            // `Routed`까지 지우므로, 무너진 습격대는 궁지에 몰려 **돌아서서 싸우고**(그게 맞는
            // 그림이다) 풀려난 포로는 도주 성향이 남아 있어 계속 달아난다.
            if (SimLocality.IsSimulated(pos))
            {
                Debug.Log($"[Leave] {bb.Self.name}: 나갈 길이 막혀 떠나기를 포기합니다.");
                bb.LeavingMap = false;
                bb.Mover.StopMovement();
                _bestDist   = float.MaxValue;
                _stuckTimer = 0f;
                return BTStatus.Failure;
            }

            Debug.Log($"[Leave] {bb.Self.name}: 더는 가장자리로 다가갈 수 없어 여기서 떠납니다.");
            Object.Destroy(bb.Self);
            return BTStatus.Running;
        }

        // 막혀 서 있으면 다시 잡는다 — 나가는 길이 하나뿐이라 벽 하나에 갇히면 영영 못 간다.
        if (!bb.Mover.IsMoving() && Time.time >= _nextRepath)
        {
            _nextRepath = Time.time + RepathSeconds;

            // **패주는 문을 다시 안 고른다**(2026-09-08). `TryFindExit`은 **직선**으로 걸어
            // 가장자리를 찾으므로 지하에서는 **몇 걸음 앞 벽**을 답으로 준다 — 그러면 왔던
            // 길로 나가라고 준 좌표를 **막힐 때마다 코앞의 벽으로 덮어쓴다.**
            // 물러나는 무리에게는 이미 *걸어 들어온 자리*라는 답이 있다(`RaidLord`).
            if (!bb.Routed && EventManager.TryFindExit(pos, out var exit)) bb.LeaveTarget = exit;
            bb.Mover.SetDestination(bb.LeaveTarget);
        }

        return BTStatus.Running;
    }
}

public class NpcEventMerchantNode : BTNode
{
    readonly float _stayDuration;
    bool _moveSent;

    const float ArrivalDist = 2f;

    public NpcEventMerchantNode(float stayDuration) => _stayDuration = stayDuration;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        switch (bb.NpcPhase)
        {
            case 0: // 목적지로 이동
                if (!_moveSent)
                {
                    bb.Mover.SetDestination(bb.NpcDestination);
                    _moveSent = true;
                }
                if (Vector3.Distance(bb.Self.transform.position, bb.NpcDestination) < ArrivalDist)
                {
                    bb.Mover.StopMovement();
                    bb.NpcTimer = 0f;
                    bb.NpcPhase = 1;
                    _moveSent   = false;
                }
                return BTStatus.Running;

            case 1: // 대기
                bb.NpcTimer += bb.TickDelta;
                if (bb.NpcTimer >= _stayDuration)
                {
                    bb.NpcPhase = 2;
                    _moveSent   = false;
                    bb.Mover.SetDestination(bb.HomePosition);
                }
                return BTStatus.Running;

            case 2: // 홈(맵 가장자리)으로 귀환 후 소멸
                if (!bb.Mover.IsMoving())
                    Object.Destroy(bb.Self);
                return BTStatus.Running;

            default:
                return BTStatus.Failure;
        }
    }
}
