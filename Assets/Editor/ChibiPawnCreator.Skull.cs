using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// **해골 머리 — 거리장(SDF)으로 빚어 표면을 메시로 뽑는다**
// (2026-10-24~27, 사용자: *"스켈레톤은 해골 헤드로 따로 조형했으면 좋겠는데"* → *"옆선 돌려서 깍아 먹는 걸로는 쉽지 않아보이는데"*).
//
// 사람 머리는 옆선을 돌려 깎는다(`Lathe`) — 어느 방향으로 잘라도 단면이 둥글어서 **파인 눈구멍 · 뚫린 볼 · 앞으로 나온 이**를
// 못 만든다. 그래서 덩어리를 더하고 빼는 수식(거리장)으로 빚고 표면을 뽑는다. 모양은 파이썬으로 렌더해 사용자가 판마다 고쳤다
// (v3 얼굴 → v25 구멍 → v28 턱 → v39~41 이). **수치를 고치면 그 그림들과 어긋난다** — 고칠 때는 다시 그려 볼 것.
//
// **고르는 축**(사용자: *"원래 치비들처럼 턱을 선택할 수 있게"*) — 메시를 갈아 끼우는 셋은 `PawnLook`의 `Head.Pick.*`이 고른다:
//   두개골 `Skull`(Whole · Cracked) · 턱 `Jaw`(None · Round(v27) · V(v28)) · 이 `Teeth`(Normal · Vampire · Orc · Monster) × 빠진 이 `Gap`.
//   **턱이 없으면 이도 없다**(위턱 바닥이 평평하게 끝난 판이 사용자가 고른 "턱 없는 해골"이다 — 이 렌더러는 `@Jaw.!None`).
//   뼈 색 · 눈구멍 속(검정 · 뼈색)은 메시가 아니라 칠이다 — `BoneLook`.
//
// ## 어기면 조용히 깨지는 것
//
// - **이 하나하나에 외곽선을 긋지 않는다** — 선은 6cm이고 이는 3cm다. 제 선을 두면 이가 검은 덩어리가 된다. 해골 살은 코처럼
//   제 선을 끄고(`Bone` — `FineOutline`) 바깥 선(`OutlineOuterFine`)이 **얼굴 밖에만** 긋는다. 그래서 선은 실루엣과 뚫린 볼에만 선다.
// - **구멍 속이 검은 것은 칠이 아니라 그늘이다**(사용자: *"검은색 말고 구멍을 뚫어볼래?"*) — 정점 색에 구워 두고(`AoOf`)
//   셰이더가 `_VertexAO`만큼 곱한다. 폰 셰이더는 빛의 방향만 보므로 굽지 않으면 깊은 구멍의 바닥이 밝게 칠해진다.
// - **위·아랫니는 따로 뽑아 한 메시로 붙인다** — 물린 이 사이가 4mm라 한 거리장으로 뽑으면 격자가 둘을 한 덩어리로 잇는다.
// - **두개골 속을 비우지 않는다**(깨진 판) — 비우면 구멍 너머 안쪽 벽이 빛을 받아 밝고, 눈구멍 속까지 밝아진다(v30).
//   깨진 자리는 눈구멍처럼 안으로 좁아지는 굴이다.
public static partial class ChibiPawnCreator
{
    // 거리장 좌표(머리 지름 ≈ 1)를 메시 좌표(머리 높이 1)로 — 턱을 단 해골의 키(1.24)가 사람 머리 높이와 거의 같게.
    const float SkullUnit = 1.20f;
    static readonly Vector3 SkullCenter = new(0f, 0.01f, -0.045f);

    // 격자 한 칸(거리장 단위) — 머리는 화면에서 100px 안팎이라 이보다 촘촘하면 정점만 늘어난다(두개골 1.1cm → 1.5cm로 4.4만 → 2.4만).
    // 금(2.2cm)과 이(3cm)가 끊기지 않을 만큼은 촘촘해야 한다.
    const float SkullStep = 0.015f, JawStep = 0.012f, TeethStep = 0.0065f;

    // 뽑은 뒤 남기는 정점 비율(`Decimate`) — 유니티 밖에서 뽑아 그려 원본과 나란히 보고 정했다(2026-10-27). 두개골 2.4만 → 3.5천 ·
    // 깨진 판은 금의 가는 가지가 무뎌져 덜 줄인다 · 이는 낟알이 작아 덜 줄인다.
    const float SkullKeep = 0.15f, CrackedKeep = 0.25f, JawKeep = 0.2f, TeethKeep = 0.25f;

    public static readonly string[] SkullTeeth = { "Normal", "Vampire", "Orc", "Monster" };
    public static readonly string[] SkullGaps  = { "None", "One", "Two" };

    // ── 굽기 ────────────────────────────────────────────────────────────────

    static Dictionary<string, Mesh> SkullMeshes()
    {
        var meshes = new Dictionary<string, Mesh>();
        Func<Vector3, float> cranium = p => Cranium(p, false);

        var b = new Bounds(new Vector3(0f, 0.165f, -0.04f), new Vector3(1.12f, 1.0f, 1.14f));
        meshes["Skull.Whole"]   = Sculpt("Chibi_Skull_Whole",   cranium,                b, SkullStep, cranium,                 SkullKeep);
        meshes["Skull.Cracked"] = Sculpt("Chibi_Skull_Cracked", p => Cranium(p, true),  b, SkullStep, p => Cranium(p, true),  CrackedKeep);

        var jb = new Bounds(new Vector3(0f, -0.39f, 0.14f), new Vector3(0.70f, 0.56f, 0.66f));
        meshes["Helmet"] = Sculpt("Chibi_Skull_Helmet", SkullCap, new Bounds(new Vector3(0f, 0.2f, -0.04f), new Vector3(1.24f, 1.0f, 1.26f)),
                                   SkullStep, SkullCap, SkullKeep);

        meshes["Jaw.Round"] = Sculpt("Chibi_Jaw_Round", p => Jaw(p, false), jb, JawStep, p => Mathf.Min(Jaw(p, false), cranium(p)), JawKeep);
        meshes["Jaw.V"]     = Sculpt("Chibi_Jaw_V",     p => Jaw(p, true),  jb, JawStep, p => Mathf.Min(Jaw(p, true),  cranium(p)), JawKeep);

        var ub = new Bounds(new Vector3(0f, -0.37f, 0.27f), new Vector3(0.48f, 0.26f, 0.40f));   // 흡혈귀 송곳니 끝(-0.485)까지
        var lb = new Bounds(new Vector3(0f, -0.32f, 0.27f), new Vector3(0.64f, 0.44f, 0.46f));   // 오크 엄니 끝(-0.14)까지
        foreach (var style in SkullTeeth)
        foreach (var gap in SkullGaps)
        {
            Func<Vector3, float> up = p => Teeth(p, style, gap, true);
            Func<Vector3, float> lo = p => Teeth(p, style, gap, false);
            Func<Vector3, float> scene = p => Mathf.Min(Mathf.Min(up(p), lo(p)), Mathf.Min(cranium(p), Jaw(p, true)));
            meshes[$"Teeth.{style}.{gap}"] = SaveSkull($"Chibi_Teeth_{style}_{gap}",
                Join(SurfaceNets(up, ub, TeethStep, scene, keep: TeethKeep), SurfaceNets(lo, lb, TeethStep, scene, keep: TeethKeep)));
        }
        return meshes;
    }

