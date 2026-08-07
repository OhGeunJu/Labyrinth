using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/FoodData")]
public class FoodData : ItemData
{
    public override string TypeName => "음식";

    [Tooltip("섭취 시 회복되는 허기량 (0-100)")]
    public float NutritionValue = 30f;

    [Tooltip("섭취 시 걸리는 시간 (초)")]
    public float EatTime = 2f;

    public override int MaxStack => 20;
}
