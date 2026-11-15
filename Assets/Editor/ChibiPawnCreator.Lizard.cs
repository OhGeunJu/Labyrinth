using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **리자드맨 — 미리보기 v9를 옮긴 것**(2026-11-12 — 사용자가 v9를 확정한 뒤).
//
// 모양은 파이썬 미리보기(`Tools/ModelLab/lizard/lizard.py` — 같은 거리장 식을 numpy로 그린 것)에서 사용자가 판마다 보고 고른 것이다.
// **수치를 고치면 그 그림과 어긋난다 — 미리보기를 먼저 고쳐 보여 주고 같이 옮길 것.** 단위 m · 발밑 0 · +z 앞 · 머리 부품은 머리 중심 원점.
//
// 큰 짐승(`ChibiPawnCreator.Brutes`)과 같은 길이다 — 거리장으로 빚어 표면을 뽑고(`SurfaceNets`) **관절마다 굳은 조각**으로 붙인다.
// 관절은 사람과 같은 `BodyJoints`라 클립이 그대로 돈다. 리자드맨만의 것:
//
//   - **굽은 등**(`LizFrame.Zs` — 허리 위로 높이의 제곱만큼 앞으로 · 트롤과 같은 식) · 머리는 그 앞에 처져 달린다(`HeadFwd` · `HeadDrop`) ·
//     팔 뼈도 굽은 만큼 앞에 선다(`BoneBody.ArmZ`).
//   - **짐승 다리** — 엉덩이 → 무릎(앞) → 뒤꿈치(뒤 · 들림) → 발볼. **뼈대는 사람 것 그대로 두고 모양만 그렇게 빚는다**: 허벅지 조각은 위다리 뼈,
//     정강이 + 발등 한 덩이는 아래다리 뼈, 발볼 · 발가락은 발 뼈. 무릎 뼈 자리(다리 한가운데)와 보이는 무릎(13cm 앞)이 달라 걸을 때 무릎에서
//     두 조각이 조금 미끄러진다 — **게임에서 봐야 한다.**
//   - **꼬리** — 엉덩이 뼈에 붙은 굳은 조각 하나(아직 안 흔들린다 — 꼬리 뼈는 다음).
//   - **잎 비늘**(`LizLeaf` — 숲살모사 · 누런 구렁이) — 뒤로 누워 겹친 납작한 잎. 끝이 종이처럼 얇아 표면 뽑기로는 뭉개지므로 **잎마다 메시를 직접 짓는다**.
//   - **색은 팔레트다**(2026-11-14 — 사용자: *"색만 다른데 왜 프리팹으로 나뉘어 · 리자드맨도 합치고 싶었어 · 색이 많으니 칸을 더 많이"*).
//     살 메시의 정점은 색이 아니라 **칸마다 몫**을 든다 — 바탕 칸(정점 색 rgba: 살 · 배 · (빈 칸) · 콧구멍)과 **덧칠 칸**(둘째 UV xyzw — 색마다
//     있는 무늬: 뱀은 붉은 뱀의 꼬리 그늘 · 보엘렌 · 인디고의 무지갯빛 · 보엘렌의 노란 띠와 빗금 · 인디고의 녹슨 턱 / 살모사는 앰버의 녹슨 등 띠).
//     덧칠 칸의 알파가 불투명도라 그 무늬가 없는 색은 칸을 끈다(`ToonOutline._VertexPal`). 칸 색은 개체마다 `PaletteLook`이 고른다
//     (재질 `Chibi_LizPal_{종류}` · 살모사 잎 비늘 `Chibi_LizScale_Viper` · 기본 눈알 `Chibi_LizEye_{Viper|Snake}` — 재질 값은 첫 팔레트).
//     **색에 딸린 모양**(누런 구렁이의 비늘 · 매끈한 뱀의 색마다 다른 가는 줄 덧판)은 그 팔레트일 때만 켠다(`@Palette.{이름}` — `PawnLook.PaletteAxis`).
//     가는 줄(입술 세로 줄 · 눈 밑 줄)은 정점 간격보다 가늘어 살 위 1cm에 띄운 얇은 덧판이다. 등줄기 돌기 · 볏 · 골판처럼 색이 정해진 조각은
//     흰 살 재질(`Chibi_LizSkin`)에 정점 색을 곱한다(`_VertexAO`).
//   - 이름이 `Chibi_Skin…`이 아니라 개체 살색(`SkinLook`)은 안 탄다.
//
// 종류 넷 · **종류마다 프리팹 하나**(보기 몸 — 아직 어디서도 안 가리킨다 · `3_Prefab/Pawn/Lizard/`):
//   도마뱀 `Chibi_Lizard`(볏은 고르기 축 `Crest` — 가시 테 · 이구아나 볏 · 없음) · 악어 `Chibi_Lizard_Croc`(몸도 굵다) ·
//   숲살모사 `Chibi_Lizard_Viper`(팔레트 Green · Slate · Coal · Amber) · 매끈한 뱀 `Chibi_Lizard_Snake`(팔레트 Boelen · Kingrat · Indigo · Red).
//   색은 무리 안에서도 섞여 나온다(엔트와 같다 — 세력을 세울 때 다시 본다). 옛 색 프리팹(`…ViperGreen` · `…SnakeBoelen`)은 새 이름으로 옮겨 guid를 지키고
//   나머지 색 프리팹은 지운다(`MigrateLizardTones`).
// 눈은 고르기 축(`Eyes` 여덟 — 사용자: *"뾰족한 눈 · 귀여운 눈 · 그 중간으로 넓게"*) · 미리보기의 기본 눈이 40%.
public static partial class ChibiPawnCreator
{
    // ── 몸 ──────────────────────────────────────────────────────────────────
    //                                                                        밑     엉덩이  허리   가슴   어깨      깊이   손     팔K    다리K  몸통K  팔굵기 다리굵기 어깨둥글림
    static readonly Shape LizardShape = new("Lizard", "리자드맨", "Male", new[] { 0.23f, 0.26f, 0.27f, 0.33f, 0.35f }, 0.88f, 0.32f, 1.12f, 1.37f, 1.05f, 1.15f, 1.12f, 0.42f);
    static readonly Shape CrocShape   = new("LizardCroc", "악어 리자드맨", "Male", new[] { 0.29f, 0.33f, 0.36f, 0.44f, 0.47f }, 1.02f, 0.39f, 1.12f, 1.30f, 1.12f, 1.45f, 1.32f, 0.34f);

    const float LizHeadFwd = 0.17f, LizHeadDrop = 0.17f;   // 머리가 굽은 어깨보다 앞으로 · 아래로
    const float LizGap = 0.007f;                           // 입선의 반 틈
    const float LizFlat = 0.36f;                           // 잎 비늘의 두께 / 폭

    static readonly Color LzBone = new(0.90f, 0.86f, 0.72f), LzDark = new(0.06f, 0.05f, 0.05f), LzMouth = new(0.30f, 0.07f, 0.08f), LzWhite = new(0.96f, 0.96f, 0.96f);
    static readonly Color LzLeather = new(0.34f, 0.22f, 0.13f), LzCloth = new(0.42f, 0.14f, 0.11f);
    static readonly Color LzAmber = new(0.95f, 0.72f, 0.16f), LzGreen = new(0.62f, 0.82f, 0.25f), LzPale = new(0.80f, 0.87f, 0.92f), LzBlack = new(0.05f, 0.05f, 0.06f);
    static readonly Color LzGrey = new(0.62f, 0.58f, 0.52f), LzYellow = new(0.93f, 0.80f, 0.20f);

    // 종류의 몸 수치 한 벌 — 미리보기 `set_body`. 몸 거리장 · 색이 다 여기서 읽는다(읽기만 — 표면 뽑기가 여러 가닥에서 부른다).
    sealed class LizFrame
    {
        public readonly BodyDims D;
        public readonly float HK, Lean, NeckR, TailK, Y0, Top, ArmZ;
        public readonly Vector3 HO;                                 // 머리 중심
        public readonly Vector3[] Tail = new Vector3[13];
        public readonly float[] TailR = new float[13], TZ = new float[13], TY = new float[13];
        public readonly Vector3 Hip, Knee, Heel, Ball;              // 오른 다리(+x)
        public readonly Vector3 Shoulder, Elbow, Wrist, HandC;      // 오른 팔
        public readonly Vector3 NeckA, NeckB;                       // 목 — 어깨 쪽 · 머리 쪽

        public LizFrame(Shape s, float hk, float lean, float neckR, float tailK)
        {
            D = DimsOf(s); HK = hk; Lean = lean; NeckR = neckR; TailK = tailK;
            Top = D.Top; Y0 = D.Lift + 0.22f * D.TorsoH;
            HO = new Vector3(0f, Top + HeadGap + D.Hh * 0.5f - LizHeadDrop, HeadZ + Zs(Top) + LizHeadFwd);
            // 꼬리 — 엉덩이에서 뒤로 내려가 땅 가까이에서 눕는다. 3차 곡선 열두 마디 · 굵기는 뿌리에서 끝까지 한 번에 준다.
            float rt = Mathf.Sqrt(tailK);
            var tp = new[] { new Vector2(D.Lift + 0.115f, -0.08f), new Vector2(D.Lift - 0.02f, -0.48f * rt), new Vector2(0.11f * tailK, -0.78f * rt), new Vector2(0.10f * tailK, -1.26f * rt) };
            for (int i = 0; i < 13; i++)
            {
                float t = i / 12f, u = 1f - t;
                var b = u * u * u * tp[0] + 3f * u * u * t * tp[1] + 3f * u * t * t * tp[2] + t * t * t * tp[3];
                Tail[i] = new Vector3(0f, b.x, b.y);
                TailR[i] = 0.168f * tailK * Mathf.Pow(1f - t, 1.25f) + 0.006f;
                TZ[i] = -b.y; TY[i] = b.x;
            }
            ArmZ = Zs(D.ShoulderY);
            Shoulder = new Vector3(D.ArmX, D.ShoulderY, ArmZ); Wrist = new Vector3(D.ArmX, D.HandY, ArmZ + 0.04f);
            Elbow = (Shoulder + Wrist) * 0.5f;
            HandC = new Vector3(D.ArmX, D.HandY - D.Hand * 0.25f, ArmZ + 0.04f);
            float L = D.Lift;
            Hip = new Vector3(D.LegX, D.HipY, 0f); Knee = new Vector3(D.LegX, L * 0.66f, 0.135f);
            Heel = new Vector3(D.LegX, L * 0.30f, -0.105f); Ball = new Vector3(D.LegX, 0.060f, 0.055f);
            NeckA = new Vector3(0f, Top - 0.08f, Zs(Top) - 0.05f); NeckB = HO + new Vector3(0f, -0.07f, -0.13f) * HK;
        }

        public float Zs(float y) { float d = Mathf.Max(y - Y0, 0f); return Lean * d * d; }   // 굽은 등
        public float TorsoR(float y) => BodyTorsoRadius(D.Half, Mathf.Clamp01((y - D.Lift) / D.TorsoH), D.TorsoH, D.TopR);
        public float BackZ(float y) => -TorsoR(y) * D.Depth + Zs(y);

        public float Torso(Vector3 p)
        {
            float z = p.z - Zs(p.y), y = p.y - D.Lift;
            float q = Mathf.Sqrt(p.x * p.x + z / D.Depth * (z / D.Depth));
            return Mathf.Max((q - TorsoR(p.y)) * D.Depth, Mathf.Max(-y, y - D.TorsoH)) * 0.9f;
        }
        public float Neck(Vector3 p) => Seg(p, NeckA, NeckB, NeckR * 1.18f, NeckR);
        public float TailSdf(Vector3 p, int upTo = 12)
        {
            float d = Seg(p, Tail[0], Tail[1], TailR[0], TailR[1]);
            for (int i = 1; i < upTo; i++) d = SMin(d, Seg(p, Tail[i], Tail[i + 1], TailR[i], TailR[i + 1]), 0.02f);
            return d;
        }
        // 몸통 조각 — 몸통 + 목 + 꼬리 뿌리로 넘어가는 둥근 이음(꼬리 조각이 덮게 3mm 가늘게).
        public float TorsoPiece(Vector3 p) => SMin(SMin(Torso(p), Neck(p), 0.05f), TailSdf(p, 3) + 0.003f, 0.06f);

        // 팔다리 · 손발(오른쪽에 적는다 — `Side`가 뒤집는다).
        public float Upper(Vector3 m) => Capsule(m, Elbow, Shoulder, D.ArmR);
        public float Fore(Vector3 m)  => Capsule(m, Wrist, Elbow, D.ArmR);
        public float Hand(Vector3 m)  => Sph(m, HandC, D.Hand * 0.5f);
        public float HandClaws(Vector3 m)
        {
            float r = D.Hand * 0.5f, d = 9f; var c = HandC;
            foreach (float dx in new[] { -0.05f, 0f, 0.05f })
                d = Mathf.Min(d, Seg(m, new Vector3(c.x + dx, c.y - r * 0.55f, c.z + 0.045f), new Vector3(c.x + dx * 1.3f, c.y - r * 1.35f, c.z + 0.085f), 0.022f * r / 0.118f, 0.003f));
            return d;
        }
        public float Thigh(Vector3 m) => Seg(m, Hip, Knee, D.LegR * 1.22f, D.LegR * 1.02f);
        public float Shin(Vector3 m)  => SMin(Seg(m, Knee, Heel, D.LegR * 0.98f, D.LegR * 0.66f), Seg(m, Heel, Ball, D.LegR * 0.66f, D.LegR * 0.70f), 0.03f);
        static readonly float[] Toes = { -0.075f, 0f, 0.075f };
        public float Foot(Vector3 m)   // 발끝으로 선다 — 발볼에서 벌어진 발가락 셋
        {
            float k = D.LegR / 0.106f;
            float pad = Ell(m, Ball + new Vector3(0f, -0.005f, 0.02f), new Vector3(0.105f * k, 0.055f, 0.10f * k)), toes = 9f;
            foreach (float dx in Toes)
                toes = Mathf.Min(toes, Seg(m, Ball + new Vector3(dx * 0.6f * k, -0.012f, 0.05f), Ball + new Vector3(dx * 1.4f * k, -0.02f, 0.215f * k), 0.042f * k, 0.032f * k));
            return SMin(pad, toes, 0.03f);
        }
        public float FootClaws(Vector3 m)
        {
            float k = D.LegR / 0.106f, d = 9f;
            foreach (float dx in Toes)
                d = Mathf.Min(d, Seg(m, Ball + new Vector3(dx * 1.4f * k, -0.018f, 0.21f * k), Ball + new Vector3(dx * 1.6f * k, -0.045f, 0.30f * k), 0.026f * k, 0.003f));
            return d;
        }

        // 등줄기 돌기(도마뱀 · 악어) — 몸통 것 · 꼬리 것. 끝으로 갈수록 작아지다 사라진다.
        public float RidgeBack(Vector3 p)
        {
            float d = 9f, k = Mathf.Sqrt(TailK);
            for (int i = 0; i < 5; i++)
            {
                float y = Mathf.Lerp(D.Lift + 0.22f, Top - 0.08f, i / 4f), zb = BackZ(y);
                d = Mathf.Min(d, Seg(p, new Vector3(0f, y, zb + 0.03f), new Vector3(0f, y + 0.02f, zb - 0.075f * k), 0.042f * k, 0.004f));
            }
            return d;
        }
        public float RidgeTail(Vector3 p)
        {
            float d = 9f;
            for (int i = 1; i < 9; i++)
            {
                var c = Tail[i]; float r = TailR[i];
                var ax = (Tail[i + 1] - Tail[i - 1]).normalized; var up = Vector3.Cross(Vector3.Cross(ax, Vector3.up), ax).normalized;
                d = Mathf.Min(d, Seg(p, c + up * (r - 0.02f), c + up * (r + 0.015f + r * 0.42f) + ax * 0.03f, 0.022f + r * 0.14f, 0.004f));
            }
            return d;
        }

