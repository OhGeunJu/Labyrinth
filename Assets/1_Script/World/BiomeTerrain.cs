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
        Biome.Desert => new BiomeTerrainProfile { Base = -0.2f, Amplitude = 1.2f, Roughness = 0.5f },
        Biome.Tundra => new BiomeTerrainProfile { Base =  0.5f, Amplitude = 2.2f, Roughness = 0.8f },
        Biome.Lava   => new BiomeTerrainProfile { Base =  0.4f, Amplitude = 4.5f, Roughness = 1.8f },
        Biome.Cavern => new BiomeTerrainProfile { Base =  1.0f, Amplitude = 7.0f, Roughness = 2.2f },  // 산악
        Biome.Swamp  => new BiomeTerrainProfile { Base = -0.8f, Amplitude = 0.4f, Roughness = 1.3f },
        _            => new BiomeTerrainProfile { Base =  0.0f, Amplitude = 0.0f, Roughness = 1.0f },  // 미지정 = 평평
    };

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
