using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/GameEventData")]
public class GameEventData : ScriptableObject
{
    [Header("공통")]
    public string EventName;
    [TextArea] public string Description;
    public Sprite Icon;
    [Tooltip("랜덤 선택 가중치. 높을수록 자주 발생")]
    public float Weight = 1f;
    // `GameTimeManager`와 같은 축(배속이 걸린 초)이다 — **하루 = 1800초**가 기준.
    // 크게 잡으면 초반 추첨 풀에서 빠져 다른 이벤트만 반복해서 뜬다(습격 600·합류 1200이
    // 그래서 한동안 아예 안 나왔다, 2026-08-06).
    [Tooltip("이 이벤트가 발동될 수 있는 최소 경과 시간(초). 하루 = 1800초")]
    public float MinGameTime = 0f;
    public GameEventType EventType;

    [Header("침공 (Raid)")]
    public List<GameObject> EnemyPrefabs = new();
    [Tooltip("스폰할 적 수")]
    public int EnemyCount = 5;

    [Header("상인 (Merchant)")]
    // **누가 오느냐(종족)와 무엇을 파느냐(상품)를 따로 굴린다.** 둘을 프리팹에 묶어 두면
    // 종족 × 종류만큼 프리팹이 필요해진다 — 고블린 무기상인, 엘프 무기상인, 인간 식료상인…
    // 사람과 등짐은 스폰할 때 조합한다. 종족을 하나 늘리는 값이 프리팹 한 개로 끝난다.
    [Tooltip("행상인으로 올 폰 프리팹(종족). 여럿이면 발동할 때마다 하나를 뽑는다.")]
    public List<GameObject> MerchantPrefabs = new();

    [Tooltip("그 행상인이 들고 오는 물건. 여럿이면 하나를 뽑는다. " +
             "비우면 프리팹이 들고 있는 것(컴포넌트 또는 역할 에셋) 그대로.")]
    public List<MerchantData> MerchantVariants = new();

    [Header("지원군 (Reinforcement)")]
    public List<GameObject> ReinforcementPrefabs = new();
    [Tooltip("스폰할 지원군 수")]
    public int ReinforcementCount = 3;

    // ── 습격에 딸려 오는 구원 ────────────────────────────────────────────────
    //
    // **혼자 굴러서는 도움이 안 된다.** 추첨으로만 오면 조용한 날에 와서 어슬렁거리다
    // 정작 몰려올 때는 없다 — 지원군이 뜻을 가지려면 **맞고 있을 때** 와야 한다.
    //
    // 그렇다고 늘 오면 습격이 무섭지 않다. 그래서 확률이고, **먼저 예고하지 않는다** —
    // 올지 안 올지 모르는 채로 막아야 오는 것이 반가운 일이 된다.
    [Tooltip("습격이 벌어질 때 이 지원군이 **딸려 올 수 있는가.** 켜면 추첨 풀과 별개로, " +
             "습격이 뜰 때마다 아래 확률로 굴려 조금 늦게 도착한다.")]
    public bool RescueOnRaid;

    [Tooltip("습격 한 번에 이 지원군이 올 확률.")]
    [Range(0f, 1f)] public float RescueChance = 0.4f;

    [Tooltip("습격이 벌어지고 몇 초 뒤에 도착하는가. **0이면 같이 나타나** 구원이 아니라 " +
             "그냥 편이 하나 더 있는 판이 된다 — 늦게 와야 '버티면 온다'가 성립한다.")]
    [Min(0f)] public float RescueDelay = 25f;

    [Header("발견 (Discovery)")]
    // **물건을 줍는다 — 자원 노드를 심지 않는다.**
    //
    // 예전엔 거점 근처에 광맥을 하나 새로 세웠다. 그런데 광맥은 **자리를 차지하고 영구히
    // 남는 것**이라, 이벤트가 몇 번 돌면 거점 둘레가 광맥 밭이 되고 건설할 땅이 사라진다.
    // "발견"은 한 번의 사건이지 지형 변화가 아니다.
    //
    // 비우면 **레지스트리의 아무 아이템이나** 준다(모드가 넣은 것도 딸려 온다).
    [Tooltip("발견될 후보. 비우면 등록된 아이템 중에서 아무거나.")]
    public List<ItemData> DiscoveryItems = new();

    [Tooltip("발견 개수 범위.")]
    [Min(1)] public int DiscoveryMinAmount = 3;
    [Min(1)] public int DiscoveryMaxAmount = 12;
}
