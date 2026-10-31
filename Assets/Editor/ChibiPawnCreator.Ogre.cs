using UnityEngine;

// **오우거 — 작고 통통한 치비 오우거**(2026-10-28 넷째 판).
//
// 판을 거친 길: 첫 판(사람 몸 늘이기) → 둘째(그림의 실제 비례 — 머리 1/7) → 사용자: *"원래 캐릭터 느낌이 너무 없는데"*(= 우리 치비
// 느낌) → 셋째(치비 비례) → 사용자가 **이런 느낌**이라며 작은 치비 오우거 그림 셋을 보냈다. 그림에서 가져온 것은 **이 계열의 전형**뿐이다
// (무늬 · 장식의 생김새는 옮기지 않는다):
//
//   - **머리** — 크고 둥근 대머리 · 머리 옆에 붙은 귀 · 넓적한 코 · 정수리 뿔. 눈두덩은 뺐다(사용자: 눈 위 혹 둘이 필요 없다 — 10-31).
//   - **입** — 조금 벌린 입(입꼬리가 처진다 · 크게 벌리면 소리 지르는 얼굴이다) · 윗니 넷 · 아랫니 셋 · 아래턱과 아랫입술이
//     앞으로 나오고 송곳니가 아랫입술에서 솟는다(살을 뚫고 나온 것처럼 보이지 않게 — 10-31).
//   - **눈** — 큰 외눈(흰자 · 홍채 · 동공) 또는 눈 둘(작으면 도깨비 같다 — 10-31에 키웠다). 개체마다 씨앗이 고른다(`Head.Pick.Eyes.One/Two` — `PawnLook`).
//     조각이 셋이라 이름 넷째 마디 뒤에 무엇인지 붙인다(`Head.Pick.Eyes.One.Iris` — `PawnLook`은 축 · 벌 뒤를 안 읽는다).
//   - **몸** — 처진 배 · 좁고 비스듬한 어깨(v4의 덩어리 그대로 작게) · 짧은 다리 · 큰 주먹 · 큰 발.
//   - **걸친 것** — 한쪽 어깨에 걸친 천 · 몸통에 붙은 허리 천(v4 — 사용자가 좋다고 한 것)과 둥근 쇠 장식 · 해진 아래 천 ·
//     가시 박힌 팔목 띠.
//
// 관절은 체형 배수가 낸다(`Shape` 다리 0.8 · 팔 1.0 → 엉덩이 0.74 · 몸통 꼭대기 1.50 · 팔목 0.84 · 무릎 0.43).
// 그림 확인은 파이썬 미리보기(같은 수식)로 했다. **수치를 고치면 그 그림과 어긋난다** — 고칠 때는 다시 그려 볼 것.
public static partial class ChibiPawnCreator
{
    const float OgreArmX = 0.52f, OgreLegX = 0.21f;

    // ── 몸통 — 처진 배 · 좁고 비스듬한 어깨 ─────────────────────────────────────

    static float OgreTorso(Vector3 p, BruteJoints j)
    {
        float t = j.Top, h = j.Hip;
        var m = Mirror(p);
        float chest = Ell(p, new Vector3(0f, t - 0.20f, 0.0f),  new Vector3(0.40f, 0.26f, 0.30f));
        float delt  = Ell(m, new Vector3(0.34f, t - 0.10f, -0.03f), new Vector3(0.16f, 0.14f, 0.17f));
        float traps = Capsule(m, new Vector3(0.05f, t + 0.04f, -0.05f), new Vector3(0.28f, t - 0.04f, -0.04f), 0.10f);
        float gut   = Ell(p, new Vector3(0f, h + 0.28f, 0.10f), new Vector3(0.46f, 0.36f, 0.40f));
        float hips  = Ell(p, new Vector3(0f, h + 0.02f, -0.02f), new Vector3(0.38f, 0.18f, 0.28f));
        float upper = SMin(SMin(chest, delt, 0.08f), traps, 0.08f);
        return SMin(upper, SMin(gut, hips, 0.12f), 0.14f);
    }

    // ── 머리 ────────────────────────────────────────────────────────────────

