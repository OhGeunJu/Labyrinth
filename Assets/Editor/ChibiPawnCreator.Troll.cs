using UnityEngine;

// **트롤 — 미리보기 v50을 옮긴 것**(2026-11-02, 사용자: *"이제 C#으로 옮겨줘"*).
//
// 모양은 파이썬 미리보기(`troll_preview.py` — 같은 거리장 식을 numpy로 그린 것)에서 사용자가 판마다 보고 고른 것이다. 수치를 고치면
// 화면이 바뀐다 — 고칠 때는 미리보기로 먼저 보여 줄 것. 단위는 머리 높이(`Hh`) · 발밑 0 · +z 앞. 축 셋:
//
//   - **굽은 등**(`Bend`) — 엉덩이 위로 올라갈수록 앞으로 민다(높이의 제곱). 혹을 붙이지 않고 몸통 자체가 굽는다. 팔은 그 굽은 만큼
//     앞에 선다(`ArmShift` — 팔 뼈도 거기 선다, `BoneBody.ArmZ`).
//   - **머리 좌표**(`HeadSpace`) — 머리는 굽은 등 앞에 처져 달린다(`HeadShift` · `HeadDrop`) · 가로로 넓다(`Wide` — x를 나눈다).
//     **목줄도 머리 좌표에서 저작한다**(사용자: 몸에서 재면 머리와 규격이 어긋난다).
//   - **굵은 목**(`Throat`) — 승모근이 등에서 그대로 이어져 머리에 닿는다. 몸통에만 녹인다(머리에 녹이면 얼굴이 부푼다).
//
// 생김새(사용자가 고른 것): 광대와 턱 모서리가 넓고 그 사이 볼은 평평 · 가로로 넓은 턱끝 · 미간 밑 V 주름과 같은 세모 코(얼굴 면을 부풀려
// 바닥이 붙는다) · 파낸 11자 콧구멍 · 노려보는 아몬드 눈 · 눈 밑 높이의 물방울 귀(오목도 같은 물방울) · 입 안 송곳니 · 위팔이 굵고
// 손목으로 가늘어지는 팔 · 손가락 둘 + 엄지 · 짧고 붙은 다리(하반신이 상체보다 짧다 — 체형의 `LegK` · `TorsoK`).
// 장신구는 고르는 축 셋(`Neck` · `Wrist` · `Ankle`) — 사슬은 고리마다 따로 굽고 판에서 흔들린다(`DanglingChain`).
public static partial class ChibiPawnCreator
{
    // 미리보기와 같은 셈의 틀 — 관절에서 한 번 낸다(`BruteFor`가 트롤을 굽기 전에 세운다).
    readonly struct TrollFrame
    {
        public readonly float Top, Hip, Lean, HeadShift, ArmShift;
        public TrollFrame(BruteJoints j)
        {
            Top = j.Top; Hip = j.Hip;
            Lean = 0.32f * (0.76f / 0.86f) * (0.76f / 0.86f);   // 몸통이 길어져도 굽은 정도는 그대로(굽힘은 엉덩이부터 높이의 제곱)
            HeadShift = Lean * Sq(Mathf.Max(j.Top - j.Hip, 0f)) + TrollHeadFwd;
            ArmShift  = Lean * Sq(Mathf.Max(j.ArmTop - j.Hip, 0f));
        }
        static float Sq(float x) => x * x;
    }
    static TrollFrame _tf;

    const float TrollHeadFwd = 0.055f;   // 머리를 조금만 앞으로 — 굵은 목이 드러나 목줄이 걸린다(많이 빼면 목이 길어진다)
    const float TrollHeadDrop = 0.12f;   // 머리가 굽은 등보다 낮게 처진다
    const float TrollWide = 1.18f;       // 얼굴을 가로로 — 머리 좌표의 x를 나눈다(눈은 `EyeLocal`로 제 너비)
    static readonly Vector3 TrollEye = new(0.128f, 0.02f, 0.418f);   // 눈 자리(x · 꼭대기 위 · z — 머리 좌표)

    static Vector3 TrollBend(Vector3 p)
    {
        float dy = Mathf.Max(p.y - _tf.Hip, 0f);
        return new Vector3(p.x, p.y, p.z - _tf.Lean * dy * dy);
    }
    static Vector3 TrollHeadSpace(Vector3 p)
    {
        var q = p - new Vector3(0f, -TrollHeadDrop, _tf.HeadShift);
        q.x /= TrollWide;
        return q;
    }
    static Vector3 TrollArmSpace(Vector3 p) => p - new Vector3(0f, 0f, _tf.ArmShift);
    static Vector3 TrollEyeLocal(Vector3 q) { q.x = TrollEye.x + (q.x - TrollEye.x) * TrollWide; return q; }

