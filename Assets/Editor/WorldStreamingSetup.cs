using UnityEditor;
using UnityEngine;

// **월드 스트리밍 배선을 씬에 세운다**(2026-08-26).
//
// 손으로 붙이면 빠뜨리는 자리가 많다 — 지형은 서는데 나무가 안 보이거나(규칙이 비었다),
// 폰이 지면 아래를 걷거나(`MapManager.절차 지형`을 안 켰다), 건설 커서가 땅을 못 찾는다
// (레이어가 `Ground`가 아니다). **셋 다 증상이 "왜 안 되지"라 원인을 짚기 어렵다.**
//
// 이 저장소가 UI에 쓰는 방식 그대로다 — **규격은 도구가 씬에 세우고, 그 안을 런타임이 채운다.**
public static class WorldStreamingSetup
{
    const string RootName = "World";

    [MenuItem("DungeonColony/Terrain/0. Setup World Streaming")]
    static void Setup()
    {
        // **이미 붙어 있는 것을 먼저 찾는다** — 이름으로만 찾으면 저작 씬의 루트를 집을 수
        // 있고(거기에 스트리머가 붙으면 판이 열릴 때 그 오브젝트가 통째로 꺼져 **세계가 안 선다**),
        // 이름을 바꿔 둔 씬에서는 두 번째 루트를 만든다.
        var existing = Object.FindFirstObjectByType<TerrainStreamer>(FindObjectsInactive.Include);
        var go = existing != null ? existing.gameObject : GameObject.Find(RootName);

        if (go != null && go.GetComponent<WorldAuthoringRoot>() != null)
        {
            Debug.LogWarning("[월드] `" + go.name + "`은 저작 씬의 루트다 — 스트리밍을 여기 붙이면 " +
                             "판이 열릴 때 통째로 꺼진다. 게임 씬에 새로 세운다.");
            go = null;
        }

        if (go == null)
        {
            go = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(go, "World 만들기");
        }
        // **트랜스폼을 되돌린다** — 스트리머는 자기 루트를 쓰므로 안 깨지지만, 여기 붙은
        // 값이 0이 아니면 다음에 보는 사람이 그것부터 의심한다.
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;

        var terrain = go.GetComponent<TerrainStreamer>() ?? Undo.AddComponent<TerrainStreamer>(go);
        var content = go.GetComponent<WorldContentStreamer>() ?? Undo.AddComponent<WorldContentStreamer>(go);

        // **무리를 실체로 세우는 쪽**(2026-08-27). 이게 없으면 `WorldBandManager`가 토큰만 굴리고
        // **지도 위의 무리가 영영 실제 폰이 되지 않는다** — 습격이 예고돼도 아무도 안 온다.
        // 값은 전부 기본값으로 뜻이 서 있어(반경은 `SimLocality`를 따라간다) 저작할 것이 없다.
        if (go.GetComponent<BandMaterializer>() == null) Undo.AddComponent<BandMaterializer>(go);

        int layer = LayerMask.NameToLayer("Ground");
        if (layer < 0) { layer = 0; Debug.LogWarning("[월드] 레이어 'Ground'가 없다 — Default로 둔다."); }

        var st = new SerializedObject(terrain);
        st.FindProperty("terrainLayer").intValue = layer;
        st.ApplyModifiedPropertiesWithoutUndo();

        WireScatter(content);
        EnableProceduralTerrain();
        int slept   = SleepOldTerrains(go);
        int screens = SleepLegacyWorldScreen();

        EditorUtility.SetDirty(go);
        Selection.activeGameObject = go;

        Debug.Log($"[월드] 배선 완료 — {go.name}에 TerrainStreamer + WorldContentStreamer + BandMaterializer. " +
                  $"레이어 '{LayerMask.LayerToName(layer)}' · 옛 Terrain {slept}개 · " +
                  $"옛 월드맵 화면 {screens}개 재움." + "\n" +
                  $"세계 {TerrainGrid.WorldSize}m · 청크 {TerrainGrid.ChunkSize}m · " +
                  $"{TerrainGrid.ChunksPerAxis}×{TerrainGrid.ChunksPerAxis}");
    }

