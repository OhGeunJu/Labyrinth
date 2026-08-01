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
//
// 2·3의 **그림·모델은 그 폴더 안에서 찾는다**(`ModAssets`) — 그래서 폴더를 통째로 넣고 빼면
// 콘텐츠가 통째로 들어오고 나간다. 어느 폴더에서 왔는지(`contentRoot`)를 로딩 내내 들고 다니는
// 이유가 이것이다. 빌드에 들어 있는 것만 가리키던 `Resources.Load`는 마지막 폴백으로 남았다.
public static class DataManager
{
    static bool _initialized;

    // ── 식별자는 `Id`(비면 에셋 파일명) ───────────────────────────────────
    // 표시명을 키로 쓰던 시절의 부채를 갚는 중이다. 표시명은 번역 대상이고 모드끼리
    // 겹치면 조용히 덮어쓴다 — 자세한 근거는 `DataId`.
    static readonly Dictionary<string, ItemData>     _items    = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, RecipeData>   _recipes  = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, ResearchData> _research = new(StringComparer.OrdinalIgnoreCase);

    // 표시명 → 데이터. **레거시 조회용 보조 색인**이다.
    // 기존 JSON과 저작물이 `"itemName": "철검"`처럼 표시명으로 가리키고 있어서,
    // 이걸 없애면 그 전부가 한꺼번에 끊긴다. 조회는 **Id 먼저, 없으면 표시명** 순으로 하고
    // 표시명으로 찾혔을 때 한 번만 경고한다(언젠가 지울 길을 보이게 남겨 둔다).
    static readonly Dictionary<string, ItemData>   _itemsByName   = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, RecipeData> _recipesByName = new(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> _warnedLegacyKeys = new(StringComparer.OrdinalIgnoreCase);

    // 건설 가능한 것들. **정렬된 리스트로 들고 있다** — 건설 메뉴의 순서가 곧 이 순서라
    // Dictionary의 임의 순서를 그대로 쓰면 실행마다 메뉴가 뒤바뀐다.
    static readonly Dictionary<string, BuildingData> _buildings = new(StringComparer.OrdinalIgnoreCase);
    static readonly List<BuildingData>               _buildingOrder = new();

    // 종족. `UnitData`로 담으므로 `MinionData`·`NpcData`까지 한 번에 들어온다
    // (`Resources.LoadAll<UnitData>`가 파생 타입을 함께 준다).
    static readonly Dictionary<string, UnitData> _units = new(StringComparer.OrdinalIgnoreCase);

    // 소환 목록도 순서가 곧 메뉴 순서라 정렬된 리스트로 따로 둔다(건설 목록과 같은 이유).
    static readonly List<MinionData> _minionOrder = new();

    // 에디터에서 Play 재시작 시 static 상태 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        _initialized = false;
        _items.Clear();
        _recipes.Clear();
        _research.Clear();
        _buildings.Clear();
        _buildingOrder.Clear();
        _units.Clear();
        _minionOrder.Clear();
        _itemsByName.Clear();
        _recipesByName.Clear();
        _warnedLegacyKeys.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        LoadAll();
    }

    // ── 공개 API ──────────────────────────────────────────────────────────

    // 조회는 **Id 먼저, 없으면 표시명**. 표시명으로 찾히면 한 번만 경고한다 —
    // 그 참조는 번역하는 순간 깨질 것이므로 미리 고칠 기회를 준다.
    public static ItemData GetItem(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (_items.TryGetValue(key, out var v)) return v;
        if (_itemsByName.TryGetValue(key, out v)) { WarnLegacy("아이템", key, v.Id, v.name); return v; }
        return null;
    }

    public static RecipeData GetRecipe(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (_recipes.TryGetValue(key, out var v)) return v;
        if (_recipesByName.TryGetValue(key, out v)) { WarnLegacy("레시피", key, v.Id, v.name); return v; }
        return null;
    }

    public static ResearchData GetResearch(string key) =>
        !string.IsNullOrEmpty(key) && _research.TryGetValue(key, out var v) ? v : null;

    static void WarnLegacy(string kind, string key, string id, string assetName)
    {
        if (!_warnedLegacyKeys.Add(key)) return;
        Debug.LogWarning($"[DataManager] {kind} '{key}'을(를) **표시명**으로 찾았습니다. " +
                         $"표시명은 번역 대상이라 곧 끊깁니다 — 식별자 '{DataId.Resolve(null, id) ?? assetName}'로 바꾸세요.");
    }

