using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// **림월드의 얼굴을 3D로 — 체형 여섯 · 머리형 열둘(림월드 이름 그대로) · 머리카락 여섯 · 눈/입 아틀라스 둘**
// (2026-09-18, 사용자가 여러 번 고쳐 줬다 — 마지막: *"얼굴형을 그냥 턱이랑 합쳐서 … 나중에 머리가
//  네모난 폰들도 존재할 수 있는거니깐 … 기본은 이거야 이 비율 이 눈크기 그냥 모든게 완벽해"* ·
//  *"입이랑 눈이랑 좀 구분되게 … 같은곳에서 다 불러올 수 있게 만들지 말고"* ·
//  *"세로로 긴 눈 말고 아예 동그란 눈"*).
//
// ## 저 인상을 만드는 것은 셋이다
//
//   1. **비례** — 보이는 것은 **머리 · 몸통 · 손 셋**뿐. 몸길이 ≈ 머리 × 1.04, 머리는 몸 위에
//      **외곽선 굵기만큼 떠 있어** 그 틈이 통째로 턱 선이 된다. 손은 몸 옆에 떨어져 뜬 공 둘.
//      머리 옆선은 **둥근 뚜껑 + 곧은 옆 + 턱 곡선**이다(타원 하나로 두면 역삼각형이 된다).
//   2. **검은 외곽선** — `Resources/Shaders/ToonOutline.shader`.
//   3. **평면 음영** — 같은 셰이더. 눈·입은 `Resources/Shaders/ChibiFace.shader`.
//
// ## 머리형은 **머리 전체**다 — 뚜껑·턱으로 나누지 않는다
//
// 한동안 머리를 뚜껑(공통) + 턱(교체)으로 나눴다. 그러면 **머리 윗부분은 영영 한 모양**이라
// 네모 머리 같은 폰을 낼 자리가 없었다(사용자: *"나중에 머리가 네모난 폰들도 존재할 수 있는거니깐"*).
// 이제 머리형 하나가 **정수리부터 턱 끝까지 한 덩어리**다. 크기는 림월드처럼 **폭(보통·좁은)과
// 성별이 가른다** — 좁은 머리는 좁고 길다(넓게 부풀리는 머리형은 없다).
//
// **리거가 쓸 `Head`는 안 보이는 구다** — `PawnRigBuilder`가 `Head`라는 살에서 목 관절 자리를 내는데,
// 보이는 머리가 전부 `Head.Shape.*`가 되자 그 이름이 비었다. 팔·다리처럼 살은 두고 렌더러만 끈다.
//
// ## 기본 머리 = 44번 사진
//
// 사진에서 잰 값이다: 머리는 **높이보다 조금 넓고**(0.88 : 0.85), 턱이 **넓고 둥글게** 모인다.
// 눈 지름 ≈ 머리 폭의 15%, 눈 가운데는 한복판에서 좌우 머리 폭의 24%, 높이는 얼굴 가운데보다
// 조금 아래(t 0.45).
//
// ## 눈과 입은 아틀라스가 따로다
//
// 한 장에 섞어 두었더니 드롭다운이 눈·입을 한 목록으로 내서 *눈 자리에 입이 들어갈 수* 있었다
// (사용자: *"같은곳에서 다 불러올 수 있게 만들지 말고"*). 이제 **눈 아틀라스 · 입 아틀라스**가
// 따로 있고 판마다 제 것만 본다. 팩 그림은 `face/eyes/` · `face/mouths/`로 가른다.
//
// ## 이목구비 — 뜯어낸 판 · 채널 분할 마스크 · 알파 자르기
//
// 눈·입을 그릴 자리만 **작은 판으로 뜯어** 얼굴보다 살짝 위에 얹는다(띄우는 높이가 곧 그리는 순서).
// 그림은 색이 아니라 **마스크 넷**(R 잉크 · G 흰자 · B 강조 · A 자르기)이라 눈 색·혀 색은 재질이 칠한다.
// 판은 **머리형마다 따로** 굽는다(`Head.Shape.Base.EyeL`) — 곡면이 달라 한 벌이면 묻히거나 뜬다.
// **왼눈(보는 사람의 오른쪽)이 그림 그대로, 오른눈이 거울**이고 칸은 따로 고른다(윙크가 된다).
//
// ## 조합은 전부 굽고 런타임이 하나씩 켠다
//
// 체형마다 프리팹 하나에 제 성별의 머리형 여섯·머리카락 여섯을 **전부 굽고** `PawnLook`이 씨앗으로 한 벌만
// 켠다(굴리지 않는다). 머리에 얹을 것은 전부 `Head.` 꼴.
//
// ## 몸은 한 덩어리다 (2026-10-09)
//
// 처음 몸은 **머리 · 몸통 · 손 셋**만 보이고 팔다리는 관절 자리만 주는 안 보이는 살이었다. 10-06에 사용자가 팔다리가 보이는
// 비율을 원해 시제품(`ChibiPawnCreator.Proto`)을 따로 지었고, 10-09에 **몸통·팔·다리를 스킨 메시 하나로** 다시 지어
// (*"너무 도형 이어붙인 느낌"* → 된 것을 보고 *"넘어가자"*) 본 몸 전부를 그것으로 옮겼다(`DressSkinned`). 체형은 이제
// **폭만** 가른다(옆선 표 `Shape.Half` · 깊이 · 손) — 높이·다리 길이·팔 길이는 머리 높이의 배수로 모두 같다.
public static partial class ChibiPawnCreator
{
    const string Source   = "Assets/3_Prefab/Pawn/Wanderer_Human.prefab";
    const string OutDir   = "Assets/3_Prefab/Pawn";
    const string MeshDir  = "Assets/3_Prefab/Pawn/Mesh";
    const string MatDir   = "Assets/2_Material/Chibi";
    // **얼굴 그림 폴더** — 눈은 `eyes/`, 입은 `mouths/` 아래 PNG를 훑는다(넣고 빼면 저절로 다시 굽는다).
    public const string PackDir = "Assets/assetsss/face";
    const string ToonShader = "DungeonColony/ToonOutline";
    const string OuterShader = "DungeonColony/ToonOutlineOuter";   // 몸통의 바깥 선만 — 셰이더 머리말
    const string FaceShader = "DungeonColony/ChibiFace";

    // ── 비례 ────────────────────────────────────────────────────────────────
    const float TorsoH  = 0.885f;                 // 레퍼런스의 몸통 높이(87.5px)가 이것이다 — 머리 크기의 눈금

    // **머리 크기 배율** — 1이면 레퍼런스 열두 장과 **같은 비**다(머리 높이 ≈ 몸통의 0.78 · 머리 폭 ≈ 몸통의 0.9).
    // 머리를 통째로 키우거나 줄이고 싶으면 여기 하나만 만진다(모양·눈 자리는 그대로 따라온다).
    const float HeadScale = 1f;

    // **머리와 몸 사이 틈 = 외곽선 굵기(6cm)보다 조금 넓게.** 2cm로 두었더니 몸통 꼭대기가 머리
    // 외곽선 껍질(머리를 6cm 부풀린 검은 겉) **안으로 들어가**, 검은 선을 뚫고 빨간 셔츠가 비쳤다
    // (사용자: *"턱이랑 몸 사이의 외곽선에 빨간셔츠가 튀어나오는게 불편해"*). 틈이 선보다 넓으면
    // 틈이 통째로 검은 선이 된다 — 레퍼런스에서 머리와 몸 사이에 **굵은 선 하나**만 있는 그 모양이다.
    // **외곽선 굵기를 바꾸면 이것도 같이 바꿀 것**(`_OutlineWidth` 0.06).
    const float HeadGap = 0.065f;
    const float HeadZ   = 0.04f;                  // 앞으로 조금 — 턱 선이 몸통 앞면 위에 얹힌다

    // 머리카락이 시작하는 높이(t — 머리 밑 0 · 정수리 1). 이마 선이다 — 기본 머리의 뚜껑 자리와 같다.
    const float HairLine = 0.62f;

    const float TopRound = 0.55f;   // 몸통 어깨 둥글림(× 어깨 반폭) — 한 덩어리 몸(`BodyTorsoRadius`)이 쓴다

    // ── 체형 ────────────────────────────────────────────────────────────────
    readonly struct Shape
    {
        public readonly string  Suffix, Label, Gender;   // Gender — 어느 머리 여섯을 굽나
        public readonly float[] Half;    // 반폭(m) — 밑·엉덩이·허리·가슴·어깨
        public readonly float   Depth;
        public readonly float   HandR;
        public Shape(string suffix, string label, string gender, float[] half, float depth, float handR)
        { Suffix = suffix; Label = label; Gender = gender; Half = half; Depth = depth; HandR = handR; }
    }

    static readonly float[] Stations = { 0f, 0.30f, 0.55f, 0.80f, 1f };

    static readonly Shape[] Shapes =
    {
        //                                                       밑     엉덩이  허리   가슴   어깨      깊이   손
        new Shape("Male",       "남성",      "Male",   new[] { 0.21f, 0.24f, 0.26f, 0.30f, 0.31f }, 0.82f, 0.30f),
        // 여성 엉덩이는 **남성과 같다**(2026-10-09, 사용자: *"Female은 허리가 너무 넓어"* → 1.2배로 줄였다가 *"남성 대비 크게
        // 말고 남성이랑 똑같게"*). 다리가 보이는 몸에서는 엉덩이 폭이 바지통 두 다리의 굵기까지 정해서(`DimsOf`), 옛 값 0.37
        // (남성의 1.54배)이면 아랫몸이 통째로 퍼졌다.
        new Shape("Female",     "여성",      "Female", new[] { 0.23f, 0.24f, 0.21f, 0.27f, 0.22f }, 0.80f, 0.27f),
        new Shape("ThinMale",   "마른 남성", "Male",   new[] { 0.19f, 0.20f, 0.21f, 0.23f, 0.24f }, 0.78f, 0.26f),
        new Shape("ThinFemale", "마른 여성", "Female", new[] { 0.18f, 0.22f, 0.17f, 0.21f, 0.20f }, 0.76f, 0.25f),
        new Shape("Fat",        "뚱뚱",      "Male",   new[] { 0.36f, 0.46f, 0.46f, 0.39f, 0.29f }, 0.95f, 0.31f),
        new Shape("Hulk",       "헐크",      "Male",   new[] { 0.27f, 0.31f, 0.34f, 0.41f, 0.43f }, 0.90f, 0.34f),
    };

