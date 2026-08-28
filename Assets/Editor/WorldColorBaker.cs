using System.IO;
using UnityEditor;
using UnityEngine;

// **세계 색조를 굽는다 — 지역의 정체성**(2026-08-27).
//
// Terrain Layer는 *"여기 무엇이 깔려 있나"*에 답한다. 그런데 눈이 *"저긴 다른 곳이다"*라고
// 읽는 것은 재질이 아니라 **색**이다. 같은 흙이라도 잿빛 골짜기와 붉은 협곡은 다르고,
// 그걸 레이어로 하려면 **색깔 수만큼 레이어를 늘려야** 하는데 URP 지형은 한 패스에 넉 장이라
// 애드 패스가 금방 늘어난다. **색조는 세계 전체를 덮는 그림 한 장으로 하는 것이 맞다.**
//
// 켄시가 높이맵 위에 색 오버레이를 얹는 그 자리다.
//
// ── 무엇에서 나오나 ────────────────────────────────────────────────────────
//
// **손으로 칠하는 것에서 시작하지 않는다.** 16,384m를 처음부터 칠하라고 하면 아무도 안 칠하고,
// 칠하다 말면 세계 절반만 색이 있다. 그래서 **이미 있는 것에서 뽑는다**:
//
//   · **바이옴** — 지도 색(`Biome.Color`)에서 색조만 떼어 온다(단일 출처).
//   · **지역 변주** — km 단위 노이즈. *한 사막 안에서도* 창백한 데와 녹슨 데가 갈린다.
//     **이것이 이 그림의 요점이다** — 바이옴만 쓰면 격자를 색으로 다시 그린 것뿐이다.
//   · **침식 마스크** — 깎인 데는 맨바위라 탈색되고, 쌓인 데는 흙이라 따뜻하고,
//     **물길은 젖어서 어둡고 푸르다**(굽어 있었으나 아무도 안 읽던 `B` 채널이 여기서 쓰인다).
//   · **표고** — 높은 데는 옅어진다(설선·공기 원근).
//
// 그렇게 나온 것이 **PNG 한 장**이라, 마음에 안 들면 포토샵으로 열어 덧칠하면 된다.
// 다시 구우면 덧칠이 날아가므로 **덧칠한 것은 이름을 바꿔 두고 팔레트에 물릴 것.**
//
// ── 왜 `.bytes`가 아니라 이미지인가 ────────────────────────────────────────
//
// 높이맵은 `.bytes`다 — 압축·sRGB·밉맵이 **값을 조용히 망치기** 때문이다. 색조는 다르다.
// 압축이 만드는 오차는 **곱하는 배율의 1% 미만**이라 안 보이고, 무엇보다 **칠할 수 있어야**
// 하는 그림이라 이미지인 편이 맞다.
//
// 다만 **sRGB는 끈다** — 이건 보여 줄 색이 아니라 **곱할 배율**이다. sRGB로 두면 임포터가
// 감마를 벗겨서 우리가 적은 값과 셰이더가 읽는 값이 갈린다.
public class WorldColorBaker : EditorWindow
{
    const string Dir  = "Assets/5_Data/World";
    const string Path = Dir + "/WorldColor.png";

    // 해상도. **높이맵과 달리 성겨도 된다** — 이건 지형의 모양이 아니라 넓은 색 얼룩이다.
    //
    //   512  → 32m/텍셀 — 지역만 갈린다
    //   1024 → 16m/텍셀 — 골짜기 하나가 읽힌다. **기본값**
    //   2048 →  8m/텍셀 — 물길이 선으로 보인다
    static readonly int[] Sizes = { 512, 1024, 2048 };

    int _resolution = 1024;
    int _seed       = 20260827;

