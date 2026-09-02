using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// **지하 세계로 갈아엎는다 — 한 번에**(2026-08-29).
//
// 지상 굽기 산출물은 전부 **도구가 만든 것**이라 다시 만드는 것이 잃는 일이 아니다(손으로
// 조각한 지형 델타는 0개였다). 그런데 순서가 있고 하나라도 빠뜨리면 **조용히 어긋난다** —
// 세계 크기를 바꾸면 구운 것이 전부 어긋나고, 장소를 다시 놓지 않으면 새 범위 밖의 마을이
// 통째로 사라지며, 다시 굽지 않으면 저작 씬의 물건이 공중에 뜬 채로 굳는다.
//
// **그래서 한 메뉴로 묶는다.** 손으로 다섯 번 누르게 하면 언젠가 넷만 누른다.
public static class UnderworldSetup
{
    const string BiomeDir = "Assets/Resources/Data/Biomes";

    // ── 지하 바이옴 에셋 ────────────────────────────────────────────────────
    //
    // `BiomeContent`가 `Resources/Data/Biomes`를 훑으므로 **에셋이 없으면 그 바이옴은
    // 티어가 1로 떨어진다**(`BiomeTier.Of`가 `null`이면 `Min`). 심연이 안전 지대가 된다는 뜻이다.
    //
    // **자원·상인·연구는 안 채운다** — 그건 저작이고, 여기서 채우면 나중에 손으로 고친 것을
    // 다시 돌릴 때 덮어쓴다. 이 도구가 책임지는 것은 **빠진 에셋을 만들어 두는 것**까지다.
    static readonly (Biome biome, int tier)[] Underground =
    {
        (Biome.Limestone, 1),   // 석회굴 — 시작 지역
        (Biome.Cavern,    1),   // 동굴
        (Biome.Mine,      2),   // 폐광
        (Biome.Fungal,    2),   // 균사림
        (Biome.Flooded,   3),   // 수몰층
        (Biome.Crystal,   3),   // 수정동
        (Biome.Lava,      4),   // 용암층
        (Biome.Abyss,     5),   // 심연
    };

    [MenuItem("DungeonColony/World/0. Create Underworld Biomes")]
    public static void CreateBiomes()
    {
        Directory.CreateDirectory(BiomeDir);

        // 이미 있는 것을 바이옴으로 색인한다 — **이름이 아니라 값으로 찾는다**(에셋 이름을
        // 바꿔 두었을 수 있고, 이름이 같아도 다른 바이옴을 들고 있을 수 있다).
        var have = new Dictionary<Biome, BiomeContentData>();
        foreach (var guid in AssetDatabase.FindAssets("t:BiomeContentData", new[] { BiomeDir }))
        {
            var a = AssetDatabase.LoadAssetAtPath<BiomeContentData>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) have[a.Biome] = a;
        }

