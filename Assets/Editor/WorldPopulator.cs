using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **세계를 채운다 — 개수와 자리를 도구가 정한다**(2026-08-27).
//
// 전에는 `5. Place Sites From World Map`이 **저작한 격자 한 칸에 하나씩** 세웠다. 그래서
// 개수가 *"몇 칸을 칠했나"*로 정해졌고 — 실제로 마을 16 · 소굴 96 · 자원지대 16이 나왔다 —
// **한 지역(256m)에 하나**라는 제한이 붙었으며, 자리도 그 칸 안으로 갇혔다.
//
// 이제 **격자는 바이옴만 든다.** 무엇이 어디에 몇 개 서는지는 여기가 정한다.
//
// ── 어떻게 뿌리나 ───────────────────────────────────────────────────────────
//
// **다트 던지기**(dart throwing) — 무작위로 찍고, 너무 가까우면 버린다. 격자로 나눠 하나씩
// 넣으면 **자로 잰 듯 늘어서고**, 완전 무작위로 두면 **뭉치고 빈 데가 생긴다.** 최소 간격만
// 강제하면 자연스럽게 흩어지면서도 사막 한복판이 텅 비지 않는다.
//
// 찍은 자리는 그대로 쓰지 않고 **둘레에서 가장 평평한 데**로 한 번 더 옮긴다 — 산비탈에
// 마을을 세우면 건물이 뜨거나 묻힌다.
//
// **시드 하나에서 전부 나온다.** 같은 시드면 몇 번을 돌려도 같은 세계다(`PawnSpec`과 같은
// 규칙). 마음에 안 들면 시드를 바꿔 다시 굴린다.
//
// ── 무엇이 어디에 서나 ──────────────────────────────────────────────────────
//
// **바이옴 티어가 정한다**(`BiomeTier.Of`, 1~5):
//
//   · **마을** — 티어가 낮을수록 많다. **4티어 이상에는 안 선다** — 용암지대에 사람이 살면
//     *"험한 곳"*이라는 말이 무너진다.
//   · **소굴** — 티어가 높을수록 많다. 그게 위험도의 실체다.
//   · **자원 지대** — 티어가 높을수록 많다. **더 험한 곳에 더 좋은 것**이 `design_progression`의
//     핵심 루프다.
//
// **시작 자리 둘레는 비워 둔다** — 판이 열리자마자 소굴이 문 앞에 있으면 탐험이 아니라 농성이다.
public class WorldPopulator : EditorWindow
{
    // ── 손잡이 ──────────────────────────────────────────────────────────────
    //
    // 기본값은 **밀도로 적는다**(2026-08-27). 그전에는 16,384m 세계에서 뽑은 절대 개수였는데,
    // 세계를 줄이자 **마을 40개가 8km 안에 들어갈 자리가 없어** 간격 조건에 걸려 절반이
    // 조용히 안 섰다(요청한 수와 실제 선 수가 갈렸다). **세계 크기는 손잡이 하나여야 한다** —
    // `TerrainGrid.ChunksPerAxis`를 바꾸면 개수가 따라와야지, 여기서 또 맞추게 하면 안 된다.
    //
    //   마을 15/100km² → 평균 간격 ≈ 2.6km ≈ 걸어서 11분. 보급이 닿을 만하고, 다음 마을이
    //                    어디인지 늘 궁금할 만한 거리다.
    //   소굴 45/100km² → 평균 간격 ≈ 1.5km. 마을 사이에 두세 개가 낀다 — **길이 위험해야 한다.**
    //   자원 22/100km² → 평균 간격 ≈ 2.1km.
    //
    // 8,192m(67km²)면 마을 10 · 소굴 30 · 자원 15가 된다.
    const float TownsPer100  = 15f;
    const float LairsPer100  = 45f;
    const float NodesPer100  = 22f;

    static float Km2 => TerrainGrid.WorldSize * (float)TerrainGrid.WorldSize / 1_000_000f;
    static int   ByArea(float per100) => Mathf.Max(1, Mathf.RoundToInt(per100 * Km2 / 100f));

    int   _seed          = 20260827;
    int   _towns         = -1;      // 처음 열 때 넓이에서 낸다
    int   _lairs         = -1;
    int   _resources     = -1;

    float _townSpacing   = 1800f;   // 마을끼리 (m)
    float _lairSpacing   = 700f;    // 소굴끼리
    float _nearTown      = 450f;    // 마을 코앞에는 아무것도 안 선다
    float _safeRadius    = 1500f;   // 시작 자리 둘레 — 소굴 없음