        public float Belt(Vector3 p) => Mathf.Max(Torso(p) - 0.014f, Mathf.Abs(p.y - (D.HemY + 0.02f)) - 0.042f);
        public float Flap(Vector3 p)   // 앞 가리개 — 뒤는 꼬리가 나와 비운다
        {
            float zf = TorsoR(D.HemY) * D.Depth;
            return RBox(p, new Vector3(0f, D.HemY - 0.155f, zf + 0.012f), new Vector3(0.105f * D.Half[1] / 0.246f, 0.185f, 0.014f), 0.012f);
        }

        // 몸을 따라 내려가는 자 — 몸통에선 높이, 꼬리에선 뒤로 간 거리를 이어 붙인다(띠 무늬가 꼬리에서 꺾이지 않게). `w` — 꼬리 몫.
        public float Along(Vector3 p, out float w)
        {
            w = SStep(-0.18f, -0.36f, p.z) * SStep(D.Lift + 0.28f, D.Lift + 0.10f, p.y);
            return p.y * (1f - w) + (D.Lift + 0.14f + p.z) * w;
        }
        // 배 몫 — 배 · 꼬리 밑은 배 색(0 살 ~ 1 배). 색은 팔레트가 든다.
        public float BellyK(Vector3 p)
        {
            float zr = p.z - Zs(p.y);
            float front = SStep(0.02f, 0.10f, zr) * SStep(0.19f, 0.13f, Mathf.Abs(p.x) * 0.246f / D.Half[1]) * SStep(D.Lift, D.Lift + 0.06f, p.y);
            float ay = Interp(-p.z, TZ, TY), ar = Interp(-p.z, TZ, TailR);
            float under = SStep(-0.22f, -0.34f, p.z) * SStep(-0.15f, -0.55f, (p.y - ay) / Mathf.Max(ar, 0.01f)) * SStep(D.Lift + 0.2f, D.Lift + 0.1f, p.y);
            return Mathf.Max(front, under);
        }
        // 등줄기 몫 — 등 한가운데 띠 + 꼬리 윗면(볏도마뱀붙이의 불꽃 무늬 · 등 줄). 0 ~ 1.
        public float DorsalK(Vector3 p)
        {
            float x = Mathf.Abs(p.x), zr = p.z - Zs(p.y);
            float back = SStep(0.13f, 0.06f, x) * SStep(-0.02f, -0.09f, zr) * SStep(D.Lift + 0.10f, D.Lift + 0.25f, p.y);
            float ay = Interp(-p.z, TZ, TY), ar = Mathf.Max(Interp(-p.z, TZ, TailR), 0.01f);
            float tail = SStep(-0.22f, -0.34f, p.z) * SStep(0.25f, 0.65f, (p.y - ay) / ar) * SStep(0.75f * ar, 0.35f * ar, x);
            return Mathf.Max(back, tail);
        }
    }

    // **반점 배치 8벌을 구워 둔다**(11-14 사용자: *"점 있는 거 좀 징그러운데"* → 성긴 찌그러진 반점(B안) · *"색마다 · 개체마다 위치가 바뀌면"* →
    // *"한 8개 정도로 정해 놓고 돌려쓰면"*). 살 메시의 셋째 · 넷째 UV가 배치 8벌의 몫 — 개체는 `PaletteLook`이 하나를 고르고 셰이더는 고른 칸만 읽는다
    // (`ToonOutline._SpotBaked` · 매 프레임 셈하지 않는다). 반점이 날 자리(살 쪽 · 배는 안)는 덧칠 칸 4의 몫(`LizBodyW` · `LizHeadW`).
    const int LizSpotLayouts = 8;
    const float LizSpotCell = 0.110f, LizSpotR = 0.030f, LizSpotKeep = 0.55f;
    static Vector3 LizSpotShift(int i) => new(i * 1.37f, i * 2.11f, i * 0.73f);   // 배치마다 무늬를 민다
    // 띄엄띄엄한 찌그러진 반점 — 칸마다 나올지 굴리고 덩이 두셋을 붙여 찌그러뜨리고 크기 0.6 ~ 1.6배 · 가장자리 일렁임. 0 ~ 1(반점 안 1).
    static float LizSpots(Vector3 p, float cell, float r, float keep)
    {
        static float H(Vector3 c, float k) { float v = Mathf.Sin(c.x * 127.1f + c.y * 311.7f + c.z * 74.7f + k) * 43758.5453f; return v - Mathf.Floor(v); }
        var g = new Vector3(Mathf.Floor(p.x / cell), Mathf.Floor(p.y / cell), Mathf.Floor(p.z / cell));
        float best = 9f;
        for (int dz = -1; dz <= 1; dz++) for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
        {
            var c = g + new Vector3(dx, dy, dz);
            if (H(c, 9.7f) > keep) continue;
            var at = (c + new Vector3(0.2f + 0.6f * H(c, 0f), 0.2f + 0.6f * H(c, 1.3f), 0.2f + 0.6f * H(c, 2.7f))) * cell;
            float rr = r * (0.6f + 1.0f * H(c, 4.1f));
            var dir1 = new Vector3(H(c, 5.3f) - 0.5f, H(c, 6.1f) - 0.5f, H(c, 7.9f) - 0.5f).normalized;
            var dir2 = new Vector3(H(c, 8.3f) - 0.5f, H(c, 3.3f) - 0.5f, H(c, 2.1f) - 0.5f).normalized;
            float lobe = Mathf.Min((p - at).magnitude - rr,
                         Mathf.Min((p - (at + dir1 * rr * 0.75f)).magnitude - rr * 0.65f,
                                   H(c, 6.6f) < 0.5f ? (p - (at + dir2 * rr * 0.70f)).magnitude - rr * 0.55f : 9f));
            best = Mathf.Min(best, lobe);
        }
        best += 0.004f * Mathf.Sin(p.x * 61f + p.y * 47f) * Mathf.Sin(p.z * 53f - p.y * 29f);
        return SStep(0.004f, -0.004f, best);
    }

