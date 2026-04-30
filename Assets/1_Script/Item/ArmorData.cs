using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ArmorData")]
public class ArmorData : ItemData
{
    [Header("방어구")]
    public ArmorSlot Slot;
    public float DamageReduction = 5f;  // 피해 감소 (고정값)
}
