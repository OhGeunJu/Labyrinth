using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **드워프 — 사람 치비 몸 + 빚은 드워프 머리**(2026-11-03, 사용자: *"이번에는 드워프를 넣을까?"* → 몸은 사람 치비 · 머리는 오크처럼 빚는다).
//
// 몸은 사람 몸(`DressSkinned`)에 체형 한 줄(`DwarfShape`) — 짧은 다리 · 긴 통짜 몸통 · 굵은 팔다리 · 각진 어깨(승모근) · 키 1.65m.
// 머리는 **넓은 네모 머리(`Male_BroadSquare` — 오크와 같은 바탕) 한 벌**을 거리장으로 다시 빚고 통째로 줄인다(`DwarfHeadK`) —
// 두툼한 눈썹뼈 · 크고 둥근 코 · 둥근 볼 · 작은 사람 귀 · 콧수염 · 빚은 눈(사람 눈 · 입 판은 안 쓴다 — `DwarfHeadParts`).
//
// 개체마다 고르는 것(`PawnLook`의 `Head.Pick.{축}.{벌}` — 셋은 **따로 고른다**, 사용자가 물었다):
//   - **수염**(Bushy · Braids · Forked · Long · Box · Stache) — 수염 없는 벌은 없다(사용자: *"수염이 없는건 좀 그런데 수염이 좀 적고 콧수염이
//     있는 거는 괜찮은 거 같아"* → Stache).
//   - **머리카락**(Bald · Fringe · Long · Braid · Crest) — 볏(Crest)은 머리 가운데 선을 따라 **같은 높이로** 잇는다(*"중간에 갑자기 높아져"* —
//     바탕 머리는 정수리가 한 점으로 모여 그것을 부풀리면 기둥이 솟는다 → 머리를 감싸는 타원에서 잰다).
//   - **눈썹**(Bushy · Heavy · Droop · Angry · Tufted).
// 털 색은 `MarkingLook`(머리카락 · 수염 · 눈썹이 한 색) · 쇠고리(`.Ring`)는 안 칠한다.
//
// 그림 확인은 파이썬 미리보기 `Tools/ModelLab/dwarf/dwarf.py`(머리) · `body.py`(몸)로 했다 — 같은 수식(단위 m · 원점은 머리 한복판 · +z 앞).
// **수치를 고치면 그 그림과 어긋난다 — 같이 고칠 것.**
public static partial class ChibiPawnCreator
{
    // ── 몸 ──────────────────────────────────────────────────────────────────
    //                                                                  밑     엉덩이  허리   가슴   어깨      깊이   손     팔K    다리K  몸통K  팔굵기 다리굵기 어깨둥글림
    static readonly Shape DwarfShape = new("Dwarf", "드워프", "Male", new[] { 0.32f, 0.36f, 0.39f, 0.43f, 0.45f }, 0.96f, 0.36f, 0.95f, 0.55f, 1.03f, 1.45f, 1.10f, 0.28f);

    // 머리 크기 — 사람 머리의 0.88(사용자: *"키는 1.65로 좀 높이고 대가리 크기를 좀 줄여"*). 밑동(목 관절)은 그대로 두고 줄인다.
    const float DwarfHeadK = 0.88f;
    const string DwarfPreview = "Chibi_Dwarf";   // 보기 몸 프리팹(`3_Prefab/Pawn/Dwarf/`) — `Pawn/3`이 굽는다
    const string DwarfBasis = "Male_BroadSquare";
    const float DwarfHeadStep = 0.006f, DwarfFineStep = 0.0035f, DwarfHairStep = 0.0055f;

    static readonly string[] DwarfBeards = { "Bushy", "Braids", "Forked", "Long", "Box", "Stache" };
    static readonly string[] DwarfHairs  = { "Fringe", "Long", "Braid", "Crest" };   // + 민머리(Bald)
    static readonly string[] DwarfBrows  = { "Bushy", "Heavy", "Droop", "Angry", "Tufted" };

    static List<Part> _dwarfHead;

    // 수염 · 머리카락(늘어진 털)의 바깥 선 — 땅 이기기를 켠 얼굴 털 선(`EnsureMaterials`의 `OutlineOuterBeard` · 몸통 꼭대기에 먹히지 않게).
    const string HangLine = "OutlineOuterBeard";

    // 드워프 머리의 틀 — 오크 틀(눈 · 코 · 입 높이 · 얼굴 앞면)에 얼굴 앞면 z 셋을 더한다.
    sealed class DwarfFrame
    {
        public readonly OrcFrame O;
        public readonly float ZE, ZN, ZM, EY, MY, NY, EX, ER, H;
        public DwarfFrame(HeadShape h)
        {
            O = new OrcFrame(h);
            ZE = O.Front(O.EyeT); ZN = O.Front(O.NoseT); ZM = O.Front(O.MouthT);
            EY = O.EY; MY = O.MY; NY = O.NY; EX = O.EX; ER = O.ER; H = O.Ht;
        }
    }

    // ── 도구 ────────────────────────────────────────────────────────────────

    static float Lock(Vector3 p, Vector3[] pts, float r0, float r1, float k = 0.008f)
    {
        float d = 9f; int n = pts.Length - 1;
        for (int i = 0; i < n; i++)
            d = SMin(d, Seg(p, pts[i], pts[i + 1], r0 + (r1 - r0) * i / n, r0 + (r1 - r0) * (i + 1) / n), k);
        return d;
    }

