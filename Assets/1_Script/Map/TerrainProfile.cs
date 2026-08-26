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
    };
}
