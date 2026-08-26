using UnityEditor;
using UnityEngine;

// **내가 그 자리에 직접 짓는다**(2026-08-26).
//
// `5. Place Sites From World Map`은 *"칸에 찍으면 도구가 자리를 골라 세운다"*이다. 그건 씨를
// 뿌리는 일이고, **켄시처럼 "여기에 이 도시"를 손으로 앉히는 길**은 따로 있어야 한다.
//
// 걸림돌은 세계가 16,384m이고 지형이 **스트리밍**이라는 것뿐이다 — 에디터에서는 청크가 안
// 서 있어 **딛고 놓을 땅이 없다.** 그래서 셋으로 나눈다:
//
//   1. `Show Terrain Here`   — 씬 뷰가 보는 자리에 지형을 띄운다(3x3 청크 = 768m)
//   2. 씬에서 프리팹을 손으로 놓는다 — **지금까지 하던 그대로**
//   3. `Save Selection As Site` — 고른 것을 프리팹으로 굽고 그 좌표에 등록한다
//
// 등록된 것은 `FromMap`이 꺼져 있어 **`5번`을 다시 돌려도 안 지워진다.**
public static class WorldSiteAuthoring
{
    const string PreviewRoot = "__TerrainPreview";
    const string SiteDir     = "Assets/3_Prefab/Site";

    // ── 1. 땅을 띄운다 ──────────────────────────────────────────────────────

    [MenuItem("DungeonColony/Terrain/A. Show Terrain Here (3x3)")]
    static void ShowHere()
    {
        var streamer = Object.FindFirstObjectByType<TerrainStreamer>();
        if (streamer != null)
        {
            TerrainHeight.Profile  = streamer.Profile;
            TerrainHeight.Features = streamer.Features;
        }
        TerrainHeight.ForgetDeltas();
        TerrainHeight.ForgetBiomes();

        var view = SceneView.lastActiveSceneView;
        Vector3 look = view != null ? view.pivot : Vector3.zero;
        var center = TerrainGrid.ChunkAt(look);

        HideHere();
        var root = new GameObject(PreviewRoot);
        Undo.RegisterCreatedObjectUndo(root, "지형 띄우기");

        int res = TerrainGrid.VertsPerAxis;
        int made = 0;

        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            var c = new Vector2Int(center.x + dx, center.y + dz);
            if (!TerrainGrid.InWorld(c)) continue;

            var data = new TerrainData
            {
                heightmapResolution = res,
                size = new Vector3(TerrainGrid.ChunkSize, TerrainStreamer.HeightRange, TerrainGrid.ChunkSize),
            };

            var hs = new float[res, res];
            for (int vz = 0; vz < res; vz++)
            for (int vx = 0; vx < res; vx++)
                hs[vz, vx] = Mathf.Clamp01(
                    (TerrainHeight.VertexHeight(c, vx, vz) - TerrainStreamer.MinHeight)
                    / TerrainStreamer.HeightRange);
            data.SetHeights(0, 0, hs);

            var go = new GameObject("Preview_" + TerrainGrid.KeyOf(c));
            go.transform.SetParent(root.transform, false);
            go.transform.position = TerrainGrid.OriginOf(c) + Vector3.up * TerrainStreamer.MinHeight;
            go.layer = LayerMask.NameToLayer("Ground");

            var t = go.AddComponent<Terrain>();
            t.terrainData = data;
            t.allowAutoConnect = true;
            go.AddComponent<TerrainCollider>().terrainData = data;
            made++;
        }

        Debug.Log("[장소] 청크 " + made + "개를 띄웠다 (가운데 " + center + "). " +
                  "여기에 프리팹을 놓고 `B. Save Selection As Site`. 끝나면 `C. Hide Terrain`.");
    }

    [MenuItem("DungeonColony/Terrain/C. Hide Terrain")]
    static void HideHere()
    {
        var root = GameObject.Find(PreviewRoot);
        if (root != null) Object.DestroyImmediate(root);
    }

    // ── 3. 고른 것을 장소로 굽는다 ──────────────────────────────────────────

    [MenuItem("DungeonColony/Terrain/B. Save Selection As Site")]
    static void SaveSelection()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) { Debug.LogWarning("[장소] 씬에서 도시의 루트 오브젝트를 고를 것."); return; }
        if (sel.transform.IsChildOf(GameObject.Find(PreviewRoot)?.transform ?? sel.transform.root)
            && sel.GetComponent<Terrain>() != null)
        { Debug.LogWarning("[장소] 지형 미리보기는 장소가 아니다."); return; }

        var content = Object.FindFirstObjectByType<WorldContentStreamer>();
        if (content == null) { Debug.LogWarning("[장소] 씬에 WorldContentStreamer가 없다."); return; }

        System.IO.Directory.CreateDirectory(SiteDir);

        // **자리는 루트의 위치가 답이다** — 저장하기 전에 읽는다(프리팹은 원점 기준으로 굽힌다).
        Vector3 at = sel.transform.position;
        float yaw  = sel.transform.eulerAngles.y;

        // 프리팹 안에서는 루트가 원점이어야 한다 — 안 그러면 세울 때 그만큼 밀린다
        // (`Instantiate(prefab, pos, rot)`는 루트의 localPosition을 덮지만, 자식들의
        //  상대 위치는 그대로라 루트가 치우쳐 있으면 도시 전체가 한쪽으로 쏠린다).
        var undoGroup = Undo.GetCurrentGroup();
        Undo.RecordObject(sel.transform, "장소 굽기");
        sel.transform.position = Vector3.zero;
        sel.transform.rotation = Quaternion.identity;

        string path = SiteDir + "/" + sel.name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(sel, path);

        sel.transform.position = at;
        sel.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Undo.CollapseUndoOperations(undoGroup);

        if (prefab == null) { Debug.LogWarning("[장소] 프리팹 저장 실패 — " + path); return; }

        Register(content, prefab, at, yaw, sel.name);

        Debug.Log("[장소] `" + sel.name + "`을(를) 구워 등록했다 → " + path + "\n" +
                  "자리 (" + Mathf.RoundToInt(at.x) + ", " + Mathf.RoundToInt(at.z) + ") · " +
                  "지역 " + TerrainGrid.RegionAt(at) + ". " +
                  "**손으로 놓은 것이라 `5. Place Sites`를 다시 돌려도 안 지워진다.**");
    }

    static void Register(WorldContentStreamer content, GameObject prefab,
                         Vector3 at, float yaw, string name)
    {
        var so  = new SerializedObject(content);
        var arr = so.FindProperty("sites");

        // 같은 프리팹이 같은 자리에 이미 있으면 덮어쓴다 — 두 번 구웠다고 둘이 서면 안 된다.
        int slot = -1;
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("Prefab").objectReferenceValue == prefab) { slot = i; break; }
        }
        if (slot < 0) { slot = arr.arraySize; arr.arraySize = slot + 1; }

        var s = arr.GetArrayElementAtIndex(slot);
        s.FindPropertyRelative("Name").stringValue            = name;
        s.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        s.FindPropertyRelative("Position").vector2Value       = new Vector2(at.x, at.z);
        s.FindPropertyRelative("YOffset").floatValue          = 0f;
        s.FindPropertyRelative("Rotation").floatValue         = yaw;
        s.FindPropertyRelative("Persistent").boolValue        = true;
        s.FindPropertyRelative("FromMap").boolValue           = false;   // 손으로 놓은 것

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(content);
    }
}
