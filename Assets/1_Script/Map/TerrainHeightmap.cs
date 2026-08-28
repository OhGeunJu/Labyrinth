using UnityEngine;

// **저작한 높이맵 — 대륙의 모양**(2026-08-27).
//
// 여기까지 세계의 큰 모양은 **펄린 한 층**(`ContinentPeriod` 4km)이었다. 그것으로는 언덕이
// 오르내릴 뿐 **물길도 능선도 골짜기도 안 생긴다** — 자연 지형이 그렇게 보이는 이유의 절반은
// **침식**인데, 노이즈는 침식을 모른다.
//
// 켄시가 `fullmap.tif` 한 장(World Machine 출력)으로 하는 그것이다. 우리도 같은 자리에 한 층을
// 둔다 — 다만 **굽는 것은 우리 도구**이고(`TerrainErosionBaker`), 나중에 Gaea 같은 것을 들이면
// **같은 자리에 그 출력을 끼우면 된다.** 바뀌는 것은 이 에셋의 내용뿐이다.
//
// ── 왜 이미지가 아니라 `.bytes`인가 ────────────────────────────────────────
//
// 높이맵을 `Texture2D`로 두면 **임포트 설정이 조용히 값을 망친다** — 압축(DXT는 손실),
// sRGB 변환, 밉맵, `Read/Write` 꺼짐. 켄시 글이 경고하는 *"잘못된 규격이 계단 지형을 만든다"*가
// 정확히 그 자리다.
//
// 원시 바이트로 두면 그 함정이 **전부 없다.** `TextAsset.bytes`는 있는 그대로 온다.
//
// ── 왜 함수인 채로 남는가 ──────────────────────────────────────────────────
//
// 데이터를 들여도 **`TerrainHeight`는 여전히 함수다** — 메모리에 상주하는 배열을 이중선형으로
// 뽑을 뿐이라, **로드 안 된 청크의 높이도 그대로 답이 나온다.** 그것이 이 저장소가 지키는
// 성질이고, 청크마다 `TerrainData`를 굽는 방식과 갈리는 지점이다.
[CreateAssetMenu(fileName = "TerrainHeightmap", menuName = "DungeonColony/TerrainHeightmap")]
public class TerrainHeightmap : ScriptableObject
{
    [Tooltip("`ushort` 행 우선. 굽는 도구가 만든다 — 손으로 넣지 말 것.")]
    public TextAsset Data;

    [Tooltip("한 변의 셀 수. 세계 한 변을 이만큼으로 나눈다.")]
    public int Resolution;

    [Tooltip("`0`과 `65535`가 각각 몇 m인가. 굽을 때 실제로 나온 범위를 적어 둔다 — " +
             "고정값으로 두면 낮은 세계는 눈금을 낭비하고 높은 세계는 잘린다.")]
    public float MinHeight, MaxHeight;

    // **침식이 알고 있는 것** — `R`=퇴적 · `G`=침식 · `B`=물길. 채널당 한 바이트.
    //
    // 경사만 봐서는 **물이 지난 데와 아닌 데를 구별할 수 없다.** 그런데 눈에는 그게
    // 다르게 보여야 한다 — 골짜기 바닥엔 흙이 쌓이고 능선은 맨바위가 드러난다.
    // 물방울 시뮬이 어차피 세는 값이라 **굽기 한 번에 같이 나온다.**
    [Tooltip("`R`=퇴적 · `G`=침식 · `B`=물길. 굽는 도구가 만든다.")]
    public TextAsset Masks;

    [Tooltip("마스크 한 변의 셀 수. **높이맵보다 성길 수 있다** — 골짜기에 흙을 까는 데 " +
             "4m 눈금까지는 필요 없고, 채널이 셋이라 크기가 1.5배로 붙는다.")]
    public int MaskResolution;

    [Tooltip("굽는 데 쓴 시드·설정 — 코드는 안 읽는다. 다시 구울 때 뭘 넣었는지 알기 위한 것.")]
    [TextArea(2, 6)] public string BakedWith;

    // **어느 크기의 세계에서 구웠나.** 이건 코드가 읽는다.
    //
    // 뽑을 때 `worldX / TerrainGrid.WorldSize`로 재므로, 세계를 줄이면 **같은 그림이 좁은
    // 데로 눌려 들어가** 모든 경사가 그만큼 가팔라진다. 16,384m에서 구운 것을 8,192m 세계에
    // 쓰면 **기울기가 두 배**가 되고, 화면에서는 *"평지인데 건물이 안 지어진다"*로만 보인다
    // (2026-08-27에 실제로 그렇게 나타났다).
    //
    // **눈으로는 못 알아챈다** — 지형은 여전히 지형처럼 생겼다. 그래서 적어 두고 알린다.
    [Tooltip("이 높이맵을 구울 때의 세계 한 변(m). 지금 세계와 다르면 경고한다. 0이면 모른다.")]
    public int BakedWorldSize;

    public bool MatchesWorld => BakedWorldSize == TerrainGrid.WorldSize;

    // **한 번 풀고 들고 있는다.** 정점마다 불리는 자리라(청크 하나가 16,641번) 매번
    // `TextAsset.bytes`를 만지면 그때마다 배열이 복사된다.
    ushort[] _cells;
    byte[]   _masks;