    // 가닥을 감는 쇠고리 — 미리보기 `band`.
    static float DwarfRing(Vector3 p, Vector3 a, Vector3 b, float r)
    {
        var ab = b - a; var c = (a + b) * 0.5f; float L = ab.magnitude; var ax = ab / L;
        var q = p - c; float h = Vector3.Dot(q, ax);
        float rr = Mathf.Sqrt(Mathf.Max(q.sqrMagnitude - h * h, 0f));
        return SMax(rr - r, Mathf.Abs(h) - L * 0.5f, 0.004f);
    }

    static float RingsOn(Vector3 p, Vector3[] pts, int[] at, float r)
    {
        float d = 9f;
        foreach (int i in at) d = Mathf.Min(d, DwarfRing(p, pts[i], pts[i + 1], r));
        return d;
    }

    static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    static float AngleOf(Vector3 p) => Mathf.Abs(Mathf.Atan2(p.x, p.z)) * Mathf.Rad2Deg;
    static float Strands(Vector3 p, float a = 0.006f, float f = 75f) => a * Mathf.Sin(p.x * f + 0.6f * Mathf.Sin(p.y * 20f));   // 세로 결
    static float Fuzz(Vector3 p, float a = 0.0025f) => a * Mathf.Sin(p.x * 90f + p.y * 40f);                                      // 눈썹 결

    // 수염이 가슴을 타고 흐르게 — 미리보기 몸(셔츠 타원) 앞면. 머리 좌표라 실제 몸통과 대략 맞는다(머리는 목 관절에 붙어 흔들린다).
    static float Chest(float x, float y)
    {
        if (y >= -0.42f) return -0.02f;
        float q = 1f - (x / 0.52f) * (x / 0.52f) - ((y + 0.84f) / 0.42f) * ((y + 0.84f) / 0.42f);
        return -0.02f + 0.37f * Mathf.Sqrt(Mathf.Max(q, 0f));
    }
    static Vector3 Drape(float x, float y, float z, float r) => new(x, y, Mathf.Max(z, Chest(x, y) + r * 0.8f));
    static Vector3[] DwarfPath(float x0, float y0, float y1, float z0, float r, int n, float dx = 0f)
    {
        var pts = new Vector3[n + 1];
        for (int i = 0; i <= n; i++) pts[i] = Drape(x0 + dx * i / n, y0 + (y1 - y0) * i / n, z0, r);
        return pts;
    }

    // ── 얼굴 ────────────────────────────────────────────────────────────────

    static float DwarfBase(Vector3 p, DwarfFrame f) => OrcBase(p, f.O);

    static float DwarfBrowRidge(Vector3 p, DwarfFrame f)   // 두툼한 눈썹뼈 — 눈이 그 밑에 묻힌다
        => Seg(Mirror(p), V3(0.02f, f.EY + 0.055f, f.ZE + 0.005f), V3(f.EX + f.ER * 1.3f, f.EY + 0.06f, f.ZE - 0.04f), 0.034f, 0.028f);

    static float DwarfNose(Vector3 p, DwarfFrame f)        // 크고 둥근 코 — 짧은 콧대 끝에 공 · 콧방울
    {
        var m = Mirror(p);
        float bridge = Seg(p, V3(0, f.EY + 0.03f, f.ZE - 0.005f), V3(0, f.NY + 0.015f, f.ZN + 0.035f), 0.022f, 0.034f);
        float bulb = Ell(p, V3(0, f.NY + 0.005f, f.ZN + 0.045f), V3(0.062f, 0.056f, 0.052f));
        float wing = Sph(m, V3(0.052f, f.NY - 0.012f, f.ZN + 0.018f), 0.030f);
        float d = SMin(SMin(bridge, bulb, 0.02f), wing, 0.015f);
        return SMax(d, -Ell(m, V3(0.024f, f.NY - 0.045f, f.ZN + 0.040f), V3(0.012f, 0.008f, 0.012f)), 0.004f);
    }

    static float DwarfCheeks(Vector3 p, DwarfFrame f) => Sph(Mirror(p), V3(0.15f, f.NY + 0.005f, f.ZN - 0.055f), 0.065f);   // 둥근 볼

    static float DwarfEar(Vector3 p, DwarfFrame f)         // 작은 사람 귀
        => Ell(Mirror(p), V3(f.O.R(f.O.EyeT) * f.O.W - 0.005f, f.EY - 0.02f, -0.03f), V3(0.025f, 0.065f, 0.045f));

    static float DwarfFace(Vector3 p, DwarfFrame f)
    {
        float d = DwarfBase(p, f);
        d = SMin(d, DwarfBrowRidge(p, f), 0.03f);
        d = SMin(d, DwarfCheeks(p, f), 0.04f);
        d = SMin(d, DwarfNose(p, f), 0.012f);
        return SMin(d, DwarfEar(p, f), 0.01f);
    }

    // ── 눈 — 작고 깊다(눈썹뼈 밑에 묻힌다) ─────────────────────────────────────
    //
    // **얼굴 살 위 얇은 껍데기 중 눈 자리만**(오크 문신과 같은 길) — 처음엔 고정 z의 타원이라 옆에서 보면 볼 앞 허공에 떴다(2026-11-03 게임 화면,
    // 사용자: *"앞에서 볼때는 좋은데 옆에서 보면 이상해"*). 앞 반만(같은 x · y의 뒤통수에 안 생기게).

