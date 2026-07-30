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

    [Header("이 바이옴에서 소환 가능한 하수인")]
    [Tooltip("비어있으면 제한 없음(모든 하수인 허용)")]
    public List<MinionData> SummonableMinions = new();

    [Header("이 바이옴 개척 시 해금되는 연구")]
    public List<ResearchData> UnlockableResearch = new();
}
