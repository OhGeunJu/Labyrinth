using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 작업대·연구대 프리팹에 **작업 자리(`WorkSpot`)** 를 붙인다.
//
// 자리는 건물 앞(-Z, 1칸)에 둔다 — 작업대는 대개 앞을 보고 쓰는 물건이고, 여기서 정확한 값을
// 고르는 것보다 **어디든 하나 정해 두고 인스펙터에서 옮기는 것**이 낫다(모양은 씬·프리팹이 갖는다).
//
// 표식은 건설 모드에서만 켜진다(`WorkSpot`이 `BuildModeManager.OnActiveChanged`를 구독).
// 그래서 건물을 놓을 때 **다른 건물의 작업 자리가 다 보이고**, 그 칸을 막는 배치를 눈으로 피할 수 있다.
public static class WorkSpotSetup
{
    const string SpotName   = "WorkSpot";
    const string MarkerName = "Marker";
    const string MaterialPath = "Assets/2_Material/WorkSpotMarker.mat";

    static readonly Color MarkerColor = new Color(0.3f, 0.8f, 1f, 0.45f);

    [MenuItem("DungeonColony/Setup Work Spots")]
    static void Setup()
    {
        // **열기 전에 거른다.** `LoadPrefabContents`는 프리팹을 임시 씬에 실제로 펼치므로
        // `RequireComponent` 같은 것이 발동해 콘솔이 시끄러워지고(예: "Creating missing
        // LayoutElement…"), 30개를 열어 9개를 찾는 낭비도 된다. 에셋을 그냥 읽어 판별만 한다.
        var targets = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/3_Prefab" }))
        {
            string path  = AssetDatabase.GUIDToAssetPath(guid);
            var    asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) continue;

            // 작업 자리가 필요한 건 폰이 붙어서 쓰는 건물뿐이다.
            bool needs = asset.GetComponentInChildren<CraftingTable>(true) != null
                      || asset.GetComponentInChildren<ResearchTable>(true) != null;
            if (needs) targets.Add(path);
        }

        var material = EnsureMaterial();
        int made = 0, skipped = 0;

        foreach (var path in targets)
        {
            // **제자리 수정** — 같은 경로에 새로 저장하면 루트 fileID가 바뀌어
            // `BuildingData.Prefab` 같은 참조가 끊긴다(docs/editor_creators.md).
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var existing = root.GetComponentInChildren<WorkSpot>(true);
                string name  = System.IO.Path.GetFileNameWithoutExtension(path);

                if (existing != null)
                {
                    // 자리(위치)는 손대지 않는다 — 인스펙터에서 옮겨 뒀을 수 있다.
                    // **표식만** 지금 모양(화살표)으로 갈아 끼운다.
                    RebuildMarker(existing, material);
                    skipped++;
                    Debug.Log($"[WorkSpotSetup] {name} — 자리는 그대로, 표식만 갱신");
                }
                else
                {
                    CreateSpot(root, material);
                    made++;
                    Debug.Log($"[WorkSpotSetup] {name} 에 작업 자리 추가");
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[WorkSpotSetup] 완료 — 추가 {made}개, 이미 있음 {skipped}개. " +
                  "자리는 건물 앞 1칸입니다. 인스펙터에서 옮기세요(건설 모드에서 표식이 보입니다).");
    }

    // 이미 있는 작업 자리의 표식만 다시 만든다(자리는 저작물이라 안 건드린다).
    static void RebuildMarker(WorkSpot spot, Material material)
    {
        var old = spot.transform.Find(MarkerName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var marker = BuildMarker(spot.transform, material);

        var so = new SerializedObject(spot);
        so.FindProperty("marker").objectReferenceValue = marker;
        so.ApplyModifiedProperties();
    }

    static void CreateSpot(GameObject root, Material material)
    {
        var spotGO = new GameObject(SpotName);
        spotGO.transform.SetParent(root.transform, false);
        spotGO.transform.localPosition = new Vector3(0f, 0f, -1f);   // 건물 앞 1칸

        var marker = BuildMarker(spotGO.transform, material);

        var spot = spotGO.AddComponent<WorkSpot>();
        var so   = new SerializedObject(spot);
        so.FindProperty("marker").objectReferenceValue = marker;
        so.ApplyModifiedProperties();
    }

    // 표식은 **화살표**다. 사각형은 "여기 뭔가 있다"까지만 말하는데, 알아야 하는 것은
    // **어느 쪽을 보고 일하는가**다 — 화살표가 작업 자리에서 건물 쪽(+Z)을 가리킨다.
    // 콜라이더는 애초에 만들지 않는다 — 있으면 `SelectionManager`의 단일 레이캐스트가
    // 표식을 맞고 건물을 못 찾는다.
    static GameObject BuildMarker(Transform parent, Material material)
    {
        var marker = new GameObject(MarkerName, typeof(MeshFilter), typeof(MeshRenderer));
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        marker.GetComponent<MeshFilter>().sharedMesh = EnsureArrowMesh();

        var renderer = marker.GetComponent<MeshRenderer>();
        renderer.sharedMaterial    = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows    = false;

        marker.SetActive(false);   // 건설 모드에서만 켜진다
        return marker;
    }

    const string MeshPath = "Assets/3_Prefab/WorkSpotArrow.asset";

    // XZ 평면에 누운 **+Z를 가리키는 화살표**. 자루(사각형) + 머리(삼각형).
    // 법선이 +Y이려면 위에서 봤을 때의 감는 방향이 정해져 있다 — 아래 순서가 그것이다.
    static Mesh EnsureArrowMesh()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing != null) return existing;

        const float shaftHalf = 0.09f;   // 자루 반폭
        const float headHalf  = 0.26f;   // 머리 반폭
        const float back      = -0.42f;  // 꼬리
        const float neck      =  0.04f;  // 자루와 머리가 만나는 곳
        const float tip       =  0.44f;  // 끝

        var vertices = new[]
        {
            new Vector3(-shaftHalf, 0f, back),   // 0
            new Vector3(-shaftHalf, 0f, neck),   // 1
            new Vector3( shaftHalf, 0f, neck),   // 2
            new Vector3( shaftHalf, 0f, back),   // 3
            new Vector3(-headHalf,  0f, neck),   // 4
            new Vector3( 0f,        0f, tip),    // 5
            new Vector3( headHalf,  0f, neck),   // 6
        };

        var mesh = new Mesh { name = "WorkSpotArrow" };
        mesh.vertices  = vertices;
        mesh.triangles = new[] { 0, 1, 2,  0, 2, 3,  4, 5, 6 };
        mesh.normals   = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                                 Vector3.up, Vector3.up, Vector3.up };
        mesh.RecalculateBounds();

        AssetDatabase.CreateAsset(mesh, MeshPath);
        return mesh;
    }

    static Material EnsureMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) return existing;

        // URP 우선, 없으면 빌트인. `Shader.Find`가 실패하면 분홍 덩어리가 되므로 폴백을 둔다.
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader) { name = "WorkSpotMarker" };
        mat.SetColor("_BaseColor", MarkerColor);
        mat.SetColor("_Color",     MarkerColor);

        // 반투명으로 — 바닥 위에 얹히는 표식이라 불투명하면 지형을 가린다.
        mat.SetFloat("_Surface", 1f);           // URP: Transparent
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        System.IO.Directory.CreateDirectory("Assets/2_Material");
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }
}
