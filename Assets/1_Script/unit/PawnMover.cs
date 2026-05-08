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
    List<Vector3> _path;
    int _pathIndex;
    Vector3 _currentTarget;
    bool _hasTarget;
    float _baseMoveSpeed;
    UnitStats _stats;
    Inventory _inventory;
    StatusEffectSystem _sfx;
    bool _isMinion;

    [Header("Athletics 성장")]
    [SerializeField] float athleticsGrowthRate = 0.005f;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _currentTarget = transform.position;
        _baseMoveSpeed = moveSpeed;
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

        var path = Pathfinder.FindPath(transform.position, worldPoint);
        if (path != null && path.Count > 0)
        {
            _path = path;
            // 시작 타일(index 0)은 현재 위치이므로 건너뜀
            _pathIndex    = path.Count > 1 ? 1 : 0;
            _currentTarget = WithY(_path[_pathIndex]);
        }
        else
        {
            // 경로 없으면 직선 이동 (장애물 없는 전투 추적 등)
            _path         = null;
            _pathIndex    = 0;
            _currentTarget = worldPoint;
        }
        _hasTarget = true;
    }

    public void StopMovement()
    {
        _hasTarget = false;
        _path      = null;
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
            Vector3 to = _currentTarget - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist <= stopDistance)
            {
                if (_path != null && _pathIndex + 1 < _path.Count)
                {
                    _pathIndex++;
                    _currentTarget = WithY(_path[_pathIndex]);
                }
                else
                {
                    _hasTarget = false;
                    _path      = null;
                }
            }
            else
            {
                float weightCorrection = CalcWeightCorrection();
                float sfxMult = _sfx != null ? _sfx.MoveSpeedMult : 1f;
                float effectiveSpeed = _stats != null
                    ? _baseMoveSpeed * (1f + _stats.Athletics / 100f) * weightCorrection * sfxMult
                    : moveSpeed;

                move += (to / dist) * effectiveSpeed;

                // 이동 중 육상 스킬 성장
                if (_stats != null)
                    if (!_isMinion)
                        _stats.Athletics = Mathf.Min(100f, _stats.Athletics + athleticsGrowthRate * Time.deltaTime);
            }
        }

        _cc.Move(move * Time.deltaTime);
    }

    public void SetLimping(bool limping)
    {
        moveSpeed = limping ? _baseMoveSpeed * 0.5f : _baseMoveSpeed;
        // ���߿� �����̴� �ִϸ��̼� ����
    }

    public void SetCrawling(bool crawling)
    {
        moveSpeed = crawling ? _baseMoveSpeed * 0.2f : _baseMoveSpeed;
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
        if (_path == null || _path.Count == 0) return;
        Gizmos.color = Color.cyan;
        Vector3 prev = transform.position;
        for (int i = _pathIndex; i < _path.Count; i++)
        {
            var cur = WithY(_path[i]);
            Gizmos.DrawLine(prev, cur);
            Gizmos.DrawSphere(cur, 0.1f);
            prev = cur;
        }
    }
#endif
}