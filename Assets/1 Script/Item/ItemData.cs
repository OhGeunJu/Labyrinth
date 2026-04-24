using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ItemData")]
public class ItemData : ScriptableObject
{
    public string ItemName;
    [TextArea(2, 5)] public string Description;
    public ResourceType ResourceType;
    public Sprite Icon;
    [Min(1)] public int MaxStack = 999;
    [Min(0)] public float Weight  = 0f;
}
