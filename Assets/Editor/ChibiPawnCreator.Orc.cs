using System;
using System.Collections.Generic;
using UnityEngine;

// **오크 얼굴 — 치비 머리에 오크의 생김새를 덧붙인다**(2026-11-01, 사용자: *"오크는 오크 전용 머리가 필요하다기보다는 차비 얼굴에서
// 오크의 특징이 되는 걸 추가했으면 좋겠어"*).
//
// 몸은 사람 치비(헐크)라 방어구가 그대로 보인다. 머리는 **넓은 네모 머리(`Male_BroadSquare`) 한 벌**을 바탕으로 거리장으로 다시 빚는다 —
// 눈썹뼈 · 넓은 아래턱 · 내민 아랫입술(입술과 턱 사이는 매끄럽게 메운다) · 아랫입술 **안에서** 솟는 엄니 · 콧방울이 위에 붙은 뾰족한 코.
// 눈 · 입 판(`Head.Shape.*.EyeL` …)은 사람 머리와 같은 판을 그 머리 자리에 둔다(표정이 그대로 돈다).
//
// 개체마다 고르는 것(`PawnLook`의 `Head.Pick.{축}.{벌}` — 씨앗이 고르고 못 박을 수도 있다):
//   - **귀**(Big · Mid · Small) — 사람 귀처럼 **앞 테두리만** 머리에 붙고 뒤로 30° 벌어져 위로 넓게 솟는다(밑은 떠 있다).
//   - **머리카락**(Bald · Topknot · WarKnot · Ponytail · Mohawk · Braids) — **묶은 머리만**(머리를 덮는 머리는 사용자가 뺐다).
//   - **수염**(None · Braid · TwinBraid · Shaggy · Goatee · Mutton).
//   - **문신 여섯 겹**(TatChin · TatClaw · TatBrow · TatCheek · TatFangs · TatTemple — 각각 On/Off) — 겹은 따로 굴려 적게도 많게도.
//     모양과 색은 따로다 — 색은 `MarkingLook`이 칠한다(머리카락 색도 거기).
//
// 그림 확인은 파이썬 미리보기(같은 수식 — 단위 m · 원점은 머리 한복판 · +z 앞)로 했다. **수치를 고치면 그 그림과 어긋난다.**
public static partial class ChibiPawnCreator
{
    const string OrcBasis = "Male_BroadSquare";
    const float OrcHeadStep = 0.007f, OrcFineStep = 0.0035f, OrcHairStep = 0.006f, OrcInkStep = 0.003f;

    static List<Part> _orcHead;

    // 오크 머리의 거리장 틀 — 머리 크기 · 기울임 · 이목구비 높이를 바탕 머리에서 받는다.
    sealed class OrcFrame
    {
        public HeadShape H;
        public float W, Ht, D, F, EyeT, MouthT, NoseT, EY, MY, NY, EX, ER, LipZ, ChinY, ChinZ, R0, EU;

        // `mouthT` — 머리 표보다 앞서는 입 높이(여성 오크 0.26 · 음수면 표 그대로) · `eyeS` — 미리보기의 눈 지름(눈썹뼈 · 속눈썹 자리만 쓴다).
        public OrcFrame(HeadShape h, float mouthT = -1f, float eyeS = 0.1578f)
        {
            H = h; W = h.Size.x; Ht = h.Size.y; D = h.Size.z; F = h.Forward;
            EyeT = h.EyeT; MouthT = mouthT >= 0f ? mouthT : h.MouthT; NoseT = h.NoseT;
            EY = (EyeT - 0.5f) * Ht; MY = (MouthT - 0.5f) * Ht; NY = (NoseT - 0.5f) * Ht;
            EX = h.Ref.EyeDx * W;
            ER = eyeS * 0.7f * W * 0.5f;           // 미리보기의 눈 반지름
            LipZ = Front(MouthT) + 0.040f;
            ChinY = MY - 0.10f; ChinZ = LipZ - 0.03f;
            EU = Mathf.Atan2(EX, Front(EyeT)) * InkR;
        }

        public float R(float t) => HeadRadius(Mathf.Clamp01(t), H);
        public float Y(float t) => (t - 0.5f) * Ht;
        public float Front(float t) { float yn = t - 0.5f; return (R(t) + (yn < 0f ? F * 2f * yn * yn : 0f)) * D; }
    }

    const float InkR = 0.30f;   // 문신 감는 좌표의 반지름(u = 방위각 × 이것)

    // ── 머리 ────────────────────────────────────────────────────────────────

