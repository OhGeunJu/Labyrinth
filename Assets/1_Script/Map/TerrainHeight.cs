using System.Collections.Generic;
using UnityEngine;

// **세계 어디든 높이를 답한다 — 로드 여부와 무관하게**(2026-08-26).
//
// 켄시 규모(29,440m)의 이어진 세계로 가면서 지형이 청크 스트리밍이 되는데, **높이를 묻는
// 곳은 스트리밍을 몰라야 한다.** `MapManager.SampleGroundHeight`는 매 틱 여러 번 불리고
// (폰 배치·건물·경로), 그중에는 **아직 로드 안 된 곳을 묻는 경우도 있다**(길찾기가 먼 곳의
// 통행을 재거나, 원정대가 지도 반대편에 있을 때).
//
// 그래서 답의 원본이 **콜라이더도 `Terrain`도 아니어야 한다**:
//
//     노이즈 함수  +  손댄 델타(희소)
//
// 셋이 한꺼번에 풀린다 — ① 언로드된 곳도 답이 나온다 ② 레이캐스트가 사라져 훨씬 빠르다
// ③ 물리 상태에 안 기대므로 **결정적**이다(같은 자리는 언제 물어도 같은 값).
//
// **그리는 것도 이 값을 쓴다.** 메시가 이 함수와 다른 높이로 그려지면 폰이 지면에 뜨거나
// 파묻힌다 — `TerrainStreamer`가 정점을 채울 때 `VertexHeight`를 그대로 부른다.
public static class TerrainHeight
{
    // 전역 정점 축당 개수: 115 × 128 + 1 = 14,721.
    // **`VertsPerAxis`가 아니라 128을 곱한다** — 청크가 소유하는 것은 0~127 줄뿐이고
    // 128번째는 이웃의 0번이다(`TerrainDelta`의 소유권 규칙).
    public const int OwnedPerChunk = TerrainGrid.VertsPerAxis - 1;   // 128

    public static TerrainProfile Profile = TerrainProfile.Default;

    // **저작한 지형 특징** — 산·분지. 노이즈 위에 얹힌다(`TerrainFeature`).
    // 씬의 `TerrainStreamer`가 넣어 준다 — 코드가 들면 저작할 수가 없다.
    public static TerrainFeature[] Features = System.Array.Empty<TerrainFeature>();

    // **저작한 높이맵 — 있으면 대륙 축을 대신한다**(2026-08-27).
    //
    // 없으면 지금까지처럼 펄린 한 층이 큰 모양을 만든다. 있으면 그것이 **물길과 능선을 가진
    // 진짜 지형**을 대신 답한다(`TerrainErosionBaker`가 굽는다).
    //
    // **여전히 함수다** — 메모리의 배열을 이중선형으로 뽑을 뿐이라 로드 안 된 청크도 답이 나온다.
    // **바뀌면 기억을 버린다** — 아래 정점 캐시가 이 값에서 나오므로, 그냥 필드로 두면
    // 침식 도구가 높이맵을 갈아 끼운 뒤에도 **옛 높이가 그대로 답한다.**
    static TerrainHeightmap _heightmap;

    public static TerrainHeightmap Heightmap
    {
        get => _heightmap;
        set
        {
            if (_heightmap == value) return;
            _heightmap = value;
            ForgetVertices();
        }
    }

    // ── 바이옴이 지형 성격을 정한다 ─────────────────────────────────────────
    //
    // 저작해 둔 월드맵(16×16)의 한 칸이 **1,024m짜리 지역**이고, 그 바이옴이 기복을 정한다
    // (`BiomeTerrain.Of` — 평원 완만 0.7 · 늪 꺼짐 −0.8 · 동굴 지역 산악 7.0).
    // **저작한 지도가 그대로 살아난다** — 다시 그릴 것이 없다.
    //
    // **이웃 넷을 이중선형으로 섞는다.** 정수로 끊으면 지역 경계에서 지형이 **칼같이 바뀌어**
    // 1,024m마다 눈에 보이는 금이 간다. 섞으면 사막에서 설원으로 걸어가는 동안 기복이 서서히
    // 변하고 경계는 안 보인다 — 옛 3D 월드맵 지형이 쓰던 그 기법이다.
    //
    // **격자는 한 번 읽고 캐싱한다** — 정점마다 부르는 자리라(청크 하나가 16,641번) `WorldMap`을
    // 매번 뒤지면 그게 굽는 시간의 전부가 된다.
    static Biome[] _biomes;

    public static void ForgetBiomes() { _biomes = null; ForgetVertices(); }

