using System;
using UnityEngine;

// **여성 오우거 · 머리 둘**(2026-11-02 — 사용자가 파이썬 미리보기 `ogre_female` · `ogre_v3`로 판마다 고른 모양을 옮긴 것).
//
//   - **여성** — 남성 오우거와 같은 관절(`BruteOgreFemale` — 체형 수치가 같고 머리 높이도 남성 기준 머리를 쓴다 · `DimsOf`) 위에
//     몸만 갈아 끼운다: 배를 줄이고 허리를 조이고 엉덩이를 넓힌 몸통 · 앞으로 나온 가슴(사용자: *"가슴도 좀더 나오게"*) · 가슴을 감은
//     천과 어깨 끈 하나 · 무릎까지 두른 치마 · 팔과 다리 전체를 가늘게(사용자: *"내가 말한거는 다리 전체였어"*) · 발은 남성의 0.9배
//     (*"발 너무 작네"*) · 좁고 둥근 턱 · 짧은 송곳니 · 속눈썹(외눈은 가운데가 똑바로 위 · 두 눈은 눈마다 셋 — *"일부로 틀어서
//     배치한거야?"*) · 귓불을 꿰고 매달린 고리 귀걸이(*"귀에 걸린 느낌이 아니야"*) · 뿔 뒤에서 묶어 등으로 늘인 땋은 머리.
//   - **머리 둘**(`Heads` 축 — 15%, 사용자: *"오우거는 머리 두개가 포인트"* · *"일부에게만 나오는 변형으로"*) — 머리를 0.74배(가로는
//     거기서 0.88배 더)로 줄여 양 어깨에 **똑바로** 세운다(*"그냥 똑바로 세워"* — 기울이면 어색했다) · 간격은 안쪽 귀가 거의 닿게.
//     눈 · 뿔은 머리마다 따로 고른다(`EyesL`/`EyesR` · `HornL`/`HornR`) · 귀 · 코 · 이 · 송곳니 · 눈꺼풀은 두 머리가 같다.
//     두 얼굴 사이에 솟던 승모근은 몸통 위를 얕은 안장으로 깎는다(`OgreSaddle` — 깊게 깎으면 *"파여버렸는데"*였다).
//   - **머리 하나도 같은 크기**(2026-11-03, 사용자: *"머리 한 개짜리도 머리를 두개짜리 크기로 해서 통일"*) — 가운데에 줄여 세운다.
//     대신 몸집을 1.45배 키웠다(종족 `BodyScale` 1.4 → 2.03 — 사용자가 씬에서 1.45로 키워 보고 *"비율 좋은데 트롤이랑 크기도
//     비슷하고"* · *"저만큼 그냥 기본적으로 키우고 싶다"*). 값은 `BigRaceCreator.Races`와 `Ogre*.asset` 둘에 같이 적는다.
public static partial class ChibiPawnCreator
{
    public const string BruteOgreFemale = "OgreFemale";
    static bool IsOgreKind(string kind) => kind == BruteOgre || kind == BruteOgreFemale;

    // ── 머리 둘 ──────────────────────────────────────────────────────────────

    const float TwinS = 0.74f, TwinKX = 0.88f, TwinX = 0.26f, TwinDrop = -0.08f;
    public const float TwinHeadChance = 0.15f;

    // 몸 좌표 → 한쪽 머리의 좌표(목 밑동 `(0, Top, 0)`을 축으로 줄이고 옆으로 옮긴 것을 되돌린다). `sgn` -1 왼 · +1 오른.
    static Vector3 TwinLocal(Vector3 p, int sgn, float top)
    {
        var q = p - new Vector3(sgn * TwinX, top + TwinDrop, 0f);
        return new Vector3(q.x / (TwinS * TwinKX), q.y / TwinS + top, q.z / TwinS);
    }

    // 머리 하나의 모양을 그 자리에 — 거리는 줄인 만큼 줄인다(가로가 더 줄어 조금 작게 잰다 — 표면은 그대로다).
    static Func<Vector3, float> OnTwin(Func<Vector3, float> f, int sgn, float top)
        => p => f(TwinLocal(p, sgn, top)) * TwinS * TwinKX;

