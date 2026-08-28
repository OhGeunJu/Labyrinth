using System.IO;
using UnityEditor;
using UnityEngine;

// **침식을 구워 세계의 큰 모양으로 삼는다**(2026-08-27).
//
// 펄린 몇 층으로는 언덕이 오르내릴 뿐 **물길도 능선도 골짜기도 안 생긴다.** 자연 지형이
// 그렇게 보이는 이유의 절반은 **침식**인데, 노이즈는 물이 어디로 흘렀는지를 모른다.
//
// 켄시가 World Machine으로 `fullmap.tif`를 뽑는 그 자리다. 우리는 **같은 일을 에디터에서**
// 한다 — 노이즈로 씨를 뿌리고, 물방울을 몇 십만 개 굴려 깎고 쌓고, 결과를 저장한다.
//
// ── 왜 직접 굽나 ───────────────────────────────────────────────────────────
//
// · **시드에서 결정적** — 이 저장소 전체의 규약과 같다. 같은 시드면 같은 세계다.
// · **다시 구울 수 있다** — 산을 옮기거나 프로필을 바꾸면 버튼 하나. 외부 도구를 왕복하면
//   그때마다 손이 가고, 내보내기 규격(해상도·비트 깊이)을 맞추는 함정이 따라온다.
// · **나중에 갈아 끼울 수 있다** — 결과물이 "높이맵 한 장"이라, Gaea 같은 것을 들이면
//   **이 도구를 지우고 그 출력을 같은 자리에 넣으면 된다.** 위에 얹힌 것은 안 바뀐다.
//
// ── 마스크도 같이 뽑는다 ────────────────────────────────────────────────────
//
// 물방울이 지나가며 **어디를 깎고 어디에 쌓았는지**를 어차피 계산한다. 그것이 곧
// Gaea·WM이 내놓는 `flow`·`wear`·`deposit` 마스크다 — **골짜기엔 흙, 능선엔 맨바위**를
// 깔려면 그 정보가 있어야 하고, 경사만 봐서는 알 수 없다(경사가 같아도 물이 지난 데와
// 아닌 데는 다르다). 나중에 필요해서 다시 굽는 것은 낭비이므로 **한 번에 같이** 낸다.
public class TerrainErosionBaker : EditorWindow
{
    const string Dir = "Assets/Resources/Terrain";

    // 마스크 상한. 높이맵과 달리 **성겨도 된다** — 골짜기에 흙을 까는 눈금이지 지형의 모양이 아니다.
    const int MaxMaskResolution = 2048;

    // 해상도. **세계 한 변을 이만큼으로 나눈다.**
    //
    //   1024 → 16m/셀 ·  2MB   — 침식 무늬가 뭉갠다
    //   2048 →  8m/셀 ·  8MB   — 골짜기가 읽힌다. **기본값**
    //   4096 →  4m/셀 · 34MB   — 곱지만 굽는 시간이 네 배
    //
    // 정점 간격이 2m이므로 4096이 실질 상한이다(그보다 잘아도 메시가 못 담는다).
    static readonly int[] Sizes = { 1024, 2048, 4096 };

    int   _resolution = 2048;
    int   _seed       = 20260827;

    float _dropletsPerCell = 0.35f;

    // ── 물방울 ──────────────────────────────────────────────────────────────
    int   _lifetime      = 42;
    float _inertia       = 0.045f;
    float _capacity      = 3.2f;
    float _minCapacity   = 0.012f;
    float _erode         = 0.35f;
    float _deposit       = 0.28f;
    float _evaporate     = 0.02f;
    float _gravity       = 4f;
    int   _radius        = 3;

    // ── 열 풍화 ─────────────────────────────────────────────────────────────
    int   _thermalPasses = 3;
    float _talus         = 38f;

    // ── 세계 가장자리 ───────────────────────────────────────────────────────
    //
    // 세계는 유한한데 지형은 그걸 모른다 — 그냥 두면 **산이 칼로 자른 듯 끊긴 채** 세계가
    // 끝난다(실제로 그렇게 보였다). 바깥 띠를 낮춰서 **저지대로 끝나게** 한다.
    // 켄시가 섬이고 둘레가 바다인 것과 같은 처리다.
    float _edgeFalloff = 900f;    // 낮추기 시작하는 띠의 폭(m)
    float _edgeHeight  = -30f;    // 맨 끝의 높이(m)