    // 나무·바위 규칙을 물린다. **이미 저작된 규칙이 있으면 안 건드린다** — 도구를 다시 돌렸다고
    // 손으로 맞춘 값이 날아가면 다시 돌릴 엄두가 안 난다(연구 그래프 베이커와 같은 판단).
    static void WireScatter(WorldContentStreamer content)
    {
        var so = new SerializedObject(content);
        var arr = so.FindProperty("scatter");
        if (arr.arraySize > 0)
        {
            Debug.Log($"[월드] 뿌리기 규칙이 이미 {arr.arraySize}개 있다 — 안 건드린다.");
            return;
        }

        var tree  = Load("Assets/3_Prefab/Node/Tree.prefab");
        var stone = Load("Assets/3_Prefab/Node/Stone.prefab");

        arr.arraySize = 0;
        if (tree  != null) Add(arr, "나무", tree,  24f, 25f, -100f, 260f, 4f,   0.85f, 1.25f);
        if (stone != null) Add(arr, "바위", stone,  8f, 40f, -100f, 400f, 6f,   0.8f,  1.4f);

        so.ApplyModifiedPropertiesWithoutUndo();

        if (tree == null || stone == null)
            Debug.LogWarning("[월드] Tree/Stone 프리팹을 못 찾았다 — 인스펙터에서 직접 물릴 것.");
        else
            Debug.Log("[월드] 뿌리기 규칙 둘 물림 — 나무(청크당 24) · 바위(8).");
    }

    static void Add(SerializedProperty arr, string name, GameObject prefab,
                    float perChunk, float maxSlope, float minH, float maxH,
                    float spacing, float scaleMin, float scaleMax)
    {
        int i = arr.arraySize;
        arr.arraySize = i + 1;
        var e = arr.GetArrayElementAtIndex(i);
        e.FindPropertyRelative("Name").stringValue          = name;
        e.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        e.FindPropertyRelative("PerChunk").floatValue       = perChunk;
        e.FindPropertyRelative("MaxSlope").floatValue       = maxSlope;
        e.FindPropertyRelative("MinHeight").floatValue      = minH;
        e.FindPropertyRelative("MaxHeight").floatValue      = maxH;
        e.FindPropertyRelative("MinSpacing").floatValue     = spacing;
        e.FindPropertyRelative("ScaleRange").vector2Value   = new Vector2(scaleMin, scaleMax);
    }

    // **폰이 딛는 지면을 절차 지형으로 돌린다.** 안 켜면 화면만 새 지형이고 폰은 옛
    // `Terrain`(또는 평지 0)을 딛어서, **지면 아래를 걷거나 허공에 뜬다.**
    static void EnableProceduralTerrain()
    {
        var maps = Object.FindObjectsByType<MapManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps.Length == 0) { Debug.LogWarning("[월드] 씬에 MapManager가 없다."); return; }

        foreach (var m in maps)
        {
            var so = new SerializedObject(m);
            var p = so.FindProperty("proceduralTerrain");
            if (p == null) continue;
            p.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(m);
        }
        Debug.Log($"[월드] MapManager {maps.Length}개의 '절차 지형'을 켰다.");
    }

    // **손으로 조각해 둔 옛 Terrain을 재운다.** 켜 두면 겹쳐 그려지고, 무엇보다
    // `Terrain.activeTerrain`이 그걸 집어서 높이가 따로 논다. **지우지는 않는다** —
    // 되돌릴 수 있어야 하고, 그건 사람이 정할 일이다.
    static int SleepOldTerrains(GameObject world)
    {
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t == null || t.transform.IsChildOf(world.transform)) continue;
            if (t.gameObject.name.StartsWith("Chunk_") || t.gameObject.name.StartsWith("Edit_")) continue;
            Undo.RecordObject(t.gameObject, "옛 Terrain 재우기");
            t.gameObject.SetActive(false);
            EditorUtility.SetDirty(t.gameObject);
            n++;
        }
        return n;
    }

    // **옛 3D 월드맵 화면의 M키를 끈다.** 안 끄면 지도가 둘 뜬다 — 새 `WorldMapPanel`과
    // `WorldMapView`가 같은 키를 문다. **컴포넌트를 지우지는 않는다**(참조가 40곳이고
    // 씬 배선이 걸려 있다) — 진입로만 끊는다.
    static int SleepLegacyWorldScreen()
    {
        int n = 0;
        foreach (var v in Object.FindObjectsByType<WorldMapView>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None))
        {
            var so = new SerializedObject(v);
            var p = so.FindProperty("legacyWorldScreen");
            if (p == null) continue;
            p.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(v);
            n++;
        }
        return n;
    }

    static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
}
