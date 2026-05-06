using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 게임 데이터 중앙 레지스트리.
// RuntimeInitializeOnLoadMethod로 자동 초기화 — 씬에 GameObject 추가 불필요.
//
// 로딩 순서:
//   1. Resources/Data/Items, Recipes, Research  ← 기존 SO 에셋 (Assets/Resources/Data/ 폴더에 위치해야 함)
//   2. StreamingAssets/Data/items, recipes      ← 기본 JSON 콘텐츠 (덮어쓰기 + 신규)
//   3. StreamingAssets/Mods/*/items, recipes    ← 모드 JSON (덮어쓰기 + 신규)
public static class DataManager
{
    static bool _initialized;

    static readonly Dictionary<string, ItemData>    _items    = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, RecipeData>  _recipes  = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, ResearchData> _research = new(StringComparer.OrdinalIgnoreCase);

    // 에디터에서 Play 재시작 시 static 상태 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        _initialized = false;
        _items.Clear();
        _recipes.Clear();
        _research.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        LoadAll();
    }

    // ── 공개 API ──────────────────────────────────────────────────────────

    public static ItemData     GetItem(string name)     => _items.TryGetValue(name, out var v) ? v : null;
    public static RecipeData   GetRecipe(string name)   => _recipes.TryGetValue(name, out var v) ? v : null;
    public static ResearchData GetResearch(string name) => _research.TryGetValue(name, out var v) ? v : null;

    public static IEnumerable<RecipeData> GetRecipesBySkill(CraftSkill skill)
    {
        foreach (var r in _recipes.Values)
            if (r.RequiredSkillType == skill) yield return r;
    }

    public static IReadOnlyDictionary<string, ItemData>   AllItems   => _items;
    public static IReadOnlyDictionary<string, RecipeData> AllRecipes => _recipes;

    // ── 로딩 ──────────────────────────────────────────────────────────────

    static void LoadAll()
    {
        // 1) Resources 폴더의 기존 SO 에셋
        foreach (var item in Resources.LoadAll<ItemData>("Data/Items"))
            if (!string.IsNullOrEmpty(item.ItemName)) _items[item.ItemName] = item;

        foreach (var recipe in Resources.LoadAll<RecipeData>("Data/Recipes"))
            if (!string.IsNullOrEmpty(recipe.RecipeName)) _recipes[recipe.RecipeName] = recipe;

        foreach (var r in Resources.LoadAll<ResearchData>("Data/Research"))
            if (r != null) _research[r.name] = r;

        // 2) StreamingAssets JSON
        string dataRoot = Path.Combine(Application.streamingAssetsPath, "Data");
        if (Directory.Exists(dataRoot))
        {
            LoadJsonItems(   Path.Combine(dataRoot, "items"));
            LoadJsonRecipes( Path.Combine(dataRoot, "recipes"));
        }

        // 3) 모드
        string modsRoot = Path.Combine(Application.streamingAssetsPath, "Mods");
        if (Directory.Exists(modsRoot))
            foreach (var modDir in Directory.GetDirectories(modsRoot))
            {
                LoadJsonItems(   Path.Combine(modDir, "items"));
                LoadJsonRecipes( Path.Combine(modDir, "recipes"));
            }

        Debug.Log($"[DataManager] 로드 완료: 아이템 {_items.Count}개, 레시피 {_recipes.Count}개, 연구 {_research.Count}개");
    }

    // ── 아이템 JSON ────────────────────────────────────────────────────────

    static void LoadJsonItems(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                string json = File.ReadAllText(file);
                var hint = JsonUtility.FromJson<ItemTypeHint>(json);
                ItemData item = hint?.type?.ToLower() switch
                {
                    "weapon"   => BuildWeapon(  JsonUtility.FromJson<WeaponSchema>(json)),
                    "armor"    => BuildArmor(   JsonUtility.FromJson<ArmorSchema>(json)),
                    "resource" => BuildResource(JsonUtility.FromJson<ResourceItemSchema>(json)),
                    "food"     => BuildFood(    JsonUtility.FromJson<FoodSchema>(json)),
                    _          => BuildItem(    JsonUtility.FromJson<ItemSchema>(json)),
                };
                if (item != null && !string.IsNullOrEmpty(item.ItemName))
                    _items[item.ItemName] = item;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DataManager] 아이템 파싱 실패: {Path.GetFileName(file)}\n{e.Message}");
            }
        }
    }

    // ── 레시피 JSON ────────────────────────────────────────────────────────

    // 아이템 로드가 완료된 뒤 resolve해야 하므로 2단계로 처리
    static readonly List<(RecipeData recipe, RecipeSchema schema)> _pending = new();

    static void LoadJsonRecipes(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var schema = JsonUtility.FromJson<RecipeSchema>(File.ReadAllText(file));
                if (schema == null || string.IsNullOrEmpty(schema.recipeName)) continue;

                var recipe = ScriptableObject.CreateInstance<RecipeData>();
                recipe.RecipeName         = schema.recipeName;
                recipe.CraftTime          = schema.craftTime;
                recipe.ResultAmount       = schema.resultAmount;
                recipe.RequiredSkillLevel = schema.requiredSkillLevel;
                recipe.SkillGrowthAmount  = schema.skillGrowthAmount;
                recipe.Category           = JobCategory.Crafting;
                if (Enum.TryParse<CraftSkill>(schema.requiredSkillType, ignoreCase: true, out var skill))
                    recipe.RequiredSkillType = skill;

                _pending.Add((recipe, schema));
                _recipes[recipe.RecipeName] = recipe;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DataManager] 레시피 파싱 실패: {Path.GetFileName(file)}\n{e.Message}");
            }
        }
        ResolveRecipes();
    }

    static void ResolveRecipes()
    {
        foreach (var (recipe, schema) in _pending)
        {
            recipe.Result = GetItem(schema.resultItemName);
            if (recipe.Result == null && !string.IsNullOrEmpty(schema.resultItemName))
                Debug.LogWarning($"[DataManager] 레시피 '{recipe.RecipeName}': 결과 아이템 '{schema.resultItemName}' 없음");

            recipe.Ingredients.Clear();
            if (schema.ingredients != null)
                foreach (var ing in schema.ingredients)
                {
                    var item = GetItem(ing.itemName);
                    if (item != null)
                        recipe.Ingredients.Add(new RecipeData.Ingredient { Item = item, Amount = ing.amount });
                    else
                        Debug.LogWarning($"[DataManager] 레시피 '{recipe.RecipeName}': 재료 '{ing.itemName}' 없음");
                }

            if (!string.IsNullOrEmpty(schema.requiredResearchId))
                recipe.RequiredResearch = GetResearch(schema.requiredResearchId);
        }
        _pending.Clear();
    }

    // ── SO 생성 헬퍼 ──────────────────────────────────────────────────────

    static void ApplyBase(ItemData item, ItemSchema s)
    {
        item.ItemName    = s.itemName;
        item.Description = s.description;
        item.Weight      = s.weight;
        item.Width       = Mathf.Max(1, s.width);
        item.Height      = Mathf.Max(1, s.height);
        if (!string.IsNullOrEmpty(s.iconPath))
            item.Icon = Resources.Load<Sprite>(s.iconPath);
    }

    static ItemData BuildItem(ItemSchema s)
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        ApplyBase(item, s);
        return item;
    }

    static WeaponData BuildWeapon(WeaponSchema s)
    {
        var w = ScriptableObject.CreateInstance<WeaponData>();
        ApplyBase(w, s);
        if (Enum.TryParse<WeaponType>(    s.weaponType,     ignoreCase: true, out var wt)) w.Type           = wt;
        if (Enum.TryParse<DamageCategory>(s.damageCategory, ignoreCase: true, out var dc)) w.DamageCategory = dc;
        if (Enum.TryParse<PhysicalType>(  s.physicalType,   ignoreCase: true, out var pt)) w.PhysicalType   = pt;
        if (Enum.TryParse<DamageElement>( s.damageElement,  ignoreCase: true, out var de)) w.DamageElement  = de;
        w.IsLargeWeapon      = s.isLargeWeapon;
        w.IsBladed           = s.isBladed;
        w.IsRanged           = s.isRanged;
        w.BaseDamage         = s.baseDamage;
        w.BaseAttackInterval = s.baseAttackInterval;
        w.AttackRange        = s.attackRange;
        w.BlockBonus         = s.blockBonus;
        w.RangedBlockBonus   = s.rangedBlockBonus;
        w.EffectChance       = s.effectChance;
        return w;
    }

    static ArmorData BuildArmor(ArmorSchema s)
    {
        var a = ScriptableObject.CreateInstance<ArmorData>();
        ApplyBase(a, s);
        if (Enum.TryParse<ArmorSlot>(s.armorSlot, ignoreCase: true, out var slot)) a.Slot = slot;
        a.DamageReduction = s.damageReduction;
        return a;
    }

    static ResourceItemData BuildResource(ResourceItemSchema s)
    {
        var r = ScriptableObject.CreateInstance<ResourceItemData>();
        ApplyBase(r, s);
        if (Enum.TryParse<ResourceType>(s.resourceType, ignoreCase: true, out var rt)) r.ResourceType = rt;
        r.Stack = s.maxStack;
        return r;
    }

    static FoodData BuildFood(FoodSchema s)
    {
        var f = ScriptableObject.CreateInstance<FoodData>();
        ApplyBase(f, s);
        f.NutritionValue = s.nutritionValue;
        f.EatTime        = s.eatTime;
        return f;
    }
}
