using System.Collections.Generic;
using UnityEngine;

// **세계 어디든 천장을 답한다 — 로드 여부와 무관하게**(2026-08-29).
//
// `TerrainHeight`의 짝이고 **모양이 똑같다**: 노이즈 + 저작한 특징 + 손댄 델타. 같은 모양인
// 것이 요점이다 — 저작 도구도 굽기도 스트리밍도 그쪽 것을 그대로 옮겨 쓴다. 지하로
// 내려가면서 새로 생기는 개념은 **천장 하나뿐**이고, 그 천장조차 이미 있는 물건의 사본이다.
//
// ── 담는 값은 "여유"다, 절대 높이가 아니다 ─────────────────────────────────
//
// 근거는 `TerrainCeilingProfile`에 적혀 있다. 요약하면 **값 하나가 곧 "지나갈 수 있나"**이고,
// **바닥을 다시 구워도 안 어긋난다.** 그래서 델타에 저장되는 것도 여유다.
//
//     천장 높이 = TerrainHeight.At(x,z) + 여유(x,z)
//
// ── 바위와 천장은 같은 것이다 ──────────────────────────────────────────────
//
// 여유가 0 아래로 내려간 자리는 천장 면이 지면 아래로 내려간다는 뜻이고, 그림으로는
// **바위가 땅을 뚫고 올라온 것**으로 보인다. 벽을 따로 세우지 않는다 — 벽은 천장이 내려앉은
// 자리일 뿐이다. 이것이 동굴 생성의 전부다.
public static class TerrainCeiling
{
    // **켜져 있나.** 씬의 `TerrainStreamer`가 넣어 준다. 꺼져 있으면 `HasHeadroom`이 언제나
    // 참이라 **지상 세계의 판정이 한 줄도 안 바뀐다** — 지하가 아직 저작 안 된 씬에서
    // 폰이 이유 없이 갇히지 않게 하는 장치다.
    public static bool Enabled;

    public static TerrainCeilingProfile Profile = TerrainCeilingProfile.Default;

    // **저작한 천장 특징** — 큰 방(+)과 기둥(−). `TerrainFeature`를 **그대로 재사용한다**:
    // 좌표와 반경과 세기로 값을 얹는 물건이라 축이 높이든 여유든 하는 일이 같다. 같은
    // 모양을 하나 더 만들면 저작 도구도 두 벌이 된다.
    public static TerrainFeature[] Features = System.Array.Empty<TerrainFeature>();

    // `TerrainHeight.OwnedPerChunk`와 **같은 값을 써야 한다** — 가장자리 정점의 주인을
    // 정하는 규칙이 갈리면 바닥과 천장의 이음매가 서로 다른 자리에서 생긴다.
    public const int OwnedPerChunk = TerrainGrid.VertsPerAxis - 1;

    // ── 여유의 노이즈 ───────────────────────────────────────────────────────

    // **저장 안 되는 것 전부** — 노이즈 + 저작한 특징. 굽는 도구가 이 값과 견줘 "손댄 것"을
    // 가려내므로, 특징이 여기 안 들어가면 **저작한 방 전체가 델타로 구워진다.**
    // (`TerrainHeight.Procedural`이 같은 이유로 특징을 포함한다.)
    //
    // **모양은 하나다: `Lerp(바위, 굴, 열림)`.** 열림이 0이면 통짜 바위, 1이면 지나갈 수
    // 있는 굴이고 그 사이가 벽면이다. 근거는 `TerrainCeilingProfile`에 적혀 있다 —
    // 요약하면 **더하면 점이 되고 능선으로 잡아야 선이 된다.**
    public static float ProceduralClearance(float worldX, float worldZ)
    {
        var p = Effective;
        float s = p.Seed * 0.6151f;   // 시드를 좌표 오프셋으로 — 유니티 펄린에 시드 인자가 없다

        // **간선 · 잔가지 · 방 중 가장 열린 것이 이긴다.** 더하면 셋이 겹치는 자리만
        // 문턱을 넘어 길이 토막 난다.
        float open = Band(RidgeDistance(worldX, worldZ, s,         p.PassagePeriod), p.PassageWidth);
        open = Mathf.Max(open, Band(RidgeDistance(worldX, worldZ, s + 4409f, p.BranchPeriod), p.BranchWidth));
        open = Mathf.Max(open, Chamber(worldX, worldZ, s + 8677f, p.ChamberPeriod, p.ChamberRarity));

        float c = Mathf.Lerp(p.BaseClearance, p.OpenClearance, open);

        // **요철은 뚫린 데서만.** 벽에까지 얹으면 문턱 바로 옆이 흔들려 톱니 벽이 된다.
        c += TerrainHeight.Octave(worldX, worldZ, s + 1301f, p.RoughPeriod) * p.RoughAmplitude * open;

        var f = Features;
        for (int i = 0; i < f.Length; i++) c += f[i].Contribution(worldX, worldZ);
        return c;
    }

