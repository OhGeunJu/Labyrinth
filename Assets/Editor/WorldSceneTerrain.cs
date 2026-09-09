using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **저작 씬에서도 땅을 밟는다**(2026-08-27).
//
// 저작 씬에 세계를 펼쳐 놓고 열면 **마을이 허공에 떠 있었다.** 데이터로는 맞다 — 물건은
// 높이를 저장하지 않고(XZ + `YOffset`), 런타임이 지면에 붙인다. 하지만 **저작하는 사람이
// 보는 것**은 다른 문제다. 땅이 안 보이면 *"저 산 너머에 마을을 놓자"*가 성립하지 않는다.
//
// 땅을 씬에 통째로 넣을 수는 없다(정점 6,712만 · 550MB). **그래서 에디터에서도 스트리밍을
// 돌린다** — 런타임이 뷰어 둘레만 세우는 것과 똑같이, 여기서는 **씬 뷰 카메라 둘레**만 세운다.
//
// ── 지키는 것 ───────────────────────────────────────────────────────────────
//
// · **씬에 저장되지 않는다**(`HideFlags.DontSave`). 안 걸면 25청크가 `World.unity`에 박힌다.
// · **떠나면 `TerrainData`까지 버린다.** 오브젝트만 지우면 돌아다닐수록 샌다(런타임 스트리머가
//   이미 겪은 자리다).
// · **콜라이더를 단다.** 프리팹을 씬 뷰로 끌어다 놓을 때 유니티가 콜라이더에 스냅하므로,
//   없으면 물건이 원점 근처에 떨어진다. `E. World Preview`(저해상도 한 장)에 콜라이더를 안
//   다는 것과 **정반대 이유**다 — 저쪽은 부정확해서 짚으면 안 되고, 이쪽은 진짜 높이다.
// · **조각 중인 청크는 건너뛴다**(`__TerrainEdit`). 같은 자리에 둘이 서면 z-파이팅이 나고,
//   무엇을 조각하고 있는지 안 보인다.
// · **플레이 중에는 안 돈다.** 그때는 진짜 `TerrainStreamer`가 세운다.
// · **저작 루트가 없는 씬에서는 안 돈다.** 게임 씬만 열어 놓고 작업할 때 끼어들면 안 된다.
[InitializeOnLoad]
public static class WorldSceneTerrain
{
    const string RootName = "__WorldSceneTerrain";
    const string PrefKey  = "DungeonColony.WorldSceneTerrain";

    // **런타임과 따로 둔다**(2026-08-27). 전에는 `TerrainStreamer.radius`와 같은 2였는데,
    // 둘은 다른 물음이다 — 런타임은 *프레임을 지키면서 몇 청크까지 세울 수 있나*이고,
    // 저작은 *무엇을 보면서 놓는가*다. **플레이를 하는 것도 아닌데 좁게 볼 이유가 없다** —
    // 마을 하나를 놓으려면 둘레의 지형이 보여야 하고, 1,280m로는 그 산이 어디서 끝나는지가
    // 안 보인다.
    //
    // **값은 제곱으로 붙는다** — 반경 4면 9×9 = 81청크(2,304m)고 하나당 129² 높이와
    // 64² 알파맵을 굽는다. 그래서 **환경설정에 둔다**(`World / Scene Terrain Radius`) —
    // 무거우면 줄이면 된다.
    const int DefaultRadius = 4;
    const string RadiusKey  = "DungeonColony.WorldSceneTerrain.Radius";

    public static int Radius
    {
        get => Mathf.Clamp(EditorPrefs.GetInt(RadiusKey, DefaultRadius), 1, 8);
        set
        {
            EditorPrefs.SetInt(RadiusKey, Mathf.Clamp(value, 1, 8));
            _center = new Vector2Int(int.MinValue, int.MinValue);   // 다음 틱에 다시 고른다
        }
    }

    // 한 번에 하나만 세운다. **콜라이더 굽기가 제일 무겁다** — 런타임이 `buildsPerFrame`을
    // 1로 두는 그 이유가 에디터에서도 그대로다.
    const double Interval = 0.1;

    static readonly Dictionary<Vector2Int, Terrain> _live = new();
    static double _next;
    static Vector2Int _center = new(int.MinValue, int.MinValue);

