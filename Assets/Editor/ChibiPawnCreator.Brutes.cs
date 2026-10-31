using System;
using System.Collections.Generic;
using UnityEngine;

// **오우거 · 트롤 — 사람 몸을 키운 것이 아니라 따로 빚은 몸**(2026-10-28, 사용자: *"오거랑 트롤도 너무 그냥 일반 캐릭터가 크기만
// 커진 거 같아"* → *"굳이 원래 캐릭 몸으로 할 필요 없이 외형을 스켈레톤처럼 새로 만들어도 되는거야"*).
//
// 해골 몸(`ChibiPawnCreator.Bones`)과 같은 길이다 — 거리장으로 빚어 표면을 뽑고(`SurfaceNets`) **관절마다 굳은 조각**으로 붙인다.
// 관절은 사람과 같은 `BodyJoints`라 클립이 그대로 돈다. 다른 것은 셋:
//
//   - **살은 이어 보여야 한다** — 뼈는 조각마다 선으로 갈렸지만 살은 한 덩어리다. 그래서 제 선이 없는 살(`SkinOuter`)에 **바깥 선만**
//     긋는다(`OutlineOuter` — 스텐실로 제 몸을 피한다). 조각이 맞닿은 자리에 선이 안 선다.
//   - **머리도 몸과 한 거리장 좌표로 빚는다**(`BoneBody.HeadInPlace`) — 머리가 어깨에 파묻히고(오우거) 앞으로 숙는(트롤) 것이
//     생김새의 절반이라, 사람 머리 자리에서 옮기는 틀(해골의 `SkullOriginY`)을 안 쓴다. 얼굴 판 · 머리카락 · 투구도 없다.
//   - **비례는 체형이 든다**(`Shape.ArmK` · `LegK`) — 관절 높이가 거기서 나오고 빚는 모양은 그 관절에 맞춘다.
//
// 생김새의 뼈대(사용자가 보여 준 그림에서 가져온 것은 **종족의 전형**뿐이다 — 그림을 옮기지 않는다):
//   - **오우거** — 모양과 비례는 `ChibiPawnCreator.Ogre`(둘째 판 — 작은 머리 · 큰 몸). 살색은 개체마다(`UnitData.SkinTones`).
//   - **트롤** — 구부정한 등의 혹 · 땅에 닿을 듯 긴 팔과 굵은 아래팔 · 큰 주먹 · 짧은 다리와 넓적한 발 · 앞으로 숙인 작은 머리에
//     넓은 입과 뭉툭한 코 · 밝은 배 · 쇠 목줄과 늘어진 사슬 · 해진 허리 천. 살색은 푸른빛 · 회색 안에서.
//
// **수치를 고치면 화면이 바뀐다** — 아직 사용자가 그림으로 본 판이 없다(첫 판). 고치면 `Pawn/9`(또는 `Pawn/3`)를 다시 돌려 볼 것.
public static partial class ChibiPawnCreator
{
    // 격자 한 칸(머리 높이 배수)과 남기는 비율 — 해골보다 굵은 덩어리라 성기게 뽑아도 모양이 산다.
    const float BruteStep = 0.022f, BruteLimbStep = 0.018f, BruteFineStep = 0.010f;
    const float BruteKeep = 0.2f, BruteFineKeep = 0.3f;

    public const string BruteOgre = "Ogre", BruteTroll = "Troll";

    // 체형 — 몸통 옆선(`Half`)은 관절 자리(다리 간격)에만 쓰인다. 비례는 팔 · 다리 배수가 든다.
    static readonly Shape[] BruteShapes =
    {
        //                                                         밑     엉덩이  허리   가슴   어깨      깊이   손     팔     다리
        new Shape(BruteOgre,  "오우거", "Male", new[] { 0.36f, 0.44f, 0.48f, 0.46f, 0.40f }, 1.00f, 0.40f, 1.00f, 0.80f),    // 치비 비례(`ChibiPawnCreator.Ogre`)
        new Shape(BruteTroll, "트롤",   "Male", new[] { 0.30f, 0.34f, 0.38f, 0.44f, 0.46f }, 0.95f, 0.46f, 1.55f, 0.65f),
    };