    static Vector3 Squash(Vector3 p, float y0, float sy) => new(p.x, y0 + (p.y - y0) / sy, p.z);   // y0 기준으로 세로를 sy배 눌러 본다(거리에 sy를 곱해 쓴다)
    static float Line2(float a, float b, float a0, float b0, float a1, float b1)                  // (a, b) 평면의 선분까지 거리
    {
        float da = a1 - a0, db = b1 - b0;
        float t = Mathf.Clamp01(((a - a0) * da + (b - b0) * db) / (da * da + db * db));
        return Mathf.Sqrt((a - a0 - t * da) * (a - a0 - t * da) + (b - b0 - t * db) * (b - b0 - t * db));
    }
    static float ColDiff(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    static Color Mul(Color c, float k) => new(c.r * k, c.g * k, c.b * k, 1f);

    // ── 잎 비늘 ─────────────────────────────────────────────────────────────
    //
    // 뒤로 누워 겹친 납작한 잎(사용자 11-12: *"가시는 징그럽다 · 사진과 안 닮았다"*) — 곤두선 가시가 아니라 살에 누운 잎이고, 끝만 살짝 들려
    // 앞줄이 뒷줄 밑동을 덮는다(솔방울). 미리보기 `Leaves` 그대로: 둥근 밑동 → 뾰족한 끝 · 두께는 폭의 `LizFlat`배.
    readonly struct LizLeaf
    {
        public readonly Vector3 B, D, S, U; public readonly float Ln, Wd;
        public LizLeaf(Vector3 basePt, Vector3 back, Vector3 n, float ln, float wd, float lift)
        {
            n = n.normalized; back = (back - n * Vector3.Dot(back, n)).normalized;
            D = (back + n * lift).normalized; S = Vector3.Cross(n, D).normalized; U = Vector3.Cross(D, S);
            B = basePt - D * (wd * 0.25f); Ln = ln; Wd = wd;
        }
        LizLeaf(Vector3 b, Vector3 d, Vector3 s, Vector3 u, float ln, float wd) { B = b; D = d; S = s; U = u; Ln = ln; Wd = wd; }
        public bool Central => Mathf.Abs(B.x) < 1e-4f && Mathf.Abs(D.x) < 1e-4f;
        public LizLeaf Flipped() { var f = new Vector3(-1f, 1f, 1f); return new LizLeaf(Vector3.Scale(B, f), Vector3.Scale(D, f), Vector3.Scale(S, f), Vector3.Scale(U, f), Ln, Wd); }
        public LizLeaf Placed(Vector3 o, float k) => new(o + B * k, D, S, U, Ln * k, Wd * k);   // 머리 좌표 → 몸 좌표
    }

    // 잎 묶음을 메시 하나로 — 잎마다 고리 넷(밑동 덮개 · 밑동 · 가운데 둘) + 양 끝 꼭짓점. 색은 밑동(`dark`)에서 끝(`tip`)으로(`e0`~`e1`).
    // `side` — 0이면 좌우 다(가운데 잎은 한 번) · ±1이면 그쪽만. `weights`면 색 대신 팔레트 몫(칸 0 밑동 · 칸 1 끝 — 살모사 비늘).
    static Surface LeafSurface(List<LizLeaf> leaves, int side, Color dark, Color tip, float e0, float e1, bool weights = false)
    {
        const int Segs = 6;
        var V = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>();
        bool lin = PlayerSettings.colorSpace == ColorSpace.Linear;
        Color At(float h)
        {
            float t = SStep(e0, e1, h);
            if (weights) return new Color(1f - t, t, 0f, 0f);
            var c = Color.Lerp(dark, tip, t); c.a = 1f; return lin ? c.linear : c;
        }
        void One(LizLeaf f)
        {
            float r0 = f.Wd * 0.5f;
            float R(float h) => 0.005f + (r0 - 0.005f) * Mathf.Pow(1f - h, 0.75f);
            // (잎 축 위의 자리 u · 반지름 · 색 자리 h)
            var rings = new (float u, float r, float h)[] { (-r0 * 0.7071f, r0 * 0.7071f, 0f), (0f, r0, 0f), (f.Ln * 0.35f, R(0.35f), 0.35f), (f.Ln * 0.70f, R(0.70f), 0.70f) };
            int o = V.Count;
            V.Add(f.B - f.D * r0); N.Add(-f.D); C.Add(At(0f));
            foreach (var (u, r, h) in rings)
                for (int k = 0; k < Segs; k++)
                {
                    float a = 2f * Mathf.PI * k / Segs, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    V.Add(f.B + f.D * u + f.S * (r * ca) + f.U * (r * LizFlat * sa));
                    N.Add((f.S * ca + f.U * (sa / LizFlat) + f.D * (u < 0f ? -0.7f : 0.25f)).normalized);
                    C.Add(At(h));
                }
            int tipI = V.Count;
            V.Add(f.B + f.D * (f.Ln + 0.005f)); N.Add(f.D); C.Add(At(1f));
            // 세모의 앞면은 잎 축에서 바깥을 본다 — 좌우를 뒤집은 잎도 같은 식으로 맞는다.
            void Tri(int a, int b, int c)
            {
                var mid = (V[a] + V[b] + V[c]) / 3f; var q = mid - f.B;
                var outward = mid - (f.B + f.D * Mathf.Clamp(Vector3.Dot(q, f.D), -r0 * 0.5f, f.Ln));
                if (Vector3.Dot(Vector3.Cross(V[b] - V[a], V[c] - V[a]), outward) < 0f) (b, c) = (c, b);
                T.Add(a); T.Add(b); T.Add(c);
            }
            int Ring(int i, int k) => o + 1 + i * Segs + k % Segs;
            for (int k = 0; k < Segs; k++)
            {
                Tri(o, Ring(0, k), Ring(0, k + 1));
                for (int i = 0; i + 1 < rings.Length; i++) { Tri(Ring(i, k), Ring(i + 1, k), Ring(i + 1, k + 1)); Tri(Ring(i, k), Ring(i + 1, k + 1), Ring(i, k + 1)); }
                Tri(Ring(rings.Length - 1, k), tipI, Ring(rings.Length - 1, k + 1));
            }
        }
        foreach (var f in leaves)
        {
            if (side >= 0) One(f);
            if (side <= 0 && !(side == 0 && f.Central)) One(f.Flipped());
        }
        return new Surface(V, N, C, T);
    }

    // 살 속(`a`)에서 `dr` 쪽으로 나가 겉면을 만나는 점 — 살 밖에서 시작하면 false.
    static bool LizSurf(Func<Vector3, float> f, Vector3 a, Vector3 dr, out Vector3 hit)
    {
        hit = a; dr.Normalize();
        if (f(a) > 0f) return false;
        float lo = 0f, hi = 0.6f;
        for (int i = 0; i < 22; i++) { float mid = (lo + hi) * 0.5f; if (f(a + dr * mid) > 0f) hi = mid; else lo = mid; }
        hit = a + dr * lo; return true;
    }

    // 몸의 잎 비늘 — 등(옆구리까지) · 목덜미 · 팔 바깥 · 허벅지 바깥 · 꼬리 윗면. 아래(꼬리에선 끝) 쪽으로 누워 겹친다. 배 · 팔다리 안쪽 · 손발은 매끈하다.
    // 팔 것은 팔꿈치에서 위 · 아래로 갈라 준다(관절마다 굳은 조각). 오른쪽(+x)에 적는다.
    sealed class LizBodyLeaves { public readonly List<LizLeaf> Back = new(), ArmUp = new(), ArmLow = new(), Thigh = new(), Tail = new(); }

    static LizBodyLeaves BodyLeaves(LizFrame F, float lift)
    {
        var L = new LizBodyLeaves(); var D = F.D;
        {
            const float ln = 0.136f, wd = 0.106f;
            float y = F.Top + 0.005f; int r = 0;
            while (y > D.Lift + 0.06f)
            {
                float t = Mathf.Min((y - D.Lift) / D.TorsoH, 0.985f), rr = BodyTorsoRadius(D.Half, Mathf.Clamp01(t), D.TorsoH, D.TopR), z0 = F.Zs(y);
                float amax = y > D.HemY + 0.07f ? 1.50f : 1.05f;   // 허리끈 아래는 꼬리 뿌리 둘레만
                for (int c = 0; c < 9; c++)
                {
                    float a = (c + (r % 2 == 1 ? 0.5f : 0f)) * wd * 0.90f / Mathf.Max(rr, 0.08f);
                    if (a > amax) break;
                    var pt = new Vector3(rr * Mathf.Sin(a), y, -rr * D.Depth * Mathf.Cos(a) + z0);
                    var n = new Vector3(Mathf.Sin(a) * D.Depth, 0.10f, -Mathf.Cos(a));
                    L.Back.Add(new LizLeaf(pt, new Vector3(0f, -1f, -2f * F.Lean * Mathf.Max(y - F.Y0, 0f)), n, ln, wd, 0.24f * lift));
                }
                y -= ln * 0.50f; r++;
            }
        }
        // 굵은 선(팔 · 허벅지 · 목)의 둘레에 줄 맞춰 — `outv` 쪽이 각도 0, 잎은 b 쪽으로 눕는다.
        void Wrap(Func<Vector3, List<LizLeaf>> to, Vector3 a, Vector3 b, float ra, float rb, Vector3 outv, float a0, float a1, float ln, float wd, float lf, float s0, float s1)
        {
            float len = (b - a).magnitude; var ax = (b - a) / len;
            var o = (outv - ax * Vector3.Dot(outv, ax)).normalized; var sd = Vector3.Cross(ax, o);
            float u = s0 * len; int r = 0;
            while (u < s1 * len)
            {
                float rad = ra + (rb - ra) * u / len, da = wd * 0.90f / rad;
                for (float ang = a0 + (r % 2 == 1 ? 0.5f * da : 0f); ang <= a1; ang += da)
                {
                    var n = o * Mathf.Cos(ang) + sd * Mathf.Sin(ang); var pt = a + ax * u + n * rad;
                    var fl = pt.x < 0f ? new Vector3(-1f, 1f, 1f) : Vector3.one;
                    pt = Vector3.Scale(pt, fl);
                    to(pt).Add(new LizLeaf(pt, Vector3.Scale(ax, fl), Vector3.Scale(n, fl), ln, wd, lf));
                }
                u += ln * 0.50f; r++;
            }
        }
        List<LizLeaf> Arm(Vector3 pt) => pt.y >= F.Elbow.y ? L.ArmUp : L.ArmLow;
        foreach (float a_ in new[] { -1.9f, -1.1f, -0.3f })   // 어깨 꼭지 한 줄
        {
            var n = new Vector3(Mathf.Cos(a_) * 0.75f, 0.66f, Mathf.Sin(a_) * 0.75f).normalized;
            L.ArmUp.Add(new LizLeaf(F.Shoulder + n * D.ArmR, new Vector3(n.x, -1f, n.z), n, 0.108f, 0.090f, 0.20f * lift));
        }
        Wrap(Arm, F.Shoulder, new Vector3(D.ArmX, D.HandY + 0.10f, F.ArmZ + 0.04f), D.ArmR, D.ArmR, Vector3.right, -2.1f, 0.35f, 0.108f, 0.090f, 0.20f * lift, 0.04f, 1.0f);
        Wrap(_ => L.Thigh, F.Hip, F.Knee, D.LegR * 1.22f, D.LegR * 1.02f, new Vector3(1f, 0f, -0.5f), -1.5f, 0.75f, 0.112f, 0.094f, 0.20f * lift, 0.30f, 0.92f);
        Wrap(_ => L.Back, F.NeckB, F.NeckA, F.NeckR, F.NeckR * 1.18f, new Vector3(0f, 0.35f, -1f), 0f, 1.45f, 0.104f, 0.086f, 0.20f * lift, 0.30f, 0.95f);
        // 꼬리 — 곡선을 따라 고리마다 엇갈려
        {
            var cum = new float[13];
            for (int i = 1; i < 13; i++) cum[i] = cum[i - 1] + (F.Tail[i] - F.Tail[i - 1]).magnitude;
            float s = 0.10f; int r = 0;
            while (s < cum[12] - 0.10f)
            {
                int i = 0;
                while (i < 11 && cum[i + 1] < s) i++;
                float f = (s - cum[i]) / (cum[i + 1] - cum[i]);
                var c = Vector3.Lerp(F.Tail[i], F.Tail[i + 1], f); float rad = Mathf.Lerp(F.TailR[i], F.TailR[i + 1], f);
                var ax = (F.Tail[i + 1] - F.Tail[i]).normalized; var up = Vector3.Cross(Vector3.Cross(ax, Vector3.up), ax).normalized;
                float l2 = 0.050f + rad * 0.62f, w2 = l2 * 0.80f;
                for (int cc = 0; cc < 7; cc++)
                {
                    float a = (cc + (r % 2 == 1 ? 0.5f : 0f)) * w2 * 0.90f / Mathf.Max(rad, 0.02f);
                    if (a > 1.95f) break;
                    var n = up * Mathf.Cos(a) + Vector3.right * Mathf.Sin(a);
                    L.Tail.Add(new LizLeaf(c + n * rad, ax, n, l2, w2, 0.24f * lift));
                }
                s += l2 * 0.50f; r++;
            }
        }
        return L;
    }

    // ── 머리 ────────────────────────────────────────────────────────────────

    // 머리 하나 — 머리통 · 위턱 · 아래턱 · 입선 높이 · 눈 자리 · 덧붙이 · 콧구멍(머리 좌표).
    sealed class LizHead
    {
        public Func<Vector3, float> Cran, Upper, Lower, Extra, MouthY;
        public Vector3 Eye, EyeN; public float EyeR, JawW;
        public Vector2 ChinZ = new(0.03f, 0.13f);                    // 아래턱의 밝은 색이 시작하는 z 띠
        public (Vector3 c, float r)[] Pits;
        public Color Skin, Belly;
        public string DefaultEye; public Color? EyeCol;              // 기본 눈 · 그 눈의 색(없으면 눈 표의 색)
        public List<LizLeaf> Leaves;                                 // 머리 비늘(머리 좌표) — 없으면 null

        public float Skin0(Vector3 p) => SMin(SMin(Cran(p), Extra(p), 0.05f), Mathf.Min(Upper(p), Lower(p)), 0.05f);
        public float Top0(Vector3 p)  => SMin(SMin(Cran(p), Extra(p), 0.05f), Upper(p), 0.05f);   // 비늘을 앉히는 윗머리
        // 머리 살 — 입선이 위아래를 가른다 · 콧구멍은 판다(튀어나온 점이 아니다). 눈꺼풀은 눈 벌마다 따로 조각이다.
        public float Skull(Vector3 p)
        {
            float yv = p.y - MouthY(p), b = SMin(Cran(p), Extra(p), 0.05f);
            float d = Mathf.Min(SMin(b, Mathf.Max(Upper(p), -(yv - LizGap)), 0.05f), SMin(b, Mathf.Max(Lower(p), yv + LizGap), 0.05f));
            var m = Mirror(p);
            foreach (var (c, r) in Pits) d = SMax(d, -((m - c).magnitude - r), 0.004f);
            return d;
        }
        public float Inner(Vector3 p) => Mathf.Min(Upper(p), Lower(p)) + 0.011f;   // 입 안 — 입선 틈으로만 보인다

        // 눈알을 살 겉면에 앉힌다 — 눈 방향 선 위에서 겉면을 찾아 눈알 중심이 살 속 `depth × 반지름`에 오게.
        public void SeatExact(float depth) { if (LizSurf(Skin0, Eye - EyeN * 0.12f, EyeN, out var pt)) Eye = pt - EyeN * (depth * EyeR); }
        // 악어 — 손으로 맞춘 값이라 옛 길 그대로(눌린 조각의 거리를 그대로 믿는다).
        public void SeatLoose(float depth) { var c = Eye; for (int i = 0; i < 8; i++) c -= EyeN * (Skin0(c) + depth * EyeR); Eye = c; }

        // 아래턱의 배 몫(아래턱 폭 안에서만) · 콧구멍 안의 검은 몫 — 색은 팔레트(칸 1 배 · 칸 3 콧구멍).
        public float JawK(Vector3 p)
        {
            float yv = p.y - MouthY(p);
            return SStep(0f, -0.03f, yv) * SStep(ChinZ.x, ChinZ.y, p.z) * SStep(JawW + 0.02f, JawW - 0.05f, Mathf.Abs(p.x));
        }
        public float PitK(Vector3 p)
        {
            var m = Mirror(p); float keep = 1f;
            foreach (var (c, r) in Pits) keep *= 1f - SStep(r * 1.25f, r * 0.95f, (m - c).magnitude);
            return 1f - keep;
        }
    }

    // 머리 윗면 · 옆면에 뒤로 누운 잎 비늘을 줄 맞춰 깐다 — z0(앞)에서 z1(뒤)로 가며 커지고 더 들린다. 눈 둘레 · 입술은 비운다.
    static List<LizLeaf> HeadLeaves(LizHead h, float z0, float z1, float ln0, float ln1, float ratio, float lift0, float lift1, float browLift = -1f)
    {
        var lv = new List<LizLeaf>(); float z = z0; int r = 0;
        Func<Vector3, float> skin = h.Top0;
        while (z > z1)
        {
            float t = Mathf.Clamp01((z0 - z) / (z0 - z1)), ln = ln0 + (ln1 - ln0) * t, wd = ln * ratio;
            var axis = new Vector3(0f, -0.03f, z);
            if (!LizSurf(skin, axis, Vector3.up, out var top)) break;
            float rad = Mathf.Max(top.y + 0.03f, 0.06f);
            for (int c = 0; c < 12; c++)
            {
                float phi = (c + (r % 2 == 1 ? 0.5f : 0f)) * wd * 0.92f / rad;
                if (phi > 1.85f) break;
                if (!LizSurf(skin, axis, new Vector3(Mathf.Sin(phi), Mathf.Cos(phi), 0f), out var pt) || pt.y < h.MouthY(pt) + 0.045f) continue;
                float de = (pt - h.Eye).magnitude;
                if (de < h.EyeR * 1.45f) continue;
                bool brow = browLift >= 0f && de < h.EyeR * 2.3f && pt.y > h.Eye.y;   // 눈 위 줄 — 눈썹
                lv.Add(new LizLeaf(pt, Vector3.back, Grad(skin, pt, 0.004f), ln * (brow ? 1.10f : 1f), wd, brow ? browLift : lift0 + (lift1 - lift0) * t * t));
            }
            z -= ln * 0.46f; r++;
        }
        return lv;
    }

    // 도마뱀의 가시 테 — 볏도마뱀붙이처럼(미리보기 v12 · 11-14 사용자 사진: *"뿔이 눈을 따라 쭉 내려가잖아 — 지금은 머리 위에 있는 뿔 같다"*).
    //   ① 속눈썹 — 눈알 윗선을 따라 앞에서 뒤로 감싸고 머리 바깥으로 젖혀진다(둑 없음 · *"속눈썹은 안쪽 말고 바깥쪽으로"*)
    //   ② 머리 옆 가장자리 — 눈 뒤에서 뒤통수 옆까지(살은 테두리 둑) ③ 뒤통수 옆에서 아래로 꺾여 목으로 — 그대로 등 · 꼬리 양쪽 두 줄(`LizFringeRows`)
    // 가시 사이는 어디나 `LizSpineGap`(*"꼬리 · 등 · 머리 쪽 뿔 간격이 너무 다르다"*).
    const float LizSpineGap = 0.030f;
    static readonly Vector3 LizEyeC = new(0.172f, 0.085f, 0.135f);   // 도마뱀 머리의 눈 자리(`LizardHead`)
    static readonly Vector3[] LizLash = { new(0.118f, 0.142f, 0.212f), new(0.168f, 0.166f, 0.160f), new(0.206f, 0.160f, 0.100f), new(0.226f, 0.140f, 0.052f) };
    static readonly int[] LizLashCount = { 3, 3, 2 };
    static readonly Vector3[] LizFringe =
    {
        new(0.226f, 0.150f, 0.052f), new(0.238f, 0.178f, -0.040f), new(0.232f, 0.180f, -0.140f),
        new(0.212f, 0.128f, -0.200f), new(0.188f, 0.050f, -0.236f), new(0.160f, -0.040f, -0.262f),
    };
    static float LizFringeBank(Vector3 p)
    {
        var m = Mirror(p); float d = 9f;
        for (int i = 0; i + 1 < LizFringe.Length; i++) d = Mathf.Min(d, Capsule(m, LizFringe[i], LizFringe[i + 1], 0.030f));
        return d;
    }
    static float LizFringeSpines(Vector3 p)
    {
        var m = Mirror(p); float d = 9f;
        for (int s = 0; s + 1 < LizLash.Length; s++)   // ① 속눈썹 — 짧고 가늘다
        {
            Vector3 a = LizLash[s], b = LizLash[s + 1]; int n = LizLashCount[s];
            for (int i = 0; i < n; i++)
            {
                var c = a + (b - a) * ((i + 0.5f) / n);
                var o = ((c - LizEyeC).normalized * 0.55f + Vector3.right * 0.80f + Vector3.up * 0.15f).normalized;
                d = Mathf.Min(d, Seg(m, c - o * 0.004f, c + o * 0.030f, 0.010f, 0.002f));
            }
        }
        for (int s = 0; s + 1 < LizFringe.Length; s++)   // ② · ③ 가장자리 — 바깥 · 뒤로(내려가는 자리에선 뒤 · 바깥으로 눕는다)
        {
            Vector3 a = LizFringe[s], b = LizFringe[s + 1];
            int n = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / LizSpineGap));
            for (int i = 0; i < n; i++)
            {
                var c = a + (b - a) * ((i + 0.5f) / n);
                float down = SStep(0.17f, 0.05f, c.y);
                var o = (new Vector3(c.x, 0f, (c.z + 0.05f) * 0.5f).normalized * 0.92f + Vector3.up * (0.34f - 0.40f * down) + new Vector3(0f, 0f, -0.30f * down)).normalized;
                float ln = 0.034f + 0.014f * Mathf.Sin(Mathf.PI * (i + 0.5f) / n);
                d = Mathf.Min(d, Seg(m, c + o * 0.012f, c + o * (0.026f + ln), 0.015f, 0.003f));
            }
        }
        return d;
    }

    // 가시 테가 이어지는 등 · 꼬리의 가시 두 줄(미리보기 `fringe_rows`) — 머리 가시 테 끝에서 목 뒤를 지나 등 양쪽으로, 꼬리 윗면 양옆을 따라 끝까지.
    // 줄을 촘촘히 짚어 겉면 위의 길을 만들고 그 길을 따라 `LizSpineGap`마다 하나씩(오른쪽만 · 거울로 쓴다). `tail` — 꼬리의 것인가(엉덩이 뼈에 붙는다).
    static List<(Vector3 a, Vector3 b, float r, bool tail)> LizFringeRows(LizFrame F)
    {
        var D = F.D;
        Func<Vector3, float> body = q => SMin(F.Torso(q), F.Neck(q), 0.05f);
        Func<Vector3, float> skinTail = q => SMin(F.Torso(q), F.TailSdf(q), 0.06f);
        var guide = new List<(Vector3 a, Vector3 dr, float k, Vector3? lay, Func<Vector3, float> f)>();
        for (int i = 0; i < 14; i++)   // 목 — 머리 쪽 → 어깨
        {
            float t = 0.15f + 0.85f * i / 13f;
            guide.Add((F.NeckB + (F.NeckA - F.NeckB) * t, new Vector3(0.78f, 0.15f, -0.60f).normalized, 1f, null, body));
        }
        for (int i = 0; i < 24; i++)   // 등 — 엉덩이로 갈수록 작다
        {
            float y = (F.Top - 0.12f) + ((D.Lift + 0.28f) - (F.Top - 0.12f)) * i / 23f;
            guide.Add((new Vector3(0f, y, F.Zs(y)), new Vector3(0.62f, 0.05f, -0.78f).normalized, 0.55f + 0.45f * (y - D.Lift) / D.TorsoH, null, body));
        }
        for (int i = 1; i < F.Tail.Length - 1; i++)   // 꼬리 — 윗면 양옆 · 꼬리 끝 쪽으로 눕는다
            for (int j = 0; j < 6; j++)
            {
                float u = j / 6f;
                var c = F.Tail[i] + (F.Tail[i + 1] - F.Tail[i]) * u; float r = F.TailR[i] + (F.TailR[i + 1] - F.TailR[i]) * u;
                var ax = (F.Tail[i + 1] - F.Tail[i - 1]).normalized;
                var up = Vector3.Cross(Vector3.Cross(ax, Vector3.up), ax).normalized; var side = Vector3.Cross(up, ax).normalized;
                if (side.x < 0f) side = -side;
                guide.Add((c, (up * 0.70f + side * 0.72f).normalized, 0.35f + 0.65f * r / F.TailR[0], ax, skinTail));
            }
        var path = new List<(Vector3 b, Vector3 dr, float k, Vector3? lay)>();
        foreach (var g in guide) if (LizSurf(g.f, g.a, g.dr, out var hit)) path.Add((hit, g.dr, g.k, g.lay));
        var outL = new List<(Vector3, Vector3, float, bool)>();
        float acc = LizSpineGap * 0.5f;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            var (b0, d0, k0, l0) = path[i]; var (b1, d1, k1, l1) = path[i + 1];
            float L = (b1 - b0).magnitude;
            while (acc <= L)
            {
                float t = acc / Mathf.Max(L, 1e-6f);
                var b = b0 + (b1 - b0) * t; var dr = (d0 + (d1 - d0) * t).normalized; float k = k0 + (k1 - k0) * t;
                var lay = l0 ?? l1;
                var tip = lay is Vector3 lv ? (dr * 0.70f + lv * 0.65f).normalized : (dr * 0.75f + new Vector3(0f, 0.15f, -0.55f)).normalized;
                outL.Add((b - dr * 0.006f, b + tip * (0.040f * k), 0.016f * k, lay != null));
                acc += LizSpineGap;
            }
            acc -= L;
        }
        return outL;
    }

    static readonly Vector3 LizCranC = new(0f, 0.04f, -0.05f), LizCranR = new(0.245f, 0.255f, 0.28f);
    static float LizComb(Vector3 p)   // 이구아나 볏 — 정수리에서 목 뒤로, 옆으로 납작한 가시
    {
        var q = new Vector3(p.x / 0.42f, p.y, p.z); float d = 9f;
        for (int i = 0; i < 9; i++)
        {
            float a = (8f + 15f * i) * Mathf.Deg2Rad; var dr = new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a));
            var bs = LizCranC + new Vector3(0f, dr.y * LizCranR.y, dr.z * LizCranR.z);
            float ln = 0.050f + 0.065f * Mathf.Sin(Mathf.PI * i / 8f);
            d = Mathf.Min(d, Seg(q, bs - dr * 0.03f, bs + dr * ln + new Vector3(0f, 0f, -0.035f), 0.050f, 0.006f));
        }
        return d * 0.42f;
    }

    // 볏 — 가시 테 · 이구아나 볏 · 없음 · **뿔 없는 가시 테 머리**(`Bare` — 가시 테 머리통 그대로에 가시만 없다 · 등도 매끈 · 11-14 사용자).
    static readonly string[] LizCrests = { "Fringe", "Comb", "None", "Bare" };

    static LizHead LizardHead(string crest)
    {
        bool flat = crest == "Fringe";   // 가시 테 머리는 윗면이 납작한 쐐기다
        var h = new LizHead { Skin = new Color(0.36f, 0.50f, 0.27f), Belly = new Color(0.80f, 0.76f, 0.52f), DefaultEye = "BigSlit", JawW = 0.19f };
        h.Cran = flat ? p => SMax(Ell(p, LizCranC, LizCranR), p.y - 0.238f, 0.05f) : p => Ell(p, LizCranC, LizCranR);
        h.Upper = p => Seg(Squash(p, -0.01f, 0.72f), V3(0, 0, 0.08f), V3(0, -0.035f, 0.42f), 0.190f, 0.105f) * 0.72f;
        h.Lower = p => Seg(Squash(p, -0.10f, 0.60f), V3(0, -0.10f, 0.02f), V3(0, -0.105f, 0.39f), 0.165f, 0.088f) * 0.60f;
        h.MouthY = p => { float c = Mathf.Clamp01(0.10f - p.z); return -0.082f - 0.03f * p.z + 0.9f * c * c; };   // 뒤쪽이 올라간다
        h.Eye = V3(0.172f, 0.085f, 0.135f); h.EyeR = 0.058f; h.EyeN = V3(0.80f, 0.12f, 0.58f).normalized;
        h.Extra = p =>
        {
            var m = Mirror(p); float cheek = Sph(m, V3(0.150f, -0.065f, 0f), 0.098f);
            return flat ? SMin(LizFringeBank(p), cheek, 0.03f) : SMin(Seg(m, V3(0.075f, 0.120f, 0.220f), V3(0.228f, 0.172f, 0.01f), 0.038f, 0.040f), cheek, 0.03f);
        };
        h.Pits = new[] { (V3(0.045f, 0.028f, 0.452f), 0.016f) };
        return h;
    }

    // 매끈한 뱀 — 머리는 하나, 색이 넷(사용자 그림: 보엘렌비단뱀 · 누런 구렁이 · 인디고 · 붉은 뱀).
    //                                                             살                           배                           눈 테                        무늬 색
    static readonly Dictionary<string, (Color skin, Color belly, Color ring, Color mark)> LizSnakeTones = new()
    {
        ["Boelen"]  = (new Color(0.07f, 0.08f, 0.11f), new Color(0.92f, 0.82f, 0.42f), new Color(0.80f, 0.66f, 0.26f), new Color(0.95f, 0.80f, 0.16f)),
        ["Kingrat"] = (new Color(0.90f, 0.74f, 0.18f), new Color(0.96f, 0.90f, 0.58f), new Color(0.56f, 0.22f, 0.10f), new Color(0.06f, 0.06f, 0.07f)),
        ["Indigo"]  = (new Color(0.08f, 0.09f, 0.14f), new Color(0.24f, 0.27f, 0.35f), new Color(0.84f, 0.46f, 0.20f), new Color(0.74f, 0.30f, 0.13f)),
        ["Red"]     = (new Color(0.80f, 0.25f, 0.16f), new Color(0.95f, 0.66f, 0.38f), new Color(0.10f, 0.08f, 0.08f), new Color(0.07f, 0.05f, 0.06f)),
        // 11-14 더함(사용자: *"사진 보면 여러 색들이 있는데 왜 4개씩만"* — 깃 밖 `_refs/2026-11-11_lizard/`의 아직 안 쓴 사진).
        ["Speckle"] = (new Color(0.06f, 0.06f, 0.07f), new Color(0.92f, 0.82f, 0.30f), new Color(0.80f, 0.66f, 0.26f), new Color(0.96f, 0.84f, 0.18f)),   // 검은 바탕에 노란 점박이(비늘 끝이 노랗다)
        ["Checker"] = (new Color(0.86f, 0.84f, 0.74f), new Color(0.94f, 0.92f, 0.86f), new Color(0.55f, 0.50f, 0.40f), new Color(0.30f, 0.26f, 0.22f)),   // 크림에 어두운 테 — 격자로 읽힌다
        ["Olive"]   = (new Color(0.42f, 0.40f, 0.26f), new Color(0.82f, 0.78f, 0.56f), new Color(0.62f, 0.50f, 0.22f), new Color(0.08f, 0.07f, 0.06f)),   // 올리브 갈색 · 입술과 눈 밑의 검은 줄
    };

    // 길고 낮은 머리 — 윗면이 평평하고 눈 위에서 주둥이로 모서리(눈썹 판)가 선다 · 둥글게 좁아지는 주둥이 · 큰 둥근 눈(테 눈 `Ring`).
    static LizHead SnakeHead(string tone)
    {
        var (skin, belly, ring, _) = LizSnakeTones[tone];
        var h = new LizHead { Skin = skin, Belly = belly, DefaultEye = "Ring", EyeCol = ring, JawW = 0.205f };
        h.Cran = p => SMax(Ell(p, V3(0, 0, -0.07f), V3(0.200f, 0.146f, 0.27f)), p.y - 0.108f, 0.045f);
        h.Upper = p => SMax(Seg(Squash(p, -0.01f, 0.62f), V3(0, -0.005f, 0.03f), V3(0, -0.022f, 0.400f), 0.184f, 0.120f) * 0.62f, p.z - 0.505f, 0.05f);
        h.Lower = p => SMax(Seg(Squash(p, -0.085f, 0.42f), V3(0, -0.085f, -0.02f), V3(0, -0.082f, 0.385f), 0.176f, 0.108f) * 0.42f, p.z - 0.488f, 0.04f);
        h.MouthY = p => { float c = Mathf.Clamp01(0.05f - p.z); return -0.070f + 0.5f * c * c; };
        h.Eye = V3(0.170f, 0.038f, 0.205f); h.EyeR = 0.058f; h.EyeN = V3(0.90f, 0.12f, 0.40f).normalized;
        h.Extra = p =>
        {
            var m = Mirror(p);
            float jaw = Sph(m, V3(0.140f, -0.030f, -0.06f), 0.100f);                                   // 턱 뒤 — 목보다 조금 넓다
            float brow = Seg(m, V3(0.126f, 0.070f, 0.110f), V3(0.062f, 0.026f, 0.430f), 0.032f, 0.022f);   // 눈 위에서 주둥이로 서는 모서리
            return SMin(jaw, brow, 0.03f);
        };
        h.Pits = new[] { (V3(0.066f, 0.022f, 0.492f), 0.012f) };
        h.SeatExact(0.30f);
        // 누런 구렁이 — 뒤통수부터 온몸에 밑동이 검은 노란 비늘. 납작하게 누워 윤곽은 매끈하고, 겹친 자리가 검은 그물로 읽힌다.
        if (tone == "Kingrat") h.Leaves = HeadLeaves(h, 0.075f, -0.300f, 0.070f, 0.096f, 0.95f, 0.05f, 0.08f);
        return h;
    }

    // 숲살모사 — 색 넷(사용자 그림: 풀빛 · 청회색 · 숯빛 · 주황). 살은 비늘 사이로 어둡게 비치고 잎 끝이 밝다.
    //                                                             살(잎 밑동)                  잎 끝                        배 · 턱                      눈                          눈 종류
    static readonly Dictionary<string, (Color skin, Color tip, Color belly, Color eye, string eyeKind)> LizViperTones = new()
    {
        ["Green"] = (new Color(0.30f, 0.50f, 0.17f), new Color(0.80f, 0.86f, 0.40f), new Color(0.86f, 0.88f, 0.52f), new Color(0.46f, 0.74f, 0.66f), "BigSlit"),
        ["Slate"] = (new Color(0.24f, 0.36f, 0.45f), new Color(0.66f, 0.80f, 0.80f), new Color(0.78f, 0.84f, 0.62f), new Color(0.84f, 0.88f, 0.62f), "BigSlit"),
        ["Coal"]  = (new Color(0.13f, 0.15f, 0.19f), new Color(0.40f, 0.45f, 0.52f), new Color(0.36f, 0.39f, 0.44f), new Color(0.05f, 0.05f, 0.06f), "Gecko"),
        ["Amber"] = (new Color(0.80f, 0.44f, 0.10f), new Color(0.99f, 0.82f, 0.30f), new Color(0.97f, 0.86f, 0.46f), new Color(0.74f, 0.86f, 0.40f), "BigSlit"),
        // 11-14 더함 — 짚빛 노랑 · 청록 · 진홍(사진).
        ["Straw"]   = (new Color(0.62f, 0.58f, 0.24f), new Color(0.92f, 0.86f, 0.50f), new Color(0.90f, 0.86f, 0.58f), new Color(0.40f, 0.36f, 0.20f), "BigSlit"),
        ["Teal"]    = (new Color(0.08f, 0.52f, 0.42f), new Color(0.48f, 0.92f, 0.74f), new Color(0.62f, 0.88f, 0.70f), new Color(0.20f, 0.30f, 0.22f), "BigSlit"),
        ["Crimson"] = (new Color(0.66f, 0.18f, 0.22f), new Color(0.96f, 0.50f, 0.40f), new Color(0.94f, 0.62f, 0.30f), new Color(0.90f, 0.78f, 0.30f), "BigSlit"),
    };

    static readonly float[] ViperZW = { -0.33f, -0.24f, -0.10f, 0.14f, 0.325f }, ViperWW = { 0.105f, 0.160f, 0.262f, 0.152f, 0.092f };   // 볼 높이의 반폭 — 턱 모서리(z −0.10)가 가장 넓다
    static readonly float[] ViperZT = { -0.33f, -0.18f, 0.15f, 0.325f }, ViperTT = { 0.070f, 0.136f, 0.138f, 0.084f };                     // 윗면 높이 — 눈 앞에서 주둥이로 조금 내려간다
    static float ViperWedge(Vector3 p, float zf, float kw, bool top)
    {
        float x = Mathf.Abs(p.x);
        float w = Interp(p.z, ViperZW, ViperWW) * kw - 0.050f * SStep(-0.02f, 0.12f, p.y) * SStep(0.30f, -0.05f, p.z);   // 윗면은 볼보다 좁다(뒤로 갈수록 더)
        float d = SMax(x - w, p.z - zf - 0.10f * Mathf.Min(p.y, 0f), 0.075f);                                           // 뭉툭하게 잘린 주둥이
        if (top) d = SMax(d, p.y - (Interp(p.z, ViperZT, ViperTT) - 0.6f * x * x), 0.045f);                              // 평평한 윗면 · 눈썹 모서리
        return SMax(d, -0.31f - p.z, 0.09f) * 0.74f;
    }

    // **다른 뱀과 윤곽이 다르다**(사용자 11-12) — 위에서 보면 넓은 세모(좁고 뭉툭한 주둥이에서 턱 모서리까지 곧게 벌어졌다가 목에서 잘록) ·
    // 옆에서 보면 윗면이 평평하고 주둥이가 깊은 채로 뚝 잘린다 · 큰 눈이 눈썹 모서리 바로 밑 · 입선이 눈 뒤 턱 모서리까지 가며 끝이 올라간다.
    static LizHead ViperHead(string tone)
    {
        var (skin, _, belly, eye, eyeKind) = LizViperTones[tone];
        var h = new LizHead { Skin = skin, Belly = belly, DefaultEye = eyeKind, EyeCol = eye, JawW = 0.27f, ChinZ = new Vector2(-0.16f, -0.08f) };
        h.Upper = p => SMax(ViperWedge(p, 0.330f, 1f, true), -0.085f - p.y, 0.02f);   // 아래는 입선이 자른다 — 그 밑으로 조금만 남긴다(입 안 조각이 기둥이 되지 않게)
        h.Lower = p => SMax(SMax(ViperWedge(p, 0.322f, 0.97f, false), (-0.150f + 0.085f * Mathf.Clamp(p.z, -0.1f, 0.33f) + 1.3f * p.x * p.x) - p.y, 0.05f), p.y, 0.03f);
        h.MouthY = p => { float c = Mathf.Clamp01(0.06f - p.z); return -0.058f + 0.75f * c * c; };
        h.Cran = p => Ell(p, V3(0, -0.010f, -0.200f), V3(0.160f, 0.135f, 0.150f));    // 뒤통수 밑 — 목이 여기 꽂힌다
        h.Extra = p => Sph(Mirror(p), V3(0.170f, -0.030f, -0.135f), 0.088f);          // 턱 모서리(독샘) — 입선이 여기서 끝난다
        h.Eye = V3(0.150f, 0.060f, 0.150f); h.EyeR = 0.060f; h.EyeN = V3(0.92f, 0.10f, 0.38f).normalized;
        h.Pits = new[] { (V3(0.078f, 0.012f, 0.318f), 0.011f) };
        h.SeatExact(0.36f);
        h.Leaves = HeadLeaves(h, 0.275f, -0.330f, 0.050f, 0.104f, 0.92f, 0.10f, 0.26f, 0.24f);
        return h;
    }

    static float CrocSW(float z) => 0.215f + (0.150f - 0.215f) * Mathf.Clamp01((z - 0.05f) / 0.50f);   // 위턱 반폭

    // 덩치 큰 악어 — 넓은 주둥이(가늘면 약해 보인다) · 깊은 아래턱 · 눈 위에서 주둥이로 내려오는 굵은 턱 · 눈은 작고 깊다.
    static LizHead CrocHead()
    {
        var h = new LizHead { Skin = new Color(0.23f, 0.32f, 0.25f), Belly = new Color(0.74f, 0.72f, 0.54f), DefaultEye = "Glare", EyeCol = LzYellow, JawW = 0.225f };
        h.Cran = p => SMax(Ell(p, V3(0, 0, -0.07f), V3(0.265f, 0.225f, 0.27f)), p.y - 0.190f, 0.05f);   // 윗면이 납작하다
        h.Upper = p => SMin(Seg(Squash(p, -0.03f, 0.60f), V3(0, -0.01f, 0.05f), V3(0, -0.03f, 0.55f), 0.215f, 0.150f) * 0.60f,
                            Ell(p, V3(0, 0.022f, 0.590f), V3(0.100f, 0.052f, 0.085f)), 0.035f);        // 콧등 둔덕 — 콧구멍이 이 위에 뚫린다
        h.Lower = p => Seg(Squash(p, -0.135f, 0.64f), V3(0, -0.135f, 0f), V3(0, -0.13f, 0.545f), 0.205f, 0.140f) * 0.64f;
        h.MouthY = p => { float c = Mathf.Clamp01(0.10f - p.z); return -0.095f + 0.011f * Mathf.Sin(p.z * 24f) + 0.7f * c * c; };   // 물결치는 입선
        h.Eye = V3(0.175f, 0.125f, 0.105f); h.EyeR = 0.046f; h.EyeN = V3(0.78f, 0.18f, 0.60f).normalized;
        Func<Vector3, float> bas = p =>
        {
            var m = Mirror(p);
            float mound = Ell(m, V3(0.130f, 0.118f, 0.050f), V3(0.092f, 0.072f, 0.108f));                // 눈 둔덕 — 머리 윗면보다 솟지 않는다
            float snout = Seg(m, V3(0.058f, 0.050f, 0.500f), V3(0.070f, 0.095f, 0.290f), 0.020f, 0.030f);   // 주둥이 위 두 줄 능선
            float hinge = Sph(m, V3(0.190f, -0.090f, -0.04f), 0.112f);                                   // 턱 근육
            return SMin(mound, SMin(snout, hinge, 0.02f), 0.03f);
        };
        h.Extra = bas;
        h.Pits = new[] { (V3(0.036f, 0.066f, 0.605f), 0.017f) };
        h.SeatLoose(0.42f);
        // 눈 위 턱 — 앞(안쪽)이 낮다(노려본다). 살에 붙여 짧게(길면 끝이 귀처럼 솟는다).
        var t = Vector3.Cross(Vector3.up, h.EyeN).normalized; var e = h.Eye; float r = h.EyeR;
        var b0 = e + Vector3.up * (r * 0.95f) - t * 0.060f + h.EyeN * 0.012f - Vector3.up * 0.022f;
        var b1 = e + Vector3.up * (r * 0.95f) + t * 0.050f - h.EyeN * 0.015f + Vector3.up * 0.010f;
        h.Extra = p => SMin(bas(p), Seg(Mirror(p), b0, b1, 0.026f, 0.032f), 0.03f);
        return h;
    }
    static float CrocPlates(Vector3 p)   // 뒤통수 골판 — 안쪽 줄은 크게, 바깥 줄은 작게
    {
        var m = Mirror(p); float d = 9f;
        foreach (float z in new[] { -0.07f, -0.17f, -0.27f })
        {
            float q = (z + 0.07f) / 0.27f, y = Mathf.Min(0.190f, 0.225f * Mathf.Sqrt(Mathf.Max(0f, 1f - q * q)));
            d = Mathf.Min(d, Seg(m, V3(0.080f, y - 0.02f, z), V3(0.095f, y + 0.075f, z - 0.055f), 0.050f, 0.012f));
            d = Mathf.Min(d, Seg(m, V3(0.180f, y - 0.05f, z - 0.02f), V3(0.215f, y + 0.025f, z - 0.065f), 0.040f, 0.010f));
        }
        return d;
    }
    static float CrocTeeth(Vector3 p)   // 굵기와 길이가 들쭉날쭉한 송곳니 — 위는 아래로, 아래 큰 것 둘은 윗입술 밖으로 솟는다
    {
        var m = Mirror(p); float d = 9f;
        foreach (var (z, ln, r) in new[] { (0.16f, 0.070f, 0.022f), (0.25f, 0.118f, 0.031f), (0.34f, 0.072f, 0.022f), (0.43f, 0.108f, 0.029f), (0.52f, 0.078f, 0.023f) })
        {
            float w = CrocSW(z) * 0.975f;
            d = Mathf.Min(d, Seg(m, V3(w, -0.082f, z), V3(w + 0.006f, -0.082f - ln, z + 0.004f), r, 0.003f));
        }
        foreach (var (z, ln, r) in new[] { (0.295f, 0.105f, 0.027f), (0.475f, 0.090f, 0.025f) })
        {
            float w = CrocSW(z) + 0.004f;
            d = Mathf.Min(d, Seg(m, V3(w, -0.118f, z), V3(w + 0.010f, -0.118f + ln, z), r, 0.003f));
        }
        foreach (float x in new[] { 0.045f, 0.115f })
        {
            float z = 0.690f - x * x * 3.2f;
            d = Mathf.Min(d, Seg(m, V3(x, -0.082f, z), V3(x, -0.160f, z + 0.006f), 0.022f, 0.003f));
        }
        return d;
    }

    // ── 눈 — 고르기 축. 날카로운 쪽에서 둥근 쪽으로 ─────────────────────────────
    //   이름       눈동자(없으면 null)  크기   윗꺼풀 높이 · 앞으로 기운 정도   아랫꺼풀(없으면 −1)   색
    static readonly (string name, string pupil, float k, float lid, float slant, float low, Color col)[] LizEyes =
    {
        ("Sharp",   "Slit",  0.95f, 0.02f, 0.80f, 0.50f, new Color(0.95f, 0.72f, 0.16f)),   // 가늘게 찢어진 눈 — 위아래 꺼풀이 다 덮는다
        ("Glare",   "Slit",  1.00f, 0.28f, 0.55f, -1f,   new Color(0.95f, 0.72f, 0.16f)),   // 노려보는 눈
        ("Almond",  "Slit",  1.08f, 0.52f, 0.30f, 0.80f, new Color(0.62f, 0.82f, 0.25f)),   // 중간 — 꺼풀이 살짝
        ("BigSlit", "Slit",  1.24f, 0.90f, 0.08f, -1f,   new Color(0.62f, 0.58f, 0.52f)),   // 크고 둥근 눈에 세로 눈동자(볏도마뱀붙이)
        ("Round",   "Round", 1.14f, 0.70f, 0.15f, -1f,   new Color(0.95f, 0.72f, 0.16f)),   // 둥근 눈동자
        ("Gecko",   null,    1.24f, 0.90f, 0.08f, -1f,   new Color(0.05f, 0.05f, 0.06f)),   // 크고 검은 눈 + 반짝임
        ("Blank",   null,    1.00f, 0.28f, 0.55f, -1f,   new Color(0.80f, 0.87f, 0.92f)),   // 눈동자 없는 흐린 눈
        ("Ring",    "Ring",  1.00f, 0.86f, 0.06f, -1f,   new Color(0.95f, 0.72f, 0.16f)),   // 큰 둥근 눈동자에 가는 테 + 반짝임(매끈한 뱀)
    };

    static float LizLid(Vector3 p, LizHead h, float k, float lid, float slant, float low)
    {
        var t = Vector3.Cross(Vector3.up, h.EyeN).normalized;
        var q = Mirror(p) - h.Eye; float r = h.EyeR * k, ball = q.magnitude - (r + 0.009f), a = Vector3.Dot(q, t);
        float d = Mathf.Max(ball, (r * lid + slant * a) - q.y);
        if (low >= 0f) d = Mathf.Min(d, Mathf.Max(ball, q.y - (-r * low + 0.25f * a)));
        return d;
    }
    static float LizPupil(Vector3 p, LizHead h, float k, string kind)
    {
        var t = Vector3.Cross(Vector3.up, h.EyeN).normalized; var u = Vector3.Cross(h.EyeN, t);   // t = 눈의 바깥 · 뒤쪽 · u = 위
        var q = Mirror(p) - h.Eye; float r = h.EyeR * k;
        float ball = q.magnitude - (r + 0.004f), fwd = r * 0.35f - Vector3.Dot(q, h.EyeN), a = Vector3.Dot(q, t), b = Vector3.Dot(q, u);
        float shape = kind == "Slit" ? Mathf.Max(Mathf.Abs(a) - r * 0.16f, Mathf.Abs(b) - r * 0.74f) : Mathf.Sqrt(a * a + b * b) - r * (kind == "Ring" ? 0.70f : 0.48f);
        return Mathf.Max(Mathf.Max(ball, fwd), shape);
    }
    static float LizGlint(Vector3 p, LizHead h, float k)
    {
        var t = Vector3.Cross(Vector3.up, h.EyeN).normalized; var u = Vector3.Cross(h.EyeN, t); float r = h.EyeR * k;
        return Sph(Mirror(p), h.Eye + (h.EyeN * 0.80f + u * 0.40f - t * 0.28f) * r, r * 0.21f);
    }

    // ── 색마다 얹는 칠 ──────────────────────────────────────────────────────

    // 매끈한 뱀 — 입술 띠(입선에서 lo~hi)와 그 안의 세로 줄.
    static float SnakeLipBand(LizHead h, Vector3 p, float lo, float hi)
    {
        float yv = p.y - h.MouthY(p);
        return SStep(lo - 0.012f, lo, yv) * SStep(hi + 0.012f, hi, yv) * SStep(-0.02f, 0.04f, p.z) * SStep(0.08f, 0.12f, Mathf.Abs(p.x)) * SStep(0.45f, 0.41f, p.z);
    }
    static float SnakeLipBars(LizHead h, Vector3 p, float lo, float hi, float k = 62f, float th = 0.45f)
        => SStep(th - 0.15f, th + 0.15f, Mathf.Sin(p.z * k + 0.6f)) * SnakeLipBand(h, p, lo, hi);
    // 눈에서 입술로 내려오는 줄 둘 + 눈 뒤로 비껴 내려가는 줄 하나.
    static float SnakeTears(LizHead h, Vector3 p, float w)
    {
        float ez = h.Eye.z, ey = h.Eye.y, er = h.EyeR;
        float d = Mathf.Min(Line2(p.z, p.y, ez + 0.030f, ey - er * 0.8f, ez + 0.062f, -0.068f), Line2(p.z, p.y, ez - 0.030f, ey - er * 0.8f, ez - 0.075f, -0.066f));
        d = Mathf.Min(d, Line2(p.z, p.y, ez - er * 0.9f, ey - 0.012f, ez - 0.215f, -0.050f));
        return p.y > h.MouthY(p) - 0.004f ? SStep(w, w * 0.6f, d) * SStep(0.07f, 0.10f, Mathf.Abs(p.x)) : 0f;
    }
    // 가는 줄 — (칠 몫 0~1 · 색) 둘까지. 살 위 1cm에 띄운 덧판으로 굽는다.
    static List<(Func<Vector3, float> mask, Color col)> SnakeMarks(LizHead h, string tone)
    {
        var (skin, _, _, mark) = LizSnakeTones[tone];
        var list = new List<(Func<Vector3, float>, Color)>();
        switch (tone)
        {
            case "Boelen":    // 윗입술 — 검은 바탕에 노란 줄 · 아랫입술 — 노란 바탕에 검은 줄
                list.Add((p => SnakeLipBars(h, p, 0f, 0.052f), mark));
                list.Add((p => { float band = SnakeLipBand(h, p, -0.060f, 0f); return band * (1f - SnakeLipBars(h, p, -0.060f, 0f, th: -0.35f)); }, skin));
                break;
            case "Kingrat":   // 눈 밑 줄 · 입술 줄(비늘의 검은 테는 빚은 비늘이 낸다)
                list.Add((p => Mathf.Max(SnakeTears(h, p, 0.009f), SnakeLipBars(h, p, -0.045f, 0.045f, 58f, 0.88f)), mark));
                break;
            case "Indigo":    // 녹슨 턱 위의 검은 줄
                list.Add((p => SnakeLipBars(h, p, -0.060f, 0.045f, 58f, 0.55f) * SStep(0.050f, 0.020f, p.y - h.MouthY(p)) * SStep(-0.10f, 0.02f, p.z), Mul(skin, 0.8f)));
                break;
            default:          // Red — 눈 밑 · 눈 뒤 검은 줄
                list.Add((p => Mathf.Max(SnakeTears(h, p, 0.013f), SnakeLipBars(h, p, -0.040f, 0.040f, 58f, 0.86f) * SStep(0.30f, 0.20f, p.z)), mark));
                break;
        }
        return list;
    }
    // ── 팔레트 ──────────────────────────────────────────────────────────────
    //
    // 살 메시의 칸(재질 `Chibi_LizPal_{종류}`):
    //   바탕(정점 색 rgba) — 0 살 · 1 배 · 2 (빈 칸) · 3 콧구멍
    //   덧칠(둘째 UV xyzw · 칸 알파 = 불투명도 — 0이면 그 무늬가 없는 색):
    //     뱀    4 꼬리 그늘(붉은 뱀) · 5 무지갯빛(보엘렌 · 인디고) · 6 노란 띠와 빗금(보엘렌) · 7 녹슨 턱(인디고)
    //     살모사 4 녹슨 등 띠(앰버)
    //     도마뱀 4 점(토케이 · 표범 · 낮도마뱀붙이 …) · 5 가로 띠(아브로니아 · 살찐꼬리) · 6 등줄기(볏도마뱀붙이 불꽃) · 7 눈테(악어도마뱀 — 눈꺼풀)
    // 살모사 잎 비늘(`Chibi_LizScale_Viper`) 0 밑동 · 1 끝 / 기본 눈알(`Chibi_LizEye_{Lizard|Viper|Snake}`) 0 /
    // 도마뱀 볏 · 등줄기 돌기(`Chibi_LizCrest_Lizard`) 0 가시 테 · 1 이구아나 볏과 돌기.
    const int LizOverTail = 0, LizOverSheen = 1, LizOverMark = 2, LizOverRust = 3;
    const int LizOverSpots = 0, LizOverBands = 1, LizOverDorsal = 2, LizOverRing = 3;   // 도마뱀
    static readonly Color LzRedTail = new(0.23f, 0.15f, 0.18f), LzAmberRust = new(0.62f, 0.24f, 0.08f);
    // 비늘 있는 매끈한 뱀 — 비늘 조각은 이 팔레트일 때만 켜진다(`@Palette.…`). 값 = 비늘 끝이 무늬 색인가(점박이 — 검은 살에 끝이 노랗다)
    // · 아니면 밑동이 무늬 색이고 끝이 살 색(누런 구렁이 · 격자 — 밑동의 어두운 테가 그물로 읽힌다).
    static readonly Dictionary<string, bool> LizScaledSnakes = new() { ["Kingrat"] = false, ["Speckle"] = true, ["Checker"] = false };
    static readonly Vector3 LzSheen = new Vector3(-0.5f, 0.8f, 1.4f) * 0.035f;   // 검푸른 살의 옅은 무지갯빛(옛 칠: 살 + 이 방향 × sin)
    static readonly string[] LizPalKinds = { "Lizard", "Croc", "Viper", "Snake" };

    static string LizPalName(string kind) => $"Chibi_LizPal_{kind}";
    static string LizScaleName(string kind) => $"Chibi_LizScale_{kind}";
    static string LizEyeName(string kind) => $"Chibi_LizEye_{kind}";
    static string LizCrestName(string kind) => $"Chibi_LizCrest_{kind}";
    static bool LizPalEye(string kind) => kind == "Viper" || kind == "Snake" || kind == "Lizard";   // 기본 눈알 색이 팔레트를 탄다

    // **기본 도마뱀의 색**(2026-11-14, 사용자: *"기본 도마뱀도 색이랑 좀 추가해줘"* + 사진 · 피규어 열하나 — 깃 밖 `_refs/2026-11-14_lizard_colors/`).
    // 처음 것(잎빛)이 옛 색이다. 무늬 칸은 알파 1이면 켠다(0.5면 옅게).
    //   이름         살                         배                          가시 테                     볏 · 돌기                   눈알                       점                          띠                          등줄기                      눈테
    static readonly (string name, Color skin, Color belly, Color fringe, Color comb, Color eye, Color spots, Color bands, Color dorsal, Color ring)[] LizLizardTones =
    {
        ("Leaf",     new(0.36f, 0.50f, 0.27f), new(0.80f, 0.76f, 0.52f), new(0.74f, 0.76f, 0.50f), new(0.24f, 0.36f, 0.20f), new(0.62f, 0.58f, 0.52f), default, default, default, default),
        ("Crested",  new(0.93f, 0.80f, 0.52f), new(0.97f, 0.93f, 0.78f), new(0.98f, 0.93f, 0.82f), new(0.86f, 0.66f, 0.38f), new(0.62f, 0.56f, 0.52f), default, default, new(0.84f, 0.56f, 0.26f, 1f), default),       // 크림 볏도마뱀붙이 — 등의 주황 불꽃
        ("Olive",    new(0.52f, 0.54f, 0.38f), new(0.78f, 0.76f, 0.64f), new(0.68f, 0.68f, 0.50f), new(0.42f, 0.44f, 0.30f), new(0.70f, 0.55f, 0.28f), default, default, new(0.64f, 0.64f, 0.50f, 1f), default),       // 올리브 볏도마뱀붙이 — 금빛 눈
        ("Skink",    new(0.11f, 0.09f, 0.09f), new(0.32f, 0.26f, 0.24f), new(0.18f, 0.14f, 0.13f), new(0.22f, 0.16f, 0.14f), new(0.05f, 0.05f, 0.06f), default, default, default, new(0.96f, 0.45f, 0.12f, 1f)),       // 붉은눈 악어도마뱀 — 검은 몸 · 주황 눈테
        ("Abronia",  new(0.10f, 0.62f, 0.44f), new(0.62f, 0.84f, 0.64f), new(0.42f, 0.82f, 0.62f), new(0.06f, 0.36f, 0.26f), new(0.93f, 0.86f, 0.20f), default, new(0.04f, 0.22f, 0.16f, 1f), default, default),       // 에메랄드 나무도마뱀 — 검은 가로 띠 · 노란 눈
        ("Tokay",    new(0.48f, 0.62f, 0.86f), new(0.86f, 0.90f, 0.96f), new(0.72f, 0.82f, 0.96f), new(0.36f, 0.48f, 0.72f), new(0.96f, 0.76f, 0.10f), new(0.95f, 0.45f, 0.15f, 1f), default, default, default),       // 토케이 — 하늘빛에 주황 점
        ("Leachie",  new(0.24f, 0.14f, 0.15f), new(0.90f, 0.74f, 0.22f), new(0.42f, 0.27f, 0.25f), new(0.17f, 0.10f, 0.10f), new(0.78f, 0.80f, 0.66f), new(0.64f, 0.22f, 0.32f, 1f), default, default, default),       // 거인 도마뱀붙이 — 검붉은 몸 · 분홍 얼룩 · 노란 배
        ("FatTail",  new(0.72f, 0.36f, 0.18f), new(0.93f, 0.87f, 0.80f), new(0.88f, 0.62f, 0.44f), new(0.50f, 0.24f, 0.12f), new(0.06f, 0.05f, 0.05f), default, new(0.95f, 0.92f, 0.86f, 1f), default, default),       // 살찐꼬리 도마뱀붙이 — 흰 가로 띠 · 검은 눈
        ("Leopard",  new(0.95f, 0.74f, 0.16f), new(0.98f, 0.94f, 0.84f), new(0.98f, 0.86f, 0.40f), new(0.70f, 0.50f, 0.10f), new(0.20f, 0.16f, 0.14f), new(0.20f, 0.12f, 0.10f, 1f), default, default, default),       // 표범 도마뱀붙이 — 노란 몸에 검은 점
        ("Phelsuma", new(0.40f, 0.75f, 0.18f), new(0.86f, 0.92f, 0.60f), new(0.62f, 0.86f, 0.36f), new(0.26f, 0.56f, 0.12f), new(0.20f, 0.18f, 0.16f), new(0.84f, 0.18f, 0.10f, 1f), default, default, default),       // 낮 도마뱀붙이 — 밝은 초록에 붉은 점
        ("LeafTail", new(0.40f, 0.26f, 0.18f), new(0.62f, 0.50f, 0.40f), new(0.56f, 0.38f, 0.26f), new(0.28f, 0.18f, 0.12f), new(0.82f, 0.22f, 0.10f), default, new(0.28f, 0.17f, 0.11f, 0.6f), default, default),     // 잎꼬리 도마뱀붙이 — 마른 잎 갈색 · 붉은 눈
    };

    // 종류의 팔레트 목록 — 첫 것이 재질 값(씬 뷰 · 프리팹).
    static List<PaletteLook.Palette> LizPalettes(string kind)
    {
        var list = new List<PaletteLook.Palette>();
        PaletteLook.Palette Pal(string name, Color skin, Color belly, (int over, Color c, bool on)[] overs)
        {
            var p = new PaletteLook.Palette { Name = name };
            void Add(string mat, string prop, Color c) => p.Slots.Add(new PaletteLook.Slot { Material = mat, Property = prop, Color = c });
            string pm = LizPalName(kind);
            Add(pm, "_Pal0", skin); Add(pm, "_Pal1", belly); Add(pm, "_Pal2", skin); Add(pm, "_Pal3", LzDark);
            for (int i = 0; i < 4; i++) Add(pm, $"_Pal{4 + i}", new Color(0f, 0f, 0f, 0f));
            foreach (var (over, c, on) in overs) { var cc = c; cc.a = on ? 1f : 0f; p.Slots[4 + over].Color = cc; }
            return p;
        }
        switch (kind)
        {
            case "Lizard":
                foreach (var t in LizLizardTones)
                {
                    var p = Pal(t.name, t.skin, t.belly, new[]
                    {
                        (LizOverSpots, t.spots, t.spots.a > 0f), (LizOverBands, t.bands, t.bands.a > 0f),
                        (LizOverDorsal, t.dorsal, t.dorsal.a > 0f), (LizOverRing, t.ring, t.ring.a > 0f),
                    });
                    for (int i = 0; i < 4; i++) { var c = p.Slots[4 + i].Color; if (c.a > 0f) { var src = i switch { 0 => t.spots, 1 => t.bands, 2 => t.dorsal, _ => t.ring }; c.a = src.a; p.Slots[4 + i].Color = c; } }
                    p.Slots.Add(new PaletteLook.Slot { Material = LizCrestName(kind), Property = "_Pal0", Color = t.fringe });
                    p.Slots.Add(new PaletteLook.Slot { Material = LizCrestName(kind), Property = "_Pal1", Color = t.comb });
                    p.Slots.Add(new PaletteLook.Slot { Material = LizEyeName(kind), Property = "_Pal0", Color = t.eye });
                    list.Add(p);
                }
                break;
            case "Croc":   { var h = CrocHead();         list.Add(Pal("Croc", h.Skin, h.Belly, new (int, Color, bool)[0])); break; }
            case "Viper":
                foreach (var (tone, t) in LizViperTones)
                {
                    var p = Pal(tone, t.skin, t.belly, new[] { (LizOverTail, LzAmberRust, tone == "Amber") });
                    p.Slots.Add(new PaletteLook.Slot { Material = LizScaleName(kind), Property = "_Pal0", Color = Mul(t.skin, 0.92f) });
                    p.Slots.Add(new PaletteLook.Slot { Material = LizScaleName(kind), Property = "_Pal1", Color = t.tip });
                    p.Slots.Add(new PaletteLook.Slot { Material = LizEyeName(kind), Property = "_Pal0", Color = t.eye });
                    list.Add(p);
                }
                break;
            case "Snake":
                foreach (var (tone, t) in LizSnakeTones)
                {
                    var sheen = new Color(Mathf.Clamp01(t.skin.r + LzSheen.x), Mathf.Clamp01(t.skin.g + LzSheen.y), Mathf.Clamp01(t.skin.b + LzSheen.z));
                    var p = Pal(tone, t.skin, t.belly, new[]
                    {
                        (LizOverTail, LzRedTail, tone == "Red"), (LizOverSheen, sheen, tone == "Boelen" || tone == "Indigo" || tone == "Speckle"),
                        (LizOverMark, t.mark, tone == "Boelen"), (LizOverRust, t.mark, tone == "Indigo"),
                    });
                    bool tipMark = LizScaledSnakes.TryGetValue(tone, out var tm) && tm;
                    p.Slots.Add(new PaletteLook.Slot { Material = LizScaleName(kind), Property = "_Pal0", Color = tipMark ? t.skin : t.mark });
                    p.Slots.Add(new PaletteLook.Slot { Material = LizScaleName(kind), Property = "_Pal1", Color = tipMark ? t.mark : t.skin });
                    p.Slots.Add(new PaletteLook.Slot { Material = LizEyeName(kind), Property = "_Pal0", Color = t.ring });
                    list.Add(p);
                }
                break;
        }
        return list;
    }

    // 몸 살의 몫(몸 좌표) — 바탕(살 · 배)과 덧칠(종류마다 무늬). 덧칠은 살 자리에만(배 색으로 넘어간 곳은 안 칠한다 — 옛 칠의 `ColDiff` 조건).
    static (Color b, Vector4 o) LizBodyW(LizFrame F, string kind, Vector3 p)
    {
        float k = F.BellyK(p);
        var w = Vector4.zero;
        float sk = 1f - SStep(0.25f, 0.35f, k);
        if (kind == "Snake")
        {
            float along = F.Along(p, out float tailw), x = Mathf.Abs(p.x);
            w[LizOverTail]  = SStep(F.Top + 0.05f, F.D.Lift - 0.35f, along) * 0.82f * sk;                                   // 붉은 뱀 — 꼬리로 갈수록 검붉은 잿빛
            w[LizOverSheen] = 0.5f * (1f + Mathf.Sin(p.y * 11f + p.z * 8f)) * sk;                                             // 무지갯빛
            float flank = SStep(0.86f, 0.95f, Mathf.Sin(p.y * 24f + x * 16f - p.z * 9f)) * SStep(0.07f, 0.15f, x) * (1f - tailw);
            float tailb = SStep(0.86f, 0.95f, Mathf.Sin(p.z * 26f + p.y * 12f)) * tailw;
            w[LizOverMark]  = Mathf.Max(flank, tailb) * sk;                                                                   // 보엘렌 — 옆구리 · 꼬리의 노란 빗금
        }
        else if (kind == "Lizard")
        {
            w[LizOverSpots]  = sk;                                                                           // 반점이 날 자리 — 등 · 옆구리 · 팔다리 · 꼬리(배는 안) · 반점은 셰이더가 찍는다
            w[LizOverBands]  = SStep(0.45f, 0.75f, Mathf.Sin(F.Along(p, out _) * 40f)) * sk;               // 가로 띠 — 몸을 따라 내려가는 자로(꼬리에서 안 꺾인다)
            w[LizOverDorsal] = F.DorsalK(p) * (0.75f + 0.25f * Mathf.Sin(p.y * 37f + p.z * 23f));        // 등줄기 — 가장자리가 물결친다(불꽃)
        }
        else if (kind == "Viper")
            w[LizOverTail] = 0.45f * SStep(0.55f, 0.85f, Mathf.Sin(F.Along(p, out _) * 30f + 0.4f * Mathf.Sin(Mathf.Abs(p.x) * 30f))) * sk;   // 앰버 — 등을 따라 녹슨 얼룩 띠
        return (new Color(1f - k, k, 0f, 0f), w);
    }

    // 머리 살의 몫(머리 좌표) — 아래턱 배 · 콧구멍 · 뱀의 입술 띠(보엘렌) · 녹슨 턱(인디고).
    static (Color b, Vector4 o) LizHeadW(LizHead h, string kind, Vector3 p)
    {
        float k = h.JawK(p), pk = h.PitK(p);
        var w = Vector4.zero;
        if (kind == "Snake")
        {
            w[LizOverMark] = SnakeLipBand(h, p, -0.060f, 0f);                                             // 보엘렌 — 아랫입술 노란 바탕
            w[LizOverRust] = SStep(0.050f, 0.020f, p.y - h.MouthY(p)) * SStep(-0.10f, 0.02f, p.z);     // 인디고 — 턱 · 입술이 녹슨 붉은색
        }
        else if (kind == "Lizard")
        {
            float above = SStep(0.02f, 0.05f, p.y - h.MouthY(p)) * (1f - k);
            w[LizOverSpots]  = above;                                                                        // 반점이 날 자리 — 입술 위 머리(얼굴도 그대로)
            w[LizOverDorsal] = SStep(0.06f, 0.14f, p.y) * SStep(0.16f, 0.08f, Mathf.Abs(p.x)) * SStep(0.30f, 0.10f, p.z);   // 정수리 — 등줄기가 머리까지
        }
        return (new Color((1f - k) * (1f - pk), k * (1f - pk), 0f, pk), w);
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    // 프리팹 한 벌 — 종류 하나(색은 팔레트). `Kind`는 Lizard · Croc · Viper · Snake.
    readonly struct LizOutfit
    {
        public readonly string Prefab, Kind;
        public LizOutfit(string prefab, string kind) { Prefab = prefab; Kind = kind; }
    }
    static readonly LizOutfit[] Lizards =
    {
        new("Chibi_Lizard", "Lizard"), new("Chibi_Lizard_Croc", "Croc"), new("Chibi_Lizard_Viper", "Viper"), new("Chibi_Lizard_Snake", "Snake"),
    };
    // 옛 색 프리팹(11-12 ~ 11-14) — 첫 것을 새 이름으로 옮겨 guid를 지키고(씬 인스턴스가 이어진다) 나머지는 지운다.
    static readonly Dictionary<string, string[]> LizOldTones = new()
    {
        ["Viper"] = new[] { "Green", "Slate", "Coal", "Amber" }, ["Snake"] = new[] { "Boelen", "Kingrat", "Indigo", "Red" },
    };
    static void MigrateLizardTones(LizOutfit o)
    {
        if (!LizOldTones.TryGetValue(o.Kind, out var tones)) return;
        string dst = PawnPrefabPaths.PathOf(o.Prefab);
        for (int i = 0; i < tones.Length; i++)
        {
            string old = PawnPrefabPaths.PathOf($"{o.Prefab}{tones[i]}");
            if (i == 0 && !System.IO.File.Exists(dst) && System.IO.File.Exists(old))
            {
                string err = AssetDatabase.MoveAsset(old, dst);
                if (!string.IsNullOrEmpty(err)) Debug.LogWarning($"[리자드맨] {old} → {dst} 옮기기 실패: {err}");
            }
            else if (System.IO.File.Exists(old)) AssetDatabase.DeleteAsset(old);
            string av = $"Assets/Controller/Avatars/{o.Prefab}{tones[i]}.asset";
            if (System.IO.File.Exists(av)) AssetDatabase.DeleteAsset(av);
        }
    }
    //                                                              체형          머리 배수  굽힘    목 굵기  꼬리 굵기
    static LizFrame LizFrameOf(string kind) => kind switch
    {
        "Croc"  => new LizFrame(CrocShape,   1.16f, 0.34f, 0.215f, 1.38f),
        "Snake" => new LizFrame(LizardShape, 1.00f, 0.30f, 0.160f, 1.10f),
        "Viper" => new LizFrame(LizardShape, 1.00f, 0.30f, 0.122f, 0.95f),
        _       => new LizFrame(LizardShape, 1.00f, 0.30f, 0.150f, 1.00f),
    };

    // 뽑은 표면(모양) — 종류마다 한 번만 뽑고 색마다 다시 칠한다. 열쇠는 `{종류}/{조각}`.
    static readonly Dictionary<string, Surface> _lizShapes = new();
    const string LizSkinMat = "LizSkin", LizLine = "OutlineOuter";

    // 재질 사본 — 흰 살(정점 색을 곱한다) · 뼈 · 먹 · 입 안 · 반짝임. 눈 · 무늬는 색마다(`LizColorMat`). 큰 짐승처럼 몸 번호만 적는다(`BodyStencil`).
    static Dictionary<string, Material> LizardMaterials(Dictionary<string, Material> mats)
    {
        var m = OutfitMaterials(mats, "Lizard", LzCloth, LzLeather, null, null);
        m[LizSkinMat] = Variant(mats["SkinOuter"], "Chibi_LizSkin", "_BaseColor", Color.white);
        m[LizSkinMat].SetFloat("_VertexAO", 1f);
        m["LizBone"]  = Variant(mats["Bone"],     "Chibi_LizBone",  "_BaseColor", LzBone);
        m["LizDark"]  = Variant(mats["NoseDark"], "Chibi_LizDark",  "_BaseColor", LzDark);
        m["LizMouth"] = Variant(mats["NoseDark"], "Chibi_LizMouth", "_BaseColor", LzMouth);
        m["LizWhite"] = Variant(mats["Bone"],     "Chibi_LizWhite", "_BaseColor", LzWhite);
        foreach (var k in new[] { "LizBone", "LizDark", "LizMouth", "LizWhite" }) BodyStencil(m[k]);
        EditorUtility.SetDirty(m[LizSkinMat]);
        // 팔레트 재질 — 종류마다(재질 값 = 그 종류의 첫 팔레트 · 씬 뷰가 보는 색). 살은 살 재질과 같은 바탕 · 비늘과 눈알도.
        foreach (var kind in LizPalKinds)
        {
            var pal = LizPalettes(kind)[0];
            Material Pm(string key, Material src, string name, bool stencil)
            {
                var pm = Variant(src, name, "_BaseColor", Color.white);
                pm.SetFloat("_VertexAO", 0f); pm.SetFloat("_VertexAlt", 0f); pm.SetFloat("_VertexPal", 1f);
                if (stencil) BodyStencil(pm);
                foreach (var s in pal.Slots)
                    if (s.Material == name) { if (s.Raw) pm.SetVector(s.Property, new Vector4(s.Color.r, s.Color.g, s.Color.b, s.Color.a)); else pm.SetColor(s.Property, s.Color); }
                // 반점 배치는 도마뱀 살만(다른 종류는 덧칠 칸 4가 다른 무늬다) · 재질 값은 첫 배치
                pm.SetFloat("_SpotBaked", key == "LizPal_Lizard" ? 1f : 0f);
                pm.SetVector("_SpotPickA", new Vector4(1f, 0f, 0f, 0f)); pm.SetVector("_SpotPickB", Vector4.zero);
                EditorUtility.SetDirty(pm);
                return m[key] = pm;
            }
            Pm($"LizPal_{kind}", mats["SkinOuter"], LizPalName(kind), false);
            if (kind == "Viper" || kind == "Snake") Pm($"LizScale_{kind}", mats["SkinOuter"], LizScaleName(kind), false);
            if (kind == "Lizard") Pm($"LizCrest_{kind}", mats["SkinOuter"], LizCrestName(kind), false);
            if (LizPalEye(kind)) Pm($"LizEye_{kind}", m["LizWhite"], LizEyeName(kind), true);
        }
        return m;
    }
    // 한 색 재질(눈알 · 무늬 덧판) — 색이 이름이다(같은 색은 한 벌).
    static string LizColorMat(Dictionary<string, Material> mats, Color c)
    {
        string key = "LizCol_" + ColorUtility.ToHtmlStringRGB(c);
        if (!mats.ContainsKey(key)) { mats[key] = Variant(mats["LizWhite"], "Chibi_" + key, "_BaseColor", c); BodyStencil(mats[key]); }
        return key;
    }

    // 리자드맨 한 벌 — 몸(관절마다 조각)과 머리 부위. 모양은 종류마다 한 번 뽑는다(`_lizShapes`) · 색은 팔레트(정점은 칸 몫).
    static (BoneBody body, List<Part> head) LizardFor(LizOutfit o, Dictionary<string, Material> mats)
    {
        var F = LizFrameOf(o.Kind); var D = F.D;
        bool lin = PlayerSettings.colorSpace == ColorSpace.Linear;
        var bb = new BoneBody { ArmX = D.ArmX, ArmZ = F.ArmZ, HeadInPlace = true };
        var head = new List<Part>();

        // 머리 — 도마뱀은 볏마다 머리통이 다르다(가시 테는 납작한 쐐기 + 둑). 나머지는 하나. 머리 모양은 색과 무관하다 —
        // 살모사는 첫 색 · 매끈한 뱀은 비늘(누런 구렁이)까지 지은 머리를 쓴다.
        var heads = new Dictionary<string, LizHead>();
        if (o.Kind == "Lizard") foreach (var c in LizCrests) heads[c] = LizardHead(c == "None" ? "Comb" : c == "Bare" ? "Fringe" : c);
        else heads[""] = o.Kind == "Croc" ? CrocHead() : o.Kind == "Viper" ? ViperHead("Green") : SnakeHead("Kingrat");
        LizHead H = null; foreach (var v in heads.Values) { H = v; break; }   // 눈 · 입은 어느 머리나 같다
        string palMat = $"LizPal_{o.Kind}";

        // 머리 좌표 ↔ 몸 좌표.
        Vector3 ToHead(Vector3 p) => (p - F.HO) / F.HK;
        Func<Vector3, float> Hd(Func<Vector3, float> f) => p => f(ToHead(p)) * F.HK;
        Bounds HB(float x, float y0, float y1, float z0, float z1)   // 머리 좌표의 상자(좌우 대칭) → 몸 좌표
            => new(F.HO + new Vector3(0f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f) * F.HK, new Vector3(2f * x, y1 - y0, z1 - z0) * F.HK);

        // 그늘은 굽지 않는다 — 미리보기에 없고, 흰 살 재질은 정점 색을 그대로 곱해 오목한 자리(입선 · 겨드랑이)가 얼룩진다.
        Func<Vector3, float> noScene = _ => 9f;

        // 모양 — 종류마다 한 번. 살은 편다(바깥 선의 가시가 안 서게).
        Surface Net(string piece, Func<Vector3, float> f, Bounds b, float step, float keep, bool relax, float tol = 0f)
        {
            string key = $"{o.Kind}/{piece}";
            if (_lizShapes.TryGetValue(key, out var have)) return have;
            var s = SurfaceNets(f, b, step, noScene, p => p, 0.04f, 0f, keep, tol);
            if (relax) Relax(s.Verts, s.Tris, 6);
            return _lizShapes[key] = s;
        }
        // 정점 색 = 색(선형 색 공간이면 선형 값) — 뽑은 표면의 그늘 칸은 1이다.
        Surface Tinted(Surface s, Func<Vector3, Color> col)
        {
            var c = new List<Color>(s.Colors.Count);
            for (int i = 0; i < s.Verts.Count; i++)
            {
                var want = col(s.Verts[i]); if (lin) want = want.linear;
                c.Add(new Color(want.r, want.g, want.b, 1f));
            }
            return new Surface(s.Verts, s.Normals, c, s.Tris);
        }
        string MeshName(string piece) => $"Chibi_Lizard_{o.Kind}_{piece.Replace('.', '_').Replace('@', '_').Replace('|', '_').Replace("!", "")}";
        // 색이 정해진 조각(등줄기 돌기 · 볏 · 골판) — 흰 살 재질에 정점 색을 곱한다 + 바깥 선.
        Part Flesh(string name, Func<Vector3, float> f, Bounds b, float step, float keep, Func<Vector3, Color> col, bool on = true, bool relax = true, string shape = null, float tol = 0f)
            => new(name, SaveSkull(MeshName(name), Tinted(Net(shape ?? name, f, b, step, keep, relax, tol), col)), Vector3.zero, Vector3.one, LizSkinMat, on, outerMat: LizLine);
        // 팔레트 조각 — 정점은 칸 몫(바탕은 정점 색 · 덧칠은 둘째 UV). 색은 개체마다 `PaletteLook`.
        Part FleshW(string name, string mat, Func<Vector3, float> f, Bounds b, float step, float keep, Func<Vector3, (Color b, Vector4 o)> wt,
                    bool on = true, bool relax = true, string shape = null, float tol = 0f, string mesh = null)
        {
            var s = Net(shape ?? name, f, b, step, keep, relax, tol);
            var c = new List<Color>(s.Verts.Count); var u = new List<Vector4>(s.Verts.Count);
            bool spots = o.Kind == "Lizard" && mat == palMat;   // 반점 배치 8벌(셋째 · 넷째 UV) — 반점이 날 자리(덧칠 칸 4의 몫)에만
            var u2 = spots ? new List<Vector4>(s.Verts.Count) : null; var u3 = spots ? new List<Vector4>(s.Verts.Count) : null;
            foreach (var v in s.Verts)
            {
                var (bw, ow) = wt(v); c.Add(bw); u.Add(ow);
                if (!spots) continue;
                Vector4 sa = Vector4.zero, sb = Vector4.zero;
                if (ow.x > 0.001f)
                    for (int k = 0; k < LizSpotLayouts; k++)
                    {
                        float sp = ow.x * LizSpots(v + LizSpotShift(k), LizSpotCell, LizSpotR, LizSpotKeep);
                        if (k < 4) sa[k] = sp; else sb[k - 4] = sp;
                    }
                u2.Add(sa); u3.Add(sb);
            }
            var m = SaveSkull(MeshName(mesh ?? name), new Surface(s.Verts, s.Normals, c, s.Tris));
            m.SetUVs(1, u);
            if (spots) { m.SetUVs(2, u2); m.SetUVs(3, u3); }
            EditorUtility.SetDirty(m);
            return new Part(name, m, Vector3.zero, Vector3.one, mat, on, outerMat: LizLine);
        }
        // 한 색 조각 — 색을 안 타는 메시(종류마다 한 벌).
        Part Plain(string name, string mat, string line, Func<Vector3, float> f, Bounds b, float step, float keep, bool on = true, string shape = null)
            => new(name, SaveSkull(MeshName(shape ?? name), Net(shape ?? name, f, b, step, keep, false)), Vector3.zero, Vector3.one, mat, on, outerMat: line);
        // 잎 비늘 — `mat`이 팔레트 재질이면 몫(밑동 · 끝)으로, 아니면 색으로.
        void AddLeaves(List<Part> to, string name, List<LizLeaf> leaves, int side, Color dark, Color tip, float e0, float e1, string mat = null)
        {
            if (leaves.Count == 0) return;
            to.Add(new Part(name, SaveSkull(MeshName(name), LeafSurface(leaves, side, dark, tip, e0, e1, weights: mat != null)), Vector3.zero, Vector3.one, mat ?? LizSkinMat, outerMat: LizLine));
        }

        // ── 몸 ──
        (Color, Vector4) BodyW(Vector3 p) => LizBodyW(F, o.Kind, p);
        const float Keep = 0.5f;
        float halfMax = 0f; foreach (float hf in D.Half) halfMax = Mathf.Max(halfMax, hf);
        Bounds Span(Vector3 a, Vector3 b, float pad) { var bd = new Bounds(a, Vector3.zero); bd.Encapsulate(b); bd.Expand(pad * 2f); return bd; }

        var torsoB = Span(new Vector3(-halfMax, D.Lift, -halfMax * D.Depth), new Vector3(halfMax, F.Top, halfMax * D.Depth + F.Zs(F.Top)), 0.08f);
        torsoB.Encapsulate(Span(F.NeckA, F.NeckB, F.NeckR * 1.18f + 0.05f)); torsoB.Encapsulate(Span(F.Tail[0], F.Tail[3], F.TailR[0] + 0.06f));
        bb.Parts.Add(FleshW("Spine.Liz.Torso", palMat, F.TorsoPiece, torsoB, 0.014f, Keep, BodyW));
        var tailB = Span(F.Tail[0], F.Tail[12], F.TailR[0] + 0.05f);
        bb.Parts.Add(FleshW("Hips.Liz.Tail", palMat, p => F.TailSdf(p), tailB, 0.012f, Keep, BodyW));
        bb.Parts.Add(Plain("Hips.Liz.Belt", "Pants", LizLine, F.Belt, Span(new Vector3(-halfMax, D.HemY - 0.03f, -halfMax * D.Depth), new Vector3(halfMax, D.HemY + 0.07f, halfMax * D.Depth + 0.05f), 0.05f), 0.008f, 0.4f));
        {
            float zf = F.TorsoR(D.HemY) * D.Depth;
            bb.Parts.Add(Plain("Hips.Liz.Flap", "Shirt", LizLine, F.Flap, Span(new Vector3(-0.16f, D.HemY - 0.35f, zf - 0.02f), new Vector3(0.16f, D.HemY + 0.04f, zf + 0.04f), 0.03f), 0.006f, 0.4f));
        }

        foreach (int sd in Sides)
        {
            string lr = sd < 0 ? "L" : "R"; int k = sd;
            Func<Vector3, float> On(Func<Vector3, float> f) => p => f(Side(p, k));
            Bounds SB(Vector3 a, Vector3 b, float pad) => Span(Side(a, k), Side(b, k), pad);
            float ar = D.ArmR, lg = D.LegR, hr = D.Hand * 0.5f;
            bb.Parts.Add(FleshW($"Arm{lr}.Liz.Upper",     palMat, On(F.Upper), SB(F.Elbow, F.Shoulder, ar + 0.04f), 0.012f, Keep, BodyW));
            bb.Parts.Add(FleshW($"LowerArm{lr}.Liz.Fore", palMat, On(F.Fore),  SB(F.Wrist, F.Elbow, ar + 0.04f), 0.012f, Keep, BodyW));
            bb.Parts.Add(FleshW($"Hand{lr}.Liz.Hand",     palMat, On(F.Hand),  SB(F.HandC, F.HandC, hr + 0.04f), 0.010f, Keep, BodyW));
            bb.Parts.Add(Plain($"Hand{lr}.Liz.Claws", "LizBone", LizLine, On(F.HandClaws),
                               SB(F.HandC + new Vector3(-0.09f, -hr * 1.5f, 0.02f), F.HandC + new Vector3(0.09f, -hr * 0.4f, 0.11f), 0.03f), 0.004f, 0.5f));
            bb.Parts.Add(FleshW($"Leg{lr}.Liz.Thigh",     palMat, On(F.Thigh), SB(F.Hip, F.Knee, lg * 1.22f + 0.04f), 0.012f, Keep, BodyW));
            bb.Parts.Add(FleshW($"LowerLeg{lr}.Liz.Shin", palMat, On(F.Shin),  SB(F.Knee, F.Heel, lg + 0.04f).Also(SB(F.Heel, F.Ball, lg * 0.7f + 0.04f)), 0.012f, Keep, BodyW));
            float fk = lg / 0.106f;
            bb.Parts.Add(FleshW($"Foot{lr}.Liz.Foot",     palMat, On(F.Foot),  SB(F.Ball + new Vector3(-0.15f * fk, -0.06f, -0.09f * fk), F.Ball + new Vector3(0.15f * fk, 0.06f, 0.26f * fk), 0.03f), 0.008f, Keep, BodyW));
            bb.Parts.Add(Plain($"Foot{lr}.Liz.Claws", "LizBone", LizLine, On(F.FootClaws),
                               SB(F.Ball + new Vector3(-0.14f * fk, -0.07f, 0.18f * fk), F.Ball + new Vector3(0.14f * fk, 0.01f, 0.31f * fk), 0.03f), 0.004f, 0.5f));
        }

        // 비늘 — 살모사는 잎(색은 팔레트 — 밑동 · 끝), 누런 구렁이도 잎(그 팔레트일 때만 · 색은 정해져 있다 — 밑동이 검고 끝이 노랗다),
        // 도마뱀 · 악어는 등줄기 돌기, 나머지 뱀은 매끈하다.
        bool viper = o.Kind == "Viper", snake = o.Kind == "Snake";
        if (viper || snake)
        {
            // 색은 팔레트(칸 0 밑동 · 칸 1 끝). 매끈한 뱀은 비늘 있는 색(누런 구렁이 · 점박이 · 격자)일 때만 켠다.
            Color dark = Color.black, tip = Color.white;
            float e0 = viper ? 0.25f : 0.12f, e1 = viper ? 0.95f : 0.42f;
            string mat = $"LizScale_{o.Kind}";
            string scaled = string.Join("|", LizScaledSnakes.Keys);
            string Nm(string joint, string what) => viper ? $"{joint}.Liz.{what}" : $"{joint}.Pick.Scaled{what}.On@Palette.{scaled}";
            var L = BodyLeaves(F, viper ? 1f : 0.28f);
            AddLeaves(bb.Parts, Nm("Spine", "Scales"), L.Back, 0, dark, tip, e0, e1, mat);
            AddLeaves(bb.Parts, Nm("Hips", "TailScales"), L.Tail, 0, dark, tip, e0, e1, mat);
            foreach (int sd in Sides)
            {
                string lr = sd < 0 ? "L" : "R";
                AddLeaves(bb.Parts, Nm($"Arm{lr}", "Scales"), L.ArmUp, sd, dark, tip, e0, e1, mat);
                AddLeaves(bb.Parts, Nm($"LowerArm{lr}", "Scales"), L.ArmLow, sd, dark, tip, e0, e1, mat);
                AddLeaves(bb.Parts, Nm($"Leg{lr}", "Scales"), L.Thigh, sd, dark, tip, e0, e1, mat);
            }
            var hl = new List<LizLeaf>(); foreach (var f in H.Leaves) hl.Add(f.Placed(F.HO, F.HK));
            AddLeaves(head, Nm("Head", "Scales"), hl, 0, dark, tip, e0, e1, mat);
        }
        else if (o.Kind != "Snake")
        {
            var rb = Span(new Vector3(-0.08f, D.Lift + 0.15f, F.BackZ(D.Lift + 0.4f) - 0.14f), new Vector3(0.08f, F.Top + 0.02f, F.BackZ(F.Top - 0.08f) + 0.06f), 0.03f);
            var rt = Span(F.Tail[1], F.Tail[9], F.TailR[1] * 1.6f + 0.06f);
            if (o.Kind == "Lizard")   // 도마뱀 — 돌기 색은 팔레트(볏 재질 — 파란 토케이에 초록 돌기가 남지 않게) · 볏마다 등이 다르다
            {
                string cm = $"LizCrest_{o.Kind}";
                // 한가운데 한 줄 — 이구아나 볏 · 볏 없음일 때만(칸 1)
                bb.Parts.Add(FleshW("Spine.Pick.Ridge.On@Crest.Comb|None", cm, F.RidgeBack, rb, 0.006f, Keep, _ => (new Color(0f, 1f, 0f, 0f), Vector4.zero), relax: false));
                bb.Parts.Add(FleshW("Hips.Pick.Ridge.On@Crest.Comb|None", cm, F.RidgeTail, rt, 0.006f, Keep, _ => (new Color(0f, 1f, 0f, 0f), Vector4.zero), relax: false));
                // 가시 테 — 머리 가시 테에서 이어지는 등 · 꼬리 양쪽 두 줄(칸 0 — 가시 테 색). 등은 등뼈, 꼬리는 엉덩이 뼈.
                var rows = LizFringeRows(F);
                foreach (bool tl in new[] { false, true })
                {
                    var mine = rows.FindAll(x => x.tail == tl);
                    if (mine.Count == 0) continue;
                    var bnd = new Bounds(mine[0].a, Vector3.zero);
                    foreach (var x in mine) { bnd.Encapsulate(x.a); bnd.Encapsulate(x.b); bnd.Encapsulate(Vector3.Scale(x.a, new Vector3(-1f, 1f, 1f))); }
                    bnd.Expand(0.06f);
                    Func<Vector3, float> rf = q =>
                    {
                        var mq = new Vector3(Mathf.Abs(q.x), q.y, q.z); float d = 9f;
                        foreach (var x in mine) d = Mathf.Min(d, Seg(mq, x.a, x.b, x.r, 0.003f));
                        return d;
                    };
                    bb.Parts.Add(FleshW(tl ? "Hips.Pick.FringeRows.On@Crest.Fringe" : "Spine.Pick.FringeRows.On@Crest.Fringe", cm, rf, bnd, tl ? 0.005f : 0.004f, Keep,   // 꼬리는 길어 조금 성기게
                                        _ => (new Color(1f, 0f, 0f, 0f), Vector4.zero), relax: false));
                }
            }
            else
            {
                var rc = new Color(0.14f, 0.21f, 0.16f);
                bb.Parts.Add(Flesh("Spine.Liz.Ridge", F.RidgeBack, rb, 0.006f, Keep, _ => rc, relax: false));
                bb.Parts.Add(Flesh("Hips.Liz.TailRidge", F.RidgeTail, rt, 0.006f, Keep, _ => rc, relax: false));
            }
        }

        // ── 머리 ──
        // **목 관절 자리** — 안 보이는 `Head`의 밑이 목이다(`DressSkinned`).
        head.Add(new Part("Head", null, F.HO, Vector3.one * 0.30f * F.HK, "HeadSkin", on: false));
        var skullB = o.Kind == "Croc" ? HB(0.34f, -0.30f, 0.30f, -0.40f, 0.76f) : HB(0.32f, -0.26f, 0.33f, -0.40f, 0.56f);
        foreach (var (crest, hh) in heads)
        {
            var h1 = hh;
            string name = crest == "" ? "Head.Liz.Skull" : $"Head.Pick.Crest.{crest}.Skull";
            // 이구아나 볏 · 볏 없음은 머리통이 같다(모양 한 벌).
            head.Add(FleshW(name, palMat, Hd(h1.Skull), skullB, 0.006f * F.HK, 0.45f, p => LizHeadW(h1, o.Kind, ToHead(p)), on: crest == "" || crest == "Fringe",
                            shape: crest == "None" ? "Head.Pick.Crest.Comb.Skull" : crest == "Bare" ? "Head.Pick.Crest.Fringe.Skull" : null,
                            tol: o.Kind == "Snake" ? 0.001f : 0f));   // 덧판을 얹는 머리는 줄일 때 1mm를 지킨다 · 볏 없음은 이구아나 머리통, 뿔 없는 머리는 가시 테 머리통과 같다
        }
        head.Add(Plain("Head.Liz.Mouth", "LizMouth", null, Hd(H.Inner), skullB, 0.008f * F.HK, 0.3f));
        if (o.Kind == "Lizard")
        {
            string cm = $"LizCrest_{o.Kind}";   // 볏 색은 팔레트(칸 0 가시 테 · 칸 1 이구아나 볏)
            head.Add(FleshW("Head.Pick.Crest.Fringe.Spines", cm, Hd(LizFringeSpines), HB(0.30f, -0.10f, 0.26f, -0.34f, 0.27f), 0.004f, Keep, _ => (new Color(1f, 0f, 0f, 0f), Vector4.zero), relax: false));
            head.Add(FleshW("Head.Pick.Crest.Comb.Spines", cm, Hd(LizComb), HB(0.06f, -0.24f, 0.46f, -0.52f, 0.10f), 0.005f, Keep, _ => (new Color(0f, 1f, 0f, 0f), Vector4.zero), on: false, relax: false));
        }
        if (o.Kind == "Croc")
        {
            var pc = new Color(0.14f, 0.21f, 0.16f);
            head.Add(Flesh("Head.Liz.Plates", Hd(CrocPlates), HB(0.28f, 0.0f, 0.32f, -0.40f, 0.0f), 0.006f * F.HK, Keep, _ => pc, relax: false));
            head.Add(Plain("Head.Liz.Teeth", "LizBone", LizLine, Hd(CrocTeeth), HB(0.26f, -0.24f, 0.02f, 0.10f, 0.74f), 0.004f * F.HK, 0.5f));
        }
        // 가는 줄 — 살 위 1cm에 띄운 덧판(머리 살은 줄이고 편 것이라 2~5mm로는 겹쳐 비친다). 색마다 무늬가 달라 넷 다 짓고 그 팔레트일 때만 켠다.
        if (o.Kind == "Snake")
            foreach (var tone in LizSnakeTones.Keys)
            {
                int n = 0;
                foreach (var (mask, col) in SnakeMarks(H, tone))
                {
                    Func<Vector3, float> f = q =>
                    {
                        float sh = Mathf.Abs(H.Skull(q) - 0.007f) - 0.004f;
                        return sh > 0.02f ? sh : SMax(sh, (0.5f - mask(q)) * 0.03f, 0.002f);
                    };
                    string nm = $"Head.Pick.Mark{tone}{(char)('A' + n++)}.On@Palette.{tone}";
                    var s = SurfaceNets(Hd(f), HB(0.27f, -0.18f, 0.12f, -0.14f, 0.50f), 0.003f, noScene, p => p, 0.04f, 0f, 0.6f);
                    head.Add(new Part(nm, SaveSkull(MeshName(nm), s), Vector3.zero, Vector3.one, LizColorMat(mats, col)));
                }
            }
        // 눈 — 고르기 축 여덟. 눈꺼풀(살) · 눈알 · 눈동자 · 반짝임. 눈알 색은 눈 표의 색 — 이 종류의 기본 눈만 제 색(`EyeCol`).
        foreach (var e in LizEyes)
        {
            var ev = e; bool on = ev.name == H.DefaultEye;
            float r = H.EyeR * ev.k;
            Bounds EB(float pad) => new(F.HO + new Vector3(0f, H.Eye.y, H.Eye.z) * F.HK, new Vector3(2f * (H.Eye.x + r + pad), 2f * (r + pad), 2f * (r + pad)) * F.HK);
            Color ball = on && H.EyeCol is Color own ? own : ev.col;
            var lidW = o.Kind == "Lizard" ? new Vector4(0f, 0f, 0f, 1f) : Vector4.zero;   // 도마뱀 — 눈꺼풀이 눈테(악어도마뱀의 주황 테)
            head.Add(FleshW($"Head.Pick.Eyes.{ev.name}.Lid", palMat, Hd(q => LizLid(q, H, ev.k, ev.lid, ev.slant, ev.low)), EB(0.03f), 0.004f, Keep,
                            _ => (new Color(1f, 0f, 0f, 0f), lidW), on, relax: false));
            // 기본 눈의 눈알은 팔레트 색(살모사 · 매끈한 뱀 — 색마다 눈이 다르다) · 나머지 눈은 눈 표의 색.
            if (on && LizPalEye(o.Kind))
                head.Add(FleshW($"Head.Pick.Eyes.{ev.name}.Ball", $"LizEye_{o.Kind}", Hd(q => Sph(Mirror(q), H.Eye, r)), EB(0.02f), 0.004f, 0.5f,
                                _ => (new Color(1f, 0f, 0f, 0f), Vector4.zero), on, relax: false, shape: $"Head.Eyes.Ball{ev.k:F2}", mesh: $"Head.Eyes.BallPal{ev.k:F2}"));
            else
                head.Add(Plain($"Head.Pick.Eyes.{ev.name}.Ball", LizColorMat(mats, ball), LizLine, Hd(q => Sph(Mirror(q), H.Eye, r)), EB(0.02f), 0.004f, 0.5f, on,
                               shape: $"Head.Eyes.Ball{ev.k:F2}"));
            if (ev.pupil != null)
                head.Add(Plain($"Head.Pick.Eyes.{ev.name}.Pupil", "LizDark", null, Hd(q => LizPupil(q, H, ev.k, ev.pupil)), EB(0.02f), 0.0025f, 0.5f, on,
                               shape: $"Head.Eyes.Pupil{ev.pupil}{ev.k:F2}"));
            if (ev.name == "Gecko" || ev.pupil == "Ring" || ball == LzBlack)
                head.Add(Plain($"Head.Pick.Eyes.{ev.name}.Glint", "LizWhite", null, Hd(q => LizGlint(q, H, ev.k)), EB(0.02f), 0.002f, 0.6f, on,
                               shape: $"Head.Eyes.Glint{ev.k:F2}"));
        }
        return (bb, head);
    }

    static Bounds Also(this Bounds a, Bounds b) { a.Encapsulate(b); return a; }

    // 리자드맨 얼굴 비우기(큰 짐승과 같다) + 기본 눈 40% + 팔레트(종류의 색 목록). 살색은 팔레트라 `SkinLook`을 안 붙인다.
    static Action<GameObject> LizardLook(string defaultEye, string kind) => root =>
    {
        if (!root.TryGetComponent(out PaletteLook pl)) pl = root.AddComponent<PaletteLook>();
        pl.SetPalettes(LizPalettes(kind));
        pl.SetSpots(kind == "Lizard" ? LizPalName(kind) : "", kind == "Lizard" ? LizSpotLayouts : 0);   // 반점 배치 8벌 — 개체마다 하나
        EditorUtility.SetDirty(pl);
        if (!root.TryGetComponent(out PawnLook look)) return;
        var so = new SerializedObject(look);
        foreach (var f in new[] { "shapeOverride", "hairOverride", "earOverride", "noseOverride", "tuskOverride" })
            so.FindProperty(f).stringValue = "";
        so.FindProperty("fittedTo").stringValue = "";
        so.ApplyModifiedPropertiesWithoutUndo();
        look.SetPickChances(new[] { new PawnLook.PickChance("Eyes", defaultEye, 0.4f) });
        EditorUtility.SetDirty(look);
        look.Apply();
    };

    // 보기 몸 넷(종류마다 하나 · 색은 팔레트) — 종족 데이터가 아직 없어 **어디서도 안 가리키는** 프리팹(`Lizard/Chibi_Lizard…`)에 굽는다(드워프 · 엘프와 같은 길).
    static int DressLizards(Dictionary<string, Material> mats, Dictionary<string, ChibiFaceAtlas> atlases)
    {
        _lizShapes.Clear();
        var lm = LizardMaterials(mats);
        int made = 0;
        foreach (var o in Lizards)
        {
            PawnPrefabPaths.Ensure(PawnPrefabPaths.FolderOf(o.Prefab));
            MigrateLizardTones(o);
            string dp = PawnPrefabPaths.PathOf(o.Prefab);
            if (System.IO.File.Exists(dp)) { System.IO.File.Copy(Source, dp, true); AssetDatabase.ImportAsset(dp, ImportAssetOptions.ForceUpdate); }
            else                           { AssetDatabase.CopyAsset(Source, dp); AssetDatabase.ImportAsset(dp); }
            var (body, head) = LizardFor(o, lm);
            string eye = o.Kind == "Lizard" ? "BigSlit" : o.Kind == "Croc" ? "Glare" : o.Kind == "Snake" ? "Ring" : "BigSlit";
            var heads = new Dictionary<string, List<Part>> { ["Male"] = head };
            if (Dress(dp, o.Kind == "Croc" ? CrocShape : LizardShape, heads, lm, atlases, isSource: false, LizardLook(eye, o.Kind), body)) made++;
        }
        _lizShapes.Clear();
        return made;
    }

    // **리자드맨만 다시 굽는다**(드워프 `Pawn/10`과 같은 길) — 얼굴 아틀라스는 구워 둔 것을 읽는다(없으면 `Pawn/3` 먼저).
    // 옛 리자드맨 메시(이번에 안 만든 `Chibi_Lizard_`)만 치운다 — 다른 폰의 메시는 안 건드린다.
    [MenuItem("DungeonColony/Pawn/16. Create Lizardfolk Only (리자드맨만 — 다른 폰은 안 건드린다)")]
    public static void CreateLizardOnly()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var toon = Shader.Find(ToonShader); var faceSh = Shader.Find(FaceShader); var outer = Shader.Find(OuterShader);
        if (toon == null || faceSh == null || outer == null) { Debug.LogError("[리자드맨] 셰이더를 못 찾았다."); return; }
        var atlases = new Dictionary<string, ChibiFaceAtlas>();
        foreach (var kind in new[] { "Eyes", "Mouths" })
        {
            var a = AssetDatabase.LoadAssetAtPath<ChibiFaceAtlas>($"{MatDir}/Chibi_{kind}Atlas.asset");
            if (a == null) { Debug.LogError($"[리자드맨] 얼굴 아틀라스({kind})가 없다 — `Pawn/3`을 먼저 돌릴 것."); return; }
            atlases[kind] = a;
        }
        var mats = EnsureMaterials(toon, faceSh, outer, atlases);

        _saved.Clear();
        NetsTimeReport();
        Heads = MakeHeads(ChibiFaceLayout.LoadOrCreate());
        int made = DressLizards(mats, atlases);

        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!_saved.Contains(path) && System.IO.Path.GetFileName(path).StartsWith("Chibi_Lizard_")) AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[리자드맨] 보기 몸 {made}/{Lizards.Length}벌(`{PawnPrefabPaths.FolderOf(Lizards[0].Prefab)}`) — 다른 폰은 그대로 · {watch.Elapsed.TotalSeconds:F0}초 · {NetsTimeReport()}");
    }
}