    // 머리 좌표의 상자 → 몸 좌표의 상자.
    static Bounds TwinBounds(Bounds b, int sgn, float top)
    {
        var c = b.center;
        var at = new Vector3(c.x * TwinS * TwinKX + sgn * TwinX, (c.y - top) * TwinS + top + TwinDrop, c.z * TwinS);
        return new Bounds(at, new Vector3(b.size.x * TwinS * TwinKX, b.size.y * TwinS, b.size.z * TwinS) + Vector3.one * 0.02f);
    }

    // 두 얼굴 사이 — 몸통 위를 얕게 낮춘다(가운데가 턱 밑 · 머리 밑에서 원래 높이로 · 미리보기 A).
    static float OgreSaddle(float d, Vector3 p, BruteJoints j) => SMax(d, p.y - (j.Top + 0.03f + 1.0f * p.x * p.x), 0.12f);

    // ── 여성 몸통 · 걸친 것 ───────────────────────────────────────────────────

    static float OgreFTorso(Vector3 p, BruteJoints j)
    {
        float t = j.Top, h = j.Hip;
        var m = Mirror(p);
        float chest = Ell(p, new Vector3(0f, t - 0.20f, 0f), new Vector3(0.36f, 0.25f, 0.27f));
        float bust  = Ell(m, new Vector3(0.15f, t - 0.31f, 0.20f), new Vector3(0.175f, 0.16f, 0.17f));   // 앞으로 낸다
        float delt  = Ell(m, new Vector3(0.31f, t - 0.10f, -0.03f), new Vector3(0.14f, 0.13f, 0.15f));
        float traps = Capsule(m, new Vector3(0.05f, t + 0.04f, -0.05f), new Vector3(0.25f, t - 0.04f, -0.04f), 0.085f);
        float waist = Ell(p, new Vector3(0f, h + 0.30f, 0.02f), new Vector3(0.33f, 0.30f, 0.28f));
        float hips  = Ell(p, new Vector3(0f, h + 0.04f, -0.03f), new Vector3(0.44f, 0.22f, 0.32f));
        float upper = SMin(SMin(SMin(chest, delt, 0.08f), traps, 0.08f), bust, 0.04f);
        return SMin(upper, SMin(waist, hips, 0.14f), 0.12f);
    }

    static float OgreFSkin(Vector3 p, BruteJoints j, float gap, float thick) => Mathf.Abs(OgreFTorso(p, j) - gap) - thick;

    // 가슴을 감은 천 띠 + 어깨 끈 하나(앞에서 사선).
    static float OgreFWrap(Vector3 p, BruteJoints j)
    {
        float t = j.Top;
        float band = SMax(OgreFSkin(p, j, 0.016f, 0.022f), Mathf.Abs(p.y - (t - 0.31f)) - 0.13f, 0.01f);
        var a = new Vector2(0.24f, t - 0.22f); var b = new Vector2(-0.18f, t + 0.02f);
        var q = new Vector2(p.x, p.y); var ab = b - a;
        float k = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
        float line = (q - (a + ab * k)).magnitude;
        float strap = SMax(SMax(OgreFSkin(p, j, 0.016f, 0.02f), line - 0.06f, 0.01f), -p.z, 0.01f);
        return Mathf.Min(band, strap);
    }

    // 허리띠 + 무릎까지 오는 치마 — 옆까지 두르고 밑단이 물결진다.
    static float OgreFSkirt(Vector3 p, BruteJoints j)
    {
        float h = j.Hip;
        float belt = SMax(OgreFSkin(p, j, 0.016f, 0.026f), Mathf.Abs(p.y - (h + 0.10f)) - 0.06f, 0.01f);
        float hem  = h - 0.42f + 0.03f * Mathf.Sin(Mathf.Atan2(p.x, p.z) * 9f);
        float r    = Mathf.Sqrt(p.x * p.x + p.z * p.z / 0.70f);
        float rad  = 0.46f + 0.10f * Mathf.Clamp01((h - p.y) / 0.42f);
        float cloth = SMax(SMax(Mathf.Abs(r - rad) - 0.018f, p.y - (h + 0.08f), 0.01f), hem - p.y, 0.01f);
        return Mathf.Min(belt, cloth);
    }

    static float OgreFBuckle(Vector3 p, BruteJoints j)
    {
        float disc = new Vector2(p.x, p.y - (j.Hip + 0.10f)).magnitude - 0.08f;
        return SMax(SMax(OgreFSkin(p, j, 0.046f, 0.008f), disc, 0.008f), -p.z, 0.01f);
    }

