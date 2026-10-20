using System.Collections.Generic;
using UnityEngine;

// 바이옴별 콘텐츠 게이팅 데이터 (step 7).
// 한 바이옴에서 등장하는 자원 노드 / 소환 가능한 하수인 / 개척 시 해금되는 연구를 정의.
// 탐험 보상 루프의 핵심: "그 바이옴에 가야만 얻는" 것을 데이터로 게이팅한다.
//
// 배치: Resources/Data/Biomes/ 아래에 두면 BiomeContent가 자동 로드(Biome별 1개 권장).

// **지도에서 어느 쪽에 앉나**(2026-09-15, 사용자가 1층 배치를 그려 줬다).
//
// 그전에는 **굴림이 돌렸다** — 구역은 크게 갈렸지만 *어느 방위에 무엇이*는 다시 누를
// 때마다 달라졌다. 사용자가 *"균사림은 북서, 이끼굴은 북동…"*으로 그려 준 순간 그것은
// **저작**이 됐고, 저작은 굴림이 못 건드려야 한다.
//
// **값은 꼬리에 붙일 것** — 정수 직렬화다. `Auto`가 0이라 **안 적은 바이옴은 예전 그대로**
// (굴림이 돌린다).
public enum MapDir
{
    Auto,       // 안 정했다 — 굴림이 둘레에 고르게 놓는다
    Center,     // 한복판 — 시작 지점이 여기 든다
    N, NE, E, SE, S, SW, W, NW,
}

[CreateAssetMenu(menuName = "DungeonColony/BiomeContentData")]
public class BiomeContentData : ScriptableObject
{
    public Biome Biome = Biome.Plains;

    // ── 지역 난도 ────────────────────────────────────────────────────────────
    //
    // **오픈월드다 — 저렙이 고렙존에 갈 수 있고, 웬만하면 죽는다**(2026-08-23).
    // 안개를 폐기하며(06-20) *"발견의 긴장은 거리+위험도 게이팅으로"*라고 적어 두고 비워
    // 뒀던 칸이 여기서 숫자가 된다.
    //
    // **칸마다 저작하지 않는다** — 지도를 그릴 때마다 위험도를 손으로 찍어야 하고, 그러면
    // 저작한 지도와 실제 난도가 반드시 어긋난다. 칸 단위 예외는 **소굴**이 이미 들고 있다.
    //
    // **막지 않는다.** 티어가 하는 일은 셋뿐이다: 무리·소굴 전력 배율(`BiomeTier.PowerMult`) ·
    // 필드맵 적 구성 · **화면에 보이기**(도착 메뉴가 경고한다). 갈 수는 있다.
    [Header("지역 난도")]
    [Tooltip("1 = 시작 지역, 5 = 가장 험한 곳. 무리·소굴 전력에 곱해지고 도착 메뉴에 경고로 뜬다.")]
    [Range(1, 5)] public int Tier = 1;

    // **지도에서 어느 쪽에 앉나.** `Terrain → 8. Generate Biome Map`이 이 방위에 씨를 놓고,
    // 칸은 가장 가까운 씨에 붙는다(`BiomeMapGenerator`). **한 층 안에서만 뜻이 있다** —
    // 다른 층과는 지도를 같이 쓰지 않는다.
    //
    // **`Auto`면 굴림이 둘레에 고르게 놓는다** — 한 층의 바이옴 중 일부만 못 박아도 된다.
    [Header("지도에서 어느 쪽에 앉나")]
    [Tooltip("비워 두면(Auto) 다시 누를 때마다 방위가 돈다. 못 박으면 굴림이 안 건드린다.")]
    public MapDir MapDirection = MapDir.Auto;

    [Header("이 바이옴에서 등장하는 자원 노드 풀")]
    public List<ResourceNodeData> ResourceNodes = new();

    // ── 광맥이 얼마나 촘촘한가 (2026-10-11) ────────────────────────────────
    //
    // 풀은 *무엇이* 나오나만 답하고 *얼마나*는 세계가 한 값으로 들었다(`RockMap.VeinDensity`). 그래서
    // 수정동을 *보석이 가득한 굴*로 만들 손잡이가 없었다 — 풀에 보석을 아무리 적어도 덩어리 수는 그대로다.
    // 벽 속 광맥(`RockMap.VeinAt`)이 덩어리 문턱에 이 값을 곱한다.
    public const float MaxVeinRichness = 3f;

    [Header("광맥이 얼마나 촘촘한가")]
    [Tooltip("1 = 보통. 2면 벽 속 광맥 덩어리가 두 배쯤 넓다. 0이면 이 바이옴 벽에는 광맥이 없다.")]
    [Range(0f, MaxVeinRichness)] public float VeinRichness = 1f;

    // ── 벽에 박힌 조명 (2026-09-28) ─────────────────────────────────────────
    //
    // **바위 칸 하나가 이것을 품을 확률**이다 — 드러난 벽면에서만 보인다(`RockMap.WallLightAt`).
    // 여럿을 적으면 확률이 **이어 붙는다**(0.03 + 0.05 = 칸의 8%에 무언가 박힌다).
    [System.Serializable]
    public class WallLightEntry
    {
        public WallLightData Light;

