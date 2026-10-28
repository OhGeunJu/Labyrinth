using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// **층 지도 2~4층을 만든다**(2026-10-01, `Floors` · `WorldMap.upperFloors`) — 세계가 층 넷을 2×2로 나란히 둔 한 평면이
// 되면서 층마다 지도가 한 장씩 필요하다. 1층 지도(`mapData`)와 같은 크기로 만들고, 바이옴은 비워 둔다 —
// **까는 것은 `Terrain → 8. Generate Biome Map`**(층마다 `BiomeContentData.Tier`가 후보를 정한다).
//
// **있으면 안 건드린다** — 손으로 칠한 층 지도를 다시 돌릴 때 날리면 안 된다(크기만 1층에 맞춘다). 몇 번 돌려도 같다.
public static class FloorMapsCreator
{
    [MenuItem("DungeonColony/World/0d. Create Floor Maps (층 지도 2~4)")]
    public static void Create()
    {
        var wm = Object.FindFirstObjectByType<WorldMap>(FindObjectsInactive.Include);
        if (wm == null) { Debug.LogError("[층 지도] 씬에 `WorldMap`이 없다."); return; }

        var so = new SerializedObject(wm);
        var first = so.FindProperty("mapData").objectReferenceValue as WorldMapData;
        if (first == null) { Debug.LogError("[층 지도] `WorldMap.mapData`(1층)가 비어 있다."); return; }

        if (first.Floor != 1)
        {
            Undo.RecordObject(first, "1층 지도");
            first.Floor = 1;
            EditorUtility.SetDirty(first);
        }

        string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(first)).Replace('\\', '/');
        var list = so.FindProperty("upperFloors");
        int made = 0, kept = 0;

        for (int f = 2; f <= Floors.Count; f++)
        {
            string path = $"{dir}/WorldMapData_Floor{f}.asset";
            var data = AssetDatabase.LoadAssetAtPath<WorldMapData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WorldMapData>();
                data.Floor = f;
                data.Resize(first.Width, first.Height);
                data.Fill(Biome.Unknown);
                data.ClearContent();
                data.StartNode = new Vector2Int(first.Width / 2, first.Height / 2);
                data.HandPainted = false;
                AssetDatabase.CreateAsset(data, path);
                made++;
            }
            else
            {
                kept++;
                if (data.Floor != f || data.Width != first.Width || data.Height != first.Height)
                {
                    Undo.RecordObject(data, "층 지도 맞추기");
                    data.Floor = f;
                    if (data.Width != first.Width || data.Height != first.Height) data.Resize(first.Width, first.Height);
                    EditorUtility.SetDirty(data);
                }
            }

            // 목록에 없으면 단다(순서는 상관없다 — `Floor`가 자리를 정한다).
            bool has = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) { has = true; break; }
            if (!has)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
            }
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        TerrainGrid.ForgetRegions();
        TerrainHeight.ForgetBiomes();
        EditorSceneManager.MarkSceneDirty(wm.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[층 지도] 2~{Floors.Count}층 — 새로 {made} · 있던 것 {kept}. 지역 {TerrainGrid.RegionSize}m · 세계 {TerrainGrid.WorldSize}m.\n" +
                  "다음: `Terrain → 8. Generate Biome Map`(층 넷을 다 깐다).");
    }
}
