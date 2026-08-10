using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 지도 위의 무리(`WorldBandData`)를 한 벌 지어 놓고 바이옴·씬에 물려 주는 일회성 도구.
//
// 손으로 하면 되는 일이지만 **연결이 여섯 군데**라 하나만 빠져도 조용히 아무 일도 안 일어난다
// (세력 SO 미연결 → 전부 중립 / 바이옴 풀 비었음 → 소굴이 아무도 안 내보냄 / 씬에 매니저 없음 →
// 무리 자체가 없음). 한 번 돌려서 자리를 잡고 나면 **인스펙터에서** 고치는 것이 정본이다.
//
// 규약(`docs/editor_creators.md`): **없는 것만 만든다.** 이미 있는 에셋·프리팹은 손대지 않고,
// 바이옴 풀도 비어 있을 때만 채운다 — 여러 번 돌려도 손으로 손본 값이 날아가지 않는다.
public static class WorldBandSetup
{
    const string BandDir   = "Assets/5_Data/World/Band";
    const string NpcDir    = "Assets/Resources/Data/Npc";
    const string PrefabDir = "Assets/3_Prefab";

    const string GuardNpcPath    = NpcDir + "/Guard.asset";
    const string GuardPrefabPath = PrefabDir + "/Npc_Guard.prefab";
    const string MerchantPrefab  = PrefabDir + "/Npc_Merchant.prefab";
    const string EnemyPrefab     = PrefabDir + "/Enemy_Goblin.prefab";

    const string FactionGoblin   = "Assets/5_Data/Faction/Faction_Goblin.asset";
    const string FactionVillager = "Assets/5_Data/Faction/Faction_Villager.asset";
    const string GoldItem        = "Assets/Resources/Data/Items/Gold.asset";
    const string CaravanGoods    = "Assets/5_Data/Merchant/Merchant_Food.asset";