        [Tooltip("바위 칸 하나가 이것을 품을 확률. 0.03이면 벽 서른세 칸에 하나꼴.")]
        [Range(0f, 0.3f)] public float Chance = 0.03f;
    }

    [Header("벽에 박힌 조명 (야광석 · 횃불)")]
    [Tooltip("비어 있으면 이 바이옴 벽에는 아무것도 안 박힌다.")]
    public List<WallLightEntry> WallLights = new();

    // ── 저절로 나는 무리 — 짐승 · 떠도는 적 · 사람 (2026-09-28 · 10-15) ─────────
    //
    // **어느 소속의 누가 어느 지형을 돌아다니나가 이 표다**(2026-10-15, 사용자: *"어느 소속의 누가 어디 지형에
    // 돌아다니게 설정할 수 있게만"*) — 지형은 이 바이옴 에셋, 누가·몇은 무리 에셋의 구성(`Members`), 소속은 무리
    // 에셋의 세력(`Faction`). 사람 무리도 짐승과 같은 길로 선다(떼로 걷고 · 멀면 거두고 · 쓰러지면 며칠 뒤 다시).
    // 지금은 마을 세력이 적어 사람 줄은 비어 있다 — **채우는 것은 저작이다.**
    //
    // 사람 무리의 식구는 **역할 없는 폰 · 주민 · 경비**로 둔다. 떠돌이·행상인 역할은 제 시간이 차면 맵을 떠나
    // 무리에서 떨어져 나간다(`WildSpawner`가 세울 때 한 번 경고한다).
    //
    // **청크가 설 때 그 자리에 선다**(`WildSpawner`). 소굴에서 나와 지도를 걷는 무리(`Lairs`)와 다른 축이다 —
    // 저쪽은 *어디서 왔나*가 있는 사건이고, 이쪽은 **그 지역에 원래 사는 것**이다. 구성은 무리 에셋을 그대로
    // 쓴다(늑대·곰은 `Band_Beasts`) — 여기서 폰 목록을 또 들면 무리 구성이 두 벌이 된다.
    [System.Serializable]
    public class WildSpawnEntry
    {
        [Tooltip("무엇이 몇이나 서나. 무리 에셋의 구성·세력을 그대로 쓴다.")]
        public WorldBandData Band;

        [Tooltip("청크(64m) 하나에 이 무리가 살 확률. 0.1이면 청크 열에 하나 — 둘레에 서는 청크가 스물다섯이니 두셋이 산다.")]
        [Range(0f, 1f)] public float ChancePerChunk = 0.1f;

        [Tooltip("다 쓰러뜨린 뒤 다시 나기까지(일).")]
        [Min(0.1f)] public float RespawnDays = 3f;

        [Tooltip("이 무리의 사람을 **대화로 영입할 수 있는가**(`UnitBrain.Recruitable`). 기본은 꺼짐 — 길에서 " +
                 "만난 사람은 대화·거래·용병 계약까지만. 켜면 그 무리 식구 전부가 영입 대상이다.")]
        public bool Recruitable;
    }

    [Header("저절로 나는 무리 (짐승 · 떠도는 적 · 사람)")]
    [Tooltip("비어 있으면 이 바이옴에는 아무것도 저절로 안 난다. 인스펙터에서 저작한다.")]
    public List<WildSpawnEntry> WildSpawns = new();

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

    // **누구의 마을인가** — 시장 값이 세력 우호도를 타는 통로다(`Shop.Faction`).
    //
    // 마을엔 씬 오브젝트가 없어 `Faction` 컴포넌트를 붙일 자리가 없다. 처음엔 `TownBands`에서
    // 끌어오려 했는데 **저작한 데이터가 그걸 반증했다**: 한 바이옴의 `TownBands`에 변방민
    // 파수꾼과 마을 사람 자경단과 용병대가 **같이** 들어 있다(그게 맞다 — 마을 하나에 여러
    // 무리가 드나든다). 거기서 하나를 고르면 목록 순서가 마을 세력을 정하게 된다.
    //
    // **비워 두면 그 마을은 우호도와 무관하게 정가로 장사한다**(배율 1) — 안 채운 것이
    // 조용한 사고가 되지 않게, 없는 것은 없는 대로 뜻이 통하는 쪽으로 떨어뜨린다.
    [Header("이 바이옴 마을의 세력")]
    [Tooltip("시장 값이 이 세력의 우호도를 탄다. 비우면 정가(우호도 무관).")]
    public FactionData TownFaction;

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
    // **지금 읽는 곳이 없다**(2026-10-09) — 고용은 옛 3D 월드맵의 마을 메뉴에서만 열렸고 그 화면과 함께 걷어냈다
    // (`TownRecruits`). 저작한 표라 지우지 않는다 — 마을 고용을 다시 열 때 이것을 읽는다.
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
