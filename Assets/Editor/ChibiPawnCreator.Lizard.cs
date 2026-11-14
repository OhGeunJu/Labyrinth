using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **리자드맨 — 미리보기 v9를 옮긴 것**(2026-11-12 — 사용자가 v9를 확정한 뒤).
//
// 모양은 파이썬 미리보기(`Tools/ModelLab/lizard/lizard.py` — 같은 거리장 식을 numpy로 그린 것)에서 사용자가 판마다 보고 고른 것이다.
// **수치를 고치면 그 그림과 어긋난다 — 미리보기를 먼저 고쳐 보여 주고 같이 옮길 것.** 단위 m · 발밑 0 · +z 앞 · 머리 부품은 머리 중심 원점.
//
// 큰 짐승(`ChibiPawnCreator.Brutes`)과 같은 길이다 — 거리장으로 빚어 표면을 뽑고(`SurfaceNets`) **관절마다 굳은 조각**으로 붙인다.
// 관절은 사람과 같은 `BodyJoints`라 클립이 그대로 돈다. 리자드맨만의 것:
//
//   - **굽은 등**(`LizFrame.Zs` — 허리 위로 높이의 제곱만큼 앞으로 · 트롤과 같은 식) · 머리는 그 앞에 처져 달린다(`HeadFwd` · `HeadDrop`) ·
//     팔 뼈도 굽은 만큼 앞에 선다(`BoneBody.ArmZ`).
//   - **짐승 다리** — 엉덩이 → 무릎(앞) → 뒤꿈치(뒤 · 들림) → 발볼. **뼈대는 사람 것 그대로 두고 모양만 그렇게 빚는다**: 허벅지 조각은 위다리 뼈,
//     정강이 + 발등 한 덩이는 아래다리 뼈, 발볼 · 발가락은 발 뼈. 무릎 뼈 자리(다리 한가운데)와 보이는 무릎(13cm 앞)이 달라 걸을 때 무릎에서
//     두 조각이 조금 미끄러진다 — **게임에서 봐야 한다.**
//   - **꼬리** — 엉덩이 뼈에 붙은 굳은 조각 하나(아직 안 흔들린다 — 꼬리 뼈는 다음).
//   - **잎 비늘**(`LizLeaf` — 숲살모사 · 누런 구렁이) — 뒤로 누워 겹친 납작한 잎. 끝이 종이처럼 얇아 표면 뽑기로는 뭉개지므로 **잎마다 메시를 직접 짓는다**.
//   - **색은 정점 색이다** — 살 재질(`Chibi_LizSkin`)은 흰 바탕에 정점 색을 곱한다(`_VertexAO`). 배 · 턱의 밝은 색, 보엘렌의 노란 빗금, 붉은 뱀의
//     꼬리로 어두워지는 몸, 잎의 어두운 밑동 → 밝은 끝이 다 정점 색이라 **색마다 메시가 따로**다(모양은 한 번만 뽑고 색만 다시 칠한다).
//     가는 줄(입술 세로 줄 · 눈 밑 줄)은 정점 간격보다 가늘어 살 위 1cm에 띄운 얇은 덧판이다.
//   - 이름이 `Chibi_Skin…`이 아니라 개체 살색(`SkinLook`)은 안 탄다 — 색은 프리팹이 든다.
//
// 종류 넷 · 프리팹 열 벌(보기 몸 — 아직 어디서도 안 가리킨다 · `3_Prefab/Pawn/Lizard/`):
//   도마뱀 `Chibi_Lizard`(볏은 고르기 축 `Crest` — 가시 테 · 이구아나 볏 · 없음) · 악어 `Chibi_Lizard_Croc`(몸도 굵다) ·
//   숲살모사 `Chibi_Lizard_Viper{Green|Slate|Coal|Amber}` · 매끈한 뱀 `Chibi_Lizard_Snake{Boelen|Kingrat|Indigo|Red}`.
// **색마다 프리팹이 따로인 것은 임시 결정이다** — 색을 한 무리 안에서 섞을지 무리마다 가를지는 사용자가 아직 안 정했다(둘 다 이 꼴에서 간다).
// 눈은 고르기 축(`Eyes` 여덟 — 사용자: *"뾰족한 눈 · 귀여운 눈 · 그 중간으로 넓게"*) · 미리보기의 기본 눈이 40%.
public static partial class ChibiPawnCreator
{
    // ── 몸 ──────────────────────────────────────────────────────────────────
    //                                                                        밑     엉덩이  허리   가슴   어깨      깊이   손     팔K    다리K  몸통K  팔굵기 다리굵기 어깨둥글림
    static readonly Shape LizardShape = new("Lizard", "리자드맨", "Male", new[] { 0.23f, 0.26f, 0.27f, 0.33f, 0.35f }, 0.88f, 0.32f, 1.12f, 1.37f, 1.05f, 1.15f, 1.12f, 0.42f);
    static readonly Shape CrocShape   = new("LizardCroc", "악어 리자드맨", "Male", new[] { 0.29f, 0.33f, 0.36f, 0.44f, 0.47f }, 1.02f, 0.39f, 1.12f, 1.30f, 1.12f, 1.45f, 1.32f, 0.34f);

    const float LizHeadFwd = 0.17f, LizHeadDrop = 0.17f;   // 머리가 굽은 어깨보다 앞으로 · 아래로
    const float LizGap = 0.007f;                           // 입선의 반 틈
    const float LizFlat = 0.36f;                           // 잎 비늘의 두께 / 폭

    static readonly Color LzBone = new(0.90f, 0.86f, 0.72f), LzDark = new(0.06f, 0.05f, 0.05f), LzMouth = new(0.30f, 0.07f, 0.08f), LzWhite = new(0.96f, 0.96f, 0.96f);
    static readonly Color LzLeather = new(0.34f, 0.22f, 0.13f), LzCloth = new(0.42f, 0.14f, 0.11f);
    static readonly Color LzAmber = new(0.95f, 0.72f, 0.16f), LzGreen = new(0.62f, 0.82f, 0.25f), LzPale = new(0.80f, 0.87f, 0.92f), LzBlack = new(0.05f, 0.05f, 0.06f);
    static readonly Color LzGrey = new(0.62f, 0.58f, 0.52f), LzYellow = new(0.93f, 0.80f, 0.20f);

    // 종류의 몸 수치 한 벌 — 미리보기 `set_body`. 몸 거리장 · 색이 다 여기서 읽는다(읽기만 — 표면 뽑기가 여러 가닥에서 부른다).
    sealed class LizFrame
    {
        public readonly BodyDims D;
        public readonly float HK, Lean, NeckR, TailK, Y0, Top, ArmZ;
        public readonly Vector3 HO;                                 // 머리 중심
        public readonly Vector3[] Tail = new Vector3[13];
        public readonly float[] TailR = new float[13], TZ = new float[13], TY = new float[13];
        public readonly Vector3 Hip, Knee, Heel, Ball;              // 오른 다리(+x)
        public readonly Vector3 Shoulder, Elbow, Wrist, HandC;      // 오른 팔
        public readonly Vector3 NeckA, NeckB;                       // 목 — 어깨 쪽 · 머리 쪽

        public LizFrame(Shape s, float hk, float lean, float neckR, float tailK)
        {
            D = DimsOf(s); HK = hk; Lean = lean; NeckR = neckR; TailK = tailK;
            Top = D.Top; Y0 = D.Lift + 0.22f * D.TorsoH;
            HO = new Vector3(0f, Top + HeadGap + D.Hh * 0.5f - LizHeadDrop, HeadZ + Zs(Top) + LizHeadFwd);
            // 꼬리 — 엉덩이에서 뒤로 내려가 땅 가까이에서 눕는다. 3차 곡선 열두 마디 · 굵기는 뿌리에서 끝까지 한 번에 준다.
            float rt = Mathf.Sqrt(tailK);
            var tp = new[] { new Vector2(D.Lift + 0.115f, -0.08f), new Vector2(D.Lift - 0.02f, -0.48f * rt), new Vector2(0.11f * tailK, -0.78f * rt), new Vector2(0.10f * tailK, -1.26f * rt) };
            for (int i = 0; i < 13; i++)
            {
                float t = i / 12f, u = 1f - t;
                var b = u * u * u * tp[0] + 3f * u * u * t * tp[1] + 3f * u * t * t * tp[2] + t * t * t * tp[3];
                Tail[i] = new Vector3(0f, b.x, b.y);
                TailR[i] = 0.168f * tailK * Mathf.Pow(1f - t, 1.25f) + 0.006f;
                TZ[i] = -b.y; TY[i] = b.x;
            }
            ArmZ = Zs(D.ShoulderY);
            Shoulder = new Vector3(D.ArmX, D.ShoulderY, ArmZ); Wrist = new Vector3(D.ArmX, D.HandY, ArmZ + 0.04f);
            Elbow = (Shoulder + Wrist) * 0.5f;
            HandC = new Vector3(D.ArmX, D.HandY - D.Hand * 0.25f, ArmZ + 0.04f);
            float L = D.Lift;
            Hip = new Vector3(D.LegX, D.HipY, 0f); Knee = new Vector3(D.LegX, L * 0.66f, 0.135f);
            Heel = new Vector3(D.LegX, L * 0.30f, -0.105f); Ball = new Vector3(D.LegX, 0.060f, 0.055f);
            NeckA = new Vector3(0f, Top - 0.08f, Zs(Top) - 0.05f); NeckB = HO + new Vector3(0f, -0.07f, -0.13f) * HK;
        }

        public float Zs(float y) { float d = Mathf.Max(y - Y0, 0f); return Lean * d * d; }   // 굽은 등
        public float TorsoR(float y) => BodyTorsoRadius(D.Half, Mathf.Clamp01((y - D.Lift) / D.TorsoH), D.TorsoH, D.TopR);
        public float BackZ(float y) => -TorsoR(y) * D.Depth + Zs(y);

        public float Torso(Vector3 p)
        {
            float z = p.z - Zs(p.y), y = p.y - D.Lift;
            float q = Mathf.Sqrt(p.x * p.x + z / D.Depth * (z / D.Depth));
            return Mathf.Max((q - TorsoR(p.y)) * D.Depth, Mathf.Max(-y, y - D.TorsoH)) * 0.9f;
        }
        public float Neck(Vector3 p) => Seg(p, NeckA, NeckB, NeckR * 1.18f, NeckR);
        public float TailSdf(Vector3 p, int upTo = 12)
        {
            float d = Seg(p, Tail[0], Tail[1], TailR[0], TailR[1]);
            for (int i = 1; i < upTo; i++) d = SMin(d, Seg(p, Tail[i], Tail[i + 1], TailR[i], TailR[i + 1]), 0.02f);
            return d;
        }
        // 몸통 조각 — 몸통 + 목 + 꼬리 뿌리로 넘어가는 둥근 이음(꼬리 조각이 덮게 3mm 가늘게).
        public float TorsoPiece(Vector3 p) => SMin(SMin(Torso(p), Neck(p), 0.05f), TailSdf(p, 3) + 0.003f, 0.06f);

