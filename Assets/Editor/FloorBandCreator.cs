using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **망자 · 교단을 층마다 가른다 — 프리팹이 아니라 데이터로**(2026-11-11, 사용자: *"데이터 중심으로 가자, 생김새는 나중에"*).
//
// 같은 프리팹(`Enemy_Skeleton` · `Enemy_SkeletonArcher` · `Enemy_CultAcolyte` · `Enemy_CultMagus`)을 쓰고
// 층마다 **등급 · 인원 · 장비**만 다른 무리 에셋을 짓는다(`Band_Undead_F1~F4` · `Band_Cult_F2~F4`). 장비는
// 구성원 칸의 `Loadout`이 프리팹 것을 덮는다(`WorldBandData.MemberEntry.Loadout` → `PawnSpec.LoadoutOverride`).
// 금속은 층 사다리 그대로다(청동 1 · 강철 2 · 티타늄 3 · 미스릴 4 — `docs/design_metals.md`).
//
// **어디에 나오나는 이미 사용자가 저작해 둔 자리를 따른다** — 바이옴 표(`WildSpawns`)에서 옛 무리(`Band_Undead` ·
// `Band_Cult`)를 가리키는 줄만 그 바이옴 층(`BiomeContentData.Tier`)의 무리로 갈아 끼운다. 확률 · 다시 나는 날은 그대로.
// 옛 무리 에셋은 지우지 않는다 — 습격 이벤트 · 디버그 창이 아직 그것을 쓴다.
//
// 다시 돌려도 된다 — 무리 · 장비는 값만 다시 적고(guid를 지킨다), 이미 갈아 끼운 바이옴은 건너뛴다.
public static class FloorBandCreator
{
    const string BandDir    = "Assets/5_Data/World/Band";
    const string LoadoutDir = "Assets/5_Data/Loadout";
    const string BiomeDir   = "Assets/Resources/Data/Biomes";

    // 프리팹 이름 → 장비 열쇠.
    static readonly Dictionary<string, string> RoleOf = new()
    {
        ["Enemy_Skeleton"]       = "Skeleton",
        ["Enemy_SkeletonArcher"] = "SkeletonArcher",
        ["Enemy_CultAcolyte"]    = "CultAcolyte",
        ["Enemy_CultMagus"]      = "CultMagus",
    };

    // 층마다 차는 것(아이템, 확률). 주머니 · 동전은 프리팹의 원래 장비에서 그대로 옮긴다.
    static readonly Dictionary<string, (string Id, float Chance)[][]> Gear = new()
    {
        ["Skeleton"] = new[]
        {
            new[] { ("Wpn_BronzeSword", 0.6f),    ("Wpn_WoodShield", 0.4f),  ("Arm_LeatherHead", 0.2f) },
            new[] { ("Wpn_SteelSword", 0.6f),     ("Wpn_WoodShield", 0.5f),  ("arm_bronze_head", 0.3f) },
            new[] { ("wpn_titanium_sword", 0.6f), ("Wpn_SteelShield", 0.5f), ("arm_steel_head", 0.4f),    ("arm_steel_chest", 0.2f) },
            new[] { ("wpn_mithril_sword", 0.6f),  ("Wpn_SteelShield", 0.6f), ("arm_titanium_head", 0.5f), ("arm_titanium_chest", 0.3f) },
        },
        ["SkeletonArcher"] = new[]
        {
            new[] { ("Wpn_ShortBow", 0.7f) },
            new[] { ("wpn_longbow", 0.7f) },
            new[] { ("wpn_composite_bow", 0.7f), ("arm_bronze_head", 0.3f) },
            new[] { ("wpn_composite_bow", 0.8f), ("arm_steel_head", 0.4f) },
        },
        // 교단은 2층부터 산다 — 1층 칸은 2층과 같게 둔다(안 쓴다).
        ["CultAcolyte"] = new[]
        {
            new[] { ("wpn_steel_dagger", 0.6f),   ("arm_cloth_chest", 0.7f) },
            new[] { ("wpn_steel_dagger", 0.6f),   ("arm_cloth_chest", 0.7f) },
            new[] { ("wpn_steel_dagger", 0.7f),   ("arm_hardleather_chest", 0.5f) },
            new[] { ("wpn_mithril_dagger", 0.6f), ("arm_hardleather_chest", 0.6f) },
        },
        ["CultMagus"] = new[]
        {
            new[] { ("wpn_dark_staff", 0.3f), ("wpn_fire_staff", 0.25f), ("wpn_water_staff", 0.2f), ("arm_cloth_chest", 0.6f) },
            new[] { ("wpn_dark_staff", 0.3f), ("wpn_fire_staff", 0.25f), ("wpn_water_staff", 0.2f), ("arm_cloth_chest", 0.6f) },
            new[] { ("wpn_dark_staff", 0.4f), ("wpn_fire_staff", 0.3f),  ("wpn_arcane_staff", 0.15f), ("arm_cloth_chest", 0.7f) },
            new[] { ("wpn_crystal_staff", 0.3f), ("wpn_dark_staff", 0.4f), ("wpn_arcane_staff", 0.2f), ("arm_hardleather_chest", 0.5f) },
        },
    };

