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

    [Tooltip("이게 무엇을 하는 물건인가. 이름만으로는 '보석 연마대'가 무엇을 만드는지 알 수 없고, " +
             "'서브코어'는 이름 자체가 설명을 요구한다. 한두 문장으로 짧게.")]
    [TextArea(2, 4)]
    public string           Description;

    [Tooltip("건설 목록의 어느 갈래에 뜨는가. **비워 두면 `기타`** — 어디에도 안 붙는 것이라는 " +
             "뜻이지 '아직 안 정했다'는 뜻이 아니므로, 새 건물은 갈래를 골라 줄 것.")]
    public BuildCategory    Category      = BuildCategory.Misc;

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

    // **여러 칸을 한 번에 그어 짓는가**(두 번 클릭 — 기준을 찍고 늘였다 줄였다 하다 다시 클릭).
    //
    // 크기(1×1)로 가르면 안 된다 — 우리·작업대·서브코어도 한 칸이라 **잇대어 쓰지 않는 것까지
    // 줄로 그어졌다**(실제로 그랬다). "한 칸이냐"와 "잇대어 쓰느냐"는 다른 물음이다.
    //
    // 켜는 것은 **벽·바닥처럼 줄지어 쓰는 것**뿐이다. 바닥은 면으로, 나머지는 줄로 그어진다
    // (`BuildModeManager.RectDrag` — 바닥을 줄로만 깔면 방 하나에 열 번을 긋는다).
    [Tooltip("벽·바닥처럼 잇대어 쓰는 것인가. 켜면 두 번 클릭으로 줄(바닥은 면)을 한 번에 짓는다.")]
    public bool             DragBuild;

    [Tooltip("이 건물을 짓기 위해 끝내야 하는 연구. 비우면 처음부터 지을 수 있다. " +
             "**그 건물이 다루는 레시피와 같은 연구를 걸 것** — 제재소를 지을 수 있는데 " +
             "판자를 못 만들거나 그 반대면, 지어 놓고 쓸 수 없는 건물이 생긴다.")]
    public ResearchData     RequiredResearch;

    // 지금 지을 수 있나. `ResearchManager`가 없으면(씬에 안 놓였으면) 막지 않는다 —
    // 연구 시스템이 없는 씬에서 건설이 통째로 죽는 것보다 낫다.
    public bool IsUnlocked => ResearchDone;

    public bool ResearchDone =>
        RequiredResearch == null
        || ResearchManager.Instance == null
        || ResearchManager.Instance.IsCompleted(RequiredResearch);

    // **왜 잠겼는지 한 줄로.** 잠금 사유를 부르는 쪽마다 조립하면 창마다 다른 말이 뜬다
    // (건설 타일 호버 · 청사진 · 살펴보기 창).
    public string LockReason
    {
        get
        {
            if (!ResearchDone) return $"{RequiredResearch.ResearchName} 연구가 필요합니다";
            return null;
        }
    }
    public GameObject       Prefab;
    [Tooltip("자재 운반 후 건설 (서브 코어 등 2단계 건설)")]
    public bool             UsesBlueprint = false;
    [Tooltip("바닥(floor) substrate. 코어 무관·공허에 배치 가능(바닥 요구 우회), 휴면/장애물 처리 안 함. 프리팹에 Floor 컴포넌트 필요.")]
    public bool             IsFloor = false;
    [Tooltip("타일 점유: 배치 시 footprint 타일을 통행 불가로 등록(유닛이 못 지나감). " +
             "벽·작업대·자원 노드·창고처럼 **몸이 부딪히는 것**은 true. " +
             "침대·농지는 false — 폰이 그 위에 눕고 그 위에서 일해야 한다. " +
             "\n\n**콜라이더 크기와는 다른 축이다.** 콜라이더는 '어디를 눌러야 잡히나'(클릭 " +
             "판정)이고 이 값은 '길을 막나'다 — 길찾기는 여기서 등록한 타일 점유를 보지 " +
             "콜라이더를 안 본다(obstacleLayer는 Wall뿐). 둘을 같은 것으로 보면 " +
             "'2×2 창고인데 콜라이더가 1×1이라 가운데만 눌린다'가 된다(2026-08-12). " +
             "\n\n**대신 둘은 짝을 맞춰야 한다: 이 값이 false면 콜라이더는 트리거여야 한다.** " +
             "안 그러면 지도는 '빈 칸'이라 하는데 몸은 부딪혀, 길찾기가 통과하는 경로를 내고 " +
             "폰이 거기 처박힌다. 침대의 ApproachPosition이 '길찾기 그래프에 없는 장애물'을 " +
             "피하려고 옆면을 고르던 것이 그 증상이었다. 크기는 늘 발자국에 맞추고(클릭), " +
             "막을지 말지는 이 값 하나로 정한다(길). " +
             "\n\n철거 시 자동 해제. 중복 배치는 이 값과 무관하게 금지됨.")]
    public bool             OccupiesTile = false;
}
