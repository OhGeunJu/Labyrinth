using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// DungeonColony → Clean Up Stray Data
//
// 참조가 끊겼거나 중복인 데이터 에셋을 치운다. **지우기 전에 대신할 것을 먼저 연결한다** —
// 씬의 `ResearchTable`이 빈 스캐폴드 연구 하나만 물고 있어서, 그걸 그냥 지우면 연구대가 빈다.
//
// 지우는 대상은 전부 GUID 역참조로 "쓰이지 않음"을 확인한 것들이다.
// 다 쓰면 이 스크립트도 지운다(생성기의 수명).
public static class DataCleanup
{
    const string ResearchDir = "Assets/Resources/Data/Research";

    static readonly string[] StrayAssets =
    {
        "Assets/5_Data/Resource/Ore.asset",                        // 새 Node_* 세트로 대체됨
        "Assets/5_Data/Resource/Crystal.asset",                    // 〃
        "Assets/Resources/Data/Research/New Research Data.asset",  // 빈 스캐폴드
        "Assets/Resources/Data/Biome/New Biome Content Data.asset",// 폴더가 Biome(단수) — 로드 안 됨
    };

    const string StrayFolder = "Assets/Resources/Data/Biome";

    [MenuItem("DungeonColony/Clean Up Stray Data")]
    static void Clean()
    {
        var log = new List<string>();

        // ── 1. 연구대에 실제 연구를 연결 (지우기 **전에**) ──────────────────
        //
        // **비활성 오브젝트까지 찾아야 한다.** 이 씬의 연구대/작업대는 `Settlement_0` 아래에
        // 꺼진 채로 있어서, 기본 탐색(`FindFirstObjectByType`)으로는 안 잡힌다 —
        // 그걸 모르고 지웠다가 목록에 빈 항목만 남긴 적이 있다.
        var tables = Object.FindObjectsByType<ResearchTable>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (tables.Length == 0)
        {
            log.Add("주의: 씬에 ResearchTable이 없습니다 — 연결 단계를 건너뜁니다.");
        }
        else
        {
            // **목록을 채우지 않고 비운다.** `ResearchTable`이 비어 있으면
            // `DataManager`에 등록된 연구 전부를 쓰도록 바뀌었기 때문이다(2026-07-31).
            //
            // 인스턴스마다 손으로 채우면 ① 연구를 새로 추가해도 이 연구대엔 안 나오고
            // ② 런타임에 지은 연구대는 목록이 텅 빈다. 레지스트리를 따라가게 두는 편이 옳다.
            // 특정 연구대만 일부만 다루게 하고 싶을 때는 인스펙터에서 채우면 그게 이긴다.
            foreach (var table in tables)
            {
                var so   = new SerializedObject(table);
                var list = so.FindProperty("availableResearch");
                int had  = list.arraySize;

                list.ClearArray();
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(table);
                EditorSceneManager.MarkSceneDirty(table.gameObject.scene);

                log.Add($"정리: {table.name}.availableResearch 비움(이전 {had}개) → DataManager 전체를 따라감" +
                        (table.gameObject.activeInHierarchy ? "" : " (비활성 오브젝트)"));
            }
        }

        // ── 2. 잔재 삭제 ────────────────────────────────────────────────────
        foreach (var path in StrayAssets)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) == null) { log.Add($"없음(건너뜀): {path}"); continue; }
            log.Add(AssetDatabase.DeleteAsset(path) ? $"삭제: {path}" : $"실패: {path}");
        }

        // 폴더는 비었을 때만 — 안에 뭔가 남아 있으면 손대지 않는다.
        if (AssetDatabase.IsValidFolder(StrayFolder))
        {
            var left = AssetDatabase.FindAssets("", new[] { StrayFolder });
            if (left.Length == 0)
                log.Add(AssetDatabase.DeleteAsset(StrayFolder) ? $"삭제: {StrayFolder}/" : $"실패: {StrayFolder}/");
            else
                log.Add($"유지: {StrayFolder}/ — 안에 {left.Length}개가 남아 있어 지우지 않았습니다.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var sb = new StringBuilder("[정리] 완료 — 씬 저장 필요\n");
        foreach (var l in log) sb.AppendLine("  " + l);
        Debug.Log(sb.ToString());
    }
}
