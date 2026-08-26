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
        if (w < 8)
        {
            Debug.LogWarning("[바이옴] 지도가 " + w + "x" + h + "라 뿌릴 자리가 없다 — " +
                             "`7. Resize World Map`으로 키운 뒤에 돌릴 것.");
            return;
        }

        Undo.RecordObject(data, "바이옴 배치 생성");

        // 덩어리 하나가 대략 여덟 칸이 되게 — 64x64면 지역 여덟 개(2km)짜리 숲·사막이 생긴다.
        float period = Mathf.Max(4f, w / 8f);
        float seed   = (w * 131 + h * 17) % 997;

        var count = new int[8];
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
            count[(int)b % 8]++;
        }

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        TerrainGrid.ForgetRegions();
        TerrainHeight.ForgetBiomes();

        Debug.Log("[바이옴] " + w + "x" + h + " 배치를 깔았다 (Content는 그대로).\n" +
                  "평원 " + count[(int)Biome.Plains % 8] + " · 숲 " + count[(int)Biome.Forest % 8] +
                  " · 사막 " + count[(int)Biome.Desert % 8] + " · 설원 " + count[(int)Biome.Tundra % 8] +
                  " · 늪 " + count[(int)Biome.Swamp % 8] + " · 산악 " + count[(int)Biome.Cavern % 8] +
                  " · 용암 " + count[(int)Biome.Lava % 8] + "\n" +
                  "`World Map Editor`로 다듬고, `5. Place Sites From World Map`을 다시 돌릴 것.");
    }

    // 온도 x 습도 x 고도 -> 바이옴. **순서가 곧 규칙**이다 — 위에 있는 것이 이긴다.
    static Biome Pick(float t, float m, float e)
    {
        if (e > 0.80f && t > 0.72f) return Biome.Lava;    // 높고 뜨거운 곳 — 드물다
        if (e > 0.70f)              return Biome.Cavern;  // 산악
        if (t < 0.30f)              return Biome.Tundra;  // 추운 곳
        if (t > 0.68f && m < 0.38f) return Biome.Desert;  // 덥고 마른 곳
        if (m > 0.70f)              return t > 0.55f ? Biome.Swamp : Biome.Forest;
        if (m > 0.48f)              return Biome.Forest;
        return Biome.Plains;
    }

    // 0~1. 두 옥타브를 겹쳐 덩어리 가장자리가 밋밋하지 않게.
    static float N(int x, int y, float period, float seed)
    {
        float a = Mathf.PerlinNoise((x + seed) / period,        (y + seed) / period);
        float b = Mathf.PerlinNoise((x + seed) / (period * 0.4f), (y + seed) / (period * 0.4f));
        return Mathf.Clamp01(a * 0.75f + b * 0.25f);
    }
}
