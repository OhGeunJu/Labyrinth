using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **엘프 — 사람 치비 몸(긴 다리) + 좁힌 사람 머리 · 뾰족귀 · 긴 머리**(2026-11-03~04, 사용자: *"엘프는 뾰족귀에 길고 비율좋은"*).
//
// 몸은 사람 몸(`DressSkinned`)에 체형 한 줄(`ElfShape` — 다리 1.37 · 몸통 1.05 · 팔 1.12 · 키 1.85m(머리 2.69개) — 11-06 *"키가 너무 크네"* · *"키 185로"*).
// **반폭 · 깊이 · 손은 사람 남성 그대로**(11-05 사용자: *"몸이 너무 얇아진 거 같아 … 일반몸에서 길이만 좀 늘어나는 거"* — 처음엔 좁은 반폭이었다).
// 머리는 **넓은 네모 머리(`Male_BroadSquare` — 드워프와 같은 바탕)를 옆으로 좁혀(`ElfNar` 0.88 — 원래 폭은 "빵떡")** 곧은 코를 얹는다.
// 눈은 드워프처럼 빚은 먹 눈 + 반짝임(흰자 · 홍채는 *"고양이 같다"*) · 눈썹은 살갗에 칠한 아치 띠(솟은 가닥은 붙인 것 같다).
//
// 개체마다 고르는 것(`PawnLook`의 `Head.Pick.{축}.{벌}`):
//   - **귀**(Leaf · Long · Sweep — 사용자: *"귀는 셋 다"*). 귀는 머리 살에 녹아 붙어 있던 것을 조각으로 떼었다 — 녹인 이음은 귀 조각이 들고,
//     머리 살과 겹치는 면은 2mm 안으로 숨긴다(`ElfEarPiece`).
//   - **머리카락**(Straight · HalfUp · Tail · BangsM · BangsArch — A자 `Bangs`는 11-06 사용자가 뺐다: 일자 · 둥근 안도 *"셋다 별로"*). 머리카락은 귀 모양대로 파여 귀가 숱 위로 나온다(`ElfEarGap` —
//     귀 둘레를 공으로 파면 *"동그랗게 파여 있다"*) — 그래서 **귀마다 한 벌**(`@Ear.{귀}`). 반묶음의 땋은 장식은 한 톤 어둡게(`.Plait` ·
//     `MarkingLook`) · 쇠붙이(`.Metal`)는 안 칠한다.
// 털 색은 `MarkingLook`(머리카락 · 눈썹이 한 색) — 후보는 종족 `Resources/Data/Units/Elf`(금발 · 백금 · 은발 …)가 든다.
//
// 그림 확인은 파이썬 미리보기 `Tools/ModelLab/elf/elf.py`(머리 v16 · 앞머리 v20 · v23) · `elf_body.py`(몸 A)로 했다 —
// 같은 수식(단위 m · 원점은 머리 한복판 · +z 앞). **수치를 고치면 그 그림과 어긋난다 — 같이 고칠 것.**
public static partial class ChibiPawnCreator
{
    // ── 몸 ──────────────────────────────────────────────────────────────────
    //                                                             밑     엉덩이  허리   가슴   어깨      깊이   손     팔K    다리K  몸통K
    static readonly Shape ElfShape = new("Elf", "엘프", "Male", new[] { 0.21f, 0.24f, 0.26f, 0.30f, 0.31f }, 0.82f, 0.30f, 1.12f, 1.37f, 1.05f);

    const float ElfNar = 0.88f;                  // 얼굴 폭 — 사람 머리를 옆으로 좁힌다
    const string ElfPreview = "Chibi_Elf";       // 보기 몸 프리팹(`3_Prefab/Pawn/Elf/`)
    const string ElfBasis = "Male_BroadSquare";
    const string ElfData = "Assets/Resources/Data/Units/Elf.asset";

    // **엘프 종족 — 없으면 사람을 바탕으로 만든다**(2026-11-06, 사용자: *"엘프하면 노랑 머리색인데 그게 없어"*). 털 색 · 살색 · 이름은
    // **비어 있을 때만** 채운다 — 그 뒤로는 에셋이 답이다(인스펙터에서 고친 것을 다시 굽기가 되돌리지 않는다).
    static readonly Color32[] ElfHairTones =
    {
        new(0xD2, 0xA8, 0x4E, 0xFF),   // 금발
        new(0xE6, 0xD0, 0x92, 0xFF),   // 옅은 금발
        new(0xEE, 0xE7, 0xD2, 0xFF),   // 백금
        new(0xC4, 0xC8, 0xCE, 0xFF),   // 은발
        new(0xB8, 0x8A, 0x45, 0xFF),   // 꿀색
        new(0x6B, 0x46, 0x2A, 0xFF),   // 갈색(드물게)
    };
    static readonly Color32[] ElfSkinTones =
    {
        new(0xF4, 0xDC, 0xC8, 0xFF), new(0xEC, 0xCD, 0xB4, 0xFF), new(0xDE, 0xBA, 0x9E, 0xFF), new(0xC8, 0x9E, 0x80, 0xFF),
    };
    static readonly string[] ElfMaleNames   = { "아엘렌", "실바렌", "카엘린", "텔라르", "엘리안", "피에렌", "아론딜", "세레안", "알렌디스", "나이렌", "이살렌" };
    static readonly string[] ElfFemaleNames = { "이리엘", "미렐", "나엘라", "시에나", "아리안", "엘레스", "라이엔", "셀린", "아에라", "피오넬" };

