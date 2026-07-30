using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// DungeonColony → Create Missing Data
//
// 손이 안 간 데이터(아이템 · 씨앗 · 자원 노드 · 레시피 · 연구 · 바이옴 매장)를 **없는 것만** 만든다.
// 여러 번 실행해도 안전하고, 이미 있는 에셋은 손대지 않는다 — 값을 손봤다면 그게 이긴다.
//
// 값은 전부 **임시 초안**이다. 밸런스를 잡으라고 만든 게 아니라, 시스템이 실제로 굴러가는 걸
// 눈으로 보려고 채우는 것이다. 숫자는 인스펙터에서 고치면 된다.
//
// 배치 규칙:
//   아이템 · 레시피 · 연구  → `Assets/Resources/Data/{Items,Recipes,Research}` (DataManager가 여기서 로드)
//   바이옴 콘텐츠          → `Assets/Resources/Data/Biomes` (BiomeContent가 여기서 로드)
//   자원 노드              → `Assets/5_Data/Resource` (Resources 밖 — 참조로만 쓰인다)
public static class DataScaffoldCreator
{
    const string ItemDir     = "Assets/Resources/Data/Items";
    const string RecipeDir   = "Assets/Resources/Data/Recipes";
    const string ResearchDir = "Assets/Resources/Data/Research";
    const string BiomeDir    = "Assets/Resources/Data/Biomes";
    const string NodeDir     = "Assets/5_Data/Resource";

    static readonly List<string> _created = new();
    static readonly List<string> _skipped = new();

    [MenuItem("DungeonColony/Create Missing Data")]
    static void Create()
    {
        _created.Clear();
        _skipped.Clear();

        EnsureFolder(ItemDir); EnsureFolder(RecipeDir);
        EnsureFolder(ResearchDir); EnsureFolder(BiomeDir); EnsureFolder(NodeDir);

        var res   = BuildResourceItems();
        var food  = BuildFoodAndCrops();
        BuildSeeds(food);
        var gear  = BuildGear(res);
        var nodes = BuildNodes(res);
        var rec   = BuildRecipes(res, food, gear);
        BuildResearch(rec, res);
        BuildBiomes(res, food, nodes);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var sb = new StringBuilder();
        sb.AppendLine($"[데이터] 신규 {_created.Count}개 / 기존 유지 {_skipped.Count}개");
        foreach (var p in _created) sb.AppendLine("  + " + p);
        sb.AppendLine("  ※ 값은 전부 임시 초안입니다. 밸런스는 인스펙터에서 고치세요.");
        Debug.Log(sb.ToString());
    }

    // ── 1. 자원 아이템 — ResourceType 하나당 하나 ────────────────────────────
    // 이미 그 타입을 쓰는 에셋이 있으면(Stone_item 등) **그걸 재사용**한다.
    // 같은 ResourceType이 둘이면 창고 집계가 어느 쪽을 세는지 흐려진다.
    static Dictionary<ResourceType, ResourceItemData> BuildResourceItems()
    {
        var byType = new Dictionary<ResourceType, ResourceItemData>();

        foreach (var guid in AssetDatabase.FindAssets("t:ResourceItemData"))
        {
            var a = AssetDatabase.LoadAssetAtPath<ResourceItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null && !byType.ContainsKey(a.ResourceType)) byType[a.ResourceType] = a;
        }

        foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
        {
            if (byType.ContainsKey(t)) { _skipped.Add($"{t} (기존 재사용)"); continue; }

            var item = Ensure<ResourceItemData>($"{ItemDir}/Res_{t}.asset", a =>
            {
                a.ResourceType = t;
                a.ItemName     = t.DisplayName();
                a.Weight       = WeightOf(t);
                a.BaseValue    = ValueOf(t);
                a.Stack        = t == ResourceType.Gold ? 9999 : 200;
                a.Description  = $"{t.DisplayName()}. 채집·제련으로 얻는다.";
            });
            byType[t] = item;
        }
        return byType;
    }

    static float WeightOf(ResourceType t) => t switch
    {
        ResourceType.Gold => 0.01f,
        ResourceType.Ruby or ResourceType.Sapphire or ResourceType.Topaz or ResourceType.Emerald
            or ResourceType.Etherstone or ResourceType.Voidstone or ResourceType.MagicStone => 0.3f,
        ResourceType.Bronze or ResourceType.Steel or ResourceType.SilverIngot or ResourceType.GoldIngot => 2f,
        ResourceType.Stone or ResourceType.CutStone => 1.5f,
        ResourceType.Wood or ResourceType.Lumber => 1f,
        _ => 1f,
    };

    static int ValueOf(ResourceType t) => t switch
    {
        ResourceType.Gold        => 1,
        ResourceType.Stone       => 2,
        ResourceType.Wood        => 3,
        ResourceType.CutStone    => 6,
        ResourceType.Lumber      => 8,
        ResourceType.Coal        => 10,
        ResourceType.CopperOre   => 12,
        ResourceType.TinOre      => 14,
        ResourceType.IronOre     => 20,
        ResourceType.SilverOre   => 35,
        ResourceType.GoldOre     => 55,
        ResourceType.MagicStone  => 70,
        ResourceType.Etherstone  => 90,
        ResourceType.Voidstone   => 90,
        ResourceType.Bronze      => 45,
        ResourceType.Steel       => 80,
        ResourceType.SilverIngot => 110,
        ResourceType.GoldIngot   => 180,
        _                        => 120,   // 보석류
    };

    // ── 2. 작물 / 음식 ───────────────────────────────────────────────────────
    class FoodSet
    {
        public ItemData Wheat, RawMeat, Fish;
        public FoodData Bread, CookedMeat, Berry, Mushroom;
    }

    static FoodSet BuildFoodAndCrops()
    {
        var s = new FoodSet();

        // 밀은 **먹는 게 아니라 재료**다 — 빵으로 구워야 배가 찬다.
        s.Wheat = Ensure<ItemData>($"{ItemDir}/Crop_Wheat.asset", a =>
        {
            a.ItemName = "밀"; a.Weight = 0.3f; a.BaseValue = 4;
            a.Description = "빻아 빵을 굽는 곡물. 그대로는 먹기 어렵다.";
        });

        s.RawMeat = Ensure<ItemData>($"{ItemDir}/Food_RawMeat.asset", a =>
        {
            a.ItemName = "생고기"; a.Weight = 0.8f; a.BaseValue = 12;
            a.Description = "익히지 않은 고기. 구워야 한다.";
        });

        s.Fish = Ensure<ItemData>($"{ItemDir}/Food_Fish.asset", a =>
        {
            a.ItemName = "생선"; a.Weight = 0.6f; a.BaseValue = 10;
            a.Description = "물가에서 얻는다. 구우면 더 낫다.";
        });

        s.Bread = Ensure<FoodData>($"{ItemDir}/Food_Bread.asset", a =>
        {
            a.ItemName = "빵"; a.Weight = 0.4f; a.BaseValue = 18;
            a.NutritionValue = 45f; a.EatTime = 2.5f;
            a.Description = "밀로 구운 주식. 오래 간다.";
        });

        s.CookedMeat = Ensure<FoodData>($"{ItemDir}/Food_CookedMeat.asset", a =>
        {
            a.ItemName = "구운 고기"; a.Weight = 0.7f; a.BaseValue = 26;
            a.NutritionValue = 60f; a.EatTime = 3f;
            a.Description = "잘 구운 고기. 허기를 크게 채운다.";
        });

        s.Berry = Ensure<FoodData>($"{ItemDir}/Food_Berry.asset", a =>
        {
            a.ItemName = "산딸기"; a.Weight = 0.2f; a.BaseValue = 6;
            a.NutritionValue = 18f; a.EatTime = 1.5f;
            a.Description = "숲에서 바로 따 먹을 수 있다. 조금 찬다.";
        });

        s.Mushroom = Ensure<FoodData>($"{ItemDir}/Food_Mushroom.asset", a =>
        {
            a.ItemName = "버섯"; a.Weight = 0.2f; a.BaseValue = 9;
            a.NutritionValue = 22f; a.EatTime = 2f;
            a.Description = "어두운 곳에서 자란다. 날로도 먹는다.";
        });

        return s;
    }

    // ── 3. 씨앗 — 심으면 무엇이 얼마나 나오는가 ──────────────────────────────
    static void BuildSeeds(FoodSet food)
    {
        Ensure<SeedData>($"{ItemDir}/Seed_Wheat.asset", a =>
        {
            a.ItemName = "밀 씨앗"; a.Weight = 0.05f; a.BaseValue = 5;
            a.HarvestItem = food.Wheat; a.HarvestAmount = 4;
            a.GrowthTime = 90f; a.MoistureDecay = 5f; a.WaterFillAmount = 100f;
            a.Description = "물을 자주 줘야 한다. 자라면 밀을 거둔다.";
        });

        Ensure<SeedData>($"{ItemDir}/Seed_Berry.asset", a =>
        {
            a.ItemName = "산딸기 씨앗"; a.Weight = 0.05f; a.BaseValue = 7;
            a.HarvestItem = food.Berry; a.HarvestAmount = 6;
            a.GrowthTime = 70f; a.MoistureDecay = 4f; a.WaterFillAmount = 100f;
            a.Description = "손이 덜 가고 자주 열린다.";
        });

        Ensure<SeedData>($"{ItemDir}/Seed_Mushroom.asset", a =>
        {
            a.ItemName = "버섯 포자"; a.Weight = 0.05f; a.BaseValue = 9;
            a.HarvestItem = food.Mushroom; a.HarvestAmount = 5;
            a.GrowthTime = 60f; a.MoistureDecay = 2f; a.WaterFillAmount = 100f;
            a.Description = "물이 덜 마른다 — 어두운 지하 농사에 맞는다.";
        });
    }

    // ── 4. 무기 / 방어구 ─────────────────────────────────────────────────────
    class GearSet
    {
        public WeaponData BronzeSword, IronSword, SteelSword, IronAxe, IronMace,
                          Spear, ShortBow, Crossbow, WoodShield, IronShield, FireStaff;
        public ArmorData  LeatherHead, LeatherChest, LeatherHands, LeatherLegs,
                          IronHead, IronChest, IronHands, IronLegs;
    }

    static GearSet BuildGear(Dictionary<ResourceType, ResourceItemData> res)
    {
        var g = new GearSet();

        g.BronzeSword = Weapon("Wpn_BronzeSword", "청동검",   WeaponType.OneHandSword, 12f, 1.9f, 1.6f, 2.5f, 90);
        g.IronSword   = Weapon("Wpn_IronSword",   "철검",     WeaponType.OneHandSword, 16f, 1.8f, 1.6f, 3f,  150);
        g.SteelSword  = Weapon("Wpn_SteelSword",  "강철 대검", WeaponType.TwoHandSword, 26f, 2.6f, 2.0f, 5f,  320, large: true);
        g.IronAxe     = Weapon("Wpn_IronAxe",     "철도끼",   WeaponType.OneHandAxe,   18f, 2.1f, 1.5f, 3.5f, 160);
        g.IronMace    = Weapon("Wpn_IronMace",    "철 철퇴",  WeaponType.OneHandMace,  17f, 2.2f, 1.4f, 3.5f, 150,
                               bladed: false, phys: PhysicalType.Blunt);
        g.Spear       = Weapon("Wpn_Spear",       "장창",     WeaponType.Polearm,      20f, 2.4f, 2.6f, 4f,  180,
                               large: true, bladed: false, phys: PhysicalType.Pierce);
        g.ShortBow    = Weapon("Wpn_ShortBow",    "단궁",     WeaponType.Bow,          14f, 2.2f, 12f,  2f,  140,
                               bladed: false, phys: PhysicalType.Pierce, ranged: true);
        g.Crossbow    = Weapon("Wpn_Crossbow",    "석궁",     WeaponType.Crossbow,     22f, 3.4f, 14f,  3f,  240,
                               bladed: false, phys: PhysicalType.Pierce, ranged: true);
        g.FireStaff   = Weapon("Wpn_FireStaff",   "화염 지팡이", WeaponType.ElementalFire, 15f, 2.6f, 9f, 2f, 300,
                               bladed: false, effectChance: 0.35f);

        g.WoodShield = Weapon("Wpn_WoodShield", "나무 방패", WeaponType.Shield, 2f, 2f, 1f, 1f, 40,
                              bladed: false, phys: PhysicalType.Blunt, block: 0.08f, rangedBlock: 0.05f);
        g.IronShield = Weapon("Wpn_IronShield", "철 방패",  WeaponType.Shield, 3f, 2f, 1f, 1.5f, 130,
                              bladed: false, phys: PhysicalType.Blunt, block: 0.15f, rangedBlock: 0.10f);

        g.LeatherHead  = Armor("Arm_LeatherHead",  "가죽 모자",  ArmorSlot.Head,  2f, 1.2f, 30);
        g.LeatherChest = Armor("Arm_LeatherChest", "가죽 갑옷",  ArmorSlot.Chest, 4f, 3f,   70);
        g.LeatherHands = Armor("Arm_LeatherHands", "가죽 장갑",  ArmorSlot.Hands, 1.5f, 0.8f, 22);
        g.LeatherLegs  = Armor("Arm_LeatherLegs",  "가죽 각반",  ArmorSlot.Legs,  2.5f, 1.6f, 40);

        g.IronHead  = Armor("Arm_IronHead",  "철 투구",   ArmorSlot.Head,  5f,  3f,   110);
        g.IronChest = Armor("Arm_IronChest", "철 갑옷",   ArmorSlot.Chest, 9f,  8f,   260);
        g.IronHands = Armor("Arm_IronHands", "철 건틀릿", ArmorSlot.Hands, 3.5f, 2f,   80);
        g.IronLegs  = Armor("Arm_IronLegs",  "철 각반",   ArmorSlot.Legs,  6f,  4.5f, 150);

        return g;
    }

    static WeaponData Weapon(string file, string name, WeaponType type,
                             float dmg, float interval, float range, float weight, int value,
                             bool large = false, bool bladed = true, bool ranged = false,
                             PhysicalType phys = PhysicalType.Slash,
                             float block = 0f, float rangedBlock = 0f, float effectChance = 0.2f)
        => Ensure<WeaponData>($"{ItemDir}/{file}.asset", a =>
        {
            a.ItemName = name; a.Weight = weight; a.BaseValue = value;
            a.Type = type; a.IsLargeWeapon = large; a.IsBladed = bladed; a.IsRanged = ranged;
            a.BaseDamage = dmg; a.BaseAttackInterval = interval; a.AttackRange = range;
            a.PhysicalType = phys; a.BlockBonus = block; a.RangedBlockBonus = rangedBlock;
            a.EffectChance = effectChance;
            a.Description = $"{name}.";
        });

    static ArmorData Armor(string file, string name, ArmorSlot slot, float reduction, float weight, int value)
        => Ensure<ArmorData>($"{ItemDir}/{file}.asset", a =>
        {
            a.ItemName = name; a.Weight = weight; a.BaseValue = value;
            a.Slot = slot; a.DamageReduction = reduction;
            a.Description = $"{name}.";
        });

    // ── 5. 자원 노드 — 필드맵에서 캐는 대상 ──────────────────────────────────
    class NodeSet
    {
        public ResourceNodeData Stone, Wood, IronVein, CopperVein, TinVein, CoalSeam,
                                SilverVein, GoldVein, MagicCrystal, EtherCrystal, VoidCrystal, GemVein;
    }

    static NodeSet BuildNodes(Dictionary<ResourceType, ResourceItemData> res)
    {
        var n = new NodeSet();

        // 기존 4종(Stone/Ore/Wood/Crystal)은 그대로 두고, 없는 것만 채운다.
        n.Stone = AssetDatabase.LoadAssetAtPath<ResourceNodeData>($"{NodeDir}/Stone.asset");
        n.Wood  = AssetDatabase.LoadAssetAtPath<ResourceNodeData>($"{NodeDir}/Wood.asset");

        n.IronVein     = Node("Node_IronVein",     ResourceType.IronOre,    res, 14f, 2, 4, 8f);
        n.CopperVein   = Node("Node_CopperVein",   ResourceType.CopperOre,  res, 11f, 2, 4, 4f);
        n.TinVein      = Node("Node_TinVein",      ResourceType.TinOre,     res, 11f, 2, 3, 4f);
        n.CoalSeam     = Node("Node_CoalSeam",     ResourceType.Coal,       res, 10f, 3, 5, 3f);
        n.SilverVein   = Node("Node_SilverVein",   ResourceType.SilverOre,  res, 18f, 1, 3, 14f);
        n.GoldVein     = Node("Node_GoldVein",     ResourceType.GoldOre,    res, 22f, 1, 2, 20f);
        n.MagicCrystal = Node("Node_MagicCrystal", ResourceType.MagicStone, res, 20f, 1, 2, 18f);
        n.EtherCrystal = Node("Node_EtherCrystal", ResourceType.Etherstone, res, 24f, 1, 2, 26f);
        n.VoidCrystal  = Node("Node_VoidCrystal",  ResourceType.Voidstone,  res, 24f, 1, 2, 26f);
        n.GemVein      = Node("Node_RubyVein",     ResourceType.Ruby,       res, 26f, 1, 2, 30f);

        return n;
    }

    static ResourceNodeData Node(string file, ResourceType type,
                                 Dictionary<ResourceType, ResourceItemData> res,
                                 float gauge, int min, int max, float skill)
        => Ensure<ResourceNodeData>($"{NodeDir}/{file}.asset", a =>
        {
            a.ResourceType  = type;
            a.DropItem      = res.TryGetValue(type, out var it) ? it : null;
            a.GaugeRequired = gauge;
            a.DropMin = min; a.DropMax = max;
            a.RequiredSkill = skill;
        });

    // ── 6. 레시피 — 무엇으로 무엇을 만드는가 ────────────────────────────────
    class RecipeSet
    {
        public RecipeData Bronze, Steel, Silver, GoldBar, CutStone, Lumber,
                          Bread, CookedMeat, IronSword, IronChest;
    }

    static RecipeSet BuildRecipes(Dictionary<ResourceType, ResourceItemData> res, FoodSet food, GearSet gear)
    {
        var r = new RecipeSet();
        ItemData R(ResourceType t) => res.TryGetValue(t, out var v) ? v : null;

        // 제련 — 원석 + 석탄 → 주괴
        r.Bronze = Recipe("Rec_Bronze", "청동 주괴", R(ResourceType.Bronze), 1, 8f, CraftSkill.Smelting, 2f,
                          (R(ResourceType.CopperOre), 3), (R(ResourceType.TinOre), 1), (R(ResourceType.Coal), 1));
        r.Steel = Recipe("Rec_Steel", "강철 주괴", R(ResourceType.Steel), 1, 12f, CraftSkill.Smelting, 12f,
                         (R(ResourceType.IronOre), 3), (R(ResourceType.Coal), 2));
        r.Silver = Recipe("Rec_SilverIngot", "은 주괴", R(ResourceType.SilverIngot), 1, 10f, CraftSkill.Smelting, 18f,
                          (R(ResourceType.SilverOre), 3), (R(ResourceType.Coal), 1));
        r.GoldBar = Recipe("Rec_GoldIngot", "금 주괴", R(ResourceType.GoldIngot), 1, 14f, CraftSkill.Smelting, 26f,
                           (R(ResourceType.GoldOre), 3), (R(ResourceType.Coal), 2));

        // 가공
        r.CutStone = Recipe("Rec_CutStone", "다듬은 돌", R(ResourceType.CutStone), 1, 5f, CraftSkill.Stonecutting, 4f,
                            (R(ResourceType.Stone), 2));
        r.Lumber = Recipe("Rec_Lumber", "판자", R(ResourceType.Lumber), 2, 4f, CraftSkill.Woodworking, 3f,
                          (R(ResourceType.Wood), 3));

        // 요리
        r.Bread = Recipe("Rec_Bread", "빵", food.Bread, 2, 6f, CraftSkill.Cooking, 3f,
                         (food.Wheat, 3));
        r.CookedMeat = Recipe("Rec_CookedMeat", "구운 고기", food.CookedMeat, 1, 5f, CraftSkill.Cooking, 6f,
                              (food.RawMeat, 1));

        // 장비
        r.IronSword = Recipe("Rec_IronSword", "철검", gear.IronSword, 1, 20f, CraftSkill.WeaponCrafting, 14f,
                             (R(ResourceType.IronOre), 4), (R(ResourceType.Lumber), 1));
        r.IronChest = Recipe("Rec_IronChest", "철 갑옷", gear.IronChest, 1, 26f, CraftSkill.ArmorCrafting, 18f,
                             (R(ResourceType.Steel), 3), (R(ResourceType.Lumber), 1));

        return r;
    }

    static RecipeData Recipe(string file, string name, ItemData result, int amount, float time,
                             CraftSkill skill, float skillLevel,
                             params (ItemData item, int amount)[] ingredients)
        => Ensure<RecipeData>($"{RecipeDir}/{file}.asset", a =>
        {
            a.RecipeName = name;
            a.Result = result; a.ResultAmount = amount; a.CraftTime = time;
            a.Category = JobCategory.Crafting;
            a.RequiredSkillType = skill; a.RequiredSkillLevel = skillLevel;
            a.SkillGrowthAmount = 0.5f;
            foreach (var (item, n) in ingredients)
                if (item != null) a.Ingredients.Add(new RecipeData.Ingredient { Item = item, Amount = n });
        });

    // ── 7. 연구 — 레시피 해금 ────────────────────────────────────────────────
    static void BuildResearch(RecipeSet rec, Dictionary<ResourceType, ResourceItemData> res)
    {
        ItemData R(ResourceType t) => res.TryGetValue(t, out var v) ? v : null;

        var smelting = Research("Res_Smelting", "제련술", "원석을 녹여 주괴로 만든다.", 40f,
                                new[] { rec.Bronze }, (R(ResourceType.Stone), 20), (R(ResourceType.Coal), 5));

        var steel = Research("Res_Steelmaking", "제강술", "더 단단한 강철을 뽑는다.", 70f,
                             new[] { rec.Steel, rec.IronChest }, (R(ResourceType.Bronze), 5), (R(ResourceType.Coal), 10));
        if (steel != null && steel.Prerequisite == null)
        {
            steel.Prerequisite = smelting;   // 이미 지정돼 있으면 그쪽이 이긴다
            EditorUtility.SetDirty(steel);
        }

        Research("Res_Stonecutting", "석공술", "돌을 다듬어 건축재로 쓴다.", 30f,
                 new[] { rec.CutStone }, (R(ResourceType.Stone), 15));

        Research("Res_Woodworking", "목공술", "통나무를 켜서 판자를 만든다.", 30f,
                 new[] { rec.Lumber }, (R(ResourceType.Wood), 15));

        Research("Res_Cooking", "조리법", "곡물과 고기를 제대로 익힌다.", 25f,
                 new[] { rec.Bread, rec.CookedMeat }, (R(ResourceType.Wood), 10));

        Research("Res_WeaponSmithing", "무기 제작", "쓸 만한 무기를 벼린다.", 60f,
                 new[] { rec.IronSword }, (R(ResourceType.IronOre), 10));
    }

    static ResearchData Research(string file, string name, string desc, float time,
                                 RecipeData[] unlocks, params (ItemData item, int amount)[] cost)
        => Ensure<ResearchData>($"{ResearchDir}/{file}.asset", a =>
        {
            a.ResearchName = name; a.Description = desc;
            a.ResearchTime = time; a.SkillGrowthAmount = 1f;
            foreach (var (item, n) in cost)
                if (item != null) a.Ingredients.Add(new ResearchData.Ingredient { Item = item, Amount = n });
            foreach (var r in unlocks)
                if (r != null) a.UnlockedRecipes.Add(r);
        });

    // ── 8. 바이옴 — 매장 자원 + 자원 노드 풀 ────────────────────────────────
    // **여기가 "어디에 가야 무엇이 있나"의 단일 출처다.** 바이옴마다 성격이 갈리도록 배분했다.
    static void BuildBiomes(Dictionary<ResourceType, ResourceItemData> res, FoodSet food, NodeSet nodes)
    {
        ItemData R(ResourceType t) => res.TryGetValue(t, out var v) ? v : null;

        // 평원 — 흔한 것 위주. 시작 지역이라 막히지 않게.
        FillBiome(Biome.Plains,
              new[] { (R(ResourceType.Stone), 1f, 40, 90), (R(ResourceType.Wood), 0.8f, 30, 70),
                      (R(ResourceType.CopperOre), 0.4f, 15, 35) },
              new[] { nodes.Stone, nodes.CopperVein });

        // 숲 — 목재가 압도적. 먹을 것도 난다.
        FillBiome(Biome.Forest,
              new[] { (R(ResourceType.Wood), 1f, 80, 160), (R(ResourceType.Stone), 0.5f, 20, 45),
                      ((ItemData)food.Berry, 0.6f, 20, 50) },
              new[] { nodes.Wood, nodes.Stone });

        // 사막 — 돌과 구리/주석. 나무는 거의 없다.
        FillBiome(Biome.Desert,
              new[] { (R(ResourceType.Stone), 1f, 60, 120), (R(ResourceType.TinOre), 0.6f, 25, 55),
                      (R(ResourceType.GoldOre), 0.25f, 10, 25) },
              new[] { nodes.Stone, nodes.TinVein, nodes.GoldVein });

        // 설원 — 철과 은. 척박하다.
        FillBiome(Biome.Tundra,
              new[] { (R(ResourceType.Stone), 0.8f, 30, 70), (R(ResourceType.IronOre), 0.6f, 25, 60),
                      (R(ResourceType.SilverOre), 0.3f, 10, 28) },
              new[] { nodes.IronVein, nodes.SilverVein });

        // 용암지대 — 석탄·철, 그리고 화염 계열 촉매. 위험한 만큼 값나간다.
        FillBiome(Biome.Lava,
              new[] { (R(ResourceType.Coal), 1f, 60, 130), (R(ResourceType.IronOre), 0.7f, 30, 70),
                      (R(ResourceType.Ruby), 0.3f, 6, 16), (R(ResourceType.MagicStone), 0.35f, 8, 20) },
              new[] { nodes.CoalSeam, nodes.IronVein, nodes.GemVein, nodes.MagicCrystal });

        // 동굴 — 광물의 중심. 마법 촉매도 여기.
        FillBiome(Biome.Cavern,
              new[] { (R(ResourceType.Stone), 1f, 80, 160), (R(ResourceType.IronOre), 0.8f, 40, 90),
                      (R(ResourceType.SilverOre), 0.4f, 15, 40), (R(ResourceType.Voidstone), 0.25f, 5, 14),
                      ((ItemData)food.Mushroom, 0.5f, 15, 35) },
              new[] { nodes.Stone, nodes.IronVein, nodes.SilverVein, nodes.VoidCrystal });

        // 늪 — 자원은 빈약하고 대신 신성 촉매가 드물게. 목재는 젖어 적다.
        FillBiome(Biome.Swamp,
              new[] { (R(ResourceType.Wood), 0.6f, 20, 45), (R(ResourceType.Coal), 0.5f, 20, 45),
                      (R(ResourceType.Etherstone), 0.2f, 4, 10),
                      ((ItemData)food.Mushroom, 0.7f, 20, 45) },
              new[] { nodes.Wood, nodes.CoalSeam, nodes.EtherCrystal });
    }

    static void FillBiome(Biome biome, (ItemData item, float chance, int min, int max)[] deposits,
                      ResourceNodeData[] nodes)
    {
        var path = $"{BiomeDir}/Biome_{biome}.asset";
        var data = AssetDatabase.LoadAssetAtPath<BiomeContentData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BiomeContentData>();
            data.Biome = biome;
            AssetDatabase.CreateAsset(data, path);
            _created.Add(path);
        }

        // **이미 채워 뒀으면 손대지 않는다** — 직접 조정한 값이 초안에 밀리면 안 된다.
        if (data.Deposits.Count == 0)
        {
            foreach (var (item, chance, min, max) in deposits)
            {
                if (item == null) continue;
                data.Deposits.Add(new BiomeContentData.DepositEntry
                {
                    Item = item, Chance = chance, MinAmount = min, MaxAmount = max
                });
            }
            _created.Add($"{path} ← 매장 {data.Deposits.Count}종");
        }

        if (data.ResourceNodes.Count == 0)
        {
            foreach (var n in nodes) if (n != null) data.ResourceNodes.Add(n);
            _created.Add($"{path} ← 자원 노드 {data.ResourceNodes.Count}종");
        }

        EditorUtility.SetDirty(data);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────

    static T Ensure<T>(string path, System.Action<T> init) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) { _skipped.Add(path); return existing; }

        var so = ScriptableObject.CreateInstance<T>();
        init?.Invoke(so);
        AssetDatabase.CreateAsset(so, path);
        _created.Add(path);
        return so;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parts = path.Split('/');
        var cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