    [MenuItem("DungeonColony/Setup World Bands")]
    static void Run()
    {
        EnsureFolder(BandDir);

        var guardPrefab = EnsureGuard();

        var raiders = EnsureBand("Band_Goblin_Raiders", "고블린 도적떼", WorldBandBehavior.Roam,
            FactionGoblin, new Color(0.85f, 0.25f, 0.2f), speed: 4.2f, chase: 14f,
            members: new[] { (EnemyPrefab, 3, 6, StatGrade.FMinus, StatGrade.EPlus) },
            merchant: null, loot: GoldItem, powerMult: 0.9f);

        var caravan = EnsureBand("Band_Caravan", "상단", WorldBandBehavior.Caravan,
            FactionVillager, new Color(0.9f, 0.8f, 0.35f), speed: 3.8f, chase: 0f,
            members: BuildCaravanMembers(guardPrefab),
            merchant: CaravanGoods, loot: GoldItem, powerMult: 0.8f);

        var militia = guardPrefab == null ? null : EnsureBand(
            "Band_Militia", "마을 자경단", WorldBandBehavior.Patrol,
            FactionVillager, new Color(0.4f, 0.65f, 1f), speed: 4.8f, chase: 12f,
            members: new[] { (GuardPrefabPath, 2, 4, StatGrade.F, StatGrade.DPlus) },
            merchant: null, loot: null, powerMult: 1f);

        WireBiomes(raiders, caravan, militia);
        WireScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[WorldBandSetup] 완료 — 무리 에셋은 " + BandDir + ", 바이옴 풀과 씬 매니저까지 물렸습니다. " +
                  "이후 수정은 인스펙터에서 하세요.");
    }

    // ── 경비 프리팹 ───────────────────────────────────────────────────────────
    //
    // 순찰대에 세울 폰이 없어서 만든다. **상인 프리팹을 떠서** 역할만 갈아 끼운다 —
    // 종족·세력·AI 프로필이 이미 배선돼 있으므로 새로 만드는 것보다 어긋날 자리가 적다
    // (연결을 빠뜨린 폰은 아무와도 관계가 없어 판정이 전부 중립으로 떨어진다).

    static GameObject EnsureGuard()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GuardPrefabPath);
        if (existing != null) return existing;

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPrefab);
        if (src == null)
        {
            Debug.LogWarning($"[WorldBandSetup] {MerchantPrefab}가 없어 경비 프리팹을 못 만듭니다 — " +
                             "순찰대는 건너뜁니다.");
            return null;
        }

        var npc = AssetDatabase.LoadAssetAtPath<NpcData>(GuardNpcPath);
        if (npc == null)
        {
            npc = ScriptableObject.CreateInstance<NpcData>();
            npc.NpcType      = NpcType.Guard;
            npc.WanderRadius = 12f;
            AssetDatabase.CreateAsset(npc, GuardNpcPath);
        }

        var contents = PrefabUtility.LoadPrefabContents(MerchantPrefab);
        try
        {
            // 경비는 장사하지 않는다. 안 떼면 클릭할 때마다 상점이 열린다.
            var merchant = contents.GetComponent<Merchant>();
            if (merchant != null) Object.DestroyImmediate(merchant, true);

            var brain = contents.GetComponent<UnitBrain>();
            if (brain != null)
            {
                var so = new SerializedObject(brain);
                so.FindProperty("npcData").objectReferenceValue = npc;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, GuardPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        Debug.Log($"[WorldBandSetup] 경비 프리팹 생성 — {GuardPrefabPath}");
        return AssetDatabase.LoadAssetAtPath<GameObject>(GuardPrefabPath);
    }

    static (string, int, int, StatGrade, StatGrade)[] BuildCaravanMembers(GameObject guard)
    {
        var list = new List<(string, int, int, StatGrade, StatGrade)>
        {
            (MerchantPrefab, 1, 1, StatGrade.F, StatGrade.D),
        };
        // 호위 없는 상단은 첫 약탈대에 그대로 없어진다 — 지도에 상단이 다니는 것 자체를 못 본다.
        if (guard != null) list.Add((GuardPrefabPath, 1, 3, StatGrade.F, StatGrade.DPlus));
        return list.ToArray();
    }

    // ── 무리 에셋 ─────────────────────────────────────────────────────────────

    static WorldBandData EnsureBand(string fileName, string display, WorldBandBehavior behavior,
                                    string factionPath, Color color, float speed, float chase,
                                    (string prefab, int min, int max, StatGrade lo, StatGrade hi)[] members,
                                    string merchant, string loot, float powerMult)
    {
        string path = $"{BandDir}/{fileName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<WorldBandData>(path);
        if (existing != null) return existing;   // 손본 값이 있을 수 있다 — 건드리지 않는다

        var asset = ScriptableObject.CreateInstance<WorldBandData>();
        AssetDatabase.CreateAsset(asset, path);

        var so = new SerializedObject(asset);
        so.FindProperty("bandName").stringValue        = display;
        so.FindProperty("tokenColor").colorValue       = color;
        so.FindProperty("behavior").enumValueIndex     = (int)behavior;
        so.FindProperty("speed").floatValue            = speed;
        so.FindProperty("ChaseRadius").floatValue      = chase;
        so.FindProperty("PowerMultiplier").floatValue  = powerMult;

        SetRef(so, "faction",  factionPath);
        SetRef(so, "merchant", merchant);

        var mem = so.FindProperty("members");
        mem.arraySize = members.Length;
        for (int i = 0; i < members.Length; i++)
        {
            var e = mem.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Prefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(members[i].prefab);
            e.FindPropertyRelative("MinCount").intValue      = members[i].min;
            e.FindPropertyRelative("MaxCount").intValue      = members[i].max;
            e.FindPropertyRelative("MinGrade").enumValueIndex = (int)members[i].lo;
            e.FindPropertyRelative("MaxGrade").enumValueIndex = (int)members[i].hi;
        }

        var lootProp = so.FindProperty("loot");
        var lootItem = string.IsNullOrEmpty(loot) ? null : AssetDatabase.LoadAssetAtPath<ItemData>(loot);
        if (lootItem != null)
        {
            lootProp.arraySize = 1;
            var e = lootProp.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("Item").objectReferenceValue = lootItem;
            e.FindPropertyRelative("MinPerKill").intValue = 5;
            e.FindPropertyRelative("MaxPerKill").intValue = 25;
            e.FindPropertyRelative("Chance").floatValue   = 0.7f;
        }
        else lootProp.arraySize = 0;

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[WorldBandSetup] 무리 에셋 생성 — {path}");
        return asset;
    }

    static void SetRef(SerializedObject so, string field, string path)
    {
        var p = so.FindProperty(field);
        if (p == null) return;
        p.objectReferenceValue = string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<Object>(path);
    }

    // ── 바이옴 풀 ─────────────────────────────────────────────────────────────
    //
    // **비어 있을 때만 채운다.** 손으로 지역색을 넣어 뒀는데 도구가 덮어쓰면, 다시 돌릴 때마다
    // 저작이 사라진다(건설 목록 등록이 같은 규약을 쓴다).

    static void WireBiomes(WorldBandData raiders, WorldBandData caravan, WorldBandData militia)
    {
        var guids = AssetDatabase.FindAssets("t:BiomeContentData");
        int touched = 0;

        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var data = AssetDatabase.LoadAssetAtPath<BiomeContentData>(path);
            if (data == null) continue;

            var so = new SerializedObject(data);
            bool changed = false;

            var lairs = so.FindProperty("Lairs");
            if (lairs.arraySize == 0 && raiders != null)
            {
                lairs.arraySize = 1;
                var e = lairs.GetArrayElementAtIndex(0);
                e.FindPropertyRelative("Band").objectReferenceValue = raiders;
                e.FindPropertyRelative("LairName").stringValue      = "고블린 소굴";
                e.FindPropertyRelative("MinStrength").intValue      = 8;
                e.FindPropertyRelative("MaxStrength").intValue      = 18;
                e.FindPropertyRelative("SortieDays").floatValue     = 0.5f;
                e.FindPropertyRelative("RegenPerDay").intValue      = 1;
                e.FindPropertyRelative("Weight").floatValue         = 1f;

                // **새 필드도 빠짐없이 적는다.** 직렬화된 목록을 `arraySize`로 늘리면 C# 필드
                // 기본값이 안 들어갈 수 있다 — `RaidIntervalDays`가 0이면 그 소굴은 습격을
                // 영영 안 오고, 그건 "안 온다"로만 보여서 원인을 짚을 데가 없다.
                e.FindPropertyRelative("RoamDays").floatValue         = 1.5f;
                e.FindPropertyRelative("RaidIntervalDays").floatValue = 3f;
                e.FindPropertyRelative("RaidWarningDays").floatValue  = 1f;
                e.FindPropertyRelative("RaidPortion").floatValue      = 0.6f;
                changed = true;
            }

            var bands = so.FindProperty("TownBands");
            if (bands.arraySize == 0)
            {
                var wanted = new List<WorldBandData>();
                if (caravan != null) wanted.Add(caravan);
                if (militia != null) wanted.Add(militia);

                bands.arraySize = wanted.Count;
                for (int i = 0; i < wanted.Count; i++)
                    bands.GetArrayElementAtIndex(i).objectReferenceValue = wanted[i];
                changed = wanted.Count > 0 || changed;
            }

            if (!changed) continue;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            touched++;
        }

        Debug.Log($"[WorldBandSetup] 바이옴 {touched}개에 소굴·마을 무리 풀을 채웠습니다 " +
                  "(이미 채워진 것은 건드리지 않았습니다).");
    }

    // ── 씬 ────────────────────────────────────────────────────────────────────

    static void WireScene()
    {
        // **비활성을 포함해 찾는다** — 이 씬은 월드 측 오브젝트가 꺼진 채로 저장돼 있다.
        var view = Object.FindFirstObjectByType<WorldMapView>(FindObjectsInactive.Include);
        if (view == null)
        {
            Debug.LogWarning("[WorldBandSetup] 씬에 WorldMapView가 없어 매니저를 못 붙였습니다. " +
                             "직접 아무 오브젝트에나 WorldBandManager를 붙이세요.");
            return;
        }

        if (view.GetComponent<WorldBandManager>() != null)
        {
            Debug.Log("[WorldBandSetup] WorldBandManager는 이미 붙어 있습니다.");
            return;
        }

        Undo.AddComponent<WorldBandManager>(view.gameObject);

        // **씬은 직접 저장해야 남는다** — SaveAssets는 에셋만 저장한다.
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[WorldBandSetup] {view.name}에 WorldBandManager를 붙이고 씬을 저장했습니다.");
    }

    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
