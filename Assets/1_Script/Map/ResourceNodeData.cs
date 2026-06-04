using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ResourceNodeData")]
public class ResourceNodeData : ScriptableObject
{
    public ResourceType ResourceType = ResourceType.Stone;
    public ItemData DropItem;

    [Tooltip("배치 시 사용할 프리팹. 비어있으면 BuildModeManager의 기본 프리팹 사용")]
    public GameObject Prefab;

    [Tooltip("게이지가 가득 찰 때까지 필요한 작업량 (Mining=1 기준 틱 수)")]
    public float GaugeRequired = 10f;

    [Tooltip("게이지 1회 달성 시 드롭 수량 범위")]
    public int DropMin = 1;
    public int DropMax = 3;

    [Tooltip("이 자원 채굴의 권장 스킬 레벨 (XP 도전 비율 계산용)")]
    public float RequiredSkill = 0f;
}
