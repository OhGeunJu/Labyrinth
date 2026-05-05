using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/MinionData")]
public class MinionData : UnitData
{
    [Header("소환 비용")]
    public ResourceType SummonCostType;
    public int SummonCostAmount = 10;

    [Header("허용 작업 (비어있으면 모두 허용)")]
    public List<JobCategory> AllowedJobs = new();

    [Header("특수 능력")]
    [Tooltip("우물 없이도 직접 물을 공급할 수 있는 하수인")]
    public bool CanProvideWater = false;
}