    static UnitData EnsureElfData()
    {
        var d = AssetDatabase.LoadAssetAtPath<UnitData>(ElfData);
        if (d == null)
        {
            var human = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Resources/Data/Units/Human.asset");
            if (human == null) { Debug.LogWarning("[엘프] 바탕 종족(사람)이 없다 — 종족 데이터를 못 만든다."); return null; }
            d = ScriptableObject.CreateInstance<UnitData>();
            EditorUtility.CopySerialized(human, d);   // 사람의 나머지 값(성장 · 도축 · 배경)을 바탕으로
            d.Id = "Elf"; d.RaceName = "엘프";
            d.NamePool = new List<string>();
            d.MaleNames = new List<string>(ElfMaleNames);
            d.FemaleNames = new List<string>(ElfFemaleNames);
            d.HairTones = new List<Color>(); d.SkinTones = new List<Color>();
            AssetDatabase.CreateAsset(d, ElfData);
        }
        bool dirty = false;
        if (d.HairTones == null || d.HairTones.Count == 0) { d.HairTones = new List<Color>(Array.ConvertAll(ElfHairTones, c => (Color)c)); dirty = true; }
        if (d.SkinTones == null || d.SkinTones.Count == 0) { d.SkinTones = new List<Color>(Array.ConvertAll(ElfSkinTones, c => (Color)c)); dirty = true; }
        if (dirty) { EditorUtility.SetDirty(d); AssetDatabase.SaveAssets(); }
        return d;
    }
    const float ElfHeadStep = 0.006f, ElfFineStep = 0.0025f, ElfHairStep = 0.0055f;

    static readonly string[] ElfEars  = { "Leaf", "Long", "Sweep" };
    static readonly string[] ElfHairs = { "Straight", "HalfUp", "Tail", "BangsM", "BangsArch" };

    static List<Part> _elfHead;

    // 귀 — 잎 모양 판(길이, 밖, 위, 뒤, 뿌리 반폭). 뿌리는 머리 옆 귀 자리, 끝은 (밖 · 위 · 뒤)로.
    static readonly Dictionary<string, (float L, float ox, float uy, float bz, float w0)> ElfEarSpecs = new()
    {
        ["Leaf"]  = (0.19f, 0.62f, 0.70f, 0.45f, 0.052f),   // 짧은 잎 — 머리 옆선 위로 끝만 솟는다
        ["Long"]  = (0.27f, 0.60f, 0.62f, 0.50f, 0.058f),   // 길게 위 · 뒤로
        ["Sweep"] = (0.40f, 0.55f, 0.38f, 0.75f, 0.060f),   // 뒤로 길게 뻗는다 — 거의 눕는다
    };

    // 엘프 머리의 틀 — 드워프 틀(오크 틀 + 얼굴 앞면 z)에 좁힌 눈 간격 · 귀 높이의 반폭 · 머리 겉면 위의 점(한 번만 잰다).
    sealed class ElfFrame
    {
        public readonly DwarfFrame D;
        public readonly float EY, MY, NY, EX, EXn, ER, ZE, ZN, ZM, R0;
        public readonly Vector3 Curl0, Curl1;
        public readonly Dictionary<float, Vector3[]> BackFalls = new();   // 길이 → 각도마다 가닥 마디
        public readonly Vector3[] HalfUpPlait;
        public readonly Vector3[] FrontLockPts;          // 앞 판 마디(옆으로 늘인 좌표)
        public readonly Vector3[][] TailPaths;           // 꽁지 다발 여섯

        // 몸(팔을 내린 묶은 자세) — 머리카락이 비켜 갈 몸통 · 위팔. 머리 좌표 y + `BodyY0` = 몸 좌표 y · 몸 z = 머리 z + `HeadZ`.
        readonly BodyDims B; readonly float BodyY0;

        public ElfFrame(HeadShape h, BodyDims body)
        {
            B = body; BodyY0 = body.Top + HeadGap + h.Size.y * 0.5f;
            D = new DwarfFrame(h);
            EY = D.EY; MY = D.MY; NY = D.NY; EX = D.EX; ER = D.ER; ZE = D.ZE; ZN = D.ZN; ZM = D.ZM;
            EXn = EX * 0.94f;                                  // 눈은 덜 좁힌다 — 좁은 얼굴에 눈이 몰리면 어려 보인다
            R0 = D.O.R(D.O.EyeT) * D.O.W * ElfNar;             // 귀 높이의 머리 옆 반폭
            Curl0 = OnCap(0.15f, EY + 0.17f, 0.25f, 0.010f);
            Curl1 = OnCap(0.21f, EY + 0.07f, 0.19f, 0.012f);
            foreach (float L in new[] { -0.80f, -0.72f }) BackFalls[L] = BackFallPts(L);
            var path = new[] { V3(0.20f, EY + 0.17f, 0.12f), V3(0.26f, EY + 0.15f, 0.02f), V3(0.27f, EY + 0.13f, -0.10f),
                               V3(0.22f, EY + 0.11f, -0.22f), V3(0.13f, EY + 0.09f, -0.30f), V3(0.03f, EY + 0.07f, -0.33f) };
            HalfUpPlait = Array.ConvertAll(path, t => OnCap(t.x, t.y, t.z, 0.062f));
            float x1 = Curl1.x, z1 = Curl1.z; var sc = V3(ElfPanelF, 1, 1);
            // 앞 판의 아래 두 마디는 몸 앞으로 비킨다(`OffBody` — 판은 앞뒤로 넓어 반지름을 크게 잡는다).
            FrontLockPts = Array.ConvertAll(new[] { Curl0, Curl1, V3(x1 + 0.020f, EY - 0.08f, z1 - 0.04f),
                                                    OffBody(V3(x1 + 0.030f, MY - 0.12f, z1 - 0.07f), 0.08f, front: true, anyHeight: true),
                                                    OffBody(V3(x1 + 0.030f, -0.62f, z1 - 0.10f), 0.08f, front: true) },
                                            t => Vector3.Scale(t, sc));
            var tails = new List<Vector3[]>();
            foreach (var (dx, dz, s) in new[] { (-0.05f, 0.00f, 0.88f), (-0.025f, -0.03f, 1.0f), (0.0f, 0.02f, 1.06f), (0.025f, -0.02f, 1.0f), (0.05f, 0.01f, 0.88f), (0.0f, -0.05f, 0.95f) })
                tails.Add(Bez(ElfTie, ElfTie + V3(dx * 0.3f, 0.13f, -0.05f), ElfTie + V3(dx * 0.9f, 0.10f, -0.34f), ElfTie + V3(dx * 2.0f, -0.90f * s, -0.20f + dz), 8));   // 끈에서 위로 솟았다가 휘어 내린다
            TailPaths = tails.ToArray();
        }