    static Shape BruteShape(string kind) => Array.Find(BruteShapes, x => x.Suffix == kind);

    // 관절 높이(머리 높이 배수) — `BodyDims`에서. 팔 · 다리 자리는 몸이 넓어 따로 준다.
    readonly struct BruteJoints
    {
        public readonly float AX, ArmTop, Elbow, Wrist, LX, Hip, Knee, Ankle, Top;
        public BruteJoints(BodyDims d, float ax, float lx)
        {
            float hh = d.Hh;
            AX = ax; LX = lx;
            ArmTop = d.ShoulderY / hh + 0.035f; Elbow = d.ElbowY / hh; Wrist = d.HandY / hh;
            Hip = d.HipY / hh; Knee = d.KneeY / hh; Ankle = d.FootY / hh; Top = d.Top / hh;
        }
    }

    static readonly Dictionary<string, (BoneBody body, List<Part> head)> _brutes = new();

    // 큰 짐승 한 벌 — 몸(관절마다 조각)과 머리 부위. `Pawn/3`이 돌 때마다 다시 굽는다(`_brutes`는 그때 비운다).
    static (BoneBody body, List<Part> head) BruteFor(string kind)
    {
        if (_brutes.TryGetValue(kind, out var have)) return have;

        var s = BruteShape(kind);
        var d = DimsOf(s);
        bool ogre = kind == BruteOgre;
        var j = new BruteJoints(d, ogre ? OgreArmX : 0.60f, ogre ? OgreLegX : 0.22f);
        float hh = d.Hh;
        Vector3 Place(Vector3 p) => p * hh;

        var bb   = new BoneBody { ArmX = j.AX * hh, HeadInPlace = true };
        var head = new List<Part>();

        Func<Vector3, float> torso = ogre ? p => OgreTorso(p, j) : p => TrollTorso(p, j);
        Func<Vector3, float> skull = ogre ? p => OgreHead(p, j)  : p => TrollHead(p, j);
        Func<Vector3, float> scene = p =>
        {
            float a = Mathf.Min(torso(p), skull(p));
            foreach (int sd in Sides)
            {
                var m = Side(p, sd);
                a = Mathf.Min(a, Mathf.Min(BruteUpper(m, j, ogre), BruteFore(m, j, ogre)));
                a = Mathf.Min(a, Mathf.Min(BruteThigh(m, j, ogre), BruteShin(m, j, ogre)));
                a = Mathf.Min(a, Mathf.Min(BruteHand(m, j, ogre), BruteFoot(m, j, ogre)));
            }
            return a;
        };

        Part Piece(string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep)
        {
            var mesh = SaveSkull($"Chibi_{kind}_{name.Replace('.', '_')}", SurfaceNets(f, b, step, scene, Place, 0.04f, 4f, keep));
            return new Part(name, mesh, Vector3.zero, Vector3.one, mat, outerMat: line);
        }
        void Body(string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep = BruteKeep)
            => bb.Parts.Add(Piece(name, mat, line, f, b, step, keep));
        void Head(string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep = BruteKeep)
            => head.Add(Piece(name, mat, line, f, b, step, keep));

        const string Flesh = "SkinOuter", Line = "OutlineOuter", Fine = "OutlineOuterFine";
        float top = j.Top;

        // **목 관절 자리** — 안 보이는 `Head`의 밑이 목이다(`DressSkinned`). 머리가 어깨에 묻히므로 몸통 꼭대기보다 낮다.
        // 오우거는 큰 머리가 어깨에 바로 얹힌다(목이 없다 — 0.8 높이 · 몸통 꼭대기 위 0.04부터).
        float neck = ogre ? top + 0.04f : top - 0.16f;
        float headH = ogre ? 0.80f : 0.5f;
        var headC = new Vector3(0f, neck + headH * 0.5f, ogre ? 0.06f : 0.30f) * hh;
        head.Add(new Part("Head", null, headC, new Vector3(headH, headH, headH) * hh, "HeadSkin", on: false));

        if (ogre)
        {
            float y0 = j.Hip - 0.26f, y1 = top + 0.18f;
            Body("Spine.Brute.Torso",  Flesh, Line, torso, Box3(0f, (y0 + y1) * 0.5f, 0.03f, 1.20f, y1 - y0, 1.06f), BruteStep);
            Body("Spine.Brute.Sash",   "Pants", Line, p => OgreSash(p, j), Box3(0f, (y0 + y1) * 0.5f + 0.06f, 0.03f, 1.10f, y1 - y0, 1.10f), BruteFineStep * 1.3f);
            Body("Hips.Brute.Loin",    "Pants", Line, p => OgreLoin(p, j), Box3(0f, j.Hip - 0.03f, 0.04f, 1.10f, 0.52f, 1.00f), BruteFineStep * 1.3f);
            Body("Hips.Brute.Buckle",  "BoneArmor", Fine, p => OgreBuckle(p, j), Box3(0f, j.Hip + 0.07f, 0.42f, 0.28f, 0.28f, 0.22f), BruteFineStep * 0.8f, BruteFineKeep);

            Head("Head.Brute.Skull", Flesh, Line, skull, Box3(0f, top + 0.42f, 0.08f, 1.00f, 0.90f, 0.86f), BruteFineStep * 1.3f);
            Head("Head.Brute.Mouth", "NoseDark", null, p => OgreMouth(p, j), Box3(0f, top + 0.245f, 0.26f, 0.36f, 0.16f, 0.16f), BruteFineStep, BruteFineKeep);
            Head("Head.Brute.Teeth", "Bone", Fine, p => OgreTeeth(p, j), Box3(0f, top + 0.255f, 0.305f, 0.32f, 0.14f, 0.10f), BruteFineStep * 0.5f, BruteFineKeep);
            Head("Head.Brute.Tusks", "Bone", Fine, p => OgreTusks(p, j), Box3(0f, top + 0.295f, 0.333f, 0.44f, 0.24f, 0.14f), BruteFineStep * 0.7f, BruteFineKeep);
            Head("Head.Brute.Horn",  "Horn", Fine, p => OgreHorn(p, j),  Box3(0f, top + 0.86f, 0.01f, 0.26f, 0.40f, 0.26f), BruteFineStep * 0.8f, BruteFineKeep);
            // 눈 하나 · 둘 — 씨앗이 고른다(`PawnLook` — `Head.Pick.{축}.{벌}` · 넷째 마디 뒤는 조각 이름이라 안 읽힌다).
            var eye1 = OneEye + Vector3.up * top; var eye2 = TwoEye + Vector3.up * top;
            Head("Head.Pick.Eyes.One.White", "EyeWhite", Fine, p => OgreOneWhite(p, j), new Bounds(eye1, new Vector3(0.26f, 0.25f, 0.20f)), BruteFineStep * 0.6f, BruteFineKeep);
            Head("Head.Pick.Eyes.One.Iris",  "Iris", null, p => OgreOneIris(p, j),  new Bounds(eye1 + new Vector3(0f, 0f, 0.067f), new Vector3(0.16f, 0.16f, 0.06f)), BruteFineStep * 0.4f, BruteFineKeep);
            Head("Head.Pick.Eyes.One.Pupil", "NoseDark", null, p => OgreOnePupil(p, j), new Bounds(eye1 + new Vector3(0f, 0f, 0.081f), new Vector3(0.08f, 0.09f, 0.04f)), BruteFineStep * 0.3f, BruteFineKeep);
            Head("Head.Pick.Eyes.Two.White", "EyeWhite", Fine, p => OgreTwoWhite(p, j), new Bounds(new Vector3(0f, eye2.y, eye2.z), new Vector3(0.50f, 0.20f, 0.16f)), BruteFineStep * 0.5f, BruteFineKeep);
            Head("Head.Pick.Eyes.Two.Iris",  "Iris", null, p => OgreTwoIris(p, j),  new Bounds(new Vector3(0f, eye2.y, eye2.z + 0.062f), new Vector3(0.42f, 0.13f, 0.05f)), BruteFineStep * 0.3f, BruteFineKeep);
            Head("Head.Pick.Eyes.Two.Pupil", "NoseDark", null, p => OgreTwoPupil(p, j), new Bounds(new Vector3(0f, eye2.y, eye2.z + 0.075f), new Vector3(0.36f, 0.08f, 0.03f)), BruteFineStep * 0.25f, BruteFineKeep);
        }
        else
        {
            Body("Spine.Brute.Torso", Flesh, Line, torso, Box3(0f, (j.Hip + top) * 0.5f + 0.04f, 0.0f, 1.15f, top - j.Hip + 0.55f, 1.05f), BruteStep);
            Body("Spine.Brute.Belly", "Belly", null, p => TrollBelly(p, j), Box3(0f, j.Hip + 0.34f, 0.24f, 0.86f, 0.96f, 0.56f), BruteFineStep * 1.6f);
            Body("Spine.Brute.Collar", "BoneArmor", Fine, p => TrollCollar(p, j), Box3(0f, top - 0.30f, 0.16f, 1.05f, 0.70f, 1.05f), BruteFineStep, BruteFineKeep);
            Body("Hips.Brute.Loin",   "Pants", Line, p => TrollLoin(p, j), Box3(0f, j.Hip - 0.06f, 0f, 0.95f, 0.64f, 0.84f), BruteFineStep * 1.6f);

            Head("Head.Brute.Skull", Flesh, Line, skull, Box3(0f, top + 0.06f, 0.32f, 0.62f, 0.60f, 0.66f), BruteFineStep * 1.3f);
            Head("Head.Brute.Eyes",  "NoseDark", null, p => TrollEyes(p, j), Box3(0f, top + 0.13f, 0.53f, 0.26f, 0.10f, 0.10f), BruteFineStep * 0.6f, BruteFineKeep);
            Head("Head.Brute.Teeth", "Bone", Fine, p => TrollTeeth(p, j), Box3(0f, top - 0.04f, 0.56f, 0.36f, 0.20f, 0.14f), BruteFineStep * 0.7f, BruteFineKeep);
        }

        foreach (int sd in Sides)
        {
            string lr = sd < 0 ? "L" : "R";
            int k = sd;
            Func<Vector3, float> On(Func<Vector3, BruteJoints, bool, float> f) => p => f(Side(p, k), j, ogre);
            Bounds Limb(float x, float y0, float y1, float pad) => new(new Vector3(x * k, (y0 + y1) * 0.5f, 0.02f),
                                                                      new Vector3(pad * 2f, Mathf.Abs(y0 - y1) + pad * 2f, pad * 2f));

            Body($"Arm{lr}.Brute.Upper",     Flesh, Line, On(BruteUpper), Limb(j.AX, j.ArmTop, j.Elbow, 0.30f), BruteLimbStep);
            Body($"LowerArm{lr}.Brute.Fore", Flesh, Line, On(BruteFore),  Limb(j.AX, j.Elbow, j.Wrist, 0.28f), BruteLimbStep);
            Body($"Hand{lr}.Brute.Hand",     Flesh, Line, On(BruteHand),  Limb(j.AX, j.Wrist + 0.02f, j.Wrist - (ogre ? 0.40f : 0.40f), 0.24f), BruteFineStep * 1.4f);
            Body($"Leg{lr}.Brute.Thigh",     Flesh, Line, On(BruteThigh), Limb(j.LX, j.Hip, j.Knee, ogre ? 0.36f : 0.30f), BruteLimbStep);   // 오우거 앞 근육
            Body($"LowerLeg{lr}.Brute.Shin", Flesh, Line, On(BruteShin),  Limb(j.LX, j.Knee, j.Ankle, 0.26f), BruteLimbStep);
            Body($"Foot{lr}.Brute.Foot",     Flesh, Line, On(BruteFoot),
                 new Bounds(new Vector3(j.LX * k, 0.11f, 0.13f), new Vector3(0.54f, 0.32f, 0.82f)), BruteFineStep * 1.4f);
            if (ogre)
            {
                Body($"LowerArm{lr}.Brute.Band",  "Pants", Line, p => OgreBand(Side(p, k), j), Limb(j.AX, j.Wrist + 0.26f, j.Wrist + 0.04f, 0.22f), BruteFineStep * 1.2f);
                Body($"LowerArm{lr}.Brute.Spike", "BoneArmor", Fine, p => OgreBandSpike(Side(p, k), j),
                     new Bounds(new Vector3((j.AX + 0.21f) * k, j.Wrist + 0.155f, 0.03f), new Vector3(0.30f, 0.16f, 0.16f)), BruteFineStep * 0.6f, BruteFineKeep);
            }
        }

        var made = (bb, head);
        _brutes[kind] = made;
        return made;
    }