    public static IEnumerable<RecipeData> GetRecipesBySkill(CraftSkill skill)
    {
        foreach (var r in _recipes.Values)
            if (r.RequiredSkillType == skill) yield return r;
    }

    // ResourceType → 해당 ResourceItemData (건설 환불 등). 없으면 null.
    public static ResourceItemData GetResourceItem(ResourceType type)
    {
        foreach (var v in _items.Values)
            if (v is ResourceItemData r && r.ResourceType == type) return r;
        return null;
    }

    public static IReadOnlyDictionary<string, ItemData>     AllItems    => _items;
    public static IReadOnlyDictionary<string, RecipeData>   AllRecipes  => _recipes;
    public static IReadOnlyDictionary<string, ResearchData> AllResearch => _research;

    // 건설 목록. **이 순서 그대로 메뉴에 뜬다**(Category → SortOrder → 이름).
    public static IReadOnlyList<BuildingData> AllBuildings => _buildingOrder;

    public static BuildingData GetBuilding(string name) =>
        _buildings.TryGetValue(name, out var v) ? v : null;

    // 종족 — 방랑자·하수인·NPC 전부. 적/NPC 스폰을 데이터로 옮길 때도 이 색인을 쓴다.
    public static IReadOnlyDictionary<string, UnitData> AllUnits => _units;

    public static UnitData GetUnit(string key) =>
        !string.IsNullOrEmpty(key) && _units.TryGetValue(key, out var v) ? v : null;

    // 소환 목록. **이 순서 그대로 던전 핵 메뉴에 뜬다**(SortOrder → 이름).
    public static IReadOnlyList<MinionData> AllMinions => _minionOrder;

    // ── 로딩 ──────────────────────────────────────────────────────────────

    static void LoadAll()
    {
        // 1) Resources 폴더의 SO 에셋 — 키는 `Id`(비면 에셋 파일명)
        foreach (var item in Resources.LoadAll<ItemData>("Data/Items"))
            RegisterItem(item);

        foreach (var recipe in Resources.LoadAll<RecipeData>("Data/Recipes"))
            RegisterRecipe(recipe);

        foreach (var r in Resources.LoadAll<ResearchData>("Data/Research"))
            if (r != null) _research[DataId.Resolve(r, r.Id)] = r;

        foreach (var b in Resources.LoadAll<BuildingData>("Data/Buildings"))
            if (b != null) _buildings[DataId.Resolve(b, b.Id)] = b;

        foreach (var u in Resources.LoadAll<UnitData>("Data/Units"))
            RegisterUnit(u);

        SortBuildings();
        SortMinions();

        // 2) StreamingAssets JSON
        string dataRoot = Path.Combine(Application.streamingAssetsPath, "Data");
        if (Directory.Exists(dataRoot))
        {
            LoadJsonItems(   Path.Combine(dataRoot, "items"), dataRoot);
            LoadJsonRecipes( Path.Combine(dataRoot, "recipes"));
        }

        // 3) 모드 — 폴더 하나가 모드 하나다. **그림·모델도 그 폴더에서 찾는다**(두 번째 인자).
        string modsRoot = Path.Combine(Application.streamingAssetsPath, "Mods");
        if (Directory.Exists(modsRoot))
            foreach (var modDir in Directory.GetDirectories(modsRoot))
            {
                LoadJsonItems(   Path.Combine(modDir, "items"), modDir);
                LoadJsonRecipes( Path.Combine(modDir, "recipes"));
            }

        Debug.Log($"[DataManager] 로드 완료: 아이템 {_items.Count}개, 레시피 {_recipes.Count}개, " +
                  $"연구 {_research.Count}개, 건물 {_buildings.Count}개, " +
                  $"종족 {_units.Count}개(소환 가능 {_minionOrder.Count}개)");
    }

    // ── 등록 ──────────────────────────────────────────────────────────────
    // Id로 넣고 표시명 색인에도 함께 올린다. **Id 충돌은 시끄럽게 알린다** —
    // 조용히 덮어쓰면 모드 둘이 겹쳤을 때 무엇이 사라졌는지 알 길이 없다.
    static void RegisterItem(ItemData item)
    {
        if (item == null) return;
        string id = DataId.Resolve(item, item.Id);
        if (string.IsNullOrEmpty(id)) return;

        if (_items.TryGetValue(id, out var prev) && prev != item)
            Debug.LogWarning($"[DataManager] 아이템 Id 충돌 '{id}' — '{prev.name}'을(를) '{item.name}'이(가) 덮어씁니다.");

        _items[id] = item;
        if (!string.IsNullOrEmpty(item.ItemName)) _itemsByName[item.ItemName] = item;
    }

