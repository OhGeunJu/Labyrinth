using System;
using System.Collections.Generic;
using UnityEngine;

// **해골 몸 — 뼈를 빚어 관절마다 붙인다**(2026-10-27, 사용자: *"몸이랑 색이 좀 다르네 … 몸도 새로 만들어주면 좋을 거 같은데 뼈다귀로"*).
//
// 전에는 해골도 사람 치비 몸(스킨 메시)이었고 셔츠 · 바지 칸을 뼈 색으로 칠했다 — 머리(`BoneLook`)와 색이 어긋났다.
// 이제 몸도 해골 머리처럼 **거리장으로 빚어 표면을 뽑는다**(`SurfaceNets`). 모양은 파이썬 렌더로 사용자가 판마다 고쳤다
// (v1 해부도 → v2·v3 굵게 · 단순하게(*"짧뚱"*) → v4 띄움 → **v5**). **수치를 고치면 그 그림과 어긋난다** — 고칠 때는 다시 그려 볼 것.
//
//   - 뼈는 **얇다**(v1 굵기 · 끝은 둥근 머리 하나) · 갈비 넷 · 골반은 작게 · 쇄골은 v1의 1.5배.
//   - 손발은 **진짜 뼈**(손가락 넷 + 엄지 · 발가락 넷 — v1 모양을 1.2~1.25배).
//   - 목은 짧고 머리를 내려 붙인다(`SkullOriginY` — 사람 머리 자리에 두면 머리가 떠 보였다).
//   - 팔다리 길이는 사람 치비와 같다(관절이 같다) · 팔만 몸에서 조금 뗀다(`BoneArmX` — 갈비에 붙어 보였다).
//
// ## 어기면 조용히 깨지는 것
//
// - **뼈는 휘지 않으니 스킨 메시가 아니라 관절마다 굳은 조각이다** — 이름이 `관절.Bone.무엇`이라 리거 규칙대로 그 관절에 붙는다.
//   관절은 사람 몸과 같은 `BodyJoints`(팔 자리만 다르다)라 클립이 그대로 돈다.
// - **선이 둘이다** — 사용자가 고른 것은 **붙어 있되 검은 선으로 갈린** 뼈이고(v5), 선은 머리처럼 굵게(C — 머리 6 · 몸통 4.5 ·
//   팔다리 3.5 · 손발 2.5cm). 제 선(`BoneBody` · `BoneLine` 1.5cm)이 맞닿은 뼈 사이의 이음매를 긋고, 폰 밖의 굵은 선은 부위마다
//   바깥 선 재질(`OutlineBone*` — 몸통 선처럼 스텐실로 제 몸을 피한다)이 긋는다. 제 선을 굵게 하면 뼈가 서로를 검게 덮고,
//   한 굵기의 6cm 바깥 선은 뼈(지름 6cm)보다 넓어 팔다리 사이 틈이 메워진다(B — 사용자가 C를 골랐다).
// - **방어구는 칠이 아니라 껍데기다**(`관절.Armor.칸` — `PawnApparel`이 찼을 때만 켠다). 껍데기가 덮은 자리만 가려진다 —
//   흉갑은 갈비통만 덮어 허리뼈 · 골반은 보인다(사용자: *"가슴까지만 가려지고 배는 안 가려져 그러면 뼈가 보여야하는 거 아니야?"*).
// - 뼈 색은 `BoneLook`이 머리와 같은 값으로 칠한다(`관절.Bone.*`).
public static partial class ChibiPawnCreator
{
    // 아래 값은 전부 **머리 높이(`Hh`)의 배수**이고 원점은 발밑 가운데다(+z가 앞).
    const float BoneArmX     = 0.43f;   // 팔 자리 — 사람 몸(≈0.30)보다 밖
    const float SkullOriginY = 2.12f;   // 해골 거리장 원점의 높이(사람 머리 자리보다 낮다 — 짧은 목)
    const float BoneLine     = 0.015f;  // 뼈 조각의 제 선(m)
    // 격자 한 칸(머리 높이 배수) — 뼈 반지름(0.04)에 네 칸 · 손가락(0.024)에 세 칸. 더 촘촘하면 해골 하나가 8만 정점을 넘는다.
    const float BoneStep = 0.014f, LimbStep = 0.011f, DigitStep = 0.0075f, ShellStep = 0.014f;
    // 뽑은 뒤 남기는 비율(`Decimate`) — 뼈 4.6만 → 9천대. 손발은 손가락 · 발가락이 가늘어 덜 줄인다.
    const float BoneKeep = 0.2f, DigitKeep = 0.25f;

