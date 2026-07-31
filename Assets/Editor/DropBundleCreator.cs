using UnityEditor;
using UnityEngine;

// 바닥에 떨어진 아이템의 **공용 모델**을 만든다 — 묶어 놓은 자루 모양.
//
// `ItemData.WorldModel`이 비어 있을 때 쓰는 폴백이라 **아무 아이템이나 될 수 있어야** 한다.
// 그래서 무엇을 닮게 만들지 않고 "뭔가 담겨 있는 꾸러미"로 간다. 색은 런타임에 등급으로
// 물들이므로(`DroppedItem`) 여기서는 **흰색에 가깝게** 둔다 — 바탕색이 진하면 틴트가 안 먹는다.
//
// **3_Prefab이 아니라 Resources에 두는 이유** — 자세한 근거는 `DroppedItem.BundlePath` 주석과
// `docs/data.md`("static 팩토리가 쓰는 고정 에셋" 절). 요약하면 `DroppedItem.Spawn`이 static이라
// 인스펙터 참조를 받을 자리가 없고, 이건 배선이 빠진 씬에서도 반드시 떠야 하는 폴백이다.
//
// 일회성 생성기 — 산출물이 자리 잡으면 지운다.
public static class DropBundleCreator
{
    const string ResDir     = "Assets/Resources";
    const string PrefabDir  = "Assets/Resources/Prefabs";
    const string PrefabPath = "Assets/Resources/Prefabs/DropBundle.prefab";
    const string MatPath    = "Assets/2_Material/Mdl_DropBundle.mat";

    [MenuItem("DungeonColony/Create Drop Bundle Model")]
    static void Create()
    {
        if (!AssetDatabase.IsValidFolder(ResDir))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder(ResDir, "Prefabs");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            // 거의 흰색 — 런타임 등급 틴트(MaterialPropertyBlock)가 그대로 살아나게.
            var baseCol = new Color(0.88f, 0.85f, 0.78f);
            mat = new Material(shader) { name = "Mdl_DropBundle" };
            mat.color = baseCol;
            if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", baseCol);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        var root = new GameObject("DropBundle");

        // **전부 같은 머티리얼을 쓴다.** 런타임 등급 틴트가 렌더러 단위로 색을 덮으므로,
        // 조각마다 다른 색을 주면 어떤 건 등급색이고 어떤 건 아닌 어중간한 그림이 된다.
        // 형태(볼록한 몸통·잘록한 목·삐져나온 자락)만으로 충분히 읽힌다.

        // 자루 — 아래가 볼록하고 위로 좁아지는 형태.
        Piece(root.transform, PrimitiveType.Sphere,   "Sack",  mat,
              new Vector3(0f, 0.11f, 0f), new Vector3(0.30f, 0.24f, 0.30f), Vector3.zero);
        // 주둥이 — 끈으로 잘록하게 묶인 목
        Piece(root.transform, PrimitiveType.Cylinder, "Neck",  mat,
              new Vector3(0f, 0.23f, 0f), new Vector3(0.11f, 0.035f, 0.11f), Vector3.zero);
        // 삐져나온 천 자락 둘 — 실루엣이 밋밋해지지 않게 서로 반대로 기울인다.
        Piece(root.transform, PrimitiveType.Cube,     "Fold1", mat,
              new Vector3(0.045f, 0.29f, 0.01f), new Vector3(0.07f, 0.10f, 0.05f), new Vector3(0f, 20f, 24f));
        Piece(root.transform, PrimitiveType.Cube,     "Fold2", mat,
              new Vector3(-0.04f, 0.28f, -0.02f), new Vector3(0.06f, 0.09f, 0.05f), new Vector3(0f, -35f, -28f));

        // **콜라이더는 넣지 않는다** — 픽업 판정은 `DroppedItem`이 루트에 다는
        // SphereCollider(트리거)가 담당한다. 여기 있으면 Spawn이 어차피 지운다.
        var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DropBundle] 생성 완료 — {PrefabPath} (조각 {saved.transform.childCount}개). " +
                  "DroppedItem이 Resources.Load로 집어 폴백 모델로 쓴다.");
    }

    static void Piece(Transform parent, PrimitiveType type, string name, Material mat,
                      Vector3 pos, Vector3 scale, Vector3 euler)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale    = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
