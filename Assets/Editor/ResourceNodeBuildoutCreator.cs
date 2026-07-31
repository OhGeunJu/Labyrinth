using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 5_Data/Resource 의 Node_* 데이터에 대해 머티리얼 + 프리팹 + BuildingData를 만든다.
// **씬은 건드리지 않는다** — `Resources/Data/Buildings/`에 에셋이 있으면 그것만으로
// 건설 목록에 오른다(`DataManager.AllBuildings`).
//
// **없는 것만 만든다** — 이미 있는 에셋은 손대지 않으므로 여러 번 실행해도 안전하다
// (인스펙터에서 모양·색을 손봤을 수 있다).
//
// 일회성 생성기 — 산출물이 자리 잡으면 지운다(docs/editor_creators.md "생성기의 수명").
public static class ResourceNodeBuildoutCreator
{
    const string MatDir    = "Assets/2_Material";
    const string PrefabDir = "Assets/3_Prefab";
    const string NodeDir   = "Assets/5_Data/Resource";
    // 건설 목록은 이 폴더를 스캔한다 — 에셋을 여기 만들기만 하면 메뉴에 오른다.
    const string BuildDir  = "Assets/Resources/Data/Buildings";

    enum Shape { Vein, Crystal }

    struct Entry
    {
        public string asset;      // Node_* 데이터 파일명
        public string display;    // 건설 목록에 뜨는 이름
        public Color  color;
        public Shape  shape;
    }

    // 광맥은 바위에 광석이 박힌 덩어리, 결정은 받침에서 솟은 기둥 — 한눈에 갈리게 형태를 나눴다.
    static readonly Entry[] Entries =
    {
        new Entry { asset = "Node_IronVein",     display = "철 광맥",       color = C(0x6E,0x62,0x57), shape = Shape.Vein    },
        new Entry { asset = "Node_CopperVein",   display = "구리 광맥",     color = C(0xB8,0x73,0x33), shape = Shape.Vein    },
        new Entry { asset = "Node_TinVein",      display = "주석 광맥",     color = C(0x9A,0xA3,0xA8), shape = Shape.Vein    },
        new Entry { asset = "Node_CoalSeam",     display = "석탄층",        color = C(0x2B,0x2B,0x2E), shape = Shape.Vein    },
        new Entry { asset = "Node_SilverVein",   display = "은 광맥",       color = C(0xC8,0xCD,0xD4), shape = Shape.Vein    },
        new Entry { asset = "Node_GoldVein",     display = "금 광맥",       color = C(0xD9,0xA5,0x21), shape = Shape.Vein    },
        new Entry { asset = "Node_RubyVein",     display = "루비 광맥",     color = C(0xC2,0x2B,0x3F), shape = Shape.Crystal },
        new Entry { asset = "Node_MagicCrystal", display = "마력석 결정",   color = C(0x34,0xD1,0xC0), shape = Shape.Crystal },
        new Entry { asset = "Node_EtherCrystal", display = "에테르석 결정", color = C(0xF2,0xE6,0xB8), shape = Shape.Crystal },
        new Entry { asset = "Node_VoidCrystal",  display = "공허석 결정",   color = C(0x6B,0x3F,0xA0), shape = Shape.Crystal },
    };

    static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

    [MenuItem("DungeonColony/Build Out Resource Nodes")]
    static void Create()
    {
        var rockMat = EnsureMaterial("Node_Rock", C(0x4A,0x47,0x44), metallic: 0f, smoothness: 0.1f);
        var made    = new List<BuildingData>();
        int newPrefabs = 0, newBuildings = 0;

        foreach (var e in Entries)
        {
            var nodeData = AssetDatabase.LoadAssetAtPath<ResourceNodeData>($"{NodeDir}/{e.asset}.asset");
            if (nodeData == null) { Debug.LogWarning($"[NodeBuildout] {e.asset} 데이터가 없습니다 — 건너뜀"); continue; }

            // 금속은 반짝이게, 결정은 매끈하게 — 형태만으로는 안 갈리는 재질감을 색과 함께 준다.
            bool metal = e.shape == Shape.Vein;
            var  mat   = EnsureMaterial(e.asset, e.color,
                                        metallic:   metal ? 0.8f : 0.2f,
                                        smoothness: metal ? 0.45f : 0.85f);

            string prefabPath = $"{PrefabDir}/{e.asset}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                prefab = BuildPrefab(e, nodeData, mat, rockMat, prefabPath);
                newPrefabs++;
            }

            // 데이터의 Prefab 칸이 비어 있으면 채운다 (BuildModeManager 기본 프리팹 폴백 방지)
            if (nodeData.Prefab == null)
            {
                nodeData.Prefab = prefab;
                EditorUtility.SetDirty(nodeData);
            }

            string buildPath = $"{BuildDir}/{e.asset}.asset";
            var building = AssetDatabase.LoadAssetAtPath<BuildingData>(buildPath);
            if (building == null)
            {
                building = ScriptableObject.CreateInstance<BuildingData>();
                building.BuildingName  = e.display;
                building.Category      = BuildCategory.Resource;
                building.Prefab        = prefab;
                building.CostType      = ResourceType.Stone;
                building.CostAmount    = 0;      // StoneBuilding과 같은 무료 배치 (샌드박스 성격)
                building.UsesBlueprint = false;
                building.OccupiesTile  = true;   // 노드는 유닛이 통과하지 못한다 (TreeBuilding과 동일)
                AssetDatabase.CreateAsset(building, buildPath);
                newBuildings++;
            }
            made.Add(building);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 씬 배열에 등록하지 않는다 — `Resources/Data/Buildings/`에 있으면 그것만으로 목록에 오른다.
        Debug.Log($"[NodeBuildout] 완료 — 프리팹 신규 {newPrefabs} / BuildingData 신규 {newBuildings} " +
                  $"(건설 목록 총 {made.Count}종, 레지스트리 자동 등록)");
    }