    [MenuItem("DungeonColony/Terrain/B. Bake Eroded Heightmap")]
    static void Open()
    {
        var w = GetWindow<TerrainErosionBaker>("Erosion");
        w.minSize = new Vector2(360f, 520f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("침식을 구워 큰 모양으로 삼는다", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"세계 {TerrainGrid.WorldSize:N0}m — 노이즈로 씨를 뿌리고 물방울을 굴린다. " +
            "결과는 높이맵 한 장 + 마스크.", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(6f);
        int idx = Mathf.Max(0, System.Array.IndexOf(Sizes, _resolution));
        var labels = new string[Sizes.Length];
        for (int i = 0; i < Sizes.Length; i++)
        {
            float m = TerrainGrid.WorldSize / (float)Sizes[i];
            labels[i] = $"{Sizes[i]}²  —  {m:0.#}m/셀 · {Sizes[i] * (long)Sizes[i] * 2 / 1048576f:0.#} MB";
        }
        _resolution = Sizes[EditorGUILayout.Popup("해상도", idx, labels)];
        _seed = EditorGUILayout.IntField("시드", _seed);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("물방울", EditorStyles.boldLabel);
        _dropletsPerCell = EditorGUILayout.Slider("셀당 개수", _dropletsPerCell, 0.05f, 2f);
        EditorGUILayout.LabelField($"= {Droplets:N0}개 — {Estimate}", EditorStyles.miniLabel);

        _lifetime  = EditorGUILayout.IntSlider("수명(걸음)", _lifetime, 8, 96);
        _radius    = EditorGUILayout.IntSlider("깎는 반경(셀)", _radius, 1, 6);
        _erode     = EditorGUILayout.Slider("깎는 속도", _erode, 0.05f, 1f);
        _deposit   = EditorGUILayout.Slider("쌓는 속도", _deposit, 0.05f, 1f);
        _capacity  = EditorGUILayout.Slider("운반 용량", _capacity, 0.5f, 8f);
        _inertia   = EditorGUILayout.Slider("관성", _inertia, 0f, 0.5f);
        _evaporate = EditorGUILayout.Slider("증발", _evaporate, 0.005f, 0.1f);
        _gravity   = EditorGUILayout.Slider("중력", _gravity, 1f, 12f);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("열 풍화 — 급경사가 무너져 내린다", EditorStyles.boldLabel);
        _thermalPasses = EditorGUILayout.IntSlider("횟수", _thermalPasses, 0, 12);
        _talus         = EditorGUILayout.Slider("무너지는 각도", _talus, 20f, 60f);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("세계 가장자리 — 저지대로 끝낸다", EditorStyles.boldLabel);
        _edgeFalloff = EditorGUILayout.Slider("낮추는 띠(m)", _edgeFalloff, 0f, 3000f);
        _edgeHeight  = EditorGUILayout.Slider("맨 끝 높이(m)", _edgeHeight, -120f, 60f);
        EditorGUILayout.LabelField(
            _edgeFalloff < 1f ? "끔 — 산이 칼로 자른 듯 끊긴다."
                              : $"바깥 {_edgeFalloff:0}m가 {_edgeHeight:0}m로 내려간다.",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(10f);
        if (GUILayout.Button("굽는다", GUILayout.Height(28f))) Bake();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "굽고 나면 씬의 TerrainStreamer에 물리고 지형이 다시 선다.\n" +
            "**씨앗은 높이맵을 뺀 노이즈다** — 이미 구운 것 위에 또 구우면 지형이 제 그림자를 좇는다.",
            MessageType.None);
    }

    int Droplets => Mathf.RoundToInt(_resolution * (long)_resolution * _dropletsPerCell);

    string Estimate
    {
        get
        {
            // 대충의 눈금. 정확할 필요는 없고 **몇 분짜리인지**만 알면 된다.
            float sec = Droplets * _lifetime / 9_000_000f + _resolution * (long)_resolution / 4_000_000f;
            return sec < 60f ? $"약 {sec:0}초" : $"약 {sec / 60f:0.#}분";
        }
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    void Bake()
    {
        var streamer = TerrainAuthoring.SyncValues();
        if (streamer == null)
        {
            Debug.LogError("[침식] 씬에 TerrainStreamer가 없다 — 어떤 노이즈로 씨를 뿌릴지 알 수 없다.");
            return;
        }

        // **높이맵을 떼고 굽는다.** 안 떼면 이미 구운 것을 도로 읽어 되먹임이 된다 —
        // 구울수록 골짜기가 깊어지다 결국 세계가 무너진다.
        var keep = TerrainHeight.Heightmap;
        TerrainHeight.Heightmap = null;

        int n = _resolution;
        var h = new float[n * n];
        var deposit = new float[n * n];   // 쌓인 양 — 골짜기 바닥
        var wear    = new float[n * n];   // 깎인 양 — 능선의 맨바위
        var flow    = new float[n * n];   // 물이 지나간 횟수

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            Seed(h, n);
            Hydraulic(h, deposit, wear, flow, n);
            for (int i = 0; i < _thermalPasses; i++)
            {
                EditorUtility.DisplayProgressBar("침식", $"열 풍화 {i + 1}/{_thermalPasses}",
                                                 0.85f + 0.1f * i / Mathf.Max(1, _thermalPasses));
                Thermal(h, n);
            }
            Write(h, deposit, wear, flow, n, sw);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            TerrainHeight.Heightmap = keep;
        }
    }

    // ── ① 씨 뿌리기 ─────────────────────────────────────────────────────────

    void Seed(float[] h, int n)
    {
        float step = TerrainGrid.WorldSize / (float)(n - 1);

        for (int y = 0; y < n; y++)
        {
            if ((y & 63) == 0)
                EditorUtility.DisplayProgressBar("침식", "노이즈로 씨를 뿌린다…", y / (float)n * 0.15f);

            for (int x = 0; x < n; x++)
            {
                float wx = x * step, wz = y * step;

                // **저작한 산·분지도 씨앗에 넣는다** — 안 넣으면 침식이 산을 모르고 지나가고,
                // 나중에 얹으면 **깎이지 않은 산**이 침식된 땅 위에 얹혀 따로 논다.
                float v = TerrainHeight.MacroNoise(wx, wz);
                var f = TerrainHeight.Features;
                for (int i = 0; i < f.Length; i++) v += f[i].Contribution(wx, wz);

                h[y * n + x] = EdgeFade(v, wx, wz);
            }
        }
    }

    // **침식하기 전에 낮춘다** — 나중에 눌러 내리면 물길이 이미 안쪽으로 다 그려진 뒤라
    // 가장자리만 어색하게 잘린다. 먼저 낮춰 두면 **물이 바깥으로 흘러** 하구가 생긴다.
    float EdgeFade(float height, float wx, float wz)
    {
        if (_edgeFalloff < 1f) return height;

        float w = TerrainGrid.WorldSize;
        float d = Mathf.Min(Mathf.Min(wx, w - wx), Mathf.Min(wz, w - wz));   // 가장 가까운 변까지
        if (d >= _edgeFalloff) return height;

        // **부드럽게** — 선형으로 끊으면 띠가 시작되는 자리에 눈에 보이는 금이 간다.
        float t = Mathf.SmoothStep(0f, 1f, d / _edgeFalloff);
        return Mathf.Lerp(_edgeHeight, height, t);
    }

    // ── ② 물 침식 ───────────────────────────────────────────────────────────
    //
    // 물방울 하나를 떨어뜨려 경사를 따라 굴린다. 빨리 내려갈수록 많이 실을 수 있고,
    // 실을 수 있는 것보다 많이 들면 **쌓고**, 적게 들면 **깎는다.** 그 단순한 규칙에서
    // 골짜기와 능선과 선상지가 나온다.
    void Hydraulic(float[] h, float[] deposit, float[] wear, float[] flow, int n)
    {
        var rng = new System.Random(_seed);
        int total = Droplets;

        // 깎을 때 **한 점만 파면 바늘 구멍이 난다** — 둘레에 가중치로 나눈다.
        int r = _radius;
        int side = r * 2 + 1;
        var kernel = new float[side * side];
        float ksum = 0f;
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float w = Mathf.Max(0f, r - d);
            kernel[(dy + r) * side + (dx + r)] = w;
            ksum += w;
        }
        if (ksum <= 0f) { kernel[0] = 1f; ksum = 1f; }
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= ksum;

        for (int drop = 0; drop < total; drop++)
        {
            if ((drop & 8191) == 0)
                EditorUtility.DisplayProgressBar("침식", $"물방울 {drop:N0} / {total:N0}",
                                                 0.15f + 0.7f * drop / total);

            float px = (float)rng.NextDouble() * (n - 1);
            float py = (float)rng.NextDouble() * (n - 1);
            float dx = 0f, dy = 0f;
            float speed = 1f, water = 1f, sediment = 0f;

            for (int life = 0; life < _lifetime; life++)
            {
                int cx = (int)px, cy = (int)py;
                if (cx < 0 || cy < 0 || cx >= n - 1 || cy >= n - 1) break;

                float fx = px - cx, fy = py - cy;

                // 이 자리의 높이와 기울기 — 이중선형.
                float h00 = h[cy * n + cx],     h10 = h[cy * n + cx + 1];
                float h01 = h[(cy + 1) * n + cx], h11 = h[(cy + 1) * n + cx + 1];

                float gx = (h10 - h00) * (1 - fy) + (h11 - h01) * fy;
                float gy = (h01 - h00) * (1 - fx) + (h11 - h10) * fx;
                float here = h00 * (1 - fx) * (1 - fy) + h10 * fx * (1 - fy)
                           + h01 * (1 - fx) * fy       + h11 * fx * fy;

                // **관성을 준다** — 없으면 물방울이 매 걸음 최급경사로 꺾여 지그재그 자국을 남긴다.
                dx = dx * _inertia - gx * (1 - _inertia);
                dy = dy * _inertia - gy * (1 - _inertia);

                float len = Mathf.Sqrt(dx * dx + dy * dy);
                if (len < 1e-5f) break;          // 평지에 고였다
                dx /= len; dy /= len;

                px += dx; py += dy;
                if (px < 0f || py < 0f || px >= n - 1 || py >= n - 1) break;

                int nx = (int)px, ny = (int)py;
                float nfx = px - nx, nfy = py - ny;
                float nh = h[ny * n + nx]           * (1 - nfx) * (1 - nfy)
                         + h[ny * n + nx + 1]       * nfx       * (1 - nfy)
                         + h[(ny + 1) * n + nx]     * (1 - nfx) * nfy
                         + h[(ny + 1) * n + nx + 1] * nfx       * nfy;

                float drop_ = nh - here;   // 음수면 내려간 것

                flow[cy * n + cx] += water;

                float cap = Mathf.Max(-drop_ * speed * water * _capacity, _minCapacity);

                if (sediment > cap || drop_ > 0f)
                {
                    // **올라가는 중이면 구덩이를 메운다** — 그러지 않으면 물방울이 판 구멍이
                    // 그대로 남아 지형이 곰보가 된다.
                    float amount = drop_ > 0f ? Mathf.Min(drop_, sediment)
                                              : (sediment - cap) * _deposit;
                    sediment -= amount;

                    // 쌓는 것은 **바로 그 네 칸에** — 퍼뜨리면 골짜기 바닥이 안 메워진다.
                    h[cy * n + cx]           += amount * (1 - fx) * (1 - fy);
                    h[cy * n + cx + 1]       += amount * fx       * (1 - fy);
                    h[(cy + 1) * n + cx]     += amount * (1 - fx) * fy;
                    h[(cy + 1) * n + cx + 1] += amount * fx       * fy;

                    deposit[cy * n + cx] += amount;
                }
                else
                {
                    // 깎는 것은 **둘레에 나눠서** — 한 점만 파면 바늘 구멍이 난다.
                    float amount = Mathf.Min((cap - sediment) * _erode, -drop_);

                    // **맵 밖으로 나간 몫을 버리지 않는다**(2026-08-27 수정).
                    //
                    // 그냥 건너뛰면 가장자리는 커널의 절반만 적용돼 **안쪽보다 덜 깎이고**,
                    // 세계 둘레에 솟은 테두리가 생긴다(실제로 그렇게 나왔다).
                    // 안에 든 몫을 먼저 세고 그것으로 나눈다 — 총량이 언제나 `amount`다.
                    float inside = 0f;
                    for (int ky = -r; ky <= r; ky++)
                    for (int kx = -r; kx <= r; kx++)
                    {
                        int ax = cx + kx, ay = cy + ky;
                        if (ax < 0 || ay < 0 || ax >= n || ay >= n) continue;
                        inside += kernel[(ky + r) * side + (kx + r)];
                    }
                    if (inside <= 1e-6f) break;

                    for (int ky = -r; ky <= r; ky++)
                    for (int kx = -r; kx <= r; kx++)
                    {
                        int ax = cx + kx, ay = cy + ky;
                        if (ax < 0 || ay < 0 || ax >= n || ay >= n) continue;

                        float w = kernel[(ky + r) * side + (kx + r)];
                        if (w <= 0f) continue;

                        float take = amount * (w / inside);
                        h[ay * n + ax]    -= take;
                        wear[ay * n + ax] += take;
                    }
                    sediment += amount;
                }

                speed = Mathf.Sqrt(Mathf.Max(0f, speed * speed + -drop_ * _gravity));
                water *= 1f - _evaporate;
                if (water < 0.01f) break;
            }
        }
    }

    // ── ③ 열 풍화 ───────────────────────────────────────────────────────────
    //
    // 물과 무관하게, **너무 가파른 곳은 무너져 내린다.** 이것이 없으면 물길 옆이 칼처럼
    // 서 있어서, 건설도 못 하고 눈에도 인공적으로 보인다.
    void Thermal(float[] h, int n)
    {
        float cell = TerrainGrid.WorldSize / (float)(n - 1);
        float maxDrop = Mathf.Tan(_talus * Mathf.Deg2Rad) * cell;
        var move = new float[h.Length];

        // **테두리도 돈다**(2026-08-27 수정) — 건너뛰면 안쪽만 무너져 내려서
        // 세계 둘레가 상대적으로 솟는다. 이웃은 좌표를 접어 짚는다.
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int i = y * n + x;
            float me = h[i];

            // 네 이웃 중 **가장 낮은 쪽으로만** 흘린다 — 여덟 방향에 나누면 지형이 뭉갠다.
            int   best = -1;
            float bestDiff = maxDrop;

            int[] nb =
            {
                y * n + Mathf.Max(0, x - 1),
                y * n + Mathf.Min(n - 1, x + 1),
                Mathf.Max(0, y - 1) * n + x,
                Mathf.Min(n - 1, y + 1) * n + x,
            };
            for (int k = 0; k < 4; k++)
            {
                if (nb[k] == i) continue;   // 접힌 이웃은 자기 자신이다
                float diff = me - h[nb[k]];
                if (diff > bestDiff) { bestDiff = diff; best = nb[k]; }
            }

            if (best < 0) continue;

            float amount = (bestDiff - maxDrop) * 0.5f;
            move[i]    -= amount;
            move[best] += amount;
        }

        for (int i = 0; i < h.Length; i++) h[i] += move[i];
    }

