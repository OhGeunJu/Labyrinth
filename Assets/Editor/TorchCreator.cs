using UnityEditor;
using UnityEngine;

// **벽걸이 횃불을 짓는다**(2026-09-04, 사용자 요청).
//
// 건물 하나인데 **모양이 둘**이다 — 벽에 닿으면 걸리고, 아니면 바닥에 선다(`WallTorch`).
// 그래서 프리팹에도 모형이 둘 들어가고, 판정이 그중 하나를 켠다.
//
// ## 불은 새로 안 만든다
//
// 언제 켜지나는 `Campfire`가(지하에서는 사람이 켠 그대로), 얼마나 밝은가는 `LightSource`가
// 이미 답한다. 여기서 그 둘을 다시 만들면 *어두워지는 때*와 *불이 붙는 때*가 갈릴 자리가
// 하나 더 생긴다. 이름이 화톳불일 뿐 그 컴포넌트가 답하는 것은 **"불을 켜고 끈다"**다.
//
// ## 불꽃 높이는 재서 넣는다
//
// 두 모형은 키가 다르다(선 횃불은 사람 키, 걸린 횃불은 한 뼘). 상수로 박으면 한쪽은
// 자루 한복판에서 타고 다른 쪽은 허공에서 탄다 — **모형의 렌더러 경계를 재서** 각각의
// 꼭대기를 쓴다. 팩을 갈아 끼워도 따라온다.
//
// **다시 돌려도 안전하다** — 있으면 값만 맞춘다.
public static class TorchCreator
{
    const string PrefabDir = "Assets/3_Prefab/Building";
    const string DataDir   = "Assets/Resources/Data/Buildings";
    const string ItemDir   = "Assets/Resources/Data/Items";
    const string Name = "WallTorch";
    const string Tag  = "[횃불]";

