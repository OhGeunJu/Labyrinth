using UnityEditor;
using UnityEngine;

// **목재 사다리 — 원목/판자 위에 단단한 나무/단단한 판자를 얹는다**
// (2026-09-17, 사용자 결정: *"2층 장비 전부"*).
//
// ## 왜 필요했나
//
// `Res_Hardwood`(단단한 나무)는 2026-09-15에 지저늪(2층)에 심어 놓고 **먹는 것을 안 정했다** —
// 캐도 팔 것밖에 안 되는 자원이라, 2층에 내려갈 이유 하나가 비어 있었다. 금속은 층마다
// 하나씩 서는데(`docs/design_metals.md`) **목재만 층을 안 탔다.**
//
// ## 규칙은 금속과 같다 — 원료 → 중간재 → 장비
//
// | 층 | 원료 | 중간재 | 먹는 것 |
// |---|---|---|---|
// | 1 | 원목 | 판자 | 청동 장비 · 활 · 지팡이 · 철창 · 석궁 |
// | 2 | 단단한 나무 | **단단한 판자** | **강철 · 티타늄 · 미스릴 장비** · 작업대 II |
// | 3 | 서리나무(빙하굴) | 서리 판자 | 작업대 III (2026-09-30) |
// | 4 | 흑단(심연) | 흑단 판자 | 작업대 IV (2026-09-30) |
//
// **가르는 줄은 "금속 이름을 단 장비"다.** 강철검 자루가 단단한 판자를 먹으므로 강철을
// 뽑는 층과 그 자루를 베는 층이 같아진다 — *깊이가 곧 티어*가 목재 축에도 걸린다.
//
// ## 안 건드린 것과 그 이유
//
// - **철창(`Wpn_Spear`)과 석궁** — 강철을 하나 먹지만 **주재료가 목재**이고, 청동 창이
//   없어서 철창이 **청동과 강철 사이를 메운다**(`docs/design_metals.md`의 *"철검은 안
//   지운다"*와 같은 자리). 여기를 올리면 1~2층 사이에 창이 없어진다.
// - **합판** — 이미 *판자의 다음*이지만 **다른 축**이다: 합판은 *여러 장을 붙인 것*(휘어야
//   하는 활대)이고 단단한 판자는 *다른 나무*(단단해야 하는 자루)다. **사슬로 이으면
//   지저늪 하나만 가면 되는 것**이 되므로(허브 ↔ 푸른 곰팡이와 같은 규칙) 나란히 둔다.
// - **지팡이** — 목재는 몸이고 심장은 결정이다. 마법 광물 축이 따로 답한다.
//
// ## 연구를 새로 안 만든다
//
// 단단한 판자도 **목공술**이 연다. 켜는 법은 판자와 같고, 막고 있는 것은 이미 **2층에
// 가는 일**이다 — 연구까지 걸면 *재료도 없고 연구도 없는* 이중 잠금이라, 열려도 아무것도
// 안 달라지는 연구가 하나 는다.
//
// **다시 돌려도 된다** — 있으면 값만 맞춘다(guid 유지).
public static class WoodCreator
{
    const string ItemDir     = "Assets/Resources/Data/Items";
    const string RecipeDir   = "Assets/Resources/Data/Recipes";
    const string ResearchDir = "Assets/Resources/Data/Research";

