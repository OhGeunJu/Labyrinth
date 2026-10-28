using UnityEngine;

// **청크에 텍스처를 깐다 — 높이·경사·바이옴에서**(2026-08-27).
//
// **저장할 것이 하나도 안 는다.** 스플랫은 이미 있는 것들의 함수다:
//
//     가중치 = 바이옴(어디인가)  ×  경사(절벽인가)
//
// 지형 높이가 노이즈 함수인 것과 같은 이유로 이것도 함수여야 한다 — 청크는 들어설 때마다
// 새로 만들어지므로, 저작해 둘 데가 애초에 없다.
//
// ── 답이 한 벌이어야 한다 ───────────────────────────────────────────────────
//
// 런타임(`TerrainStreamer`)과 저작 도구(`TerrainAuthoring.Fill`)가 **같은 식**을 써야 한다.
// 갈리면 에디터에서 본 땅과 게임의 땅이 다른 색이 되고, 그건 "왜 다르지"로만 보인다.
public static class TerrainSplat
{
    // **씬이 팔레트의 주인이다** — `TerrainStreamer`가 넣어 준다(프로필·특징과 같은 자리).
    public static TerrainPalette Palette;

    // 청크 한 변의 스플랫 해상도. 256m를 64로 나누면 **한 칸이 4m**다.
    //
    // 더 키우면 경계가 곱지만 알파맵이 제곱으로 는다. 스타일라이즈드 텍스처는 타일이 큼직해
    // 4m면 눈에 거슬리지 않는다.
    public const int Resolution = 64;

    // 청크마다 다시 쓰는 알파맵 버퍼 — 근거는 `Apply` 안에 적어 두었다.
    static float[,,] _weights;

    static bool _warnedNoPalette;

