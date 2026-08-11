using System.IO;
using UnityEditor;
using UnityEngine;

// **우리** 프리팹 + `BuildingData`를 만든다. `DungeonColony → Create Prison Cage`
//
// 붙잡아 온 사람을 가두는 자리. 죄수 침대(`Bed.ForPrisoners`)와 나란한 물건이다 —
// 우리는 가두는 것이 전부라 싸고, 침대는 회복·치료가 붙는 대신 자리를 더 먹는다.
//
// 모델은 **기본 도형으로 짓는다**(기둥 넷 + 바닥). 어차피 나중에 만든 것으로 갈아 끼울
// 자리고, 없으면 기능을 시험할 수가 없다. 갈아 끼울 때는 프리팹만 바꾸면 된다 —
// `BuildingData`가 프리팹을 가리키고 있고 나머지는 컴포넌트가 들고 있다.
//
// **없는 것만 만든다** — 이미 있으면 손대지 않는다(모양·값을 인스펙터에서 손봤을 수 있다).
public static class PrisonCageCreator
{
    const string PrefabPath = "Assets/3_Prefab/PrisonCage.prefab";
    const string DataPath   = "Assets/Resources/Data/Buildings/PrisonCage.asset";

    [MenuItem("DungeonColony/Create Prison Cage")]
    static void Create()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
        var data   = AssetDatabase.LoadAssetAtPath<BuildingData>(DataPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BuildingData>();
            data.BuildingName = "우리";
            data.Description  = "붙잡아 온 사람을 가두는 자리. 한 명이 들어간다. " +
                                "회복도 치료도 없다 — 살려서 다룰 사람은 죄수 침대에 눕힌다.";
            data.Category     = BuildCategory.Misc;
            data.SortOrder    = 60;
            data.Size         = new Vector2Int(1, 1);
            data.UsesBlueprint = true;
            data.OccupiesTile  = false;   // 폰이 그 칸에 들어가 서야 한다

            var wood = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Data/Items/Res_Wood.asset");
            data.CostItem   = wood;
            data.CostAmount = 15;

            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.CreateAsset(data, DataPath);
        }

        // 프리팹 연결은 **늘 다시 건다** — 프리팹을 새로 만들었으면 참조가 비어 있다.
        if (data.Prefab == null && prefab != null)
        {
            data.Prefab = prefab;
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = data;
        Debug.Log("[PrisonCage] 우리 프리팹 + BuildingData 준비 완료. " +
                  "건설 목록은 레지스트리에서 오므로 따로 등록할 것이 없습니다.");
    }

    static GameObject BuildPrefab()
    {
        var root = new GameObject("PrisonCage");

        // 바닥 판 — 보이기만 한다.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Base";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        floor.transform.localScale    = new Vector3(0.9f, 0.1f, 0.9f);
        Object.DestroyImmediate(floor.GetComponent<Collider>());

        // **클릭 판정은 루트에 둔다.** `SelectionManager`·`MoveController`가
        // `hit.collider.GetComponent<IWorldClickable>()`로 묻기 때문에, 콜라이더가 자식에
        // 있으면 컴포넌트를 못 찾아 **눌러도 아무 일도 안 일어난다**(침대 프리팹도 루트에 있다).
        // 트리거라 폰이 우리 안으로 걸어 들어올 수 있다.
        var box = root.AddComponent<BoxCollider>();
        box.center    = new Vector3(0f, 0.6f, 0f);
        box.size      = new Vector3(0.9f, 1.2f, 0.9f);
        box.isTrigger = true;

        // 기둥 넷.
        for (int i = 0; i < 4; i++)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = $"Post{i}";
            post.transform.SetParent(root.transform, false);
            float x = (i % 2 == 0) ? -0.38f : 0.38f;
            float z = (i < 2)      ? -0.38f : 0.38f;
            post.transform.localPosition = new Vector3(x, 0.9f, z);
            post.transform.localScale    = new Vector3(0.08f, 0.9f, 0.08f);

            // **기둥에는 콜라이더를 안 남긴다** — 폰이 우리 안으로 들어가 서야 하는데,
            // 기둥이 물리적으로 막으면 문 없는 상자가 되어 아무도 못 들어간다.
            Object.DestroyImmediate(post.GetComponent<Collider>());
        }

        // 갇힌 사람이 설 자리 — 한가운데.
        var anchor = new GameObject("HoldAnchor");
        anchor.transform.SetParent(root.transform, false);
        anchor.transform.localPosition = Vector3.zero;

        var cage = root.AddComponent<PrisonCage>();
        var so   = new SerializedObject(cage);
        so.FindProperty("holdAnchor").objectReferenceValue = anchor.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }
}
