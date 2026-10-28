using UnityEditor;
using UnityEngine;

// **소환진을 만든다 — 건물 하나와 프리팹 하나**(2026-09-29, 사용자: *"던전 코어에 있는 소환을 다른 건축물로 새로 만들고"*).
//
// 몸은 **던전 제단**(djf `P_PROP_altar_dungeon_01`) 위에 **보라 마법장**(3DForge `Magicfield_purple`)을 얹는다 — 부르는 자리로
// 읽히게. 누르면 소환 창이 열린다(`SummonCircle` → `DungeonCoreUI`). **칸을 막는다**(2×2) · **연구가 없다**(소환은 판 처음부터
// 되던 일이다 — 잠그면 판이 열리자마자 하수인을 못 부른다) · 값은 돌.
//
// **다시 돌려도 된다** — 있으면 모형만 갈고 값만 맞춘다(guid 유지).
public static class SummonCircleCreator
{
    const string Altar     = "Assets/assetsss/djf/prefabs/PROPS/furniture/P_PROP_altar_dungeon_01.prefab";
    const string Field     = "Assets/assetsss/3DForge/Cave Adventure kit/Particles/Magic_Fields/Magicfield_purple.prefab";
    const string PrefabDir = "Assets/3_Prefab/Building";
    const string DataDir   = "Assets/Resources/Data/Buildings";
    const string ItemDir   = "Assets/Resources/Data/Items";

    const float Footprint = 2f;   // 칸(2×2)

    [MenuItem("DungeonColony/Building/B. Create Summoning Circle (소환진)")]
    static void Create()
    {
        var altar = AssetDatabase.LoadAssetAtPath<GameObject>(Altar);
        if (altar == null) { Debug.LogError($"[소환진] 제단 모형을 못 찾았다 — {Altar}"); return; }
        var field = AssetDatabase.LoadAssetAtPath<GameObject>(Field);   // 없으면 제단만
        var stone = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/Res_Stone.asset");

        // ── 건물 데이터(프리팹이 가리키므로 먼저) ──────────────────────────────
        string dataPath = $"{DataDir}/SummonCircle.asset";
        var data = AssetDatabase.LoadAssetAtPath<BuildingData>(dataPath);
        if (data == null) { data = ScriptableObject.CreateInstance<BuildingData>(); AssetDatabase.CreateAsset(data, dataPath); }

        // ── 프리팹 ──────────────────────────────────────────────────────────
        string path = $"{PrefabDir}/SummonCircle.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = existing != null ? PrefabUtility.LoadPrefabContents(path) : new GameObject("SummonCircle");
        try
        {
            // **클릭 판정은 루트의 콜라이더 하나** — `SelectionManager`는 맞은 콜라이더 자신에서 `IWorldClickable`을 찾는다.
            // **`??`를 안 쓴다** — 에디터의 `GetComponent`는 없을 때 *가짜 null*을 돌려줘 `??`를 통과한다(`BridgeCreator`가 적어 둔 그 함정).
            var box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();
            box.size   = new Vector3(Footprint * 0.9f, 1.2f, Footprint * 0.9f);
            box.center = new Vector3(0f, 0.6f, 0f);

            var old = root.transform.Find("[Art]");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var art = new GameObject("[Art]").transform;
            art.SetParent(root.transform, false);

            var body = (GameObject)PrefabUtility.InstantiatePrefab(altar, art);
            foreach (var c in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            Fit(body.transform, Footprint * 0.95f);

            if (field != null)
            {
                var fx = (GameObject)PrefabUtility.InstantiatePrefab(field, art);
                fx.transform.localPosition = new Vector3(0f, TopOf(body) + 0.05f, 0f);
            }

            if (root.GetComponent<SummonCircle>() == null) root.AddComponent<SummonCircle>();
            var pb = root.GetComponent<PlacedBuilding>();
            if (pb == null) pb = root.AddComponent<PlacedBuilding>();
            var so = new SerializedObject(pb);
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (existing != null) PrefabUtility.UnloadPrefabContents(root);
            else                  Object.DestroyImmediate(root);
        }

        data.BuildingName  = "소환진";
        data.Description   = "하수인을 부르는 제단. 눌러서 소환한다 - 목록과 한도는 이 자리를 덮는 핵이 정한다.";
        data.Category      = BuildCategory.Dungeon;
        data.SortOrder     = 5;
        data.Size          = new Vector2Int((int)Footprint, (int)Footprint);
        data.Prefab        = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        data.OccupiesTile  = true;
        data.UsesBlueprint = true;
        data.Costs.Clear();
        if (stone != null) data.Costs.Add(new RecipeData.Ingredient { Item = stone, Amount = 20 });

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[소환진] 소환진을 만들었다 — 건설 `제작` 갈래 · 돌 20 · 2x2. 핵을 누르면 이제 배치 창이 열린다.");
    }

    // 가로로 가장 긴 쪽을 `size`에 맞추고 발밑을 0에.
    static void Fit(Transform t, float size)
    {
        var b = BoundsOf(t.gameObject);
        float w = Mathf.Max(b.size.x, b.size.z);
        if (w > 1e-4f) t.localScale = Vector3.one * (size / w);
        b = BoundsOf(t.gameObject);
        t.position += new Vector3(t.parent.position.x - b.center.x, t.parent.position.y - b.min.y, t.parent.position.z - b.center.z);
    }

    static float TopOf(GameObject go) => BoundsOf(go).max.y - go.transform.parent.position.y;

    static Bounds BoundsOf(GameObject go)
    {
        var b = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
