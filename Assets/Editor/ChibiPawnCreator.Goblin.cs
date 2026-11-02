using System;
using System.Collections.Generic;
using UnityEngine;

// **고블린 코 · 귀 — 빚은 입체**(2026-11-02, 사용자: *"코랑 귀만 좀더 고블린처럼 해줘 코는 매부리코 귀도 좀 더 성의 있게"*).
//
// 사람 치비 머리에 붙는 부품(`Head.Ear.Goblin*.L/R` · `Head.Nose.Hook*.{머리형}`)이다 — 머리 · 몸 · 다른 이목구비는 그대로다.
// 옛 것은 옆선 하나를 돌려 깎은 회전체(`Lathe`)라 귀는 납작한 잎, 코는 원뿔이었다. 회전체로는 귓바퀴의 오목함도 콧등의 굽음도
// 안 나와서 오크 머리와 같은 길(거리장 → 표면 그물 `SurfaceNets`)로 빚는다.
//
//   - **매부리코** — 옆모습 윤곽 하나(얼굴 속 밑동 → 콧등 → 혹 → 아래로 굽은 코끝 → 코밑 → 얼굴)를 좌우로 둥글게 민다
//     (단면은 윤곽 끝에서 폭 0 — 콧등이 등성이로 선다). 콧방울은 녹여 붙이고 콧구멍은 파낸다. 머리형마다 같은 메시를
//     그 머리의 코 자리에 둔다.
//   - **귀** — 옆으로 길게 뻗어 끝이 조금 처지는 넓적한 잎. 테(귓바퀴)를 말고 안쪽을 오목하게 판다(바닥에는 막을 남긴다) ·
//     안쪽 등성이 하나. 오목한 면이 **앞 · 조금 위**를 본다(그림의 고블린 귀 — 위에서 보는 게임 카메라에서도 안쪽이 보인다).
//     기준 머리(성별마다)에 맞춰 오른귀를 빚고 왼귀는 거울로 뒤집는다 — 고른 머리로는 `PawnLook`이 머리카락과 같은 셈으로 옮긴다.
//
// **변형 — 귀 셋 · 코 셋**(같은 날, 사용자: *"좀 더 고블린처럼 크고 긴 버전도"* · *"귀 한쪽이 잘린 버전도 넣고 큰 코 작은코"*). 같은 식에
// 값만 다르다(`GoblinEars` · `HookNoses`). 고블린은 `Goblin*` · `Hook*`로 못 박아 **개체마다 씨앗이 고른다**(`PawnLook.Family`).
//
// 재질은 `SkinShaded` — 고운 살(`SkinFine` — 제 선을 끄고 바깥 선만)에 **정점 색 그늘을 켠 것**이다. 툰 음영은 빛의 방향만 봐서
// 오목한 바닥이 판과 같은 밝기로 칠해졌다(미리보기). 정점 색에 그늘(`AoOf`)과 **귀 안쪽 한 톤**(`GEarInner`)을 구워 넣는다.
//
// 모양은 파이썬 미리보기(같은 식 — 단위 m · 원점 머리 한복판 · +z 앞)로 맞췄다. **수치를 고치면 그 그림과 어긋난다.**
public static partial class ChibiPawnCreator
{
    public const string GoblinEar = "Goblin";   // `Head.Ear.Goblin*.L/R` — `PawnLook.earOverride`
    public const string HookNose  = "Hook";     // `Head.Nose.Hook*.{머리형}` — `PawnLook.noseOverride`

    const float GoblinEarStep = 0.0025f, GoblinNoseStep = 0.0022f;
    const float GoblinAoStep = 0.0075f, GoblinAoGain = 4f;

    // ── 매부리코 ────────────────────────────────────────────────────────────
    //
    // 원점은 그 머리의 코 높이(`NoseT`)에서 잰 **얼굴 앞면** — 밑동은 뒤(−z)로 얼굴 속에 묻힌다.

