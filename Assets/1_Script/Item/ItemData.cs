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

    [Header("경제")]
    [Tooltip("기준 가격 (G). 상인 풀에서 BasePrice가 0이면 이 값 사용. 인벤토리 툴팁에도 표시.")]
    [Min(0)] public int BaseValue = 0;

    // 기본은 1칸 1개. 스택 가능한 자원/소비품은 ResourceItemData에서 오버라이드.
    public virtual int MaxStack => 1;
}