    static float OnFace(Vector3 p, DwarfFrame f, float region, float outward, float inner = 0.010f)
    {
        if (region > 0.03f) return region;   // 자리에서 멀면 얼굴을 안 잰다(빠르게)
        float fd = DwarfFace(p, f);
        float shell = Mathf.Max(fd - outward, -(fd + inner));
        return SMax(Mathf.Max(shell, region), (f.ZE - 0.12f) - p.z, 0.002f);
    }
    static float DwarfEyes(Vector3 p, DwarfFrame f)
        => OnFace(p, f, Ell(Mirror(p), V3(f.EX, f.EY - 0.005f, f.ZE), V3(f.ER * 0.72f, f.ER * 0.80f, 0.3f)), 0.004f);
    static float DwarfGlint(Vector3 p, DwarfFrame f)
        => OnFace(p, f, Ell(Mirror(p), V3(f.EX - f.ER * 0.22f, f.EY + f.ER * 0.2f, f.ZE), V3(f.ER * 0.22f, f.ER * 0.22f, 0.3f)), 0.007f);

    // (x, y)에서 얼굴 앞면의 z — 앞에서 뒤로 걸어 들어가 처음 닿는 곳.
    static float SurfZ(float x, float y, DwarfFrame f)
    {
        float z = 0.6f;
        for (int i = 0; i < 300; i++)
        {
            float d = DwarfFace(V3(x, y, z), f);
            if (d < 0.0005f) break;
            z -= Mathf.Max(d * 0.8f, 0.0005f);
        }
        return z;
    }

    // ── 눈썹 ────────────────────────────────────────────────────────────────

    // 눈썹 한 벌 — 마디(면에 얹은 점) · 굵기 · 결 · (터럭 뭉치 밑동, 번호).
    sealed class BrowSpec
    {
        public Vector3[] Pts; public float R0, R1, K, Fuzz;
        public (Vector3 b0, int i)[] Tufts = Array.Empty<(Vector3, int)>();
    }

    // **마디는 (x, y)만 적고 z는 그 자리의 얼굴 면에 얹는다**(가닥 중심을 반지름의 1/4 묻는다) — 처음엔 고정 z라 옆에서 이마 앞에 뿔처럼 떴다
    // (2026-11-03 게임 화면 → 미리보기 v13). 면을 재는 데 시간이 드니 한 번만 잰다.
    static Dictionary<string, BrowSpec> DwarfBrowSpecs(DwarfFrame f)
    {
        float EY = f.EY, EX = f.EX, ER = f.ER;
        BrowSpec Spec(Vector2[] xy, float r0, float r1, float k, float fuzz)
        {
            int n = xy.Length - 1;
            var pts = new Vector3[xy.Length];
            for (int i = 0; i < xy.Length; i++)
                pts[i] = V3(xy[i].x, xy[i].y, SurfZ(xy[i].x, xy[i].y, f) - (r0 + (r1 - r0) * i / Mathf.Max(n, 1)) * 0.25f);
            return new BrowSpec { Pts = pts, R0 = r0, R1 = r1, K = k, Fuzz = fuzz };
        }
        var specs = new Dictionary<string, BrowSpec>
        {
            ["Bushy"]  = Spec(new[] { V2(0.03f, EY + 0.085f), V2(EX, EY + 0.10f), V2(EX + ER * 1.5f, EY + 0.12f), V2(EX + ER * 2.3f, EY + 0.16f) }, 0.028f, 0.008f, 0.008f, 0.0025f),
            ["Heavy"]  = Spec(new[] { V2(0.006f, EY + 0.078f), V2(EX * 0.6f, EY + 0.088f), V2(EX + ER * 0.6f, EY + 0.090f), V2(EX + ER * 1.9f, EY + 0.080f) }, 0.036f, 0.024f, 0.012f, 0.003f),
            ["Droop"]  = Spec(new[] { V2(0.03f, EY + 0.08f), V2(EX, EY + 0.105f), V2(EX + ER * 1.7f, EY + 0.085f), V2(EX + ER * 2.4f, EY + 0.03f), V2(EX + ER * 2.8f, EY - 0.03f) }, 0.030f, 0.012f, 0.012f, 0.003f),
            ["Angry"]  = Spec(new[] { V2(0.022f, EY + 0.048f), V2(EX * 0.7f, EY + 0.075f), V2(EX + ER * 0.5f, EY + 0.105f), V2(EX + ER * 1.8f, EY + 0.135f) }, 0.032f, 0.012f, 0.010f, 0.0025f),
            ["Tufted"] = Spec(new[] { V2(0.03f, EY + 0.085f), V2(EX, EY + 0.095f), V2(EX + ER * 1.5f, EY + 0.095f) }, 0.026f, 0.020f, 0.01f, 0.0025f),
        };
        var roots = new[] { 0.06f, EX + 0.005f, EX + ER * 1.3f };
        var tufts = new (Vector3, int)[roots.Length];
        for (int i = 0; i < roots.Length; i++) tufts[i] = (V3(roots[i], EY + 0.10f, SurfZ(roots[i], EY + 0.10f, f) - 0.002f), i);
        specs["Tufted"].Tufts = tufts;
        return specs;
    }