    // ── 능선 ────────────────────────────────────────────────────────────────

    // **노이즈의 등고선(값이 0인 자리)까지 몇 m인가.**
    //
    // `|n| / |∇n|` — 값을 기울기로 나누면 그 등고선까지의 거리가 나온다(1차 근사). 이걸
    // 거치는 이유는 하나다: **통로 폭을 미터로 저작하기 위해서다.** 그냥 `|n| < 문턱`으로
    // 자르면 같은 문턱이 주기 40m에서는 실핏줄이고 200m에서는 광장이라, 주기를 만질 때마다
    // 폭을 다시 찾아야 한다 — 저작이 짐작이 된다.
    //
    // 봉우리·골짜기(기울기 0)에서는 `|n|`이 크므로 거리가 크게 나온다 = 닫힌다. 맞는 답이다.
    const float RidgeStep = 2f;   // 기울기를 재는 간격(m) — 청크 정점 간격과 같은 눈금

    static float RidgeDistance(float x, float z, float offset, float period)
    {
        if (period <= 0.01f) return float.MaxValue;

        float n  = TerrainHeight.Octave(x, z, offset, period);
        float gx = (TerrainHeight.Octave(x + RidgeStep, z, offset, period) - n) / RidgeStep;
        float gz = (TerrainHeight.Octave(x, z + RidgeStep, offset, period) - n) / RidgeStep;

        float g = Mathf.Sqrt(gx * gx + gz * gz);
        return g < 1e-6f ? float.MaxValue : Mathf.Abs(n) / g;
    }

    // 능선에서 `dist`만큼 떨어진 자리가 얼마나 열려 있나(0~1).
    //
    // **벽은 두께가 있어야 한다** — 딱 자르면 여유가 한 정점 만에 −11에서 +6으로 뛰어
    // 천장 면이 수직으로 선다(그림에서 종잇장처럼 보인다). `WallFade`만큼 걸쳐서 내린다.
    const float WallFade = 1.5f;

    static float Band(float dist, float width)
    {
        if (width <= 0.01f) return 0f;
        return Mathf.Clamp01((width * 0.5f + WallFade * 0.5f - dist) / WallFade);
    }

    // 방 — 큰 주기 노이즈의 **꼭대기**만 연다. 통로와 달리 덩어리라 통로에 매달린 광장이 된다.
    static float Chamber(float x, float z, float offset, float period, float rarity)
    {
        if (period <= 0.01f) return 0f;

        float v = TerrainHeight.Octave(x, z, offset, period) * 0.5f + 0.5f;   // 0~1
        return Mathf.Clamp01((v - rarity) / 0.06f);   // 문턱을 넘는 만큼만 — 가장자리가 벽이 된다
    }

