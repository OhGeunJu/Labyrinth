using System;
using System.Collections.Generic;
using UnityEngine;

// 사람 얼굴을 빚는다(2026-11-07, 사용자: *"원래 치비도 지금 다른 캐릭들처럼 외형을 좀 바꾸자"*) — 미리보기 `Tools/ModelLab/human/human.py`를 옮긴다.
//
// **머리 모양 표(`ChibiHeadProfiles`)는 그대로 살리고 얼굴만 빚는다** — 머리 살은 회전체 메시 그대로이고, 그 위에 머리 모양마다
// 먹 눈 · 반짝임 · 입 · 둥근 귀 · 코 셋 · 칠한 눈썹 셋을 빚어 붙인다(이목구비 판 · 아틀라스를 안 쓴다).
// - 코 · 눈썹은 고르기 축이다(`Head.Pick.Nose.*` · `Head.Pick.Brow.*`) — 머리 모양마다 한 벌씩 굽고 `@Shape.{머리}`로 매단다
//   (`PawnLook`이 지금 머리의 것만 켠다). 눈 · 반짝임 · 입 · 귀는 머리 모양의 조각(`Head.Shape.{머리}.Eyes`)이라 머리와 같이 켜진다.
// - **넓은 날카로운 머리는 뺀다**(사용자: *"실제 얼굴로 하기에는 너무 만화느낌"*) · **좁은 머리는 세로 0.88배**.
// - **여자 얼굴의 이목구비 자리 · 크기는 같은 모양의 남자 얼굴 값**(사용자: *"남자 이목구비랑 맞춰서"*) — 표의 여자 값은 그림 판 시절에
//   따로 재어 머리마다 들쭉날쭉했다. 눈 사이는 0.82배로 좁힌다(표의 간격은 그림 판 눈의 것).
// 고블린은 옛 얼굴 그대로다(`HeadParts`의 `human`이 거짓) — 이 파일은 사람만 받는다.
public static partial class ChibiPawnCreator
{
    const float HumanSlimH = 0.88f, HumanEyeSpread = 0.82f;
    static bool HumanDropped(string head) => head.EndsWith("BroadSharp");
    static readonly string[] HumanNoses = { "Button", "Straight", "Broad" };
    // 이름: 굵기 안 · 밖, 아치 높이, 기울기(미리보기 `BROWS`)
    static readonly (string Name, float Th0, float Th1, float Arch, float Tilt)[] HumanBrows =
    {
        ("Soft",  0.010f, 0.004f, 0.016f, -0.004f),
        ("Thick", 0.016f, 0.008f, 0.008f, -0.002f),
        ("Arch",  0.009f, 0.003f, 0.026f, -0.010f),
    };

    static Vector3 HumanSize(HeadShape h) => h.Name.Contains("Slim") ? new Vector3(h.Size.x, h.Size.y * HumanSlimH, h.Size.z) : h.Size;
    static Vector3 HumanHeadPos(HeadShape h) => new(0f, TorsoH + HeadGap + HumanSize(h).y * 0.5f, HeadZ);

    // 머리 한 벌의 얼굴 틀 — 미리보기 `Head`와 같은 셈(머리 한복판이 원점 · +z 앞 · 아래 반이 앞으로 쏠린다).
    sealed class HumanFrame
    {
        public readonly HeadShape H;
        public readonly float W, Ht, D, F, EY, MY, NY, EX, ER, ZE, ZN, ZM, R0, Aspect;

        public HumanFrame(HeadShape h, HeadShape layout)
        {
            H = h; var s = HumanSize(h); W = s.x; Ht = s.y; D = s.z; F = h.Forward;
            float et = layout.EyeT, mt = layout.MouthT, nt = layout.NoseT;
            EY = (et - 0.5f) * Ht; MY = (mt - 0.5f) * Ht; NY = (nt - 0.5f) * Ht;
            EX = layout.EyeDx * W * HumanEyeSpread;
            ER = layout.EyeSize * 0.6f / layout.Size.x * W * 0.5f;   // 판 한 변(`EyeSize`) → 눈 지름 → 반지름
            ZE = Front(et); ZN = Front(nt); ZM = Front(mt);
            R0 = HeadRadius(et, h) * W;
            Aspect = h.EyeAspect;
        }

        public float Front(float t) { float yn = t - 0.5f; return (HeadRadius(Mathf.Clamp01(t), H) + (yn < 0f ? F * 2f * yn * yn : 0f)) * D; }

        public float Head(Vector3 p)
        {
            float ux = p.x / W, uy = p.y / Ht, uz = p.z / D;
            if (uy < 0f) uz -= F * 2f * uy * uy;
            float d = Mathf.Sqrt(ux * ux + uz * uz) - HeadRadius(Mathf.Clamp01(uy + 0.5f), H);
            return Mathf.Max(d, Mathf.Abs(uy) - 0.5f) * 0.6f;
        }
    }