    static float OrcBase(Vector3 p, OrcFrame o)
    {
        float ux = p.x / o.W, uy = p.y / o.Ht, uz = p.z / o.D;
        if (uy < 0f) uz -= o.F * 2f * uy * uy;
        float r = o.R(uy + 0.5f);
        float d = Mathf.Sqrt(ux * ux + uz * uz) - r;
        d = Mathf.Max(d, Mathf.Abs(uy) - 0.5f);
        return d * 0.6f;
    }

    // 눈 위 선반 — 안쪽 끝이 내려와 노려본다.
    static float OrcBrow(Vector3 p, OrcFrame o)
    {
        var m = Mirror(p); float z = o.Front(o.EyeT + 0.12f);
        var a = new Vector3(0.025f, o.EY + o.ER * 1.35f + 0.012f, z + 0.010f);
        var b = new Vector3(o.EX + o.ER * 1.25f, o.EY + o.ER * 1.55f + 0.03f, z - 0.03f);
        return Seg(m, a, b, 0.030f, 0.024f);
    }

    // 넓은 아래턱 — 치비 턱이 모이는 자리를 네모로 채운다.
    static float OrcJaw(Vector3 p, OrcFrame o)
        => RBox(p, new Vector3(0f, o.Y(0.13f), o.Front(0.12f) - 0.13f), new Vector3(0.215f, 0.085f, 0.13f), 0.075f);

    static Vector3 LipBend(Vector3 p) => new(p.x, p.y + 0.9f * p.x * p.x, p.z);   // 입꼬리가 처진다

    static float OrcLowerLip(Vector3 p, OrcFrame o)
        => Capsule(LipBend(p), new Vector3(-0.132f, o.MY - 0.025f, o.LipZ), new Vector3(0.132f, o.MY - 0.025f, o.LipZ), 0.038f);

    // 콧대가 눈썹 사이에서 내려와 끝이 선다 — 콧방울은 작게 위에(V자) · 콧구멍은 끝 밑.
    static float OrcNose(Vector3 p, OrcFrame o)
    {
        var m = Mirror(p); float z = o.Front(o.NoseT), ny = o.NY;
        float bridge = Seg(p, new Vector3(0f, ny + 0.09f, z - 0.030f), new Vector3(0f, ny - 0.005f, z + 0.040f), 0.018f, 0.024f);
        float tip    = Seg(p, new Vector3(0f, ny, z + 0.042f), new Vector3(0f, ny - 0.035f, z + 0.046f), 0.026f, 0.012f);
        float wing   = Ell(m, new Vector3(0.035f, ny + 0.010f, z + 0.010f), new Vector3(0.016f, 0.015f, 0.017f));
        float d = SMin(SMin(bridge, tip, 0.015f), wing, 0.020f);
        return SMax(d, -Ell(m, new Vector3(0.019f, ny - 0.008f, z + 0.031f), new Vector3(0.009f, 0.007f, 0.010f)), 0.004f);
    }

    static float OrcHead(Vector3 p, OrcFrame o)
    {
        float d = OrcBase(p, o);
        d = SMin(d, OrcJaw(p, o), 0.07f);
        d = Mathf.Max(d, -o.Ht * 0.5f - p.y);   // 턱 블렌드가 머리 바닥 밑으로 새지 않게(턱 밑 혹)
        d = SMin(d, OrcBrow(p, o), 0.035f);
        d = SMin(d, OrcNose(p, o), 0.015f);
        float lip = OrcLowerLip(p, o);
        // 입술 밑만 넓게 메운다(옆에서 입술과 턱이 이어진다) — 위(입 · 이)는 그대로.
        float tight = SMin(d, lip, 0.02f), fill = SMin(d, lip, 0.08f);
        float w = Mathf.Clamp01((o.MY - 0.02f - p.y) / 0.04f); w = w * w * (3f - 2f * w);
        return tight + (fill - tight) * w;
    }

    static float OrcMouth(Vector3 p, OrcFrame o)
        => RBox(LipBend(p), new Vector3(0f, o.MY + 0.010f, o.Front(o.MouthT) + 0.012f), new Vector3(0.12f, 0.012f, 0.02f), 0.01f);

    static float OrcTeeth(Vector3 p, OrcFrame o)
    {
        var q = LipBend(p); float z = o.LipZ + 0.006f, d = 9f;
        foreach (float x in new[] { -0.07f, -0.025f, 0.025f, 0.07f })
            d = Mathf.Min(d, Seg(q, new Vector3(x, o.MY + 0.004f, z), new Vector3(x * 1.02f, o.MY + 0.026f, z - 0.004f), 0.010f, 0.004f));
        return d;
    }

