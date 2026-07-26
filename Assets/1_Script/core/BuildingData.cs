using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/BuildingData")]
public class BuildingData : ScriptableObject
{
    public string           BuildingName  = "건물";
    public BuildCategory    Category      = BuildCategory.Building;
    public ResourceType     CostType;
    public int              CostAmount;
    public GameObject       Prefab;
    [Tooltip("자재 운반 후 건설 (서브 코어 등 2단계 건설)")]
    public bool             UsesBlueprint = false;
    [Tooltip("바닥(floor) substrate. 코어 무관·공허에 배치 가능(바닥 요구 우회), 휴면/장애물 처리 안 함. 프리팹에 Floor 컴포넌트 필요.")]
    public bool             IsFloor = false;
    [Tooltip("타일 점유: 배치 시 footprint 타일을 통행 불가로 등록(유닛이 못 지나감). 벽·작업대 등 실체 건물은 true, 창고·침대·농지처럼 유닛이 밟는 건물은 false. 철거 시 자동 해제. 중복 배치는 이 값과 무관하게 금지됨.")]
    public bool             OccupiesTile = false;
}
