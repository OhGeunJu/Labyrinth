using UnityEditor;
using UnityEngine;

// **지도에 찍힌 마을/소굴/자원지대를 세울 프리팹**(2026-08-26).
//
// `WorldSitePlacer`가 `WorldMapData`(16x16)를 읽어 좌표를 정하고, 그 자리에 이 프리팹들을
// 세운다. **여기서 만드는 것은 자리를 잡아 주는 임시본이다** - 파이프라인이 도는지 눈으로
// 보려는 것이고, 진짜 마을은 씬에서 손으로 지어 프리팹으로 저장하면 그대로 갈아 끼운다
// (`WorldContentStreamer` 인스펙터의 프리팹 칸을 바꾸면 끝이다).
//
// **기본 도형으로 짓는다** - 이 저장소의 건물 대부분이 그렇고, 모양을 다듬는 것은 나중에
// 모델을 갈아 끼우는 일이지 지금 배선을 막을 이유가 아니다.
//
// **폰을 프리팹 안에 넣는다** - 장소가 서면 그 안의 주민/산적도 같이 선다. `WorldSite`의
// `Persistent`가 켜져 있어(마을/소굴) **한 번만 서므로** 다시 밟는다고 늘어나지 않는다.
public static class WorldSiteCreator
{
    const string Dir = "Assets/3_Prefab/Site";

