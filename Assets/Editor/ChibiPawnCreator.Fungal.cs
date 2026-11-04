using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **감염자(기생림) — 사람 치비 몸 + 버섯**(2026-11-03~04, 사용자: *"따로 만들지 말고 치비 몸에서 수정해"* · *"더 죽은 느낌으로 어두운 버섯"* ·
// *"몸 자체가 버섯이 되버린 느낌"* · *"모든 기생버섯 몬스터들이 다 똑같은 위치에 버섯을 달고 있으면 이상"* · *"눈에 난 버섯처럼 특이한 버섯"*).
//
// 몸은 사람 남성 치비(`DressSkinned`) 그대로 · 색만 버섯 살(셔츠 · 바지 · 손 · 신발). 머리는 넓은 네모 머리(`Male_BroadSquare` — 드워프 · 오크와 같은
// 바탕)에 위아래로 벌린 입을 깎았다. 그 위에 얹는 것:
//   - **얼굴**(`Head.Pick.Face.{A|B|C}`) — 버섯이 눈을 덮는다. A 가면(두 눈 위 부채) · B 터짐(겹겹이 위 · 밖) · C 한쪽(한 눈만 · 다른 눈은 탁하다). 셋 다 쓴다.
//   - **머리 얼룩**(`Head.Pick.Rot.1~4`) — 얼굴 쪽 얼룩은 늘 있고 뒤통수 쪽이 벌마다 다르다 · **뒤통수 말굽 무더기**(`Head.Pick.Stack.*` 다섯 자리).
//   - **몸 말굽 버섯**(`{관절}.Pick.SiteNN.On` — 자리 열여덟이 저마다 켜고 끄는 축 · 켜질 몫 `FungalSiteChance`) — 죽은 나무에 붙듯 두툼한 반원이 위로 겹친다.
//   - **터진 틈**(`Spine.Pick.WoundN.On` — 다섯 자리) — 살이 입술처럼 젖혀지고 안에서 주름살이 비어져 나온다.
//   - **대표 버섯 하나**(`Special` 축) — 목도리 · 큰 버섯(좌 · 우) · 포자 주머니(좌 · 우) · 먹물 버섯 다발 · 입 버섯 · 동충하초.
// 고르는 것은 전부 `PawnLook`의 고르기 축이라 씨앗(`PawnSpec.Seed`) 하나에서 나온다 — 개체마다 버섯 자리가 다르다.
//
// 그림 확인은 파이썬 미리보기 `Tools/ModelLab/fungal/fungal.py`(v24) — 같은 수식(단위 m · 몸은 발밑 원점 · 머리는 머리 한복판 원점 · +z 앞).
// 미리보기의 +x가 여기 +x다(관절 이름은 `-x = L`). **수치를 고치면 그 그림과 어긋난다 — 같이 고칠 것.**
public static partial class ChibiPawnCreator
{
    const string FungalPreview = "Chibi_Fungal";   // 보기 몸 프리팹(`3_Prefab/Pawn/Fungal/`)
    const float FungalSiteChance = 0.47f;           // 몸 말굽 자리 하나가 켜질 몫 — 열여덟 중 평균 8.5(미리보기 7~10)
    const float FungalWoundChance = 0.30f;          // 틈 자리 하나가 켜질 몫 — 다섯 중 평균 1.5
    const float FungalStep = 0.006f, FungalFineStep = 0.0035f;
    // 버섯의 제 선(m) — 해골 뼈 사이 선(1.5cm)보다 가늘게. 2.5cm였을 때 작은 버섯 · 겹친 말굽마다 선이 겹쳐 반쯤 검었고 틈 둘레가 검은 톱니가 됐다(11-04 게임 화면).
    const float FungalLine = 0.012f;
    // 제 선 껍데기를 이만큼 뒤로(`ToonOutline._OutlineBack`) — 굽은 테 · 주름에서 뒤집힌 앞면이 검은 조각으로 박히던 것(11-04 게임 화면 확대).
    const float FungalLineBack = 0.02f;

    // 몸 — 사람 남성과 같은 치수의 제 줄(몸 메시 `Chibi_Body_Fungal`을 따로 굽는다 · 정점 색에 살 얼룩을 적으니 사람 몸과 같이 쓰면 안 된다).
    static readonly Shape FungalShape = new("Fungal", "감염자", "Male", new[] { 0.21f, 0.24f, 0.26f, 0.30f, 0.31f }, 0.82f, 0.30f);

    // 색 — 몸은 차가운 회녹색 시체빛, 버섯은 따뜻한 녹슨 갈색 · 마른 황갈색 · 멍든 자주(11-04, 사용자: *"옷색이 버섯색하고 비슷해서 티가 안 나"* — 살과 버섯이
    // 둘 다 어두운 갈색이라 명도 · 색상이 0.02 차이였다 → 색상으로 가르고 밝은 버섯은 명도로도). 미리보기 `SKIN` …과 같다(v25).
    static readonly Color FcSkin = new(0.40f, 0.42f, 0.38f), FcFlesh = new(0.34f, 0.37f, 0.34f), FcRag = new(0.16f, 0.15f, 0.17f), FcPants = new(0.68f, 0.59f, 0.45f), FcRoot = new(0.20f, 0.17f, 0.15f);
    static readonly Color FcFrill = new(0.38f, 0.24f, 0.16f), FcFrillB = new(0.48f, 0.38f, 0.27f), FcFrillIn = new(0.27f, 0.13f, 0.22f), FcGill = new(0.68f, 0.59f, 0.45f);
    static readonly Color FcRot = new(0.31f, 0.23f, 0.18f), FcCap = new(0.32f, 0.18f, 0.11f), FcStalk = new(0.58f, 0.55f, 0.46f), FcCord = new(0.13f, 0.11f, 0.10f);
    static readonly Color FcClub = new(0.56f, 0.40f, 0.22f), FcSpot = new(0.62f, 0.58f, 0.48f), FcPuff = new(0.52f, 0.49f, 0.42f), FcInk = new(0.06f, 0.05f, 0.06f);
    static readonly Color FcInkCap = new(0.40f, 0.38f, 0.35f), FcCloud = new(0.66f, 0.70f, 0.52f), FcMouth = new(0.12f, 0.03f, 0.04f), FcTooth = new(0.70f, 0.66f, 0.54f);

    // ── 도구 ────────────────────────────────────────────────────────────────

    static Vector3 Nrm(Vector3 v) => v.normalized;

    // 부채 버섯 한 장 — c 중심 · n 펼친 면의 법선 · R 반지름 · 큰 물결 k개 · gill이면 결(미리보기 `frill`).
    sealed class Frill
    {
        readonly Vector3 c, u, v, n; readonly float R, th, amp, cup, sq, gill, lobe; readonly int k;
        public Frill(Vector3 c, Vector3 n, float R, float th = 0.009f, int k = 4, float amp = 0.02f, float cup = 0.045f, float sq = 1f, float gill = 0f, float lobe = 0.15f)
        {
            this.n = n.normalized; var up = Mathf.Abs(this.n.y) > 0.95f ? Vector3.right : Vector3.up;
            u = Vector3.Cross(up, this.n).normalized; v = Vector3.Cross(this.n, u);
            this.c = c; this.R = R; this.th = th; this.k = k; this.amp = amp; this.cup = cup; this.sq = sq; this.gill = gill; this.lobe = lobe;
        }
        public float D(Vector3 p)
        {
            var q = p - c;
            float a = Vector3.Dot(q, u), b = Vector3.Dot(q, v) / sq, w = Vector3.Dot(q, n);
            float r = Mathf.Sqrt(a * a + b * b), ang = Mathf.Atan2(b, a), rr = Mathf.Clamp(r / R, 0f, 1.3f);
            float w2 = w - amp * Mathf.Sin(k * ang) * Mathf.Pow(rr, 1.5f) + cup * rr * rr;
            w2 -= gill * Mathf.Abs(Mathf.Sin(ang * 26f)) * rr;
            float Re = R * (1f + lobe * Mathf.Sin(k * ang + 0.6f));
            return SMax(r - Re, Mathf.Abs(w2) - th * (1.3f - 0.7f * rr), 0.008f) * 0.6f;
        }
    }
    static float Union(IEnumerable<Frill> fs, Vector3 p) { float d = 9f; foreach (var f in fs) d = Mathf.Min(d, f.D(p)); return d; }

    // 말굽 버섯 하나 — 두툼한 반원 · 윗면은 둥글고 테 무늬 · 바깥 끝이 처진다(미리보기 `bracket`). `ang`은 xz 평면의 방위(0 = +x · π/2 = +z).
    static float Bracket(Vector3 p, Vector3 c, float ang, float r, float wide = 1f)
    {
        var u = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)); var w = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));
        var q = p - c;
        float a = Vector3.Dot(q, u), b = q.y, cc = Vector3.Dot(q, w) / wide;
        float rad = Mathf.Sqrt(a * a + cc * cc);
        float t = Mathf.Clamp(a / r, 0f, 1.2f);
        b += 0.22f * r * t * t;
        float th = Mathf.Atan2(cc, a);
        float rr = r * (1f + 0.07f * Mathf.Sin(5f * th + ang * 3f));
        // 윗면의 잔 테 무늬(3.5mm)는 뺐다 — 툰 명암이 세 칸으로 끊어 칠해 잔물결마다 칸이 뒤집혀 점이 자글자글했다(11-04 게임 화면).
        return (Mathf.Sqrt((rad / rr) * (rad / rr) + (b / (0.30f * r)) * (b / (0.30f * r))) - 1f) * 0.30f * r;
    }

    // 말굽 버섯 무더기 — 몸 한쪽(ang)에 위로 겹겹이 · 아래 것이 크다(미리보기 `shelf`).
    readonly struct Shelf
    {
        public readonly float Cx, Cz, Y, Ang, R, Step, Shrink, Base; public readonly int N;
        public Shelf(float cx, float cz, float y, float ang, float R, int n = 3, float step = 0.05f, float shrink = 0.75f, float baseR = 0.07f)
        { Cx = cx; Cz = cz; Y = y; Ang = ang; this.R = R; N = n; Step = step; Shrink = shrink; Base = baseR; }
        public float D(Vector3 p) => D(p, 0, N);
        public float D(Vector3 p, int from, int to)   // 장 [from, to)만
        {
            float d = 9f;
            for (int i = from; i < to; i++)
            {
                float r = R * Mathf.Pow(Shrink, i), a = Ang + 0.35f * i * (i % 2 == 0 ? 1f : -1f), o = Base + r * 0.15f;
                d = Mathf.Min(d, Bracket(p, new Vector3(Cx + Mathf.Cos(a) * o, Y + i * Step, Cz + Mathf.Sin(a) * o), a, r));
            }
            return d;
        }
        public Bounds Box => new(new Vector3(Cx, Y + (N - 1) * Step * 0.5f, Cz),
                                 new Vector3(2f * (Base + 1.35f * R) + 0.06f, 0.7f * R + N * Step + 0.12f, 2f * (Base + 1.35f * R) + 0.06f));
    }

    // ── 몸통 — 몸 메시와 같은 셈(`BodyTorsoRadius`) ────────────────────────────

    static BodyDims _fd;
    static float FgTorso(Vector3 p)
    {
        float y = p.y - _fd.Lift, t = y / _fd.TorsoH;
        float r = BodyTorsoRadius(_fd.Half, Mathf.Clamp01(t), _fd.TorsoH, _fd.TopR);
        float q = Mathf.Sqrt(p.x * p.x + (p.z / _fd.Depth) * (p.z / _fd.Depth));
        return Mathf.Max((q - r) * _fd.Depth, Mathf.Max(-y, y - _fd.TorsoH));
    }
    static float FgTorsoZ(float x, float y, float side)   // (x, y)에서 몸통 앞(side 1) · 뒤(-1) 면의 z
    {
        float r = BodyTorsoRadius(_fd.Half, Mathf.Clamp01((y - _fd.Lift) / _fd.TorsoH), _fd.TorsoH, _fd.TopR);
        return side * _fd.Depth * Mathf.Sqrt(Mathf.Max(0f, r * r - x * x));
    }
    // 몸통 겉의 얇은 껍데기(`out` 만큼 밖 · `inner` 만큼 안) — 몸 메시 위에 덧붙이는 조각이 속을 채우지 않게.
    static float FgShell(float torso, float outward, float inner) => Mathf.Max(torso - outward, -(torso + inner));

    // ── 머리(머리 좌표 — 한복판 원점) ─────────────────────────────────────────

    sealed class FungalFrame
    {
        public readonly DwarfFrame F; public readonly OrcFrame O;
        public readonly Vector3 MC, MR;
        public FungalFrame(HeadShape h) { F = new DwarfFrame(h); O = F.O; MC = V3(0f, F.MY - 0.005f, F.ZM); MR = V3(0.062f, 0.085f, 0.09f); }
        public Vector3 Eye(float s) => V3(s * F.EX, F.EY, F.ZE);
        // 머리 중심(0, 0.02, 0)에서 d 방향으로 나가 머리 겉에 닿는 점(미리보기 `surf`).
        public Vector3 Surf(Vector3 d)
        {
            d = d.normalized; var c = V3(0f, 0.02f, 0f); float t = 0.05f;
            while (OrcBase(c + d * t, O) < 0f && t < 1f) t += 0.002f;
            return c + d * t;
        }
    }

    // 위아래로 벌린 입 — 가장자리는 2.5cm로 둥글린다(1.2cm면 줄인 머리 메시가 꺾인 테를 못 따라가 선이 가시로 튀었다 — 11-04 게임 화면 확대).
    static float FgHead(Vector3 p, FungalFrame f) => SMax(OrcBase(p, f.O), -Ell(p, f.MC + V3(0, 0, 0.02f), f.MR), 0.025f);
    static float FgMouth(Vector3 p, FungalFrame f) => Ell(p, f.MC - V3(0, 0, 0.035f), Vector3.Scale(f.MR, V3(1f, 1f, 0.8f)));
    static float FgTeeth(Vector3 p, FungalFrame f)   // 윗니는 아래로 · 아랫니는 위로 · 길이가 들쭉날쭉
    {
        float d = 9f;
        float[] xs = { -0.04f, -0.02f, 0f, 0.02f, 0.04f }, l1 = { 0.032f, 0.022f, 0.038f, 0.020f, 0.030f }, l2 = { 0.022f, 0.032f, 0.018f, 0.034f, 0.025f };
        for (int i = 0; i < 5; i++)
        {
            float x = xs[i], ry = f.MR.y * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / f.MR.x) * (x / f.MR.x))) - 0.008f, zz = f.F.ZM + 0.035f - 2f * x * x;
            d = Mathf.Min(d, Seg(p, V3(x, f.MC.y + ry, zz), V3(x * 0.9f, f.MC.y + ry - l1[i], zz - 0.006f), 0.009f, 0.002f));
            d = Mathf.Min(d, Seg(p, V3(x, f.MC.y - ry, zz), V3(x * 0.9f, f.MC.y - ry + l2[i], zz - 0.006f), 0.009f, 0.002f));
        }
        return d;
    }

    static Frill EyeDisc(FungalFrame f, float s)
        => new(f.Eye(s) + V3(s * 0.01f, 0f, 0.045f), V3(s * 0.15f, 0.05f, 1f), 0.055f, th: 0.007f, k: 6, amp: 0.004f, cup: 0.02f, gill: 0.003f, lobe: 0.05f);

    // 얼굴 셋 — (겉 부채 · 둘째 부채 · 눈 원판).
    static (List<Frill> a, List<Frill> b, List<Frill> disc) FgFace(string face, FungalFrame f)
    {
        var a = new List<Frill>(); var b = new List<Frill>(); var disc = new List<Frill>();
        switch (face)
        {
            case "A":   // 가면 — 두 눈 위에 부채가 나비 날개처럼
                foreach (float s in new[] { 1f, -1f })
                {
                    var e = f.Eye(s);
                    a.Add(new Frill(e + V3(s * 0.04f, 0f, 0.025f), V3(s * 0.30f, 0f, 1f), 0.12f, sq: 1.15f));
                    b.Add(new Frill(e + V3(s * 0.10f, 0.12f, -0.01f), V3(s * 0.45f, 0.5f, 0.8f), 0.115f, k: 3));
                    disc.Add(EyeDisc(f, s));
                }
                break;
            case "B":   // 터짐 — 눈구멍 · 이마에서 겹겹이 위 · 밖으로
                foreach (float s in new[] { 1f, -1f })
                {
                    var e = f.Eye(s);
                    a.Add(new Frill(e + V3(s * 0.04f, 0f, 0.035f), V3(s * 0.5f, 0f, 1f), 0.10f));
                    a.Add(new Frill(e + V3(s * 0.12f, 0.20f, -0.05f), V3(s * 0.8f, 0.8f, 0.3f), 0.12f, k: 3));
                    b.Add(new Frill(e + V3(s * 0.10f, 0.08f, 0f), V3(s * 0.9f, 0.3f, 0.6f), 0.11f));
                    b.Add(new Frill(e + V3(s * 0.02f, -0.06f, 0.04f), V3(s * 0.3f, -0.7f, 1f), 0.065f));
                    disc.Add(EyeDisc(f, s));
                }
                a.Add(new Frill(V3(0f, 0.23f, 0.25f), V3(0f, 0.8f, 1f), 0.13f, k: 3));
                b.Add(new Frill(V3(0f, 0.10f, 0.31f), V3(0f, 0.3f, 1f), 0.10f));
                break;
            case "C":   // 한쪽 — (+x) 눈만 무더기
            {
                var e = f.Eye(1f);
                a.Add(new Frill(e + V3(0.035f, 0f, 0.03f), V3(0.4f, 0f, 1f), 0.11f));
                a.Add(new Frill(e + V3(0.11f, 0.17f, -0.04f), V3(0.8f, 0.8f, 0.4f), 0.12f, k: 3));
                b.Add(new Frill(e + V3(0.11f, 0.06f, -0.01f), V3(1f, 0.3f, 0.5f), 0.10f));
                b.Add(new Frill(e + V3(0.015f, -0.07f, 0.035f), V3(0.3f, -0.7f, 1f), 0.07f));
                b.Add(new Frill(e + V3(-0.05f, 0.13f, 0.02f), V3(-0.3f, 0.8f, 0.8f), 0.08f));
                disc.Add(EyeDisc(f, 1f));
                break;
            }
        }
        return (a, b, disc);
    }
    static float FgCloudEye(Vector3 p, FungalFrame f) => Ell(p, V3(-f.F.EX, f.F.EY, f.F.ZE - 0.012f), V3(f.F.ER, f.F.ER * 1.1f, 0.02f));   // C의 안 덮인 눈

    // 머리 얼룩 — 얼굴 쪽은 늘 · 뒤통수 쪽은 벌마다(미리보기 `SPREAD` · `SPREAD_BACK`).
    static readonly (Vector3 dir, Vector3 r)[] FgSpread =
    {
        (V3(0.25f, 0.35f, 0.9f), V3(0.12f, 0.09f, 0.10f)), (V3(0.6f, 0.6f, 0.4f), V3(0.10f, 0.12f, 0.12f)), (V3(0.15f, 1f, 0f), V3(0.11f, 0.07f, 0.12f)),
        (V3(-0.45f, 0.1f, 0.9f), V3(0.06f, 0.05f, 0.05f)), (V3(0.7f, -0.4f, 0.55f), V3(0.05f, 0.09f, 0.06f)),
    };
    static readonly (Vector3 dir, Vector3 r)[] FgSpreadBack =
    {
        (V3(-0.35f, 0.45f, -0.85f), V3(0.16f, 0.14f, 0.12f)), (V3(-0.9f, 0.2f, -0.3f), V3(0.10f, 0.12f, 0.14f)), (V3(0.4f, 0.1f, -0.9f), V3(0.09f, 0.10f, 0.08f)),
        (V3(0f, -0.5f, -0.85f), V3(0.12f, 0.07f, 0.10f)), (V3(0.55f, 0.5f, -0.65f), V3(0.13f, 0.12f, 0.11f)), (V3(0.9f, 0.1f, -0.35f), V3(0.09f, 0.11f, 0.12f)),
    };
    static readonly int[][] FgRotSets = { new[] { 0, 2, 3 }, new[] { 1, 4 }, new[] { 3, 5, 0 }, new[] { 2, 4, 5 } };   // `Head.Pick.Rot.1~4`

    static float FgRot(Vector3 p, FungalFrame f, (Vector3 c, Vector3 r)[] blobs)
    {
        // **머리 살에서 1cm 넘게 띄운다**(11-04 게임 화면, 사용자: *"얼룩이 얼굴이랑 겹쳐져서"*) — 처음엔 2~5mm였는데 머리 메시는 줄이고 편 것이라
        // 거리장 표면과 몇 mm씩 어긋나 두 면이 번갈아 비쳤다(빗금). 결도 바깥으로만 솟는다.
        float d = OrcBase(p, f.O) - 0.011f - 0.003f * Mathf.Abs(Mathf.Sin(p.x * 70f + p.y * 40f));
        float blob = 9f; foreach (var (c, r) in blobs) blob = Mathf.Min(blob, Ell(p, c, r));
        blob += 0.012f * Mathf.Sin(p.x * 60f) * Mathf.Sin(p.y * 55f);   // 들쭉날쭉한 테
        return SMax(d, blob, 0.01f);
    }

    static readonly (Vector3 dir, float r)[] FgNubs =
    {
        (V3(0.35f, 0.55f, 0.75f), 0.022f), (V3(0.6f, 0.35f, 0.7f), 0.017f), (V3(0.3f, 0.95f, 0.15f), 0.026f), (V3(0.6f, 0.75f, 0.05f), 0.019f),
        (V3(0.35f, 0.9f, -0.25f), 0.015f), (V3(-0.45f, 0.15f, 0.88f), 0.014f), (V3(0.7f, -0.3f, 0.6f), 0.013f), (V3(0.5f, 0.6f, 0.45f), 0.012f),
        (V3(-0.2f, 0.55f, -0.8f), 0.020f), (V3(-0.55f, 0.3f, -0.75f), 0.016f), (V3(-0.85f, 0.35f, -0.2f), 0.018f), (V3(0.35f, 0.3f, -0.88f), 0.014f),
        (V3(-0.1f, 0f, -1f), 0.015f),
    };
    // 뒤통수 말굽 무더기 — 자리 다섯(`Head.Pick.Stack.*`).
    static readonly (string name, (Vector3 dir, float r)[] stack)[] FgStacks =
    {
        ("BackR", new[] { (V3(-0.30f, -0.05f, -0.95f), 0.20f), (V3(-0.22f, 0.22f, -0.95f), 0.16f), (V3(-0.15f, 0.45f, -0.88f), 0.12f) }),
        ("BackL", new[] { (V3(0.35f, -0.10f, -0.93f), 0.18f), (V3(0.30f, 0.15f, -0.94f), 0.14f), (V3(0.22f, 0.38f, -0.90f), 0.10f) }),
        ("Crown", new[] { (V3(0f, 0.55f, -0.83f), 0.15f), (V3(0.05f, 0.75f, -0.6f), 0.11f) }),
        ("Ear",   new[] { (V3(-0.80f, -0.15f, -0.55f), 0.15f), (V3(-0.75f, 0.10f, -0.62f), 0.11f) }),
        ("Nape",  new[] { (V3(0.15f, -0.35f, -0.92f), 0.13f), (V3(-0.12f, -0.30f, -0.94f), 0.10f) }),
    };

    // 먹물 버섯 다발 — 정수리에서 솟은 길쭉한 종 · 끝이 검게 녹아 흘러내린다(미리보기 `ink`). (대 · 갓 · 녹은 테)
    static (float stalk, float cap, float drip) FgInkCaps(Vector3 p, (Vector3 b, Vector3 tip, float r)[] ink)
    {
        float st = 9f, caps = 9f, drip = 9f;
        foreach (var (b, tip, r) in ink)
        {
            st = Mathf.Min(st, Seg(p, b, tip, 0.016f, 0.012f));
            var cc = tip - V3(0f, r * 1.2f, 0f);
            float bell = SMax(Ell(p, cc + V3(0f, r * 0.6f, 0f), V3(r, r * 2f, r)), -(p.y - (cc.y - r * 0.9f)), 0.006f);
            caps = Mathf.Min(caps, bell);
            float rim = SMax(Ell(p, cc - V3(0f, r * 0.75f, 0f), V3(r * 1.05f, r * 0.35f, r * 1.05f)), -(p.y - (cc.y - r * 1.25f)), 0.004f);
            drip = Mathf.Min(drip, rim + 0.012f * Mathf.Sin(Mathf.Atan2(p.z - cc.z, p.x - cc.x) * 7f));
        }
        return (st, caps, drip);
    }

    // 입에서 비어져 나온 버섯 무리 — 벌린 입 안쪽에서 앞 · 아래로(미리보기 `mouthshroom`). (대 · 갓)
    static (float stalk, float cap) FgMouthShrooms(Vector3 p, FungalFrame f)
    {
        float st = 9f, caps = 9f;
        foreach (var (dx, dy, dz, r) in new[] { (0f, -0.04f, 0.20f, 0.065f), (0.09f, -0.13f, 0.16f, 0.052f), (-0.08f, -0.12f, 0.17f, 0.055f), (0.05f, 0.05f, 0.15f, 0.045f),
                                                (-0.04f, -0.22f, 0.11f, 0.042f), (0.13f, -0.03f, 0.10f, 0.038f), (-0.12f, 0.02f, 0.11f, 0.036f), (0.02f, -0.18f, 0.19f, 0.040f) })
        {
            var b0 = f.MC + V3(dx * 0.4f, dy * 0.3f, -0.02f); var b1 = f.MC + V3(dx, dy, dz);
            st = Mathf.Min(st, Seg(p, b0, b1, 0.018f, 0.014f));
            var n = (b1 - b0).normalized;
            caps = Mathf.Min(caps, SMax(Ell(p, b1 + n * r * 0.2f, V3(r, r * 0.65f, r)), -Vector3.Dot(p - b1, n), 0.004f));
        }
        return (st, caps);
    }

    // ── 몸(Model 좌표 — 발밑 원점) ────────────────────────────────────────────

    // 말굽 자리 열여덟(미리보기 `SITES` — 순서가 축 번호다. 끝에만 붙일 것: 번호가 바뀌면 같은 씨앗의 버섯 자리가 바뀐다).
    static Shelf[] FgSites(BodyDims d)
    {
        float T = d.Top, AX = d.ArmX, LX = d.LegX, HY = d.HandY, BK = -Mathf.PI / 2f, FR = Mathf.PI / 2f, PI = Mathf.PI;
        return new[]
        {
            new Shelf(0.04f, 0f, T - 0.50f, BK + 0.2f, 0.15f, 3, 0.05f, baseR: 0.15f),    // 허리 뒤
            new Shelf(0.13f, 0f, T - 0.18f, BK + 0.6f, 0.11f, 2, 0.045f, baseR: 0.15f),   // 날개뼈(+x)
            new Shelf(-0.13f, 0f, T - 0.16f, BK - 0.6f, 0.11f, 2, 0.045f, baseR: 0.15f),  // 날개뼈(-x)
            new Shelf(0.04f, 0f, T - 0.06f, 0.4f, 0.15f, 3, 0.05f, baseR: 0.18f),         // 목(+x)
            new Shelf(-0.04f, 0f, T - 0.06f, PI - 0.4f, 0.14f, 3, 0.05f, baseR: 0.18f),   // 목(-x)
            new Shelf(-0.10f, 0f, T - 0.24f, FR + 0.35f, 0.08f, 2, 0.035f, baseR: 0.16f), // 가슴(-x)
            new Shelf(0.10f, 0f, T - 0.20f, FR - 0.35f, 0.08f, 2, 0.035f, baseR: 0.16f),  // 가슴(+x)
            new Shelf(-0.02f, 0f, d.HemY + 0.06f, FR - 0.25f, 0.07f, 2, 0.03f, baseR: 0.17f),  // 아랫배
            new Shelf(0.10f, 0f, T - 0.40f, 0.1f, 0.10f, 2, 0.04f, baseR: 0.18f),         // 옆구리(+x)
            new Shelf(-0.10f, 0f, T - 0.34f, PI - 0.1f, 0.10f, 2, 0.04f, baseR: 0.18f),   // 옆구리(-x)
            new Shelf(AX, 0f, HY + 0.10f, 0.1f, 0.12f, 3, baseR: 0.06f),                  // 팔뚝(+x)
            new Shelf(-AX, 0f, HY + 0.12f, PI - 0.2f, 0.11f, 3, baseR: 0.06f),            // 팔뚝(-x)
            new Shelf(-AX, 0f, HY + 0.04f, 3.0f, 0.10f, 2, baseR: 0.06f),                 // 손목(-x)
            new Shelf(AX, 0f, HY + 0.03f, 0.2f, 0.09f, 2, baseR: 0.06f),                  // 손목(+x)
            new Shelf(-LX, 0f, 0.28f, 1.9f, 0.12f, 2, baseR: 0.09f),                      // 무릎(-x)
            new Shelf(LX, 0f, 0.30f, 1.3f, 0.11f, 2, baseR: 0.09f),                       // 무릎(+x)
            new Shelf(LX, 0f, 0.14f, -0.5f, 0.12f, 2, 0.045f, baseR: 0.09f),              // 정강이 뒤(+x)
            new Shelf(-LX, 0f, 0.16f, -2.4f, 0.11f, 2, 0.045f, baseR: 0.09f),             // 종아리(-x)
        };
    }

    static readonly int[] FgFaceSites = { 3, 4 };   // 목(+x) · 목(-x) — **맨 위 한 장**이 턱에 닿는다(그 장만 얼굴 덩어리)

    // 붙을 관절 — 자리로 가른다(팔 · 다리는 아래 마디, 나머지는 등뼈).
    static string FgJoint(Vector3 c, BodyDims d)
    {
        string side = c.x < 0f ? "L" : "R";
        if (Mathf.Abs(c.x) >= d.ArmX * 0.8f && c.y > d.HipY) return "LowerArm" + side;
        if (c.y < d.HipY) return "LowerLeg" + side;
        return "Spine";
    }

    // 터진 틈 다섯(미리보기 `WOUND_SITES`) — (중심 x, y) · (반지름 x, y) · 면(1 앞 · -1 뒤).
    static (Vector2 c, Vector2 r, float side)[] FgWounds(BodyDims d) => new[]
    {
        (new Vector2(0.07f, d.Top - 0.36f), new Vector2(0.085f, 0.14f), 1f),    // 배(+x)
        (new Vector2(-0.08f, d.Top - 0.22f), new Vector2(0.075f, 0.12f), 1f),   // 가슴(-x)
        (new Vector2(-0.09f, d.Top - 0.24f), new Vector2(0.075f, 0.12f), -1f),  // 날개뼈(-x)
        (new Vector2(0.08f, d.Top - 0.42f), new Vector2(0.08f, 0.11f), -1f),    // 허리 뒤(+x)
        (new Vector2(0.10f, d.Top - 0.15f), new Vector2(0.06f, 0.10f), -1f),    // 날개뼈(+x)
    };
    static float FgWoundR(Vector3 p, (Vector2 c, Vector2 r, float side) w)   // 테두리까지의 비율(1 = 가장자리) · 그 면에만
    {
        if (p.z * w.side <= 0.02f) return 9f;
        float x = (p.x - w.c.x) / w.r.x, y = (p.y - w.c.y) / w.r.y, a = Mathf.Atan2(y, x);
        return Mathf.Sqrt(x * x + y * y) * (1f + 0.12f * Mathf.Sin(5f * a + 1f + w.side) + 0.06f * Mathf.Sin(11f * a));
    }
    static float FgWoundGill(Vector3 p, (Vector2 c, Vector2 r, float side) w)   // 틈 안 — 주름살이 비어져 나온다
    {
        float tor = FgTorso(p);
        float a = Mathf.Atan2(p.y - w.c.y - 0.05f, p.x - w.c.x);
        float d = SMax(tor - 0.006f - 0.010f * Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 13f)), 0.6f), (FgWoundR(p, w) - 0.95f) * 0.08f, 0.004f) * 0.7f;
        return Mathf.Max(d, -(tor + 0.02f));
    }
    static float FgWoundLip(Vector3 p, (Vector2 c, Vector2 r, float side) w)    // 틈 둘레 — 살이 입술처럼 젖혀진다
    {
        float wr = FgWoundR(p, w);
        if (wr > 3f) return 0.05f;
        float tor = FgTorso(p), lip = Mathf.Exp(-((wr - 1.05f) / 0.16f) * ((wr - 1.05f) / 0.16f));
        return SMax(FgShell(tor - 0.006f - 0.018f * lip, 0f, 0.012f), (0.25f - lip) * 0.05f, 0.003f);   // 몸 메시(다각 통)와 겹쳐 비치지 않게 6mm 넘게 띄운다
    }

    static float FgRag(Vector3 p)   // 허리에 걸린 넝마 — 해진 아랫단
    {
        float bot = _fd.HemY - 0.10f + 0.03f * Mathf.Sin(p.x * 55f + 1f) + 0.02f * Mathf.Sin(p.z * 70f);
        return Mathf.Max(FgShell(FgTorso(p), 0.008f, 0.012f), Mathf.Max(p.y - _fd.HemY, bot - p.y));
    }

    static float FgBodyNubs(Vector3 p)   // 앞가슴 · 배의 작은 혹
    {
        float T = _fd.Top, d = 9f;
        foreach (var (x, y, r) in new[] { (-0.04f, T - 0.12f, 0.020f), (-0.13f, T - 0.33f, 0.016f), (0.16f, T - 0.18f, 0.014f), (0.13f, T - 0.46f, 0.017f), (-0.06f, T - 0.40f, 0.012f), (0f, T - 0.18f, 0.011f) })
            d = Mathf.Min(d, Sph(p, V3(x, y, FgTorsoZ(x, y, 1f)), r));
        return d;
    }

    static float FgRoots(Vector3 p)   // 신 대신 뿌리 발가락 — 앞으로 셋(발 하나 · 미러)
    {
        var q = Mirror(p); float LX = _fd.LegX, d = 9f;
        foreach (var (dx, l) in new[] { (-0.05f, 0.16f), (0f, 0.19f), (0.05f, 0.15f) })
            d = Mathf.Min(d, Seg(q, V3(LX + dx * 0.6f, 0.07f, 0.02f), V3(LX + dx, 0.015f, l), 0.035f, 0.010f));
        return Mathf.Min(d, Ell(q, V3(LX, 0.07f, 0f), V3(0.08f, 0.07f, 0.09f)));
    }

    // ── 대표 버섯 ────────────────────────────────────────────────────────────

    static float FgCollar(Vector3 p, Vector3 HO, FungalFrame f, Frill fr)   // 목도리 — 목 뒤에서 머리를 감싸며 크게 · 몸 · 머리를 뚫지 않는다
    {
        float d = fr.D(p);
        if (d > 0.05f) return d;
        d = SMax(d, -(FgTorso(p) - 0.004f), 0.01f);
        d = SMax(d, -(OrcBase(p - HO, f.O) - 0.004f), 0.01f);
        return SMax(d, (_fd.Top - 0.04f) - p.y + 0.02f * Mathf.Sin(p.x * 40f), 0.01f);
    }

    static (float stalk, float cap, float under, float spots) FgSprout(Vector3 p, float sx)   // 큰 버섯 하나 — 어깨에서 머리 옆으로 굽어 솟은 대 + 처진 갓
    {
        float T = _fd.Top;
        var b0 = V3(sx * 0.17f, T - 0.04f, -0.03f); var b1 = V3(sx * 0.33f, T + 0.25f, -0.05f); var b2 = V3(sx * 0.40f, T + 0.62f, -0.02f);
        float stalk = SMin(Seg(p, b0, b1, 0.065f, 0.048f), Seg(p, b1, b2, 0.048f, 0.036f), 0.03f);
        var cc = b2 + V3(sx * 0.03f, 0.05f, 0f);
        float ang = Mathf.Atan2(p.z - cc.z, p.x - cc.x);
        float cap = SMax(Ell(p, cc, V3(0.27f, 0.15f, 0.27f)), -(p.y - (cc.y - 0.03f)), 0.012f);
        cap -= 0.016f * Mathf.Sin(ang * 6f) * Mathf.Clamp01(((p - cc).magnitude - 0.16f) / 0.1f);
        float under = SMax(Ell(p, cc - V3(0f, 0.035f, 0f), V3(0.25f, 0.05f, 0.25f)), p.y - (cc.y - 0.025f), 0.005f) - 0.004f * Mathf.Abs(Mathf.Sin(ang * 40f));
        float spots = 9f;
        foreach (var (dx, dz, r) in new[] { (0f, 0f, 0.03f), (0.12f, 0.06f, 0.022f), (-0.10f, -0.10f, 0.025f), (0.06f, -0.15f, 0.018f), (-0.15f, 0.07f, 0.02f), (0.03f, 0.14f, 0.017f) })
            spots = Mathf.Min(spots, Sph(p, cc + V3(dx, 0.145f - 0.9f * (dx * dx + dz * dz), dz), r));
        return (stalk, cap, under, spots);
    }

    static (float ball, float inner) FgPuff(Vector3 p, float sx)   // 포자 주머니 — 등 · 어깨에 반쯤 묻힌 덩이 · 둘은 찢어져 속이 검다
    {
        float T = _fd.Top, ball = 9f, holes = 9f, inner = 9f;
        var pf = new[] { (0.10f, -0.12f, 0.12f), (-0.07f, -0.06f, 0.085f), (0.19f, 0.02f, 0.08f), (0.02f, -0.30f, 0.075f), (-0.13f, -0.24f, 0.06f), (0.21f, -0.22f, 0.055f) };
        for (int i = 0; i < pf.Length; i++)
        {
            var (x, y, r) = pf[i];
            float rz = _fd.Depth * BodyTorsoRadius(_fd.Half, Mathf.Clamp01((T + y - _fd.Lift) / _fd.TorsoH), _fd.TorsoH, _fd.TopR);
            float cz = -Mathf.Sqrt(Mathf.Max(0f, rz * rz - (x * 0.8f) * (x * 0.8f))) + r * 0.25f;
            var c = y < 0f ? V3(sx * x, T + y, cz) : V3(sx * x, T + y - 0.02f, -0.02f);
            var n = (c - V3(0f, T - 0.15f, 0f)).normalized;
            ball = Mathf.Min(ball, Ell(p, c + n * r * 0.1f, V3(r, r * 0.92f, r * 0.85f)) - 0.006f * Mathf.Sin(p.x * 90f) * Mathf.Sin(p.y * 85f) * Mathf.Sin(p.z * 80f));
            if (i == 0 || i == 3)
            {
                var h = c + n * r;
                holes = Mathf.Min(holes, Sph(p, h, r * 0.45f) + 0.012f * Mathf.Sin(Mathf.Atan2(p.y - h.y, p.x - h.x) * 7f));
                inner = Mathf.Min(inner, Sph(p, c + n * r * 0.55f, r * 0.5f));
            }
        }
        return (SMax(ball, -holes, 0.008f), inner);
    }

    static (float stalk, float club) FgCordy(Vector3 p)   // 동충하초 — 등 · 어깨에서 뿔처럼 휘어 솟은 검은 대 · 끝에 황토색 곤봉
    {
        float T = _fd.Top, st = 9f, club = 9f;
        foreach (var (o, dir, L) in new[] { (V3(0.14f, -0.06f, -0.12f), V3(0.30f, 0.42f, -0.30f), 0.40f), (V3(-0.10f, -0.02f, -0.15f), V3(-0.20f, 0.55f, -0.35f), 0.48f),
                                            (V3(0.02f, -0.18f, -0.19f), V3(0.05f, 0.40f, -0.55f), 0.38f), (V3(0.20f, 0f, -0.02f), V3(0.55f, 0.38f, -0.05f), 0.30f),
                                            (V3(-0.18f, -0.08f, -0.08f), V3(-0.50f, 0.30f, -0.25f), 0.32f), (V3(0.08f, -0.30f, -0.19f), V3(0.25f, 0.10f, -0.60f), 0.28f) })
        {
            var b0 = o + V3(0f, T, 0f); var dn = dir.normalized;
            var m = b0 + dn * L * 0.5f + V3(0f, L * 0.12f, 0f); var b2 = b0 + dn * L + V3(0f, L * 0.30f, 0f);
            st = Mathf.Min(st, SMin(Seg(p, b0, m, 0.020f, 0.015f), Seg(p, m, b2, 0.015f, 0.010f), 0.01f));
            var cd = (b2 - m).normalized;
            club = Mathf.Min(club, Seg(p, b2 - cd * 0.01f, b2 + cd * 0.09f, 0.026f, 0.016f));
        }
        return (st, club);
    }

    // ── 부위 ────────────────────────────────────────────────────────────────

    static Dictionary<string, Material> FungalMaterials(Dictionary<string, Material> mats)
    {
        // 몸 메시의 두 칸(셔츠 · 바지)은 황갈 바탕에 **정점 색을 곱해** 회녹 살로 번진다(`FungalBodyColors` · 11-04, 사용자: *"다리 색이랑 몸색이랑 어느정도
        // 섞이면"* — 바지는 *"틈하고 같은 색"*). 손 · 머리 살은 잿빛 · 신발은 뿌리 색.
        var m = OutfitMaterials(mats, "Fungal", FcPants, FcPants, FcSkin, null);
        foreach (var k in new[] { "Shirt", "Pants" }) { m[k].SetFloat("_VertexAO", 1f); EditorUtility.SetDirty(m[k]); }
        // **몸 쪽 굵은 바깥 선은 제 얼굴 위에 안 긋는다**(11-04 게임 화면 — 사용자: 얼굴 옆 버섯에 굵은 선). 바깥 선은 번호가 다르면 긋는데, 몸 조각(몸 번호)의
        // 선이 얼굴(얼굴 표시) 위에 그어졌다 → 몸 선은 얼굴 표시를 무시하고 폰 번호만 본다(`_StencilReadMask` 127). **얼굴 선은 그대로** — 머리가 몸 위에
        // 겹치는 턱 밑은 굵은 선이 규칙이다(사용자: *"원래 턱 밑은 굵은 선이 규칙"* — 둘 다 무시하게 했더니 턱 선이 사라졌다).
        {
            var o = Variant(mats["OutlineOuter"], "Chibi_OutlineOuter_Fungal", "_OutlineColor", mats["OutlineOuter"].GetColor("_OutlineColor"));
            o.SetFloat("_StencilReadMask", 127f);
            o.renderQueue = mats["OutlineOuter"].renderQueue;
            EditorUtility.SetDirty(o);
            m["OutlineOuter"] = o;
        }
        m["Shoe"] = Variant(mats["Shoe"], "Chibi_Shoe_Fungal", "_BaseColor", FcRoot);
        // 머리에 붙는 것은 고운 털 재질(얼굴 스텐실 · 제 선 없음 · 바깥 선 `OutlineOuterFine`), 몸에 붙는 것은 바지 재질(몸 번호 · 바깥 선 `OutlineOuter`)의 사본.
        // **버섯은 제 선을 긋고 · 스텐실에 제 자리를 적는다**(11-04 게임 화면 둘).
        // - 처음엔 머리 쪽은 고운 털, 몸 쪽은 바지 재질의 사본 — 둘 다 제 선을 끄고 폰 바깥 선만 받아 몸 위에 겹친 버섯 사이에 선이 없었다
        //   (사용자: *"버섯 땜에 외곽선이 애매하게 돼 확실하게 그어줘"*).
        // - 그래서 손 · 귀처럼 제 선을 긋는 살(`Skin`)로 바꿨더니 **버섯이 검게 덮였다** — 바깥 선(카메라 쪽으로 당겨 긋는다)은 스텐실에 "내 몸 · 내 얼굴"이
        //   적힌 자리만 비키는데 `Skin`은 아무것도 안 적는다(큰 짐승 이 · 눈이 검게 덮였던 것과 같은 까닭).
        // - 둘 다 하는 재질의 사본으로 — 몸 쪽은 해골 몸(`BoneBody` — 제 선 + 몸 번호), 머리 쪽은 머리 살(`HeadSkin` — 제 선 + 얼굴 표시). 선만 `FungalLine`.
        //   **머리 얼룩(`Rot`)만 선 없이**(고운 털 그대로) — 번짐이 스티커가 된다.
        //   **정점 그늘은 끈다**(해골 몸은 켜져 있다 — 겹친 말굽 사이가 얼룩덜룩 검었다) · **몸에 붙은 얇은 조각은 제 선 없이**(`Flat` — 틈 테두리 · 넝마 · 뿌리 · 혹:
        //   몸에 거의 붙은 안쪽 면에 선이 둘려 검은 테가 됐다). 선이 없어도 스텐실은 적어 바깥 선이 안 덮는다.
        Material Lined(string src, string name, Color c, float width) { var v = Variant(mats[src], name, "_BaseColor", c); v.SetFloat("_OutlineWidth", width); v.SetFloat("_OutlineBack", FungalLineBack); v.SetFloat("_VertexAO", 0f); EditorUtility.SetDirty(v); return v; }
        //   **머리 버섯은 자리로 가른다**(11-04 게임 화면 둘). 바깥 선은 스텐실 번호만 비교하고 깊이는 안 본다.
        //   - **얼굴 위의 것**(눈 부채 · 입 버섯 · 얼룩)은 **얼굴 표시**(`HeadSkin` — 코처럼): 얼굴과 한 덩어리로 묶여 둘레에 굵은 선 하나 · 안에는 가는 제 선만.
        //     몸 번호로 했더니 버섯끼리 남이 되어 갓마다 6cm 선이 서로 위에 그어져 검은 덩어리가 됐다(사용자: *"입에서 나오는 버섯은 하나의 외곽선으로"*).
        //   - **뒤통수 · 정수리의 것**(말굽 무더기 · 혹 · 먹물 버섯)은 **몸 번호**(`BoneBody`): 얼굴 표시면 머리 둘레 선이 머리 뒤의 그것들까지 "내 얼굴"로 보고 비켜
        //     비쳤다(사용자: *"가려져야 할 버섯이 보인다"*). 몸 번호면 머리 선이 깊이가 앞서는 곳에서 덮는다.
        //   머리 얼룩(`Rot`)은 얼굴 살 위에 칠한 것이라 제 선도 없다(고운 털).
        void H(string key, Color c) => m["FgH" + key] = key == "Rot" ? Variant(mats["HairFine"], $"Chibi_FungalHead_{key}", "_BaseColor", c) : Lined("HeadSkin", $"Chibi_FungalHead_{key}", c, FungalLine);
        void HB(string key, Color c) => m["FgK" + key] = Lined("BoneBody", $"Chibi_FungalBack_{key}", c, FungalLine);
        HB("FrillB", FcFrillB); HB("InkCap", FcInkCap); HB("Ink", FcInk); HB("Stalk", FcStalk);
        void B(string key, Color c) => m["FgB" + key] = Lined("BoneBody", $"Chibi_FungalBody_{key}", c, FungalLine);
        void Flat(string key, Color c) => m["FgF" + key] = Lined("BoneBody", $"Chibi_FungalFlat_{key}", c, 0f);
        Flat("Flesh", FcFlesh); Flat("Gill", FcGill); Flat("Rag", FcRag); Flat("Root", FcRoot); Flat("FrillB", FcFrillB);
        H("Frill", FcFrill); H("FrillB", FcFrillB); H("FrillIn", FcFrillIn); H("Rot", FcRot); H("Cloud", FcCloud); H("Mouth", FcMouth); H("Tooth", FcTooth);
        H("Flesh", FcFlesh); H("InkCap", FcInkCap); H("Ink", FcInk); H("Stalk", FcStalk);
        B("Frill", FcFrill); B("FrillB", FcFrillB); B("FrillIn", FcFrillIn); B("Gill", FcGill); B("Flesh", FcFlesh); B("Rag", FcRag); B("Root", FcRoot);
        B("Cap", FcCap); B("Spot", FcSpot); B("Puff", FcPuff); B("Ink", FcInk); B("Cord", FcCord); B("Club", FcClub);
        return m;
    }

    // 머리 부위 — 사람 머리(`HeadParts`) 대신 받는다(드워프와 같은 길 · 크기는 사람 그대로).
    static List<Part> FungalHeadParts()
    {
        var h = Array.Find(Heads, x => x.Name == DwarfBasis);
        var f = new FungalFrame(h);
        var pos = HeadPos(h);
        var parts = new List<Part>();
        Func<Vector3, float> scene = p => FgHead(p, f);
        // `relax` — 펼 횟수(0이면 안 편다). 얇은 얼굴 부채는 두 번만(많이 펴면 판이 오그라든다) · 덜 줄인다(가장자리가 계단 톱니가 됐다).
        Mesh Sculpt(string name, Func<Vector3, float> sdf, Bounds b, float step, float keep, int relax = 0)
        {
            var surf = SurfaceNets(sdf, b, step, scene, p => p, 0.03f, 4f, keep);
            if (relax > 0) Relax(surf.Verts, surf.Tris, relax);
            return SaveSkull($"Chibi_Fungal_{name}", surf);
        }
        void Add(string name, Mesh mesh, string mat, string line = "OutlineOuterFine", bool on = true)
            => parts.Add(new Part(name, mesh, pos, Vector3.one, mat, on, outerMat: line));

        parts.Add(new Part("Head", null, pos, h.Size, "HeadSkin", on: false));
        var hb = new Bounds(new Vector3(0f, -0.01f, 0.03f), new Vector3(h.Size.x + 0.08f, h.Size.y + 0.08f, h.Size.z + 0.16f));
        // 머리 살의 바깥 선은 **땅 이기기를 켠 얼굴 선**(`HangLine` — 드워프 수염 선) — 깊이를 카메라 쪽으로 당겨 **머리 뒤의 몸 조각(등 포자 · 말굽)을 덮는다**(11-04 게임 화면,
        // 사용자: *"뒤에 포자 주머니는 외곽선에 가려서 안 보이길"*). 당기지 않는 얼굴 선은 테두리 정점 깊이에 그려져, 위에서 비스듬히 보면 머리 아래 테두리와 등 위 포자의
        // 깊이가 거의 같아 포자가 선을 뚫었다.
        Add($"Head.Shape.{h.Name}", Sculpt("Head", scene, hb, DwarfHeadStep, 0.3f, relax: 4), "SkinFine", HangLine);
        var mb = new Bounds(f.MC, new Vector3(0.18f, 0.22f, 0.20f));
        Add("Head.Fungal.Mouth", Sculpt("Mouth", p => FgMouth(p, f), mb, FungalFineStep, 0.5f), "FgHMouth", null);
        Add("Head.Fungal.Teeth", Sculpt("Teeth", p => FgTeeth(p, f), mb, 0.0018f, 0.4f), "FgHTooth", null);

        // 얼굴 셋 — 눈을 덮는 부채(`Head.Pick.Face.*` · 첫 벌이 켜져 굽는다).
        var fb = new Bounds(new Vector3(0f, f.F.EY + 0.08f, f.F.ZE - 0.02f), new Vector3(0.62f, 0.52f, 0.34f));
        foreach (var face in new[] { "A", "B", "C" })
        {
            var (a, b, disc) = FgFace(face, f);
            Add($"Head.Pick.Face.{face}.Frill",  Sculpt($"Face{face}_Frill",  p => Union(a, p), fb, FungalFineStep, 0.7f, relax: 2), "FgHFrill", on: face == "A");
            Add($"Head.Pick.Face.{face}.FrillB", Sculpt($"Face{face}_FrillB", p => Union(b, p), fb, FungalFineStep, 0.7f, relax: 2), "FgHFrillB", on: face == "A");
            Add($"Head.Pick.Face.{face}.Disc",   Sculpt($"Face{face}_Disc",   p => Union(disc, p), fb, 0.0025f, 0.4f), "FgHFrillIn", null, on: face == "A");
        }
        Add("Head.Pick.Face.C.Eye", Sculpt("FaceC_Eye", p => FgCloudEye(p, f), new Bounds(V3(-f.F.EX, f.F.EY, f.F.ZE), V3(0.10f, 0.10f, 0.08f)), 0.002f, 0.5f), "FgHCloud", null, on: false);

        // 머리 얼룩 · 혹 · 뒤통수 무더기.
        var front = Array.ConvertAll(FgSpread, s => (f.Surf(s.dir), s.r));
        var hb2 = new Bounds(new Vector3(0f, 0.02f, 0f), new Vector3(h.Size.x + 0.10f, h.Size.y + 0.10f, h.Size.z + 0.14f));
        for (int i = 0; i < FgRotSets.Length; i++)
        {
            var blobs = new List<(Vector3, Vector3)>(front);
            foreach (int j in FgRotSets[i]) blobs.Add((f.Surf(FgSpreadBack[j].dir), FgSpreadBack[j].r));
            var arr = blobs.ToArray();
            Add($"Head.Pick.Rot.{i + 1}", Sculpt($"Rot{i + 1}", p => FgRot(p, f, arr), hb2, FungalStep * 0.8f, 0.4f), "FgHRot", on: i == 0);
        }
        var nubs = Array.ConvertAll(FgNubs, n => (f.Surf(n.dir), n.r));
        Add("Head.Fungal.Nubs", Sculpt("Nubs", p => { float d = 9f; foreach (var (c, r) in nubs) d = Mathf.Min(d, Sph(p, c, r)); return d; }, hb2, 0.003f, 0.4f), "FgKFrillB");
        for (int i = 0; i < FgStacks.Length; i++)
        {
            var (name, stack) = FgStacks[i];
            var br = Array.ConvertAll(stack, s => { var c = f.Surf(s.dir); float a = Mathf.Atan2(c.z, c.x); return (c - new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s.r * 0.15f, a, s.r); });
            var box = new Bounds(br[0].Item1, Vector3.one * 0.5f);
            foreach (var (c, _, r) in br) box.Encapsulate(new Bounds(c, Vector3.one * (2.4f * r)));
            Add($"Head.Pick.Stack.{name}", Sculpt($"Stack{name}", p => { float d = 9f; foreach (var (c, a, r) in br) d = Mathf.Min(d, Bracket(p, c, a, r, 0.72f)); return d; }, box, FungalStep * 0.8f, 0.6f, relax: 4),
                "FgKFrillB", on: i == 0);
        }

        // 대표 버섯 중 머리에 붙는 둘 — 먹물 버섯 다발 · 입 버섯(`Special` 축 · 몸 쪽 넷은 `FungalBodyParts`).
        var ink = Array.ConvertAll(new[] { (0f, -0.02f, 0.30f, 0.05f), (0.09f, 0.04f, 0.22f, 0.042f), (-0.08f, 0.05f, 0.25f, 0.045f), (0.05f, -0.12f, 0.20f, 0.04f), (-0.10f, -0.08f, 0.17f, 0.036f), (0.14f, -0.06f, 0.14f, 0.032f) },
                                   t => { var b = f.Surf(V3(t.Item1, 1f, t.Item2)); return (b, b + V3(t.Item1 * 0.8f, t.Item3, t.Item2 * 0.8f), t.Item4); });
        var ib = new Bounds(V3(0.02f, f.O.Ht * 0.5f + 0.16f, -0.02f), V3(0.52f, 0.46f, 0.46f));
        Add("Head.Pick.Special.Ink.Stalk", Sculpt("Ink_Stalk", p => FgInkCaps(p, ink).stalk, ib, 0.003f, 0.4f), "FgKStalk", on: false);
        Add("Head.Pick.Special.Ink.Cap",   Sculpt("Ink_Cap",   p => FgInkCaps(p, ink).cap,   ib, 0.003f, 0.4f), "FgKInkCap", on: false);
        Add("Head.Pick.Special.Ink.Drip",  Sculpt("Ink_Drip",  p => FgInkCaps(p, ink).drip,  ib, 0.0025f, 0.4f), "FgKInk", on: false);
        var msb = new Bounds(f.MC + V3(0f, -0.06f, 0.12f), V3(0.42f, 0.48f, 0.40f));
        Add("Head.Pick.Special.Mouth.Stalk", Sculpt("MouthShroom_Stalk", p => FgMouthShrooms(p, f).stalk, msb, 0.003f, 0.4f), "FgHStalk", on: false);
        Add("Head.Pick.Special.Mouth.Cap",   Sculpt("MouthShroom_Cap",   p => FgMouthShrooms(p, f).cap,   msb, 0.003f, 0.4f), "FgHInkCap", on: false);
        return parts;
    }

    // 몸에 붙는 부위 — Model 좌표로 빚어 관절에 단다(이름 앞머리 = 관절).
    static List<Part> FungalBodyParts(BodyDims d)
    {
        _fd = d;
        var h = Array.Find(Heads, x => x.Name == DwarfBasis);
        var f = new FungalFrame(h);
        var HO = new Vector3(0f, d.Top + HeadGap + h.Size.y * 0.5f, HeadZ);
        var parts = new List<Part>();
        // **편다**(`Relax`) · **덜 줄인다**(`keep` 하한 0.6) — 줄이고 안 편 메시는 면마다 법선이 튀어 툰 명암의 칸이 점처럼 뒤집혔다(사용자: *"점들이 자글자글"*).
        Mesh Sculpt(string name, Func<Vector3, float> sdf, Bounds b, float step, float keep)
        {
            var surf = SurfaceNets(sdf, b, step, sdf, p => p, 0.03f, 4f, Mathf.Max(keep, 0.6f));
            Relax(surf.Verts, surf.Tris, 4);
            return SaveSkull($"Chibi_Fungal_{name}", surf);
        }
        void Add(string name, Mesh mesh, string mat, bool on = true, string line = "OutlineOuter")
            => parts.Add(new Part(name, mesh, Vector3.zero, Vector3.one, mat, on, outerMat: line));

        float T = d.Top;
        var torsoBox = new Bounds(new Vector3(0f, d.Lift + d.TorsoH * 0.5f, 0f), new Vector3(0.62f, d.TorsoH + 0.06f, 0.52f));
        Add("Hips.Fungal.Rag", Sculpt("Rag", FgRag, new Bounds(new Vector3(0f, d.HemY - 0.06f, 0f), new Vector3(0.62f, 0.18f, 0.52f)), 0.005f, 0.4f), "FgFRag");
        Add("Spine.Fungal.Nubs", Sculpt("BodyNubs", FgBodyNubs, torsoBox, 0.004f, 0.4f), "FgFFrillB");
        var rb = new Bounds(new Vector3(-d.LegX, 0.07f, 0.08f), new Vector3(0.24f, 0.16f, 0.36f));
        Add("FootL.Fungal.Roots", Sculpt("RootsL", FgRoots, rb, 0.005f, 0.4f), "FgFRoot");
        rb.center = new Vector3(d.LegX, 0.07f, 0.08f);
        Add("FootR.Fungal.Roots", Sculpt("RootsR", FgRoots, rb, 0.005f, 0.4f), "FgFRoot");

        // 말굽 자리 — 저마다 켜고 끄는 축(`SiteNN` = On · Off). 두 색을 번갈아.
        var sites = FgSites(d);
        for (int i = 0; i < sites.Length; i++)
        {
            var s = sites[i]; string ax = $"Site{i + 1:00}";
            // **목 옆 두 자리(`FgFaceSites`)는 턱에 닿는 맨 위 한 장만 얼굴 덩어리**(11-04 게임 화면, 사용자: *"얼굴에 붙어 있는 저거 딱 한 조각"*) — 몸 조각이면
            // 머리 둘레 굵은 선이 얼굴과 그 장 사이를 갈랐고, 무더기 전체를 얼굴로 했더니 가슴 앞 장마다 굵은 선이 섰다. 같은 축의 둘째 메시(`.On.Top`)로 갈라
            // 그 장만 얼굴 표시(`FgH*`) + 얼굴 선 · 나머지는 몸 그대로. 켜고 끄는 것은 같은 축이라 늘 같이 간다.
            string mat = i % 2 == 0 ? "Frill" : "FrillB", at = FgJoint(new Vector3(s.Cx, s.Y, s.Cz), d);
            bool face = Array.IndexOf(FgFaceSites, i) >= 0;
            int lower = face ? s.N - 1 : s.N;
            Add($"{at}.Pick.{ax}.On@{ax}.!Off", Sculpt(ax, p => s.D(p, 0, lower), s.Box, FungalStep, 0.4f), "FgB" + mat, on: false);
            if (face)
                Add($"{at}.Pick.{ax}.On.Top@{ax}.!Off", Sculpt($"{ax}_Top", p => s.D(p, lower, s.N), s.Box, FungalStep, 0.4f), "FgH" + mat, on: false, line: "OutlineOuterFine");
        }

        // 터진 틈 — 다섯 자리(`WoundN`).
        var wounds = FgWounds(d);
        for (int i = 0; i < wounds.Length; i++)
        {
            var w = wounds[i]; string ax = $"Wound{i + 1}";
            var wb = new Bounds(new Vector3(w.c.x, w.c.y, FgTorsoZ(w.c.x, w.c.y, w.side)), new Vector3(w.r.x * 3f, w.r.y * 3f, 0.16f));
            // 틈은 선 없이(제 선 · 바깥 선 둘 다 — 사용자: *"틈에는 걍 선을 안 넣어도"*).
            Add($"Spine.Pick.{ax}.On.Gill@{ax}.!Off", Sculpt($"{ax}_Gill", p => FgWoundGill(p, w), wb, 0.0035f, 0.4f), "FgFGill", on: false, line: null);
            Add($"Spine.Pick.{ax}.On.Lip@{ax}.!Off",  Sculpt($"{ax}_Lip",  p => FgWoundLip(p, w),  wb, 0.004f, 0.4f),  "FgFFlesh", on: false, line: null);
        }

        // 대표 버섯 중 몸에 붙는 넷 — 목도리 · 큰 버섯(좌우) · 포자 주머니(좌우) · 동충하초.
        var collar = new Frill(HO + V3(0f, -0.12f, -0.17f), V3(0f, 0.45f, 1f), 0.66f, th: 0.013f, k: 7, amp: 0.05f, cup: -0.14f, sq: 0.80f, gill: 0.014f, lobe: 0.14f);
        Add("Spine.Pick.Special.Collar", Sculpt("Collar", p => FgCollar(p, HO, f, collar), new Bounds(HO + V3(0f, -0.10f, -0.20f), V3(1.50f, 1.15f, 0.70f)), 0.0055f, 0.4f), "FgBFrillIn", on: true);
        foreach (var (sx, tag) in new[] { (-1f, "L"), (1f, "R") })
        {
            float s = sx;
            var sb = new Bounds(V3(s * 0.32f, T + 0.36f, -0.03f), V3(0.72f, 0.92f, 0.62f));
            Add($"Spine.Pick.Special.Sprout{tag}.Stalk", Sculpt($"Sprout{tag}_Stalk", p => FgSprout(p, s).stalk, sb, FungalStep, 0.4f), "FgBFlesh", on: false);
            Add($"Spine.Pick.Special.Sprout{tag}.Cap",   Sculpt($"Sprout{tag}_Cap",   p => FgSprout(p, s).cap,   sb, FungalStep, 0.4f), "FgBCap", on: false);
            Add($"Spine.Pick.Special.Sprout{tag}.Gill",  Sculpt($"Sprout{tag}_Gill",  p => FgSprout(p, s).under, sb, 0.004f, 0.4f), "FgBGill", on: false);
            Add($"Spine.Pick.Special.Sprout{tag}.Spot",  Sculpt($"Sprout{tag}_Spot",  p => FgSprout(p, s).spots, sb, 0.004f, 0.4f), "FgBSpot", on: false);
            var pb = new Bounds(V3(s * 0.07f, T - 0.14f, -0.14f), V3(0.62f, 0.56f, 0.40f));
            Add($"Spine.Pick.Special.Puff{tag}.Ball",  Sculpt($"Puff{tag}_Ball",  p => FgPuff(p, s).ball,  pb, 0.005f, 0.4f), "FgBPuff", on: false);
            Add($"Spine.Pick.Special.Puff{tag}.Inner", Sculpt($"Puff{tag}_Inner", p => FgPuff(p, s).inner, pb, 0.005f, 0.4f), "FgBInk", on: false);
        }
        var cb = new Bounds(V3(0f, T + 0.22f, -0.32f), V3(1.30f, 0.90f, 0.95f));
        Add("Spine.Pick.Special.Cordy.Stalk", Sculpt("Cordy_Stalk", p => FgCordy(p).stalk, cb, 0.004f, 0.4f), "FgBCord", on: false);
        Add("Spine.Pick.Special.Cordy.Club",  Sculpt("Cordy_Club",  p => FgCordy(p).club,  cb, 0.004f, 0.4f), "FgBClub", on: false);
        return parts;
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    // 보기 몸 하나 — 종족 데이터가 아직 없어 **어디서도 안 가리키는** 프리팹(`Fungal/Chibi_Fungal`)에 굽는다(드워프와 같은 길).
    static bool DressFungal(Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases)
    {
        var shape = FungalShape;
        var heads = new Dictionary<string, List<Part>> { ["Male"] = FungalHeadParts() };
        var body = FungalBodyParts(DimsOf(shape));
        string fp = PawnPrefabPaths.PathOf(FungalPreview);
        PawnPrefabPaths.Ensure(PawnPrefabPaths.FolderOf(FungalPreview));
        if (System.IO.File.Exists(fp)) { System.IO.File.Copy(Source, fp, true); AssetDatabase.ImportAsset(fp, ImportAssetOptions.ForceUpdate); }
        else                           { AssetDatabase.CopyAsset(Source, fp); AssetDatabase.ImportAsset(fp); }
        return Dress(fp, shape, heads, FungalMaterials(mats), atlases, isSource: false, FungalLook, extra: body);
    }

    // **감염자만 다시 굽는다** — 드워프의 `Pawn/10`과 같은 까닭(다른 폰은 안 건드린다).
    [MenuItem("DungeonColony/Pawn/11. Create Fungal Only (감염자만 — 다른 폰은 안 건드린다)")]
    public static void CreateFungalOnly()
    {
        var toon = Shader.Find(ToonShader); var face = Shader.Find(FaceShader); var outer = Shader.Find(OuterShader);
        if (toon == null || face == null || outer == null) { Debug.LogError("[감염자] 셰이더를 못 찾았다."); return; }
        var atlases = new Dictionary<string, ChibiFaceAtlas>();
        foreach (var kind in new[] { "Eyes", "Mouths" })
        {
            var a = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>($"{MatDir}/Chibi_{kind}Atlas.asset");
            if (a == null) { Debug.LogError($"[감염자] 얼굴 아틀라스({kind})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return; }
            atlases[kind] = a;
        }
        var mats = EnsureMaterials(toon, face, outer, atlases);
        _saved.Clear();
        Heads = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        bool ok = DressFungal(mats, atlases);
        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!_saved.Contains(path) && System.IO.Path.GetFileName(path).StartsWith("Chibi_Fungal_")) AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.SaveAssets();
        if (ok) Debug.Log($"[감염자] {PawnPrefabPaths.PathOf(FungalPreview)} — 머리 · 몸 · 버섯만 다시 구웠다(다른 폰은 그대로).");
    }

    // 고르는 몫 — 자리 · 틈은 켜질 몫, 대표 버섯은 종류마다 1/6(좌우가 있는 둘은 반씩). 얼굴 · 얼룩 · 뒤통수 무더기는 똑같이 나눈다.
    static void FungalLook(GameObject root)
    {
        var look = root.GetComponent<PawnLook>();
        if (look == null) return;
        var so = new SerializedObject(look);
        foreach (var fld in new[] { "shapeOverride", "hairOverride", "earOverride", "noseOverride", "tuskOverride" })
            so.FindProperty(fld).stringValue = "";
        so.FindProperty("mouthOverride").stringValue = PawnLook.None;
        so.FindProperty("hairOverride").stringValue = PawnLook.Bald;
        so.FindProperty("earOverride").stringValue = PawnLook.None;
        so.FindProperty("fittedTo").stringValue = "";
        so.ApplyModifiedPropertiesWithoutUndo();

        var ch = new List<PawnLook.PickChance>();
        for (int i = 1; i <= 18; i++) ch.Add(new PawnLook.PickChance($"Site{i:00}", "On", FungalSiteChance));
        for (int i = 1; i <= 5; i++)  ch.Add(new PawnLook.PickChance($"Wound{i}", "On", FungalWoundChance));
        const float one = 1f / 6f;
        foreach (var (opt, c) in new[] { ("Collar", one), ("Ink", one), ("Mouth", one), ("Cordy", one), ("SproutL", one * 0.5f), ("SproutR", one * 0.5f), ("PuffL", one * 0.5f), ("PuffR", one * 0.5f) })
            ch.Add(new PawnLook.PickChance("Special", opt, c));
        look.SetPickChances(ch);
        EditorUtility.SetDirty(look);
        look.Apply();
        FungalBodyColors(root);
    }

    // ── 몸에 번진 살 — 정점 색 ─────────────────────────────────────────────
    //
    // 1 = 회녹 살 · 0 = 황갈(다리 · 틈 색). 다리에서 배 위로 혀처럼 번져 오르고 경계가 들쭉날쭉 · 몸통엔 찌그러진 황갈 얼룩 · 허벅지엔 살 얼룩(미리보기 `fleshness` v29).
    static float SStep(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
    static float FgFleshness(Vector3 p, BodyDims d)
    {
        float n = 0.5f * Mathf.Sin(p.x * 23f + p.y * 7f) + 0.3f * Mathf.Sin(p.z * 31f - p.y * 17f + 1.3f) + 0.2f * Mathf.Sin((p.x + p.z) * 47f + p.y * 29f);
        float tongue = 0.10f * Mathf.Sin(Mathf.Atan2(p.x, p.z) * 3f + 0.7f);
        float f = SStep(d.HemY + 0.04f, d.HemY + 0.30f, p.y + 0.10f * n - tongue);
        float T = d.Top;
        foreach (var (c, r) in new[] { (V3(0.12f, T - 0.14f, 0.15f), V3(0.07f, 0.13f, 0.10f)), (V3(-0.14f, T - 0.30f, -0.15f), V3(0.10f, 0.07f, 0.10f)),
                                       (V3(-d.ArmX, d.HandY + 0.06f, 0f), V3(0.08f, 0.10f, 0.08f)) })
            f *= SStep(0.55f, 1f, Vector3.Scale(p - c, new Vector3(1f / r.x, 1f / r.y, 1f / r.z)).magnitude + 0.35f * n);
        foreach (var (c, r) in new[] { (V3(d.LegX, 0.40f, 0.08f), V3(0.07f, 0.10f, 0.07f)), (V3(-d.LegX, 0.22f, -0.08f), V3(0.07f, 0.09f, 0.07f)) })
            f = Mathf.Max(f, 1f - SStep(0.55f, 1f, Vector3.Scale(p - c, new Vector3(1f / r.x, 1f / r.y, 1f / r.z)).magnitude + 0.35f * n));
        return f;
    }

    // 몸 메시(제 것 — `FungalShape`)에 정점 색을 적는다. 재질은 황갈 바탕 × 정점 색이라 색은 (원하는 색 ÷ 황갈) — 선형 색 공간이면 선형 값으로 나눈다.
    // 정점 자리는 Model 좌표(몸은 Model 바로 아래 제자리 · 팔을 내린 바인드 자세).
    static void FungalBodyColors(GameObject root)
    {
        var body = root.transform.Find("Model/Body");
        if (body == null || !body.TryGetComponent(out SkinnedMeshRenderer smr) || smr.sharedMesh == null) { Debug.LogWarning("[감염자] 몸 메시를 못 찾아 살 얼룩을 못 적었다."); return; }
        var mesh = smr.sharedMesh;
        var d = DimsOf(FungalShape);
        bool lin = PlayerSettings.colorSpace == ColorSpace.Linear;
        Color baseC = lin ? FcPants.linear : FcPants;
        var verts = mesh.vertices; var cols = new Color[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var want = Color.Lerp(FcPants, FcFlesh, FgFleshness(verts[i], d));
            if (lin) want = want.linear;
            cols[i] = new Color(want.r / baseC.r, want.g / baseC.g, want.b / baseC.b, 1f);
        }
        mesh.colors = cols;
        EditorUtility.SetDirty(mesh);
    }
}