    static Bounds Box3(float x, float y, float z, float sx, float sy, float sz) => new(new Vector3(x, y, z), new Vector3(sx, sy, sz));

    static float Torus(Vector3 p, Vector3 c, float big, float small)
    {
        var q = p - c;
        return new Vector2(new Vector2(q.x, q.z).magnitude - big, q.y).magnitude - small;
    }

    // ── 몸통 ────────────────────────────────────────────────────────────────

    // 트롤 — 등의 혹(어깨 뒤로 솟는다) · 좁아지는 허리 · 처진 배 · 앞으로 기운 가슴.
    static float TrollTorso(Vector3 p, BruteJoints j)
    {
        float t = j.Top;
        float chest = Ell(p, new Vector3(0f, t - 0.24f, 0.04f),    new Vector3(0.46f, 0.30f, 0.34f));
        float hump  = Ell(p, new Vector3(0f, t - 0.04f, -0.16f),   new Vector3(0.44f, 0.26f, 0.30f));
        float belly = Ell(p, new Vector3(0f, j.Hip + 0.32f, 0.06f), new Vector3(0.38f, 0.32f, 0.33f));
        float hips  = Ell(p, new Vector3(0f, j.Hip + 0.02f, -0.02f), new Vector3(0.34f, 0.20f, 0.28f));
        float traps = Capsule(Mirror(p), new Vector3(0.02f, t - 0.10f, -0.06f), new Vector3(0.48f, t - 0.14f, -0.04f), 0.15f);
        // 등줄기 혹 — 등뼈를 따라 돋은 마디 넷(사람 몸에 없는 것 하나가 실루엣을 바꾼다).
        float knobs = 9f;
        for (int i = 0; i < 4; i++)
            knobs = Mathf.Min(knobs, Sph(p, new Vector3(0f, t - 0.02f - i * 0.13f, -0.42f + i * 0.04f), 0.07f - i * 0.008f));
        float d = SMin(SMin(chest, hump, 0.16f), SMin(belly, hips, 0.12f), 0.12f);
        return SMin(SMin(d, traps, 0.10f), knobs, 0.05f);
    }