    [MenuItem("DungeonColony/Building/3. Create Wall Torch (벽걸이 횃불)")]
    static void Create()
    {
        var root = new GameObject(Name);

        var mount = new GameObject("Mount").transform;
        mount.SetParent(root.transform, false);

        var standing = FireArt.Prop(mount, "Standing", "light/P_PROP_torch_standing_dungeon", Tag);
        var wall     = FireArt.Prop(mount, "Wall",     "light/P_PROP_torch_wall_dungeon",     Tag);

        // **꼭대기 언저리에서 탄다** — 0.92는 자루 끝의 심지 자리다(정확히 꼭대기면 불꽃이
        // 모형 위로 떠 보인다). 모형을 못 찾았으면 눈대중 값으로 두고 경고가 남는다.
        float standHead = FireArt.TopOf(standing, fallback: 1.35f) * 0.92f;
        float wallHead  = FireArt.TopOf(wall,     fallback: 0.40f) * 0.92f;

        // ── 불꽃과 등 ────────────────────────────────────────────────────
        var head = new GameObject("Head").transform;
        head.SetParent(mount, false);

        // **불은 화톳불과 같은 부품을 쓴다**(`FireArt`) — 갈리면 같은 세계의 두 불이 다르게
        // 탄다. 여기가 정하는 것은 **한 갈래이고 작다**는 것뿐이다(화톳불은 세 갈래).
        var flame = FireArt.Flames(head, size: 0.22f, tongues: 1);
        var lamp  = FireArt.Lamp(head, intensity: 2.4f, range: 6f);

        // ── 컴포넌트 ─────────────────────────────────────────────────────
        var light = root.AddComponent<LightSource>();
        FireArt.Set(light, so =>
        {
            // **화톳불보다 좁고 덜 밝다**(8·1.0 → 5·0.85). 그래야 화톳불이 값을 갖는다 —
            // 싸고 안 막는 물건이 넓기까지 하면 넓은 불을 지을 이유가 없다.
            so.FindProperty("radius").floatValue   = 5f;
            so.FindProperty("strength").floatValue = 0.85f;
            so.FindProperty("flicker").floatValue  = 0.18f;
            so.FindProperty("lamp").objectReferenceValue   = lamp;
            so.FindProperty("flames").objectReferenceValue = flame;
        });

        var fire = root.AddComponent<Campfire>();
        FireArt.Set(fire, so => so.FindProperty("lightSource").objectReferenceValue = light);

        var torch = root.AddComponent<WallTorch>();
        FireArt.Set(torch, so =>
        {
            so.FindProperty("mount").objectReferenceValue    = mount;
            so.FindProperty("standing").objectReferenceValue = standing;
            so.FindProperty("wall").objectReferenceValue     = wall;
            so.FindProperty("head").objectReferenceValue     = head;
            so.FindProperty("standHead").floatValue = standHead;
            so.FindProperty("wallHead").floatValue  = wallHead;
        });

        // **콜라이더는 루트에, 그리고 트리거다** — `OccupiesTile`이 꺼져 있는데 트리거가
        // 아니면 지도는 빈 칸인데 몸이 부딪혀 **폰이 처박힌다**(밭·수로가 겪은 그 자리).
        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center    = new Vector3(0f, 0.9f, 0f);
        col.size      = new Vector3(0.8f, 1.8f, 0.8f);

        root.AddComponent<PlacedBuilding>();

        string path  = PrefabDir + "/" + Name + ".prefab";
        var    saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        if (saved == null) { Debug.LogError("[횃불] 프리팹 저장 실패 — " + path); return; }

        Data(saved);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[횃불] 벽걸이 횃불을 만들었습니다 — 불꽃 높이: 선 것 {standHead:F2}m · "
                + $"걸린 것 {wallHead:F2}m. 건설 메뉴는 레지스트리를 훑으므로 씬 배선은 없습니다. "
                + "**벽걸이 모형이 벽을 등지지 않으면** 프리팹의 `Wall Yaw`에 180을 넣으세요.");
    }

    static void Data(GameObject prefab)
    {
        string path = DataDir + "/" + Name + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<BuildingData>();
            AssetDatabase.CreateAsset(d, path);
        }

        d.BuildingName = "벽걸이 횃불";
        d.Description  = "벽에 닿으면 걸리고, 걸 벽이 없으면 바닥에 우뚝 선다. "
                       + "칸을 막지 않아 통로를 좁히지 않는다. 화톳불보다 좁게 밝다.";
        d.Category  = BuildCategory.Living;   // 화톳불 옆
        d.SortOrder = 36;
        d.Size      = Vector2Int.one;

        // **돌이다.** 지하에 나무가 없어(목재 축은 미결) 나무로 적으면 정작 어두운 데서
        // 못 짓는다 — 굴을 밝히려고 만든 물건이 굴에서 안 지어지면 뜻이 없다.
        d.CostItem   = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/Res_Stone.asset");
        d.CostAmount = 3;
        if (d.CostItem == null) Debug.LogWarning("[횃불] Res_Stone 을 못 찾았습니다 — 값이 빈 건물이 됩니다.");

        d.UsesBlueprint = true;
        d.OccupiesTile  = false;   // 두 모양 다 안 막는다 — 그래서 콜라이더도 트리거다
        // **한 개씩 놓는다**(2026-09-04, 사용자 결정). 줄로 그으면 한 번에 여럿이 서는데,
        // 이 물건은 **칸마다 모양이 갈린다**(벽에 걸리나 바닥에 서나) — 줄 하나에 두 모양이
        // 섞여 서면 어디에 무엇이 설지를 놓기 전에 읽을 수가 없다. 불은 몇 걸음마다 하나씩
        // 놓는 물건이라 줄 긋기로 아끼는 것도 적다.
        d.DragBuild     = false;
        d.RequiredResearch = null; // 처음부터 열려 있다 — 지하의 첫 물건이다
        d.Prefab = prefab;
        EditorUtility.SetDirty(d);

        // **프리팹 쪽에도 물린다** — 한쪽만 물리면 조용히 반쪽이 된다(발자국 미등록 ·
        // 좌클릭해도 정보 패널이 안 뜸).
        var contents = PrefabUtility.LoadPrefabContents(PrefabDir + "/" + Name + ".prefab");
        var pb = contents.GetComponent<PlacedBuilding>() ?? contents.AddComponent<PlacedBuilding>();
        var so = new SerializedObject(pb);
        so.FindProperty("data").objectReferenceValue = d;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, PrefabDir + "/" + Name + ".prefab");
        PrefabUtility.UnloadPrefabContents(contents);
    }
}