    static Biome BiomeAt(int rx, int ry)
    {
        int n = TerrainGrid.RegionsPerAxis;
        if (_biomes == null)
        {
            _biomes = new Biome[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                _biomes[y * n + x] = WorldMap.AuthoredBiome(x, y);
        }
        rx = Mathf.Clamp(rx, 0, n - 1);
        ry = Mathf.Clamp(ry, 0, n - 1);
        return _biomes[ry * n + rx];
    }

    // **그 자리에 어느 바이옴이 얼마나 섞였나.**
    //
    // `BlendedBiome`이 *"기복이 얼마나 험한가"*를 답한다면 이쪽은 *"무엇이 깔리나"*에 답한다 —
    // 스플랫(`TerrainSplat`)이 쓴다. **같은 식이어야 한다**: 갈리면 기복이 바뀌는 자리와
    // 텍스처가 바뀌는 자리가 어긋나 **경계가 두 줄로 보인다.**
    public struct BiomeBlend
    {
        public Biome A, B, C, D;
        public float Wa, Wb, Wc, Wd;
    }

    public static BiomeBlend BlendAt(float worldX, float worldZ)
    {
        var uv = TerrainGrid.BiomeUV(worldX, worldZ);   // **굽힌 자리**로 묻는다
        float fx = uv.x - 0.5f, fy = uv.y - 0.5f;

        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;

        return new BiomeBlend
        {
            A = BiomeAt(x0,     y0    ), Wa = (1f - tx) * (1f - ty),
            B = BiomeAt(x0 + 1, y0    ), Wb = tx        * (1f - ty),
            C = BiomeAt(x0,     y0 + 1), Wc = (1f - tx) * ty,
            D = BiomeAt(x0 + 1, y0 + 1), Wd = tx        * ty,
        };
    }

    // 그 자리의 지형 성격 — 이웃 넷을 섞은 것.
    public static BiomeTerrainProfile BlendedBiome(float worldX, float worldZ)
    {
        // **반 칸 밀어서 잰다** — 안 밀면 지역의 *가운데*가 아니라 *모서리*가 그 바이옴의
        // 순수한 값이 되어, 칸 한복판에서 이웃 색이 절반 섞인다.
        var uv = TerrainGrid.BiomeUV(worldX, worldZ);   // **굽힌 자리**로 묻는다
        float fx = uv.x - 0.5f, fy = uv.y - 0.5f;

        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;

        var a = BiomeTerrain.Of(BiomeAt(x0,     y0));
        var b = BiomeTerrain.Of(BiomeAt(x0 + 1, y0));
        var c = BiomeTerrain.Of(BiomeAt(x0,     y0 + 1));
        var d = BiomeTerrain.Of(BiomeAt(x0 + 1, y0 + 1));

        return new BiomeTerrainProfile
        {
            Base      = Bi(a.Base,      b.Base,      c.Base,      d.Base,      tx, ty),
            Amplitude = Bi(a.Amplitude, b.Amplitude, c.Amplitude, d.Amplitude, tx, ty),
            Roughness = Bi(a.Roughness, b.Roughness, c.Roughness, d.Roughness, tx, ty),
        };
    }

    static float Bi(float a, float b, float c, float d, float tx, float ty) =>
        Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);

    // ── 노이즈 ──────────────────────────────────────────────────────────────
    //
    // **결정적이어야 한다.** 청크 경계의 정점은 두 청크가 각자 계산하므로, 같은 입력에서
    // 같은 값이 안 나오면 그 줄에 틈이 생긴다. `Mathf.PerlinNoise`는 결정적이고, 시드는
    // **좌표를 옮기는 것**으로 준다(유니티 펄린에는 시드 인자가 없다).
    //
    // 옥타브를 함수로 접지 않고 셋을 직접 적는 이유: 각 층이 **화면에서 무엇으로 보이는지**가
    // 다르다(대륙 · 언덕 · 발밑). 이름이 붙어 있어야 저작할 때 어느 손잡이를 돌릴지 안다.
    // **이름이 `Procedural`인 이유**: 노이즈만이 아니라 **저작한 특징까지** 더한 값이다.
    // 저장 안 되는 것(= 델타가 아닌 것) 전부를 뜻한다 — 굽는 도구가 이 값과 견줘 "손댄 것"을
    // 가려내므로, 특징이 여기 안 들어가면 **산 전체가 델타로 구워진다.**
    public static float Procedural(float worldX, float worldZ)
    {
        float h = BaseNoise(worldX, worldZ);

        // **특징이 몇 개뿐이라 그냥 훑는다.** 수백 개가 되면 여기 공간 색인이 필요해진다 —
        // 정점마다 불리는 자리다(청크 하나가 16,641번).
        var f = Features;
        for (int i = 0; i < f.Length; i++) h += f[i].Contribution(worldX, worldZ);
        return h;
    }

    // **큰 모양 — 저작한 높이맵이 있으면 그것이 답한다**(2026-08-27).
    //
    // 세 층의 역할이 이걸로 깔끔하게 갈린다:
    //   · 대륙(여기)   — 물길·능선·골짜기. **저작하거나** 노이즈
    //   · 언덕·잔기복  — 바이옴이 크기와 잘기를 정한다
    //   · 특징         — 손으로 얹은 산·분지
    static float Macro(float worldX, float worldZ)
    {
        var hm = Heightmap;
        if (hm != null && hm.IsUsable) return hm.SampleAt(worldX, worldZ);
        return MacroNoise(worldX, worldZ);
    }

