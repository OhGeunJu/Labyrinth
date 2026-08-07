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

    [Header("이 바이옴에서 소환 가능한 하수인")]
    [Tooltip("비어있으면 제한 없음(모든 하수인 허용)")]
    public List<MinionData> SummonableMinions = new();

    [Header("이 바이옴 개척 시 해금되는 연구")]
    public List<ResearchData> UnlockableResearch = new();
}