    // ── 머리형 — 림월드의 열둘 ────────────────────────────────────────────────
    //
    // (2026-09-18, 사용자가 림월드 머리형 열두 장을 순서대로 찍어 보냈다 — *"저 얼굴형들에 100%
    //  일치하게 … 턱이 좀 앞으로 나와있어야해 뒤는 둥글고"*.)
    //
    // **옆선을 숫자로 짓지 않고 그림에서 잰다**(`ChibiHeadProfiles` — 자동 생성). 전에는 뚜껑·곧은 옆·
    // 턱 곡선을 지수로 적었는데, 그렇게 **눈대중으로 맞춘 모양은 세 번 다 틀렸다**(역삼각형 외계인 두 번 ·
    // 실크햇 한 번). 재 보니 실제 림월드 머리는 **가장 넓은 곳이 한복판보다 위(t 0.5~0.65)**이고
    // 턱은 **아래 3분의 1에서만** 모인다 — 곧은 옆이라는 가정부터가 틀렸다. 이제 그림의 살색 윤곽을
    // 한 행씩 재어 **그 반폭을 그대로 돌려 깎는다**. 정면 실루엣이 곧 그림이다.
    //
    // **깊이는 그림에 없다** — 폭의 0.95로 두고(뒤통수는 둥근 채), **턱은 앞으로 민다**(`Shear` — 아랫반쪽만).
    //
    // 크기도 그림에서 온다 — 같은 그림 속 몸통 높이가 `TorsoH`라서 픽셀 × (`TorsoH` ÷ 몸통 픽셀)이 곧 미터다.
    readonly struct HeadShape
    {
        public readonly string  Name, Gender;
        public readonly Vector3 Size;
        public readonly ChibiHeadProfiles.Ref Ref;
        public readonly float   Forward, EyeT, EyeDx, EyeSize, EyeAspect, MouthT, NoseT, NoseK;
        // 눈·입 자리는 **사람이 고친 표**(`ChibiFaceLayout`)에서 온다 — 처음 값은 측정이고, 고친 것이 이긴다.
        public HeadShape(ChibiHeadProfiles.Ref r, ChibiFaceLayout layout)
        {
            var e = layout != null ? layout.Of(r.Name) : null;
            Ref = r; Name = r.Name; Gender = r.Name.Substring(0, r.Name.IndexOf('_'));
            float m = TorsoH / ChibiHeadProfiles.BodyPx * HeadScale;   // 픽셀 → 미터
            Size = new Vector3(r.W * m, r.H * m, r.W * m * Depth);
            Forward = r.Name.EndsWith("Sharp") ? 0.26f : 0.20f;
            e ??= ChibiFaceLayout.Measured(r);
            // 전체 손잡이(`ChibiFaceLayout`의 위쪽 넷)를 머리마다의 값 위에 얹는다.
            float sizeK  = layout != null ? layout.EyeSizeScale    : ChibiFaceLayout.DefaultEyeSizeScale;
            float lift   = layout != null ? layout.EyeHeightOffset : 0f;
            float spread = layout != null ? layout.EyeSpacingScale : 1f;
            EyeAspect    = layout != null ? layout.EyeAspect       : ChibiFaceLayout.DefaultEyeAspect;
            EyeT = e.EyeHeight + lift; EyeDx = e.EyeSpacing * spread; MouthT = e.MouthHeight;
            NoseT = e.NoseHeight > 0f ? e.NoseHeight : ChibiFaceLayout.DefaultNose(e);
            NoseK = layout != null ? layout.NoseSizeScale : 1f;
            // 눈 판 한 변 = 눈 지름 ÷ 0.6(판 안에서 동그란 눈이 차지하는 몫 — `ink_eye` 반지름 0.30)
            EyeSize = e.EyeSize * sizeK * Size.x / 0.6f;
        }
    }

    const float Depth = 0.95f;   // 깊이 ÷ 폭 — 그림에 없는 값이라 둥글게

    // 굽기가 시작할 때 **표를 읽어 다시 만든다**(정적 초기화에 박으면 고친 값이 에디터를 껐다 켤 때까지 안 먹는다).
    static HeadShape[] Heads = MakeHeads(null);

    static HeadShape[] MakeHeads(ChibiFaceLayout layout)
    {
        var list = new List<HeadShape>();
        foreach (var r in ChibiHeadProfiles.All) list.Add(new HeadShape(r, layout));
        return list.ToArray();
    }

    static readonly string[] Genders = { "Male", "Female" };

    // 기준 머리 — 그 성별의 Average·Normal. 머리카락은 여기에 맞춰 굽고 `PawnLook`이 다른 머리로 옮긴다.
    static HeadShape BasisOf(string gender)
    {
        foreach (var h in Heads)
            if (h.Gender == gender && h.Name.EndsWith(PawnLook.BasisShape)) return h;
        return Heads[0];
    }

    static Vector3 HeadPos(HeadShape h) => new(0f, TorsoH + HeadGap + h.Size.y * 0.5f, HeadZ);

    static readonly (string name, Color color)[] Mats =
    {
        ("Skin",  new Color32(0xF2, 0xD2, 0xA8, 0xFF)),
        // **머리 살은 손·팔·귀의 살과 재질을 가른다**(2026-09-19) — 색은 같고 **스텐실에 "얼굴"을 적는 것**만 다르다.
        // 코 선은 제 얼굴 위에서만 비키고 손·몸통·다른 폰 위에서는 서야 하는데(사용자: *"머리처럼 다른 부위랑 겹쳐졌을때도
        // 외곽선이 보여야지"* · *"근데 머리랑은 외곽선이 보이면 안돼"*), 한 재질이면 얼굴과 손을 스텐실로 못 가른다.
        ("HeadSkin", new Color32(0xF2, 0xD2, 0xA8, 0xFF)),
        ("Shirt", new Color32(0xC4, 0x45, 0x3C, 0xFF)),
        ("Pants", new Color32(0x46, 0x37, 0x32, 0xFF)),   // 다리 — 비율 시제품(`ChibiPawnCreator.Proto`)
        ("Shoe",  new Color32(0x5A, 0x3C, 0x2D, 0xFF)),   // 신발 — 같은 곳
        // 시제품의 손 — 색은 살과 같고 **선을 바깥에만** 받는다(본 몸의 손은 `Skin` 그대로 — 몸통 선이 제 손 위에도 서야 한다).
        ("SkinOuter", new Color32(0xF2, 0xD2, 0xA8, 0xFF)),
        ("Hair",  new Color32(0xA8, 0x72, 0x3C, 0xFF)),
        // **코는 제 선을 끄고 바깥 선만 받는다**(2026-09-19, 사용자: *"코에 외곽선이 떠서 문제가 있고"*) — 제 재질의
        // 6cm 선은 스텐실을 안 봐서 **얼굴 위에 검은 덩어리**로 떴다. 선은 `OutlineOuterFine`이 얼굴 밖에만 긋는다.
        ("SkinFine",  new Color32(0xF2, 0xD2, 0xA8, 0xFF)),
        ("NoseDark",  new Color32(0x2E, 0x24, 0x24, 0xFF)),   // 동물 코 — 짙은 코끝
    };

    // 제 선을 끄고 바깥 선을 따로 받는 코 재질 — 이름으로 가른다(`Mats`의 둘).
    static bool FineOutline(string mat) => mat == "SkinFine" || mat == "NoseDark";

    // ── 이목구비 판 ─────────────────────────────────────────────────────────
    //
    // 자리는 **방위각(앞 = 90°)과 높이(t)**이고 **머리마다 그림에서 잰 값**이다(눈 높이 · 한복판에서 떨어진
    // 거리 · 지름). 판은 곡면을 따라 깎이므로 각도·t로 되돌리는데, 그 값도 머리마다 계산한다 — 같은
    // 각도를 쓰면 좁은 머리에서 눈이 **세로로 길쭉해진다**. 판은 늘 **실제로 정사각**이다.
    readonly struct Feature
    {
        public readonly string Name, Kind, DefaultCell;
        public readonly int    Side;     // -1 · +1 = 눈의 쪽 · 0 = 가운데(입)
        public readonly float  Lift;
        public readonly bool   Mirror;
        public readonly FaceSlot Slot;   // 어느 높이 · 어느 크기를 쓰나
        public Feature(string name, string kind, string cell, int side, float lift, bool mirror, FaceSlot slot)
        { Name = name; Kind = kind; DefaultCell = cell; Side = side; Lift = lift; Mirror = mirror; Slot = slot; }
    }

    // **왼눈(방위 90° + = 보는 사람의 오른쪽)이 그림 그대로**이고 오른눈이 거울이다 — 그 눈의 코 쪽이
    // 그림의 왼쪽이다. 입은 기본이 빈 칸.
    static readonly Feature[] Features =
    {
        new Feature("EyeR",  "Eyes",   "ink_eye", -1, 1.012f, mirror: true,  FaceSlot.Eye),
        new Feature("EyeL",  "Eyes",   "ink_eye", +1, 1.012f, mirror: false, FaceSlot.Eye),
        new Feature("Mouth", "Mouths", "",          0, 1.008f, mirror: false, FaceSlot.Mouth),
    };
    const float MouthScale = 0.75f;   // 입 판 = 눈 판 × 이만큼
    enum FaceSlot { Eye, Mouth }   // 코는 판이 아니라 입체다(`Noses`)

    const int CellPx = 128, CellPad = 3;

    // ── 옆선 ────────────────────────────────────────────────────────────────

    static float HalfAt(float[] half, float t)
    {
        int n = Stations.Length;
        t = Mathf.Clamp01(t);
        int i = 1;
        while (i < n - 1 && t > Stations[i]) i++;
        int a = i - 1, b = i;

        float Slope(int k)
        {
            int lo = Mathf.Max(0, k - 1), hi = Mathf.Min(n - 1, k + 1);
            return (half[hi] - half[lo]) / (Stations[hi] - Stations[lo]);
        }

        float h = Stations[b] - Stations[a];
        float u = (t - Stations[a]) / h, u2 = u * u, u3 = u2 * u;
        return ( 2f * u3 - 3f * u2 + 1f) * half[a] + (u3 - 2f * u2 + u) * h * Slope(a)
             + (-2f * u3 + 3f * u2)      * half[b] + (u3 - u2)          * h * Slope(b);
    }

    // **머리 옆선** — 그림에서 잰 반폭(최대 0.5). 크기는 `HeadShape.Size`가 곱한다.
    static float HeadRadius(float t, HeadShape h) => h.Ref.RadiusAt(t);

    // ── 돌려 깎기 ───────────────────────────────────────────────────────────

    // 극 쪽 고리를 촘촘히 — -1 시작 끝 · +1 마지막 끝 · 2 양 끝 · 0 고르게. 고르게 나누면 둥근 끝의
    // **마지막 한 칸이 원뿔**이 되어 턱·정수리가 뾰족하게 꽂힌다.
    static float Ring(int i, int rings, int densePole)
    {
        float f = i / (float)rings;
        switch (densePole)
        {
            case -1: return 1f - Mathf.Cos(f * Mathf.PI * 0.5f);
            case +1: return Mathf.Sin(f * Mathf.PI * 0.5f);
            case  2: return (1f - Mathf.Cos(f * Mathf.PI)) * 0.5f;
            default: return f;
        }
    }