    // 엄니 — 아랫입술 **안에서** 솟는다(밑동은 입술이 덮는다). 얼굴에 박으면 붙여 놓은 것처럼 보였다.
    static float OrcTusks(Vector3 p, OrcFrame o)
    {
        var m = Mirror(p); float z = o.LipZ;
        var a = new Vector3(0.11f, o.MY - 0.03f, z); var b = new Vector3(0.128f, o.MY + 0.04f, z + 0.014f); var c = new Vector3(0.142f, o.MY + 0.085f, z + 0.006f);
        return Mathf.Min(Seg(m, a, b, 0.026f, 0.017f), Seg(m, b, c, 0.017f, 0.003f));
    }

    // ── 귀 — 앞 테두리만 붙는다 ─────────────────────────────────────────────
    //
    // 처음엔 잎을 밑동 한 점에 꽂았고(꼭지처럼 붙었다) → 밑동을 넓히자 턱까지 붙었다(사용자: *"사람귀는 밑이 아니라 옆이 붙어"*).
    // 귀 판은 제 평면(u = 붙은 테두리에서 뒤로 · v = 위)에 그린 윤곽이고, 판을 뒤로 `flare`도 벌린다.

    static readonly (string name, float s, Vector2 tip)[] OrcEars =
    {
        ("Big", 1.00f, new Vector2(0.12f, 0.20f)), ("Mid", 0.88f, new Vector2(0.11f, 0.18f)), ("Small", 0.76f, new Vector2(0.10f, 0.16f)),
    };
    const float OrcEarFlare = 30f, OrcEarThick = 0.022f;

    static float Taper2(float px, float py, Vector2 a, Vector2 b, float ra, float rb)
    {
        var ab = b - a; var q = new Vector2(px, py) - a;
        float k = Mathf.Clamp01(Vector2.Dot(q, ab) / ab.sqrMagnitude);
        return (q - ab * k).magnitude - Mathf.Lerp(ra, rb, k);
    }

    static float EarOutline(float u, float v, float s, Vector2 tip)
    {
        float body  = Taper2(u, v, new Vector2(0.045f * s, 0f), tip, 0.050f * s, 0.006f);
        float upper = Taper2(u, v, new Vector2(0.04f * s, 0.03f * s), new Vector2(tip.x * 0.75f, tip.y * 0.6f), 0.055f * s, 0.03f * s);
        float lu = (u - 0.035f * s) / (0.040f * s), lv = (v + 0.045f * s) / (0.040f * s);
        float lobe = Mathf.Sqrt(lu * lu + lv * lv) * 0.040f * s - 0.040f * s;
        return Mathf.Min(Mathf.Min(body, upper), lobe);
    }