    // 옆모습 윤곽 (y, z) — 닫힌 곡선(`CatmullClosed`)의 조종점. 혹은 콧등 직선보다 2cm 앞으로 · 코끝은 콧방울보다 아래로 늘어진다.
    static readonly Vector2[] HookSide =
    {
        new(0.066f, -0.030f), new(0.060f, 0.000f), new(0.038f, 0.030f), new(0.014f, 0.066f), new(-0.016f, 0.096f),
        new(-0.042f, 0.110f), new(-0.062f, 0.104f), new(-0.076f, 0.088f), new(-0.072f, 0.072f), new(-0.060f, 0.058f),
        new(-0.052f, 0.030f), new(-0.050f, 0.000f), new(-0.050f, -0.030f),
    };

    // 높이마다 반폭 (y, 반폭) — 콧대는 가늘고 혹 · 코끝에서 넓다. y 오름차순.
    static readonly Vector2[] HookHalfWidth =
        { new(-0.080f, 0.016f), new(-0.050f, 0.022f), new(-0.010f, 0.020f), new(0.030f, 0.015f), new(0.066f, 0.012f) };
    const float HookRound = 0.028f;   // 윤곽 안으로 이만큼 들어가야 반폭이 다 선다(단면을 둥글게)

    // 코 변형 — 같은 윤곽에 배율: 앞으로(z > 0만) · 아래로(y < 0만) · 폭 · 위로(y > 0만 — 콧등이 미간 위에서 시작한다).
    // 콧방울 · 콧구멍도 같은 배율로 늘어난다(사용자: *"긴 코는 콧망울도 길어져야해"*).
    sealed class HookShape
    {
        public readonly string Name; public readonly float ZK, YK, WK, UK;
        public readonly Vector2[] Poly, HalfWidth;
        public HookShape(string name, float zk, float yk, float wk, float uk)
        {
            Name = name; ZK = zk; YK = yk; WK = wk; UK = uk;
            var side = new Vector2[HookSide.Length];
            for (int i = 0; i < side.Length; i++) side[i] = K(HookSide[i]);
            Poly = CatmullClosed(side, 8);
            HalfWidth = new Vector2[HookHalfWidth.Length];
            for (int i = 0; i < HalfWidth.Length; i++)
                HalfWidth[i] = new Vector2(K(new Vector2(HookHalfWidth[i].x, 0f)).x, HookHalfWidth[i].y * wk);
        }
        public Vector2 K(Vector2 yz) => new(yz.x < 0f ? yz.x * YK : yz.x * UK, yz.y > 0f ? yz.y * ZK : yz.y);
    }

    // 위 표들보다 뒤에 둘 것(정적 초기화는 적힌 순서다).
    static readonly HookShape[] HookNoses =
    {
        //                       앞     아래   폭     위
        new(HookNose,           1.00f, 1.00f, 1.00f, 1.00f),
        new(HookNose + "Big",   1.50f, 1.20f, 1.15f, 1.80f),   // 앞으로 · 위로 길게 · 코끝이 입 앞까지(사용자: *"앞쪽으로도 길어야하는데 위로도"*)
        new(HookNose + "Small", 0.68f, 0.82f, 0.90f, 1.00f),
    };

    static float HookNoseSdf(Vector3 q, HookShape n)
    {
        float d2 = PolySdf(q.y, q.z, n.Poly);
        float hw = Interp(q.y, n.HalfWidth) * Mathf.Sqrt(Mathf.Clamp01(-d2 / HookRound));
        float wx = Mathf.Abs(q.x) - hw;
        float d  = d2 > 0f || wx > 0f ? new Vector2(Mathf.Max(d2, 0f), Mathf.Max(wx, 0f)).magnitude : Mathf.Max(d2, wx);
        var m = Mirror(q);
        var wing = n.K(new Vector2(-0.050f, 0.030f));
        // 콧방울 — **볼(얼굴) 속에서 나와 코끝 쪽으로** 뻗는다(사용자: *"콧망울이 우리 얼굴에 붙어있잖아"* — 코 밑에 둔 알은 얼굴에서 떠 보였다).
        var wingIn  = new Vector3(0.030f * n.WK, wing.x + 0.002f, -0.020f);
        var wingOut = new Vector3(0.020f * n.WK, wing.x, wing.y + 0.018f * n.ZK);
        d = SMin(d * 0.8f, Seg(m, wingIn, wingOut, 0.016f * n.WK, 0.013f * n.WK), 0.022f);
        var nost = n.K(new Vector2(-0.061f, 0.046f));
        return SMax(d, -Ell(m, new Vector3(0.012f * n.WK, nost.x, nost.y), new Vector3(0.0065f * n.WK, 0.005f * n.YK, 0.012f * n.ZK)), 0.003f); // 콧구멍
    }