    // 숱 많은(Bushy) · 굵은 일자(Heavy) · 늙은 처진(Droop) · 성난(Angry) · 터럭이 솟은(Tufted).
    static float DwarfBrow(BrowSpec b, Vector3 p)
    {
        var m = Mirror(p);
        float d = Lock(m, b.Pts, b.R0, b.R1, b.K);
        foreach (var (b0, i) in b.Tufts)
            d = SMin(d, Seg(m, b0, b0 + V3(0.020f + 0.012f * i, 0.045f + 0.008f * i, -0.008f - 0.006f * i), 0.020f, 0.005f), 0.012f);
        return d + Fuzz(m, b.Fuzz);
    }

    // ── 콧수염 · 수염 ───────────────────────────────────────────────────────

    static float DwarfMustache(Vector3 p, DwarfFrame f)    // 코 밑에서 입을 덮고 양옆으로 처진다
        => Lock(Mirror(p), new[] { V3(0.015f, f.NY - 0.050f, f.ZN + 0.030f), V3(0.07f, f.NY - 0.065f, f.ZN + 0.020f), V3(0.12f, f.MY - 0.005f, f.ZM + 0.020f),
                                   V3(0.155f, f.MY - 0.06f, f.ZM - 0.01f), V3(0.16f, f.MY - 0.11f, f.ZM - 0.04f) }, 0.036f, 0.012f, 0.015f);

    static float DwarfSideburns(Vector3 p, DwarfFrame f)   // 귀 앞에서 턱으로 — 수염과 잇는다
        => Seg(Mirror(p), V3(0.275f, f.EY + 0.02f, 0f), V3(0.24f, f.MY - 0.06f, 0.13f), 0.045f, 0.06f);

    // 수염 껍데기 두께 — **귀 쪽으로 갈수록 얇아진다**(같은 두께로 자르면 귀 앞에 세로 판이 섰다 — 2026-11-03 옆모습 → 미리보기 v13).
    static float BeardThick(float ang, float th) => th * (0.15f + 0.85f * (1f - Ease((ang - 45f) / 40f)));

    // 턱 · 볼 아래를 덮는 수염 덩어리 — 윗선은 가운데가 입 위 · 옆으로 갈수록 올라 볼 아래를 지나 귀 앞으로. 귀 앞까지만.
    static float DwarfJawMass(Vector3 p, DwarfFrame f)
    {
        float ang = AngleOf(p);
        float d = SMax(DwarfBase(p, f) - BeardThick(ang, 0.045f), p.y - (f.MY + 0.035f + 1.1f * p.x * p.x), 0.02f);
        d = SMax(d, (ang - 85f) * 0.004f, 0.01f);
        return SMin(d, DwarfSideburns(p, f), 0.03f);
    }

    // 가닥 자리(가슴을 타고 흐른다) — 미리보기와 같은 셈이라 한 번만 잰다.
    sealed class DwarfBeardPaths
    {
        public readonly Vector3[] BraidC, BraidM, BraidS, Forked, Long;
        public DwarfBeardPaths(DwarfFrame f)
        {
            float y0 = f.MY - 0.17f;
            BraidC = DwarfPath(0f,     y0, y0 - 0.50f, f.ZM - 0.01f, 0.034f, 7, 0f);
            BraidM = DwarfPath(0.075f, y0, y0 - 0.42f, f.ZM - 0.01f, 0.034f, 7, 0.075f * 0.15f);
            BraidS = DwarfPath(0.15f,  y0, y0 - 0.32f, f.ZM - 0.01f, 0.034f, 7, 0.15f * 0.15f);
            Forked = DwarfPath(0.07f, f.MY - 0.14f, f.MY - 0.50f, f.ZM - 0.03f, 0.05f, 5, 0.01f);
            Long   = DwarfPath(0f, f.MY - 0.12f, -1.02f, f.ZM - 0.06f, 0.12f, 6);
        }
    }

