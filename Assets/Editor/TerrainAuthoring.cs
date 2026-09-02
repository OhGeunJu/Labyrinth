using UnityEditor;
using UnityEngine;

// **지형 저작 도구들이 같이 쓰는 것**(2026-08-27) — 씬에서 값을 읽어 오는 자리와,
// 띄워 둔 미리보기 지형을 채우는 식.
//
// 도구가 늘 때마다 `TerrainHeight.Profile = streamer.Profile` 세 줄이 복사됐다(조각 · 장소
// 배치 · 장소 굽기, 그리고 이제 산·분지 핸들까지 넷). 하나를 빠뜨리면 **에디터에서 보는
// 지형과 게임에서 도는 지형이 갈려** 구운 델타가 통째로 어긋나는데, 증상이 *"조각한 자리가
// 게임에서는 딴 데"*라 원인을 짚기가 어렵다. 그래서 답을 한 벌로 만든다.
public static class TerrainAuthoring
{
    // **미리보기 지형의 루트 이름은 여기 하나다.** 이름을 아는 곳이 둘이면 한쪽이 못 찾는다.
    public const string PreviewRoot = "__TerrainPreview";

    // 마지막으로 밀어 넣은 값 — 바뀐 것을 알아채려고만 든다(위 `SyncValues` 참고).
    static TerrainProfile        _lastProfile;
    static TerrainCeilingProfile _lastCeiling;
    static TerrainFeature[]      _lastFeatures;
    static TerrainFeature[]      _lastCeilingFeatures;
    static TerrainHeightmap      _lastHeightmap;

    // ── 씬에서 값을 읽어 온다 ───────────────────────────────────────────────

    // **값만 — 매 프레임 불러도 싼 것.** 핸들을 그리는 자리(`OnSceneGUI`)가 쓴다.
    // 캐시를 비우지 않으므로 델타·바이옴은 이미 읽어 둔 것을 그대로 쓴다.
    public static TerrainStreamer SyncValues()
    {
        var s = Object.FindFirstObjectByType<TerrainStreamer>();
        if (s == null) return null;
        TerrainHeight.Profile  = s.Profile;
        TerrainHeight.Features = s.Features;
        TerrainHeight.Heightmap = s.Heightmap;
        TerrainSplat.Palette   = s.Palette;
        if (s.Palette != null) s.Palette.PushToShader();   // **산까지 같이** — 빠뜨리면 산 전체가 델타로 구워진다

        // **천장도 같이 읽는다** — 안 읽으면 도구가 코드 기본값으로 굴을 그리고 게임은 씬
        // 값으로 판정해서, *"그림에는 길이 있는데 폰은 벽을 민다"*가 된다. 바닥 프로필을
        // 여기 모아 둔 것과 같은 이유다.
        TerrainCeiling.Enabled  = s.CeilingOn;
        TerrainCeiling.Profile  = s.CeilingProfile;
        TerrainCeiling.Features = s.CeilingFeatures ?? System.Array.Empty<TerrainFeature>();

        // **값이 바뀌었으면 기억한 정점을 버린다.** `TerrainHeight`/`TerrainCeiling`은 정점
        // 하나를 한 번만 세고 담아 두는데, 여기는 **캐시를 안 비우기로 한 자리**라(프레임마다
        // 도는 손잡이 그리기) 그대로 두면 *"손잡이는 움직이는데 지형은 그대로"*가 된다.
        // 인스펙터 편집은 `OnValidate → Apply`가 이미 버리므로 여기서 잡는 것은 **그 밖의
        // 경로**(도구가 다른 씬의 값을 밀어 넣는 경우)다. 안 바뀌었으면 아무 일도 안 한다.
        bool changed = !_lastProfile.Equals(s.Profile)
                    || !_lastCeiling.Equals(s.CeilingProfile)
                    || !ReferenceEquals(_lastFeatures, s.Features)
                    || !ReferenceEquals(_lastCeilingFeatures, s.CeilingFeatures)
                    || _lastHeightmap != s.Heightmap;
        if (changed)
        {
            _lastProfile         = s.Profile;
            _lastCeiling         = s.CeilingProfile;
            _lastFeatures        = s.Features;
            _lastCeilingFeatures = s.CeilingFeatures;
            _lastHeightmap       = s.Heightmap;
            TerrainHeight.ForgetVertices();
            TerrainCeiling.ForgetVertices();
        }
        return s;
    }