    // 트롤의 밝은 배 — 배 앞에 얹힌 판(몸통 배보다 조금 앞으로 나와 그 자리만 밝게 보인다). 살색을 안 타는 재질이다(`Belly`).
    static float TrollBelly(Vector3 p, BruteJoints j)
    {
        float d = Ell(p, new Vector3(0f, j.Hip + 0.34f, 0.11f), new Vector3(0.35f, 0.42f, 0.31f));
        return SMax(d, 0.14f - p.z, 0.04f);   // 앞쪽만
    }

    // ── 머리 ────────────────────────────────────────────────────────────────

    // 트롤 — 작고 앞으로 나온 머리 · 툭 튀어나온 눈두덩 · 넓은 입 · 돼지처럼 들린 코(콧구멍 둘) · 작게 붙은 귀.
    static float TrollHead(Vector3 p, BruteJoints j)
    {
        float t = j.Top;
        var m = Mirror(p);
        float cranium = Ell(p, new Vector3(0f, t + 0.12f, 0.30f), new Vector3(0.21f, 0.20f, 0.23f));
        float muzzle  = RBox(p, new Vector3(0f, t - 0.02f, 0.44f), new Vector3(0.20f, 0.08f, 0.12f), 0.07f);
        float brow    = Capsule(m, new Vector3(0.02f, t + 0.19f, 0.50f), new Vector3(0.16f, t + 0.17f, 0.45f), 0.05f);
        float nose    = Ell(p, new Vector3(0f, t + 0.10f, 0.56f), new Vector3(0.07f, 0.05f, 0.05f));
        float ear     = Ell(m, new Vector3(0.21f, t + 0.12f, 0.24f), new Vector3(0.035f, 0.06f, 0.05f));
        float d = SMin(SMin(cranium, muzzle, 0.09f), SMin(brow, nose, 0.04f), 0.05f);
        d = SMin(d, ear, 0.03f);
        d = SMax(d, -Sph(m, new Vector3(0.025f, t + 0.10f, 0.61f), 0.018f), 0.008f);                                  // 콧구멍
        d = SMax(d, -Sph(m, new Vector3(0.09f, t + 0.13f, 0.52f), 0.045f), 0.012f);                                   // 눈구멍
        return SMax(d, -RBox(p, new Vector3(0f, t - 0.035f, 0.55f), new Vector3(0.15f, 0.022f, 0.05f), 0.015f), 0.01f);   // 벌린 입
    }