    // (수염, 쇠고리) — 고리가 없으면 9.
    static (float hair, float ring) DwarfBeard(string style, Vector3 p, DwarfFrame f, DwarfBeardPaths bp)
    {
        var m = Mirror(p); float MY = f.MY, ZM = f.ZM;
        switch (style)
        {
            case "Bushy":    // 덥수룩한 큰 수염 — 둥근 아랫단 · 양옆 쇠고리
            {
                float d = DwarfJawMass(p, f);
                d = SMin(d, Ell(p, V3(0, MY - 0.17f, ZM - 0.02f), V3(0.31f, 0.25f, 0.17f)) + Strands(p), 0.06f);
                Vector3 a = V3(0.17f, MY - 0.06f, ZM + 0.03f), b = V3(0.18f, MY - 0.12f, ZM + 0.025f);
                d = SMin(d, Seg(m, a, b + V3(0, -0.05f, 0), 0.05f, 0.045f), 0.02f);
                return (SMin(d, DwarfMustache(p, f), 0.012f), DwarfRing(m, a, b, 0.055f));
            }
            case "Braids":   // 짧은 덩어리 + 땋아 내린 다섯 가닥(가운데가 가장 길다) · 가닥마다 고리 둘
            {
                float d = DwarfJawMass(p, f);
                d = SMin(d, Ell(p, V3(0, MY - 0.11f, ZM - 0.05f), V3(0.25f, 0.14f, 0.15f)) + Strands(p), 0.05f);
                float rg = 9f;
                foreach (var (pts, side) in new[] { (bp.BraidC, p), (bp.BraidM, m), (bp.BraidS, m) })
                {
                    d = SMin(d, OrcChain(side, pts, 0.036f, 0.026f, 0.010f), 0.02f);
                    rg = Mathf.Min(rg, RingsOn(side, pts, new[] { 3, 6 }, 0.031f));
                }
                return (SMin(d, DwarfMustache(p, f), 0.012f), rg);
            }
            case "Forked":   // 짧은 수염 + 땋아 내린 두 가닥(쇠고리)
            {
                float d = DwarfJawMass(p, f);
                d = SMin(d, Ell(p, V3(0, MY - 0.11f, ZM - 0.08f), V3(0.23f, 0.13f, 0.14f)) + Strands(p), 0.05f);
                d = SMin(d, OrcChain(m, bp.Forked, 0.050f, 0.036f), 0.025f);
                return (SMin(d, DwarfMustache(p, f), 0.012f), RingsOn(m, bp.Forked, new[] { 4 }, 0.044f));
            }
            case "Long":     // 배까지 늘어뜨린 긴 수염 — 가슴을 타고 흐르고 끝을 고리로 묶었다(앞뒤로 납작 — 깊이 1/1.7)
            {
                float d = DwarfJawMass(p, f);
                var q = V3(p.x, p.y, p.z * 1.7f);
                var flat = Array.ConvertAll(bp.Long, t => V3(t.x, t.y, t.z * 1.7f));
                float lng = Lock(q, flat, 0.24f, 0.06f, 0.03f) / 1.7f + Strands(p, 0.004f, 45f);
                d = SMin(d, lng, 0.06f);
                var tip = bp.Long[^2];
                return (SMin(d, DwarfMustache(p, f), 0.012f), DwarfRing(p, tip + V3(0, 0.04f, 0), tip + V3(0, -0.02f, 0), 0.075f));
            }
            case "Box":      // 짧게 다듬은 네모 수염
            {
                float d = DwarfJawMass(p, f);
                d = SMin(d, RBox(p, V3(0, MY - 0.12f, ZM - 0.07f), V3(0.20f, 0.10f, 0.12f), 0.06f), 0.05f);
                return (SMin(d, DwarfMustache(p, f), 0.012f), 9f);
            }
            case "Stache":   // 턱을 따라 짧게 깎은 수염 + 크게 기른 콧수염(끝이 턱 밑까지 처지고 고리로 묶었다)
            {
                float ang = AngleOf(p);
                float d = SMax(DwarfBase(p, f) - BeardThick(ang, 0.022f), p.y - (MY + 0.02f + 1.1f * p.x * p.x), 0.02f);
                d = SMax(d, (ang - 85f) * 0.004f, 0.01f);
                d = SMin(d, Ell(p, V3(0, MY - 0.10f, ZM - 0.06f), V3(0.13f, 0.07f, 0.08f)) + Strands(p, 0.003f), 0.04f);   // 턱끝 한 줌
                var pts = new[] { V3(0.015f, f.NY - 0.048f, f.ZN + 0.032f), V3(0.08f, f.NY - 0.062f, f.ZN + 0.024f), V3(0.14f, MY, ZM + 0.025f),
                                  V3(0.175f, MY - 0.07f, ZM + 0.005f), V3(0.18f, MY - 0.15f, ZM - 0.02f), V3(0.175f, MY - 0.23f, ZM - 0.03f) };
                float st = Lock(m, pts, 0.046f, 0.022f, 0.015f) + Strands(p, 0.003f, 90f);
                return (SMin(d, st, 0.012f), DwarfRing(m, pts[4], pts[5], 0.030f));
            }
        }
        return (9f, 9f);
    }

    // ── 머리카락 ────────────────────────────────────────────────────────────

    // 머리 껍데기 + 아랫선(`low`) 위만 — 정수리는 따로 누른다(껍데기를 그냥 부풀리면 정수리가 한 점이라 꼭지가 솟는다).
    static float Scalp(Vector3 p, DwarfFrame f, float th, float low)
    {
        float d = DwarfBase(p, f) - th;
        d = SMax(d, p.y - (f.H * 0.5f + th * 0.4f), 0.05f);
        return SMax(d, low - p.y, 0.02f);
    }

    // 이마 → 귀 위 → 목덜미로 내려가는 선.
    static float Hairline(float ang, float frontY, DwarfFrame f)
    {
        float side = f.EY + 0.05f, nape = -0.30f;
        return ang < 90f ? frontY + (side - frontY) * Ease((ang - 20f) / 70f) : side + (nape - side) * Ease((ang - 95f) / 70f);
    }