    static float OgreHead(Vector3 p, BruteJoints j)
    {
        float t = j.Top;
        var m = Mirror(p);
        float cranium = Ell(p, new Vector3(0f, t + 0.44f, 0.02f), new Vector3(0.32f, 0.33f, 0.31f));
        float jaw     = RBox(p, new Vector3(0f, t + 0.16f, 0.16f), new Vector3(0.27f, 0.10f, 0.20f), 0.10f);   // 아래턱이 윗입술보다 앞으로
        float muzzle  = Ell(p, new Vector3(0f, t + 0.31f, 0.21f), new Vector3(0.22f, 0.08f, 0.12f));
        float d = SMin(SMin(cranium, jaw, 0.10f), muzzle, 0.06f);
        d = SMin(d, OgreNose(p, j), 0.03f);
        d = SMin(d, OgreEar(m, j), 0.02f);
        float lip = Seg(m, new Vector3(0f, t + 0.235f, 0.345f), new Vector3(0.15f, t + 0.215f, 0.32f), 0.03f, 0.025f);   // 내민 아랫입술
        d = SMin(d, lip, 0.03f);
        // 벌린 입 — 입꼬리가 처지게 판다(속은 `OgreMouth`가 어둡게 막는다).
        return SMax(d, -RBox(MouthBend(p, t), new Vector3(0f, t, 0.34f), new Vector3(0.15f, 0.032f, 0.08f), 0.028f), 0.02f);
    }

    // 입 — 가운데서 멀수록 내려간다(입꼬리가 처진다). 높이를 펴서 곧은 상자로 판다(이도 같이 휜다).
    static Vector3 MouthBend(Vector3 p, float t) => new(p.x, p.y - (0.255f - 0.6f * p.x * p.x), p.z);

    // 코 — 앞으로 내민 콧등 + 넓은 코끝 + 콧방울 · 콧구멍. 공 하나는 너무 둥글었고 · 납작하면 낮아 보였다(10-31).
    static float OgreNose(Vector3 p, BruteJoints j)
    {
        float t = j.Top;
        var m = Mirror(p);
        float bridge = Seg(p, new Vector3(0f, t + 0.43f, 0.29f), new Vector3(0f, t + 0.375f, 0.37f), 0.035f, 0.045f);
        float tip    = Ell(p, new Vector3(0f, t + 0.355f, 0.35f), new Vector3(0.085f, 0.048f, 0.06f));
        float wing   = Sph(m, new Vector3(0.055f, t + 0.35f, 0.33f), 0.042f);
        float d = SMin(SMin(bridge, tip, 0.03f), wing, 0.02f);
        return SMax(d, -Sph(m, new Vector3(0.04f, t + 0.325f, 0.37f), 0.018f), 0.008f);
    }

    // 귀 — 머리 옆에 붙은 얇은 판을 앞-바깥(52°)으로 돌리고 앞면만 얕게 판다.
    // 옆을 보는 판을 깊게 파면 크레이터 같았고(v9) · 35°는 너무 튀어나왔다(v10).
    static float OgreEar(Vector3 m, BruteJoints j)
    {
        var q = m - new Vector3(0.31f, j.Top + 0.42f, -0.02f);
        const float c = 0.615661f, s = 0.788011f;   // 52°
        q = new Vector3(q.x * c - q.z * s, q.y, q.x * s + q.z * c);
        float shell = Ell(q, new Vector3(0.025f, 0f, 0f), new Vector3(0.075f, 0.105f, 0.03f));
        float cup   = Ell(q, new Vector3(0.025f, -0.005f, 0.03f), new Vector3(0.040f, 0.060f, 0.022f));
        return SMax(shell, -cup, 0.012f);
    }

    // 입 속 — 판 자리 뒤를 어둡게 막는다(속이 비면 머리 안쪽 벽이 밝게 보인다).
    static float OgreMouth(Vector3 p, BruteJoints j)
        => Ell(MouthBend(p, j.Top), new Vector3(0f, j.Top, 0.26f), new Vector3(0.15f, 0.045f, 0.07f));

    // 이 — 윗니 넷은 윗입술에 매달리고 아랫니 셋은 송곳니 사이에 선다. 위아래 사이로 입 속이 보인다.
    static float OgreTeeth(Vector3 p, BruteJoints j)
    {
        float t = j.Top, d = 9f;
        var q = MouthBend(p, t);
        foreach (float x in new[] { -0.105f, -0.035f, 0.035f, 0.105f })
            d = Mathf.Min(d, RBox(q, new Vector3(x, t + 0.029f, 0.30f), new Vector3(0.028f, 0.012f, 0.02f), 0.008f));
        foreach (float x in new[] { -0.07f, 0f, 0.07f })
            d = Mathf.Min(d, RBox(q, new Vector3(x, t - 0.029f, 0.31f), new Vector3(0.025f, 0.011f, 0.02f), 0.008f));
        return d;
    }

    // 송곳니 — 아랫입술 안에서 솟아 윗입술 앞으로 올라온다(살을 뚫고 나온 것처럼 보이지 않게 — 10-31).
    static float OgreTusks(Vector3 p, BruteJoints j)
        => Seg(Mirror(p), new Vector3(0.15f, j.Top + 0.22f, 0.31f), new Vector3(0.17f, j.Top + 0.37f, 0.355f), 0.042f, 0.008f);