    static Dictionary<string, Mesh> HookNoseMeshes()
    {
        var meshes = new Dictionary<string, Mesh>();
        // 큰 코까지 들어가는 한 상자(아래로 −0.10 · 위로 0.12 · 앞으로 0.17).
        var b = new Bounds(new Vector3(0f, 0f, 0.070f), new Vector3(0.15f, 0.30f, 0.24f));
        foreach (var n in HookNoses)
        {
            var shape = n;
            float Sdf(Vector3 q) => HookNoseSdf(q, shape);
            // 그늘을 재는 장면 — 코 + 얼굴(코 뒤의 판판한 면으로 친다). 머리형마다 같은 메시라 머리 하나의 곡면을 넣지 않는다.
            float Scene(Vector3 q) => Mathf.Min(Sdf(q), q.z + 0.004f);
            meshes[n.Name] = SaveSkull($"Chibi_Nose_{n.Name}",
                                       SurfaceNets(Sdf, b, GoblinNoseStep, Scene, p => p, GoblinAoStep, GoblinAoGain, keep: 0.45f));
        }
        return meshes;
    }

    // 코 자리 — `Noses`와 같은 셈(그 높이의 얼굴 앞면 · 턱을 민 만큼 앞으로). 크기는 전체 손잡이(`NoseK`)만 곱한다.
    static void HookNoseParts(List<Part> parts, HeadShape h, Vector3 pos, Dictionary<string, Mesh> meshes)
    {
        float yN = h.NoseT - 0.5f;
        float front = HeadRadius(h.NoseT, h) + (yN < 0f ? h.Forward * 2f * yN * yN : 0f);
        var root = new Vector3(0f, pos.y + yN * h.Size.y, pos.z + front * h.Size.z);
        foreach (var kv in meshes)
            parts.Add(new Part($"Head.Nose.{kv.Key}.{h.Name}", kv.Value, root, Vector3.one * h.NoseK, "SkinShaded", on: false,
                               outerMat: "OutlineOuterFine"));
    }

    // ── 귀 ──────────────────────────────────────────────────────────────────
    //
    // 귀의 틀 — 밑동(머리 옆) · `U` 바깥으로 뻗는 길이 축 · `V` 판 안의 위 · `N` 오목한 면이 보는 쪽. 원점은 머리 한복판.
    const float GEarLift = 0.01f, GEarTilt = 28f;                 // 눈 높이에서 올림 · 오목한 면을 위로 젖히는 각(도)
    const float GEarTh = 0.011f, GEarRim = 0.020f, GEarRimAt = 0.012f, GEarRimW = 0.0085f;   // 판 반두께 · 테 높이 · 테 자리 · 테 폭
    const float GEarBowl = 0.028f, GEarSkin = 0.008f;             // 오목한 깊이 · 바닥에 남기는 막 두께
    const float GEarInner = 0.72f;                                  // 귀 안쪽 정점 색
    const float GEarCutAt = 0.56f, GEarCutSlant = 0.45f;           // 잘린 자리(길이의 몫) · 기울기(위가 더 남는다)

