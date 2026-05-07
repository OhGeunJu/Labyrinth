using UnityEditor;
using UnityEngine;

public static class LoggingCreator
{
    const string PrefabDir = "Assets/3_Prefab";

    [MenuItem("DungeonColony/Create Tree Prefab")]
    static void CreateTreePrefab()
    {
        var root = new GameObject("Tree");

        // 줄기 — 갈색 원통
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(root.transform, false);
        trunk.transform.localScale    = new Vector3(0.35f, 0.8f, 0.35f);
        trunk.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var trunkMat = new Material(Shader.Find("Standard"));
        trunkMat.color = new Color(0.4f, 0.25f, 0.1f);
        trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;
        Object.DestroyImmediate(trunk.GetComponent<Collider>());

        // 수관 — 초록 구
        var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "Crown";
        crown.transform.SetParent(root.transform, false);
        crown.transform.localScale    = new Vector3(1.4f, 1.2f, 1.4f);
        crown.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        var crownMat = new Material(Shader.Find("Standard"));
        crownMat.color = new Color(0.15f, 0.5f, 0.15f);
        crown.GetComponent<MeshRenderer>().sharedMaterial = crownMat;
        Object.DestroyImmediate(crown.GetComponent<Collider>());

        // 콜라이더 (루트에) — 클릭 및 NavMesh 차단용
        var col    = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1.2f, 0f);
        col.radius = 0.5f;
        col.height = 2.4f;

        // ResourceNode 컴포넌트 추가 (인스펙터에서 ResourceNodeData 연결 필요)
        root.AddComponent<ResourceNode>();

        var path   = $"{PrefabDir}/Tree.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        Debug.Log($"[LoggingCreator] Tree 프리팹 생성 → {path}\n" +
                  "인스펙터에서 ResourceNode의 Data 필드에 Wood용 ResourceNodeData를 연결하세요.\n" +
                  "(ResourceType=Wood, DropItem=Wood 아이템, GaugeRequired=20 권장)");
    }
}
