using System.Collections.Generic;
using UnityEngine;

// **레지스트리 데이터다.** `Assets/Resources/Data/Units/` 에 두면 `DataManager`가 자동으로 집어
// 던전 핵의 소환 목록에 올린다 — 씬의 `DungeonCore.minionEntries`에 손으로 등록할 필요가 없다.
// (`BuildingData`가 `Resources/Data/Buildings/`로 간 것과 같은 이유: 씬에 사는 목록은 조용히
//  날아가고, 모드가 종족을 추가할 진입점도 없다.)
[CreateAssetMenu(menuName = "DungeonColony/MinionData")]
public class MinionData : UnitData
{
    [Header("소환")]
    [Tooltip("소환할 폰 프리팹. **에셋이어야 한다** — 에셋은 씬 오브젝트를 참조할 수 없다. " +
             "씬 오브젝트를 쓰던 기존 배선은 DungeonCore.minionEntries에 남겨 두면 그쪽이 이긴다.")]
    public GameObject Prefab;

    [Tooltip("소환 목록 안에서의 순서. 작을수록 앞. 같으면 이름순. " +
             "폴더 스캔은 순서를 보장하지 않으므로 이 값이 메뉴 순서의 유일한 근거다.")]
    public int SortOrder = 0;

    [Header("소환 비용")]
    [Tooltip("소환 비용으로 쓰는 자원 아이템. 비우면 무료.")]
    public ItemData SummonCostItem;
    public int SummonCostAmount = 10;

    // **연구로 여는 하수인.** 비우면 처음부터 열려 있다 — `RecipeData.RequiredResearch`와
    // 같은 규약이고, 그래서 저작 방법도 같다.
    //
    // 왜 소환 쪽에 게이트가 필요한가: 도축이 내놓는 뼈가 **쓸 데가 있어야** 치우는 일이
    // 값을 갖는다. 뼈 → 연구 → 해골 하수인 → 그 해골이 또 뼈를 남긴다는 고리가 닫히면,
    // 시체를 치우는 것이 잡일이 아니라 **생산 라인의 앞단**이 된다.
    [Header("해금")]
    [Tooltip("이 연구를 마쳐야 소환 목록에 뜬다. 비우면 처음부터 열려 있다.")]
    public ResearchData RequiredResearch;

    // 지금 소환할 수 있나 — **`ResearchManager`가 없으면 열린 것으로 본다**(연구 체계를 안 쓰는
    // 씬에서 소환이 통째로 막히면 안 된다. `RecipeData` 쪽과 같은 폴백).
    public bool IsUnlocked =>
        RequiredResearch == null
        || ResearchManager.Instance == null
        || ResearchManager.Instance.IsCompleted(RequiredResearch);

    [Header("허용 작업 (비어있으면 모두 허용)")]
    public List<JobCategory> AllowedJobs = new();

    [Header("특수 능력")]
    [Tooltip("우물 없이도 직접 물을 공급할 수 있는 하수인")]
    public bool CanProvideWater = false;
}