    // 층마다 (인원 최소, 최대, 등급 최소, 최대). 등급은 `StatGrade` 순번(0 F- · 3 E- · 6 D- · 9 C- · 12 B- · 15 A-).
    static readonly Dictionary<string, (int Min, int Max, StatGrade Lo, StatGrade Hi)[]> Ranks = new()
    {
        ["Skeleton"] = new[]
        {
            (2, 4, (StatGrade)0, (StatGrade)3),  (2, 5, (StatGrade)3, (StatGrade)6),
            (3, 5, (StatGrade)6, (StatGrade)9),  (3, 6, (StatGrade)9, (StatGrade)12),
        },
        ["SkeletonArcher"] = new[]
        {
            (0, 1, (StatGrade)0, (StatGrade)3),  (1, 2, (StatGrade)3, (StatGrade)6),
            (1, 2, (StatGrade)6, (StatGrade)9),  (2, 3, (StatGrade)9, (StatGrade)12),
        },
        ["CultAcolyte"] = new[]
        {
            (2, 4, (StatGrade)3, (StatGrade)7),  (2, 4, (StatGrade)3, (StatGrade)7),
            (2, 4, (StatGrade)6, (StatGrade)10), (3, 5, (StatGrade)9, (StatGrade)13),
        },
        ["CultMagus"] = new[]
        {
            (0, 1, (StatGrade)7, (StatGrade)10),  (0, 1, (StatGrade)7, (StatGrade)10),
            (1, 1, (StatGrade)10, (StatGrade)13), (1, 2, (StatGrade)13, (StatGrade)16),
        },
    };

    static readonly string[] FloorName = { "", "1층", "2층", "3층", "4층" };

    // 장비 몫의 전력 보정(청동 · 강철 · 티타늄 · 미스릴).
    static readonly float[] GearPower = { 1f, 1f, 1.15f, 1.3f, 1.45f };

    [MenuItem("DungeonColony/Pawn/15. Create Floor Bands (망자 · 교단 층마다 — 등급 · 인원 · 장비)")]
    public static void Create()
    {
        var undead = AssetDatabase.LoadAssetAtPath<WorldBandData>($"{BandDir}/Band_Undead.asset");
        var cult   = AssetDatabase.LoadAssetAtPath<WorldBandData>($"{BandDir}/Band_Cult.asset");
        if (undead == null || cult == null) { Debug.LogError("[층 무리] Band_Undead · Band_Cult가 없다."); return; }

        var made = new Dictionary<(WorldBandData Base, int Floor), WorldBandData>();
        for (int f = 1; f <= 4; f++) made[(undead, f)] = Ensure(undead, "Undead", f);
        for (int f = 2; f <= 4; f++) made[(cult, f)]   = Ensure(cult, "Cult", f);

        int swapped = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:BiomeContentData", new[] { BiomeDir }))
        {
            var biome = AssetDatabase.LoadAssetAtPath<BiomeContentData>(AssetDatabase.GUIDToAssetPath(guid));
            if (biome == null) continue;

            var so   = new SerializedObject(biome);
            var list = so.FindProperty("WildSpawns");
            bool dirty = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                var p    = list.GetArrayElementAtIndex(i).FindPropertyRelative("Band");
                var band = p.objectReferenceValue as WorldBandData;
                if (band != undead && band != cult) continue;

                // 교단은 2층부터 — 1층 바이옴에 교단이 적혀 있으면 2층 무리를 쓴다.
                int floor = Mathf.Clamp(biome.Tier, band == cult ? 2 : 1, 4);
                var to = made[(band, floor)];
                p.objectReferenceValue = to;
                dirty = true;
                swapped++;
                Debug.Log($"[층 무리] {biome.name}: {band.name} → {to.name}");
            }
            if (!dirty) continue;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[층 무리] 무리 {made.Count} · 바이옴 줄 {swapped}개 갈아 끼움. 옛 Band_Undead · Band_Cult는 습격 · 디버그가 쓰므로 남긴다.");
    }

