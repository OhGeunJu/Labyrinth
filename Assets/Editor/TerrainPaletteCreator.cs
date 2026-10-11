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
// ── 여러 장을 쓰되, **이 층 것만** 굽는다 (2026-09-15) ─────────────────────
//
// 한두 장으로 세계를 덮으면 **어디를 가도 같은 바닥**이라 파고 들어가는 재미가 없다. 그래서
// 바이옴마다 **바닥과 변형을 따로** 준다 — 그런데 **URP 지형은 한 패스에 넉 장**이라
// 장 수가 곧 **지형을 몇 번 그리느냐**다.
//
// 지하 여덟을 전부 구울 때가 열여섯 장, 정확히 **네 패스**였다. 여기에 바이옴 다섯이 더해져
// 열셋이 되면 스물여섯 장 — **일곱 패스**다. 그대로 두면 바이옴을 늘린 값을 프레임으로
// 치른다.
//
// **그런데 지도 한 장은 이제 한 층이다**(`WorldMapData.Floor`). 1층 지도에 수정동 바닥을
// 구워 둘 이유가 없다 — **그 칸이 세계에 없다.** 그래서 **이 층의 바이옴만** 굽는다:
// 세 바이옴이면 여섯 장 + 미지정 + 절벽이라 **두 패스**다. 바이옴을 늘렸는데 패스는
// 오히려 절반이 됐다.
//
// **층을 바꾸면 다시 구워야 한다** — `1. Rebuild Underworld`가 이 도구를 부르므로 묶음으로
// 돌리면 저절로 따라온다. 팔레트에 없는 바이옴은 **첫 줄로 떨어진다**(`TerrainPalette`) —
// 조용히 분홍색이 되지는 않는다.
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

    // **밖에서 받아 온 것**(2026-09-15, 사용자가 cgbookcase의 Moss 01을 골랐다 — CC0).
    //
    // Yughues 세트만으로는 결이 모자란 자리가 생긴다. 다른 출처를 섞을 수 있어야 하는데,
    // 세트마다 **파일 이름 규칙이 다르다** — 그래서 아래 `Suffixes`가 여러 규칙을 훑는다.
    const string ExtPack = "Assets/assetsss/";

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

    // **이 지도가 몇 층인가.** 씬의 `WorldMap`이 물고 있는 `WorldMapData`가 답한다 —
    // `BiomeMapGenerator`가 후보를 모을 때 쓰는 그 값이고, 여기서 또 물으면 **팔레트와
    // 지도가 다른 층을 볼 수 있다.**
    //
    // **못 찾으면 전부 굽는다** — 층을 모르는데 좁히면 *지도에 있는 바이옴이 팔레트에 없는*
    // 상태가 조용히 생긴다(화면에는 바닥 한 장으로만 보인다). 모를 때는 넓게 가는 편이 낫다.
    static int MapFloor(out string why)
    {
        // **층이 넷이 된 뒤로는 늘 전부 굽는다**(2026-10-01, `Floors`) — 한 세계에 층 넷이 나란히 서므로 팔레트에 네 층의
        // 바이옴이 다 있어야 한다. 한 층으로 좁히면 나머지 세 층 바닥이 한 장으로 뜬다.
        if (WorldMap.FloorData(2) != null) { why = null; return 0; }

        var view = Object.FindFirstObjectByType<WorldMap>(FindObjectsInactive.Include);
        var data = view != null ? new SerializedObject(view).FindProperty("mapData")?.objectReferenceValue
                                  as WorldMapData
                                : null;

        if (data == null)
        {
            why = "씬에서 `WorldMap.mapData`를 못 찾아 **층을 모른다** — 지하 전부를 굽는다";
            return 0;
        }

        why = null;
        return data.Floor;
    }

    static readonly (Biome biome, string ground, string variant, Color tint, Color tintV)[] Ground =
    {
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

        // 빙하굴 — **얼어붙은 점토와 얼음판.** 결은 그대로 쓰고 색만 민다(2026-09-15에
        // 수몰층에서 개명했다) — *"색을 위해 텍스처를 새로 찾지 않는다"*의 그 자리다.
        // 고인 물의 결이 곧 **얼어붙은 웅덩이**의 결이다.
        (Biome.Glacier,   GroundPack + "Clay",            NaturePack + "StagnantWater",
                          new Color(1.10f, 1.35f, 1.65f), new Color(1.20f, 1.50f, 1.85f)),

        // 수정동 — **소금이 결정 노릇을 한다.** 하얗게 부서지는 결이 그럴듯하다.
        (Biome.Crystal,   RockPack   + "09",              GroundPack + "Salt", None, None),

        // 용암층 — 탄 바닥과 검게 그을린 바위.
        (Biome.Lava,      GroundPack + "Burnt",           RockPack   + "06", None, None),

        // 심연 — 어두운 바위에 **살덩이가 번졌다**(2026-10-11, 사용자: *"심연은 약간 크툴루 느낌"*).
        // 전에는 바위 둘이었다. 변형을 살로 바꿔 바닥이 군데군데 살아 있는 것처럼 보이게 한다.
        // **기생림 바닥과 같은 장이다** — 층이 달라 화면에서 안 부딪히고, 채널을 하나 아낀다.
        (Biome.Abyss,     RockPack   + "02",              NaturePack + "Flesh", None, None),

        // ── 층마다 하나씩 더 (2026-09-15) ───────────────────────────────────

        // 이끼굴(1층) — **균사림과 같은 결에 제 색으로.** 이끼 텍스처는 원래 초록이라
        // 색조가 필요 없다 — 균사림이 그것을 남보라로 **비틀어** 쓰는 쪽이다.
        (Biome.Mossgrotto, GroundPack + "SoilMoss",        GroundPack + "GroundStones01", None, None),

        // 납골굴(2층) — 돌관이 깔린 바닥과 마른 흙. **뼈빛으로 창백하게** 민다.
        (Biome.Ossuary,    CobblePack + "07",              GroundPack + "Dry02",
                           new Color(1.25f, 1.18f, 1.02f), new Color(1.20f, 1.14f, 1.00f)),

        // 모래굴(2층) — 물이 마른 굴. 고운 모래와 마른 흙.
        (Biome.Desert,     GroundPack + "SandySoil",       GroundPack + "Dry01", None, None),

        // 지저늪(2층) — **이끼와 고인 물.** 2층에서 가장 젖은 쪽이다.
        //
        // 바닥은 **밖에서 받아 온 이끼**다(cgbookcase `Moss 01`, CC0 — 사용자가 골랐다).
        // Yughues의 `SoilMoss`는 *이끼 낀 흙*이라 이끼굴이 쓰고 있고, 이쪽은 **이끼가
        // 바닥을 덮은** 결이라 늪에 맞는다. 둘은 **층이 달라 같은 팔레트에 안 올라오므로**
        // 결이 비슷해도 화면에서 부딪히지 않는다.
        //
        // **1K로 받는다** — Yughues 세트가 전부 1024이고, 한 장만 2K면 **그 바이옴 바닥만
        // 유독 또렷해서** 나머지가 흐려 보인다(한 층 안에서는 특히 눈에 띈다). 타일이 6m라
        // 1K면 미터당 170픽셀이고, 내려다보는 화면에서 그보다 가까이 볼 일이 없다.
        //
        // **Metalness/Roughness로 받는다** — `BaseColor`·`Normal`이 각각 제 파일로 나온다.
        // Channel-packed의 `NormHeight`·`NormRough`는 노멀에 높이·거칠기를 섞어 넣은 것이라
        // 그대로 물리면 **파란 채널에 엉뚱한 값이 든 노멀**이 되고, 화면에는 *조명이 좀
        // 이상하다*로만 보인다. AO·Height·Roughness는 안 쓴다(아래에서 일부러 눌러 둔다).
        //
        // **없으면 이 바이옴만 첫 줄 바닥으로 떨어진다**(그리고 굽기가 에러로 말한다).
        (Biome.Swamp,      ExtPack    + "Moss01_MR_1K/Moss01_1K", NaturePack + "StagnantWater", None, None),

        // ── 사용자가 더한 셋 (2026-10-11) ───────────────────────────────────
        //
        // **바이옴마다 새 장은 하나씩만 쓴다** — 변형은 이웃이 이미 쓰는 장을 빌린다(중복은 접힌다).
        // 셋을 둘씩 새로 주면 스물여덟 장 = 일곱 패스이고, 이렇게 두면 스물넉 장 = 전과 같은 여섯 패스다.

        // 기생림(2층) — **살 위에 마른 흙.** 시체에서 버섯이 자라는 곳이라 바닥이 살이다.
        // 변형은 납골굴의 뼈빛 흙을 그대로 빌린다(이웃 바이옴이고 같은 죽음의 결이다).
        (Biome.Blight,     NaturePack + "Flesh",           GroundPack + "Dry02",
                           None,                           new Color(1.20f, 1.14f, 1.00f)),

        // 지저밀림(2층) — 낙엽 덮인 풀과 이끼 낀 흙. 늪(이끼·고인 물)보다 **마르고 밝은 초록**이다.
        (Biome.Jungle,     GroundPack + "GrassLeafs01",    GroundPack + "SoilMoss", None, None),

        // 방사굴(3층) — **갈라진 땅을 연두로 민다.** 변형은 용암층의 탄 바닥(재)이다.
        (Biome.Irradiated, GroundPack + "Dry03",           GroundPack + "Burnt",
                           new Color(0.75f, 1.45f, 0.40f), None),

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

        // **이 층 것만 굽는다** — 장 수가 곧 지형을 몇 번 그리느냐다(URP는 한 패스에 넉 장).
        // 0이면 층을 모른다는 뜻이라 전부 굽는다.
        int floor = MapFloor(out string why);
        if (why != null) Debug.LogWarning("[팔레트] " + why + ".");

        foreach (var (biome, ground, variant, tint, tintV) in Ground)
        {
            // **미지정은 늘 굽는다** — 안 칠한 칸의 폴백이라 어느 층에도 안 걸린다.
            if (floor > 0 && biome != Biome.Unknown && BiomeTier.Of(biome) != floor) continue;

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

        // **이 층에 깔릴 바이옴이 팔레트에 다 있나.** 빠지면 그 칸이 **첫 줄 바닥**으로
        // 떨어지는데, 화면에는 *"저기만 바닥이 다르다"*로만 보이고 원인이 팔레트라는 표시가
        // 없다(분홍색이 되지는 않아 더 안 보인다).
        if (floor > 0)
        {
            var lack = new List<string>();
            foreach (var (biome, _, _, _, _) in Ground)
            {
                if (biome == Biome.Unknown || BiomeTier.Of(biome) != floor) continue;
                if (entries.Exists(e => e.Biome == biome)) continue;
                lack.Add(biome.DisplayName());
            }
            if (lack.Count > 0)
                Debug.LogError($"[팔레트] {floor}층 바이옴 중 못 구운 것: {string.Join(", ", lack)} — " +
                               "텍스처를 못 찾았다. 그 칸은 첫 줄 바닥으로 떨어진다.");
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

        // **이름 규칙이 세트마다 다르다**(2026-09-15). Yughues는 `_d.tga`/`_n.tga`인데
        // cgbookcase는 `_BaseColor.png`/`_Normal.png`다. 하드코딩해 두면 **밖에서 받아 온
        // 텍스처를 쓸 때마다 손으로 이름을 바꾸게 되고**, 그건 곧 안 쓰게 된다는 뜻이다.
        var diffuse = Find(baseName, Color_);
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
        layer.normalMapTexture = Normal(FindPath(baseName, Normal_), IsDirectX(baseName));
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
    // **DirectX로 내보낸 것**(2026-09-15에 자리를 옮겼다).
    //
    // 처음엔 *파일 이름이 `_NormalDX.png`면 뒤집는다*로 뒀는데, 그건 **받은 사람에게 이름을
    // 바꾸라고 시키는 것**이다. 한 번은 하겠지만 다음에 받을 때 반드시 빠뜨리고, 빠뜨리면
    // **조명이 조용히 뒤집힌다**(텍스처도 레이어도 멀쩡해 보인다).
    //
    // **규약은 만든 쪽이 아는 사실이고, 그걸 아는 자리는 그 텍스처를 고른 여기다.**
    // 받은 파일은 **그대로 두고** 여기 한 줄 적는다. cgbookcase는 전부 DirectX다.
    static readonly string[] DirectXNormals = { "Moss01_1K" };

    static bool IsDirectX(string baseName)
    {
        string file = System.IO.Path.GetFileName(baseName);
        foreach (var x in DirectXNormals) if (file == x) return true;
        return false;
    }

    // **훑는 순서가 곧 우선순위다** — 앞엣것이 이긴다. Yughues를 앞에 둬서 지금 있는
    // 것들이 한 톨도 안 달라지게 한다.
    static readonly string[] Color_  = { "_d.tga", "_BaseColor.png", "_Color.png", "_albedo.png", ".png", ".jpg" };
    static readonly string[] Normal_ = { "_n.tga", "_Normal.png", "_NormalGL.png", "_NormalDX.png", "_normal.png" };

    static string FindPath(string baseName, string[] suffixes)
    {
        foreach (var x in suffixes)
        {
            string path = baseName + x;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) return path;
        }
        return null;
    }

    static Texture2D Find(string baseName, string[] suffixes)
    {
        string path = FindPath(baseName, suffixes);
        return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D Normal(string path, bool directX = false)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return null;

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap)
        {
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
        }

        // **DirectX 노멀은 초록이 뒤집혀 있다**(2026-09-15). 유니티는 OpenGL 규약이라
        // 그대로 쓰면 **튀어나온 것이 파인 것으로** 보인다 — 어두운 굴에서 바닥을 위에서
        // 내려다보면 *"뭔가 이상한데 뭔지 모르겠다"*로만 나타나고, 텍스처도 레이어도
        // 멀쩡해 보인다. cgbookcase는 DirectX로 내보낸다.
        //
        // **표가 답한다** — 픽셀을 보고 맞힐 방법이 없다(`DirectXNormals`). 이름이
        // `_NormalDX`로 온 것도 받아 준다.
        if ((directX || path.EndsWith("_NormalDX.png")) && !path.Contains("_flippedGL"))
        {
            var gl = FlipGreen(path);
            if (gl != null) return gl;
        }
        return tex;
    }

    // 초록 채널을 한 번 뒤집어 **옆에 새 파일로** 남긴다. 원본은 안 건드린다 —
    // 다시 받았을 때 *이미 뒤집힌 것을 또 뒤집는* 사고가 없어야 한다.
    static Texture2D FlipGreen(string path)
    {
        // **옆에 새 파일로 남긴다** — 원본을 뒤집으면 다시 받았을 때 *이미 뒤집은 것을 또
        // 뒤집는* 사고가 나고, 그건 되돌릴 방법이 없다(눈으로 구별이 안 된다).
        string dst = System.IO.Path.ChangeExtension(path, null) + "_flippedGL.png";

        var made = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        if (made != null) return Normal(dst);

        // **읽을 수 있게 열어야 픽셀을 본다** — 임포터가 기본으로는 안 열어 준다.
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;

        bool wasReadable = imp.isReadable;
        var  wasType     = imp.textureType;
        bool wasSrgb     = imp.sRGBTexture;

        // **sRGB로 읽으면 값이 휜다** — 노멀은 색이 아니라 **방향을 담은 숫자**라
        // 감마를 먹이면 뒤집기 전에 이미 틀린 값이 된다.
        imp.isReadable   = true;
        imp.textureType  = TextureImporterType.Default;   // 노멀로 읽으면 압축이 끼어든다
        imp.sRGBTexture  = false;
        imp.SaveAndReimport();

        var src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (src == null) return null;

        var px = src.GetPixels();
        for (int i = 0; i < px.Length; i++) px[i].g = 1f - px[i].g;

        var outTex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true, true);
        outTex.SetPixels(px);
        outTex.Apply();
        System.IO.File.WriteAllBytes(dst, outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);

        // 원본을 원래대로 되돌린다.
        imp.isReadable   = wasReadable;
        imp.textureType  = wasType;
        imp.sRGBTexture  = wasSrgb;
        imp.SaveAndReimport();

        AssetDatabase.ImportAsset(dst);
        Debug.Log($"[팔레트] DirectX 노멀의 초록을 뒤집어 저장했다 → {dst}\n" +
                  "유니티는 OpenGL 규약이라, 안 뒤집으면 **튀어나온 것이 파인 것으로** 보인다.");
        return Normal(dst);
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
