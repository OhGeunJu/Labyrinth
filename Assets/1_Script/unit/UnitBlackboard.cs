using UnityEngine;

public class UnitBlackboard
{
    public GameObject Self;
    public PawnMover Mover;

    // 전투
    public GameObject Target;
    public float DetectRange  = 10f;
    public float AttackRange  = 1.5f;
    public float AttackDamage = 10f;

    // 작업
    public JobBase CurrentJob;
    public ResourceNode AssignedNode;

    // 상태
    public bool IsDrafted;
    public bool IsUnarmed  = true;
    public bool IsKnockedOut;
    public bool IsStaggered;
    public float StaggerTimer;

    // 순찰
    public PatrolZone AssignedPatrolZone;

    // 무드
    public float Mood;
}
