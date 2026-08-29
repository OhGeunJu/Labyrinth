using UnityEditor;
using UnityEngine;

// **바이옴 배치의 첫 벌을 깐다**(2026-08-26).
//
// 지도를 64x64로 키워도 **그린 것이 큰 덩어리 여섯이면 그림은 그대로**다 — 칸이 잘게 갈릴 뿐
// 같은 색이 갈린다. 그렇다고 4,096칸을 손으로 칠할 수도 없다.
//
// 그래서 **씨를 뿌려 준다** — 이 저장소가 기술 트리·연구 자리에 쓴 방식 그대로다
// (`Create Tech Tree`가 마흔넷을 깔고 사람이 다듬는다). 저작을 대신하는 것이 아니라
// **저작을 시작할 자리**를 만든다.
//
// ── 왜 노이즈 둘인가 ────────────────────────────────────────────────────────
//
// 바이옴 하나를 노이즈 하나로 뽑으면 **띠무늬**가 된다(값이 낮은 데부터 높은 데까지 순서대로
// 늘어선다). **온도와 습도 두 축**으로 뽑으면 위트커 도표처럼 *"덥고 마른 곳=사막, 춥고
// 습한 곳=숲"*이 되어 **덩어리가 자연스럽게 얽힌다.**
//
// **`Content`는 안 건드린다** — 마을·소굴은 저작한 것이고, 바이옴만 새로 까는 도구다.
public static class BiomeMapGenerator
{
    [MenuItem("DungeonColony/Terrain/8. Generate Biome Map")]
    static void Generate()
    {
        var view = Object.FindFirstObjectByType<WorldMap>(FindObjectsInactive.Include);
        var so   = view != null ? new SerializedObject(view) : null;
        var data = so?.FindProperty("mapData")?.objectReferenceValue as WorldMapData;

        if (data == null) { Debug.LogWarning("[바이옴] 씬의 WorldMap에 mapData가 안 물려 있다."); return; }
        if (!data.IsSized) { Debug.LogWarning("[바이옴] mapData의 배열이 Width x Height와 어긋난다."); return; }

        int w = data.Width, h = data.Height;
        if (w < 4)
        {
            Debug.LogWarning("[바이옴] 지도가 " + w + "x" + h + "라 뿌릴 자리가 없다 — " +
                             "`7. Resize World Map`으로 키운 뒤에 돌릴 것.");
            return;
        }

        Undo.RecordObject(data, "바이옴 배치 생성");

        // 덩어리 하나가 대략 여덟 칸이 되게 — 64x64면 지역 여덟 개(2km)짜리 숲·사막이 생긴다.
        // **작은 지도에서도 덩어리가 갈리게.** 4로 박아 두면 4x4 지도에서 온 세계가
        // 한 덩어리가 되어 바이옴이 하나만 깔린다.
        float period = Mathf.Max(1.5f, w / 6f);
        float seed   = (w * 131 + h * 17) % 997;

        // **enum 전체 길이로 잡는다.** 8로 박아 두고 `% 8`로 접었더니, 지하 바이옴을 여섯
        // 더한 순간 **석회굴이 평원 칸에 세어지는** 꼴이 됐다(로그만 틀리는 것이라 더 나쁘다).
        var count = new int[System.Enum.GetValues(typeof(Biome)).Length];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float t = N(x, y, period,        seed);          // 온도
            float m = N(x, y, period * 0.8f, seed + 311f);   // 습도
            float e = N(x, y, period * 1.6f, seed + 733f);   // 고도

            var b = Pick(t, m, e);
            var n = data.Get(x, y);
            n.Biome = b;              // **`Content`는 그대로 둔다** — 마을·소굴은 저작물이다
            data.Set(x, y, n);
            count[(int)b]++;
        }

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        TerrainGrid.ForgetRegions();
        TerrainHeight.ForgetBiomes();

        // **나온 것만 적는다** — 안 나온 바이옴까지 0으로 늘어놓으면 정작 무엇이 얼마나
        // 깔렸는지가 안 읽힌다(지상 값이 아홉 개 섞여 있다).
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < count.Length; i++)
        {
            if (count[i] == 0) continue;
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(((Biome)i).DisplayName()).Append(' ').Append(count[i]);
        }

        Debug.Log("[바이옴] " + w + "x" + h + " 지하 배치를 깔았다 (Content는 그대로).\n" +
                  sb + "\n" +
                  "`World Map Editor`로 다듬고, `5. Place Sites From World Map`을 다시 돌릴 것.");
    }

    // 열 x 물 x 깊이 -> 바이옴. **순서가 곧 규칙**이다 — 위에 있는 것이 이긴다.
    //
    // ── 지하로 갈아엎었다 (2026-08-29) ──────────────────────────────────────
    //
    // 축 셋은 그대로 쓰되 뜻이 바뀐다. 지하에는 해가 없으므로 **온도는 위에서 오는 것이
    // 아니라 아래에서 온다** — 깊을수록 뜨겁고, 그래서 용암층과 심연이 같은 구석에 몰린다.
    // 습도는 **물길**이고(수몰층·균사림), 고도는 **깊이**다.
    //
    // **지상 바이옴은 하나도 안 뽑는다.** 값은 enum에 남아 있지만(지우면 저작 에셋이 밀린다)
    // 배치가 안 하므로 세계에 안 나타난다.
    static Biome Pick(float t, float m, float e)
    {
        // 깊고 뜨거운 구석 — 드물다. 이 둘이 세계에서 가장 험한 곳이다.
        if (e > 0.82f && t > 0.70f) return Biome.Lava;
        if (e > 0.78f)              return Biome.Abyss;

        // 물이 고인 곳. **깊이보다 물이 먼저다** — 얕은 데서도 물은 고인다.
        if (m > 0.74f)              return Biome.Flooded;

        // 물기가 있고 안 뜨거우면 균사가 자란다 — **지하의 숲이고 농사·목재가 여기서 나온다.**
        if (m > 0.55f && t < 0.62f) return Biome.Fungal;

        // 차고 마른 곳에 결정이 자란다.
        if (t < 0.32f && m < 0.45f) return Biome.Crystal;

        // 사람이 팠던 자리 — 얕은 데 몰린다(깊이 파고들 만큼 오래 못 버텼다).
        if (e < 0.30f && m < 0.50f) return Biome.Mine;

        // 물이 깎은 넓고 완만한 방 — **시작 지역**이다.
        if (m > 0.40f)              return Biome.Limestone;

        return Biome.Cavern;   // 그 밖의 평범한 굴
    }

    // 0~1. 두 옥타브를 겹쳐 덩어리 가장자리가 밋밋하지 않게.
    static float N(int x, int y, float period, float seed)
    {
        float a = Mathf.PerlinNoise((x + seed) / period,        (y + seed) / period);
        float b = Mathf.PerlinNoise((x + seed) / (period * 0.4f), (y + seed) / (period * 0.4f));
        return Mathf.Clamp01(a * 0.75f + b * 0.25f);
    }
}