        // 팔다리 · 손발(오른쪽에 적는다 — `Side`가 뒤집는다).
        public float Upper(Vector3 m) => Capsule(m, Elbow, Shoulder, D.ArmR);
        public float Fore(Vector3 m)  => Capsule(m, Wrist, Elbow, D.ArmR);
        public float Hand(Vector3 m)  => Sph(m, HandC, D.Hand * 0.5f);
        public float HandClaws(Vector3 m)
        {
            float r = D.Hand * 0.5f, d = 9f; var c = HandC;
            foreach (float dx in new[] { -0.05f, 0f, 0.05f })
                d = Mathf.Min(d, Seg(m, new Vector3(c.x + dx, c.y - r * 0.55f, c.z + 0.045f), new Vector3(c.x + dx * 1.3f, c.y - r * 1.35f, c.z + 0.085f), 0.022f * r / 0.118f, 0.003f));
            return d;
        }
        public float Thigh(Vector3 m) => Seg(m, Hip, Knee, D.LegR * 1.22f, D.LegR * 1.02f);
        public float Shin(Vector3 m)  => SMin(Seg(m, Knee, Heel, D.LegR * 0.98f, D.LegR * 0.66f), Seg(m, Heel, Ball, D.LegR * 0.66f, D.LegR * 0.70f), 0.03f);
        static readonly float[] Toes = { -0.075f, 0f, 0.075f };
        public float Foot(Vector3 m)   // 발끝으로 선다 — 발볼에서 벌어진 발가락 셋
        {
            float k = D.LegR / 0.106f;
            float pad = Ell(m, Ball + new Vector3(0f, -0.005f, 0.02f), new Vector3(0.105f * k, 0.055f, 0.10f * k)), toes = 9f;
            foreach (float dx in Toes)
                toes = Mathf.Min(toes, Seg(m, Ball + new Vector3(dx * 0.6f * k, -0.012f, 0.05f), Ball + new Vector3(dx * 1.4f * k, -0.02f, 0.215f * k), 0.042f * k, 0.032f * k));
            return SMin(pad, toes, 0.03f);
        }
        public float FootClaws(Vector3 m)
        {
            float k = D.LegR / 0.106f, d = 9f;
            foreach (float dx in Toes)
                d = Mathf.Min(d, Seg(m, Ball + new Vector3(dx * 1.4f * k, -0.018f, 0.21f * k), Ball + new Vector3(dx * 1.6f * k, -0.045f, 0.30f * k), 0.026f * k, 0.003f));
            return d;
        }

        // 등줄기 돌기(도마뱀 · 악어) — 몸통 것 · 꼬리 것. 끝으로 갈수록 작아지다 사라진다.
        public float RidgeBack(Vector3 p)
        {
            float d = 9f, k = Mathf.Sqrt(TailK);
            for (int i = 0; i < 5; i++)
            {
                float y = Mathf.Lerp(D.Lift + 0.22f, Top - 0.08f, i / 4f), zb = BackZ(y);
                d = Mathf.Min(d, Seg(p, new Vector3(0f, y, zb + 0.03f), new Vector3(0f, y + 0.02f, zb - 0.075f * k), 0.042f * k, 0.004f));
            }
            return d;
        }
        public float RidgeTail(Vector3 p)
        {
            float d = 9f;
            for (int i = 1; i < 9; i++)
            {
                var c = Tail[i]; float r = TailR[i];
                var ax = (Tail[i + 1] - Tail[i - 1]).normalized; var up = Vector3.Cross(Vector3.Cross(ax, Vector3.up), ax).normalized;
                d = Mathf.Min(d, Seg(p, c + up * (r - 0.02f), c + up * (r + 0.015f + r * 0.42f) + ax * 0.03f, 0.022f + r * 0.14f, 0.004f));
            }
            return d;
        }

        public float Belt(Vector3 p) => Mathf.Max(Torso(p) - 0.014f, Mathf.Abs(p.y - (D.HemY + 0.02f)) - 0.042f);
        public float Flap(Vector3 p)   // 앞 가리개 — 뒤는 꼬리가 나와 비운다
        {
            float zf = TorsoR(D.HemY) * D.Depth;
            return RBox(p, new Vector3(0f, D.HemY - 0.155f, zf + 0.012f), new Vector3(0.105f * D.Half[1] / 0.246f, 0.185f, 0.014f), 0.012f);
        }

        // 몸을 따라 내려가는 자 — 몸통에선 높이, 꼬리에선 뒤로 간 거리를 이어 붙인다(띠 무늬가 꼬리에서 꺾이지 않게). `w` — 꼬리 몫.
        public float Along(Vector3 p, out float w)
        {
            w = SStep(-0.18f, -0.36f, p.z) * SStep(D.Lift + 0.28f, D.Lift + 0.10f, p.y);
            return p.y * (1f - w) + (D.Lift + 0.14f + p.z) * w;
        }
        // 살 색 — 배 · 꼬리 밑은 배 색.
        public Color BodyCol(Vector3 p, Color skin, Color belly)
        {
            float zr = p.z - Zs(p.y);
            float front = SStep(0.02f, 0.10f, zr) * SStep(0.19f, 0.13f, Mathf.Abs(p.x) * 0.246f / D.Half[1]) * SStep(D.Lift, D.Lift + 0.06f, p.y);
            float ay = Interp(-p.z, TZ, TY), ar = Interp(-p.z, TZ, TailR);
            float under = SStep(-0.22f, -0.34f, p.z) * SStep(-0.15f, -0.55f, (p.y - ay) / Mathf.Max(ar, 0.01f)) * SStep(D.Lift + 0.2f, D.Lift + 0.1f, p.y);
            return Color.Lerp(skin, belly, Mathf.Max(front, under));
        }
    }

