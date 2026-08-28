using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **청크 콘텐츠 에셋을 읽고 쓰는 자리 — 하나다**(2026-08-27).
//
// 여기에 쓰는 도구가 셋이다(이행 · `5. Place Sites` · `B. Save Selection As Site`). 각자
// `AssetDatabase.CreateAsset`을 부르면 **경로 규약과 guid 보존 규칙이 세 벌**이 되고, 하나만
// 어긋나도 *"도구가 쓴 자리를 런타임이 안 읽는"* 꼴이 된다.
public static class WorldContentAssets
{
    public const string Dir = "Assets/Resources/" + WorldChunkContent.ResourceDir;

    public static string PathOf(Vector2Int chunk) => Dir + "/" + TerrainGrid.KeyOf(chunk) + ".asset";

    public static WorldChunkContent Load(Vector2Int chunk) =>
        AssetDatabase.LoadAssetAtPath<WorldChunkContent>(PathOf(chunk));

    // 청크 하나를 통째로 쓴다. **비면 에셋을 지운다** — 남겨 두면 씬에서 지운 마을이
    // 게임에는 그대로 선다(빈 에셋과 "없음"은 같은 뜻이어야 한다).
    public static void Save(Vector2Int chunk, List<WorldObject> objects)
    {
        string path = PathOf(chunk);

        if (objects == null || objects.Count == 0)
        {
            if (Load(chunk) != null) AssetDatabase.DeleteAsset(path);
            return;
        }

        System.IO.Directory.CreateDirectory(Dir);

        // **있던 것을 다시 쓴다** — 지우고 새로 만들면 guid가 바뀐다(`TerrainDeltaBaker`가
        // 델타에 대해 지키는 그 규칙. 지금은 아무도 이 에셋을 guid로 안 가리키지만, 한 번
        // 어기면 나중에 가리키는 쪽이 생겼을 때 조용히 끊긴다).
        var asset = Load(chunk);
        bool isNew = asset == null;
        if (isNew) asset = ScriptableObject.CreateInstance<WorldChunkContent>();

        asset.Chunk   = chunk;
        asset.Objects = objects.ToArray();

        if (isNew) AssetDatabase.CreateAsset(asset, path);
        else       EditorUtility.SetDirty(asset);
    }

    // 이미 있는 청크 전부. **손으로 놓은 것을 지키려면 먼저 읽어야 한다.**
    public static List<Vector2Int> AllChunks()
    {
        var list = new List<Vector2Int>();
        if (!System.IO.Directory.Exists(Dir)) return list;

        foreach (var guid in AssetDatabase.FindAssets("t:WorldChunkContent", new[] { Dir }))
        {
            var a = AssetDatabase.LoadAssetAtPath<WorldChunkContent>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) list.Add(a.Chunk);
        }
        return list;
    }

    // 그 청크의 목록을 읽어 온다(없으면 빈 목록). **원본을 그대로 넘기지 않는다** —
    // 부르는 쪽이 고치면 에셋이 저 몰래 바뀐다.
    public static List<WorldObject> Read(Vector2Int chunk)
    {
        var a = Load(chunk);
        var list = new List<WorldObject>();
        if (a?.Objects != null) list.AddRange(a.Objects);
        return list;
    }

    // 저작 도구가 쓰고 나서 부른다 — 저장 · 새로고침 · 런타임 캐시 비우기가 **늘 같이** 가야
    // 한다. 캐시를 안 비우면 에디터가 옛 목록을 계속 쓴다.
    public static void Flush()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        WorldContentStreamer.ForgetContent();
        WorldSites.Forget();
    }

    // ── 신원 ────────────────────────────────────────────────────────────────

    // **도구가 뽑은 것은 결정적으로 짓는다** — `5. Place Sites`를 다시 돌려도 같은 소굴이
    // 같은 신원을 유지해야, `Persistent` 추적과 (나중의) 세이브가 안 어긋난다.
    public static string MapId(WorldNodeContent kind, Vector2Int region) =>
        "map_" + kind + "_" + region.x + "_" + region.y;

    // **1단계 이행이 낸 옛 형식.** 그때는 이름에서 되짚었어서 표시명이 들어갔다
    // (`map_적 소굴_12_12`). 도구가 그것도 **알아보고 지금 형식으로 갈아 끼운다** —
    // 안 그러면 다음 배치 때 같은 소굴을 *처음 보는 것*과 *지도에 없는 것*으로 동시에 보아
    // 지우고 다시 세운다(손으로 옮긴 자리가 사라진다).
    public static string LegacyMapId(WorldNodeContent kind, Vector2Int region) =>
        "map_" + kind.DisplayName() + "_" + region.x + "_" + region.y;

    // 손으로 놓은 것. **이름에서 뽑지 않는다** — 이름은 바뀌라고 있는 것이다.
    public static string NewId() => System.Guid.NewGuid().ToString("N");
}