    static float TrollEyes(Vector3 p, BruteJoints j) => Sph(Mirror(p), new Vector3(0.09f, j.Top + 0.13f, 0.51f), 0.028f);

    // 트롤 이 — 들쭉날쭉한 아랫니 넷(벌린 입 속에서 위로).
    static float TrollTeeth(Vector3 p, BruteJoints j)
    {
        float t = j.Top, d = 9f;
        float[] xs = { -0.11f, -0.04f, 0.035f, 0.10f };
        float[] hs = { 0.050f, 0.030f, 0.040f, 0.055f };
        for (int i = 0; i < xs.Length; i++)
            d = Mathf.Min(d, Seg(p, new Vector3(xs[i], t - 0.06f, 0.57f), new Vector3(xs[i] * 1.05f, t - 0.06f + hs[i], 0.575f), 0.018f, 0.006f));
        return d;
    }

    // ── 팔다리(오른쪽에 적는다 — `Side`가 뒤집는다) ─────────────────────────────

    // 위팔 — 어깨 공이 크다. 트롤은 가늘고 길다.
    static float BruteUpper(Vector3 m, BruteJoints j, bool ogre)
    {
        if (ogre) return OgreUpper(m, j);
        float r0 = ogre ? 0.17f : 0.13f, r1 = ogre ? 0.14f : 0.11f;
        float arm = Seg(m, new Vector3(j.AX, j.ArmTop, 0f), new Vector3(j.AX, j.Elbow, 0.02f), r0, r1);
        return SMin(arm, Sph(m, new Vector3(j.AX - 0.02f, j.ArmTop + 0.02f, -0.01f), ogre ? 0.20f : 0.17f), 0.08f);
    }