        // **머리카락 마디를 몸 밖으로**(2026-11-06, 사용자: *"엘프 머리카락에 외곽선땜에 어깨쪽이 좀 이상하게 보여"*) — 몸을 사람 폭으로
        // 넓힌 뒤로(`ElfShape`) 귀 뒤로 곧게 떨어지던 가닥이 위팔(x 0.19~0.32)을 꿰뚫어, 겹친 자리에 외곽선이 지저분하게 번졌다.
        // 위팔(위팔 관절에서 곧게 내린 통)을 앞 판은 앞으로 · 뒷머리는 뒤로 비키고, 몸통(타원 단면)은 바깥으로 비킨다. `r` — 마디 반지름(+ 틈 12mm).
        // `anyHeight` — 위팔 위(목 높이) 마디도 민다. 몸은 움직이니(팔 휘두르기) 머리카락을 몸 모양으로 파내지는 않는다 — 마디만 옮긴다.
        Vector3 OffBody(Vector3 q, float r, bool front, bool anyHeight = false)
        {
            const float Gap = 0.012f;
            float my = q.y + BodyY0;
            if (anyHeight || my < B.ShoulderY + B.ArmR + r)
            {
                float need = B.ArmR + r + Gap, dx = Mathf.Abs(q.x) - B.ArmX;
                if (Mathf.Abs(dx) < need)
                {
                    float dz = Mathf.Sqrt(need * need - dx * dx), az = -HeadZ;
                    q.z = front ? Mathf.Max(q.z, az + dz) : Mathf.Min(q.z, az - dz);
                }
            }
            if (my > B.Lift && my < B.Top + r)
            {
                float w = BodyTorsoRadius(B.Half, Mathf.Clamp01((my - B.Lift) / B.TorsoH), B.TorsoH, B.TopR);
                float rx = w + r + Gap, rz = w * B.Depth + r + Gap, zz = q.z + HeadZ;
                float e = Mathf.Sqrt(q.x * q.x / (rx * rx) + zz * zz / (rz * rz));
                if (e < 1f && e > 1e-4f) { q.x /= e; q.z = zz / e - HeadZ; }
            }
            return q;
        }

        public float Head(Vector3 p) => OrcBase(V3(p.x / ElfNar, p.y, p.z), D.O) * ElfNar;

        // 머리 겉면(+ off) 위의 점 — 가로로 밀어 맞춘다.
        public Vector3 OnCap(float x, float y, float z, float off)
        {
            float lo = 0.5f, hi = 2f;
            for (int i = 0; i < 30; i++)
            {
                float s = (lo + hi) * 0.5f;
                if (Head(V3(x * s, y, z * s)) < off) lo = s; else hi = s;
            }
            return V3(x * lo, y, z * lo);
        }

        // 둘레 각도 a(앞 0 · 정뒤 180)에서 숱 겉면이 가장 바깥인 자리 — 가닥은 거기서 곧게 떨어진다.
        Vector3 Column(float a)
        {
            float dx = Mathf.Sin(a * Mathf.Deg2Rad), dz = Mathf.Cos(a * Mathf.Deg2Rad);
            Vector3 best = default; bool any = false;
            for (int i = 0; i < 13; i++)
            {
                float y = -0.12f + 0.24f * i / 12f;
                var q = OnCap(dx * 0.3f, y, dz * 0.3f, 0.012f);
                if (!any || new Vector2(q.x, q.z).magnitude > new Vector2(best.x, best.z).magnitude) { best = q; any = true; }
            }
            return best;
        }

        // 귀 뒤(100°)부터 정뒤(180°)까지 아홉 가닥 — 각 가닥 마디 넷(위는 숱 속 · 가운데가 길고 귀 뒤로 갈수록 짧다).
        Vector3[] BackFallPts(float L)
        {
            var all = new Vector3[9 * 4];
            for (int k = 0; k < 9; k++)
            {
                float a = 100f + 10f * k;
                float dx = Mathf.Sin(a * Mathf.Deg2Rad), dz = Mathf.Cos(a * Mathf.Deg2Rad); var nrm = V3(dx, 0, dz);
                var c = Column(a);
                float yEnd = L + 0.14f * Mathf.Pow((180f - a) / 80f, 2f);
                all[k * 4 + 0] = OnCap(dx * 0.3f, 0.17f, dz * 0.3f, -0.004f);
                all[k * 4 + 1] = V3(c.x, 0.04f, c.z);
                // 아래 두 마디는 몸 뒤로 비킨다(`OffBody`) — 위 마디(목 높이)는 위팔 위라도 밀어 둔다(아래 마디까지 곧게 내리면 그 사이가 어깨를 뚫는다).
                all[k * 4 + 2] = OffBody(V3(c.x, -0.30f, c.z) + nrm * 0.004f, 0.05f, front: false, anyHeight: true);
                all[k * 4 + 3] = OffBody(V3(c.x, yEnd, c.z) + nrm * 0.012f, 0.04f, front: false);
            }
            return all;
        }
    }

    // ── 도구 ────────────────────────────────────────────────────────────────

    // `Interp`(np.interp) · `Bez`는 여성 오크의 것(`ChibiPawnCreator.OrcFemale`)을 같이 쓴다 — 같은 식이다.

    static float LockR(Vector3 p, Vector3[] pts, float[] radii, float k = 0.006f)   // 마디마다 굵기를 주는 다발
    {
        float d = 9f;
        for (int i = 0; i < pts.Length - 1; i++) d = SMin(d, Seg(p, pts[i], pts[i + 1], radii[i], radii[i + 1]), k);
        return d;
    }

