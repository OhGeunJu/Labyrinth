using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ResourceNodeData")]
public class ResourceNodeData : ScriptableObject
{
    [Tooltip("이 노드가 내놓는 자원 아이템. **이것이 노드의 정체성이다** — " +
             "예전엔 ResourceType enum이 옆에 같이 있었는데, 이 필드와 늘 같은 것을 가리켜서 지웠다.")]
    public ItemData DropItem;

    // 목재 묶음을 내놓는 노드 = 나무. 채굴이 아니라 **벌목**이고, UI 문구도 스킬도 갈린다.
    // 예전엔 `ResourceType == Wood`로 봤는데, 그러면 모드가 넣은 나무는 영영 벌목이 안 된다.
    // 묶음으로 보면 `"resourceGroup": "Wood"` 자원을 내놓는 것은 무엇이든 나무로 취급된다.
    public bool IsTree => DropItem is ResourceItemData r && r.Group == ResourceGroup.Wood;

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

    [Tooltip("이 노드에 동시에 붙을 수 있는 폰 수. 0이면 제한 없음. " +
             "노드가 차지하는 칸 수를 기준으로 잡으면 화면과 맞는다 (1×1은 1~2, 2×2는 3~4).")]
    [Min(0)] public int MaxWorkers = 2;
}
