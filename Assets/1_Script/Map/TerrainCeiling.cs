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
        float c = NaturalClearance(worldX, worldZ);

        var f = Features;
        for (int i = 0; i < f.Length; i++) c += f[i].Contribution(worldX, worldZ);
        return c;
    }

    // **저작한 방을 뺀 굴** — 노이즈만의 답. 특징까지 넣고 재면 **지난번에 판 방 자신을
    // 찾는다** — `RockMap.OpenPathToCave`가 캔 칸을 안 세는 것과 같은 이유다.
    //
    // **열림은 둘이다: 길과 큰 굴**(2026-09-15). 길은 세계를 가로지르는 선이고, 큰 굴은
    // 그 사이사이에 놓인 덩어리다. 둘을 `max`로 합친다(더하면 겹치는 자리만 열린다).
    public static float NaturalClearance(float worldX, float worldZ)
    {
        var p = Effective;
        float s = p.Seed * 0.6151f;   // 시드를 좌표 오프셋으로 — 유니티 펄린에 시드 인자가 없다

        float open = RoadOpen(worldX, worldZ, p, s, out float gap);
        open = Mathf.Max(open, CavernOpen(worldX, worldZ, p, s, gap));

        float c = Mathf.Lerp(p.BaseClearance, p.OpenClearance, open);

        // **요철은 뚫린 데서만.** 벽에까지 얹으면 문턱 바로 옆이 흔들려 톱니 벽이 된다.
        c += TerrainHeight.Octave(worldX, worldZ, s + 1301f, p.RoughPeriod) * p.RoughAmplitude * open;
        return c;
    }

    // **길만의 열림**(0~1). 1이면 큰길 한가운데다 — **이어진 그물에 올라탔나**를 묻는 쪽이
    // 쓴다(시작 방을 잇는 도구). 큰 굴까지 세면 **바위 속에 봉해 둔 굴에 시작 방을 잇는다.**
    public static float RoadOpenness(float worldX, float worldZ)
    {
        var p = Effective;
        return RoadOpen(worldX, worldZ, p, p.Seed * 0.6151f, out _);
    }

    // `gap`은 **가장 가까운 길의 가장자리까지 몇 m인가**(안쪽이면 음수) — 봉한 굴이 길에서
    // 벽 두께만큼 물러서는 데 쓴다.
    static float RoadOpen(float x, float z, in TerrainCeilingProfile p, float s, out float gap)
    {
        // **방은 따로 열지 않고 길을 넓힌다**(2026-09-15). 전에는 방이 간선·잔가지와 나란히
        // `max`의 셋째 항이었는데, 그러면 방은 **길과 무관한 자리에 선 덩어리**다 — 길이 촘촘할
        // 때는 거의 늘 길에 걸려 티가 안 났지만, 길을 성기게 하자 **바위 속에 떠 있는 섬**이
        // 되었다. 방 노이즈가 높은 자리에서 **그 길의 폭이 는다.** 따로 선 덩어리는 아래의
        // 큰 굴이 맡는다 — 그쪽은 **이어질지를 굴마다 정한다.**
        float swell = Swell(x, z, s + 8677f, p.ChamberPeriod, p.ChamberRarity);

        float wP = p.PassageWidth + p.ChamberWiden * swell;
        float wB = p.BranchWidth  + p.ChamberWiden * 0.5f * swell;
        float dP = RidgeDistance(x, z, s,         p.PassagePeriod);
        float dB = RidgeDistance(x, z, s + 4409f, p.BranchPeriod);

        gap = Mathf.Min(wP > 0.01f ? dP - wP * 0.5f : float.MaxValue,
                        wB > 0.01f ? dB - wB * 0.5f : float.MaxValue);

        // **간선 · 잔가지 중 가장 열린 것이 이긴다.**
        return Mathf.Max(Band(dP, wP), Band(dB, wB));
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

    // 방 — 큰 주기 노이즈의 **꼭대기**에서 길이 부푼다(0~1). **완만하게 올린다** — 가파르면
    // 길 위에 **구슬이 꿰인 모양**이 되고(폭이 몇 m 만에 뛴다), 완만하면 강폭처럼 넓어졌다 좁아진다.
    const float SwellRamp = 0.22f;

    static float Swell(float x, float z, float offset, float period, float rarity)
    {
        if (period <= 0.01f) return 0f;

        float v = TerrainHeight.Octave(x, z, offset, period) * 0.5f + 0.5f;   // 0~1
        return Mathf.Clamp01((v - rarity) / SwellRamp);
    }

    // ── 큰 굴 ────────────────────────────────────────────────────────────────
    //
    // **2026-09-15, 사용자:** *"큰 동굴 같은 곳을 부분 부분 만들어 줘. 꼭 길이 이어져 있을
    // 필요는 없어 — 그렇다고 안 이어진 것만 있을 필요도 없고."*
    //
    // **노이즈 꼭대기로 열지 않고 굴마다 하나씩 놓는다.** 처음에 노이즈로 흉내 냈더니 두 가지가
    // 안 됐다: ① *이어졌나*를 굴마다 정할 수가 없다 — 노이즈에는 **"이 굴"이라는 단위가 없어서**
    // 봉할지를 또 다른 노이즈로 가르면 그 경계가 **굴 한복판을 자로 잰 듯 가로질렀다.**
    // ② 봉하지 않으면 큰 굴은 자주 길에 걸린다(굴 지름이 길 간격과 견줄 만하다).
    //
    // 그래서 **세계를 `CavernSpacing` 칸으로 나누고 칸마다 굴 하나를 굴린다**(있나 · 자리 ·
    // 크기 · 기울기 · **봉했나**). 칸 좌표의 해시라 저장할 것이 없고 청크 경계가 안 갈린다
    // (`string.GetHashCode`처럼 실행마다 달라지는 것을 안 쓴다).
    //
    // - **칸마다 반드시 하나, 칸에서 가장 빈 자리에 놓는다**(2026-09-15에 바로잡았다 — 사용자:
    //   *"비어 있는 곳은 너무 비어 있고 붙어 있는 곳은 너무 붙어 있다 · 6개가 근처에 있는 곳이
    //   있다"*). 처음엔 칸마다 확률로 굴리고 자리를 칸 안에서 아무렇게나 골랐는데, 그러면 뭉침이
    //   **두 번 겹친다**: 이웃 칸의 굴이 서로 맞붙은 가장자리로 쏠리면 72m 안에 몰리고, 굴이 안
    //   난 칸이 길 사이 빈터와 겹치면 **바위만 수백 m**다. 그래서 ① **확률 없이 칸마다 하나**
    //   (`CavernChance` 기본 1) ② 후보 여섯 중 **가장 가까운 길에서 가장 먼 자리**를 고른다 —
    //   빈터가 곧 길에서 먼 곳이라 굴이 **빈 곳을 메우고 길이 촘촘한 곳은 피한다.** 칸 한복판
    //   에서 멀수록 조금 깎아 이웃과 한 점으로 모이지 않게 한다.
    // - **칸은 세계에 맞춘다**(`세계 / round(세계 / 간격)`) — 간격을 그대로 쓰면 마지막 칸이
    //   세계 밖에 걸쳐 **가장자리 굴이 반쯤 사라진다.**
    // - **봉한 굴은 길에서 벽 두께만큼 물러선다** — 크기를 한복판에서 길까지의 거리에 맞춰
    //   줄이고, 그래도 가장자리가 흔들려 닿는 자리는 벽으로 깎는다. **안 맞으면 버리지 않고
    //   트인 굴로 둔다**(버리면 길이 촘촘한 곳에서 굴이 통째로 사라진다).
    // - **트인 굴은 가장 가까운 길까지 샛길을 낸다** — 굴을 빈터에 놓게 되면서 트인 굴도 길에서
    //   멀어졌다: 샛길이 없으면 *"이어진 굴"*이 사라지고 전부 봉한 굴이 된다. 샛길은 흔든
    //   선분이다(곧으면 사람이 판 갱도다). 칸의 0.9배 안에 길이 없으면 떨어진 채 둔다.
    // - **모양은 좌표를 흔든 타원이다** — 원을 그대로 두면 사람이 판 방이다. 흔드는 주기를
    //   굴 크기에 매단다(작게 흔들면 가장자리가 구름처럼 보풀인다).
    struct Cavern
    {
        public bool  Exists, Sealed, HasSpur;
        public float X, Z, R, Cos, Sin, Aspect, Warp, SpurX, SpurZ;
    }

    const int CavernCandidates = 6;

    static readonly Dictionary<long, Cavern> _caverns = new();

    static uint Hash(int x, int z, int seed, uint salt)
    {
        unchecked
        {
            uint h = (uint)x * 73856093u ^ (uint)z * 19349663u ^ (uint)seed * 83492791u ^ salt * 2654435761u;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15; h *= 0x27d4eb2du; h ^= h >> 16;
            return h;
        }
    }

    static float Hash01(int x, int z, int seed, uint salt) => Hash(x, z, seed, salt) / 4294967296f;

    // 세계에 맞춘 칸 수 · 칸 크기.
    static int   CavernCells(in TerrainCeilingProfile p) =>
        Mathf.Max(1, Mathf.RoundToInt(TerrainGrid.WorldSize / Mathf.Max(1f, p.CavernSpacing)));
    static float CavernCell(in TerrainCeilingProfile p) => TerrainGrid.WorldSize / (float)CavernCells(p);

    static Cavern CavernOf(int ix, int iz, in TerrainCeilingProfile p, float s)
    {
        long key = ((long)ix << 32) ^ (uint)iz;
        if (_caverns.TryGetValue(key, out var cv)) return cv;

        cv = default;
        int   n    = CavernCells(p);
        float cell = CavernCell(p);
        if ((uint)ix < n && (uint)iz < n && Hash01(ix, iz, p.Seed, 1) < p.CavernChance)
        {
            cv.Exists = true;

            // **후보 중 길에서 가장 먼 자리.** 칸 가장자리로는 안 민다(0.2~0.8) — 이웃 칸만
            // 훑어도 되게 하는 장치이자, 이웃 굴과 맞붙지 않게 하는 최소 간격이다.
            float bestScore = float.MinValue, gap = 0f;
            float mx = (ix + 0.5f) * cell, mz = (iz + 0.5f) * cell;
            for (uint k = 0; k < CavernCandidates; k++)
            {
                float x = (ix + 0.2f + 0.6f * Hash01(ix, iz, p.Seed, 20 + k)) * cell;
                float z = (iz + 0.2f + 0.6f * Hash01(ix, iz, p.Seed, 40 + k)) * cell;
                RoadOpen(x, z, p, s, out float g);
                float score = Mathf.Min(g, cell) - 0.25f * Mathf.Sqrt((x - mx) * (x - mx) + (z - mz) * (z - mz));
                if (score <= bestScore) continue;
                bestScore = score; gap = g; cv.X = x; cv.Z = z;
            }

            cv.R = Mathf.Lerp(p.CavernRadiusMin, p.CavernRadiusMax, Hash01(ix, iz, p.Seed, 4));

            float a = Hash01(ix, iz, p.Seed, 6) * Mathf.PI;
            cv.Cos = Mathf.Cos(a); cv.Sin = Mathf.Sin(a);
            cv.Aspect = 1f + 0.7f * Hash01(ix, iz, p.Seed, 7);
            cv.Warp   = ix * 131.1f + iz * 71.7f;

            if (Hash01(ix, iz, p.Seed, 5) < p.CavernSealed)
            {
                float fit = (gap - p.CavernWall) / 1.2f;   // 흔들림·기울기 몫으로 넉넉히
                if (fit >= p.CavernRadiusMin * 0.6f)
                {
                    cv.Sealed = true;
                    cv.R = Mathf.Min(cv.R, fit);
                }
            }

            // 트인 굴이 길에 안 닿으면 샛길. **길 한가운데를 찾는다**(`RoadOpen` = 1) — 벽면
            // 문턱에 닿으면 샛길 끝이 벽에 막힌다.
            if (!cv.Sealed && p.CavernSpurWidth > 0.01f && gap > cv.R * 0.5f)
                cv.HasSpur = NearestRoad(cv.X, cv.Z, cell * 0.9f, p, s, out cv.SpurX, out cv.SpurZ);
        }
        _caverns[key] = cv;
        return cv;
    }

    static bool NearestRoad(float x, float z, float maxRadius, in TerrainCeilingProfile p, float s,
                            out float rx, out float rz)
    {
        rx = x; rz = z;
        for (float r = 4f; r <= maxRadius; r += 3f)
        {
            int steps = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r / 3f));
            for (int i = 0; i < steps; i++)
            {
                float ang = i / (float)steps * Mathf.PI * 2f;
                float qx = x + Mathf.Cos(ang) * r, qz = z + Mathf.Sin(ang) * r;
                if (RoadOpen(qx, qz, p, s, out _) < 0.99f) continue;
                rx = qx; rz = qz;
                return true;
            }
        }
        return false;
    }

    static float CavernOpen(float x, float z, in TerrainCeilingProfile p, float s, float gap)
    {
        if (p.CavernSpacing < 1f || p.CavernChance <= 0f || p.CavernRadiusMax <= 0.01f) return 0f;

        float cell = CavernCell(p);
        int cx = Mathf.FloorToInt(x / cell);
        int cz = Mathf.FloorToInt(z / cell);
        float best = 0f;

        for (int iz = cz - 1; iz <= cz + 1; iz++)
        for (int ix = cx - 1; ix <= cx + 1; ix++)
        {
            var cv = CavernOf(ix, iz, p, s);
            if (!cv.Exists) continue;

            float ddx = x - cv.X, ddz = z - cv.Z;

            // ── 굴 몸통 ──
            // 멀면 흔들기 전에 버린다 — 흔들림(0.45R) + 늘임(1.7배)을 넉넉히 덮는다.
            float reach = cv.R * 2.3f;
            if (ddx * ddx + ddz * ddz <= reach * reach)
            {
                float wx = x + TerrainHeight.Octave(x, z, s + cv.Warp + 500f, cv.R * 1.3f) * cv.R * 0.45f;
                float wz = z + TerrainHeight.Octave(x, z, s + cv.Warp + 900f, cv.R * 1.3f) * cv.R * 0.45f;
                float dx = wx - cv.X, dz = wz - cv.Z;

                float u = ( dx * cv.Cos + dz * cv.Sin) / cv.Aspect;
                float v = (-dx * cv.Sin + dz * cv.Cos) * Mathf.Sqrt(cv.Aspect);
                float d = Mathf.Sqrt(u * u + v * v);

                float o = Mathf.Clamp01((cv.R - d) / WallFade);
                if (cv.Sealed) o = Mathf.Min(o, Mathf.Clamp01((gap - p.CavernWall) / WallFade));
                if (o > best) best = o;
            }

            // ── 샛길 ── 흔든 선분. 선분 둘레 상자 밖이면 안 잰다.
            if (cv.HasSpur)
            {
                const float SpurWobble = 5f, SpurWobblePeriod = 28f;
                float pad = p.CavernSpurWidth + SpurWobble + WallFade;
                if (x < Mathf.Min(cv.X, cv.SpurX) - pad || x > Mathf.Max(cv.X, cv.SpurX) + pad ||
                    z < Mathf.Min(cv.Z, cv.SpurZ) - pad || z > Mathf.Max(cv.Z, cv.SpurZ) + pad)
                    continue;

                float wx = x + TerrainHeight.Octave(x, z, s + cv.Warp + 300f, SpurWobblePeriod) * SpurWobble;
                float wz = z + TerrainHeight.Octave(x, z, s + cv.Warp + 700f, SpurWobblePeriod) * SpurWobble;

                float ex = cv.SpurX - cv.X, ez = cv.SpurZ - cv.Z;
                float len2 = Mathf.Max(1e-4f, ex * ex + ez * ez);
                float t = Mathf.Clamp01(((wx - cv.X) * ex + (wz - cv.Z) * ez) / len2);
                float qx = wx - (cv.X + ex * t), qz = wz - (cv.Z + ez * t);

                float o = Band(Mathf.Sqrt(qx * qx + qz * qz), p.CavernSpurWidth);
                if (o > best) best = o;
            }
        }
        return best;
    }

    // **몇 개가 났나** — `2. Preview Cave Map`이 한 줄로 말한다. 그림만으로는 *봉한 굴*과
    // *샛길로 이은 굴*이 구별되지 않는다.
    public static void CountCaverns(out int total, out int sealedCount, out int spurCount)
    {
        total = sealedCount = spurCount = 0;
        var p = Effective;
        if (p.CavernSpacing < 1f || p.CavernChance <= 0f) return;

        float s = p.Seed * 0.6151f;
        int n = CavernCells(p);
        for (int iz = 0; iz < n; iz++)
        for (int ix = 0; ix < n; ix++)
        {
            var cv = CavernOf(ix, iz, p, s);
            if (!cv.Exists) continue;
            total++;
            if (cv.Sealed)  sealedCount++;
            if (cv.HasSpur) spurCount++;
        }
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
                                 "`DungeonColony/Terrain/0. Setup World Streaming`을 한 번 돌릴 것.");
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
        _caverns.Clear();   // 굴은 프로필에서 나오므로 같이 버린다
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
