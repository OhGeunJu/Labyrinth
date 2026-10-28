using System;
using UnityEngine;

// **저작한 지형 특징 — "여기에 산"**(2026-08-26).
//
// 노이즈만으로는 *어디에* 산이 설지를 못 정한다. 주파수를 올리면 세계가 통째로 험해질 뿐이고,
// 그러면 **건설 가능한 땅이 사라진다**(`MapManager.IsFlatEnough`가 발자국 네 모서리 사이
// 0.5m를 요구한다). 반대로 산 하나를 손으로 조각하면 몇 km — 청크 수십 개 분량의 델타가 된다.
//
// **그래서 좌표로 저작한다.** 산 하나가 **몇 줄**이고, 노이즈 위에 얹히므로 저장할 것이 없다:
//
//     높이 = 노이즈  +  Σ(특징의 기여)  +  손댄 델타
//
// 지형 특징이 늘면 세계가 *"어디를 가도 비슷한 곳"*에서 **"저 산 너머"**가 있는 곳이 된다.
[Serializable]
public struct TerrainFeature
{
    [Tooltip("저작용 이름 — 코드는 안 읽는다.")]
    public string Name;

    [Tooltip("월드 좌표(m). 세계는 0 ~ 29,440.")]
    public Vector2 Center;

    [Tooltip("이 밖으로는 기여가 0이다(m). 산맥이면 수 km.")]
    public float Radius;

    [Tooltip("가운데에서 더해지는 높이(m). **음수면 분지·협곡**이 된다.")]
    public float Height;

    [Tooltip("0이면 뾰족한 봉우리, 1에 가까우면 넓은 고원(꼭대기가 평평해 건설이 된다).")]
    [Range(0f, 0.95f)] public float Plateau;

    [Tooltip("능선 잡음의 세기(m). 0이면 매끈한 언덕, 크면 바위산.")]
    public float Ridge;

    [Tooltip("능선 잡음의 주기(m).")]
    public float RidgePeriod;

    // 이 자리에 얼마나 얹히는가. **밖에서는 정확히 0이어야 한다** — 그래야 특징이 없는 곳의
    // 높이가 노이즈 그대로이고, 특징을 지웠을 때 지형이 원래대로 돌아온다.
    public float Contribution(float worldX, float worldZ)
    {
        if (Radius <= 0.01f) return 0f;

        float dx = worldX - Center.x;
        float dz = worldZ - Center.y;
        float d  = Mathf.Sqrt(dx * dx + dz * dz) / Radius;
        if (d >= 1f) return 0f;

        // 가운데 `Plateau`까지는 1로 평평하고, 거기서 가장자리까지 부드럽게 0으로 내려간다.
        // **`SmoothStep`이라 경계에서 기울기도 0이다** — 선형으로 끊으면 산 밑동에 **눈에
        // 보이는 원**이 생긴다.
        float t = d <= Plateau ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (d - Plateau) / (1f - Plateau));

        float h = Height * t;

        if (Ridge > 0.01f && RidgePeriod > 0.01f)
        {
            // 능선 잡음: 펄린을 접어 올려(|2n-1|) 날을 세운다 — 산등성이가 생긴다.
            float n = Mathf.PerlinNoise(worldX / RidgePeriod + 91.7f, worldZ / RidgePeriod + 17.3f);
            h += (1f - Mathf.Abs(n * 2f - 1f)) * Ridge * t;
        }
        return h;
    }
}
