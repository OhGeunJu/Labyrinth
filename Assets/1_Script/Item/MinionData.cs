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

    [Header("허용 작업 (비어있으면 모두 허용)")]
    public List<JobCategory> AllowedJobs = new();

    [Header("특수 능력")]
    [Tooltip("우물 없이도 직접 물을 공급할 수 있는 하수인")]
    public bool CanProvideWater = false;
}