    [MenuItem("DungeonColony/Terrain/6. Create Site Prefabs")]
    static void Create()
    {
        System.IO.Directory.CreateDirectory(Dir);

        var town = BuildTown();
        var lair = BuildLair();
        var res  = BuildResourceSite();

        Wire(town, lair, res);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[장소] 프리팹 셋을 만들고 물렸다 -> " + Dir + "\n" +
                  "이제 `5. Place Sites From World Map`을 돌리면 지도에 찍힌 자리에 선다. " +
                  "진짜 마을을 지으면 인스펙터에서 프리팹만 갈아 끼우면 된다.");
    }

    static GameObject BuildTown()
    {
        var root = new GameObject("Site_Town");

        // 오두막 다섯을 원형으로 - 가운데를 비워 광장처럼.
        var wall = Mat("Wall", 0.55f, 0.45f, 0.34f);
        var roof = Mat("Roof", 0.42f, 0.26f, 0.20f);
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f;
            var at = new Vector3(Mathf.Cos(a) * 9f, 0f, Mathf.Sin(a) * 9f);
            Hut(root.transform, at, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), wall, roof);
        }

        Pawn(root.transform, "Npc_Merchant", new Vector3(0f, 0f, 0f));
        Pawn(root.transform, "Npc_Villager", new Vector3(2.5f, 0f, 1.5f));
        Pawn(root.transform, "Npc_Villager", new Vector3(-2.0f, 0f, 2.2f));
        Pawn(root.transform, "Npc_Guard",    new Vector3(1.0f, 0f, -3.0f));

        return Save(root, "Site_Town");
    }

    static GameObject BuildLair()
    {
        var root = new GameObject("Site_Lair");

        var post = Mat("Post", 0.30f, 0.26f, 0.22f);
        var tent = Mat("Tent", 0.34f, 0.30f, 0.24f);

        // 말뚝 울타리 - 한쪽을 터 둔다(입구).
        for (int i = 0; i < 14; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            var at = new Vector3(Mathf.Cos(a) * 8f, 1f, Mathf.Sin(a) * 8f);
            Box(root.transform, at, new Vector3(0.35f, 2f, 0.35f), Quaternion.identity, post, "Post");
        }

        Box(root.transform, new Vector3(-3f, 1f, 1f),  new Vector3(3.5f, 2f, 3f), Quaternion.identity, tent, "Tent");
        Box(root.transform, new Vector3(3f, 1f, -1f),  new Vector3(3.5f, 2f, 3f), Quaternion.identity, tent, "Tent");

        var fire = Box(root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(1.6f, 0.4f, 1.6f),
                       Quaternion.identity, Mat("Fire", 0.18f, 0.16f, 0.15f), "Firepit");
        Strip(fire);

        Pawn(root.transform, "Enemy_Bandit",       new Vector3(-1.5f, 0f, 2.0f));
        Pawn(root.transform, "Enemy_Bandit",       new Vector3(1.8f, 0f, 1.2f));
        Pawn(root.transform, "Enemy_BanditArcher", new Vector3(0.5f, 0f, -2.4f));
        Pawn(root.transform, "Enemy_BanditChief",  new Vector3(-0.8f, 0f, -1.0f));

        return Save(root, "Site_Lair");
    }

    static GameObject BuildResourceSite()
    {
        var root = new GameObject("Site_ResourceSite");

        // **광맥 노드를 그대로 쓴다** - `ResourceNode`가 붙어 있어 캐면 자원이 나온다.
        string[] veins = { "Node_IronVein", "Node_CoalSeam", "Node_CopperVein", "Node_IronVein" };
        for (int i = 0; i < veins.Length; i++)
        {
            float a = i / (float)veins.Length * Mathf.PI * 2f;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3_Prefab/Node/" + veins[i] + ".prefab");
            if (src == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            go.transform.localPosition = new Vector3(Mathf.Cos(a) * 5f, 0f, Mathf.Sin(a) * 5f);
        }

        return Save(root, "Site_ResourceSite");
    }

    static void Hut(Transform parent, Vector3 at, Quaternion rot, Material wall, Material roof)
    {
        var hut = new GameObject("Hut");
        hut.transform.SetParent(parent, false);
        hut.transform.SetLocalPositionAndRotation(at, rot);

        Box(hut.transform, new Vector3(0f, 1.2f, 0f), new Vector3(4f, 2.4f, 4f),
            Quaternion.identity, wall, "Wall");

        // 지붕 - 큐브를 45도 눕혀 얹는다(원뿔 프리미티브가 없어서).
        var r = Box(hut.transform, new Vector3(0f, 2.9f, 0f), new Vector3(3.2f, 3.2f, 4.4f),
                    Quaternion.Euler(0f, 0f, 45f), roof, "Roof");
        Strip(r);
    }

    static GameObject Box(Transform parent, Vector3 at, Vector3 size, Quaternion rot,
                          Material mat, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(at, rot);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // 장식은 콜라이더를 뗀다 - 길찾기는 타일 점유만 보지만, 콜라이더가 있으면 **땅 클릭
    // 레이캐스트가 걸린다**(지붕에 대고 이동 명령을 내리게 된다).
    static void Strip(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
    }

    static void Pawn(Transform parent, string prefabName, Vector3 at)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3_Prefab/Pawn/" + prefabName + ".prefab");
        if (src == null) { Debug.LogWarning("[장소] 폰 프리팹 없음 - " + prefabName); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        go.transform.localPosition = at;
    }

    // **머티리얼은 에셋으로 남긴다** - 런타임에 만들면 프리팹마다 인스턴스가 생기고
    // 에디터에서 색이 안 보인다(침대 색을 저작으로 돌린 그 판단과 같은 자리).
    static Material Mat(string name, float r, float g, float b)
    {
        string path = Dir + "/Site_" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh) { name = "Site_" + name, color = new Color(r, g, b) };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Save(GameObject root, string name)
    {
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Dir + "/" + name + ".prefab");
        Object.DestroyImmediate(root);
        return saved;
    }

    static void Wire(GameObject town, GameObject lair, GameObject res)
    {
        var c = Object.FindFirstObjectByType<WorldContentStreamer>();
        if (c == null) { Debug.LogWarning("[장소] 씬에 WorldContentStreamer가 없다 - 프리팹만 만들었다."); return; }

        var so = new SerializedObject(c);
        so.FindProperty("townPrefab").objectReferenceValue         = town;
        so.FindProperty("lairPrefab").objectReferenceValue         = lair;
        so.FindProperty("resourceSitePrefab").objectReferenceValue = res;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(c);
    }
}
