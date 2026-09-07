using UnityEditor;
using UnityEngine;

// **힘을 먹는 건물에 `PowerUser`를 물린다**(2026-09-06).
//
// **손으로 붙이게 두지 않는다** — 스물몇 개 프리팹 중 하나만 빠뜨리면 **그것만 공짜로 도는
// 건물**이 되고, 화면에는 아무 표시가 안 난다(이 저장소가 여러 번 겪은 *"조용히 틀리는"*
// 자리). 목록을 코드에 두면 **무엇이 힘을 먹는지가 한 곳에 적힌다.**
//
// ## 무엇이 먹나 — "가만히 있으면서 계속 내놓는 것"
//
// 이식 광맥(`Node_*Vein`)과 유지 시설(충전대·제단). **벽·바닥·침대·작업대는 안 먹는다** —
// 값이 배치 재료로 끝나거나(전자) 폰이 붙어 있어야만 돌아가기(후자) 때문이다. 힘은
// *사람 없이 도는 것*의 값이다.
//
// ## 눈금
//
// 핵이 10, 부핵이 6이다. 광맥 2 · 유지 시설 3 — **처음 핵으로는 광맥 다섯이나 충전대 셋**이고,
// 더 세우려면 밖에 나가 부핵 재료를 캐 와야 한다. **그것이 이 축을 넣은 이유다.**
public static class PowerWiring
{
    // **폴더가 둘이다** — 이식 광맥 프리팹은 `Node`에 있고 충전대·제단은 `Building`에 있다.
    // 하나만 훑었더니 **광맥 열두 개가 통째로 배선에서 빠졌고**, 그것은 화면에서
    // *"핵이 없는데도 잘 캐진다"*로만 보였다(사용자가 그렇게 잡아냈다).
    static readonly string[] PrefabDirs =
    {
        "Assets/3_Prefab/Building",
        "Assets/3_Prefab/Node",
    };

    const int VeinCost   = 2;   // 이식 광맥
    const int UpkeepCost  = 3;   // 충전대 · 제단

    [MenuItem("DungeonColony/World/8. Wire Power Users (전력 배선)")]
    static void Wire()
    {
        int done = 0, skipped = 0, stripped = 0;
        _built = null;   // 다시 돌릴 때는 다시 센다

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", PrefabDirs))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // **이름이 아니라 "지어지는 것인가"로 고른다**(2026-09-06에 바로잡았다).
            //
            // `Node_` 앞머리로 골랐더니 **야생 거대버섯(`Node_GiantMushroom`)에도 붙었고**,
            // 핵이 없으니 힘을 못 받아 **폰이 베러 가서 아무것도 안 했다.** 이름은 사람이
            // 붙이는 것이라 규칙의 근거가 못 된다.
            //
            // **지어지는 것은 `BuildingData`가 가리키는 프리팹이다** — 그것이 곧 *미궁핵에
            // 건설된 자원*의 정의이고, 새 광맥을 저작해도 저절로 따라온다.
            int cost = CostFor(path);
            if (cost <= 0)
            {
                // **잘못 붙은 것은 걷어낸다** — 야생에 붙으면 핵이 없을 때 **폰이 가서
                // 아무것도 안 한다.** 남겨 두면 다음에 이 도구를 돌려도 안 고쳐진다.
                var stray = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (stray != null && stray.GetComponent<PowerUser>() != null)
                {
                    var c2 = PrefabUtility.LoadPrefabContents(path);
                    var pu = c2.GetComponent<PowerUser>();
                    if (pu != null) Object.DestroyImmediate(pu, true);
                    PrefabUtility.SaveAsPrefabAsset(c2, path);
                    PrefabUtility.UnloadPrefabContents(c2);
                    stripped++;
                }
                else skipped++;
                continue;
            }

            // **프리팹은 열어서 고친다** — `LoadAssetAtPath`로 얻은 것에 컴포넌트를 붙이면
            // 씬 인스턴스에만 붙고 에셋에는 안 남는다.
            var contents = PrefabUtility.LoadPrefabContents(path);
            // **자원 노드가 아닌 프리팹에는 안 붙인다** — `Node_` 앞머리가 붙었어도
            // 모형만인 것이 있을 수 있다. 붙일 근거는 *힘을 먹는 물건인가*이지 이름이 아니다.
            var user = contents.GetComponent<PowerUser>();
            if (user == null) user = contents.AddComponent<PowerUser>();

            var so = new SerializedObject(user);
            so.FindProperty("cost").intValue = cost;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            done++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[전력] `PowerUser`를 프리팹 {done}개에 물렸다 · 잘못 붙은 것 {stripped}개를 걷어냈다(건너뜀 {skipped}).\n" +
                  "핵 10 · 부핵 6 · 광맥 2 · 유지 시설 3 — 처음 핵으로는 광맥 다섯쯤이다.\n" +
                  "**이미 씬에 서 있는 건물에는 안 붙는다** — 프리팹에서 다시 짓거나 손으로 붙일 것.");
    }

    // 이 프리팹을 가리키는 `BuildingData`가 있나 — 있으면 *지어지는 것*이다.
    static int CostFor(string prefabPath)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (go == null) return 0;

        _built ??= BuildBuiltSet();
        if (!_built.Contains(go)) return 0;

        if (go.GetComponent<Recharger>()    != null) return UpkeepCost;
        if (go.GetComponent<ResourceNode>() != null) return VeinCost;
        return 0;
    }

    static System.Collections.Generic.HashSet<GameObject> _built;

    static System.Collections.Generic.HashSet<GameObject> BuildBuiltSet()
    {
        var set = new System.Collections.Generic.HashSet<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:BuildingData"))
        {
            var d = AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(guid));
            if (d != null && d.Prefab != null) set.Add(d.Prefab);
        }
        return set;
    }
}
