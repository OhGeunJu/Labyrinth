using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **절벽 칠하기 · 자연 다리 배치 — 절벽을 저작하는 문은 이것 하나다**(2026-09-26, `World → I. Paint Chasms`).
//
// 절벽은 **그린 곳에만 선다**(사용자 결정). 씬 뷰에서 **끌어서 칠하고 Shift로 지운다**(2m 덩어리 단위 —
// 땅 높이 정점 사이의 한 칸이라 지형 구멍과 같은 격자다). 칠하면 곧바로 씬 뷰 지형에 구멍이 뚫린다
// (`WorldSceneTerrain.Refresh` → `TerrainAuthoring.Fill` → `Chasm.ApplyHoles`, 런타임과 같은 문).
//
// **자연 다리는 규칙이 놓는다** — 절벽을 가로지르는 곧은 줄(양 끝이 땅이고 높이가 한 단 이내)을 찾아
// 길이가 맞는 다리를 고르고, 서로 `Spacing`만큼 떨어뜨린다. 돌다리는 **바이옴을 따른다**(얼음 굴은 얼음,
// 이끼·버섯·늪은 이끼, 나머지는 그림자). 다리를 못 놓은 줄 몇 곳에는 **무너진 돌다리**를 걸친다(장식).
// **씨가 같으면 같은 배치다** — 마음에 안 들면 씨를 바꿔 다시 누른다. 손으로 옮길 것은 `ChasmData`에서.
public class ChasmPainter : EditorWindow
{
    const string DataPath = "Assets/Resources/World/Chasms.asset";

    ChasmData _data;
    bool  _painting;
    int   _brush = 1;          // 덩어리 반지름(0 = 한 덩어리)
    int   _seed  = 1;
    float _spacing = 24f;
    float _detour  = 80f;   // 다리 없이 이만큼 안에 걸어서 건너편에 닿으면 다리를 안 놓는다(m)
    bool  _dirty;

    // 자동 생성 — 사용자: *"절벽도 자동으로 생성해줘"* · *"협곡처럼 넓은 절벽"*(2026-09-26).
    // 처음엔 폭을 2덩어리(4m)로 묶었다 — 다리가 늘 걸려야 길이 안 끊긴다고 봤는데 **작은 틈**만 났다.
    // 이제 **넓게 파고 천연 다리(땅 줄기)를 남겨** 길을 잇는다.
    int _rifts    = 2;     // 세계를 가로지르는 큰 협곡의 수
    int _minWidth = 4;     // 덩어리(2m) — 길 따라 이 사이에서 출렁인다(좁은 목에 다리가 걸린다)
    int _maxWidth = 10;
    int _landGap  = 60;    // 다리 목 사이(덩어리, 120m) — 0이면 안 만든다(끊긴 곳에만 목이 생긴다)
    bool _ruins   = false; // 무너진 돌다리 장식 — 끊긴 다리로 보여 헷갈린다(사용자가 물었다)
    int _riftSeed = 1;

    [MenuItem("DungeonColony/World/I. Paint Chasms (절벽 칠하기 · 자연 다리)")]
    static void Open() => GetWindow<ChasmPainter>("절벽");

    void OnEnable()  { SceneView.duringSceneGui += OnScene; Load(); }
    void OnDisable() { SceneView.duringSceneGui -= OnScene; Commit(); }

    void Load()
    {
        _data = AssetDatabase.LoadAssetAtPath<ChasmData>(DataPath);
        if (_data != null) return;
        BridgeCreator.EnsureFolder("Assets/Resources/World");
        _data = CreateInstance<ChasmData>();
        AssetDatabase.CreateAsset(_data, DataPath);
        AssetDatabase.SaveAssets();
    }