    static float OrcEar(Vector3 p, OrcFrame o, float s, Vector2 tip)
    {
        float f = OrcEarFlare * Mathf.Deg2Rad;
        var ua = new Vector3(Mathf.Sin(f), 0f, -Mathf.Cos(f));   // 뒤로, 바깥으로 벌린
        var na = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));    // 판의 바깥 면
        var q = Mirror(p) - new Vector3(o.R(o.EyeT) * o.W - 0.01f, o.EY - 0.03f, -0.035f);
        float u = Vector3.Dot(q, ua), v = q.y, n = Vector3.Dot(q, na);
        float d2 = Mathf.Max(EarOutline(u, v, s, tip), -u - 0.03f);   // 앞 테두리는 머리 속으로 — 붙는 곳은 거기뿐
        float plate = Mathf.Max(d2, Mathf.Abs(n) - OrcEarThick);
        float cup = Mathf.Max(EarOutline(u - 0.006f, v - 0.004f, s * 0.78f, tip * 0.8f) + 0.004f, -(n - OrcEarThick * 0.3f));
        return SMax(plate, -cup, 0.004f);
    }

    // ── 머리카락 · 수염 ─────────────────────────────────────────────────────

    // 땋은 가닥 — 마디 타원을 줄줄이.
    static float OrcChain(Vector3 p, Vector3[] pts, float r0, float r1, float k = 0.012f)
    {
        float d = 9f; int n = pts.Length - 1;
        for (int i = 0; i < n; i++)
        {
            float r = r0 + (r1 - r0) * i / Mathf.Max(n - 1, 1);
            var c = (pts[i] + pts[i + 1]) * 0.5f;
            d = SMin(d, Ell(p, c, new Vector3(r, (pts[i + 1] - pts[i]).magnitude * 0.62f, r)), k);
        }
        return d;
    }

    // 가닥을 감는 얇은 뼈 띠 — 가닥보다 살짝만 굵고 짧다.
    static float OrcCuff(Vector3 p, Vector3 a, Vector3 b, float r)
    {
        var ab = b - a; var c = (a + b) * 0.5f; float L = ab.magnitude; var ax = ab / L;
        var q = p - c; float h = Vector3.Dot(q, ax);
        float rr = Mathf.Sqrt(Mathf.Max(q.sqrMagnitude - h * h, 0f));
        return SMax(rr - (r * 0.80f + 0.006f), Mathf.Abs(h) - L * 0.5f, 0.004f);
    }

    static Vector3 V3(float x, float y, float z) => new(x, y, z);

    // (머리카락, 뼈 장식) — 장식이 없으면 9.
    static (float hair, float deco) OrcHair(string style, Vector3 p)
    {
        switch (style)
        {
            case "Topknot":
            {
                float root = Seg(p, V3(0, 0.30f, -0.10f), V3(0, 0.40f, -0.13f), 0.06f, 0.05f);
                float tail = Seg(p, V3(0, 0.40f, -0.13f), V3(0, 0.46f, -0.22f), 0.05f, 0.05f);
                float fall = Seg(p, V3(0, 0.46f, -0.22f), V3(0, 0.12f, -0.42f), 0.05f, 0.02f);
                return (SMin(SMin(root, tail, 0.03f), fall, 0.03f), OrcCuff(p, V3(0, 0.345f, -0.115f), V3(0, 0.385f, -0.125f), 0.058f));
            }
            case "Ponytail":
            {
                float knot = Seg(p, V3(0, 0.16f, -0.24f), V3(0, 0.09f, -0.33f), 0.055f, 0.055f);
                float tail = Seg(p, V3(0, 0.08f, -0.34f), V3(0, -0.40f, -0.42f), 0.058f, 0.025f);
                return (SMin(knot, tail, 0.03f), OrcCuff(p, V3(0, 0.095f, -0.325f), V3(0, 0.065f, -0.335f), 0.064f));
            }
            case "Mohawk":
            {
                float crest = 9f;
                for (int i = 0; i < 14; i++)
                {
                    float a = Mathf.Lerp(35f, 200f, i / 13f) * Mathf.Deg2Rad;
                    var c = V3(0, 0.30f * Mathf.Sin(a) + 0.02f, 0.30f * Mathf.Cos(a) - 0.03f) * 1.12f;
                    crest = SMin(crest, Ell(p, c, V3(0.045f, 0.085f, 0.06f)), 0.03f);
                }
                return (crest, 9f);
            }
            case "WarKnot":
            {
                float root = Seg(p, V3(0, 0.30f, -0.10f), V3(0, 0.40f, -0.13f), 0.06f, 0.05f);
                var pts = new[] { V3(0, 0.40f, -0.14f), V3(0, 0.36f, -0.30f), V3(0, 0.24f, -0.40f), V3(0, 0.10f, -0.44f),
                                  V3(0, -0.04f, -0.46f), V3(0, -0.18f, -0.47f), V3(0, -0.32f, -0.47f) };
                return (SMin(root, OrcChain(p, pts, 0.045f, 0.032f), 0.02f),
                        Mathf.Min(OrcCuff(p, V3(0, 0.345f, -0.115f), V3(0, 0.385f, -0.125f), 0.058f),
                                  OrcCuff(p, V3(0, -0.20f, -0.47f), V3(0, -0.23f, -0.47f), 0.038f)));
            }
            case "Braids":   // 귀 뒤에서 앞으로 늘인 땋은 머리 두 가닥
            {
                var m = Mirror(p);
                float c = Seg(m, V3(0.20f, 0.17f, -0.18f), V3(0.25f, 0.08f, -0.17f), 0.045f, 0.042f);
                var pts = new[] { V3(0.25f, 0.08f, -0.17f), V3(0.29f, -0.02f, -0.14f), V3(0.31f, -0.12f, -0.08f), V3(0.32f, -0.22f, -0.02f),
                                  V3(0.32f, -0.32f, 0.04f), V3(0.31f, -0.42f, 0.10f), V3(0.30f, -0.52f, 0.15f) };
                float deco = Mathf.Min(OrcCuff(m, V3(0.31f, -0.32f, 0.095f), V3(0.31f, -0.345f, 0.105f), 0.036f),
                                       OrcCuff(m, V3(0.30f, -0.52f, 0.175f), V3(0.30f, -0.545f, 0.185f), 0.031f));
                return (SMin(c, OrcChain(m, pts, 0.035f, 0.026f), 0.02f), deco);
            }
        }
        return (9f, 9f);
    }

    // 입술 밑 턱을 덮는 수염 밑동.
    static float BeardPatch(Vector3 p, OrcFrame o, float half = 0.10f)
    {
        float d = SMax(OrcHead(p, o) - 0.022f, p.y - (o.MY - 0.055f), 0.015f);
        return SMax(SMax(d, Mathf.Abs(p.x) - half, 0.02f), -(p.z - 0.15f), 0.02f);
    }

    static (float hair, float deco) OrcBeard(string style, Vector3 p, OrcFrame o)
    {
        float cy = o.ChinY, cz = o.ChinZ;
        switch (style)
        {
            case "Braid":
            {
                var pts = new[] { V3(0, cy, cz), V3(0, cy - 0.08f, cz + 0.01f), V3(0, cy - 0.16f, cz + 0.03f), V3(0, cy - 0.24f, cz + 0.06f), V3(0, cy - 0.31f, cz + 0.09f) };
                float tuft = Seg(p, V3(0, cy - 0.31f, cz + 0.09f), V3(0, cy - 0.41f, cz + 0.12f), 0.04f, 0.012f);
                return (SMin(SMin(BeardPatch(p, o), OrcChain(p, pts, 0.055f, 0.035f), 0.025f), tuft, 0.02f),
                        OrcCuff(p, V3(0, cy - 0.26f, cz + 0.07f), V3(0, cy - 0.285f, cz + 0.078f), 0.045f));
            }
            case "TwinBraid":
            {
                var m = Mirror(p);
                var pts = new[] { V3(0.06f, cy, cz), V3(0.065f, cy - 0.08f, cz + 0.01f), V3(0.07f, cy - 0.16f, cz + 0.03f), V3(0.07f, cy - 0.24f, cz + 0.06f) };
                return (SMin(BeardPatch(p, o, 0.12f), OrcChain(m, pts, 0.042f, 0.032f), 0.025f),
                        OrcCuff(m, V3(0.07f, cy - 0.19f, cz + 0.04f), V3(0.07f, cy - 0.21f, cz + 0.048f), 0.038f));
            }
            case "Shaggy":   // 입술 바로 밑에서 턱을 덮는 짧은 수염(미리보기 v14 → v25 L — 사용자가 판마다 골랐다)
            {
                float d = SMax(OrcJaw(p, o) - 0.03f, p.y - (o.MY - 0.05f), 0.02f);
                d = SMax(d, -p.z, 0.03f);
                // 입술 바로 밑까지(입술이 턱처럼 보여 그 밑에서 시작하면 덜 자란 수염 같았다) · 윗선은 입술처럼 옆으로 처진다(곧은 줄이면 안 된다).
                float yb = p.y + 1.4f * p.x * p.x;
                float up = SMax(SMax(OrcHead(p, o) - 0.022f, yb - (o.MY - 0.045f), 0.012f), -p.z, 0.03f);
                d = SMin(d, up, 0.02f);
                // 턱 끝 숱 — v14의 덩어리를 작게 하고 입술 앞면까지 앞으로 당겼다.
                return (SMin(d, Ell(p, V3(0, cy - 0.005f, o.LipZ - 0.005f), V3(0.14f, 0.055f, 0.05f)), 0.04f), 9f);
            }
            case "Goatee":
                return (SMin(BeardPatch(p, o, 0.05f), Seg(p, V3(0, cy, cz), V3(0, cy - 0.14f, cz + 0.04f), 0.045f, 0.012f), 0.02f), 9f);
            case "Mutton":   // 귀 앞 구레나룻이 턱선을 따라 내려와 턱 끝에서 만난다 — 볼과 입 둘레는 맨살(가면처럼 덮으면 안 된다)
            {
                var m = Mirror(p);
                var path = new (Vector3 c, float r)[]
                {
                    (V3(0.285f, o.EY - 0.04f, -0.03f), 0.040f), (V3(0.272f, o.MY - 0.01f, 0.04f), 0.046f), (V3(0.21f, o.MY - 0.085f, 0.12f), 0.052f),
                    (V3(0.08f, cy - 0.03f, cz), 0.056f), (V3(0f, cy - 0.04f, cz + 0.005f), 0.058f),
                };
                float vol = 9f;
                for (int i = 0; i + 1 < path.Length; i++) vol = SMin(vol, Seg(m, path[i].c, path[i + 1].c, path[i].r, path[i + 1].r), 0.02f);
                return (SMax(OrcHead(p, o) - 0.020f, vol, 0.012f), 9f);   // 턱 끝 숱(공)은 뺐다 — 띠가 그대로 이어진다
            }
        }
        return (9f, 9f);
    }

    static readonly string[] OrcHairs  = { "Topknot", "WarKnot", "Ponytail", "Mohawk", "Braids" };   // + 민머리(Bald)
    static readonly string[] OrcBeards = { "Braid", "TwinBraid", "Shaggy", "Goatee", "Mutton" };    // + 없음(None)

    // ── 문신 — 감은 얼굴 좌표(u = 머리 둘레 호 · v = 높이)에 그린 붓질 ───────────────────
    //
    // 굵기가 같은 막대는 약하고 따로 놀았다(사용자: *"진짜 오크처럼"*) — 곡선을 타고 가운데가 굵고 끝이 빠지는 붓질로, 볼 · 관자놀이 선은
    // 머리 둘레를 따라 옆으로 감긴다. 겹치면 한 문양처럼 이어지게 자리를 맞췄다. 입 밑 띠는 뺐다.

    static float Stroke(float u, float v, Vector2[] pts, float w0, float w1, float ta = 0.15f, float tb = 0.85f, int n = 20)
    {
        Vector2 At(float t)
        {
            float s = 1f - t;
            return pts.Length == 3 ? s * s * pts[0] + 2f * s * t * pts[1] + t * t * pts[2]
                                   : s * s * s * pts[0] + 3f * s * s * t * pts[1] + 3f * s * t * t * pts[2] + t * t * t * pts[3];
        }
        float Width(float t)
        {
            float k = ta > 0f ? Mathf.Min(t / ta, 1f) : 1f;
            if (tb < 1f) k = Mathf.Min(k, (1f - t) / (1f - tb));
            return (w0 + (w1 - w0) * t) * Mathf.Pow(Mathf.Max(k, 0f), 0.6f);
        }
        float d = 9f; var q = new Vector2(u, v);
        for (int i = 0; i < n; i++)
        {
            float t0 = i / (float)n, t1 = (i + 1) / (float)n;
            var a = At(t0); var b = At(t1); float wa = Width(t0), wb = Width(t1);
            var ab = b - a; float k = Mathf.Clamp01(Vector2.Dot(q - a, ab) / (ab.sqrMagnitude + 1e-9f));
            d = Mathf.Min(d, (q - a - ab * k).magnitude - (wa + (wb - wa) * k));
        }
        return d;
    }

    static Vector2 V2(float x, float y) => new(x, y);

    static float Both(Func<float, float, float> f, float u, float v) => Mathf.Min(f(u, v), f(-u, v));

    static float OrcInk(string shape, float u, float v, OrcFrame o)
    {
        float EY = o.EY, MY = o.MY, NY = o.NY, EU = o.EU;
        switch (shape)
        {
            case "Chin":   // 턱 세 줄 — 아랫입술 위에서 턱 밑까지
            {
                float d = 9f;
                foreach (var (x, w) in new[] { (-0.05f, 0.010f), (0f, 0.013f), (0.05f, 0.010f) })
                    d = Mathf.Min(d, Stroke(u, v, new[] { V2(x, MY - 0.002f), V2(x * 1.05f, -0.30f), V2(x * 1.15f, -0.37f) }, w, w * 0.7f, 0.05f, 0.75f));
                return d;
            }
            case "Claw":   // 한쪽 눈을 할퀸 세 줄
            {
                float d = 9f;
                for (int k = -1; k <= 1; k++)
                {
                    float off = k * 0.035f;
                    d = Mathf.Min(d, Stroke(u, v, new[] { V2(-EU - 0.09f + off, EY + 0.15f), V2(-EU - 0.01f + off, EY + 0.02f), V2(-EU + 0.05f + off, EY - 0.15f) }, 0.011f, 0.011f));
                }
                return d;
            }
            case "Brow":   // 눈썹에서 솟는 갈고리 + 이마 가운데 가시
            {
                float Side(float uu, float vv) => Mathf.Min(
                    Stroke(uu, vv, new[] { V2(0.025f, EY + 0.075f), V2(0.12f, EY + 0.10f), V2(0.17f, EY + 0.20f), V2(0.24f, EY + 0.15f) }, 0.014f, 0.008f, 0.1f, 0.8f),
                    Stroke(uu, vv, new[] { V2(0.07f, EY + 0.13f), V2(0.10f, EY + 0.17f), V2(0.09f, EY + 0.24f) }, 0.009f, 0.004f, 0.1f, 0.8f));
                float centre = Stroke(u, v, new[] { V2(0f, EY + 0.06f), V2(0f, EY + 0.16f), V2(0f, EY + 0.27f) }, 0.016f, 0.004f, 0.05f, 0.7f);
                return Mathf.Min(Both(Side, u, v), centre);
            }
            case "Cheek":   // 광대를 따라 귀 쪽으로 휘는 선 + 늘어진 가지
            {
                float c = NY + 0.055f;
                float Side(float uu, float vv) => Mathf.Min(Mathf.Min(
                    Stroke(uu, vv, new[] { V2(0.06f, c + 0.005f), V2(0.17f, c - 0.07f), V2(0.33f, c + 0.04f) }, 0.016f, 0.007f, 0.08f, 0.8f),
                    Stroke(uu, vv, new[] { V2(0.09f, c - 0.045f), V2(0.18f, c - 0.10f), V2(0.30f, c - 0.04f) }, 0.010f, 0.005f, 0.1f, 0.8f)),
                    Stroke(uu, vv, new[] { V2(0.20f, c - 0.05f), V2(0.22f, c - 0.10f), V2(0.21f, c - 0.15f) }, 0.009f, 0.003f, 0.05f, 0.8f));
                return Both(Side, u, v);
            }
            case "Fangs":   // 눈 밑에서 흘러내리는 송곳니
            {
                float Side(float uu, float vv) => Mathf.Min(
                    Stroke(uu, vv, new[] { V2(EU - 0.015f, EY - 0.05f), V2(EU - 0.02f, EY - 0.10f), V2(EU - 0.01f, EY - 0.17f) }, 0.017f, 0f, 0.02f, 1f),
                    Stroke(uu, vv, new[] { V2(EU + 0.035f, EY - 0.05f), V2(EU + 0.04f, EY - 0.09f), V2(EU + 0.05f, EY - 0.13f) }, 0.012f, 0f, 0.02f, 1f));
                return Both(Side, u, v);
            }
            case "Temple":   // 눈꼬리에서 관자놀이로 갈라지는 선
            {
                float Side(float uu, float vv) => Mathf.Min(
                    Stroke(uu, vv, new[] { V2(0.19f, EY + 0.06f), V2(0.30f, EY + 0.10f), V2(0.42f, EY + 0.06f), V2(0.52f, EY + 0.12f) }, 0.015f, 0.006f, 0.08f, 0.85f),
                    Stroke(uu, vv, new[] { V2(0.30f, EY + 0.10f), V2(0.36f, EY + 0.18f), V2(0.44f, EY + 0.22f) }, 0.009f, 0.004f, 0.05f, 0.8f));
                return Both(Side, u, v);
            }
        }
        return 9f;
    }

    static readonly string[] OrcTattoos = { "Chin", "Claw", "Brow", "Cheek", "Fangs", "Temple" };

    // 문신 한 겹 — 머리 살 위 2.5mm의 껍데기 중 붓질 안. 속은 1cm 아래에서 닫는다(뒤로 새지 않게 앞 반만).
    static float OrcInkShell(string shape, Vector3 p, OrcFrame o)
    {
        float h = OrcHead(p, o);
        float shell = Mathf.Max(h - 0.0025f, -(h + 0.010f));
        if (shell > 0.01f) return shell;   // 껍데기에서 멀면 붓질을 안 잰다(빠르게)
        float u = Mathf.Atan2(p.x, p.z) * InkR;
        return SMax(Mathf.Max(shell, OrcInk(shape, u, p.y, o)), -p.z - 0.15f, 0.002f);
    }

    // ── 부위 ────────────────────────────────────────────────────────────────

    // 오크 머리 부위 — 사람 머리(`HeadParts`) 대신 오크가 받는다. **목 관절 자리(`Head`)와 투구는 사람과 같은 틀**이다.
    static List<Part> OrcHeadParts(Dictionary<(string head, string feature), Mesh> features, Dictionary<string, Mesh> hairs)
    {
        if (_orcHead != null) return _orcHead;
        var h = Array.Find(Heads, x => x.Name == OrcBasis);
        var o = new OrcFrame(h);
        var pos = HeadPos(h);
        var parts = new List<Part>();
        Func<Vector3, float> head = p => OrcHead(p, o);

        Mesh OrcSculpt(string name, Func<Vector3, float> f, Bounds b, float step, float keep)
            => SaveSkull($"Chibi_Orc_{name}", SurfaceNets(f, b, step, head, p => p, 0.03f, 4f, keep));
        void Add(string name, Mesh mesh, string mat, string line, bool on = true)
            => parts.Add(new Part(name, mesh, pos, Vector3.one, mat, on, outerMat: line));

        parts.Add(new Part("Head", null, pos, h.Size, "HeadSkin", on: false));

        // 머리 살 — 이름이 머리형이라 `PawnLook`이 이 머리 하나를 고른다. 눈 · 입 판은 사람 머리와 같다(입은 빚은 입이 대신한다).
        var hb = new Bounds(new Vector3(0f, -0.01f, 0.03f), new Vector3(h.Size.x + 0.06f, h.Size.y + 0.08f, h.Size.z + 0.22f));
        // **제 선을 끄고 바깥 선만**(`SkinFine` + `OutlineOuterFine`) — 사람 머리 살(`HeadSkin`)은 제 선(6cm 껍데기)을 두는데, 그것은 볼록한
        // 돌림 머리에서나 맞다. 눈썹뼈 · 코 · 입술이 파인 오크 머리에서는 껍데기가 오목한 자리마다 살을 뚫고 나와 얼굴이 검은 점투성이였다(첫 Play).
        Add($"Head.Shape.{h.Name}", OrcSculpt("Head", head, hb, OrcHeadStep, 0.3f), "SkinFine", "OutlineOuterFine");
        foreach (var f in Features)
            parts.Add(new Part($"Head.Shape.{h.Name}.{f.Name}", features[(h.Name, f.Name)], pos, h.Size, "Face" + f.Kind, true,
                               cell: f.DefaultCell, kind: f.Kind));

        var mouthB = new Bounds(new Vector3(0f, o.MY + 0.03f, o.LipZ), new Vector3(0.36f, 0.20f, 0.14f));
        Add("Head.Orc.Mouth", OrcSculpt("Mouth", p => OrcMouth(p, o), mouthB, OrcFineStep, 0.4f), "NoseDark", null);
        Add("Head.Orc.Teeth", OrcSculpt("Teeth", p => OrcTeeth(p, o), mouthB, OrcFineStep * 0.8f, 0.4f), "Bone", "OutlineOuterFine");
        Add("Head.Orc.Tusks", OrcSculpt("Tusks", p => OrcTusks(p, o), mouthB, OrcFineStep, 0.4f), "Bone", "OutlineOuterFine");

        // 귀 셋 — 씨앗이 하나를 고른다(`Head.Pick.Ear.*`).
        float earX = o.R(o.EyeT) * o.W;
        var earB = new Bounds(new Vector3(0f, o.EY + 0.04f, -0.10f), new Vector3(2f * earX + 0.26f, 0.40f, 0.30f));
        foreach (var (name, s, tip) in OrcEars)
            Add($"Head.Pick.Ear.{name}", OrcSculpt($"Ear_{name}", p => OrcEar(p, o, s, tip), earB, OrcFineStep * 1.2f, 0.35f),
                "SkinFine", "OutlineOuterFine", on: name == "Big");

        // 머리카락 · 수염 — 없는 벌(민머리 · 없음)은 렌더러가 없어 조건에만 적는다(`@Hair.!Bald`).
        var hairB = new Bounds(new Vector3(0f, -0.01f, -0.08f), new Vector3(0.80f, 1.24f, 0.98f));
        foreach (var style in OrcHairs)
        {
            string st = style;
            Add($"Head.Pick.Hair.{st}@Hair.!Bald", OrcSculpt($"Hair_{st}", p => OrcHair(st, p).hair, hairB, OrcHairStep, 0.35f), "HairFine", "OutlineOuterFine", on: false);
            if (st != "Mohawk")
                Add($"Head.Pick.Hair.{st}.Cuff@Hair.!Bald", OrcSculpt($"Hair_{st}_Cuff", p => OrcHair(st, p).deco, hairB, OrcFineStep * 1.3f, 0.4f),
                    "Bone", "OutlineOuterFine", on: false);
        }
        var beardB = new Bounds(new Vector3(0f, o.ChinY - 0.075f, 0.17f), new Vector3(0.72f, 0.78f, 0.62f));
        foreach (var style in OrcBeards)
        {
            string st = style;
            Add($"Head.Pick.Beard.{st}@Beard.!None", OrcSculpt($"Beard_{st}", p => OrcBeard(st, p, o).hair, beardB, OrcHairStep * 0.8f, 0.35f), "HairFine", "OutlineOuterFine", on: false);
            if (st is "Braid" or "TwinBraid")
                Add($"Head.Pick.Beard.{st}.Cuff@Beard.!None", OrcSculpt($"Beard_{st}_Cuff", p => OrcBeard(st, p, o).deco, beardB, OrcFineStep * 1.3f, 0.4f),
                    "Bone", "OutlineOuterFine", on: false);
        }

        // 문신 — 겹마다 따로 켜고 끈다(`Head.Pick.TatChin.On` · 끈 벌 `Off`는 조건에만). 색은 `MarkingLook`.
        var inkB = new Bounds(new Vector3(0f, -0.05f, 0.11f), new Vector3(h.Size.x + 0.10f, h.Size.y + 0.06f, 0.60f));
        foreach (var t in OrcTattoos)
        {
            string tt = t;
            Add($"Head.Pick.Tat{tt}.On@Tat{tt}.!Off", OrcSculpt($"Tattoo_{tt}", p => OrcInkShell(tt, p, o), inkB, OrcInkStep, 0.35f),
                "Ink", null, on: false);
        }

        // 투구 — 사람 투구 껍데기를 이 머리 크기로(사람은 `PawnLook`이 고른 머리로 옮기지만 오크는 머리가 하나다).
        var capPos = pos + new Vector3(0f, 0.015f, -0.01f);
        foreach (var fit in HairFits)
            parts.Add(new Part($"Head.Helmet.Cap.Shell.{fit}", hairs[$"{fit}.Helmet"], capPos, h.Size * HelmetScale, "Helmet", on: false));

        _orcHead = parts;
        return parts;
    }
}
