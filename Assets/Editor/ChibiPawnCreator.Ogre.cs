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
//   - **고르는 축은 눈 말고도 일곱**(2026-11-01, 사용자: *"고를 수 있는게 눈 뿐"*) — 귀(둥근 · 뜯긴 · 작은) × 코(넓적 · 들린)는 머리 한 벌로
//     조합마다 굽고 · 뿔(없음 · 하나 · 부러진 — 11-02에 둘을 빼고 없음을 다시 넣었다) · 송곳니(긴 · 짧은 · 한쪽 부러진 · 양쪽 부러진 — 없는 것은 없다) · 이(다 있음 · 빠짐) · 어깨 천 · 팔목 띠(있음 · 없음).
//     몸에 붙은 축도 같은 이름 규칙이다(`Spine.Pick.Sash.On` — 첫 마디가 관절). 첫 벌이 10-31에 통과한 모습이다(구울 때 켜 둔다).
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

    // **고르는 축**(2026-11-01, 사용자: *"고를 수 있는게 눈 뿐"*) — 귀 · 코는 머리와 부드럽게 이어 빚으므로 따로 떼지 않고
    // **머리 한 벌을 귀 × 코 조합마다** 굽는다(`Head.Pick.Ears.{귀}.Skull@Nose.{코}`). 첫 벌(`Round` · `Broad`)이 10-31에 통과한 얼굴이다.
    public static readonly string[] OgreEars  = { "Round", "Torn", "Small" };
    public static readonly string[] OgreNoses = { "Broad", "Pug" };

    static float OgreHead(Vector3 p, BruteJoints j) => OgreHead(p, j, OgreEars[0], OgreNoses[0]);

    // 귀 · 코를 비우면(null) 맨 머리 — 그늘을 재는 장면(`scene`)이 쓴다(한 조합의 귀가 다른 조합의 살을 어둡게 하지 않게).
    static float OgreHead(Vector3 p, BruteJoints j, string ear, string nose)
    {
        float t = j.Top;
        var m = Mirror(p);
        float cranium = Ell(p, new Vector3(0f, t + 0.44f, 0.02f), new Vector3(0.32f, 0.33f, 0.31f));
        float jaw     = RBox(p, new Vector3(0f, t + 0.16f, 0.16f), new Vector3(0.27f, 0.10f, 0.20f), 0.10f);   // 아래턱이 윗입술보다 앞으로
        float muzzle  = Ell(p, new Vector3(0f, t + 0.31f, 0.21f), new Vector3(0.22f, 0.08f, 0.12f));
        float d = SMin(SMin(cranium, jaw, 0.10f), muzzle, 0.06f);
        if (nose != null) d = SMin(d, OgreNose(p, j, nose), 0.03f);
        if (ear  != null) d = SMin(d, OgreEar(m, j, ear), 0.02f);
        float lip = Seg(m, new Vector3(0f, t + 0.235f, 0.345f), new Vector3(0.15f, t + 0.215f, 0.32f), 0.03f, 0.025f);   // 내민 아랫입술
        d = SMin(d, lip, 0.03f);
        // 벌린 입 — 입꼬리가 처지게 판다(속은 `OgreMouth`가 어둡게 막는다).
        return SMax(d, -RBox(MouthBend(p, t), new Vector3(0f, t, 0.34f), new Vector3(0.15f, 0.032f, 0.08f), 0.028f), 0.02f);
    }

    // 입 — 가운데서 멀수록 내려간다(입꼬리가 처진다). 높이를 펴서 곧은 상자로 판다(이도 같이 휜다).
    static Vector3 MouthBend(Vector3 p, float t) => new(p.x, p.y - (0.255f - 0.6f * p.x * p.x), p.z);

    // 코 — 앞으로 내민 콧등 + 넓은 코끝 + 콧방울 · 콧구멍. 공 하나는 너무 둥글었고 · 납작하면 낮아 보였다(10-31).
    // `Pug` — 짧게 들린 코(콧구멍이 앞을 본다).
    static float OgreNose(Vector3 p, BruteJoints j, string kind)
    {
        float t = j.Top;
        var m = Mirror(p);
        if (kind == "Pug")
        {
            float pb = Seg(p, new Vector3(0f, t + 0.43f, 0.29f), new Vector3(0f, t + 0.39f, 0.345f), 0.03f, 0.04f);
            float pt = Ell(p, new Vector3(0f, t + 0.375f, 0.345f), new Vector3(0.075f, 0.05f, 0.05f));
            float pw = Sph(m, new Vector3(0.05f, t + 0.365f, 0.325f), 0.038f);
            float pd = SMin(SMin(pb, pt, 0.03f), pw, 0.02f);
            return SMax(pd, -Sph(m, new Vector3(0.032f, t + 0.36f, 0.392f), 0.017f), 0.008f);
        }
        float bridge = Seg(p, new Vector3(0f, t + 0.43f, 0.29f), new Vector3(0f, t + 0.375f, 0.37f), 0.035f, 0.045f);
        float tip    = Ell(p, new Vector3(0f, t + 0.355f, 0.35f), new Vector3(0.085f, 0.048f, 0.06f));
        float wing   = Sph(m, new Vector3(0.055f, t + 0.35f, 0.33f), 0.042f);
        float d = SMin(SMin(bridge, tip, 0.03f), wing, 0.02f);
        return SMax(d, -Sph(m, new Vector3(0.04f, t + 0.325f, 0.37f), 0.018f), 0.008f);
    }

    // 귀 — 머리 옆에 붙은 얇은 판을 앞-바깥(52°)으로 돌리고 앞면만 얕게 판다.
    // 옆을 보는 판을 깊게 파면 크레이터 같았고(v9) · 35°는 너무 튀어나왔다(v10).
    // `Torn` — 위 가장자리를 물어뜯긴 자리 둘 · `Small` — 붙은 자리를 두고 0.7배.
    static float OgreEar(Vector3 m, BruteJoints j, string kind)
    {
        var q = m - new Vector3(0.31f, j.Top + 0.42f, -0.02f);
        const float c = 0.615661f, s = 0.788011f;   // 52°
        q = new Vector3(q.x * c - q.z * s, q.y, q.x * s + q.z * c);
        float k = kind == "Small" ? 0.7f : 1f;
        q /= k;
        float shell = Ell(q, new Vector3(0.025f, 0f, 0f), new Vector3(0.075f, 0.105f, 0.03f));
        float cup   = Ell(q, new Vector3(0.025f, -0.005f, 0.03f), new Vector3(0.040f, 0.060f, 0.022f));
        float d = SMax(shell, -cup, 0.012f);
        if (kind == "Torn")
        {
            d = SMax(d, -Sph(q, new Vector3(0.07f, 0.085f, 0f), 0.035f), 0.006f);
            d = SMax(d, -Sph(q, new Vector3(0.105f, -0.03f, 0f), 0.02f), 0.004f);
        }
        return d * k;
    }

    // 입 속 — 판 자리 뒤를 어둡게 막는다(속이 비면 머리 안쪽 벽이 밝게 보인다).
    static float OgreMouth(Vector3 p, BruteJoints j)
        => Ell(MouthBend(p, j.Top), new Vector3(0f, j.Top, 0.26f), new Vector3(0.15f, 0.045f, 0.07f));

    // 이 — 윗니 넷은 윗입술에 매달리고 아랫니 셋은 송곳니 사이에 선다. 위아래 사이로 입 속이 보인다.
    // `gap` — 윗니 하나(가운데 오른쪽)가 빠졌다.
    static float OgreTeeth(Vector3 p, BruteJoints j, bool gap)
    {
        float t = j.Top, d = 9f;
        var q = MouthBend(p, t);
        foreach (float x in gap ? new[] { -0.105f, -0.035f, 0.105f } : new[] { -0.105f, -0.035f, 0.035f, 0.105f })
            d = Mathf.Min(d, RBox(q, new Vector3(x, t + 0.029f, 0.30f), new Vector3(0.028f, 0.012f, 0.02f), 0.008f));
        foreach (float x in new[] { -0.07f, 0f, 0.07f })
            d = Mathf.Min(d, RBox(q, new Vector3(x, t - 0.029f, 0.31f), new Vector3(0.025f, 0.011f, 0.02f), 0.008f));
        return d;
    }

    // 송곳니 — 아랫입술 안에서 솟아 윗입술 앞으로 올라온다(살을 뚫고 나온 것처럼 보이지 않게 — 10-31).
    // **송곳니 없는 오우거는 없다**(2026-11-01, 사용자: *"한쪽이 부러지거나 두쪽다 부러지는 건 이해하는데 없는 건 안돼"*) — 벌은
    // 긴 · 짧은 · 한쪽 부러진 · 양쪽 부러진 넷. 한쪽씩 따로 빚는다(`side` -1 왼 · +1 오른 — 반대쪽은 멀어서 안 걸린다).
    public static readonly (string name, string left, string right)[] OgreTuskSets =
    {
        ("Long", "Long", "Long"), ("Short", "Short", "Short"), ("BrokenOne", "Broken", "Long"), ("BrokenBoth", "Broken", "Broken"),
    };

    static float OgreTusk(Vector3 p, BruteJoints j, int side, string kind)
    {
        var q = new Vector3(p.x * side, p.y, p.z);
        float t = j.Top;
        if (kind == "Short")   // 같은 뿌리에서 윗입술에 못 미친다
            return Seg(q, new Vector3(0.15f, t + 0.22f, 0.31f), new Vector3(0.16f, t + 0.31f, 0.34f), 0.038f, 0.01f);
        float d = Seg(q, new Vector3(0.15f, t + 0.22f, 0.31f), new Vector3(0.17f, t + 0.37f, 0.355f), 0.042f, 0.008f);
        // 부러진 것 — 긴 송곳니를 중간에서 비스듬히 자른 그루터기(바깥쪽이 낮다).
        if (kind == "Broken") d = SMax(d, q.y - (t + 0.295f + 0.35f * (q.x - 0.155f)), 0.006f);
        return d;
    }

    static float OgreTusks(Vector3 p, BruteJoints j, string left, string right)
        => Mathf.Min(OgreTusk(p, j, -1, left), OgreTusk(p, j, 1, right));

    // 외눈 — 흰자 · 홍채 · 동공. 이마 가운데에 반쯤 튀어나온다.
    // **눈꺼풀이 덮는다**(2026-11-01, 사용자: *"튀어 나온 눈보다 아까 그 전눈이 좋아보이네 … 좀 자연스럽게"*) — 그 전에는 머리 살이 눈알을
    // 아무 데나 덮어 위아래가 들쭉날쭉 잘렸다. 눈은 그 전 자리에서 1.5cm만 앞으로 · 위 눈꺼풀이 흰자 위를 고르게 덮고 아래가 얇게 받친다
    // (`OgreLid` — 파이썬 미리보기로 맞췄다: 반쯤 덮으면 졸린 얼굴이라 위 절반 위만).
    static readonly Vector3 OneEye = new(0f, 0.505f, 0.285f);
    static readonly Vector3 OneWhiteR = new(0.105f, 0.10f, 0.075f), TwoWhiteR = new(0.088f, 0.088f, 0.07f);
    static float OgreOneWhite(Vector3 p, BruteJoints j) => Ell(p, OneEye + Vector3.up * j.Top, OneWhiteR);
    static float OgreOneLid(Vector3 p, BruteJoints j)   => OgreLid(p, OneEye + Vector3.up * j.Top, OneWhiteR);
    static float OgreTwoLid(Vector3 p, BruteJoints j)   => OgreLid(Mirror(p), TwoEye + Vector3.up * j.Top, TwoWhiteR);

    // 눈꺼풀 — 흰자를 감싼 얇은 껍데기에서 위(흰자 높이의 위 1/4)와 아래 가장자리만 남긴다.
    static float OgreLid(Vector3 q, Vector3 e, Vector3 r)
    {
        float shell = SMax(Ell(q, e, r * 1.12f), -Ell(q, e, r * 1.02f), 0.004f);
        float rel = (q.y - e.y) / r.y;
        float up = SMax(shell, (0.5f - rel) * r.y, 0.01f);
        float lo = SMax(shell, (rel + 0.72f) * r.y, 0.01f);
        return Mathf.Min(up, lo);
    }
    static float OgreOneIris(Vector3 p, BruteJoints j)  => Ell(p, OneEye + new Vector3(0f, j.Top, 0.067f), new Vector3(0.062f, 0.062f, 0.015f));
    static float OgreOnePupil(Vector3 p, BruteJoints j) => Ell(p, OneEye + new Vector3(0f, j.Top, 0.081f), new Vector3(0.026f, 0.032f, 0.008f));

    // 눈 둘 — 양쪽 · 외눈보다 조금 작게.
    static readonly Vector3 TwoEye = new(0.145f, 0.49f, 0.25f);
    static float OgreTwoWhite(Vector3 p, BruteJoints j) => Ell(Mirror(p), TwoEye + Vector3.up * j.Top, TwoWhiteR);
    static float OgreTwoIris(Vector3 p, BruteJoints j)  => Ell(Mirror(p), TwoEye + new Vector3(0f, j.Top, 0.062f), new Vector3(0.054f, 0.054f, 0.014f));
    static float OgreTwoPupil(Vector3 p, BruteJoints j) => Ell(Mirror(p), TwoEye + new Vector3(0f, j.Top, 0.075f), new Vector3(0.023f, 0.028f, 0.007f));

    // 정수리 뿔 — 위로 솟다 뒤로 휜다. **키웠다**(2026-11-02, 사용자: *"뿔 크기를 좀 더 키워줘"* — 남녀 둘 다 · 미리보기 `ogre_v3`).
    // 벌은 없음 · 하나 · 부러진 셋이다(사용자: *"뿔 없는 버전도 다시 넣고 뿔 두개는 없애버리자"*).
    static float OgreHorn(Vector3 p, BruteJoints j)
    {
        float t = j.Top;
        var a = new Vector3(0f, t + 0.70f, 0.05f); var b = new Vector3(0f, t + 0.94f, 0.01f); var c = new Vector3(0f, t + 1.12f, -0.08f);
        return Mathf.Min(Seg(p, a, b, 0.125f, 0.072f), Seg(p, b, c, 0.072f, 0.012f));
    }

    // 부러진 뿔 — 아랫마디만 남고 비스듬히 잘렸다.
    static float OgreHornBroken(Vector3 p, BruteJoints j)
    {
        float t = j.Top;
        float stub = Seg(p, new Vector3(0f, t + 0.70f, 0.05f), new Vector3(0f, t + 0.94f, 0.01f), 0.125f, 0.072f);
        return SMax(stub, p.y - (t + 0.86f + 0.06f * p.x), 0.01f);
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
