using System.Collections.Generic;
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

    // **건설 비용 — 여러 재료가 든다**(2026-09-17, 사용자 결정: *"건물들도 나무랑 써야지"*).
    //
    // 한 칸(`CostItem` + `CostAmount`)이던 동안에는 *돌이냐 나무냐*를 골라야 했고, 그래서
    // 지하로 내려올 때 `0d. Swap Wood Costs To Stone`이 **목재 비용을 전부 돌로 갈아엎었다**
    // (그때는 지하에 나무가 없었다). 이제 1층에 균사림·이끼굴이 있고 2층에 단단한 나무가
    // 나므로 그 전제가 뒤집혔는데, **한 칸으로는 "돌 벽에 나무 문"을 적을 자리가 없다.**
    //
    // `RecipeData.Ingredient`를 빌려 쓴다 — 필드가 `Item`/`Amount`로 정확히 같다.
    // **공용 타입으로 옮기지는 않는다**: 유니티가 직렬화 타입을 못 찾으면 이미 저작된 값이
    // 통째로 날아간다(`TraitData.StatOffset`을 빌려 쓴 것과 같은 규칙).
    [Tooltip("건설 비용. 비우면 무료. 여러 줄이면 **전부** 있어야 짓는다.")]
    public List<RecipeData.Ingredient> Costs = new();

    // 부서질 때까지 버티는 양(`BuildingHealth`). **0이면 비용에서 뽑는다**(`40 + 3 × 값`) — 여든다섯을 손으로 적으면
    // 비용을 바꿀 때마다 체력이 뒤처진다. 특별히 단단하거나 약해야 하는 것만 적는다.
    [Tooltip("최대 체력. 0이면 건설 비용의 시세에서 뽑는다(40 + 3 × 값).")]
    [Min(0f)] public float MaxHealth = 0f;

    // **옛 한 칸 — 저작하는 자리가 아니다.** 이름을 그대로 둬야 이미 구워진 에셋 쉰여덟의
    // 값이 들어온다(직렬화 키가 필드 이름이다). `OnEnable`이 `Costs`로 흡수하므로 **읽는
    // 쪽은 `Costs`만 본다** — 두 자리에 답이 있으면 언젠가 한쪽만 고쳐진다.
    //
    // 비우는 것은 `F. Migrate Building Costs`가 한다(그때 이 필드는 껍데기만 남는다).
    [SerializeField, HideInInspector] ItemData CostItem;
    [SerializeField, HideInInspector] int      CostAmount;

    void OnEnable()
    {
        if (Costs.Count == 0 && CostItem != null && CostAmount > 0)
            Costs.Add(new RecipeData.Ingredient { Item = CostItem, Amount = CostAmount });
    }

    // **짓는 데 무엇이 드는가는 여기 하나가 답한다.** 부르는 쪽이 `Costs`를 직접 훑어도
    // 되지만, *무료인가*·*모자란 것이 무엇인가*는 일곱 군데(커서 힌트·목록·청사진·철거
    // 환불·살펴보기 창·우클릭 창·즉시 배치)가 각자 세던 물음이라 여기로 모은다.
    public bool IsFree
    {
        get
        {
            foreach (var c in Costs)
                if (c.Item != null && c.Amount > 0) return false;
            return true;
        }
    }

    // 이 자리에서 지금 살 수 있나 — **핵 영역 안의 창고 + 배낭**(`MaterialPool`이 그 축이다).
    public bool CanAfford(Vector3 worldPos)
    {
        foreach (var c in Costs)
        {
            if (c.Item == null || c.Amount <= 0) continue;
            if (MaterialPool.AvailableNear(c.Item, worldPos) < c.Amount) return false;
        }
        return true;
    }

    // **여러 칸을 한 번에 그어 짓는가**(두 번 클릭 — 기준을 찍고 늘였다 줄였다 하다 다시 클릭).
    //
    // 크기(1×1)로 가르면 안 된다 — 우리·작업대·서브코어도 한 칸이라 **잇대어 쓰지 않는 것까지
    // 줄로 그어졌다**(실제로 그랬다). "한 칸이냐"와 "잇대어 쓰느냐"는 다른 물음이다.
    //
    // 켜는 것은 **벽·바닥처럼 줄지어 쓰는 것**뿐이다. 바닥은 면으로, 나머지는 줄로 그어진다
    // (`BuildModeManager.RectDrag` — 바닥을 줄로만 깔면 방 하나에 열 번을 긋는다).
    [Tooltip("벽·바닥처럼 잇대어 쓰는 것인가. 켜면 두 번 클릭으로 줄(또는 면)을 한 번에 짓는다.")]
    public bool             DragBuild;

    // **줄인가 면인가는 따로 묻는다**(2026-09-02).
    //
    // 전에는 `IsFloor`가 그 답을 겸했는데, 그건 *"바닥 데이터로 깔리는가"*라는 **다른 물음**이다.
    // 밭은 바닥이 아니지만 **면으로 까는 물건**이다 — 밭 한 뙈기를 줄로만 깔면 스무 번을 긋는다.
    [Tooltip("끌 때 줄이 아니라 **면**으로 깔린다. 바닥은 이걸 안 켜도 면이다(`IsFloor`).")]
    public bool             AreaDrag;

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
    [Tooltip("다리 — 절벽을 가로질러야만 놓인다(`Bridge.WhyNot`). 프리팹에 `Bridge` 컴포넌트가 있어야 한다. " +
             "격자 배치로만 놓는다.")]
    public bool             IsBridge = false;

    // **경사를 따르는 포장 ↔ 경사를 무시하는 기단.** 두 물음이 다르다 —
    // 포장은 *"이 땅을 딛기 좋게 한다"*이고 기단은 *"여기에 평평한 자리를 만든다"*다.
    // 절차 지형에서는 뒤엣것이 없으면 **건물을 세울 평평한 자리가 거의 없다**(평탄 판정을
    // 기울기로 바꾼 뒤에도 그렇다). 이어 붙은 기단은 **높이가 하나**이므로 그 위에서는
    // 평탄 검사가 저절로 통과한다.
    [Tooltip("평탄 기단 — 지면 경사를 안 따르고 **이어 붙은 바닥이 모두 같은 높이**로 깔린다. " +
             "`IsFloor`가 켜져 있을 때만 뜻이 있다. 끄면 지금까지처럼 칸마다 지면 높이를 따른다.")]
    public bool             LevelFloor = false;

    // **0이 기본이고 0은 한도 없음이다.** 처음엔 3m로 막아 뒀는데, 그건 *"경사와 상관없이
    // 평평한 자리를 만든다"*는 이 물건의 뜻을 절반만 지킨 것이었다 — 비탈을 조금만 내려가도
    // 안 깔려서, 정작 기단이 필요한 자리에서 거절당한다. 공중 다리가 걱정이면 **막는 것이
    // 아니라 값을 치르게 하는 쪽**이 맞고(치마가 길어질수록 자재가 는다), 그건 나중 일이다.
    [Tooltip("기단이 지면에서 이만큼까지 뜰 수 있다(m). **0이면 한도 없음.** " +
             "값을 넣으면 더 내려가야 하는 칸에는 안 깔린다.")]
    public float            LevelMaxStep = 0f;

    // **계단은 기단의 특수한 경우다** — 같은 바닥 데이터인데 칸마다 한 단씩 낮아질 뿐이다.
    // 따로 만들면 배치·미리보기·철거를 두 벌 갖게 되고, 무엇보다 **폰이 딛는 면이 두 종류**가
    // 된다(그러면 길찾기의 오르막 판정도 둘로 갈린다).
    [Tooltip("계단 — 그은 방향으로 한 칸에 한 단씩 낮아진다. `LevelFloor`가 켜져 있어야 한다. " +
             "한 단 높이는 `BuildModeManager.levelStep`이고 **폰의 `StepUp`보다 낮아야** 걸어 오른다.")]
    public bool             Stairs = false;
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

    // ── 칸마다 다르게 막기 ────────────────────────────────────────────────
    //
    // **걸어 들어가는 건물은 발자국 전체를 막을 수도, 전부 틀 수도 없다.** 전부 막으면
    // 폰이 못 들어가고(그 안의 침대·작업대는 영영 못 쓴다), 전부 트면 벽을 통과한다.
    // `OccupiesTile` 하나로는 답이 없는 물음이라 **칸마다 적는다.**
    //
    // 비워 두면 지금까지처럼 `OccupiesTile`이 발자국 전체에 걸린다 — **기존 건물은
    // 아무것도 안 바뀐다.**
    //
    // 한 줄이 한 행이고 `#`가 막는 칸, 그 밖(`.`·공백)이 트인 칸이다:
    //
    //     "#####"      ← BlockMask[0]  = 가장 **뒤**(+Z) 줄
    //     "#...#"
    //     "#...#"
    //     "##.##"      ← BlockMask[3]  = 가장 **앞**(−Z) 줄, 가운데가 문간
    //
    // **위에서 내려다본 그림 그대로 적는다**(첫 줄이 위, 왼쪽이 −X) — 반대로 잡으면
    // 문이 뒤로 가고, 그건 놓아 보기 전에는 안 보인다. 회전은 배치할 때 알아서 돈다.
    //
    // 줄 수가 `Size.y`와, 줄 길이가 `Size.x`와 다르면 **무시하고 경고한다** —
    // 조용히 절반만 맞춰 주면 "왜 여기가 막히지"를 영영 못 찾는다.
    [Tooltip("칸마다 길을 막는지. 한 줄이 한 행, `#`가 막는 칸. 첫 줄이 가장 뒤(+Z), 왼쪽이 −X. " +
             "비우면 `OccupiesTile`이 발자국 전체에 걸린다.")]
    public string[]         BlockMask;

    // **얇은 벽 — 칸이 아니라 변을 막는다**(2026-10-01, `EdgeWalls` · `ThinWall`). 칸은 비어 있어 침대·작업대를
    // 벽에 붙여 놓을 수 있고, 건너지 못하는 것은 건물 앞(+Z)이 가리키는 변 하나다. 이 값이 켜지면
    // `OccupiesTile`은 끈다(칸을 막으면 얇은 벽이 아니라 외곽벽이다) · 다른 건물과 같은 칸에 놓인다(겹치는 것은
    // 같은 변의 얇은 벽끼리만 막는다).
    [Tooltip("얇은 벽 — 건물 앞(+Z)이 가리키는 칸의 변 하나를 막는다. 칸 자체는 비워 둔다(OccupiesTile 끌 것).")]
    public bool             EdgeWall = false;

    // 마스크가 쓸 만한가. 한 번 재고 결과를 든다 — 배치마다 다시 재면 경고가 쏟아진다.
    bool _maskChecked;
    bool _maskValid;

    public bool HasBlockMask
    {
        get
        {
            if (_maskChecked) return _maskValid;
            _maskChecked = true;
            _maskValid   = false;

            if (BlockMask == null || BlockMask.Length == 0) return false;

            if (BlockMask.Length != Mathf.Max(1, Size.y))
            {
                Debug.LogWarning($"[건물] `{name}`의 `BlockMask`가 {BlockMask.Length}줄인데 " +
                                 $"크기는 {Size.x}×{Size.y}다 — 무시한다.");
                return false;
            }
            foreach (var row in BlockMask)
                if (row == null || row.Length != Mathf.Max(1, Size.x))
                {
                    Debug.LogWarning($"[건물] `{name}`의 `BlockMask` 한 줄이 " +
                                     $"{row?.Length ?? 0}칸인데 크기는 {Size.x}×{Size.y}다 — 무시한다.");
                    return false;
                }

            _maskValid = true;
            return true;
        }
    }

    // 회전 전 좌표계에서 그 칸이 길을 막는가. `lz`는 0이 **앞**(−Z)이다 — 마스크는 첫 줄이
    // 뒤라서 뒤집어 읽는다(그림대로 적을 수 있어야 저작이 성립한다).
    public bool BlocksLocal(int lx, int lz)
    {
        if (!HasBlockMask) return OccupiesTile;

        int row = Mathf.Max(1, Size.y) - 1 - lz;
        if (row < 0 || row >= BlockMask.Length) return OccupiesTile;

        var line = BlockMask[row];
        if (lx < 0 || lx >= line.Length) return OccupiesTile;
        return line[lx] == '#';
    }

    void OnValidate() { _maskChecked = false; }
}
