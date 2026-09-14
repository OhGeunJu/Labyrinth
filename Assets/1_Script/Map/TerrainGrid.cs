using UnityEngine;

// **지형 청크 좌표계 — 이후 전부의 기준**(2026-08-26).
//
// 켄시 규모의 이어진 세계로 가기로 했다(사용자 결정). 필드맵이 200×200 한 장이던 시절에는
// 좌표가 하나뿐이라 규약이 필요 없었는데, **세계를 청크로 나누는 순간 축이 셋이 된다**:
// 월드 좌표(m) · 타일 좌표(1m 격자) · 청크 좌표. 셋을 오가는 식을 파일마다 적으면
// 반드시 하나가 어긋난다 — 그래서 여기 한 곳에 모은다.
//
// ── 값이 어떻게 정해졌나 ────────────────────────────────────────────────────
//
// **세계 29,440m**(≈866 km²) — 켄시가 ~870 km²다. 폰 이동 속도 4m/s · 하루 1800초이므로
// **하루 종일 걸으면 7.2km**, 세계를 가로지르는 데 **4.1 게임일**(실제 두 시간)이다.
//
// **청크 256m** — 작으면(64m) 이음매와 전환이 잦고 관리 대상이 폭증하며, 크면(1km) 하나
// 로드할 때 뚝 끊긴다.
//
// **정점 간격 2m** — 건물은 1m 격자에 놓이지만 `MapManager.IsFlatEnough`가 발자국의
// **네 모서리**만 재므로 2m로 충분하다. 1m로 하면 데이터가 **네 배**가 된다.
//
// ── 왜 전부 저장하지 않나 ───────────────────────────────────────────────────
//
// 축당 14,720 정점 → **2억 1,700만 개**. 16비트로 저장해도 **433MB**다. 그래서 높이의
// 원본은 **노이즈 함수 + 손댄 델타**이고(2번), 저장되는 것은 **손댄 청크뿐**이다.
// Terrain은 데이터가 아니라 **렌더러로** 쓴다 — 청크에 들어설 때 `TerrainData`를 만들어
// 채우고 떠나면 버린다.
public static class TerrainGrid
{
    // ── 규약 ────────────────────────────────────────────────────────────────