    // ── 씬이 값을 안 들고 있으면 ────────────────────────────────────────────
    //
    // **새 필드는 씬에서 0이다.** 프로필은 `TerrainStreamer`에 직렬화돼 있어서, 필드를
    // 늘린 날 씬은 **옛 값 + 0**을 든 채 굳는다 — 그대로 두면 통로 폭이 0이라 **세계가
    // 통짜 바위**가 되고, 화면에는 *"파도 파도 굴이 없다"*로만 보인다. `EventManager`의
    // 주기와 천장 여유가 먼저 겪은 그 함정이다.
    //
    // **그래서 안 써진 프로필은 기본값으로 대신하고 한 번 알린다.** 값을 씬에 쓰는 문은
    // `0. Setup World Streaming`이다.
    //
    // **저작 도구도 이걸 봐야 한다** — 도구가 씬 값을 그대로 읽으면 폴백이 걸린 씬에서
    // *"도구가 판 방은 코드 기본값 기준, 게임이 그리는 굴은 폴백 기준"*으로 갈린다.
    static bool _warnedUnwritten;

    public static TerrainCeilingProfile Effective
    {
        get
        {
            var p = Profile;
            if (p.OpenClearance > 0.01f || p.PassageWidth > 0.01f || p.BranchWidth > 0.01f)
                return p;

            if (!_warnedUnwritten)
            {
                _warnedUnwritten = true;
                Debug.LogWarning("[천장] 씬의 천장 프로필에 통로 값이 없다 — **기본값으로 대신한다.** " +
                                 "`DungeonColony/World/0. Setup World Streaming`을 한 번 돌릴 것.");
            }
            return TerrainCeilingProfile.Default;
        }
    }

    // ── 델타 ────────────────────────────────────────────────────────────────
    //
    // `TerrainHeight`와 **같은 규칙**이다: 청크 단위로 게으르게 읽고 **없다는 사실도 기억한다**.
    // 에셋 타입도 `TerrainDelta`를 그대로 쓴다 — 담긴 숫자의 뜻만 다르다(높이 → 여유).
    static readonly Dictionary<Vector2Int, Dictionary<int, float>> _delta = new();

    public const string DeltaPath = "Ceiling";   // Resources/Ceiling/{x}_{y}.asset

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

    public static void ForgetDeltas() { _delta.Clear(); ForgetVertices(); }

    // ── 정점 ────────────────────────────────────────────────────────────────

    // **전역 정점 좌표로 묻는다** — 가장자리에서 어느 청크의 값인지가 갈리면 이음매가 생긴다.
    // ── 정점 하나는 한 번만 센다 ────────────────────────────────────────────
    //
    // **`TerrainHeight`와 같은 장치이고 같은 이유다** — 여유 한 정점을 내는 데 능선 두 벌
    // (각 3층) + 방 + 요철 = **펄린 여덟 층**이 들고, `RockMap.IsRock`이 칸마다 그것을 넷씩
    // 부른다. 길찾기 베이크(40×40)가 한 번 돌면 **5만 번**이다 — 지하로 내려온 뒤 폰이 늘
    // 때마다 프레임이 무너지던 자리가 정확히 여기다.
    //
    // 담는 크기·버리는 때 모두 `TerrainHeight`와 같다(1MB · 저작이 바뀌면 통째로).
    const int GlobalVerts = TerrainGrid.ChunksPerAxis * OwnedPerChunk + 1;

    static float[] _vertex;
    static bool[]  _vertexKnown;

    // **버린 횟수를 밖에서도 볼 수 있어야 한다** — `RockMap`이 이 함수의 답을 칸 단위로 한 겹
    // 더 기억하는데, 여기서 값이 바뀐 것을 그쪽이 알 방법이 없으면 **저작을 고친 뒤에도 바위가
    // 옛 자리에 그대로 선다.** 되부르는 대신 **번호를 보게 한다**(의존이 한 방향으로만 흐른다).
    public static int Version { get; private set; }

    public static void ForgetVertices()
    {
        _vertex      = null;
        _vertexKnown = null;
        Version++;
    }

