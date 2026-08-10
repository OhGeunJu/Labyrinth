using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 씬에 손으로 놓은 시험용 적을 **`Enemy_Goblin` 프리팹 인스턴스로** 갈아 끼운다.
//
// 왜 필요했나: 씬의 `enemy`가 `Faction_Goblin` + **`Human` 데이터**였다. 두 축이 서로를 모르는
// 설계라(종족과 세력은 따로다) 어긋나도 아무도 불평하지 않고, 화면에는 `던컨 (인간)`이라고
// 정확히 뜬다 — **틀린 게 아니라 그렇게 저작된 것**이라 잡는 데 오래 걸렸다.
//
// 프리팹으로 갈아 끼우면 그 조합이 **한 곳에서 관리된다.** 씬에서 조립하면 다음에 적을 하나 더
// 놓을 때 또 어긋난다.
//
// **일회성 도구다** — 다 갈아 끼우면 지운다(`docs/editor_creators.md` "생성기의 수명").
public static class SceneEnemyPrefabSwap
{
    const string PrefabPath = "Assets/3_Prefab/Enemy_Goblin.prefab";

    // 갈아 끼울 대상 이름. 씬에 손으로 놓은 시험용 적이 이 이름들이다.
    static readonly string[] TargetNames = { "enemy" };

    [MenuItem("DungeonColony/Fix: Swap Scene Enemies To Enemy_Goblin")]
    static void Swap()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError($"[EnemySwap] 프리팹이 없습니다: {PrefabPath}"); return; }

        // **비활성 포함으로 훑는다** — 이 씬은 필드 오브젝트가 꺼진 채 저장돼 있는 것이 많다.
        var all = Object.FindObjectsByType<UnitBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var targets = new List<UnitBrain>();
        foreach (var b in all)
        {
            if (b == null) continue;
            if (System.Array.IndexOf(TargetNames, b.gameObject.name) < 0) continue;

            // **이미 프리팹 인스턴스면 건너뛴다** — 여러 번 돌려도 안전해야 한다.
            if (PrefabUtility.GetCorrespondingObjectFromSource(b.gameObject) != null) continue;

            targets.Add(b);
        }

        if (targets.Count == 0) { Debug.Log("[EnemySwap] 갈아 끼울 것이 없습니다(이미 프리팹이거나 대상 없음)."); return; }

        foreach (var old in targets)
        {
            var go = old.gameObject;
            var t  = go.transform;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t.parent);
            Undo.RegisterCreatedObjectUndo(inst, "Swap Enemy Prefab");

            // **자리·방향·크기를 그대로 물려받는다.** 시험용 적은 어디에 서 있느냐가 곧 시험 조건이라
            // 원점으로 튀면 다시 배치해야 한다.
            inst.transform.SetPositionAndRotation(t.position, t.rotation);
            inst.transform.localScale = t.localScale;
            inst.transform.SetSiblingIndex(t.GetSiblingIndex());
            inst.name = go.name;
            inst.SetActive(go.activeSelf);

            Debug.Log($"[EnemySwap] {go.name} → {prefab.name} (pos {t.position})");
            Undo.DestroyObjectImmediate(go);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[EnemySwap] {targets.Count}개 교체 완료.");
    }
}
