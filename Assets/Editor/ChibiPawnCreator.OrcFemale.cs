using System;
using System.Collections.Generic;
using UnityEngine;

// **여성 오크**(2026-11-03 — 파이썬 미리보기 `orc_work/female/`에서 옮겼다 · 같은 수식 · 단위 m · 원점은 머리 한복판 · +z 앞).
//
// - **머리 바탕은 사람 여성 머리(`Female_BroadSoft`)** — 남성 오크는 `Male_BroadSquare`(`OrcBasis`). 옆선 · 눈 · 코 높이는 그 머리의 값이고
//   **입 높이만 제 것(0.26)**이다(사람 여성 표 0.299는 오크 얼굴에서 높아 보였다 — 사용자가 골랐다).
// - 얼굴 — 얇은 눈썹 선반(덜 노려본다) · 작은 턱(입 − 0.10 높이) · 덜 내민 아랫입술(입술 색은 살의 0.72배 — 정점 색) · 짧은 엄니 · 속눈썹 둘.
// - **몸은 사람 체형 `HulkFemale`**(사용자: *"원래 Female몸에서 몸을 더 키운 여성용 hulk"*) — 다리 굵기는 `DimsOf`가 엉덩이 반폭에서 내므로
//   엉덩이를 낮춰 같이 좁혔다(여성 엉덩이 = 같은 체격 남성 규칙에서 이 체형만 벗어난다 · 사용자가 골랐다). 방랑자 몸 목록(`Shapes`)에는 안 넣는다.
// - **머리 열**(`Head.Pick.Hair.*` — 씨앗이 고른다): 포니테일 다섯(A · B · A2 · B2 · C — 사용자가 판마다 골랐다)과 아직 다듬지 않은 다섯
//   (볏 · 양 갈래 · 땋은 머리 · 뾰족 단발 · 볏 + 꼬리 — 사용자: *"지금 상태로 넘어갔다가 별로면 다시 고칠 것"*).
//   조각마다 머리카락 · 가죽(`.Leather`) · 쇠(`.Metal`) · 반삭(`.Buzz` — 살과 머리색 사이) · 짧은 머리(`.Short` — 반삭 자리를 머리색으로).
//   색은 `MarkingLook`이 칠한다. 좌표는 박지 않고 머리 표면에서 뽑는다(`OnHead` · `Proj`).
// - 귀는 남성과 같은 셋(`OrcEar`)이고 귀마다 구리 고리 셋(`.Rings`)이 뒷선을 따라 달린다.
//
// **수치를 고치면 미리보기 그림과 어긋난다.**
public static partial class ChibiPawnCreator
{
    const string OrcFemaleBasis = "Female_BroadSoft";
    const float  OrcFemaleMouthT = 0.26f, OrcFemaleEyeS = 0.167f, OrcLipShade = 0.72f;

    // 밑 · 엉덩이 · 허리 · 가슴 · 어깨 · 깊이 · 손 — 미리보기 `body_cmp.py`의 B.
    static readonly Shape HulkFemale = new("HulkFemale", "여성 헐크", "Female", new[] { 0.26f, 0.26f, 0.24f, 0.34f, 0.26f }, 0.85f, 0.29f);

    static List<Part> _orcFemaleHead;

    // ── 얼굴 ────────────────────────────────────────────────────────────────

    // 선반이 얇고 덜 노려본다 — 바깥 끝이 살짝 올라간다.
    static float OrcFBrow(Vector3 p, OrcFrame o)
    {
        var m = Mirror(p); float z = o.Front(o.EyeT + 0.12f);
        var a = new Vector3(0.035f, o.EY + o.ER * 1.45f + 0.012f, z + 0.002f);
        var b = new Vector3(o.EX + o.ER * 1.15f, o.EY + o.ER * 1.75f + 0.03f, z - 0.035f);
        return Seg(m, a, b, 0.020f, 0.014f);
    }

    // 작은 턱 — 입보다 0.10 낮게.
    static float OrcFJaw(Vector3 p, OrcFrame o)
        => RBox(p, new Vector3(0f, o.Y(o.MouthT - 0.10f), o.Front(0.12f) - 0.14f), new Vector3(0.14f, 0.055f, 0.10f), Mathf.Min(0.07f, 0.055f * 0.95f));

    // 아랫입술은 덜 내민다.
    static float OrcFLip(Vector3 p, OrcFrame o)
        => Capsule(LipBend(p), new Vector3(-0.10f, o.MY - 0.022f, o.LipZ - 0.012f), new Vector3(0.10f, o.MY - 0.022f, o.LipZ - 0.016f), 0.024f);

    static float OrcFHead(Vector3 p, OrcFrame o)
    {
        float d = OrcBase(p, o);
        d = SMin(d, OrcFJaw(p, o), 0.07f);
        d = Mathf.Max(d, -o.Ht * 0.5f - p.y);
        d = SMin(d, OrcFBrow(p, o), 0.035f);
        d = SMin(d, OrcNose(p, o) + 0.004f, 0.015f);   // 코는 조금 작게
        float lip = OrcFLip(p, o);
        float tight = SMin(d, lip, 0.02f), fill = SMin(d, lip, 0.06f);
        float w = Mathf.Clamp01((o.MY - 0.02f - p.y) / 0.04f); w = w * w * (3f - 2f * w);
        return tight + (fill - tight) * w;
    }