    // 높이맵이 **없을 때의** 큰 모양. **굽는 도구가 이것을 씨앗으로 쓴다** — `Macro`를 부르면
    // 이미 구운 높이맵을 도로 읽어 되먹임이 된다(구울수록 지형이 제 그림자를 좇는다).
    public static float MacroNoise(float worldX, float worldZ)
    {
        var p = Profile;
        var b = BlendedBiome(worldX, worldZ);
        float s = p.Seed * 0.7913f;   // 시드를 좌표 오프셋으로

        // **대륙은 바이옴을 안 탄다** — 그건 지역보다 큰 축(주기 4km)이라, 여기에 바이옴을
        // 곱하면 지역 경계에서 **대륙 전체가 들썩인다.**
        return p.BaseHeight + b.Base * BiomeBaseScale
             + Octave(worldX, worldZ, s, p.ContinentPeriod) * p.ContinentAmplitude;
    }

    static float BaseNoise(float worldX, float worldZ)
    {
        var p = Profile;
        var b = BlendedBiome(worldX, worldZ);
        float s = p.Seed * 0.7913f;

        float h = Macro(worldX, worldZ);

        // 언덕은 바이옴이 **크기**를, 잔기복은 **잘기**를 정한다.
        h += Octave(worldX, worldZ, s + 311f, p.HillPeriod)   * p.HillAmplitude   * b.Amplitude;
        h += Octave(worldX, worldZ, s + 977f, p.DetailPeriod) * p.DetailAmplitude * b.Roughness;
        h += Terrace(worldX, worldZ);
        return h;
    }

    // ── 계단 — 평평한 패치가 층층이 ─────────────────────────────────────────
    //
    // **2026-09-24, 사용자:** *"바닥에 높낮이를 추가하면 좋겠어 너무 평면이니깐 ... 근데
    // 이어지는 높낮이가 아니라 마크처럼 / 한칸씩 말고 0.1칸씩?"*
    //
    // 위의 두 층과 **다른 축이다.** 언덕·잔기복은 연속이라 *어디까지가 한 평면인가*를 못
    // 정하는데, 여기서 지키는 것은 **한 패치가 통째로 한 높이**라는 사실이다.
    //
    // **셀 한복판으로 노이즈를 묻는 것이 요점이다** — 좌표를 그대로 묻고 값만 양자화하면
    // 그건 *연속 기복을 끊은 것*이라, 패치 안에서도 미세하게 기울고 층 경계가 노이즈
    // 등고선을 따라 구불거린다(격자가 아니라 **지도의 등고선**이 된다). 셀로 접어서 물으면
    // 셀 안의 모든 좌표가 같은 값을 받으므로 패치가 **구조적으로** 평평하다.
    //
    // **셀은 정점 간격의 배수로 접는다** — 아니면 셀 경계가 정점 사이에 떨어져 한 패치가
    // 두 높이에 걸치고, 그러면 평평하게 만든 이유가 사라진다.
    //
    // ── 수직 계단은 구조적으로 안 된다 ─────────────────────────────────────
    //
    // 유니티 `Terrain`은 정점 격자를 **선형 보간**해서 그린다 — 어떤 단이든 전이가 최소
    // 한 정점 간격, 즉 **2m 폭 경사**로 나온다(정점을 1m로 내려도 1m 경사다. 데이터는 네
    // 배가 된다). 진짜 수직면은 바닥을 메시로 그려야 나오고 — `FloorRenderer`·`RockRenderer`가
    // 정확히 그 모양이다 — 그건 스플랫·콜라이더·스트리밍을 통째로 옮기는 일이다.
    //
    // **그래서 단이 곧 보이는 각도다** — `atan(단 / 2m)`. 처음 0.1m로 넣었는데 **2.9°는
    // 화면에서 평면과 구별되지 않았다**(층은 여섯 개 실제로 났다 — `0b. Probe Floor
    // Terraces`가 그걸 갈랐다: *안 났다*와 *났는데 안 읽힌다*는 화면에서 같은 말로 들린다).
    // 사용자가 고른 값은 **1.0m = 26.6°**다.
    //
    // **1.0m이 수직 턱을 만드는 것은 아니다** — *맞닿은 셀이 한 단 차일 때는*(09-26부터 봉투가
    // 그걸 지킨다 — 아래 `TerraceLevel`) 전이가 2m에 걸치므로 칸당 상승이 0.5m이고
    // `PathGrid`가 자연 지형에 허용하는 `SlopeRise`(1.0m/칸) 안이다. `PawnMover.StepUp`은
    // **바닥이 만든 수직면**에 쓰는 값이라 여기 안 걸린다. 걸리는 것은 하나뿐이다 —
    // **0.8m을 넘으면 1×1 건물이 단 경계에 못 놓인다**(`MapManager.Allowed`, 기단이 그 답).
    //
    // ── 바이옴을 안 탄다 ───────────────────────────────────────────────────
    //
    // 언덕·잔기복은 바이옴 진폭을 곱하는데 **여기에 곱하면 양자화가 깨진다** — 바이옴은
    // 이웃 넷을 이중선형으로 섞어 **연속**이라, 곱하면 단 높이가 셀마다 조금씩 달라져
    // *같은 층이 이어지지 않는다.* 계단은 세계가 한 벌을 쓴다.
    static float Terrace(float worldX, float worldZ)
    {
        var p = Profile;
        if (p.TerraceStep <= 0.0001f || p.TerraceLevels <= 0) return 0f;

        float cell = TerraceCellSize(p);
        int ix = Mathf.FloorToInt(worldX / cell);
        int iz = Mathf.FloorToInt(worldZ / cell);
        return TerraceLevel(ix, iz) * p.TerraceStep;
    }