    // 아래팔 — 트롤은 손목 쪽이 더 굵다(유인원의 아래팔).
    static float BruteFore(Vector3 m, BruteJoints j, bool ogre)
    {
        if (ogre) return OgreFore(m, j);
        float r0 = ogre ? 0.14f : 0.12f, r1 = ogre ? 0.13f : 0.17f;
        return Seg(m, new Vector3(j.AX, j.Elbow, 0.02f), new Vector3(j.AX, j.Wrist + 0.03f, 0.04f), r0, r1);
    }

    // 손 — 두툼한 손바닥 · 굵은 손가락 셋 · 엄지 · 손톱 끝. 트롤 주먹이 더 크다.
    static float BruteHand(Vector3 m, BruteJoints j, bool ogre)
    {
        if (ogre) return OgreHand(m, j);
        float s = ogre ? 1.2f : 1.3f;
        var w = new Vector3(j.AX, j.Wrist, 0.04f);
        var q = (m - w) / s;
        float d = RBox(q, new Vector3(0f, -0.10f, 0.02f), new Vector3(0.07f, 0.09f, 0.10f), 0.06f);
        foreach (var z in new[] { 0.07f, 0.0f, -0.07f })
            d = SMin(d, Capsule(q, new Vector3(0.01f, -0.17f, z + 0.02f), new Vector3(0.03f, -0.27f, z + 0.04f), 0.042f), 0.03f);
        d = SMin(d, Capsule(q, new Vector3(-0.04f, -0.08f, 0.11f), new Vector3(-0.03f, -0.17f, 0.15f), 0.04f), 0.03f);   // 엄지
        return d * s;
    }