    [MenuItem("DungeonColony/World/E. Create Wood Ladder (단단한 · 서리 · 흑단 판자)")]
    static void Create()
    {
        var hardwood = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/Res_Hardwood.asset");
        if (hardwood == null)
        {
            Debug.LogError("[목재] Res_Hardwood가 없습니다 — `7. Create Flora`를 먼저 돌리세요.");
            return;
        }

        // **값은 판자와 같은 배수에서 나온다.** 원목 3(9G) → 판자 2(16G)가 1.78배이므로
        // 단단한 나무 3(36G) → 단단한 판자 2는 64G, 개당 **32G**다.
        //
        // **판자와 나란히 적을 것** — 배수를 따로 정하면 *제재가 나무마다 다르게 남는*
        // 뜻이 되고, 그건 `Tools/export_gamedata.py`가 붉게 칠해 줄 때까지 안 보인다.
        var plank = Item("Res_HardLumber", "단단한 판자", 32, 1.6f,
            "물먹은 둥치를 켜서 말린 판자. 무겁고 결이 빽빽해 자루가 부러지지 않는다.");

        // **판자와 같은 얼개, 더 느리고 더 어렵다**(12초/스킬 3 → 20초/스킬 6).
        // 나무가 단단하다는 것은 화면에서 **켜는 데 오래 걸리는 것**으로만 드러난다.
        var recipe = Recipe("Rec_HardLumber", "단단한 판자", hardwood, 3, plank, 2,
                            craftTime: 20f, skillLevel: 6f);

        WireResearch(recipe);

        CreateDeepWoods();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[목재] 단단한 판자(32G) · 서리 판자(80G, 3층 빙하굴) · 흑단 판자(200G, 4층 심연)와 " +
                  "그 레시피를 만들고 목공술에 물렸습니다. 서리나무·흑단 고목을 뿌리기에 걸었습니다 — **씬을 저장하세요.**\n"
                + "**먹는 쪽은 JSON 레시피라 이 도구가 안 건드립니다** — "
                + "강철·티타늄·미스릴 장비 일곱이 이미 단단한 판자를 가리킵니다. 서리·흑단 판자는 지금 작업대 등급 재료(`BenchTier.CostTo`)만 먹습니다.");
    }

    // ── 층마다 새 나무 — 3층 서리나무 · 4층 흑단 ──────────────────────────
    //
    // (2026-09-30, 사용자: *"단단한 판자도 좀 다른 걸로 바꿔야지 계속 쓸 거야?"* ·
    //  *"나무 류로 가야지 나무 류가 층마다 새로운 나무가 나는 느낌"*)
    //
    // 작업대 등급을 *다음 층의 물건*으로 올리면서(`BenchTier.CostTo`) II·III·IV가 전부 단단한
    // 판자를 먹었다 — **금속만 층을 타고 목재는 2층에서 멈췄다.** 사다리를 층 수만큼 늘린다:
    //
    // | 층 | 나무(뿌리기) | 원료 | 판자 | 어디 |
    // |---|---|---|---|---|
    // | 1 | 거대버섯 · 이끼 낀 나무 | 원목 3G | 판자 8G | 균사림 · 이끼굴 |
    // | 2 | 물먹은 둥치 | 단단한 나무 12G | 단단한 판자 32G | 지저늪 |
    // | 3 | **서리나무** | **서리나무 30G** | **서리 판자 80G** | **빙하굴** |
    // | 4 | **흑단 고목** | **흑단 75G** | **흑단 판자 200G** | **심연** |
    //
    // **값은 한 사다리다** — 원료가 층마다 2.5배, 판자는 *원료 3 → 판자 2*에 판자와 같은
    // 배수(≈2.67)를 곱한다. 배수를 나무마다 따로 정하면 *제재가 나무마다 다르게 남는* 뜻이 된다.
    //
    // **한 바이옴에만 난다**(물먹은 둥치가 지저늪에만 나는 것과 같다) — 층 전부에 뿌리면
    // *어디로 가나*가 안 좁혀지고, 무엇보다 **그 바이옴의 얼굴**이 흐려진다. 빙하굴은 얼음,
    // 심연은 어둠이라 둘의 성격에서 나무가 나온다.
    //
    // **연구를 새로 안 만든다**(단단한 판자와 같은 이유) — 목공술이 연다. 막고 있는 것은
    // 이미 **그 층에 가는 일**이다.
    static readonly Biome[] GlacierOnly = { Biome.Glacier };
    static readonly Biome[] AbyssOnly   = { Biome.Abyss };

    static void CreateDeepWoods()
    {
        // ── 3층 — 서리나무 ───────────────────────────────────────────────
        var frost = Item("Res_Frostwood", "서리나무", 30, 1.2f,
            "빙하굴의 얼음 속에서 자란 나무. 속까지 얼어 있어 쪼개면 서리가 인다.");
        var frostPlank = Item("Res_FrostLumber", "서리 판자", 80, 1.4f,
            "서리나무를 켜서 녹이지 않고 말린 판자. 가볍고 휘지 않는다.");
        var frostRecipe = Recipe("Rec_FrostLumber", "서리 판자", frost, 3, frostPlank, 2,
                                 craftTime: 28f, skillLevel: 12f);
        WireResearch(frostRecipe);

        var frostNode = MushroomCreator.Node("FrostwoodTree", frost, prefab: null,
            gauge: 400f, min: 3, max: 5, workers: 2, totalYields: 5,
            displayName: "서리나무",
            description: "얼음에 갇힌 채 자라는 나무. 가지마다 얼음 결정이 맺혔다. " +
                         "**다섯 번 베면 없어진다.**");
        var frostPrefab = BuildFrostTree(frostNode);
        frostNode.Prefab = frostPrefab;
        EditorUtility.SetDirty(frostNode);

        // ── 4층 — 흑단 ───────────────────────────────────────────────────
        var ebony = Item("Res_Ebony", "흑단", 75, 1.6f,
            "심연의 어둠 속에서 천 년을 자란 검은 나무. 돌처럼 무겁고 쇠처럼 단단하다.");
        var ebonyPlank = Item("Res_EbonyLumber", "흑단 판자", 200, 1.8f,
            "흑단을 켠 판자. 결이 보이지 않을 만큼 빽빽하고, 두드리면 쇳소리가 난다.");
        var ebonyRecipe = Recipe("Rec_EbonyLumber", "흑단 판자", ebony, 3, ebonyPlank, 2,
                                 craftTime: 40f, skillLevel: 20f);
        WireResearch(ebonyRecipe);

        var ebonyNode = MushroomCreator.Node("EbonyTree", ebony, prefab: null,
            gauge: 520f, min: 2, max: 4, workers: 2, totalYields: 5,
            displayName: "흑단 고목",
            description: "심연 바닥에 뿌리를 박은 검은 고목. 잎 하나 없이 비틀려 올라갔다. " +
                         "**다섯 번 베면 없어진다.**");
        var ebonyPrefab = BuildEbonyTree(ebonyNode);
        ebonyNode.Prefab = ebonyPrefab;
        EditorUtility.SetDirty(ebonyNode);

        // ── 뿌리기 — 물먹은 둥치와 같은 밀도(상위 재료는 드물게) ─────────
        if (!ScatterWiring.Open("목재", out var so, out var rules)) return;
        ScatterWiring.Set(rules, "서리나무", frostPrefab, perChunk: 2.5f, spacing: 16f,
             regrowDays: MushroomCreator.TreeRegrowDays, biomes: GlacierOnly, cluster: (1, 1), spread: 0);
        ScatterWiring.Set(rules, "흑단 고목", ebonyPrefab, perChunk: 2f, spacing: 18f,
             regrowDays: MushroomCreator.TreeRegrowDays, biomes: AbyssOnly, cluster: (1, 1), spread: 0);
        ScatterWiring.Commit(so);
    }

    // **서리나무 — 곧고 흰 줄기에 얼음 결정이 맺혔다.** 물먹은 둥치(검고 기울고 부러진)와
    // 실루엣이 갈려야 층이 읽힌다: 이쪽은 **곧게 서고 위가 뾰족하다.** 결정은 빛나지 않는다
    // (빙하굴은 이미 밝은 바이옴이라, 등을 달면 야광석과 구별이 흐려진다).
    static GameObject BuildFrostTree(ResourceNodeData data)
    {
        var root = new GameObject("Node_FrostwoodTree");
        var bark = new Color(0.62f, 0.68f, 0.74f);
        var ice  = new Color(0.72f, 0.88f, 1.00f);

        var low = MushroomCreator.Prim(PrimitiveType.Cylinder, "TrunkLow", root.transform,
                       new Vector3(0.55f, 0.90f, 0.55f), new Vector3(0f, 0.90f, 0f));
        MushroomCreator.Paint(low, "Mdl_Frostwood_Bark", bark);

        var high = MushroomCreator.Prim(PrimitiveType.Cylinder, "TrunkHigh", root.transform,
                        new Vector3(0.36f, 0.85f, 0.36f), new Vector3(0f, 2.55f, 0f));
        MushroomCreator.Paint(high, "Mdl_Frostwood_Bark", bark);

        // 꼭대기 — 뾰족하게. 둥근 윗면이면 기둥으로 읽힌다.
        var tip = MushroomCreator.Prim(PrimitiveType.Cube, "Tip", root.transform,
                       new Vector3(0.30f, 0.60f, 0.30f), new Vector3(0f, 3.55f, 0f));
        tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        MushroomCreator.Paint(tip, "Mdl_Frostwood_Ice", ice);

        // 가지 대신 결정 — 세 방향으로 비스듬히. 같은 높이에 두면 바퀴살로 보인다.
        for (int i = 0; i < 3; i++)
        {
            float yaw = i * 120f + 20f;
            float y   = 1.7f + i * 0.55f;
            var shard = MushroomCreator.Prim(PrimitiveType.Cube, $"Shard{i}", root.transform,
                            new Vector3(0.16f, 0.70f, 0.16f), Vector3.zero);
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            shard.transform.localPosition = new Vector3(0f, y, 0f) + dir * 0.35f;
            shard.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(50f, 0f, 0f);
            MushroomCreator.Paint(shard, "Mdl_Frostwood_Ice", ice);
        }

        AddTreeBody(root, data, height: 3.8f, radius: 0.5f);
        return MushroomCreator.Save(root, "Node_FrostwoodTree");
    }

    // **흑단 고목 — 굵은 뿌리목에서 비틀려 올라간 검은 줄기.** 심연은 이 저장소에서 유일하게
    // 어두운 바이옴이라 나무도 거의 검다. 속살(가지 끝)만 **자줏빛으로 한 톤 밝게** — 전부 같은
    // 색이면 어둠 속에서 윤곽이 사라진다.
    static GameObject BuildEbonyTree(ResourceNodeData data)
    {
        var root = new GameObject("Node_EbonyTree");
        var bark = new Color(0.09f, 0.07f, 0.10f);
        var core = new Color(0.32f, 0.18f, 0.34f);

        var flare = MushroomCreator.Prim(PrimitiveType.Cylinder, "Flare", root.transform,
                         new Vector3(1.10f, 0.25f, 1.10f), new Vector3(0f, 0.22f, 0f));
        MushroomCreator.Paint(flare, "Mdl_Ebony_Bark", bark);

        // 세 토막을 조금씩 틀어 쌓는다 — *비틀려 올라갔다*.
        float y = 0.45f;
        float w = 0.72f;
        for (int i = 0; i < 3; i++)
        {
            float h = 0.75f;
            var seg = MushroomCreator.Prim(PrimitiveType.Cylinder, $"Trunk{i}", root.transform,
                          new Vector3(w, h, w), new Vector3(0.08f * i, y + h, 0.05f * i));
            seg.transform.localRotation = Quaternion.Euler(4f * i, 35f * i, -5f * i);
            MushroomCreator.Paint(seg, "Mdl_Ebony_Bark", bark);
            y += h * 1.7f;
            w *= 0.78f;
        }

        // 잎 없는 가지 둘 — 끝이 속살 색.
        for (int i = 0; i < 2; i++)
        {
            float yaw = i * 160f + 30f;
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var branch = MushroomCreator.Prim(PrimitiveType.Cylinder, $"Branch{i}", root.transform,
                             new Vector3(0.14f, 0.55f, 0.14f), Vector3.zero);
            branch.transform.localPosition = new Vector3(0.12f, y - 0.3f, 0.08f) + dir * 0.45f;
            branch.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(55f, 0f, 0f);
            MushroomCreator.Paint(branch, "Mdl_Ebony_Core", core);
        }

        AddTreeBody(root, data, height: 4.2f, radius: 0.6f);
        return MushroomCreator.Save(root, "Node_EbonyTree");
    }

    // **트리거가 아니다** — 나무 자리라 몸으로 막는다(물먹은 둥치와 같은 규칙).
    static void AddTreeBody(GameObject root, ResourceNodeData data, float height, float radius)
    {
        var col = root.AddComponent<CapsuleCollider>();
        col.height = height;
        col.radius = radius;
        col.center = new Vector3(0f, height * 0.5f, 0f);

        var node = root.AddComponent<ResourceNode>();
        SerializedWrite.Set(node, so => so.FindProperty("data").objectReferenceValue = data);
    }

    // ── 목공술이 둘 다 연다 ─────────────────────────────────────────────────
    //
    // **`UnlockedRecipes`에 안 넣으면 레시피가 처음부터 열려 있다**(`RequiredResearch`만으로는
    // 한쪽 방향이다 — CLAUDE.md의 *"잠금은 양방향 어느 쪽으로 적어도 걸린다"*). 둘 다 적어
    // 두면 **연구 화면이 이 연구가 무엇을 여는지 말한다.**
    static void WireResearch(RecipeData recipe)
    {
        var research = AssetDatabase.LoadAssetAtPath<ResearchData>(ResearchDir + "/Res_Woodworking.asset");
        if (research == null)
        {
            Debug.LogWarning("[목재] 목공술 연구를 못 찾아 레시피를 안 잠갔습니다.");
            return;
        }

        recipe.RequiredResearch = research;
        EditorUtility.SetDirty(recipe);

        if (!research.UnlockedRecipes.Contains(recipe))
        {
            research.UnlockedRecipes.Add(recipe);
            EditorUtility.SetDirty(research);
        }
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────────────

    static ResourceItemData Item(string id, string name, int value, float weight, string desc)
    {
        string path = ItemDir + "/" + id + ".asset";
        var r = AssetDatabase.LoadAssetAtPath<ResourceItemData>(path);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<ResourceItemData>();
            AssetDatabase.CreateAsset(r, path);
        }

        r.Id          = id;
        r.ItemName    = name;
        r.Description = desc;
        r.Weight      = weight;
        r.Width       = 1;
        r.Height      = 1;
        r.BaseValue   = value;
        r.Group       = ResourceGroup.Wood;   // 창고 필터가 목재로 묶는다
        r.Stack       = 200;
        EditorUtility.SetDirty(r);
        return r;
    }

    static RecipeData Recipe(string assetName, string name, ItemData ingredient, int amount,
                             ItemData result, int resultAmount, float craftTime, float skillLevel)
    {
        string path = RecipeDir + "/" + assetName + ".asset";
        var r = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<RecipeData>();
            AssetDatabase.CreateAsset(r, path);
        }

        r.RecipeName = name;
        r.Ingredients.Clear();
        r.Ingredients.Add(new RecipeData.Ingredient { Item = ingredient, Amount = amount });
        r.Result             = result;
        r.ResultAmount       = resultAmount;
        r.CraftTime          = craftTime;
        r.Category           = JobCategory.Crafting;
        r.RequiredSkillType  = CraftSkill.Woodworking;
        r.RequiredSkillLevel = skillLevel;
        r.SkillGrowthAmount  = 0.5f;
        EditorUtility.SetDirty(r);
        return r;
    }
}
