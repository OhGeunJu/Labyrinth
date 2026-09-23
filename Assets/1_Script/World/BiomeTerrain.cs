using UnityEngine;

// 바이옴이 지형에 주는 **성격**. 칸마다 따로 노는 값이 아니라, 이웃 칸끼리 섞여서
// 하나로 이어진 지형을 만드는 재료다 — 숲에서 산지로 가면 기복이 서서히 험해진다.
public struct BiomeTerrainProfile
{
    public float Base;        // 기준 표고(월드 유닛). 늪처럼 꺼진 지역은 음수.
    public float Amplitude;   // 기복의 크기. 클수록 험하다.
    public float Roughness;   // 기복의 잘기. 클수록 자잘하고 날카롭다.
}

public static class BiomeTerrain
{
    // 바이옴별 지형 성격 — **여기가 단일 출처**다. 지도 모양을 바꾸려면 이 표만 고친다.
    public static BiomeTerrainProfile Of(Biome b) => b switch
    {
        Biome.Plains => new BiomeTerrainProfile { Base =  0.0f, Amplitude = 0.7f, Roughness = 0.6f },
        Biome.Forest => new BiomeTerrainProfile { Base =  0.3f, Amplitude = 1.8f, Roughness = 1.0f },
        Biome.Tundra => new BiomeTerrainProfile { Base =  0.5f, Amplitude = 2.2f, Roughness = 0.8f },
        Biome.Lava   => Flat,

        // ── 지하는 완전 평지다 (2026-08-29, 사용자 결정) ─────────────────
        //
        // 처음엔 *"조금 노이즈가 있게"*로 잡았다가 **완전 평지**로 다시 정했다. 참고로 삼은
        // 화면들이 전부 그렇다 — 파낸 바닥이 **한 장의 평면**이고, 높이의 뜻은 오직
        // *"파였나 안 파였나"*뿐이다. 바닥이 조금이라도 물결치면 벽 높이가 칸마다 달라져
        // **파낸 방이 울퉁불퉁한 웅덩이**처럼 보인다.
        //
        // **기복은 바닥이 아니라 바위가 만든다.** 그러니 여기서 흔들 이유가 없다 —
        // 바이옴은 이제 *무엇이 깔리고 무엇이 나오는가*만 답한다.
        //
        // **계단은 이 규칙을 안 깬다**(2026-09-24 — `TerrainProfile.Terrace*`). 사용자가
        // *"너무 평면"*이라 높낮이를 넣었는데 **연속 기복이 아니라 층**이다: 한 패치가
        // 통째로 한 높이라 파낸 방은 여전히 **한 장의 평면**이고, 갈리는 것은 *이 평면과
        // 저 평면의 높이*뿐이다. 그래서 위의 진폭 셋은 여기서 계속 0이다 — 되살리면
        // 그건 언덕이고, 웅덩이로 보이는 그 사고가 그대로 돌아온다.
        //
        // **계단은 바이옴을 안 탄다** — 여기 값은 이웃 넷과 이중선형으로 섞여 **연속**이라,
        // 곱하면 단 높이가 셀마다 조금씩 달라져 *같은 층이 이어지지 않는다.*
        //
        // **`Base`도 0이다** — 25를 곱하는 값이라 0.1만 달라도 바이옴 경계가 2.5m 비탈이 된다.
        Biome.Cavern    => Flat,
        Biome.Limestone => Flat,
        Biome.Fungal    => Flat,
        Biome.Glacier   => Flat,
        Biome.Crystal   => Flat,
        Biome.Mine      => Flat,
        Biome.Abyss     => Flat,

        Biome.Mossgrotto => Flat,
        Biome.Saltflat   => Flat,
        Biome.Ossuary    => Flat,
        Biome.Sulfur     => Flat,
        Biome.Rift       => Flat,

        // **지하로 되살린 둘**(2026-09-15) — 지상 arm을 **지우고** 여기로 옮겼다.
        // 그대로 뒀으면 사막이 진폭 1.2에 거칠기 0.5라 **천장의 여유를 바닥이 먹어**
        // 굴이 막힌다(지하 바닥은 완전 평지여야 한다).
        Biome.Desert     => Flat,
        Biome.Swamp      => Flat,

        _            => new BiomeTerrainProfile { Base =  0.0f, Amplitude = 0.0f, Roughness = 1.0f },  // 미지정 = 평평
    };

    // 지하의 기본 — 완전 평지.
    static BiomeTerrainProfile Flat => new() { Base = 0f, Amplitude = 0f, Roughness = 0f };

    // 프랙탈 노이즈(fBm) — 옥타브를 겹쳐 자연스러운 능선을 만든다.
    // `Mathf.PerlinNoise`는 결정적이라 같은 좌표·시드면 항상 같은 지형이 나온다.
    public static float FBm(float x, float y, int octaves, float seed)
    {
        float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
        for (int i = 0; i < octaves; i++)
        {
            sum  += (Mathf.PerlinNoise(x * freq + seed, y * freq + seed) * 2f - 1f) * amp;
            norm += amp;
            amp  *= 0.5f;
            freq *= 2f;
        }
        return norm > 0f ? sum / norm : 0f;   // -1 ~ 1
    }
}