    // 해골 머리 부위 — 사람 머리(`HeadParts`) 대신 해골 셋이 받는다. **목 관절 자리(`Head`)와 투구는 사람과 같은 틀**이라
    // 몸·투구·`PawnLook`이 사람 머리와 똑같이 다룬다.
    static List<Part> SkullHeadParts(string gender, Dictionary<string, Mesh> skull)
    {
        var parts = new List<Part>();
        var basis = BasisOf(gender);
        var bPos  = HeadPos(basis);
        var size  = Vector3.one * basis.Size.y;

        parts.Add(new Part("Head", null, bPos, basis.Size, "HeadSkin", on: false));

        void Pick(string name, string mesh, bool on) =>
            parts.Add(new Part($"Head.Pick.{name}", skull[mesh], bPos, size, "Bone", on, outerMat: "OutlineOuterFine"));

        Pick("Skull.Whole",   "Skull.Whole",   true);
        Pick("Skull.Cracked", "Skull.Cracked", false);
        Pick("Jaw.Round",     "Jaw.Round",     false);
        Pick("Jaw.V",         "Jaw.V",         true);
        foreach (var style in SkullTeeth)
        foreach (var gap in SkullGaps)
            Pick($"Teeth.{style}@Gap.{gap}@Jaw.!None", $"Teeth.{style}.{gap}", style == "Normal" && gap == "None");

        // 투구는 두개골에 맞춰 빚은 모자 한 벌(`SkullCap`) — 이름만 윗머리 모양마다 둔다(`PawnLook`이 모양으로 켠다).
        foreach (var fit in HairFits)
            parts.Add(new Part($"Head.Helmet.Cap.Shell.{fit}", skull["Helmet"], bPos, size, "Helmet", on: false));
        return parts;
    }

    // ── 두개골 ──────────────────────────────────────────────────────────────

    static readonly Vector3 SockR = new(0.148f, 0.155f, 0.13f), SockC = new(0.20f, 0.06f, 0.46f);

    static float Cranium(Vector3 p, bool cracked)
    {
        var m = Mirror(p);
        float d = Ell(p, new Vector3(0f, 0.16f, -0.04f), new Vector3(0.50f, 0.47f, 0.52f));
        d = SMax(d, -(p.y + 0.16f), 0.05f);                                                     // 머리뼈 밑 — 아래는 얼굴뿐
        float face = RBox(p, new Vector3(0f, -0.17f, 0.24f), new Vector3(0.21f, 0.16f, 0.16f), 0.10f);   // 좁고 얕은 위턱
        face = SMax(face, -RBox(p, new Vector3(0f, -0.24f, 0.04f), new Vector3(0.13f, 0.12f, 0.13f), 0.05f), 0.03f);   // 이 뒤는 빈 말굽
        d = SMin(d, face, 0.14f);
        d = SMax(d, -Sph(m, new Vector3(0.66f, -0.10f, 0.02f), 0.26f), 0.10f);                  // 관자놀이가 들어간다
        d = SMin(d, Arch(m), 0.07f);                                                             // 광대 다리 — 밑은 뚫려 있다
        d = SMin(d, Ell(m, new Vector3(0.17f, 0.19f, 0.38f), new Vector3(0.12f, 0.04f, 0.06f)), 0.06f);   // 눈썹뼈
        d = SMax(d, -Socket(m), 0.03f);                                                          // 눈구멍 — 깊은 굴
        d = SMax(d, -Mathf.Max(Nose2D(p) - 0.004f, -0.05f - p.z), 0.012f);                      // 코 — 곧게 뚫는다
        d = SMax(d, -(p.y + 0.30f), 0.02f);                                                      // 위턱 바닥은 평평하다
        return cracked ? Crack(d, p) : d;
    }

    // **투구 — 두개골을 한 겹 부풀린 모자**(2026-10-27). 사람 투구 껍데기(머리카락 틀)를 줄여 씌웠더니 귀 옆까지 내려와
    // 가죽 투구가 **단발머리로 읽혔다**(사용자: *"해골이 머리카락이 있어"*). 앞은 눈구멍 위에서, 뒤는 뒤통수 아래에서 기울게 끊는다.
    // 속은 채운다 — 쓰면 두개골 윗머리가 다 가려진다.
    static float SkullCap(Vector3 p)
    {
        float d = Ell(p, new Vector3(0f, 0.16f, -0.04f), new Vector3(0.55f, 0.52f, 0.57f));
        d = SMax(d, 0.13f + 0.25f * p.z - p.y, 0.03f);   // 앞(z 0.5) 0.255 — 눈구멍 위(0.215)를 비켜서 · 뒤(z -0.5) 0.005
        return d;
    }

    // 광대 다리 — 눈구멍 바깥 아래에서 귀 쪽으로, 살짝 바깥으로 휜 막대.
    static float Arch(Vector3 m)
    {
        var a = new Vector3(0.24f, -0.075f, 0.31f); var mid = new Vector3(0.33f, -0.075f, 0.13f); var b = new Vector3(0.31f, -0.02f, -0.17f);
        return Mathf.Min(Capsule(m, a, mid, 0.034f), Capsule(m, mid, b, 0.03f));
    }