    // `heights`는 `TerrainAuthoring.Fill`이 이미 채운 정규화 높이(0~1)다.
    // **거기서 경사를 낸다 — 노이즈를 다시 부르지 않는다.** 다시 부르면 청크당 16,000번이
    // 더해져 세우는 비용이 두 배가 된다.
    public static void Apply(TerrainData data, Vector2Int chunk, float[,] heights)
    {
        var pal = Palette;
        if (data == null) return;

        // **조용히 돌아가지 않는다**(2026-09-02). 팔레트가 없거나 레이어가 비면 여기서 그냥
        // 나갔는데, 그러면 지형에 **텍스처가 한 장도 안 깔린 채** 서고 화면은 **검은 판**이다 —
        // *왜* 검은지가 어디에도 안 적혀 있어서, 빌드에서만 검게 나왔을 때 짚을 데가 없었다.
        // **한 번만 알린다**(청크마다 부르는 자리다).
        if (pal == null || !pal.IsUsable)
        {
            if (!_warnedNoPalette)
            {
                _warnedNoPalette = true;
                Debug.LogError(pal == null
                    ? "[지형] 팔레트가 없다 — 지형이 **검게** 나온다. 씬의 `TerrainStreamer.palette`를 볼 것."
                    : "[지형] 팔레트에 레이어가 하나도 없다 — 지형이 **검게** 나온다. " +
                      "`TerrainPalette.Biomes`의 `Ground`가 비었나?");
            }
            return;
        }

        int n = pal.Layers.Length;

        // **레이어를 먼저** — 알파맵이 이 길이에 맞춰진다.
        //
        // **다만 같은 것을 다시 넣지 않는다**(2026-09-02). `TerrainStreamer`가 청크 그릇을
        // 돌려 쓰게 된 뒤로 이 함수는 **이미 쓰던 `TerrainData`**에 다시 불린다 — 레이어나
        // 해상도를 그대로 다시 대입하면 유니티가 **알파맵 텍스처를 새로 만들어** 돌려 쓰는
        // 뜻이 사라진다. 팔레트가 진짜로 바뀐 때만 갈아 끼운다(저작 중에는 바뀐다).
        var cur = data.terrainLayers;
        bool same = cur != null && cur.Length == n;
        if (same)
            for (int i = 0; i < n; i++)
                if (cur[i] != pal.Layers[i]) { same = false; break; }

        if (!same) data.terrainLayers = pal.Layers;
        if (data.alphamapResolution != Resolution) data.alphamapResolution = Resolution;

        int hRes = heights.GetLength(0);              // 129

        // **알파맵 배열을 다시 쓴다**(2026-09-02). `64×64×16`은 **262KB**이고, 청크를
        // 세울 때마다 새로 만들면 그만큼이 통째로 쓰레기가 된다 — 걸어 다니는 내내 쌓이다가
        // **10초쯤마다 GC가 16ms를 먹는다**(프로파일러의 `GarbageCollector.CollectIncremental`
        // 10.9ms + `GC.Collect` 6.0ms짜리 프레임이 그것이다. **할당한 프레임이 아니라 엉뚱한
        // 프레임이 값을 치른다** — 그래서 원인을 찾기가 어렵다).
        //
        // `SetAlphamaps`가 값을 복사해 가므로 다시 써도 된다. **다만 반드시 비운다** —
        // `AddBiome`이 **더하기**라, 안 비우면 지난 청크의 가중치가 남아 **팔레트에 없는
        // 채널이 살아 있는 것처럼 보이고** 마지막 정규화가 그걸 나눠 담는다.
        if (_weights == null || _weights.GetLength(2) != n)
            _weights = new float[Resolution, Resolution, n];
        else
            System.Array.Clear(_weights, 0, _weights.Length);

        var w = _weights;

        var hm = TerrainHeight.Heightmap;

        float cliffLo = Mathf.Cos((pal.CliffAngle + pal.CliffBlend) * Mathf.Deg2Rad);
        float cliffHi = Mathf.Cos(Mathf.Max(0f, pal.CliffAngle - pal.CliffBlend) * Mathf.Deg2Rad);

        // **시드를 좌표 오프셋으로 준다** — 유니티 펄린에 시드 인자가 없다(`TerrainHeight`가
        // 쓰는 그 수법). 지형과 **다른 오프셋**을 쓴다: 같으면 얼룩이 기복과 겹쳐 붙어 다닌다.
        float seed = TerrainHeight.Profile.Seed * 0.7913f + 4177f;

        for (int z = 0; z < Resolution; z++)
        for (int x = 0; x < Resolution; x++)
        {
            // 스플랫 칸 → 높이 격자 → 월드 좌표.
            int hx = Mathf.Clamp(Mathf.RoundToInt(x / (float)(Resolution - 1) * (hRes - 1)), 0, hRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(z / (float)(Resolution - 1) * (hRes - 1)), 0, hRes - 1);

            float wx = chunk.x * TerrainGrid.ChunkSize + x / (float)(Resolution - 1) * TerrainGrid.ChunkSize;
            float wz = chunk.y * TerrainGrid.ChunkSize + z / (float)(Resolution - 1) * TerrainGrid.ChunkSize;

            // ── 경사 ────────────────────────────────────────────────────────
            //
            // **알파맵은 [z, x]이고 높이 배열도 [z, x]다.** 뒤집으면 텍스처가 전치돼
            // 절벽이 엉뚱한 면에 붙는다.
            float hL = heights[hz, Mathf.Max(0, hx - 1)];
            float hR = heights[hz, Mathf.Min(hRes - 1, hx + 1)];
            float hD = heights[Mathf.Max(0, hz - 1), hx];
            float hU = heights[Mathf.Min(hRes - 1, hz + 1), hx];

            // 정규화 높이(0~1)를 미터로 되돌려 기울기를 낸다.
            float dx = (hR - hL) * TerrainStreamer.HeightRange / (2f * TerrainGrid.VertexSpacing);
            float dz = (hU - hD) * TerrainStreamer.HeightRange / (2f * TerrainGrid.VertexSpacing);

            // 법선의 위쪽 성분 — 1이면 평지, 0이면 수직.
            float up = 1f / Mathf.Sqrt(dx * dx + dz * dz + 1f);

            // **부드럽게 넘긴다.** 각도로 칼같이 자르면 산비탈에 등고선 같은 줄이 생긴다.
            //
            // 그것만으로는 **매끈한 띠**가 남는다 — 경사가 같은 자리를 따라 도는 줄이라
            // 자연에는 없는 모양이다. 문턱을 흔들면 바위가 삐죽삐죽 물려 들어간다.
            float edge = 0f;
            if (pal.EdgeNoise > 0.001f && pal.EdgePeriod > 0.01f)
                edge = (Mathf.PerlinNoise((wx + seed) / pal.EdgePeriod,
                                          (wz + seed) / pal.EdgePeriod) - 0.5f) * pal.EdgeNoise;

            float cliff = 1f - Mathf.SmoothStep(cliffLo, cliffHi, Mathf.Clamp01(up + edge));

            // ── 침식이 아는 것 ──────────────────────────────────────────────
            //
            // **깎인 데는 암반이 드러나고 쌓인 데는 흙이 덮인다.** 경사만 봐서는 못 가르는
            // 자리 — 능선과 골짜기 바닥은 둘 다 완만할 수 있는데 눈에는 전혀 다르다.
            float depositMask = 0f;
            if (hm != null && hm.HasMasks)
            {
                var m = hm.SampleMask(wx, wz);
                cliff = Mathf.Clamp01(cliff + m.y * pal.WearToRock * (1f - cliff));
                depositMask = m.x * pal.DepositToVariant;
            }

            // ── 바이옴 ──────────────────────────────────────────────────────
            //
            // **지형 높이와 같은 식으로 섞는다**(`TerrainHeight.BlendAt`) — 갈리면 기복이
            // 바뀌는 자리와 텍스처가 바뀌는 자리가 어긋나 **경계가 두 줄로 보인다.**
            var b = TerrainHeight.BlendAt(wx, wz);
            float ground = 1f - cliff;

            // ── 변형 ────────────────────────────────────────────────────────
            //
            // **넓은 얼룩으로 같은 바닥의 두 얼굴을 섞는다.** 타일을 키우는 것만으로는
            // 가까이서만 나아지고 **멀리서 보는 반복은 그대로**다 — 눈에 걸리는 것은
            // 2m 타일이 아니라 *"저 벌판 전체가 한 장"*이라는 인상이다.
            //
            // 주기가 시야보다 커야 *"저쪽은 색이 좀 다르네"*로 읽힌다. 작으면 그냥 지저분하다.
            float blob = Blob(wx, wz, seed, pal);

            // 가운데를 부드럽게 넘긴다 — 선형으로 두면 절반이 늘 어중간하게 섞여 흐려진다.
            float v = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((blob - 0.5f) * 2.2f + 0.5f))
                    * pal.VariantAmount;

            // **퇴적은 얼룩을 이긴다** — 골짜기 바닥은 노이즈가 뭐라 하든 흙이 쌓인 자리다.
            v = Mathf.Max(v, depositMask);

            // **절벽도 바이옴을 따라간다** — 사막 비탈과 숲 비탈이 같으면 바이옴이 절반만
            // 바뀐 것으로 보인다(바닥은 모래인데 비탈은 이끼 낀 바위인 꼴).
            AddBiome(w, z, x, pal, b.A, b.Wa, ground, cliff, v);
            AddBiome(w, z, x, pal, b.B, b.Wb, ground, cliff, v);
            AddBiome(w, z, x, pal, b.C, b.Wc, ground, cliff, v);
            AddBiome(w, z, x, pal, b.D, b.Wd, ground, cliff, v);

            // **합을 1로 되돌린다.** 유니티는 합이 1이 아니면 제멋대로 정규화하는데,
            // 전부 0인 칸(팔레트에 없는 바이옴)은 그때 **첫 채널로 통째로 떨어진다.**
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += w[z, x, i];
            if (sum <= 0.0001f) w[z, x, 0] = 1f;
            else for (int i = 0; i < n; i++) w[z, x, i] /= sum;
        }

