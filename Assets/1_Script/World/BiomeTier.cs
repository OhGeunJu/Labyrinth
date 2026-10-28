using UnityEngine;

// **지역 난도의 단일 출처**(2026-08-23).
//
// 티어를 읽는 곳이 여럿이다(무리 전력 · 도착 메뉴 경고 · 칸 표시). 각자 `BiomeContent.Get`을
// 훑고 제 배율을 곱하면 **화면에 뜬 위험도와 실제 전력이 갈린다** — 이 저장소가 여러 번
// 겪은 *"답이 두 벌이면 어긋난다"*의 자리다.
public static class BiomeTier
{
    public const int Min = 1, Max = 5;

    public static int Of(Biome b)
    {
        var data = BiomeContent.Get(b);
        return data != null ? Mathf.Clamp(data.Tier, Min, Max) : Min;
    }

    // **전력 배율.** 티어마다 +35% — 5티어가 2.4배다.
    //
    // **무리 에셋을 티어마다 복제하지 않는다.** 그렇게 하면 바이옴 × 무리의 곱셈이 되고,
    // 같은 고블린인데 어느 쪽이 용암지대 것인지를 에셋 이름으로 외워야 한다.
    public static float PowerMult(int tier) => 1f + (Mathf.Clamp(tier, Min, Max) - 1) * 0.35f;
    public static float PowerMultOf(Biome b) => PowerMult(Of(b));

    // 화면에 어떻게 부르나. **"1티어"라고 적지 않는다** — 숫자는 비교하라는 뜻인데
    // 플레이어가 재야 하는 것은 *갈까 말까*다.
    public static string Label(int tier) => Mathf.Clamp(tier, Min, Max) switch
    {
        1 => "안전한 편",
        2 => "만만치 않다",
        3 => "위험하다",
        4 => "매우 위험하다",
        _ => "죽으러 가는 길",
    };

    // 경고 색 — 초록에서 붉게.
    public static string Color(int tier) => Mathf.Clamp(tier, Min, Max) switch
    {
        1 => "#8C8", 2 => "#CC8", 3 => "#E96", 4 => "#E66", _ => "#F44",
    };
}
