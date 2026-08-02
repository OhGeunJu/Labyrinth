using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/SeedData")]
public class SeedData : ItemData
{
    [Header("농사")]
    public ItemData HarvestItem;
    public int      HarvestAmount   = 3;
    public float    GrowthTime      = 60f;   // 초
    public float    MoistureDecay   = 5f;    // 초당 수분 감소 (0~100 기준)
    public float    WaterFillAmount = 100f;  // 물 한 번 줄 때 채워지는 수분량
}