    // ── 몸통 ────────────────────────────────────────────────────────────────

    static float TrollTorsoRaw(Vector3 p, BruteJoints j)
    {
        float t = j.Top, h = j.Hip;
        float chest = Ell(p, new Vector3(0f, t - 0.26f, 0.04f), new Vector3(0.48f, 0.30f, 0.32f));
        // 어깨 — 등 가운데에서 팔까지 한 둥근 등성이(따로 솟은 혹 · 어깨 덩어리를 두지 않는다 — W자가 됐다)
        float dome  = Ell(p, new Vector3(0f, t - 0.18f, -0.12f), new Vector3(0.62f, 0.30f, 0.26f));
        float traps = Ell(p, new Vector3(0f, t - 0.10f, 0.02f), new Vector3(0.34f, 0.16f, 0.24f));
        float gut   = Ell(p, new Vector3(0f, h + 0.24f, 0.08f), new Vector3(0.40f, 0.30f, 0.34f));
        float hips  = Ell(p, new Vector3(0f, h + 0.02f, -0.02f), new Vector3(0.34f, 0.16f, 0.26f));
        float d = SMin(SMin(chest, dome, 0.14f), traps, 0.12f);
        return SMin(d, SMin(gut, hips, 0.10f), 0.14f);
    }

    // 굵은 목 — 둔덕 속에서 머리 속까지(앞이 조금 처진다) · 가로는 머리보다 좁게(넓으면 얼굴이 커 보인다).
    static float TrollThroat(Vector3 p, BruteJoints j)
    {
        var q = new Vector3(p.x / 1.05f, p.y, p.z);
        return Capsule(q, new Vector3(0f, j.Top - 0.12f, 0.18f), new Vector3(0f, j.Top - 0.18f, 0.33f), 0.20f);
    }

    static float TrollTorso(Vector3 p, BruteJoints j)
        => SMin(TrollTorsoRaw(TrollBend(p), j) * 0.9f, TrollThroat(p, j), 0.12f);

    // 밝은 배 — 살 면 위에 얇게(판처럼 안 뜨게) · 가슴에서 배로 좁아진다. 살색을 안 타는 재질이다(`Belly`).
    static float TrollBelly(Vector3 p, BruteJoints j)
    {
        float region = Ell(TrollBend(p), new Vector3(0f, j.Hip + 0.30f, 0.34f), new Vector3(0.27f, 0.34f, 0.22f));
        return SMax(TrollTorso(p, j) - 0.004f, region, 0.06f);
    }

    // 허리끈 · 앞뒤 천 — 띠는 **몸통과 허벅지를 같이** 감는다(허벅지 윗살이 허리 높이까지 올라와 옆 · 앞을 덮는다) · 띠 가운데 높이의
    // 둘레를 곧게 세운다(비탈을 따르면 쟁반처럼 벌어진다).
    static float TrollLoin(Vector3 p, BruteJoints j)
    {
        float h = j.Hip, yc = h + 0.06f;
        var flat = new Vector3(p.x, yc, p.z);
        float waist = Mathf.Min(TrollTorso(flat, j), Mathf.Min(TrollThigh(Side(flat, 1), j), TrollThigh(Side(flat, -1), j)));
        float belt  = SMax(Mathf.Abs(waist - 0.020f) - 0.020f, Mathf.Abs(p.y - yc) - 0.05f, 0.01f);
        float hem   = h - 0.22f + 0.03f * Mathf.Sin(p.x * 40f + 1f);
        float front = SMax(RBox(p, new Vector3(0f, h - 0.06f, 0.27f),  new Vector3(0.20f, 0.13f, 0.02f), 0.015f), hem - p.y, 0.01f);
        float back  = SMax(RBox(p, new Vector3(0f, h - 0.06f, -0.27f), new Vector3(0.24f, 0.13f, 0.02f), 0.015f), hem - p.y, 0.01f);
        return Mathf.Min(belt, Mathf.Min(front, back));
    }

    // ── 머리(머리 좌표) ─────────────────────────────────────────────────────

    static Vector3 RotY(Vector3 q, float a) => new(q.x * Mathf.Cos(a) + q.z * Mathf.Sin(a), q.y, -q.x * Mathf.Sin(a) + q.z * Mathf.Cos(a));
    static Vector3 RotX(Vector3 q, float a) => new(q.x, q.y * Mathf.Cos(a) + q.z * Mathf.Sin(a), -q.y * Mathf.Sin(a) + q.z * Mathf.Cos(a));