    static Mesh Lathe(string name, Func<float, float> radiusAt, int rings, int segs, out float maxR,
                      bool normalize = true, float tFrom = 0f, float tTo = 1f,
                      float startDeg = 0f, float sweepDeg = 360f, float lift = 1f, bool cap = true,
                      int densePole = 0)
    {
        var rs = new float[rings + 1];
        var ts = new float[rings + 1];
        maxR = 0f;
        for (int i = 0; i <= rings; i++)
        {
            ts[i] = Mathf.Lerp(tFrom, tTo, Ring(i, rings, densePole));
            rs[i] = radiusAt(ts[i]);
            // **옆선이 숫자가 아니면 굽지 않는다** — NaN 정점은 화면에서 조용히 빠질 뿐이라, 모르고 구우면 한참 뒤
            // 엉뚱한 자리(내보내기·레이캐스트)에서 터진다. 여기서 이름을 대고 멈춘다.
            if (float.IsNaN(rs[i]) || float.IsInfinity(rs[i]))
                throw new InvalidOperationException($"[치비] {name}: 옆선이 t={ts[i]:F3}에서 {rs[i]}를 냈다 — 음수의 거듭제곱·0 나누기를 확인할 것");
            maxR = Mathf.Max(maxR, rs[i]);
        }
        float norm = (normalize && maxR > 1e-5f ? 0.5f / maxR : 1f) * lift;
        bool closed = cap && sweepDeg >= 359.9f;

        var verts = new List<Vector3>();
        var uvs   = new List<Vector2>();
        var tris  = new List<int>();

        for (int i = 0; i <= rings; i++)
        {
            float y = (ts[i] - 0.5f) * lift;
            float r = rs[i] * norm;
            for (int k = 0; k <= segs; k++)
            {
                float a = (startDeg + sweepDeg * k / segs) * Mathf.Deg2Rad;
                verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(k / (float)segs, i / (float)rings));
            }
        }
        int stride = segs + 1;
        for (int i = 0; i < rings; i++)
        for (int k = 0; k < segs; k++)
        {
            int a = i * stride + k, b = a + 1, c = a + stride, d = c + 1;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }

        void Cap(int ring, float y, bool up)
        {
            if (!closed || rs[ring] * norm < 1e-4f) return;
            int center = verts.Count;
            verts.Add(new Vector3(0f, y, 0f));
            uvs.Add(new Vector2(0.5f, up ? 1f : 0f));
            int b0 = ring * stride;
            for (int k = 0; k < segs; k++)
            {
                // 감김이 곧 앞면이다 — 거꾸로 감으면 **뚜껑이 안쪽을 본다**(2026-09-19에 법선을 계산해 잡았다: 윗뚜껑이
                // (0,−1,0)이었다). 그러면 앞에서 본 코끝 뚜껑이 뒷면이라 살은 안 그려지고 **외곽선 껍질(Cull Front)이
                // 그 자리를 검게 칠했다.** 몸통 위아래 뚜껑도 같은 함수라 같이 뒤집혀 있었다.
                if (up) { tris.Add(center); tris.Add(b0 + k + 1); tris.Add(b0 + k); }
                else    { tris.Add(center); tris.Add(b0 + k); tris.Add(b0 + k + 1); }
            }
        }
        Cap(0, (tFrom - 0.5f) * lift, up: false);
        Cap(rings, (tTo - 0.5f) * lift, up: true);