    // 귀 변형 — 길이 · 위로 · 뒤로 뻗는 각(도) · 끝이 처지는 정도 · 가장자리가 앞으로 말리는 정도 · 밑동의 윗 · 아랫 반폭 · 왼귀 끝이 잘렸나.
    readonly struct GoblinEarShape
    {
        public readonly string Name;
        public readonly float L, Elev, Back, Droop, Cup, WT, WB;
        public readonly bool CutLeft;
        public GoblinEarShape(string name, float l, float elev, float back, float droop, float cup, float wt, float wb, bool cutLeft = false)
        { Name = name; L = l; Elev = elev; Back = back; Droop = droop; Cup = cup; WT = wt; WB = wb; CutLeft = cutLeft; }
    }

    static readonly GoblinEarShape[] GoblinEars =
    {
        //                         길이   위    뒤    처짐   말림  윗폭    아랫폭
        new(GoblinEar,            0.38f, 16f, 22f, 0.40f, 2.5f, 0.085f, 0.100f),
        new(GoblinEar + "Long",   0.52f, 20f, 20f, 0.28f, 2.0f, 0.100f, 0.125f),                 // 크고 길게
        new(GoblinEar + "Cut",    0.38f, 16f, 22f, 0.40f, 2.5f, 0.085f, 0.100f, cutLeft: true),  // 왼귀 끝이 잘렸다
    };

    readonly struct EarFrame
    {
        public readonly Vector3 Root, U, V, N;
        public readonly GoblinEarShape E;
        public readonly bool Cut;
        public EarFrame(Vector3 root, Vector3 u, Vector3 v, Vector3 n, GoblinEarShape e, bool cut)
        { Root = root; U = u; V = v; N = n; E = e; Cut = cut; }
    }

    static EarFrame GoblinEarFrame(HeadShape basis, GoblinEarShape ear, bool cut)
    {
        float t = basis.EyeT + GEarLift;
        var root = new Vector3(HeadRadius(t, basis) * basis.Size.x - 0.018f, (t - 0.5f) * basis.Size.y, -0.03f);
        float e = ear.Elev * Mathf.Deg2Rad, s = ear.Back * Mathf.Deg2Rad, k = GEarTilt * Mathf.Deg2Rad;
        var u  = new Vector3(Mathf.Cos(e) * Mathf.Cos(s), Mathf.Sin(e), -Mathf.Cos(e) * Mathf.Sin(s));
        var n0 = (Vector3.forward - u * Vector3.Dot(Vector3.forward, u)).normalized;
        var v0 = Vector3.Cross(n0, u);
        if (v0.y < 0f) v0 = -v0;
        return new EarFrame(root, u, v0 * Mathf.Cos(k) - n0 * Mathf.Sin(k), n0 * Mathf.Cos(k) + v0 * Mathf.Sin(k), ear, cut);
    }

    // 귀 판의 자리 — a 길이 · b 판 안의 위(처짐을 편) · c 판의 두께 쪽(말림을 편) · s = a / 길이.
    static void EarLocal(Vector3 p, in EarFrame f, out float a, out float b, out float c, out float s)
    {
        var q = p - f.Root;
        a = Vector3.Dot(q, f.U);
        float ap = Mathf.Max(a, 0f);
        b = Vector3.Dot(q, f.V) + f.E.Droop * ap * ap;
        c = Vector3.Dot(q, f.N) - f.E.Cup * b * b;
        s = Mathf.Clamp01(a / f.E.L);
    }

    // 판의 윤곽(판 안 − · 밖 +) — 윗선은 곧게, 아랫선은 부풀었다 끝으로 모인다 · 밑동 아래 작은 귓불 · 밑동은 머리 속까지.
    // 잘린 귀는 끝을 비스듬히 자른다(위가 더 남고 자른 자리는 조금 들쭉날쭉) — 테도 그 자리를 따라 말린다(아문 자리).
    static float EarOutline(float a, float b, float s, in EarFrame f)
    {
        float cb   = 0.012f * s;
        float wt   = f.E.WT * (1f - s) + 0.003f;
        float lobe = 0.010f * Mathf.Exp(-Sq((s - 0.05f) / 0.10f));
        float wb   = f.E.WB * Mathf.Pow(1f - s, 1.6f) + 0.003f + lobe;
        float d2 = Mathf.Max(b - (cb + wt), cb - wb - b);
        d2 = Mathf.Max(d2, a - f.E.L);
        d2 = Mathf.Max(d2, -a - 0.06f);
        if (f.Cut) d2 = Mathf.Max(d2, a - (GEarCutAt * f.E.L + GEarCutSlant * b + 0.006f * Mathf.Sin(b * 160f)));
        return d2;
    }

