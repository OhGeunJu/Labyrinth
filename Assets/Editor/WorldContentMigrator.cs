using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// **씬 배열 → 청크 에셋, 한 번만**(2026-08-27).
//
// `WorldContentStreamer.sites`에 쌓여 있던 것을 `Resources/WorldContent/{x}_{y}.asset`으로
// 옮긴다. 옮기고 나면 **배열을 비운다** — 두 벌을 남기면 *어느 쪽이 이기는가*가 또 규칙이
// 되고, 그 규칙은 반드시 어긋난다.
//
// ── `FromMap`이 꺼진 도구 생성분 ────────────────────────────────────────────
//
// `FromMap` 필드는 `WorldSiteAuthoring`과 같이 나중에 생겼다. 그전에 `5. Place Sites`가 뽑아
// 둔 것들은 **전부 `FromMap`이 꺼진 채로 남아 있다.** 그 상태로 `5.`를 다시 돌리면 도구가
// *"손으로 놓은 것"*으로 보고 지나가므로 **같은 소굴이 하나씩 더 선다.**
//
// 그래서 이행할 때 한 번 묻는다. **이름으로 가려낸다** — `5.`가 짓는 이름은
// `"{종류} {지역x},{지역y}"` 한 형식뿐이라 알아볼 수 있다. **묻지 않고 고치지는 않는다**:
// 손으로 그 형식대로 이름 붙인 것이 있을 수 있고, 그건 저작자만 안다.
public static class WorldContentMigrator
{
    // `5. Place Sites`가 짓는 이름 — `"적 소굴 12,12"`.
    static readonly Regex ToolName = new(@"\s\d+,\d+$");

    [MenuItem("DungeonColony/World/1. Migrate Sites To Chunk Assets")]
    static void Migrate()
    {
        var streamer = Object.FindFirstObjectByType<WorldContentStreamer>(FindObjectsInactive.Include);
        if (streamer == null)
        {
            Debug.LogWarning("[이행] 씬에 WorldContentStreamer가 없다.");
            return;
        }

        var old = streamer.LegacySites;
        if (old == null || old.Length == 0)
        {
            Debug.Log("[이행] 옛 목록이 비어 있다 — 옮길 것이 없다. " +
                      "(이미 옮겼다면 정상이다.)");
            return;
        }

        // ── 도구 생성분으로 보이는데 FromMap이 꺼진 것 ──────────────────────
        int suspect = 0;
        foreach (var s in old)
            if (s != null && !s.FromMap && s.Name != null && ToolName.IsMatch(s.Name)) suspect++;

        bool markFromMap = false;
        if (suspect > 0)
        {
            markFromMap = EditorUtility.DisplayDialog(
                "도구 생성분으로 보이는 것이 있다",
                $"이름이 `5. Place Sites` 형식(\"종류 x,y\")인데 FromMap이 꺼진 항목이 {suspect}개 있다.\n\n" +
                "이 필드는 나중에 생겨서, 그전에 도구가 뽑아 둔 것은 전부 꺼져 있다.\n" +
                "끈 채로 두면 `5.`를 다시 돌릴 때 도구가 지나가므로 **같은 것이 하나씩 더 선다.**\n\n" +
                "도구 생성분으로 표시할까?",
                "표시한다 (권장)", "그대로 둔다");
        }

        // ── 좌표로 청크에 나눈다 — **주인은 좌표가 정한다** ─────────────────
        var byChunk = new Dictionary<Vector2Int, List<WorldObject>>();
        int outside = 0;

        foreach (var s in old)
        {
            if (s == null || s.Prefab == null) continue;

            var chunk = TerrainGrid.ChunkAt(s.Position.x, s.Position.y);
            if (!TerrainGrid.InWorld(chunk)) { outside++; continue; }

            bool fromMap = s.FromMap || (markFromMap && s.Name != null && ToolName.IsMatch(s.Name));

            if (!byChunk.TryGetValue(chunk, out var list))
                byChunk[chunk] = list = new List<WorldObject>();

            list.Add(new WorldObject
            {
                // **도구 생성분은 결정적 신원**을 준다 — `5.`를 다시 돌려도 같은 것이 유지된다.
                Id         = fromMap ? MapIdOf(s, chunk) : WorldContentAssets.NewId(),
                Name       = s.Name,
                Prefab     = s.Prefab,
                Position   = s.Position,
                YOffset    = s.YOffset,
                Yaw        = s.Rotation,
                Persistent = s.Persistent,
                Generated  = fromMap,
            });
        }

        // ── 쓴다. **덮어쓰지 않고 합친다** — 이미 옮겨 둔 청크가 있을 수 있다 ──
        foreach (var kv in byChunk)
        {
            var existing = WorldContentAssets.Read(kv.Key);

            // 같은 신원이 이미 있으면 건너뛴다 — 두 번 실행해도 두 배가 되면 안 된다.
            var have = new HashSet<string>();
            foreach (var e in existing) if (!string.IsNullOrEmpty(e.Id)) have.Add(e.Id);

            foreach (var o in kv.Value)
                if (string.IsNullOrEmpty(o.Id) || have.Add(o.Id)) existing.Add(o);

            WorldContentAssets.Save(kv.Key, existing);
        }

        // ── 옛 배열을 비운다 ────────────────────────────────────────────────
        var so = new SerializedObject(streamer);
        so.FindProperty("sites").arraySize = 0;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(streamer);

        WorldContentAssets.Flush();

        Debug.Log($"[이행] {old.Length}개를 청크 {byChunk.Count}개로 옮겼다 → {WorldContentAssets.Dir}\n" +
                  (markFromMap ? $"도구 생성분 {suspect}개를 `FromMap`으로 표시했다. " : "") +
                  (outside > 0 ? $"**세계 밖 {outside}개는 버렸다.** " : "") +
                  "옛 배열은 비웠다 — **씬을 저장할 것**(Ctrl+S). " +
                  "안 저장하면 다음에 열 때 옛 목록이 그대로 돌아온다.");
    }

