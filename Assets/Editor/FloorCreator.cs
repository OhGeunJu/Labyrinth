using UnityEditor;
using UnityEngine;

// DungeonColony/Create Floor Prefab + Data 실행
// → 바닥 프리팹(1×1 얇은 슬래브 + Floor 컴포넌트 + BoxCollider) 저장
//   + BuildingData(IsFloor, 무료) 에셋 생성 + 씬 BuildModeManager.placeableBuildings에 자동 추가.
// 바닥은 코어 무관 substrate(모델 A). Collider는 공허에서 배치 레이캐스트가 바닥 위에서만 걸리게 함.
public static class FloorCreator
{
    const string PrefabPath = "Assets/3_Prefab/Floor.prefab";
    const string MatPath    = "Assets/2_Material/FloorMat.mat";
    const string DataPath   = "Assets/5_Data/Building/Floor.asset";

    [MenuItem("DungeonColony/Create Floor Prefab + Data")]
    static void Create()
    {
        // ── 머티리얼 (던전 바닥 느낌의 어두운 회색)
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(sh);
            SetColor(mat, new Color(0.30f, 0.30f, 0.34f));
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        // ── 바닥 GameObject: 1×1 얇은 슬래브(Cube 스케일) → 축 정렬 BoxCollider가 자동으로 맞음
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Floor";
        go.transform.localScale = new Vector3(1f, 0.05f, 1f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.AddComponent<Floor>();   // 배치/사전배치 시 자기 타일을 MapManager._floored에 등록

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        // ── BuildingData (바닥, 무료)
        var data = AssetDatabase.LoadAssetAtPath<BuildingData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BuildingData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }
        data.BuildingName  = "바닥";
        data.IsFloor       = true;
        data.CostAmount    = 0;       // 무료
        data.UsesBlueprint = false;
        data.Prefab        = prefab;
        EditorUtility.SetDirty(data);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TryWireIntoBuildMode(data);
        Debug.Log($"[FloorCreator] 바닥 프리팹({PrefabPath}) + 데이터({DataPath}) 생성 완료");
    }

    // 씬에 BuildModeManager가 있으면 placeableBuildings 배열에 바닥 데이터 추가(중복 방지).
    static void TryWireIntoBuildMode(BuildingData data)
    {
        var bm = Object.FindFirstObjectByType<BuildModeManager>();
        if (bm == null)
        {
            Debug.Log("[FloorCreator] 씬에 BuildModeManager 없음 — placeableBuildings에 수동 추가 필요.");
            return;
        }

        var so  = new SerializedObject(bm);
        var arr = so.FindProperty("placeableBuildings");
        if (arr == null) return;

        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).objectReferenceValue == data) return; // 이미 있음

        arr.InsertArrayElementAtIndex(arr.arraySize);
        arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = data;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(bm);
        Debug.Log("[FloorCreator] BuildModeManager.placeableBuildings에 바닥 추가.");
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
    }
}
