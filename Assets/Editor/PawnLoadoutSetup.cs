using UnityEditor;
using UnityEngine;

// **역할 장비(`LoadoutData`)를 만들고 프리팹에 물린다.**
//
// 에셋을 손으로 만들 수도 있지만(`Create → DungeonColony → LoadoutData`), 첫 벌은 도구로 짓는다 —
// `ItemId` 문자열을 인스펙터에 하나씩 타이핑하면 오타가 나고, 오타는 **조용히 아무것도 안 나오는
// 것**으로 나타난다(`Resolve()`가 null을 돌려주고 그 항목은 그냥 건너뛰어진다).
// 여기서 만들면 아래 목록이 곧 정본이라 오타가 있으면 로그에 뜬다.
//
// **없는 것만 만들고 비어 있을 때만 채운다** — 여러 번 돌려도 인스펙터에서 손본 값이 안 날아간다
// (`Setup World Bands`와 같은 규약).
//
// 일회성 도구다. 역할이 몇 개 서고 나면 지우고, 이후는 인스펙터에서 만든다.
public static class PawnLoadoutSetup
{
    const string Dir = "Assets/5_Data/Loadout";

    [MenuItem("DungeonColony/Setup Pawn Loadouts")]
    static void Setup()
    {
        EnsureFolder(Dir);

        var raider = EnsureAsset("Loadout_GoblinRaider", d =>
        {
            // 고블린 **도적**이 지니는 것. 고블린이라서가 아니라 도적이라서 지닌다 —
            // 같은 `Goblin` 종족을 쓰는 `Minion_Goblin`(내 하수인)은 이걸 안 단다.
            d.CoinDropMin = 5;
            d.CoinDropMax = 30;

            Wear(d, "Wpn_IronAxe",      0.55f);
            Wear(d, "Wpn_WoodShield",   0.35f);
            Wear(d, "Arm_LeatherChest", 0.40f);
            Wear(d, "Arm_LeatherHead",  0.25f);

            // **확률을 1로 두지 않는다** — 매번 같은 것이 나오면 시체를 뒤질 이유가 없다.
            Carry(d, "leather",         0.60f, 1, 3);
            Carry(d, "food_dried_meat", 0.45f, 1, 2);
            Carry(d, "herbs",           0.30f, 1, 4);
            Carry(d, "rope",            0.20f, 1, 1);
        });

        WirePrefab("Assets/3_Prefab/Enemy_Goblin.prefab", raider);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Loadout] 준비 완료. 역할을 더 만들려면 Create → DungeonColony → LoadoutData.");
    }

    static void Wear(LoadoutData d, string itemId, float chance) =>
        d.Loadout.Add(new LoadoutEntry { ItemId = itemId, Chance = chance });

    static void Carry(LoadoutData d, string itemId, float chance, int min, int max) =>
        d.Loot.Add(new LootEntry { ItemId = itemId, Chance = chance, MinCount = min, MaxCount = max });

    // 있으면 그대로 두고 없을 때만 만든다.
    static LoadoutData EnsureAsset(string name, System.Action<LoadoutData> fill)
    {
        string path = $"{Dir}/{name}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<LoadoutData>(path);
        if (existing != null) { Debug.Log($"[Loadout] {name} — 이미 있어 건드리지 않습니다."); return existing; }

        var asset = ScriptableObject.CreateInstance<LoadoutData>();
        fill(asset);
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[Loadout] {name} 생성 (장비 {asset.Loadout.Count} · 소지품 {asset.Loot.Count}).");
        return asset;
    }

    // **프리팹은 제자리에서 고친다.** 같은 경로에 새로 저장하면 루트 fileID가 바뀌어
    // 그 프리팹을 물고 있는 에셋(이벤트·무리 데이터)의 참조가 끊긴다.
    static void WirePrefab(string prefabPath, LoadoutData data)
    {
        if (data == null) return;

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) { Debug.LogWarning($"[Loadout] 프리팹이 없습니다: {prefabPath}"); return; }

        try
        {
            var brain = root.GetComponent<UnitBrain>();
            if (brain == null) { Debug.LogWarning($"[Loadout] {prefabPath}에 UnitBrain이 없습니다."); return; }

            var so   = new SerializedObject(brain);
            var prop = so.FindProperty("loadout");

            // **비어 있을 때만 채운다** — 손으로 다른 역할을 물려 뒀을 수 있다.
            if (prop.objectReferenceValue != null)
            {
                Debug.Log($"[Loadout] {prefabPath} — 이미 {prop.objectReferenceValue.name}이 물려 있어 둡니다.");
                return;
            }

            prop.objectReferenceValue = data;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[Loadout] {prefabPath} ← {data.name}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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