    // ── ④ 쓰기 ──────────────────────────────────────────────────────────────

    void Write(float[] h, float[] deposit, float[] wear, float[] flow, int n,
               System.Diagnostics.Stopwatch sw)
    {
        EditorUtility.DisplayProgressBar("침식", "쓰는 중…", 0.96f);
        Directory.CreateDirectory(Dir);

        // **실제로 나온 범위를 눈금으로 쓴다** — 고정값으로 두면 낮은 세계는 눈금을 낭비하고
        // 높은 세계는 잘린다(16bit라 범위가 좁을수록 정밀하다).
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var v in h) { if (v < lo) lo = v; if (v > hi) hi = v; }
        if (hi - lo < 1f) hi = lo + 1f;

        var bytes = new byte[h.Length * 2];
        for (int i = 0; i < h.Length; i++)
        {
            ushort u = (ushort)Mathf.Clamp(Mathf.RoundToInt((h[i] - lo) / (hi - lo) * 65535f), 0, 65535);
            bytes[i * 2]     = (byte)(u & 0xFF);
            bytes[i * 2 + 1] = (byte)(u >> 8);
        }
        File.WriteAllBytes(Dir + "/Heightmap.bytes", bytes);

        // ── 마스크 ──────────────────────────────────────────────────────────
        //
        // **각자 제 최대로 정규화한다** — 퇴적과 침식은 자릿수가 다르다.
        Norm(deposit); Norm(wear); Norm(flow);