    // ── 얼굴 ────────────────────────────────────────────────────────────────

    static float HumanNose(Vector3 p, HumanFrame f, string kind)
    {
        var m = Mirror(p); float z = f.ZN, ny = f.NY;
        switch (kind)
        {
            case "Button":     // 작고 둥근 코끝 — 콧대는 거의 없다
                return SMin(Sph(p, V3(0, ny + 0.004f, z + 0.016f), 0.024f), Sph(m, V3(0.020f, ny - 0.006f, z + 0.002f), 0.016f), 0.010f);
            case "Straight":   // 곧은 콧대 · 작은 끝
                return SMin(Seg(p, V3(0, f.EY - 0.005f, f.ZE), V3(0, ny + 0.004f, z + 0.026f), 0.011f, 0.018f),
                            Ell(p, V3(0, ny - 0.002f, z + 0.024f), V3(0.024f, 0.019f, 0.020f)), 0.012f);
            default:           // 넓고 둥근 코 — 콧방울이 벌어진다
                return SMin(Ell(p, V3(0, ny - 0.002f, z + 0.014f), V3(0.040f, 0.026f, 0.028f)), Sph(m, V3(0.032f, ny - 0.010f, z + 0.002f), 0.020f), 0.012f);
        }
    }

    static float HumanEar(Vector3 p, HumanFrame f)   // 둥근 사람 귀 — 머리 옆에 세운 납작한 콩 · 안쪽은 오목
    {
        var q = Mirror(p) - V3(f.R0 - 0.004f, (f.EY + f.NY) * 0.5f - 0.005f, -0.03f);
        float shell = Ell(q, Vector3.zero, V3(0.030f, 0.062f, 0.046f));
        float cup = Ell(q, V3(0.020f, 0.004f, 0.006f), V3(0.016f, 0.042f, 0.030f));
        return SMax(shell, -cup, 0.006f);
    }

    // 머리 살에 녹이는 조각(코 · 귀) — 미리보기는 머리에 녹였다. 녹인 이음까지 조각이 들고, 머리 살과 겹치는 면은 2mm 안으로 숨긴다
    // (같은 자리에 면 둘이면 번갈아 비친다) · 머리 속은 덜어 낸다 · 조각 둘레 3cm 안만(엘프 귀 `ElfEarPiece`와 같은 길).
    static float HumanMelt(Vector3 p, HumanFrame f, float piece, float k)
    {
        if (piece > 0.08f) return piece;
        float head = f.Head(p);
        float d = SMin(head + 0.002f, piece, k);
        d = SMax(d, -(head + 0.006f), 0.002f);
        return SMax(d, piece - 0.03f, 0.005f);
    }

    // 얼굴 살 위 `out` 두께의 껍데기 중 `region` 안(미리보기 `on_face` — 옆에서 봐도 면에 붙는다).
    static float OnFace(Vector3 p, HumanFrame f, float region, float out_, float inner = 0.010f)
    {
        if (region > 0.03f) return region;
        float fd = f.Head(p);
        float shell = Mathf.Max(fd - out_, -(fd + inner));
        return SMax(Mathf.Max(shell, region), (f.ZE - 0.12f) - p.z, 0.002f);
    }

    static float HumanEyes(Vector3 p, HumanFrame f)
        => OnFace(p, f, Ell(Mirror(p), V3(f.EX, f.EY - 0.004f, f.ZE), V3(f.ER * 0.80f, f.ER * 0.92f, 0.3f)), 0.004f);
    static float HumanGlint(Vector3 p, HumanFrame f)
        => OnFace(p, f, Ell(Mirror(p), V3(f.EX - f.ER * 0.25f, f.EY + f.ER * 0.25f, f.ZE), V3(f.ER * 0.25f, f.ER * 0.25f, 0.3f)), 0.007f);
    static float HumanMouth(Vector3 p, HumanFrame f)
        => OnFace(p, f, Mathf.Max(Mathf.Abs(p.y - (f.MY + 0.004f)) - 0.004f, Mathf.Abs(p.x) - 0.040f), 0.003f);

    static float HumanBrow(Vector3 p, HumanFrame f, (string, float Th0, float Th1, float Arch, float Tilt) b)   // 살갗에 칠한 눈썹 띠
    {
        var m = Mirror(p);
        float x0 = f.EX - f.ER * 1.3f, x1 = f.EX + f.ER * 1.4f;
        float t = Mathf.Clamp01((m.x - x0) / (x1 - x0));
        float yc = f.EY + f.ER / Mathf.Sqrt(f.Aspect) + 0.040f + b.Arch * Mathf.Sin(t * Mathf.PI * 0.9f) + b.Tilt * t;
        float th = b.Th0 * (1f - t) + b.Th1 * t;
        return OnFace(p, f, Mathf.Max(Mathf.Abs(m.y - yc) - th, Mathf.Max(x0 - m.x, m.x - x1)), 0.003f);
    }