    static WorldBandData Ensure(WorldBandData from, string key, int floor)
    {
        string path = $"{BandDir}/Band_{key}_F{floor}.asset";
        var band = AssetDatabase.LoadAssetAtPath<WorldBandData>(path);
        if (band == null)
        {
            band = Object.Instantiate(from);
            AssetDatabase.CreateAsset(band, path);
        }
        else EditorUtility.CopySerialized(from, band);
        band.name = $"Band_{key}_F{floor}";

        var so = new SerializedObject(band);
        so.FindProperty("bandName").stringValue = $"{from.BandName} ({FloorName[floor]})";

        // 숫자로 푸는 싸움(`WorldBattle`)은 실제 장비를 안 보고 이 보정이 장비를 대신한다 — 금속이 한 층 오를 때마다 +15%.
        // 대신 근거지 층 배율은 안 받는다(등급이 이미 층을 든다).
        so.FindProperty("PowerMultiplier").floatValue = from.PowerMultiplier * GearPower[floor];
        so.FindProperty("ScaledByFloor").boolValue    = true;

        var members = so.FindProperty("members");
        for (int i = 0; i < members.arraySize; i++)
        {
            var m      = members.GetArrayElementAtIndex(i);
            var prefab = m.FindPropertyRelative("Prefab").objectReferenceValue as GameObject;
            if (prefab == null || !RoleOf.TryGetValue(prefab.name, out var role))
            {
                Debug.LogWarning($"[층 무리] {path}: 모르는 구성원 {(prefab != null ? prefab.name : "(비었음)")} — 그대로 둔다.");
                continue;
            }

            var r = Ranks[role][floor - 1];
            m.FindPropertyRelative("MinCount").intValue = r.Min;
            m.FindPropertyRelative("MaxCount").intValue = r.Max;
            m.FindPropertyRelative("MinGrade").intValue = (int)r.Lo;
            m.FindPropertyRelative("MaxGrade").intValue = (int)r.Hi;
            m.FindPropertyRelative("Loadout").objectReferenceValue = EnsureLoadout(role, floor, prefab);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(band);
        return band;
    }

    static LoadoutData EnsureLoadout(string role, int floor, GameObject prefab)
    {
        string path = $"{LoadoutDir}/Loadout_{role}_F{floor}.asset";
        var l = AssetDatabase.LoadAssetAtPath<LoadoutData>(path);
        if (l == null) { l = ScriptableObject.CreateInstance<LoadoutData>(); AssetDatabase.CreateAsset(l, path); }

        l.Loadout = new List<LoadoutEntry>();
        foreach (var (id, chance) in Gear[role][floor - 1]) l.Loadout.Add(new LoadoutEntry { ItemId = id, Chance = chance });

        // 주머니 · 동전은 프리팹의 원래 장비 그대로 — 층이 바꾸는 것은 차는 것뿐이다.
        var source = BaseLoadoutOf(prefab);
        l.Loot = new List<LootEntry>();
        if (source != null)
        {
            foreach (var e in source.Loot)
                if (e != null) l.Loot.Add(new LootEntry { Item = e.Item, ItemId = e.ItemId, Chance = e.Chance, MinCount = e.MinCount, MaxCount = e.MaxCount });
            (l.CoinDropMin, l.CoinDropMax) = (source.CoinDropMin, source.CoinDropMax);
        }
        EditorUtility.SetDirty(l);
        return l;
    }

    static LoadoutData BaseLoadoutOf(GameObject prefab)
    {
        if (!prefab.TryGetComponent<UnitBrain>(out var brain)) return null;
        return new SerializedObject(brain).FindProperty("loadout").objectReferenceValue as LoadoutData;
    }
}
