using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/StorageBuildingData")]
public class StorageBuildingData : ScriptableObject
{
    [Header("기본 정보")]
    public string BuildingName = "창고";

    [Header("건설 UI 분류")]
    public BuildCategory Category = BuildCategory.Building;

    [Header("건설 비용")]
    public ResourceType CostType   = ResourceType.Stone;
    public int          CostAmount = 5;

    [Header("프리팹")]
    public GameObject Prefab;
}