    // 귀 — 물방울 판(뿌리는 둥글고 뒤 · 위로 가늘어져 뾰족하다) · 오목도 같은 물방울을 줄여 판다(앞쪽 뿌리는 열리고 판은 안 뚫는다).
    static float TrollEar(Vector3 m, float t)
    {
        var q = m - new Vector3(0.189f, t - 0.03f, 0.285f);   // 눈 밑 높이 · 볼에 뿌리를 붙인다(.182는 묻히고 .196은 판처럼 튄다)
        q = RotX(RotY(q, 28f * Mathf.Deg2Rad), 35f * Mathf.Deg2Rad);   // 귓바퀴 면을 앞으로 · 뒤로 눕힌다
        float Drop(Vector3 a, Vector3 b, float ra, float rb) => Seg(new Vector3(0f, q.y / 1.2f, q.z), a, b, ra, rb);
        float ear    = SMax(Drop(Vector3.zero, new Vector3(0f, 0.008f, -0.044f), 0.030f, 0.003f), Mathf.Abs(q.x) - 0.013f, 0.008f);
        float hollow = SMax(Drop(new Vector3(0f, -0.002f, 0.010f), new Vector3(0f, 0.007f, -0.038f), 0.021f, 0.001f), 0.004f - q.x, 0.004f);
        return SMax(ear, -hollow, 0.004f);
    }

    // 세모 코 — 윗변은 미간 밑 V 주름과 같은 기울기 · 얼굴 면 자체를 부풀려 바닥이 면에 딱 붙는다(평면 위에 얹으면 옆에서 턱이 선다).
    static float TrollNose(Vector3 p, float t, float d)
    {
        float yy = p.y - t;
        float hw = (0.046f - yy) * 1.1f - 0.003f;
        float tri = SMax(Mathf.Abs(p.x) - hw, yy - 0.044f, 0.004f);
        float k = Mathf.Clamp01((yy + 0.014f) / 0.050f);
        return SMax(tri, d - 0.013f * k * (2f - k), 0.004f);   // 위(V 밑)가 가장 높고 바닥에서 0
    }

    static float TrollHeadRaw(Vector3 p, BruteJoints j)
    {
        float t = j.Top;
        var m = Mirror(p);
        var ec = new Vector3(TrollEye.x, t + TrollEye.y, TrollEye.z);
        float cran     = Ell(p, new Vector3(0f, t + 0.05f, 0.25f), new Vector3(0.19f, 0.16f, 0.20f));
        float brow     = Ell(m, new Vector3(0.07f, t + 0.056f, 0.42f), new Vector3(0.085f, 0.034f, 0.06f));   // 눈썹뼈 — 미간 쪽에 몰린다
        float glabella = Seg(p, new Vector3(0f, t + 0.055f, 0.445f), new Vector3(0f, t + 0.02f, 0.47f), 0.05f, 0.016f);   // 미간 세모
        float side     = Ell(p, new Vector3(0f, t - 0.05f, 0.30f), new Vector3(0.148f, 0.14f, 0.18f));     // 볼 바탕 — 좁게
        float jaw      = Ell(p, new Vector3(0f, t - 0.155f, 0.30f), new Vector3(0.132f, 0.10f, 0.16f));    // 턱 — 덜 처지게
        float masseter = Ell(m, new Vector3(0.114f, t - 0.11f, 0.31f), new Vector3(0.048f, 0.083f, 0.072f));
        float muzzle   = Ell(p, new Vector3(0f, t - 0.10f, 0.40f), new Vector3(0.12f, 0.13f, 0.10f));
        float lowlip   = Ell(p, new Vector3(0f, t - 0.165f, 0.45f), new Vector3(0.10f, 0.04f, 0.07f));
        float chin     = Ell(p, new Vector3(0f, t - 0.23f, 0.42f), new Vector3(0.118f, 0.062f, 0.085f));   // 턱끝 — 가로로 넓다(V자가 아니다)
        float slope    = Seg(new Vector3(p.x * 0.86f, p.y, p.z), new Vector3(0f, t + 0.04f, 0.40f), new Vector3(0f, t - 0.07f, 0.47f), 0.07f, 0.065f);   // 콧날 판
        float d = SMin(SMin(cran, side, 0.08f), brow, 0.02f);
        d = SMin(SMin(SMin(d, jaw, 0.06f), masseter, 0.05f), muzzle, 0.05f);
        // 광대와 턱 모서리가 넓다(그 사이 볼은 평평 — 파지 않는다)
        float cheekbone = Ell(m, new Vector3(0.118f, t - 0.005f, 0.37f), new Vector3(0.07f, 0.042f, 0.065f));
        float jawangle  = Ell(m, new Vector3(0.105f, t - 0.17f, 0.28f), new Vector3(0.042f, 0.075f, 0.075f));   // 조금만(크면 어금니 뺀 볼)
        d = SMin(SMin(d, cheekbone, 0.03f), jawangle, 0.07f);
        d = SMin(SMin(SMin(d, chin, 0.04f), lowlip, 0.03f), slope, 0.05f);
        d = SMin(d, glabella, 0.025f);
        d = SMin(d, TrollNose(p, t, d), 0.004f);
        d = SMax(d, -Ell(m, new Vector3(0.016f, t + 0.002f, 0.514f), new Vector3(0.0075f, 0.015f, 0.016f)), 0.002f);   // 콧구멍 — 판다(11자)
        // 미간과 코 사이 — 두 덩어리가 만나는 선만 얕게(뒤집힌 V)
        var c0 = new Vector3(0f, t + 0.046f, 0.494f); var c1 = new Vector3(0.021f, t + 0.027f, 0.499f); var c2 = new Vector3(0.042f, t + 0.008f, 0.479f);
        d = SMax(d, -Mathf.Min(Capsule(m, c0, c1, 0.010f), Capsule(m, c1, c2, 0.010f)), 0.008f);
        d = SMin(d, TrollEar(m, t), 0.02f);
        // 눈 — 눈 자리를 안쪽 끝까지 파고, 위아래로 조여 가는 아몬드 눈꺼풀(윗선은 안쪽으로 내려간다 — 노려본다)
        var me = TrollEyeLocal(m);
        d = SMax(d, -Ell(me, ec + new Vector3(-0.006f, 0f, 0.022f), new Vector3(0.052f, 0.03f, 0.04f)), 0.012f);
        float shell = Ell(me, ec, new Vector3(0.043f, 0.032f, 0.033f));
        float dx = me.x - ec.x;
        float upper = SMax(shell, (ec.y + 0.012f + 0.10f * dx - 8.0f * dx * dx) - p.y, 0.003f);
        float lower = SMax(shell, p.y - (ec.y - 0.017f + 6.0f * dx * dx), 0.003f);
        d = SMin(d, Mathf.Min(upper, lower), 0.004f);
        d = SMax(d, -RBox(p, new Vector3(0f, t - 0.12f, 0.53f), new Vector3(0.10f, 0.029f, 0.10f), 0.02f), 0.012f);   // 벌린 입 — 모서리까지
        return SMax(d, -RBox(p, new Vector3(0f, t - 0.12f, 0.40f), new Vector3(0.11f, 0.06f, 0.07f), 0.03f), 0.01f);  // 입 안 — 이 뒤로 깊게
    }