    // 납작한 가닥 — 바깥 방향(n)으로 얇다(옆쪽 가닥도 판처럼 붙게).
    static float NLock(Vector3 p, Vector3[] pts, int from, float r0, float r1, Vector3 n, float F = 2.2f)
    {
        float g = F - 1f;
        var q = p + g * Vector3.Dot(p, n) * n;
        float d = 9f; var a = pts[from] + g * Vector3.Dot(pts[from], n) * n;
        for (int i = 0; i < 3; i++)   // 마디 넷(`Lock`과 같은 굵기 셈)
        {
            var b = pts[from + i + 1] + g * Vector3.Dot(pts[from + i + 1], n) * n;
            d = SMin(d, Seg(q, a, b, r0 + (r1 - r0) * i / 3f, r0 + (r1 - r0) * (i + 1) / 3f), 0.02f);
            a = b;
        }
        return d / F;
    }

    // ── 얼굴 ────────────────────────────────────────────────────────────────

    static float ElfNose(Vector3 p, ElfFrame f)   // 곧고 가는 콧대 · 작은 끝
    {
        float bridge = Seg(p, V3(0, f.EY + 0.01f, f.ZE), V3(0, f.NY, f.ZN + 0.030f), 0.012f, 0.020f);
        float tip = Ell(p, V3(0, f.NY - 0.004f, f.ZN + 0.028f), V3(0.026f, 0.020f, 0.022f));
        return SMin(bridge, tip, 0.012f);
    }

    static float ElfFace(Vector3 p, ElfFrame f) => SMin(f.Head(p), ElfNose(p, f), 0.012f);

    // 귀 — 잎 모양 판. 판의 두께 방향은 축과 '앞'에 수직(조금 밖으로 기울여 정면에서도 판이 보이게).
    static float ElfEarBlade(Vector3 p, ElfFrame f, (float L, float ox, float uy, float bz, float w0) e)
    {
        var m = Mirror(p);
        var root = V3(f.R0 - 0.02f, f.EY - 0.025f, -0.035f);
        var u = V3(e.ox, e.uy, -e.bz).normalized;
        var n = Vector3.Cross(Vector3.Cross(u, Vector3.forward), u).normalized;
        n = n * 0.55f + Vector3.right * 0.45f; n -= u * Vector3.Dot(n, u); n.Normalize();
        var w = Vector3.Cross(n, u);
        var q = m - root;
        float s = Vector3.Dot(q, u), a = Vector3.Dot(q, w), c = Vector3.Dot(q, n);
        float t = Mathf.Clamp01(s / e.L);
        float half = e.w0 * Mathf.Sqrt(Mathf.Max(1f - t, 0f)) * (0.55f + 0.45f * Mathf.Sin(Mathf.Clamp(t * 1.6f + 0.35f, 0f, Mathf.PI)));   // 밑에서 부풀고 끝은 뾰족
        a += 0.25f * e.w0 * t * t;                                                                                                      // 끝이 살짝 위로 휜다
        float d2 = Mathf.Max(Mathf.Abs(a) - half, Mathf.Max(-s, s - e.L));
        float d = SMax(d2, Mathf.Abs(c + 0.010f * Mathf.Sin(t * Mathf.PI)) - (0.014f + 0.008f * (1f - t)), 0.006f);
        float cup = SMax(Mathf.Abs(a + 0.004f) - half * 0.55f, Mathf.Max(0.02f - s, s - e.L * 0.80f), 0.004f);   // 귓바퀴 안 오목
        cup = SMax(cup, -(c - 0.004f), 0.004f);
        return SMax(d, -cup, 0.004f);
    }

    // 귀 조각 — 미리보기는 귀를 머리 살에 녹였다(0.018). 녹인 이음까지 귀가 들고, 머리 살과 겹치는 면은 2mm 안으로 숨긴다
    // (같은 자리에 면 둘이면 번갈아 비친다) · 머리 속은 덜어 낸다 · 귀 둘레 3cm 안만.
    static float ElfEarPiece(Vector3 p, ElfFrame f, (float, float, float, float, float) e)
    {
        float ear = ElfEarBlade(p, f, e);
        if (ear > 0.08f) return ear;
        float face = ElfFace(p, f);
        float d = SMin(face + 0.002f, ear, 0.018f);
        d = SMax(d, -(face + 0.006f), 0.002f);
        return SMax(d, ear - 0.03f, 0.005f);
    }

    const float ElfBrowAsp = 1.161895f;   // √1.35 — 눈썹 높이를 잡는 눈 세로 비(`ChibiFaceLayout.DefaultEyeAspect`)

    static float ElfEyes(Vector3 p, ElfFrame f)    // 드워프 눈 느낌 — 진한 먹 눈 하나 + 반짝임
        => Ell(Mirror(p), V3(f.EXn, f.EY - 0.004f, f.ZE - 0.014f), V3(f.ER * 0.82f, f.ER * 0.92f, 0.02f));
    static float ElfGlint(Vector3 p, ElfFrame f)
        => Ell(Mirror(p), V3(f.EXn - f.ER * 0.24f, f.EY + f.ER * 0.22f, f.ZE - 0.004f), V3(f.ER * 0.24f, f.ER * 0.24f, 0.02f));

    static float ElfBrow(Vector3 p, ElfFrame f)    // 살갗에 칠한 눈썹 — 얼굴 곡면을 따라가는 얇은 띠 · 완만한 아치 · 안쪽 굵고 바깥 가늘게
    {
        var m = Mirror(p);
        float x0 = 0.055f, x1 = f.EXn + f.ER * 1.45f;
        float t = Mathf.Clamp01((m.x - x0) / (x1 - x0));
        float yc = f.EY + f.ER / ElfBrowAsp + 0.045f + 0.018f * Mathf.Sin(t * Mathf.PI * 0.9f) - 0.006f * t;
        float th = 0.011f * (1f - t) + 0.004f * t;
        float band = Mathf.Max(Mathf.Abs(m.y - yc) - th, Mathf.Max(x0 - m.x, m.x - x1));
        if (band > 0.03f) return band;
        float shell = Mathf.Abs(f.Head(p) - 0.002f) - 0.004f;
        return SMax(SMax(band, shell, 0.002f), -m.z, 0.002f);
    }