    // ── 여성 팔다리 — 두 단 가늘게 · 발은 남성의 0.9배 ───────────────────────────

    static float OgreFUpper(Vector3 m, BruteJoints j) => OgreUpper(m, j) + 0.05f;
    static float OgreFFore(Vector3 m, BruteJoints j)  => OgreFore(m, j) + 0.045f;
    static float OgreFHand(Vector3 m, BruteJoints j)  => OgreHand(m, j) + 0.012f;
    static float OgreFBand(Vector3 m, BruteJoints j)
        => SMax(Mathf.Abs(OgreFFore(m, j) - 0.012f) - 0.018f, Mathf.Abs(m.y - (j.Wrist + 0.15f)) - 0.08f, 0.01f);
    static float OgreFThigh(Vector3 m, BruteJoints j) => OgreThigh(m - new Vector3(0.02f, 0f, 0f), j) + 0.045f;
    static float OgreFShin(Vector3 m, BruteJoints j)  => OgreShin(m, j) + 0.045f;
    static float OgreFFoot(Vector3 m, BruteJoints j)
    {
        const float k = 0.9f;
        var q = new Vector3(j.LX + (m.x - j.LX) / k, m.y, m.z);
        return OgreFoot(q, j) * k;
    }

    // ── 여성 머리 ──────────────────────────────────────────────────────────

    // 귀 판의 자리 · 방향(`OgreEar`와 같은 52°) — 귓불 · 귀걸이가 읽는다.
    static readonly Vector3 OgreEarC = new(0.31f, 0.42f, -0.02f);
    static readonly Vector3 OgreEarN = new(0.788011f, 0f, 0.615661f);    // 귀가 보는 쪽(앞-바깥)
    static readonly Vector3 OgreEarT = new(0.615661f, 0f, -0.788011f);   // 귀 면을 따라 가로

    // 귓불 — 귀 판의 아래 끝(작은 귀는 귀 자리를 두고 0.7배).
    static Vector3 OgreLobe(BruteJoints j, string ear)
    {
        float k = ear == "Small" ? 0.7f : 1f;
        return OgreEarC + Vector3.up * j.Top + (OgreEarT * 0.02f + Vector3.down * 0.075f) * k;
    }

    // 턱이 좁고 둥글다 · 아랫입술 덜 내밈 · 도톰한 귓불. 귀 · 코를 비우면 맨 머리(그늘 장면).
    static float OgreFHead(Vector3 p, BruteJoints j, string ear, string nose)
    {
        float t = j.Top;
        var m = Mirror(p);
        float cranium = Ell(p, new Vector3(0f, t + 0.45f, 0.02f), new Vector3(0.30f, 0.32f, 0.30f));
        float jaw     = RBox(p, new Vector3(0f, t + 0.19f, 0.13f), new Vector3(0.22f, 0.09f, 0.18f), 0.09f);
        float muzzle  = Ell(p, new Vector3(0f, t + 0.31f, 0.20f), new Vector3(0.19f, 0.075f, 0.11f));
        float d = SMin(SMin(cranium, jaw, 0.11f), muzzle, 0.06f);
        if (nose != null) d = SMin(d, OgreNose(p, j, nose) + 0.008f, 0.03f);
        if (ear != null)
        {
            float lobe = Sph(m, OgreLobe(j, ear), 0.035f * (ear == "Small" ? 0.8f : 1f));
            d = SMin(d, SMin(OgreEar(m, j, ear), lobe, 0.03f), 0.02f);
        }
        float lip = Seg(m, new Vector3(0f, t + 0.245f, 0.315f), new Vector3(0.13f, t + 0.225f, 0.29f), 0.026f, 0.020f);
        d = SMin(d, lip, 0.03f);
        return SMax(d, -RBox(MouthBend(p, t), new Vector3(0f, t, 0.34f), new Vector3(0.15f, 0.032f, 0.08f), 0.028f), 0.02f);
    }

