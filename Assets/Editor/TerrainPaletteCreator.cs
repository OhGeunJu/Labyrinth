using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **팔레트를 깔아 준다 — 손으로 열 개를 끌어다 놓지 않게**(2026-08-27).
//
// `Blink/70 Stylized Textures`가 `.terrainlayer`를 **이미 76장 들고 있다**(사막·숲·설원·용암
// 네 세트). 만들 것이 없고 **고르기만 하면 된다.**
//
// ── 왜 복제하나 ─────────────────────────────────────────────────────────────
//
// 번들의 레이어는 **`TileSize`가 2m**다. 256m 청크에 같은 그림이 축당 **128번** 반복돼
// 격자처럼 읽힌다. 키워야 하는데, **원본을 고치면 번들을 다시 임포트할 때 날아가고**
// 다음에 보는 사람은 왜 값이 다른지 알 수 없다.
//
// 그래서 `5_Data/World/TerrainLayers/`로 **복제하고 거기서 키운다** — 저작한 값은 우리 것이고,
// 번들은 그대로 둔다(에셋 폴더 규칙과 같은 판단).
//
// **역할마다 타일 크기를 다르게 준다.** 바닥과 변형이 같은 주기면 둘이 **박자를 맞춰 반복해서**
// 섞는 뜻이 절반으로 준다 — 12m와 17m처럼 안 떨어지는 값으로 어긋나게 둔다.
public static class TerrainPaletteCreator
{
    const string Dir      = "Assets/5_Data/World";
    const string LayerDir = Dir + "/TerrainLayers";
    const string Path     = Dir + "/TerrainPalette.asset";

    // 텍스처 번들의 뿌리. **경로가 바뀌면 여기만 고친다.**
    const string Pack = "Assets/assetsss/Blink/Art/Textures";

    // 바이옴 → (바닥, 변형, 절벽). **이름이 아니라 경로로 가리킨다** — 같은 이름이 세트마다
    // 있어서(`Cliff_1`이 숲에도 용암지대에도 있다) 이름으로 찾으면 엉뚱한 것이 걸린다.
    //
    // 절벽은 **계열끼리 나눠 쓴다** — 일곱 개를 다 두면 스무 장이 되고, 하나만 쓰면
    // *"바닥은 모래인데 비탈은 이끼 낀 바위"*가 된다. 온대 · 건조/화산 · 한대 셋이면
    // 눈에 걸리는 어긋남이 거의 없다.
    const string CliffTemperate = "StylizedForestTextures/Cliff_1/Cliff_1.terrainlayer";
    const string CliffArid      = "StylizedLavaTextures/Cliff_1/Cliff_1.terrainlayer";
    const string CliffCold      = "StylizedIceTextures/Rocks_1_Snow_4/Rocks_1_Snow_4.terrainlayer";

    static readonly (Biome biome, string ground, string variant, string cliff)[] Ground =
    {
        (Biome.Plains,  "StylizedForestTextures/Grass_1/Grass_1.terrainlayer",
                        "StylizedForestTextures/Grass_Flowers/Grass_Flowers.terrainlayer", CliffTemperate),
        (Biome.Forest,  "StylizedForestTextures/Grass_Roots/Grass_Roots.terrainlayer",
                        "StylizedForestTextures/Grass_3/Grass_3.terrainlayer",             CliffTemperate),
        (Biome.Desert,  "StylizedDesertTextures/Sand_1/Sand_1.terrainlayer",
                        "StylizedDesertTextures/Sand_3/Sand_3.terrainlayer",               CliffArid),
        (Biome.Tundra,  "StylizedIceTextures/Snow_1/Snow_1.terrainlayer",
                        "StylizedIceTextures/Snow_3/Snow_3.terrainlayer",                  CliffCold),
        (Biome.Swamp,   "StylizedForestTextures/Mud_1/Mud_1.terrainlayer",
                        "StylizedForestTextures/Mud_3/Mud_3.terrainlayer",                 CliffTemperate),
        // **동굴만 변형이 없다** — 여기까지 채우면 바닥 7 + 변형 7 + 절벽 3 = **열일곱**이라
        // 패스가 다섯으로 늘어난다(`TerrainPalette.Build`가 경고한다). 한 장을 접어야 한다면
        // 동굴이다 — 지표에 거의 안 깔리는 바이옴이라 반복이 눈에 띌 자리가 가장 적다.
        // 비우면 `TerrainPalette.VariantOf`가 바닥 채널을 그대로 돌려준다.
        (Biome.Cavern,  "StylizedLavaTextures/Rock_Ground/Rock_Ground.terrainlayer",
                        null,                                                              CliffArid),
        (Biome.Lava,    "StylizedLavaTextures/Dry_Lava/Dry_Lava.terrainlayer",
                        "StylizedLavaTextures/Lava_Ground_02/Lava_Ground_02.terrainlayer", CliffArid),

        // **미지정도 채운다** — 안 칠한 칸이 회색으로 뜨면 "버그"로 보인다. 평원과 같은 것을
        // 쓰면 채널이 안 늘고(중복은 접힌다) 화면에서도 자연스럽다.
        (Biome.Unknown, "StylizedForestTextures/Grass_1/Grass_1.terrainlayer",
                        "StylizedForestTextures/Grass_Flowers/Grass_Flowers.terrainlayer", CliffTemperate),
    };