    // **값 + 캐시 비우기.** 굽기 전에 부른다 — 옛 델타·옛 바이옴으로 조각하면 그대로 굳는다.
    public static void SyncFromScene()
    {
        if (SyncValues() == null)
            Debug.LogWarning("[지형] 씬에 TerrainStreamer가 없다 — 기본 프로필로 조각한다. " +
                             "게임에서 다른 값을 쓰면 구운 델타가 어긋난다.");
        TerrainHeight.ForgetDeltas();
        TerrainHeight.ForgetBiomes();
    }

    // ── 청크 하나를 지금 높이로 채운다 ──────────────────────────────────────

    // **정규화는 세계가 한 벌을 쓴다**(`TerrainStreamer.MinHeight`/`HeightRange`) —
    // 청크마다 제 최소/최대로 재면 이웃과 눈금이 달라져 경계에서 지형이 어긋난다.
    public static float Norm(float worldHeight) =>
        Mathf.Clamp01((worldHeight - TerrainStreamer.MinHeight) / TerrainStreamer.HeightRange);

    // **띄우는 쪽과 다시 그리는 쪽이 같은 식을 써야 한다** — 갈리면 미리보기를 갱신한 순간
    // 지형이 조용히 어긋나고, 그 위에 놓아 둔 마을이 뜨거나 파묻힌다.
    public static void Fill(TerrainData data, Vector2Int chunk)
    {
        int res = TerrainGrid.VertsPerAxis;
        var h = new float[res, res];
        for (int vz = 0; vz < res; vz++)
        for (int vx = 0; vx < res; vx++)
            h[vz, vx] = Norm(TerrainHeight.VertexHeight(chunk, vx, vz));
        data.SetHeights(0, 0, h);

        // **텍스처도 같은 자리에서 깐다** — 채우는 문이 하나여야 저작 씬에서 본 땅과
        // 게임의 땅이 같은 색이다(`TerrainSplat`).
        TerrainSplat.Apply(data, chunk, h);
    }

    // ── 띄워 둔 미리보기를 지금 높이로 다시 그린다 ──────────────────────────
    //
    // 산을 옮기면 미리보기는 **뒤처진다** — 만들 때 한 번 채운 메시라 저 혼자 안 따라온다.
    // 그래서 손잡이를 놓는 순간 여기를 부른다.
    //
    // **조각 중인 청크(`__TerrainEdit`)는 절대 안 건드린다** — 거기엔 아직 안 구운 붓질이
    // 들어 있어, 다시 그리면 그 작업이 통째로 날아간다.
    public static int RefreshPreview()
    {
        int n = 0;

        // ① 세계 한 장(`WorldPreview`) — **띄워 둔 것이 있을 때만** 다시 굽는다. 없는데
        // 만들어 주면, 산 하나 옮겼다고 26만 정점이 난데없이 화면에 선다.
        if (WorldPreview.Rebuild()) n++;

        // ② 저작 씬에 서 있는 진짜 해상도 지형(`WorldSceneTerrain`) — 세워 둔 것이 있을 때만.
        WorldSceneTerrain.Refresh();

        // ③ 조각용 3×3 미리보기
        var root = GameObject.Find(PreviewRoot);
        if (root == null) return n;

        SyncValues();
        foreach (var t in root.GetComponentsInChildren<Terrain>())
        {
            if (t.terrainData == null) continue;
            Fill(t.terrainData, TerrainGrid.ChunkAt(t.transform.position));
            n++;
        }
        return n;
    }
}