    public static float VertexClearanceGlobal(int gx, int gz)
    {
        if ((uint)gx >= GlobalVerts || (uint)gz >= GlobalVerts) return ComputeVertexClearance(gx, gz);

        if (_vertexKnown == null)
        {
            _vertex      = new float[GlobalVerts * GlobalVerts];
            _vertexKnown = new bool[GlobalVerts * GlobalVerts];
        }

        int i = gz * GlobalVerts + gx;
        if (_vertexKnown[i]) return _vertex[i];

        float c = ComputeVertexClearance(gx, gz);
        _vertex[i]      = c;
        _vertexKnown[i] = true;
        return c;
    }

    static float ComputeVertexClearance(int gx, int gz)
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
                && d.TryGetValue(lz * TerrainGrid.VertsPerAxis + lx, out float c))
                return c;
        }

        return ProceduralClearance(gx * TerrainGrid.VertexSpacing, gz * TerrainGrid.VertexSpacing);
    }

    public static float VertexClearance(Vector2Int chunk, int vx, int vz) =>
        VertexClearanceGlobal(chunk.x * OwnedPerChunk + vx, chunk.y * OwnedPerChunk + vz);

    // ── 임의 좌표 ───────────────────────────────────────────────────────────

    // 그 자리의 여유(m). **이중선형** — `TerrainHeight.At`과 같은 식이어야 한다. 갈리면
    // 그려진 천장과 길찾기가 재는 천장이 어긋나 **보이는 굴을 못 지나간다.**
    public static float ClearanceAt(float worldX, float worldZ)
    {
        float fx = worldX / TerrainGrid.VertexSpacing;
        float fz = worldZ / TerrainGrid.VertexSpacing;

        int gx = Mathf.FloorToInt(fx);
        int gz = Mathf.FloorToInt(fz);
        float tx = fx - gx;
        float tz = fz - gz;

        float c00 = VertexClearanceGlobal(gx,     gz);
        float c10 = VertexClearanceGlobal(gx + 1, gz);
        float c01 = VertexClearanceGlobal(gx,     gz + 1);
        float c11 = VertexClearanceGlobal(gx + 1, gz + 1);

        return Mathf.Lerp(Mathf.Lerp(c00, c10, tx), Mathf.Lerp(c01, c11, tx), tz);
    }

    public static float ClearanceAt(Vector3 worldPos) => ClearanceAt(worldPos.x, worldPos.z);

    // 천장의 **절대 높이**(m) — 그리는 쪽이 쓴다.
    //
    // **여유가 음수면 바닥 아래로 내려간다**(`SinkDepth`까지). 그래야 바위가 지면을 뚫고
    // 올라온 것처럼 보인다 — 0에서 자르면 천장 면과 지면이 정확히 같은 높이에서 만나
    // **z-파이팅으로 지글거린다.** 판정은 이 값을 안 쓴다(여유를 직접 본다).
    public static float At(float worldX, float worldZ)
    {
        float c = Mathf.Max(ClearanceAt(worldX, worldZ), -Mathf.Max(0f, Profile.SinkDepth));
        return TerrainHeight.At(worldX, worldZ) + c;
    }

    // ── 지나갈 수 있나 ──────────────────────────────────────────────────────

    // **폰이 서 있을 수 있는 머리 위 여유가 있나.**
    //
    // 이것이 없으면 **여유 0.3m짜리 틈으로 경로가 나고 폰이 천장에 머리를 박은 채 벽을
    // 민다** — 기단에서 겪은 것과 똑같은 사고다(길찾기가 높이를 모르면 경로는 태연히
    // 못 가는 곳으로 난다). 그때는 발밑이었고 이번엔 머리 위다.
    //
    // **꺼져 있으면 언제나 참이다** — 지상 씬의 판정을 한 줄도 안 바꾼다.
    public static bool HasHeadroom(float worldX, float worldZ) =>
        !Enabled || ClearanceAt(worldX, worldZ) >= PawnMover.BodyHeight;

    public static bool HasHeadroom(Vector3 worldPos) => HasHeadroom(worldPos.x, worldPos.z);
}