    static float ElfMouth(Vector3 p, ElfFrame f) => RBox(p, V3(0, f.MY + 0.005f, f.ZM + 0.006f), V3(0.045f, 0.005f, 0.015f), 0.004f);   // 작은 입 — 얇은 선

    // ── 머리카락 ────────────────────────────────────────────────────────────

    // 숱 = 머리를 가운데에서 고르게 키운 껍데기. `머리 - 두께`로 부풀리면 정수리(원뿔꼴 꼭대기)에서는 회전축 위에서만 먹어 가운데만 바늘처럼
    // 솟는다(드워프 `Scalp`는 정수리를 따로 누른다 — 여기는 미리보기대로 키운다).
    static float ElfScalp(Vector3 p, ElfFrame f, float th, float low)
    {
        float s = 1f + th / 0.30f;
        return SMax(f.Head(p / s) * s, low - p.y, 0.02f);
    }

    static readonly float[] ElfGapShifts = { -0.064f, -0.048f, -0.032f, -0.016f, 0f, 0.016f };   // 음수가 바깥

    // 귀가 머리카락을 뚫고 나온다 — 귀 판을 옆으로 밀어 낸 자국(바깥 · 안)까지 덜어 귀 모양 그대로 갈라진다.
    static float ElfEarGap(Vector3 p, float d, ElfFrame f, (float, float, float, float, float) e)
    {
        float sx = Mathf.Sign(p.x);
        if (p.x == 0f) sx = 0f;
        float cut = 9f;
        foreach (float k in ElfGapShifts)
            cut = Mathf.Min(cut, ElfEarBlade(V3(p.x + sx * k, p.y, p.z), f, e));
        return SMax(d, -(cut - 0.004f), 0.004f);
    }

    static float ElfPart(Vector3 p)   // 가운데 가르마 — 정수리 앞에 얕은 골(더한다 · 빼면 등줄기가 솟는다)
        => p.z > -0.05f && p.y > 0.15f ? 0.012f * Mathf.Exp(-(p.x / 0.012f) * (p.x / 0.012f)) : 0f;

    static float ElfFrontLine(float ang, ElfFrame f)   // 이마 → 관자놀이 → 귀 뒤 → 목덜미(두 번째 머리 · 꽁지)
    {
        float top = f.EY + 0.25f, side = f.EY + 0.03f, nape = -0.28f;
        return ang < 85f ? top + (side - top) * Ease((ang - 15f) / 70f) : side + (nape - side) * Ease((ang - 100f) / 60f);
    }

    // 생머리 — 가르마에서 이마 양쪽으로 둥글게 흘러내리다 관자놀이에서 꺾이고, 귀 앞은 구레나룻으로 턱께까지(수평이면 맨살 띠가 남는다).
    static float ElfCurtainLine(float ang, ElfFrame f)
    {
        float top = f.EY + 0.26f, temple = f.EY + 0.05f, burn = f.EY - 0.12f, nape = -0.28f;
        float fr = ang < 50f ? top + (temple - top) * Mathf.Pow(Ease(ang / 50f), 0.8f) : temple + (burn - temple) * Ease((ang - 50f) / 38f);
        return ang < 90f ? fr : burn + (nape - burn) * Ease((ang - 90f) / 70f);
    }

    // 뒤로 늘어진 생머리 — 숱 겉면에 붙어 귀 뒤까지 두르고, 정수리 곡선 그대로 곧게 떨어진다.
    static float ElfBackFall(Vector3 p, ElfFrame f, float L = -0.80f)
    {
        var m = Mirror(p); var pts = f.BackFalls[L]; float d = 9f;
        for (int k = 0; k < 9; k++)
        {
            float a = 100f + 10f * k;
            var nrm = V3(Mathf.Sin(a * Mathf.Deg2Rad), 0, Mathf.Cos(a * Mathf.Deg2Rad));
            d = SMin(d, NLock(k == 8 ? p : m, pts, k * 4, 0.055f, 0.034f, nrm), 0.02f);
        }
        return d;
    }

    // 넓적한 머리 판 — 얼굴 옆(x)으로 얇고 앞뒤로 넓다. 앞 가닥: 이마 곡선의 끝(관자놀이)에서 숱과 이어져 꺾이고 가슴까지 곧게.
    const float ElfPanelF = 1.8f;
    static readonly float[] ElfFrontLockR = Array.ConvertAll(new[] { 0.030f, 0.042f, 0.048f, 0.044f, 0.026f }, r => r * ElfPanelF);
    static float ElfFrontLocks(Vector3 p, ElfFrame f)
    {
        var m = Mirror(p);
        return LockR(V3(m.x * ElfPanelF, m.y, m.z), f.FrontLockPts, ElfFrontLockR, 0.02f) / ElfPanelF;
    }

