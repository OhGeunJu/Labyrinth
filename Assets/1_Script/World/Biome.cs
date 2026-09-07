using UnityEngine;

// 월드맵 노드의 지형 유형. 바이옴별 전용 자원/하수인/연구가 게이팅된다(step 7).
public enum Biome
{
    Unknown,    // 미지정 — 데이터가 없을 때의 기본값(게이팅 대상 아님)
    Plains,     // 평원 (시작 지역)
    Forest,     // 숲
    Desert,     // 사막
    Tundra,     // 설원
    Lava,       // 용암지대 — **지하에서도 산다**(용암층)
    Cavern,     // 동굴 — **지하에서 제 이름을 되찾는다**(지상 시절엔 "산악"으로 쓰였다)
    Swamp,      // 늪

    // ── 지하 (2026-08-29) ────────────────────────────────────────────────
    //
    // **값은 반드시 꼬리에 붙인다** — 정수로 직렬화되므로 중간에 끼우면 `BiomeContentData`
    // 에셋도, 저작한 월드맵 격자도 통째로 한 칸씩 밀린다.
    //
    // **지상 값(평원·숲·사막·설원·늪)을 지우지 않는다.** 같은 이유다 — 안 쓰면 그만이고,
    // 지우는 순간 그 뒤의 모든 값이 밀린다.
    Limestone,  // 석회굴 — 넓고 완만한 방, 물이 깎은 곳
    Fungal,     // 균사림 — 지하의 숲. 농사와 목재가 여기서 나온다
    Flooded,    // 수몰층 — 낮고 평평하고 물에 잠긴 곳
    Crystal,    // 수정동 — 날카롭고 밝다
    Mine,       // 폐광 — 사람이 팠던 곳. 곧고 평평하다
    Abyss,      // 심연 — 깊고 험하다
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
        Biome.Lava    => "용암층",
        Biome.Cavern  => "동굴",
        Biome.Swamp   => "늪",

        Biome.Limestone => "석회굴",
        Biome.Fungal    => "균사림",
        Biome.Flooded   => "수몰층",
        Biome.Crystal   => "수정동",
        Biome.Mine      => "폐광",
        Biome.Abyss     => "심연",

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

        // **지하는 서로 구별되게 벌린다** — 전부 회색 계열로 두면 지도가 한 덩어리로 보여
        // 어디로 갈지를 색으로 못 고른다(지도를 보는 이유의 절반이 그것이다).
        Biome.Limestone => new Color(0.62f, 0.58f, 0.50f),
        // **초록이 아니다**(2026-09-06) — 바닥을 남보라로 물들였으므로 지도도 따라간다.
        // **안 옮기면 지도만 초록이고**, 그러면 색으로 고르는 일이 거짓말이 된다.
        // 수정동(연보라)보다 **짙고 푸르게** 둔다 — 둘이 붙어 있으면 가르는 뜻이 없다.
        Biome.Fungal    => new Color(0.25f, 0.45f, 0.95f),
        Biome.Flooded   => new Color(0.22f, 0.34f, 0.52f),
        Biome.Crystal   => new Color(0.55f, 0.42f, 0.70f),
        Biome.Mine      => new Color(0.46f, 0.36f, 0.26f),
        Biome.Abyss     => new Color(0.14f, 0.13f, 0.18f),

        _            => new Color(0.28f, 0.28f, 0.30f),
    };

    // **바위도 바이옴을 따라간다**(2026-09-06, 사용자: *"돌도 바이옴에 어울리게 바이옴마다
    // 바꿨으면 좋겠어"*).
    //
    // 바닥은 이미 바이옴마다 갈리는데(`TerrainPalette`) **벽은 어디서나 같은 회색**이었다.
    // 그런데 굴에서 화면의 절반 이상은 **벽**이다 — 바닥만 바꾸면 *"바닥 색이 좀 다른 같은
    // 굴"*로 보인다.
    //
    // **재질을 바이옴마다 만들지 않는다** — `MaterialPropertyBlock`으로 청크마다 색만
    // 얹는다(`RockRenderer`). 재질을 늘리면 배칭이 갈리고, 무엇보다 **여덟 벌을 손으로
    // 맞추게 된다.**
    //
    // **벽의 밝기를 유지하고 색만 돌린다**(2026-09-06에 한 번 어둡게 잡았다 고쳤다).
    //
    // 처음에는 0.09~0.18짜리 어두운 색을 넣었다. **바위 재질에는 텍스처가 있고
    // `_BaseColor`는 그 위에 곱해지므로**, 그건 색을 바꾼 것이 아니라 **밝기를 4분의 1로
    // 내린 것**이었다 — 화면에는 *"바이옴마다 다른 느낌이 안 들고 그냥 다 어두운 벽"*으로만
    // 보였다(사용자). **곱하기에는 원래 값 근처에서 색만 돌려야 한다.**
    //
    // 기준은 `UnderworldSetup`이 옆면에 준 (0.72, 0.76, 0.80)이다 — **밝기는 그 언저리로
    // 두고 채도만 올린다.** 어두운 굴에서는 채도가 낮으면 전부 같은 회색으로 읽힌다.
    //
    // **윗면에는 안 건다** — 그건 *안 판 자리 = 어둠*이라 거의 검어야 세계의 경계가 읽힌다
    // (`RockRenderer`가 옆면에만 얹는다). 심연만 예외적으로 어둡다 — **그게 그 바이옴의 뜻**이다.
    public static UnityEngine.Color RockTint(this Biome b) => b switch
    {
        Biome.Limestone => new Color(0.90f, 0.80f, 0.60f),   // 마른 석회 — 누렇다
        Biome.Cavern    => new Color(0.70f, 0.73f, 0.80f),   // 평범한 굴 — 회청
        Biome.Mine      => new Color(0.78f, 0.60f, 0.40f),   // 폐광 — 흙 묻은 갱벽
        Biome.Fungal    => new Color(0.38f, 0.56f, 1.00f),   // 균사림 — 벽까지 푸르다
        Biome.Flooded   => new Color(0.38f, 0.70f, 0.80f),   // 수몰층 — 젖은 청록
        Biome.Crystal   => new Color(0.74f, 0.58f, 1.00f),   // 수정동 — 보랏빛
        Biome.Lava      => new Color(0.90f, 0.38f, 0.26f),   // 용암층 — 붉게 그을렸다
        Biome.Abyss     => new Color(0.24f, 0.22f, 0.30f),   // 심연 — 여기만 어둡다
        _               => new Color(0.70f, 0.73f, 0.80f),
    };
}