    // **정점 간격의 배수로 접는다.** 0이나 음수가 들어와도 정점 간격 하나로 떨어진다.
    public static float TerraceCellSize(TerrainProfile p) =>
        Mathf.Max(1, Mathf.RoundToInt(p.TerraceCell / TerrainGrid.VertexSpacing)) * TerrainGrid.VertexSpacing;

    // ── 어디든 걸어갈 수 있게 — 맞닿은 셀은 한 단까지만 ─────────────────────
    //
    // **2026-09-26, 사용자:** *"땅이 한칸씩 해서 무조건 이동할 수 있게"*
    //
    // 셀마다 노이즈를 따로 반올림하면 **맞닿은 두 셀이 두 단 이상** 벌어질 수 있다 — 전이가
    // 정점 한 간격(2m)에 걸치므로 2m 넘게 오르면 칸당 상승이 `SlopeRise`(1m)를 넘어 **아무도
    // 못 넘는 턱**이 된다. 층이 가두면 길찾기가 *"거기는 못 간다"*를 정직하게 답하므로
    // 화면에는 *폰이 멈춘다*로만 보인다(절벽 다리를 건넌 뒤 못 움직이던 것의 한 원인).
    //
    // **답은 1-립시츠 봉투 둘의 가운데다.** 날것 층 r에 대해
    //   · 아래 봉투  lo(c) = min_c' ( r(c') + d(c, c') )
    //   · 위 봉투    hi(c) = max_c' ( r(c') − d(c, c') )     (d = 체비쇼프 셀 거리)
    // 는 둘 다 이웃끼리 한 단 넘게 안 다르고(대각선 이웃도 — 그래서 체비쇼프다) lo ≤ r ≤ hi다.
    // **둘의 평균을 내림하면 여전히 한 단 이내**다(|x−y| ≤ 1이면 ⌊x⌋와 ⌊y⌋도 한 단 이내 —
    // 반올림은 0.5 → 0 · 1.5 → 2가 되어 **안 된다**). 한쪽 봉투만 쓰면 고원이 층수만큼 셀씩
    // 깎이거나 골짜기가 메워진다 — 가운데를 쓰면 **벼랑이 제자리에서 비탈이 된다.**
    //
    // **창은 2 × 층수면 정확하다** — 그보다 먼 셀은 r(c') + d > −L + 2L = L ≥ r(c)라 아래
    // 봉투를 못 이긴다(위도 대칭). 그래서 **결정적이고 지역적이다**: 청크가 어느 순서로 서도
    // 같은 셀은 같은 층을 받는다(이음매가 안 생긴다).
    //
    // **한 셀은 한 번만 센다** — 정점 캐시(`VertexHeightGlobal`)는 정점마다 한 번이지만 한
    // 셀에 정점이 아홉이라, 캐시가 없으면 같은 봉투를 아홉 번 잰다.
    // 진단(`Terrain → 0b. Probe Floor Terraces`)이 **판정과 같은 함수로** 재도록 연다.
    public static int TerraceCellLevel(int ix, int iz)
    {
        var p = Profile;
        if (p.TerraceStep <= 0.0001f || p.TerraceLevels <= 0) return 0;
        return TerraceLevel(ix, iz);
    }

    static int TerraceLevel(int ix, int iz)
    {
        var p = Profile;
        EnsureTerraceCache(p);

        int L = p.TerraceLevels;
        int gx = ix - _terOrigin, gz = iz - _terOrigin;
        bool inside = (uint)gx < (uint)_terSize && (uint)gz < (uint)_terSize;
        if (inside && _terFinal[gz * _terSize + gx] != short.MinValue)
            return _terFinal[gz * _terSize + gx];

        int self = RawTerraceLevel(ix, iz);

        // **고리마다 넓히다 더 못 이기는 거리에서 멈춘다** — r ≥ −L이므로 거리 k의 고리는
        // 아래 봉투를 k − L보다 낮출 수 없다(위도 대칭). 평지 한복판이면 몇 고리에서 끝난다.
        int lo = self, hi = self;
        for (int k = 1; k <= 2 * L; k++)
        {
            bool loDone = k - L >= lo, hiDone = L - k <= hi;
            if (loDone && hiDone) break;

            for (int d = -k; d <= k; d++)
            {
                RingPair(ix, iz, d, k, ref lo, ref hi);
            }
            for (int d = -k + 1; d <= k - 1; d++)
            {
                RingSide(ix, iz, d, k, ref lo, ref hi);
            }
        }

        int level = FloorDiv(lo + hi, 2);
        if (inside) _terFinal[gz * _terSize + gx] = (short)level;
        return level;
    }