    // ── 부위 ────────────────────────────────────────────────────────────────

    // 머리 모양 하나의 얼굴 조각 — 구운 메시는 머리 이름으로 담아 둔다(남녀 · 역할 체형마다 다시 빚지 않는다).
    static readonly Dictionary<string, List<Part>> _humanFace = new();

    static List<Part> HumanFaceParts(HeadShape h)
    {
        if (_humanFace.TryGetValue(h.Name, out var cached)) return cached;
        var layout = h.Gender == "Female" ? Array.Find(Heads, x => x.Name == h.Name.Replace("Female_", "Male_")) : h;
        if (layout.Name == null) layout = h;
        var f = new HumanFrame(h, layout);
        var pos = HumanHeadPos(h);
        var parts = new List<Part>();
        Func<Vector3, float> head = f.Head;

        Mesh Sculpt(string name, Func<Vector3, float> sdf, Bounds b, float step, float keep, bool relax = false)
        {
            var surf = SurfaceNets(sdf, b, step, head, p => p, 0.03f, 4f, keep);
            if (relax) Relax(surf.Verts, surf.Tris, 6);
            return SaveSkull($"Chibi_Human_{h.Name}_{name}", surf);
        }
        void Add(string name, Mesh mesh, string mat, string line, bool on = true)
            => parts.Add(new Part(name, mesh, pos, Vector3.one, mat, on, outerMat: line));

        string sh = $"@Shape.{h.Name}";

        // 눈 · 반짝임 · 입 — 머리 모양의 조각(머리와 같이 켜진다).
        var eyeB = new Bounds(new Vector3(0f, f.EY, f.ZE - 0.02f), new Vector3(2f * (f.EX + f.ER) + 0.04f, 2f * f.ER + 0.04f, 0.10f));
        Add($"Head.Shape.{h.Name}.Eyes",  Sculpt("Eyes",  p => HumanEyes(p, f),  eyeB, 0.002f, 0.5f),  "NoseDark", null);
        Add($"Head.Shape.{h.Name}.Glint", Sculpt("Glint", p => HumanGlint(p, f), eyeB, 0.0015f, 0.5f), "Bone", null);
        var mouthB = new Bounds(new Vector3(0f, f.MY + 0.004f, f.ZM - 0.01f), new Vector3(0.12f, 0.04f, 0.08f));
        Add($"Head.Shape.{h.Name}.Mouth", Sculpt("Mouth", p => HumanMouth(p, f), mouthB, 0.0015f, 0.5f), "HumanLip", null);

        // 귀 — 둥근 사람 귀 하나(머리 살에 녹인다).
        var earB = new Bounds(new Vector3(0f, (f.EY + f.NY) * 0.5f, -0.03f), new Vector3(2f * f.R0 + 0.16f, 0.22f, 0.18f));
        Add($"Head.Shape.{h.Name}.Ear", Sculpt("Ear", p => HumanMelt(p, f, HumanEar(p, f), 0.012f), earB, 0.003f, 0.4f, relax: true), "SkinFine", "OutlineOuterFine");

        // 코 셋 · 눈썹 셋 — 고르기 축(이 머리일 때만).
        var noseB = new Bounds(new Vector3(0f, (f.EY + f.NY) * 0.5f, f.ZN), new Vector3(0.16f, f.EY - f.NY + 0.12f, 0.14f));
        foreach (var n in HumanNoses)
        {
            string k = n;
            Add($"Head.Pick.Nose.{k}{sh}", Sculpt($"Nose_{k}", p => HumanMelt(p, f, HumanNose(p, f, k), 0.010f), noseB, 0.002f, 0.4f, relax: true),
                "SkinFine", "OutlineOuterFine", on: k == HumanNoses[0]);
        }
        var browB = new Bounds(new Vector3(0f, f.EY + f.ER + 0.05f, f.ZE - 0.04f), new Vector3(2f * (f.EX + f.ER * 1.5f) + 0.04f, 0.12f, 0.20f));
        foreach (var b in HumanBrows)
        {
            var bb = b;
            Add($"Head.Pick.Brow.{b.Name}{sh}", Sculpt($"Brow_{b.Name}", p => HumanBrow(p, f, bb), browB, 0.0015f, 0.4f),
                "HairFine", "OutlineOuterFine", on: b.Name == HumanBrows[0].Name);
        }

        _humanFace[h.Name] = parts;
        return parts;
    }
}
