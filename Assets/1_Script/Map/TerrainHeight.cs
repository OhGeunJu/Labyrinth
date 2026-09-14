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
    // 변하고 경계는 안 보인다 — `WorldMapTerrain`이 월드맵 규모에서 쓰던 그 기법이다.
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
        return h;
    }

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

    public static void ForgetVertices()
    {
        _vertex      = null;
        _vertexKnown = null;
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

    static float ComputeVertexHeight(int gx, int gz)
    {
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