    // **2026-08-29에 256→64로 줄였다**(사용자: 지금 크기의 1/4). 세계 한 변이 **256m**다.
    //
    // **`ChunksPerAxis`가 아니라 이쪽을 줄인다.** 청크 수를 4에서 더 내리면 지역 수도 같이
    // 내려가야 하고(`ChunksPerAxis`는 지역 수의 배수여야 한다), 그러면 **세계에 바이옴이
    // 한 종류만 남는다** — 여덟을 저작해 놓고 하나만 보게 된다. 청크를 줄이면 격자 수는
    // 그대로라 **지역이 넷×넷으로 유지된다**(한 지역 64m).
    //
    // **정점 수는 2의 거듭제곱 + 1이어야 한다**(유니티 `Terrain`의 요구) — 64/2+1 = **33**이라
    // 맞는다. 128이면 65, 256이면 129였다.
    public const int ChunkSize     = 64;    // 청크 한 변(m = 타일)
    public const int VertexSpacing = 2;     // 정점 간격(m)
    // **16의 배수여야 한다**(2026-08-26). 저작해 둔 월드맵이 16×16이라, 나눠떨어지지 않으면
    // **지역 경계가 청크 한복판을 가른다** — 바이옴이 바뀌는 자리와 지형이 로드되는 자리가
    // 어긋나서, 이음매에서 성격이 갑자기 튄다.
    //
    // 115였을 때는 지역이 1,840m로 딱 안 떨어졌다. 64면 지역이 **정확히 4청크**다.
    //
    // **줄여도 세계가 단조로워지지 않는다** — 지형은 노이즈가 만들고, 다양성은 크기가 아니라
    // **바이옴 배치**가 낸다(16×16 저작이 그대로 산다).
    //
    //   청크  세계        면적     지역     가로지르기(4m/s)
    //     16   4,096m     17km²    256m     0.6 게임일
    //   → 32   8,192m     67km²    512m     1.1
    //     48  12,288m    151km²    768m     1.7
    //     64  16,384m    268km²  1,024m     2.3
    //     80  20,480m    419km²  1,280m     2.8
    //    112  28,672m    822km²  1,792m     4.0   ← 켄시(~870km²)
    //
    // **이 값을 바꾸면 구운 것이 전부 어긋난다**(2026-08-27). 높이맵·색조는 세계 한 변을
    // 0~1로 재서 뽑으므로 줄이면 **그림이 가로로 지발려** 산이 가파른지고, 장소는 절대
    // 좌표라 **새 범위 밖의 것이 통째로 사라진다.** 순서가 있다:
    // `B. Bake Eroded Heightmap` → `5. Populate World` → `6. Resnap` → `5. Bake World` → `C. Bake World Color Map`.
    //
    // **2026-08-27에 64→32로 줄였다**(사용자 결정) — 아직 테스트만 하는데 268km²를
    // 들고 다니면 굽는 것도 걸어 다니는 것도 네 배가 비싸다. **다시 키우는 것은 이 상수 하나다.**
    // **2026-08-29에 32→16으로 다시 줄였다**(지하로 엎으면서). 층을 다섯 벌 손으로 저작해야
    // 하므로 층당 8km는 너무 크다 — 16이면 층 하나가 4,096m·17km²이고 **다섯을 합쳐 85km²**로
    // 지상 한 장(67km²)보다 오히려 넓다. 16은 지역 수 16의 배수라 **지역이 정확히 1청크**(256m)다.
    // **2026-08-29에 다시 4로 줄였다**(사용자: *"테스트하는데 너무 커"*) — 한 변 256m.
    //
    // **2026-08-31에 16으로 키웠다**(사용자 결정 — 한 변 4배, 넓이 16배). 한 변 **1,024m** ·
    // 1.05km² · 가로지르는 데 256초(0.14 게임일)이고, **지역이 정확히 1청크(64m)**다
    // (`UnderworldSetup.FitWorldMap`이 지도를 16×16으로 맞춘다 — `ChunksPerAxis`는 지역
    // 수의 배수여야 한다).
    //
    // **`ChunkSize`는 안 건드린다** — 그쪽을 키우면 스트리밍 단위가 같이 커져서
    // `radius: 2`가 세계를 통째로 올린다(스트리밍이 있는 이유가 사라진다).
    //
    // **굴의 주기는 같이 안 키운다**(`WorldStreamingSetup.WriteUnderworldProfiles` 참고).
    // 키우고 줄일 때 따라와야 하는 것은 **셀 수로 재는 값**(광역 격자)이지 **미터로 재는
    // 값**(통로 폭·간격)이 아니다 — 후자는 폰의 눈금이라 세계가 넓어져도 그대로다.
    //
    // 키울 때는 이 상수 하나만 올리고 `1. Rebuild Underworld`.
    public const int ChunksPerAxis = 16;    // 세계 한 변의 청크 수

    // **129 = 128 + 1.** 마지막 정점은 이웃 청크의 첫 정점과 **같은 자리**다 — 가장자리를
    // 공유하지 않으면 부동소수 오차로 **틈이 벌어져 하늘이 보인다.** 공유하는 대신 그 줄을
    // 두 청크가 각자 계산하므로, **같은 입력이면 같은 값**이 나와야 한다(노이즈가 결정적인
    // 이유이기도 하다).
    public const int VertsPerAxis = ChunkSize / VertexSpacing + 1;   // 129

    public const int WorldSize = ChunkSize * ChunksPerAxis;          // 1,024m

    // ── 지역 — 저작한 월드맵의 한 칸 ────────────────────────────────────────
    //
    // 월드맵(`WorldMapData`)이 16×16으로 저작돼 있다. 이어진 세계에서 그 격자는 *"이동하는
    // 칸"*이 아니라 **세계를 나눈 구역**으로 읽힌다 — 그 지역의 바이옴이 **지형 성격 · 나오는
    // 자원 · 위험도 · 마을/소굴**을 정한다. **저작해 둔 것이 그대로 산다.**
    // **저작한 지도의 크기를 그대로 쓴다 — 상수로 박지 않는다**(2026-08-26).
    //
    // 16으로 못 박아 두었더니 **세계를 넓힐 때마다 지역이 같이 커져** 1,024m가 됐다.
    // 그러면 바이옴 하나가 걸어서 4분이고, 지도가 **거대한 격자 아홉 칸**처럼 보인다.
    // 지역의 크기는 세계가 아니라 **저작한 해상도**가 정해야 한다 — 지도를 64×64로 키우면
    // 지역이 256m(청크 하나)가 되고, **여기도 저기도 다른 곳**이 된다.
    //
    // `WorldMapData`를 읽으므로 **저작 도구로 지도를 키우면 세계가 따라온다.**
    static int _regions;

