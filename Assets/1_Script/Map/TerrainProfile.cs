using System;
using UnityEngine;

// 지형의 성격 — 노이즈 파라미터 한 벌.
//
// **바이옴마다 다르게 두는 것이 다음 단계다**(`WorldMapTerrain`이 월드맵 규모에서 이미
// 그렇게 한다 — 이웃 칸끼리 이중선형으로 섞어 경계를 지운다). 지금은 세계 전체가 한 벌을
// 쓰고, **섞는 자리는 `TerrainHeight.ProfileAt`에 뚫어 두었다.**
[Serializable]
public struct TerrainProfile
{
    [Tooltip("같은 시드면 언제 어디서 계산해도 같은 높이가 나온다 — 청크 경계가 안 갈라지는 근거다.")]
    public int Seed;

    [Tooltip("바다 높이가 아니라 기준면. 노이즈가 이 값을 중심으로 오르내린다.")]
    public float BaseHeight;

    [Header("대륙 — 며칠 걸어야 넘는 기복")]
    public float ContinentPeriod;      // m
    public float ContinentAmplitude;   // m

    [Header("언덕 — 한 화면 안에서 보이는 기복")]
    public float HillPeriod;
    public float HillAmplitude;

    [Header("잔기복 — 발밑의 울퉁불퉁함")]
    public float DetailPeriod;
    public float DetailAmplitude;

    // ── 계단 — 평평한 패치가 층층이 (2026-09-24, 사용자 결정) ─────────────────
    //
    // 위의 셋과 **다른 축이다.** 저쪽은 연속이라 *어디까지가 한 평면인가*를 못 정하는데,
    // 이쪽은 **한 패치가 통째로 한 높이**다. 마인크래프트의 인상은 단의 크기가 아니라
    // **평평한 면이 넓다**는 사실에서 온다 — 그래서 손잡이가 넷이고, 그중 둘이 축을 가른다:
    // **셀**(어느 넓이가 한 높이인가)과 **단**(한 층이 몇 m인가).
    //
    // **셀은 정점 간격(2m)의 배수여야 한다** — 아니면 셀 경계가 정점 사이에 떨어져 한
    // 패치가 두 높이에 걸린다(`TerrainHeight.Terrace`가 배수로 스냅한다).
    [Header("계단 — 평평한 패치가 층층이")]
    [Tooltip("한 패치의 한 변(m). 정점 간격 2m의 배수로 접힌다. 크면 평평한 면이 넓어지고, " +
             "작으면 전이 경사(늘 2m)가 차지하는 몫이 커져 계단이 물결로 읽힌다.")]
    public float TerraceCell;

    [Tooltip("한 층의 높이(m). **0이면 계단이 꺼진다.** 전이가 늘 정점 한 간격(2m)에 걸치므로 " +
             "이 값이 곧 보이는 각도다 — 0.1m = 2.9°(안 보인다) · 0.5m = 14° · 1.0m = 26.6°. " +
             "0.8m을 넘으면 1×1 건물이 단 경계에 못 놓인다(평탄 허용 0.6 + 0.2 × 칸).")]
    public float TerraceStep;

    [Tooltip("몇 층까지 오르내리나(±). 6이면 단 1m에서 ±6m다. " +
             "**적은 값이 실제 상한이다** — `TerrainHeight.Terrace`가 펄린을 펴서 끝까지 쓴다.")]
    public int TerraceLevels;

    [Tooltip("층이 바뀌는 큰 경향의 주기(m). 셀보다 훨씬 커야 한 층이 여러 패치에 걸친다 — " +
             "셀과 비슷하게 두면 패치마다 층이 달라 자잘한 체크무늬가 된다.")]
    public float TerracePeriod;

    // **잔기복을 작게 두는 것이 요점이다.** 건물은 `MapManager.IsFlatEnough`를 지나야 놓이는데
    // 그 허용치가 발자국 네 모서리 사이 **0.5m**다. 잔기복이 크면 **세계 대부분이 건설 불가**가
    // 되고, 그러면 넓힌 값이 화면에만 남는다.
    public static TerrainProfile Default => new()
    {
        Seed               = 1337,
        BaseHeight         = 30f,
        ContinentPeriod    = 4000f,
        ContinentAmplitude = 60f,
        HillPeriod         = 400f,
        HillAmplitude      = 12f,
        DetailPeriod       = 80f,
        DetailAmplitude    = 2.5f,
        TerraceCell        = 6f,
        TerraceStep        = 1.0f,
        TerraceLevels      = 6,
        TerracePeriod      = 40f,
    };
}
