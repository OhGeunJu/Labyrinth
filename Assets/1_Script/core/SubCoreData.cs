using UnityEngine;

[CreateAssetMenu(fileName = "SubCoreData", menuName = "DungeonColony/SubCoreData")]
public class SubCoreData : ScriptableObject
{
    [Header("기본 정보")]
    public string CoreName = "서브 코어";

    [Header("범위")]
    public float Radius = 8f;

    [Header("하수인 캡 증가")]
    public int MinionCapBonus = 3;

    [Header("범위 내 유닛 효과")]
    [Tooltip("이동속도 배율 보정 (양수=가속, 음수=감속)")]
    public float MoveSpeedModifier = 0f;
    [Tooltip("초당 피해 (적 범위 효과용)")]
    public float DamagePerSecond = 0f;

    [Header("건설 비용")]
    public ResourceType CostType = ResourceType.Stone;
    public int CostAmount = 10;

    [Header("건설 UI 분류")]
    public BuildCategory Category = BuildCategory.Building;

    [Header("프리팹")]
    public GameObject Prefab;
}