    public static int RegionsPerAxis
    {
        get
        {
            if (_regions <= 0) _regions = Mathf.Max(1, WorldMap.AuthoredWidth);
            return _regions;
        }
    }

    // 지도를 다시 키운 뒤 부른다. 안 비우면 옛 해상도로 계속 잰다.
    public static void ForgetRegions() => _regions = 0;

    public static float RegionSize => WorldSize / (float)RegionsPerAxis;

    // 월드 좌표 → 지역. **경계 밖은 가장자리로 접는다** — 세계 끝에서 배열 밖을 짚으면
    // 바이옴이 통째로 기본값으로 떨어져 그 줄만 지형이 다르다.
    public static Vector2Int RegionAt(float worldX, float worldZ) => new(
        Mathf.Clamp(Mathf.FloorToInt(worldX / RegionSize), 0, RegionsPerAxis - 1),
        Mathf.Clamp(Mathf.FloorToInt(worldZ / RegionSize), 0, RegionsPerAxis - 1));

    public static Vector2Int RegionAt(Vector3 worldPos) => RegionAt(worldPos.x, worldPos.z);

    // 지역의 가운데(월드 좌표). 마을·소굴을 그 지역 어딘가에 세울 때의 기준.
    public static Vector3 RegionCenter(Vector2Int region) => new(
        (region.x + 0.5f) * RegionSize, 0f, (region.y + 0.5f) * RegionSize);

    // **지역 격자에서의 연속 좌표**(0 ~ 15.999…). 이웃 넷을 섞을 때 쓴다 —
    // 정수로 끊으면 지역 경계에서 지형 성격이 **칼같이 바뀐다.**
    public static Vector2 RegionUV(float worldX, float worldZ) => new(
        worldX / RegionSize, worldZ / RegionSize);

    // ── 바이옴 경계는 격자보다 곱다 (2026-09-15) ────────────────────────────
    //
    // 사용자: *"칠한거는 네모낳게 깔리잖아 내가 원하는 건 자연스러운 경계인데."*
    //
    // 지역이 64m라 **칠하면 64m 계단**이다. 해상도를 올리면(지도를 64x64로) 칠할 칸이
    // 4,096개가 되어 **저작이 노동이 되고**, 무엇보다 `ChunksPerAxis`가 지역 수의 배수여야
    // 한다는 제약에 걸린다.
    //
    // **저작 해상도와 경계 해상도를 같게 둘 이유가 없다.** 칠하는 것은 성기게 두고
    // **읽을 때 자리를 굽힌다**(도메인 워프) — 그러면 64m 계단이 10~20m로 굽이치는 선이
    // 되고, 저작은 여전히 칸 열여섯이다.
    //
    // **모든 바이옴 물음이 이 문을 지난다** — 바닥 텍스처(`TerrainSplat`) · 지도 색
    // (`TerrainHeight.MapColor`) · 기복(`BlendedBiome`) · 광맥 풀과 벽 색조와 뿌리기
    // (`WorldMap.BiomeAtWorld`). 한 곳이라도 안 지나면 **바닥이 바뀌는 자리와 광맥이
    // 바뀌는 자리가 어긋나** 경계가 두 줄로 보인다.
    //
    // **주소는 안 굽힌다** — `RegionAt`은 *"여기가 몇 번 칸인가"*에 답하고 그건 저작
    // 좌표다(지도 툴팁 · `WorldSites.InRegion` · 장소 배치). 굽히면 **툴팁이 거짓말을 한다.**
    //
    // 폭은 **지역의 3할 남짓**(±18m). 더 키우면 한 칸짜리 구역이 이웃에 통째로 먹히고,
    // 줄이면 계단이 그대로 보인다. 잔물결을 한 겹 얹는 이유는 **큰 것만 쓰면 경계가
    // 지나치게 매끈한 곡선**이라 사람이 그린 선으로 안 읽히기 때문이다.
    const float WarpPeriod = 110f;   // m — 크게 굽이치는 주기
    const float WarpRipple =  38f;   // m — 잔물결
    const float WarpAmp    =  30f;   // m — (펄린이 ±0.3이라 실효 ±18m 남짓)

