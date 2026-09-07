using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **지하 팔레트를 깔아 준다 — 손으로 열여섯 장을 끌어다 놓지 않게**(2026-08-27, 08-29에 다시 씀).
//
// ── 왜 다시 썼나 (2026-08-29) ───────────────────────────────────────────────
//
// 처음에는 `Blink/70 Stylized Textures`의 `.terrainlayer`를 복제해 썼다. 그 세트는
// **손으로 그린 스타일라이즈드**라, 지상에서는 맞았지만 지하로 내려오니 *애니메이션 배경*처럼
// 보였다(사용자). Yughues 무료 세트로 갈아탄다 — **사진 기반이라 젖은 돌과 이끼가 진짜처럼
// 읽힌다.**
//
// **그쪽은 `.terrainlayer`가 없다 — 텍스처(diffuse·normal)뿐이다.** 그래서 복제가 아니라
// **만든다.** 만드는 김에 노멀맵 임포터도 같이 고친다(아래).
//
// ── 여러 장을 쓴다 ─────────────────────────────────────────────────────────
//
// 한두 장으로 세계를 덮으면 **어디를 가도 같은 바닥**이라 파고 들어가는 재미가 없다. 바이옴
// 여덟에 **바닥과 변형을 따로** 주어 열여섯 장을 쓴다 — 그런데 **URP 지형은 한 패스에 넉
// 장**이라 열여섯이 정확히 **네 패스**다. 한 장이라도 늘리면 다섯 패스가 된다.
//
// **절벽은 한 장으로 접었다.** 바닥이 완전 평지가 된 뒤로 경사가 0이라 **절벽 채널이 화면에
// 아예 안 나온다** — 폴백으로 하나만 있으면 되고, 남는 자리를 바닥에 쓰는 것이 이득이다.
public static class TerrainPaletteCreator
{
    const string Dir      = "Assets/5_Data/World";
    const string LayerDir = Dir + "/TerrainLayers";
    const string Path     = Dir + "/TerrainPalette.asset";

    // Yughues 무료 세트의 뿌리. **경로가 바뀌면 여기만 고친다.**
    const string Pack = "Assets/assetsss/Yughues";

    const string RockPack   = Pack + "/YughuesFreeRockMaterials/Textures/T_YFRM_";
    const string GroundPack = Pack + "/YughuesFreeGroundMaterials/Textures/T_YFGM_";
    const string CobblePack = Pack + "/YughuesFreeCobbleMaterials/Textures/T_YFCM_";
    const string NaturePack = Pack + "/YughuesFreeNatureMaterials/Textures/T_YFNM_";

    // 바이옴 → (바닥, 변형). **둘 다 실제로 다른 그림이어야 한다** — 같은 계열 둘을 주면
    // 섞이는 것이 안 보여서 한 장 쓴 것과 같다.
    //
    // 고른 기준은 *"이 바이옴이 무엇으로 되어 있는가"*다. 이름만 그럴듯한 것을 고르면
    // 화면에서 서로 구별이 안 되고, 그러면 바이옴을 여덟으로 나눈 뜻이 없어진다.
    // ── 색조 — **텍스처가 없어도 색은 바꿀 수 있다**(2026-09-06) ────────────
    //
    // 사용자: *"내가 원하는 균사림은 파랑거나 보라색의 땅인데 지금은 material이 없어서
    // 구현할 수 없는건가?"* **아니다.** `TerrainLayer.diffuseRemapMax`가 **곱하기 색조**라,
    // 같은 결에 다른 색을 입힐 수 있다(URP 지형 셰이더의 `_DiffuseRemapScale`, 이 저장소의
    // 사본에도 그대로 있다).
    //
    // **색을 위해 텍스처를 새로 찾지 않는다.** 필요한 것은 *결*이고 결은 이미 맞다 —
    // 이끼는 뭉실뭉실 덩어리진 결이라 **균사에 그대로 어울린다.** 색만 바꾸면 된다.
    //
    // **1을 넘겨야 색이 바뀐다**(2026-09-06에 약하게 잡았다 고쳤다).
    //
    // 색조는 **곱하기**라 1보다 작은 값만 쓰면 **원래 색이 그대로 남는다** — 초록 이끼에
    // 보라를 0.4로 곱하면 보라가 아니라 **어두운 진흙**이 된다(사용자: *"색이 바뀐건지 잘
    // 모르겠어"*). 바꾸려면 **죽일 채널은 눌러 버리고 살릴 채널은 1 위로 밀어 올린다**:
    // 이끼(≈0.30, 0.35, 0.20)에 (0.18, 0.30, 1.90)을 곱하면 (0.05, 0.11, 0.38)이라
    // **파랑이 나머지의 서너 배**가 된다. 색조는 `Color`라 1을 넘겨도 잘리지 않는다.
    //
    // **그래도 바닥만으로는 안 된다** — 굴이 어두우면 무슨 색이든 검게 눌린다. 그 지대를
    // *버섯 지형*으로 읽게 만드는 것은 **발광 버섯**이다(`MushroomCreator.Glow`).
    //
    // **흰색(1,1,1)이 "안 건드림"이다** — 나머지 바이옴은 전부 그대로 둔다.
    static readonly Color None = Color.white;