    // 눈구멍 — 앞은 둥근 네모(초타원), 안으로 갈수록 좁아지는 굴.
    static float Socket(Vector3 m)
    {
        float k  = Mathf.Clamp01((SockC.z - m.z) / 0.45f);
        float sc = 1f - 0.45f * k;
        float qx = Mathf.Abs((m.x - SockC.x) / (SockR.x * sc)), qy = Mathf.Abs((m.y - SockC.y) / (SockR.y * sc));
        float q4 = Mathf.Pow(Mathf.Pow(qx, 3.2f) + Mathf.Pow(qy, 3.2f), 1f / 3.2f);
        float d2 = (q4 - 1f) * Mathf.Min(SockR.x, SockR.y) * sc;
        return Mathf.Max(d2, Mathf.Max(m.z - (SockC.z + 0.2f), (SockC.z - 0.45f) - m.z));
    }

    // 콧구멍 — 위는 둥글고 아래로 벌어진 뒤집힌 하트, 밑 가운데 세모 돌기는 **아래 뼈와 한 덩어리**(사용자: *"인중이랑 나뉘어져 보이는데"*).
    static float Nose2D(Vector3 p)
    {
        const float nt = -0.11f, nb = -0.25f, nw = 0.062f, notch = -0.205f;
        float x = Mathf.Abs(p.x), y = p.y;
        float k  = Mathf.Clamp01((nt - y) / (nt - nb));
        float rx = nw * Mathf.Sqrt(Mathf.Clamp01(k * 1.6f)) * (0.75f + 0.25f * k);
        float d  = Mathf.Max(x - rx, Mathf.Max(y - nt, nb - y));
        bool inV = x < (notch - y) * 0.55f && y < notch;
        return inV ? Mathf.Max(d, 0.03f) : d;
    }

    // 깨진 자리 — 윗머리 앞쪽 한쪽(맞은 자리)에 들쭉날쭉한 굴과 테두리에서 뻗는 금 넷.
    static readonly Vector3 CrackC = new(0f, 0.16f, -0.04f);
    static readonly Vector3 CrackAx = new Vector3(0.42f, 0.78f, 0.46f).normalized;

    static float Crack(float d, Vector3 p)
    {
        var u = Vector3.Cross(CrackAx, Vector3.forward).normalized;
        var v = Vector3.Cross(CrackAx, u);
        var q = p - CrackC;
        float a = Vector3.Dot(q, CrackAx), x = Vector3.Dot(q, u), y = Vector3.Dot(q, v);
        float th = Mathf.Atan2(y, x), r = Mathf.Sqrt(x * x + y * y);
        float rad = 0.105f + 0.022f * Mathf.Sin(3f * th + 0.7f) + 0.014f * Mathf.Sin(7f * th + 2.1f)
                  + 0.008f * Mathf.Sin(13f * th + 4.0f) + 0.005f * Mathf.Sin(23f * th + 1.3f);
        float tap  = 0.35f + 0.65f * Mathf.Clamp01((a - 0.05f) / 0.42f);
        float hole = Mathf.Max(r - rad * tap, 0.05f - a);
        d = SMax(d, -hole, 0.006f);

        // 금 — 렌더(0.7cm)보다 굵다: 격자 한 칸(`SkullStep`)보다 가늘면 뽑을 때 끊긴다.
        foreach (var (ang, ln, bend) in new[] { (0.3f, 0.20f, 0.05f), (2.0f, 0.16f, -0.04f), (3.6f, 0.22f, 0.06f), (5.1f, 0.13f, -0.03f) })
        {
            const float r0 = 0.10f;
            if (r < r0 - 0.02f || r > r0 + ln) continue;
            float t   = Mathf.Clamp01((r - r0) / ln);
            float aim = ang + bend * Mathf.Sin(t * 9f) + 0.12f * t;
            float dth = Mathf.DeltaAngle(aim * Mathf.Rad2Deg, th * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float g   = Mathf.Abs(dth) * r - 0.011f * (1f - t);
            d = SMax(d, -Mathf.Max(g, 0.10f - a), 0.004f);
        }
        return d;
    }

    // ── 턱 ──────────────────────────────────────────────────────────────────

    // 치열 곡선 — 앞니에서 좁고 어금니에서 광대 폭까지 넓은 포물선(xz). 턱은 이것을 따라 휘는 둥근 막대다.
    const float ArchZF = 0.40f, ArchZB = 0.04f, ArchHW = 0.27f;
    static readonly Vector2[] JawArch = ArchPoints(0.93f, ArchZF - 0.03f);

    static Vector2[] ArchPoints(float scale, float zf, int n = 24)
    {
        var half = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float z = Mathf.Lerp(zf, ArchZB, i / (n - 1f));
            half[i] = new Vector2(Mathf.Sqrt(Mathf.Clamp01((zf - z) / (zf - ArchZB))) * ArchHW * scale, z);
        }
        var pts = new Vector2[n * 2 - 1];
        for (int i = 0; i < n; i++) pts[i] = new Vector2(-half[n - 1 - i].x, half[n - 1 - i].y);
        for (int i = 1; i < n; i++) pts[n - 1 + i] = half[i];
        return pts;
    }