    static void RegisterRecipe(RecipeData recipe)
    {
        if (recipe == null) return;
        string id = DataId.Resolve(recipe, recipe.Id);
        if (string.IsNullOrEmpty(id)) return;

        if (_recipes.TryGetValue(id, out var prev) && prev != recipe)
            Debug.LogWarning($"[DataManager] 레시피 Id 충돌 '{id}' — '{prev.name}'을(를) '{recipe.name}'이(가) 덮어씁니다.");

        _recipes[id] = recipe;
        if (!string.IsNullOrEmpty(recipe.RecipeName)) _recipesByName[recipe.RecipeName] = recipe;
    }

    static void RegisterUnit(UnitData unit)
    {
        if (unit == null) return;
        string id = DataId.Resolve(unit, unit.Id);
        if (string.IsNullOrEmpty(id)) return;

        if (_units.TryGetValue(id, out var prev) && prev != unit)
            Debug.LogWarning($"[DataManager] 종족 Id 충돌 '{id}' — '{prev.name}'을(를) '{unit.name}'이(가) 덮어씁니다.");

        _units[id] = unit;
        if (unit is MinionData m && !_minionOrder.Contains(m)) _minionOrder.Add(m);
    }

    // 소환 메뉴 순서 = 저작한 SortOrder → 이름. 건설 목록과 같은 이유로 여기서 못 박는다.
    static void SortMinions()
    {
        _minionOrder.Sort((a, b) =>
        {
            int c = a.SortOrder.CompareTo(b.SortOrder);
            if (c != 0) return c;
            return string.Compare(a.RaceName, b.RaceName, StringComparison.OrdinalIgnoreCase);
        });
    }

