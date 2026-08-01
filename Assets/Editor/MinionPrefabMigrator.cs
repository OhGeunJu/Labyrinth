using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 씬에 저작돼 있던 소환 배선(`DungeonCore.minionEntries`)을 **레지스트리로 옮기는 도구**.
//
// 왜 손으로 못 하나: 씬의 항목은 프리팹이 아니라 **씬 안의 오브젝트**를 가리키고 있었다
// (`Prefab: {fileID: ...}`). 에셋은 씬 오브젝트를 참조할 수 없으므로 `MinionData.Prefab`에
// 그냥 끌어다 놓을 수가 없다 — 먼저 프리팹 에셋으로 떠야 한다.
//
// 하는 일:
//   1. 씬 오브젝트를 `3_Prefab/Minion_{데이터이름}.prefab` 으로 뜬다 (**이미 있으면 안 만든다**)
//   2. 그 프리팹을 `MinionData.Prefab` 에 연결한다
//   3. `minionEntries` 를 비운다 → 이제 목록·프리팹 모두 `Resources/Data/Units/` 에서 온다
//
// 전부 성공했을 때만 3번을 한다. 하나라도 실패하면 씬 배선을 그대로 남긴다 —
// 배선을 지웠는데 대체물이 없으면 소환이 통째로 죽는다.
public static class MinionPrefabMigrator
{
    const string PrefabDir = "Assets/3_Prefab";

    [MenuItem("DungeonColony/Migrate Minion Prefabs")]
    static void Migrate()
    {
        // 이 씬은 필드 오브젝트가 꺼진 채로 저장돼 있는 것이 많다 — 비활성을 반드시 포함해 찾는다.
        var cores = Object.FindObjectsByType<DungeonCore>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cores.Length == 0)
        {
            Debug.LogError("[MinionPrefabMigrator] 씬에 DungeonCore가 없습니다.");
            return;
        }

        EnsureFolder(PrefabDir);
        int made = 0, linked = 0, cleared = 0;

        foreach (var core in cores)
        {
            var so  = new SerializedObject(core);
            var arr = so.FindProperty("minionEntries");
            if (arr == null || !arr.isArray || arr.arraySize == 0) continue;

            bool allOk = true;

            for (int i = 0; i < arr.arraySize; i++)
            {
                var el   = arr.GetArrayElementAtIndex(i);
                var data = el.FindPropertyRelative("Data").objectReferenceValue as MinionData;
                var src  = el.FindPropertyRelative("Prefab").objectReferenceValue as GameObject;

                if (data == null)
                {
                    Debug.LogWarning($"[MinionPrefabMigrator] {core.name}: {i}번 항목에 MinionData가 없습니다 — 건너뜁니다.");
                    allOk = false;
                    continue;
                }

                if (src == null)
                {
                    Debug.LogWarning($"[MinionPrefabMigrator] '{data.name}'에 연결된 오브젝트가 없습니다.");
                    allOk = false;
                    continue;
                }

                // 이미 에셋이면 그대로 쓴다. 씬 오브젝트일 때만 뜬다.
                GameObject asset = EditorUtility.IsPersistent(src) ? src : SaveAsAsset(src, data, ref made);
                if (asset == null) { allOk = false; continue; }

                if (data.Prefab != asset)
                {
                    data.Prefab = asset;
                    EditorUtility.SetDirty(data);
                    linked++;
                    Debug.Log($"[MinionPrefabMigrator] {data.name}.Prefab → {AssetDatabase.GetAssetPath(asset)}");
                }
            }

            if (allOk)
            {
                arr.arraySize = 0;
                cleared++;
            }
            else
            {
                Debug.LogWarning($"[MinionPrefabMigrator] {core.name}: 실패한 항목이 있어 minionEntries를 그대로 둡니다. " +
                                 "위 경고를 고치고 다시 실행하세요.");
            }

            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(core.gameObject.scene);
        }

        // 에셋(MinionData)과 **씬을 둘 다** 저장한다. SaveAssets는 씬을 저장하지 않으므로,
        // 이걸 빠뜨리면 유니티를 닫는 순간 minionEntries를 비운 것이 통째로 날아간다.
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[MinionPrefabMigrator] 완료 — 프리팹 생성 {made}개, 데이터 연결 {linked}건, " +
                  $"소환 배선 비움 {cleared}개. 이제 소환 목록은 Resources/Data/Units/ 에서 옵니다.");
    }

    static GameObject SaveAsAsset(GameObject sceneObj, MinionData data, ref int made)
    {
        string path = $"{PrefabDir}/Minion_{data.name}.prefab";

        // 이미 있으면 손대지 않는다 — 인스펙터에서 모양·값을 손봤을 수 있다(여러 번 실행해도 안전).
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            Debug.Log($"[MinionPrefabMigrator] {path} 이미 있음 — 그대로 씁니다.");
            return existing;
        }

        // **켜서 저장한다.** 씬의 템플릿은 꺼져 있고 `Instantiate`는 활성 상태를 그대로 복제하므로,
        // 꺼진 채로 뜨면 소환된 하수인이 꺼진 채 나와 `Awake`조차 안 돈다.
        // 씬 오브젝트를 직접 켜지 않으려고 임시 복제본에서 켠다.
        GameObject temp = null;
        try
        {
            temp = Object.Instantiate(sceneObj);
            temp.name = sceneObj.name;
            temp.SetActive(true);

            var asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
            if (asset == null)
            {
                Debug.LogError($"[MinionPrefabMigrator] 프리팹 저장 실패: {path}");
                return null;
            }
            made++;
            return asset;
        }
        finally
        {
            if (temp != null) Object.DestroyImmediate(temp);
        }
    }

    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
