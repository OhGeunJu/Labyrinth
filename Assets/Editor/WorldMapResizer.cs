using UnityEditor;
using UnityEngine;

// **저작한 지도의 해상도를 올린다 — 그린 것은 지키면서**(2026-08-26).
//
// 세계가 16,384m인데 지도가 16x16이면 지역 하나가 **1,024m**다. 바이옴 하나가 걸어서 4분이라
// **어디를 가도 같은 곳**이고, 지도는 거대한 격자처럼 보인다.
//
// 그런데 다시 그리라고 할 수는 없다 — 이미 저작한 배치가 있다. **최근접으로 부풀린다**:
// 16x16을 64x64로 키우면 한 칸이 넷으로 갈라지고 **그림은 그대로**다. 그다음 `World Map
// Editor`로 가장자리만 다듬으면 된다.
//
// **크기는 청크에 맞춘다** — 지역 경계가 청크 경계와 어긋나면 바이옴이 바뀌는 자리와 지형이
// 로드되는 자리가 갈려 이음매에서 성격이 튄다. 세계가 64청크이므로 **64의 약수**여야 한다
// (16 / 32 / 64).
public static class WorldMapResizer
{
    [MenuItem("DungeonColony/Terrain/7. Resize World Map (x2)")]
    static void Double() => Resize(2);

    [MenuItem("DungeonColony/Terrain/7. Resize World Map (x4)")]
    static void Quad() => Resize(4);

    static void Resize(int factor)
    {
        var view = Object.FindFirstObjectByType<WorldMap>(FindObjectsInactive.Include);
        var so   = view != null ? new SerializedObject(view) : null;
        var data = so?.FindProperty("mapData")?.objectReferenceValue as WorldMapData;

        if (data == null) { Debug.LogWarning("[지도] 씬의 WorldMap에 mapData가 안 물려 있다."); return; }
        if (!data.IsSized) { Debug.LogWarning("[지도] mapData의 배열이 Width x Height와 어긋난다."); return; }

        int oldW = data.Width, oldH = data.Height;
        int newW = oldW * factor, newH = oldH * factor;

        int chunks = TerrainGrid.ChunksPerAxis;
        if (chunks % newW != 0)
        {
            Debug.LogWarning("[지도] " + newW + "은 세계 청크 수(" + chunks + ")의 약수가 아니다 — " +
                             "지역 경계가 청크 한복판을 가른다. 다른 배율을 쓸 것.");
            return;
        }

        // **옛 격자를 먼저 읽어 둔다** — 크기를 바꾸면 배열이 다시 잡힌다.
        var old = new WorldMapData.Node[oldW * oldH];
        for (int y = 0; y < oldH; y++)
        for (int x = 0; x < oldW; x++)
            old[y * oldW + x] = data.Get(x, y);

        Undo.RecordObject(data, "지도 해상도 올리기");

        // **`Resize`는 왼쪽 아래로 잘라 붙인다** — 여기서는 그 결과를 안 쓰고 아래에서
        // 통째로 다시 칠하므로 상관없다. 배열 크기를 맞추는 데만 쓴다.
        data.Resize(newW, newH);

        for (int y = 0; y < newH; y++)
        for (int x = 0; x < newW; x++)
            data.Set(x, y, old[(y / factor) * oldW + (x / factor)]);

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        TerrainGrid.ForgetRegions();
        TerrainHeight.ForgetBiomes();

        float region = TerrainGrid.WorldSize / (float)newW;
        Debug.Log("[지도] " + oldW + "x" + oldH + " -> " + newW + "x" + newH +
                  " (그린 것은 그대로). 지역 하나 = " + region + "m = " +
                  (region / TerrainGrid.ChunkSize) + "청크.\n" +
                  "이제 `World Map Editor`로 가장자리를 다듬고, `5. Place Sites From World Map`을 다시 돌릴 것.");
    }
}