    // 입술 색 — 아랫입술 겉만.
    static float OrcFLipSkin(Vector3 p, OrcFrame o) => SMax(OrcFLip(p, o) - 0.003f, -(p.z - (o.LipZ - 0.02f)), 0.004f);

    // 짧은 엄니 — 입술 위로 조금만.
    static float OrcFTusks(Vector3 p, OrcFrame o)
    {
        var m = Mirror(p); float z = o.LipZ - 0.008f;
        var a = new Vector3(0.095f, o.MY - 0.025f, z); var b = new Vector3(0.105f, o.MY + 0.025f, z + 0.010f); var c = new Vector3(0.112f, o.MY + 0.050f, z + 0.004f);
        return Mathf.Min(Seg(m, a, b, 0.019f, 0.013f), Seg(m, b, c, 0.013f, 0.003f));
    }

    // 눈 바깥 위에 짧은 속눈썹 두 가닥.
    static float OrcFLash(Vector3 p, OrcFrame o)
    {
        var m = Mirror(p); float z = o.Front(o.EyeT) + 0.004f; float EX = o.EX, EY = o.EY, ER = o.ER;
        float d = Seg(m, new Vector3(EX + ER * 0.55f, EY + ER * 0.85f, z), new Vector3(EX + ER * 1.25f, EY + ER * 1.25f, z - 0.012f), 0.009f, 0.003f);
        return Mathf.Min(d, Seg(m, new Vector3(EX + ER * 0.85f, EY + ER * 0.45f, z - 0.004f), new Vector3(EX + ER * 1.45f, EY + ER * 0.70f, z - 0.016f), 0.008f, 0.003f));
    }

    // ── 귀고리 — 귓바퀴 뒷선을 따라 셋(귀 크기를 따라 줄인다) ─────────────────────

    static float Ring(Vector3 p, Vector3 c, Vector3 axis, float R, float r)
    {
        var q = p - c; float h = Vector3.Dot(q, axis);
        float rad = Mathf.Sqrt(Mathf.Max(q.sqrMagnitude - h * h, 0f)) - R;
        return Mathf.Sqrt(rad * rad + h * h) - r;
    }

    static float OrcFEarrings(Vector3 p, OrcFrame o, float s)
    {
        float f = OrcEarFlare * Mathf.Deg2Rad;
        var ua = new Vector3(Mathf.Sin(f), 0f, -Mathf.Cos(f)); var na = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
        var ear0 = new Vector3(o.R(o.EyeT) * o.W - 0.01f, o.EY - 0.03f, -0.035f);
        var m = Mirror(p); float d = 9f;
        foreach (var (u, v) in new[] { (0.040f, -0.035f), (0.075f, 0.035f), (0.098f, 0.105f) })
            d = Mathf.Min(d, Ring(m, ear0 + ua * (u * s + 0.012f) + new Vector3(0f, v * s, 0f), na, 0.034f * s, 0.009f));
        return d;
    }

    // ── 머리 표면에서 뽑는 자리 ─────────────────────────────────────────────

    // 방위각 a(0 = 정면 · 90 = 오른쪽 · 180 = 뒤) · 높이 y의 머리 표면 점 — out만큼 바깥으로.
    static Vector3 OnHead(OrcFrame o, float aDeg, float y, float outward = 0f)
    {
        float uy = y / o.Ht, r = o.R(uy + 0.5f), a = aDeg * Mathf.Deg2Rad, sh = uy < 0f ? o.F * 2f * uy * uy : 0f;
        float x = r * Mathf.Sin(a) * o.W, z = (r * Mathf.Cos(a) + sh) * o.D;
        var n = new Vector3(x / (o.W * o.W), 0f, (z / o.D - sh) / o.D);
        n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.zero;
        return new Vector3(x, y, z) + n * outward;
    }

    // 머리 가운데에서 q 쪽으로 쏴 닿는 표면 점 + out.
    static Vector3 Proj(OrcFrame o, Vector3 q, float outward)
    {
        var d = q.normalized; float lo = 0.02f, hi = 0.8f;
        for (int i = 0; i < 40; i++) { float mid = (lo + hi) * 0.5f; if (OrcBase(d * mid, o) < 0f) lo = mid; else hi = mid; }
        return d * (lo + outward);
    }