    static float GoblinEarSdf(Vector3 p, in EarFrame f)
    {
        EarLocal(p, f, out float a, out float b, out float c, out float s);
        float d2 = EarOutline(a, b, s, f);
        float inside = GStep(0f, 0.030f, -d2);
        float rim    = Mathf.Exp(-Sq((d2 + GEarRimAt) / GEarRimW));
        float th     = GEarTh * (1f - 0.45f * s);
        float ridge  = 0.006f * Mathf.Exp(-Sq((b - (0.012f * s - 0.020f)) / 0.011f)) * GStep(0.04f, 0.14f, s) * (1f - GStep(0.40f, 0.62f, s));
        float bowl   = GEarBowl * inside * Mathf.Pow(1f - s, 0.35f);
        float back   = th + 0.004f * rim;
        float face   = Mathf.Max(th + GEarRim * rim - bowl + ridge * inside, GEarSkin - back);   // 오목한 바닥에도 막을 남긴다
        return Mathf.Max(d2, Mathf.Max(c - face, -c - back)) * 0.8f;
    }

    // 귀 안쪽(앞면 · 테 안)을 한 톤 — 1이면 그대로.
    static float GoblinEarTint(Vector3 p, in EarFrame f)
    {
        EarLocal(p, f, out float a, out float b, out float c, out float s);
        float d2 = EarOutline(a, b, s, f);
        float th = GEarTh * (1f - 0.45f * s);
        float inner = GStep(0.012f, 0.024f, -d2) * GStep(-0.6f * th, -0.2f * th, c) * GStep(0f, 0.05f, a) * (1f - GStep(0.80f, 0.95f, s));
        return 1f - (1f - GEarInner) * inner;
    }

    // 기준 머리(성별마다)의 귀 한 쌍씩 — 오른귀(+x)를 빚고 왼귀는 거울(잘린 판은 왼귀를 따로 잘라 빚는다). 메시 원점이 머리 한복판이라
    // 부품 자리는 기준 머리 자리다.
    static void GoblinEarParts(List<Part> parts, HeadShape basis, Vector3 bPos, string gender)
    {
        foreach (var e in GoblinEars)
        {
            var right = SculptGoblinEar(basis, GoblinEarFrame(basis, e, cut: false));
            var left  = e.CutLeft ? SculptGoblinEar(basis, GoblinEarFrame(basis, e, cut: true)) : right;
            var meshR = SaveSkull($"Chibi_Ear_{e.Name}_{gender}_R", right);
            var meshL = SaveSkull($"Chibi_Ear_{e.Name}_{gender}_L", MirrorX(left));
            parts.Add(new Part($"Head.Ear.{e.Name}.L", meshL, bPos, Vector3.one, "SkinShaded", on: false, outerMat: "OutlineOuterFine"));
            parts.Add(new Part($"Head.Ear.{e.Name}.R", meshR, bPos, Vector3.one, "SkinShaded", on: false, outerMat: "OutlineOuterFine"));
        }
    }

    // 오른귀 하나 — 그늘(머리까지 넣어 잰다) × 귀 안쪽 한 톤을 정점 색에.
    static Surface SculptGoblinEar(HeadShape basis, EarFrame f)
    {
        float Ear(Vector3 p) => GoblinEarSdf(p, f);
        float Scene(Vector3 p) => Mathf.Min(GoblinEarSdf(p, f), HeadSdf(p, basis));

        var b = new Bounds(f.Root, Vector3.zero);
        foreach (float a in new[] { -0.07f, f.E.L + 0.02f })
        foreach (float v in new[] { -0.24f, 0.14f })
        foreach (float n in new[] { -0.07f, 0.08f })
            b.Encapsulate(f.Root + f.U * a + f.V * v + f.N * n);

        var surf = SurfaceNets(Ear, b, GoblinEarStep, Scene, p => p, GoblinAoStep, GoblinAoGain, keep: 0.5f);
        for (int i = 0; i < surf.Verts.Count; i++)
        {
            float k = GoblinEarTint(surf.Verts[i], f);
            var c = surf.Colors[i];
            surf.Colors[i] = new Color(c.r * k, c.g * k, c.b * k, 1f);
        }
        return surf;
    }

