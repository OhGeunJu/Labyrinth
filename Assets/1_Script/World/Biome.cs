using UnityEngine;

// 월드맵 노드의 지형 유형. 바이옴별 전용 자원/하수인/연구가 게이팅된다(step 7).
public enum Biome
{
    Unknown,    // 미지정 — 데이터가 없을 때의 기본값(게이팅 대상 아님)
    Plains,     // 평원 (시작 지역)
    Forest,     // 숲
    Desert,     // 사막
    Tundra,     // 설원
    Lava,       // 용암지대
    Cavern,     // 동굴
    Swamp,      // 늪
}

public static class BiomeExt
{
    public static string DisplayName(this Biome b) => b switch
    {
        Biome.Unknown => "미지",
        Biome.Plains  => "평원",
        Biome.Forest  => "숲",
        Biome.Desert  => "사막",
        Biome.Tundra  => "설원",
        Biome.Lava    => "용암지대",
        Biome.Cavern  => "동굴",
        Biome.Swamp   => "늪",
        _             => b.ToString(),
    };

    // ※ 바이옴에는 높이도 생김새도 없다. 지형은 씬에 직접 만들어 붙인 Terrain이고,
    //   바이옴은 그 위 격자 칸의 **논리 성격**(콘텐츠 게이팅)일 뿐이다.

    // 구역 표시색 — 저작 창의 격자와 (프리팹 없는) 기본 마커가 같은 값을 쓴다(단일 출처).
    // 게임 화면의 지형색이 아니다 — 그건 저작한 Terrain의 텍스처가 갖는다.
    public static UnityEngine.Color Color(this Biome b) => b switch
    {
        Biome.Plains => new Color(0.45f, 0.70f, 0.35f),
        Biome.Forest => new Color(0.18f, 0.42f, 0.20f),
        Biome.Desert => new Color(0.85f, 0.76f, 0.48f),
        Biome.Tundra => new Color(0.80f, 0.86f, 0.90f),
        Biome.Lava   => new Color(0.70f, 0.25f, 0.16f),
        Biome.Cavern => new Color(0.38f, 0.38f, 0.42f),
        Biome.Swamp  => new Color(0.36f, 0.40f, 0.26f),
        _            => new Color(0.28f, 0.28f, 0.30f),
    };
}
