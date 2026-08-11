using UnityEditor;
using UnityEngine;

// 건물 프리팹에 `PlacedBuilding` + `data`를 미리 붙인다.
// `DungeonColony → Fix: Attach PlacedBuilding To Prefabs`
//
// **없으면 씬에 손으로 놓은 건물이 "건물"로 인식되지 않는다.** 배치로 지은 것은
// `BuildModeManager`/`BuildingBlueprint`가 `Attach`를 부르지만, 에디터에서 끌어다 놓은 것은
// 아무도 안 불러 준다 — 그래서
//  · `PlacedBuilding.Start`의 **발자국 등록이 안 돌고**(길찾기가 아는 건 콜라이더뿐이라
//    모델 한복판을 가로지르는 경로가 나온다. 2026-08-08 연구대 사건),
//  · 좌클릭해도 **어느 건물인지 알 수 없어** 정보 패널에 이름·설명·[이동]·[분해]가 안 뜬다.
//
// CLAUDE.md가 "건물 프리팹에는 `PlacedBuilding` + `data`를 미리 붙여 둘 것"이라고 적어 둔
// 그것을 실제로 붙이는 도구다.
//
// **레지스트리에서 거꾸로 찾는다** — `BuildingData.Prefab`이 프리팹을 가리키므로, 그 짝을
// 그대로 뒤집으면 프리팹마다 어느 데이터인지가 나온다(손으로 짝지으면 반드시 어긋난다).
public static class PlacedBuildingAttacher
{
    [MenuItem("DungeonColony/Fix: Attach PlacedBuilding To Prefabs")]
    static void Run()
    {
        var datas = Resources.LoadAll<BuildingData>("Data/Buildings");
        if (datas == null || datas.Length == 0)
        {
            Debug.LogError("[PlacedBuildingAttacher] Resources/Data/Buildings 에서 BuildingData를 못 찾았습니다.");
            return;
        }

        int attached = 0, wired = 0, skipped = 0, noPrefab = 0;

        foreach (var data in datas)
        {
            if (data == null) continue;
            if (data.Prefab == null) { noPrefab++; continue; }

            string path = AssetDatabase.GetAssetPath(data.Prefab);
            if (string.IsNullOrEmpty(path)) { noPrefab++; continue; }

            // **제자리 수정** — 같은 경로에 새로 저장하면 GUID가 같아도 루트 fileID가 바뀌어
            // `BuildingData.Prefab` 참조가 끊긴다(`docs/editor_creators.md`).
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pb = root.GetComponent<PlacedBuilding>();
                bool changed = false;

                if (pb == null) { pb = root.AddComponent<PlacedBuilding>(); attached++; changed = true; }

                var so   = new SerializedObject(pb);
                var prop = so.FindProperty("data");
                if (prop.objectReferenceValue != data)
                {
                    prop.objectReferenceValue = data;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    wired++;
                    changed = true;
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                else         skipped++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PlacedBuildingAttacher] 컴포넌트 추가 {attached} · data 연결 {wired} · " +
                  $"이미 됨 {skipped} · 프리팹 없음 {noPrefab}\n" +
                  "씬에 이미 놓인 인스턴스는 프리팹을 따라 자동으로 갱신됩니다. " +
                  "이어서 `Snap Placed Buildings To Grid`를 돌려 격자에 맞추세요.");
    }
}