    static float TrollHead(Vector3 p, BruteJoints j) => TrollHeadRaw(TrollHeadSpace(p), j);

    static float TrollEyeWhite(Vector3 p, BruteJoints j)
        => Ell(TrollEyeLocal(Mirror(TrollHeadSpace(p))), new Vector3(TrollEye.x, j.Top + TrollEye.y, TrollEye.z), new Vector3(0.038f, 0.027f, 0.028f));
    static float TrollIris(Vector3 p, BruteJoints j)
        => Ell(TrollEyeLocal(Mirror(TrollHeadSpace(p))), new Vector3(TrollEye.x - 0.005f, j.Top + TrollEye.y, TrollEye.z + 0.021f), new Vector3(0.017f, 0.017f, 0.008f));
    static float TrollPupil(Vector3 p, BruteJoints j)   // 흰자 안의 작은 눈동자 — 조금 안쪽을 본다
        => Ell(TrollEyeLocal(Mirror(TrollHeadSpace(p))), new Vector3(TrollEye.x - 0.005f, j.Top + TrollEye.y, TrollEye.z + 0.027f), new Vector3(0.007f, 0.007f, 0.005f));

    // 입 안 뒷벽 — 송곳니(z .47)보다 뒤(앞에 두면 송곳니를 덮는다).
    static float TrollMouth(Vector3 p, BruteJoints j)
        => RBox(TrollHeadSpace(p), new Vector3(0f, j.Top - 0.12f, 0.37f), new Vector3(0.11f, 0.06f, 0.07f), 0.025f);

    // 이 — 큼직한 윗니 넷 · 아랫니 넷 · 아래 송곳니 둘(아랫잇몸에서 입 안으로 솟는다 — 입술 살을 안 뚫는다).
    static float TrollTeeth(Vector3 p, BruteJoints j)
    {
        var q = TrollHeadSpace(p);
        float t = j.Top, d = 9f;
        foreach (float x in new[] { -0.054f, -0.018f, 0.018f, 0.054f })
            d = Mathf.Min(d, RBox(q, new Vector3(x, t - 0.099f, 0.495f), new Vector3(0.014f, 0.013f, 0.012f), 0.006f));
        foreach (float x in new[] { -0.048f, -0.016f, 0.016f, 0.048f })
            d = Mathf.Min(d, RBox(q, new Vector3(x, t - 0.139f, 0.497f), new Vector3(0.013f, 0.012f, 0.012f), 0.006f));
        foreach (float x in new[] { -0.075f, 0.075f })
            d = Mathf.Min(d, Seg(q, new Vector3(x, t - 0.15f, 0.472f), new Vector3(x * 0.95f, t - 0.098f, 0.474f), 0.016f, 0.005f));
        return d;
    }