        data.SetAlphamaps(0, 0, w);
    }

    // ── 얼룩 마스크 ─────────────────────────────────────────────────────────
    //
    // 펄린 **한 옥타브**로 뽑으면 얼룩이 죄다 동글동글하고 **크기가 비슷하다** — 그것대로
    // 규칙이 보여서, 타일 반복을 깨려던 것이 다른 반복이 된다.
    //
    // 둘로 고친다:
    //
    //   ① **프랙탈** — 큰 층 위에 절반 크기, 그 위에 또 절반. 큰 얼룩 안에 작은 얼룩이
    //      물려 들어가 **크기가 섞인다.**
    //   ② **도메인 워핑** — 좌표 자체를 다른 노이즈로 밀어서 샘플한다. 방울의 경계가
    //      **구불구불 흐트러진다.** 노이즈를 곱하거나 더하는 것과 다르다: 모양을 바꾼다.
    //
    // 셋 다 좌표의 함수라 **저장되는 것은 여전히 없다.**
    static float Blob(float wx, float wz, float seed, TerrainPalette pal)
    {
        float period = pal.VariantPeriod;
        if (period < 1f) return 0f;

        float u = (wx + seed) / period;
        float v = (wz + seed) / period;

        // ① 워핑 — **다른 오프셋**을 쓴다. 같으면 좌표가 제 노이즈로 밀려 무늬가 굳는다.
        if (pal.VariantWarp > 0.001f)
        {
            float wu = Mathf.PerlinNoise(u * 1.7f + 31.4f, v * 1.7f + 71.2f) - 0.5f;
            float wv = Mathf.PerlinNoise(u * 1.7f + 11.9f, v * 1.7f + 53.7f) - 0.5f;
            u += wu * pal.VariantWarp * 2f;
            v += wv * pal.VariantWarp * 2f;
        }

        // ② 프랙탈 — 층마다 주기 절반, 세기 절반. 합을 정규화해 0~1로 되돌린다
        //    (안 되돌리면 층을 늘릴 때마다 마스크가 통째로 흐려진다).
        float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
        for (int i = 0; i < 3; i++)
        {
            sum  += Mathf.PerlinNoise(u * freq, v * freq) * amp;
            norm += amp;
            amp  *= 0.5f;
            freq *= 2f;
        }
        return norm > 0f ? sum / norm : 0.5f;
    }

    // 바이옴 하나 몫을 **본래와 변형에 나눠** 얹는다. 변형이 없으면 `VariantOf`가 본래
    // 채널을 돌려주므로 그대로 합쳐진다 — 부르는 쪽이 있나 없나를 안 묻는다.
    static void AddBiome(float[,,] w, int z, int x, TerrainPalette pal, Biome b,
                         float weight, float ground, float cliff, float v)
    {
        if (weight <= 0f) return;

        float g = weight * ground;
        Add(w, z, x, pal.ChannelOf(b), g * (1f - v));
        Add(w, z, x, pal.VariantOf(b), g * v);
        Add(w, z, x, pal.CliffOf(b),   weight * cliff);
    }

    static void Add(float[,,] w, int z, int x, int channel, float v)
    {
        if (channel < 0 || channel >= w.GetLength(2) || v <= 0f) return;
        w[z, x, channel] += v;
    }
}
