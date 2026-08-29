using System;
using UnityEngine;

// 천장의 성격 — 노이즈 파라미터 한 벌. `TerrainProfile`의 짝이다.
//
// ── 왜 절대 높이가 아니라 "여유"인가 ────────────────────────────────────────
//
// 천장을 바닥과 무관한 절대 높이 함수로 두면, 둘이 제멋대로 오르내려 **어디가 방이고 어디가
// 바위인지를 저작할 수가 없다** — 산 위에서는 천장이 땅에 파묻히고 골짜기에서는 200m 위에
// 떠 있다. 값 하나를 보고 *"여기 지나갈 수 있나"*를 답할 수 없다는 뜻이다.
//
// **여유(clearance = 천장 − 바닥)로 두면 그 값 하나가 곧 답이다:**
//
//     여유 ≤ 0        바위다 (천장이 바닥을 만난다)
//     0 < 여유 < 폰 키  기어야 지날 틈 — **길찾기가 막아야 하는 자리**
//     여유 ≥ 폰 키     지나갈 수 있는 굴
//
// 그리고 딸려 오는 것이 하나 더 있다: **바닥을 다시 구워도 천장이 안 어긋난다.** 절대
// 높이로 저장하면 지형을 다시 굽는 순간 천장이 통째로 제자리에 남아 땅에 파묻히거나
// 하늘로 뜬다 — `WorldObject`가 높이를 저장하지 않는 것(XZ + `YOffset`)과 같은 이유다.
[Serializable]
public struct TerrainCeilingProfile
{
    [Tooltip("같은 시드면 언제 어디서 계산해도 같은 천장이 나온다 — 청크 경계가 안 갈라지는 근거다.")]
    public int Seed;

    [Tooltip("기준 여유(m). 노이즈가 이 값을 중심으로 오르내린다. 이 값이 클수록 세계가 " +
             "뻥 뚫리고, 작을수록 바위가 는다.")]
    public float BaseClearance;

    [Header("동굴 — 방과 바위 덩어리 (한참 걸어야 벗어나는 크기)")]
    public float CavernPeriod;      // m
    public float CavernAmplitude;   // m

    [Header("통로 — 한 화면 안에서 넓어졌다 좁아지는 굴곡")]
    public float TunnelPeriod;
    public float TunnelAmplitude;

    [Header("요철 — 천장의 울퉁불퉁함")]
    public float RoughPeriod;
    public float RoughAmplitude;

    [Tooltip("바위가 바닥 **아래로** 얼마나 파묻히게 그릴 것인가(m). 0이면 천장 면과 지면이 " +
             "정확히 같은 높이에서 만나 **z-파이팅**이 난다 — 조금 파묻어야 바위가 땅에서 " +
             "솟은 것처럼 보인다. **판정에는 안 쓴다**(그리기 전용).")]
    public float SinkDepth;

    // ── 기본값의 근거 ───────────────────────────────────────────────────────
    //
    // 여유의 범위는 대략 `Base ± (Cavern + Tunnel + Rough)` = 9 ± 16.5, 즉 **−7.5 ~ +25.5m**다.
    // 그중 0 미만(바위)이 대략 3분의 1이라 **걸어 다니면 굴이고 멀리 보면 바위벽**이 된다.
    // 바위를 더 늘리려면 `BaseClairance`를 내리는 것이 가장 곧은 손잡이다.
    //
    // **주기는 지형보다 짧게 잡는다.** 지형의 언덕이 400m인데 천장 굴곡이 그보다 길면
    // 천장이 지형을 그냥 따라가는 것처럼 보여 **방과 통로라는 구조가 안 생긴다.**
    public static TerrainCeilingProfile Default => new()
    {
        Seed            = 20260829,
        BaseClearance   = -7f,
        CavernPeriod    = 520f,
        CavernAmplitude = 11f,
        TunnelPeriod    = 130f,
        TunnelAmplitude = 4f,
        RoughPeriod     = 26f,
        RoughAmplitude  = 1.5f,
        SinkDepth       = 2f,
    };
}
