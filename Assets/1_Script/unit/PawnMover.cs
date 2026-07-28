using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PawnMover : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 4f;
    public float stopDistance = 0.12f;

    [Header("Ground")]
    public float gravity = -20f;
    public float groundStickForce = -2f;
    [Tooltip("이 높이 아래로 떨어지면 마지막 접지 위치로 복귀 (공허 던전 맵 낙하 안전망)")]
    public float fallRecoveryY = -10f;

    CharacterController _cc;
    float _verticalVel;
    Vector3 _lastGroundedPos;

    // 내비게이션: 코스 라우트(L2 장거리) + 유닛 중심 로컬 정밀 경로
    Vector3       _finalGoal;
    List<Vector3> _coarseRoute;
    int           _coarseIndex;
    List<Vector3> _localPath;
    int           _localIndex;
    Vector3       _replanAnchor;
    float         _replanDriftSqr;

    Vector3 _currentTarget;
    bool _hasTarget;
    float _baseMoveSpeed;
    float _mobilityMult = 1f;   // 절뚝임/기어가기 등 이동 속도 배율
    UnitStats _stats;
    Inventory _inventory;
    StatusEffectSystem _sfx;
    UnitBrain _brain;
    bool _isMinion;

    // 기절 중이면 BT가 정지해 이동 명령을 안 내리지만, MoveController 우클릭 등
    // BT 밖 경로가 SetMoveTarget을 호출할 수 있어 이동 차단이 필요하다.
    bool IsKnockedOut => _brain != null && _brain.Blackboard != null && _brain.Blackboard.IsKnockedOut;

    [Header("내비게이션")]
    [Tooltip("최종 목적지 도착 판정 거리")]
    public float arriveDistance = 0.5f;
    [Tooltip("코스 웨이포인트 통과 판정 거리")]
    public float coarseReachDistance = 4f;
    [Tooltip("이만큼 이동하면 로컬 윈도우를 다시 베이크(재계획)")]
    public float replanDistance = 15f;
    [Tooltip("새 목표가 기존 목표와 이 거리 안이면 재계획 생략 (매 틱 같은 목표 재지정 시 재베이크 방지)")]
    public float goalEpsilon = 1.5f;
    [Tooltip("공허 낭떠러지 가드 전방 확인 거리(m). 던전형 맵에서 이만큼 앞이 바닥 밖이면 정지")]
    public float ledgeLookahead = 0.6f;

    [Header("장애물 회피")]
    [Tooltip("이 거리 안의 다른 폰을 피해 조향한다. 0이면 회피 없음")]
    public float avoidRadius = 1.5f;
    [Tooltip("회피 조향의 세기. 클수록 크게 돌아간다")]
    public float avoidStrength = 1f;
    [Tooltip("이 시간(초)마다 실제 이동량을 검사해 막혔는지 판단")]
    public float blockedCheckTime = 0.35f;
    [Tooltip("막혔을 때 옆으로 비켜 걷는 시간(초)")]
    public float sidestepTime = 0.6f;

    // 회피/정체 상태
    Vector3 _progressAnchor;
    float   _progressTimer;
    int     _blockedCount;
    Vector3 _sidestepDir;
    float   _sidestepTimer;
    float   _lastDiagLogTime = -999f;   // 계측 로그 폰당 1초 제한

    [Header("Athletics 성장")]
    [SerializeField] float athleticsGrowthRate = 0.005f;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _currentTarget = transform.position;
        _lastGroundedPos = transform.position;
        _baseMoveSpeed = moveSpeed;
        _replanDriftSqr = replanDistance * replanDistance;

        ApplyPawnCollisionRule();
    }

    // 같은 레이어(= 같은 편)끼리는 서로 통과한다.
    // 폰 지름이 타일 한 칸과 같아서, 아군이 몸으로 길을 막으면 옆으로 비킬 여유가 없다 —
    // ㄷ자 포위·정면 교착이 여기서 나온다. 통과시키면 그 부류가 원인 채로 사라진다.
    // 적(다른 레이어)과는 그대로 충돌하므로 몸으로 라인을 막는 전술은 살아 있다.
    void ApplyPawnCollisionRule()
    {
        if (_cc == null) return;

        // Default(0)에는 지면·잡오브젝트가 섞여 있어 제외하면 바닥을 뚫는다.
        // 유닛은 반드시 전용 레이어(Player/Enemy 등)에 둘 것.
        int layer = gameObject.layer;
        if (layer == 0) return;

        _cc.excludeLayers = 1 << layer;
    }

    // other가 나를 물리적으로 막는가 — 길찾기 베이크가 같은 규칙을 쓰도록 공개한다.
    // (물리에서 통과하는 상대를 그래프에서 벽으로 굽으면 헛돌게 된다.)
    public bool IsBlockedBy(PawnMover other)
        => other != null && other != this && other.gameObject.layer != gameObject.layer;

    public void Init(UnitData data)
    {
        moveSpeed      = data.MoveSpeed;
        _baseMoveSpeed = data.MoveSpeed;
    }

    public void SetStats(UnitStats stats, Inventory inventory)
    {
        _stats     = stats;
        _inventory = inventory;
        _sfx       = GetComponent<StatusEffectSystem>();
        _brain     = GetComponent<UnitBrain>();
        _isMinion  = _brain?.Data is MinionData;
    }

    public void SetMoveTarget(Vector3 worldPoint)
    {
        // 기절 중엔 이동 명령 무시 (기상하면 IsKnockedOut이 풀려 복귀 이동은 정상 동작)
        if (IsKnockedOut) return;

        // 이동 명령 = 일어난다. 누운 자세(침대 위)에서 경로를 잡지 않도록 먼저 내려온다.
        if (IsSleeping) SetSleeping(false);

        worldPoint.y = transform.position.y;

        // 목표가 거의 동일하고 이미 이동 중이면 재계획 생략 (재베이크 방지).
        if (_hasTarget && HorizDistSqr(worldPoint, _finalGoal) < goalEpsilon * goalEpsilon)
            return;

        _finalGoal   = worldPoint;
        _coarseRoute = Pathfinder.ComputeCoarseRoute(transform.position, worldPoint);
        _coarseIndex = 0;
        _hasTarget   = true;
        ResetProgress();
        ReplanLocal();
    }

    public void StopMovement()
    {
        _hasTarget   = false;
        _coarseRoute = null;
        _localPath   = null;
    }

    // ── 눕기(침대 휴식) ──────────────────────────────────────────────
    // 누워 있는 동안은 CharacterController를 끄고 위치·회전을 침대에 고정한다.
    // (중력/조향이 계속 돌면 침대 위에서 미끄러지거나 파묻힌다.)
    // 일어날 때는 눕기 직전 서 있던 자리로 되돌린다 — 침대 콜라이더 위에서 깨어나 끼는 것 방지.
    public bool IsSleeping { get; private set; }

    Vector3    _wakePosition;
    Quaternion _wakeRotation;

    public void SetSleeping(bool sleeping, Vector3 lyingPos = default, Quaternion lyingRot = default)
    {
        if (sleeping == IsSleeping) return;

        if (sleeping)
        {
            StopMovement();
            _wakePosition = transform.position;
            _wakeRotation = transform.rotation;

            _cc.enabled = false;
            transform.SetPositionAndRotation(lyingPos, lyingRot);
            IsSleeping = true;
        }
        else
        {
            transform.SetPositionAndRotation(_wakePosition, _wakeRotation);
            _cc.enabled  = true;
            _verticalVel = 0f;
            IsSleeping   = false;
        }
    }

    // 유닛 위치 중심 윈도우를 즉석 베이크해 조향 목표까지 로컬 정밀 경로 생성.
    void ReplanLocal()
    {
        Vector3 pos   = transform.position;
        _replanAnchor = pos;

        Vector3 steer = SelectSteeringTarget(pos);
        var local     = Pathfinder.FindLocalPath(pos, steer, this);   // 다른 폰도 장애물로 넣어 우회

        if (local != null && local.Count > 0)
        {
            _localPath  = local;
            _localIndex = 0;
            _currentTarget = WithY(local[0]);
        }
        else
        {
            // 로컬 경로 실패 — 조향 목표로 직선 폴백
            _localPath  = null;
            _localIndex = 0;
            _currentTarget = WithY(steer);
        }
    }

    // 이미 지난 코스 웨이포인트는 통과하고, 다음으로 향할 조향 목표 반환.
    // 근접(reached)했거나, 다음 웨이포인트에 더 가까워졌으면(passed=추월) 인덱스 전진.
    // → 로컬 우회로 웨이포인트를 정확히 밟지 못해도 뒤로 돌아가지 않는다.
    Vector3 SelectSteeringTarget(Vector3 pos)
    {
        if (_coarseRoute == null || _coarseRoute.Count == 0) return _finalGoal;
        while (_coarseIndex < _coarseRoute.Count - 1)
        {
            Vector3 cur  = _coarseRoute[_coarseIndex];
            Vector3 next = _coarseRoute[_coarseIndex + 1];
            bool reached = HorizDistSqr(pos, cur) < coarseReachDistance * coarseReachDistance;
            bool passed  = HorizDistSqr(pos, next) < HorizDistSqr(cur, next);
            if (reached || passed) _coarseIndex++;
            else break;
        }
        return _coarseRoute[_coarseIndex];
    }

    // 현재 웨이포인트 도달 시 다음 단계 진행.
    void AdvanceWaypoint()
    {
        // 로컬 경로의 다음 점
        if (_localPath != null && _localIndex + 1 < _localPath.Count)
        {
            _localIndex++;
            _currentTarget = WithY(_localPath[_localIndex]);
            return;
        }

        Vector3 pos = transform.position;

        // 최종 목적지 도착
        if (HorizDistSqr(pos, _finalGoal) <= arriveDistance * arriveDistance)
        {
            StopMovement();
            return;
        }

        // 아직 멀다 — 다음 윈도우로 재계획
        ReplanLocal();
        if (!IsStuck(pos)) return;

        // 로컬이 막힘 — 전역 재계획(현재 위치 기준 코스 라우트 갱신: 메소 벽 지식 반영)
        _coarseRoute = Pathfinder.ComputeCoarseRoute(pos, _finalGoal);
        _coarseIndex = 0;
        ReplanLocal();

        // 그래도 제자리면 정지 (제자리 떨림/무한 루프 방지)
        if (IsStuck(pos)) StopMovement();
    }

    // 던전형 맵(FloorRequired)에서 다음 스텝이 바닥 밖이면 true.
    // 현재 타일이 바닥일 때만 발동 — 어쩌다 바닥 밖에 놓인 유닛의 복귀 이동은 막지 않는다.
    bool IsVoidAhead(Vector3 pos, Vector3 dir)
    {
        var map = MapManager.Instance;
        if (map == null || !map.FloorRequired) return false;
        return map.IsFloored(pos) && !map.IsFloored(pos + dir * ledgeLookahead);
    }

    bool IsStuck(Vector3 pos)
    {
        Vector3 t = _currentTarget; t.y = pos.y;
        return (t - pos).sqrMagnitude <= stopDistance * stopDistance;
    }

    // ── 지역 회피 & 정체 탈출 ────────────────────────────────────────
    // 길찾기 그래프는 벽(정적)만 안다. 다른 폰·침대·창고처럼 그래프에 없는 장애물은
    // 경로가 그대로 관통하게 나오고, CharacterController가 막혀 제자리에서 밀기만 한다.
    // → ① 앞쪽 폰은 조향 단계에서 비켜 간다 ② 그래도 안 움직이면 옆으로 비켜섰다가 재계획한다.

    static readonly List<PawnMover> _all = new();

    // 길찾기 베이크가 다른 폰을 장애물로 굽는 데 쓴다.
    public static IReadOnlyList<PawnMover> All => _all;

    void OnEnable()  => _all.Add(this);
    void OnDisable() => _all.Remove(this);

    // 진행 방향 기준 오른쪽(수평)
    static Vector3 RightOf(Vector3 dir) => new Vector3(dir.z, 0f, -dir.x);

    Vector3 Steer(Vector3 pos, Vector3 desired)
    {
        Vector3 steer = desired;

        // 막혀서 비켜서는 중 — 측면 성분을 크게 준다
        if (_sidestepTimer > 0f)
        {
            _sidestepTimer -= Time.deltaTime;
            steer = (desired * 0.35f + _sidestepDir).normalized;
        }

        Vector3 avoid = AvoidNearbyPawns(pos, desired);
        if (avoid != Vector3.zero)
            steer = (steer + avoid * avoidStrength).normalized;

        return steer.sqrMagnitude > 0.001f ? steer : desired;
    }

    // 앞쪽에 있는 다른 폰에서 벗어나는 조향 성분.
    // 측면 회피는 **항상 자기 기준 오른쪽** — 마주 오는 둘이 서로 반대 방향으로 갈라져
    // "서로 맞대고 아무도 못 가는" 교착이 풀린다(우측통행). 근접 시 밀어내는 성분도 섞는다.
    Vector3 AvoidNearbyPawns(Vector3 pos, Vector3 desired)
    {
        if (avoidRadius <= 0f) return Vector3.zero;

        // 목적지 코앞에서는 회피하지 않는다 — 도착 지점에 모여야 하는 경우(창고·작업대·침대 옆)
        // 끝까지 서로 밀어내면 아무도 도착하지 못한다.
        if (HorizDistSqr(pos, _finalGoal) <= arriveDistance * arriveDistance * 4f)
            return Vector3.zero;

        // 교전 상대는 피하지 않는다 — 붙어야 때린다
        var combatTarget = _brain != null && _brain.Blackboard != null ? _brain.Blackboard.Target : null;

        Vector3 right = RightOf(desired);
        Vector3 avoid = Vector3.zero;

        foreach (var other in _all)
        {
            if (other == this || other == null || other.IsSleeping) continue;
            if (combatTarget != null && other.gameObject == combatTarget) continue;

            Vector3 d = other.transform.position - pos;
            d.y = 0f;

            float distSqr = d.sqrMagnitude;
            if (distSqr > avoidRadius * avoidRadius || distSqr < 0.0001f) continue;

            float dist = Mathf.Sqrt(distSqr);
            Vector3 dir = d / dist;

            float ahead = Vector3.Dot(dir, desired);
            if (ahead <= 0.2f) continue;   // 뒤·옆으로 스치는 상대는 무시

            float weight = (1f - dist / avoidRadius) * ahead;
            avoid += (right + -dir * 0.5f) * weight;
        }

        return Vector3.ClampMagnitude(avoid, 1f);
    }

    // 실제로 나아가고 있는지 감시. 밀리기만 하면 단계적으로 탈출을 시도한다.
    void TrackProgress(Vector3 pos, Vector3 desired, float speed)
    {
        _progressTimer += Time.deltaTime;
        if (_progressTimer < blockedCheckTime) return;

        float moved    = Mathf.Sqrt(HorizDistSqr(pos, _progressAnchor));
        float expected = speed * _progressTimer * 0.35f;   // 기대치의 35%도 못 가면 막힌 것

        if (moved >= expected) _blockedCount = 0;
        else                   OnBlocked(pos, desired);

        _progressAnchor = pos;
        _progressTimer  = 0f;
    }

    // 막혔을 때의 대응. **좌우로 번갈아 비켜서지 않는다** — U자로 둘러싸인 자리에서
    // 그러면 한쪽으로 갔다가 되돌아오는 진동만 반복하고 절대 빠져나가지 못한다.
    // 한 번만 비켜서 보고, 안 되면 그 자리를 막힌 곳으로 기억시켜 **플래너에게 우회를 맡긴다.**
    void OnBlocked(Vector3 pos, Vector3 desired)
    {
        _blockedCount++;

        MovementDiagnostics.RecordStage(_blockedCount,
                                        _brain != null ? _brain.UnitName : name,
                                        pos, ref _lastDiagLogTime);

        switch (_blockedCount)
        {
            case 1:   // 스치는 정체 — 한 번 비켜서 본다
                _sidestepDir   = RightOf(desired);
                _sidestepTimer = sidestepTime;
                break;

            case 2:   // 막힌 자리를 기억시키고 경로를 다시 뽑는다 (그래프가 우회로를 낸다)
                _sidestepTimer = 0f;
                RememberBlockedSpot(pos, desired);
                ReplanLocal();
                break;

            case 3:   // 로컬 윈도우 밖으로 돌아야 하는 규모 — 전역 경로부터 다시
                RememberBlockedSpot(pos, desired);
                _coarseRoute = Pathfinder.ComputeCoarseRoute(pos, _finalGoal);
                _coarseIndex = 0;
                ReplanLocal();
                break;

            default:  // 포기 — BT가 다른 행동을 고르거나 다음 틱에 다시 명령한다
                MovementDiagnostics.LogGiveUp(_brain != null ? _brain.UnitName : name,
                                              pos, _finalGoal, _localPath != null);
                StopMovement();
                break;
        }
    }

    // 앞을 막고 있는 자리를 잠시 통행 불가로 기록. 내 발밑이 아니라 **가려던 쪽**을 막는다.
    // 목적지 코앞은 제외 — 도착 지점 자체를 막아버리면 영영 못 간다.
    void RememberBlockedSpot(Vector3 pos, Vector3 desired)
    {
        Vector3 spot = pos + desired * 0.9f;
        if (HorizDistSqr(spot, _finalGoal) <= arriveDistance * arriveDistance * 4f) return;
        PathBlockMemory.Block(spot);
    }

    void ResetProgress()
    {
        _progressAnchor = transform.position;
        _progressTimer  = 0f;
        _blockedCount   = 0;
        _sidestepTimer  = 0f;
    }

    static float HorizDistSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    void Update()
    {
        // 누워 있는 동안은 중력·조향 모두 정지 (위치는 SetSleeping이 고정해뒀다)
        if (IsSleeping) return;

        if (_cc.isGrounded)
        {
            _verticalVel = groundStickForce;
            _lastGroundedPos = transform.position;
        }
        else
        {
            _verticalVel += gravity * Time.deltaTime;

            // 공허 낙하 안전망: 바닥 밖으로 떨어지면 마지막 접지 위치로 복귀
            if (transform.position.y < fallRecoveryY)
            {
                StopMovement();
                _verticalVel = 0f;
                _cc.enabled = false;
                transform.position = _lastGroundedPos;
                _cc.enabled = true;
                return;
            }
        }

        Vector3 move = new Vector3(0f, _verticalVel, 0f);

        // 기절 상태에서 목표가 남아 있으면 (KO 직전 타이밍 등) 정지시켜 조향을 막는다.
        // 중력/낙하 안전망은 위에서 이미 처리됐으므로 아래 조향만 건너뛴다.
        if (IsKnockedOut)
        {
            if (_hasTarget) StopMovement();
        }
        else if (_hasTarget)
        {
            Vector3 pos = transform.position;

            // 윈도우를 충분히 벗어났으면 새 윈도우로 재계획
            if ((pos - _replanAnchor).sqrMagnitude > _replanDriftSqr)
                ReplanLocal();

            Vector3 to = _currentTarget - pos;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist <= stopDistance)
            {
                AdvanceWaypoint();
            }
            else if (_localPath == null && IsVoidAhead(pos, to / dist))
            {
                // 공허 낭떠러지 가드: 경로 실패 직선 폴백이 공허를 향할 때만 발동.
                // 유효한 로컬 경로는 이미 바닥 게이트를 통과했으므로 검사하지 않는다
                // (좁은 다리에서 전방 샘플이 대각 공허 타일을 찍는 오발 방지).
                StopMovement();
            }
            else
            {
                float weightCorrection = CalcWeightCorrection();
                float sfxMult = _sfx != null ? _sfx.MoveSpeedMult : 1f;
                float effectiveSpeed = (_stats != null
                    ? _baseMoveSpeed * (1f + _stats.Athletics / 100f) * weightCorrection * sfxMult
                    : moveSpeed) * _mobilityMult;

                Vector3 desired = to / dist;
                Vector3 steer   = Steer(pos, desired);

                // 회피/비켜서기로 방향이 틀어졌으니 공허 검사도 실제 진행 방향으로 다시 한다
                if (_localPath == null && IsVoidAhead(pos, steer)) steer = desired;

                move += steer * effectiveSpeed;
                TrackProgress(pos, desired, effectiveSpeed);

                if (_stats != null)
                    _stats.GrowStat(StatName.Athletics, athleticsGrowthRate * Time.deltaTime * CombatXp.Sol(_stats.Athletics));
            }
        }

        _cc.Move(move * Time.deltaTime);
    }

    public void SetLimping(bool limping)
    {
        _mobilityMult = limping ? 0.5f : 1f;
        // 나중에 절뚝이는 애니메이션 연결
    }

    public void SetCrawling(bool crawling)
    {
        _mobilityMult = crawling ? 0.2f : 1f;
        // 나중에 기는 애니메이션 연결
    }

    float CalcWeightCorrection()
    {
        if (_stats == null || _inventory == null) return 1f;
        float maxWeight     = _stats.Strength * 5f;
        float currentWeight = _inventory.GetTotalWeight();
        if (maxWeight <= 0f || currentWeight <= maxWeight) return 1f;
        return maxWeight / currentWeight;
    }

    public bool HasTarget()  => _hasTarget;
    public bool IsMoving()   => _hasTarget;
    public void SetDestination(Vector3 pos) => SetMoveTarget(pos);
    public Vector3 Target()  => _currentTarget;

    Vector3 WithY(Vector3 v) => new Vector3(v.x, transform.position.y, v.z);

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // 코스 라우트(L2) — 노랑
        if (_coarseRoute != null)
        {
            Gizmos.color = Color.yellow;
            for (int i = _coarseIndex; i < _coarseRoute.Count; i++)
            {
                var cur = WithY(_coarseRoute[i]);
                Gizmos.DrawSphere(cur, 0.3f);
            }
        }

        // 로컬 정밀 경로 — 청록
        if (_localPath != null && _localPath.Count > 0)
        {
            Gizmos.color = Color.cyan;
            Vector3 prev = transform.position;
            for (int i = _localIndex; i < _localPath.Count; i++)
            {
                var cur = WithY(_localPath[i]);
                Gizmos.DrawLine(prev, cur);
                Gizmos.DrawSphere(cur, 0.1f);
                prev = cur;
            }
        }
    }
#endif
}