    // ── 팔다리(오른쪽에 적는다 — `Side`가 뒤집는다 · 팔은 팔 좌표) ────────────

    static float TrollUpper(Vector3 m, BruteJoints j)
    {
        m = TrollArmSpace(m);
        float ax = j.AX;
        float arm = Seg(m, new Vector3(ax, j.ArmTop - 0.06f, -0.02f), new Vector3(ax, j.Elbow, 0.03f), 0.19f, 0.16f);   // 어깨 쪽이 굵다
        float mid = (j.ArmTop + j.Elbow) * 0.5f - 0.03f;
        arm = SMin(arm, Ell(m, new Vector3(ax, mid, 0.06f), new Vector3(0.15f, 0.14f, 0.13f)), 0.05f);           // 이두
        arm = SMin(arm, Ell(m, new Vector3(ax, mid + 0.03f, -0.05f), new Vector3(0.15f, 0.15f, 0.13f)), 0.05f);  // 삼두
        return SMin(arm, Sph(m, new Vector3(ax - 0.06f, j.ArmTop - 0.10f, -0.02f), 0.18f), 0.08f);              // 어깨 머리는 낮게
    }

    // 아래팔(팔 좌표) — 팔꿈치 밑이 살짝 볼록하고 손목으로 매끈하게 준다 · 팔찌 · 가죽 끈도 이 살을 감는다(답은 여기 하나).
    static float TrollForeRaw(Vector3 m, BruteJoints j)
    {
        float ax = j.AX;
        float d = Seg(m, new Vector3(ax, j.Elbow, 0.03f), new Vector3(ax, j.Wrist + 0.06f, 0.06f), 0.165f, 0.12f);
        return SMin(d, Ell(m, new Vector3(ax + 0.01f, j.Elbow - 0.10f, 0.04f), new Vector3(0.168f, 0.16f, 0.16f)), 0.08f);
    }
    static float TrollFore(Vector3 m, BruteJoints j) => TrollForeRaw(TrollArmSpace(m), j);

    // 손 — 손가락 둘 + 엄지(엄지까지 셋) · 주먹 · 마디마다 따로 서서 사이에 골이 선다.
    static float TrollHand(Vector3 m, BruteJoints j)
    {
        m = TrollArmSpace(m);
        float ax = j.AX, w = j.Wrist;
        float d = RBox(m, new Vector3(ax + 0.01f, w - 0.10f, 0.06f), new Vector3(0.115f, 0.11f, 0.13f), 0.09f);
        float f = 9f;
        foreach (float z in new[] { 0.12f, 0.0f })
        {
            var a1 = new Vector3(ax + 0.03f, w - 0.16f, z); var a2 = new Vector3(ax + 0.035f, w - 0.25f, z + 0.01f); var a3 = new Vector3(ax + 0.03f, w - 0.31f, z + 0.03f);
            f = Mathf.Min(f, SMin(Capsule(m, a1, a2, 0.06f), Capsule(m, a2, a3, 0.055f), 0.01f));
        }
        d = SMin(d, f, 0.015f);
        float thumb = SMin(Capsule(m, new Vector3(ax - 0.07f, w - 0.08f, 0.15f), new Vector3(ax - 0.07f, w - 0.16f, 0.19f), 0.05f),
                           Capsule(m, new Vector3(ax - 0.07f, w - 0.16f, 0.19f), new Vector3(ax - 0.06f, w - 0.22f, 0.20f), 0.045f), 0.01f);
        return SMin(d, thumb, 0.02f);
    }

    static float TrollThigh(Vector3 m, BruteJoints j)
        => Seg(m, new Vector3(j.LX, j.Hip, 0f), new Vector3(j.LX, j.Knee + 0.02f, 0.03f), 0.19f, 0.165f);
    static float TrollShin(Vector3 m, BruteJoints j)   // 발목 족쇄도 이 살을 감는다
        => Seg(m, new Vector3(j.LX, j.Knee, 0.03f), new Vector3(j.LX, j.Ankle + 0.08f, 0.02f), 0.165f, 0.15f);
    static float TrollFoot(Vector3 m, BruteJoints j)   // 넓적한 발 · 발가락 셋
    {
        float lx = j.LX;
        float d = RBox(m, new Vector3(lx, 0.09f, 0.08f), new Vector3(0.19f, 0.09f, 0.24f), 0.08f);
        foreach (float x in new[] { -0.10f, 0f, 0.10f })
            d = SMin(d, Sph(m, new Vector3(lx + x, 0.06f, 0.31f), 0.06f), 0.03f);
        return d;
    }