        int made = 0, tiered = 0;
        foreach (var (biome, tier) in Underground)
        {
            if (!have.TryGetValue(biome, out var data))
            {
                data = ScriptableObject.CreateInstance<BiomeContentData>();
                data.Biome = biome;
                AssetDatabase.CreateAsset(data, $"{BiomeDir}/Biome_{biome}.asset");
                made++;
            }

            if (data.Tier != tier)
            {
                Undo.RecordObject(data, "티어");
                data.Tier = tier;
                tiered++;
            }
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[지하] 바이옴 에셋 — 새로 {made}개 · 티어 고침 {tiered}개.\n" +
                  "자원·상인·연구는 안 채웠다(저작이다). `Resources/Data/Biomes/`에서 채울 것.");
    }

    // ── 바위 재질 — 벽도 Yughues로 ──────────────────────────────────────────
    //
    // `RockRenderer`가 비워 두면 **단색**으로 만든다(런타임에 `Resources` 밖의 텍스처를 못
    // 읽는다). 단색 벽은 저폴리 참고 화면에서는 그럴듯했는데, 사진 기반 바닥으로 갈아탄
    // 뒤로는 **바닥만 진짜고 벽은 종이**로 보인다. 그래서 **에셋으로 만들어 씬에 물린다** —
    // 팔레트를 만들어 스트리머에 물리는 것과 같은 자리다.
    //
    // **윗면과 옆면을 따로 만든다.** 윗면은 *안 판 자리 = 어둠*이라 거의 검고, 옆면은
    // *파낸 방의 벽*이라 빛을 받아야 한다. 같은 텍스처를 쓰되 **밝기만 다르다** — 다른
    // 텍스처를 쓰면 위에서 볼 때 벽 윗면과 옆면이 다른 돌처럼 보인다.
    const string MatDir  = "Assets/2_Material/Underworld";
    const string RockTex = "Assets/assetsss/Yughues/YughuesFreeRockMaterials/Textures/T_YFRM_02";

    [MenuItem("DungeonColony/World/0. Create Rock Materials")]
    public static void CreateRockMaterials()
    {
        System.IO.Directory.CreateDirectory(MatDir);
        AssetDatabase.Refresh();

        var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(RockTex + "_d.tga");
        if (diffuse == null)
        {
            Debug.LogWarning($"[지하] 바위 텍스처를 못 찾았다: {RockTex}_d.tga — 단색으로 둔다.");
            return;
        }
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(RockTex + "_n.tga");

        // 윗면은 거의 검다. **완전한 0으로는 안 내린다** — 그러면 벽 윗면과 화면 배경이
        // 구별이 안 가 **세계의 경계**를 못 읽는다.
        var top  = Mat("Rock_Top",  diffuse, normal, new Color(0.05f, 0.06f, 0.07f));
        var wall = Mat("Rock_Wall", diffuse, normal, new Color(0.72f, 0.76f, 0.80f));

        // **찍힌 칸은 스스로 빛난다.** 색만 바꾸면 어두운 굴에서 **주변광에 같이 묻혀**
        // 티가 안 난다 — 밝기를 올리면 벽도 같이 밝아지므로 대비가 그대로다.
        // 발광은 조명과 무관하게 제 밝기를 들고 있어서 **어두울수록 더 잘 보인다.**
        var mark = Mat("Rock_Mark", diffuse, normal, new Color(1.00f, 0.74f, 0.24f),
                       glow: new Color(0.95f, 0.55f, 0.08f));

        AssetDatabase.SaveAssets();

        int wired = 0;
        foreach (var r in Object.FindObjectsByType<RockRenderer>(FindObjectsInactive.Include,
                                                                FindObjectsSortMode.None))
        {
            var so = new SerializedObject(r);
            so.FindProperty("rockMaterial").objectReferenceValue   = top;
            so.FindProperty("wallMaterial").objectReferenceValue   = wall;
            so.FindProperty("markedMaterial").objectReferenceValue = mark;

            // **맨바위를 캐면 나오는 것.** 비워 두면 벽을 파도 아무것도 안 나온다 —
            // 손으로 물리게 하면 언젠가 빠뜨리고, 그건 *"왜 돌이 안 나오지"*로만 보인다.
            var plain = so.FindProperty("plainRock");
            if (plain != null && plain.objectReferenceValue == null)
                plain.objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ResourceNodeData>("Assets/5_Data/Resource/Stone.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(r);
            wired++;
        }

        Debug.Log($"[지하] 바위 재질 셋을 만들어 `RockRenderer` {wired}개에 물렸다 → {MatDir}.\n" +
                  "색이 마음에 안 들면 그 셋의 `Base Map` 색조만 만지면 된다.");
    }

    static Material Mat(string name, Texture2D diffuse, Texture2D normal, Color tint, Color? glow = null)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetTexture("_BaseMap", diffuse);
        m.SetColor("_BaseColor", tint);

        // **매끈함을 낮춘다** — 사진 텍스처의 기본값을 그대로 두면 젖은 플라스틱처럼
        // 번들거려서, 어두운 굴에서 **벽만 혼자 빛난다.**
        m.SetFloat("_Smoothness", 0.04f);
        m.SetFloat("_Metallic",   0f);

        // **양면으로 그린다**(2026-08-29). 벽이 **투명하게 보이던 원인**이다 — 면마다 네
        // 모서리를 손으로 적었는데 방향에 따라 감김이 뒤집혀서, 뒤집힌 면은 뒷면 컬링에
        // 걸려 **통째로 안 그려졌다.** 그 자리는 벽이 아니라 구멍으로 보인다.
        //
        // 감김을 하나씩 맞추는 길도 있지만, **면이 여섯 방향이고 새 면을 추가할 때마다 또
        // 틀린다.** 바위는 속이 꽉 찬 물건이라 양면으로 그려도 잃는 것이 없다.
        m.SetFloat("_Cull", 0f);                    // 0 = Off (양면)
        m.doubleSidedGI = true;

        // **발광은 키워드를 같이 켜야 한다** — 색만 넣으면 URP가 그 패스를 안 돌아
        // **아무 일도 안 일어난다**(값은 들어가 있는데 화면은 그대로라 원인을 짚기 어렵다).
        if (glow.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", glow.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        if (normal != null)
        {
            var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
            m.SetTexture("_BumpMap", normal);
            m.EnableKeyword("_NORMALMAP");
        }

        EditorUtility.SetDirty(m);
        return m;
    }

    // ── 목재는 균사림에서 나온다 ────────────────────────────────────────────
    //
    // **지하에 나무가 없다**는 물음의 답(2026-08-29, 사용자 결정). 세 갈래 중 **균사**를
    // 골랐다: 지상 교역은 *"지상이 없다"*는 결정을 흐리고, 목재를 지우면 **모든 것이 돌로
    // 만들어져** 재료 축이 하나 줄어든다.
    //
    // **표시명은 "목재" 그대로 둔다**(사용자). 묶음(`ResourceGroup.Wood`)은 **화면을 나누는
    // 축**이라 안에 무엇이 들었는지와 무관하고, 이름을 바꾸면 레시피·건물 비용에 걸린 곳을
    // 전부 다시 확인해야 한다 — 얻는 자리만 바꾸면 된다.
    //
    // ── 새 시스템이 없다 ────────────────────────────────────────────────────
    //
    // **광맥과 같은 길로 온다.** `RockMap.VeinAt`이 바이옴의 `ResourceNodes` 풀에서 뽑으므로,
    // 균사림 바이옴에 *"캐면 목재가 나오는 노드"*를 하나 넣는 것이 전부다 — 코드는 한 줄도
    // 안 바뀐다.
    //
    // 그리고 이게 값을 한다: **균사림은 2티어**라 목재를 쓰려면 **시작 지역(석회굴 1티어)을
    // 벗어나야 한다.** *탐험이 본류*라는 루프가 그 자리에서 닫힌다.
    const string WoodItemGuid = "889bd169115baac4f9de284d7066cadc";   // Res_Wood
    const string FungalNodePath = "Assets/5_Data/Resource/FungalWood.asset";

    [MenuItem("DungeonColony/World/0. Create Fungal Wood Source")]
    public static void CreateFungalWood()
    {
        var wood = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(WoodItemGuid));
        if (wood == null) { Debug.LogWarning("[지하] 목재 아이템을 못 찾았다."); return; }

        // ① 캐면 목재가 나오는 노드.
        var node = AssetDatabase.LoadAssetAtPath<ResourceNodeData>(FungalNodePath);
        if (node == null)
        {
            node = ScriptableObject.CreateInstance<ResourceNodeData>();
            AssetDatabase.CreateAsset(node, FungalNodePath);
        }

        node.DropItem = wood;

        // **돌보다 조금 더 걸리고 조금 더 나온다.** 균사림은 2티어라 거기까지 간 값을 한다 —
        // 같은 값이면 *"굳이 위험한 데를 갈 이유"*가 없어진다.
        node.GaugeRequired = 80f;
        node.DropMin = 3;
        node.DropMax = 6;
        node.RequiredSkill = 0f;
        EditorUtility.SetDirty(node);

        // ② 균사림 바이옴의 풀에 넣는다. **여기가 유일한 배선이다** — `RockMap.VeinAt`이
        //    이 목록에서 뽑으므로, 넣는 순간 균사림 벽에서 목재가 나오기 시작한다.
        var fungal = FindBiome(Biome.Fungal);
        if (fungal == null)
        {
            Debug.LogWarning("[지하] 균사림 바이옴 에셋이 없다 — `0. Create Underworld Biomes` 먼저.");
            return;
        }

        if (!fungal.ResourceNodes.Contains(node))
        {
            Undo.RecordObject(fungal, "균사림 자원");
            fungal.ResourceNodes.Add(node);
            EditorUtility.SetDirty(fungal);
        }

        AssetDatabase.SaveAssets();
        BiomeContent.ClearCache();

        Debug.Log("[지하] 균사림 벽에서 **목재**가 나온다 — 캐면 3~6개(게이지 80).\n" +
                  "**균사림은 2티어다** — 시작 지역(석회굴)을 벗어나야 목재를 쓴다.\n" +
                  "표시명은 '목재' 그대로다(묶음은 화면을 나누는 축이라 안에 무엇이 들었는지와 무관하다).");
    }

    static BiomeContentData FindBiome(Biome b)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:BiomeContentData", new[] { BiomeDir }))
        {
            var a = AssetDatabase.LoadAssetAtPath<BiomeContentData>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null && a.Biome == b) return a;
        }
        return null;
    }

    // ── 목재를 돌로 바꾼다 ──────────────────────────────────────────────────
    //
    // **지하에는 나무가 없다.** 그런데 건물 절반이 목재를 요구해서(오두막 63) 아무것도 못
    // 짓는다 — 그러면 파낸 굴이 그냥 빈 구멍으로 남는다.
    //
    // 처음엔 **값을 0으로 내렸는데**, 그건 *"재료가 필요 없다"*는 뜻이라 짓는 일에서 **비용이
    // 통째로 사라진다**(사용자: *"화톳불이 건설할 때 나무가 필요하지 않을까? 돌이 필요하네"*).
    // 지하에서 흔한 것은 **돌**이고, 그건 이미 바위를 파면 나온다 — **파는 일이 곧 자재를
    // 버는 일**이 되어 루프가 닫힌다.
    //
    // **양은 반으로 줄인다.** 목재 값은 나무가 흔한 지상 기준이라 그대로 옮기면 돌 63개짜리
    // 오두막이 된다 — 굴을 예순세 칸 파야 오두막 하나다.
    //
    // **되돌릴 수 있게 로그에 옛 값을 남긴다.**
    const string WoodGuid  = "889bd169115baac4f9de284d7066cadc";   // Res_Wood
    const string StoneGuid = "1bbfcbce39cb01d4fa76acec6a0b64a0";   // Res_Stone

    [MenuItem("DungeonColony/World/0. Swap Wood Costs To Stone")]
    public static void SwapWoodToStone()
    {
        var wood  = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(WoodGuid));
        var stone = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(StoneGuid));
        if (wood == null || stone == null)
        {
            Debug.LogWarning("[지하] 목재나 돌 아이템을 못 찾았다 — 아무것도 안 했다.");
            return;
        }

        var changed = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:BuildingData"))
        {
            var b = AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(guid));
            if (b == null) continue;

            var so   = new SerializedObject(b);
            var item = so.FindProperty("CostItem");
            var amt  = so.FindProperty("CostAmount");
            if (item == null || amt == null) continue;
            if (item.objectReferenceValue != wood) continue;

            int was = amt.intValue;
            item.objectReferenceValue = stone;
            amt.intValue = Mathf.Max(1, Mathf.RoundToInt(was * 0.5f));

            changed.Add($"{b.name} 목재{was}→돌{amt.intValue}");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(b);
        }

        AssetDatabase.SaveAssets();
        Debug.Log(changed.Count == 0
            ? "[지하] 목재를 요구하는 건물이 없다 — 이미 바꿨거나 다른 재료를 쓴다."
            : $"[지하] 목재 비용을 돌로 바꿨다 ({changed.Count}개): {string.Join(" · ", changed)}");
    }

    // ── 지상 굽기 산출물 지우기 ─────────────────────────────────────────────

    [MenuItem("DungeonColony/World/0. Clear Surface Bakes")]
    public static void ClearSurfaceBakes()
    {
        // **높이맵과 마스크** — 비가 깎은 지형이다. 지하에는 비가 안 온다.
        // 떼고 나면 `TerrainHeight.Macro`가 `MacroNoise`로 떨어져 완만한 바닥이 나온다.
        int n = 0;
        n += DeleteIfExists("Assets/Resources/Terrain/Heightmap.asset");
        n += DeleteIfExists("Assets/Resources/Terrain/Heightmap.bytes");
        n += DeleteIfExists("Assets/Resources/Terrain/HeightmapMasks.bytes");

        // **세계 색조** — 지상 지역색이다.
        n += DeleteIfExists("Assets/5_Data/World/WorldColor.png");

        // **구운 청크 내용** — 세계를 4,096m로 줄이면 그 밖의 것은 어차피 사라진다.
        // 남겨 두면 **범위 밖 좌표를 든 마을**이 조회에만 걸려 지도에 유령으로 뜬다.
        int chunks = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:WorldChunkContent",
                                                      new[] { "Assets/Resources/WorldContent" }))
        {
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            chunks++;
        }

        // **손으로 조각한 지형 델타도 지운다** — 세계 크기가 바뀌면 청크 좌표의 뜻이 달라져
        // 그 조각이 엉뚱한 자리에 나타난다. (지금은 0개다.)
        int deltas = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:TerrainDelta", new[] { "Assets/Resources" }))
        {
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            deltas++;
        }

        AssetDatabase.Refresh();
        TerrainHeight.ForgetDeltas();
        TerrainCeiling.ForgetDeltas();

        Debug.Log($"[지하] 지상 굽기 산출물을 지웠다 — 파일 {n}개 · 청크 내용 {chunks}개 · 지형 델타 {deltas}개.\n" +
                  "**git에 남아 있으므로 되돌릴 수 있다.**");
    }

    static int DeleteIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) == null) return 0;
        AssetDatabase.DeleteAsset(path);
        return 1;
    }

    // ── 지도 해상도를 세계에 맞춘다 ─────────────────────────────────────────
    //
    // **`ChunksPerAxis`는 지역 수의 배수여야 한다** — 아니면 바이옴이 바뀌는 자리와 지형이
    // 로드되는 자리가 어긋나 이음매에서 성격이 튄다. 세계를 8,192m에서 4,096m로 줄이면
    // 청크가 32→16이 되는데 저작된 지도는 64×64라 **지역이 64m**가 된다 — 규칙을 깨는 것은
    // 물론이고, 바이옴이 64m마다 바뀌면 걸어 다니는 내내 성격이 흔들린다.
    //
    // **`7. Resize World Map`은 키우기만 한다**(×2/×4). 줄이는 길이 없어 여기서 직접 맞춘다.
    //
    // **그린 것을 안 지킨다** — 지킬 이유가 없다. 저작돼 있는 것은 **지상 바이옴**이고 바로
    // 다음 단계가 지하로 다시 깐다. 격자의 `Content`(옛 마을·소굴)도 같이 비는데, 장소는
    // 이제 `5. Populate World`가 낸다(격자는 바이옴만 든다).
    static bool FitWorldMap()
    {
        var view = Object.FindFirstObjectByType<WorldMap>(FindObjectsInactive.Include);
        var so   = view != null ? new SerializedObject(view) : null;
        var data = so?.FindProperty("mapData")?.objectReferenceValue as WorldMapData;

        if (data == null)
        {
            Debug.LogError("[지하] 씬의 `WorldMap`에 mapData가 안 물려 있다 — 여기서 멈춘다.");
            return false;
        }

        // **지역을 청크 하나 크기로 맞춘다.** 세계 4,096m · 청크 256m이므로 지역도 256m다.
        // 청크보다 크게 잡으면(예: 8×8 = 512m) 바이옴 덩어리가 세계의 절반이 되어 서너 개밖에
        // 안 생긴다 — 지하 바이옴 여덟을 만들어 놓고 셋만 보게 된다.
        int want = TerrainGrid.ChunksPerAxis;

        if (data.Width == want && data.Height == want) return true;

        Undo.RecordObject(data, "지도 해상도 맞추기");
        int oldW = data.Width, oldH = data.Height;
        data.Resize(want, want);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        TerrainGrid.ForgetRegions();
        TerrainHeight.ForgetBiomes();

        Debug.Log($"[지하] 지도 {oldW}×{oldH} → {want}×{want} (지역 {TerrainGrid.RegionSize}m = 청크 하나).");
        return true;
    }

    // ── 한 번에 다시 세우기 ─────────────────────────────────────────────────

    // **순서가 규칙이다.** `CLAUDE.md`가 적어 둔 그 순서이고, 하나라도 건너뛰면 조용히 어긋난다:
    //
    //   바이옴 에셋 → 지상 굽기 지우기 → 배선(천장 켜기·높이맵 떼기)
    //     → 지도 크기 → 바이옴 배치 → 장소 배치 → 지면에 맞추기 → 굽기
    //
    // **`ExecuteMenuItem`으로 부른다** — 각 도구의 메서드가 `private static`이라서이기도 하지만,
    // 더 큰 이유는 **사람이 누르는 것과 같은 길로 가야** 도구가 바뀌어도 여기가 안 뒤처지기
    // 때문이다. 메뉴 이름이 바뀌면 바로 실패해서 알려 준다(조용히 건너뛰지 않는다).
    [MenuItem("DungeonColony/World/1. Rebuild Underworld (전부)")]
    static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog(
                "지하 세계로 다시 세운다",
                $"세계를 {TerrainGrid.WorldSize}m({TerrainGrid.ChunksPerAxis}×{TerrainGrid.ChunksPerAxis} 청크)로 다시 세운다.\n\n" +
                "· 지상 높이맵·색조·구운 청크 내용을 지운다\n" +
                "· 바이옴을 지하로 다시 깐다\n" +
                "· 장소를 다시 배치하고 굽는다\n\n" +
                "손으로 놓은 장소(Generated 꺼짐)는 그대로 둔다.\n" +
                "지운 것은 git에 남아 있다.",
                "다시 세운다", "그만"))
            return;

        CreateBiomes();
        ClearSurfaceBakes();

        // 순서대로. **하나라도 실패하면 멈춘다** — 어긋난 채로 다음을 돌리면 그 다음이
        // 무엇을 봤는지 알 수 없게 된다.
        if (!FitWorldMap()) return;

        string[] steps =
        {
            "DungeonColony/Terrain/0. Setup World Streaming",   // 천장 켜기 · 높이맵 떼기 · 렌더러 붙이기
            "DungeonColony/Terrain/9. Create Terrain Palette (지하)",  // 바닥 텍스처를 지하 것으로
            "DungeonColony/World/0. Create Rock Materials",     // 벽 텍스처도 같은 세트로
            "DungeonColony/World/0. Swap Wood Costs To Stone",  // 지하에는 나무가 없다 — 돌로
            "DungeonColony/World/0. Create Fungal Wood Source", // 목재는 균사림에서 캔다
            "DungeonColony/Terrain/8. Generate Biome Map",      // 지하 바이옴을 깐다
            "DungeonColony/World/3. Open Authoring Scene",      // **장소는 저작 씬에 놓인다**
        };

        foreach (var m in steps)
        {
            if (EditorApplication.ExecuteMenuItem(m)) continue;

            Debug.LogError($"[지하] `{m}`을 실행하지 못했다 — 여기서 멈춘다. " +
                           "메뉴 이름이 바뀌었거나 그 도구가 조건을 못 채웠다.");
            return;
        }

        // ── 장소 ────────────────────────────────────────────────────────────
        //
        // **`ExecuteMenuItem`으로 안 부른다.** 배치 도구의 메뉴 항목은 *창을 여는 것*이라
        // 그렇게 부르면 **창만 뜨고 아무것도 안 놓이는데 참을 돌려준다** — 묶음은 성공한 줄
        // 알고 넘어가고, 그 뒤 굽기는 빈 씬을 구워 **세계에 마을이 하나도 없게 된다.**
        // 실제로 그렇게 됐다(구운 청크 0개, 2026-08-30에 드러났다).
        WorldPopulator.RunHeadless();

        var authoring = WorldAuthoringScene.FindRoot();
        if (authoring == null)
        {
            Debug.LogError("[지하] 저작 루트를 못 찾았다 — 장소를 놓지도 굽지도 못한다. 여기서 멈춘다.");
            return;
        }

        // **굽기는 "씬이 세계 전부를 담고 있다"는 표시를 요구한다**(없으면 거절한다 — 일부만
        // 펼쳐 놓고 구우면 안 펼친 청크가 통째로 사라지기 때문이다). 여기서는 바로 앞에서
        // **구운 것을 전부 지웠으므로** 씬이 곧 세계 전부다. 그 사실을 적어 준다.
        authoring.MarkLoaded(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " (Rebuild)");
        EditorUtility.SetDirty(authoring);

        foreach (var m in new[]
                 {
                     "DungeonColony/World/6. Resnap Sites To Terrain",   // 지면이 바뀌었으니 다시 앉힌다
                     "DungeonColony/World/5. Bake World",                // 청크 에셋으로 굽는다
                 })
        {
            if (EditorApplication.ExecuteMenuItem(m)) continue;

            Debug.LogError($"[지하] `{m}`을 실행하지 못했다 — 여기서 멈춘다.");
            return;
        }

        // **구워졌는지 세어 본다.** 위의 도구들은 조건을 못 채우면 **로그만 남기고 참을
        // 돌려준다** — 그래서 묶음이 *"다 세웠다"*라고 말하는데 세계가 비어 있을 수 있다.
        // 한 번 겪은 사고는 **다음에 조용히 지나가지 않게** 여기서 막는다.
        int baked = AssetDatabase.FindAssets("t:WorldChunkContent",
                                             new[] { "Assets/Resources/WorldContent" }).Length;
        if (baked == 0)
            Debug.LogError("[지하] **구운 청크가 0개다 — 게임에 마을도 소굴도 안 선다.**\n" +
                           "저작 씬(`World.unity`)이 열려 있는지, 배치가 실제로 놓았는지 위 로그를 볼 것.");

        TerrainGrid.ForgetRegions();
        TerrainHeight.ForgetBiomes();
        TerrainHeight.ForgetDeltas();
        TerrainCeiling.ForgetDeltas();

        Debug.Log($"[지하] 다 세웠다 — {TerrainGrid.WorldSize}m · 지역 {TerrainGrid.RegionSize}m.\n" +
                  "**씬을 저장할 것**(배선·지형 프로필·조명이 씬에 들어갔다).\n" +
                  "남은 것은 지하 색조뿐이다(`C. Bake World Color Map` — 지금은 그림이 없어 꺼져 있다).");
    }

    // ── 굴이 어떻게 났는지 본다 ─────────────────────────────────────────────
    //
    // **저작해 놓고 못 보면 그건 짐작이다.** 천장 값을 만질 때마다 Play Mode에 들어가
    // 카메라를 끌고 다니며 *"길이 났나"*를 확인하는 것은 값 하나 바꾸는 데 1분이라, 결국
    // 아무도 안 맞추고 기본값으로 굳는다. 세계 전체를 그림 한 장으로 뽑는다.
    //
    // **판정과 같은 함수를 쓴다**(`TerrainCeiling.ClearanceAt` + 폰 키) — 여기서 눈금을
    // 따로 들면 *"그림에는 길이 있는데 폰은 못 지나가는"* 자리가 생긴다.
    //
    // **`Terrain`도 메시도 안 만든다** — 보기만 할 것이라 PNG 한 장이면 되고, 씬에 세우면
    // 커서 레이캐스트가 그걸 짚는다(`E. World Preview`가 콜라이더를 안 다는 그 이유).
    [MenuItem("DungeonColony/World/2. Preview Cave Map")]
    static void PreviewCaves()
    {
        var st = TerrainAuthoring.SyncValues();
        if (st == null)
        {
            Debug.LogError("[굴] 씬에 `TerrainStreamer`가 없다 — 게임이 쓸 값을 알 수 없어 그리지 않는다.");
            return;
        }
        TerrainCeiling.ForgetDeltas();

        int world = TerrainGrid.WorldSize;
        int step  = Mathf.Max(1, Mathf.CeilToInt(world / 1024f));   // 큰 세계는 성기게
        int res   = world / step;

        var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
        var px  = new Color32[res * res];

        int open = 0, crawl = 0;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float c = TerrainCeiling.ClearanceAt(x * step + 0.5f, y * step + 0.5f);

            Color32 col;
            if (c >= PawnMover.BodyHeight)      { open++;  col = new Color32(226, 214, 188, 255); }  // 지나갈 수 있다
            else if (c > 0f)                    { crawl++; col = new Color32(120, 104,  84, 255); }  // 기어야 하는 틈
            else                                {          col = new Color32( 38,  36,  33, 255); }  // 바위

            px[y * res + x] = col;
        }
        tex.SetPixels32(px);
        tex.Apply();

        string dir  = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Tools");
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, "cave_preview.png");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        float total = res * (float)res;
        Debug.Log($"[굴] {world}m 세계 · {step}m/픽셀 — **지나갈 수 있는 곳 {open / total:P0}** · " +
                  $"기어야 하는 틈 {crawl / total:P0} · 바위 {(total - open - crawl) / total:P0}\n{path}\n" +
                  "**3분의 1 언저리가 열려 있어야 굴처럼 보인다** — 너무 적으면 통로 폭을, " +
                  "너무 많으면 주기를 키울 것(캘 바위가 남아야 한다).");

        EditorUtility.RevealInFinder(path);
    }

    // ── 이어져 있나 — 그물을 색으로 가른다 ──────────────────────────────────
    //
    // **`2. Preview Cave Map`은 *어디가 열렸나*에 답하지 *어디까지 갈 수 있나*에는 답하지
    // 못한다.** 그런데 길찾기를 고칠 때 실제로 필요한 물음은 뒤쪽이다 — *"폰이 여기서 저기로
    // 걸어갈 수 있나"*를 화면으로 못 재니, 고칠 때마다 **의견끼리 부딪혔다**(*"길은 이어져
    // 있어" ↔ "격자는 끊겼다는데"*). 2026-08-31에 그 다툼으로 하루가 반나절 갔다.
    //
    // **폰과 같은 규칙으로 훑는다** — 8방향이되 **대각선은 직교 이웃 둘이 다 열렸을 때만**
    // (`PathGrid.Enterable`과 같은 규칙이다. 여기서 규칙을 따로 쓰면 그림이 또 거짓말을 한다).
    //
    // **시작점을 안 묻는다** — 덩어리를 전부 세어 **큰 것부터 색을 준다.** 그러면 한 장으로
    // *"이 두 곳이 같은 그물인가"*에 답할 수 있고, 시작 자리가 어디냐에 안 흔들린다.
    [MenuItem("DungeonColony/World/2b. Preview Cave Connectivity (그물)")]
    static void PreviewConnectivity()
    {
        var st = TerrainAuthoring.SyncValues();
        if (st == null)
        {
            Debug.LogError("[굴] 씬에 `TerrainStreamer`가 없다 — 게임이 쓸 값을 알 수 없어 그리지 않는다.");
            return;
        }
        TerrainCeiling.ForgetDeltas();

        int world = TerrainGrid.WorldSize;
        int res   = world;                     // **1m 눈금 그대로** — 폰이 걷는 눈금이다
        var walk  = new bool[res * res];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
            walk[y * res + x] = TerrainCeiling.ClearanceAt(x + 0.5f, y + 0.5f) >= PawnMover.BodyHeight;

        // 덩어리 나누기 — 반복형 BFS(재귀는 100만 칸에서 스택이 터진다)
        var label = new int[res * res];        // 0 = 아직, -1 = 바위
        for (int i = 0; i < label.Length; i++) if (!walk[i]) label[i] = -1;

        var sizes = new System.Collections.Generic.List<int> { 0 };   // 1번부터 쓴다
        var queue = new int[res * res];

        for (int start = 0; start < label.Length; start++)
        {
            if (label[start] != 0) continue;

            int id = sizes.Count;
            sizes.Add(0);

            int head = 0, tail = 0;
            label[start] = id;
            queue[tail++] = start;

            while (head < tail)
            {
                int i = queue[head++];
                sizes[id]++;

                int cx = i % res, cy = i / res;
                for (int d = 0; d < 8; d++)
                {
                    int dx = d < 4 ? (d == 0 ? 1 : d == 1 ? -1 : 0) : (d == 4 || d == 5 ? 1 : -1);
                    int dy = d < 4 ? (d == 2 ? 1 : d == 3 ? -1 : 0) : (d == 4 || d == 6 ? 1 : -1);

                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= res || ny >= res) continue;

                    int ni = ny * res + nx;
                    if (label[ni] != 0) continue;

                    // **대각선은 직교 둘이 다 열려야 간다** — 폰의 규칙 그대로.
                    if (dx != 0 && dy != 0 &&
                        (!walk[cy * res + (cx + dx)] || !walk[(cy + dy) * res + cx])) continue;

                    label[ni] = id;
                    queue[tail++] = ni;
                }
            }
        }

        // 큰 것부터 순위를 매긴다
        var order = new System.Collections.Generic.List<int>();
        for (int id = 1; id < sizes.Count; id++) order.Add(id);
        order.Sort((a, b) => sizes[b].CompareTo(sizes[a]));

        var rank = new int[sizes.Count];
        for (int r = 0; r < order.Count; r++) rank[order[r]] = r;

        var palette = new[]
        {
            new Color32(226, 214, 188, 255),   // 1등 — 본 그물
            new Color32( 96, 176, 232, 255),   // 2등
            new Color32(232, 152,  72, 255),   // 3등
            new Color32(200,  84,  96, 255),   // 나머지 — **여기 찍으면 파기 전엔 못 간다**
        };

        var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
        var px  = new Color32[res * res];
        for (int i = 0; i < label.Length; i++)
        {
            int id = label[i];
            px[i] = id < 0 ? new Color32(38, 36, 33, 255)
                           : palette[Mathf.Min(rank[id], palette.Length - 1)];
        }
        tex.SetPixels32(px);
        tex.Apply();

        string dir  = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Tools");
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, "cave_connectivity.png");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        int openTotal = 0;
        foreach (int id in order) openTotal += sizes[id];

        var sb = new System.Text.StringBuilder();
        sb.Append($"[굴] 그물 {order.Count}개 · 열린 넓이 {openTotal / (float)(res * res):P0}\n");
        for (int r = 0; r < Mathf.Min(5, order.Count); r++)
            sb.Append($"  {r + 1}등 {sizes[order[r]]:N0}칸 ({sizes[order[r]] / (float)openTotal:P0})\n");
        sb.Append(path + "\n**색이 다르면 파기 전에는 서로 못 간다** — 길찾기가 아니라 세계의 문제다.");
        Debug.Log(sb.ToString());

        EditorUtility.RevealInFinder(path);
    }
}
