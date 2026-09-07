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
    const string SeedKey = "DungeonColony.BiomeMapSeed";

    // 배치가 뽑는 것만 센다 — enum에 지상 값이 아홉 섞여 있어 전부 세면 *"안 나온 것"*이
    // 늘 아홉이다.
    static bool Underground(Biome b) => b is Biome.Limestone or Biome.Fungal or Biome.Flooded
                                          or Biome.Crystal or Biome.Mine or Biome.Abyss
                                          or Biome.Cavern or Biome.Lava;

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

        // ── 덩어리를 크게, 그리고 딱 떨어지게 (2026-09-06) ───────────────────
        //
        // 사용자: *"바이옴이 군데군데 흩어져있는게 아니라 그냥 어느 한쪽에 몰아서 크게
        // 있으면 좋겠어. 켄시도 보면 구역이 딱딱 정해져있잖아."*
        //
        // 주기가 `w/6`이면 16x16 지도에서 **2.7칸짜리 얼룩**이라 바이옴이 소금처럼 흩뿌려진다 —
        // 걸어가는 동안 성격이 계속 바뀌니 *"여기는 어디다"*가 성립하지 않는다. 지도를
        // 64x64로 저작하던 시절의 값이고, 16x16으로 줄인 뒤로 **눈금이 틀린 채로 남아 있었다.**
        //
        // **주기는 지도 크기에 비례한다** — 한 변에 덩어리가 두셋 들어가면 그것이 *구역*이다.
        float period = Mathf.Max(3f, w / 2.2f);
        // **다시 돌리면 다시 굴린다**(2026-09-06). 덩어리를 크게 잡으면 한 변에 구역이
        // 두셋뿐이라 **어떤 바이옴은 아예 안 나올 수 있다** — 그런데 씨앗이 지도 크기에서
        // 나오면 몇 번을 눌러도 **같은 지도**라 손쓸 방법이 없었다.
        //
        // 씨앗을 `EditorPrefs`에 들고 매번 올린다. **로그에 찍는다** — 마음에 든 지도를
        // 다시 얻으려면 그 숫자가 필요하다(안 찍으면 되돌릴 길이 없다).
        int  roll = EditorPrefs.GetInt(SeedKey, 0) + 1;
        EditorPrefs.SetInt(SeedKey, roll);
        float seed = (w * 131 + h * 17 + roll * 7919) % 9973;

        // **enum 전체 길이로 잡는다.** 8로 박아 두고 `% 8`로 접었더니, 지하 바이옴을 여섯
        // 더한 순간 **석회굴이 평원 칸에 세어지는** 꼴이 됐다(로그만 틀리는 것이라 더 나쁘다).
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
        }

        // ── 조각을 걷어낸다 ─────────────────────────────────────────────────
        //
        // 노이즈를 아무리 키워도 **문턱 근처에서는 한 칸짜리 조각**이 남는다(세 축이 서로
        // 다른 곳에서 문턱을 넘기 때문이다). 그 한 칸이 화면에서는 *"굴을 걷다 보니 벽 색이
        // 한 번 튀었다"*로 보이고, **구역이 딱 떨어진다는 느낌을 그것 하나가 깬다.**
        //
        // **다수결로 두 번 훑는다** — 제 칸을 둘로 세고 이웃 여덟을 하나씩 세어, 이긴 것으로
        // 바꾼다. 제 칸에 무게를 주는 이유는 **안 주면 두 바이옴의 경계가 매 번 뒤집혀**
        // 덩어리가 녹아 버리기 때문이다.
        //
        // **읽는 것과 쓰는 것을 가른다** — 제자리에서 고치면 먼저 고친 칸이 뒤엣것의 이웃으로
        // 세어져 한쪽 방향으로 번진다(세포 자동자에서 늘 겪는 자리).
        Despeckle(data, w, h, passes: 2);

        // 개수는 **다듬은 뒤에** 센다 — 다듬기 전 숫자를 찍으면 로그와 화면이 갈린다.
        var count = new int[System.Enum.GetValues(typeof(Biome)).Length];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            count[(int)data.Get(x, y).Biome]++;

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

        // **안 나온 것을 같이 말한다** — 덩어리를 키운 대가가 그것이고, 안 알리면
        // *"수정동이 왜 없지"*를 지도에서 눈으로 찾게 된다. 다시 누르면 다시 굴린다.
        var gone = new System.Text.StringBuilder();
        foreach (Biome b in System.Enum.GetValues(typeof(Biome)))
        {
            if (b == Biome.Unknown || count[(int)b] > 0 || !Underground(b)) continue;
            if (gone.Length > 0) gone.Append(", ");
            gone.Append(b.DisplayName());
        }

        Debug.Log("[바이옴] " + w + "x" + h + " 지하 배치를 깔았다 (Content는 그대로) · 씨앗 " + roll + ".\n" +
                  sb + "\n" +
                  (gone.Length > 0
                      ? "**안 나온 것**: " + gone + " — 다시 누르면 다시 굴린다.\n"
                      : "") +
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

    // 이웃 다수결로 한 칸짜리 조각을 지운다. **`Content`는 안 건드린다** — 마을·소굴은
    // 저작물이라 바이옴이 바뀌어도 그 자리에 그대로 선다.
    static void Despeckle(WorldMapData data, int w, int h, int passes)
    {
        var src = new Biome[w * h];
        var dst = new Biome[w * h];
        for (int i = 0; i < src.Length; i++) src[i] = data.Get(i % w, i / w).Biome;

        var tally = new int[System.Enum.GetValues(typeof(Biome)).Length];

        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                System.Array.Clear(tally, 0, tally.Length);
                tally[(int)src[y * w + x]] += 2;   // 제 칸에 무게 — 안 주면 경계가 녹는다

                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    tally[(int)src[ny * w + nx]]++;
                }

                int best = (int)src[y * w + x];
                for (int i = 0; i < tally.Length; i++)
                    if (tally[i] > tally[best]) best = i;

                dst[y * w + x] = (Biome)best;
            }

            (src, dst) = (dst, src);
        }

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var n = data.Get(x, y);
            n.Biome = src[y * w + x];
            data.Set(x, y, n);
        }
    }

    // 0~1. 두 옥타브를 겹쳐 덩어리 가장자리가 밋밋하지 않게.
    //
    // **잔 옥타브를 눌렀다**(2026-09-06). 0.25는 가장자리를 흔드는 정도가 아니라 **덩어리를
    // 부수는 값**이었다 — 큰 주기로 덩어리를 키워도 잔 옥타브가 그 안에 다른 바이옴 조각을
    // 찍어 넣었다. 테두리만 울퉁불퉁하면 되므로 0.12면 넉넉하다.
    static float N(int x, int y, float period, float seed)
    {
        float a = Mathf.PerlinNoise((x + seed) / period,          (y + seed) / period);
        float b = Mathf.PerlinNoise((x + seed) / (period * 0.55f), (y + seed) / (period * 0.55f));
        return Mathf.Clamp01(a * 0.88f + b * 0.12f);
    }
}
