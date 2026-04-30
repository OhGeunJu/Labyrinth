using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ItemData")]
public class ItemData : ScriptableObject
{
    public string ItemName;
    [TextArea(2, 5)] public string Description;
    public Sprite Icon;
    [Min(0)] public float Weight = 0f;

    [Header("그리드 크기 (셀 단위)")]
    [Min(1)] public int Width  = 1;
    [Min(1)] public int Height = 1;

    // 기본은 1칸 1개. 스택 가능한 자원/소비품은 ResourceItemData에서 오버라이드.
    public virtual int MaxStack => 1;
}
