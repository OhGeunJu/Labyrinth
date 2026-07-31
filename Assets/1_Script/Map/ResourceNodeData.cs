using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ResourceNodeData")]
public class ResourceNodeData : ScriptableObject
{
    public ResourceType ResourceType = ResourceType.Stone;
    public ItemData DropItem;

    [Tooltip("배치 시 사용할 프리팹. 비어있으면 BuildModeManager의 기본 프리팹 사용")]
    public GameObject Prefab;

    // 단위는 **스킬-초**(스킬 수치 × 작업한 초). 스킬 10인 폰이 15초 붙어 있으면 150.
    // 그래서 "초보(스킬 10) 기준 몇 초에 한 번 나오는가 × 10"으로 읽으면 된다.
    [Tooltip("1회 산출까지 필요한 작업량 (스킬-초 = 스킬 수치 × 작업 초). 스킬 10이 15초 → 150")]
    public float GaugeRequired = 100f;

    [Tooltip("게이지 1회 달성 시 드롭 수량 범위")]
    public int DropMin = 1;
    public int DropMax = 3;

    [Tooltip("이 자원 채굴의 권장 스킬 레벨 (XP 도전 비율 계산용)")]
    public float RequiredSkill = 0f;
}