    static float ArchDist(Vector3 p, Vector2[] pts)
    {
        var xz = new Vector2(p.x, p.z);
        float d = 1e9f;
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            var pa = xz - pts[i]; var ba = pts[i + 1] - pts[i];
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            d = Mathf.Min(d, (pa - ba * h).magnitude);
        }
        return d;
    }

    // 치열을 따라 휘는 막대 — 위는 `ytop`, 밑은 점마다(턱끝이 낮고 뒤가 높은 V).
    static float Tube(Vector3 p, Vector2[] pts, float r, float ytop, float ybot)
    {
        float dxz = ArchDist(p, pts) - r;
        float yc = (ytop + ybot) * 0.5f, hy = Mathf.Abs(ytop - ybot) * 0.5f;
        float dy = Mathf.Abs(p.y - yc) - hy;
        return new Vector2(Mathf.Max(dxz, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dxz, dy), 0f);
    }

    // `v` — v28(사용자: 정면에서 둥근 V) · 아니면 v27(밑이 덜 내려가고 뒷기둥이 굵다). 둘 다 사용자가 *"괜찮아"*.
    static float Jaw(Vector3 p, bool v)
    {
        var m = Mirror(p);
        float sx  = Mathf.Clamp01((ArchZF - p.z) / (ArchZF - ArchZB));                        // 턱끝 0 · 뒤 1
        float bot = v ? -0.61f + 0.19f * Mathf.Pow(sx, 1.15f) : -0.575f + 0.10f * Mathf.Pow(sx, 1.4f);
        float body = Tube(p, JawArch, 0.004f, -0.425f, bot + 0.03f) - 0.031f;
        body = v ? SMin(body, Ell(p, new Vector3(0f, -0.57f, 0.37f), new Vector3(0.06f, 0.035f, 0.035f)), 0.04f)
                 : SMin(body, Ell(p, new Vector3(0f, -0.535f, 0.375f), new Vector3(0.075f, 0.038f, 0.035f)), 0.04f);   // 턱끝 돌기

        // 뒷기둥 — 턱 모서리에서 뚫린 볼 공간으로 비스듬히 올라가는 둥근 판.
        var a = v ? new Vector2(-0.43f, 0.05f) : new Vector2(-0.47f, 0.06f);
        var b = new Vector2(-0.20f, -0.05f);
        var yz = new Vector2(m.y, m.z); var pa = yz - a; var ba = b - a;
        float hh = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        float d2 = (pa - ba * hh).magnitude - (v ? 0.042f + 0.004f * hh : 0.058f - 0.012f * hh);
        float dx = Mathf.Abs(m.x - 0.25f) - 0.012f;
        float ramus = new Vector2(Mathf.Max(d2, 0f), Mathf.Max(dx, 0f)).magnitude + Mathf.Min(Mathf.Max(d2, dx), 0f) - 0.012f;
        return SMin(body, ramus, 0.07f);
    }

    // ── 이 ──────────────────────────────────────────────────────────────────

    // 한 줄의 이 다섯(앞니 · 옆니 · 송곳니 · 작은 어금니 · 어금니) — 포물선 위에 호 길이로 늘어놓는다.
    readonly struct Tooth
    {
        public readonly float X, Z, Tx, Tz, W, K;   // K: 앞니 0 → 어금니 1
        public Tooth(float x, float z, float tx, float tz, float w, float k) { X = x; Z = z; Tx = tx; Tz = tz; W = w; K = k; }
    }

    static readonly Tooth[] UpperRow = ToothRow(0.19f, 0.395f, 0.16f);
    static readonly Tooth[] LowerRow = ToothRow(0.175f, 0.378f, 0.15f);

    static Tooth[] ToothRow(float hw, float zf, float zb)
    {
        const int samples = 400;
        var xs = new float[samples]; var zs = new float[samples]; var len = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (samples - 1f);
            xs[i] = hw * t; zs[i] = zf - (zf - zb) * t * t;
            if (i > 0) len[i] = len[i - 1] + new Vector2(xs[i] - xs[i - 1], zs[i] - zs[i - 1]).magnitude;
        }
        float[] widths = { 0.026f, 0.022f, 0.024f, 0.028f, 0.034f };
        var row = new Tooth[widths.Length];
        float pos = 0f;
        for (int n = 0; n < widths.Length; n++)
        {
            float w = widths[n], c = pos + w; pos = c + w + 0.004f;
            int j = Array.BinarySearch(len, c); if (j < 0) j = ~j;
            j = Mathf.Min(j, samples - 2);
            var tan = new Vector2(xs[j + 1] - xs[j], zs[j + 1] - zs[j]).normalized;
            row[n] = new Tooth(xs[j], zs[j], tan.x, tan.y, w, n / (widths.Length - 1f));
        }
        return row;
    }

    // 이 하나의 생김 — (길이 배수 · 뾰족함 · 물림선을 넘나 · 앞으로 내밈). null이면 그 자리는 비운다(오크의 아래 송곳니는 엄니가 된다).
    static (float len, float point, bool past, float forward)? ToothSpec(string style, int i, bool up)
    {
        if (style == "Vampire" && up && i == 2) return (1.7f, 1f, true, 0.03f);
        if (style == "Orc" && !up && i == 2)    return null;
        if (style == "Monster")                 return (i >= 1 ? 1.5f : 1.25f, 1f, true, up ? 0.012f : 0f);
        return (1f, 0f, false, 0f);
    }

    // 빠진 이 — 씨앗이 고르는 셋. 오크 엄니 자리(아래 2)는 안 뺀다.
    static bool Missing(string gap, bool up, int i, int side) => gap switch
    {
        "One" => up && i == 1 && side > 0,
        "Two" => (!up && i == 3 && side < 0) || (up && i == 0 && side < 0),
        _     => false,
    };

    static float Teeth(Vector3 p, string style, string gap, bool up)
    {
        float yc = up ? -0.345f : -0.43f, h = up ? 0.058f : 0.052f, bite = up ? -0.385f : -0.389f;
        var row = up ? UpperRow : LowerRow;
        float d = 1e9f, free = 1e9f;
        for (int side = 1; side >= -1; side -= 2)
        for (int i = 0; i < row.Length; i++)
        {
            if (Missing(gap, up, i, side)) continue;
            var spec = ToothSpec(style, i, up);
            if (spec == null) continue;
            var (ln, pt, past, fw) = spec.Value;
            var tt = row[i];
            float cx = tt.X * side, ux = tt.Tx * side;
            float dx = p.x - cx, dz = p.z - (tt.Z + fw);
            float u = dx * ux + dz * tt.Tz, w = -dx * tt.Tz + dz * ux;   // 치열을 따라 · 가로질러
            float hh = h * (1f - 0.15f * tt.K) * (1f + 0.06f * Mathf.Sin(7.3f * tt.X + 3f * side)) * ln;
            float cy = ln > 1f ? (up ? -(hh - h) : hh - h) : 0f;          // 뿌리는 보통 이 자리 · 끝만 자란다
            float yl = p.y - yc - cy;
            float tip = Mathf.Clamp01((up ? -yl : yl) / hh);
            float sc  = 1f - pt * 0.85f * Mathf.Pow(tip, 1.2f);
            var q = new Vector3(u / sc, yl, w / sc);
            float dd = Ell(q, Vector3.zero, new Vector3(tt.W * (ln > 1.6f ? 1.15f : 1f), hh, 0.020f + 0.014f * tt.K)) * Mathf.Min(sc, 1f) * 0.8f;
            if (past) free = Mathf.Min(free, dd); else d = Mathf.Min(d, dd);
        }
        d = SMax(d, up ? bite - p.y : p.y - bite, 0.012f);                // 물리는 끝은 살짝 뭉툭하게
        if (!up) free = Mathf.Max(free, -0.43f - p.y);                     // 아랫니는 턱 윗면 아래로 안 내려간다
        if (!up && style == "Orc") free = Mathf.Min(free, Tusk(p));
        return Mathf.Min(d, free);
    }

    // 오크 엄니 — 아래 송곳니 자리(이 줄)에서 솟아 앞 · 바깥으로 나왔다가 위로 휘는 뿔. 밑동이 굵고 끝이 뾰족하다.
    // (사용자: *"아예 이런식으로 밖으로 튀어나오게"* → *"위치가 이빨이 아니라 그냥 턱에 난거잖아"* → *"더 굵게 · 진짜 이빨 위치로"*)
    static float Tusk(Vector3 p)
    {
        var m  = Mirror(p);
        var c  = LowerRow[2];
        var p0 = new Vector3(c.X, -0.41f, c.Z);
        var p1 = p0 + new Vector3(0.075f, 0.05f, 0.08f);
        var p2 = p0 + new Vector3(0.14f, 0.27f, 0.06f);
        const int n = 12;
        float d = 1e9f;
        var prev = p0; float prevR = 0.054f;
        for (int i = 1; i <= n; i++)
        {
            float t = i / (float)n;
            var pt = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
            float r = 0.05f * Mathf.Pow(1 - t, 0.8f) + 0.004f;
            var pa = m - prev; var ba = pt - prev;
            float hh = Mathf.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
            d = Mathf.Min(d, (pa - ba * hh).magnitude - Mathf.Lerp(prevR, r, hh));
            prev = pt; prevR = r;
        }
        return d;
    }

    // ── 거리장 조각 ─────────────────────────────────────────────────────────

    static Vector3 Mirror(Vector3 p) => new(Mathf.Abs(p.x), p.y, p.z);

    static float SMin(float a, float b, float k)
    {
        float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
        return b * (1 - h) + a * h - k * h * (1 - h);
    }
    static float SMax(float a, float b, float k) => -SMin(-a, -b, k);

    static float Ell(Vector3 p, Vector3 c, Vector3 r)
    {
        var q  = new Vector3((p.x - c.x) / r.x, (p.y - c.y) / r.y, (p.z - c.z) / r.z);
        float k0 = q.magnitude;
        float k1 = new Vector3(q.x / r.x, q.y / r.y, q.z / r.z).magnitude;
        return k0 * (k0 - 1f) / Mathf.Max(k1, 1e-6f);
    }

    static float RBox(Vector3 p, Vector3 c, Vector3 h, float r)
    {
        var q = new Vector3(Mathf.Abs(p.x - c.x) - (h.x - r), Mathf.Abs(p.y - c.y) - (h.y - r), Mathf.Abs(p.z - c.z) - (h.z - r));
        return Vector3.Max(q, Vector3.zero).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f) - r;
    }

    static float Sph(Vector3 p, Vector3 c, float r) => (p - c).magnitude - r;

    static float Capsule(Vector3 p, Vector3 a, Vector3 b, float r)
    {
        var pa = p - a; var ba = b - a;
        float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    // ── 표면 뽑기 — 표면 그물(surface nets) ─────────────────────────────────
    //
    // 칸마다 부호가 바뀌면 점 하나(모서리 교차점의 평균을 표면으로 당긴 것)를 두고, 부호가 바뀌는 격자 변마다 그 변을
    // 둘러싼 네 칸의 점을 사각형으로 잇는다. 마칭 큐브 표가 필요 없고 점이 칸마다 하나라 메시가 고르다.
    // 법선은 거리장의 기울기 · 정점 색은 그늘(`AoOf` — 셰이더의 `_VertexAO`).

    readonly struct Surface
    {
        public readonly List<Vector3> Verts, Normals; public readonly List<Color> Colors; public readonly List<int> Tris;
        public Surface(List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t) { Verts = v; Normals = n; Colors = c; Tris = t; }
    }

    static Mesh Sculpt(string name, Func<Vector3, float> f, Bounds b, float step, Func<Vector3, float> scene, float keep)
        => SaveSkull(name, SurfaceNets(f, b, step, scene, keep: keep));

    // `place` — 거리장 좌표를 메시 좌표로(비우면 해골 머리의 틀 — `SkullCenter` · `SkullUnit`) · `aoStep`·`aoGain` — 그늘을 재는 걸음(거리장 단위) ·
    // `keep` — 뽑은 뒤 정점을 이 비율까지 줄인다(`Decimate`).
    static Surface SurfaceNets(Func<Vector3, float> f, Bounds b, float step, Func<Vector3, float> scene,
                               Func<Vector3, Vector3> place = null, float aoStep = 0.04f, float aoGain = 4.5f, float keep = 1f)
    {
        int nx = Mathf.CeilToInt(b.size.x / step) + 1, ny = Mathf.CeilToInt(b.size.y / step) + 1, nz = Mathf.CeilToInt(b.size.z / step) + 1;
        var min = b.min;
        var val = new float[nx * ny * nz];
        int Idx(int i, int j, int k) => (k * ny + j) * nx + i;
        Vector3 At(int i, int j, int k) => min + new Vector3(i * step, j * step, k * step);

        Parallel.For(0, nz, k =>
        {
            for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++) val[Idx(i, j, k)] = f(At(i, j, k));
        });

        // 칸마다 점 하나.
        var cellVert = new int[(nx - 1) * (ny - 1) * (nz - 1)];
        int Cell(int i, int j, int k) => (k * (ny - 1) + j) * (nx - 1) + i;
        var verts = new List<Vector3>();
        var corner = new float[8];
        int[,] edges = { {0,1},{2,3},{4,5},{6,7},{0,2},{1,3},{4,6},{5,7},{0,4},{1,5},{2,6},{3,7} };
        for (int k = 0; k < nz - 1; k++)
        for (int j = 0; j < ny - 1; j++)
        for (int i = 0; i < nx - 1; i++)
        {
            int neg = 0;
            for (int c = 0; c < 8; c++)
            {
                corner[c] = val[Idx(i + (c & 1), j + ((c >> 1) & 1), k + ((c >> 2) & 1))];
                if (corner[c] < 0f) neg++;
            }
            if (neg == 0 || neg == 8) { cellVert[Cell(i, j, k)] = -1; continue; }

            var sum = Vector3.zero; int cnt = 0;
            for (int e = 0; e < 12; e++)
            {
                int a = edges[e, 0], c2 = edges[e, 1];
                float va = corner[a], vb = corner[c2];
                if ((va < 0f) == (vb < 0f)) continue;
                float t = va / (va - vb);
                var pa = new Vector3(a & 1, (a >> 1) & 1, (a >> 2) & 1);
                var pb = new Vector3(c2 & 1, (c2 >> 1) & 1, (c2 >> 2) & 1);
                sum += Vector3.Lerp(pa, pb, t); cnt++;
            }
            cellVert[Cell(i, j, k)] = verts.Count;
            verts.Add(At(i, j, k) + sum / cnt * step);
        }

        // 표면으로 당기고 법선을 잰다 — 평균 점은 표면에서 조금 떠 있다. `reach` 0이면 옮기지 않고 법선만 잰다.
        Vector3[] Settle(Vector3[] ps, float reach)
        {
            var ns = new Vector3[ps.Length];
            Parallel.For(0, ps.Length, v =>
            {
                var p = ps[v];
                for (int it = 0; it < 2; it++)
                {
                    var g = Grad(f, p, step * 0.5f);
                    float gl = g.magnitude;
                    if (gl < 1e-6f) break;
                    var moved = p - g / gl * (f(p) / gl);
                    if ((moved - ps[v]).sqrMagnitude > reach * reach) break;   // 멀리 튀면 제자리에 둔다
                    p = moved;
                }
                ps[v] = p;
                ns[v] = Grad(f, p, step * 0.5f).normalized;
            });
            return ns;
        }
        var pts = verts.ToArray();
        var normals = Settle(pts, step);

        // 부호가 바뀌는 격자 변마다 사각형 하나. 네 칸은 변 축을 오른손으로 도는 순서로 받는다 — 그 순서면 면이 변 축의 +쪽을
        // 본다. **앞뒤는 그 변의 부호로만 정한다**(안 → 밖이 +쪽이면 그대로) — 꺾인 사각형의 대각선으로 재면 접힌 자리에서
        // 틀려 이웃과 앞뒤가 엇갈리고, 뒷면을 버리는 셰이더에서 그대로 구멍이 된다(처음 판 — 두개골에 6천 개).
        var tris = new List<int>();
        void Quad(int a, int b2, int c, int d, bool outward)
        {
            if (a < 0 || b2 < 0 || c < 0 || d < 0) return;
            bool flip = !outward;
            // 짧은 대각선으로 가른다 — 긴 쪽으로 가르면 얇은 세모가 선다.
            bool diagAC = (pts[a] - pts[c]).sqrMagnitude <= (pts[b2] - pts[d]).sqrMagnitude;
            int[] q = flip ? new[] { a, d, c, b2 } : new[] { a, b2, c, d };
            if (diagAC) tris.AddRange(new[] { q[0], q[1], q[2], q[0], q[2], q[3] });
            else        tris.AddRange(new[] { q[1], q[2], q[3], q[1], q[3], q[0] });
        }
        for (int k = 1; k < nz - 1; k++)
        for (int j = 1; j < ny - 1; j++)
        for (int i = 1; i < nx - 1; i++)
        {
            float v0 = val[Idx(i, j, k)];
            bool in0 = v0 < 0f;
            // +x 변
            if ((val[Idx(i + 1, j, k)] < 0f) != in0)
                Quad(cellVert[Cell(i, j - 1, k - 1)], cellVert[Cell(i, j, k - 1)], cellVert[Cell(i, j, k)], cellVert[Cell(i, j - 1, k)],
                     in0);
            // +y 변
            if ((val[Idx(i, j + 1, k)] < 0f) != in0)
                Quad(cellVert[Cell(i - 1, j, k - 1)], cellVert[Cell(i - 1, j, k)], cellVert[Cell(i, j, k)], cellVert[Cell(i, j, k - 1)],
                     in0);
            // +z 변
            if ((val[Idx(i, j, k + 1)] < 0f) != in0)
                Quad(cellVert[Cell(i - 1, j - 1, k)], cellVert[Cell(i, j - 1, k)], cellVert[Cell(i, j, k)], cellVert[Cell(i - 1, j, k)],
                     in0);
        }

        if (keep < 1f)
        {
            // **줄인 점은 다시 표면으로 당기지 않는다** — 접은 자리는 이차 오차가 이미 둘레 면 위에 두었다. 당기면 큰 세모의 꼭짓점만
            // 곡면으로 끌려가 면이 울퉁불퉁해졌다(쇄골 · 껍데기). 법선만 그 자리의 기울기로 잰다.
            pts = Decimate(pts, tris, Mathf.Max(64, Mathf.RoundToInt(pts.Length * keep)));
            normals = Settle(pts, 0f);
        }

        var colors = new Color[pts.Length];
        Parallel.For(0, pts.Length, v => { float ao = AoOf(scene, pts[v], normals[v], aoStep, aoGain); colors[v] = new Color(ao, ao, ao, 1f); });

        var outV = new List<Vector3>(pts.Length);
        foreach (var p in pts) outV.Add(place != null ? place(p) : (p - SkullCenter) / SkullUnit);
        return new Surface(outV, new List<Vector3>(normals), new List<Color>(colors), tris);
    }

    // ── 표면 줄이기 — 이차 오차로 변을 접는다 ──────────────────────────────────
    //
    // 표면 그물은 평평한 곳에도 굽은 곳과 같은 촘촘함으로 점을 깐다 — 해골 하나가 사람 폰의 여덟 배(8.4만)였다(2026-10-27,
    // 사용자: *"보통 몹 하나의 정점이 어느정도여야하는데?"*). 점마다 둘레 면의 평면을 모아 두고(이차 오차), **모양을 덜 바꾸는 변부터**
    // 접는다. 접어서 둘레 면이 뒤집히거나 납작해지면 그 변은 건너뛴다. 법선 · 그늘은 줄인 뒤 거리장에서 다시 잰다(`Settle`).
    // `tris`는 제자리에서 고쳐 쓰고, 살아남은 점만 새 번호로 돌려준다.
    static Vector3[] Decimate(Vector3[] pos, List<int> tris, int target)
    {
        int nv = pos.Length, nt = tris.Count / 3;
        if (nv <= target) return pos;
        var tv = tris.ToArray();
        var q = new double[nv * 10];
        var vtris = new List<int>[nv];
        for (int v = 0; v < nv; v++) vtris[v] = new List<int>(8);
        for (int t = 0; t < nt; t++)
        {
            Vector3 a = pos[tv[3 * t]], b = pos[tv[3 * t + 1]], c = pos[tv[3 * t + 2]];
            var n = Vector3.Cross(b - a, c - a);
            double area = n.magnitude;
            for (int k = 0; k < 3; k++) vtris[tv[3 * t + k]].Add(t);
            if (area < 1e-12) continue;
            double nx = n.x / area, ny = n.y / area, nz = n.z / area, d = -(nx * a.x + ny * a.y + nz * a.z);
            double[] kp = { nx * nx, nx * ny, nx * nz, nx * d, ny * ny, ny * nz, ny * d, nz * nz, nz * d, d * d };
            for (int k = 0; k < 3; k++)
            {
                int o = tv[3 * t + k] * 10;
                for (int i = 0; i < 10; i++) q[o + i] += kp[i] * area;
            }
        }

        double ErrAt(int o, double x, double y, double z)
            => q[o] * x * x + 2 * q[o + 1] * x * y + 2 * q[o + 2] * x * z + 2 * q[o + 3] * x
             + q[o + 4] * y * y + 2 * q[o + 5] * y * z + 2 * q[o + 6] * y + q[o + 7] * z * z + 2 * q[o + 8] * z + q[o + 9];
        double Err(int v, int w, Vector3 p) => ErrAt(v * 10, p.x, p.y, p.z) + ErrAt(w * 10, p.x, p.y, p.z);
        // 두 점의 오차를 가장 덜 키우는 자리 — 3×3을 풀고, 안 풀리거나(평평 · 곧은 홈) 멀리 튀면 두 끝과 가운데 중 낫는 것.
        (double cost, Vector3 at) Best(int v, int w)
        {
            int o = v * 10, u = w * 10;
            double a00 = q[o] + q[u], a01 = q[o + 1] + q[u + 1], a02 = q[o + 2] + q[u + 2];
            double a11 = q[o + 4] + q[u + 4], a12 = q[o + 5] + q[u + 5], a22 = q[o + 7] + q[u + 7];
            double b0 = -(q[o + 3] + q[u + 3]), b1 = -(q[o + 6] + q[u + 6]), b2 = -(q[o + 8] + q[u + 8]);
            double det = a00 * (a11 * a22 - a12 * a12) - a01 * (a01 * a22 - a12 * a02) + a02 * (a01 * a12 - a11 * a02);
            Vector3 mid = (pos[v] + pos[w]) * 0.5f;
            if (Math.Abs(det) > 1e-15)
            {
                double x = (b0 * (a11 * a22 - a12 * a12) - a01 * (b1 * a22 - a12 * b2) + a02 * (b1 * a12 - a11 * b2)) / det;
                double y = (a00 * (b1 * a22 - a12 * b2) - b0 * (a01 * a22 - a12 * a02) + a02 * (a01 * b2 - b1 * a02)) / det;
                double z = (a00 * (a11 * b2 - b1 * a12) - a01 * (a01 * b2 - b1 * a02) + b0 * (a01 * a12 - a11 * a02)) / det;
                var p = new Vector3((float)x, (float)y, (float)z);
                if ((p - mid).sqrMagnitude <= (pos[v] - pos[w]).sqrMagnitude) return (Err(v, w, p), p);
            }
            double ev = Err(v, w, pos[v]), ew = Err(v, w, pos[w]), em = Err(v, w, mid);
            return ev <= ew && ev <= em ? (ev, pos[v]) : ew <= em ? (ew, pos[w]) : (em, mid);
        }

        // 최소 힙 — 꺼낼 때 두 점의 도장(`stamp`)이 그대로인지 보고 낡은 것은 버린다.
        var dead  = new bool[nt];
        var gone  = new bool[nv];
        var stamp = new int[nv];
        var heap  = new List<(double cost, int v, int w, int sv, int sw, Vector3 at)>();
        void Push(int v, int w)
        {
            var (c, at) = Best(v, w);
            heap.Add((c, v, w, stamp[v], stamp[w], at));
            for (int i = heap.Count - 1; i > 0;)
            {
                int pa = (i - 1) / 2;
                if (heap[pa].cost <= heap[i].cost) break;
                (heap[pa], heap[i]) = (heap[i], heap[pa]); i = pa;
            }
        }
        (double cost, int v, int w, int sv, int sw, Vector3 at) Pop()
        {
            var top = heap[0];
            heap[0] = heap[^1]; heap.RemoveAt(heap.Count - 1);
            for (int i = 0; ;)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < heap.Count && heap[l].cost < heap[m].cost) m = l;
                if (r < heap.Count && heap[r].cost < heap[m].cost) m = r;
                if (m == i) break;
                (heap[m], heap[i]) = (heap[i], heap[m]); i = m;
            }
            return top;
        }
        bool Has(int t, int v) => tv[3 * t] == v || tv[3 * t + 1] == v || tv[3 * t + 2] == v;
        // `v`를 `at`으로 옮기면 `v`의 면 중 `w`와 안 닿는 것이 뒤집히거나 납작해지나.
        bool Folds(int v, int w, Vector3 at)
        {
            foreach (int t in vtris[v])
            {
                if (dead[t] || Has(t, w)) continue;
                Vector3 a = pos[tv[3 * t]], b = pos[tv[3 * t + 1]], c = pos[tv[3 * t + 2]];
                var before = Vector3.Cross(b - a, c - a);
                if (tv[3 * t] == v) a = at; else if (tv[3 * t + 1] == v) b = at; else c = at;
                var after = Vector3.Cross(b - a, c - a);
                if (after.sqrMagnitude < 1e-14f || Vector3.Dot(before.normalized, after.normalized) < 0.8f) return true;   // 37°보다 꺾이면
            }
            return false;
        }
        // 두 점의 이웃 중 둘 다와 닿는 것은 그 변을 낀 면의 맞은편 꼭짓점 둘뿐이어야 한다 — 더 있으면 접는 순간 면이 겹쳐 붙는다(얇은 판 · 고리).
        var mark = new int[nv];
        int markGen = 0;
        bool Pinches(int v, int w)
        {
            markGen++;
            foreach (int t in vtris[v]) if (!dead[t]) for (int k = 0; k < 3; k++) mark[tv[3 * t + k]] = markGen;
            int shared = 0;
            foreach (int t in vtris[w])
                if (!dead[t])
                    for (int k = 0; k < 3; k++)
                    {
                        int n = tv[3 * t + k];
                        if (n != v && n != w && mark[n] == markGen) { shared++; mark[n] = 0; }   // 센 것은 지운다 — 두 면에서 나와도 한 번
                    }
            return shared != 2;
        }

        for (int t = 0; t < nt; t++)
            for (int k = 0; k < 3; k++)
            {
                int v = tv[3 * t + k], w = tv[3 * t + (k + 1) % 3];
                if (v < w) Push(v, w);   // 닫힌 면이면 변마다 두 번 나온다 — 한 번만
            }

        int live = nv;
        while (live > target && heap.Count > 0)
        {
            var e = Pop();
            if (gone[e.v] || gone[e.w] || stamp[e.v] != e.sv || stamp[e.w] != e.sw) continue;
            if (Pinches(e.v, e.w) || Folds(e.v, e.w, e.at) || Folds(e.w, e.v, e.at)) continue;

            int keep = e.v, drop = e.w;
            pos[keep] = e.at;
            for (int i = 0; i < 10; i++) q[keep * 10 + i] += q[drop * 10 + i];
            gone[drop] = true; live--;
            foreach (int t in vtris[drop])
            {
                if (dead[t]) continue;
                if (Has(t, keep)) { dead[t] = true; continue; }
                for (int k = 0; k < 3; k++) if (tv[3 * t + k] == drop) tv[3 * t + k] = keep;
                vtris[keep].Add(t);
            }
            vtris[drop].Clear();
            vtris[keep].RemoveAll(t => dead[t]);
            stamp[keep]++;
            markGen++;   // 이웃은 면 둘에 걸쳐 나온다 — 한 번만 넣는다
            foreach (int t in vtris[keep])
                for (int k = 0; k < 3; k++)
                {
                    int n = tv[3 * t + k];
                    if (n == keep || mark[n] == markGen) continue;
                    mark[n] = markGen;
                    Push(keep, n);
                }
        }

        var remap = new int[nv];
        var outP = new List<Vector3>(live);
        for (int v = 0; v < nv; v++) { remap[v] = gone[v] ? -1 : outP.Count; if (!gone[v]) outP.Add(pos[v]); }
        tris.Clear();
        for (int t = 0; t < nt; t++)
        {
            if (dead[t]) continue;
            int a = tv[3 * t], b = tv[3 * t + 1], c = tv[3 * t + 2];
            if (a == b || b == c || a == c) continue;
            tris.Add(remap[a]); tris.Add(remap[b]); tris.Add(remap[c]);
        }
        return outP.ToArray();
    }


    static Vector3 Grad(Func<Vector3, float> f, Vector3 p, float e) => new(
        f(p + new Vector3(e, 0, 0)) - f(p - new Vector3(e, 0, 0)),
        f(p + new Vector3(0, e, 0)) - f(p - new Vector3(0, e, 0)),
        f(p + new Vector3(0, 0, e)) - f(p - new Vector3(0, 0, e)));

    // 그늘 — 법선 쪽으로 나가며 가까운 표면이 얼마나 막나(렌더의 v25 셈 그대로). 깊은 구멍 속이 검어지는 것은 이것 하나다.
    static float AoOf(Func<Vector3, float> scene, Vector3 p, Vector3 n, float step, float gain)
    {
        float ao = 1f;
        for (int i = 1; i <= 8; i++)
        {
            float h = step * i;
            ao -= Mathf.Max(h - scene(p + n * h), 0f) * Mathf.Pow(0.8f, i) * gain;
        }
        return Mathf.Clamp(ao, 0.08f, 1f);
    }

    static Surface Join(Surface a, Surface b)
    {
        int off = a.Verts.Count;
        var v = new List<Vector3>(a.Verts); v.AddRange(b.Verts);
        var n = new List<Vector3>(a.Normals); n.AddRange(b.Normals);
        var c = new List<Color>(a.Colors); c.AddRange(b.Colors);
        var t = new List<int>(a.Tris); foreach (var i in b.Tris) t.Add(i + off);
        return new Surface(v, n, c, t);
    }

    // `Save`와 같은 규칙(있으면 속만 갈아 끼운다 · 쓴 것을 적어 둔다) — 법선과 정점 색을 든다.
    static Mesh SaveSkull(string name, Surface s)
    {
        EnsureDir(MeshDir);
        string path = $"{MeshDir}/{name}.asset";
        _saved.Add(path);
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool fresh = m == null;
        if (fresh) m = new Mesh(); else m.Clear();

        m.name = name;
        m.indexFormat = s.Verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(s.Verts);
        m.SetNormals(s.Normals);
        m.SetColors(s.Colors);
        m.SetTriangles(s.Tris, 0);
        m.RecalculateBounds();

        if (fresh) AssetDatabase.CreateAsset(m, path);
        else       EditorUtility.SetDirty(m);
        return m;
    }
}
