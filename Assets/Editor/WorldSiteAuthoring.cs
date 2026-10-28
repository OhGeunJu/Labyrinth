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
    const string SiteDir     = "Assets/3_Prefab/Site";

    // ── 1. 땅을 띄운다 ──────────────────────────────────────────────────────

    [MenuItem("DungeonColony/Authoring/Show Terrain Here (3x3)")]
    static void ShowHere()
    {
        TerrainAuthoring.SyncFromScene();

        var view = SceneView.lastActiveSceneView;
        Vector3 look = view != null ? view.pivot : Vector3.zero;
        var center = TerrainGrid.ChunkAt(look);

        HideHere();
        // **씬에 안 남긴다** — 저장된 채 Play에 들어가면 런타임 청크와 겹쳐 옛 높이의 벽이 된다(2026-09-26).
        var root = new GameObject(TerrainAuthoring.PreviewRoot) { hideFlags = HideFlags.DontSave };
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

            TerrainAuthoring.Fill(data, c);

            var go = new GameObject("Preview_" + TerrainGrid.KeyOf(c)) { hideFlags = HideFlags.DontSave };
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

    [MenuItem("DungeonColony/Authoring/Hide Terrain")]
    static void HideHere()
    {
        var root = GameObject.Find(TerrainAuthoring.PreviewRoot);
        if (root != null) Object.DestroyImmediate(root);
    }

    // ── 3. 고른 것을 장소로 굽는다 ──────────────────────────────────────────

    [MenuItem("DungeonColony/Authoring/Save Selection As Site")]
    static void SaveSelection()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) { Debug.LogWarning("[장소] 씬에서 도시의 루트 오브젝트를 고를 것."); return; }
        if (sel.transform.IsChildOf(GameObject.Find(TerrainAuthoring.PreviewRoot)?.transform ?? sel.transform.root)
            && sel.GetComponent<Terrain>() != null)
        { Debug.LogWarning("[장소] 지형 미리보기는 장소가 아니다."); return; }

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

        // **굽고 나서 고른 것 자체를 그 프리팹의 인스턴스로 만든다**(`AndConnect`).
        //
        // 이게 요점이다. 그냥 저장하면 씬에 남는 것은 **프리팹이 아닌 덩어리**이고,
        // `5. Bake World`는 되짚을 원본이 없어 **굽지 못한다**(굽기가 "프리팹 인스턴스가
        // 아닌 것"으로 세어 경고만 하고 지나간다). 연결해 두면 그 자리에서 바로 왕복에 든다.
        string path = SiteDir + "/" + sel.name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(sel, path, InteractionMode.AutomatedAction);

        sel.transform.position = at;
        sel.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (prefab == null)
        {
            Undo.CollapseUndoOperations(undoGroup);
            Debug.LogWarning("[장소] 프리팹 저장 실패 — " + path);
            return;
        }

        Adopt(sel, at);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log("[장소] `" + sel.name + "`을(를) 구웠다 → " + path + "\n" +
                  "자리 (" + Mathf.RoundToInt(at.x) + ", " + Mathf.RoundToInt(at.z) + ") · " +
                  "지역 " + TerrainGrid.RegionAt(at) + ". " +
                  "**손으로 놓은 것이라 `5. Place Sites`를 다시 돌려도 안 지워진다.**\n" +
                  "**아직 안 구웠다** — `World → 5. Bake World`. 씬 저장도 잊지 말 것.");
    }

    // **저작 씬에 들인다** — 청크 에셋에 직접 쓰지 않는다.
    //
    // 쓰는 문은 `5. Bake World` 하나여야 한다. 여기서 에셋에 바로 쓰면 **저작 씬이 모르는
    // 물건**이 되고, 다음 굽기가 *"씬에 없으니 지워진 것"*으로 보아 **없애 버린다**
    // (굽기는 지우기까지 한다).
    static void Adopt(GameObject go, Vector3 at)
    {
        if (!TerrainGrid.InWorld(TerrainGrid.ChunkAt(at.x, at.z)))
            Debug.LogWarning("[장소] 세계 밖이다 — 굽기가 건너뛴다. " + TerrainGrid.ChunkAt(at.x, at.z));

        var root = WorldAuthoringScene.FindRoot();
        if (root == null)
        {
            Debug.LogWarning("[장소] 저작 씬이 안 열려 있다 — 프리팹만 만들었다. " +
                             "`World → 3. Open Authoring Scene`을 열고 이 오브젝트를 그 아래로 옮길 것 " +
                             "(안 옮기면 굽기가 못 본다).");
            return;
        }

        // **월드 자리를 지키며 옮긴다** — 굽기가 `transform.position`을 읽으므로 자리가
        // 곧 데이터다.
        if (!go.transform.IsChildOf(root.transform))
            Undo.SetTransformParent(go.transform, root.transform, "저작 씬에 들이기");

        var tag = go.GetComponent<WorldObjectTag>();
        if (tag == null) tag = Undo.AddComponent<WorldObjectTag>(go);

        if (string.IsNullOrEmpty(tag.Id)) tag.Id = WorldContentAssets.NewId();
        tag.Persistent = true;
        tag.Generated  = false;    // 손으로 놓은 것 — `5. Place Sites`가 안 건드린다
        EditorUtility.SetDirty(tag);
    }
}