    // ── 고르는 장신구 ────────────────────────────────────────────────────────

    // 쇠목줄 — 머리 좌표에서 저작한 단단한 타원 고리(몸에 맞춰 재지 않는다 · 살 속으로 든 곳은 그냥 가려진다). 아래 끝은 턱 밑,
    // 위 끝은 귀보다 뒤 — 등과 떨어지되 귀에는 안 닿는다.
    static readonly Vector3 CollarBotH = new(0f, -0.292f, 0.246f), CollarTopH = new(0f, 0.232f, 0.170f);   // 중심선(꼭대기 기준 · 머리 좌표)
    const float CollarA = 0.195f, CollarW = 0.040f, CollarT = 0.018f;   // 옆 반폭 · 띠 반너비 · 반두께
    static float TrollCollar(Vector3 p, BruteJoints j)
    {
        var top = CollarTopH + Vector3.up * j.Top; var bot = CollarBotH + Vector3.up * j.Top;
        var ct = (top - bot).normalized; var cn = new Vector3(0f, -ct.z, ct.y);
        float b = (top - bot).magnitude * 0.5f;
        var q = TrollHeadSpace(p) - (top + bot) * 0.5f;
        float e = (new Vector2(q.x / CollarA, Vector3.Dot(q, ct) / b).magnitude - 1f) * Mathf.Min(CollarA, b);
        return SMax(Mathf.Abs(e) - CollarT, Mathf.Abs(Vector3.Dot(q, cn)) - CollarW, 0.008f);
    }
    // 목줄 맨 아래(월드) — 사슬이 여기서 곧게 떨어진다.
    static Vector3 TrollCollarBottom(BruteJoints j)
    {
        var top = CollarTopH + Vector3.up * j.Top; var bot = CollarBotH + Vector3.up * j.Top;
        var ct = (top - bot).normalized;
        var h = bot - ct * CollarT;
        return h + new Vector3(0f, -TrollHeadDrop, _tf.HeadShift);
    }

    // 사슬 고리 — 길쭉한 타원 고리(두 반원 + 곧은 두 변)가 90°씩 번갈아 돌며 **앞 고리 구멍에 꿰인다**. 고리 사이 = 고리 안쪽 길이.
    const float LinkH = 0.016f, LinkR = 0.016f, LinkW = 0.0075f;   // 곧은 변 반길이 · 반원 반지름(중심선) · 쇠 굵기(반)
    const float LinkPitch = 2f * (LinkH + LinkR) - 2f * LinkW, LinkHalf = LinkH + LinkR + LinkW;
    static Vector3 LinkCenter(Vector3 top, int i) => top - Vector3.up * (LinkHalf - 2f * LinkW + LinkPitch * i);
    static float Link(Vector3 p, Vector3 c, bool turn)
    {
        var q = p - c;
        float a = turn ? q.z : q.x, n = turn ? q.x : q.z;
        float y = Mathf.Clamp(q.y, -LinkH, LinkH);
        float d2 = new Vector2(a, q.y - y).magnitude;
        return new Vector2(d2 - LinkR, n).magnitude - LinkW;
    }
    static Bounds LinkBounds(Vector3 c) => new(c, new Vector3(2f * (LinkR + LinkW) + 0.03f, 2f * LinkHalf + 0.03f, 2f * (LinkR + LinkW) + 0.03f));
    const int NeckLinks = 4, WristLinks = 3;   // 목은 넷(다섯이면 허리띠에 닿는다) · 손목은 셋

