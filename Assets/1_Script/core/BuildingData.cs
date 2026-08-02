using UnityEngine;

// **레지스트리 데이터다.** `Assets/Resources/Data/Buildings/` 에 두면 `DataManager`가 자동으로
// 집어 건설 목록에 올린다 — 씬 배열에 손으로 등록할 필요가 없다.
// (2026-08-01 이전에는 `5_Data/Building` + `BuildModeManager.placeableBuildings` 씬 배열이었다.
//  씬에 사는 목록이라 에디터 도구가 등록해도 씬을 저장 안 하면 조용히 날아갔고, 모드가
//  건물을 추가할 진입점도 없었다. `docs/data.md` "테이블은 레지스트리에서 끌어온다"와 같은 이유.)
[CreateAssetMenu(menuName = "DungeonColony/BuildingData")]
public class BuildingData : ScriptableObject
{
    [Tooltip("식별자. 비우면 에셋 파일명을 쓴다(권장). 표시명과 달리 번역되지 않는다.")]
    public string           Id;

    [Tooltip("화면에 보이는 이름. 번역 대상이므로 **식별자로 쓰지 말 것**.")]
    public string           BuildingName  = "건물";
    public BuildCategory    Category      = BuildCategory.Building;

    [Tooltip("건설 목록 안에서의 순서. 작을수록 앞. 같으면 이름순. " +
             "폴더 스캔은 순서를 보장하지 않으므로 이 값이 메뉴 순서의 유일한 근거다.")]
    public int              SortOrder     = 0;
    [Tooltip("차지하는 타일 수 (가로×세로). 회전하면 축이 바뀐다. " +
             "모델이 이 크기에 맞게 만들어져 있어야 한다 — 코드는 칸만 잡고 모양은 안 건드린다.")]
    public Vector2Int       Size = Vector2Int.one;

    [Tooltip("건설 비용으로 쓰는 자원 아이템. 비우면 무료. " +
             "예전엔 ResourceType enum이었는데, 그 값이 자원 아이템과 1:1이라 이름만 하나 더 만드는 " +
             "꼴이었다 — 게다가 UI에 enum 이름(IronOre)이 그대로 찍혔다.")]
    public ItemData         CostItem;
    public int              CostAmount;

    [Tooltip("이 건물을 짓기 위해 끝내야 하는 연구. 비우면 처음부터 지을 수 있다. " +
             "**그 건물이 다루는 레시피와 같은 연구를 걸 것** — 제재소를 지을 수 있는데 " +
             "판자를 못 만들거나 그 반대면, 지어 놓고 쓸 수 없는 건물이 생긴다.")]
    public ResearchData     RequiredResearch;

    // 지금 지을 수 있나. `ResearchManager`가 없으면(씬에 안 놓였으면) 막지 않는다 —
    // 연구 시스템이 없는 씬에서 건설이 통째로 죽는 것보다 낫다.
    public bool IsUnlocked =>
        RequiredResearch == null
        || ResearchManager.Instance == null
        || ResearchManager.Instance.IsCompleted(RequiredResearch);
    public GameObject       Prefab;
    [Tooltip("자재 운반 후 건설 (서브 코어 등 2단계 건설)")]
    public bool             UsesBlueprint = false;
    [Tooltip("바닥(floor) substrate. 코어 무관·공허에 배치 가능(바닥 요구 우회), 휴면/장애물 처리 안 함. 프리팹에 Floor 컴포넌트 필요.")]
    public bool             IsFloor = false;
    [Tooltip("타일 점유: 배치 시 footprint 타일을 통행 불가로 등록(유닛이 못 지나감). 벽·작업대 등 실체 건물은 true, 창고·침대·농지처럼 유닛이 밟는 건물은 false. 철거 시 자동 해제. 중복 배치는 이 값과 무관하게 금지됨.")]
    public bool             OccupiesTile = false;
}