    // 머리 표면을 따라 내려오다, 머리가 안으로 말려 들어가는 높이부터는 곧게 떨어지는 가닥 점들.
    static Vector3[] Down(OrcFrame o, float a, float y0, float y1, int n, float out0, float out1, float flare = 0f)
    {
        var pts = new Vector3[n]; float bestR = -1f, bx = 0f, bz = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)(n - 1), y = y0 + (y1 - y0) * k;
            var q = OnHead(o, a, Mathf.Max(y, -o.Ht * 0.45f), out0 + (out1 - out0) * k);
            float hr = Mathf.Sqrt(q.x * q.x + q.z * q.z);
            if (hr >= bestR) { bestR = hr; bx = q.x; bz = q.z; } else q = new Vector3(bx, q.y, bz);
            pts[i] = new Vector3(q.x * (1f + flare * k), y, q.z * (1f + flare * k));
        }
        return pts;
    }

    static Vector3[] Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int n)
    {
        var o = new Vector3[n + 1];
        for (int i = 0; i <= n; i++) { float t = i / (float)n, s = 1f - t; o[i] = a * (s * s * s) + b * (3f * s * s * t) + c * (3f * s * t * t) + d * (t * t * t); }
        return o;
    }

    static float Interp(float x, float[] xs, float[] ys)
    {
        if (x <= xs[0]) return ys[0];
        for (int i = 1; i < xs.Length; i++)
            if (x <= xs[i]) return Mathf.Lerp(ys[i - 1], ys[i], (x - xs[i - 1]) / (xs[i] - xs[i - 1]));
        return ys[ys.Length - 1];
    }

    static float AngDeg(Vector3 p) => Mathf.Abs(Mathf.Atan2(p.x, p.z)) * Mathf.Rad2Deg;

    // ── 거리장 나무 — 상자 밖이면 재지 않는다(머리 하나에 다발이 수십이라 칸마다 다 재면 굽기가 몇 분) ─────────
    //
    // `SMin(d, b, k)`는 b ≥ d + k이면 정확히 d다 — 상자까지의 거리가 그보다 멀면 건너뛰어도 값이 같다. 상자는 반지름 · 안쪽 섞임 여유를 품는다.
    sealed class FNode
    {
        public Func<Vector3, float> Leaf;
        public List<(FNode n, float k)> Kids;
        public bool Mirrored;
        public Vector3 Min, Max;   // 제 좌표(접은 좌표면 접은 쪽)

        public static FNode Of(Func<Vector3, float> f, Vector3 min, Vector3 max) => new() { Leaf = f, Min = min, Max = max };
        public static FNode Group(bool mirrored = false) => new() { Kids = new List<(FNode, float)>(), Mirrored = mirrored, Min = Vector3.one * 9f, Max = Vector3.one * -9f };

        public FNode Add(FNode n, float k)
        {
            Kids.Add((n, k));
            var (mn, mx) = n.Outer();
            Min = Vector3.Min(Min, mn - Vector3.one * k); Max = Vector3.Max(Max, mx + Vector3.one * k);
            return this;
        }

        // 부모 좌표에서의 상자 — 접는 무리는 양쪽으로 편다.
        public (Vector3, Vector3) Outer()
        {
            if (!Mirrored) return (Min, Max);
            float ax = Mathf.Max(Mathf.Abs(Min.x), Mathf.Abs(Max.x));
            return (new Vector3(-ax, Min.y, Min.z), new Vector3(ax, Max.y, Max.z));
        }

        public float BoxDist(Vector3 p)
        {
            var (mn, mx) = Outer();
            return Vector3.Max(Vector3.Max(mn - p, p - mx), Vector3.zero).magnitude;
        }

        public float Eval(Vector3 p)
        {
            if (Mirrored) p = new Vector3(Mathf.Abs(p.x), p.y, p.z);
            if (Leaf != null) return Leaf(p);
            float d = 9f;
            foreach (var (n, k) in Kids)
            {
                if (d < 8f && n.BoxDist(p) >= d + k) continue;
                d = SMin(d, n.Eval(p), k);
            }
            return d;
        }

        // 굽는 함수 — 나무 상자에서 먼 칸은 상자 거리만(부호만 맞으면 된다).
        public Func<Vector3, float> Field(float pad = 0.03f) => p => { float b = BoxDist(p); return b > pad ? b : Eval(p); };

        public Bounds Bounds(float pad)
        {
            var (mn, mx) = Outer(); var b = new Bounds(); b.SetMinMax(mn - Vector3.one * pad, mx + Vector3.one * pad); return b;
        }
    }

    // 끝으로 갈수록 가늘어지는 다발 한 가닥(마디마다 굵기).
    static FNode FLock(Vector3[] pts, float[] radii, float k = 0.006f)
    {
        var mn = Vector3.one * 9f; var mx = Vector3.one * -9f; float rmax = 0f;
        foreach (var q in pts) { mn = Vector3.Min(mn, q); mx = Vector3.Max(mx, q); }
        foreach (var r in radii) rmax = Mathf.Max(rmax, r);
        var pad = Vector3.one * (rmax + k);
        return FNode.Of(p =>
        {
            float d = 9f;
            for (int i = 0; i + 1 < pts.Length; i++) d = SMin(d, Seg(p, pts[i], pts[i + 1], radii[i], radii[i + 1]), k);
            return d;
        }, mn - pad, mx + pad);
    }

    static FNode FLock(Vector3[] pts, float r0, float r1, float k = 0.006f)
    {
        var radii = new float[pts.Length]; int n = pts.Length - 1;
        for (int i = 0; i <= n; i++) radii[i] = r0 + (r1 - r0) * i / n;
        return FLock(pts, radii, k);
    }

    static FNode FChain(Vector3[] pts, float r0, float r1)
    {
        var mn = Vector3.one * 9f; var mx = Vector3.one * -9f; float L = 0f;
        foreach (var q in pts) { mn = Vector3.Min(mn, q); mx = Vector3.Max(mx, q); }
        for (int i = 0; i + 1 < pts.Length; i++) L = Mathf.Max(L, (pts[i + 1] - pts[i]).magnitude);
        var pad = Vector3.one * (Mathf.Max(r0, r1, L * 0.62f) + 0.012f);
        return FNode.Of(p => OrcChain(p, pts, r0, r1), mn - pad, mx + pad);
    }

    static FNode FCuff(Vector3 a, Vector3 b, float r)
    {
        var pad = Vector3.one * (r + 0.01f);
        return FNode.Of(p => OrcCuff(p, a, b, r), Vector3.Min(a, b) - pad, Vector3.Max(a, b) + pad);
    }

    static FNode FSeg(Vector3 a, Vector3 b, float ra, float rb)
    {
        var pad = Vector3.one * Mathf.Max(ra, rb);
        return FNode.Of(p => Seg(p, a, b, ra, rb), Vector3.Min(a, b) - pad, Vector3.Max(a, b) + pad);
    }

    // 머리 겉에 붙는 층(덮개 · 띠 · 받침 · 반삭)의 상자.
    static FNode FShell(OrcFrame o, Func<Vector3, float> f)
        => FNode.Of(f, new Vector3(-o.W * 0.5f - 0.07f, -o.Ht * 0.5f - 0.07f, -o.D * 0.5f - 0.07f),
                       new Vector3(o.W * 0.5f + 0.07f, o.Ht * 0.5f + 0.07f, o.D * 0.5f + 0.15f));

    // ── 머리 겉 층 ──────────────────────────────────────────────────────────

    // 가운데 띠만 남긴 머리(옆은 민다).
    static float FStrip(Vector3 p, OrcFrame o, float half, float frontY, float backY, float th = 0.022f)
    {
        float k = Mathf.Abs(Mathf.Atan2(p.x, p.z)) / Mathf.PI;
        float line = frontY + (backY - frontY) * k + 0.8f * p.x * p.x;
        float above = p.y - line, w = Mathf.Clamp01(above / 0.02f);
        float d = OrcBase(p, o) - th * w;
        return SMax(SMax(d, -above, 0.005f), Mathf.Abs(p.x) - half, 0.02f);
    }

    static readonly float[] CapLineA = { 0f, 20f, 45f, 75f, 100f, 118f, 140f, 180f };
    static readonly float[] CapLineY = { 0.17f, 0.16f, 0.12f, 0.10f, 0.10f, -0.10f, -0.24f, -0.27f };

    // 덮개 — 관자놀이에서 내려오고 귀 뒤에서 목덜미까지 덮는다 + 가운데 가르마 앞머리(미리보기의 마지막 판 그대로).
    static float FCap(Vector3 p, OrcFrame o, float th)
    {
        float above = p.y - Interp(AngDeg(p), CapLineA, CapLineY);
        float k = Mathf.Clamp01(above / 0.025f); k = k * k * (3f - 2f * k);
        float hair = SMax(OrcBase(p, o) - th * k, -above, 0.006f);
        // 앞머리 — 가운데 가르마 · 양쪽으로 커튼
        float t = Mathf.Atan2(p.x, p.z);
        float hem = 0.165f - 0.115f * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(t) / 0.85f), 0.8f);
        float shell = OrcBase(p, o) - (0.018f + 0.012f * Mathf.Clamp01((p.y - 0.08f) / 0.08f));
        float bang = SMax(shell, hem - p.y, 0.006f);
        bang = SMax(bang, Mathf.Abs(t) - 1.15f, 0.03f);
        bang = SMax(bang, -p.z, 0.02f);
        return SMin(hair, bang, 0.012f);
    }

    // 다발 밑을 받치는 머리 층 — 다발 사이로 두피가 비쳐 보이지 않게. 선(방위각 → 높이) 위만 덮는다.
    static float FUnder(Vector3 p, OrcFrame o, float[] la, float[] ly, float th = 0.052f)
    {
        float above = p.y - Interp(AngDeg(p), la, ly);
        float k = Mathf.Clamp01(above / 0.03f); k = k * k * (3f - 2f * k);
        return SMax(OrcBase(p, o) - th * k, -above, 0.006f);
    }

    // 반삭이 덮는 아랫선 — 관자놀이는 눈썹 위에서 시작해 귀 위를 넘고 귀 뒤에서 목덜미 쪽으로(귀 앞 구레나룻은 귓불 높이까지).
    static readonly float[] BuzzLoA = { 56f, 62f, 72f, 86f, 93f, 112f, 124f, 180f };
    static readonly float[] BuzzLoY = { 0.30f, 0.12f, 0.00f, -0.07f, 0.19f, 0.19f, -0.08f, -0.12f };

    // 반삭 — 민 자리를 살 위 얇은 층으로 덮어 짧은 머리가 비치게 한다.
    static float FBuzz(Vector3 p, OrcFrame o, float[] la, float[] ly)
    {
        float ang = AngDeg(p);
        float top = Interp(ang, la, ly) + 0.02f, lo = Interp(ang, BuzzLoA, BuzzLoY);
        float band = SMax(lo - p.y, p.y - top, 0.008f);
        return SMax(SMax(OrcBase(p, o) - 0.004f, band, 0.004f), (56f - ang) * 0.004f, 0.01f);
    }

    // ── 머리 열 ─────────────────────────────────────────────────────────────

    sealed class FemHair
    {
        public string Name;
        public FNode Hair, Leather, Metal;
        public Func<Vector3, float> Buzz;   // 반삭 층(없으면 null)
        public bool BuzzFull;               // 반삭 자리를 머리색으로(짧은 머리)
    }

    static readonly float[] TailR = { 0.030f, 0.050f, 0.074f, 0.080f, 0.070f, 0.056f, 0.042f, 0.026f, 0.012f };

    // 이마 · 관자놀이 · 뒷머리에서 묶는 자리로 당겨 모인 다발 — 가운데가 두피에서 puff만큼 떠 둥글게 부푼다.
    static FNode FGathered(OrcFrame o, Vector3 R, List<Vector3> starts, float puff, float r0, float r1, int n = 8)
    {
        var g = FNode.Group();
        foreach (var s in starts)
        {
            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                pts[i] = Proj(o, s * (1f - t) + R * t, 0.012f + puff * Mathf.Sin(Mathf.PI * Mathf.Min(t * 1.25f, 1f)) * (1f - 0.35f * t));
            }
            g.Add(FLock(pts, r0, r1), 0.028f);
        }
        return g;
    }

    // 포니테일 — 옆은 밀고 · 머리를 끌어모아 정수리 뒤에서 끈으로 묶어 크게 휘어 내리는 꼬리.
    static FemHair FPony(OrcFrame o, string name, List<(float a, float y)> startsAb, float[] la, float[] ly, bool buzz, bool full = false)
    {
        var R = OnHead(o, 180f, 0.30f, 0.05f);
        var starts = new List<Vector3>();
        foreach (var (a, y) in startsAb) starts.Add(OnHead(o, a, y));
        var hair = FNode.Group();
        hair.Add(FShell(o, p => FUnder(p, o, la, ly)), 0.001f);
        hair.Add(FGathered(o, R, starts, 0.055f, 0.050f, 0.026f), 0.02f);
        // 속 다발 — 겉보다 낮고 굵게 같은 길을 지나 다발 사이 골 밑을 막는다.
        hair.Add(FGathered(o, R, starts, 0.030f, 0.060f, 0.030f), 0.01f);
        foreach (var (dx, dz, s) in new[] { (-0.06f, 0.00f, 0.85f), (-0.03f, -0.03f, 1.0f), (0.0f, 0.02f, 1.08f), (0.03f, -0.02f, 1.0f), (0.06f, 0.01f, 0.85f), (0.0f, -0.06f, 0.95f) })
        {
            var pts = Bez(R, R + new Vector3(dx * 0.3f, 0.27f, -0.05f), R + new Vector3(dx * 0.9f, 0.24f, -0.44f), R + new Vector3(dx * 2.6f, -0.55f * s, -0.52f + dz), 8);
            hair.Add(FLock(pts, TailR), 0.03f);
        }
        var tie = FCuff(R + new Vector3(0f, 0.012f, -0.002f), R + new Vector3(0f, 0.05f, -0.010f), 0.050f);   // 끈 — 잘록한 목보다 살짝만 굵다
        return new FemHair { Name = name, Hair = hair, Leather = tie, Buzz = buzz ? p => FBuzz(p, o, la, ly) : null, BuzzFull = full };
    }

    // 볏 — 이마에서 목덜미까지 뒤로 누운 뾰족한 가시들.
    static FNode FCrest(OrcFrame o, int n, float length, float frontY, float backY, float half)
    {
        var g = FNode.Group();
        g.Add(FShell(o, p => FStrip(p, o, half, frontY, backY - 0.06f)), 0.001f);
        var path = new List<Vector3> { OnHead(o, 0f, frontY + 0.03f), OnHead(o, 0f, 0.27f), OnHead(o, 0f, 0.32f), new Vector3(0f, o.Ht * 0.5f - 0.004f, 0f) };
        foreach (var y in new[] { 0.31f, 0.26f, 0.19f, 0.11f, backY }) path.Add(OnHead(o, 180f, y));
        var cum = new float[path.Count];
        for (int i = 1; i < path.Count; i++) cum[i] = cum[i - 1] + (path[i] - path[i - 1]).magnitude;
        for (int i = 0; i < n; i++)
        {
            float s = cum[cum.Length - 1] * (i + 0.5f) / n; int j = 1;
            while (j < cum.Length - 1 && cum[j] < s) j++;
            var b = Vector3.Lerp(path[j - 1], path[j], (s - cum[j - 1]) / Mathf.Max(cum[j] - cum[j - 1], 1e-6f));
            var outward = (b - new Vector3(0f, -0.05f, 0f)).normalized;
            float L = length * (0.75f + 0.45f * Mathf.Sin(Mathf.PI * (i + 0.5f) / n));
            foreach (int side in new[] { -1, 1 })
            {
                float Ls = L * (side < 0 ? 1f : 0.85f);
                var off = new Vector3(0.028f * side, 0f, 0f);
                var tip = b + off * 1.6f + outward * Ls + new Vector3(0f, 0f, -0.10f);
                g.Add(FSeg(b + off - outward * 0.02f, tip, 0.058f, 0.004f), 0.018f);
            }
        }
        return g;
    }

    // 머리를 덮고(앞 가르마) 귀 뒤 높이 묶어 양옆으로 퍼지는 꼬리 둘 · 쇠고리.
    static FemHair FTwinTails(OrcFrame o)
    {
        var hair = FNode.Group();
        hair.Add(FShell(o, p =>
        {
            float full = FCap(p, o, 0.026f);
            return p.z > 0.05f ? SMax(full, -(Mathf.Abs(p.x) - 0.008f), 0.006f) : full;
        }), 0.001f);
        var T = OnHead(o, 112f, 0.22f, 0.06f);
        var tails = FNode.Group(mirrored: true);
        foreach (var (dx, dz, s) in new[] { (0.00f, 0.03f, 1.0f), (0.04f, -0.03f, 0.92f), (0.07f, 0.02f, 0.85f), (0.02f, -0.06f, 0.95f), (-0.02f, 0.0f, 0.9f) })
            tails.Add(FLock(Bez(T, T + new Vector3(0.12f, 0.08f, -0.03f), T + new Vector3(0.22f, -0.10f, -0.04f + dz), T + new Vector3(0.15f + dx, -0.58f * s, 0.02f + dz), 6), 0.070f, 0.012f), 0.025f);
        hair.Add(tails, 0.025f);
        var rc = T + new Vector3(0.045f, 0.012f, -0.008f); var ax = new Vector3(1f, 0.3f, -0.1f).normalized;
        var ring = FNode.Group(mirrored: true).Add(FNode.Of(p => Ring(p, rc, ax, 0.058f, 0.014f), rc - Vector3.one * 0.08f, rc + Vector3.one * 0.08f), 0.001f);
        return new FemHair { Name = "TwinTails", Hair = hair, Metal = ring };
    }

    // 머리를 덮고 등까지 풀어 내린 다발 + 관자놀이에서 어깨 앞으로 땋은 둘(가죽 감개 · 쇠 고리).
    static FemHair FLongBraids(OrcFrame o)
    {
        var hair = FNode.Group();
        hair.Add(FShell(o, p => FCap(p, o, 0.026f)), 0.001f);
        foreach (float a in new[] { 138f, 152f, 166f, 180f, 194f, 208f, 222f })
            hair.Add(FLock(Down(o, a, 0.26f, -0.64f - 0.05f * Mathf.Cos((a - 180f) * Mathf.Deg2Rad * 4f), 7, 0.03f, 0.06f), 0.065f, 0.018f), 0.03f);
        var b2 = OnHead(o, 92f, -0.10f, 0.06f);
        var bp = new[] { OnHead(o, 76f, 0.13f, 0.03f), OnHead(o, 84f, 0.03f, 0.05f), b2,
                         b2 + new Vector3(0.015f, -0.12f, 0.07f), b2 + new Vector3(0.02f, -0.24f, 0.12f), b2 + new Vector3(0.02f, -0.34f, 0.15f) };
        hair.Add(FNode.Group(mirrored: true).Add(FChain(bp, 0.040f, 0.030f), 0.001f), 0.012f);
        var leather = FNode.Group(mirrored: true)
            .Add(FCuff(bp[2] + new Vector3(0f, 0.02f, 0f), bp[2] + new Vector3(0.004f, -0.05f, 0.02f), 0.046f), 0.001f)
            .Add(FCuff(bp[4] + new Vector3(0f, 0.03f, -0.01f), bp[4] + new Vector3(0f, -0.03f, 0.02f), 0.040f), 0.001f);
        var metal = FNode.Group(mirrored: true).Add(FCuff(bp[3] + new Vector3(0f, 0.018f, -0.006f), bp[3] + new Vector3(0f, -0.018f, 0.006f), 0.044f), 0.001f);
        return new FemHair { Name = "LongBraids", Hair = hair, Leather = leather, Metal = metal };
    }

    // 머리를 덮고 이마를 덮는 뾰족 앞머리 · 턱까지 오는 옆 다발 · 위로 뻗친 끝.
    static FemHair FSpikyBob(OrcFrame o)
    {
        var hair = FNode.Group();
        hair.Add(FShell(o, p => FCap(p, o, 0.030f)), 0.001f);
        foreach (float a in new[] { -48f, -30f, -12f, 6f, 24f, 42f })
        {
            var s = OnHead(o, a, 0.25f, 0.025f); var e = OnHead(o, a * 1.1f, 0.07f + 0.012f * Mathf.Cos(a), 0.04f);   // cos는 미리보기 그대로(도 숫자를 라디안으로)
            hair.Add(FLock(new[] { s, (s + e) * 0.5f + s.normalized * 0.03f, e }, 0.045f, 0.005f), 0.012f);
        }
        foreach (float a in new[] { 72f, 100f, 128f, 155f, 180f, 205f, 232f, 260f, 288f })
        {
            float sa = Mathf.Sin(a * Mathf.Deg2Rad);
            var s = OnHead(o, a, 0.14f, 0.03f); var e = OnHead(o, a, -0.13f - 0.03f * sa * sa, 0.07f);
            var mid = (s + e) * 0.5f + new Vector3(s.x, 0f, s.z).normalized * 0.03f;
            hair.Add(FLock(new[] { s, mid, e }, 0.052f, 0.006f), 0.012f);
        }
        foreach (float a in new[] { 150f, 180f, 210f })
        {
            var b = OnHead(o, a, 0.29f);
            hair.Add(FSeg(b, b + new Vector3(0.03f * Mathf.Sin(a * Mathf.Deg2Rad), 0.10f, -0.07f), 0.04f, 0.004f), 0.012f);
        }
        return new FemHair { Name = "SpikyBob", Hair = hair };
    }

    // 짧은 볏 + 목덜미에서 가죽으로 감아 내린 꼬리.
    static FemHair FCrestTail(OrcFrame o)
    {
        var hair = FNode.Group();
        hair.Add(FCrest(o, 6, 0.12f, 0.20f, 0.0f, 0.07f), 0.001f);
        var R = OnHead(o, 180f, -0.02f, 0.04f);
        hair.Add(FLock(new[] { R, R + new Vector3(0f, -0.15f, -0.04f), R + new Vector3(0f, -0.32f, -0.05f), R + new Vector3(0f, -0.46f, -0.03f) }, 0.050f, 0.010f), 0.015f);
        var leather = FCuff(R + new Vector3(0f, -0.04f, -0.012f), R + new Vector3(0f, -0.12f, -0.035f), 0.054f);
        return new FemHair { Name = "CrestTail", Hair = hair, Leather = leather };
    }

    static List<FemHair> OrcFemaleHairs(OrcFrame o)
    {
        var front = new List<(float, float)>();
        foreach (float a in new[] { -60f, -40f, -20f, 0f, 20f, 40f, 60f }) front.Add((a, 0.15f + 0.00001f * a * a));
        var temple = new List<(float, float)> { (-88f, 0.19f), (88f, 0.19f), (-115f, 0.19f), (115f, 0.19f) };
        var back = new List<(float, float)>();
        foreach (float a in new[] { 125f, 140f, 155f, 170f, 190f, 205f, 220f, 235f }) back.Add((a, -0.13f));
        back.Add((-118f, -0.04f)); back.Add((118f, -0.04f));
        List<(float, float)> Cat(params List<(float, float)>[] ls) { var r = new List<(float, float)>(); foreach (var l in ls) r.AddRange(l); return r; }

        // 받침 층의 선 — (방위각, 높이). 묶는 자리 아래 뒤통수는 민다.
        float[] aTA = { 0f, 60f, 88f, 118f, 140f, 180f }, aTY = { 0.15f, 0.186f, 0.19f, 0.19f, 0.30f, 0.32f };
        float[] bSA = { 0f, 60f, 78f, 120f, 180f },       bSY = { 0.15f, 0.186f, 0.25f, 0.31f, 0.32f };
        float[] aBA = { 0f, 60f, 88f, 112f, 128f, 180f }, aBY = { 0.15f, 0.186f, 0.19f, 0.12f, -0.13f, -0.13f };
        float[] bBA = { 0f, 60f, 78f, 104f, 126f, 180f }, bBY = { 0.15f, 0.186f, 0.25f, 0.08f, -0.13f, -0.13f };

        return new List<FemHair>
        {
            FPony(o, "PonyA",  Cat(front, temple),       aTA, aTY, buzz: true),               // 이마 + 관자놀이 · 반삭
            FPony(o, "PonyB",  Cat(front),               bSA, bSY, buzz: true),               // 관자놀이 민 · 반삭
            FPony(o, "PonyA2", Cat(front, temple, back), aBA, aBY, buzz: false),              // A + 뒷머리
            FPony(o, "PonyB2", Cat(front, back),         bBA, bBY, buzz: true),               // B + 뒷머리 · 반삭
            FPony(o, "PonyC",  Cat(front, temple, back), aBA, aBY, buzz: true, full: true),   // A2 + 반삭 자리를 머리색으로
            new FemHair { Name = "Mohawk", Hair = FCrest(o, 9, 0.20f, 0.18f, 0.02f, 0.08f) },
            FTwinTails(o), FLongBraids(o), FSpikyBob(o), FCrestTail(o),
        };
    }

    // ── 부위 ────────────────────────────────────────────────────────────────

    // 여성 오크 머리 부위 — 남성 오크(`OrcHeadParts`)와 같은 틀(목 관절 자리 · 눈 판 · 투구)에 여성 얼굴 · 머리 열.
    static List<Part> OrcFemaleHeadParts(Dictionary<(string head, string feature), Mesh> features, Dictionary<string, Mesh> hairs)
    {
        if (_orcFemaleHead != null) return _orcFemaleHead;
        var h = Array.Find(Heads, x => x.Name == OrcFemaleBasis);
        var o = new OrcFrame(h, OrcFemaleMouthT, OrcFemaleEyeS);
        var pos = HeadPos(h);
        var parts = new List<Part>();
        Func<Vector3, float> head = p => OrcFHead(p, o);

        Surface Nets(Func<Vector3, float> f, Bounds b, float step, float keep) => SurfaceNets(f, b, step, head, p => p, 0.03f, 4f, keep);
        Mesh Sculpt(string name, Func<Vector3, float> f, Bounds b, float step, float keep) => SaveSkull($"Chibi_OrcF_{name}", Nets(f, b, step, keep));
        void Add(string name, Mesh mesh, string mat, string line, bool on = true) => parts.Add(new Part(name, mesh, pos, Vector3.one, mat, on, outerMat: line));

        parts.Add(new Part("Head", null, pos, h.Size, "HeadSkin", on: false));

        var hb = new Bounds(new Vector3(0f, -0.01f, 0.03f), new Vector3(h.Size.x + 0.06f, h.Size.y + 0.08f, h.Size.z + 0.22f));
        Add($"Head.Shape.{h.Name}", Sculpt("Head", head, hb, OrcHeadStep, 0.3f), "SkinFine", "OutlineOuterFine");
        foreach (var f in Features)
            parts.Add(new Part($"Head.Shape.{h.Name}.{f.Name}", features[(h.Name, f.Name)], pos, h.Size, "Face" + f.Kind, true, cell: f.DefaultCell, kind: f.Kind));

        var mouthB = new Bounds(new Vector3(0f, o.MY + 0.03f, o.LipZ), new Vector3(0.36f, 0.20f, 0.14f));
        Add("Head.Orc.Mouth", Sculpt("Mouth", p => OrcMouth(p, o), mouthB, OrcFineStep, 0.4f), "NoseDark", null);
        Add("Head.Orc.Teeth", Sculpt("Teeth", p => OrcTeeth(p, o), mouthB, OrcFineStep * 0.8f, 0.4f), "Bone", "OutlineOuterFine");
        Add("Head.Orc.Tusks", Sculpt("Tusks", p => OrcFTusks(p, o), mouthB, OrcFineStep, 0.4f), "Bone", "OutlineOuterFine");
        // 입술 색 — 빚은 살(`SkinShaded`)의 정점 색을 0.72배로 깎는다(개체 살색 `SkinLook`을 같이 탄다).
        {
            var lip = Nets(p => OrcFLipSkin(p, o), mouthB, OrcFineStep * 0.8f, 0.4f);
            for (int i = 0; i < lip.Colors.Count; i++) { var c = lip.Colors[i]; lip.Colors[i] = new Color(c.r * OrcLipShade, c.g * OrcLipShade, c.b * OrcLipShade, c.a); }
            Add("Head.Orc.Lips", SaveSkull("Chibi_OrcF_Lips", lip), "SkinShaded", "OutlineOuterFine");
        }
        var lashB = new Bounds(new Vector3(0f, o.EY + o.ER, o.Front(o.EyeT)), new Vector3(2f * (o.EX + o.ER * 1.6f) + 0.04f, o.ER * 1.6f + 0.05f, 0.08f));
        Add("Head.Orc.Lashes", Sculpt("Lashes", p => OrcFLash(p, o), lashB, OrcFineStep * 0.8f, 0.4f), "NoseDark", null);

        // 귀 셋 + 귀마다 구리 고리 셋.
        float earX = o.R(o.EyeT) * o.W;
        var earB = new Bounds(new Vector3(0f, o.EY + 0.04f, -0.10f), new Vector3(2f * earX + 0.30f, 0.44f, 0.34f));
        foreach (var (name, s, tip) in OrcEars)
        {
            float ss = s; var tt = tip;
            Add($"Head.Pick.Ear.{name}", Sculpt($"Ear_{name}", p => OrcEar(p, o, ss, tt), earB, OrcFineStep * 1.2f, 0.35f), "SkinFine", "OutlineOuterFine", on: name == "Big");
            Add($"Head.Pick.Ear.{name}.Rings", Sculpt($"Ear_{name}_Rings", p => OrcFEarrings(p, o, ss), earB, OrcFineStep, 0.4f), "Copper", "OutlineOuterFine", on: name == "Big");
        }

        // 머리 열 — 첫 벌만 켜 둔다(프리팹 · 씬 뷰). 판에서는 씨앗이 고른다.
        bool first = true;
        foreach (var fh in OrcFemaleHairs(o))
        {
            string n = $"Head.Pick.Hair.{fh.Name}";
            Add(n, Sculpt($"Hair_{fh.Name}", fh.Hair.Field(), fh.Hair.Bounds(0.03f), OrcHairStep, 0.35f), "HairFine", "OutlineOuterFine", on: first);
            if (fh.Leather != null)
                Add($"{n}.Leather", Sculpt($"Hair_{fh.Name}_Leather", fh.Leather.Field(), fh.Leather.Bounds(0.02f), OrcFineStep * 1.3f, 0.4f), "Leather", "OutlineOuterFine", on: first);
            if (fh.Metal != null)
                Add($"{n}.Metal", Sculpt($"Hair_{fh.Name}_Metal", fh.Metal.Field(), fh.Metal.Bounds(0.02f), OrcFineStep * 1.3f, 0.4f), "Metal", "OutlineOuterFine", on: first);
            if (fh.Buzz != null)
            {
                var bb = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(o.W + 0.06f, o.Ht + 0.06f, o.D + 0.10f));
                Add($"{n}.{(fh.BuzzFull ? "Short" : "Buzz")}", Sculpt($"Hair_{fh.Name}_Buzz", fh.Buzz, bb, OrcFineStep * 1.2f, 0.4f), "HairFine", null, on: first);
            }
            first = false;
        }

        var capPos = pos + new Vector3(0f, 0.015f, -0.01f);
        foreach (var fit in HairFits)
            parts.Add(new Part($"Head.Helmet.Cap.Shell.{fit}", hairs[$"{fit}.Helmet"], capPos, h.Size * HelmetScale, "Helmet", on: false));

        _orcFemaleHead = parts;
        return parts;
    }

    // **여성 오크 — 오크 역할마다 몸 하나 더**(여성 오우거 `DressOgreFemale`과 같은 길 — `Orc/Body/Enemy_Orc_Female`).
    static bool DressOrcFemale(Outfit o, string rolePath, Dictionary<string, List<Part>> femaleHead,
                               Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases)
        => DressFemaleCopy(o.Prefab, rolePath, fp => Dress(fp, HulkFemale, femaleHead, mats, atlases, isSource: false, o.Look));
}