    static WorldSceneTerrain()
    {
        // **도메인 리로드를 건너 살아남은 것을 도로 잡는다.** `DontSave`는 리로드로 안 죽어서,
        // 안 잡으면 화면에는 있는데 사전은 비어 **영영 안 치워지는 청크**가 남는다.
        Adopt();
        EditorApplication.update += Tick;
    }

    // ── 켜고 끄기 ───────────────────────────────────────────────────────────

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set { EditorPrefs.SetBool(PrefKey, value); if (!value) Clear(); }
    }

    [MenuItem("DungeonColony/Authoring/Terrain In Authoring Scene")]
    static void Toggle() => Enabled = !Enabled;

    [MenuItem("DungeonColony/Authoring/Terrain In Authoring Scene", true)]
    static bool ToggleCheck()
    {
        Menu.SetChecked("DungeonColony/Authoring/Terrain In Authoring Scene", Enabled);
        return true;
    }

    // 반경은 눈으로 맞추는 값이라 **메뉴에 둔다** — 숫자를 코드에서 고치게 하면 안 맞춘다.
    const string RadiusMenu = "DungeonColony/Authoring/Scene Terrain Radius/";

    [MenuItem(RadiusMenu + "2 — 1,280m (좁게)")] static void R2() => Radius = 2;
    [MenuItem(RadiusMenu + "3 — 1,792m")]        static void R3() => Radius = 3;
    [MenuItem(RadiusMenu + "4 — 2,304m (기본)")] static void R4() => Radius = 4;
    [MenuItem(RadiusMenu + "6 — 3,328m")]        static void R6() => Radius = 6;
    [MenuItem(RadiusMenu + "8 — 4,352m (무겁다)")] static void R8() => Radius = 8;

    [MenuItem(RadiusMenu + "2 — 1,280m (좁게)", true)] static bool R2C() => Check(2, "2 — 1,280m (좁게)");
    [MenuItem(RadiusMenu + "3 — 1,792m", true)]        static bool R3C() => Check(3, "3 — 1,792m");
    [MenuItem(RadiusMenu + "4 — 2,304m (기본)", true)] static bool R4C() => Check(4, "4 — 2,304m (기본)");
    [MenuItem(RadiusMenu + "6 — 3,328m", true)]        static bool R6C() => Check(6, "6 — 3,328m");
    [MenuItem(RadiusMenu + "8 — 4,352m (무겁다)", true)] static bool R8C() => Check(8, "8 — 4,352m (무겁다)");

    static bool Check(int r, string item)
    {
        Menu.SetChecked(RadiusMenu + item, Radius == r);
        return true;
    }

    // ── 도는 자리 ───────────────────────────────────────────────────────────

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Clear(); return; }
        if (!Enabled) return;                                    // 끌 때 이미 치웠다

        // **저작 루트가 있는 씬에서만.** 게임 씬만 열어 놓고 작업할 때 끼어들면 안 된다.
        if (WorldAuthoringScene.FindRoot() == null) { Clear(); return; }

        var view = SceneView.lastActiveSceneView;
        if (view == null) return;

        if (EditorApplication.timeSinceStartup < _next) return;
        _next = EditorApplication.timeSinceStartup + Interval;

        var center = TerrainGrid.ChunkAt(view.pivot);

        // 자리도 그대로고 빠진 것도 없으면 아무것도 안 한다 — 매 틱 사전을 훑을 이유가 없다.
        if (center == _center && _live.Count == Wanted(center).Count) return;
        _center = center;

        // 지형 프로필이 없으면 **아무것도 세우지 않는다** — 기본값으로 세우면 게임과 다른
        // 땅이 서고, 그 위에 놓은 마을이 게임에서 뜨거나 묻힌다.
        var streamer = TerrainAuthoring.SyncValues();
        if (streamer == null) { Clear(); return; }

        var wanted = Wanted(center);

        // ① 벗어난 것 버리기 — **먼저 버린다**(잠깐 둘 다 올라가 있지 않게).
        var stale = new List<Vector2Int>();
        foreach (var kv in _live) if (!wanted.Contains(kv.Key)) stale.Add(kv.Key);
        foreach (var c in stale) Unload(c);

        // ② 없는 것 하나만 세우기 — 가운데부터.
        Vector2Int? pick = null;
        int best = int.MaxValue;
        foreach (var c in wanted)
        {
            if (_live.ContainsKey(c)) continue;
            int d = (c.x - center.x) * (c.x - center.x) + (c.y - center.y) * (c.y - center.y);
            if (d < best) { best = d; pick = c; }
        }

        if (pick.HasValue) Build(pick.Value, streamer);
        if (stale.Count > 0 || pick.HasValue) Terrain.SetConnectivityDirty();
    }

    static HashSet<Vector2Int> Wanted(Vector2Int center)
    {
        // 조각 중인 청크는 빼 둔다 — 같은 자리에 둘이 서면 z-파이팅이 난다.
        var editing = new HashSet<Vector2Int>();
        var edit = GameObject.Find("__TerrainEdit");
        if (edit != null)
            foreach (var t in edit.GetComponentsInChildren<Terrain>())
                editing.Add(TerrainGrid.ChunkAt(t.transform.position));

        var set = new HashSet<Vector2Int>();
        for (int dz = -Radius; dz <= Radius; dz++)
        for (int dx = -Radius; dx <= Radius; dx++)
        {
            var c = new Vector2Int(center.x + dx, center.y + dz);
            if (TerrainGrid.InWorld(c) && !editing.Contains(c)) set.Add(c);
        }
        return set;
    }

    // ── 세우고 버리기 ───────────────────────────────────────────────────────

    static void Build(Vector2Int chunk, TerrainStreamer streamer)
    {
        int res = TerrainGrid.VertsPerAxis;

        var data = new TerrainData
        {
            name        = "SceneTerrain " + TerrainGrid.KeyOf(chunk),
            hideFlags   = HideFlags.DontSave,
            heightmapResolution = res,
            size = new Vector3(TerrainGrid.ChunkSize, TerrainStreamer.HeightRange, TerrainGrid.ChunkSize),
        };

        // **채우는 식은 런타임·미리보기와 한 벌이다**(`TerrainAuthoring.Fill`) — 갈리면
        // 저작할 때 본 땅과 게임의 땅이 조용히 어긋난다.
        TerrainAuthoring.Fill(data, chunk);   // 높이 + 스플랫이 한 문에서 나온다

        var go = new GameObject("SceneChunk_" + TerrainGrid.KeyOf(chunk)) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(Root(), false);
        go.transform.position = TerrainGrid.OriginOf(chunk) + Vector3.up * TerrainStreamer.MinHeight;

        int layer = LayerMask.NameToLayer("Ground");
        if (layer >= 0) go.layer = layer;

        var terrain = go.AddComponent<Terrain>();
        terrain.terrainData      = data;
        terrain.materialTemplate = streamer.ChunkMaterial();
        terrain.drawInstanced    = true;
        terrain.allowAutoConnect = true;
        terrain.groupingID       = 0;

        // **콜라이더를 단다** — 프리팹을 씬 뷰로 끌어다 놓을 때 여기에 스냅된다.
        go.AddComponent<TerrainCollider>().terrainData = data;

        _live[chunk] = terrain;
    }

    static void Unload(Vector2Int chunk)
    {
        if (!_live.TryGetValue(chunk, out var t)) return;
        _live.Remove(chunk);
        if (t == null) return;

        // **`TerrainData`도 같이 버린다** — 오브젝트만 지우면 돌아다닐수록 샌다.
        var data = t.terrainData;
        Object.DestroyImmediate(t.gameObject);
        if (data != null) Object.DestroyImmediate(data);
    }

    static Transform Root()
    {
        var go = GameObject.Find(RootName);
        if (go == null) go = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;   // 기울거나 커지면 세계가 통째로 어긋난다
        return go.transform;
    }

    // ── 밖에서 부르는 문 ────────────────────────────────────────────────────

    public static void Clear()
    {
        foreach (var c in new List<Vector2Int>(_live.Keys)) Unload(c);
        _live.Clear();
        _center = new Vector2Int(int.MinValue, int.MinValue);

        var go = GameObject.Find(RootName);
        if (go != null) Object.DestroyImmediate(go);
    }

    // 산을 옮기거나 델타를 다시 구운 뒤 부른다 — 세워 둔 것은 만들 때 한 번 채운 메시라
    // 저 혼자 안 따라온다. **다음 틱에 처음부터 세운다.**
    public static void Refresh()
    {
        if (_live.Count == 0) return;
        Clear();
    }

    // 도메인 리로드를 건너 살아남은 오브젝트를 사전에 도로 담는다.
    static void Adopt()
    {
        var go = GameObject.Find(RootName);
        if (go == null) return;

        foreach (var t in go.GetComponentsInChildren<Terrain>())
            _live[TerrainGrid.ChunkAt(t.transform.position)] = t;
    }
}