    public static Vector2 BiomeSample(float worldX, float worldZ)
    {
        float nx = Mathf.PerlinNoise(worldX / WarpPeriod + 13.7f, worldZ / WarpPeriod + 91.3f) - 0.5f;
        float nz = Mathf.PerlinNoise(worldX / WarpPeriod + 57.1f, worldZ / WarpPeriod + 24.9f) - 0.5f;

        nx += (Mathf.PerlinNoise(worldX / WarpRipple +  5.5f, worldZ / WarpRipple + 71.2f) - 0.5f) * 0.35f;
        nz += (Mathf.PerlinNoise(worldX / WarpRipple + 44.3f, worldZ / WarpRipple +  8.1f) - 0.5f) * 0.35f;

        return new Vector2(worldX + nx * WarpAmp, worldZ + nz * WarpAmp);
    }

    // 굽힌 자리의 지역 격자 좌표 — **바이옴을 묻는 곳은 전부 이걸 쓴다.**
    public static Vector2 BiomeUV(float worldX, float worldZ)
    {
        var p = BiomeSample(worldX, worldZ);
        return RegionUV(p.x, p.y);
    }

    public static Vector2Int BiomeRegion(float worldX, float worldZ)
    {
        var p = BiomeSample(worldX, worldZ);
        return RegionAt(p.x, p.y);
    }

    // **원점은 (0,0) 모서리다 — 가운데가 아니다.** 기존 코드가 타일 좌표를 음수로 안 받는
    // 자리가 있고(`MapManager.IsInBuildZone`이 0부터 잰다), 세계를 가운데 정렬해서 얻는 것이
    // 없다. 타일은 0 ~ 29,439.

    // ── 변환 ────────────────────────────────────────────────────────────────

    // 월드 좌표 → 청크. **`FloorToInt`여야 한다** — 음수에서 0으로 끌리는 정수 나눗셈을 쓰면
    // 경계 밖 좌표가 (0,0) 청크로 접혀 **세계 반대편이 같은 청크로 보인다.**
    public static Vector2Int ChunkAt(float worldX, float worldZ) => new(
        Mathf.FloorToInt(worldX / ChunkSize),
        Mathf.FloorToInt(worldZ / ChunkSize));

    public static Vector2Int ChunkAt(Vector3 worldPos) => ChunkAt(worldPos.x, worldPos.z);

    public static Vector2Int ChunkOf(Vector2Int tile) => new(
        Mathf.FloorToInt(tile.x / (float)ChunkSize),
        Mathf.FloorToInt(tile.y / (float)ChunkSize));

    // 청크의 남서쪽 모서리(월드 좌표). `TerrainData`를 놓는 자리이기도 하다.
    public static Vector3 OriginOf(Vector2Int chunk) =>
        new(chunk.x * ChunkSize, 0f, chunk.y * ChunkSize);

    // 세계 안인가 — 청크·타일·월드 좌표 각각.
    public static bool InWorld(Vector2Int chunk) =>
        chunk.x >= 0 && chunk.y >= 0 && chunk.x < ChunksPerAxis && chunk.y < ChunksPerAxis;

    public static bool TileInWorld(Vector2Int tile) =>
        tile.x >= 0 && tile.y >= 0 && tile.x < WorldSize && tile.y < WorldSize;

    // ── 이름 ────────────────────────────────────────────────────────────────

    // 델타 에셋 파일 이름 등에 쓴다. **좌표를 그대로 쓴다** — 순번을 매기면 청크를 옮기거나
    // 세계 크기를 바꿀 때 이름과 자리가 어긋난다.
    public static string KeyOf(Vector2Int chunk) => $"{chunk.x}_{chunk.y}";
}