    // 외눈 — 흰자 · 홍채 · 동공. 이마 가운데에 반쯤 튀어나온다.
    static readonly Vector3 OneEye = new(0f, 0.505f, 0.27f);
    static float OgreOneWhite(Vector3 p, BruteJoints j) => Ell(p, OneEye + Vector3.up * j.Top, new Vector3(0.105f, 0.10f, 0.075f));
    static float OgreOneIris(Vector3 p, BruteJoints j)  => Ell(p, OneEye + new Vector3(0f, j.Top, 0.067f), new Vector3(0.062f, 0.062f, 0.015f));
    static float OgreOnePupil(Vector3 p, BruteJoints j) => Ell(p, OneEye + new Vector3(0f, j.Top, 0.081f), new Vector3(0.026f, 0.032f, 0.008f));

    // 눈 둘 — 양쪽 · 외눈보다 조금 작게.
    static readonly Vector3 TwoEye = new(0.145f, 0.49f, 0.235f);
    static float OgreTwoWhite(Vector3 p, BruteJoints j) => Ell(Mirror(p), TwoEye + Vector3.up * j.Top, new Vector3(0.088f, 0.088f, 0.07f));
    static float OgreTwoIris(Vector3 p, BruteJoints j)  => Ell(Mirror(p), TwoEye + new Vector3(0f, j.Top, 0.062f), new Vector3(0.054f, 0.054f, 0.014f));
    static float OgreTwoPupil(Vector3 p, BruteJoints j) => Ell(Mirror(p), TwoEye + new Vector3(0f, j.Top, 0.075f), new Vector3(0.023f, 0.028f, 0.007f));

    // 정수리 뿔 — 위로 솟다 뒤로 휜다.
    static float OgreHorn(Vector3 p, BruteJoints j)
    {
        float t = j.Top;
        var a = new Vector3(0f, t + 0.72f, 0.05f); var b = new Vector3(0f, t + 0.90f, 0.02f); var c = new Vector3(0f, t + 1.02f, -0.04f);
        return Mathf.Min(Seg(p, a, b, 0.095f, 0.055f), Seg(p, b, c, 0.055f, 0.01f));
    }

    // ── 팔 — 통통한 원기둥 · 큰 주먹 ──────────────────────────────────────────

    static float OgreUpper(Vector3 m, BruteJoints j)
    {
        float ax = j.AX;
        float arm   = Seg(m, new Vector3(ax, j.ArmTop, 0f), new Vector3(ax, j.Elbow, 0.01f), 0.17f, 0.15f);
        float bicep = Ell(m, new Vector3(ax, (j.ArmTop + j.Elbow) * 0.5f, 0.06f), new Vector3(0.15f, 0.15f, 0.14f));   // 알통
        return SMin(SMin(arm, bicep, 0.05f), Sph(m, new Vector3(ax - 0.03f, j.ArmTop + 0.02f, -0.01f), 0.19f), 0.06f);
    }

    // 아래팔 — 팔꿈치 밑이 불룩하다.
    static float OgreFore(Vector3 m, BruteJoints j)
    {
        float ax = j.AX;
        float arm  = Seg(m, new Vector3(ax, j.Elbow, 0.01f), new Vector3(ax, j.Wrist + 0.04f, 0.03f), 0.15f, 0.15f);
        float meat = Ell(m, new Vector3(ax, j.Elbow - (j.Elbow - j.Wrist) * 0.3f, 0.03f), new Vector3(0.17f, 0.12f, 0.16f));
        return SMin(arm, meat, 0.05f);
    }

    static float OgreHand(Vector3 m, BruteJoints j)
    {
        float ax = j.AX, w = j.Wrist;
        float d = RBox(m, new Vector3(ax, w - 0.13f, 0.04f), new Vector3(0.13f, 0.14f, 0.14f), 0.11f);
        foreach (float z in new[] { 0.09f, 0.03f, -0.03f, -0.09f })   // 마디 넷
            d = SMin(d, Sph(m, new Vector3(ax + 0.06f, w - 0.24f, z + 0.05f), 0.05f), 0.03f);
        return SMin(d, Capsule(m, new Vector3(ax - 0.08f, w - 0.09f, 0.16f), new Vector3(ax - 0.06f, w - 0.19f, 0.20f), 0.055f), 0.04f);   // 엄지
    }

    // 팔목 띠 — 아래팔 표면의 껍데기 · 바깥으로 가시 하나.
    static float OgreBand(Vector3 m, BruteJoints j)
        => SMax(Mathf.Abs(OgreFore(m, j) - 0.014f) - 0.022f, Mathf.Abs(m.y - (j.Wrist + 0.15f)) - 0.09f, 0.01f);

    static float OgreBandSpike(Vector3 m, BruteJoints j)
        => Seg(m, new Vector3(j.AX + 0.12f, j.Wrist + 0.15f, 0.03f), new Vector3(j.AX + 0.30f, j.Wrist + 0.16f, 0.03f), 0.06f, 0.006f);

