using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **세계를 채운다 — 개수와 자리를 도구가 정한다**(2026-08-27).
//
// 전에는 `5. Place Sites From World Map`이 **저작한 격자 한 칸에 하나씩** 세웠다. 그래서
// 개수가 *"몇 칸을 칠했나"*로 정해졌고 — 실제로 마을 16 · 소굴 96이 나왔다 —
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
//
// **자원은 여기서 안 놓는다**(2026-09-14). 자원 지대를 한 덩어리씩 세우던 것을 걷어냈다 —
// *"어디에 무엇이 나나"*는 `BiomeContentData.ResourceNodes` **한 표**가 답하고, 그것을
// 벽(`RockMap.VeinAt`)과 바닥(`ScatterRule.FromBiomePool`)이 같이 읽는다. 여기서 또 놓으면
// **같은 물음에 답이 셋**이 되고, 그중 하나만 *손으로 찍은 자리*에 매여 있었다.
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
    //
    // 8,192m(67km²)면 마을 10 · 소굴 30이 된다.
    const float TownsPer100  = 15f;
    const float LairsPer100  = 45f;

    static float Km2 => TerrainGrid.WorldSize * (float)TerrainGrid.WorldSize / 1_000_000f;
    static int   ByArea(float per100) => Mathf.Max(1, Mathf.RoundToInt(per100 * Km2 / 100f));

    int   _seed          = 20260827;
    int   _towns         = -1;      // 처음 열 때 넓이에서 낸다
    int   _lairs         = -1;

    // **간격도 넓이에서 낸다**(2026-08-30). 절대값(1,800m·700m·450m·1,500m)으로 박혀 있었는데,
    // 세계를 256m로 줄이고 나니 **넷 다 세계보다 크다** — 그러면 조건이 뜻을 잃는 정도가
    // 아니라 **아무것도 못 놓는다**: 시작 둘레 1,500m를 비우라는 규칙 하나로 **세계 전체가
    // 금지 구역**이 되어 소굴이 0개가 됐다. 개수를 밀도로 적은 것과 **같은 자리**다.
    float _townSpacing   = -1f;
    float _lairSpacing   = -1f;
    float _nearTown      = -1f;
    float _safeRadius    = -1f;

    float _maxSlope      = 6f;      // 발자국 안에서 이만큼(m) 넘게 기울면 버린다

    void OnEnable() => Defaults();

    // **간격은 평균 간격에서 낸다** — `세계 한 변 / √개수`가 평균 간격이고, 그보다 작게 잡아야
    // 조건이 *"뭉치지 마라"*로 읽힌다. 크게 잡으면 *"놓지 마라"*가 된다.
    static float Spread(int n, float frac) =>
        TerrainGrid.WorldSize / Mathf.Sqrt(Mathf.Max(1, n)) * frac;

    void Defaults()
    {
        if (_towns     < 0) _towns     = ByArea(TownsPer100);
        if (_lairs     < 0) _lairs     = ByArea(LairsPer100);

        if (_townSpacing < 0f) _townSpacing = Spread(_towns, 0.55f);
        if (_lairSpacing < 0f) _lairSpacing = Spread(_lairs, 0.55f);
        if (_nearTown    < 0f) _nearTown    = _townSpacing * 0.25f;
        if (_safeRadius  < 0f) _safeRadius  = TerrainGrid.WorldSize * 0.18f;
    }

    // **한 번에 다시 세우기가 부르는 문**(2026-08-30). 메뉴 항목은 *창을 여는 것*이라
    // `ExecuteMenuItem`으로 부르면 **창만 뜨고 아무것도 안 놓인다** — 그런데 참을 돌려주므로
    // 묶음 도구는 성공한 줄 알고 다음으로 넘어갔다. 마을이 하나도 안 서 있던 원인이 이거다.
    public static void RunHeadless()
    {
        var w = ScriptableObject.CreateInstance<WorldPopulator>();
        try
        {
            w.Defaults();
            w.Populate();
        }
        finally { Object.DestroyImmediate(w); }
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
        EditorGUILayout.LabelField(Spacing(), EditorStyles.miniLabel);
        if (GUILayout.Button("넓이에 맞게 되돌린다", EditorStyles.miniButton))
        {
            _towns = ByArea(TownsPer100); _lairs = ByArea(LairsPer100);
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
        return $"평균 간격 — 마을 {One(_towns)} · 소굴 {One(_lairs)}";
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
        var rooms  = new List<TerrainFeature>();
        var rng    = new System.Random(_seed);
        int made   = 0;

        BuildReach();

        try
        {
            // **마을을 먼저 놓는다.** 소굴이 자리를 다 차지한 뒤에 마을을 끼우면, 사람 사는
            // 데가 남는 자투리로 밀린다 — 순서가 곧 우선순위다.
            made += Scatter(root, content, placed, rooms, rng, WorldNodeContent.Town,         _towns,     0f);
            made += Scatter(root, content, placed, rooms, rng, WorldNodeContent.EnemyLair,    _lairs,     _safeRadius);
        }
        finally { EditorUtility.ClearProgressBar(); }

        rooms.AddRange(StartRooms());
        WriteRooms(rooms);

        Undo.SetCurrentGroupName("세계 채우기");
        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        int t = 0, l = 0;
        foreach (var p in placed)
            if (p.kind == WorldNodeContent.Town) t++;
            else if (p.kind == WorldNodeContent.EnemyLair) l++;

        Debug.Log($"[배치] 시드 {_seed} — 마을 {t} · 소굴 {l} (합 {made}).\n" +
                  $"도구가 낸 옛것 {wiped}개를 지웠고, 손으로 놓은 {kept}개는 그대로 뒀다." +
                  (t < _towns || l < _lairs
                      ? "\n**바란 만큼 다 못 놓았다** — 간격을 좁히거나 허용 경사를 키울 것 " +
                        "(평평한 땅이 모자라면 개수를 못 채운다)."
                      : "") +
                  "\n**아직 안 구웠다** — `World → 5. Bake World`. 씬 저장도 잊지 말 것.");
    }

    int Scatter(WorldAuthoringRoot root, WorldContentStreamer content,
                List<(Vector2 pos, WorldNodeContent kind)> placed, List<TerrainFeature> rooms,
                System.Random rng, WorldNodeContent kind, int target, float safeRadius)
    {
        var prefab = PrefabFor(kind, content);

        // **조용히 0을 돌려주지 않는다.** 프리팹이 안 물려 있으면 그 종류가 통째로 안 서는데,
        // 화면에는 *"마을이 없다"*로만 보이고 원인이 배치 도구인지 굽기인지 스트리밍인지
        // 가릴 수가 없다 — 이 저장소가 여러 번 겪은 자리다.
        if (prefab == null && target > 0)
            Debug.LogWarning($"[배치] `WorldContentStreamer`에 {kind.DisplayName()} 프리팹이 안 물려 있다 — " +
                             $"{target}개를 통째로 못 놓는다.");

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

            // **굴 위로 미끄러뜨린다**(2026-08-30). 지하는 세계의 3분의 2가 바위인데 여기는
            // 그걸 하나도 안 보고 있었다 — 마을 열에 일곱이 **통짜 바위 속**에 섰고, 화면에는
            // *"마을이 아예 없다"*로만 보인다(벽에 가려 보이지도, 갈 수도 없다).
            //
            // **가장 가까운 굴로 옮긴다** — 자리를 버리지 않는 이유는 개수 때문이다: 바위면
            // 버리게 두면 요청한 수의 3분의 1만 서고 **"바란 만큼 다 못 놓았다"**가 상수가 된다.
            // 그리고 굴 위에 서야 **길이 이어진다**(열린 넓이의 9할이 한 그물이다).
            if (!RockMap.TryNearestOpen(new Vector3(p.x, 0f, p.y), 30f, out var open)) continue;
            p = new Vector2(open.x, open.z);
            if (Slope(p, foot) > _maxSlope) continue;   // 옮겼으니 다시 잰다

            // **길 그물에서 걸어갈 수 있는 굴에만 세운다**(2026-09-15). 큰 굴 중에는 길에 안
            // 닿은 것이 있고 그건 설계다(캐서 찾는 굴) — 그런데 거기 소굴이 서면 **무리가
            // 영영 못 나오고**, 마을이 서면 **갈 수 없는 마을**이다. 가장 가까운 열린 칸은
            // 그걸 모른다.
            if (!Reachable(p)) continue;

            if (TooClose(placed, p, kind, mine)) continue;

            Create(root, prefab, kind, p, made, rng);
            placed.Add((p, kind));
            rooms.Add(Room(kind, p, made));
            made++;
        }
        return made;
    }

    // ── 장소는 제 방을 판다 ────────────────────────────────────────────────
    //
    // 굴 위에 세우는 것만으로는 모자란다 — **통로는 9m인데 마을 발자국은 20m다.** 그대로
    // 두면 마을의 바깥 절반이 벽에 박힌다.
    //
    // **방을 파는 물건은 이미 있다: 천장 특징**(`TerrainCeiling.Features` — *"저작한 방(+)과
    // 기둥(−)"*). 새 표를 만들지 않는 이유가 셋이다: **함수라 저장할 것이 없고**(캔 칸처럼
    // 좌표를 쌓지 않는다) · **미리보기와 길찾기와 건설이 이미 그걸 본다** · 그리고 굽는 쪽이
    // 특징을 *"손댄 것"*으로 안 세므로 **델타가 안 불어난다.**
    //
    // **높이는 저작하지 않고 파생시킨다** — 기준 여유가 바뀌면 방도 같이 깊어져야 한다.
    // 따로 적으면 언젠가 기준보다 얕아지고, 그때 **방을 팠는데 여전히 바위**가 된다.
    static TerrainFeature Room(WorldNodeContent kind, Vector2 center, int index)
    {
        float foot = Footprint(kind);
        var   p    = TerrainCeiling.Effective;

        return new TerrainFeature
        {
            Name   = $"{RoomPrefix}{kind}_{index}",
            Center = center,

            // 가장자리는 부드럽게 닫히므로 **뚫리는 반지름은 반경의 4분의 3쯤**이다 —
            // 발자국이 그 안에 들어가야 해서 넉넉히 잡는다.
            Radius = foot * 1.4f,
            Plateau = 0.6f,

            // 바위(기준 여유)에서 폰 키보다 넉넉히 위로. **여유 있게 올린다** — 딱 맞추면
            // 요철 한 번에 천장이 문턱 아래로 내려가 **방 한가운데가 막힌다.**
            Height = PawnMover.BodyHeight + 4f - p.BaseClearance,
        };
    }

    // ── 시작 자리도 제 방을 판다 ────────────────────────────────────────────
    //
    // **2026-09-15, 사용자:** *"처음 시작 지점이 원래는 막혀 있잖아 — 그냥 크게 뚫어 놓으면
    // 안 돼? 시작을 안 누르면 막혀 있으니까 불편해."*
    //
    // 전에는 시작 자리를 **판이 열릴 때** 팠다(`RockRenderer.Start` → `OpenAround` 12m +
    // `OpenPathToCave`). 그건 캔 칸이라 **런타임 상태**다 — 씬 뷰에서도, `2. Preview Cave Map`
    // 에서도, 플레이를 누르기 전의 어디에서도 시작 자리는 **통짜 바위**였다. 시작 자리는
    // 판마다 같은 곳이므로 사건이 아니라 **지형**이다. 그래서 장소의 방과 같은 물건(천장
    // 특징)으로 판다 — 함수라 **에디터·미리보기·길찾기·게임이 전부 같은 굴을 본다.**
    //
    // **자연 굴에 잇는 것까지 여기서 한다.** 길을 성기게 한 뒤로 시작 칸 한복판이 큰길에서
    // 수십 m 떨어질 수 있다 — 방만 파면 *크게 뚫린 섬*이다. 가장 가까운 **자연 굴**
    // (`TerrainCeiling.NaturalClearance` — 지난번에 판 방을 안 센다)까지 둥근 방을 줄줄이
    // 놓아 길을 낸다. 특징은 원뿐이라 원을 꿴다(겹친 자리는 더 열릴 뿐이다).
    //
    // **런타임 파기는 남긴다** — 무리가 시작 칸 밖에 서 있으면(`WarnStartTile`이 경고한다)
    // 거기서는 여전히 그것이 폰을 꺼내 준다.
    const float StartRoomRadius = 36f;   // 뚫리는 반지름은 그 4분의 3쯤 — 지름 50m 남짓
    const float StartPathRadius = 4.5f;  // 뚫리는 폭 6m 남짓 — 큰길과 비슷하게
    const float StartPathStep   = 3f;
    const float StartSearch     = 220f;

    static List<TerrainFeature> StartRooms()
    {
        var list  = new List<TerrainFeature>();
        var start = StartPoint();
        var p     = TerrainCeiling.Effective;
        float h   = PawnMover.BodyHeight + 4f - p.BaseClearance;

        list.Add(new TerrainFeature
        {
            Name        = $"{RoomPrefix}Start",
            Center      = start,
            Radius      = StartRoomRadius,
            Plateau     = 0.6f,
            Height      = h,
            // **가장자리를 흔든다** — 완전한 원은 사람이 판 방처럼 보인다(`OpenAround`와 같은 이유).
            Ridge       = 6f,
            RidgePeriod = 11f,
        });

        // 방 안에서 확실히 뚫리는 반지름. `Room`의 주석대로 반경의 4분의 3쯤이다.
        float inner = StartRoomRadius * 0.7f;

        if (!NearestNaturalCave(start, StartSearch, out var cave))
        {
            // **막힌 시작 방도 괜찮다**(2026-09-15, 사용자: *"시작 방이 막힌 동굴 형태여도 된다"*) —
            // 파서 나가면 된다. 경고가 아니라 무슨 일이 있었는지만 적는다.
            Debug.Log($"[배치] 시작 칸 둘레 {StartSearch:0}m 안에 큰길이 없어 **시작 방을 막힌 채로** 둔다 " +
                      "— 파서 나가야 한다.");
            return list;
        }

        float dist = Vector2.Distance(start, cave);
        if (dist <= inner - StartPathRadius) return list;   // 큰길이 이미 방을 지나간다

        var dir = (cave - start) / dist;
        int n = 0;
        for (float t = inner - StartPathRadius; t <= dist + StartPathRadius; t += StartPathStep)
        {
            list.Add(new TerrainFeature
            {
                Name    = $"{RoomPrefix}StartPath_{n++}",
                Center  = start + dir * t,
                Radius  = StartPathRadius,
                Plateau = 0.6f,
                Height  = h,
            });
        }

        Debug.Log($"[배치] 시작 방(지름 {inner * 2f:0}m 남짓)을 파고 {dist - inner:0}m 떨어진 " +
                  $"자연 굴까지 길을 이었다 — 원 {n}개. 방위 **{EventManager.Compass8(dir.x, dir.y)}**.");
        return list;
    }

    // 시작 자리에서 가장 가까운 **큰길**. 고리 모양으로 넓혀 간다 — 가까운 것부터 봐야
    // 길이 짧다. **길 한가운데만 센다**(`RoadOpenness` = 1) — 벽면 문턱에 걸린 칸을 고르면
    // 길 끝이 벽에 닿는다.
    //
    // **큰 굴은 안 센다**(2026-09-15) — 큰 굴 중에는 일부러 길에서 떼어 봉한 것이 있어서,
    // 거기에 이으면 시작 방이 **큰 섬 하나에 붙은 섬**이 된다.
    static bool NearestNaturalCave(Vector2 from, float maxRadius, out Vector2 found)
    {
        found = from;
        float world = TerrainGrid.WorldSize;

        for (float r = 0f; r <= maxRadius; r += 2f)
        {
            int steps = r < 1f ? 1 : Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r / 2f));
            for (int i = 0; i < steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                var q = new Vector2(from.x + Mathf.Cos(a) * r, from.y + Mathf.Sin(a) * r);
                if (q.x < 4f || q.y < 4f || q.x > world - 4f || q.y > world - 4f) continue;
                if (TerrainCeiling.RoadOpenness(q.x, q.y) < 0.99f) continue;

                found = q;
                return true;
            }
        }
        return false;
    }

    // ── 길 그물에서 걸어갈 수 있나 ──────────────────────────────────────────
    //
    // **정점 눈금(2m)으로 한 번 훑는다** — 1,024m면 513×513 = 26만 칸이라 한 번이면 된다.
    // 폰의 규칙(대각선 조건 · 1m)과 똑같지는 않지만 *"이 굴이 그물에 붙었나"*를 가르는 데는
    // 충분하다 — 봉한 굴의 벽은 6m라 2m 눈금이 새어 넘지 못한다.
    //
    // **시작 칸에서 가장 가까운 큰길에서 훑는다** — 시작 방은 아직 안 팠고(이 뒤에 판다),
    // 그 방은 어차피 그 길에 이어진다.
    static bool[] _reach;
    static int    _reachSize;

    static void BuildReach()
    {
        _reach = null;
        if (!TerrainCeiling.Enabled) return;
        if (!NearestNaturalCave(StartPoint(), StartSearch, out var seed)) return;

        float step = TerrainGrid.VertexSpacing;
        int n = Mathf.FloorToInt(TerrainGrid.WorldSize / step) + 1;
        var reach = new bool[n * n];
        var queue = new Queue<int>();

        // **지난번에 이 도구가 판 방은 안 센다**(2026-09-15, 코드 리뷰). 이 판정은 `WriteRooms`가
        // 옛 `site:` 방을 지우기 **전에** 돈다 — 그대로 `VertexClearanceGlobal`을 읽으면 옛 소굴 방이
        // 봉한 굴의 벽을 뚫고 있을 때 그 굴이 *이어졌다*로 나오고, 새 소굴이 거기 선 뒤 옛 방이
        // 지워지면 **유일한 입구가 사라진다**(막으려던 바로 그 사고). 그래서 여기서는 **자연 굴 +
        // 손으로 저작한 방**만으로 잰다.
        var kept = new List<TerrainFeature>();
        foreach (var f in TerrainCeiling.Features)
            if (f.Name == null || !f.Name.StartsWith(RoomPrefix)) kept.Add(f);

        var known = new bool[n * n];
        var open  = new bool[n * n];
        bool Open(int gx, int gz)
        {
            int k = gz * n + gx;
            if (known[k]) return open[k];
            float wx = gx * step, wz = gz * step;
            float c = TerrainCeiling.NaturalClearance(wx, wz);
            for (int f = 0; f < kept.Count; f++) c += kept[f].Contribution(wx, wz);
            known[k] = true;
            return open[k] = c >= PawnMover.BodyHeight;
        }

        int sx = Mathf.Clamp(Mathf.RoundToInt(seed.x / step), 0, n - 1);
        int sz = Mathf.Clamp(Mathf.RoundToInt(seed.y / step), 0, n - 1);
        reach[sz * n + sx] = true;
        queue.Enqueue(sz * n + sx);

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % n, z = i / n;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                int nz = z + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if ((uint)nx >= n || (uint)nz >= n) continue;
                int j = nz * n + nx;
                if (reach[j]) continue;
                if (!Open(nx, nz)) continue;
                reach[j] = true;
                queue.Enqueue(j);
            }
        }
        _reach = reach;
        _reachSize = n;
    }

    // 둘레 한 눈금 안에 닿은 정점이 있으면 참 — 자리가 벽면 문턱에 걸려도 안 버린다.
    // **훑은 것이 없으면 막지 않는다**(길이 하나도 없는 세계에서 배치가 통째로 멎으면 안 된다).
    static bool Reachable(Vector2 p)
    {
        if (_reach == null) return true;

        float step = TerrainGrid.VertexSpacing;
        int cx = Mathf.RoundToInt(p.x / step), cz = Mathf.RoundToInt(p.y / step);
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int x = cx + dx, z = cz + dz;
            if ((uint)x >= _reachSize || (uint)z >= _reachSize) continue;
            if (_reach[z * _reachSize + x]) return true;
        }
        return false;
    }

    // 도구가 판 방인지 이름으로 가른다 — **손으로 저작한 방은 안 건드린다**(물건에 `Generated`가
    // 있는 것과 같은 규칙). 안 가르면 다시 돌릴 때마다 방이 쌓여 **세계가 통째로 뚫린다.**
    const string RoomPrefix = "site:";

    static void WriteRooms(List<TerrainFeature> rooms)
    {
        var st = Object.FindFirstObjectByType<TerrainStreamer>(FindObjectsInactive.Include);
        if (st == null)
        {
            Debug.LogWarning("[배치] 씬에 `TerrainStreamer`가 없어 장소의 방을 못 팠다 — " +
                             "마을이 바위에 박힌 채로 남는다.");
            return;
        }

        var so    = new SerializedObject(st);
        var arr   = so.FindProperty("ceilingFeatures");
        if (arr == null)
        {
            Debug.LogError("[배치] `ceilingFeatures`를 못 찾았다 — 필드 이름이 바뀌었나? **방을 안 팠다.**");
            return;
        }

        // 손으로 저작한 것만 남긴다.
        var kept = new List<TerrainFeature>();
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            string name = e.FindPropertyRelative("Name")?.stringValue ?? "";
            if (name.StartsWith(RoomPrefix)) continue;

            kept.Add(new TerrainFeature
            {
                Name        = name,
                Center      = e.FindPropertyRelative("Center").vector2Value,
                Radius      = e.FindPropertyRelative("Radius").floatValue,
                Height      = e.FindPropertyRelative("Height").floatValue,
                Plateau     = e.FindPropertyRelative("Plateau").floatValue,
                Ridge       = e.FindPropertyRelative("Ridge").floatValue,
                RidgePeriod = e.FindPropertyRelative("RidgePeriod").floatValue,
            });
        }

        kept.AddRange(rooms);
        arr.arraySize = kept.Count;
        for (int i = 0; i < kept.Count; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Name").stringValue         = kept[i].Name;
            e.FindPropertyRelative("Center").vector2Value      = kept[i].Center;
            e.FindPropertyRelative("Radius").floatValue        = kept[i].Radius;
            e.FindPropertyRelative("Height").floatValue        = kept[i].Height;
            e.FindPropertyRelative("Plateau").floatValue       = kept[i].Plateau;
            e.FindPropertyRelative("Ridge").floatValue         = kept[i].Ridge;
            e.FindPropertyRelative("RidgePeriod").floatValue   = kept[i].RidgePeriod;
        }
        so.ApplyModifiedProperties();

        // **static에도 즉시 밀어 넣는다** — 안 밀면 다음에 도구가 재는 여유가 옛 것이라
        // *"방을 팠는데 미리보기에는 없다"*가 된다.
        TerrainAuthoring.SyncValues();

        Debug.Log($"[배치] 장소의 방 {rooms.Count}개를 천장에 팠다(손으로 저작한 {kept.Count - rooms.Count}개는 그대로). " +
                  "**씬을 저장할 것** — 방은 씬의 `ceilingFeatures`에 들어간다.");
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

        // **안 내린다** — 안의 창고·폰·건물이 처음으로 돌아가면 안 된다.
        tag.Persistent = true;

        Undo.RegisterCreatedObjectUndo(go, "장소 세우기");
    }

    static GameObject PrefabFor(WorldNodeContent kind, WorldContentStreamer c) => kind switch
    {
        WorldNodeContent.Town         => c.TownPrefab,
        WorldNodeContent.EnemyLair    => c.LairPrefab,
        _                             => null,
    };
}
