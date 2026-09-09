using UnityEngine;

// NPC의 **역할** — 무엇을 하는 사람인가(주민·경비·상인·행상인).
//
// **종족이 아니다.** 예전엔 `UnitData`를 상속해서 `Villager`·`Npc_Merchant` 에셋이 종족 데이터를
// 겸했다 — "주민"이라는 종족이 생긴 셈이라, 인간 주민과 엘프 주민을 만들려면 에셋을 둘로 늘리고
// 이름 풀·스탯을 복붙해야 했다(종족 × 역할의 곱셈).
//
// 이제 **조합은 프리팹이 한다**: `UnitBrain`이 종족(`unitData`)과 역할(`npcData`)을 따로 들고,
// 인간 상인 = 프리팹(Human + 상인 역할), 엘프 상인 = 프리팹(Elf + **같은** 상인 역할)이다.
// 역할 에셋은 종족을 모르므로 종족이 몇이 되든 하나로 족하다.
//
// CLAUDE.md의 세 축(종족=`UnitData` / 세력=`FactionData` / AI 성향=`AiProfileData`)에
// **역할**이 하나 더 붙은 꼴이다 — 그 축들과 마찬가지로 서로를 모른다.
[CreateAssetMenu(menuName = "DungeonColony/NpcData")]
public class NpcData : ScriptableObject
{
    public NpcType NpcType = NpcType.Villager;

    [Tooltip("화면에 보이는 역할 이름(`떠돌이`·`마을 사람`). **비우면 타입 기본값**을 쓴다 — " +
             "역할이 늘 때마다 문구를 채우게 하면 하나 빠뜨렸을 때 에셋 이름(`Drifter`)이 그대로 뜬다.\n" +
             "`UnitData.RaceName`·`FactionData.FactionName`과 같은 자리 — **식별자로 쓰지 말 것**(번역 대상).")]
    public string RoleName;

    // 이 역할을 뭐라고 부르나. **에셋 이름을 그대로 쓰지 않는다** — 그건 식별자라 영어이고,
    // 화면에 `Drifter`가 뜨면 그게 곧 번역 안 된 UI다.
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(RoleName) ? RoleName : NpcType switch
        {
            NpcType.Villager      => "마을 사람",
            NpcType.Guard         => "경비",
            NpcType.ShopMerchant  => "상인",
            NpcType.EventMerchant => "행상인",
            NpcType.Drifter       => "떠돌이",
            _                     => name,
        };

    [Tooltip("배회 반경 (Villager, Guard)")]
    public float WanderRadius = 10f;

    [Tooltip("도주 감지 범위 (Villager)")]
    public float FleeDetectRange = 8f;

    [Tooltip("EventMerchant 머무는 시간(초)")]
    public float StayDuration = 120f;

    // **역할의 기본 상품일 뿐이다.** 무엇을 파는가는 폰에 붙인 `Merchant` 컴포넌트가 들고,
    // 그쪽이 비어 있을 때만 이 값이 쓰인다(`UnitBrain.EnsureMerchant`). 여기에만 적을 수
    // 있으면 종류마다 역할 에셋이 하나씩 필요해진다 — 역할 × 상품의 곱셈이 된다.
    [Tooltip("상인 역할의 **기본** 상품. 프리팹의 Merchant 컴포넌트에 물린 것이 이긴다.")]
    public MerchantData MerchantData;
}
