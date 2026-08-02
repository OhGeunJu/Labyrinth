using System.IO;
using UnityEditor;
using UnityEngine;

// 새 건물을 **프리팹 + `BuildingData`** 한 쌍으로 만든다.
//
// 건설 목록은 레지스트리(`Resources/Data/Buildings/`)에서 오므로, 여기에 에셋을 놓는 것만으로
// B키 메뉴에 오른다 — 씬은 건드리지 않는다.
//
// **없는 것만 만든다.** 이미 있으면 손대지 않는다(인스펙터에서 값을 손봤을 수 있다).
public static class NewBuildingCreator
{
    const string PrefabDir = "Assets/3_Prefab";
    const string DataDir   = "Assets/Resources/Data/Buildings";
    const string MatDir    = "Assets/2_Material";
    const int    WallLayer = 7;   // ProjectSettings/TagManager: 7 = Wall (길찾기 장애물 판정 축)

    [MenuItem("DungeonColony/Create New Buildings")]
    static void Create()
    {
        int made = 0;

        made += CreateWall()        ? 1 : 0;
        made += CreateLargeStorage()? 1 : 0;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[NewBuildingCreator] 새 건물 {made}종 생성 (이미 있는 것은 건너뜀). " +
                  "B키 목록에 바로 뜹니다 — 씬은 안 건드렸습니다.");
    }

    // ── 벽 ────────────────────────────────────────────────────────────────
    // 길을 막는 유일한 수단이다. **레이어가 핵심** — `HierarchicalPathfinder.obstacleLayer`가
    // Wall만 보고 장애물 격자를 굽는다. 레이어를 안 넣으면 벽을 세워도 폰이 그냥 지나간다.
    static bool CreateWall()
    {
        const string name = "Wall";
        if (Exists(name)) return false;

        var root = new GameObject(name);
        root.layer = WallLayer;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        body.transform.localScale    = new Vector3(0.96f, 2.0f, 0.96f);
        body.layer = WallLayer;
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().sharedMaterial =
            EnsureMaterial(new Color(0.42f, 0.41f, 0.39f));

        // 콜라이더는 **루트에 하나** — 조각마다 달면 `SelectionManager`의 단일 레이캐스트가
        // 조각을 맞고 건물을 못 찾는다(규약).
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 1.0f, 0f);
        col.size   = new Vector3(0.96f, 2.0f, 0.96f);

        var prefab = SavePrefab(root, name);
        MakeData(name, "벽", prefab, BuildCategory.Building, "Res_Stone", 5,
                 Vector2Int.one, occupies: true, sortOrder: 15);
        return true;
    }

    // ── 큰 창고 ───────────────────────────────────────────────────────────
    // 기본 창고(10×10 격자)보다 넓다. **격자 인벤토리라 한계는 개수가 아니라 칸**이므로
    // 큰 물건을 많이 넣으려면 이쪽이 필요하다.
    static bool CreateLargeStorage()
    {
        const string name = "LargeStorage";
        if (Exists(name)) return false;

        var root = new GameObject(name);

        var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Crate";
        crate.transform.SetParent(root.transform, false);
        crate.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        crate.transform.localScale    = new Vector3(2.8f, 0.7f, 1.8f);
        Object.DestroyImmediate(crate.GetComponent<Collider>());
        crate.GetComponent<MeshRenderer>().sharedMaterial =
            EnsureMaterial(new Color(0.46f, 0.38f, 0.28f));

        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "Lid";
        lid.transform.SetParent(root.transform, false);
        lid.transform.localPosition = new Vector3(0f, 0.74f, 0f);
        lid.transform.localScale    = new Vector3(2.9f, 0.1f, 1.9f);
        Object.DestroyImmediate(lid.GetComponent<Collider>());
        lid.GetComponent<MeshRenderer>().sharedMaterial =
            EnsureMaterial(new Color(0.34f, 0.27f, 0.19f));

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.4f, 0f);
        col.size   = new Vector3(2.9f, 0.8f, 1.9f);

        // 인벤토리를 먼저 붙여 크기를 잡는다 — `Storage.Awake`가 없으면 스스로 붙이는데
        // 그때는 기본 크기(8×10)가 되어 "큰 창고"가 아니게 된다.
        var inv   = root.AddComponent<Inventory>();
        var invSO = new SerializedObject(inv);
        invSO.FindProperty("width").intValue  = 16;
        invSO.FindProperty("height").intValue = 12;
        invSO.ApplyModifiedProperties();

        root.AddComponent<Storage>();

        var prefab = SavePrefab(root, name);
        MakeData(name, "큰 창고", prefab, BuildCategory.Building, "Res_Wood", 40,
                 new Vector2Int(3, 2), occupies: false, sortOrder: 25);
        return true;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────
    static bool Exists(string name) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab") != null;

    static GameObject SavePrefab(GameObject root, string name)
    {
        Directory.CreateDirectory(PrefabDir);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void MakeData(string assetName, string display, GameObject prefab,
                         BuildCategory category, string costItemId, int costAmount,
                         Vector2Int size, bool occupies, int sortOrder)
    {
        Directory.CreateDirectory(DataDir);
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.BuildingName = display;
        data.Category     = category;
        data.SortOrder    = sortOrder;
        data.CostItem     = LoadItem(costItemId);
        data.CostAmount   = costAmount;
        data.Prefab       = prefab;
        data.Size         = size;
        data.OccupiesTile = occupies;
        // Id는 비워 둔다 — `DataId.Resolve`가 에셋 파일명을 쓴다(프로젝트 관례).

        AssetDatabase.CreateAsset(data, $"{DataDir}/{assetName}.asset");
    }

    // 비용 자원을 **에셋 파일명(= 식별자)** 으로 찾는다. 예전엔 `ResourceType` enum이라
    // 컴파일러가 잡아 줬는데, 이제 참조라 없는 이름을 적으면 조용히 비용 없는 건물이 된다 — 알린다.
    static ItemData LoadItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/Resources/Data/Items/{id}.asset");
        if (item == null) Debug.LogWarning($"[NewBuildingCreator] 비용 자원 '{id}'를 못 찾았습니다 — 비용 없이 만듭니다.");
        return item;
    }

    static Material EnsureMaterial(Color color)
    {
        string path = $"{MatDir}/Bld_{ColorUtility.ToHtmlStringRGB(color)}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color",     color);

        Directory.CreateDirectory(MatDir);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
