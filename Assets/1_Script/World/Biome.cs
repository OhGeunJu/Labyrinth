// 월드맵 노드의 지형 유형. 바이옴별 전용 자원/하수인/연구가 게이팅된다(step 7).
public enum Biome
{
    Unknown,    // 미발견(안개) — 발견 전 표시용
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
}