    // 도구가 뽑은 이름에서 지역 좌표를 되짚는다. 못 짚으면 자리에서 낸다 —
    // **결정적이기만 하면 된다**(같은 것이 같은 신원을 갖는 것이 목적이다).
    static string MapIdOf(WorldSite s, Vector2Int chunk)
    {
        var m = ToolName.Match(s.Name ?? "");
        if (m.Success)
        {
            var parts = m.Value.Trim().Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int rx) && int.TryParse(parts[1], out int ry))
            {
                string kind = s.Name.Substring(0, s.Name.Length - m.Value.Length).Trim();
                return "map_" + kind + "_" + rx + "_" + ry;
            }
        }
        return "map_" + TerrainGrid.KeyOf(chunk) + "_" +
               Mathf.RoundToInt(s.Position.x) + "_" + Mathf.RoundToInt(s.Position.y);
    }

    // ── 확인용 ──────────────────────────────────────────────────────────────

    [MenuItem("DungeonColony/World/2. Report World Content")]
    static void Report()
    {
        var chunks = WorldContentAssets.AllChunks();
        if (chunks.Count == 0)
        {
            Debug.Log("[월드콘텐츠] 청크 에셋이 하나도 없다 — 아직 안 옮겼거나 세계가 비어 있다.");
            return;
        }

        int total = 0, fromMap = 0, persistent = 0, noId = 0, noPrefab = 0;
        foreach (var c in chunks)
            foreach (var o in WorldContentAssets.Read(c))
            {
                total++;
                if (o.Generated) fromMap++;
                if (o.Persistent) persistent++;
                if (string.IsNullOrEmpty(o.Id)) noId++;
                if (o.Prefab == null) noPrefab++;
            }

        Debug.Log($"[월드콘텐츠] 청크 {chunks.Count}개 · 물건 {total}개 — " +
                  $"지도에서 뽑은 것 {fromMap} · 안 내리는 것 {persistent}." +
                  (noId > 0     ? $"\n**신원 없는 것 {noId}개** — `Persistent` 추적이 자리로 대신한다." : "") +
                  (noPrefab > 0 ? $"\n**프리팹이 빈 것 {noPrefab}개** — 그건 안 선다." : ""));
    }
}
