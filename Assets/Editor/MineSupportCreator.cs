using UnityEditor;
using UnityEngine;

// **갱도 버팀목을 만든다 — 건물 하나와 프리팹 하나**
// (2026-09-18, 사용자 결정: 넓게 파려면 받쳐야 한다).
//
// **동굴 팩에 그 물건이 그대로 있다**(`Wooden/Supports/cave_supports_*`) — 갱도 천장을
// 받치는 나무 기둥이고, 이 게임에서 하는 일이 정확히 그것이다.
//
// **값은 나무다** — 돌로 두면 *벽을 파서 벽을 받치는* 꼴이라 제자리걸음이고, 무엇보다
// 굴을 넓히는 값이 **목재를 구하러 다니는 일**이 되어 1층 바이옴에 갈 이유가 하나 는다.
//
// **다시 돌려도 된다** — 있으면 값만 맞춘다(guid 유지).
public static class MineSupportCreator
{
    const string Cave      = "Assets/assetsss/3DForge/Cave Adventure kit/Prefabs";
    const string PrefabDir = "Assets/3_Prefab/Building";
    const string DataDir   = "Assets/Resources/Data/Buildings";
    const string ItemDir   = "Assets/Resources/Data/Items";

    const string Prop = Cave + "/Wooden/Supports/cave_supports_large1.prefab";

    [MenuItem("DungeonColony/Building/A. Create Mine Support (갱도 버팀목)")]
    static void Create()
    {
        var prop = AssetDatabase.LoadAssetAtPath<GameObject>(Prop);
        if (prop == null) { Debug.LogError($"[버팀목] 소품을 못 찾았습니다 — {Prop}"); return; }

        var wood = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/Res_Wood.asset");
        if (wood == null) { Debug.LogError("[버팀목] Res_Wood를 못 찾았습니다."); return; }

        // ── 프리팹 ──────────────────────────────────────────────────────────
        string path = $"{PrefabDir}/MineSupport.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        GameObject root;
        if (existing != null) root = PrefabUtility.LoadPrefabContents(path);
        else
        {
            root = new GameObject("MineSupport");

            // **칸을 안 막는다 — 그래서 트리거다**(통로 한복판에 세우는 물건이라 막으면
            // 받치려던 그 길이 끊긴다). 콜라이더는 **집기 위해서** 있다.
            var box = root.AddComponent<BoxCollider>();
            box.size      = new Vector3(0.8f, 2.2f, 0.8f);
            box.center    = new Vector3(0f, 1.1f, 0f);
            box.isTrigger = true;
        }

        try
        {
            // 지난번 모형을 걷는다.
            var old = root.transform.Find("[Art]");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var art = (GameObject)PrefabUtility.InstantiatePrefab(prop, root.transform);
            art.name = "[Art]";
            art.transform.localPosition = Vector3.zero;
            art.transform.localRotation = Quaternion.identity;
            art.transform.localScale    = Vector3.one;
            foreach (var c in art.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            if (root.GetComponent<MineSupport>() == null) root.AddComponent<MineSupport>();

            if (existing != null) PrefabUtility.SaveAsPrefabAsset(root, path);
            else                  PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (existing != null) PrefabUtility.UnloadPrefabContents(root);
            else                  Object.DestroyImmediate(root);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // ── 건물 데이터 ─────────────────────────────────────────────────────
        string dataPath = $"{DataDir}/MineSupport.asset";
        var data = AssetDatabase.LoadAssetAtPath<BuildingData>(dataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BuildingData>();
            AssetDatabase.CreateAsset(data, dataPath);
        }

        data.BuildingName = "갱도 버팀목";
        data.Description  = "굴을 지키는 나무 기둥. 둘레 네 칸의 판 굴이 다시 메워지지 않는다. "
                          + "핵 영역 밖에서 판 굴은 이틀 뒤 바위로 돌아온다.";
        data.Category     = BuildCategory.Mining;    // 파는 일을 지키는 물건 — 광맥 갈래와 같이 연다(2026-09-29)
        data.SortOrder    = 20;
        data.Size         = Vector2Int.one;
        data.Prefab       = prefab;
        data.OccupiesTile = false;      // 통로에 세운다 — 막으면 뜻이 없다
        data.UsesBlueprint = true;      // 값이 붙은 것은 청사진이어야 한다

        data.Costs.Clear();
        data.Costs.Add(new RecipeData.Ingredient { Item = wood, Amount = 12 });

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[버팀목] 갱도 버팀목을 만들었습니다 — 원목 12 · 둘레 "
                + RockSupport.SafeRadius + "칸의 판 굴을 지킵니다(핵 밖의 판 굴은 "
                + RockRegrow.Days + "일 뒤 메워진다).");
    }
}
