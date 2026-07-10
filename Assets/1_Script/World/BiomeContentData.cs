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

    [Header("이 바이옴에서 소환 가능한 하수인")]
    [Tooltip("비어있으면 제한 없음(모든 하수인 허용)")]
    public List<MinionData> SummonableMinions = new();

    [Header("이 바이옴 개척 시 해금되는 연구")]
    public List<ResearchData> UnlockableResearch = new();
}