    static (float hair, float ring) DwarfHair(string style, Vector3 p, DwarfFrame f)
    {
        float ang = AngleOf(p);
        switch (style)
        {
            case "Fringe":   // 정수리는 벗어지고 귀 뒤 · 뒤통수만 — 윗단이 들쭉날쭉(곧은 선이면 판으로 읽힌다) · 목덜미로 갈수록 숱이 준다
            {
                float top = 0.02f + 0.10f * Ease((ang - 100f) / 70f) + 0.018f * Mathf.Sin(ang * Mathf.Deg2Rad * 14f);
                float d = Scalp(p, f, 0.030f, -0.30f);
                d = SMax(d, p.y - top, 0.02f);
                float frontEdge = 100f - 20f * Ease(-p.y / 0.25f);                                   // 앞선은 비스듬히 — 아래로 갈수록 귀 쪽으로
                d = SMax(d, (frontEdge - ang) * 0.004f, 0.02f);
                d += 0.03f * Ease((-0.16f - p.y) / 0.12f);
                d = SMax(d, -(p.y + 0.27f + 0.025f * Mathf.Sin(ang * Mathf.Deg2Rad * 18f)), 0.015f);
                return (d + 0.006f * Mathf.Sin(ang * Mathf.Deg2Rad * 40f + p.y * 15f), 9f);
            }
            case "Long":     // 뒤로 넘긴 긴 머리 — 어깨 뒤까지 · 뒤는 가닥 다섯(한 판이면 벽이 된다)
            {
                float cap = Scalp(p, f, 0.035f, Hairline(ang, f.EY + 0.20f, f));
                var m = Mirror(p); float fall = 9f;
                foreach (var (x, L, mirrored) in new[] { (0f, -0.66f, false), (0.10f, -0.60f, true), (0.19f, -0.50f, true) })
                {
                    float z = -0.40f + 0.5f * x * x;
                    fall = SMin(fall, Lock(mirrored ? m : p, new[] { V3(x * 0.8f, 0.15f, z + 0.02f), V3(x, -0.10f, z - 0.06f), V3(x * 1.08f, -0.35f, z - 0.08f),
                                                                    V3(x * 1.12f, L, z - 0.04f) }, 0.085f, 0.045f, 0.02f), 0.012f);
                }
                float side = Lock(m, new[] { V3(0.24f, 0f, -0.15f), V3(0.28f, -0.25f, -0.16f), V3(0.31f, -0.46f, -0.14f) }, 0.07f, 0.05f, 0.03f);
                return (SMin(SMin(cap, fall, 0.08f), side, 0.05f) + Strands(p, 0.004f, 55f), 9f);
            }
            case "Braid":    // 뒤로 빗어 넘겨 굵게 한 가닥 땋았다
            {
                float cap = Scalp(p, f, 0.032f, Hairline(ang, f.EY + 0.20f, f)) + Strands(p, 0.004f, 90f);
                var pts = new[] { V3(0, 0.05f, -0.36f), V3(0, -0.08f, -0.40f), V3(0, -0.20f, -0.42f), V3(0, -0.32f, -0.43f), V3(0, -0.44f, -0.42f), V3(0, -0.56f, -0.40f), V3(0, -0.66f, -0.38f) };
                return (SMin(cap, OrcChain(p, pts, 0.07f, 0.05f, 0.012f), 0.05f), RingsOn(p, pts, new[] { 2, 5 }, 0.062f));
            }
            case "Crest":    // 옆은 밀고 가운데 볏 — 머리 가운데 선을 따라 같은 높이로(이마 · 목덜미에서만 잦아든다) · 뒤로 짧은 꽁지
            {
                float th = Mathf.Atan2(p.z, p.y);                                  // 정수리 0 · 앞(+z)으로 + · 뒤로 -
                const float T0 = 0.80f, T1 = -1.95f;
                float rise = Ease((T0 - th) / 0.45f);
                float hm = 0.10f * rise * Ease((th - T1) / 0.55f) + 0.010f * Mathf.Sin(th * 26f) * rise;
                // 머리를 감싸는 타원에서 잰다(위 머리말).
                float g = 1f + hm / 0.32f;
                var q = V3(p.x / (0.31f * g), p.y / (0.345f * g), p.z / (0.30f * g));
                float fin = SMax((q.magnitude - 1f) * 0.30f, Mathf.Abs(p.x) - 0.06f, 0.025f);
                fin = SMax(fin, Mathf.Max(th - T0, T1 - th) * 0.2f, 0.02f);
                float tail = Lock(p, new[] { V3(0, 0.02f, -0.34f), V3(0, -0.10f, -0.40f), V3(0, -0.24f, -0.40f) }, 0.06f, 0.035f, 0.02f);
                return (SMin(fin, tail, 0.04f), DwarfRing(p, V3(0, -0.02f, -0.355f), V3(0, -0.07f, -0.385f), 0.05f));
            }
        }
        return (9f, 9f);
    }

    // ── 부위 ────────────────────────────────────────────────────────────────

