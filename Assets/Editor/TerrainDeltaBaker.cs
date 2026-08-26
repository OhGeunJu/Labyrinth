using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **지형 저작 — 유니티 붓으로 조각하고, 노이즈와 다른 것만 굽는다**(2026-08-26).
//
// 세계가 29,440m라 높이맵을 통째로 저장할 수 없다(433MB). 그래서 원본은 **노이즈 함수**이고
// 저작으로 바꾼 자리만 `Resources/Terrain/{x}_{y}.asset`에 남는다.
//
// **붓을 직접 만들지 않는다** — 유니티 Terrain의 조각 도구(올리기·내리기·부드럽게·평탄하게,
// 세기·모양·Undo)가 이미 다듬어져 있다. 그것으로 조각하고 결과를 구우면 런타임은 Terrain을
// 데이터로 안 쓴다. `Bake Research Graph`와 같은 패턴이다 — 저작은 편한 도구로, 런타임은 내 데이터로.
//
// 순서:
//   1. Open Chunk  — 조각할 청크의 Terrain을 씬에 세운다(노이즈 + 기존 델타로 채워서)
//   2. 유니티 붓으로 조각
//   3. Bake Delta  — 노이즈와 다른 정점만 골라 에셋에 쓴다
//   4. Close       — 조각용 Terrain을 치운다(런타임엔 TerrainStreamer가 세운다)
public static class TerrainDeltaBaker
{
    const string EditRoot  = "__TerrainEdit";
    const string AssetDir  = "Assets/Resources/Terrain";
    const float  Threshold = 0.01f;   // 1cm — 부동소수 잡음을 델타로 굽지 않는다

    [MenuItem("DungeonColony/Terrain/1. Open Chunk For Editing")]
    static void OpenChunk()
    {
        SyncProfile();

        var view = SceneView.lastActiveSceneView;
        Vector3 look = view != null ? view.pivot : Vector3.zero;
        var chunk = TerrainGrid.ChunkAt(look);
        if (!TerrainGrid.InWorld(chunk))
        {
            Debug.LogWarning("[지형] 씬 뷰가 보는 곳 " + chunk + "은 세계 밖이다 (0~" +
                             (TerrainGrid.ChunksPerAxis - 1) + ").");
            return;
        }
        if (Find(chunk) != null) { Debug.Log("[지형] 청크 " + chunk + "는 이미 열려 있다."); return; }

        int res = TerrainGrid.VertsPerAxis;
        var data = new TerrainData
        {
            heightmapResolution = res,
            size = new Vector3(TerrainGrid.ChunkSize, TerrainStreamer.HeightRange, TerrainGrid.ChunkSize),
        };

        // **노이즈 + 기존 델타로 채운다** — 이미 조각한 청크를 다시 열면 그 결과에서 이어야 한다.
        var h = new float[res, res];
        for (int vz = 0; vz < res; vz++)
        for (int vx = 0; vx < res; vx++)
            h[vz, vx] = Norm(TerrainHeight.VertexHeight(chunk, vx, vz));
        data.SetHeights(0, 0, h);

        var root = GameObject.Find(EditRoot) ?? new GameObject(EditRoot);
        var go = new GameObject("Edit_" + TerrainGrid.KeyOf(chunk));
        go.transform.SetParent(root.transform, false);
        go.transform.position = TerrainGrid.OriginOf(chunk) + Vector3.up * TerrainStreamer.MinHeight;

        var t = go.AddComponent<Terrain>();
        t.terrainData = data;
        t.allowAutoConnect = true;
        go.AddComponent<TerrainCollider>().terrainData = data;

        Undo.RegisterCreatedObjectUndo(go, "청크 열기");
        Selection.activeGameObject = go;
        Debug.Log("[지형] 청크 " + chunk + " 열림 — 붓으로 조각한 뒤 Bake Delta.");
    }