    // 뼈 목걸이 — 가는 검은 끈에 뼈 구슬 셋과 굽은 송곳니 넷을 번갈아 꿴다(2026-11-02~03, 사용자가 보낸 해골 · 송곳니 목걸이 그림 —
    // 해골 얼굴은 안 찍고 맨 구슬). **끈은 직접 저작한 줄이다**(점을 잇는 캡슐 — 몸 표면 × 판으로 재면 굽은 등에서 둔덕 위를 넘어 등에 걸쳤다).
    // 미리보기 `necklace56.py`에서 이렇게 지었다: 뒤는 쇠목줄 고리(머리 좌표 · 옆 반폭 0.20)를 높이 0.45배로 낮춘 고리 — 살 속에 묻혀
    // 가려진다(살 위로 밀면 굵은 목이 머리보다 넓어 정면에서 얼굴 테가 됐고, 쇠목줄 높이 그대로면 머리 위로 떴다) · 앞은 목 양옆에서 곧게
    // 떨어져 가슴에 U자로 처진다(맨 아래 꼭대기 −0.52 · 몸통 앞면에서 0.018 띄움) · 둘의 이음이 옆에서 ㄱ자로 꺾여 닫힌 줄 전체를 64점으로
    // 다시 뽑아 폈다(장신구가 걸린 꼭대기 −0.30 아래는 고정 — 펴면 고리가 줄어 장신구가 끈 밑에 뜬다).
    // 점과 장신구 자리는 그 미리보기에서 잰 값이다(x · 꼭대기 아래 · z) — 몸이나 쇠목줄 자리를 고치면 다시 잴 것.
    const float CordR = 0.006f, BeadR = 0.026f;
    static readonly Vector3[] BoneCord =
    {
        new(0.2143f, -0.1502f, 0.4507f), new(0.2062f, -0.1322f, 0.4481f), new(0.1953f, -0.1156f, 0.4453f), new(0.1816f, -0.1005f, 0.4425f),
        new(0.1655f, -0.0871f, 0.4400f), new(0.1472f, -0.0754f, 0.4378f), new(0.1271f, -0.0655f, 0.4360f), new(0.1055f, -0.0573f, 0.4346f),
        new(0.0829f, -0.0509f, 0.4336f), new(0.0595f, -0.0462f, 0.4328f), new(0.0357f, -0.0431f, 0.4323f), new(0.0115f, -0.0416f, 0.4321f),
        new(-0.0127f, -0.0416f, 0.4321f), new(-0.0368f, -0.0432f, 0.4324f), new(-0.0606f, -0.0464f, 0.4329f), new(-0.0840f, -0.0512f, 0.4336f),
        new(-0.1065f, -0.0576f, 0.4347f), new(-0.1280f, -0.0659f, 0.4361f), new(-0.1481f, -0.0759f, 0.4379f), new(-0.1663f, -0.0877f, 0.4401f),
        new(-0.1823f, -0.1012f, 0.4427f), new(-0.1958f, -0.1163f, 0.4454f), new(-0.2066f, -0.1330f, 0.4483f), new(-0.2146f, -0.1510f, 0.4508f),
        new(-0.2200f, -0.1703f, 0.4528f), new(-0.2230f, -0.1906f, 0.4539f), new(-0.2239f, -0.2118f, 0.4540f), new(-0.2231f, -0.2338f, 0.4529f),
        new(-0.2211f, -0.2563f, 0.4507f), new(-0.2183f, -0.2791f, 0.4477f), new(-0.2150f, -0.3022f, 0.4443f), new(-0.2079f, -0.3249f, 0.4369f),
        new(-0.1996f, -0.3471f, 0.4293f), new(-0.1900f, -0.3691f, 0.4228f), new(-0.1788f, -0.3910f, 0.4187f), new(-0.1662f, -0.4123f, 0.4175f),
        new(-0.1520f, -0.4326f, 0.4196f), new(-0.1365f, -0.4514f, 0.4247f), new(-0.1197f, -0.4685f, 0.4314f), new(-0.1013f, -0.4838f, 0.4382f),
        new(-0.0813f, -0.4971f, 0.4449f), new(-0.0597f, -0.5078f, 0.4510f), new(-0.0363f, -0.5154f, 0.4547f), new(-0.0118f, -0.5192f, 0.4569f),
        new(0.0130f, -0.5192f, 0.4568f), new(0.0375f, -0.5152f, 0.4546f), new(0.0607f, -0.5074f, 0.4507f), new(0.0823f, -0.4965f, 0.4446f),
        new(0.1022f, -0.4832f, 0.4379f), new(0.1205f, -0.4677f, 0.4310f), new(0.1373f, -0.4506f, 0.4244f), new(0.1527f, -0.4317f, 0.4194f),
        new(0.1668f, -0.4114f, 0.4175f), new(0.1794f, -0.3900f, 0.4188f), new(0.1904f, -0.3681f, 0.4230f), new(0.2000f, -0.3461f, 0.4296f),
        new(0.2083f, -0.3238f, 0.4372f), new(0.2153f, -0.3011f, 0.4447f), new(0.2185f, -0.2781f, 0.4480f), new(0.2213f, -0.2553f, 0.4509f),
        new(0.2232f, -0.2328f, 0.4530f), new(0.2239f, -0.2109f, 0.4540f), new(0.2229f, -0.1897f, 0.4539f), new(0.2198f, -0.1694f, 0.4527f),
        new(0.2143f, -0.1502f, 0.4507f),
    };
    static readonly Vector3[] BoneBeads = { new(-0.1431f, -0.4595f, 0.4425f), new(0.0000f, -0.5300f, 0.4795f), new(0.1431f, -0.4595f, 0.4425f) };
    // 송곳니 — 뿌리 · 마디 · 끝. 호의 바깥(아래)으로 뻗다가 끝이 가운데로 휜다 · 가운데 둘이 길다.
    static readonly Vector3[][] BoneFangs =
    {
        new[] { new Vector3(-0.1837f, -0.3908f, 0.4315f), new Vector3(-0.2418f, -0.4222f, 0.3844f), new Vector3(-0.2717f, -0.4671f, 0.3504f) },
        new[] { new Vector3(-0.0767f, -0.5044f, 0.4625f), new Vector3(-0.1159f, -0.5791f, 0.4565f), new Vector3(-0.1154f, -0.6481f, 0.4385f) },
        new[] { new Vector3(0.0767f, -0.5044f, 0.4625f), new Vector3(0.1159f, -0.5791f, 0.4565f), new Vector3(0.1154f, -0.6481f, 0.4385f) },
        new[] { new Vector3(0.1837f, -0.3908f, 0.4315f), new Vector3(0.2418f, -0.4222f, 0.3844f), new Vector3(0.2717f, -0.4671f, 0.3504f) },
    };
    static Vector3 OnTop(Vector3 v, BruteJoints j) => new(v.x, j.Top + v.y, v.z);