    static Vector3 Squash(Vector3 p, float y0, float sy) => new(p.x, y0 + (p.y - y0) / sy, p.z);   // y0 기준으로 세로를 sy배 눌러 본다(거리에 sy를 곱해 쓴다)
    static float Line2(float a, float b, float a0, float b0, float a1, float b1)                  // (a, b) 평면의 선분까지 거리
    {
        float da = a1 - a0, db = b1 - b0;
        float t = Mathf.Clamp01(((a - a0) * da + (b - b0) * db) / (da * da + db * db));
        return Mathf.Sqrt((a - a0 - t * da) * (a - a0 - t * da) + (b - b0 - t * db) * (b - b0 - t * db));
    }
    static float ColDiff(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    static Color Mul(Color c, float k) => new(c.r * k, c.g * k, c.b * k, 1f);

    // ── 잎 비늘 ─────────────────────────────────────────────────────────────
    //
    // 뒤로 누워 겹친 납작한 잎(사용자 11-12: *"가시는 징그럽다 · 사진과 안 닮았다"*) — 곤두선 가시가 아니라 살에 누운 잎이고, 끝만 살짝 들려
    // 앞줄이 뒷줄 밑동을 덮는다(솔방울). 미리보기 `Leaves` 그대로: 둥근 밑동 → 뾰족한 끝 · 두께는 폭의 `LizFlat`배.
    readonly struct LizLeaf
    {
        public readonly Vector3 B, D, S, U; public readonly float Ln, Wd;
        public LizLeaf(Vector3 basePt, Vector3 back, Vector3 n, float ln, float wd, float lift)
        {
            n = n.normalized; back = (back - n * Vector3.Dot(back, n)).normalized;
            D = (back + n * lift).normalized; S = Vector3.Cross(n, D).normalized; U = Vector3.Cross(D, S);
            B = basePt - D * (wd * 0.25f); Ln = ln; Wd = wd;
        }
        LizLeaf(Vector3 b, Vector3 d, Vector3 s, Vector3 u, float ln, float wd) { B = b; D = d; S = s; U = u; Ln = ln; Wd = wd; }
        public bool Central => Mathf.Abs(B.x) < 1e-4f && Mathf.Abs(D.x) < 1e-4f;
        public LizLeaf Flipped() { var f = new Vector3(-1f, 1f, 1f); return new LizLeaf(Vector3.Scale(B, f), Vector3.Scale(D, f), Vector3.Scale(S, f), Vector3.Scale(U, f), Ln, Wd); }
        public LizLeaf Placed(Vector3 o, float k) => new(o + B * k, D, S, U, Ln * k, Wd * k);   // 머리 좌표 → 몸 좌표
    }

    // 잎 묶음을 메시 하나로 — 잎마다 고리 넷(밑동 덮개 · 밑동 · 가운데 둘) + 양 끝 꼭짓점. 색은 밑동(`dark`)에서 끝(`tip`)으로(`e0`~`e1`).
    // `side` — 0이면 좌우 다(가운데 잎은 한 번) · ±1이면 그쪽만.
    static Surface LeafSurface(List<LizLeaf> leaves, int side, Color dark, Color tip, float e0, float e1)
    {
        const int Segs = 6;
        var V = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>();
        bool lin = PlayerSettings.colorSpace == ColorSpace.Linear;
        Color At(float h) { var c = Color.Lerp(dark, tip, SStep(e0, e1, h)); c.a = 1f; return lin ? c.linear : c; }
        void One(LizLeaf f)
        {
            float r0 = f.Wd * 0.5f;
            float R(float h) => 0.005f + (r0 - 0.005f) * Mathf.Pow(1f - h, 0.75f);
            // (잎 축 위의 자리 u · 반지름 · 색 자리 h)
            var rings = new (float u, float r, float h)[] { (-r0 * 0.7071f, r0 * 0.7071f, 0f), (0f, r0, 0f), (f.Ln * 0.35f, R(0.35f), 0.35f), (f.Ln * 0.70f, R(0.70f), 0.70f) };
            int o = V.Count;
            V.Add(f.B - f.D * r0); N.Add(-f.D); C.Add(At(0f));
            foreach (var (u, r, h) in rings)
                for (int k = 0; k < Segs; k++)
                {
                    float a = 2f * Mathf.PI * k / Segs, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    V.Add(f.B + f.D * u + f.S * (r * ca) + f.U * (r * LizFlat * sa));
                    N.Add((f.S * ca + f.U * (sa / LizFlat) + f.D * (u < 0f ? -0.7f : 0.25f)).normalized);
                    C.Add(At(h));
                }
            int tipI = V.Count;
            V.Add(f.B + f.D * (f.Ln + 0.005f)); N.Add(f.D); C.Add(At(1f));
            // 세모의 앞면은 잎 축에서 바깥을 본다 — 좌우를 뒤집은 잎도 같은 식으로 맞는다.
            void Tri(int a, int b, int c)
            {
                var mid = (V[a] + V[b] + V[c]) / 3f; var q = mid - f.B;
                var outward = mid - (f.B + f.D * Mathf.Clamp(Vector3.Dot(q, f.D), -r0 * 0.5f, f.Ln));
                if (Vector3.Dot(Vector3.Cross(V[b] - V[a], V[c] - V[a]), outward) < 0f) (b, c) = (c, b);
                T.Add(a); T.Add(b); T.Add(c);
            }
            int Ring(int i, int k) => o + 1 + i * Segs + k % Segs;
            for (int k = 0; k < Segs; k++)
            {
                Tri(o, Ring(0, k), Ring(0, k + 1));
                for (int i = 0; i + 1 < rings.Length; i++) { Tri(Ring(i, k), Ring(i + 1, k), Ring(i + 1, k + 1)); Tri(Ring(i, k), Ring(i + 1, k + 1), Ring(i, k + 1)); }
                Tri(Ring(rings.Length - 1, k), tipI, Ring(rings.Length - 1, k + 1));
            }
        }
        foreach (var f in leaves)
        {
            if (side >= 0) One(f);
            if (side <= 0 && !(side == 0 && f.Central)) One(f.Flipped());
        }
        return new Surface(V, N, C, T);
    }

    // 살 속(`a`)에서 `dr` 쪽으로 나가 겉면을 만나는 점 — 살 밖에서 시작하면 false.
    static bool LizSurf(Func<Vector3, float> f, Vector3 a, Vector3 dr, out Vector3 hit)
    {
        hit = a; dr.Normalize();
        if (f(a) > 0f) return false;
        float lo = 0f, hi = 0.6f;
        for (int i = 0; i < 22; i++) { float mid = (lo + hi) * 0.5f; if (f(a + dr * mid) > 0f) hi = mid; else lo = mid; }
        hit = a + dr * lo; return true;
    }

    // 몸의 잎 비늘 — 등(옆구리까지) · 목덜미 · 팔 바깥 · 허벅지 바깥 · 꼬리 윗면. 아래(꼬리에선 끝) 쪽으로 누워 겹친다. 배 · 팔다리 안쪽 · 손발은 매끈하다.
    // 팔 것은 팔꿈치에서 위 · 아래로 갈라 준다(관절마다 굳은 조각). 오른쪽(+x)에 적는다.
    sealed class LizBodyLeaves { public readonly List<LizLeaf> Back = new(), ArmUp = new(), ArmLow = new(), Thigh = new(), Tail = new(); }

    static LizBodyLeaves BodyLeaves(LizFrame F, float lift)
    {
        var L = new LizBodyLeaves(); var D = F.D;
        {
            const float ln = 0.136f, wd = 0.106f;
            float y = F.Top + 0.005f; int r = 0;
            while (y > D.Lift + 0.06f)
            {
                float t = Mathf.Min((y - D.Lift) / D.TorsoH, 0.985f), rr = BodyTorsoRadius(D.Half, Mathf.Clamp01(t), D.TorsoH, D.TopR), z0 = F.Zs(y);
                float amax = y > D.HemY + 0.07f ? 1.50f : 1.05f;   // 허리끈 아래는 꼬리 뿌리 둘레만
                for (int c = 0; c < 9; c++)
                {
                    float a = (c + (r % 2 == 1 ? 0.5f : 0f)) * wd * 0.90f / Mathf.Max(rr, 0.08f);
                    if (a > amax) break;
                    var pt = new Vector3(rr * Mathf.Sin(a), y, -rr * D.Depth * Mathf.Cos(a) + z0);
                    var n = new Vector3(Mathf.Sin(a) * D.Depth, 0.10f, -Mathf.Cos(a));
                    L.Back.Add(new LizLeaf(pt, new Vector3(0f, -1f, -2f * F.Lean * Mathf.Max(y - F.Y0, 0f)), n, ln, wd, 0.24f * lift));
                }
                y -= ln * 0.50f; r++;
            }
        }
        // 굵은 선(팔 · 허벅지 · 목)의 둘레에 줄 맞춰 — `outv` 쪽이 각도 0, 잎은 b 쪽으로 눕는다.
        void Wrap(Func<Vector3, List<LizLeaf>> to, Vector3 a, Vector3 b, float ra, float rb, Vector3 outv, float a0, float a1, float ln, float wd, float lf, float s0, float s1)
        {
            float len = (b - a).magnitude; var ax = (b - a) / len;
            var o = (outv - ax * Vector3.Dot(outv, ax)).normalized; var sd = Vector3.Cross(ax, o);
            float u = s0 * len; int r = 0;
            while (u < s1 * len)
            {
                float rad = ra + (rb - ra) * u / len, da = wd * 0.90f / rad;
                for (float ang = a0 + (r % 2 == 1 ? 0.5f * da : 0f); ang <= a1; ang += da)
                {
                    var n = o * Mathf.Cos(ang) + sd * Mathf.Sin(ang); var pt = a + ax * u + n * rad;
                    var fl = pt.x < 0f ? new Vector3(-1f, 1f, 1f) : Vector3.one;
                    pt = Vector3.Scale(pt, fl);
                    to(pt).Add(new LizLeaf(pt, Vector3.Scale(ax, fl), Vector3.Scale(n, fl), ln, wd, lf));
                }
                u += ln * 0.50f; r++;
            }
        }
        List<LizLeaf> Arm(Vector3 pt) => pt.y >= F.Elbow.y ? L.ArmUp : L.ArmLow;
        foreach (float a_ in new[] { -1.9f, -1.1f, -0.3f })   // 어깨 꼭지 한 줄
        {
            var n = new Vector3(Mathf.Cos(a_) * 0.75f, 0.66f, Mathf.Sin(a_) * 0.75f).normalized;
            L.ArmUp.Add(new LizLeaf(F.Shoulder + n * D.ArmR, new Vector3(n.x, -1f, n.z), n, 0.108f, 0.090f, 0.20f * lift));
        }
        Wrap(Arm, F.Shoulder, new Vector3(D.ArmX, D.HandY + 0.10f, F.ArmZ + 0.04f), D.ArmR, D.ArmR, Vector3.right, -2.1f, 0.35f, 0.108f, 0.090f, 0.20f * lift, 0.04f, 1.0f);
        Wrap(_ => L.Thigh, F.Hip, F.Knee, D.LegR * 1.22f, D.LegR * 1.02f, new Vector3(1f, 0f, -0.5f), -1.5f, 0.75f, 0.112f, 0.094f, 0.20f * lift, 0.30f, 0.92f);
        Wrap(_ => L.Back, F.NeckB, F.NeckA, F.NeckR, F.NeckR * 1.18f, new Vector3(0f, 0.35f, -1f), 0f, 1.45f, 0.104f, 0.086f, 0.20f * lift, 0.30f, 0.95f);
        // 꼬리 — 곡선을 따라 고리마다 엇갈려
        {
            var cum = new float[13];
            for (int i = 1; i < 13; i++) cum[i] = cum[i - 1] + (F.Tail[i] - F.Tail[i - 1]).magnitude;
            float s = 0.10f; int r = 0;
            while (s < cum[12] - 0.10f)
            {
                int i = 0;
                while (i < 11 && cum[i + 1] < s) i++;
                float f = (s - cum[i]) / (cum[i + 1] - cum[i]);
                var c = Vector3.Lerp(F.Tail[i], F.Tail[i + 1], f); float rad = Mathf.Lerp(F.TailR[i], F.TailR[i + 1], f);
                var ax = (F.Tail[i + 1] - F.Tail[i]).normalized; var up = Vector3.Cross(Vector3.Cross(ax, Vector3.up), ax).normalized;
                float l2 = 0.050f + rad * 0.62f, w2 = l2 * 0.80f;
                for (int cc = 0; cc < 7; cc++)
                {
                    float a = (cc + (r % 2 == 1 ? 0.5f : 0f)) * w2 * 0.90f / Mathf.Max(rad, 0.02f);
                    if (a > 1.95f) break;
                    var n = up * Mathf.Cos(a) + Vector3.right * Mathf.Sin(a);
                    L.Tail.Add(new LizLeaf(c + n * rad, ax, n, l2, w2, 0.24f * lift));
                }
                s += l2 * 0.50f; r++;
            }
        }
        return L;
    }

    // ── 머리 ────────────────────────────────────────────────────────────────

    // 머리 하나 — 머리통 · 위턱 · 아래턱 · 입선 높이 · 눈 자리 · 덧붙이 · 콧구멍(머리 좌표).
    sealed class LizHead
    {
        public Func<Vector3, float> Cran, Upper, Lower, Extra, MouthY;
        public Vector3 Eye, EyeN; public float EyeR, JawW;
        public Vector2 ChinZ = new(0.03f, 0.13f);                    // 아래턱의 밝은 색이 시작하는 z 띠
        public (Vector3 c, float r)[] Pits;
        public Color Skin, Belly;
        public string DefaultEye; public Color? EyeCol;              // 기본 눈 · 그 눈의 색(없으면 눈 표의 색)
        public List<LizLeaf> Leaves;                                 // 머리 비늘(머리 좌표) — 없으면 null

        public float Skin0(Vector3 p) => SMin(SMin(Cran(p), Extra(p), 0.05f), Mathf.Min(Upper(p), Lower(p)), 0.05f);
        public float Top0(Vector3 p)  => SMin(SMin(Cran(p), Extra(p), 0.05f), Upper(p), 0.05f);   // 비늘을 앉히는 윗머리
        // 머리 살 — 입선이 위아래를 가른다 · 콧구멍은 판다(튀어나온 점이 아니다). 눈꺼풀은 눈 벌마다 따로 조각이다.
        public float Skull(Vector3 p)
        {
            float yv = p.y - MouthY(p), b = SMin(Cran(p), Extra(p), 0.05f);
            float d = Mathf.Min(SMin(b, Mathf.Max(Upper(p), -(yv - LizGap)), 0.05f), SMin(b, Mathf.Max(Lower(p), yv + LizGap), 0.05f));
            var m = Mirror(p);
            foreach (var (c, r) in Pits) d = SMax(d, -((m - c).magnitude - r), 0.004f);
            return d;
        }
        public float Inner(Vector3 p) => Mathf.Min(Upper(p), Lower(p)) + 0.011f;   // 입 안 — 입선 틈으로만 보인다

        // 눈알을 살 겉면에 앉힌다 — 눈 방향 선 위에서 겉면을 찾아 눈알 중심이 살 속 `depth × 반지름`에 오게.
        public void SeatExact(float depth) { if (LizSurf(Skin0, Eye - EyeN * 0.12f, EyeN, out var pt)) Eye = pt - EyeN * (depth * EyeR); }
        // 악어 — 손으로 맞춘 값이라 옛 길 그대로(눌린 조각의 거리를 그대로 믿는다).
        public void SeatLoose(float depth) { var c = Eye; for (int i = 0; i < 8; i++) c -= EyeN * (Skin0(c) + depth * EyeR); Eye = c; }

        // 살 색 — 아래턱은 배 색(아래턱 폭 안에서만) · 콧구멍 안은 검다. `wide` — 색마다 얹는 넓은 칠(머리 좌표 · 지금 색 → 새 색).
        public Color Col(Vector3 p, Func<Vector3, Color, Color> wide)
        {
            float yv = p.y - MouthY(p);
            float k = SStep(0f, -0.03f, yv) * SStep(ChinZ.x, ChinZ.y, p.z) * SStep(JawW + 0.02f, JawW - 0.05f, Mathf.Abs(p.x));
            var col = Color.Lerp(Skin, Belly, k);
            if (wide != null) col = wide(p, col);
            var m = Mirror(p);
            foreach (var (c, r) in Pits) col = Color.Lerp(col, LzDark, SStep(r * 1.25f, r * 0.95f, (m - c).magnitude));
            return col;
        }
    }

    // 머리 윗면 · 옆면에 뒤로 누운 잎 비늘을 줄 맞춰 깐다 — z0(앞)에서 z1(뒤)로 가며 커지고 더 들린다. 눈 둘레 · 입술은 비운다.
    static List<LizLeaf> HeadLeaves(LizHead h, float z0, float z1, float ln0, float ln1, float ratio, float lift0, float lift1, float browLift = -1f)
    {
        var lv = new List<LizLeaf>(); float z = z0; int r = 0;
        Func<Vector3, float> skin = h.Top0;
        while (z > z1)
        {
            float t = Mathf.Clamp01((z0 - z) / (z0 - z1)), ln = ln0 + (ln1 - ln0) * t, wd = ln * ratio;
            var axis = new Vector3(0f, -0.03f, z);
            if (!LizSurf(skin, axis, Vector3.up, out var top)) break;
            float rad = Mathf.Max(top.y + 0.03f, 0.06f);
            for (int c = 0; c < 12; c++)
            {
                float phi = (c + (r % 2 == 1 ? 0.5f : 0f)) * wd * 0.92f / rad;
                if (phi > 1.85f) break;
                if (!LizSurf(skin, axis, new Vector3(Mathf.Sin(phi), Mathf.Cos(phi), 0f), out var pt) || pt.y < h.MouthY(pt) + 0.045f) continue;
                float de = (pt - h.Eye).magnitude;
                if (de < h.EyeR * 1.45f) continue;
                bool brow = browLift >= 0f && de < h.EyeR * 2.3f && pt.y > h.Eye.y;   // 눈 위 줄 — 눈썹
                lv.Add(new LizLeaf(pt, Vector3.back, Grad(skin, pt, 0.004f), ln * (brow ? 1.10f : 1f), wd, brow ? browLift : lift0 + (lift1 - lift0) * t * t));
            }
            z -= ln * 0.46f; r++;
        }
        return lv;
    }

    // 도마뱀의 가시 테 — 눈 위(속눈썹 자리)에서 머리 윗면 가장자리를 따라 뒤통수로. 살은 테두리 둑, 그 위에 잔가시가 촘촘하다(볏도마뱀붙이).
    static readonly Vector3[] LizFringe = { new(0.070f, 0.135f, 0.240f), new(0.185f, 0.172f, 0.130f), new(0.215f, 0.215f, -0.020f), new(0.185f, 0.225f, -0.160f), new(0.110f, 0.200f, -0.270f) };
    static float LizFringeBank(Vector3 p)
    {
        var m = Mirror(p); float d = 9f;
        for (int i = 0; i + 1 < LizFringe.Length; i++) d = Mathf.Min(d, Capsule(m, LizFringe[i], LizFringe[i + 1], 0.033f));
        return d;
    }
    static readonly int[] LizFringeCount = { 3, 5, 5, 4 };
    static float LizFringeSpines(Vector3 p)
    {
        var m = Mirror(p); float d = 9f;
        for (int s = 0; s + 1 < LizFringe.Length; s++)
        {
            Vector3 a = LizFringe[s], b = LizFringe[s + 1]; int n = LizFringeCount[s];
            for (int i = 0; i < n; i++)
            {
                var c = a + (b - a) * ((i + 0.5f) / n);
                var o = new Vector3(c.x, 0f, (c.z + 0.05f) * 0.5f).normalized * 0.92f + Vector3.up * 0.34f;
                float ln = 0.040f + 0.018f * Mathf.Sin(Mathf.PI * (i + 0.5f) / n);
                d = Mathf.Min(d, Seg(m, c + o * 0.015f, c + o * (0.03f + ln), 0.017f, 0.003f));
            }
        }
        return d;
    }
    static readonly Vector3 LizCranC = new(0f, 0.04f, -0.05f), LizCranR = new(0.245f, 0.255f, 0.28f);
    static float LizComb(Vector3 p)   // 이구아나 볏 — 정수리에서 목 뒤로, 옆으로 납작한 가시
    {
        var q = new Vector3(p.x / 0.42f, p.y, p.z); float d = 9f;
        for (int i = 0; i < 9; i++)
        {
            float a = (8f + 15f * i) * Mathf.Deg2Rad; var dr = new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a));
            var bs = LizCranC + new Vector3(0f, dr.y * LizCranR.y, dr.z * LizCranR.z);
            float ln = 0.050f + 0.065f * Mathf.Sin(Mathf.PI * i / 8f);
            d = Mathf.Min(d, Seg(q, bs - dr * 0.03f, bs + dr * ln + new Vector3(0f, 0f, -0.035f), 0.050f, 0.006f));
        }
        return d * 0.42f;
    }

    static readonly string[] LizCrests = { "Fringe", "Comb", "None" };

    static LizHead LizardHead(string crest)
    {
        bool flat = crest == "Fringe";   // 가시 테 머리는 윗면이 납작한 쐐기다
        var h = new LizHead { Skin = new Color(0.36f, 0.50f, 0.27f), Belly = new Color(0.80f, 0.76f, 0.52f), DefaultEye = "BigSlit", JawW = 0.19f };
        h.Cran = flat ? p => SMax(Ell(p, LizCranC, LizCranR), p.y - 0.238f, 0.05f) : p => Ell(p, LizCranC, LizCranR);
        h.Upper = p => Seg(Squash(p, -0.01f, 0.72f), V3(0, 0, 0.08f), V3(0, -0.035f, 0.42f), 0.190f, 0.105f) * 0.72f;
        h.Lower = p => Seg(Squash(p, -0.10f, 0.60f), V3(0, -0.10f, 0.02f), V3(0, -0.105f, 0.39f), 0.165f, 0.088f) * 0.60f;
        h.MouthY = p => { float c = Mathf.Clamp01(0.10f - p.z); return -0.082f - 0.03f * p.z + 0.9f * c * c; };   // 뒤쪽이 올라간다
        h.Eye = V3(0.172f, 0.085f, 0.135f); h.EyeR = 0.058f; h.EyeN = V3(0.80f, 0.12f, 0.58f).normalized;
        h.Extra = p =>
        {
            var m = Mirror(p); float cheek = Sph(m, V3(0.150f, -0.065f, 0f), 0.098f);
            return flat ? SMin(LizFringeBank(p), cheek, 0.03f) : SMin(Seg(m, V3(0.075f, 0.120f, 0.220f), V3(0.228f, 0.172f, 0.01f), 0.038f, 0.040f), cheek, 0.03f);
        };
        h.Pits = new[] { (V3(0.045f, 0.028f, 0.452f), 0.016f) };
        return h;
    }

    // 매끈한 뱀 — 머리는 하나, 색이 넷(사용자 그림: 보엘렌비단뱀 · 누런 구렁이 · 인디고 · 붉은 뱀).
    //                                                             살                           배                           눈 테                        무늬 색
    static readonly Dictionary<string, (Color skin, Color belly, Color ring, Color mark)> LizSnakeTones = new()
    {
        ["Boelen"]  = (new Color(0.07f, 0.08f, 0.11f), new Color(0.92f, 0.82f, 0.42f), new Color(0.80f, 0.66f, 0.26f), new Color(0.95f, 0.80f, 0.16f)),
        ["Kingrat"] = (new Color(0.90f, 0.74f, 0.18f), new Color(0.96f, 0.90f, 0.58f), new Color(0.56f, 0.22f, 0.10f), new Color(0.06f, 0.06f, 0.07f)),
        ["Indigo"]  = (new Color(0.08f, 0.09f, 0.14f), new Color(0.24f, 0.27f, 0.35f), new Color(0.84f, 0.46f, 0.20f), new Color(0.74f, 0.30f, 0.13f)),
        ["Red"]     = (new Color(0.80f, 0.25f, 0.16f), new Color(0.95f, 0.66f, 0.38f), new Color(0.10f, 0.08f, 0.08f), new Color(0.07f, 0.05f, 0.06f)),
    };

    // 길고 낮은 머리 — 윗면이 평평하고 눈 위에서 주둥이로 모서리(눈썹 판)가 선다 · 둥글게 좁아지는 주둥이 · 큰 둥근 눈(테 눈 `Ring`).
    static LizHead SnakeHead(string tone)
    {
        var (skin, belly, ring, _) = LizSnakeTones[tone];
        var h = new LizHead { Skin = skin, Belly = belly, DefaultEye = "Ring", EyeCol = ring, JawW = 0.205f };
        h.Cran = p => SMax(Ell(p, V3(0, 0, -0.07f), V3(0.200f, 0.146f, 0.27f)), p.y - 0.108f, 0.045f);
        h.Upper = p => SMax(Seg(Squash(p, -0.01f, 0.62f), V3(0, -0.005f, 0.03f), V3(0, -0.022f, 0.400f), 0.184f, 0.120f) * 0.62f, p.z - 0.505f, 0.05f);
        h.Lower = p => SMax(Seg(Squash(p, -0.085f, 0.42f), V3(0, -0.085f, -0.02f), V3(0, -0.082f, 0.385f), 0.176f, 0.108f) * 0.42f, p.z - 0.488f, 0.04f);
        h.MouthY = p => { float c = Mathf.Clamp01(0.05f - p.z); return -0.070f + 0.5f * c * c; };
        h.Eye = V3(0.170f, 0.038f, 0.205f); h.EyeR = 0.058f; h.EyeN = V3(0.90f, 0.12f, 0.40f).normalized;
        h.Extra = p =>
        {
            var m = Mirror(p);
            float jaw = Sph(m, V3(0.140f, -0.030f, -0.06f), 0.100f);                                   // 턱 뒤 — 목보다 조금 넓다
            float brow = Seg(m, V3(0.126f, 0.070f, 0.110f), V3(0.062f, 0.026f, 0.430f), 0.032f, 0.022f);   // 눈 위에서 주둥이로 서는 모서리
            return SMin(jaw, brow, 0.03f);
        };
        h.Pits = new[] { (V3(0.066f, 0.022f, 0.492f), 0.012f) };
        h.SeatExact(0.30f);
        // 누런 구렁이 — 뒤통수부터 온몸에 밑동이 검은 노란 비늘. 납작하게 누워 윤곽은 매끈하고, 겹친 자리가 검은 그물로 읽힌다.
        if (tone == "Kingrat") h.Leaves = HeadLeaves(h, 0.075f, -0.300f, 0.070f, 0.096f, 0.95f, 0.05f, 0.08f);
        return h;
    }

    // 숲살모사 — 색 넷(사용자 그림: 풀빛 · 청회색 · 숯빛 · 주황). 살은 비늘 사이로 어둡게 비치고 잎 끝이 밝다.
    //                                                             살(잎 밑동)                  잎 끝                        배 · 턱                      눈                          눈 종류
    static readonly Dictionary<string, (Color skin, Color tip, Color belly, Color eye, string eyeKind)> LizViperTones = new()
    {
        ["Green"] = (new Color(0.30f, 0.50f, 0.17f), new Color(0.80f, 0.86f, 0.40f), new Color(0.86f, 0.88f, 0.52f), new Color(0.46f, 0.74f, 0.66f), "BigSlit"),
        ["Slate"] = (new Color(0.24f, 0.36f, 0.45f), new Color(0.66f, 0.80f, 0.80f), new Color(0.78f, 0.84f, 0.62f), new Color(0.84f, 0.88f, 0.62f), "BigSlit"),
        ["Coal"]  = (new Color(0.13f, 0.15f, 0.19f), new Color(0.40f, 0.45f, 0.52f), new Color(0.36f, 0.39f, 0.44f), new Color(0.05f, 0.05f, 0.06f), "Gecko"),
        ["Amber"] = (new Color(0.80f, 0.44f, 0.10f), new Color(0.99f, 0.82f, 0.30f), new Color(0.97f, 0.86f, 0.46f), new Color(0.74f, 0.86f, 0.40f), "BigSlit"),
    };

    static readonly float[] ViperZW = { -0.33f, -0.24f, -0.10f, 0.14f, 0.325f }, ViperWW = { 0.105f, 0.160f, 0.262f, 0.152f, 0.092f };   // 볼 높이의 반폭 — 턱 모서리(z −0.10)가 가장 넓다
    static readonly float[] ViperZT = { -0.33f, -0.18f, 0.15f, 0.325f }, ViperTT = { 0.070f, 0.136f, 0.138f, 0.084f };                     // 윗면 높이 — 눈 앞에서 주둥이로 조금 내려간다
    static float ViperWedge(Vector3 p, float zf, float kw, bool top)
    {
        float x = Mathf.Abs(p.x);
        float w = Interp(p.z, ViperZW, ViperWW) * kw - 0.050f * SStep(-0.02f, 0.12f, p.y) * SStep(0.30f, -0.05f, p.z);   // 윗면은 볼보다 좁다(뒤로 갈수록 더)
        float d = SMax(x - w, p.z - zf - 0.10f * Mathf.Min(p.y, 0f), 0.075f);                                           // 뭉툭하게 잘린 주둥이
        if (top) d = SMax(d, p.y - (Interp(p.z, ViperZT, ViperTT) - 0.6f * x * x), 0.045f);                              // 평평한 윗면 · 눈썹 모서리
        return SMax(d, -0.31f - p.z, 0.09f) * 0.74f;
    }

    // **다른 뱀과 윤곽이 다르다**(사용자 11-12) — 위에서 보면 넓은 세모(좁고 뭉툭한 주둥이에서 턱 모서리까지 곧게 벌어졌다가 목에서 잘록) ·
    // 옆에서 보면 윗면이 평평하고 주둥이가 깊은 채로 뚝 잘린다 · 큰 눈이 눈썹 모서리 바로 밑 · 입선이 눈 뒤 턱 모서리까지 가며 끝이 올라간다.
    static LizHead ViperHead(string tone)
    {
        var (skin, _, belly, eye, eyeKind) = LizViperTones[tone];
        var h = new LizHead { Skin = skin, Belly = belly, DefaultEye = eyeKind, EyeCol = eye, JawW = 0.27f, ChinZ = new Vector2(-0.16f, -0.08f) };
        h.Upper = p => SMax(ViperWedge(p, 0.330f, 1f, true), -0.085f - p.y, 0.02f);   // 아래는 입선이 자른다 — 그 밑으로 조금만 남긴다(입 안 조각이 기둥이 되지 않게)
        h.Lower = p => SMax(SMax(ViperWedge(p, 0.322f, 0.97f, false), (-0.150f + 0.085f * Mathf.Clamp(p.z, -0.1f, 0.33f) + 1.3f * p.x * p.x) - p.y, 0.05f), p.y, 0.03f);
        h.MouthY = p => { float c = Mathf.Clamp01(0.06f - p.z); return -0.058f + 0.75f * c * c; };
        h.Cran = p => Ell(p, V3(0, -0.010f, -0.200f), V3(0.160f, 0.135f, 0.150f));    // 뒤통수 밑 — 목이 여기 꽂힌다
        h.Extra = p => Sph(Mirror(p), V3(0.170f, -0.030f, -0.135f), 0.088f);          // 턱 모서리(독샘) — 입선이 여기서 끝난다
        h.Eye = V3(0.150f, 0.060f, 0.150f); h.EyeR = 0.060f; h.EyeN = V3(0.92f, 0.10f, 0.38f).normalized;
        h.Pits = new[] { (V3(0.078f, 0.012f, 0.318f), 0.011f) };
        h.SeatExact(0.36f);
        h.Leaves = HeadLeaves(h, 0.275f, -0.330f, 0.050f, 0.104f, 0.92f, 0.10f, 0.26f, 0.24f);
        return h;
    }

    static float CrocSW(float z) => 0.215f + (0.150f - 0.215f) * Mathf.Clamp01((z - 0.05f) / 0.50f);   // 위턱 반폭

    // 덩치 큰 악어 — 넓은 주둥이(가늘면 약해 보인다) · 깊은 아래턱 · 눈 위에서 주둥이로 내려오는 굵은 턱 · 눈은 작고 깊다.
    static LizHead CrocHead()
    {
        var h = new LizHead { Skin = new Color(0.23f, 0.32f, 0.25f), Belly = new Color(0.74f, 0.72f, 0.54f), DefaultEye = "Glare", EyeCol = LzYellow, JawW = 0.225f };
        h.Cran = p => SMax(Ell(p, V3(0, 0, -0.07f), V3(0.265f, 0.225f, 0.27f)), p.y - 0.190f, 0.05f);   // 윗면이 납작하다
        h.Upper = p => SMin(Seg(Squash(p, -0.03f, 0.60f), V3(0, -0.01f, 0.05f), V3(0, -0.03f, 0.55f), 0.215f, 0.150f) * 0.60f,
                            Ell(p, V3(0, 0.022f, 0.590f), V3(0.100f, 0.052f, 0.085f)), 0.035f);        // 콧등 둔덕 — 콧구멍이 이 위에 뚫린다
        h.Lower = p => Seg(Squash(p, -0.135f, 0.64f), V3(0, -0.135f, 0f), V3(0, -0.13f, 0.545f), 0.205f, 0.140f) * 0.64f;
        h.MouthY = p => { float c = Mathf.Clamp01(0.10f - p.z); return -0.095f + 0.011f * Mathf.Sin(p.z * 24f) + 0.7f * c * c; };   // 물결치는 입선
        h.Eye = V3(0.175f, 0.125f, 0.105f); h.EyeR = 0.046f; h.EyeN = V3(0.78f, 0.18f, 0.60f).normalized;
        Func<Vector3, float> bas = p =>
        {
            var m = Mirror(p);
            float mound = Ell(m, V3(0.130f, 0.118f, 0.050f), V3(0.092f, 0.072f, 0.108f));                // 눈 둔덕 — 머리 윗면보다 솟지 않는다
            float snout = Seg(m, V3(0.058f, 0.050f, 0.500f), V3(0.070f, 0.095f, 0.290f), 0.020f, 0.030f);   // 주둥이 위 두 줄 능선
            float hinge = Sph(m, V3(0.190f, -0.090f, -0.04f), 0.112f);                                   // 턱 근육
            return SMin(mound, SMin(snout, hinge, 0.02f), 0.03f);
        };
        h.Extra = bas;
        h.Pits = new[] { (V3(0.036f, 0.066f, 0.605f), 0.017f) };
        h.SeatLoose(0.42f);
        // 눈 위 턱 — 앞(안쪽)이 낮다(노려본다). 살에 붙여 짧게(길면 끝이 귀처럼 솟는다).
        var t = Vector3.Cross(Vector3.up, h.EyeN).normalized; var e = h.Eye; float r = h.EyeR;
        var b0 = e + Vector3.up * (r * 0.95f) - t * 0.060f + h.EyeN * 0.012f - Vector3.up * 0.022f;
        var b1 = e + Vector3.up * (r * 0.95f) + t * 0.050f - h.EyeN * 0.015f + Vector3.up * 0.010f;
        h.Extra = p => SMin(bas(p), Seg(Mirror(p), b0, b1, 0.026f, 0.032f), 0.03f);
        return h;
    }
    static float CrocPlates(Vector3 p)   // 뒤통수 골판 — 안쪽 줄은 크게, 바깥 줄은 작게
    {
        var m = Mirror(p); float d = 9f;
        foreach (float z in new[] { -0.07f, -0.17f, -0.27f })
        {
            float q = (z + 0.07f) / 0.27f, y = Mathf.Min(0.190f, 0.225f * Mathf.Sqrt(Mathf.Max(0f, 1f - q * q)));
            d = Mathf.Min(d, Seg(m, V3(0.080f, y - 0.02f, z), V3(0.095f, y + 0.075f, z - 0.055f), 0.050f, 0.012f));
            d = Mathf.Min(d, Seg(m, V3(0.180f, y - 0.05f, z - 0.02f), V3(0.215f, y + 0.025f, z - 0.065f), 0.040f, 0.010f));
        }
        return d;
    }
    static float CrocTeeth(Vector3 p)   // 굵기와 길이가 들쭉날쭉한 송곳니 — 위는 아래로, 아래 큰 것 둘은 윗입술 밖으로 솟는다
    {
        var m = Mirror(p); float d = 9f;
        foreach (var (z, ln, r) in new[] { (0.16f, 0.070f, 0.022f), (0.25f, 0.118f, 0.031f), (0.34f, 0.072f, 0.022f), (0.43f, 0.108f, 0.029f), (0.52f, 0.078f, 0.023f) })
        {
            float w = CrocSW(z) * 0.975f;
            d = Mathf.Min(d, Seg(m, V3(w, -0.082f, z), V3(w + 0.006f, -0.082f - ln, z + 0.004f), r, 0.003f));
        }
        foreach (var (z, ln, r) in new[] { (0.295f, 0.105f, 0.027f), (0.475f, 0.090f, 0.025f) })
        {
            float w = CrocSW(z) + 0.004f;
            d = Mathf.Min(d, Seg(m, V3(w, -0.118f, z), V3(w + 0.010f, -0.118f + ln, z), r, 0.003f));
        }
        foreach (float x in new[] { 0.045f, 0.115f })
        {
            float z = 0.690f - x * x * 3.2f;
            d = Mathf.Min(d, Seg(m, V3(x, -0.082f, z), V3(x, -0.160f, z + 0.006f), 0.022f, 0.003f));
        }
        return d;
    }

    // ── 눈 — 고르기 축. 날카로운 쪽에서 둥근 쪽으로 ─────────────────────────────
    //   이름       눈동자(없으면 null)  크기   윗꺼풀 높이 · 앞으로 기운 정도   아랫꺼풀(없으면 −1)   색
    static readonly (string name, string pupil, float k, float lid, float slant, float low, Color col)[] LizEyes =
    {
        ("Sharp",   "Slit",  0.95f, 0.02f, 0.80f, 0.50f, new Color(0.95f, 0.72f, 0.16f)),   // 가늘게 찢어진 눈 — 위아래 꺼풀이 다 덮는다
        ("Glare",   "Slit",  1.00f, 0.28f, 0.55f, -1f,   new Color(0.95f, 0.72f, 0.16f)),   // 노려보는 눈
        ("Almond",  "Slit",  1.08f, 0.52f, 0.30f, 0.80f, new Color(0.62f, 0.82f, 0.25f)),   // 중간 — 꺼풀이 살짝
        ("BigSlit", "Slit",  1.24f, 0.90f, 0.08f, -1f,   new Color(0.62f, 0.58f, 0.52f)),   // 크고 둥근 눈에 세로 눈동자(볏도마뱀붙이)
        ("Round",   "Round", 1.14f, 0.70f, 0.15f, -1f,   new Color(0.95f, 0.72f, 0.16f)),   // 둥근 눈동자
        ("Gecko",   null,    1.24f, 0.90f, 0.08f, -1f,   new Color(0.05f, 0.05f, 0.06f)),   // 크고 검은 눈 + 반짝임
        ("Blank",   null,    1.00f, 0.28f, 0.55f, -1f,   new Color(0.80f, 0.87f, 0.92f)),   // 눈동자 없는 흐린 눈
        ("Ring",    "Ring",  1.00f, 0.86f, 0.06f, -1f,   new Color(0.95f, 0.72f, 0.16f)),   // 큰 둥근 눈동자에 가는 테 + 반짝임(매끈한 뱀)
    };

    static float LizLid(Vector3 p, LizHead h, float k, float lid, float slant, float low)
    {
        var t = Vector3.Cross(Vector3.up, h.EyeN).normalized;
        var q = Mirror(p) - h.Eye; float r = h.EyeR * k, ball = q.magnitude - (r + 0.009f), a = Vector3.Dot(q, t);
        float d = Mathf.Max(ball, (r * lid + slant * a) - q.y);
        if (low >= 0f) d = Mathf.Min(d, Mathf.Max(ball, q.y - (-r * low + 0.25f * a)));
        return d;
    }
    static float LizPupil(Vector3 p, LizHead h, float k, string kind)
    {
        var t = Vector3.Cross(Vector3.up, h.EyeN).normalized; var u = Vector3.Cross(h.EyeN, t);   // t = 눈의 바깥 · 뒤쪽 · u = 위
        var q = Mirror(p) - h.Eye; float r = h.EyeR * k;
        float ball = q.magnitude - (r + 0.004f), fwd = r * 0.35f - Vector3.Dot(q, h.EyeN), a = Vector3.Dot(q, t), b = Vector3.Dot(q, u);
        float shape = kind == "Slit" ? Mathf.Max(Mathf.Abs(a) - r * 0.16f, Mathf.Abs(b) - r * 0.74f) : Mathf.Sqrt(a * a + b * b) - r * (kind == "Ring" ? 0.70f : 0.48f);
        return Mathf.Max(Mathf.Max(ball, fwd), shape);
    }
    static float LizGlint(Vector3 p, LizHead h, float k)
    {
        var t = Vector3.Cross(Vector3.up, h.EyeN).normalized; var u = Vector3.Cross(h.EyeN, t); float r = h.EyeR * k;
        return Sph(Mirror(p), h.Eye + (h.EyeN * 0.80f + u * 0.40f - t * 0.28f) * r, r * 0.21f);
    }

    // ── 색마다 얹는 칠 ──────────────────────────────────────────────────────

    // 매끈한 뱀 — 입술 띠(입선에서 lo~hi)와 그 안의 세로 줄.
    static float SnakeLipBand(LizHead h, Vector3 p, float lo, float hi)
    {
        float yv = p.y - h.MouthY(p);
        return SStep(lo - 0.012f, lo, yv) * SStep(hi + 0.012f, hi, yv) * SStep(-0.02f, 0.04f, p.z) * SStep(0.08f, 0.12f, Mathf.Abs(p.x)) * SStep(0.45f, 0.41f, p.z);
    }
    static float SnakeLipBars(LizHead h, Vector3 p, float lo, float hi, float k = 62f, float th = 0.45f)
        => SStep(th - 0.15f, th + 0.15f, Mathf.Sin(p.z * k + 0.6f)) * SnakeLipBand(h, p, lo, hi);
    // 눈에서 입술로 내려오는 줄 둘 + 눈 뒤로 비껴 내려가는 줄 하나.
    static float SnakeTears(LizHead h, Vector3 p, float w)
    {
        float ez = h.Eye.z, ey = h.Eye.y, er = h.EyeR;
        float d = Mathf.Min(Line2(p.z, p.y, ez + 0.030f, ey - er * 0.8f, ez + 0.062f, -0.068f), Line2(p.z, p.y, ez - 0.030f, ey - er * 0.8f, ez - 0.075f, -0.066f));
        d = Mathf.Min(d, Line2(p.z, p.y, ez - er * 0.9f, ey - 0.012f, ez - 0.215f, -0.050f));
        return p.y > h.MouthY(p) - 0.004f ? SStep(w, w * 0.6f, d) * SStep(0.07f, 0.10f, Mathf.Abs(p.x)) : 0f;
    }
    // 머리 살에 정점 색으로 얹는 넓은 칠(가는 줄은 덧판 — `SnakeMarks`).
    static Func<Vector3, Color, Color> SnakeWide(LizHead h, string tone)
    {
        var mark = LizSnakeTones[tone].mark;
        if (tone == "Boelen") return (p, col) => Color.Lerp(col, mark, SnakeLipBand(h, p, -0.060f, 0f));   // 아랫입술 — 노란 바탕
        if (tone == "Indigo") return (p, col) => Color.Lerp(col, mark, SStep(0.050f, 0.020f, p.y - h.MouthY(p)) * SStep(-0.10f, 0.02f, p.z));   // 턱 · 입술이 녹슨 붉은색
        return null;
    }
    // 가는 줄 — (칠 몫 0~1 · 색) 둘까지. 살 위 1cm에 띄운 덧판으로 굽는다.
    static List<(Func<Vector3, float> mask, Color col)> SnakeMarks(LizHead h, string tone)
    {
        var (skin, _, _, mark) = LizSnakeTones[tone];
        var list = new List<(Func<Vector3, float>, Color)>();
        switch (tone)
        {
            case "Boelen":    // 윗입술 — 검은 바탕에 노란 줄 · 아랫입술 — 노란 바탕에 검은 줄
                list.Add((p => SnakeLipBars(h, p, 0f, 0.052f), mark));
                list.Add((p => { float band = SnakeLipBand(h, p, -0.060f, 0f); return band * (1f - SnakeLipBars(h, p, -0.060f, 0f, th: -0.35f)); }, skin));
                break;
            case "Kingrat":   // 눈 밑 줄 · 입술 줄(비늘의 검은 테는 빚은 비늘이 낸다)
                list.Add((p => Mathf.Max(SnakeTears(h, p, 0.009f), SnakeLipBars(h, p, -0.045f, 0.045f, 58f, 0.88f)), mark));
                break;
            case "Indigo":    // 녹슨 턱 위의 검은 줄
                list.Add((p => SnakeLipBars(h, p, -0.060f, 0.045f, 58f, 0.55f) * SStep(0.050f, 0.020f, p.y - h.MouthY(p)) * SStep(-0.10f, 0.02f, p.z), Mul(skin, 0.8f)));
                break;
            default:          // Red — 눈 밑 · 눈 뒤 검은 줄
                list.Add((p => Mathf.Max(SnakeTears(h, p, 0.013f), SnakeLipBars(h, p, -0.040f, 0.040f, 58f, 0.86f) * SStep(0.30f, 0.20f, p.z)), mark));
                break;
        }
        return list;
    }
    // 몸의 칠(몸 좌표 · 배 색까지 정해진 색 → 새 색). 배 색은 안 건드린다.
    static Func<Vector3, Color, Color> LizBodyPaint(LizFrame F, string kind, string tone, Color skin)
    {
        if (kind == "Snake")
        {
            var mark = LizSnakeTones[tone].mark;
            if (tone == "Boelen" || tone == "Indigo")
                return (p, col) =>
                {
                    bool back = col.r + col.g + col.b < 0.6f;
                    if (!back) return col;
                    float w = 0.035f * Mathf.Sin(p.y * 11f + p.z * 8f);   // 검푸른 살의 옅은 무지갯빛
                    col = new Color(Mathf.Clamp01(col.r - 0.5f * w), Mathf.Clamp01(col.g + 0.8f * w), Mathf.Clamp01(col.b + 1.4f * w), 1f);
                    if (tone != "Boelen") return col;
                    F.Along(p, out float tailw); float x = Mathf.Abs(p.x);   // 옆구리 · 꼬리의 노란 빗금
                    float flank = SStep(0.86f, 0.95f, Mathf.Sin(p.y * 24f + x * 16f - p.z * 9f)) * SStep(0.07f, 0.15f, x) * (1f - tailw);
                    float tailb = SStep(0.86f, 0.95f, Mathf.Sin(p.z * 26f + p.y * 12f)) * tailw;
                    return Color.Lerp(col, mark, Mathf.Max(flank, tailb));
                };
            if (tone == "Red")   // 붉은 머리에서 꼬리로 갈수록 검붉은 잿빛
                return (p, col) => ColDiff(col, skin) < 0.25f ? Color.Lerp(col, new Color(0.23f, 0.15f, 0.18f), SStep(F.Top + 0.05f, F.D.Lift - 0.35f, F.Along(p, out _)) * 0.82f) : col;
            return null;
        }
        if (kind == "Viper" && tone == "Amber")   // 주황 — 등을 따라 녹슨 얼룩 띠
            return (p, col) =>
            {
                if (ColDiff(col, skin) >= 0.2f) return col;
                float k = SStep(0.55f, 0.85f, Mathf.Sin(F.Along(p, out _) * 30f + 0.4f * Mathf.Sin(Mathf.Abs(p.x) * 30f)));
                return Color.Lerp(col, new Color(0.62f, 0.24f, 0.08f), 0.45f * k);
            };
        return null;
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    // 프리팹 한 벌 — 종류 · 색. `Kind`는 Lizard · Croc · Viper · Snake.
    readonly struct LizOutfit
    {
        public readonly string Prefab, Kind, Tone;
        public LizOutfit(string prefab, string kind, string tone) { Prefab = prefab; Kind = kind; Tone = tone; }
        public string Key => Kind + Tone;
    }
    static readonly LizOutfit[] Lizards =
    {
        new("Chibi_Lizard", "Lizard", ""), new("Chibi_Lizard_Croc", "Croc", ""),
        new("Chibi_Lizard_ViperGreen", "Viper", "Green"), new("Chibi_Lizard_ViperSlate", "Viper", "Slate"),
        new("Chibi_Lizard_ViperCoal", "Viper", "Coal"), new("Chibi_Lizard_ViperAmber", "Viper", "Amber"),
        new("Chibi_Lizard_SnakeBoelen", "Snake", "Boelen"), new("Chibi_Lizard_SnakeKingrat", "Snake", "Kingrat"),
        new("Chibi_Lizard_SnakeIndigo", "Snake", "Indigo"), new("Chibi_Lizard_SnakeRed", "Snake", "Red"),
    };
    //                                                              체형          머리 배수  굽힘    목 굵기  꼬리 굵기
    static LizFrame LizFrameOf(string kind) => kind switch
    {
        "Croc"  => new LizFrame(CrocShape,   1.16f, 0.34f, 0.215f, 1.38f),
        "Snake" => new LizFrame(LizardShape, 1.00f, 0.30f, 0.160f, 1.10f),
        "Viper" => new LizFrame(LizardShape, 1.00f, 0.30f, 0.122f, 0.95f),
        _       => new LizFrame(LizardShape, 1.00f, 0.30f, 0.150f, 1.00f),
    };

    // 뽑은 표면(모양) — 종류마다 한 번만 뽑고 색마다 다시 칠한다. 열쇠는 `{종류}/{조각}`.
    static readonly Dictionary<string, Surface> _lizShapes = new();
    const string LizSkinMat = "LizSkin", LizLine = "OutlineOuter";

    // 재질 사본 — 흰 살(정점 색을 곱한다) · 뼈 · 먹 · 입 안 · 반짝임. 눈 · 무늬는 색마다(`LizColorMat`). 큰 짐승처럼 몸 번호만 적는다(`BodyStencil`).
    static Dictionary<string, Material> LizardMaterials(Dictionary<string, Material> mats)
    {
        var m = OutfitMaterials(mats, "Lizard", LzCloth, LzLeather, null, null);
        m[LizSkinMat] = Variant(mats["SkinOuter"], "Chibi_LizSkin", "_BaseColor", Color.white);
        m[LizSkinMat].SetFloat("_VertexAO", 1f);
        m["LizBone"]  = Variant(mats["Bone"],     "Chibi_LizBone",  "_BaseColor", LzBone);
        m["LizDark"]  = Variant(mats["NoseDark"], "Chibi_LizDark",  "_BaseColor", LzDark);
        m["LizMouth"] = Variant(mats["NoseDark"], "Chibi_LizMouth", "_BaseColor", LzMouth);
        m["LizWhite"] = Variant(mats["Bone"],     "Chibi_LizWhite", "_BaseColor", LzWhite);
        foreach (var k in new[] { "LizBone", "LizDark", "LizMouth", "LizWhite" }) BodyStencil(m[k]);
        EditorUtility.SetDirty(m[LizSkinMat]);
        return m;
    }
    // 한 색 재질(눈알 · 무늬 덧판) — 색이 이름이다(같은 색은 한 벌).
    static string LizColorMat(Dictionary<string, Material> mats, Color c)
    {
        string key = "LizCol_" + ColorUtility.ToHtmlStringRGB(c);
        if (!mats.ContainsKey(key)) { mats[key] = Variant(mats["LizWhite"], "Chibi_" + key, "_BaseColor", c); BodyStencil(mats[key]); }
        return key;
    }

    // 리자드맨 한 벌 — 몸(관절마다 조각)과 머리 부위. 모양은 종류마다 한 번 뽑아 두고(`_lizShapes`) 색만 다시 칠한다.
    static (BoneBody body, List<Part> head) LizardFor(LizOutfit o, Dictionary<string, Material> mats)
    {
        var F = LizFrameOf(o.Kind); var D = F.D;
        bool lin = PlayerSettings.colorSpace == ColorSpace.Linear;
        var bb = new BoneBody { ArmX = D.ArmX, ArmZ = F.ArmZ, HeadInPlace = true };
        var head = new List<Part>();

        // 머리 — 도마뱀은 볏마다 머리통이 다르다(가시 테는 납작한 쐐기 + 둑). 나머지는 하나.
        var heads = new Dictionary<string, LizHead>();
        if (o.Kind == "Lizard") foreach (var c in LizCrests) heads[c] = LizardHead(c == "None" ? "Comb" : c);
        else heads[""] = o.Kind == "Croc" ? CrocHead() : o.Kind == "Viper" ? ViperHead(o.Tone) : SnakeHead(o.Tone);
        LizHead H = null; foreach (var v in heads.Values) { H = v; break; }   // 눈 · 입 · 색은 어느 머리나 같다
        Color skin = H.Skin, belly = H.Belly;

        // 머리 좌표 ↔ 몸 좌표.
        Vector3 ToHead(Vector3 p) => (p - F.HO) / F.HK;
        Func<Vector3, float> Hd(Func<Vector3, float> f) => p => f(ToHead(p)) * F.HK;
        Bounds HB(float x, float y0, float y1, float z0, float z1)   // 머리 좌표의 상자(좌우 대칭) → 몸 좌표
            => new(F.HO + new Vector3(0f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f) * F.HK, new Vector3(2f * x, y1 - y0, z1 - z0) * F.HK);

        // 그늘은 굽지 않는다 — 미리보기에 없고, 흰 살 재질은 정점 색을 그대로 곱해 오목한 자리(입선 · 겨드랑이)가 얼룩진다.
        Func<Vector3, float> noScene = _ => 9f;

        // 모양 — 종류마다 한 번. 살은 편다(바깥 선의 가시가 안 서게).
        Surface Net(string piece, Func<Vector3, float> f, Bounds b, float step, float keep, bool relax, float tol = 0f)
        {
            string key = $"{o.Kind}/{piece}";
            if (_lizShapes.TryGetValue(key, out var have)) return have;
            var s = SurfaceNets(f, b, step, noScene, p => p, 0.04f, 0f, keep, tol);
            if (relax) Relax(s.Verts, s.Tris, 6);
            return _lizShapes[key] = s;
        }
        // 정점 색 = 색(선형 색 공간이면 선형 값) — 뽑은 표면의 그늘 칸은 1이다.
        Surface Tinted(Surface s, Func<Vector3, Color> col)
        {
            var c = new List<Color>(s.Colors.Count);
            for (int i = 0; i < s.Verts.Count; i++)
            {
                var want = col(s.Verts[i]); if (lin) want = want.linear;
                c.Add(new Color(want.r, want.g, want.b, 1f));
            }
            return new Surface(s.Verts, s.Normals, c, s.Tris);
        }
        string MeshName(string piece, bool toned) => $"Chibi_Lizard_{(toned ? o.Key : o.Kind)}_{piece.Replace('.', '_').Replace('@', '_').Replace("!", "")}";
        // 살 조각 — 색마다 메시가 따로(정점 색). 흰 살 재질 + 바깥 선.
        Part Flesh(string name, Func<Vector3, float> f, Bounds b, float step, float keep, Func<Vector3, Color> col, bool on = true, bool relax = true, string shape = null, float tol = 0f)
            => new(name, SaveSkull(MeshName(name, true), Tinted(Net(shape ?? name, f, b, step, keep, relax, tol), col)), Vector3.zero, Vector3.one, LizSkinMat, on, outerMat: LizLine);
        // 한 색 조각 — 색을 안 타는 메시(종류마다 한 벌).
        Part Plain(string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep, bool on = true, string shape = null)
            => new(name, SaveSkull(MeshName(shape ?? name, false), Net(shape ?? name, f, b, step, keep, false)), Vector3.zero, Vector3.one, mat, on, outerMat: line);
        void AddLeaves(List<Part> to, string name, List<LizLeaf> leaves, int side, Color dark, Color tip, float e0, float e1)
        {
            if (leaves.Count == 0) return;
            to.Add(new Part(name, SaveSkull(MeshName(name, true), LeafSurface(leaves, side, dark, tip, e0, e1)), Vector3.zero, Vector3.one, LizSkinMat, outerMat: LizLine));
        }

        // ── 몸 ──
        var paint = LizBodyPaint(F, o.Kind, o.Tone, skin);
        Color BodyC(Vector3 p) { var c = F.BodyCol(p, skin, belly); return paint != null ? paint(p, c) : c; }
        const float Keep = 0.5f;
        float halfMax = 0f; foreach (float hf in D.Half) halfMax = Mathf.Max(halfMax, hf);
        Bounds Span(Vector3 a, Vector3 b, float pad) { var bd = new Bounds(a, Vector3.zero); bd.Encapsulate(b); bd.Expand(pad * 2f); return bd; }

        var torsoB = Span(new Vector3(-halfMax, D.Lift, -halfMax * D.Depth), new Vector3(halfMax, F.Top, halfMax * D.Depth + F.Zs(F.Top)), 0.08f);
        torsoB.Encapsulate(Span(F.NeckA, F.NeckB, F.NeckR * 1.18f + 0.05f)); torsoB.Encapsulate(Span(F.Tail[0], F.Tail[3], F.TailR[0] + 0.06f));
        bb.Parts.Add(Flesh("Spine.Liz.Torso", F.TorsoPiece, torsoB, 0.014f, Keep, BodyC));
        var tailB = Span(F.Tail[0], F.Tail[12], F.TailR[0] + 0.05f);
        bb.Parts.Add(Flesh("Hips.Liz.Tail", p => F.TailSdf(p), tailB, 0.012f, Keep, BodyC));
        bb.Parts.Add(Plain("Hips.Liz.Belt", "Pants", LizLine, F.Belt, Span(new Vector3(-halfMax, D.HemY - 0.03f, -halfMax * D.Depth), new Vector3(halfMax, D.HemY + 0.07f, halfMax * D.Depth + 0.05f), 0.05f), 0.008f, 0.4f));
        {
            float zf = F.TorsoR(D.HemY) * D.Depth;
            bb.Parts.Add(Plain("Hips.Liz.Flap", "Shirt", LizLine, F.Flap, Span(new Vector3(-0.16f, D.HemY - 0.35f, zf - 0.02f), new Vector3(0.16f, D.HemY + 0.04f, zf + 0.04f), 0.03f), 0.006f, 0.4f));
        }

        foreach (int sd in Sides)
        {
            string lr = sd < 0 ? "L" : "R"; int k = sd;
            Func<Vector3, float> On(Func<Vector3, float> f) => p => f(Side(p, k));
            Bounds SB(Vector3 a, Vector3 b, float pad) => Span(Side(a, k), Side(b, k), pad);
            float ar = D.ArmR, lg = D.LegR, hr = D.Hand * 0.5f;
            bb.Parts.Add(Flesh($"Arm{lr}.Liz.Upper",     On(F.Upper), SB(F.Elbow, F.Shoulder, ar + 0.04f), 0.012f, Keep, BodyC));
            bb.Parts.Add(Flesh($"LowerArm{lr}.Liz.Fore", On(F.Fore),  SB(F.Wrist, F.Elbow, ar + 0.04f), 0.012f, Keep, BodyC));
            bb.Parts.Add(Flesh($"Hand{lr}.Liz.Hand",     On(F.Hand),  SB(F.HandC, F.HandC, hr + 0.04f), 0.010f, Keep, BodyC));
            bb.Parts.Add(Plain($"Hand{lr}.Liz.Claws", "LizBone", LizLine, On(F.HandClaws),
                               SB(F.HandC + new Vector3(-0.09f, -hr * 1.5f, 0.02f), F.HandC + new Vector3(0.09f, -hr * 0.4f, 0.11f), 0.03f), 0.004f, 0.5f));
            bb.Parts.Add(Flesh($"Leg{lr}.Liz.Thigh",     On(F.Thigh), SB(F.Hip, F.Knee, lg * 1.22f + 0.04f), 0.012f, Keep, BodyC));
            bb.Parts.Add(Flesh($"LowerLeg{lr}.Liz.Shin", On(F.Shin),  SB(F.Knee, F.Heel, lg + 0.04f).Also(SB(F.Heel, F.Ball, lg * 0.7f + 0.04f)), 0.012f, Keep, BodyC));
            float fk = lg / 0.106f;
            bb.Parts.Add(Flesh($"Foot{lr}.Liz.Foot",     On(F.Foot),  SB(F.Ball + new Vector3(-0.15f * fk, -0.06f, -0.09f * fk), F.Ball + new Vector3(0.15f * fk, 0.06f, 0.26f * fk), 0.03f), 0.008f, Keep, BodyC));
            bb.Parts.Add(Plain($"Foot{lr}.Liz.Claws", "LizBone", LizLine, On(F.FootClaws),
                               SB(F.Ball + new Vector3(-0.14f * fk, -0.07f, 0.18f * fk), F.Ball + new Vector3(0.14f * fk, 0.01f, 0.31f * fk), 0.03f), 0.004f, 0.5f));
        }

        // 비늘 — 살모사 · 누런 구렁이는 잎, 도마뱀 · 악어는 등줄기 돌기, 나머지 뱀은 매끈하다.
        bool viper = o.Kind == "Viper", kingrat = o.Kind == "Snake" && o.Tone == "Kingrat";
        if (viper || kingrat)
        {
            Color dark = viper ? Mul(skin, 0.92f) : LizSnakeTones[o.Tone].mark, tip = viper ? LizViperTones[o.Tone].tip : skin;
            float e0 = viper ? 0.25f : 0.12f, e1 = viper ? 0.95f : 0.42f;
            var L = BodyLeaves(F, viper ? 1f : 0.28f);
            AddLeaves(bb.Parts, "Spine.Liz.Scales", L.Back, 0, dark, tip, e0, e1);
            AddLeaves(bb.Parts, "Hips.Liz.TailScales", L.Tail, 0, dark, tip, e0, e1);
            foreach (int sd in Sides)
            {
                string lr = sd < 0 ? "L" : "R";
                AddLeaves(bb.Parts, $"Arm{lr}.Liz.Scales", L.ArmUp, sd, dark, tip, e0, e1);
                AddLeaves(bb.Parts, $"LowerArm{lr}.Liz.Scales", L.ArmLow, sd, dark, tip, e0, e1);
                AddLeaves(bb.Parts, $"Leg{lr}.Liz.Scales", L.Thigh, sd, dark, tip, e0, e1);
            }
            var hl = new List<LizLeaf>(); foreach (var f in H.Leaves) hl.Add(f.Placed(F.HO, F.HK));
            AddLeaves(head, "Head.Liz.Scales", hl, 0, dark, tip, e0, e1);
        }
        else if (o.Kind != "Snake")
        {
            var rc = o.Kind == "Lizard" ? new Color(0.24f, 0.36f, 0.20f) : new Color(0.14f, 0.21f, 0.16f);
            var rb = Span(new Vector3(-0.08f, D.Lift + 0.15f, F.BackZ(D.Lift + 0.4f) - 0.14f), new Vector3(0.08f, F.Top + 0.02f, F.BackZ(F.Top - 0.08f) + 0.06f), 0.03f);
            bb.Parts.Add(Flesh("Spine.Liz.Ridge", F.RidgeBack, rb, 0.006f, Keep, _ => rc, relax: false));
            var rt = Span(F.Tail[1], F.Tail[9], F.TailR[1] * 1.6f + 0.06f);
            bb.Parts.Add(Flesh("Hips.Liz.TailRidge", F.RidgeTail, rt, 0.006f, Keep, _ => rc, relax: false));
        }

        // ── 머리 ──
        // **목 관절 자리** — 안 보이는 `Head`의 밑이 목이다(`DressSkinned`).
        head.Add(new Part("Head", null, F.HO, Vector3.one * 0.30f * F.HK, "HeadSkin", on: false));
        var wide = o.Kind == "Snake" ? SnakeWide(H, o.Tone) : null;
        var skullB = o.Kind == "Croc" ? HB(0.34f, -0.30f, 0.30f, -0.40f, 0.76f) : HB(0.32f, -0.26f, 0.33f, -0.40f, 0.56f);
        foreach (var (crest, hh) in heads)
        {
            var h1 = hh;
            string name = crest == "" ? "Head.Liz.Skull" : $"Head.Pick.Crest.{crest}.Skull";
            // 이구아나 볏 · 볏 없음은 머리통이 같다(모양 한 벌).
            head.Add(Flesh(name, Hd(h1.Skull), skullB, 0.006f * F.HK, 0.45f, p => h1.Col(ToHead(p), wide), on: crest == "" || crest == "Fringe",
                           shape: crest == "None" ? "Head.Pick.Crest.Comb.Skull" : null, tol: o.Kind == "Snake" ? 0.001f : 0f));   // 덧판을 얹는 머리는 줄일 때 1mm를 지킨다
        }
        head.Add(Plain("Head.Liz.Mouth", "LizMouth", null, Hd(H.Inner), skullB, 0.008f * F.HK, 0.3f));
        if (o.Kind == "Lizard")
        {
            Color fringe = new(0.74f, 0.76f, 0.50f), comb = new(0.24f, 0.36f, 0.20f);
            head.Add(Flesh("Head.Pick.Crest.Fringe.Spines", Hd(LizFringeSpines), HB(0.32f, 0.08f, 0.32f, -0.36f, 0.30f), 0.004f, Keep, _ => fringe, relax: false));
            head.Add(Flesh("Head.Pick.Crest.Comb.Spines", Hd(LizComb), HB(0.06f, -0.24f, 0.46f, -0.52f, 0.10f), 0.005f, Keep, _ => comb, on: false, relax: false));
        }
        if (o.Kind == "Croc")
        {
            var pc = new Color(0.14f, 0.21f, 0.16f);
            head.Add(Flesh("Head.Liz.Plates", Hd(CrocPlates), HB(0.28f, 0.0f, 0.32f, -0.40f, 0.0f), 0.006f * F.HK, Keep, _ => pc, relax: false));
            head.Add(Plain("Head.Liz.Teeth", "LizBone", LizLine, Hd(CrocTeeth), HB(0.26f, -0.24f, 0.02f, 0.10f, 0.74f), 0.004f * F.HK, 0.5f));
        }
        // 가는 줄 — 살 위 1cm에 띄운 덧판(머리 살은 줄이고 편 것이라 2~5mm로는 겹쳐 비친다).
        if (o.Kind == "Snake")
        {
            int n = 0;
            foreach (var (mask, col) in SnakeMarks(H, o.Tone))
            {
                Func<Vector3, float> f = q =>
                {
                    float sh = Mathf.Abs(H.Skull(q) - 0.007f) - 0.004f;
                    return sh > 0.02f ? sh : SMax(sh, (0.5f - mask(q)) * 0.03f, 0.002f);
                };
                string nm = $"Head.Liz.Marks{(char)('A' + n++)}";
                var s = SurfaceNets(Hd(f), HB(0.27f, -0.18f, 0.12f, -0.14f, 0.50f), 0.003f, noScene, p => p, 0.04f, 0f, 0.6f);
                head.Add(new Part(nm, SaveSkull(MeshName(nm, true), s), Vector3.zero, Vector3.one, LizColorMat(mats, col)));
            }
        }
        // 눈 — 고르기 축 여덟. 눈꺼풀(살) · 눈알 · 눈동자 · 반짝임. 눈알 색은 눈 표의 색 — 이 종류의 기본 눈만 제 색(`EyeCol`).
        foreach (var e in LizEyes)
        {
            var ev = e; bool on = ev.name == H.DefaultEye;
            float r = H.EyeR * ev.k;
            Bounds EB(float pad) => new(F.HO + new Vector3(0f, H.Eye.y, H.Eye.z) * F.HK, new Vector3(2f * (H.Eye.x + r + pad), 2f * (r + pad), 2f * (r + pad)) * F.HK);
            Color ball = on && H.EyeCol is Color own ? own : ev.col;
            head.Add(Flesh($"Head.Pick.Eyes.{ev.name}.Lid", Hd(q => LizLid(q, H, ev.k, ev.lid, ev.slant, ev.low)), EB(0.03f), 0.004f, Keep, _ => skin, on, relax: false));
            head.Add(Plain($"Head.Pick.Eyes.{ev.name}.Ball", LizColorMat(mats, ball), LizLine, Hd(q => Sph(Mirror(q), H.Eye, r)), EB(0.02f), 0.004f, 0.5f, on,
                           shape: $"Head.Eyes.Ball{ev.k:F2}"));
            if (ev.pupil != null)
                head.Add(Plain($"Head.Pick.Eyes.{ev.name}.Pupil", "LizDark", null, Hd(q => LizPupil(q, H, ev.k, ev.pupil)), EB(0.02f), 0.0025f, 0.5f, on,
                               shape: $"Head.Eyes.Pupil{ev.pupil}{ev.k:F2}"));
            if (ev.name == "Gecko" || ev.pupil == "Ring" || ball == LzBlack)
                head.Add(Plain($"Head.Pick.Eyes.{ev.name}.Glint", "LizWhite", null, Hd(q => LizGlint(q, H, ev.k)), EB(0.02f), 0.002f, 0.6f, on,
                               shape: $"Head.Eyes.Glint{ev.k:F2}"));
        }
        return (bb, head);
    }

    static Bounds Also(this Bounds a, Bounds b) { a.Encapsulate(b); return a; }

    // 리자드맨 얼굴 비우기(큰 짐승과 같다) + 기본 눈 40%. 살색은 정점 색이라 `SkinLook`을 안 붙인다.
    static Action<GameObject> LizardLook(string defaultEye) => root =>
    {
        if (!root.TryGetComponent(out PawnLook look)) return;
        var so = new SerializedObject(look);
        foreach (var f in new[] { "shapeOverride", "hairOverride", "earOverride", "noseOverride", "tuskOverride" })
            so.FindProperty(f).stringValue = "";
        so.FindProperty("fittedTo").stringValue = "";
        so.ApplyModifiedPropertiesWithoutUndo();
        look.SetPickChances(new[] { new PawnLook.PickChance("Eyes", defaultEye, 0.4f) });
        EditorUtility.SetDirty(look);
        look.Apply();
    };

    // 보기 몸 열 벌 — 종족 데이터가 아직 없어 **어디서도 안 가리키는** 프리팹(`Lizard/Chibi_Lizard…`)에 굽는다(드워프 · 엘프와 같은 길).
    static int DressLizards(Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases)
    {
        _lizShapes.Clear();
        var lm = LizardMaterials(mats);
        int made = 0;
        foreach (var o in Lizards)
        {
            string dp = PawnPrefabPaths.PathOf(o.Prefab);
            PawnPrefabPaths.Ensure(PawnPrefabPaths.FolderOf(o.Prefab));
            if (System.IO.File.Exists(dp)) { System.IO.File.Copy(Source, dp, true); AssetDatabase.ImportAsset(dp, ImportAssetOptions.ForceUpdate); }
            else                           { AssetDatabase.CopyAsset(Source, dp); AssetDatabase.ImportAsset(dp); }
            var (body, head) = LizardFor(o, lm);
            string eye = o.Kind == "Lizard" ? "BigSlit" : o.Kind == "Croc" ? "Glare" : o.Kind == "Snake" ? "Ring" : LizViperTones[o.Tone].eyeKind;
            var heads = new Dictionary<string, List<Part>> { ["Male"] = head };
            if (Dress(dp, o.Kind == "Croc" ? CrocShape : LizardShape, heads, lm, atlases, isSource: false, LizardLook(eye), body)) made++;
        }
        _lizShapes.Clear();
        return made;
    }

    // **리자드맨만 다시 굽는다**(드워프 `Pawn/10`과 같은 길) — 얼굴 아틀라스는 구워 둔 것을 읽는다(없으면 `Pawn/3` 먼저).
    // 옛 리자드맨 메시(이번에 안 만든 `Chibi_Lizard_`)만 치운다 — 다른 폰의 메시는 안 건드린다.
    [MenuItem("DungeonColony/Pawn/16. Create Lizardfolk Only (리자드맨만 — 다른 폰은 안 건드린다)")]
    public static void CreateLizardOnly()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var toon = Shader.Find(ToonShader); var faceSh = Shader.Find(FaceShader); var outer = Shader.Find(OuterShader);
        if (toon == null || faceSh == null || outer == null) { Debug.LogError("[리자드맨] 셰이더를 못 찾았다."); return; }
        var atlases = new Dictionary<string, ChibiFaceAtlas>();
        foreach (var kind in new[] { "Eyes", "Mouths" })
        {
            var a = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>($"{MatDir}/Chibi_{kind}Atlas.asset");
            if (a == null) { Debug.LogError($"[리자드맨] 얼굴 아틀라스({kind})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return; }
            atlases[kind] = a;
        }
        var mats = EnsureMaterials(toon, faceSh, outer, atlases);

        _saved.Clear();
        NetsTimeReport();
        Heads = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        int made = DressLizards(mats, atlases);

        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!_saved.Contains(path) && System.IO.Path.GetFileName(path).StartsWith("Chibi_Lizard_")) AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[리자드맨] 보기 몸 {made}/{Lizards.Length}벌(`{PawnPrefabPaths.FolderOf(Lizards[0].Prefab)}`) — 다른 폰은 그대로 · {watch.Elapsed.TotalSeconds:F0}초 · {NetsTimeReport()}");
    }
}
