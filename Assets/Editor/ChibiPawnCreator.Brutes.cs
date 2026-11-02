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
//   - **트롤** — `ChibiPawnCreator.Troll`(사용자가 미리보기로 판마다 고른 모양 — 굽은 등 · 굵은 승모근 목 · 짧은 하반신 · 고르는
//     장신구 셋). 살색은 푸른빛 · 회색 안에서.
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
        // 여성 오우거 — 남성과 같은 수치(관절이 같아야 클립 · 뼈가 같다 · 머리 높이도 남성 기준 — `DimsOf`). 몸 모양은 `ChibiPawnCreator.OgreFemale`.
        new Shape(BruteOgreFemale, "여성 오우거", "Female", new[] { 0.36f, 0.44f, 0.48f, 0.46f, 0.40f }, 1.00f, 0.40f, 1.00f, 0.80f),
        // 트롤 — 하반신이 짧고 상체가 길다(2026-11-02, 사용자) — 다리를 0.10 줄인 만큼 몸통을 늘린다(머리 꼭대기 · 팔은 그대로).
        new Shape(BruteTroll, "트롤",   "Male", new[] { 0.30f, 0.34f, 0.38f, 0.44f, 0.46f }, 0.95f, 0.46f, 1.55f, 0.40f, (0.96f + 0.10f) / 0.96f),
    };

    static Shape BruteShape(string kind) => Array.Find(BruteShapes, x => x.Suffix == kind);
    static bool IsBruteShape(Shape s) => Array.Exists(BruteShapes, x => x.Suffix == s.Suffix);

    // 지금 빚는 몸이 여성 오우거인가 — 팔다리 모양 함수(`BruteUpper` …)가 읽는다(`BruteFor`가 정한다 · 트롤의 `_tf`와 같은 길).
    static bool _ogreF;

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
        bool ogre = IsOgreKind(kind), fem = kind == BruteOgreFemale;
        _ogreF = fem;
        var j = new BruteJoints(d, ogre ? OgreArmX : 0.60f, ogre ? OgreLegX : 0.205f);   // 트롤 다리는 붙는다(허벅지가 거의 맞닿는다)
        float hh = d.Hh;
        Vector3 Place(Vector3 p) => p * hh;
        if (!ogre) _tf = new TrollFrame(j);   // 굽은 등 · 머리 좌표 — 트롤 모양 함수가 읽는다(`ChibiPawnCreator.Troll`)

        // 트롤의 팔은 굽은 등만큼 앞에 선다 — 뼈도 거기 세워야 휘두를 때 축이 안 어긋난다.
        var bb   = new BoneBody { ArmX = j.AX * hh, ArmZ = ogre ? 0f : _tf.ArmShift * hh, HeadInPlace = true };
        var head = new List<Part>();

        Func<Vector3, float> torso = ogre ? (fem ? p => OgreFTorso(p, j) : p => OgreTorso(p, j)) : p => TrollTorso(p, j);
        // 오우거의 그늘 장면은 귀 · 코 없는 맨 머리다 — 귀 · 코는 조합마다 다른 머리 벌이라 하나를 장면에 넣으면 다른 벌의 살이 그 그늘을 탄다.
        Func<Vector3, float> skull = ogre ? (fem ? p => OgreFHead(p, j, null, null) : p => OgreHead(p, j, null, null)) : p => TrollHead(p, j);
        float Limbs(Vector3 p)
        {
            float a = 9f;
            foreach (int sd in Sides)
            {
                var m = Side(p, sd);
                a = Mathf.Min(a, Mathf.Min(BruteUpper(m, j, ogre), BruteFore(m, j, ogre)));
                a = Mathf.Min(a, Mathf.Min(BruteThigh(m, j, ogre), BruteShin(m, j, ogre)));
                a = Mathf.Min(a, Mathf.Min(BruteHand(m, j, ogre), BruteFoot(m, j, ogre)));
            }
            return a;
        }
        // 오우거 머리 하나도 머리 둘과 같은 크기다(2026-11-03, 사용자: *"머리 한 개짜리도 머리를 두개짜리 크기로 해서 통일"*) — 가운데에 줄여 세운다.
        Func<Vector3, float> skull1 = ogre ? OnTwin(skull, 0, j.Top) : skull;
        Func<Vector3, float> scene = p => Mathf.Min(Mathf.Min(torso(p), skull1(p)), Limbs(p));
        // 머리 둘(오우거)의 그늘 장면 — 깎은 몸통 + 양 어깨의 머리 둘. 머리 둘 벌의 조각만 이것으로 굽는다.
        Func<Vector3, float> torsoTwin = p => OgreSaddle(torso(p), p, j);
        Func<Vector3, float> skullL = OnTwin(skull, -1, j.Top), skullR = OnTwin(skull, 1, j.Top);
        Func<Vector3, float> sceneTwin = p => Mathf.Min(Mathf.Min(torsoTwin(p), Mathf.Min(skullL(p), skullR(p))), Limbs(p));

        Part Piece(string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep, Func<Vector3, float> sc = null)
        {
            // 조건(`@축.!벌`)도 이름에 남긴다 — 귀 × 코 머리 벌은 조건만 다르다.
            var surf = SurfaceNets(f, b, step, sc ?? scene, Place, 0.04f, 4f, keep);
            // **살은 편다**(2026-11-01, 사용자: *"검은 선이 자글자글"*) — 줄인 표면은 잘게 울퉁불퉁하고(칠은 거리장 법선이 가려 매끈해 보인다)
            // 바깥 선은 그 정점을 법선으로 밀어 그리므로 털처럼 됐다. 얇은 천 · 뿔 · 이는 펴면 무너지니 살만.
            if (mat == "SkinOuter" && !name.Contains(".Lids.")) Relax(surf.Verts, surf.Tris, 6);
            var mesh = SaveSkull($"Chibi_{kind}_{name.Replace('.', '_').Replace('@', '_').Replace("!", "")}", surf);
            return new Part(name, mesh, Vector3.zero, Vector3.one, mat, outerMat: line);
        }
        void Body(string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep = BruteKeep)
            => bb.Parts.Add(Piece(name, mat, line, f, b, step, keep));
        void Head(string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep = BruteKeep)
            => head.Add(Piece(name, mat, line, f, b, step, keep));
        // 고르는 축의 벌 — 첫 벌(통과한 모습)만 켜 두고 구운 뒤에는 `PawnLook`이 씨앗으로 고른다.
        void Opt(List<Part> to, string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep, bool on,
                 Func<Vector3, float> sc = null)
        {
            var pc = Piece(name, mat, line, f, b, step, keep, sc);
            to.Add(new Part(pc.Name, pc.Mesh, pc.Center, pc.Size, pc.Mat, on, outerMat: pc.OuterMat));
        }

        // 큰 짐승은 얼굴 선(`OutlineOuterFine` — *얼굴 표시* 자리만 비킨다)을 안 쓴다 — 머리 살이 몸 번호만 적어 그 선이 살 위에 검은 띠로 섰다
        // (2026-11-01 — 눈 · 쇠 장식 · 송곳니 · 뿔 밑동). 가는 조각도 몸 바깥 선을 받는다.
        const string Flesh = "SkinOuter", Line = "OutlineOuter", Fine = Line;
        float top = j.Top;

        // 오우거 얼굴 한 벌 — `sgn` 0이면 가운데 머리 하나 · -1 / +1이면 머리 둘의 왼 / 오른 머리(줄여 옮긴 좌표 · 모두 꺼 두고 굽는다).
        // 이름 규칙: 머리 하나는 `@Heads.One` · 둘은 `@Heads.Two`를 꼬리에 붙이고, 눈 · 뿔은 머리마다 축이 따로다(`EyesL` · `HornR` …).
        // 귀 · 코 · 이 · 송곳니 · 눈꺼풀은 두 머리가 같은 축을 읽는다(같은 얼굴). 넷째 마디 뒤(`.L`)는 이름을 가르는 꼬리일 뿐이다.
        void OgreFace(int sgn)
        {
            bool twin = sgn != 0;
            string tag = sgn < 0 ? "L" : sgn > 0 ? "R" : "", cond = twin ? "@Heads.Two" : "@Heads.One";
            string eyes = "Eyes" + tag, horn = "Horn" + tag, tail = twin ? "." + tag : "";
            Func<Vector3, float> sc = twin ? sceneTwin : null;
            // 머리 하나(`sgn` 0)도 같은 크기로 줄여 가운데에 세운다 — 옆으로만 안 옮긴다.
            Func<Vector3, float> W(Func<Vector3, float> f) => OnTwin(f, sgn, j.Top);
            Bounds B(Bounds b) => TwinBounds(b, sgn, j.Top);
            float S(float step) => step * TwinS;   // 줄인 머리는 격자도 줄인다(같은 촘촘함)
            void H(string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep, bool on)
                => Opt(head, name, mat, line, W(f), B(b), S(step), keep, on && !twin, sc);
            float t = j.Top;

            for (int ei = 0; ei < OgreEars.Length; ei++)
            for (int ni = 0; ni < OgreNoses.Length; ni++)
            {
                string ear = OgreEars[ei], nose = OgreNoses[ni];
                H($"Head.Pick.Ears.{ear}.Skull{tag}@Nose.{nose}{cond}", Flesh, Line,
                  fem ? p => OgreFHead(p, j, ear, nose) : p => OgreHead(p, j, ear, nose),
                  Box3(0f, t + 0.42f, 0.08f, 1.00f, 0.90f, 0.86f), BruteFineStep * 1.3f, BruteKeep, ei == 0 && ni == 0);
            }
            H(twin ? $"Head.Pick.Heads.Two.Mouth{tag}" : "Head.Pick.Heads.One.Mouth", "NoseDark", null, p => OgreMouth(p, j),
              Box3(0f, t + 0.245f, 0.26f, 0.36f, 0.16f, 0.16f), BruteFineStep, BruteFineKeep, true);
            var teethBox = Box3(0f, t + 0.255f, 0.305f, 0.32f, 0.14f, 0.10f);
            H($"Head.Pick.Teeth.Full{tail}{cond}", "Bone", Fine, p => OgreTeeth(p, j, false), teethBox, BruteFineStep * 0.5f, BruteFineKeep, true);
            H($"Head.Pick.Teeth.Gap{tail}{cond}",  "Bone", Fine, p => OgreTeeth(p, j, true),  teethBox, BruteFineStep * 0.5f, BruteFineKeep, false);
            var tuskBox = Box3(0f, t + 0.295f, 0.333f, 0.44f, 0.24f, 0.14f);
            // 송곳니 — 없는 벌은 없다 · 선은 몸 바깥 선(`Line` — 얼굴 선은 살 위에 검은 띠를 그었다, 눈 · 쇠 장식과 같은 까닭).
            for (int ti = 0; ti < OgreTuskSets.Length; ti++)
            {
                var (tn, tl, tr) = OgreTuskSets[ti];
                H($"Head.Pick.Tusks.{tn}{tail}{cond}", "Bone", Line, fem ? p => OgreFTusks(p, j, tl, tr) : p => OgreTusks(p, j, tl, tr),
                  tuskBox, BruteFineStep * 0.7f, BruteFineKeep, ti == 0);
            }
            // 뿔 — 없음 · 하나 · 부러진(`@Horn.!None`은 `None` 벌을 축에 올리는 조건이다 — 없음은 렌더러가 없다).
            var hornBox = Box3(0f, t + 0.85f, 0.02f, 0.32f, 0.62f, 0.40f);
            H($"Head.Pick.{horn}.One@{horn}.!None{cond}", "Horn", Fine, p => OgreHorn(p, j),       hornBox, BruteFineStep * 0.8f, BruteFineKeep, true);
            H($"Head.Pick.{horn}.Broken{cond}",           "Horn", Fine, p => OgreHornBroken(p, j), hornBox, BruteFineStep * 0.8f, BruteFineKeep, false);
            // 눈 하나 · 둘 — 씨앗이 고른다(`PawnLook` — `Head.Pick.{축}.{벌}` · 넷째 마디 뒤는 조각 이름이라 안 읽힌다).
            var eye1 = OneEye + Vector3.up * t; var eye2 = TwoEye + Vector3.up * t;
            var two = new Vector3(0f, eye2.y, eye2.z);
            H($"Head.Pick.{eyes}.One.White{cond}", "EyeWhite", Line, p => OgreOneWhite(p, j), new Bounds(eye1, new Vector3(0.26f, 0.25f, 0.20f)), BruteFineStep * 0.6f, BruteFineKeep, true);
            H($"Head.Pick.{eyes}.One.Iris{cond}",  "Iris", null, p => OgreOneIris(p, j),  new Bounds(eye1 + new Vector3(0f, 0f, 0.067f), new Vector3(0.16f, 0.16f, 0.06f)), BruteFineStep * 0.4f, BruteFineKeep, true);
            H($"Head.Pick.{eyes}.One.Pupil{cond}", "NoseDark", null, p => OgreOnePupil(p, j), new Bounds(eye1 + new Vector3(0f, 0f, 0.081f), new Vector3(0.08f, 0.09f, 0.04f)), BruteFineStep * 0.3f, BruteFineKeep, true);
            // 눈꺼풀 — **축이다**(`Lids` 있음 · 없음 — 2026-11-01, 사용자: *"눈꺼풀은 축으로"*). 조각은 눈 벌마다 하나이고 그 눈일 때만 켜진다
            // (`@Eyes.One`). `@Lids.!Off`는 `Off` 벌을 축에 올리는 조건이다(없음은 렌더러가 없다). 얇은 껍데기라 펴지 않는다(`Relax`가 거른다).
            H($"Head.Pick.Lids.On.One{tag}@{eyes}.One@Lids.!Off{cond}", Flesh, Line, p => OgreOneLid(p, j), new Bounds(eye1, new Vector3(0.28f, 0.27f, 0.22f)), BruteFineStep * 0.4f, BruteFineKeep, true);
            H($"Head.Pick.Lids.On.Two{tag}@{eyes}.Two{cond}",           Flesh, Line, p => OgreTwoLid(p, j), new Bounds(two, new Vector3(0.52f, 0.24f, 0.20f)), BruteFineStep * 0.4f, BruteFineKeep, true);
            H($"Head.Pick.{eyes}.Two.White{cond}", "EyeWhite", Line, p => OgreTwoWhite(p, j), new Bounds(two, new Vector3(0.50f, 0.20f, 0.16f)), BruteFineStep * 0.5f, BruteFineKeep, true);
            H($"Head.Pick.{eyes}.Two.Iris{cond}",  "Iris", null, p => OgreTwoIris(p, j),  new Bounds(two + new Vector3(0f, 0f, 0.062f), new Vector3(0.42f, 0.13f, 0.05f)), BruteFineStep * 0.3f, BruteFineKeep, true);
            H($"Head.Pick.{eyes}.Two.Pupil{cond}", "NoseDark", null, p => OgreTwoPupil(p, j), new Bounds(two + new Vector3(0f, 0f, 0.075f), new Vector3(0.36f, 0.08f, 0.03f)), BruteFineStep * 0.25f, BruteFineKeep, true);
            if (!fem) return;

            // 여성 — 속눈썹(눈 벌마다) · 땋은 머리 · 끝 고리 · 귓불 귀걸이(귀 벌마다 — 작은 귀는 귓불이 올라간다).
            H($"Head.Pick.{eyes}.One.Lash{cond}", "NoseDark", null, p => OgreOneLashes(p, j), new Bounds(eye1 + new Vector3(0f, 0.07f, 0.03f), new Vector3(0.38f, 0.26f, 0.16f)), BruteFineStep * 0.35f, BruteFineKeep, true);
            H($"Head.Pick.{eyes}.Two.Lash{cond}", "NoseDark", null, p => OgreTwoLashes(p, j), new Bounds(two + new Vector3(0f, 0.06f, 0.03f), new Vector3(0.66f, 0.24f, 0.16f)), BruteFineStep * 0.35f, BruteFineKeep, true);
            H(twin ? $"Head.Pick.Heads.Two.Hair{tag}" : "Head.Pick.Heads.One.Hair", "OgreHair", Line, p => OgreFHair(p, j),
              Box3(0f, t + 0.09f, -0.29f, 0.36f, 1.46f, 0.60f), BruteFineStep * 1.1f, BruteKeep, true);
            H(twin ? $"Head.Pick.Heads.Two.HairRing{tag}" : "Head.Pick.Heads.One.HairRing", "BoneArmor", Line, p => OgreFHairRing(p, j),
              Box3(0f, t - 0.44f, -0.455f, 0.22f, 0.12f, 0.22f), BruteFineStep * 0.5f, BruteFineKeep, true);
            foreach (var ear in OgreEars)
                H($"Head.Pick.Ears.{ear}.Ring{tag}{cond}", "BoneArmor", Line, p => OgreEarring(p, j, ear),
                  Box3(0f, t + 0.30f, -0.03f, 0.86f, 0.24f, 0.18f), BruteFineStep * 0.4f, BruteFineKeep, ear == OgreEars[0]);
        }

        // **목 관절 자리** — 안 보이는 `Head`의 밑이 목이다(`DressSkinned`). 머리가 어깨에 묻히므로 몸통 꼭대기보다 낮다.
        // 오우거는 큰 머리가 어깨에 바로 얹힌다(목이 없다 — 0.8 높이 · 몸통 꼭대기 위 0.04부터).
        // 머리는 줄여 세웠으니(`TwinS` · 0.08 내림) 목 자리 · 머리 높이도 같이 줄인다.
        float neck = ogre ? top + 0.04f * TwinS + TwinDrop : top - 0.16f;
        float headH = ogre ? 0.80f * TwinS : 0.5f;
        var headC = new Vector3(0f, neck + headH * 0.5f, ogre ? 0.06f : 0.30f) * hh;
        head.Add(new Part("Head", null, headC, new Vector3(headH, headH, headH) * hh, "HeadSkin", on: false));

        if (ogre)
        {
            float y0 = j.Hip - 0.26f, y1 = top + 0.18f;
            var torsoBox = Box3(0f, (y0 + y1) * 0.5f, 0.03f, 1.20f, y1 - y0, 1.06f);
            // 몸통 — 머리 하나 · 둘(`Heads` 축 — 둘이면 두 얼굴 사이를 얕게 낮춘 몸통 · `OgreSaddle`).
            Opt(bb.Parts, "Spine.Pick.Heads.One.Torso", Flesh, Line, torso,     torsoBox, BruteStep, BruteKeep, true);
            Opt(bb.Parts, "Spine.Pick.Heads.Two.Torso", Flesh, Line, torsoTwin, torsoBox, BruteStep, BruteKeep, false, sceneTwin);
            if (!fem)
            {
                // 어깨 천 — 있음 · 없음(`@Sash.!Off`는 `Off` 벌을 축에 올리는 조건이다 — 없음은 렌더러가 없다). 머리 둘이면 깎은 몸통을 따라 깎는다.
                var sashBox = Box3(0f, (y0 + y1) * 0.5f + 0.06f, 0.03f, 1.10f, y1 - y0, 1.10f);
                Opt(bb.Parts, "Spine.Pick.Sash.On@Sash.!Off@Heads.One", "Pants", Line, p => OgreSash(p, j), sashBox, BruteFineStep * 1.3f, BruteKeep, true);
                Opt(bb.Parts, "Spine.Pick.Sash.On.Twin@Heads.Two", "Pants", Line, p => OgreSaddle(OgreSash(p, j), p, j), sashBox, BruteFineStep * 1.3f, BruteKeep, false, sceneTwin);
                Body("Hips.Brute.Loin",    "Pants", Line, p => OgreLoin(p, j), Box3(0f, j.Hip - 0.03f, 0.04f, 1.10f, 0.52f, 1.00f), BruteFineStep * 1.3f);
                // 쇠 장식 · 눈 흰자는 **몸의 바깥 선**(`Line`)을 받는다(2026-11-01, 사용자: *"눈쪽에 검은색 · 허리띠 장신구에도 검은 선"*) —
                // 얼굴 선(`Fine`)은 *제 얼굴 표시(+128)*가 적힌 자리만 비키는데 오우거 살 · 천은 몸 번호만 적어, 반쯤 묻힌 껍데기가 살 위에 띠로 섰다.
                Body("Hips.Brute.Buckle",  "BoneArmor", Line, p => OgreBuckle(p, j), Box3(0f, j.Hip + 0.07f, 0.42f, 0.28f, 0.28f, 0.22f), BruteFineStep * 0.8f, BruteFineKeep);
            }
            else
            {
                // 여성 — 가슴을 감은 천(머리 둘이면 깎은 몸통을 따라) · 무릎까지 두른 치마 · 작은 쇠 장식.
                var wrapBox = Box3(0f, top - 0.18f, 0.03f, 0.90f, 0.64f, 0.84f);
                Opt(bb.Parts, "Spine.Pick.Heads.One.Wrap", "Pants", Line, p => OgreFWrap(p, j), wrapBox, BruteFineStep * 1.1f, BruteKeep, true);
                Opt(bb.Parts, "Spine.Pick.Heads.Two.Wrap", "Pants", Line, p => OgreSaddle(OgreFWrap(p, j), p, j), wrapBox, BruteFineStep * 1.1f, BruteKeep, false, sceneTwin);
                Body("Hips.Brute.Skirt",  "Pants", Line, p => OgreFSkirt(p, j), Box3(0f, j.Hip - 0.15f, 0f, 1.28f, 0.72f, 1.08f), BruteFineStep * 1.3f);
                Body("Hips.Brute.Buckle", "BoneArmor", Line, p => OgreFBuckle(p, j), Box3(0f, j.Hip + 0.10f, 0.40f, 0.24f, 0.24f, 0.22f), BruteFineStep * 0.8f, BruteFineKeep);
            }

            // 머리 — 하나(`@Heads.One`) · 둘이면 양 어깨에 한 벌씩(`@Heads.Two` · 눈 · 뿔 축은 머리마다 `L`/`R`).
            OgreFace(0); OgreFace(-1); OgreFace(1);

            // 구울 때는 **머리 하나 · 눈 둘만 켜 둔다** — 씬에 놓인 것은 에디터에서 `PawnLook`이 안 돌아 구운 그대로 보인다. 외눈까지 켜 두면 두 벌이 겹쳐
            // 눈꺼풀이 얼굴을 덮었다(2026-11-01, 사용자: *"그냥 씬일 때 왜 이렇게 보이지"*). 판에서는 씨앗이 하나를 고른다.
            for (int i = 0; i < head.Count; i++)
                if (head[i].Name.StartsWith("Head.Pick.Eyes.One.") || head[i].Name.StartsWith("Head.Pick.Lids.On.One"))
                {
                    var x = head[i];
                    head[i] = new Part(x.Name, x.Mesh, x.Center, x.Size, x.Mat, false, outerMat: x.OuterMat);
                }
        }
        else
        {
            // **트롤 — 미리보기 v50**(`ChibiPawnCreator.Troll`). 상자는 미리보기에서 각 조각이 차지하는 범위를 잰 것(머리 높이 · 여유 0.04) —
            // 모양을 고쳐 상자 밖으로 나가면 조각이 잘린다(다시 잴 것).
            Body("Spine.Brute.Torso", Flesh, Line, torso, Box3(0f, 1.008f, 0.124f, 1.34f, 1.24f, 0.93f), BruteStep);
            Body("Spine.Brute.Belly", "Belly", null, p => TrollBelly(p, j), Box3(0f, 0.894f, 0.328f, 0.60f, 0.73f, 0.46f), BruteFineStep * 1.6f);
            Body("Hips.Brute.Loin",   "Pants", Line, p => TrollLoin(p, j), Box3(0f, 0.540f, 0.040f, 0.94f, 0.39f, 0.82f), BruteFineStep * 1.2f);

            Head("Head.Brute.Skull", Flesh, Line, skull, Box3(0f, 1.278f, 0.526f, 0.59f, 0.59f, 0.59f), 0.006f, BruteFineKeep);
            Head("Head.Brute.EyeWhite", "EyeWhite", Fine, p => TrollEyeWhite(p, j), Box3(0f, 1.338f, 0.658f, 0.45f, 0.13f, 0.13f), 0.004f, BruteFineKeep);
            Head("Head.Brute.Iris",  "Iris", null, p => TrollIris(p, j), Box3(0f, 1.338f, 0.680f, 0.40f, 0.09f, 0.07f), 0.003f, BruteFineKeep);
            Head("Head.Brute.Pupil", "NoseDark", null, p => TrollPupil(p, j), Box3(0f, 1.338f, 0.688f, 0.36f, 0.06f, 0.05f), 0.0025f, BruteFineKeep);
            Head("Head.Brute.Mouth", "NoseDark", null, p => TrollMouth(p, j), Box3(0f, 1.206f, 0.610f, 0.34f, 0.20f, 0.22f), BruteFineStep, BruteFineKeep);
            Head("Head.Brute.Teeth", "Bone", Fine, p => TrollTeeth(p, j), Box3(0f, 1.194f, 0.724f, 0.30f, 0.15f, 0.14f), 0.004f, BruteFineKeep);

            // 목 — 쇠목줄 · 뼈 목걸이 · 없음(`@Neck.!Off`는 `Off` 벌을 축에 올리는 조건이다 — 없음은 렌더러가 없다).
            // 쇠목줄과 사슬은 머리 좌표로 빚었으니 머리 관절에 붙는다 · 사슬은 고리마다 따로(판에서 `DanglingChain`이 흔든다).
            Opt(head, "Head.Pick.Neck.Collar.Ring@Neck.!Off", "BoneArmor", Fine, p => TrollCollar(p, j), Box3(0f, 1.290f, 0.448f, 0.59f, 0.66f, 0.24f), 0.006f, BruteFineKeep, true);
            var neckTop = TrollCollarBottom(j) + Vector3.up * 0.010f;
            for (int i = 0; i < NeckLinks; i++)
            {
                var c = LinkCenter(neckTop, i);
                bool turn = i % 2 == 0;   // 첫 고리는 목줄 띠를 꿰도록 옆으로 선다
                Opt(head, $"Head.Pick.Neck.Collar.Link{i}", "BoneArmor", Fine, p => Link(p, c, turn), LinkBounds(c), 0.003f, BruteFineKeep, true);
            }
            Opt(bb.Parts, "Spine.Pick.Neck.Bones.Cord",  "NoseDark", Line, p => TrollBoneCord(p, j),  Box3(0f, 1.160f, 0.437f, 0.48f, 0.52f, 0.08f), 0.0025f, BruteFineKeep, false);
            Opt(bb.Parts, "Spine.Pick.Neck.Bones.Teeth", "Bone",  Fine, p => TrollBoneTeeth(p, j), Box3(0f, 0.930f, 0.417f, 0.62f, 0.32f, 0.17f), 0.003f, BruteFineKeep, false);
            Opt(bb.Parts, "Spine.Pick.Neck.Bones.Beads", "Bone",  Fine, p => TrollBoneBeads(p, j), Box3(0f, 0.945f, 0.461f, 0.38f, 0.17f, 0.13f), 0.003f, BruteFineKeep, false);
        }

        foreach (int sd in Sides)
        {
            string lr = sd < 0 ? "L" : "R";
            int k = sd;
            Func<Vector3, float> On(Func<Vector3, BruteJoints, bool, float> f) => p => f(Side(p, k), j, ogre);
            Bounds Limb(float x, float y0, float y1, float pad) => new(new Vector3(x * k, (y0 + y1) * 0.5f, 0.02f),
                                                                      new Vector3(pad * 2f, Mathf.Abs(y0 - y1) + pad * 2f, pad * 2f));

            // 트롤은 미리보기에서 잰 상자(오른쪽 — `k`가 뒤집는다).
            Bounds TB(float cx, float cy, float cz, float sx, float sy, float sz) => new(new Vector3(cx * k, cy, cz), new Vector3(sx, sy, sz));

            Body($"Arm{lr}.Brute.Upper",     Flesh, Line, On(BruteUpper), ogre ? Limb(j.AX, j.ArmTop, j.Elbow, 0.30f) : TB(0.578f, 1.080f, 0.124f, 0.52f, 0.82f, 0.51f), BruteLimbStep);
            Body($"LowerArm{lr}.Brute.Fore", Flesh, Line, On(BruteFore),  ogre ? Limb(j.AX, j.Elbow, j.Wrist, 0.28f) : TB(0.602f, 0.726f, 0.172f, 0.45f, 0.71f, 0.44f), BruteLimbStep);
            Body($"Hand{lr}.Brute.Hand",     Flesh, Line, On(BruteHand),  ogre ? Limb(j.AX, j.Wrist + 0.02f, j.Wrist - 0.40f, 0.24f) : TB(0.602f, 0.294f, 0.226f, 0.33f, 0.47f, 0.40f), BruteFineStep * 1.4f);
            Body($"Leg{lr}.Brute.Thigh",     Flesh, Line, On(BruteThigh), ogre ? Limb(j.LX, j.Hip, j.Knee, 0.36f) : TB(0.206f, 0.486f, -0.002f, 0.47f, 0.66f, 0.47f), BruteLimbStep);   // 오우거 앞 근육
            Body($"LowerLeg{lr}.Brute.Shin", Flesh, Line, On(BruteShin),  ogre ? Limb(j.LX, j.Knee, j.Ankle, 0.26f) : TB(0.206f, 0.276f, 0.028f, 0.42f, 0.55f, 0.41f), BruteLimbStep);
            Body($"Foot{lr}.Brute.Foot",     Flesh, Line, On(BruteFoot),
                 ogre ? new Bounds(new Vector3(j.LX * k, 0.11f, 0.13f), new Vector3(0.54f, 0.32f, 0.82f)) : TB(0.206f, 0.084f, 0.106f, 0.47f, 0.27f, 0.61f), BruteFineStep * 1.4f);
            if (!ogre)
            {
                // 손목 — 쇠고랑(걸쇠 + 끊어진 사슬 셋) · 가죽 끈 · 없음 / 발목 — 족쇄 · 없음(양쪽이 한 축이다). 사슬은 고리마다 따로(`DanglingChain`).
                Opt(bb.Parts, $"LowerArm{lr}.Pick.Wrist.Shackle.Band@Wrist.!Off", "BoneArmor", Fine, p => TrollCuff(Side(p, k), j),
                    TB(0.602f, j.Wrist + 0.05f, 0.06f + _tf.ArmShift, 0.42f, 0.16f, 0.42f), 0.005f, BruteFineKeep, true);
                var cuffTop = TrollCuffChainTop(j) + Vector3.forward * _tf.ArmShift;   // 팔 좌표 → 몸 좌표
                for (int i = 0; i < WristLinks; i++)
                {
                    var c = LinkCenter(cuffTop, i);
                    c.x *= k;
                    bool turn = i % 2 == 0;   // 첫 고리가 걸쇠를 꿰도록 옆으로 선다
                    Opt(bb.Parts, $"LowerArm{lr}.Pick.Wrist.Shackle.Link{i}", "BoneArmor", Fine, p => Link(p, c, turn), LinkBounds(c), 0.003f, BruteFineKeep, true);
                }
                Opt(bb.Parts, $"LowerArm{lr}.Pick.Wrist.Wrap", "Pants", Line, p => TrollWrap(Side(p, k), j), TB(0.602f, 0.558f, 0.178f, 0.42f, 0.25f, 0.44f), 0.005f, BruteFineKeep, false);
                Opt(bb.Parts, $"LowerLeg{lr}.Pick.Ankle.Shackle@Ankle.!Off", "BoneArmor", Fine, p => TrollAnklet(Side(p, k), j), TB(0.206f, 0.222f, 0.028f, 0.44f, 0.15f, 0.45f), 0.004f, BruteFineKeep, true);
            }
            if (ogre)
            {
                // 팔목 띠 · 가시 — 있음 · 없음(양팔이 한 축이다).
                Opt(bb.Parts, $"LowerArm{lr}.Pick.Bands.On.Band@Bands.!Off", "Pants", Line, fem ? p => OgreFBand(Side(p, k), j) : p => OgreBand(Side(p, k), j),
                    Limb(j.AX, j.Wrist + 0.26f, j.Wrist + 0.04f, 0.22f), BruteFineStep * 1.2f, BruteKeep, true);
                if (!fem)   // 여성 팔목 띠에는 가시가 없다(미리보기 그대로)
                    Opt(bb.Parts, $"LowerArm{lr}.Pick.Bands.On.Spike", "BoneArmor", Fine, p => OgreBandSpike(Side(p, k), j),
                        new Bounds(new Vector3((j.AX + 0.21f) * k, j.Wrist + 0.155f, 0.03f), new Vector3(0.30f, 0.16f, 0.16f)), BruteFineStep * 0.6f, BruteFineKeep, true);
            }
        }

        var made = (bb, head);
        _brutes[kind] = made;
        return made;
    }

    static Bounds Box3(float x, float y, float z, float sx, float sy, float sz) => new(new Vector3(x, y, z), new Vector3(sx, sy, sz));

    // 정점을 이웃 평균 쪽으로 폈다가(λ) 되민다(μ) — 잔물결만 지우고 덩어리는 안 줄인다(Taubin). 법선 · 그늘은 거리장에서 잰 그대로 둔다.
    // 미리보기(메시 에셋을 읽어 선을 같은 식으로 그린 것)로 6번이면 선 가장자리의 털이 사라지는 것을 봤다.
    static void Relax(List<Vector3> v, List<int> tris, int iterations)
    {
        int n = v.Count;
        var nb = new HashSet<int>[n];
        for (int i = 0; i < n; i++) nb[i] = new HashSet<int>();
        for (int t = 0; t < tris.Count; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            nb[a].Add(b); nb[a].Add(c); nb[b].Add(a); nb[b].Add(c); nb[c].Add(a); nb[c].Add(b);
        }
        var tmp = new Vector3[n];
        void Step(float k)
        {
            for (int i = 0; i < n; i++)
            {
                if (nb[i].Count == 0) { tmp[i] = v[i]; continue; }
                var avg = Vector3.zero;
                foreach (int o in nb[i]) avg += v[o];
                avg /= nb[i].Count;
                tmp[i] = v[i] + (avg - v[i]) * k;
            }
            for (int i = 0; i < n; i++) v[i] = tmp[i];
        }
        for (int it = 0; it < iterations; it++) { Step(0.5f); Step(-0.53f); }
    }

    static float Torus(Vector3 p, Vector3 c, float big, float small)
    {
        var q = p - c;
        return new Vector2(new Vector2(q.x, q.z).magnitude - big, q.y).magnitude - small;
    }

    // ── 팔다리(오른쪽에 적는다 — `Side`가 뒤집는다) ─────────────────────────────
    // 트롤 몸통 · 머리 · 팔다리 · 걸친 것은 `ChibiPawnCreator.Troll`(미리보기 v50을 옮긴 것).

    static float BruteUpper(Vector3 m, BruteJoints j, bool ogre) => ogre ? (_ogreF ? OgreFUpper(m, j) : OgreUpper(m, j)) : TrollUpper(m, j);
    static float BruteFore(Vector3 m, BruteJoints j, bool ogre)  => ogre ? (_ogreF ? OgreFFore(m, j) : OgreFore(m, j))  : TrollFore(m, j);
    static float BruteHand(Vector3 m, BruteJoints j, bool ogre)  => ogre ? (_ogreF ? OgreFHand(m, j) : OgreHand(m, j))  : TrollHand(m, j);

    static float BruteThigh(Vector3 m, BruteJoints j, bool ogre)
        => ogre ? (_ogreF ? OgreFThigh(m, j) : OgreThigh(m, j)) : TrollThigh(m, j);

    static float BruteShin(Vector3 m, BruteJoints j, bool ogre)
        => ogre ? (_ogreF ? OgreFShin(m, j) : OgreShin(m, j)) : TrollShin(m, j);

    static float BruteFoot(Vector3 m, BruteJoints j, bool ogre) => ogre ? (_ogreF ? OgreFFoot(m, j) : OgreFoot(m, j)) : TrollFoot(m, j);
}
