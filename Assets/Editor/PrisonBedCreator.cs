using System.IO;
using UnityEditor;
using UnityEngine;

// **죄수 침대**를 건설 목록의 별개 항목으로 만든다. `DungeonColony → Create Prison Bed`
//
// 전환은 그대로 된다(우클릭 [죄수용으로 바꾼다] / [보통 침대로 되돌린다]) — 이건 **처음부터
// 죄수용으로 지어지는 침대**다. 잡아 오고 나서 방을 하나 돌리는 것과, 애초에 감옥을 짓는 것은
// 다른 일이고 둘 다 필요하다.
//
// **침대 프리팹을 복제해 플래그만 켠다.** 모양·자는 자리(`sleepAnchor`)·작업 자리(`WorkSpot`)가
// 전부 같아야 하는데 새로 만들면 반드시 어긋난다 — 색은 `Bed`가 런타임에 표식으로 칠한다.
//
// **없는 것만 만든다** — 이미 있으면 손대지 않는다(인스펙터에서 값을 손봤을 수 있다).
public static class PrisonBedCreator
{
    const string SourcePrefab = "Assets/3_Prefab/Bed.prefab";
    const string SourceData   = "Assets/Resources/Data/Buildings/Bed.asset";
    const string PrefabPath   = "Assets/3_Prefab/PrisonBed.prefab";
    const string DataPath     = "Assets/Resources/Data/Buildings/PrisonBed.asset";

    [MenuItem("DungeonColony/Create Prison Bed")]
    static void Create()
    {
        var srcData = AssetDatabase.LoadAssetAtPath<BuildingData>(SourceData);
        if (srcData == null) { Debug.LogError($"[PrisonBed] {SourceData} 가 없습니다."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
        if (prefab == null) return;

        var data = AssetDatabase.LoadAssetAtPath<BuildingData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BuildingData>();
            data.BuildingName = "죄수 침대";
            data.Description  = "붙잡아 온 사람을 눕히는 자리. 우리와 달리 회복과 치료가 붙는다 — " +
                                "살려서 다룰 사람은 여기에. 우클릭으로 보통 침대와 서로 바꿀 수 있다.";
            data.Category  = srcData.Category;
            data.SortOrder = srcData.SortOrder + 1;   // 보통 침대 바로 뒤에 선다
            data.Size      = srcData.Size;

            // **값은 같다.** 자물쇠를 다는 것이 아니라 용도를 정하는 것뿐이라,
            // 더 받으면 "왜 같은 침대가 비싸지"가 된다.
            data.CostItem      = srcData.CostItem;
            data.CostAmount    = srcData.CostAmount;
            data.UsesBlueprint = srcData.UsesBlueprint;
            data.IsFloor       = srcData.IsFloor;
            data.OccupiesTile  = srcData.OccupiesTile;
            data.RequiredResearch = srcData.RequiredResearch;

            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.CreateAsset(data, DataPath);
        }

        if (data.Prefab == null) { data.Prefab = prefab; EditorUtility.SetDirty(data); }

        // 프리팹의 `PlacedBuilding.data`가 **제 것**을 가리키게 한다 — 복제본이라 원본(보통
        // 침대)을 물고 있으면 분해 환불도 정보 패널도 엉뚱한 것을 답한다.
        WireData(prefab, data);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = data;
        Debug.Log("[PrisonBed] 죄수 침대 프리팹 + BuildingData 준비 완료. " +
                  "건설 목록은 레지스트리에서 오므로 따로 등록할 것이 없습니다.");
    }

    static GameObject BuildPrefab()
    {
        if (!File.Exists(SourcePrefab)) { Debug.LogError($"[PrisonBed] {SourcePrefab} 가 없습니다."); return null; }
        if (!AssetDatabase.CopyAsset(SourcePrefab, PrefabPath))
        {
            Debug.LogError("[PrisonBed] 침대 프리팹 복제 실패.");
            return null;
        }
        AssetDatabase.Refresh();

        // **제자리 수정** — 같은 경로에 새로 저장하면 루트 fileID가 바뀌어 참조가 끊긴다.
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var bed = root.GetComponent<Bed>();
            if (bed == null) { Debug.LogError("[PrisonBed] 복제본에 Bed 컴포넌트가 없습니다."); return null; }

            var so = new SerializedObject(bed);
            so.FindProperty("forPrisoners").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    static void WireData(GameObject prefab, BuildingData data)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var pb = root.GetComponent<PlacedBuilding>() ?? root.AddComponent<PlacedBuilding>();
            var so = new SerializedObject(pb);
            var prop = so.FindProperty("data");
            if (prop.objectReferenceValue == data) return;

            prop.objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
