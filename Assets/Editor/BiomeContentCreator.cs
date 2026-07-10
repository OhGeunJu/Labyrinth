using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// 바이옴별 콘텐츠 게이팅 데이터(BiomeContentData) 스캐폴드 생성.
// Resources/Data/Biomes/ 아래에 바이옴별 SO를 만들어, 인스펙터에서
// ResourceNodes / SummonableMinions / UnlockableResearch를 채우게 한다.
// BiomeContent가 이 경로를 자동 로드하므로 채우는 즉시 게이팅이 동작.
// 이미 있으면 건너뜀(덮어쓰지 않음).
public static class BiomeContentCreator
{
    const string Dir = "Assets/Resources/Data/Biomes";

    [MenuItem("DungeonColony/Create Biome Content Assets")]
    static void Create()
    {
        EnsureFolder(Dir);

        int made = 0;
        foreach (Biome b in Enum.GetValues(typeof(Biome)))
        {
            if (b == Biome.Unknown) continue;  // 미발견 표시용 — 콘텐츠 없음
            string path = $"{Dir}/Biome_{b}.asset";
            if (AssetDatabase.LoadAssetAtPath<BiomeContentData>(path) != null) continue;

            var so = ScriptableObject.CreateInstance<BiomeContentData>();
            so.Biome = b;
            AssetDatabase.CreateAsset(so, path);
            made++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BiomeContentCreator] {made}개 생성 (경로: {Dir}). 인스펙터에서 풀을 채우세요.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf   = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
