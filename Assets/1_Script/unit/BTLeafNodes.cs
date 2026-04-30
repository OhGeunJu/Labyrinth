using System;
using UnityEngine;

// ���� üũ
public class ConditionNode : BTNode
{
    readonly Func<UnitBlackboard, bool> _condition;
    public ConditionNode(Func<UnitBlackboard, bool> condition) => _condition = condition;
    public override BTStatus Tick(UnitBlackboard bb) =>
        _condition(bb) ? BTStatus.Success : BTStatus.Failure;
}

// �� ����
public class DetectNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        // �̹� Ÿ�� ������ ����ִ��� Ȯ��
        if (bb.Target != null)
        {
            if (bb.Target.activeInHierarchy) return BTStatus.Success;
            bb.Target = null;
        }

        // �ֺ� Ž��
        var myFaction = bb.Self.GetComponent<Faction>();
        Collider[] hits = Physics.OverlapSphere(bb.Self.transform.position, bb.DetectRange);

        foreach (var hit in hits)
        {
            if (hit.gameObject == bb.Self) continue;

            var otherFaction = hit.GetComponent<Faction>();
            if (otherFaction == null) continue;

            if (myFaction.IsHostileTo(otherFaction))
            {
                bb.Target = hit.gameObject;
                return BTStatus.Success;
            }
        }

        bb.Target = null;
        return BTStatus.Failure;
    }
}

// ���� ���� (���� ���)
public class DraftedNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        // �ǰ� �� �ݰ��� ���߿� ���⼭ ó��
        return BTStatus.Running;
    }
}

// ����
public class FightNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        var combat = bb.Self.GetComponent<CombatHandler>();
        if (combat == null) return BTStatus.Failure;

        // Ÿ�� ���ų� �׾����� ���� ����
        if (bb.Target == null || !bb.Target.activeInHierarchy)
        {
            bb.Target = null;
            combat.ExitCombat();
            return BTStatus.Failure;
        }

        float dist = Vector3.Distance(bb.Self.transform.position,
                                       bb.Target.transform.position);

        // ���� ���� ���̸� �̵�
        if (dist > bb.AttackRange)
        {
            combat.ExitCombat();
            bb.Mover.SetMoveTarget(bb.Target.transform.position);
            return BTStatus.Running;
        }

        // ���� ���� ���̸� ����
        bb.Mover.StopMovement();
        combat.EnterCombat();
        return BTStatus.Running;
    }
}

// �۾�
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

        if (!bb.Mover.HasTarget())
        {
            var jobCenter = MapManager.Instance.TileToWorld(currentJob.TileCoord);
            var pos = bb.Self.transform.position;
            float dist = Vector2.Distance(new Vector2(pos.x, pos.z),
                                          new Vector2(jobCenter.x, jobCenter.z));

            if (dist > 1.5f)
                bb.Mover.SetMoveTarget(TileToWorld(currentJob.TileCoord, bb));
            else
            {
                currentJob.Tick(bb.Self.GetComponent<UnitBrain>());
                if (currentJob.IsComplete())
                {
                    JobManager.Instance.CompleteJob(currentJob);
                    bb.CurrentJob = null;
                    return BTStatus.Success;
                }
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

        if (dist > 1.5f)
        {
            if (!bb.Mover.HasTarget())
                bb.Mover.SetMoveTarget(TileToWorld(node.TileCoord, bb));
        }
        else
        {
            bb.Mover.StopMovement();
            node.Work(bb.Self.GetComponent<UnitBrain>().Stats.Mining);
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

// ���
public class IdleNode : BTNode
{
    public override BTStatus Tick(UnitBlackboard bb)
    {
        return BTStatus.Running;
    }
}