    // 역할별 타일 크기(m). **서로 안 떨어지는 값**으로 둔다.
    const float GroundTile  = 12f;
    const float VariantTile = 17f;
    const float CliffTile   = 9f;

    [MenuItem("DungeonColony/Terrain/9. Create Terrain Palette")]
    static void Create()
    {
        System.IO.Directory.CreateDirectory(LayerDir);
        AssetDatabase.Refresh();

        var pal = AssetDatabase.LoadAssetAtPath<TerrainPalette>(Path);
        bool isNew = pal == null;
        if (isNew) pal = ScriptableObject.CreateInstance<TerrainPalette>();

        var entries = new List<TerrainPalette.Entry>();
        var missing = new List<string>();
        var made    = new Dictionary<string, TerrainLayer>();

        foreach (var (biome, ground, variant, cliff) in Ground)
        {
            var g = Clone(ground, GroundTile, made, missing);
            if (g == null) continue;

            entries.Add(new TerrainPalette.Entry
            {
                Biome   = biome,
                Ground  = g,
                Variant = variant != null ? Clone(variant, VariantTile, made, missing) : null,
                Cliff   = cliff   != null ? Clone(cliff,   CliffTile,   made, missing) : null,
            });
        }

        // 공용 절벽 — 바이옴에 안 적힌 것이 떨어질 자리.
        var fallbackCliff = Clone(CliffTemperate, CliffTile, made, missing);

        if (entries.Count == 0)
        {
            Debug.LogError($"[팔레트] `{Pack}` 아래에서 `.terrainlayer`를 하나도 못 찾았다 — " +
                           "번들이 다른 자리에 임포트됐으면 `TerrainPaletteCreator.Pack`을 고칠 것.");
            return;
        }

        pal.Biomes = entries.ToArray();
        pal.Cliff  = fallbackCliff;
        pal.Forget();

        if (isNew) AssetDatabase.CreateAsset(pal, Path);
        else       EditorUtility.SetDirty(pal);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // **씬의 스트리머에 물린다** — 만들어만 두면 아무 데도 안 쓰이고, 그건 "안 바뀐다"로만 보인다.
        int wired = 0;
        foreach (var s in Object.FindObjectsByType<TerrainStreamer>(FindObjectsInactive.Include,
                                                                   FindObjectsSortMode.None))
        {
            var so = new SerializedObject(s);
            var p  = so.FindProperty("palette");
            if (p == null) continue;
            p.objectReferenceValue = pal;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(s);
            wired++;
        }

        WorldSceneTerrain.Refresh();
        Selection.activeObject = pal;

        int passes = Mathf.CeilToInt(pal.Layers.Length / 4f);
        Debug.Log($"[팔레트] 레이어 {pal.Layers.Length}장 = 지형을 **{passes}번** 그린다 " +
                  $"(URP 지형은 한 패스에 네 장). 바이옴 {entries.Count} — 바닥·변형·절벽. → {Path}\n" +
                  $"번들에서 {made.Count}장을 `{LayerDir}`로 복제하고 타일을 " +
                  $"{GroundTile}m(바닥) · {VariantTile}m(변형) · {CliffTile}m(절벽)로 키웠다 " +
                  $"— **원본 2m는 축당 128번 반복이었다.**\n" +
                  $"씬의 TerrainStreamer {wired}개에 물렸다. **씬을 저장할 것.**" +
                  (missing.Count > 0
                      ? $"\n**못 찾은 것 {missing.Count}개**: {string.Join(", ", missing)}"
                      : ""));
    }

    // 번들의 레이어를 우리 폴더로 복제하고 타일 크기를 준다.
    //
    // **이미 있으면 다시 만들지 않는다** — 손으로 조절한 값이 도구를 돌릴 때마다 되돌아가면
    // 다시 돌릴 엄두가 안 난다(연구 그래프 베이커·장소 배치와 같은 판단). 타일 크기만 맞춘다.
    // `StylizedForestTextures/...` → `Forest`. 못 알아보면 `Pack`으로 떨어진다.
    static string SetName(string rel)
    {
        int slash = rel.IndexOf('/');
        string folder = slash > 0 ? rel.Substring(0, slash) : rel;
        folder = folder.Replace("Stylized", "").Replace("Textures", "");
        return string.IsNullOrEmpty(folder) ? "Pack" : folder;
    }

    static TerrainLayer Clone(string rel, float tile,
                              Dictionary<string, TerrainLayer> made, List<string> missing)
    {
        if (made.TryGetValue(rel, out var cached)) return cached;

        var src = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Pack + "/" + rel);
        if (src == null) { missing.Add(rel); return null; }

        // **세트 이름을 앞에 붙인다.** `Cliff_1`이 숲에도 용암지대에도 있어서, 이름만 쓰면
        // 둘이 같은 파일로 떨어진다 — 먼저 복제된 쪽이 이기고 **용암지대 절벽이 조용히
        // 숲 절벽이 된다.** 경로가 다르면 파일도 달라야 한다.
        string dst = LayerDir + "/" + SetName(rel) + "_" + src.name + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(dst);

        if (layer == null)
        {
            if (!AssetDatabase.CopyAsset(Pack + "/" + rel, dst)) { missing.Add(rel); return null; }
            AssetDatabase.ImportAsset(dst);
            layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(dst);
            if (layer == null) { missing.Add(rel); return null; }
        }

        layer.tileSize = new Vector2(tile, tile);
        EditorUtility.SetDirty(layer);

        made[rel] = layer;
        return layer;
    }
}
