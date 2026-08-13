using System.Collections.Generic;
using UnityEngine;

// 바이옴별 콘텐츠 게이팅 데이터 (step 7).
// 한 바이옴에서 등장하는 자원 노드 / 소환 가능한 하수인 / 개척 시 해금되는 연구를 정의.
// 탐험 보상 루프의 핵심: "그 바이옴에 가야만 얻는" 것을 데이터로 게이팅한다.
//
// 배치: Resources/Data/Biomes/ 아래에 두면 BiomeContent가 자동 로드(Biome별 1개 권장).
[CreateAssetMenu(menuName = "DungeonColony/BiomeContentData")]
public class BiomeContentData : ScriptableObject
{
    public Biome Biome = Biome.Plains;

    [Header("이 바이옴에서 등장하는 자원 노드 풀")]
    public List<ResourceNodeData> ResourceNodes = new();

    // ── 매장 자원 (월드맵 칸) ────────────────────────────────────────────────
    // "이 바이옴 칸을 파면 무엇이 얼마나 나오는가". 실제 칸별 수량은 **칸 좌표로 결정적으로**
    // 굴린다(`WorldDeposits`) — 저작 없이도 칸마다 다르고, 다시 실행해도 같은 값이 나온다.
    [System.Serializable]
    public class DepositEntry
    {
        public ItemData Item;

        [Tooltip("이 바이옴 칸에 이 자원이 묻혀 있을 확률. 1이면 항상.")]
        [Range(0f, 1f)] public float Chance = 1f;

        [Tooltip("묻혀 있을 때의 매장량 범위.")]
        [Min(0)] public int MinAmount = 20;
        [Min(0)] public int MaxAmount = 60;
    }

    [Header("이 바이옴 칸의 매장 자원")]
    [Tooltip("비어 있으면 그 바이옴 칸에는 아무것도 묻혀 있지 않다.")]
    public List<DepositEntry> Deposits = new();

    // ── 마을 시장 (월드맵 Town 칸) ───────────────────────────────────────────
    // "이 지역 마을에 가면 누가 장사하는가". 어느 상인이 서는지는 **칸 좌표로 결정적으로**
    // 고른다(`TownMarket`) — 마을마다 손으로 지정하지 않아도 지역색이 생기고, 다시 실행해도
    // 같은 마을엔 같은 상인이 선다.
    //
    // **지역별 시세가 여기서 나온다.** 같은 아이템이라도 `MerchantData.ItemOverrides`가
    // 바이옴마다 다르면(해안=생선 0.5배 / 내륙=생선 2배) 싸게 사서 비싸게 파는 교역이
    // 규칙을 따로 만들지 않아도 성립한다.
    [Header("이 바이옴 마을의 상인 풀")]
    [Tooltip("비어 있으면 그 바이옴의 마을엔 시장이 없다(도착 메뉴에서 잠긴다).")]
    public List<MerchantData> TownMerchants = new();

    // ── 마을 인력 (월드맵 Town 칸) ───────────────────────────────────────────
    // "이 지역 마을에서 누구를 구할 수 있는가". 상인과 같은 방식으로 **칸 좌표 + 날짜**에서
    // 결정적으로 뽑는다(`TownRecruits`) — 마을마다 손으로 채우지 않아도 지역색이 생기고,
    // 같은 날 다시 들르면 같은 사람이 서 있다.
    [System.Serializable]
    public class RecruitEntry
    {
        [Tooltip("고용하면 이걸 찍어낸다. **씬의 폰을 그대로 뜬 프리팹**이어야 한다 — " +
                 "종족·세력·AI 프로필이 배선돼 있지 않으면 아무와도 관계가 없는 폰이 태어난다.")]
        public GameObject Prefab;

        // **고용가는 여기 없다.** 값은 그 사람의 스탯이 정한다(`PawnSpec.Price`) —
        // 지역이나 후보마다 값을 따로 적으면 "왜 이 사람이 비싼가"의 답이 둘이 되고,
        // 등급 범위를 올렸을 때 값이 따라오지 않는다.

        [Tooltip("이 마을에서 구할 수 있는 사람의 등급 범위. 종족 데이터의 기본 범위를 **덮는다** — " +
                 "같은 인간이라도 어디서 만났느냐로 갈린다. 값도 여기에 딸려 온다(등급이 값을 정하므로).")]
        public StatGrade MinGrade = StatGrade.FMinus;
        public StatGrade MaxGrade = StatGrade.E;

        [Tooltip("뽑기 가중치.")]
        [Min(0)] public float Weight = 1f;
    }

    [Header("이 바이옴 마을에서 구할 수 있는 사람")]
    [Tooltip("비어 있으면 그 바이옴의 마을엔 구할 사람이 없다.")]
    public List<RecruitEntry> TownRecruits = new();