    float _maxSlope      = 6f;      // 발자국 안에서 이만큼(m) 넘게 기울면 버린다

    void OnEnable()
    {
        if (_towns     < 0) _towns     = ByArea(TownsPer100);
        if (_lairs     < 0) _lairs     = ByArea(LairsPer100);
        if (_resources < 0) _resources = ByArea(NodesPer100);
    }

    [MenuItem("DungeonColony/Terrain/5. Populate World (자동 배치)")]
    static void Open()
    {
        var w = GetWindow<WorldPopulator>("Populate World");
        w.minSize = new Vector2(360f, 420f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("세계를 채운다", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"세계 {TerrainGrid.WorldSize:N0}m — 개수와 자리를 도구가 정한다. " +
            "격자는 이제 바이옴만 든다.", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(6f);
        _seed = EditorGUILayout.IntField("시드", _seed);
        EditorGUILayout.LabelField("같은 시드면 언제나 같은 세계다. 마음에 안 들면 바꿔 다시 굴린다.",
                                   EditorStyles.miniLabel);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("개수", EditorStyles.boldLabel);
        _towns     = EditorGUILayout.IntSlider("마을",      _towns,     0, 200);
        _lairs     = EditorGUILayout.IntSlider("적 소굴",   _lairs,     0, 500);
        _resources = EditorGUILayout.IntSlider("자원 지대", _resources, 0, 300);
        EditorGUILayout.LabelField(Spacing(), EditorStyles.miniLabel);
        if (GUILayout.Button("넓이에 맞게 되돌린다", EditorStyles.miniButton))
        {
            _towns = ByArea(TownsPer100); _lairs = ByArea(LairsPer100); _resources = ByArea(NodesPer100);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("간격 (m)", EditorStyles.boldLabel);
        _townSpacing = EditorGUILayout.Slider("마을끼리",       _townSpacing, 200f, 4000f);
        _lairSpacing = EditorGUILayout.Slider("소굴끼리",       _lairSpacing, 100f, 2000f);
        _nearTown    = EditorGUILayout.Slider("마을 코앞 금지", _nearTown,      0f, 1500f);
        _safeRadius  = EditorGUILayout.Slider("시작 둘레 안전", _safeRadius,    0f, 5000f);
        _maxSlope    = EditorGUILayout.Slider("허용 경사(m)",   _maxSlope,      1f,   30f);

        EditorGUILayout.Space(10f);
        if (GUILayout.Button("전부 지우고 다시 채운다", GUILayout.Height(28f))) Populate();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "자동 배치가 낸 것만 지운다 — 손으로 놓은 것(`Generated` 꺼짐)은 그대로 둔다.\n" +
            "끝나면 `World → 5. Bake World`. 씬 저장도 잊지 말 것.",
            MessageType.None);
    }

    string Spacing()
    {
        float w = TerrainGrid.WorldSize;
        string One(int n) => n <= 0 ? "—" : $"{Mathf.RoundToInt(w / Mathf.Sqrt(n)):N0}m";
        return $"평균 간격 — 마을 {One(_towns)} · 소굴 {One(_lairs)} · 자원 {One(_resources)}";
    }

    // ── 채우기 ──────────────────────────────────────────────────────────────

    void Populate()
    {
        var root = WorldAuthoringScene.FindRoot();
        if (root == null) { WorldAuthoringScene.NoRoot(); return; }

        var content = Object.FindFirstObjectByType<WorldContentStreamer>(FindObjectsInactive.Include);
        if (content == null)
        {
            Debug.LogWarning("[배치] 씬에 WorldContentStreamer가 없다 — 게임 씬을 같이 열어 둘 것.");
            return;
        }
        if (!WorldAuthoringScene.SyncTerrain("배치")) return;

        // ── ① 도구가 낸 것을 전부 치운다 ────────────────────────────────────
        //
        // **손으로 놓은 것은 그대로 둔다.** 그게 없으면 자동 배치를 한 번 돌릴 때마다 손으로
        // 지은 도시가 사라져 **다시 돌릴 엄두가 안 난다.**
        var found   = new List<GameObject>();
        var orphans = new List<GameObject>();
        WorldAuthoringScene.Collect(root.transform, found, orphans);

        int group = Undo.GetCurrentGroup();
        int wiped = 0, kept = 0;

        foreach (var go in found)
        {
            var tag = go.GetComponent<WorldObjectTag>();
            if (tag == null || !tag.Generated) { kept++; continue; }
            Undo.DestroyObjectImmediate(go);
            wiped++;
        }

        // ── ② 뿌린다 ────────────────────────────────────────────────────────
        var placed = new List<(Vector2 pos, WorldNodeContent kind)>();
        var rng    = new System.Random(_seed);
        int made   = 0;

        try
        {
            // **마을을 먼저 놓는다.** 소굴이 자리를 다 차지한 뒤에 마을을 끼우면, 사람 사는
            // 데가 남는 자투리로 밀린다 — 순서가 곧 우선순위다.
            made += Scatter(root, content, placed, rng, WorldNodeContent.Town,         _towns,     0f);
            made += Scatter(root, content, placed, rng, WorldNodeContent.EnemyLair,    _lairs,     _safeRadius);
            made += Scatter(root, content, placed, rng, WorldNodeContent.ResourceSite, _resources, 0f);
        }
        finally { EditorUtility.ClearProgressBar(); }

        Undo.SetCurrentGroupName("세계 채우기");
        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        int t = 0, l = 0, r = 0;
        foreach (var p in placed)
            if (p.kind == WorldNodeContent.Town) t++;
            else if (p.kind == WorldNodeContent.EnemyLair) l++;
            else r++;

        Debug.Log($"[배치] 시드 {_seed} — 마을 {t} · 소굴 {l} · 자원지대 {r} (합 {made}).\n" +
                  $"도구가 낸 옛것 {wiped}개를 지웠고, 손으로 놓은 {kept}개는 그대로 뒀다." +
                  (t < _towns || l < _lairs || r < _resources
                      ? "\n**바란 만큼 다 못 놓았다** — 간격을 좁히거나 허용 경사를 키울 것 " +
                        "(평평한 땅이 모자라면 개수를 못 채운다)."
                      : "") +
                  "\n**아직 안 구웠다** — `World → 5. Bake World`. 씬 저장도 잊지 말 것.");
    }

    int Scatter(WorldAuthoringRoot root, WorldContentStreamer content,
                List<(Vector2 pos, WorldNodeContent kind)> placed, System.Random rng,
                WorldNodeContent kind, int target, float safeRadius)
    {
        var prefab = PrefabFor(kind, content);
        if (prefab == null || target <= 0) return 0;

        float world = TerrainGrid.WorldSize;
        float foot  = Footprint(kind);
        float mine  = kind == WorldNodeContent.Town ? _townSpacing : _lairSpacing;

        Vector2 start = StartPoint();

        // **던지는 횟수에 천장을 둔다** — 평평한 땅이 모자라면 영원히 못 채운다.
        int attempts = Mathf.Max(target * 60, 2000);
        int made = 0;

        for (int i = 0; i < attempts && made < target; i++)
        {
            if ((i & 255) == 0)
                EditorUtility.DisplayProgressBar("세계를 채운다",
                    $"{kind.DisplayName()} {made}/{target}", made / (float)target);

            var p = new Vector2((float)rng.NextDouble() * world, (float)rng.NextDouble() * world);

            // 시작 둘레는 비워 둔다.
            if (safeRadius > 0f && (p - start).sqrMagnitude < safeRadius * safeRadius) continue;

            var biome = WorldMap.BiomeAtWorld(p);
            if (!Allowed(kind, biome)) continue;

            // **티어로 솎는다** — 통과 확률이 곧 밀도다. 지역마다 개수를 따로 세는 것보다
            // 단순하고, 바이옴 경계가 자연스럽게 흐려진다.
            if (rng.NextDouble() > Density(kind, biome)) continue;

            // 둘레에서 가장 평평한 데로 한 번 옮긴다.
            p = Flattest(p, foot, rng);
            if (Slope(p, foot) > _maxSlope) continue;

            if (TooClose(placed, p, kind, mine)) continue;

            Create(root, prefab, kind, p, made, rng);
            placed.Add((p, kind));
            made++;
        }
        return made;
    }

    // ── 규칙 ────────────────────────────────────────────────────────────────

    // **4티어 이상에는 마을이 안 선다** — 용암지대에 사람이 살면 "험한 곳"이라는 말이 무너진다.
    static bool Allowed(WorldNodeContent kind, Biome b) =>
        kind != WorldNodeContent.Town || BiomeTier.Of(b) <= 3;

    // 그 바이옴에서 이 종류가 얼마나 자주 뽑히나(0~1).
    static float Density(WorldNodeContent kind, Biome b)
    {
        int tier = BiomeTier.Of(b);   // 1~5
        return kind switch
        {
            // 낮은 티어일수록 사람이 산다. 1티어 1.0 → 3티어 0.4.
            WorldNodeContent.Town => Mathf.Lerp(1f, 0.4f, (tier - 1) / 2f),
            // 높은 티어일수록 험하다. 1티어 0.35 → 5티어 1.0.
            WorldNodeContent.EnemyLair => Mathf.Lerp(0.35f, 1f, (tier - 1) / 4f),
            // 좋은 것은 험한 데 있다 — `design_progression`의 루프 그 자체다.
            _ => Mathf.Lerp(0.5f, 1f, (tier - 1) / 4f),
        };
    }

    // 그 자리에 들어설 것이 얼마나 넓나(m).
    static float Footprint(WorldNodeContent kind) => kind switch
    {
        WorldNodeContent.Town      => 20f,
        WorldNodeContent.EnemyLair => 12f,
        _                          => 8f,
    };

    bool TooClose(List<(Vector2 pos, WorldNodeContent kind)> placed, Vector2 p,
                  WorldNodeContent kind, float mine)
    {
        float mineSqr = mine * mine;
        float townSqr = _nearTown * _nearTown;

        foreach (var o in placed)
        {
            float d = (o.pos - p).sqrMagnitude;

            // **마을 코앞에는 아무것도 안 선다** — 소굴이 성문 앞에 있으면 마을이 하루도 못 산다.
            if (o.kind == WorldNodeContent.Town && d < townSqr) return true;
            if (o.kind == kind && d < mineSqr) return true;
        }
        return false;
    }

    // ── 자리 고르기 ─────────────────────────────────────────────────────────

    // 찍은 자리 둘레에서 가장 평평한 데. **찍은 자리를 그대로 쓰지 않는다** — 무작위로 찍으면
    // 절반은 비탈이다.
    static Vector2 Flattest(Vector2 around, float foot, System.Random rng)
    {
        const int   Tries  = 16;
        const float Radius = 160f;

        Vector2 best = around;
        float bestSlope = Slope(around, foot);

        for (int i = 0; i < Tries; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt((float)rng.NextDouble()) * Radius;
            var p = new Vector2(around.x + Mathf.Cos(a) * r, around.y + Mathf.Sin(a) * r);

            if (p.x < 0f || p.y < 0f || p.x >= TerrainGrid.WorldSize || p.y >= TerrainGrid.WorldSize)
                continue;

            float s = Slope(p, foot);
            if (s < bestSlope) { bestSlope = s; best = p; }
        }
        return best;
    }

    // 발자국 네 모서리의 높이차. `MapManager.IsFlatEnough`가 보는 것과 같은 축.
    static float Slope(Vector2 p, float d)
    {
        float a = TerrainHeight.Procedural(p.x - d, p.y - d);
        float b = TerrainHeight.Procedural(p.x + d, p.y - d);
        float c = TerrainHeight.Procedural(p.x - d, p.y + d);
        float e = TerrainHeight.Procedural(p.x + d, p.y + d);
        return Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, e)) - Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, e));
    }

    static Vector2 StartPoint()
    {
        var c = TerrainGrid.RegionCenter(WorldMap.AuthoredStart);
        return new Vector2(c.x, c.z);
    }

    // ── 세우기 ──────────────────────────────────────────────────────────────

    void Create(WorldAuthoringRoot root, GameObject prefab, WorldNodeContent kind,
                Vector2 p, int index, System.Random rng)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.gameObject.scene);
        if (go == null) return;

        go.transform.SetParent(root.transform, false);
        go.transform.SetPositionAndRotation(
            new Vector3(p.x, TerrainHeight.At(p.x, p.y), p.y),
            Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));

        go.name = $"{kind.DisplayName()} {index}";

        var tag = go.GetComponent<WorldObjectTag>() ?? go.AddComponent<WorldObjectTag>();

        // **신원은 시드와 순번에서 결정적으로 짓는다** — 같은 시드로 다시 채우면 같은 소굴이
        // 같은 신원을 유지하므로, `Persistent` 추적과 상점 재고가 안 어긋난다.
        tag.Id        = $"gen_{_seed}_{kind}_{index}";
        tag.Kind      = kind;
        tag.Generated = true;

        // **마을은 안 내린다** — 안의 창고·폰·건물이 처음으로 돌아가면 안 된다.
        tag.Persistent = kind != WorldNodeContent.ResourceSite;

        Undo.RegisterCreatedObjectUndo(go, "장소 세우기");
    }

    static GameObject PrefabFor(WorldNodeContent kind, WorldContentStreamer c) => kind switch
    {
        WorldNodeContent.Town         => c.TownPrefab,
        WorldNodeContent.EnemyLair    => c.LairPrefab,
        WorldNodeContent.ResourceSite => c.ResourceSitePrefab,
        _                             => null,
    };
}