    // 땋은 가닥 — 길을 따라 누운 마디(가운데가 불룩 · 마디 사이가 잘록).
    static float ElfPlait(Vector3 p, Vector3[] pts, float r0, float r1, int n = 14)
    {
        var s = new float[pts.Length];
        for (int i = 1; i < pts.Length; i++) s[i] = s[i - 1] + (pts[i] - pts[i - 1]).magnitude;
        Vector3 At(float u)
        {
            for (int i = 1; i < pts.Length; i++)
                if (u <= s[i]) return Vector3.Lerp(pts[i - 1], pts[i], (u - s[i - 1]) / (s[i] - s[i - 1]));
            return pts[^1];
        }
        float d = 9f;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = At(s[^1] * (2 * i) / (2 * n)), c = At(s[^1] * (2 * i + 1) / (2 * n)), b = At(s[^1] * (2 * i + 2) / (2 * n));
            float r = r0 + (r1 - r0) * i / Mathf.Max(n - 1, 1);
            d = SMin(d, Mathf.Min(Seg(p, a, c, r * 0.62f, r), Seg(p, c, b, r, r * 0.62f)), 0.004f);
        }
        return d;
    }

    // 꽁지 굵기 — 끈 밑에서 가늘게 나와 부풀었다가 끝으로 준다(여성 오크 꼬리와 같은 표).
    static readonly float[] ElfTailR = { 0.030f, 0.050f, 0.074f, 0.080f, 0.070f, 0.056f, 0.042f, 0.026f, 0.012f };
    static readonly Vector3 ElfTie = new(0f, 0.27f, -0.31f);

    // 커튼 앞머리의 아랫단 — 이마를 감싸는 매끈한 한 장(다발을 띄워 붙이면 옆에서 볼록볼록 혹이 선다). 두께는 뿌리에서 숱과 같고 아랫단으로 얇아진다.
    static float ElfHem(string style, float ang, ElfFrame f)
    {
        var (xs, ys) = style == "BangsArch" ? (HemRx, HemRy) : (HemMx, HemMy);
        return f.EY + Interp(ang, xs, ys);
    }
    // 둘레 각도 → 눈 높이(EY)에서 올린 높이.
    // ∧ — 가르마에서 둥글게 흘러내린다(숱 이마선이 단 아래까지 덮어 이 모양이 된다).
    static readonly float[] HemRx = { 0f, 3f, 8f, 14f, 26f, 38f, 46f, 54f, 64f, 72f };
    static readonly float[] HemRy = { 0.235f, 0.215f, 0.165f, 0.148f, 0.14f, 0.145f, 0.12f, 0.06f, -0.02f, -0.06f };
    // M자 — 이마는 가운데 좁은 세로 틈으로만(위는 거의 평평 · 가운데만 살짝 파인다 · 옆은 곧게 · 아래 끝은 바깥으로 벌어진다).
    static readonly float[] HemMx = { 0f, 2f, 4f, 8f, 11f, 13f, 14.5f, 15.5f, 16.5f, 18f, 21f, 26f, 34f, 45f, 55f, 64f, 72f };
    static readonly float[] HemMy = { 0.24f, 0.25f, 0.262f, 0.265f, 0.26f, 0.245f, 0.22f, 0.18f, 0.13f, 0.10f, 0.08f, 0.07f, 0.065f, 0.065f, 0.04f, -0.02f, -0.06f };

    static float ElfBangs(Vector3 p, string style, ElfFrame f)
    {
        float ang = AngleOf(p); float h = ElfHem(style, ang, f);
        float th = 0.010f + 0.022f * Ease((p.y - h) / 0.10f);
        float d = ElfScalp(p, f, th, h);
        return SMax(d, (ang - 72f) * Mathf.Deg2Rad * 0.28f, 0.01f);   // 앞에만 — 옆은 숱 · 구레나룻이 든다
    }

    // 머리카락(`ElfHairPart`) · 쇠붙이(`ElfMetalPart`) · 땋은 장식 중 **물은 것 하나만** 잰다 — 없으면 9. 머리카락은 귀(`e`) 모양대로 파인다.
    // (2026-11-06 — 셋을 한 번에 재 돌려주면 쇠붙이 · 땋은 장식을 구울 때도 무거운 숱을 매 점 쟀다. 식은 그대로라 결과는 같다.)
    const int ElfHairPart = 0, ElfMetalPart = 1, ElfPlaitPart = 2;
    static float ElfHair(string style, Vector3 p, ElfFrame f, (float, float, float, float, float) e, int part)
    {
        float ang = AngleOf(p);
        switch (style)
        {
            case "Straight":   // 가운데 가르마 긴 생머리 — 뒤는 허리께 · 앞 두 가닥
            {
                if (part != ElfHairPart) return 9f;
                float cap = ElfScalp(p, f, 0.030f, ElfCurtainLine(ang, f)) + ElfPart(p);
                float d = SMin(cap, ElfBackFall(p, f), 0.04f);
                d = SMin(d, ElfFrontLocks(p, f), 0.03f);
                return ElfEarGap(p, d, f, e) + Strands(p, 0.0015f, 55f);
            }
            case "HalfUp":     // 긴 생머리 — 관자놀이에서 땋은 두 가닥이 뒤통수에서 만난다
            {
                var pts = f.HalfUpPlait;
                if (part == ElfPlaitPart) return ElfPlait(Mirror(p), pts, 0.026f, 0.020f);
                if (part == ElfMetalPart) return Ell(p, V3(0f, pts[^1].y, pts[^1].z - 0.012f), V3(0.05f, 0.034f, 0.024f));   // 걸쇠
                float cap = ElfScalp(p, f, 0.030f, ElfFrontLine(ang, f)) + ElfPart(p);
                float d = SMin(cap, ElfBackFall(p, f, -0.72f), 0.04f);
                return ElfEarGap(p, d, f, e) + Strands(p, 0.0012f, 45f);
            }
            case "Tail":       // 뒤로 빗어 넘겨 높이 묶은 꽁지 + 이마 띠(서클릿) · 끝이 말꼬리처럼 갈라진다
            {
                if (part == ElfPlaitPart) return 9f;
                if (part == ElfMetalPart)   // 묶은 고리 · 이마 띠 · 보석
                {
                    float tie = DwarfRing(p, ElfTie + V3(0, 0.01f, 0.005f), ElfTie + V3(0, 0.045f, -0.01f), 0.040f);
                    float circ = SMax(Mathf.Abs(f.Head(p) - 0.034f) - 0.006f, Mathf.Abs(p.y - (f.EY + 0.17f - 0.25f * Mathf.Max(-p.z, 0f))) - 0.014f, 0.003f);
                    float gem = Sph(p, V3(0, f.EY + 0.175f, f.D.O.Front(f.D.O.EyeT + 0.25f) + 0.032f), 0.022f);
                    return Mathf.Min(Mathf.Min(tie, circ), gem);
                }
                float cap = ElfScalp(p, f, 0.028f, ElfFrontLine(ang, f) + 0.02f) + Strands(p, 0.003f, 90f);
                float tail = 9f;
                foreach (var pts in f.TailPaths) tail = SMin(tail, LockR(p, pts, ElfTailR), 0.03f);
                float d = SMin(cap, tail + Strands(p, 0.002f, 60f), 0.03f);
                return ElfEarGap(p, d, f, e);
            }
            default:           // 앞머리 생머리(M · ∧) — 뒤 · 옆은 생머리 그대로, 앞 판 대신 커튼 앞머리
            {
                if (part != ElfHairPart) return 9f;
                float low = ElfCurtainLine(ang, f);
                // M자는 숱 이마선이 단보다 내려오지 않는다 — 내려오면 평평한 단을 가려 다시 비스듬한 A가 된다.
                if (style == "BangsM" && ang < 90f) low = Mathf.Max(low, ElfHem(style, ang, f));
                float cap = ElfScalp(p, f, 0.030f, low) + ElfPart(p);
                float d = SMin(cap, ElfBackFall(p, f), 0.04f);
                d = SMin(d, ElfBangs(p, style, f), 0.015f);
                return ElfEarGap(p, d, f, e) + (ang > 70f ? Strands(p, 0.0012f, 55f) : 0f);   // 앞머리에는 결 골이 없다(선으로 읽힌다)
            }
        }
    }

    // ── 부위 ────────────────────────────────────────────────────────────────

    // 엘프 머리 부위 — 사람 머리(`HeadParts`) 대신 받는다. 머리 크기는 사람 그대로(폭만 거리장에서 좁힌다).
    static List<Part> ElfHeadParts(Dictionary<string, Mesh> hairs)
    {
        if (_elfHead != null) return _elfHead;
        var h = Array.Find(Heads, x => x.Name == ElfBasis);
        var f = new ElfFrame(h, DimsOf(ElfShape));
        var pos = HeadPos(h);
        var parts = new List<Part>();
        Func<Vector3, float> face = p => ElfFace(p, f);

        // `relax` — 살 · 털은 편다(바깥 선의 가시가 안 서게). 작은 눈 · 쇠붙이는 펴면 무너지니 안 편다.
        Mesh Sculpt(string name, Func<Vector3, float> sdf, Bounds b, float step, float keep, bool relax = false)
        {
            var surf = SurfaceNets(sdf, b, step, face, p => p, 0.03f, 4f, keep);
            NetsLap(-1);
            if (relax) Relax(surf.Verts, surf.Tris, 6);
            NetsLap(5);
            var m = SaveSkull($"Chibi_Elf_{name}", surf);
            NetsLap(6);
            return m;
        }
        void Add(string name, Mesh mesh, string mat, string line, bool on = true)
            => parts.Add(new Part(name, mesh, pos, Vector3.one, mat, on, outerMat: line));

        parts.Add(new Part("Head", null, pos, h.Size, "HeadSkin", on: false));

        // 머리 살 — 이름이 머리형이라 `PawnLook`이 이 머리 하나를 고른다. 제 선을 끄고 바깥 선만.
        var hb = new Bounds(new Vector3(0f, -0.01f, 0.04f), new Vector3(h.Size.x + 0.08f, h.Size.y + 0.08f, h.Size.z + 0.24f));
        Add($"Head.Shape.{h.Name}", Sculpt("Head", face, hb, ElfHeadStep, 0.3f, relax: true), "SkinFine", "OutlineOuterFine");

        // 귀 — 셋 중 씨앗이 하나(`Head.Pick.Ear.*`).
        var earB = new Bounds(new Vector3(0f, -0.02f, -0.12f), new Vector3(1.04f, 0.50f, 0.56f));
        foreach (var en in ElfEars)
        {
            var spec = ElfEarSpecs[en];
            Add($"Head.Pick.Ear.{en}", Sculpt($"Ear_{en}", p => ElfEarPiece(p, f, spec), earB, 0.004f, 0.4f, relax: true), "SkinFine", "OutlineOuterFine", on: en == "Leaf");
        }

        // 눈 · 반짝임 · 눈썹 · 입.
        var eyeB = new Bounds(new Vector3(0f, f.EY, f.ZE - 0.01f), new Vector3(2f * (f.EXn + f.ER) + 0.04f, 0.10f, 0.08f));
        Add("Head.Elf.Eyes",  Sculpt("Eyes",  p => ElfEyes(p, f),  eyeB, 0.002f, 0.5f),  "NoseDark", null);
        Add("Head.Elf.Glint", Sculpt("Glint", p => ElfGlint(p, f), eyeB, 0.0015f, 0.5f), "Bone", null);
        var browB = new Bounds(new Vector3(0f, f.EY + 0.08f, f.ZE - 0.04f), new Vector3(0.44f, 0.14f, 0.20f));
        Add("Head.Pick.Brow.Arch", Sculpt("Brow", p => ElfBrow(p, f), browB, 0.0015f, 0.4f), "HairFine", "OutlineOuterFine");
        var mouthB = new Bounds(new Vector3(0f, f.MY + 0.005f, f.ZM + 0.006f), new Vector3(0.12f, 0.04f, 0.06f));
        Add("Head.Elf.Mouth", Sculpt("Mouth", p => ElfMouth(p, f), mouthB, 0.0015f, 0.5f), "ElfLip", null);

        // 머리카락 — 여섯 × 귀 셋(귀 모양대로 파인다). 쇠붙이 · 땋은 장식은 귀를 안 타서 한 벌.
        var hairB = new Bounds(new Vector3(0f, -0.17f, -0.14f), new Vector3(0.86f, 1.44f, 1.20f));
        foreach (var st in ElfHairs)
        {
            string s = st;
            foreach (var en in ElfEars)
            {
                var spec = ElfEarSpecs[en];
                Add($"Head.Pick.Hair.{s}@Ear.{en}", Sculpt($"Hair_{s}_{en}", p => ElfHair(s, p, f, spec, ElfHairPart), hairB, ElfHairStep, 0.35f, relax: true),
                    "HairFine", HangLine, on: s == "Straight" && en == "Leaf");
            }
            var any = ElfEarSpecs["Leaf"];
            if (s is "HalfUp" or "Tail")
                Add($"Head.Pick.Hair.{s}.Metal", Sculpt($"Hair_{s}_Metal", p => ElfHair(s, p, f, any, ElfMetalPart), hairB, ElfFineStep, 0.4f), "Metal", HangLine, on: false);
            if (s == "HalfUp")
                Add($"Head.Pick.Hair.{s}.Plait", Sculpt($"Hair_{s}_Plait", p => ElfHair(s, p, f, any, ElfPlaitPart), hairB, ElfFineStep, 0.4f, relax: true), "HairFine", HangLine, on: false);
        }

        // 투구 — 사람 투구 껍데기 그대로(머리 크기가 사람과 같다).
        var capPos = pos + new Vector3(0f, 0.015f, -0.01f);
        foreach (var fit in HairFits)
            parts.Add(new Part($"Head.Helmet.Cap.Shell.{fit}", hairs[$"{fit}.Helmet"], capPos, h.Size * HelmetScale, "Helmet", on: false));

        _elfHead = parts;
        return parts;
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    // 보기 몸 하나 — 종족 데이터가 아직 없어 **어디서도 안 가리키는** 프리팹(`Elf/Chibi_Elf`)에 굽는다(드워프와 같은 길).
    static long _elfT0; static double _elfHeadSecs;   // 굽기 시간 진단 — 머리를 빚는 데 든 시간(나머지는 몸 · 프리팹)

    static bool DressElf(Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases, Dictionary<string, Mesh> hairs)
    {
        _elfHead = null;
        _elfT0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var parts = new Dictionary<string, List<Part>> { ["Male"] = ElfHeadParts(hairs) };
        string dp = PawnPrefabPaths.PathOf(ElfPreview);
        PawnPrefabPaths.Ensure(PawnPrefabPaths.FolderOf(ElfPreview));
        if (System.IO.File.Exists(dp)) { System.IO.File.Copy(Source, dp, true); AssetDatabase.ImportAsset(dp, ImportAssetOptions.ForceUpdate); }
        else                           { AssetDatabase.CopyAsset(Source, dp); AssetDatabase.ImportAsset(dp); }
        var eMats = OutfitMaterials(mats, "Elf", new Color32(0x47, 0x61, 0x42, 0xFF), new Color32(0x46, 0x3A, 0x2C, 0xFF), null, null);
        eMats["ElfLip"] = Variant(mats["Bone"], "Chibi_ElfLip", "_BaseColor", new Color(0.72f, 0.45f, 0.42f));
        _elfHeadSecs = (System.Diagnostics.Stopwatch.GetTimestamp() - _elfT0) / (double)System.Diagnostics.Stopwatch.Frequency;
        return Dress(dp, ElfShape, parts, eMats, atlases, isSource: false, ElfLook);
    }

    // **엘프만 다시 굽는다**(드워프 `Pawn/10`과 같은 길) — 얼굴 아틀라스 · 투구 껍데기는 구워 둔 것을 읽고(없으면 `Pawn/3` 먼저).
    // 옛 엘프 메시(이번에 안 만든 `Chibi_Elf_`)만 치운다 — 다른 폰의 메시는 안 건드린다.
    [MenuItem("DungeonColony/Pawn/12. Create Elf Only (엘프만 — 다른 폰은 안 건드린다)")]
    public static void CreateElfOnly()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var toon = Shader.Find(ToonShader); var faceSh = Shader.Find(FaceShader); var outer = Shader.Find(OuterShader);
        if (toon == null || faceSh == null || outer == null) { Debug.LogError("[엘프] 셰이더를 못 찾았다."); return; }
        var atlases = new Dictionary<string, ChibiFaceAtlas>();
        foreach (var kind in new[] { "Eyes", "Mouths" })
        {
            var a = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>($"{MatDir}/Chibi_{kind}Atlas.asset");
            if (a == null) { Debug.LogError($"[엘프] 얼굴 아틀라스({kind})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return; }
            atlases[kind] = a;
        }
        var hairs = new Dictionary<string, Mesh>();
        foreach (var fit in HairFits)
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/Chibi_Helmet_{fit}.asset");
            if (m == null) { Debug.LogError($"[엘프] 투구 껍데기(Chibi_Helmet_{fit})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return; }
            hairs[$"{fit}.Helmet"] = m;
        }
        var mats = EnsureMaterials(toon, faceSh, outer, atlases);

        _saved.Clear();
        NetsTimeReport();
        Heads = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        double prep = watch.Elapsed.TotalSeconds;
        bool ok = DressElf(mats, atlases, hairs);
        double dressed = watch.Elapsed.TotalSeconds;

        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!_saved.Contains(path) && System.IO.Path.GetFileName(path).StartsWith("Chibi_Elf_")) AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.SaveAssets();
        if (ok) Debug.Log($"[엘프] {PawnPrefabPaths.PathOf(ElfPreview)} — 머리 · 몸만 다시 구웠다(다른 폰은 그대로) · {watch.Elapsed.TotalSeconds:F0}초.\n" +
            $"[엘프 시간] 준비 {prep:F1}s · 머리 빚기 {_elfHeadSecs:F1}s(그중 {NetsTimeReport()}) · 몸 · 프리팹 {dressed - prep - _elfHeadSecs:F1}s · 정리 · 저장 {watch.Elapsed.TotalSeconds - dressed:F1}s");
    }

    // 엘프 — 머리는 빚은 머리 한 벌이다(눈 · 입 판 없음). 사람 머리 시절의 못 박음을 비운다. 털 색은 `MarkingLook`.
    static void ElfLook(GameObject root)
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
        // 종족은 엘프(털 색 · 살색 후보를 든다 — `EnsureElfData`). 베낀 원본(방랑자)은 사람을 가리킨다.
        var elf = EnsureElfData();
        if (elf != null && root.TryGetComponent(out UnitBrain brain))
        {
            var bso = new SerializedObject(brain);
            bso.FindProperty("unitData").objectReferenceValue = elf;
            bso.ApplyModifiedPropertiesWithoutUndo();
        }
        if (!root.TryGetComponent(out SkinLook _)) root.AddComponent<SkinLook>();
        look.Apply();
    }
}