    public bool IsUsable => Data != null && Resolution > 1;

    public float MetersPerCell => TerrainGrid.WorldSize / (float)Resolution;

    // 다시 구운 뒤 부른다. 안 비우면 **에디터가 옛 높이를 계속 쓴다.**
    public void Forget() { _cells = null; _masks = null; }

    ushort[] Cells
    {
        get
        {
            if (_cells != null) return _cells;
            if (!IsUsable) return null;

            var bytes = Data.bytes;
            int want  = Resolution * Resolution;

            if (bytes.Length < want * 2)
            {
                // **조용히 잘라 쓰지 않는다** — 절반만 든 높이맵은 세계 반쪽이 평지가 되는데,
                // 그건 "지형이 이상하다"로만 보여서 원인을 짚을 데가 없다.
                Debug.LogError($"[높이맵] `{name}`의 데이터가 모자란다 — {Resolution}²이면 " +
                               $"{want * 2:N0}바이트여야 하는데 {bytes.Length:N0}뿐이다. 다시 구울 것.");
                Resolution = 0;
                return null;
            }

            _cells = new ushort[want];
            System.Buffer.BlockCopy(bytes, 0, _cells, 0, want * 2);

            // **한 번만 알린다** — 정점마다 불리는 자리라 매번 찍으면 콘솔이 잠긴다.
            if (BakedWorldSize <= 0)
                Debug.LogWarning($"[높이맵] `{name}`은 어느 크기의 세계에서 구웠는지 모른다 " +
                                 $"(지금 {TerrainGrid.WorldSize:N0}m). 다시 구우면 기록된다.");
            else if (!MatchesWorld)
                Debug.LogError($"[높이맵] `{name}`은 {BakedWorldSize:N0}m 세계에서 구웠는데 " +
                               $"지금 세계는 {TerrainGrid.WorldSize:N0}m다 — **그림이 " +
                               $"{TerrainGrid.WorldSize / (float)BakedWorldSize:0.##}배로 눌려 들어가** " +
                               "모든 경사가 그만큼 달라진다(평지가 비탈이 된다). " +
                               "`B. Bake Eroded Heightmap`을 다시 돌릴 것.");

            return _cells;
        }
    }

    public bool HasMasks => Masks != null && MaskResolution > 1;

    byte[] MaskCells
    {
        get
        {
            if (_masks != null) return _masks;
            if (!HasMasks) return null;

            var bytes = Masks.bytes;
            int want  = MaskResolution * MaskResolution * 3;
            if (bytes.Length < want)
            {
                Debug.LogError($"[높이맵] `{name}`의 마스크가 모자란다 — {MaskResolution}²×3이면 " +
                               $"{want:N0}바이트여야 하는데 {bytes.Length:N0}뿐이다. 다시 구울 것.");
                MaskResolution = 0;
                return null;
            }
            _masks = bytes;
            return _masks;
        }
    }

    // ── 뽑기 ────────────────────────────────────────────────────────────────

    // 침식 마스크(0~1). x=퇴적 · y=침식 · z=물길.
    //
    // **가장 가까운 셀을 쓴다 — 이중선형이 아니다.** 스플랫이 4m 격자로 뽑는데 마스크가
    // 8m 눈금이면 이미 충분히 성기고, 여기에 보간을 더하면 골짜기 경계가 흐려져
    // *"어디부터 흙인가"*가 안 읽힌다.
    public Vector3 SampleMask(float worldX, float worldZ)
    {
        var m = MaskCells;
        if (m == null) return Vector3.zero;

        int n = MaskResolution;
        int x = Mathf.Clamp(Mathf.RoundToInt(worldX / TerrainGrid.WorldSize * (n - 1)), 0, n - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(worldZ / TerrainGrid.WorldSize * (n - 1)), 0, n - 1);

        int i = (y * n + x) * 3;
        return new Vector3(m[i] / 255f, m[i + 1] / 255f, m[i + 2] / 255f);
    }

    // 월드 좌표의 높이(m). **이중선형** — 셀 눈금이 그대로 드러나면 8m마다 계단이 보인다.
    public float SampleAt(float worldX, float worldZ)
    {
        var c = Cells;
        if (c == null) return 0f;

        int n = Resolution;
        float u = worldX / TerrainGrid.WorldSize * (n - 1);
        float v = worldZ / TerrainGrid.WorldSize * (n - 1);

        // **세계 밖은 가장자리로 접는다** — 배열 밖을 짚으면 그 줄만 통째로 다른 높이가 된다.
        u = Mathf.Clamp(u, 0f, n - 1.001f);
        v = Mathf.Clamp(v, 0f, n - 1.001f);

        int x0 = (int)u, y0 = (int)v;
        int x1 = Mathf.Min(x0 + 1, n - 1), y1 = Mathf.Min(y0 + 1, n - 1);
        float tx = u - x0, ty = v - y0;

        float h00 = c[y0 * n + x0], h10 = c[y0 * n + x1];
        float h01 = c[y1 * n + x0], h11 = c[y1 * n + x1];

        float raw = Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
        return Mathf.Lerp(MinHeight, MaxHeight, raw / 65535f);
    }
}