        // **높이맵보다 성기게 낸다.** 골짜기에 흙을 까는 데 4m 눈금까지는 필요 없고,
        // 채널이 셋이라 크기가 1.5배로 붙는다 — 4096²이면 마스크만 **50MB**다.
        // 2048에서 자르면 12.6MB이고 화면에서는 차이가 안 보인다.
        int mn = Mathf.Min(n, MaxMaskResolution);
        var masks = new byte[mn * mn * 3];

        for (int y = 0; y < mn; y++)
        for (int x = 0; x < mn; x++)
        {
            // **줄여도 평균을 낸다** — 한 셀만 집으면 물길처럼 가느다란 것이 통째로 빠진다.
            int step = n / mn;
            float d = 0f, w = 0f, f = 0f;
            int cnt = 0;

            for (int sy = 0; sy < step; sy++)
            for (int sx = 0; sx < step; sx++)
            {
                int ax = Mathf.Min(x * step + sx, n - 1);
                int ay = Mathf.Min(y * step + sy, n - 1);
                int i  = ay * n + ax;
                d += deposit[i]; w += wear[i]; f += flow[i];
                cnt++;
            }
            if (cnt == 0) cnt = 1;

            int o = (y * mn + x) * 3;
            masks[o]     = (byte)(Mathf.Clamp01(d / cnt) * 255f);
            masks[o + 1] = (byte)(Mathf.Clamp01(w / cnt) * 255f);
            masks[o + 2] = (byte)(Mathf.Clamp01(f / cnt) * 255f);
        }
        File.WriteAllBytes(Dir + "/HeightmapMasks.bytes", masks);