    // ── 적 소굴 (월드맵 EnemyLair 칸) ────────────────────────────────────────
    // "이 지역 소굴엔 누가 사는가". 상인·인력과 같은 방식으로 **칸 좌표 + 시드**에서
    // 결정적으로 고른다(`WorldLairs`) — 소굴을 하나하나 저작하지 않아도 지역색이 생긴다.
    //
    // **소굴은 무리의 출처다.** 소굴이 없으면 지도에 약탈대가 나올 데가 없고, 약탈대가
    // 없으면 소굴은 그냥 붉은 표식이다 — 둘은 서로의 이유가 된다.
    [System.Serializable]
    public class LairEntry
    {
        [Tooltip("이 소굴이 내보내는 무리. **Behavior가 Roam인 것**을 쓴다. " +
                 "예고된 습격도 이 구성으로 온다 — 소굴 하나에 무리 종류가 둘이면 " +
                 "'떠돌던 그놈들이 쳐들어왔다'가 성립하지 않는다.")]
        public WorldBandData Band;

        [Tooltip("소굴 이름. 비우면 무리 이름을 쓴다.")]
        public string LairName;

        [Tooltip("소굴이 품은 총 병력. 내보낼 때마다 줄고, 다 떨어지면 소굴이 사라진다.")]
        [Min(1)] public int MinStrength = 8;
        [Min(1)] public int MaxStrength = 18;

        [Tooltip("몇 **일**마다 떠돌이 무리를 내보내는가. 하루 길이(`GameTimeManager.DayDuration`)를 " +
                 "축으로 쓰므로 하루를 늘리면 같이 늘어난다. 0.5 = 반나절마다.")]
        [Min(0.05f)] public float SortieDays = 0.5f;

        [Tooltip("내보낸 무리가 밖에서 떠도는 기간(일). 지나면 근거지로 돌아가고 병력이 복귀한다.")]
        [Min(0.1f)] public float RoamDays = 1.5f;

        // ── 예고된 습격 ──────────────────────────────────────────────────────
        // 떠돌이와 **다른 축**이다. 떠돌이는 마주쳐야 사건이 되지만, 습격은 작정하고 온다 —
        // 대신 **미리 알려 준다.** 예고가 없으면 대비할 수 없어 그냥 재해가 되고,
        // 예고가 있으면 "그 전에 소굴을 친다"가 선택이 된다.

        [Tooltip("이 소굴이 **얼마나 자주 오려 하는가**(일). 0이면 습격을 아예 안 온다(떠돌기만 한다).\n" +
                 "**총량은 여기가 정하지 않는다** — 언제 하나 오는지는 `WorldBandManager.raidIntervalDays`" +
                 "(전역)가 정하고, 이 값은 그 차례를 누가 가져가느냐의 **가중치**로 읽힌다(작을수록 자주).\n" +
                 "소굴마다 제 시계를 들면 습격이 소굴 수만큼 곱해져, 바이옴에 소굴 종류를 얹는 것만으로 " +
                 "초반에 사방에서 몰려온다 — 소굴을 늘리는 것은 **빈도가 아니라 다양성**을 늘리는 일이어야 한다.")]
        [Min(0f)] public float RaidIntervalDays = 3f;

        [Tooltip("예고에서 실제 도착까지의 시간(일). **막을 시간이 여기서 나온다** — " +
                 "짧으면 예고가 통보가 되고, 길면 잊어버린다.")]
        [Min(0.1f)] public float RaidWarningDays = 1f;

        [Tooltip("습격에 병력의 몇 할을 보내는가. 1이면 소굴을 비우고 전부 온다.")]
        [Range(0.1f, 1f)] public float RaidPortion = 0.6f;

        [Tooltip("하루에 회복하는 병력. 0이면 한 번 깎인 소굴은 영영 안 돌아온다 — " +
                 "습격이 되돌릴 수 없는 성과가 되지만, 지도가 조용해지는 속도도 빨라진다.")]
        [Min(0)] public int RegenPerDay = 1;

        [Tooltip("뽑기 가중치.")]
        [Min(0f)] public float Weight = 1f;
    }

    [Header("이 바이옴 소굴에 사는 무리")]
    [Tooltip("비어 있으면 그 바이옴의 소굴은 비어 있다(아무도 안 나온다).")]
    public List<LairEntry> Lairs = new();

    // ── 마을이 내보내는 무리 (월드맵 Town 칸) ────────────────────────────────
    // 상단(Caravan)과 순찰대(Patrol). **행동 종류당 하나씩** 선다 —
    // `TownMarket`이 상인 종류당 하나를 세우는 것과 같은 규칙이다.
    [Header("이 바이옴 마을이 내보내는 무리")]
    [Tooltip("비어 있으면 그 마을에선 상단도 순찰대도 나오지 않는다.")]
    public List<WorldBandData> TownBands = new();

    [Header("이 바이옴에서 소환 가능한 하수인")]
    [Tooltip("비어있으면 제한 없음(모든 하수인 허용)")]
    public List<MinionData> SummonableMinions = new();

    [Header("이 바이옴 개척 시 해금되는 연구")]
    public List<ResearchData> UnlockableResearch = new();
}