    // 해골 몸 한 벌 — `DressSkinned`가 사람 몸 대신 받는다.
    sealed class BoneBody
    {
        public readonly List<Part> Parts = new();
        public readonly HashSet<string> Shells = new();   // 방어구 껍데기 — 게임 오브젝트를 꺼 둔다
        public float ArmX;                // 관절(m)
        public bool  HeadInPlace;         // 머리 부위가 이미 몸과 한 좌표에 있다(큰 짐승) — `DressSkinned`가 안 옮긴다
    }

    static readonly Dictionary<string, BoneBody> _boneBodies = new();

    static BoneBody BoneBodyFor(Shape s)
    {
        if (_boneBodies.TryGetValue(s.Suffix, out var bb)) return bb;
        var d = DimsOf(s);
        bb = new BoneBody { ArmX = BoneArmX * d.Hh };
        var j = new BoneJoints(d);
        float hh = d.Hh;
        Vector3 Place(Vector3 p) => p * hh;

        // 그늘은 몸 전체가 서로 가리는 것으로 잰다(갈비 사이 · 골반 속 · 손가락 사이). 손발이 빠지면 손가락 사이 · 손바닥 밑이
        // 그늘 없이 구워져 손발만 납작해 보인다.
        Func<Vector3, float> body = p =>
        {
            float a = Mathf.Min(Ribcage(p), Mathf.Min(Column(p), Collar(p)));
            a = Mathf.Min(a, Pelvis(p));
            foreach (int sd in Sides)
            {
                var m = Side(p, sd);
                a = Mathf.Min(a, Mathf.Min(Humerus(m, j), Forearm(m, j)));
                a = Mathf.Min(a, Mathf.Min(Femur(m, j), Tibia(m, j)));
                a = Mathf.Min(a, Mathf.Min(Hand(m, j), Foot(m, j)));
            }
            return a;
        };

        void Add(string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep, Func<Vector3, float> scene)
        {
            var mesh = SaveSkull($"Chibi_BoneBody_{s.Suffix}_{name.Replace('.', '_')}",
                                 SurfaceNets(f, b, step, scene, Place, 0.03f, 5.5f, keep));
            bb.Parts.Add(new Part(name, mesh, Vector3.zero, Vector3.one, mat, outerMat: line));
        }
        // 바깥 선 굵기는 그 조각이 얼마나 굵나로 — 몸통 · 팔다리 · 손발. 손발은 손가락 · 발가락이 가늘어 촘촘히 뽑고 덜 줄인다.
        void Bone(string name, string line, Func<Vector3, float> f, Bounds b, float step) => Add(name, "BoneBody", line, f, b, step, BoneKeep, body);
        void Digit(string name, Func<Vector3, float> f, Bounds b) => Add(name, "BoneBody", OutlineBoneDigit, f, b, DigitStep, DigitKeep, body);
        // 껍데기는 켜 둔 채로 굽고 **게임 오브젝트를 꺼 둔다** — `PawnApparel`이 찼을 때 켠다(렌더러를 끄고 켜면 시체를 파낼 때
        // 렌더러를 다 켜는 `Corpse.SetBuried`나 모델을 갈아 끼우며 다 끄는 `UnitBrain.ApplyLook`과 서로 덮어쓴다).
        void Shell(string name, string line, Func<Vector3, float> f, Bounds b)
        {
            Add(name, "BoneArmor", line, f, b, ShellStep, BoneKeep, f);
            bb.Shells.Add(name);
        }

        Bone("Spine.Bone.Ribs",   OutlineBoneTorso, Ribcage, new Bounds(new Vector3(0f, 1.32f, 0.02f), new Vector3(0.66f, 0.70f, 0.56f)), BoneStep);
        Bone("Spine.Bone.Column", OutlineBoneTorso, Column,  new Bounds(new Vector3(0f, 1.43f, -0.09f), new Vector3(0.20f, 1.06f, 0.24f)), BoneStep);
        Bone("Spine.Bone.Collar", OutlineBoneTorso, Collar,  new Bounds(new Vector3(0f, 1.53f, 0.07f), new Vector3(0.84f, 0.20f, 0.34f)), BoneStep);
        Bone("Hips.Bone.Pelvis",  OutlineBoneTorso, Pelvis,  new Bounds(new Vector3(0f, 0.84f, -0.01f), new Vector3(0.52f, 0.34f, 0.40f)), BoneStep);
        Shell("Spine.Armor.Chest", OutlineBoneTorso, Breastplate, new Bounds(new Vector3(0f, 1.32f, 0.02f), new Vector3(0.72f, 0.60f, 0.64f)));

        foreach (int sd in Sides)
        {
            string lr = sd < 0 ? "L" : "R";
            int k = sd;
            Func<Vector3, float> On(Func<Vector3, BoneJoints, float> f) => p => f(Side(p, k), j);
            Bounds Box(Vector3 a, Vector3 b, float pad) => new((a + b) * 0.5f, new Vector3(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y), Mathf.Abs(a.z - b.z)) + Vector3.one * pad * 2f);
            Vector3 X(float x, float y, float z) => new(x * k, y, z);

            Bone($"Arm{lr}.Bone.Upper",      OutlineBoneLimb, On(Humerus), Box(X(j.AX, j.ArmTop, 0f), X(j.AX, j.Elbow, 0f), 0.09f), LimbStep);
            Bone($"LowerArm{lr}.Bone.Fore",  OutlineBoneLimb, On(Forearm), Box(X(j.AX, j.Elbow, 0f), X(j.AX, j.Wrist, 0f), 0.08f), LimbStep);
            Digit($"Hand{lr}.Bone.Hand",                       On(Hand),    Box(X(j.AX - 0.10f, j.Wrist + 0.02f, -0.12f), X(j.AX + 0.05f, j.Wrist - 0.32f, 0.18f), 0.03f));
            Bone($"Leg{lr}.Bone.Thigh",      OutlineBoneLimb, On(Femur),   Box(X(j.LX, j.Hip, 0f), X(j.LX, j.Knee, 0f), 0.10f), LimbStep);
            Bone($"LowerLeg{lr}.Bone.Shin",  OutlineBoneLimb, On(Tibia),   Box(X(j.LX, j.Knee, 0f), X(j.LX, j.Ankle, 0f), 0.09f), LimbStep);
            Digit($"Foot{lr}.Bone.Foot",                       On(Foot),    Box(X(j.LX - 0.10f, 0f, -0.12f), X(j.LX + 0.10f, 0.16f, 0.34f), 0.03f));

            Shell($"Leg{lr}.Armor.Legs",      OutlineBoneLimb, On(Cuisse),   Box(X(j.LX, j.Hip, 0f), X(j.LX, j.Knee, 0f), 0.12f));
            Shell($"LowerLeg{lr}.Armor.Legs", OutlineBoneLimb, On(Greave),   Box(X(j.LX, j.Knee, 0f), X(j.LX, j.Ankle, 0f), 0.11f));
            Shell($"Hand{lr}.Armor.Hands",    OutlineBoneLimb, On(Gauntlet), Box(X(j.AX - 0.12f, j.Wrist + 0.12f, -0.16f), X(j.AX + 0.10f, j.Wrist - 0.36f, 0.22f), 0.03f));
            Shell($"Foot{lr}.Armor.Feet",     OutlineBoneLimb, On(Boot),     Box(X(j.LX - 0.12f, 0f, -0.14f), X(j.LX + 0.12f, 0.24f, 0.36f), 0.03f));
        }