    // ── 다리 — 짧고 굵다 · 큰 발 ─────────────────────────────────────────────

    // 넓적다리 — 앞 근육이 불룩해 배에서 무릎까지 일자로 떨어지지 않는다(10-31).
    static float OgreThigh(Vector3 m, BruteJoints j)
    {
        float lx = j.LX;
        float leg  = Seg(m, new Vector3(lx, j.Hip, 0f), new Vector3(lx, j.Knee + 0.02f, 0.02f), 0.21f, 0.19f);
        // 공 하나를 앞에 붙이면 엉덩이처럼 보였다(v12) — 넓적다리를 따라 긴 덩어리로, 위는 가늘고 무릎 위가 굵다.
        float quad  = Seg(m, new Vector3(lx, j.Hip - 0.02f, 0.03f), new Vector3(lx - 0.01f, j.Knee + 0.09f, 0.07f), 0.18f, 0.19f);
        float inner = Ell(m, new Vector3(lx - 0.07f, j.Knee + 0.10f, 0.08f), new Vector3(0.10f, 0.10f, 0.12f));   // 무릎 안쪽 위 물방울
        return SMin(SMin(leg, quad, 0.08f), inner, 0.05f);
    }

    static float OgreShin(Vector3 m, BruteJoints j)
        => Seg(m, new Vector3(j.LX, j.Knee, 0.02f), new Vector3(j.LX, j.Ankle + 0.08f, 0.02f), 0.19f, 0.18f);

    static float OgreFoot(Vector3 m, BruteJoints j)
    {
        float lx = j.LX;
        float d = RBox(m, new Vector3(lx, 0.095f, 0.09f), new Vector3(0.18f, 0.095f, 0.25f), 0.09f);
        float[] xs = { -0.12f, -0.05f, 0.02f, 0.09f };
        float[] rs = { 0.060f, 0.052f, 0.050f, 0.046f };   // 안쪽(엄지발가락)이 굵다
        for (int i = 0; i < xs.Length; i++)
            d = SMin(d, Sph(m, new Vector3(lx + xs[i], 0.055f, 0.32f), rs[i]), 0.03f);
        return d;
    }

    // ── 걸친 것 ─────────────────────────────────────────────────────────────

    // 몸통 겉의 껍데기 — 천은 몸에 붙는다.
    static float OgreSkin(Vector3 p, BruteJoints j, float gap, float thick) => Mathf.Abs(OgreTorso(p, j) - gap) - thick;

    // 한쪽 어깨에 걸친 천 — 오른 엉덩이에서 왼 어깨로 비스듬한 띠(앞뒤 같은 줄).
    static float OgreSash(Vector3 p, BruteJoints j)
    {
        var a = new Vector2(0.42f, j.Hip + 0.18f); var b = new Vector2(-0.30f, j.Top + 0.02f);
        var q = new Vector2(p.x, p.y); var ab = b - a;
        float k = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
        float line = (q - (a + ab * k)).magnitude;
        return SMax(OgreSkin(p, j, 0.016f, 0.02f), line - 0.15f, 0.02f);
    }

    // 허리 천(v4 — 몸통 표면에 감는다) · 해진 아래 천 앞뒤.
    static float OgreLoin(Vector3 p, BruteJoints j)
    {
        float h = j.Hip;
        float belt  = SMax(OgreSkin(p, j, 0.016f, 0.026f), Mathf.Abs(p.y - (h + 0.07f)) - 0.075f, 0.01f);
        float hem   = h - 0.28f + 0.035f * Mathf.Sin(p.x * 38f);   // 해진 밑단
        float front = SMax(RBox(p, new Vector3(0f, h - 0.10f, 0.35f), new Vector3(0.24f, 0.17f, 0.02f), 0.015f), hem - p.y, 0.01f);
        float back  = SMax(RBox(p, new Vector3(0f, h - 0.10f, -0.30f), new Vector3(0.26f, 0.17f, 0.02f), 0.015f), hem - p.y, 0.01f);
        return Mathf.Min(belt, Mathf.Min(front, back));
    }

    // 허리띠 위에 겹쳐 붙은 둥근 쇠 판 — 배 곡면을 그대로 탄다. 납작한 원반을 띄우면 배 밑에 반쯤 묻혀 반달로 보였다(10-31).
    static float OgreBuckle(Vector3 p, BruteJoints j)
    {
        float disc = new Vector2(p.x, p.y - (j.Hip + 0.07f)).magnitude - 0.10f;
        return SMax(SMax(OgreSkin(p, j, 0.046f, 0.008f), disc, 0.008f), -p.z, 0.01f);
    }
}