    static float TrollBoneCord(Vector3 p, BruteJoints j)
    {
        float d = 9f;
        for (int i = 1; i < BoneCord.Length; i++) d = Mathf.Min(d, Capsule(p, OnTop(BoneCord[i - 1], j), OnTop(BoneCord[i], j), CordR));
        return d;
    }
    static float TrollBoneBeads(Vector3 p, BruteJoints j)
    {
        float d = 9f;
        foreach (var c in BoneBeads) d = Mathf.Min(d, Sph(p, OnTop(c, j), BeadR));
        return d;
    }
    static float TrollBoneTeeth(Vector3 p, BruteJoints j)   // 송곳니 넷(조각 이름은 옛 `Teeth`를 잇는다)
    {
        float d = 9f;
        foreach (var f in BoneFangs)
        {
            Vector3 a = OnTop(f[0], j), b = OnTop(f[1], j), c = OnTop(f[2], j);
            d = Mathf.Min(d, SMin(Seg(p, a, b, 0.022f, 0.015f), Seg(p, b, c, 0.015f, 0.002f), 0.004f));
        }
        return d;
    }

    // 손목 쇠고랑 — 진짜 손목 자리 · 바깥 아래에 사슬이 걸리는 걸쇠(없으면 사슬이 뜬다).
    static Vector3 TrollCuffEye(BruteJoints j) => new(j.AX + 0.168f, j.Wrist + 0.012f, 0.06f);   // 팔 좌표
    static float TrollCuff(Vector3 m, BruteJoints j)
    {
        m = TrollArmSpace(m);
        float band = SMax(Mathf.Abs(TrollForeRaw(m, j) - 0.019f) - 0.019f, Mathf.Abs(m.y - (j.Wrist + 0.05f)) - 0.04f, 0.01f);
        var q = m - TrollCuffEye(j);
        return Mathf.Min(band, Torus(new Vector3(q.x, q.z, q.y), Vector3.zero, 0.018f, 0.007f));   // 걸쇠 — 앞을 보는 작은 고리
    }
    static Vector3 TrollCuffChainTop(BruteJoints j) => TrollCuffEye(j) - Vector3.up * 0.014f;

    // 손목 가죽 끈 — 팔뚝을 비스듬히 감은 띠(골이 비스듬히 돈다).
    static float TrollWrap(Vector3 m, BruteJoints j)
    {
        m = TrollArmSpace(m);
        float ang = Mathf.Atan2(m.z - 0.05f, m.x - j.AX);
        float ridge = 0.022f + 0.007f * Mathf.Cos(55f * m.y + 2f * ang);   // 골에서도 살 위 .015(.003까지 내려가면 손목에 묻힌다)
        return SMax(TrollForeRaw(m, j) - ridge, Mathf.Abs(m.y - (j.Wrist + 0.09f)) - 0.07f, 0.01f);
    }

    // 발목 족쇄 — 발등 바로 위 · 얇게(두 다리가 붙어 있어 두꺼우면 한 막대로 맞닿는다).
    static float TrollAnklet(Vector3 m, BruteJoints j)
        => SMax(Mathf.Abs(TrollShin(m, j) - 0.015f) - 0.015f, Mathf.Abs(m.y - (j.Ankle + 0.11f)) - 0.035f, 0.008f);
}
