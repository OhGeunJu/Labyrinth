using UnityEngine;

public class UnitBlackboard
{
    public GameObject Self;
    public PawnMover Mover;

    // 이번 BT 틱까지 실제로 흐른 시간(초). BT는 tickInterval마다 돌므로 노드 안에서
    // Time.deltaTime(한 프레임)을 누적하면 시간이 tickInterval/frameTime 배만큼 느리게 간다.
    // 시간을 다루는 노드는 반드시 이 값을 쓸 것.
    public float TickDelta;

    // 전투
    public GameObject Target;

    // 마지막으로 나를 때린 상대 — 관계(중립/적대)와 무관하게 반격 대상이 된다.
    // 중립 세력이 선공당했을 때 대응할 수 있게 하는 축. RetaliationMemory 동안만 유효.
    public GameObject LastAttacker;
    public float      LastAttackedTime = -999f;
    public const float RetaliationMemory = 15f;

    public bool HasFreshAttacker =>
        LastAttacker != null && Time.time - LastAttackedTime <= RetaliationMemory;

    public void RecordAttacker(GameObject attacker)
    {
        if (attacker == null || attacker == Self) return;
        LastAttacker     = attacker;
        LastAttackedTime = Time.time;
    }

    public float DetectRange  = 10f;
    public float AttackRange  = 1.5f;
    public float AttackDamage = 10f;

    // 작업
    public JobBase CurrentJob;
    public ResourceNode AssignedNode;

    // 이번 틱에 RestNode가 실행됐는가 — 상위 분기(전투/소집 등)가 휴식을 밀어냈을 때
    // UnitBrain이 누워 있는 폰을 일으켜 세우는 데 쓴다. 매 틱 UnitBrain이 리셋한다.
    public bool RestTicked;

    // 플레이어가 직접 내린 이동 명령이 진행 중인가.
    // 켜져 있는 동안은 전투·작업 같은 자동 행동보다 우선한다 — 명령했는데 BT가 덮어쓰면
    // 조작이 안 먹는 것처럼 느껴진다. 도착하거나 이동이 끝나면 자동 해제(ManualOrderNode).
    public bool ManualMove;

    // 명령으로 **제자리에 붙들려 있는가**. 원정 편성 중 집결 지점에 먼저 도착한 폰이
    // 다시 일하러 가버리지 않게 잡아둔다 — `ManualMove`는 목적지가 소진되면 자동 해제되므로
    // "도착했지만 계속 대기"를 표현할 수 없다. 푸는 건 명령을 건 쪽의 몫이다.
    public bool HoldPosition;

    // 상태
    public bool IsDrafted;
    public bool IsUnarmed  = true;
    public bool IsKnockedOut;

    // 순찰
    public PatrolZone AssignedPatrolZone;

    // 허기 (0 = 굶주림, 100 = 배부름)
    public float Hunger = 100f;

    // 배회/복귀 기준점. NPC는 스폰 시 설정, 그 외 유닛은 처음 배회할 때 그 자리로 잡는다.
    public Vector3 HomePosition;
    public bool    HomeSet;

    // NPC 전용
    public Vector3 NpcDestination; // EventMerchant 목적지
    public float   NpcTimer;       // 범용 타이머
    public int     NpcPhase;       // EventMerchant 단계 (0=이동중 1=대기 2=퇴장)
}
