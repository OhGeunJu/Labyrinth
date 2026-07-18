using UnityEditor;
using UnityEngine;

// DungeonColony/Create Dungeon Starter Floor 실행
// → 씬의 DungeonCore(없으면 선택 오브젝트/원점) 주변에 코어바닥 슬래브 생성.
// 첫 던전 맵 저작용: 공허 씬(지형 없음)에 코어 + 이 바닥만 있으면 시작 가능.
// 슬래브 하나가 Floor(autoFromRenderer)로 자기 범위 타일 전부를 _floored에 등록하고,
// BoxCollider가 유닛 접지 + 배치/이동 레이캐스트 대상이 된다. 이후 확장은 빌드모드 바닥 페인트.
public static class DungeonStarterCreator
{
    const string MatPath = "Assets/2_Material/FloorMat.mat"; // FloorCreator와 공유
    const int    SizeTiles = 20;   // 코어바닥 한 변(타일). 짝수 유지 — 타일 경계 정렬 전제.
    const float  Thickness = 0.05f;

    [MenuItem("DungeonColony/Create Dungeon Starter Floor")]
    static void Create()
    {
        // 중심: DungeonCore > 선택 오브젝트 > 원점
        Vector3 center = Vector3.zero;
        var core = Object.FindFirstObjectByType<DungeonCore>();
        if (core != null) center = core.transform.position;
        else if (Selection.activeTransform != null) center = Selection.activeTransform.position;

        // 타일 경계 정렬: 짝수 크기 슬래브의 중심을 정수 좌표에 스냅
        // → Renderer 바운드 min/max가 정수 경계에 떨어져 Floor가 정확히 Size×Size 타일 등록.
        center = new Vector3(Mathf.Round(center.x), 0f, Mathf.Round(center.z));

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(sh);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.30f, 0.30f, 0.34f));
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     new Color(0.30f, 0.30f, 0.34f));
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "[DungeonStarterFloor]";
        go.transform.position   = center;                                      // 슬래브 상면 ≈ y+0.025 (배치 바닥과 동일)
        go.transform.localScale = new Vector3(SizeTiles, Thickness, SizeTiles);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.AddComponent<Floor>(); // autoFromRenderer 기본값 → 런타임에 범위 타일 자동 등록

        Undo.RegisterCreatedObjectUndo(go, "Create Dungeon Starter Floor");
        Selection.activeGameObject = go;

        // 던전형 맵 플래그: 바닥 0장이어도 "건물은 바닥 위에만" 게이트가 걸리도록.
        var map = Object.FindFirstObjectByType<MapManager>();
        if (map != null)
        {
            var so = new SerializedObject(map);
            var prop = so.FindProperty("requireFloor");
            if (prop != null && !prop.boolValue)
            {
                prop.boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(map);
                Debug.Log("[DungeonStarterCreator] MapManager.requireFloor 켬 — 건물은 바닥 위에만 배치됩니다.");
            }
        }

        Debug.Log($"[DungeonStarterCreator] 코어바닥 생성 — 중심 {center}, {SizeTiles}×{SizeTiles}타일. " +
                  "공허 던전 씬이면 지형/그라운드 오브젝트를 제거하고, 코어·유닛을 이 바닥 위에 배치하세요.");
    }
}