        AssetDatabase.Refresh();

        var asset = AssetDatabase.LoadAssetAtPath<TerrainHeightmap>(Dir + "/Heightmap.asset");
        bool isNew = asset == null;
        if (isNew) asset = CreateInstance<TerrainHeightmap>();

        asset.Data           = AssetDatabase.LoadAssetAtPath<TextAsset>(Dir + "/Heightmap.bytes");
        asset.Masks          = AssetDatabase.LoadAssetAtPath<TextAsset>(Dir + "/HeightmapMasks.bytes");
        asset.Resolution     = n;
        asset.MaskResolution = mn;
        asset.BakedWorldSize = TerrainGrid.WorldSize;   // 세계를 줄이면 그림이 눌린다 — 알아채려면 적어야 한다
        asset.MinHeight  = lo;
        asset.MaxHeight  = hi;
        asset.BakedWith  = $"시드 {_seed} · {n}² · 물방울 {Droplets:N0}(수명 {_lifetime}) · " +
                           $"열 풍화 {_thermalPasses}회 {_talus:0}° · " +
                           $"가장자리 {_edgeFalloff:0}m → {_edgeHeight:0}m";
        asset.Forget();

        if (isNew) AssetDatabase.CreateAsset(asset, Dir + "/Heightmap.asset");
        else       EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        // 씬에 물린다 — 만들어만 두면 아무 데도 안 쓰이고, 그건 "안 바뀐다"로만 보인다.
        int wired = 0;
        foreach (var s in FindObjectsByType<TerrainStreamer>(FindObjectsInactive.Include,
                                                            FindObjectsSortMode.None))
        {
            var so = new SerializedObject(s);
            var p  = so.FindProperty("heightmap");
            if (p == null) continue;
            p.objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(s);
            wired++;
        }

