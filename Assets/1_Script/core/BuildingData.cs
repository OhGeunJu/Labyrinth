using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/BuildingData")]
public class BuildingData : ScriptableObject
{
    public string           BuildingName  = "건물";
    public BuildCategory    Category      = BuildCategory.Building;
    public ResourceType     CostType;
    public int              CostAmount;
    public GameObject       Prefab;
    [Tooltip("자재 운반 후 건설 (서브 코어 등 2단계 건설)")]
    public bool             UsesBlueprint = false;
}