    [MenuItem("DungeonColony/Terrain/2. Bake Delta (열린 청크 전부)")]
    static void BakeAll()
    {
        SyncProfile();

        var root = GameObject.Find(EditRoot);
        var terrains = root != null ? root.GetComponentsInChildren<Terrain>() : new Terrain[0];
        if (terrains.Length == 0) { Debug.LogWarning("[지형] 열린 청크가 없다."); return; }

        // **주인별로 모은다.** 청크가 소유하는 것은 0~127 줄뿐이고 128번째 줄은 이웃의 0번이다
        // (`TerrainDelta`의 소유권 규칙) — 그래서 **가장자리를 조각하면 이웃 에셋이 바뀐다.**
        // 그 규칙이 없으면 두 청크가 같은 자리를 각자 저장해 이음매에 틈이 생긴다.
        var byOwner = new Dictionary<Vector2Int, Dictionary<int, float>>();
        int res = TerrainGrid.VertsPerAxis;

        foreach (var t in terrains)
        {
            var chunk = TerrainGrid.ChunkAt(t.transform.position);
            var heights = t.terrainData.GetHeights(0, 0, res, res);

            for (int vz = 0; vz < res; vz++)
            for (int vx = 0; vx < res; vx++)
            {
                float sculpted = heights[vz, vx] * TerrainStreamer.HeightRange + TerrainStreamer.MinHeight;

                int gx = chunk.x * TerrainHeight.OwnedPerChunk + vx;
                int gz = chunk.y * TerrainHeight.OwnedPerChunk + vz;

                // **노이즈와 견준다 — 기존 델타가 아니라.** 델타와 견주면 이미 구운 것이
                // "안 바뀐 것"이 되어 다음 굽기에서 통째로 사라진다.
                float baseH = TerrainHeight.Procedural(gx * TerrainGrid.VertexSpacing,
                                                       gz * TerrainGrid.VertexSpacing);
                if (Mathf.Abs(sculpted - baseH) <= Threshold) continue;

                var owner = new Vector2Int(
                    Mathf.Clamp(gx / TerrainHeight.OwnedPerChunk, 0, TerrainGrid.ChunksPerAxis - 1),
                    Mathf.Clamp(gz / TerrainHeight.OwnedPerChunk, 0, TerrainGrid.ChunksPerAxis - 1));
                int lx = gx - owner.x * TerrainHeight.OwnedPerChunk;
                int lz = gz - owner.y * TerrainHeight.OwnedPerChunk;
                if (lx < 0 || lz < 0 || lx >= res || lz >= res) continue;

                if (!byOwner.TryGetValue(owner, out var d))
                    byOwner[owner] = d = new Dictionary<int, float>();
                d[lz * res + lx] = sculpted;
            }
        }

        if (byOwner.Count == 0) { Debug.Log("[지형] 노이즈와 다른 정점이 없다 — 쓸 것이 없다."); return; }

        System.IO.Directory.CreateDirectory(AssetDir);
        int verts = 0;
        foreach (var kv in byOwner) { Write(kv.Key, kv.Value); verts += kv.Value.Count; }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        TerrainHeight.ForgetDeltas();

        Debug.Log("[지형] 델타 구움 — 청크 " + byOwner.Count + "개 · 정점 " + verts + "개 → " + AssetDir);
    }

    static void Write(Vector2Int chunk, Dictionary<int, float> edits)
    {
        string path = AssetDir + "/" + TerrainGrid.KeyOf(chunk) + ".asset";

        // **있던 것을 다시 쓴다** — 지우고 새로 만들면 guid가 바뀐다(프리팹 .meta 규칙과 같은 자리).
        var asset = AssetDatabase.LoadAssetAtPath<TerrainDelta>(path);
        bool isNew = asset == null;
        if (isNew) asset = ScriptableObject.CreateInstance<TerrainDelta>();

        var idx = new int[edits.Count];
        var hs  = new float[edits.Count];
        int i = 0;
        foreach (var kv in edits) { idx[i] = kv.Key; hs[i] = kv.Value; i++; }

        asset.Chunk = chunk; asset.Index = idx; asset.Height = hs;

        if (isNew) AssetDatabase.CreateAsset(asset, path);
        else       EditorUtility.SetDirty(asset);
    }

    [MenuItem("DungeonColony/Terrain/3. Close Edit Chunks")]
    static void CloseAll()
    {
        var root = GameObject.Find(EditRoot);
        if (root == null) { Debug.Log("[지형] 열린 청크가 없다."); return; }
        Undo.DestroyObjectImmediate(root);
        Debug.Log("[지형] 조각용 청크를 치웠다 — 런타임은 TerrainStreamer가 세운다.");
    }

    // **씬의 `TerrainStreamer`가 프로필의 주인이다.** 도구가 제 값을 들면 에디터에서 조각한
    // 지형과 게임에서 도는 지형이 갈려 구운 델타가 통째로 어긋난다.
    static void SyncProfile()
    {
        var s = Object.FindFirstObjectByType<TerrainStreamer>();
        if (s != null)
        {
            TerrainHeight.Profile  = s.Profile;
            TerrainHeight.Features = s.Features;   // **산까지 같이** — 빠뜨리면 산 전체가 델타로 구워진다
        }
        else Debug.LogWarning("[지형] 씬에 TerrainStreamer가 없다 — 기본 프로필로 조각한다. " +
                              "게임에서 다른 값을 쓰면 구운 델타가 어긋난다.");
        TerrainHeight.ForgetDeltas();
        TerrainHeight.ForgetBiomes();
    }

    static float Norm(float worldHeight) =>
        Mathf.Clamp01((worldHeight - TerrainStreamer.MinHeight) / TerrainStreamer.HeightRange);

    static Terrain Find(Vector2Int chunk)
    {
        var root = GameObject.Find(EditRoot);
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Terrain>())
            if (TerrainGrid.ChunkAt(t.transform.position) == chunk) return t;
        return null;
    }
}