    void OnGUI()
    {
        if (_data == null) Load();

        EditorGUILayout.HelpBox("씬 뷰에서 끌어서 칠합니다. Shift를 누르고 끌면 지웁니다.\n" +
                                "단위는 2m 덩어리입니다. 칠한 곳은 땅에 구멍이 뚫리고, 게임에서 벽과 안개가 섭니다.", MessageType.Info);

        _painting = GUILayout.Toggle(_painting, _painting ? "칠하는 중 (다시 누르면 멈춤)" : "칠하기 시작", "Button", GUILayout.Height(28));
        _brush = EditorGUILayout.IntSlider("붓 크기 (덩어리)", _brush, 0, 5);

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        float bottom = EditorGUILayout.FloatField("절벽 바닥 높이 (m)", _data.BottomY);
        float margin = EditorGUILayout.FloatField("땅 고르기 여유 (m)", _data.LevelMargin);
        float fog    = EditorGUILayout.FloatField("안개 시작 깊이 (m)", _data.FogStartDepth);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_data, "절벽");
            _data.BottomY = bottom; _data.LevelMargin = margin; _data.FogStartDepth = Mathf.Max(0f, fog);
            _dirty = true;
        }

        EditorGUILayout.LabelField($"덩어리 {_data.Blocks.Count} · 자연 다리 {_data.Bridges.Count} · 무너진 다리 {_data.Ruins.Count}");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("자동 생성", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("세계 한 변에서 맞은편 변까지 가는 큰 협곡을 냅니다. 누르면 있던 절벽을 전부 갈아 끼웁니다(Ctrl+Z).", MessageType.None);
        _rifts    = EditorGUILayout.IntSlider("협곡 수", _rifts, 1, 6);
        _minWidth = EditorGUILayout.IntSlider("최소 폭 (덩어리 2m)", _minWidth, 1, 15);
        _maxWidth = EditorGUILayout.IntSlider("최대 폭 (덩어리 2m)", Mathf.Max(_maxWidth, _minWidth), _minWidth, 15);
        _landGap  = EditorGUILayout.IntSlider("다리 목 사이 (덩어리, 0 = 없음)", _landGap, 0, 200);
        _riftSeed = EditorGUILayout.IntField("절벽 씨", _riftSeed);
        if (GUILayout.Button("절벽 자동 생성 (+ 다리 자동 배치)", GUILayout.Height(24))) GenerateChasms();

        EditorGUILayout.Space();
        _seed    = EditorGUILayout.IntField("다리 배치 씨", _seed);
        _spacing = EditorGUILayout.FloatField("다리 사이 거리 (m)", _spacing);
        _detour  = EditorGUILayout.FloatField("다리가 필요한 우회 거리 (m)", _detour);
        _ruins   = EditorGUILayout.Toggle("무너진 돌다리 장식", _ruins);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("다리 자동 배치")) PlaceBridges();
            if (GUILayout.Button("다리 전부 지우기"))
            {
                Undo.RecordObject(_data, "절벽");
                _data.Bridges.Clear(); _data.Ruins.Clear(); _dirty = true;
            }
        }
        if (GUILayout.Button("절벽 전부 지우기") &&
            EditorUtility.DisplayDialog("절벽", "칠한 절벽과 다리를 전부 지웁니다.", "지운다", "그만둔다"))
        {
            Undo.RecordObject(_data, "절벽");
            _data.Blocks.Clear(); _data.Bridges.Clear(); _data.Ruins.Clear(); _dirty = true;
        }

        if (_dirty && GUILayout.Button("저장하고 지형에 반영", GUILayout.Height(24))) Commit();
    }

    // ── 씬 뷰 ───────────────────────────────────────────────────────────────

    HashSet<Vector2Int> _set;

    // **칠한 덩어리를 쓰는 자리마다 먼저 부른다** — 처음엔 씬 뷰가 한 번 그려질 때만 채웠더니, 창을 열고
    // 곧바로 *다리 자동 배치*를 누르면 비어 있어서 터졌다(2026-09-26, 사용자가 겪었다).
    void EnsureSet()
    {
        if (_data == null) Load();
        if (_set == null || _set.Count != _data.Blocks.Count) _set = new HashSet<Vector2Int>(_data.Blocks);
    }

    void OnScene(SceneView sv)
    {
        if (_data == null) return;
        EnsureSet();

        Draw(sv);

        if (!_painting) return;

        var e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (Hit(e.mousePosition, out var world))
        {
            var c = GroundLevel.BlockOfWorld(world);
            Handles.color = e.shift ? new Color(1f, 0.4f, 0.3f) : new Color(1f, 0.85f, 0.3f);
            float s = TerrainGrid.VertexSpacing;
            Handles.DrawWireCube(new Vector3((c.x + 0.5f) * s, world.y, (c.y + 0.5f) * s),
                                 new Vector3(s * (_brush * 2 + 1), 0.1f, s * (_brush * 2 + 1)));
            sv.Repaint();

            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                Undo.RecordObject(_data, "절벽");
                bool changed = false;
                for (int dz = -_brush; dz <= _brush; dz++)
                for (int dx = -_brush; dx <= _brush; dx++)
                {
                    var b = new Vector2Int(c.x + dx, c.y + dz);
                    if (e.shift) changed |= _set.Remove(b);
                    else         changed |= _set.Add(b);
                }
                if (changed) { _data.Blocks = new List<Vector2Int>(_set); _dirty = true; }
                e.Use();
            }
            if (e.type == EventType.MouseUp && e.button == 0 && _dirty) { Commit(); e.Use(); }
        }
    }

    // 칠한 덩어리의 드러난 변과 다리를 그린다 — 카메라 둘레만.
    void Draw(SceneView sv)
    {
        float s = TerrainGrid.VertexSpacing;
        var cam = sv.camera.transform.position;
        Handles.color = new Color(1f, 0.55f, 0.2f, 0.9f);
        foreach (var b in _set)
        {
            var center = new Vector3((b.x + 0.5f) * s, 0f, (b.y + 0.5f) * s);
            if ((new Vector2(center.x - cam.x, center.z - cam.z)).sqrMagnitude > 250f * 250f) continue;
            Vector3 P(int dx, int dz) => new((b.x + dx) * s, TerrainHeight.VertexHeightGlobal(b.x + dx, b.y + dz) + 0.1f, (b.y + dz) * s);
            if (!_set.Contains(b + Vector2Int.left))  Handles.DrawLine(P(0, 0), P(0, 1));
            if (!_set.Contains(b + Vector2Int.right)) Handles.DrawLine(P(1, 0), P(1, 1));
            if (!_set.Contains(b + Vector2Int.down))  Handles.DrawLine(P(0, 0), P(1, 0));
            if (!_set.Contains(b + Vector2Int.up))    Handles.DrawLine(P(0, 1), P(1, 1));
        }

        Handles.color = new Color(0.4f, 0.9f, 1f, 1f);
        foreach (var br in _data.Bridges)
        {
            if (br?.Kind == null) continue;
            var rot = Quaternion.Euler(0f, br.Yaw, 0f);
            var p = br.Position; p.y = TerrainHeight.At(p) + 0.5f;
            Handles.matrix = Matrix4x4.TRS(p, rot, Vector3.one);
            Handles.DrawWireCube(Vector3.zero, new Vector3(br.Kind.Size.x, 0.4f, br.Kind.Size.y));
            Handles.matrix = Matrix4x4.identity;
        }
        Handles.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        foreach (var r in _data.Ruins)
        {
            var p = r.Position; p.y = TerrainHeight.At(p) + 0.5f;
            Handles.DrawWireDisc(p, Vector3.up, 1f);
        }
    }

    // 화면 → 땅. 지형 콜라이더가 없어도 되게 **높이 함수로 걷는다**(구멍 난 자리도 짚어야 지울 수 있다).
    static bool Hit(Vector2 gui, out Vector3 world)
    {
        world = default;
        var ray = HandleUtility.GUIPointToWorldRay(gui);
        float t = 0f;
        for (int i = 0; i < 4000; i++, t += 0.5f)
        {
            var p = ray.GetPoint(t);
            if (p.y <= TerrainHeight.At(p))
            {
                world = p;
                return true;
            }
        }
        return false;
    }

    // ── 반영 ────────────────────────────────────────────────────────────────

    void Commit()
    {
        if (!_dirty || _data == null) return;
        _dirty = false;
        EditorUtility.SetDirty(_data);
        AssetDatabase.SaveAssets();
        Chasm.Reload();
        WorldSceneTerrain.Refresh();   // 씬 뷰 지형을 다시 세운다 — 구멍이 따라온다
        SceneView.RepaintAll();
    }

    // ── 자연 다리 자동 배치 ─────────────────────────────────────────────────
    //
    // 절벽을 **X 또는 Z로 곧게** 가로지르는 줄을 찾는다: 절벽 덩어리가 k개 이어지고 양 끝 덩어리가 땅이다.
    // 그 줄을 건널 수 있는 다리(길이 L ≥ 2k·2 + 2칸)를 고르되 **너무 길지 않은 것**(L ≤ 2k·2 + 4)만 —
    // 짧은 틈에 긴 돌다리가 땅 위로 한참 뻗으면 이상하다.
    void PlaceBridges()
    {
        EnsureSet();
        if (_set.Count == 0) { Debug.LogWarning("[절벽] 칠한 절벽이 없습니다 — 먼저 씬 뷰에서 칠할 것."); return; }

        var kinds = LoadKinds();
        if (kinds.Count == 0) { Debug.LogWarning("[절벽] 다리가 없습니다 — `World → J. Create Bridges`를 먼저 돌릴 것."); return; }

        var ruins = new List<GameObject>();
        foreach (var n in new[] { "Ruin_BridgeMoss", "Ruin_BridgeFrozen", "Ruin_BridgeShadow" })
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/3_Prefab/Building/{n}.prefab");
            ruins.Add(p);
        }

        Undo.RecordObject(_data, "절벽 다리");
        _data.Bridges.Clear();
        _data.Ruins.Clear();

        var rng = new System.Random(_seed);
        float s = TerrainGrid.VertexSpacing;
        var placed = new List<Vector3>();
        var decks  = new HashSet<Vector2Int>();   // 이번에 놓은 다리가 덮은 절벽 — 우회 길에 친다

        // 후보를 모은다 — (가운데, 방향, 틈 길이 m, 가장자리 한쪽 자리·방향)
        BuildOpen();
        var cands = new List<(Vector3 center, bool alongZ, float gap, Vector3 rim, float yawIn, Vector2Int before, Vector2Int after)>();
        foreach (var b in _set)
        {
            foreach (bool alongZ in new[] { true, false })
            {
                var step = alongZ ? Vector2Int.up : Vector2Int.right;
                if (_set.Contains(b - step)) continue;   // 줄의 첫 덩어리에서만 시작
                int k = 0;
                while (_set.Contains(b + step * k) && k <= _jumpBlocks) k++;
                if (_set.Contains(b + step * k)) continue;   // 가장 긴 다리로도 못 건넌다

                var before = b - step;
                var after  = b + step * k;
                if (!Ground(before) || !Ground(after)) continue;

                float hb = BlockTop(before), ha = BlockTop(after);
                if (Mathf.Abs(hb - ha) > Bridge.MaxRise(k * 2 + 2)) continue;   // 다리는 비스듬히 놓인다

                // 가운데 — 줄의 월드 가운데(폭 방향은 덩어리 한가운데).
                var c0 = new Vector3((b.x + 0.5f) * s, 0f, (b.y + 0.5f) * s);
                var c1 = new Vector3((after.x - step.x + 0.5f) * s, 0f, (after.y - step.y + 0.5f) * s);
                var mid = (c0 + c1) * 0.5f;
                var rim = new Vector3((b.x + 0.5f - step.x * 0.5f) * s, 0f, (b.y + 0.5f - step.y * 0.5f) * s);
                cands.Add((mid, alongZ, k * s, rim, alongZ ? 0f : 90f, before, after));
            }
        }

        // 섞어서 차례로 — 씨가 같으면 같은 배치.
        for (int i = cands.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (cands[i], cands[j]) = (cands[j], cands[i]); }

        int bridges = 0, broken = 0;
        foreach (var cd in cands)
        {
            bool near = false;
            foreach (var p in placed) if ((p - cd.center).sqrMagnitude < _spacing * _spacing) { near = true; break; }
            if (near) continue;

            // **돌아가면 금방인 틈에는 안 놓는다**(2026-09-26, 사용자: *"그냥 이어져 있는 길에 다리가 있는 게 필요가
            // 없잖아"*) — 협곡 끝이나 가장자리의 작은 홈을 가로지르는 다리가 많았다. 다리 없이(이미 놓은 다리는 쳐서)
            // 걸어서 `_detour` 안에 건너편에 닿으면 건너뛴다.
            if (WalkWithin(cd.before, cd.after, decks, Mathf.CeilToInt(_detour / s))) continue;

            var fit = Fit(kinds, cd.gap);

            // 장식을 켰으면 셋 중 하나는 무너진 다리로 남긴다(못 건너는 풍경) — 맞는 다리가 없는 틈도 여기로 온다.
            // **기본은 끈다** — 협곡 한가운데 떠 있는 돌 조각이 *"다리가 왜 중간에 끊겼나"*로 읽혔다(2026-09-26).
            if (fit.Count == 0) continue;
            if (_ruins && rng.NextDouble() < 0.33)
            {
                var ruin = ruins[RockVariant(cd.center)];
                if (ruin != null && cd.gap >= 4f)
                {
                    _data.Ruins.Add(new ChasmData.Ruin
                    {
                        Prefab = ruin,
                        Position = cd.rim + (cd.alongZ ? Vector3.forward : Vector3.right) * 1.2f,
                        Yaw = cd.yawIn,
                    });
                    placed.Add(cd.center);
                    broken++;
                }
                continue;
            }

            var spot = SpotFor(fit, cd.center, cd.alongZ, cd.yawIn, rng);
            if (spot == null) continue;
            _data.Bridges.Add(spot);
            placed.Add(cd.center);
            AddDeck(decks, spot);
            bridges++;
        }

        // **끊긴 곳이 없게 한다** — 간격 규칙 때문에 어떤 틈의 *유일한* 건널목이 빠질 수 있다. 후보마다 양쪽 땅이
        // **실제로 놓인 다리로** 이어져 있나를 보고(`Label`에 다리를 넣어서), 아니면 **간격을 무시하고** 다리를 놓는다.
        // 후보는 정의상 다리로 건널 수 있는 틈이므로 *이어질 수 있는데 안 이어진* 곳이 정확히 이것이다.
        //
        // **세계 전체를 본다** — 전에는 시작 자리에서 갈 수 있는 땅만 쟀다(협곡이 그 둘레에만 났을 때의 계산이다).
        //
        // **한 후보는 한 번만 시도하고, 놓아서 안 이어지면 거둔다**(2026-09-26) — 전에는 놓은 다리가 연결을 못 늘려도
        // *처음 걸린 후보*로 다시 돌아가 **같은 자리에 다리 197개가 겹쳐 쌓였다**(바위를 가른 절벽 덩어리가 *열린 땅*이
        // 아니라서 다리 위를 지나가는 것으로 안 쳤다 — `Reach`의 `Pass`).
        int added = 0, unjoined = 0;
        decks     = DeckBlocks();
        var lab   = Label(decks);
        int gn    = GridN;
        for (int ci = 0; ci < cands.Count; ci++)
        {
            var cd = cands[ci];
            int la = lab[cd.before.y * gn + cd.before.x], lb = lab[cd.after.y * gn + cd.after.x];
            if (la == 0 || lb == 0 || la == lb) continue;

            var fit = Fit(kinds, cd.gap);
            if (fit.Count == 0) { unjoined++; continue; }
            var spot = SpotFor(fit, cd.center, cd.alongZ, cd.yawIn, rng);
            if (spot == null) { unjoined++; continue; }
            _data.Bridges.Add(spot);

            decks = DeckBlocks();
            if (!Reach(cd.before, false, decks).Contains(cd.after))
            {
                _data.Bridges.RemoveAt(_data.Bridges.Count - 1);
                decks = DeckBlocks();
                unjoined++;
                continue;
            }
            // 이어졌다 — 뒤쪽 번호를 앞쪽으로 합친다(다시 세지 않는다).
            for (int i = 0; i < lab.Length; i++) if (lab[i] == lb) lab[i] = la;
            added++;
        }
        if (unjoined > 0)
            Debug.LogWarning($"[절벽] 다리로 못 이은 틈 {unjoined}곳 — 맞는 길이의 다리가 없거나 놓아도 안 이어졌다.");

        _dirty = true;
        Commit();
        Debug.Log($"[절벽] 자연 다리 {bridges + added}(끊긴 곳 잇기 {added}) · 무너진 다리 {broken} (후보 {cands.Count}, 씨 {_seed})");
    }

    // ── 다리 목록 · 길이 맞추기 ────────────────────────────────────────────
    //
    // **건널 수 있는 틈의 폭은 다리 목록이 정한다** — 전에는 `Reach`가 *2덩어리까지*, 후보 찾기가 *8덩어리까지*로
    // 각자 박아 두어 다리를 키워도(`BridgeCreator.Sizes`) 판정이 안 따라왔다. 가장 긴 다리가 양 끝 땅에 1m씩
    // 걸치고 건너는 덩어리 수가 `_jumpBlocks`다 — `LoadKinds`가 매번 다시 잰다.
    int _jumpBlocks = 2;

    List<BuildingData> LoadKinds()
    {
        var kinds = new List<BuildingData>();
        foreach (var g in AssetDatabase.FindAssets("t:BuildingData", new[] { "Assets/Resources/Data/Buildings" }))
        {
            var d = AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(g));
            if (d != null && d.IsBridge && d.Prefab != null) kinds.Add(d);
        }
        int maxL = 0;
        foreach (var d in kinds) maxL = Mathf.Max(maxL, d.Size.y);
        _jumpBlocks = Mathf.Max(0, (maxL - 2) / TerrainGrid.VertexSpacing);
        return kinds;
    }

    // 이 틈(m)에 맞는 다리 — 양 끝에 1m씩은 걸치고(L ≥ 틈 + 2) **땅 위로 너무 뻗지 않는 것**(L ≤ 틈 + 4 — 넘게 뻗으면 끝이 다음 층에 묻힌다).
    // 그런 것이 없으면 **걸치는 것 중 가장 짧은 것**(좁은 틈에 긴 다리가 뻗더라도 못 건너는 것보다 낫다 — 다리가
    // 커진 뒤로 한 덩어리 틈에 맞는 다리가 없어졌다).
    static List<BuildingData> Fit(List<BuildingData> kinds, float gap)
    {
        var fit = new List<BuildingData>();
        BuildingData shortest = null;
        foreach (var d in kinds)
        {
            int L = d.Size.y;
            if (L < gap + 2) continue;
            if (L <= gap + 4) fit.Add(d);
            if (shortest == null || L < shortest.Size.y) shortest = d;
        }
        if (fit.Count == 0 && shortest != null)
            foreach (var d in kinds) if (d.Size.y == shortest.Size.y) fit.Add(d);
        return fit;
    }

    // **맞는 다리 중 그 자리에 실제로 앉는 것** — 바이옴·씨로 고른 것부터 해 보고 안 되면 나머지를 차례로.
    // 앉는가는 게임의 짓기와 같은 판정이다(`Bridge.WhyNotShape` — 끝이 땅에 닿나 · 기울기 · 끝이 안 묻히나).
    // 아무것도 안 앉으면 null(그 틈은 다리 없이 남는다).
    ChasmData.BridgeSpot SpotFor(List<BuildingData> fit, Vector3 center, bool alongZ, float yaw, System.Random rng)
    {
        var order = new List<BuildingData> { PickKind(fit, center, rng) };
        foreach (var d in fit) if (!order.Contains(d)) order.Add(d);

        bool IsChasm(Vector2Int t) => _set.Contains(GroundLevel.BlockOf(t));
        float Height(Vector2Int t) => TerrainHeight.At(t.x + 0.5f, t.y + 0.5f);
        float? NoDeck(Vector2Int t) => null;   // 에디터에는 다리 윗면 등록이 없다(런타임 것이다)

        foreach (var d in order)
        {
            var pos = Snap(center, d, alongZ);
            var fp  = new List<Vector2Int>(PlacedBuilding.FootprintAt(pos, d.Size, Quaternion.Euler(0f, yaw, 0f)));
            bool rock = false;
            foreach (var t in fp)
                if (!IsChasm(t) && !Ground(GroundLevel.BlockOf(t))) { rock = true; break; }
            if (rock) continue;
            if (Bridge.WhyNotShape(fp, IsChasm, Height, NoDeck) != null) continue;
            // **자연 다리는 양 끝이 닿아야 한다** — 허공 끝은 플레이어가 다음 다리를 이을 때의 것이다.
            var sp = Bridge.SpanOf(fp, IsChasm, Height, NoDeck);
            if (!sp.BackSolid || !sp.FrontSolid) continue;
            return new ChasmData.BridgeSpot { Kind = d, Position = pos, Yaw = yaw };
        }
        return null;
    }

    // 돌다리는 바이옴을 따르고, 나무·판자는 섞는다.
    BuildingData PickKind(List<BuildingData> fit, Vector3 at, System.Random rng)
    {
        var rock = new List<BuildingData>();
        var wood = new List<BuildingData>();
        string want = RockName(RockVariant(at));
        foreach (var d in fit)
        {
            if (d.name.StartsWith("Bridge_Rock")) { if (d.name == want || d.name.StartsWith(want + "_")) rock.Add(d); }
            else wood.Add(d);
        }
        if (rock.Count > 0 && (wood.Count == 0 || rng.NextDouble() < 0.5)) return rock[rng.Next(rock.Count)];
        if (wood.Count > 0) return wood[rng.Next(wood.Count)];
        return fit[rng.Next(fit.Count)];
    }

    // 0 이끼 · 1 얼음 · 2 그림자.
    static int RockVariant(Vector3 at)
    {
        string b = WorldMap.BiomeAtWorld(new Vector2(at.x, at.z)).ToString();
        if (b == "Glacier" || b == "Crystal" || b == "Tundra") return 1;
        if (b == "Mossgrotto" || b == "Fungal" || b == "Swamp" || b == "Forest") return 0;
        return 2;
    }

    static string RockName(int v) => v == 0 ? "Bridge_RockMoss" : v == 1 ? "Bridge_RockFrozen" : "Bridge_RockShadow";

    // **발자국이 칸에 맞게 가운데를 놓는다** — 폭 2칸은 덩어리 경계에 맞고, 길이가 홀수면 칸 한가운데여야 한다.
    static Vector3 Snap(Vector3 mid, BuildingData d, bool alongZ)
    {
        float along = alongZ ? mid.z : mid.x;
        if (d.Size.y % 2 == 1) along = Mathf.Floor(along) + 0.5f;
        else                   along = Mathf.Round(along);
        return alongZ ? new Vector3(mid.x, 0f, along) : new Vector3(along, 0f, mid.z);
    }

    // 그 덩어리가 걸을 수 있는 땅인가 — 절벽도 바위도 아니다(에디터라 `MapManager` 없이 높이·바위 함수로 잰다).
    bool Ground(Vector2Int b)
    {
        if (_set.Contains(b)) return false;
        if (_open != null) return Open(b);
        float s = TerrainGrid.VertexSpacing;
        var t = new Vector2Int(b.x * (int)s, b.y * (int)s);
        if (!TerrainGrid.TileInWorld(t)) return false;
        for (int dz = 0; dz < s; dz++)
        for (int dx = 0; dx < s; dx++)
            if (RockMap.Enabled && RockMap.IsRock(new Vector2Int(t.x + dx, t.y + dz))) return false;
        return true;
    }

    static float BlockTop(Vector2Int b)
    {
        float s = TerrainGrid.VertexSpacing;
        return TerrainHeight.At((b.x + 0.5f) * s, (b.y + 0.5f) * s);
    }

    // ── 절벽 자동 생성 — 세계를 가로지르는 큰 협곡 ─────────────────────────────
    //
    // **사용자(2026-09-26): *"이럴 거면 그냥 엄청 크고 긴 절벽을 만들자 — 따로따로 작은 절벽은 좀 없애고"*.**
    // 전에는 열린 땅 한 점에서 30~70덩어리를 걷는 협곡을 여럿 냈다 — 작은 도랑이 흩어져 *절벽*으로 안 읽혔고,
    // 출발점이 시작 굴에서만 뽑혀 **시작 둘레 청크 열 개에만** 났다.
    //
    // 이제 협곡 하나가 **세계 한 변에서 맞은편 변까지** 간다. 방향은 펄린으로 완만하게 휘고(되감기지 않게 ±0.8
    // 라디안 안), **폭도 길 따라 펄린으로 출렁인다** — 좁은 목에는 다리가 걸리고 넓은 곳은 못 건넌다. 바위도
    // 가른다(절벽 칸은 바위가 아니다). 시작 둘레·장소 둘레는 **건너뛰고 계속 간다**(거기서 끊기면 협곡이 둘로 보인다).
    //
    // **협곡은 끊지 않는다**(사용자: *"토막 나는데? 안 끊어지게 해 봐"*) — 처음엔 땅 줄기(천연 다리)로 협곡을 끊어
    // 길을 이었는데, 길을 끊을 때마다 줄기가 늘어 **짧은 토막 수십 개**가 됐고 토막 사이로 금방 돌아가니 다리도
    // 의미가 없어졌다. 이제 건너는 곳은 **다리 목**이다 — `_landGap`마다 협곡을 가장 짧은 다리로 건널 만큼
    // 좁히고(끊지 않는다) 다리 배치가 거기에 다리를 건다.
    //
    // **길을 끊으면 고친다** — 파기 전에 같은 땅이던 가장자리끼리 못 가게 되면, 떨어져 나간 쪽이 작은 조각이면
    // 협곡에 삼키고(`TrySwallow`), 큰 땅이면 가장 가까운 길 위치를 다리 목으로 좁힌다. 목으로도 안 되면(양쪽 높이
    // 차가 다리 한계를 넘는다) **그때만** 땅 줄기로 끊는다.
    //
    // **파면 안 되는 자리(시작·장소 둘레)는 돌아서 간다**(`AvoidForbidden`) — 안 파고 지나가면 거기서 끊긴다.
    //
    // **누르면 있던 절벽을 전부 갈아 끼운다**(작은 절벽을 없애 달라는 것이 요청이다 — Ctrl+Z로 되돌린다).
    void GenerateChasms()
    {
        _gates = null;   // 층 지도의 통로 자리가 바뀌었을 수 있다
        EnsureSet();
        BuildOpen();
        if (StartBlock() == null) { Debug.LogWarning("[절벽] 시작 자리를 못 찾았다 — 씬에 `StartSpot`을 두거나 지도의 시작 칸을 확인할 것."); return; }

        LoadKinds();   // 건널 수 있는 틈(`_jumpBlocks`)을 다리 목록에서
        Undo.RecordObject(_data, "절벽 자동 생성");
        _set.Clear();
        _data.Bridges.Clear(); _data.Ruins.Clear();

        int n = GridN;
        const int Margin = 7;   // `Forbidden`의 가장자리(6)보다 한 덩어리 안에서 시작한다
        var rng = new System.Random(_riftSeed);
        var label = Label();
        int made = 0, rejected = 0, blocks = 0, lands = 0, repairs = 0, swallowed = 0, spans = 0;
        var path = new List<(Vector2 pos, Vector2 dir, float r)>();

        // **층마다 따로**(2026-10-01, `Floors`) — 협곡 하나가 **그 층의 한 변에서 맞은편 변까지** 간다. 세계 한 변으로 내면
        // 협곡이 층 사이 기반암을 가로질러 네 층을 한 줄로 꿴다. `_rifts`는 **층마다의 수**다.
        int fn = Floors.Size / TerrainGrid.VertexSpacing;   // 층 한 변(덩어리)
        int total = _rifts * Floors.Count;
        int floor = 1, madeHere = 0, triesHere = 0;
        Vector2 fo = Vector2.zero;

        try
        {
        for (int attempt = 0; made < total && attempt < total * 6; attempt++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("절벽 자동 생성", $"협곡 {made}/{total}", made / (float)total)) break;
            path.Clear();

            // 이 층에 다 냈거나 너무 많이 버렸으면 다음 층으로.
            if (madeHere >= _rifts || triesHere >= _rifts * 6) { floor++; madeHere = 0; triesHere = 0; }
            if (floor > Floors.Count) break;
            triesHere++;
            var fq = Floors.Quadrant(floor);
            fo = new Vector2(fq.x * fn, fq.y * fn);

            // 한 변 위의 한 점에서 맞은편 변을 향해(층 안 덩어리 좌표로 정하고 세계로 옮긴다).
            int side = rng.Next(4);
            float u = Mathf.Lerp(0.15f, 0.85f, (float)rng.NextDouble()) * fn;
            Vector2 pos = fo + side switch
            {
                0 => new Vector2(Margin, u),
                1 => new Vector2(fn - Margin, u),
                2 => new Vector2(u, Margin),
                _ => new Vector2(u, fn - Margin),
            };
            float baseAng = side switch { 0 => 0f, 1 => Mathf.PI, 2 => Mathf.PI * 0.5f, _ => -Mathf.PI * 0.5f };
            baseAng += (float)(rng.NextDouble() - 0.5) * 0.9f;   // 맞은편으로 곧게만 가지 않게
            float nA = (float)rng.NextDouble() * 1000f, nW = (float)rng.NextDouble() * 1000f;

            for (int i = 0; i < n * 3; i++)
            {
                if (pos.x < fo.x + Margin - 1 || pos.y < fo.y + Margin - 1 ||
                    pos.x > fo.x + fn - Margin + 1 || pos.y > fo.y + fn - Margin + 1) break;   // 층 경계에서 멈춘다

                float ang = baseAng + (Mathf.PerlinNoise(nA, i * 0.012f) - 0.5f) * 1.6f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float w = Mathf.Lerp(_minWidth, _maxWidth, Mathf.PerlinNoise(nW, i * 0.025f));
                float r = Mathf.Max(0.5f, w * 0.5f);

                // **파면 안 되는 자리는 돌아서 간다** — 전에는 그 자리를 안 파고 지나가 협곡이 거기서 끊겼다.
                dir = AvoidForbidden(pos, dir, r);
                baseAng = Mathf.Atan2(dir.y, dir.x) - (Mathf.PerlinNoise(nA, i * 0.012f) - 0.5f) * 1.6f;

                path.Add((pos, dir, r));
                pos += dir;
            }
            if (path.Count < fn / 3) continue;   // 모서리를 스치고 나갔다

            // **다리 목** — `_landGap`마다 협곡을 다리로 건널 만큼 좁힌다(끊지 않는다).
            var necks = new HashSet<int>();
            if (_landGap > 0)
                for (int i = _landGap / 2; i < path.Count - 3; i += _landGap) Neck(path, i, necks);

            var addedNow = new List<Vector2Int>();
            var extra    = new HashSet<Vector2Int>();   // 삼킨 조각 — 다시 팔 때도 남긴다
            Recarve(path, extra, addedNow);

            bool ok = false;
            int necksNow = necks.Count;
            for (int fix = 0; fix < 120; fix++)
            {
                if (KeepsConnected(addedNow, label, out var lost, out var anchor)) { ok = true; break; }

                // **떨어져 나간 쪽이 작은 조각이면 협곡에 삼킨다** — 협곡이 휘며 가장자리에 1~2덩어리짜리 땅이
                // 떨어져 나가는 일이 흔하다.
                if (TrySwallow(lost, addedNow, extra) || TrySwallow(anchor, addedNow, extra)) { swallowed++; continue; }

                // 큰 땅을 갈랐다 — 가장 가까운 길 위치를 다리 목으로 좁힌다. 이미 목이면(높이 차가 커서 다리로도 못
                // 건넌다) 그때만 땅 줄기로 끊는다.
                int best = 0; float bd = float.MaxValue;
                var lp = new Vector2(lost.x + 0.5f, lost.y + 0.5f);
                for (int i = 0; i < path.Count; i++)
                {
                    float dd = (path[i].pos - lp).sqrMagnitude;
                    if (dd < bd) { bd = dd; best = i; }
                }
                if (!necks.Contains(best)) { Neck(path, best, necks); necksNow++; }
                else
                {
                    // 앞뒤 원이 서로 겹치므로 한 점만 비우면 안 끊긴다 — 목 반지름보다 넓게 비운다.
                    for (int j = Mathf.Max(0, best - 3); j <= Mathf.Min(path.Count - 1, best + 3); j++)
                        path[j] = (path[j].pos, path[j].dir, 0f);
                    spans++;
                }
                Recarve(path, extra, addedNow);
                repairs++;
            }
            if (!ok)
            {
                foreach (var b in addedNow) _set.Remove(b);
                rejected++;
                continue;
            }

            label = Label();
            made++;
            madeHere++;
            blocks += addedNow.Count;
            lands  += necksNow;
        }
        }
        finally { EditorUtility.ClearProgressBar(); }

        _data.Blocks = new List<Vector2Int>(_set);
        _dirty = true;
        Commit();
        Debug.Log($"[절벽] 자동 생성 — 협곡 {made}개 · 덩어리 {blocks} · 다리 목 {lands}(끊긴 곳 고치기 {repairs}) · " +
                  $"삼킨 작은 조각 {swallowed} · 어쩔 수 없이 끊은 곳 {spans} · " +
                  $"버린 것 {rejected} (씨 {_riftSeed})");

        PlaceBridges();
    }

    // 이 덩어리가 속한 땅이 `SliverMax` 이하면 절벽으로 삼킨다(금지 자리가 끼어 있으면 안 삼킨다).
    const int SliverMax = 60;   // 덩어리 — 240m²

    bool TrySwallow(Vector2Int at, List<Vector2Int> addedNow, HashSet<Vector2Int> extra)
    {
        var part = Reach(at, true, null);
        if (part.Count > SliverMax) return false;
        foreach (var b in part) if (Forbidden(b)) return false;
        foreach (var b in part) { extra.Add(b); if (_set.Add(b)) addedNow.Add(b); }
        return true;
    }

    // 길 따라 원을 굴려 판다 — 이번 협곡이 넣은 것(`addedNow`)을 걷어 내고 다시 넣는다(앞 협곡의 덩어리는 안 건드린다).
    void Recarve(List<(Vector2 pos, Vector2 dir, float r)> path, HashSet<Vector2Int> extra, List<Vector2Int> addedNow)
    {
        foreach (var b in addedNow) _set.Remove(b);
        addedNow.Clear();

        var cut = new HashSet<Vector2Int>(extra);
        foreach (var (pos, _, r) in path)
        {
            if (r <= 0f) continue;
            var here = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
            int ri = Mathf.CeilToInt(r);
            for (int dz = -ri; dz <= ri; dz++)
            for (int dx = -ri; dx <= ri; dx++)
            {
                var b = new Vector2Int(here.x + dx, here.y + dz);
                var d = new Vector2(b.x + 0.5f, b.y + 0.5f) - pos;
                if (d.sqrMagnitude <= r * r && !Forbidden(b)) cut.Add(b);
            }
        }
        foreach (var b in cut) if (_set.Add(b)) addedNow.Add(b);
    }

    // 길 위치 `i` 둘레를 **가장 짧은 다리로 건널 폭**으로 좁힌다. 폭은 다리 목록에서 — 가장 긴 다리가 건너는 폭보다
    // 한 덩어리 좁게, 2~4덩어리(4~8m). 이미 목이 가까이 있으면 그 목을 쓴다.
    void Neck(List<(Vector2 pos, Vector2 dir, float r)> path, int i, HashSet<int> necks)
    {
        float neckR = Mathf.Clamp(_jumpBlocks - 1, 2, 4) * 0.5f;
        for (int j = Mathf.Max(0, i - 5); j <= Mathf.Min(path.Count - 1, i + 5); j++)
        {
            path[j] = (path[j].pos, path[j].dir, Mathf.Min(path[j].r, neckR));
            necks.Add(j);
        }
    }

    // 시작·장소 둘레에 다가가면 밀어낸다 — 반지름 + 협곡 폭 + 4덩어리 안에서 멀어지는 쪽으로 꺾는다.
    Vector2 AvoidForbidden(Vector2 pos, Vector2 dir, float r)
    {
        float s = TerrainGrid.VertexSpacing;
        void Push(Vector2 centerWorld, float radiusM)
        {
            var c = centerWorld / s;
            float R = radiusM / s + r + 4f;
            var away = pos - c;
            float dist = away.magnitude;
            if (dist >= R || dist < 0.001f) return;
            dir += away / dist * (1f - dist / R) * 2f;
        }
        if (_startWorld != null) Push(_startWorld.Value, 30f);
        foreach (var site in WorldSites.All) Push(site.Position, 20f);
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : dir;
    }

    // ── 굴 모양 격자(덩어리 단위) ───────────────────────────────────────────
    //
    // **판정과 같은 함수로 잰다**(`TerrainCeiling.ClearanceAt` — `2. Preview Cave Map`과 같은 문). 에디터에는
    // `RockMap`이 판을 모르므로(캔 칸·천장 켜짐이 런타임 것이다) 여유를 직접 묻는다.
    bool[] _open;
    int _openN;

    void BuildOpen()
    {
        var st = TerrainAuthoring.SyncValues();
        if (st == null) { Debug.LogWarning("[절벽] 씬에 `TerrainStreamer`가 없다 — 굴 모양을 모른다(바위도 땅으로 친다)."); _open = null; return; }
        TerrainCeiling.ForgetDeltas();

        int s = TerrainGrid.VertexSpacing;
        _openN = TerrainGrid.WorldSize / s;
        _open  = new bool[_openN * _openN];
        for (int z = 0; z < _openN; z++)
        for (int x = 0; x < _openN; x++)
            _open[z * _openN + x] = !Floors.IsSeam(x * s + 1, z * s + 1)   // 층 사이 기반암 — 연결 판정이 옆 층으로 새지 않게
                                 && TerrainCeiling.ClearanceAt(x * s + 1f, z * s + 1f) >= PawnMover.BodyHeight;
    }

    // **세계 밖은 언제나 닫혀 있다** — `_open`이 없을 때(씬에 `TerrainStreamer`가 없다) 밖까지 열려 있으면 `Reach`가 끝없이 번진다.
    bool Open(Vector2Int b)
    {
        int n = GridN;
        if (b.x < 0 || b.y < 0 || b.x >= n || b.y >= n) return false;
        return _open == null || _open[b.y * _openN + b.x];
    }

    // 절벽을 내면 안 되는 자리 — 세계 가장자리 · 시작 둘레 30m · 장소 둘레 20m.
    bool Forbidden(Vector2Int b)
    {
        float s = TerrainGrid.VertexSpacing;
        int n = _openN > 0 ? _openN : TerrainGrid.WorldSize / TerrainGrid.VertexSpacing;
        if (b.x < 6 || b.y < 6 || b.x >= n - 6 || b.y >= n - 6) return true;
        // **층 가장자리도 세계 가장자리다**(2026-10-01, `Floors`) — 층 사이 기반암에 절벽이 붙으면 그 둘레가 반쪽 협곡이 된다.
        int fn = Floors.Size / TerrainGrid.VertexSpacing;
        int lx = b.x % fn, lz = b.y % fn;
        if (lx < 6 || lz < 6 || lx >= fn - 6 || lz >= fn - 6) return true;
        var c = new Vector2((b.x + 0.5f) * s, (b.y + 0.5f) * s);
        if (_startWorld != null && (c - _startWorld.Value).sqrMagnitude < 30f * 30f) return true;
        // 층 통로 둘레 — 내려가자마자 절벽이면 안 된다(통로는 판이 열릴 때 둘레를 연다, `FloorPassage`).
        foreach (var g in Gates()) if ((c - g).sqrMagnitude < 30f * 30f) return true;
        foreach (var site in WorldSites.All)
            if ((c - site.Position).sqrMagnitude < 20f * 20f) return true;
        return false;
    }

    Vector2? _startWorld;

    // 층 통로 자리 — **한 번만 잰다**(`Forbidden`은 덩어리마다 불린다 · 자리는 씬의 층 지도에서 나온다). 생성을 시작할 때 비운다.
    List<Vector2> _gates;
    List<Vector2> Gates()
    {
        if (_gates != null) return _gates;
        _gates = new List<Vector2>();
        for (int f = 1; f <= Floors.Count; f++)
        {
            if (f < Floors.Count) { var d = FloorPassage.PointFor(f, PassageKind.Down); _gates.Add(new Vector2(d.x, d.z)); }
            if (f > 1)            { var u = FloorPassage.PointFor(f, PassageKind.Up);   _gates.Add(new Vector2(u.x, u.z)); }
        }
        return _gates;
    }

    // 시작 자리 — 씬의 `StartSpot`, 없으면 지도의 시작 칸 한가운데. 그 덩어리가 바위면 가장 가까운 열린 덩어리.
    Vector2Int? StartBlock()
    {
        Vector3? w = null;
        var spot = Object.FindFirstObjectByType<StartSpot>(FindObjectsInactive.Include);
        if (spot != null) w = spot.transform.position;
        else
        {
            var map = Object.FindFirstObjectByType<WorldMap>(FindObjectsInactive.Include);
            var data = map != null ? new SerializedObject(map).FindProperty("mapData")?.objectReferenceValue as WorldMapData : null;
            if (data != null) w = TerrainGrid.RegionCenter(data.StartNode);
        }
        if (w == null) return null;
        _startWorld = new Vector2(w.Value.x, w.Value.z);

        var b0 = GroundLevel.BlockOfWorld(w.Value);
        for (int r = 0; r < 40; r++)
        for (int dz = -r; dz <= r; dz++)
        for (int dx = -r; dx <= r; dx++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
            var b = new Vector2Int(b0.x + dx, b0.y + dz);
            if (Open(b) && !_set.Contains(b)) return b;
        }
        return null;
    }

    int GridN => _openN > 0 ? _openN : TerrainGrid.WorldSize / TerrainGrid.VertexSpacing;

    // **이어진 땅마다 번호** — 열린 땅(절벽 아님)을 `Reach`로 묶는다(다리를 안 주면 건널 수 있는 틈은 이어진 것으로,
    // 주면 놓인 다리로만). 0은 땅이 아니다.
    int[] Label(HashSet<Vector2Int> decks = null)
    {
        int n = GridN;
        var lab = new int[n * n];
        int id = 0;
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            var b = new Vector2Int(x, z);
            if (lab[z * n + x] != 0 || _set.Contains(b) || !Open(b)) continue;
            id++;
            // 다리를 주면 **놓인 다리로만** 잇는다(건널 수 있는 틈이 아니라).
            foreach (var v in Reach(b, decks == null, decks)) if (!_set.Contains(v)) lab[v.y * n + v.x] = id;
        }
        return lab;
    }

    // **협곡이 길을 끊었나** — 시작 자리 하나가 아니라 **협곡 가장자리 전부**를 본다: 파기 전에 같은 땅이던
    // 가장자리끼리 판 뒤에도 서로 갈 수 있어야 한다. 시작 굴만 재면 세계 전체에서 협곡을 뽑을 때 먼 굴을
    // 쪼개도 모른다.
    bool KeepsConnected(List<Vector2Int> added, int[] lab, out Vector2Int lost, out Vector2Int anchor)
    {
        lost = default; anchor = default;
        int n = GridN;
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        var groups = new Dictionary<int, HashSet<Vector2Int>>();
        foreach (var b in added)
        foreach (var d in dirs)
        {
            var nb = b + d;
            if (_set.Contains(nb) || !Open(nb)) continue;
            int l = lab[nb.y * n + nb.x];
            if (l == 0) continue;
            if (!groups.TryGetValue(l, out var g)) groups[l] = g = new HashSet<Vector2Int>();
            g.Add(nb);
        }
        foreach (var g in groups.Values)
        {
            if (g.Count < 2) continue;
            Vector2Int first = default;
            foreach (var b in g) { first = b; break; }
            var r = Reach(first, true, null);
            foreach (var b in g) if (!r.Contains(b)) { lost = b; anchor = first; return false; }
        }
        return true;
    }

    // **시작 자리에서 갈 수 있는 덩어리** — 열린 땅이고 절벽이 아니면 지나간다(4-이웃).
    //   `jumps` — 곧게 `_jumpBlocks` 덩어리 이하인 절벽 줄을 **다리로 건널 수 있는 것**으로 친다(양 끝 높이 한 단 이내).
    //   `decks` — 실제로 놓인 다리의 절벽 덩어리(지나간다).
    HashSet<Vector2Int> Reach(Vector2Int start, bool jumps, HashSet<Vector2Int> decks)
    {
        var seen  = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // **다리가 덮은 절벽은 열린 땅인지 안 묻는다** — 협곡은 바위도 가르므로 바위였던 덩어리가 절벽이 되는데
        // (`Chasm` 칸은 바위가 아니다), 여기서 `Open`을 먼저 물으면 그 위의 다리를 못 지나간다.
        bool Pass(Vector2Int b) => _set.Contains(b) ? decks != null && decks.Contains(b) : Open(b);

        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            foreach (var d in dirs)
            {
                var v = u + d;
                if (Pass(v)) { if (seen.Add(v)) queue.Enqueue(v); continue; }
                if (!jumps || !_set.Contains(v)) continue;

                // 절벽 — 곧게 건너편까지.
                int k = 0;
                while (k <= _jumpBlocks && _set.Contains(u + d * (k + 1))) k++;
                if (k == 0 || k > _jumpBlocks) continue;
                var land = u + d * (k + 1);
                if (!Open(land) || _set.Contains(land)) continue;
                if (Mathf.Abs(BlockTop(land) - BlockTop(u)) > Bridge.MaxRise(k * 2 + 2)) continue;
                if (seen.Add(land)) queue.Enqueue(land);
            }
        }
        return seen;
    }

    // 놓인 자연 다리가 덮는 절벽 덩어리.
    // `a`에서 걸어서(4-이웃 · 열린 땅 · 절벽은 다리가 덮은 곳만) `steps` 덩어리 안에 `b`에 닿나.
    bool WalkWithin(Vector2Int a, Vector2Int b, HashSet<Vector2Int> decks, int steps)
    {
        var dist  = new Dictionary<Vector2Int, int> { [a] = 0 };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(a);
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            int du = dist[u];
            if (u == b) return true;
            if (du >= steps) continue;
            foreach (var d in dirs)
            {
                var v = u + d;
                if (dist.ContainsKey(v)) continue;
                bool pass = _set.Contains(v) ? decks.Contains(v) : Open(v);
                if (!pass) continue;
                dist[v] = du + 1;
                queue.Enqueue(v);
            }
        }
        return false;
    }

    void AddDeck(HashSet<Vector2Int> decks, ChasmData.BridgeSpot br)
    {
        foreach (var t in PlacedBuilding.FootprintAt(br.Position, br.Kind.Size, Quaternion.Euler(0f, br.Yaw, 0f)))
        {
            var b = GroundLevel.BlockOf(t);
            if (_set.Contains(b)) decks.Add(b);
        }
    }

    HashSet<Vector2Int> DeckBlocks()
    {
        var set = new HashSet<Vector2Int>();
        foreach (var br in _data.Bridges)
        {
            if (br?.Kind == null) continue;
            foreach (var t in PlacedBuilding.FootprintAt(br.Position, br.Kind.Size, Quaternion.Euler(0f, br.Yaw, 0f)))
            {
                var b = GroundLevel.BlockOf(t);
                if (_set.Contains(b)) set.Add(b);
            }
        }
        return set;
    }
}
