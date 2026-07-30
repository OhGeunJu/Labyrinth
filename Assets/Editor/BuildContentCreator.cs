using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// DungeonColony → Add Missing Buildings
//
// 건설 목록에 없던 것들을 채운다 — 작업대 3종(무기/제련/조리) · 연구대 · 나무.
//
// 작업대와 연구대는 **씬에만 있고 프리팹이 없었다.** 그래서 씬 인스턴스를 프리팹으로 저장한 뒤
// 그걸 가리키는 `BuildingData`를 만들고 `BuildModeManager.placeableBuildings`에 얹는다.
//
// **씬 탐색은 비활성 포함**이다 — 이 씬의 작업대·연구대는 `Settlement_0` 아래에 꺼진 채로 있다
// (`docs/editor_creators.md`의 규약). 여러 번 실행해도 안전하다.
public static class BuildContentCreator
{
    const string PrefabDir = "Assets/3_Prefab";
    const string DataDir   = "Assets/5_Data/Building";

    [MenuItem("DungeonColony/Add Missing Buildings")]
    static void Create()
    {
        var log = new List<string>();
        var made = new List<BuildingData>();

        // ── 작업대 — tableType별로 프리팹이 따로다 ─────────────────────────
        // `BuildingData`에는 스킬 종류를 담는 칸이 없다. 그래서 "무엇을 만드는 작업대인가"는
        // 프리팹이 들고 있어야 하고, 종류마다 프리팹이 하나씩 필요하다.
        var tables = Object.FindObjectsByType<CraftingTable>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        var wanted = new (CraftSkill skill, string file, string name, ResourceType cost, int amount)[]
        {
            (CraftSkill.WeaponCrafting, "CraftingTable_Weapon", "무기 제작대", ResourceType.Wood,  25),
            (CraftSkill.Smelting,       "CraftingTable_Smelt",  "용광로",      ResourceType.Stone, 40),
            (CraftSkill.Cooking,        "CraftingTable_Cook",   "조리대",      ResourceType.Wood,  20),
        };

        foreach (var w in wanted)
        {
            var src = FindTable(tables, w.skill);
            if (src == null) { log.Add($"건너뜀: {w.name} — 씬에 {w.skill} 작업대가 없습니다"); continue; }

            var prefab = SavePrefab(src.gameObject, w.file, log);
            if (prefab == null) continue;

            var data = EnsureData(w.file, w.name, prefab, BuildCategory.Building,
                                  w.cost, w.amount, occupiesTile: true, log);
            if (data != null) made.Add(data);
        }

        // ── 연구대 ──────────────────────────────────────────────────────────
        var research = Object.FindFirstObjectByType<ResearchTable>(FindObjectsInactive.Include);
        if (research == null) log.Add("건너뜀: 연구대 — 씬에 ResearchTable이 없습니다");
        else
        {
            var prefab = SavePrefab(research.gameObject, "ResearchTable", log);
            if (prefab != null)
            {
                var data = EnsureData("ResearchTable", "연구대", prefab, BuildCategory.Building,
                                      ResourceType.Wood, 30, occupiesTile: true, log);
                if (data != null) made.Add(data);
            }
        }

        // ── 나무 — 프리팹은 이미 있다 ───────────────────────────────────────
        var tree = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Tree.prefab");
        if (tree == null) log.Add("건너뜀: 나무 — Tree.prefab이 없습니다");
        else
        {
            var data = EnsureData("TreeBuilding", "나무 심기", tree, BuildCategory.Resource,
                                  ResourceType.Wood, 5, occupiesTile: true, log);
            if (data != null) made.Add(data);
        }

        WireBuildMode(made, log);

        var sb = new StringBuilder("[건설 추가] 완료 — 씬 저장 필요\n");
        foreach (var l in log) sb.AppendLine("  " + l);
        Debug.Log(sb.ToString());
    }

    static CraftingTable FindTable(CraftingTable[] tables, CraftSkill skill)
    {
        foreach (var t in tables)
        {
            if (t == null) continue;
            var so = new SerializedObject(t);
            var p  = so.FindProperty("tableType");
            if (p != null && p.enumValueIndex == (int)skill) return t;
        }
        return null;
    }

    // 씬 인스턴스 → 프리팹. 이미 있으면 **덮어쓰지 않는다**(인스펙터에서 손봤을 수 있다).
    static GameObject SavePrefab(GameObject src, string file, List<string> log)
    {
        var path = $"{PrefabDir}/{file}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) { log.Add($"확인: {path}"); return existing; }

        // 씬에서 꺼져 있어도 프리팹은 켜진 상태로 저장해야 배치했을 때 보인다.
        var copy = Object.Instantiate(src);
        copy.name = file;
        copy.SetActive(true);

        var prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
        Object.DestroyImmediate(copy);

        log.Add(prefab != null ? $"생성: {path}" : $"실패: {path}");
        return prefab;
    }

    static BuildingData EnsureData(string file, string name, GameObject prefab, BuildCategory category,
                                   ResourceType cost, int amount, bool occupiesTile, List<string> log)
    {
        var path = $"{DataDir}/{file}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        if (existing != null) { log.Add($"확인: {path}"); return existing; }

        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.BuildingName = name;
        data.Category     = category;
        data.Prefab       = prefab;
        data.CostType     = cost;
        data.CostAmount   = amount;
        data.OccupiesTile = occupiesTile;   // 작업대·연구대·나무는 유닛이 통과하지 못한다

        AssetDatabase.CreateAsset(data, path);
        log.Add($"생성: {path} ({name} — {cost.DisplayName()} {amount})");
        return data;
    }

    // 만든 것을 건설 목록에 얹는다. **이미 실려 있으면 건너뛴다.**
    static void WireBuildMode(List<BuildingData> made, List<string> log)
    {
        var bm = Object.FindFirstObjectByType<BuildModeManager>(FindObjectsInactive.Include);
        if (bm == null) { log.Add("주의: 씬에 BuildModeManager가 없어 목록에 못 얹었습니다."); return; }

        var so   = new SerializedObject(bm);
        var list = so.FindProperty("placeableBuildings");

        var already = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
            already.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

        int added = 0;
        foreach (var d in made)
        {
            if (d == null || already.Contains(d)) continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = d;
            added++;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(bm);
        EditorSceneManager.MarkSceneDirty(bm.gameObject.scene);

        log.Add($"연결: BuildModeManager.placeableBuildings +{added} (총 {list.arraySize})");
    }
}
