using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **저작한 지도가 세계의 마을 배치가 된다**(2026-08-26).
//
// 마을·소굴은 이미 `WorldMapData`(16×16)에 찍혀 있다. 이어진 세계에서 그 격자는 *"이동하는
// 칸"*이 아니라 **1,024m짜리 지역**이고, 칸에 찍힌 내용물은 **그 지역 어딘가에 실제로 선다**는
// 뜻이 된다. **다시 그릴 것이 없다.**
//
// **좌표를 손으로 옮겨 적지 않는다** — 그러면 지도와 세계가 두 벌이 되어 반드시 어긋난다.
// 지도를 고치고 이 도구를 다시 돌리면 세계가 따라온다.
//
// ── 지역 안 어디에 서나 ─────────────────────────────────────────────────────
//
// 가운데에 박으면 **1,024m 격자가 눈에 보인다** — 마을이 자로 잰 듯 늘어선다. 칸 좌표에서
// 결정적으로 흔들어(`WorldDeposits`·`TownMarket`이 쓰는 그 방식) 자연스럽게 흩되, **같은
// 지도면 언제나 같은 자리**다.
//
// **평평한 데를 고른다** — 산비탈에 마을을 세우면 건물이 공중에 뜨거나 파묻힌다.
// 후보를 몇 군데 굴려 보고 가장 완만한 곳을 쓴다.
public static class WorldSitePlacer
{
    const int    Candidates  = 12;     // 지역 하나당 후보 수
    const float  EdgeInset   = 0.18f;  // 지역 가장자리에서 이만큼 안쪽에만 (이웃과 안 붙게)

    [MenuItem("DungeonColony/Terrain/5. Place Sites From World Map")]
    static void Place()
    {
        var content = Object.FindFirstObjectByType<WorldContentStreamer>();
        if (content == null) { Debug.LogWarning("[장소] 씬에 WorldContentStreamer가 없다 — `0. Setup World Streaming` 먼저."); return; }

        TerrainHeight.ForgetBiomes();
        var streamer = Object.FindFirstObjectByType<TerrainStreamer>();
        if (streamer != null)
        {
            TerrainHeight.Profile  = streamer.Profile;
            TerrainHeight.Features = streamer.Features;
        }

        int n = TerrainGrid.RegionsPerAxis;
        var made = new List<(WorldNodeContent kind, Vector2Int region, Vector2 pos, GameObject prefab)>();

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            var kind = WorldMap.AuthoredContent(x, y);
            var prefab = PrefabFor(kind, content);
            if (prefab == null) continue;

            var region = new Vector2Int(x, y);
            made.Add((kind, region, PickSpot(region), prefab));
        }

        if (made.Count == 0)
        {
            Debug.LogWarning("[장소] 세울 것이 없다 — 지도에 마을·소굴이 안 찍혔거나 프리팹이 안 물렸다 " +
                             "(WorldContentStreamer 인스펙터의 '지도에서 세울 프리팹').");
            return;
        }

        var so  = new SerializedObject(content);
        var arr = so.FindProperty("sites");

        // **손으로 놓은 것은 지킨다.** 목록을 통째로 다시 쓰면 씬에서 지어 등록한 도시가
        // 같이 지워진다 — 그러면 이 도구를 다시 돌릴 엄두가 안 난다(연구 그래프 베이커가
        // 옮긴 노드를 지키는 것과 같은 자리).
        var kept = new List<int>();
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            var fromMap = e.FindPropertyRelative("FromMap");
            if (fromMap == null || !fromMap.boolValue) kept.Add(i);
        }

        // 지킬 것을 앞으로 당긴 뒤 그 뒤에 새로 쓴다.
        for (int k = 0; k < kept.Count; k++)
            if (kept[k] != k) arr.MoveArrayElement(kept[k], k);
        arr.arraySize = kept.Count + made.Count;

        for (int i = 0; i < made.Count; i++)
        {
            var m = made[i];
            var e = arr.GetArrayElementAtIndex(kept.Count + i);
            e.FindPropertyRelative("FromMap").boolValue = true;
            e.FindPropertyRelative("Name").stringValue            = $"{m.kind.DisplayName()} {m.region.x},{m.region.y}";
            e.FindPropertyRelative("Prefab").objectReferenceValue = m.prefab;
            e.FindPropertyRelative("Position").vector2Value       = m.pos;
            e.FindPropertyRelative("YOffset").floatValue          = 0f;
            e.FindPropertyRelative("Rotation").floatValue         = Rand(m.region, 7) * 360f;

            // **마을은 안 내린다** — 안의 창고·폰·건물이 처음으로 돌아가면 안 된다.
            // 자원 지대는 장식에 가까워 내려도 된다.
            e.FindPropertyRelative("Persistent").boolValue = m.kind != WorldNodeContent.ResourceSite;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(content);

        int towns = 0, lairs = 0, res = 0;
        foreach (var m in made)
            if (m.kind == WorldNodeContent.Town) towns++;
            else if (m.kind == WorldNodeContent.EnemyLair) lairs++;
            else res++;

        Debug.Log($"[장소] 지도에서 {made.Count}곳을 세웠다 — 마을 {towns} · 소굴 {lairs} · 자원지대 {res}. " +
                  $"지역 {TerrainGrid.RegionSize}m 안에서 가장 평평한 자리를 골랐다. " +
                  $"손으로 놓은 {kept.Count}곳은 그대로 뒀다.");
    }

    static GameObject PrefabFor(WorldNodeContent kind, WorldContentStreamer c) => kind switch
    {
        WorldNodeContent.Town         => c.TownPrefab,
        WorldNodeContent.EnemyLair    => c.LairPrefab,
        WorldNodeContent.ResourceSite => c.ResourceSitePrefab,
        _                             => null,
    };

    // 지역 안에서 **가장 평평한** 자리. 후보를 결정적으로 굴려 경사를 재고 제일 나은 것.
    static Vector2 PickSpot(Vector2Int region)
    {
        float size = TerrainGrid.RegionSize;
        float lo = size * EdgeInset, hi = size * (1f - EdgeInset);

        Vector2 best = default;
        float bestSlope = float.MaxValue;

        for (int i = 0; i < Candidates; i++)
        {
            float x = region.x * size + Mathf.Lerp(lo, hi, Rand(region, i * 2 + 1));
            float z = region.y * size + Mathf.Lerp(lo, hi, Rand(region, i * 2 + 2));

            float s = Slope(x, z);
            if (s < bestSlope) { bestSlope = s; best = new Vector2(x, z); }
        }
        return best;
    }

    // 폰·건물이 놓일 넓이(±8m)에서의 높이차. `MapManager.IsFlatEnough`가 보는 것과 같은 축.
    static float Slope(float x, float z)
    {
        const float d = 8f;
        float a = TerrainHeight.Procedural(x - d, z - d);
        float b = TerrainHeight.Procedural(x + d, z - d);
        float c = TerrainHeight.Procedural(x - d, z + d);
        float e = TerrainHeight.Procedural(x + d, z + d);
        float hi = Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, e));
        float lo = Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, e));
        return hi - lo;
    }

    // 칸 좌표에서 결정적으로 — **같은 지도면 언제나 같은 자리**다.
    static float Rand(Vector2Int r, int salt)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)r.x) * 16777619u;
            h = (h ^ (uint)r.y) * 16777619u;
            h = (h ^ (uint)salt) * 16777619u;
            h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
