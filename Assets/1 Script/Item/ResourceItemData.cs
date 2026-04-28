using UnityEngine;

// 자원 아이템 — 스택 가능 + ResourceType 분류 (Storage 필터, 자원 소비 등).
[CreateAssetMenu(menuName = "DungeonColony/ResourceItemData")]
public class ResourceItemData : ItemData
{
    public ResourceType ResourceType;
    [Min(1)] public int Stack = 999;

    public override int MaxStack => Stack;
}