    // 고리의 위·아래 변(z = ±k)
    static void RingPair(int ix, int iz, int d, int k, ref int lo, ref int hi)
    {
        Touch(RawTerraceLevel(ix + d, iz - k), k, ref lo, ref hi);
        Touch(RawTerraceLevel(ix + d, iz + k), k, ref lo, ref hi);
    }

    // 고리의 왼·오른 변(x = ±k) — 모서리는 위에서 셌다
    static void RingSide(int ix, int iz, int d, int k, ref int lo, ref int hi)
    {
        Touch(RawTerraceLevel(ix - k, iz + d), k, ref lo, ref hi);
        Touch(RawTerraceLevel(ix + k, iz + d), k, ref lo, ref hi);
    }

    static void Touch(int r, int k, ref int lo, ref int hi)
    {
        if (r + k < lo) lo = r + k;
        if (r - k > hi) hi = r - k;
    }

    static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    // **날것 층 — 봉투를 씌우기 전의 셀 값.** 봉투가 이웃을 몇 겹씩 다시 읽으므로 이것도 담는다.
    static int RawTerraceLevel(int ix, int iz)
    {
        int gx = ix - _terOrigin, gz = iz - _terOrigin;
        bool inside = (uint)gx < (uint)_terSize && (uint)gz < (uint)_terSize;
        if (inside && _terRaw[gz * _terSize + gx] != short.MinValue)
            return _terRaw[gz * _terSize + gx];

        var p = Profile;
        float cell = TerraceCellSize(p);
        float cx = (ix + 0.5f) * cell;
        float cz = (iz + 0.5f) * cell;

        float n = Octave(cx, cz, p.Seed * 0.7913f + 2221f, p.TerracePeriod);   // −1 ~ +1

        // **펴 주지 않으면 층수가 거짓말을 한다**(2026-09-24). 펄린은 값이 0 근처에 몰려
        // 이론상의 ±1까지 거의 안 간다 — ±3으로 적어 두고 재 보니 실제로 난 것은 **−3~+2**
        // 였고 그 양 끝이 각각 5%였다(0층 혼자 34%). 그 상태로 층수를 올리면 **올린 만큼
        // 안 올라간다**: 상한만 멀어지고 도달은 더 드물어져, *"최대 높이가 낮다"*는 인상이
        // 층수를 만질수록 굳는다. 통로 폭을 미터로 저작하려고 능선 거리를 거친 것과 같은
        // 자리다 — **적은 값이 실제로 걸리지 않으면 저작이 짐작이 된다.**
        //
        // **클램프가 덤을 준다** — 펴다 넘친 자리가 최대 층에 딱 붙어 **넓은 고원**이 된다
        // (마크의 메사가 그 모양이다). 그래서 자르는 편이 실효 범위로 정확히 나누는 것보다
        // 화면에서 낫다.
        n = Mathf.Clamp(n / TerraceSpread, -1f, 1f);
        int level = Mathf.RoundToInt(n * p.TerraceLevels);
        if (inside) _terRaw[gz * _terSize + gx] = (short)level;
        return level;
    }

    // ── 셀 캐시 ─────────────────────────────────────────────────────────────
    //
    // 세계 한 변 + 봉투 창만큼 여유를 둔 정사각 격자. **값을 만든 손잡이를 같이 적어 두고**
    // 달라지면 통째로 버린다 — 캐시를 비우는 문을 따로 두면 손잡이를 돌리는 자리
    // (`TerrainStreamer`·`TerrainAuthoring`)가 그 문을 잊는 날 옛 계단이 남는다.
    // 밖(세계에서 창 너머)은 안 담고 그때그때 센다 — 세계 밖은 바위라 거의 안 묻는다.
    static short[] _terRaw, _terFinal;
    static int _terOrigin, _terSize;
    static int _terSeed = int.MinValue, _terLevels, _terCells;
    static float _terPeriod;

    static void EnsureTerraceCache(TerrainProfile p)
    {
        int cells = Mathf.RoundToInt(TerraceCellSize(p) / TerrainGrid.VertexSpacing);
        if (_terRaw != null && _terSeed == p.Seed && _terLevels == p.TerraceLevels &&
            _terCells == cells && _terPeriod == p.TerracePeriod) return;

        _terSeed = p.Seed; _terLevels = p.TerraceLevels; _terCells = cells; _terPeriod = p.TerracePeriod;

        // 층은 short에 담는다 — short.MinValue가 *"아직 안 셌다"*다.
        int L = p.TerraceLevels;
        int worldCells = Mathf.CeilToInt(TerrainGrid.WorldSize / TerraceCellSize(p));
        _terOrigin = -2 * L;
        _terSize   = worldCells + 4 * L;
        _terRaw    = new short[_terSize * _terSize];
        _terFinal  = new short[_terSize * _terSize];
        System.Array.Fill(_terRaw,   short.MinValue);
        System.Array.Fill(_terFinal, short.MinValue);
    }