    static readonly (Biome biome, string ground, string variant, Color tint, Color tintV)[] Ground =
    {
        // 석회굴 — 물이 깎은 넓은 방. 마른 바위 바닥에 잔자갈이 섞인다.
        (Biome.Limestone, RockPack   + "04",              GroundPack + "SoilGravel01", None, None),

        // 평범한 굴 — 맨바위와 굴러 떨어진 돌.
        (Biome.Cavern,    RockPack   + "01",              GroundPack + "GroundStones02", None, None),

        // 폐광 — **사람이 팠던 곳이라 바닥이 깔려 있다.** 자갈로 반쯤 묻힌 포석.
        (Biome.Mine,      CobblePack + "03",              GroundPack + "SoilGravel03", None, None),

        // 균사림 — **지하의 숲.** 이끼 낀 흙과 이끼 낀 돌. 참고 화면의 그 초록이 여기다.
        // **균사림은 초록이 아니다** — 지하의 숲이되 해를 못 본 숲이다. 바닥은 **짙은
        // 남보라**, 그 위의 덩어리는 **푸른 보라**. 둘을 다른 색으로 둬야 섞이는 것이 보인다
        // (같은 색 둘이면 한 장 쓴 것과 같다 — 텍스처를 둘 고른 것과 같은 이유다).
        (Biome.Fungal,    GroundPack + "SoilMoss",        GroundPack + "MossStones",
                          new Color(0.18f, 0.30f, 1.90f), new Color(0.22f, 0.55f, 2.60f)),

        // 수몰층 — 젖은 점토와 고인 물.
        (Biome.Flooded,   GroundPack + "Clay",            NaturePack + "StagnantWater", None, None),

        // 수정동 — **소금이 결정 노릇을 한다.** 하얗게 부서지는 결이 그럴듯하다.
        (Biome.Crystal,   RockPack   + "09",              GroundPack + "Salt", None, None),

        // 용암층 — 탄 바닥과 검게 그을린 바위.
        (Biome.Lava,      GroundPack + "Burnt",           RockPack   + "06", None, None),

        // 심연 — 어두운 바위 둘. **여기만 둘 다 바위다** — 흙 한 톨 없는 곳이라는 뜻이다.
        (Biome.Abyss,     RockPack   + "02",              RockPack   + "10", None, None),

        // **미지정도 채운다** — 안 칠한 칸이 회색으로 뜨면 "버그"로 보인다. 동굴과 같은 것을
        // 쓰면 채널이 안 늘고(중복은 접힌다) 화면에서도 자연스럽다.
        (Biome.Unknown,   RockPack   + "01",              GroundPack + "GroundStones02", None, None),
    };

    // **폴백 절벽.** 심연의 바닥과 같은 것을 쓴다 — 중복은 접히므로 **채널을 안 먹는다.**
    // 평지에서는 어차피 안 나온다.
    const string Cliff = RockPack + "02";

    // 역할별 타일 크기(m). **서로 안 떨어지는 값**으로 둔다 — 같은 주기면 바닥과 변형이
    // **박자를 맞춰 반복해서** 섞는 뜻이 절반으로 준다.
    //
    // 스타일라이즈드 세트를 쓸 때는 12m였는데, 사진 기반은 결이 잘아서 그만큼 키우면
    // **뭉개져 진흙 색면**이 된다. 6m면 사람 키의 세 배쯤이라 결이 보인다.
    const float GroundTile  = 6f;
    const float VariantTile = 8.5f;
    const float CliffTile   = 5f;

    [MenuItem("DungeonColony/Terrain/9. Create Terrain Palette (지하)")]
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

        foreach (var (biome, ground, variant, tint, tintV) in Ground)
        {
            var g = Layer(ground, GroundTile, made, missing, tint);
            if (g == null) continue;

            entries.Add(new TerrainPalette.Entry
            {
                Biome   = biome,
                Ground  = g,
                Variant = Layer(variant, VariantTile, made, missing, tintV),
                Cliff   = Layer(Cliff,   CliffTile,   made, missing, None),
            });
        }

        if (entries.Count == 0)
        {
            Debug.LogError($"[팔레트] `{Pack}` 아래에서 텍스처를 하나도 못 찾았다 — " +
                           "Yughues 세트가 다른 자리에 임포트됐으면 `TerrainPaletteCreator.Pack`을 고칠 것.");
            return;
        }