    static float BruteThigh(Vector3 m, BruteJoints j, bool ogre)
        => ogre ? OgreThigh(m, j) : Seg(m, new Vector3(j.LX, j.Hip, 0f), new Vector3(j.LX, j.Knee, 0.02f), ogre ? 0.20f : 0.17f, ogre ? 0.16f : 0.15f);

    static float BruteShin(Vector3 m, BruteJoints j, bool ogre)
        => ogre ? OgreShin(m, j) : Seg(m, new Vector3(j.LX, j.Knee, 0.02f), new Vector3(j.LX, j.Ankle + 0.05f, 0.02f), ogre ? 0.15f : 0.15f, ogre ? 0.13f : 0.14f);

    // 발 — 넓적한 발등과 발가락 셋(트롤은 넷 · 더 넓다).
    static float BruteFoot(Vector3 m, BruteJoints j, bool ogre)
    {
        if (ogre) return OgreFoot(m, j);
        float wide = ogre ? 1f : 1.2f;
        float d = RBox(m, new Vector3(j.LX, 0.07f, 0.06f), new Vector3(0.12f * wide, 0.065f, 0.16f), 0.06f);
        float[] xs = ogre ? new[] { -0.07f, 0f, 0.07f } : new[] { -0.10f, -0.035f, 0.035f, 0.10f };
        foreach (float x in xs)
            d = SMin(d, Capsule(m, new Vector3(j.LX + x, 0.04f, 0.20f), new Vector3(j.LX + x * 1.1f, 0.035f, 0.28f), 0.042f), 0.03f);
        return d;
    }

    // ── 걸친 것 ─────────────────────────────────────────────────────────────

    // 트롤 허리 천 — 띠 · 아래 끝이 들쭉날쭉한 앞 천.
    static float TrollLoin(Vector3 p, BruteJoints j)
    {
        float belt  = SMax(Mathf.Abs(Ell(p, new Vector3(0f, j.Hip + 0.06f, -0.01f), new Vector3(0.37f, 0.28f, 0.31f))) - 0.022f,
                           Mathf.Abs(p.y - (j.Hip + 0.06f)) - 0.05f, 0.01f);
        float hem   = j.Hip - 0.30f + 0.04f * Mathf.Sin(p.x * 40f);   // 해진 밑단
        float front = SMax(RBox(p, new Vector3(0f, j.Hip - 0.10f, 0.28f), new Vector3(0.20f, 0.22f, 0.018f), 0.015f), hem - p.y, 0.01f);
        return Mathf.Min(belt, front);
    }

    // 트롤 쇠 목줄 — 목을 감은 고리 · 고리에 박힌 가시 · 가슴께로 늘어진 사슬 고리 넷.
    static float TrollCollar(Vector3 p, BruteJoints j)
    {
        float t = j.Top;
        var c = new Vector3(0f, t - 0.13f, 0.14f);
        float ring = Torus(p, c, 0.29f, 0.05f);
        float spikes = 9f;
        for (int i = 0; i < 7; i++)
        {
            float a = (i / 7f) * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            spikes = Mathf.Min(spikes, Seg(p, c + dir * 0.32f, c + dir * 0.44f + Vector3.up * 0.03f, 0.028f, 0.005f));
        }
        float chain = 9f;
        for (int i = 0; i < 4; i++)
        {
            var at = new Vector3(0f, t - 0.22f - i * 0.085f, 0.45f - i * 0.015f);
            var q = p - at;
            // 고리를 번갈아 세운다 — 하나는 앞을 보고 다음은 옆을 본다.
            q = i % 2 == 0 ? new Vector3(q.x, q.z, q.y) : new Vector3(q.z, q.x, q.y);
            chain = Mathf.Min(chain, new Vector2(new Vector2(q.x / 0.65f, q.y).magnitude - 0.04f, q.z).magnitude - 0.012f);
        }
        return Mathf.Min(Mathf.Min(ring, spikes), chain);
    }
}