    // 펄린이 실제로 오가는 폭. 손잡이로 안 낸다 — **층수가 곧 상한**이라는 약속을 지키는
    // 값이라, 밖에서 돌릴 수 있게 두면 그 약속이 다시 깨진다.
    const float TerraceSpread = 0.75f;

    // **지도가 쓰는 높이 — 잔기복을 뺀 것**(2026-08-26).
    //
    // 지도는 세계 16,384m를 256픽셀로 그리므로 **한 픽셀이 64m**다. 그런데 잔기복의 주기가
    // 80m라 **표본 간격이 주기와 비슷해져** 모아레가 생긴다 — 화면에 자잘한 얼룩이 낀다.
    // 표본을 늘려 평균 내는 길도 있지만, 애초에 **지도에 2m짜리 요철은 그릴 것이 아니다.**
    public static float MapHeight(float worldX, float worldZ)
    {
        var p = Profile;
        var b = BlendedBiome(worldX, worldZ);
        float s = p.Seed * 0.7913f;

        float h = Macro(worldX, worldZ);
        h += Octave(worldX, worldZ, s + 311f, p.HillPeriod) * p.HillAmplitude * b.Amplitude;

        var f = Features;
        for (int i = 0; i < f.Length; i++) h += f[i].Contribution(worldX, worldZ);
        return h;
    }

    // 지도가 쓰는 색 — **바이옴이 정하고 높이가 명암을 준다.**
    //
    // 높이만으로 칠하면 저작해 둔 지도가 화면에 안 나타난다(사막도 숲도 같은 초록이 된다).
    // 지도를 보는 이유의 절반이 *"저쪽은 어떤 곳인가"*이므로 **바이옴이 색을 든다** —
    // 그 표는 이미 하나뿐이다(`Biome.Color`, 저작 창의 격자가 쓰는 그것).
    public static Color MapColor(float worldX, float worldZ)
    {
        var uv = TerrainGrid.BiomeUV(worldX, worldZ);   // **굽힌 자리**로 묻는다
        float fx = uv.x - 0.5f, fy = uv.y - 0.5f;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;

        Color a = BiomeAt(x0,     y0    ).Color();
        Color b = BiomeAt(x0 + 1, y0    ).Color();
        Color c = BiomeAt(x0,     y0 + 1).Color();
        Color d = BiomeAt(x0 + 1, y0 + 1).Color();
        Color col = Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);