        pal.Biomes = entries.ToArray();
        pal.Cliff  = Layer(Cliff, CliffTile, made, missing, None);
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
        Debug.Log($"[팔레트] Yughues에서 레이어 **{made.Count}장**을 만들었다 → 지형을 " +
                  $"**{passes}번** 그린다(URP 지형은 한 패스에 네 장). 바이옴 {entries.Count}.\n" +
                  $"타일 {GroundTile}m(바닥) · {VariantTile}m(변형) · {CliffTile}m(절벽) — " +
                  $"사진 기반이라 스타일라이즈드 때(12m)보다 잘게 둔다.\n" +
                  $"씬의 TerrainStreamer {wired}개에 물렸다. **씬을 저장할 것.**" +
                  (missing.Count > 0
                      ? $"\n**못 찾은 것 {missing.Count}개**: {string.Join(", ", missing)}"
                      : ""));
    }

    // ── 텍스처에서 레이어를 만든다 ──────────────────────────────────────────
    //
    // **이미 있으면 다시 만들지 않는다** — 손으로 조절한 값이 도구를 돌릴 때마다 되돌아가면
    // 다시 돌릴 엄두가 안 난다(연구 그래프 베이커·장소 배치와 같은 판단). 타일 크기만 맞춘다.
    static TerrainLayer Layer(string baseName, float tile,
                              Dictionary<string, TerrainLayer> made, List<string> missing,
                              Color tint)
    {
        if (string.IsNullOrEmpty(baseName)) return null;

        // **캐시 키에 색조를 넣는다** — 같은 텍스처를 다른 색으로 쓰는 바이옴이 생기면,
        // 이름만으로 캐싱했을 때 **먼저 만든 색이 뒤에 온 바이옴까지 물들인다.**
        string key = baseName + "|" + ColorUtility.ToHtmlStringRGB(tint);
        if (made.TryGetValue(key, out var cached)) return cached;

        var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(baseName + "_d.tga");
        if (diffuse == null) { missing.Add(baseName); return null; }

        // 파일 이름은 **세트를 앞에 붙인다** — 록의 `02`와 코블의 `02`가 이름만으로는 같다.
        // **색조를 입힌 것은 이름도 갈라야 한다** — 같은 파일에 쓰면 색이 서로를 덮어쓴다.
        bool tinted = tint != Color.white;
        string dst  = LayerDir + "/" + NameOf(baseName)
                    + (tinted ? "_" + ColorUtility.ToHtmlStringRGB(tint) : "") + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(dst);

        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, dst);
        }

        layer.diffuseTexture = diffuse;
        layer.normalMapTexture = Normal(baseName + "_n.tga");
        layer.tileSize   = new Vector2(tile, tile);
        layer.tileOffset = Vector2.zero;

        // **금속·매끈함을 낮춘다.** 사진 텍스처의 스펙큘러를 그대로 두면 젖은 플라스틱처럼
        // 번들거려서, 어두운 굴에서 **바닥만 혼자 빛난다.**
        layer.specular   = new Color(0.04f, 0.04f, 0.04f);
        layer.metallic   = 0f;
        layer.smoothness = 0.05f;

        // **색조는 곱하기다** — `diffuseRemapMin`이 0, `Max`가 색. 흰색이면 원본 그대로다.
        // 텍스처를 새로 만들지 않는 것이 요점이다: **결은 그대로 두고 색만 바꾼다.**
        layer.diffuseRemapMin = Color.black;
        layer.diffuseRemapMax = tint;

        EditorUtility.SetDirty(layer);
        made[key] = layer;
        return layer;
    }

    // **노멀맵은 임포터를 고쳐야 한다.** Yughues의 `_n.tga`는 보통 그냥 텍스처로 들어와서,
    // 그대로 물리면 유니티가 *"노멀맵으로 안 잡혀 있다"*고 경고하고 **조명이 이상하게 뒤집힌다.**
    // 여기서 한 번 바꿔 주면 다음부터는 그대로 산다.
    static Texture2D Normal(string path)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return null;

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap)
        {
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
        }
        return tex;
    }

    // `.../Textures/T_YFRM_04` → `Rock_04`. 세트를 접두어로 남긴다.
    static string NameOf(string baseName)
    {
        string file = System.IO.Path.GetFileName(baseName);       // T_YFRM_04
        string set  = file.StartsWith("T_YFRM") ? "Rock"
                    : file.StartsWith("T_YFGM") ? "Ground"
                    : file.StartsWith("T_YFCM") ? "Cobble"
                    : file.StartsWith("T_YFNM") ? "Nature"
                    : "Yughues";

        int us = file.IndexOf('_', file.IndexOf('_') + 1);        // 두 번째 밑줄 뒤가 이름
        string rest = us >= 0 ? file.Substring(us + 1) : file;
        return set + "_" + rest;
    }
}
