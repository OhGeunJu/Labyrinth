using System;
using System.Collections.Generic;
using UnityEngine;

// 바이옴 콘텐츠 게이팅 레지스트리 (step 7). DataManager와 같은 static 로더 패턴.
// Resources/Data/Biomes/ 의 모든 BiomeContentData를 Biome별로 색인해 질의에 답한다.
//
// 게이팅 미설정(SO 없음/해당 바이옴 없음)이면 "제한 없음"으로 처리 → 기존 씬 무영향.
public static class BiomeContent
{
    static Dictionary<Biome, BiomeContentData> _byBiome;

    static void EnsureLoaded()
    {
        if (_byBiome != null) return;
        _byBiome = new Dictionary<Biome, BiomeContentData>();
        foreach (var d in Resources.LoadAll<BiomeContentData>("Data/Biomes"))
            if (d != null) _byBiome[d.Biome] = d;
    }

    // 실린 바이옴 데이터 전부. 한 바이옴에 없는 것을 다른 바이옴에서 빌려 쓸 때 훑는다
    // (`TownRecruits` — 마을에 아무도 없는 것보다 옆 지역 사람이라도 서 있는 편이 낫다).
    public static IEnumerable<BiomeContentData> All
    {
        get { EnsureLoaded(); return _byBiome.Values; }
    }

    public static BiomeContentData Get(Biome b)
    {
        EnsureLoaded();
        return _byBiome.TryGetValue(b, out var v) ? v : null;
    }

    public static IReadOnlyList<ResourceNodeData> ResourceNodes(Biome b) =>
        Get(b)?.ResourceNodes ?? (IReadOnlyList<ResourceNodeData>)Array.Empty<ResourceNodeData>();

    public static IReadOnlyList<BiomeContentData.WildSpawnEntry> WildSpawns(Biome b) =>
        Get(b)?.WildSpawns ?? (IReadOnlyList<BiomeContentData.WildSpawnEntry>)Array.Empty<BiomeContentData.WildSpawnEntry>();

    public static IReadOnlyList<MinionData> SummonableMinions(Biome b) =>
        Get(b)?.SummonableMinions ?? (IReadOnlyList<MinionData>)Array.Empty<MinionData>();

    public static IReadOnlyList<ResearchData> UnlockableResearch(Biome b) =>
        Get(b)?.UnlockableResearch ?? (IReadOnlyList<ResearchData>)Array.Empty<ResearchData>();

    // 이 바이옴에서 해당 하수인을 소환할 수 있는가.
    // 데이터 없음/목록 비어있음 = 제한 없음(전부 허용) → 게이팅 미도입 씬 그대로 동작.
    public static bool IsMinionAllowed(Biome b, MinionData m)
    {
        var d = Get(b);
        return d == null || d.SummonableMinions.Count == 0 || d.SummonableMinions.Contains(m);
    }

#if UNITY_EDITOR
    // 에디터에서 SO 추가/수정 후 캐시 무효화용(런타임 미사용).
    public static void ClearCache() => _byBiome = null;
#endif
}