    // 드워프 머리 부위 — 사람 머리(`HeadParts`) 대신 받는다. 목 관절 자리(`Head`) · 눈 판 · 투구는 사람과 같은 틀을 `DwarfHeadK`로 줄인 것.
    static List<Part> DwarfHeadParts(Dictionary<string, Mesh> hairs)
    {
        if (_dwarfHead != null) return _dwarfHead;
        var h = Array.Find(Heads, x => x.Name == DwarfBasis);
        var f = new DwarfFrame(h);
        var bp = new DwarfBeardPaths(f);
        const float k = DwarfHeadK;
        // 밑동(목)을 그대로 두고 줄인다 — 한복판이 줄어든 반만큼 내려온다.
        var pos = HeadPos(h) - Vector3.up * (h.Size.y * (1f - k) * 0.5f);
        var parts = new List<Part>();
        Func<Vector3, float> face = p => DwarfFace(p / k, f) * k;

        // 거리장은 줄이기 전 좌표로 적었다 — 재는 자리(`p / k`)와 거리(`* k`)를 같이 줄인다. 상자도 줄인다.
        // `relax` — **살 · 털은 편다**(큰 짐승과 같은 까닭 — 바깥 선은 정점을 법선으로 밀어 그리므로 표면의 잔물결이 선의 가시가 된다. 2026-11-03,
        // 사용자: *"수염 외곽선 잘 처리해줘봐"*). 작은 쇠고리 · 눈은 펴면 무너지니 안 편다.
        Mesh Sculpt(string name, Func<Vector3, float> sdf, Bounds b, float step, float keep, bool relax = false)
        {
            var surf = SurfaceNets(p => sdf(p / k) * k, new Bounds(b.center * k, b.size * k), step, face, p => p, 0.03f, 4f, keep);
            if (relax) Relax(surf.Verts, surf.Tris, 6);
            return SaveSkull($"Chibi_Dwarf_{name}", surf);
        }
        void Add(string name, Mesh mesh, string mat, string line, bool on = true)
            => parts.Add(new Part(name, mesh, pos, Vector3.one, mat, on, outerMat: line));

        parts.Add(new Part("Head", null, pos, h.Size * k, "HeadSkin", on: false));

        // 머리 살 — 이름이 머리형이라 `PawnLook`이 이 머리 하나를 고른다. 제 선을 끄고 바깥 선만(오크와 같은 까닭 — 파인 얼굴에서 껍데기가 살을 뚫는다).
        var hb = new Bounds(new Vector3(0f, -0.01f, 0.04f), new Vector3(h.Size.x + 0.08f, h.Size.y + 0.08f, h.Size.z + 0.24f));
        Add($"Head.Shape.{h.Name}", Sculpt("Head", p => DwarfFace(p, f), hb, DwarfHeadStep, 0.3f, relax: true), "SkinFine", "OutlineOuterFine");
        // **눈은 빚는다 — 사람 눈 · 입 판은 안 쓴다**(2026-11-03 첫 화면, 사용자: *"이미지에서는 이렇지 않았는데"*). 판은 사람 머리 크기라 두툼한
        // 눈썹뼈 · 볼에 묻혀 점 둘만 보였다. 미리보기처럼 작고 깊은 검은 눈 + 흰 반짝임(해골 머리처럼 판 없이 돈다 — 입은 콧수염이 덮는다).
        var eyeB = new Bounds(new Vector3(0f, f.EY, f.ZE - 0.01f), new Vector3(2f * (f.EX + f.ER) + 0.04f, 0.10f, 0.08f));
        Add("Head.Dwarf.Eyes",  Sculpt("Eyes",  p => DwarfEyes(p, f),  eyeB, 0.002f, 0.5f),  "NoseDark", null);
        Add("Head.Dwarf.Glint", Sculpt("Glint", p => DwarfGlint(p, f), eyeB, 0.0015f, 0.5f), "Bone", null);

        // 눈썹 — 다섯 중 씨앗이 하나(`Head.Pick.Brow.*`).
        var browB = new Bounds(new Vector3(0f, f.EY + 0.07f, f.ZE - 0.02f), new Vector3(0.52f, 0.28f, 0.22f));
        var brows = DwarfBrowSpecs(f);
        foreach (var st in DwarfBrows)
        {
            var spec = brows[st];
            Add($"Head.Pick.Brow.{st}", Sculpt($"Brow_{st}", p => DwarfBrow(spec, p), browB, DwarfFineStep, 0.4f, relax: true), "HairFine", "OutlineOuterFine", on: st == "Bushy");
        }

        // 수염 — 여섯 중 하나(없는 벌은 없다). 쇠고리는 같은 축의 조각(`.Ring` — `MarkingLook`이 안 칠한다).
        var beardB = new Bounds(new Vector3(0f, -0.49f, 0.16f), new Vector3(0.82f, 1.26f, 0.74f));
        foreach (var st in DwarfBeards)
        {
            string s = st;
            Add($"Head.Pick.Beard.{s}", Sculpt($"Beard_{s}", p => DwarfBeard(s, p, f, bp).hair, beardB, DwarfHairStep, 0.35f, relax: true), "HairFine", HangLine, on: s == "Bushy");
            if (s != "Box")
                Add($"Head.Pick.Beard.{s}.Ring", Sculpt($"Beard_{s}_Ring", p => DwarfBeard(s, p, f, bp).ring, beardB, DwarfFineStep * 1.3f, 0.4f),
                    "Metal", HangLine, on: s == "Bushy");
        }

        // 머리카락 — 민머리(Bald)는 렌더러가 없어 조건에만 적는다(`@Hair.!Bald`).
        var hairB = new Bounds(new Vector3(0f, -0.10f, -0.11f), new Vector3(0.84f, 1.32f, 1.04f));
        foreach (var st in DwarfHairs)
        {
            string s = st;
            Add($"Head.Pick.Hair.{s}@Hair.!Bald", Sculpt($"Hair_{s}", p => DwarfHair(s, p, f).hair, hairB, DwarfHairStep, 0.35f, relax: true), "HairFine", HangLine, on: false);
            if (s is "Braid" or "Crest")
                Add($"Head.Pick.Hair.{s}.Ring@Hair.!Bald", Sculpt($"Hair_{s}_Ring", p => DwarfHair(s, p, f).ring, hairB, DwarfFineStep * 1.3f, 0.4f),
                    "Metal", HangLine, on: false);
        }

        // 투구 — 사람 투구 껍데기를 이 머리 크기로.
        var capPos = pos + new Vector3(0f, 0.015f, -0.01f) * k;
        foreach (var fit in HairFits)
            parts.Add(new Part($"Head.Helmet.Cap.Shell.{fit}", hairs[$"{fit}.Helmet"], capPos, h.Size * HelmetScale * k, "Helmet", on: false));

        _dwarfHead = parts;
        return parts;
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    // 보기 몸 하나 — 종족 데이터 · 씨족이 아직 없어 **어디서도 안 가리키는** 프리팹(`Dwarf/Chibi_Dwarf`)에 굽는다.
    // 방랑자처럼 원본(`Source`)을 베껴 `Model`만 갈고, 있으면 내용만 덮어 guid를 지킨다. 종족을 들일 때 역할 프리팹이 이 몸을 그대로 받는다.
    static bool DressDwarf(Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases, Dictionary<string, Mesh> hairs)
    {
        _dwarfHead = null;
        var parts = new Dictionary<string, List<Part>> { ["Male"] = DwarfHeadParts(hairs) };
        string dp = PawnPrefabPaths.PathOf(DwarfPreview);
        PawnPrefabPaths.Ensure(PawnPrefabPaths.FolderOf(DwarfPreview));
        if (System.IO.File.Exists(dp)) { System.IO.File.Copy(Source, dp, true); AssetDatabase.ImportAsset(dp, ImportAssetOptions.ForceUpdate); }
        else                           { AssetDatabase.CopyAsset(Source, dp); AssetDatabase.ImportAsset(dp); }
        var dMats = OutfitMaterials(mats, "Dwarf", new Color32(0x5E, 0x48, 0x34, 0xFF), new Color32(0x3A, 0x3A, 0x44, 0xFF), null, null);
        return Dress(dp, DwarfShape, parts, dMats, atlases, isSource: false, DwarfLook);
    }

    // **드워프만 다시 굽는다**(2026-11-03, 사용자: *"에디터를 드워프만 바꾸는데 모든 모델을 건드는 건 좀 아닌거 같아"*) — `Pawn/3`은 모든 폰을
    // 다시 굽는다. 여기는 드워프 메시 · 프리팹만: 얼굴 아틀라스 · 투구 껍데기는 구워 둔 것을 읽고(없으면 `Pawn/3` 먼저), 재질은 같은 값으로 맞춘다.
    // 옛 드워프 메시(이번에 안 만든 `Chibi_Dwarf_`)만 치운다 — 다른 폰의 메시는 안 건드린다.
    [MenuItem("DungeonColony/Pawn/10. Create Dwarf Only (드워프만 — 다른 폰은 안 건드린다)")]
    public static void CreateDwarfOnly()
    {
        var toon = Shader.Find(ToonShader); var face = Shader.Find(FaceShader); var outer = Shader.Find(OuterShader);
        if (toon == null || face == null || outer == null) { Debug.LogError("[드워프] 셰이더를 못 찾았다."); return; }
        var atlases = new Dictionary<string, ChibiFaceAtlas>();
        foreach (var kind in new[] { "Eyes", "Mouths" })
        {
            var a = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>($"{MatDir}/Chibi_{kind}Atlas.asset");
            if (a == null) { Debug.LogError($"[드워프] 얼굴 아틀라스({kind})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return; }
            atlases[kind] = a;
        }
        var hairs = new Dictionary<string, Mesh>();
        foreach (var fit in HairFits)
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/Chibi_Helmet_{fit}.asset");
            if (m == null) { Debug.LogError($"[드워프] 투구 껍데기(Chibi_Helmet_{fit})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return; }
            hairs[$"{fit}.Helmet"] = m;
        }
        var mats = EnsureMaterials(toon, face, outer, atlases);

        _saved.Clear();
        Heads = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        bool ok = DressDwarf(mats, atlases, hairs);

        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!_saved.Contains(path) && System.IO.Path.GetFileName(path).StartsWith("Chibi_Dwarf_")) AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.SaveAssets();
        if (ok) Debug.Log($"[드워프] {PawnPrefabPaths.PathOf(DwarfPreview)} — 머리 · 몸만 다시 구웠다(다른 폰은 그대로).");
    }

    // 드워프 — 머리는 빚은 머리 한 벌이다(눈 · 입 판 없음). 사람 머리 시절의 못 박음을 비운다. 털 색은 `MarkingLook`.
    // **프리팹에도 실제 얼굴을 켜 둔다**(고블린과 같은 까닭 — 고르기는 판에서만 돌아서 프리팹 · 씬 뷰에는 구울 때의 기본이 보인다).
    static void DwarfLook(GameObject root)
    {
        var look = root.GetComponent<PawnLook>();
        if (look == null) return;
        var so = new SerializedObject(look);
        foreach (var fld in new[] { "shapeOverride", "hairOverride", "earOverride", "noseOverride", "tuskOverride" })
            so.FindProperty(fld).stringValue = "";
        so.FindProperty("mouthOverride").stringValue = PawnLook.None;
        so.FindProperty("fittedTo").stringValue = "";
        so.ApplyModifiedPropertiesWithoutUndo();
        if (!root.TryGetComponent(out MarkingLook _)) root.AddComponent<MarkingLook>();
        look.Apply();
    }
}