    // ── 얼마나 얹나 ────────────────────────────────────────────────────────
    //
    // 전부 **1을 중심으로 한 배율**이다. 세게 잡으면 레이어가 들고 있는 성격을 덮어 버리므로
    // 기본값은 전부 얌전하다 — 색조는 *알아채는* 것이지 *보이는* 것이 아니다.
    float _biome   = 0.30f;   // 바이옴 색조
    float _region  = 0.16f;   // 지역 변주(밝기)
    float _hue     = 0.09f;   // 지역 변주(따뜻/차가움)
    float _wear    = 0.35f;   // 깎인 데 → 맨바위
    float _deposit = 0.30f;   // 쌓인 데 → 흙
    float _flow    = 0.45f;   // 물길 → 젖은 골짜기
    float _pale    = 0.22f;   // 높은 데 → 옅어짐

    float _regionPeriod = 2600f;   // 지역 변주의 주기(m)
    float _paleFrom     = 90f;     // 옅어지기 시작하는 표고(m)
    float _paleTo       = 260f;    // 완전히 옅어지는 표고(m)

    [MenuItem("DungeonColony/Terrain/C. Bake World Color Map")]
    static void Open()
    {
        var w = GetWindow<WorldColorBaker>("World Color");
        w.minSize = new Vector2(360f, 480f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("세계 색조 — 지역의 정체성", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "레이어는 *무엇이 깔렸나*에 답하고 이건 *무슨 색인가*에 답한다. " +
            "바이옴·침식·표고에서 뽑아 한 장으로 굽는다.", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(6f);
        int idx = Mathf.Max(0, System.Array.IndexOf(Sizes, _resolution));
        var labels = new string[Sizes.Length];
        for (int i = 0; i < Sizes.Length; i++)
            labels[i] = $"{Sizes[i]}²  —  {TerrainGrid.WorldSize / (float)Sizes[i]:0.#}m/텍셀";
        _resolution = Sizes[EditorGUILayout.Popup("해상도", idx, labels)];
        _seed = EditorGUILayout.IntField("시드", _seed);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("어디서 나오나", EditorStyles.boldLabel);
        _biome  = EditorGUILayout.Slider("바이옴 색조", _biome, 0f, 1f);
        _region = EditorGUILayout.Slider("지역 변주(밝기)", _region, 0f, 0.5f);
        _hue    = EditorGUILayout.Slider("지역 변주(색조)", _hue, 0f, 0.3f);
        _regionPeriod = EditorGUILayout.Slider("지역 변주 주기(m)", _regionPeriod, 600f, 6000f);

        EditorGUILayout.Space(4f);
        _wear    = EditorGUILayout.Slider("깎인 데 → 맨바위", _wear, 0f, 1f);
        _deposit = EditorGUILayout.Slider("쌓인 데 → 흙", _deposit, 0f, 1f);
        _flow    = EditorGUILayout.Slider("물길 → 젖은 골짜기", _flow, 0f, 1f);

        var hm = FindHeightmap();
        if (hm == null || !hm.HasMasks)
            EditorGUILayout.HelpBox(
                "침식 마스크가 없다 — 위 셋은 아무 일도 안 한다. `B. Bake Eroded Heightmap`을 먼저.",
                MessageType.Warning);

        EditorGUILayout.Space(4f);
        _pale     = EditorGUILayout.Slider("높은 데 → 옅어짐", _pale, 0f, 0.6f);
        _paleFrom = EditorGUILayout.Slider("옅어지기 시작(m)", _paleFrom, -50f, 300f);
        _paleTo   = EditorGUILayout.Slider("완전히 옅어짐(m)", _paleTo, 0f, 600f);

        EditorGUILayout.Space(10f);
        if (GUILayout.Button("굽는다", GUILayout.Height(28f))) Bake();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            $"{Path}에 쓰고 팔레트에 물린다.\n" +
            "**PNG라 덧칠할 수 있다** — 다만 다시 구우면 덮어쓰므로 덧칠한 것은 이름을 바꿀 것.",
            MessageType.None);
    }

    static TerrainHeightmap FindHeightmap()
    {
        var s = Object.FindFirstObjectByType<TerrainStreamer>();
        return s != null ? s.Heightmap : TerrainHeight.Heightmap;
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    void Bake()
    {
        var streamer = TerrainAuthoring.SyncValues();
        if (streamer == null)
        {
            Debug.LogError("[색조] 씬에 TerrainStreamer가 없다 — 어떤 세계를 칠할지 알 수 없다.");
            return;
        }

        var hm = TerrainHeight.Heightmap;
        bool masks = hm != null && hm.HasMasks;

        int n = _resolution;
        float meters = TerrainGrid.WorldSize / (float)n;
        var px = new Color32[n * n];

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            for (int y = 0; y < n; y++)
            {
                if ((y & 63) == 0 && EditorUtility.DisplayCancelableProgressBar(
                        "세계 색조", $"{y} / {n}", y / (float)n))
                    return;

                float wz = (y + 0.5f) * meters;
                for (int x = 0; x < n; x++)
                {
                    float wx = (x + 0.5f) * meters;
                    Color t = TintAt(wx, wz, hm, masks);

                    // **0.5가 중립이다** — 셰이더가 두 배 해서 읽으므로 0~1이 ×0~×2가 된다.
                    px[y * n + x] = new Color32(
                        (byte)(Mathf.Clamp01(t.r * 0.5f) * 255f),
                        (byte)(Mathf.Clamp01(t.g * 0.5f) * 255f),
                        (byte)(Mathf.Clamp01(t.b * 0.5f) * 255f),
                        255);
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        Write(px, n, streamer);

        Debug.Log($"[색조] {n}² ({meters:0.#}m/텍셀) — {sw.ElapsedMilliseconds / 1000f:0.#}초. " +
                  (masks ? "침식 마스크를 같이 읽었다." : "**침식 마스크 없이** 구웠다(바이옴·표고만)."));
    }

    // 그 자리의 배율. 1이 중립이다.
    Color TintAt(float wx, float wz, TerrainHeightmap hm, bool masks)
    {
        Color t = Color.white;

        // ── ① 바이옴 ────────────────────────────────────────────────────────
        //
        // **지도 색에서 색조만 떼어 온다** — 그 표는 이미 하나뿐이다(`Biome.Color`).
        // 값을 그대로 곱하면 사막이 노랗게 타 버리므로 **평균을 1로 맞춰** 밝기는 안 건드린다.
        var bb = TerrainHeight.BlendAt(wx, wz);
        Color biome = Hue(bb.A) * bb.Wa + Hue(bb.B) * bb.Wb + Hue(bb.C) * bb.Wc + Hue(bb.D) * bb.Wd;
        t = Mul(t, Color.Lerp(Color.white, biome, _biome));

        // ── ② 지역 변주 ─────────────────────────────────────────────────────
        //
        // **이것이 요점이다.** 바이옴만 쓰면 격자를 색으로 다시 그린 것뿐이고, 격자는 512m
        // 한 칸이라 *"이 골짜기만 붉다"*를 적을 자리가 없다. 여기서 그것이 생긴다.
        float p = Mathf.Max(200f, _regionPeriod);
        float big = BiomeTerrain.FBm(wx / p, wz / p, 3, _seed * 0.371f);
        float mid = BiomeTerrain.FBm(wx / (p * 0.34f), wz / (p * 0.34f), 3, _seed * 0.913f + 37f);
        float v   = big * 0.7f + mid * 0.3f;
        t = Mul(t, Gray(1f + v * _region));

        float warm = BiomeTerrain.FBm(wx / (p * 1.6f) + 11f, wz / (p * 1.6f) + 11f, 2, _seed * 0.577f + 91f);
        t = Mul(t, new Color(1f + warm * _hue, 1f, 1f - warm * _hue));

        // ── ③ 침식이 아는 것 ────────────────────────────────────────────────
        if (masks)
        {
            // x=퇴적 · y=침식 · z=물길
            var m = hm.SampleMask(wx, wz);

            // 깎인 데는 맨바위 — **탈색되고 살짝 밝다.**
            t = Color.Lerp(t, Mul(Desaturate(t, 0.8f), Gray(1.05f)), Mathf.Clamp01(m.y) * _wear);

            // 쌓인 데는 흙 — **따뜻하고 살짝 어둡다.**
            t = Color.Lerp(t, Mul(t, new Color(1.08f, 0.99f, 0.88f) * 0.96f), Mathf.Clamp01(m.x) * _deposit);

            // 물길은 젖어서 **어둡고 푸르다.** 굽어 있었으나 아무도 안 읽던 채널이 여기서 쓰인다.
            t = Color.Lerp(t, Mul(t, new Color(0.84f, 0.93f, 0.95f) * 0.9f), Mathf.Clamp01(m.z) * _flow);
        }

        // ── ④ 표고 ──────────────────────────────────────────────────────────
        //
        // **잔기복을 뺀 높이를 쓴다**(`MapHeight`) — 텍셀이 16m인데 잔기복 주기가 80m라
        // 그냥 `At`을 쓰면 표본이 주기와 비슷해져 얼룩이 낀다(지도가 먼저 겪은 함정이다).
        if (_pale > 0.001f)
        {
            float h = TerrainHeight.MapHeight(wx, wz);
            float k = Mathf.InverseLerp(_paleFrom, Mathf.Max(_paleTo, _paleFrom + 1f), h);
            t = Color.Lerp(t, Mul(Desaturate(t, 0.55f), Gray(1.06f)), k * _pale);
        }

        return t;
    }

    // 바이옴 색에서 **색조만** — 평균을 1로 맞춘다. 밝기는 다른 축이 정한다.
    static Color Hue(Biome b)
    {
        var c = b.Color();
        float avg = Mathf.Max(0.001f, (c.r + c.g + c.b) / 3f);
        return new Color(c.r / avg, c.g / avg, c.b / avg, 1f);
    }

    static Color Gray(float v)  => new Color(v, v, v, 1f);
    static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, 1f);

    static Color Desaturate(Color c, float amount)
    {
        float g = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        return Color.Lerp(c, Gray(g), amount);
    }

    // ── 쓰기 ────────────────────────────────────────────────────────────────

    void Write(Color32[] px, int n, TerrainStreamer streamer)
    {
        Directory.CreateDirectory(Dir);

        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
        tex.SetPixels32(px);
        tex.Apply(false, false);
        File.WriteAllBytes(Path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

        var imp = AssetImporter.GetAtPath(Path) as TextureImporter;
        if (imp != null)
        {
            // **sRGB를 끈다** — 보여 줄 색이 아니라 **곱할 배율**이다. 켜 두면 임포터가 감마를
            // 벗겨서 우리가 적은 값과 셰이더가 읽는 값이 갈린다.
            imp.textureType       = TextureImporterType.Default;
            imp.sRGBTexture       = false;
            imp.wrapMode          = TextureWrapMode.Clamp;   // 세계 밖은 가장자리로 접는다
            imp.filterMode        = FilterMode.Bilinear;
            imp.mipmapEnabled     = true;
            imp.maxTextureSize    = n;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        var pal = streamer.Palette;
        if (pal == null)
        {
            Debug.LogWarning($"[색조] `{Path}`에 썼지만 스트리머에 팔레트가 없어 못 물렸다 — " +
                             "`9. Create Terrain Palette`을 먼저.");
            return;
        }

        pal.WorldColor = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
        EditorUtility.SetDirty(pal);
        AssetDatabase.SaveAssets();

        // **밀어야 화면이 따라온다** — 전역 유니폼이라 에셋만 고쳐서는 아무 일도 안 일어난다.
        pal.PushToShader();
        SceneView.RepaintAll();
    }
}
