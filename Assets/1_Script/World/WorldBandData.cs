using System.Collections.Generic;
using UnityEngine;

// 지도 위를 도는 무리가 무엇을 하러 다니는가.
//
// **셋 다 "어디를 근거로 도는가"다.** 처음엔 소굴 무리를 "우리 거점으로 행군한다"로 만들었는데,
// 그건 떠도는 위협이 아니라 **눈에 보이는 타이머**였다(2026-08-10 사용자 지적). 지도 위의
// 무리는 제 볼일로 다녀야 하고, 마주치는 것이 사건이어야 한다.
//
// **작정하고 오는 습격은 무리가 아니라 예고된 사건이다** — `WorldBandManager`의 습격 계획이
// 맡는다("어디의 누가 며칠 뒤에 온다"). 그래야 막을 시간이 생기고, 소굴을 치는 데 이유가 붙는다.
public enum WorldBandBehavior
{
    Roam,      // 소굴 근방을 떠돈다. 마주치는 상단·순찰대·원정대와 싸우고, 때가 되면 돌아간다.
    Caravan,   // 마을 → 다른 마을(없으면 내 정착지). 마주치면 길 위에서 거래한다.
    Patrol,    // 마을 둘레를 돈다. 적대 무리를 보면 쫓아가 싸운다.
}

// **지도 위의 무리 한 종류.** "고블린 약탈대" · "평원 상단" · "마을 자경단" 같은 것.
//
// 여기 있는 것은 **종류**고, 실제로 지도에 서는 것은 `WorldBand`(런타임)다 —
// `MerchantData`(무엇을 팔 수 있는가)와 `Shop`(오늘 무엇이 진열됐는가)의 관계와 같다.
//
// **폰 프리팹을 든다.** 무리 자체는 명세(`PawnSpec`)로만 살지만, 약탈대가 거점에 닿으면
// 실제 적이 필드맵에 서야 한다 — 그때 찍을 것이 필요하다. 프리팹에는 `Faction`이 배선돼
// 있어야 하고(연결을 빠뜨리면 아무와도 관계가 없는 유령이 온다), 무리의 세력은 그와 별개로
// 여기서 정한다 — 지도 위의 판정은 폰을 찍지 않고도 답할 수 있어야 하기 때문이다.
[CreateAssetMenu(fileName = "Band", menuName = "DungeonColony/WorldBandData")]
public class WorldBandData : ScriptableObject
{
    [System.Serializable]
    public class MemberEntry
    {
        [Tooltip("이 자리에 설 폰 프리팹. **UnitBrain + Faction이 배선된 것**이어야 한다.")]
        public GameObject Prefab;

        [Tooltip("이 종류가 무리에 몇이나 끼는가.")]
        [Min(0)] public int MinCount = 1;
        [Min(0)] public int MaxCount = 3;

        [Tooltip("이 자리에 서는 사람의 등급 범위. 종족 기본을 덮는다 — " +
                 "같은 고블린이라도 소굴 잡졸이냐 두목이냐가 갈린다.")]
        public StatGrade MinGrade = StatGrade.FMinus;
        public StatGrade MaxGrade = StatGrade.E;
    }

    [Header("표시")]
    [SerializeField] string bandName = "무리";
    [Tooltip("지도 토큰 색. 비워도 세력을 알 수 없으니 종류마다 다르게 줄 것.")]
    [SerializeField] Color tokenColor = new(0.85f, 0.25f, 0.25f);

    [Header("정체")]
    [Tooltip("이 무리의 세력. **반드시 연결할 것** — 비우면 아무와도 관계가 없어 " +
             "적대 판정이 전부 중립으로 떨어진다(`Faction`과 같은 함정).")]
    [SerializeField] FactionData faction;

    [SerializeField] WorldBandBehavior behavior = WorldBandBehavior.Roam;

    [Header("구성")]
    [Tooltip("비어 있으면 무리가 서지 않는다.")]
    [SerializeField] List<MemberEntry> members = new();

    [Header("이동")]
    [Tooltip("월드유닛/초. 옛 원정대 속도(기본 5)와 견줘서 정했다 — " +
             "**이 값이 곧 '뿌리칠 수 있는가'다.** 느린 무리는 피할 수 있고 빠른 무리는 못 피한다.")]
    [SerializeField] float speed = 4.5f;

    [Tooltip("이 반경 안에 적대 대상이 들어오면 쫓는다. 0이면 아무도 쫓지 않는다(상단).")]
    [Min(0f)] public float ChaseRadius = 12f;

    [Header("장사 (상단만)")]
    [Tooltip("길 위에서 거래할 때 파는 것. 비우면 [거래한다]가 뜨지 않는다.")]
    [SerializeField] MerchantData merchant;

    [System.Serializable]
    public class LootEntry
    {
        public ItemData Item;

        [Tooltip("JSON 아이템을 가리킬 때(에셋이 없다) — `Item`이 비어 있으면 이 식별자로 찾는다.")]
        public string ItemId;

        // **`Item`을 직접 읽지 말 것** — JSON 아이템(종족별 고기 · 갑각)이 조용히 null이 된다(`LootEntry`와 같은 규약).
        public ItemData Resolve() => Item != null ? Item : DataManager.GetItem(ItemId);

        [Tooltip("쓰러뜨린 **한 명당** 이만큼 나온다.")]
        [Min(0)] public int MinPerKill;
        [Min(0)] public int MaxPerKill = 2;

        [Tooltip("한 명당 이 확률로 나온다. 1이면 반드시.")]
        [Range(0f, 1f)] public float Chance = 0.5f;
    }

    [Header("전리품")]
    [Tooltip("이겼을 때 쓰러뜨린 수만큼 나온다. **비우면 이겨도 아무것도 안 나온다** — " +
             "그러면 싸울 이유가 '길을 막아서'뿐이라 무리를 보면 늘 피하는 게 정답이 된다.\n" +
             "상단은 여기에 더해 진열 재고에서도 나온다(들고 다니던 것이므로).")]
    [SerializeField] List<LootEntry> loot = new();

    [Header("전력 보정")]
    [Tooltip("이 무리의 무장 수준. 전투 판정에서 스탯으로 낸 전력에 곱한다.\n" +
             "**장비를 여기로 대신한다** — 무리는 실제 장비를 안 들기 때문이다. 한쪽만 장비를 " +
             "세면 플레이어가 늘 이긴다. 잘 무장한 무리는 1.3, 굶주린 도적떼는 0.8쯤.")]
    [Min(0.1f)] public float PowerMultiplier = 1f;

    public string            BandName  => string.IsNullOrEmpty(bandName) ? name : bandName;
    public Color             TokenColor => tokenColor;
    public FactionData       Faction   => faction;
    public WorldBandBehavior Behavior  => behavior;
    public IReadOnlyList<MemberEntry> Members => members;
    public float             Speed     => Mathf.Max(0.1f, speed);
    public MerchantData      Merchant  => merchant;
    public IReadOnlyList<LootEntry> Loot => loot;

    // 쓸 수 없는 명세를 지도에 세우지 않는다 — 멤버 없는 무리는 전투도 표시도 성립하지 않는다.
    public bool IsUsable
    {
        get
        {
            if (members == null) return false;
            foreach (var m in members)
                if (m != null && m.Prefab != null && m.MaxCount > 0) return true;
            return false;
        }
    }
}