    // 건설 메뉴 순서 = 탭(Category) → 저작한 SortOrder → 이름.
    // `Resources.LoadAll`은 순서를 보장하지 않으므로 여기서 못 박지 않으면
    // 실행할 때마다 메뉴 배치가 달라진다.
    static void SortBuildings()
    {
        _buildingOrder.Clear();
        _buildingOrder.AddRange(_buildings.Values);
        _buildingOrder.Sort((a, b) =>
        {
            int c = a.Category.CompareTo(b.Category);
            if (c != 0) return c;
            c = a.SortOrder.CompareTo(b.SortOrder);
            if (c != 0) return c;
            return string.Compare(a.BuildingName, b.BuildingName, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── 아이템 JSON ────────────────────────────────────────────────────────

    // `contentRoot` = 이 JSON이 속한 콘텐츠 폴더(모드 폴더 또는 StreamingAssets/Data).
    // 그림·모델 경로를 여기 기준으로 푼다 — 없으면 Resources만 본다(예전 동작).
    static void LoadJsonItems(string folder, string contentRoot)
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
                    "weapon"   => BuildWeapon(  JsonUtility.FromJson<WeaponSchema>(json),       contentRoot),
                    "armor"    => BuildArmor(   JsonUtility.FromJson<ArmorSchema>(json),        contentRoot),
                    "resource" => BuildResource(JsonUtility.FromJson<ResourceItemSchema>(json), contentRoot),
                    "food"     => BuildFood(    JsonUtility.FromJson<FoodSchema>(json),         contentRoot),
                    _          => BuildItem(    JsonUtility.FromJson<ItemSchema>(json),         contentRoot),
                };
                RegisterItem(item);   // Id 색인 + 표시명 보조 색인 + 충돌 경고
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
                // JSON 레시피도 `.name`이 비어 있어 파일명 폴백이 안 통한다 — Id를 못 박는다.
                recipe.Id                 = !string.IsNullOrWhiteSpace(schema.id)
                                          ? schema.id.Trim() : schema.recipeName;
                recipe.name               = recipe.Id;
                recipe.RecipeName         = schema.recipeName;
                recipe.CraftTime          = schema.craftTime;
                recipe.ResultAmount       = schema.resultAmount;
                recipe.RequiredSkillLevel = schema.requiredSkillLevel;
                recipe.SkillGrowthAmount  = schema.skillGrowthAmount;
                recipe.Category           = JobCategory.Crafting;
                if (Enum.TryParse<CraftSkill>(schema.requiredSkillType, ignoreCase: true, out var skill))
                    recipe.RequiredSkillType = skill;

                _pending.Add((recipe, schema));
                RegisterRecipe(recipe);   // Id 색인 + 표시명 보조 색인 + 충돌 경고
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
            // `resultItemId`/`itemId`가 있으면 그걸, 없으면 예전처럼 표시명을 쓴다.
            string resultKey = schema.ResultKey;
            recipe.Result = GetItem(resultKey);
            if (recipe.Result == null && !string.IsNullOrEmpty(resultKey))
                Debug.LogWarning($"[DataManager] 레시피 '{recipe.RecipeName}': 결과 아이템 '{resultKey}' 없음");

            recipe.Ingredients.Clear();
            if (schema.ingredients != null)
                foreach (var ing in schema.ingredients)
                {
                    string key  = ing.Key;
                    var    item = GetItem(key);
                    if (item != null)
                        recipe.Ingredients.Add(new RecipeData.Ingredient { Item = item, Amount = ing.amount });
                    else
                        Debug.LogWarning($"[DataManager] 레시피 '{recipe.RecipeName}': 재료 '{key}' 없음");
                }

            if (!string.IsNullOrEmpty(schema.requiredResearchId))
                recipe.RequiredResearch = GetResearch(schema.requiredResearchId);
        }
        _pending.Clear();
    }

    // ── SO 생성 헬퍼 ──────────────────────────────────────────────────────

    static void ApplyBase(ItemData item, ItemSchema s, string root)
    {
        // JSON 아이템은 에셋 파일이 아니라 `.name`이 비어 있다 —
        // `DataId.Resolve`의 파일명 폴백이 통하지 않으므로 여기서 Id를 못 박는다.
        // `id`가 없으면 예전처럼 표시명이 식별자가 된다(기존 콘텐츠 호환).
        item.Id          = !string.IsNullOrWhiteSpace(s.id) ? s.id.Trim() : s.itemName;
        item.name        = item.Id;   // 로그·인스펙터에서 알아볼 수 있게
        item.ItemName    = s.itemName;
        item.Description = s.description;
        item.Weight      = s.weight;
        item.Width       = Mathf.Max(1, s.width);
        item.Height      = Mathf.Max(1, s.height);

        // 그림·모델은 **이 콘텐츠가 온 폴더 안에서** 먼저 찾는다(`ModAssets`).
        // 그래야 모드가 자기 PNG를 들고 올 수 있다 — 예전엔 `Resources.Load`뿐이라
        // 빌드에 이미 들어 있는 것만 가리킬 수 있었다.
        if (!string.IsNullOrEmpty(s.iconPath))
            item.Icon = ModAssets.LoadSprite(s.iconPath, root);
        if (!string.IsNullOrEmpty(s.modelPath))
            item.WorldModel = ModAssets.LoadPrefab(s.modelPath, root);
    }

    static ItemData BuildItem(ItemSchema s, string root)
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        ApplyBase(item, s, root);
        return item;
    }

    static WeaponData BuildWeapon(WeaponSchema s, string root)
    {
        var w = ScriptableObject.CreateInstance<WeaponData>();
        ApplyBase(w, s, root);
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

    static ArmorData BuildArmor(ArmorSchema s, string root)
    {
        var a = ScriptableObject.CreateInstance<ArmorData>();
        ApplyBase(a, s, root);
        if (Enum.TryParse<ArmorSlot>(s.armorSlot, ignoreCase: true, out var slot)) a.Slot = slot;
        a.DamageReduction = s.damageReduction;
        return a;
    }

    static ResourceItemData BuildResource(ResourceItemSchema s, string root)
    {
        var r = ScriptableObject.CreateInstance<ResourceItemData>();
        ApplyBase(r, s, root);
        if (Enum.TryParse<ResourceType>(s.resourceType, ignoreCase: true, out var rt)) r.ResourceType = rt;
        r.Stack = s.maxStack;
        return r;
    }

    static FoodData BuildFood(FoodSchema s, string root)
    {
        var f = ScriptableObject.CreateInstance<FoodData>();
        ApplyBase(f, s, root);
        f.NutritionValue = s.nutritionValue;
        f.EatTime        = s.eatTime;
        return f;
    }
}