    // 짧은 송곳니 — 윗입술 밑까지만. 벌은 남성과 같은 넷(긴 · 짧은 · 부러진 — `OgreTuskSets`).
    static float OgreFTusk(Vector3 p, BruteJoints j, int side, string kind)
    {
        var q = new Vector3(p.x * side, p.y, p.z);
        float t = j.Top;
        if (kind == "Short")
            return Seg(q, new Vector3(0.13f, t + 0.23f, 0.28f), new Vector3(0.135f, t + 0.29f, 0.30f), 0.028f, 0.008f);
        float d = Seg(q, new Vector3(0.13f, t + 0.23f, 0.28f), new Vector3(0.14f, t + 0.32f, 0.31f), 0.030f, 0.007f);
        if (kind == "Broken") d = SMax(d, q.y - (t + 0.275f + 0.35f * (q.x - 0.135f)), 0.006f);
        return d;
    }

    static float OgreFTusks(Vector3 p, BruteJoints j, string left, string right)
        => Mathf.Min(OgreFTusk(p, j, -1, left), OgreFTusk(p, j, 1, right));

    // 속눈썹 — 외눈은 셋(가운데가 똑바로 위 · 55 · 90 · 125°) · 두 눈은 눈마다 셋(바깥으로 살짝 — 좌우 대칭).
    static float OgreLashes(Vector3 p, Vector3 e, float[] angles, float sx, float sy, float ex, float ey, float r0)
    {
        float d = 9f;
        foreach (float deg in angles)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            var from = e + new Vector3(sx * c, sy * s, 0.045f);
            var to   = e + new Vector3(ex * c, ey * s + 0.01f, 0.03f);
            d = Mathf.Min(d, Seg(p, from, to, r0, 0.004f));
        }
        return d;
    }
    static readonly float[] OneLashAngles = { 55f, 90f, 125f }, TwoLashAngles = { 40f, 75f, 110f };
    static float OgreOneLashes(Vector3 p, BruteJoints j)
        => OgreLashes(p, OneEye + Vector3.up * j.Top, OneLashAngles, 0.095f, 0.095f, 0.15f, 0.14f, 0.012f);
    static float OgreTwoLashes(Vector3 p, BruteJoints j)
        => OgreLashes(Mirror(p), TwoEye + Vector3.up * j.Top, TwoLashAngles, 0.085f, 0.085f, 0.125f, 0.125f, 0.011f);

    // 땋은 머리 — 뿔 뒤에서 묶어 등 뒤로 늘인 굵은 한 가닥 · 끝을 쇠고리로 묶는다.
    static readonly Vector3[] BraidPts =
    {
        new(0f, 0.68f, -0.22f), new(0f, 0.52f, -0.33f), new(0f, 0.34f, -0.36f), new(0f, 0.16f, -0.40f),
        new(0f, -0.02f, -0.44f), new(0f, -0.20f, -0.46f), new(0f, -0.38f, -0.46f), new(0f, -0.54f, -0.45f),
    };
    static float OgreFHair(Vector3 p, BruteJoints j)
    {
        float t = j.Top, d = 9f;
        var up = Vector3.up * t;
        for (int i = 0; i < BraidPts.Length - 1; i++)
        {
            Vector3 a = BraidPts[i] + up, b = BraidPts[i + 1] + up;
            float r = 0.085f - 0.004f * i;
            d = SMin(d, Ell(p, (a + b) * 0.5f, new Vector3(r, (b - a).magnitude * 0.62f, r)), 0.02f);
        }
        float knot = Ell(p, new Vector3(0f, t + 0.70f, -0.14f), new Vector3(0.13f, 0.08f, 0.12f));
        return SMin(knot, d, 0.03f);
    }
    static float OgreFHairRing(Vector3 p, BruteJoints j)
    {
        var q = p - new Vector3(0f, j.Top - 0.44f, -0.455f);
        return SMax(new Vector2(q.x, q.z).magnitude - 0.075f, Mathf.Abs(q.y) - 0.035f, 0.01f);
    }

    // 귀걸이 — 귓불을 꿰고 매달린 고리. 고리 면이 귀 면을 뚫고 지나간다(귀가 보는 쪽 + 위아래).
    static float OgreEarring(Vector3 p, BruteJoints j, string ear)
    {
        var q = Mirror(p) - (OgreLobe(j, ear) + Vector3.down * 0.055f);
        float u = Vector3.Dot(q, OgreEarN), w = Vector3.Dot(q, OgreEarT);
        float a = new Vector2(u, q.y).magnitude - 0.06f;
        return new Vector2(a, w).magnitude - 0.012f;
    }
}