        // 명암 — 높은 데는 밝고 낮은 데는 어둡다. 색을 덮지 않을 만큼만(0.7~1.25).
        float h = MapHeight(worldX, worldZ);
        float t = Mathf.InverseLerp(-40f, 200f, h);
        return col * Mathf.Lerp(0.7f, 1.25f, t);
    }

    // 바이옴의 기준 표고 한 칸이 몇 m인가. 늪 −0.8 → −20m, 동굴 지역 +1.0 → +25m.
    const float BiomeBaseScale = 25f;

    // −1 ~ +1. **펄린이 0~1을 주므로 가운데를 0으로 옮긴다** — 안 옮기면 층을 더할수록
    // 지형이 통째로 위로 밀려 `BaseHeight`가 뜻을 잃는다.
    //
    // **`TerrainCeiling`도 이걸 쓴다**(2026-08-29) — 사본을 만들면 가운데를 옮기는 저 규칙이
    // 두 벌이 되고, 한쪽만 고치는 날 **천장만 통째로 위로 밀린다.**
    public static float Octave(float x, float z, float offset, float period)
    {
        if (period <= 0.01f) return 0f;
        float u = (x + offset) / period;
        float v = (z + offset) / period;
        return Mathf.PerlinNoise(u, v) * 2f - 1f;
    }

    // ── 델타 ────────────────────────────────────────────────────────────────
    //
    // **청크 단위로 게으르게 읽는다.** 손댄 청크만 에셋이 있으므로 `Resources.LoadAll`로
    // 통째로 읽으면 없는 것까지 훑는다 — 물어볼 때 하나씩 읽고 **없다는 사실도 기억한다**
    // (안 그러면 안 건드린 청크를 매 정점마다 다시 찾는다).
    static readonly Dictionary<Vector2Int, Dictionary<int, float>> _delta = new();

    public const string DeltaPath = "Terrain";   // Resources/Terrain/{x}_{y}.asset

    static Dictionary<int, float> DeltaOf(Vector2Int chunk)
    {
        if (_delta.TryGetValue(chunk, out var d)) return d;

        var asset = Resources.Load<TerrainDelta>($"{DeltaPath}/{TerrainGrid.KeyOf(chunk)}");
        d = null;
        if (asset != null && asset.Index != null && asset.Height != null)
        {
            int n = Mathf.Min(asset.Index.Length, asset.Height.Length);
            if (n > 0)
            {
                d = new Dictionary<int, float>(n);
                for (int i = 0; i < n; i++) d[asset.Index[i]] = asset.Height[i];
            }
        }
        _delta[chunk] = d;    // **null도 넣는다** — "없음"을 기억하는 것이 요점이다
        return d;
    }

    // 저작 도구가 델타를 다시 구운 뒤 부른다. 안 비우면 **에디터가 옛 높이를 계속 쓴다.**
    public static void ForgetDeltas() { _delta.Clear(); ForgetVertices(); }

    // ── 정점 높이 ───────────────────────────────────────────────────────────

    // **전역 정점 좌표로 묻는다.** 청크 지역 좌표로 물으면 가장자리에서 어느 청크의 값인지가
    // 갈려 이음매가 생긴다 — 주인을 좌표에서 계산해 **답이 하나뿐이게** 만든다.
    // ── 정점 하나는 한 번만 센다 ────────────────────────────────────────────
    //
    // **`VertexHeightGlobal`은 순수 함수다** — 같은 정점은 언제 물어도 같은 값이고, 그 값을
    // 내는 데 펄린 여러 층과 바이옴 이중선형이 든다. 그런데 **묻는 쪽이 정점 단위가 아니다**:
    // `At`은 한 번에 정점 넷을 뽑고, 길찾기 베이크는 1m 칸마다 `At`을 부른다 — 2m 정점
    // 격자에서 **같은 정점이 한 베이크 안에서만도 수십 번 다시 계산된다.**
    //
    // 세계는 한 변 513정점(16청크 × 32 + 1)이라 **통째로 담아도 1MB**다. 게으르게 채우고
    // 저작이 바뀌면 통째로 버린다(`ForgetDeltas`·`ForgetBiomes`·`Heightmap` 설정).
    //
    // **밖은 안 담는다** — 세계 밖 좌표는 청크가 클램프돼 답이 겹치므로 색인이 성립하지 않는다.
    const int GlobalVerts = TerrainGrid.ChunksPerAxis * OwnedPerChunk + 1;

    static float[] _vertex;
    static bool[]  _vertexKnown;

    // **버린 횟수를 밖에서도 볼 수 있어야 한다** — `TerrainCeiling.Version`과 같은 장치이고
    // 같은 이유다: 여기 값이 바뀐 것을 **이미 구워 둔 메시**가 알 방법이 없다. 그래서
    // 저작 화면(`WorldSceneTerrain`)이 지형을 세워 둔 채 프로필을 돌리면 **정점 캐시만
    // 비워지고 화면은 그대로**여서, 손잡이를 돌려도 아무 일이 안 일어난 것으로 보인다.
    // **되부르지 않고 번호를 보게 한다** — 의존이 한 방향으로만 흐른다.
    public static int Version { get; private set; }

    public static void ForgetVertices()
    {
        _vertex      = null;
        _vertexKnown = null;
        Version++;
    }

    public static float VertexHeightGlobal(int gx, int gz)
    {
        if ((uint)gx >= GlobalVerts || (uint)gz >= GlobalVerts) return ComputeVertexHeight(gx, gz);

        if (_vertexKnown == null)
        {
            _vertex      = new float[GlobalVerts * GlobalVerts];
            _vertexKnown = new bool[GlobalVerts * GlobalVerts];
        }

        int i = gz * GlobalVerts + gx;
        if (_vertexKnown[i]) return _vertex[i];

        float h = ComputeVertexHeight(gx, gz);
        _vertex[i]      = h;
        _vertexKnown[i] = true;
        return h;
    }

    // ── 런타임에 고른 땅 ────────────────────────────────────────────────────
    //
    // **`RockMap._mined`와 같은 자리다**(2026-09-24). 높이의 원본은 *함수 + 저작 델타*인데
    // 델타는 `Resources` 에셋이라 **런타임에 쓸 수 없다** — 그래서 판이 열린 뒤에 바뀌는
    // 높이는 별 층이 든다(캔 칸이 그러는 것과 같다).
    //
    // **여기가 델타보다 먼저다** — 방금 고른 땅이 굽힌 값에 지면 *"팠는데 그대로다"*가 된다.
    //
    // **`ForgetVertices`가 이걸 안 비운다** — 캐시는 함수를 다시 셀 수 있지만 이건 **되살릴
    // 근거가 없는 상태**다(캔 칸을 잊으면 벽이 도로 서는 것과 같다).
    static readonly Dictionary<int, float> _leveled = new();

    public static int LeveledCount => _leveled.Count;

    // **높이가 런타임에 바뀌었다** — 그 칸 둘레. `RockMap.OnChanged`와 같은 규약이고,
    // 듣는 쪽이 제 것만 다시 굽는다(`RockRenderer`가 벽 밑면을 굽는다: 벽이 바닥 위에 서므로
    // 안 들으면 **벽 밑이 뚫려 보인다**).
    //
    // **지형 메시는 이 이벤트로 안 알린다** — 다시 채울 청크를 알려면 *어느 정점이 바뀌었나*를
    // 알아야 하고 그건 고치는 쪽만 안다(`GroundLevel.Refresh`가 직접 `Refill`을 부른다).
    // 칸 하나로 청크를 짐작하면 **청크 경계의 정점**에서 한쪽만 다시 굽혀 이음매가 갈린다.
    public static event System.Action<Vector2Int> OnHeightChanged;

    public static void NotifyHeightChanged(Vector2Int tile) => OnHeightChanged?.Invoke(tile);

    // 고른 값이 있으면 그것. 없으면 `null` — **판정이 아니라 조회용**이다(0을 돌려주면
    // 바닥이 0인 세계에서 *"안 골랐다"*와 *"0으로 골랐다"*가 구별되지 않는다).
    public static bool TryLeveled(int gx, int gz, out float y)
    {
        y = 0f;
        if ((uint)gx >= GlobalVerts || (uint)gz >= GlobalVerts) return false;
        return _leveled.TryGetValue(gz * GlobalVerts + gx, out y);
    }

    // 정점 하나를 그 높이로 못 박는다. **바뀌었으면 참** — 부르는 쪽이 다시 굽는 일을
    // 그 답으로 가른다(`RockMap.Mark`와 같은 규약).
    //
    // **캐시도 같이 고친다.** 안 고치면 `VertexHeightGlobal`이 이미 담아 둔 옛 값을 계속
    // 돌려줘, 사전에는 새 높이가 있는데 **화면과 판정은 옛 높이**로 남는다.
    public static bool SetLeveled(int gx, int gz, float y)
    {
        if ((uint)gx >= GlobalVerts || (uint)gz >= GlobalVerts) return false;

        int i = gz * GlobalVerts + gx;
        if (_leveled.TryGetValue(i, out float cur) && Mathf.Abs(cur - y) < 0.001f) return false;

        _leveled[i] = y;
        if (_vertexKnown != null)
        {
            _vertex[i]      = y;
            _vertexKnown[i] = true;
        }
        return true;
    }

    // **판을 새로 열 때 비운다** — static이라 플레이를 나갔다 들어와도 살아남는다.
    public static void ForgetLeveled() => _leveled.Clear();

    static float ComputeVertexHeight(int gx, int gz)
    {
        if ((uint)gx < GlobalVerts && (uint)gz < GlobalVerts
            && _leveled.TryGetValue(gz * GlobalVerts + gx, out float lv)) return lv;

        var chunk = new Vector2Int(
            Mathf.Clamp(gx / OwnedPerChunk, 0, TerrainGrid.ChunksPerAxis - 1),
            Mathf.Clamp(gz / OwnedPerChunk, 0, TerrainGrid.ChunksPerAxis - 1));

        var d = DeltaOf(chunk);
        if (d != null)
        {
            int lx = gx - chunk.x * OwnedPerChunk;
            int lz = gz - chunk.y * OwnedPerChunk;
            if (lx >= 0 && lz >= 0 && lx < TerrainGrid.VertsPerAxis && lz < TerrainGrid.VertsPerAxis
                && d.TryGetValue(lz * TerrainGrid.VertsPerAxis + lx, out float h))
                return h;
        }

        return Procedural(gx * TerrainGrid.VertexSpacing, gz * TerrainGrid.VertexSpacing);
    }

    // 청크 하나를 그릴 때 쓰는 형태 — 지역 인덱스를 전역으로 옮겨 위를 부른다.
    public static float VertexHeight(Vector2Int chunk, int vx, int vz) =>
        VertexHeightGlobal(chunk.x * OwnedPerChunk + vx, chunk.y * OwnedPerChunk + vz);

    // ── 임의 좌표의 높이 ────────────────────────────────────────────────────

    // **이중선형** — 정점 넷 사이를 섞는다. 메시가 사각형을 두 삼각형으로 그리므로 엄밀히는
    // 삼각형 보간이 맞지만, 그 차이는 2m 격자에서 몇 cm이고 **`IsFlatEnough`의 허용치(0.5m)
    // 안**이다. 대신 식이 대각선 방향에 안 기대므로 **어디서 물어도 같은 값**이 나온다.
    public static float At(float worldX, float worldZ)
    {
        float fx = worldX / TerrainGrid.VertexSpacing;
        float fz = worldZ / TerrainGrid.VertexSpacing;

        int gx = Mathf.FloorToInt(fx);
        int gz = Mathf.FloorToInt(fz);
        float tx = fx - gx;
        float tz = fz - gz;

        float h00 = VertexHeightGlobal(gx,     gz);
        float h10 = VertexHeightGlobal(gx + 1, gz);
        float h01 = VertexHeightGlobal(gx,     gz + 1);
        float h11 = VertexHeightGlobal(gx + 1, gz + 1);

        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
    }

    public static float At(Vector3 worldPos) => At(worldPos.x, worldPos.z);
}