        _boneBodies[s.Suffix] = bb;
        return bb;
    }

    static readonly int[] Sides = { -1, 1 };
    static Vector3 Side(Vector3 p, int side) => new(p.x * side, p.y, p.z);   // 오른쪽(+x)에 적은 팔다리를 그 쪽으로

    // 관절 높이(머리 높이 배수) — 사람 몸과 같은 `BodyDims`에서 온다. 팔 자리만 `BoneArmX`.
    readonly struct BoneJoints
    {
        public readonly float AX, ArmTop, Elbow, Wrist, LX, Hip, Knee, Ankle;
        public BoneJoints(BodyDims d)
        {
            float hh = d.Hh;
            AX = BoneArmX; ArmTop = d.ShoulderY / hh + 0.035f; Elbow = d.ElbowY / hh; Wrist = d.HandY / hh;
            LX = d.LegX / hh; Hip = d.HipY / hh; Knee = d.KneeY / hh; Ankle = d.FootY / hh;
        }
    }

    static float Seg(Vector3 p, Vector3 a, Vector3 b, float ra, float rb)
    {
        var pa = p - a; var ba = b - a;
        float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
        return (pa - ba * h).magnitude - Mathf.Lerp(ra, rb, h);
    }

    // 긴 뼈 — 가운데가 잘록한 대 + 양 끝의 둥근 머리.
    static float LongBone(Vector3 p, Vector3 a, Vector3 b, float r, float knob)
    {
        var mid = (a + b) * 0.5f;
        float d = Mathf.Min(Seg(p, a, mid, r, r * 0.85f), Seg(p, mid, b, r * 0.85f, r));
        d = SMin(d, Sph(p, a, knob), 0.03f);
        return SMin(d, Sph(p, b, knob), 0.03f);
    }

    // ── 몸통 ────────────────────────────────────────────────────────────────

    // 갈비통 — 달걀 껍데기를 앞으로 기운 띠로 잘라 갈비 넷을 남긴다 · 위아래는 트이고 앞 아래는 V로 열린다 · 복장뼈.
    static float Ribcage(Vector3 p)
    {
        var c = new Vector3(0f, 1.30f, 0.01f);
        float d = Mathf.Abs(Ell(p, c, new Vector3(0.27f, 0.28f, 0.22f))) - 0.035f;
        float yy = p.y + 0.16f * Mathf.Pow(Mathf.Clamp01((p.z + 0.05f) / 0.25f), 1.3f);   // 갈비는 앞으로 내려간다
        const float top = 1.53f, bot = 1.04f, ribs = 4f, gap = 0.5f;
        float pitch = (top - bot) / ribs, t = (yy - bot) / pitch;
        d = SMax(d, (Mathf.Abs(t - Mathf.Floor(t) - 0.5f) - gap * 0.5f) * pitch, 0.012f);   // 갈비 사이 틈
        d = Mathf.Max(d, bot - 0.02f - yy);
        d = SMax(d, p.y - (c.y + 0.20f), 0.03f);
        if (p.z > 0.05f && p.y < 1.27f) d = SMax(d, -(Mathf.Abs(p.x) - (1.27f - p.y) * 0.75f), 0.02f);   // 앞 아래 V
        return SMin(d, RBox(p, new Vector3(0f, 1.39f, 0.225f), new Vector3(0.045f, 0.13f, 0.03f), 0.028f), 0.02f);
    }

    // 척추 — 허리 마디 둘 · 목 마디 셋(목은 앞으로 기울어 두개골 밑에 닿는다). 갈비 속 등뼈는 갈비가 가린다.
    static readonly float[] Vertebrae = { 0.98f, 1.10f, 1.62f, 1.75f, 1.88f };
    static float Column(Vector3 p)
    {
        float d = 9f;
        foreach (float y in Vertebrae)
        {
            float z = -0.14f + 0.08f * Mathf.Clamp01((y - 1.55f) / 0.3f);
            d = Mathf.Min(d, RBox(p, new Vector3(0f, y, z), new Vector3(0.065f, 0.04f, 0.055f), 0.03f));
        }
        return d;
    }

    static float Collar(Vector3 p) => Seg(Mirror(p), new Vector3(0.04f, 1.55f, 0.16f), new Vector3(0.35f, 1.51f, -0.01f), 0.045f, 0.05f);

    // 골반 — 기운 날개 둘(납작한 껍데기) · 엉치뼈 · 두덩뼈 · 엉덩이 소켓.
    static float Pelvis(Vector3 p)
    {
        var m = Mirror(p);
        var q = m - new Vector3(0.13f, 0.86f, -0.01f);
        float ca = Mathf.Cos(0.5f), sa = Mathf.Sin(0.5f);
        q = new Vector3(q.x * ca + q.y * sa, -q.x * sa + q.y * ca, q.z);
        float wing = Mathf.Abs(Ell(q, Vector3.zero, new Vector3(0.105f, 0.08f, 0.10f))) - 0.022f;
        wing = SMax(wing, -(m.y - 0.80f), 0.02f);
        float sacrum = RBox(p, new Vector3(0f, 0.85f, -0.11f), new Vector3(0.05f, 0.06f, 0.03f), 0.022f);
        float pubis  = Capsule(m, new Vector3(0f, 0.76f, 0.09f), new Vector3(0.10f, 0.79f, 0.05f), 0.03f);
        float socket = Sph(m, new Vector3(0.12f, 0.80f, 0f), 0.05f);
        return SMin(SMin(wing, sacrum, 0.04f), SMin(pubis, socket, 0.04f), 0.04f);
    }

    // ── 팔다리(오른쪽에 적는다 — 왼쪽은 `Side`가 뒤집는다) ─────────────────────

    static float Humerus(Vector3 m, BoneJoints j) => LongBone(m, new Vector3(j.AX, j.ArmTop, 0f), new Vector3(j.AX, j.Elbow, 0f), 0.042f, 0.06f);
    static float Forearm(Vector3 m, BoneJoints j) => LongBone(m, new Vector3(j.AX, j.Elbow - 0.01f, 0f), new Vector3(j.AX, j.Wrist + 0.02f, 0f), 0.036f, 0.05f);
    static float Femur(Vector3 m, BoneJoints j)   => LongBone(m, new Vector3(j.LX, j.Hip, 0f), new Vector3(j.LX, j.Knee, 0f), 0.05f, 0.068f);
    static float Tibia(Vector3 m, BoneJoints j)   => LongBone(m, new Vector3(j.LX, j.Knee - 0.04f, 0f), new Vector3(j.LX, j.Ankle + 0.02f, 0f), 0.042f, 0.058f);

    // 손 — 손바닥 · 손가락 넷 · 엄지. v1 모양을 1.25배로 손목에 매단다(v1 좌표 그대로 적고 옮겨 잰다).
    static float Hand(Vector3 m, BoneJoints j)
    {
        const float S = 1.25f;
        var q = (m - new Vector3(j.AX, j.Wrist, 0f)) / S + new Vector3(0.31f, 0.97f, 0f);
        float d = RBox(q, new Vector3(0.31f, 0.905f, 0.01f), new Vector3(0.03f, 0.05f, 0.07f), 0.028f);
        foreach (var (z, ln) in new[] { (0.065f, 0.10f), (0.022f, 0.115f), (-0.022f, 0.11f), (-0.062f, 0.09f) })
        {
            var a = new Vector3(0.31f, 0.86f, z); var b = new Vector3(0.30f, 0.86f - ln, z + 0.01f);
            d = Mathf.Min(d, SMin(Capsule(q, a, (a + b) * 0.5f, 0.019f), Capsule(q, (a + b) * 0.5f, b, 0.017f), 0.01f));
        }
        d = Mathf.Min(d, Capsule(q, new Vector3(0.29f, 0.90f, 0.07f), new Vector3(0.26f, 0.82f, 0.10f), 0.02f));
        return d * S;
    }

    // 발 — 뒤꿈치 · 발등 · 발가락 넷. v1 모양을 1.2배로 땅 위에.
    static float Foot(Vector3 m, BoneJoints j)
    {
        const float S = 1.2f;
        var o = new Vector3(j.LX, 0f, 0f);
        var q = (m - o) / S + o;
        float d = SMin(RBox(q, new Vector3(j.LX, 0.055f, -0.02f), new Vector3(0.05f, 0.05f, 0.06f), 0.04f),
                       RBox(q, new Vector3(j.LX, 0.045f, 0.09f), new Vector3(0.065f, 0.035f, 0.07f), 0.03f), 0.04f);
        foreach (float x in new[] { -0.045f, -0.015f, 0.015f, 0.045f })
            d = Mathf.Min(d, Capsule(q, new Vector3(j.LX + x, 0.03f, 0.15f), new Vector3(j.LX + x * 1.15f, 0.022f, 0.24f), 0.018f));
        return d * S;
    }

    // ── 방어구 껍데기 — 찼을 때만 켜진다(`PawnApparel`) ───────────────────────────

    // 흉갑 — 갈비통보다 조금 큰 덩어리 · 목과 허리에서 판판하게 끊긴다. **갈비통만 덮는다** — 허리뼈와 골반은 보인다.
    // 속을 채운다 — 속이 빈 껍데기면 안팎 두 겹이 뽑혀 정점이 곱이 되는데, 어차피 갈비를 다 가린다.
    static float Breastplate(Vector3 p)
    {
        float d = Ell(p, new Vector3(0f, 1.30f, 0.02f), new Vector3(0.32f, 0.32f, 0.28f));
        d = SMax(d, p.y - 1.58f, 0.03f);
        return SMax(d, 1.06f - p.y, 0.02f);
    }

    static float Cuisse(Vector3 m, BoneJoints j) => Seg(m, new Vector3(j.LX, j.Hip - 0.02f, 0f), new Vector3(j.LX, j.Knee + 0.03f, 0f), 0.085f, 0.075f);
    static float Greave(Vector3 m, BoneJoints j) => Seg(m, new Vector3(j.LX, j.Knee - 0.03f, 0f), new Vector3(j.LX, j.Ankle + 0.06f, 0f), 0.075f, 0.068f);

    static float Gauntlet(Vector3 m, BoneJoints j)
    {
        float cuff = Seg(m, new Vector3(j.AX, j.Wrist + 0.07f, 0f), new Vector3(j.AX, j.Wrist - 0.02f, 0f), 0.06f, 0.065f);
        float mitt = RBox(m, new Vector3(j.AX - 0.02f, j.Wrist - 0.16f, 0.025f), new Vector3(0.085f, 0.17f, 0.145f), 0.05f);   // 손가락 끝(손목 아래 0.3)까지
        return SMin(cuff, mitt, 0.03f);
    }

    static float Boot(Vector3 m, BoneJoints j)
        => SMin(RBox(m, new Vector3(j.LX, 0.10f, -0.01f), new Vector3(0.085f, 0.10f, 0.09f), 0.06f),
                RBox(m, new Vector3(j.LX, 0.06f, 0.16f), new Vector3(0.09f, 0.06f, 0.16f), 0.05f), 0.05f);
}