    // ── 프리팹 조립 ───────────────────────────────────────────────────────
    // 루트의 localPosition.y는 **0으로 둔다.** 양수면 BuildModeManager가 "저작 부양 높이"로
    // 보고 그만큼 띄우는데(CLAUDE.md), 조각마다 높이가 달라 하드코딩이 맞지 않는다.
    // 0이면 SnapOnSurface가 렌더러 바닥면을 표면에 맞춰준다.
    static GameObject BuildPrefab(Entry e, ResourceNodeData data, Material oreMat, Material rockMat, string path)
    {
        var root = new GameObject(e.asset);
        root.transform.localPosition = Vector3.zero;

        if (e.shape == Shape.Vein) BuildVein(root.transform, oreMat, rockMat);
        else                       BuildCrystal(root.transform, oreMat, rockMat);

        // 클릭·길찾기용 콜라이더는 루트에 하나. 조각마다 달면 레이캐스트가 지저분해진다.
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.45f, 0f);
        col.size   = new Vector3(1f, 0.9f, 1f);

        var node = root.AddComponent<ResourceNode>();
        var so   = new SerializedObject(node);
        so.FindProperty("data").objectReferenceValue = data;
        so.ApplyModifiedProperties();

        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    // 낮고 넓은 바위 + 박힌 광석 조각들
    static void BuildVein(Transform parent, Material ore, Material rock)
    {
        AddBlock(parent, "Rock",  rock, new Vector3(0f, 0.22f, 0f),        new Vector3(0.92f, 0.44f, 0.92f), 0f);
        AddBlock(parent, "Rock2", rock, new Vector3(-0.16f, 0.52f, 0.1f),  new Vector3(0.5f, 0.3f, 0.5f),   22f);
        AddBlock(parent, "Ore1",  ore,  new Vector3(0.22f, 0.46f, -0.14f), new Vector3(0.34f, 0.3f, 0.34f), 35f);
        AddBlock(parent, "Ore2",  ore,  new Vector3(-0.05f, 0.7f, -0.2f),  new Vector3(0.24f, 0.22f, 0.24f), -18f);
        AddBlock(parent, "Ore3",  ore,  new Vector3(0.3f, 0.24f, 0.28f),   new Vector3(0.26f, 0.24f, 0.26f), 8f);
    }

    // 납작한 받침 + 솟은 결정 기둥들 (45도로 돌려 마름모 실루엣이 나오게)
    static void BuildCrystal(Transform parent, Material ore, Material rock)
    {
        AddBlock(parent, "Base",    rock, new Vector3(0f, 0.1f, 0f),        new Vector3(0.86f, 0.2f, 0.86f), 0f);
        AddBlock(parent, "Shard",   ore,  new Vector3(0f, 0.55f, 0f),       new Vector3(0.3f, 0.9f, 0.3f),  45f);
        AddBlock(parent, "Shard2",  ore,  new Vector3(0.26f, 0.36f, 0.14f), new Vector3(0.2f, 0.52f, 0.2f), 45f, tiltZ: 16f);
        AddBlock(parent, "Shard3",  ore,  new Vector3(-0.22f, 0.3f, -0.2f), new Vector3(0.17f, 0.4f, 0.17f), 45f, tiltZ: -20f);
    }

    static void AddBlock(Transform parent, string name, Material mat,
                         Vector3 pos, Vector3 scale, float yaw, float tiltZ = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        // 조각 콜라이더는 지운다 — 루트 BoxCollider 하나로 통일.
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, tiltZ);
        go.transform.localScale    = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ── 머티리얼 ──────────────────────────────────────────────────────────
    static Material EnsureMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{MatDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        // 프로젝트의 기존 머티리얼이 전부 URP/Lit이다. 못 찾으면 표준 셰이더로 폴백.
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = name };
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