    // ── 거들기 ──────────────────────────────────────────────────────────────

    // 돌림 머리의 거리장(그늘을 재는 데만 쓴다) — 원점 머리 한복판 · 턱을 앞으로 민 것(`Shear`)까지.
    static float HeadSdf(Vector3 p, HeadShape h)
    {
        float ux = p.x / h.Size.x, uy = p.y / h.Size.y, uz = p.z / h.Size.z;
        if (uy < 0f) uz -= h.Forward * 2f * uy * uy;
        float d = Mathf.Sqrt(ux * ux + uz * uz) - HeadRadius(Mathf.Clamp01(uy + 0.5f), h);
        return Mathf.Max(d, Mathf.Abs(uy) - 0.5f) * 0.6f;
    }

    // x를 뒤집은 사본 — 세모의 감김도 뒤집는다(안 뒤집으면 앞면이 안을 본다).
    static Surface MirrorX(Surface s)
    {
        var v = new List<Vector3>(s.Verts.Count);
        foreach (var p in s.Verts) v.Add(new Vector3(-p.x, p.y, p.z));
        var n = new List<Vector3>(s.Normals.Count);
        foreach (var p in s.Normals) n.Add(new Vector3(-p.x, p.y, p.z));
        var t = new List<int>(s.Tris.Count);
        for (int i = 0; i < s.Tris.Count; i += 3) { t.Add(s.Tris[i]); t.Add(s.Tris[i + 2]); t.Add(s.Tris[i + 1]); }
        return new Surface(v, n, new List<Color>(s.Colors), t);
    }

    // 닫힌 Catmull-Rom — 조종점마다 `n`개.
    static Vector2[] CatmullClosed(Vector2[] pts, int n)
    {
        var outP = new Vector2[pts.Length * n];
        int m = pts.Length;
        for (int i = 0; i < m; i++)
        {
            Vector2 p0 = pts[(i - 1 + m) % m], p1 = pts[i], p2 = pts[(i + 1) % m], p3 = pts[(i + 2) % m];
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                outP[i * n + k] = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
            }
        }
        return outP;
    }

    // 닫힌 꺾은선(성분 x = 높이 y · 성분 y = 앞 z)까지의 부호 거리 — 안 −.
    static float PolySdf(float py, float pz, Vector2[] poly)
    {
        float d = float.MaxValue; bool inside = false;
        for (int i = 0, m = poly.Length; i < m; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % m];
            float ey = b.x - a.x, ez = b.y - a.y, wy = py - a.x, wz = pz - a.y;
            float h = Mathf.Clamp01((wy * ey + wz * ez) / (ey * ey + ez * ez));
            float dy = wy - ey * h, dz = wz - ez * h;
            d = Mathf.Min(d, dy * dy + dz * dz);
            bool c1 = pz >= a.y, c2 = pz < b.y, c3 = ey * wz > ez * wy;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) inside = !inside;
        }
        d = Mathf.Sqrt(d);
        return inside ? -d : d;
    }

    // 표(x 오름차순)에서 선형으로 — 끝 밖은 끝 값.
    static float Interp(float x, Vector2[] table)
    {
        if (x <= table[0].x) return table[0].y;
        for (int i = 1; i < table.Length; i++)
            if (x <= table[i].x) return Mathf.Lerp(table[i - 1].y, table[i].y, (x - table[i - 1].x) / (table[i].x - table[i - 1].x));
        return table[^1].y;
    }

    static float GStep(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
    static float Sq(float x) => x * x;
}
