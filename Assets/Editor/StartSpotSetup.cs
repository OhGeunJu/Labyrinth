using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// **씬의 내 폰을 치우고 그 자리에 시작 표식을 둔다**(2026-09-20, 사용자: *"씬에 있는 폰 지워버리고 시작 자리 하나 만들"*).
//
// 판이 열리면 캐릭터를 만드는 화면이 한 명을 세운다(`CharacterCreationUI`). 씬에 미리 놓인 방랑자가 남아 있으면
// **만든 사람 옆에 모르는 사람이 서 있다.** 그래서 치우고, **첫 방랑자의 자리를 시작 자리로 물려받는다** — 그 자리가
// 지금까지 판이 열리던 곳이라(`RockRenderer`가 거기를 팠다) 굴·마을과의 거리도 그대로다.
//
// - **방랑자만 치운다** — 하수인·적·NPC는 안 건드린다(치울 것은 *내가 만들 사람의 자리에 서 있던 사람*이다).
// - **표식이 이미 있으면 자리를 안 옮긴다**(손으로 옮겨 둔 자리를 다시 덮으면 안 된다).
// - 되돌리기(Undo) 한 덩어리 · 씬을 저장한다(`EditorSceneManager.SaveOpenScenes` — 에셋 저장으로는 씬이 안 남는다).
public static class StartSpotSetup
{
    [MenuItem("DungeonColony/World/I. Place Start Spot (씬의 방랑자 치우기)")]
    static void Run()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var wanderers = new List<UnitBrain>();
        foreach (var b in Object.FindObjectsByType<UnitBrain>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
        {
            if (EditorUtility.IsPersistent(b)) continue;                 // 에셋(프리팹 원본)은 아니다
            var f = b.GetComponent<Faction>();
            bool player = f != null && f.Data != null && f.Data.PlayerControlled;
            if (player && !(b.Data is MinionData)) wanderers.Add(b);
        }

        var spot = Object.FindFirstObjectByType<StartSpot>(FindObjectsInactive.Include);
        if (spot == null)
        {
            var go = new GameObject("StartSpot");
            Undo.RegisterCreatedObjectUndo(go, "시작 자리");
            spot = go.AddComponent<StartSpot>();
            if (wanderers.Count > 0) go.transform.position = wanderers[0].transform.position;
            else Debug.LogWarning("[시작 자리] 씬에 방랑자가 없어 원점에 두었다 — 씬에서 자리를 옮길 것.");
        }

        var names = new List<string>();
        foreach (var w in wanderers)
        {
            names.Add(w.name);
            // 프리팹 인스턴스 안쪽이면 바깥 뿌리째 지운다(인스턴스 일부만 지울 수는 없다).
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(w.gameObject);
            Undo.DestroyObjectImmediate(root != null ? root : w.gameObject);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(spot.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[시작 자리] `{spot.name}` {spot.transform.position} · 치운 방랑자 {wanderers.Count}명" +
                  (names.Count > 0 ? $" — {string.Join(", ", names)}" : ""));
    }
}
