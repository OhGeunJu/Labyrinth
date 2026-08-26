using UnityEngine;

// 적대 세력을 만났을 때의 행동. 종족(UnitData)도 세력(FactionData)도 아닌 **역할** 축.
public enum CombatStance
{
    Fight,              // 교전
    Flee,               // 항상 도주 (비전투 개체)
    FightWhileHealthy,  // 교전하다 혈액이 FleeBloodRatio 아래로 떨어지면 도주

    // **반격 — 먼저 걸지 않는다**(2026-08-26). 값은 **반드시 꼬리에** 붙인다(직렬화가 정수라
    // 중간에 끼우면 프로필 에셋의 성향이 통째로 밀린다).
    //
    // 그전에는 이 상태에 **이름이 없었다.** 방랑자 트리가 `DetectNode(retaliateOnly: true)`로
    // 못 박고 있어서, P패널에서 `교전`을 골라도 `자동`과 하나도 다르지 않았다 — 성향 넷 중
    // **실제로 먹히는 것은 `도주` 하나뿐**이었다(그건 `PlayerFleeNode`가 따로 읽었다).
    //
    // 이름을 주고 나니 축이 닫힌다: **먼저 친다(교전·신중) ↔ 맞으면 친다(반격) ↔ 도망(도주)**.
    Retaliate,
}

// AI 프로필 — 개체가 "위협/유휴 상황에서 어떻게 굴 것인가"를 담은 설정 묶음.
// 세 축 분리: 종족(UnitData)=스탯 / 세력(FactionData)=누가 적인가 / **프로필(이 SO)=어떻게 반응하는가**.
// 같은 "사람" 종족·같은 마을 세력이어도 마을사람은 겁쟁이 프로필, 경비병은 전투 프로필을 쓴다.
//
// 연결: 유닛 프리팹의 `UnitBrain.aiProfile`. 비워두면 타입별 기본값
// (일반 유닛·경비 = 교전+배회 / NPC 주민 = 도주+배회 / 상점 상인 = 도주+제자리).
[CreateAssetMenu(fileName = "AiProfile", menuName = "DungeonColony/AiProfileData")]
public class AiProfileData : ScriptableObject
{
    [Header("표시")]
    [SerializeField] string profileName = "프로필";

    [Header("전투 성향")]
    [Tooltip("적대 상대를 만났을 때의 행동.")]
    [SerializeField] CombatStance stance = CombatStance.Fight;

    [Tooltip("FightWhileHealthy일 때 도주를 시작하는 혈액 비율(0~1).")]
    [Range(0f, 1f)]
    [SerializeField] float fleeBloodRatio = 0.4f;

    [Tooltip("도주 시 한 번에 물러나는 거리.")]
    [SerializeField] float fleeDistance = 15f;

    [Tooltip("위협 감지 범위. 0이면 UnitData.DetectRange(NPC 주민은 FleeDetectRange)를 쓴다.")]
    [SerializeField] float fleeDetectRange = 0f;

    [Header("유휴 행동")]
    [Tooltip("할 일이 없을 때 주변을 배회한다. 끄면 제자리 대기(보초·상점 상인 등).")]
    [SerializeField] bool wander = true;

    [Tooltip("배회 반경(홈 기준). 0이면 타입 기본값.")]
    [SerializeField] float wanderRadius = 0f;

    [Tooltip("배회 지점 도착 후 멈춰 있는 시간(초) 범위.")]
    [SerializeField] float pauseMin = 2f;
    [SerializeField] float pauseMax = 6f;

    public string       ProfileName     => string.IsNullOrEmpty(profileName) ? name : profileName;
    public CombatStance Stance          => stance;
    public float        FleeBloodRatio  => fleeBloodRatio;
    public float        FleeDistance    => fleeDistance;
    public float        FleeDetectRange => fleeDetectRange;
    public bool         Wander          => wander;
    public float        WanderRadius    => wanderRadius;
    public float        PauseMin        => pauseMin;
    public float        PauseMax        => pauseMax;
}