        return Save(name, verts, uvs, tris);
    }

    // **아랫반쪽을 앞으로 민다** — z += forward × 2y² (적도에서 밀림도 기울기도 0).
    static Mesh Shear(Mesh m, float forward)
    {
        var v = m.vertices;
        for (int i = 0; i < v.Length; i++)
            if (v[i].y < 0f) v[i].z += forward * 2f * v[i].y * v[i].y;
        m.vertices = v;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // **판의 UV를 왼쪽 아래 칸 안으로 옮긴다**(여백 안쪽) — 칸 고르기는 셰이더가 칸 단위로 더한다.
    static Mesh ToCell(Mesh m, int cells, bool mirror)
    {
        var uv = m.uv;
        float p = CellPad / (float)CellPx, span = 1f - 2f * p;
        for (int i = 0; i < uv.Length; i++)
        {
            float u = mirror ? 1f - uv[i].x : uv[i].x;
            uv[i] = new Vector2((p + u * span) / cells, (p + uv[i].y * span) / cells);
        }
        m.uv = uv;
        EditorUtility.SetDirty(m);
        return m;
    }

    // **메시는 있으면 속만 갈아 끼운다** — `CreateAsset`을 같은 경로에 다시 부르면 옛 객체가
    // 죽어 열린 씬·로드된 프리팹이 빈 렌더러를 그린다(`VeinArt.Save`가 겪었다).
    static readonly HashSet<string> _saved = new();

    static Mesh Save(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {
        EnsureDir(MeshDir);
        string path = $"{MeshDir}/{name}.asset";
        _saved.Add(path);
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool fresh = m == null;
        if (fresh) m = new Mesh(); else m.Clear();

        m.name = name;
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();

        if (fresh) AssetDatabase.CreateAsset(m, path);
        else       EditorUtility.SetDirty(m);
        return m;
    }

    // ── 살 ──────────────────────────────────────────────────────────────────

    readonly struct Part
    {
        public readonly string  Name;
        public readonly Mesh    Mesh;          // null이면 구 프리미티브
        public readonly Vector3 Center, Size;
        public readonly string  Mat;
        public readonly bool    On;
        public readonly string  Cell;          // 이목구비 판이면 기본 칸 이름
        public readonly string  Kind;          // 이목구비 판이면 어느 아틀라스(Eyes · Mouths)
        public readonly string  OuterMat;      // 외곽선을 몸 밖에만 긋는 둘째 재질(`ToonOutlineOuter`) — 없으면 null
        public readonly Vector3 Euler;         // 기울여 놓는 것(귀·코) — 나머지는 0
        public Part(string name, Mesh mesh, Vector3 center, Vector3 size, string mat, bool on = true,
                    string cell = null, string kind = null, string outerMat = null, Vector3 euler = default)
        { Name = name; Mesh = mesh; Center = center; Size = size; Mat = mat; On = on; Cell = cell; Kind = kind; OuterMat = outerMat; Euler = euler; }
    }

    static List<Part> HeadParts(string gender, Dictionary<string, Mesh> heads,
                                Dictionary<(string head, string feature), Mesh> features,
                                Dictionary<string, Mesh> hairs, Dictionary<string, Mesh> ears,
                                Dictionary<string, Mesh> noses)
    {
        var parts = new List<Part>();
        var basis = BasisOf(gender);
        var bPos  = HeadPos(basis);

        // **리거가 쓸 `Head`** — 안 보이는 구. 목 관절 자리가 여기서 나온다(`Bottom` = 턱 끝).
        parts.Add(new Part("Head", null, bPos, basis.Size, "HeadSkin", on: false));

        // 이 성별의 머리 여섯과 그 이목구비 — 기준만 켜 둔다.
        foreach (var h in Heads)
        {
            if (h.Gender != gender) continue;
            bool on = h.Name == basis.Name;
            var pos = HeadPos(h);
            parts.Add(new Part($"Head.Shape.{h.Name}", heads[h.Name], pos, h.Size, "HeadSkin", on));
            foreach (var f in Features)
                parts.Add(new Part($"Head.Shape.{h.Name}.{f.Name}", features[(h.Name, f.Name)],
                                   pos, h.Size, "Face" + f.Kind, on, cell: f.DefaultCell, kind: f.Kind));
        }

        // 머리카락 — **기준 머리에 맞춰 굽고** `PawnLook`이 고른 머리로 옮긴다(폭·높이·자리를 같은 비로).
        // 뚜껑·커튼은 **윗머리 모양이 둘**이라(둥근 · 네모) 두 벌 굽고 `PawnLook.HairFitOf`가 하나만 켠다
        // (네모 머리에 둥근 뚜껑을 씌우면 모서리가 뚫고, 둥근 머리에 네모 뚜껑이면 머리가 각져 보인다).
        var capSize = basis.Size * 1.06f;
        var capPos  = bPos + new Vector3(0f, 0.005f, -0.01f);
        string basisFit = PawnLook.HairFitOf(basis.Name);
        void Hair(string style, string part, Mesh mesh, Vector3 pos, Vector3 size, bool on)
            => parts.Add(new Part($"Head.Hair.{style}.{part}", mesh, pos, size, "Hair", on));
        void Shell(string style, string sides, bool on)
        {
            foreach (var fit in HairFits)
                Hair(style, $"Shell.{fit}", hairs[$"{fit}.{sides}"], capPos, capSize, on && fit == basisFit);
        }

        Shell("Short",    "Short", on: true);
        Shell("Bob",      "Bob",   on: false);
        Shell("Long",     "Long",  on: false);
        Hair ("Long",     "Back",  null, bPos + new Vector3(0f, -0.32f, -0.26f), new Vector3(0.60f, 0.60f, 0.26f), on: false);
        Shell("Ponytail", "Short", on: false);
        Hair ("Ponytail", "Tail",  null, bPos + new Vector3(0f, -0.10f, -0.47f), new Vector3(0.17f, 0.48f, 0.17f), on: false);
        Shell("Bun",      "Short", on: false);
        Hair ("Bun",      "Knot",  null, bPos + new Vector3(0f, 0.30f, -0.26f), Vector3.one * 0.26f, on: false);

        // 귀 — **기준 머리에 맞춰 굽고** 머리카락처럼 `PawnLook`이 고른 머리로 옮긴다. 기본은 일반 귀만 켠다.
        // **밑동을 머리 옆에 붙이고 끝을 기울인다** — 가운데를 옆에 두면 긴 귀가 머리 속으로 반쯤 파묻힌다.
        foreach (var e in Ears)
        {
            float t  = basis.EyeT + e.Lift;
            float rx = HeadRadius(t, basis) * basis.Size.x;
            float y  = bPos.y + (t - 0.5f) * basis.Size.y;
            float z  = bPos.z - basis.Size.z * 0.06f;
            foreach (int s in new[] { -1, +1 })
            {
                // 끝이 바깥(±x)으로 `Out`도, 뒤(−z)로 `Back`도 기운다. 납작한 면은 앞뒤를 본다(옆에서 넓게 · 앞에서 날렵하게).
                var euler = new Vector3(-e.Back, 0f, -s * e.Out);
                var dir   = Quaternion.Euler(euler) * Vector3.up;
                var root  = new Vector3(s * (rx - e.Size.x * 0.3f), y, z);
                var c     = root + dir * (e.Size.y * 0.5f - e.Sink);
                parts.Add(new Part($"Head.Ear.{e.Name}.{(s < 0 ? "L" : "R")}", ears[e.Name], c, e.Size, "Skin",
                                   on: e.Name == PawnLook.DefaultEar, euler: euler));
            }
        }

        // 코 — **머리형마다 따로 굽는다**(`Head.Nose.{모양}.{머리형}`). 턱을 앞으로 민 정도(`Forward`)와 코 높이가
        // 머리마다 달라, 한 벌을 옮겨 쓰면 코가 파묻히거나 뜬다. 얼굴 곡면의 앞면 자리를 머리마다 재어 밑동을 붙인다.
        foreach (var h in Heads)
        {
            if (h.Gender != gender) continue;
            var pos = HeadPos(h);
            foreach (var n in Noses)
            {
                // 밑동 — 그 높이의 **얼굴 앞면**(`Shear`와 같은 식으로 턱을 민 만큼 앞으로).
                float t  = h.NoseT + n.Lift;
                float yN = t - 0.5f;
                float front = HeadRadius(t, h) + (yN < 0f ? h.Forward * 2f * yN * yN : 0f);
                var root = new Vector3(0f, pos.y + yN * h.Size.y, pos.z + front * h.Size.z);

                var size  = n.Size * h.NoseK;
                // 축(위)을 앞으로 눕히고 끝을 떨군다 — **90 + 떨굼**이다(90 − 떨굼이면 코끝이 위를 본다: 계산으로 잡았다).
                // 떨굼이 크면(콧대 있는 코) 축이 얼굴을 따라 **아래로** 내려간다.
                var euler = new Vector3(90f + n.Droop, 0f, 0f);
                var dir   = Quaternion.Euler(euler) * Vector3.up;
                var c     = root + dir * (size.y * (0.5f - n.Sink)) + Vector3.back * (size.z * n.Bury);
                // **선은 옆에서만**(사용자: *"옆에서 볼때는 보이게 앞에서 볼때는 안보이게"*) — 제 선은 끄고, 둘째 재질이
                // **폰의 살 밖에서만** 긋는다. 앞에서 보면 코 둘레가 전부 얼굴 위라 선이 안 서고, 옆에서 보면 배경으로
                // 삐져나온 윤곽에만 선다(몸통 가로선을 없앤 것과 같은 장치 — `ToonOutlineOuter`).
                parts.Add(new Part($"Head.Nose.{n.Name}.{h.Name}", noses[n.Name], c, size, n.Mat, on: false,
                                   outerMat: "OutlineOuterFine", euler: euler));
            }
        }

        return parts;
    }

    // ── 귀(2026-09-19, 사용자: *"귀도 추가해"*) ────────────────────────────────
    //
    // **판이 아니라 입체다** — 눈·코·입은 얼굴 곡면에 붙인 그림이지만 귀는 **머리 옆으로 튀어나와야** 옆에서도
    // 귀로 읽히고 외곽선이 선다(살 재질이라 먹선이 저절로 둘린다). 모양은 옆선 하나를 돌려 깎고 **한 축을 눌러**
    // 납작하게 만든다(`Size.x` = 두께). 크기는 실제 m — 머리 폭 0.6m 언저리에 맞췄다.
    // 귀는 **셋**이다(사용자: *"그냥 일반 귀랑 고블린 귀나 엘프귀 처럼 긴 귀처럼 특이한 귀만"*) — 크기만 다른 둥근 귀
    // 여럿은 고를 이유가 없다. 긴 귀는 **밑동을 붙이고 끝을 기울인다**(`Out` 바깥 · `Back` 뒤, 도).
    readonly struct Ear
    {
        public readonly string Name;
        public readonly Vector3 Size;     // 두께 · 길이 · 너비(옆에서 본 폭)
        public readonly float  Lift;      // 눈 높이에서 얼마나 올리나(t)
        public readonly float  Out, Back; // 기울기(도)
        public readonly float  Sink;      // 밑동이 머리 속으로 들어가는 길이(m)
        public readonly Func<float, float> Profile;
        public Ear(string name, Vector3 size, float lift, float outDeg, float backDeg, float sink, Func<float, float> profile)
        { Name = name; Size = size; Lift = lift; Out = outDeg; Back = backDeg; Sink = sink; Profile = profile; }
    }

    static float Oval(float t)   => Mathf.Sqrt(Mathf.Max(0f, t * (1f - t)));
    // 아래는 둥글고 위로 갈수록 가늘어져 끝이 선다.
    // `Max(0, …)` — `Sin(π)`은 부동소수로 −8.7e-8이라 **음수의 0.8제곱 = NaN**이 됐다(2026-09-19). 화면에서는 귀 끝 고리가
    // 조용히 안 그려질 뿐이라 몰랐고, FBX 내보내기가 `(NaN, 0.50, NaN)`에서 넘어져서야 드러났다.
    static float Blade(float t)  => Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t))), 0.8f) * (1f - 0.8f * t);

    static readonly Ear[] Ears =
    {
        new Ear("Human",  new Vector3(0.07f, 0.20f, 0.15f), 0.00f,  0f,  0f, 0.10f, Oval),
        new Ear("Elf",    new Vector3(0.05f, 0.38f, 0.13f), 0.04f, 35f, 25f, 0.06f, Blade),   // 위·뒤로 솟는다
        new Ear("Goblin", new Vector3(0.06f, 0.44f, 0.22f), 0.02f, 78f, 12f, 0.06f, Blade),   // 옆으로 뻗는다
    };

    // ── 코 — 입체(2026-09-19, 사용자: *"코는 텍스쳐가 아니라 모델링으로"*) ─────────────────────
    //
    // 처음엔 눈·입처럼 얼굴에 붙인 **먹선 그림**이었는데, 치비 얼굴에서 코는 **튀어나온 것**이라 옆에서 보면
    // 없는 것과 같았다. 옆선 하나를 돌려 깎아 **앞으로 눕힌다**(`Droop` — 끝을 조금 떨군다). 살 재질이라 먹선이
    // 둘리고 그림자도 진다. **기본은 없음**(있던 폰의 얼굴이 조용히 바뀌지 않게) — 폰마다 `PawnLook`에서 고른다.
    //
    // **둥근 코는 뺐다**(사용자: *"버튼코 같이 둥그런 코는 필요없어"*). 대신 셋을 더했다 — **콧대가 있는 실제 코**,
    // **애니의 조그만 코**, **동물 코**(*"콧대가 있는 실제 코모양이랑 … 애니에 나오는 좀 조그만 코 … 동물 코같은 것도"*).
    readonly struct Nose
    {
        public readonly string Name;
        public readonly Vector3 Size;     // 폭 · 길이(축 방향) · 두께
        public readonly float  Droop;     // 끝을 아래로(도) — 0 = 곧장 앞 · 65쯤이면 얼굴을 따라 내려오는 콧대
        public readonly float  Lift;      // 밑동을 코 높이에서 얼마나 올리나(t) — 콧대는 눈 사이에서 시작한다
        public readonly float  Sink;      // 축 방향으로 얼굴 속에 묻히는 몫(0~0.5)
        public readonly float  Bury;      // 뒤로 밀어 넣는 몫(두께 대비) — 콧대는 반쯤 묻혀 **등성이만** 솟는다
        public readonly string Mat;
        public readonly Func<float, float> Profile;
        public Nose(string name, Vector3 size, float droop, float lift, float sink, float bury, string mat, Func<float, float> profile)
        { Name = name; Size = size; Droop = droop; Lift = lift; Sink = sink; Bury = bury; Mat = mat; Profile = profile; }
    }

    // 원뿔 — 밑이 넓고 끝이 둥글게 좁아진다.
    static float Cone(float t) => Mathf.Sqrt(Mathf.Max(0f, t)) * (1f - 0.85f * t);
    // 콧대 — 밑동(눈 사이)은 가늘고 코끝으로 갈수록 도톰해지다 **둥글게 닫힌다.**
    // 애니 코 — 밑에서 부풀었다가 **끝이 뾰족하게** 닫힌다(둥글게 닫히면 덩이로 읽힌다).
    static float Tip(float t) => Mathf.Pow(Mathf.Max(0f, t), 0.6f) * Mathf.Pow(Mathf.Max(0f, 1f - t), 0.9f);
    static float Bridge(float t) => (0.35f + 0.65f * t) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(t, 8f)));

    static readonly Nose[] Noses =
    {
        //                          폭      길이    두께      떨굼  올림    묻힘   뒤로   재질
        // 콧대 있는 코 — **옆모습을 파이썬으로 그려 맞췄다**: 처음 값(길이 0.17 · 떨굼 62)은 눈 위에서 시작해 코끝이 6cm나
        // 튀어나온 곤봉이었고, 길이 0.14는 코끝이 입 아래로 내려갔다. 눈 바로 아래에서 시작해 눈과 입 사이 절반에서 끝난다.
        new Nose("Real",   new Vector3(0.042f, 0.100f, 0.048f), 70f, 0.065f, 0.05f, 0.35f, "SkinFine", Bridge),
        // 애니 코 — **끝이 뾰족한 작은 쐐기**(`Tip`). 둥근 원뿔로는 *"너무 못만든"* 갈고리 덩이였다(사용자). 옆모습을
        // 셋 그려 보고 고른 값 — 얼굴에서 아래·앞으로 살짝 솟아 끝이 선다.
        new Nose("Anime",  new Vector3(0.022f, 0.050f, 0.022f), 62f, 0.02f, 0.05f, 0.30f, "SkinFine", Tip),
        new Nose("Pointy", new Vector3(0.050f, 0.070f, 0.050f), 15f, 0.00f, 0.25f, 0.00f, "SkinFine", Cone),    // 뾰족한 코(덜 둥글게)
        new Nose("Long",   new Vector3(0.060f, 0.160f, 0.060f), 20f, 0.00f, 0.25f, 0.00f, "SkinFine", Cone),    // 긴 코
        new Nose("Animal", new Vector3(0.095f, 0.050f, 0.065f),  8f, 0.00f, 0.30f, 0.00f, "NoseDark", Oval),    // 동물 코
    };

    static readonly string[] HairFits = { PawnLook.FitRound, PawnLook.FitBox };

    // ── 굽기 ────────────────────────────────────────────────────────────────

    [MenuItem("DungeonColony/Pawn/3. Create Chibi Pawns (체형 · 머리형 · 머리카락 · 눈 · 입)")]
    public static void Create()
    {
        var toon  = Shader.Find(ToonShader);
        var face  = Shader.Find(FaceShader);
        var outer = Shader.Find(OuterShader);
        if (toon == null || face == null || outer == null)
        { Debug.LogError($"[치비] 셰이더를 못 찾았다 — {ToonShader} / {FaceShader} / {OuterShader}"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Source) == null) { Debug.LogError($"[치비] 바탕 폰이 없다 — {Source}"); return; }

        // **아틀라스를 늘 다시 굽는다** — 옛 아틀라스를 쓰면 *"넣었는데 안 보인다"*가 된다.
        var atlases = BuildFaceAtlases();
        var mats = EnsureMaterials(toon, face, outer, atlases);

        _saved.Clear();
        Heads = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        var heads    = new Dictionary<string, Mesh>();
        var features = new Dictionary<(string, string), Mesh>();
        foreach (var head in Heads)
        {
            var h = head;
            Func<float, float> profile = t => HeadRadius(t, h);

            // 고리 64 — 표가 49점이라 그보다 성기면 턱 끝의 반 픽셀 닫힘을 놓친다. 양 끝을 촘촘히.
            heads[h.Name] = Shear(Lathe($"Chibi_Head_{h.Name}", profile, 64, 48, out _, normalize: false,
                                        densePole: 2), h.Forward);

            foreach (var f in Features)
                features[(h.Name, f.Name)] = BuildFeature(h, f, atlases[f.Kind].Cells);
        }

        // 머리카락 — 스타일마다 **한 장**(정수리는 빙 둘러 덮고 이마 아래에서만 앞을 비운다). 윗머리 모양마다
        // 한 벌이고 곡면은 **그 모양의 머리들 중 가장 넓은 것**을 따른다(좁은 것으로 깎으면 넓은 머리에서 살 속에 묻힌다).
        var hairs = new Dictionary<string, Mesh>();
        foreach (var fit in HairFits)
        {
            var group = new List<HeadShape>();
            foreach (var hh in Heads) if (PawnLook.HairFitOf(hh.Name) == fit) group.Add(hh);
            float forward = 0f;
            foreach (var hh in group) forward += hh.Forward / group.Count;

            Func<float, float> hairR = t =>
            {
                float r = 0f;
                foreach (var hh in group) r = Mathf.Max(r, HeadRadius(t, hh));
                return r;
            };
            hairs[$"{fit}.Short"] = Shear(HairShell($"Chibi_Hair_{fit}_Short", hairR, 0.44f, 150f), forward);
            hairs[$"{fit}.Bob"]   = Shear(HairShell($"Chibi_Hair_{fit}_Bob",   hairR, 0.26f, 125f), forward);
            hairs[$"{fit}.Long"]  = Shear(HairShell($"Chibi_Hair_{fit}_Long",  hairR, 0.10f, 110f), forward);
        }
        var ears = new Dictionary<string, Mesh>();
        foreach (var e in Ears)
            ears[e.Name] = Lathe($"Chibi_Ear_{e.Name}", e.Profile, 16, 16, out _, densePole: 2);
        var noses = new Dictionary<string, Mesh>();
        foreach (var n in Noses)
            noses[n.Name] = Lathe($"Chibi_Nose_{n.Name}", n.Profile, 12, 16, out _, densePole: 2);

        // **코 그림(아틀라스)은 걷어냈다** — 한 번 구웠으면 남은 에셋을 치운다(안 치우면 무엇이 쓰이는지 모른다).
        foreach (var stale in new[] { $"{MatDir}/Chibi_NosesAtlas.asset", $"{MatDir}/Chibi_NosesAtlas.png", $"{MatDir}/Chibi_FaceNoses.mat" })
            if (AssetDatabase.LoadMainAssetAtPath(stale) != null) AssetDatabase.DeleteAsset(stale);

        var headParts = new Dictionary<string, List<Part>>();
        foreach (var g in Genders) headParts[g] = HeadParts(g, heads, features, hairs, ears, noses);

        var made   = new List<string>();
        var bodies = new List<string>();   // 방랑자가 태어날 수 있는 몸들(`BodyVariants`)
        foreach (var shape in Shapes)
        {
            var s = shape;
            string target = $"{OutDir}/Chibi_{s.Suffix}.prefab";

            // **guid를 지킨다 — 지우고 다시 만들지 않는다.** 방랑자의 몸 목록(`BodyVariants`)이 이 여섯을 guid로
            // 가리키는데, 지웠다 복사하면 돌릴 때마다 새 guid라 **목록이 조용히 빈다**(CLAUDE.md "`.meta`를 짝지어").
            // 이미 있으면 파일 내용만 원본으로 덮고 `.meta`는 그대로 둔다.
            if (File.Exists(target))
            {
                File.Copy(Source, target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                AssetDatabase.CopyAsset(Source, target);
                AssetDatabase.ImportAsset(target);
            }

            if (Dress(target, s, headParts, mats, atlases, isSource: false))
            {
                made.Add($"{s.Label}(Chibi_{s.Suffix})");
                bodies.Add(target);
            }

            // **방랑자(내 폰)도 치비 몸이다**(2026-09-20, 사용자: *"원래 폰은 없애버려"* → *"내 폰(방랑자)만 치비로"*).
            // 새 프리팹으로 갈아 끼우지 않고 **제자리에서 살만 바꾼다** — `Wanderer_Human`은 합류 이벤트·바이옴이 guid로
            // 가리키고 씬에도 놓여 있어서, 지우거나 새로 만들면 그 참조가 조용히 끊겨 **아무도 안 온다.** 루트와 컴포넌트는
            // 그대로 두고 `Model` 자식만 갈므로 guid·루트 fileID가 지켜진다(CLAUDE.md "프리팹을 통째로 다시 짓지 않는다").
            // 체형은 남성 하나다 — 한 프리팹은 한 몸이다. 이 파일이 **치비 여섯의 원본**(`Source`)이기도 하지만 복사한 뒤
            // `Model`을 비우고 다시 굽으므로 원본이 치비여도 결과는 같다.
            if (s.Suffix == WandererShape && Dress(Source, s, headParts, mats, atlases, isSource: true))
                made.Add($"방랑자(Wanderer_Human = {s.Label})");
        }

        // **고블린도 치비 몸이다 — 초록 피부로**(2026-09-29, 사용자: *"고블린도 지금 폰에 맞춰서 바꿔줘 초록색 피부로"*).
        // 방랑자와 같은 길이다 — **제자리에서 `Model`만 간다**(소굴 무리·로드아웃·씬이 guid로 가리킨다). `Model`의 크기(0.8~0.85)는
        // 그대로 남아 사람보다 작게 선다. 재질만 고블린 벌(`GoblinMaterials`)을 쓰고, 얼굴은 민머리 · 고블린 귀 · 긴 코로 못 박는다.
        {
            var gShape = Array.Find(Shapes, x => x.Suffix == GoblinShape);
            var gMats  = GoblinMaterials(mats);
            foreach (var gp in GoblinPrefabs)
            {
                if (!File.Exists(gp)) { Debug.LogWarning($"[치비] 고블린 프리팹이 없다 — {gp}"); continue; }
                if (Dress(gp, gShape, headParts, gMats, atlases, isSource: false, GoblinLook))
                    made.Add($"고블린({Path.GetFileNameWithoutExtension(gp)})");
            }
        }

        // **방랑자가 태어날 몸 목록** — 이벤트·바이옴·모집은 `Wanderer_Human`만 가리키고, 세울 때 `PawnSpawn`이
        // 성별에 맞는 몸을 여기서 고른다. 원본 자신의 몸(남성)은 씬에 직접 놓을 때만 쓰인다.
        {
            var root = PrefabUtility.LoadPrefabContents(Source);
            try
            {
                if (!root.TryGetComponent(out BodyVariants bv)) bv = root.AddComponent<BodyVariants>();
                bv.Variants.Clear();
                foreach (var path in bodies)
                {
                    var body = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var pb   = body != null ? body.GetComponent<PawnBody>() : null;
                    if (pb != null) bv.Variants.Add(pb);
                    else Debug.LogWarning($"[치비] {path}에 `PawnBody`가 없다 — 몸 목록에서 빠졌다.");
                }
                PrefabUtility.SaveAsPrefabAsset(root, Source);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // **이번에 안 만든 옛 메시를 치운다** — 머리형 이름이 바뀌면(Base → Male_AverageNormal) 옛 것이
        // 폴더에 쌓여 무엇이 쓰이는지 알 수 없게 된다. 이 폴더의 `Chibi_` 메시는 이 도구만 만든다.
        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!_saved.Contains(path) && Path.GetFileName(path).StartsWith("Chibi_")) AssetDatabase.DeleteAsset(path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[치비] 체형 {made.Count}벌 — {string.Join(" · ", made)} · 머리형 {Heads.Length}(성별마다 {Heads.Length / Genders.Length}) · 머리카락 6 · " +
                  $"눈 {atlases["Eyes"].Names.Length} · 입 {atlases["Mouths"].Names.Length}. " +
                  "관절까지 섰다(몸은 스킨 메시 하나 — `Pawn/2`는 필요 없다).");
    }

    // 방랑자에게 입힐 체형(`Shapes`의 `Suffix`).
    const string WandererShape = "Male";

    // ── 고블린 (2026-09-29) ─────────────────────────────────────────────────
    //
    // 체형은 **마른 남성** — 작고 가늘다(키는 프리팹 `Model`의 0.8~0.85가 준다). 적 셋과 내 하수인 하나가 같은 몸이다.
    const string GoblinShape = "ThinMale";
    static readonly string[] GoblinPrefabs =
    {
        $"{OutDir}/Enemy_Goblin.prefab",
        $"{OutDir}/Enemy_GoblinArcher.prefab",
        $"{OutDir}/Enemy_GoblinSpear.prefab",
        $"{OutDir}/Minion_Goblin.prefab",
    };

    static readonly Color GoblinSkin = new Color32(0x86, 0xA8, 0x5A, 0xFF);   // 초록 살
    static readonly Color GoblinRag  = new Color32(0x6E, 0x56, 0x38, 0xFF);   // 해진 갈색 천
    static readonly Color GoblinEye  = new Color32(0xD8, 0xB0, 0x30, 0xFF);   // 누런 눈

    // **살 재질은 모든 치비가 같이 쓴다** — 그대로 칠하면 방랑자까지 초록이 된다. 그래서 고블린 벌을 **사본으로** 따로 든다
    // (`Chibi_Skin_Goblin` …). 사본은 원본의 값을 매번 다시 받아(외곽선 · 스텐실 · 음영) 원본을 고치면 따라오고,
    // 색만 고블린 것이다. **눈·입 판도 살색을 든다**(`_SkinColor` — 판의 바탕이 살과 섞인다) — 안 바꾸면 초록 얼굴에
    // 살색 눈두덩이 뜬다.
    static Dictionary<string, Material> GoblinMaterials(Dictionary<string, Material> baseMats)
    {
        var map = new Dictionary<string, Material>(baseMats);
        foreach (var n in new[] { "Skin", "HeadSkin", "SkinFine", "SkinOuter" })
            map[n] = Variant(baseMats[n], $"Chibi_{n}_Goblin", "_BaseColor", GoblinSkin);
        map["Shirt"] = Variant(baseMats["Shirt"], "Chibi_Shirt_Goblin", "_BaseColor", GoblinRag);

        var eyes = Variant(baseMats["FaceEyes"], "Chibi_FaceEyes_Goblin", "_SkinColor", GoblinSkin);
        eyes.SetColor("_AccentColor", GoblinEye);
        map["FaceEyes"]   = eyes;
        map["FaceMouths"] = Variant(baseMats["FaceMouths"], "Chibi_FaceMouths_Goblin", "_SkinColor", GoblinSkin);
        return map;
    }

    static Material Variant(Material src, string name, string colorProp, Color color)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(src) { name = name }; AssetDatabase.CreateAsset(m, path); }
        else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        m.renderQueue = src.renderQueue;
        m.SetColor(colorProp, color);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 얼굴을 못 박는다 — 민머리 · 고블린 귀 · 긴 코. 머리형은 씨앗이 고른다(고블린마다 턱이 다르다).
    static void GoblinLook(GameObject root)
    {
        var look = root.GetComponent<PawnLook>();
        if (look == null) return;
        var so = new SerializedObject(look);
        so.FindProperty("hairOverride").stringValue = PawnLook.Bald;
        so.FindProperty("earOverride").stringValue  = "Goblin";
        so.FindProperty("noseOverride").stringValue = "Long";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 프리팹 하나의 `Model` 자식을 치비 살로 갈아 끼우고 치비 컴포넌트를 붙인다. **루트는 안 건드린다.**
    // `finish` — 저장 직전에 한 번 더 손볼 것(고블린의 얼굴처럼).
    //
    // **몸은 한 덩어리다**(2026-10-09 — 시제품에서 옮겨 왔다, `ChibiPawnCreator.Proto`의 `DressSkinned`). 관절까지 여기서
    // 세우므로 `Pawn/2`가 따로 필요 없다(그 도구는 스킨 메시 몸을 건너뛴다).
    static bool Dress(string path, Shape s, Dictionary<string, List<Part>> headParts,
                      Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases, bool isSource,
                      Action<GameObject> finish = null)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (!DressSkinned(root, DimsOf(s), headParts[s.Gender], mats, atlases,
                              Path.GetFileNameWithoutExtension(path), $"Chibi_Body_{s.Suffix}")) return false;

            if (root.GetComponent<PawnLook>() == null) root.AddComponent<PawnLook>();
            // 몸통 선이 **제 몸만** 피하게 폰마다 번호를 준다(`ToonOutlineOuter`).
            if (root.GetComponent<PawnOutlineId>() == null) root.AddComponent<PawnOutlineId>();

            // **이 몸이 무엇인가** — 성별은 몸이 답한다(`PawnBody`). 머리형도 이 성별의 것만 구웠다.
            if (!root.TryGetComponent(out PawnBody pb)) pb = root.AddComponent<PawnBody>();
            pb.Gender     = s.Gender == "Female" ? Gender.Female : Gender.Male;
            pb.ShapeLabel = s.Label;

            // **몸 목록은 원본(방랑자)만 든다** — 복사본은 원본을 복사해 만들어 목록까지 따라오는데, 남겨 두면 체형
            // 프리팹을 직접 세울 때 **다른 몸으로 바꿔치기**된다.
            if (!isSource)
            {
                var bv = root.GetComponent<BodyVariants>();
                if (bv != null) UnityEngine.Object.DestroyImmediate(bv);
            }

            finish?.Invoke(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // **체형 → 몸의 치수 — 남성 체형이 곧 시제품 몸이고, 다른 체형은 남성에 대한 비만 가져온다.**
    //
    // 처음엔 옛 옆선 표(`Shape.Half`)를 새 몸통 높이에 맞춰 줄여 그대로 썼다. 그랬더니 남성 몸이 시제품보다 **엉덩이가 20% 넘게
    // 좁고 다리도 그만큼 가늘었다**(2026-10-09, 사용자: *"몸이 프로토에비해 너무 마른 거 아냐?"*) — 옛 몸은 다리가 없이 몸통이
    // 땅으로 모이는 모양이라 밑·엉덩이가 좁게 적혀 있었다. 사용자가 보고 고른 것은 시제품이므로 **그것이 기준**이고, 옛 표는
    // 체형끼리의 차이(여자 엉덩이 · 뚱뚱한 배 · 헐크 어깨)만 준다 — 자리마다 `체형 ÷ 남성`을 시제품 값에 곱한다.
    // - 밑은 엉덩이 비를 따른다(시제품 밑은 바지통이라 엉덩이와 같이 간다 — 옛 밑은 모여 있던 자리라 비로 쓸 수 없다).
    // - 다리 자리·굵기는 엉덩이 폭에서, 팔 굵기는 손에서 — 시제품의 비 그대로(뚱뚱한 몸은 다리도 굵다).
    // 머리 높이는 그 성별의 기준 머리 — 한 프리팹에 머리형 여섯이 다 구워지고 `PawnLook`이 하나를 켜므로 몸은 기준에 맞춘다.
    static BodyDims DimsOf(Shape s)
    {
        var male = Array.Find(Shapes, x => x.Suffix == WandererShape);
        float hh = BasisOf(s.Gender).Size.y;
        var half = new float[s.Half.Length];
        for (int i = 0; i < half.Length; i++)
        {
            int at = i == 0 ? 1 : i;   // 밑은 엉덩이 비
            half[i] = PTorsoHalf[i] * hh * (s.Half[at] / male.Half[at]);
        }
        float hand  = PHand * hh * (s.HandR / male.HandR);
        float depth = PDepth * (s.Depth / male.Depth);
        float hip   = half[1];
        return new BodyDims(hh, half, depth, hand, hand * (PArmW / PHand) * 0.5f,
                            hip * (PLegX / PTorsoHalf[1]), hip * (PLegW * 0.5f / PTorsoHalf[1]));
    }

    static void Build(Transform model, Part p, Dictionary<string, Material> mats,
                      Dictionary<string, ChibiFaceAtlas> atlases, int layer)
    {
        GameObject go;
        if (p.Mesh != null)
        {
            go = new GameObject(p.Name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = p.Mesh;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = p.Name;
            // **콜라이더는 뗀다** — 살에 남으면 커서 레이캐스트가 살을 짚어 폰을 못 고른다.
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        go.layer = layer;
        go.transform.SetParent(model, false);
        go.transform.localPosition = p.Center;
        go.transform.localScale    = p.Size;
        go.transform.localRotation = Quaternion.Euler(p.Euler);

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterials   = p.OuterMat != null ? new[] { mats[p.Mat], mats[p.OuterMat] } : new[] { mats[p.Mat] };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.enabled = p.On;   // **끄되 지우지 않는다**

        if (p.Cell != null)
        {
            var part = go.AddComponent<ChibiFacePart>();
            part.Atlas       = atlases[p.Kind];
            part.Cell        = p.Cell;
            part.DefaultCell = p.Cell;
            part.Apply();
        }
    }

    // ── 머리카락 한 장 ──────────────────────────────────────────────────────
    //
    // **정수리는 빙 둘러 덮고, 이마 선(`HairLine`) 아래에서만 앞을 비운다 — 한 메시로.**
    // 앞을 통째로 비웠더니 이마부터 벗겨졌고(반대머리), 그래서 뚜껑과 옆머리를 **두 조각**으로 나눴더니
    // 조각마다 제 외곽선을 그려 **이마를 가로지르는 검은 줄**이 섰다(2026-09-18 사용자 화면 — 눈썹처럼
    // 보였다). 외곽선은 메시의 테두리마다 서므로 **보여서는 안 되는 이음매는 메시를 가르지 않는 것**이
    // 답이다. 이제 테두리는 얼굴 둘레와 밑단뿐이고, 거기 서는 선은 림월드 그림에도 있는 선이다.
    //
    // 고리는 **이마 선에 정확히 하나**를 둔다(아니면 비운 자리의 윗변이 계단이 된다) · 비우는 칸은
    // **각도 칸 단위**로 고른다(칸 경계와 어긋나면 비운 가장자리가 톱니가 된다).
    static Mesh HairShell(string name, Func<float, float> radiusAt, float tFrom, float openDeg)
    {
        const int below = 14, above = 18, segs = 48;
        var ts = new List<float>();
        for (int i = 0; i <= below; i++) ts.Add(Mathf.Lerp(tFrom, HairLine, i / (float)below));
        for (int i = 1; i <= above; i++) ts.Add(Mathf.Lerp(HairLine, 1f, Mathf.Sin(i / (float)above * Mathf.PI * 0.5f)));
        int rings = ts.Count - 1;

        // 앞(방위 90°)을 가운데로 비운다 — 칸 0이 비운 자리의 오른쪽 끝에서 시작하게 돌린다.
        int openSegs = Mathf.Clamp(Mathf.RoundToInt(openDeg / 360f * segs), 0, segs - 2);
        float start = 90f + openSegs * (360f / segs) * 0.5f;

        var verts = new List<Vector3>();
        var uvs   = new List<Vector2>();
        var tris  = new List<int>();
        for (int i = 0; i <= rings; i++)
        {
            float r = radiusAt(ts[i]), y = ts[i] - 0.5f;
            for (int k = 0; k <= segs; k++)
            {
                float a = (start + 360f * k / segs) * Mathf.Deg2Rad;
                verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(k / (float)segs, i / (float)rings));
            }
        }
        int stride = segs + 1;
        for (int i = 0; i < rings; i++)
        {
            bool open = ts[i] < HairLine - 1e-4f;                      // 이마 선 아래 고리 띠
            for (int k = 0; k < segs; k++)
            {
                if (open && k >= segs - openSegs) continue;            // 앞을 비운 칸
                int a = i * stride + k, b = a + 1, c = a + stride, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        // **정수리를 닫는다**(2026-10-09, 사용자: *"어떤 것들은 정수리에 원형탈모가 생겨"*). 고리는 t = 1까지 쌓지만 그 고리의
        // 반지름은 머리 옆선이 정한다 — 정수리가 평평한 머리형(그 모양 무리의 가장 넓은 곡면)은 t = 1에서도 0이 아니라
        // **그만한 구멍이 뚫려** 안의 머리 살이 비쳤다. 가운데 점 하나로 부채를 덮는다(감김은 위 고리 띠와 같다 — 외곽선 껍데기가
        // 법선을 따른다).
        {
            int top = rings * stride, center = verts.Count;
            verts.Add(new Vector3(0f, ts[rings] - 0.5f, 0f));
            uvs.Add(new Vector2(0.5f, 1f));
            for (int k = 0; k < segs; k++) { tris.Add(top + k); tris.Add(center); tris.Add(top + k + 1); }
        }
        return Save(name, verts, uvs, tris);
    }

    // ── 이목구비 판 굽기 ────────────────────────────────────────────────────
    //
    // **이 머리의 곡면을 그대로 따라** 깎고, 같은 몫만큼 앞으로 민다. 크기는 실제 m로 정하고 각도·t로
    // 되돌린다. 눈은 **가로가 조금 길다**(`EyeAspect`) — 림월드 눈이 가로 타원이고, 동그란 판을 둥근
    // 머리 옆에 붙이면 옆으로 눌려 **세로로 길쭉해 보인다**(2026-09-18 사용자 화면: *"눈이 너무 크잖아"*).
    static Mesh BuildFeature(HeadShape h, Feature f, int cells)
    {
        Func<float, float> profile = t => HeadRadius(t, h);
        float tc    = f.Slot == FaceSlot.Mouth ? h.MouthT : h.EyeT;
        float side  = h.EyeSize * (f.Slot == FaceSlot.Mouth ? MouthScale : 1f);
        float asp   = f.Slot == FaceSlot.Eye ? Mathf.Max(0.3f, h.EyeAspect) : 1f;
        float sizeW = side * Mathf.Sqrt(asp), sizeH = side / Mathf.Sqrt(asp);   // 넓이는 그대로 두고 비율만
        float rN    = Mathf.Max(0.05f, HeadRadius(tc, h));            // 그 높이의 반폭(정규화)
        float rM    = rN * h.Size.x;                                  // 실제 반지름
        float wDeg  = sizeW / rM * Mathf.Rad2Deg;
        float hT    = sizeH / h.Size.y;
        // 눈 가운데의 방위 — 가로 자리(폭의 몫)를 그 높이의 반폭으로 되돌린다.
        float az    = f.Side == 0 ? 0f : f.Side * Mathf.Asin(Mathf.Clamp(h.EyeDx / rN, -0.95f, 0.95f)) * Mathf.Rad2Deg;
        var mesh = Lathe($"Chibi_Face_{h.Name}_{f.Name}", profile, 8, 10, out _, normalize: false,
                         tFrom: tc - hT * 0.5f, tTo: tc + hT * 0.5f,
                         startDeg: 90f + az - wDeg * 0.5f, sweepDeg: wDeg, lift: f.Lift, cap: false);
        return ToCell(Shear(mesh, h.Forward), cells, f.Mirror);
    }

    // **눈·입만 다시 굽는다** — `ChibiFaceLayout`을 고치는 즉시 부른다(사용자: *"설정해도 안바껴"* —
    // [다시 굽기]를 따로 눌러야 먹던 것이 그 말이었다). 메시 에셋을 **제자리에서** 갈아 끼우므로
    // 프리팹도 씬에 놓인 폰도 손대지 않고 바로 바뀐다. 체형·머리·리그는 안 건드린다.
    static bool _faceQueued;
    public static void RequestFaceRebake()
    {
        if (_faceQueued) return;
        _faceQueued = true;
        EditorApplication.delayCall += () => { _faceQueued = false; RebakeFaces(); };
    }

    public static void RebakeFaces()
    {
        // 아틀라스는 이목구비 종류마다(`Feature.Kind`) — 하나라도 없으면 먼저 굽기를 돌려야 한다.
        var atlases = new Dictionary<string, ChibiFaceAtlas>();
        foreach (var f in Features)
        {
            if (atlases.ContainsKey(f.Kind)) continue;
            var a = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>($"{MatDir}/Chibi_{f.Kind}Atlas.asset");
            if (a == null) { Debug.LogWarning($"[치비] {f.Kind} 아틀라스가 없다 — `Pawn/3. Create Chibi Pawns`를 먼저 돌릴 것."); return; }
            atlases[f.Kind] = a;
        }

        var layout = AssetDatabase.LoadAssetAtPath<ChibiFaceLayout>(ChibiFaceLayout.Path);
        Heads = MakeHeads(layout);
        foreach (var h in Heads)
        foreach (var f in Features)
            BuildFeature(h, f, atlases[f.Kind].Cells);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    // ── 아틀라스 — 눈 · 입 따로 ─────────────────────────────────────────────
    //
    // 칸은 두 벌에서 온다: **도구가 그리는 림월드식**(`ink_*` — 폴더가 비어도 늘 있다)과
    // **`PackDir/eyes/` · `PackDir/mouths/` 아래의 PNG**(넣고 빼면 저절로 다시 굽는다).
    //
    // 그림은 **채널 분할 마스크**로 바꾼다:
    //   B = 채도(색이 있는 자리 — 홍채·혀)  · R = 1 − 밝기(잉크)
    //   G = 밝기 × (1 − B)(흰자)            · A = 알파
    // 그림 속 색 자체는 버린다 — 눈 색은 재질이 칠한다. 칸은 정사각이고 그림은 **비율을 지켜 가운데에**.
    // 칸 이름은 **파일 이름**이다(번호로 부르면 그림이 늘 때 표정이 밀린다).
    [MenuItem("DungeonColony/Pawn/4. Build Face Atlases (눈 · 입)")]
    public static Dictionary<string, ChibiFaceAtlas> BuildFaceAtlases()
    {
        var result = new Dictionary<string, ChibiFaceAtlas>
        {
            ["Eyes"]   = BuildAtlas("Eyes",   "eyes",   RimEyes()),
            ["Mouths"] = BuildAtlas("Mouths", "mouths", RimMouths()),
        };

        // 열린 씬·프리팹의 판이 **새 칸 번호로** 다시 찾게 한다.
        foreach (var part in UnityEngine.Object.FindObjectsByType<ChibiFacePart>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            part.Apply();
        return result;
    }

    // 옛 이름을 부르는 곳(감시자)이 있어 남긴다.
    public static void BuildFaceAtlas() => BuildFaceAtlases();

    static ChibiFaceAtlas BuildAtlas(string kind, string folder, List<(string name, Func<float, float, Color32> draw)> builtins)
    {
        string png = $"{MatDir}/Chibi_{kind}Atlas.png";
        string so  = $"{MatDir}/Chibi_{kind}Atlas.asset";

        var files = new List<string>();
        string dir = $"{PackDir}/{folder}";
        if (Directory.Exists(dir))
            foreach (var f in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories)) files.Add(f.Replace('\\', '/'));
        files.Sort(StringComparer.Ordinal);

        int total = builtins.Count + files.Count;
        int cells = Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(total + 1)));
        int size  = cells * CellPx;
        var px    = new Color32[size * size];   // 전부 투명 — 마지막 칸은 빈 칸
        var names = new List<string>();
        int inner = CellPx - 2 * CellPad;

        void Cell(int i, out int x0, out int y0)
        {
            int col = i % cells, rowFromTop = i / cells;
            x0 = col * CellPx + CellPad;
            y0 = (cells - 1 - rowFromTop) * CellPx + CellPad;
        }

        foreach (var (name, draw) in builtins)
        {
            Cell(names.Count, out int x0, out int y0);
            for (int y = 0; y < inner; y++)
            for (int x = 0; x < inner; x++)
                px[(y0 + y) * size + (x0 + x)] = draw((x + 0.5f) / inner, (y + 0.5f) / inner);
            names.Add(name);
        }

        var src  = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var seen = new HashSet<string>(names);
        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!seen.Add(name))
            {
                Debug.LogWarning($"[치비] {kind} 그림 이름이 겹친다 — `{name}`({file}). 칸은 이름으로 찾으므로 건너뛴다.");
                continue;
            }

            src.LoadImage(File.ReadAllBytes(file));   // 가져오기 설정을 거치지 않는다 — 팩은 읽기 불가·압축일 수 있다
            float aspect = src.width / (float)Mathf.Max(1, src.height);
            float w = aspect >= 1f ? 1f : aspect, h = aspect >= 1f ? 1f / aspect : 1f;
            float ox = (1f - w) * 0.5f, oy = (1f - h) * 0.5f;

            Cell(names.Count, out int x0, out int y0);
            for (int y = 0; y < inner; y++)
            for (int x = 0; x < inner; x++)
            {
                float u = ((x + 0.5f) / inner - ox) / w, v = ((y + 0.5f) / inner - oy) / h;
                if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                var c = src.GetPixelBilinear(u, v);
                float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                float sat = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                float accent = Mathf.Clamp01((sat - 0.12f) * 3f);
                px[(y0 + y) * size + (x0 + x)] = new Color(1f - lum, lum * (1f - accent), accent, c.a);
            }
            names.Add(name);
        }
        UnityEngine.Object.DestroyImmediate(src);

        EnsureDir(MatDir);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, linear: true);
        tex.SetPixels32(px);
        File.WriteAllBytes(png, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);

        // **마스크는 색이 아니다** — sRGB를 끈다(감마를 먹이면 반반 경계가 한쪽으로 쏠린다).
        var imp = (TextureImporter)AssetImporter.GetAtPath(png);
        imp.sRGBTexture         = false;
        imp.alphaIsTransparency = false;
        imp.mipmapEnabled       = false;
        imp.wrapMode            = TextureWrapMode.Clamp;
        imp.filterMode          = FilterMode.Bilinear;
        imp.textureCompression  = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();

        var atlas = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>(so);
        bool fresh = atlas == null;
        if (fresh) atlas = ScriptableObject.CreateInstance<ChibiFaceAtlas>();
        atlas.Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        atlas.Cells   = cells;
        atlas.Names   = names.ToArray();
        if (fresh) AssetDatabase.CreateAsset(atlas, so); else EditorUtility.SetDirty(atlas);

        // 재질이 새 칸 수를 알아야 한다 — 칸 수가 바뀌면 모든 판이 엉뚱한 칸을 본다.
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Chibi_Face{kind}.mat");
        if (mat != null)
        {
            mat.SetTexture("_BaseMap", atlas.Texture);
            mat.SetFloat("_Cells", cells);
            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[치비] {kind} 아틀라스 — 림월드식 {builtins.Count} + 폴더 {files.Count}장({dir}) · {cells}×{cells}칸");
        return atlas;
    }

    // ── 림월드식 눈 — 도구가 그린다 ─────────────────────────────────────────
    //
    // **동그란 검은 점**이 기본이다(사용자: *"세로로 긴 눈 말고 아예 동그란 눈"*). 표정은 그 점의
    // 모양을 바꾸되 **크기를 점과 같게** 둔다 — 감은 눈·웃는 눈을 가는 선으로 두었더니 점 옆에서
    // *너무 작아* 보였다. 선 굵기는 점 지름의 **3분의 1**, 폭은 **점 지름만큼**이다.
    //
    // 모두 **보는 사람의 오른쪽 눈(= 캐릭터의 왼눈) 기준**이다 — 코 쪽이 그림의 **왼쪽**.
    // 반대쪽 눈은 판이 거울로 뒤집는다.
    const float EyeR    = 0.30f;           // 점 반지름(칸의 몫) — 판 0.20m에서 지름 0.12m = 머리 폭의 14%
    const float EyeLine = EyeR * 0.66f;    // 선 굵기(점 지름의 1/3)

    static List<(string name, Func<float, float, Color32> draw)> RimEyes() => new()
    {
        ("ink_eye",        (u, v) => Ink(Soft(Circle(u, v, 0.5f, 0.5f, EyeR)))),
        // 크게 뜬 눈 — 조금 크고 흰 반짝임.
        ("ink_eye_wide",   (u, v) =>
        {
            float body  = Soft(Circle(u, v, 0.5f, 0.5f, EyeR * 1.15f));
            float spark = Soft(Circle(u, v, 0.40f, 0.62f, EyeR * 0.28f));
            return new Color(1f - spark, spark, 0f, body);
        }),
        // 졸린 눈 — 위를 눈꺼풀이 덮은 반달(44번 사진의 그 눈).
        ("ink_eye_sleepy", (u, v) => Ink(Soft(Mathf.Max(Circle(u, v, 0.5f, 0.45f, EyeR), v - 0.50f)))),
        // 감은 눈 — 아래로 휜 굵은 호(∪).
        ("ink_eye_closed", (u, v) => Ink(Soft(Arc(u, v, 0.5f, 0.62f, EyeR, EyeLine, up: false)))),
        // 웃는 눈 — 위로 휜 굵은 호(∩).
        ("ink_eye_happy",  (u, v) => Ink(Soft(Arc(u, v, 0.5f, 0.38f, EyeR, EyeLine, up: true)))),
        // 화난 눈 — **코 쪽 위**가 비스듬히 깎인 점.
        ("ink_eye_angry",  (u, v) => Ink(Soft(Mathf.Max(Circle(u, v, 0.5f, 0.46f, EyeR),
                                                        (v - 0.58f) + (0.5f - u) * 0.8f)))),
        // 슬픈 눈 — **바깥쪽 위**가 깎인 점.
        ("ink_eye_sad",    (u, v) => Ink(Soft(Mathf.Max(Circle(u, v, 0.5f, 0.46f, EyeR),
                                                        (v - 0.58f) + (u - 0.5f) * 0.8f)))),
        // 놀란 눈 — 가운데가 빈 고리.
        ("ink_eye_shock",  (u, v) => Ink(Soft(Mathf.Abs(Circle(u, v, 0.5f, 0.5f, EyeR)) - EyeLine * 0.4f))),
    };

    // ── 림월드식 입 ─────────────────────────────────────────────────────────
    //
    // 입은 **눈보다 가늘다** — 같은 굵기면 얼굴에서 입이 눈보다 무거워 보인다.
    const float MouthW = 0.28f, MouthLine = 0.07f;

    static List<(string name, Func<float, float, Color32> draw)> RimMouths() => new()
    {
        ("ink_mouth_line",  (u, v) => Ink(Soft(Bar(u, v, 0.5f, 0.5f, MouthW, MouthLine * 0.5f)))),
        ("ink_mouth_smile", (u, v) => Ink(Soft(Arc(u, v, 0.5f, 0.62f, MouthW, MouthLine, up: false)))),
        ("ink_mouth_frown", (u, v) => Ink(Soft(Arc(u, v, 0.5f, 0.38f, MouthW, MouthLine, up: true)))),
        ("ink_mouth_o",     (u, v) => Ink(Soft(Mathf.Abs(Circle(u, v, 0.5f, 0.5f, 0.12f)) - MouthLine * 0.5f))),
        // 벌린 입 — 검은 반원에 혀(B).
        ("ink_mouth_open",  (u, v) =>
        {
            float body   = Soft(Mathf.Max(Circle(u, v, 0.5f, 0.56f, MouthW), v - 0.56f));
            float tongue = Soft(Circle(u, v, 0.5f, 0.36f, MouthW * 0.45f));
            return new Color(1f - tongue, 0f, tongue, body);
        }),
    };

    // ── 그리기 도구 — 부호 있는 거리(안쪽이 음수) ───────────────────────────
    // 선분 — (ax,ay)~(bx,by), 굵기 w, 끝은 둥글게.
    static float Segment(float u, float v, float ax, float ay, float bx, float by, float w)
    {
        float px = u - ax, py = v - ay, dx = bx - ax, dy = by - ay;
        float t  = Mathf.Clamp01((px * dx + py * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
        float ex = px - dx * t, ey = py - dy * t;
        return Mathf.Sqrt(ex * ex + ey * ey) - w * 0.5f;
    }
    const float Px = 1f / (CellPx - 2 * CellPad);
    static Color32 Ink(float cover) => new Color(1f, 0f, 0f, Mathf.Clamp01(cover));
    static float Soft(float d) => Mathf.Clamp01(0.5f - d / Px);   // 1px 부드럽게 — 딱 자르면 계단이 선다
    static float Circle(float u, float v, float cx, float cy, float r)
        => Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r;
    // 호 — 가운데 (cx, cy) 반지름 r, 굵기 w, 위쪽 반만(up) 또는 아래쪽 반만. 끝은 둥글게.
    static float Arc(float u, float v, float cx, float cy, float r, float w, bool up)
    {
        float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
        bool inHalf = up ? v >= cy : v <= cy;
        if (inHalf) return Mathf.Abs(d - r) - w * 0.5f;
        // 반원 밖이면 두 끝점의 둥근 마개로 잰다.
        float e1 = Mathf.Sqrt((u - (cx - r)) * (u - (cx - r)) + (v - cy) * (v - cy));
        float e2 = Mathf.Sqrt((u - (cx + r)) * (u - (cx + r)) + (v - cy) * (v - cy));
        return Mathf.Min(e1, e2) - w * 0.5f;
    }
    static float Bar(float u, float v, float cx, float cy, float hw, float hh)
    {
        // 끝이 둥근 막대.
        float dx = Mathf.Max(0f, Mathf.Abs(u - cx) - hw + hh);
        float dy = v - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - hh;
    }

    // ── 재질 ────────────────────────────────────────────────────────────────

    static Dictionary<string, Material> EnsureMaterials(Shader toon, Shader face, Shader outer,
                                                        Dictionary<string, ChibiFaceAtlas> atlases)
    {
        EnsureDir(MatDir);
        var map = new Dictionary<string, Material>();
        foreach (var (name, color) in Mats)
        {
            var mat = LoadOrMake($"{MatDir}/Chibi_{name}.mat", toon);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_ShadeColor", new Color(0.70f, 0.72f, 0.82f, 1f));
            mat.SetColor("_OutlineColor", OutlineColor);
            // **몸통(셔츠)은 제 선을 끈다** — 그 선은 두 번째 재질(`OutlineOuter`)이 몸 밖에만 긋는다.
            // **코(가는 선 재질)도 제 선을 끈다** — 선은 둘째 재질(`OutlineOuterFine`)이 몸 밖에만 긋는다.
            // **다리(`Pants`)도 몸통처럼** — 팔다리는 관절마다 두 토막이라 제 선을 두면 무릎·팔꿈치마다 검은 띠가 선다(시제품, 2026-10-06).
            // **신발·시제품 손도** — 제 선(스텐실을 안 보는 껍데기)이 발목·손목에 검은 띠를 긋고, 땅 이기기를 안 받아 발밑 선이 먹혔다(10-09).
            bool outerOnly = name == "Shirt" || name == "Pants" || name == "Shoe" || name == "SkinOuter" || FineOutline(name);
            mat.SetFloat("_OutlineWidth", outerOnly ? 0f : OutlineWidth);
            mat.SetFloat("_OutlineMin",   outerOnly ? 0f : OutlineMin);
            // 스텐실에 제 자리를 적는 것은 **몸통과 얼굴**뿐이다 — 번호는 폰마다 `PawnOutlineId`가 준다(여기 값은 씬 뷰용 기본값).
            // 얼굴(머리 살·코)은 번호에 **128을 얹어** 적는다: 코 선이 비킬 곳은 제 얼굴뿐이라 손·몸통과 갈라야 한다.
            // **손·팔·귀는 안 적는다** — 적으면 몸통 선이 제 손 위에서 사라진다(겹친 부위 사이에도 선이 서야 한다).
            bool isFace = name == "HeadSkin" || FineOutline(name);
            mat.SetFloat("_StencilFace",      isFace ? 1f : 0f);
            mat.SetFloat("_StencilRef",       isFace ? 1f + PawnOutlineId.FaceBit : 1f);
            mat.SetFloat("_StencilWriteMask", outerOnly || isFace ? 255f : 0f);
            // **머리카락만 양면이다** — 커튼은 앞을 비운 한 겹이라 트인 자리로 안쪽이 보인다.
            mat.SetFloat("_Cull", name == "Hair" ? 0f : 2f);
            EditorUtility.SetDirty(mat);
            map[name] = mat;
        }

        // 눈 재질 · 입 재질 — 같은 셰이더, **아틀라스가 따로**고 강조색이 다르다(눈 색 ↔ 혀 색).
        Material Face(string kind, Color accent)
        {
            var atlas = atlases[kind];
            var mat = LoadOrMake($"{MatDir}/Chibi_Face{kind}.mat", face);
            mat.SetTexture("_BaseMap", atlas.Texture);
            mat.SetFloat("_Cells", atlas.Cells);
            mat.SetColor("_AccentColor", accent);
            mat.SetColor("_SkinColor", Mats[0].color);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(mat);
            return mat;
        }
        var o = LoadOrMake($"{MatDir}/Chibi_OutlineOuter.mat", outer);
        o.SetColor("_OutlineColor", OutlineColor);
        o.SetFloat("_OutlineWidth", OutlineWidth);
        o.SetFloat("_OutlineMin",   OutlineMin);
        o.SetFloat("_StencilRef",   1f);
        // 살이 다 칠해진 뒤 · **발밑 선택 고리(반투명 3000)보다도 뒤** — 앞이면 고리가 선을 뚫는다.
        o.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;
        EditorUtility.SetDirty(o);
        map["OutlineOuter"] = o;

        // 코의 바깥 선 — **굵기는 머리와 같고 · 땅을 이기려 당기지 않는다**(코는 땅에 안 닿는다 · 당기면 얼굴 앞으로 떠 보인다).
        // 처음엔 1.2cm로 가늘게 뒀는데 머리 테두리(6cm) 옆에서 **코끝만 선이 얇아** 선이 없는 것처럼 읽혔다(2026-09-19, 사용자:
        // *"외곽선이 엄청 얇은데 머리랑 맞추는게 맞지"*). 굵게 해도 앞에서 검은 덩어리가 안 되는 것은 **얼굴 위에서는 스텐실이
        // 선을 지우기 때문**이다 — 코 제 재질에 6cm 선을 걸었을 때 덩어리가 된 것은 그 선이 스텐실을 안 보는 옛 패스였기 때문이다.
        var of = LoadOrMake($"{MatDir}/Chibi_OutlineOuterFine.mat", outer);
        of.SetColor("_OutlineColor", OutlineColor);
        of.SetFloat("_OutlineWidth", OutlineWidth);
        of.SetFloat("_OutlineMin",   OutlineMin);
        of.SetFloat("_OutlineGroundPush", 0f);
        // **"내 얼굴"만 비킨다** — 번호 + 128과 같은 자리에서만 안 긋고, 제 손·몸통(번호만 적힌 자리)과 다른 폰 위에서는 긋는다.
        of.SetFloat("_StencilFace",  1f);
        of.SetFloat("_StencilRef",   1f + PawnOutlineId.FaceBit);
        of.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;
        EditorUtility.SetDirty(of);
        map["OutlineOuterFine"] = of;

        map["FaceEyes"]   = Face("Eyes",   new Color32(0x5A, 0x3C, 0x28, 0xFF));
        map["FaceMouths"] = Face("Mouths", new Color32(0xC8, 0x5A, 0x5A, 0xFF));
        return map;
    }

    // 외곽선 값은 한 벌 — 살 재질과 바깥 선 재질이 같이 쓴다(갈리면 몸통 선만 굵기가 다르다).
    static readonly Color OutlineColor = new(0.06f, 0.05f, 0.06f, 1f);
    const float OutlineWidth = 0.06f, OutlineMin = 1f;   // 최소는 픽셀(`ToonOutlineHull`)

    static Material LoadOrMake(string path, Shader shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        return mat;
    }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf   = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
