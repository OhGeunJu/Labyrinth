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

    // 순찰
    public PatrolZone AssignedPatrolZone;

    // 허기 (0 = 굶주림, 100 = 배부름)
    public float Hunger = 100f;

    // NPC 전용
    public Vector3 HomePosition;   // 배회/복귀 기준점
    public Vector3 NpcDestination; // EventMerchant 목적지
    public float   NpcTimer;       // 범용 타이머
    public int     NpcPhase;       // EventMerchant 단계 (0=이동중 1=대기 2=퇴장)
}
