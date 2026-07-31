using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// CraftSkill 종류마다 작업대 프리팹 + BuildingData를 만든다.
// **씬은 건드리지 않는다** — `Resources/Data/Buildings/`에 에셋이 있으면 그것만으로
// 건설 목록에 오른다(`DataManager.AllBuildings`).
//
// **작업대는 종류마다 프리팹이 하나씩 필요하다** — BuildingData에는 스킬 칸이 없고
// "무엇을 만드는 작업대인가"는 프리팹의 `CraftingTable.tableType`이 들고 있기 때문이다
// (2026-07-31 (4) 기록과 같은 이유).
//
// 목록은 인스턴스가 아니라 레지스트리에서 온다(`DataManager.GetRecipesBySkill`) — 그래서
// 레시피가 아직 없는 종류(연금술·보석)도 만들어 두면 레시피가 생기는 즉시 채워진다.
//
// 없는 것만 만든다. 일회성 생성기 — 다 쓰면 지운다.
public static class CraftingTableBuildoutCreator
{
    const string MatDir    = "Assets/2_Material";
    const string PrefabDir = "Assets/3_Prefab";
    // 건설 목록은 이 폴더를 스캔한다 — 에셋을 여기 만들기만 하면 메뉴에 오른다.
    const string BuildDir  = "Assets/Resources/Data/Buildings";

    struct Entry
    {
        public CraftSkill    skill;
        public string        file;      // 프리팹/에셋 파일명
        public Color         color;     // 상판 색 — 작업대끼리 구분되게
        public ResourceType  costType;
        public int           costAmount;
    }

    // 이미 있는 셋(무기·용광로·조리)은 목록에 없다 — 프리팹이 씬에서 뜬 것이라 건드리지 않는다.
    static readonly Entry[] Entries =
    {
        new Entry { skill = CraftSkill.ArmorCrafting, file = "CraftingTable_Armor",
                    color = C(0x7E,0x8A,0x99), costType = ResourceType.Wood,     costAmount = 25 },
        new Entry { skill = CraftSkill.Stonecutting,  file = "CraftingTable_Stonecut",
                    color = C(0x8C,0x8A,0x84), costType = ResourceType.Stone,    costAmount = 35 },
        new Entry { skill = CraftSkill.Woodworking,   file = "CraftingTable_Woodwork",
                    color = C(0x9C,0x6B,0x3A), costType = ResourceType.Wood,     costAmount = 30 },
        new Entry { skill = CraftSkill.Alchemy,       file = "CraftingTable_Alchemy",
                    color = C(0x5E,0x8F,0x5A), costType = ResourceType.CutStone, costAmount = 20 },
        new Entry { skill = CraftSkill.Gemcutting,    file = "CraftingTable_Gemcut",
                    color = C(0x8E,0x5A,0x9E), costType = ResourceType.CutStone, costAmount = 25 },
    };

    static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

    [MenuItem("DungeonColony/Build Out Crafting Tables")]
    static void Create()
    {
        var frameMat = EnsureMaterial("Table_Frame", C(0x5A,0x42,0x2E));
        var made     = new List<BuildingData>();
        int newPrefabs = 0, newBuildings = 0;

        foreach (var e in Entries)
        {
            var topMat = EnsureMaterial($"Table_{e.skill}", e.color);

            string prefabPath = $"{PrefabDir}/{e.file}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                prefab = BuildPrefab(e, frameMat, topMat, prefabPath);
                newPrefabs++;
            }

            string buildPath = $"{BuildDir}/{e.file}.asset";
            var building = AssetDatabase.LoadAssetAtPath<BuildingData>(buildPath);
            if (building == null)
            {
                building = ScriptableObject.CreateInstance<BuildingData>();
                building.BuildingName  = e.skill.DisplayName();   // 이름의 단일 출처
                building.Category      = BuildCategory.Building;
                building.Prefab        = prefab;
                building.CostType      = e.costType;
                building.CostAmount    = e.costAmount;
                building.UsesBlueprint = false;
                building.OccupiesTile  = true;   // 실체 건물 — 유닛이 통과하지 못한다
                AssetDatabase.CreateAsset(building, buildPath);
                newBuildings++;
            }
            made.Add(building);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TableBuildout] 완료 — 프리팹 신규 {newPrefabs} / BuildingData 신규 {newBuildings} " +
                  $"(작업대 총 {made.Count}종, 레지스트리 자동 등록)");
    }

    // 다리 넷 + 상판. 루트 y는 0 — SnapOnSurface가 렌더러 바닥면을 표면에 맞춘다.
    static GameObject BuildPrefab(Entry e, Material frame, Material top, string path)
    {
        var root = new GameObject(e.file);
        root.transform.localPosition = Vector3.zero;

        AddBlock(root.transform, "Top", top, new Vector3(0f, 0.62f, 0f), new Vector3(0.94f, 0.14f, 0.94f));
        const float L = 0.36f;
        AddBlock(root.transform, "Leg1", frame, new Vector3( L, 0.28f,  L), new Vector3(0.13f, 0.56f, 0.13f));
        AddBlock(root.transform, "Leg2", frame, new Vector3(-L, 0.28f,  L), new Vector3(0.13f, 0.56f, 0.13f));
        AddBlock(root.transform, "Leg3", frame, new Vector3( L, 0.28f, -L), new Vector3(0.13f, 0.56f, 0.13f));
        AddBlock(root.transform, "Leg4", frame, new Vector3(-L, 0.28f, -L), new Vector3(0.13f, 0.56f, 0.13f));

        // 클릭 판정은 루트 콜라이더 하나로 — 다리 사이 빈 곳을 클릭해도 잡히게 통째로 감싼다.
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.35f, 0f);
        col.size   = new Vector3(1f, 0.7f, 1f);

        var table = root.AddComponent<CraftingTable>();
        var so    = new SerializedObject(table);
        so.FindProperty("tableType").enumValueIndex = (int)e.skill;
        so.ApplyModifiedProperties();

        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    static void AddBlock(Transform parent, string name, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material EnsureMaterial(string name, Color color)
    {
        string path = $"{MatDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = name };
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
