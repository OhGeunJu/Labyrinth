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

    CharacterController _cc;
    float _verticalVel;

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
    bool _isMinion;

    [Header("내비게이션")]
    [Tooltip("최종 목적지 도착 판정 거리")]
    public float arriveDistance = 0.5f;
    [Tooltip("코스 웨이포인트 통과 판정 거리")]
    public float coarseReachDistance = 4f;
    [Tooltip("이만큼 이동하면 로컬 윈도우를 다시 베이크(재계획)")]
    public float replanDistance = 15f;
    [Tooltip("새 목표가 기존 목표와 이 거리 안이면 재계획 생략 (매 틱 같은 목표 재지정 시 재베이크 방지)")]
    public float goalEpsilon = 1.5f;

    [Header("Athletics 성장")]
    [SerializeField] float athleticsGrowthRate = 0.005f;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _currentTarget = transform.position;
        _baseMoveSpeed = moveSpeed;
        _replanDriftSqr = replanDistance * replanDistance;
    }

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
        _isMinion  = GetComponent<UnitBrain>()?.Data is MinionData;
    }

    public void SetMoveTarget(Vector3 worldPoint)
    {
        worldPoint.y = transform.position.y;

        // 목표가 거의 동일하고 이미 이동 중이면 재계획 생략 (재베이크 방지).
        if (_hasTarget && HorizDistSqr(worldPoint, _finalGoal) < goalEpsilon * goalEpsilon)
            return;

        _finalGoal   = worldPoint;
        _coarseRoute = Pathfinder.ComputeCoarseRoute(transform.position, worldPoint);
        _coarseIndex = 0;
        _hasTarget   = true;
        ReplanLocal();
    }

    public void StopMovement()
    {
        _hasTarget   = false;
        _coarseRoute = null;
        _localPath   = null;
    }

    // 유닛 위치 중심 윈도우를 즉석 베이크해 조향 목표까지 로컬 정밀 경로 생성.
    void ReplanLocal()
    {
        Vector3 pos   = transform.position;
        _replanAnchor = pos;

        Vector3 steer = SelectSteeringTarget(pos);
        var local     = Pathfinder.FindLocalPath(pos, steer);

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

    bool IsStuck(Vector3 pos)
    {
        Vector3 t = _currentTarget; t.y = pos.y;
        return (t - pos).sqrMagnitude <= stopDistance * stopDistance;
    }

    static float HorizDistSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    void Update()
    {
        if (_cc.isGrounded)
            _verticalVel = groundStickForce;
        else
            _verticalVel += gravity * Time.deltaTime;

        Vector3 move = new Vector3(0f, _verticalVel, 0f);

        if (_hasTarget)
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
            else
            {
                float weightCorrection = CalcWeightCorrection();
                float sfxMult = _sfx != null ? _sfx.MoveSpeedMult : 1f;
                float effectiveSpeed = (_stats != null
                    ? _baseMoveSpeed * (1f + _stats.Athletics / 100f) * weightCorrection * sfxMult
                    : moveSpeed) * _mobilityMult;

                move += (to / dist) * effectiveSpeed;

                if (_stats != null)
                    _stats.GrowStat(StatName.Athletics, athleticsGrowthRate * Time.deltaTime * CombatXp.Sol(_stats.Athletics));
            }
        }

        _cc.Move(move * Time.deltaTime);
    }

    public void SetLimping(bool limping)
    {
        _mobilityMult = limping ? 0.5f : 1f;
        // ���߿� �����̴� �ִϸ��̼� ����
    }

    public void SetCrawling(bool crawling)
    {
        _mobilityMult = crawling ? 0.2f : 1f;
        // ���߿� ���� �ִϸ��̼� ����
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