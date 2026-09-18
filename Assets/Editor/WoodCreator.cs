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
// | 2 | 단단한 나무 | **단단한 판자** | **강철 · 티타늄 · 미스릴 장비** |
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

    [MenuItem("DungeonColony/World/E. Create Wood Ladder (단단한 판자)")]
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

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[목재] 단단한 판자(32G)와 그 레시피를 만들고 목공술에 물렸습니다.\n"
                + "**먹는 쪽은 JSON 레시피라 이 도구가 안 건드립니다** — "
                + "강철·티타늄·미스릴 장비 일곱이 이미 그것을 가리킵니다.");
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