        TerrainHeight.Heightmap = asset;
        WorldSceneTerrain.Refresh();
        TerrainAuthoring.RefreshPreview();
        Selection.activeObject = asset;

        sw.Stop();
        Debug.Log($"[침식] {n}² · 물방울 {Droplets:N0} · {sw.Elapsed.TotalSeconds:0.#}초.\n" +
                  $"높이 {lo:0}m ~ {hi:0}m · {bytes.Length / 1048576f:0.#}MB → {Dir}\n" +
                  $"마스크 {mn}²(퇴적·침식·물길) {masks.Length / 1048576f:0.#}MB — " +
                  $"스플랫이 능선엔 맨바위, 골짜기엔 흙을 깐다.\n" +
                  $"TerrainStreamer {wired}개에 물렸다. **씬을 저장할 것.**");
    }

    static void Norm(float[] a)
    {
        float max = 0f;
        foreach (var v in a) if (v > max) max = v;
        if (max <= 0f) return;

        // **제곱근을 씌운다** — 침식·퇴적은 소수의 자리에 몰려서, 선형으로 정규화하면
        // 나머지가 전부 0에 붙어 마스크가 검은 판이 된다.
        for (int i = 0; i < a.Length; i++) a[i] = Mathf.Sqrt(Mathf.Clamp01(a[i] / max));
    }
}
