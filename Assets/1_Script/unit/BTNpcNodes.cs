using UnityEngine;

// NPC 전용 BT 노드 모음

// 주변 적 감지 시 도주
public class NpcFleeNode : BTNode
{
    readonly float _fleeRange;
    readonly float _fleeDist;

    public NpcFleeNode(float detectRange, float fleeDistance = 15f)
    {
        _fleeRange = detectRange;
        _fleeDist  = fleeDistance;
    }

    public override BTStatus Tick(UnitBlackboard bb)
    {
        var cols = Physics.OverlapSphere(bb.Self.transform.position, _fleeRange);
        GameObject threat = null;
        foreach (var c in cols)
        {
            var f = c.GetComponent<Faction>();
            var myF = bb.Self.GetComponent<Faction>();
            if (f != null && myF != null && f.IsHostileTo(myF))
            { threat = c.gameObject; break; }
        }
        if (threat == null) return BTStatus.Failure;

        Vector3 away = (bb.Self.transform.position - threat.transform.position).normalized;
        Vector3 dest = bb.Self.transform.position + away * _fleeDist;
        bb.Mover.SetDestination(dest);
        return BTStatus.Running;
    }
}

// 홈포인트 주변 랜덤 배회
public class NpcWanderNode : BTNode
{
    readonly float _radius;
    float _waitTimer;
    float _waitDuration;
    bool _waiting;

    public NpcWanderNode(float radius) => _radius = radius;

    public override BTStatus Tick(UnitBlackboard bb)
    {
        if (_waiting)
        {
            _waitTimer += Time.deltaTime;
            if (_waitTimer < _waitDuration) return BTStatus.Running;
            _waiting = false;
        }

        if (!bb.Mover.IsMoving())
        {
            Vector2 rnd = Random.insideUnitCircle * _radius;
            Vector3 dest = bb.HomePosition + new Vector3(rnd.x, 0f, rnd.y);
            bb.Mover.SetDestination(dest);

            _waitTimer    = 0f;
            _waitDuration = Random.Range(2f, 6f);
            _waiting      = true;
        }
        return BTStatus.Running;
    }
}

// EventMerchant: 목적지까지 이동 → 대기 → 홈으로 귀환 → 소멸
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
                bb.NpcTimer += Time.deltaTime;
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
