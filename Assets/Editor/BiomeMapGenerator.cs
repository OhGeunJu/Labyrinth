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
    // **지금 깔리는 것만 센다** — 잠재운 넷(석회굴·소금굴·유황굴·균열대)은 여기 없다.
    // 넣어 두면 *"안 나온 것"* 경고가 **정상 상태에서 매번 뜨고**, 그런 경고는 곧 아무도
    // 안 읽는다.
    static bool Underground(Biome b) => b is Biome.Fungal or Biome.Mossgrotto
                                          or Biome.Cavern or Biome.Mine
                                          or Biome.Swamp or Biome.Ossuary or Biome.Desert
                                          or Biome.Jungle or Biome.Blight
                                          or Biome.Glacier or Biome.Crystal or Biome.Lava
                                          or Biome.Irradiated
                                          or Biome.Abyss;

    // **층 넷을 다 깐다**(2026-10-01, `Floors`) — 층 지도마다 한 번씩. 그린 층(1층)은 그림 그대로, 칠한 층은 안
    // 건드리고, 나머지는 그 층의 티어 바이옴으로 굴린다. 층 지도가 없으면 `World → 0d. Create Floor Maps`를 먼저.
    [MenuItem("DungeonColony/Terrain/8. Generate Biome Map")]
    static void Generate()
    {
        bool any = false;
        for (int f = 1; f <= Floors.Count; f++)
        {
            var d = WorldMap.FloorData(f);
            if (d == null) { Debug.LogWarning($"[바이옴] {f}층 지도가 없다 — `World → 0d. Create Floor Maps`를 돌릴 것."); continue; }
            GenerateFloor(d);
            any = true;
        }
        if (!any) Debug.LogWarning("[바이옴] 씬의 WorldMap에 층 지도가 하나도 안 물려 있다.");
    }

    static void GenerateFloor(WorldMapData data)
    {
        if (data == null) { Debug.LogWarning("[바이옴] 씬의 WorldMap에 mapData가 안 물려 있다."); return; }
        if (!data.IsSized) { Debug.LogWarning("[바이옴] mapData의 배열이 Width x Height와 어긋난다."); return; }

        // **손으로 칠한 지도는 안 건드린다**(2026-09-15). 이 도구는 *저작을 시작할 자리*를
        // 만드는 것이지 저작을 대신하는 것이 아니다 — 그런데 `1. Rebuild Underworld`가
        // 이걸 단계로 부르므로, 막지 않으면 **묶음 한 번에 칠한 것이 통째로 날아간다.**
        // (`WorldObjectTag.Generated`가 손으로 놓은 장소를 지키는 그 규칙이다.)
        if (data.HandPainted)
        {
            Debug.LogWarning($"[바이옴] `{data.name}`은 **손으로 칠한 지도**라 안 건드렸다.\n" +
                             "다시 굴리려면 `DungeonColony → World Map Editor`에서 " +
                             "**[손으로 칠함]을 끄고** 다시 누를 것.");
            return;
        }

        int w = data.Width, h = data.Height;
        if (w < 4)
        {
            Debug.LogWarning("[바이옴] 지도가 " + w + "x" + h + "라 뿌릴 자리가 없다 — " +
                             "`7. Resize World Map`으로 키운 뒤에 돌릴 것.");
            return;
        }

        // **이 층에 깔 것이 있나.** 비어 있으면 `BiomeContentData.Tier`에 그 층이 없는
        // 것이고, 조용히 동굴로 덮으면 *"몇 층을 깔아도 같은 지도"*가 된다.
        var floorSet = new System.Collections.Generic.List<Biome>(FloorBiomes(data.Floor));
        if (floorSet.Count == 0)
        {
            Debug.LogError($"[바이옴] **{data.Floor}층에 해당하는 바이옴이 없다** — " +
                           "`Resources/Data/Biomes/`의 `Tier`를 볼 것. 여기서 멈춘다.");
            return;
        }

        Undo.RecordObject(data, "바이옴 배치 생성");

        // **노이즈가 하는 일은 이제 경계를 구불리는 것뿐이다**(2026-09-15에 구역으로
        // 옮겼다 — 아래 `SeedPoints`). 주기를 지도의 절반쯤으로 두면 경계가 **한 번에 서너
        // 칸씩** 물결친다. 더 잘게 두면 경계가 **지글거리고**, 더 크게 두면 **자로 잰 듯**
        // 곧아져 구역이 파이 조각으로 보인다.
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

        // **씨는 굴림이 정해진 뒤에 놓는다** — 돌림각을 굴림에서 내기 때문이다(안 그러면
        // 몇 번을 다시 눌러도 늘 같은 방향이다).
        // **그린 층이면 그대로 찍는다** — 굴릴 것이 없다.
        var drawn = AuthoredFloor(data.Floor);
        if (drawn != null && (drawn.Length != h || drawn[0].Length != w))
        {
            Debug.LogError($"[바이옴] {data.Floor}층 그림이 {drawn[0].Length}x{drawn.Length}인데 " +
                           $"지도는 {w}x{h}다 — 크기를 맞추거나 그림을 지울 것. 여기서 멈춘다.");
            return;
        }

        var seeds = SeedPoints(floorSet, w, h, roll);

        // **enum 전체 길이로 잡는다.** 8로 박아 두고 `% 8`로 접었더니, 지하 바이옴을 여섯
        // 더한 순간 **석회굴이 평원 칸에 세어지는** 꼴이 됐다(로그만 틀리는 것이라 더 나쁘다).
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Biome b;
            if (drawn != null)
            {
                // **첫 줄이 북쪽이다** — 격자의 y는 위로 자라므로 뒤집어 읽는다.
                char ch = drawn[h - 1 - y][x];
                if (!Legend.TryGetValue(ch, out b))
                {
                    Debug.LogError($"[바이옴] {data.Floor}층 그림의 '{ch}'를 모른다 " +
                                   $"({x},{y}) — `Legend`에 없다. 여기서 멈춘다.");
                    return;
                }
            }
            else
            {
                // **칸을 흔들어서 묻는다**(도메인 워프) — 어느 구역인지는 씨가 정하고,
                // 노이즈는 *어디서 묻느냐*만 옮긴다. 근거는 `NearestSeed` 위에 적었다.
                float wx = (N(x, y, period * 0.55f, seed + 911f) - 0.5f) * Warp(w);
                float wy = (N(x, y, period * 0.55f, seed + 337f) - 0.5f) * Warp(w);

                b = floorSet[NearestSeed(seeds, x + 0.5f + wx, y + 0.5f + wy)];
            }
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
        // **그린 층은 안 다듬는다** — 한 칸짜리 조각도 **일부러 그린 것**일 수 있다.
        // 굴린 층에서만 노이즈가 남긴 부스러기를 걷어낸다.
        if (drawn == null) Despeckle(data, w, h, passes: 2);

        // 개수는 **다듬은 뒤에** 센다 — 다듬기 전 숫자를 찍으면 로그와 화면이 갈린다.
        var count = new int[System.Enum.GetValues(typeof(Biome)).Length];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            count[(int)data.Get(x, y).Biome]++;

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        TerrainGrid.ForgetRegions();
        TerrainHeight.ForgetBiomes();

        // **어느 쪽에 앉았는지를 같이 적는다**(2026-09-15).
        //
        // 구역으로 바꾼 이유가 *"동쪽에 뭐, 남쪽에 뭐"*인데, 개수만 찍으면 **그 답이
        // 로그에 없다** — 지도를 눈으로 열어 봐야 안다. 칸의 무게중심에서 방위를 낸다.
        //
        // **방위 이름은 `EventManager.Compass8`이 답한다** — 습격 알림이 쓰는 그 표다.
        // 여기서 다시 적으면 같은 방향을 한쪽은 `북동`, 다른 쪽은 `북동쪽`이라 부른다.
        var sumX = new float[count.Length];
        var sumY = new float[count.Length];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = (int)data.Get(x, y).Biome;
            sumX[i] += x + 0.5f;
            sumY[i] += y + 0.5f;
        }

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < count.Length; i++)
        {
            if (count[i] == 0) continue;

            float cx = sumX[i] / count[i] - w * 0.5f;
            float cy = sumY[i] / count[i] - h * 0.5f;

            // **한복판은 방위가 아니다** — 지도 한 변의 8% 안이면 *가운데*라고 적는다
            // (0.3칸 치우친 것을 `북`이라 부르면 로그가 거짓말을 한다).
            string dir = (cx * cx + cy * cy) < (w * 0.08f) * (w * 0.08f)
                       ? "가운데"
                       : EventManager.Compass8(cx, cy) ?? "가운데";

            if (sb.Length > 0) sb.Append(" · ");
            sb.Append($"{dir} {((Biome)i).DisplayName()} {count[i]}칸");
        }

        // **안 나온 것을 같이 말한다** — 다만 **이 층의 것만** 센다(2026-09-15).
        //
        // 전에는 지하 여덟을 전부 훑어 *"안 나온 것: 용암층, 심연…"*이라고 적었다. 한 판에
        // 여덟을 다 깔던 시절에는 맞는 말이었지만, **층이 후보를 정하는 지금은 다른 층의
        // 바이옴이 안 나오는 것이 정상**이다 — 그대로 두면 **정상 상태가 매번 경고로 뜬다**
        // (그리고 그런 경고는 곧 아무도 안 읽는다).
        var gone = new System.Text.StringBuilder();
        foreach (Biome b in System.Enum.GetValues(typeof(Biome)))
        {
            if (b == Biome.Unknown || count[(int)b] > 0 || !Underground(b)) continue;
            if (BiomeTier.Of(b) != data.Floor) continue;   // 다른 층 — 안 나오는 것이 맞다
            if (gone.Length > 0) gone.Append(", ");
            gone.Append(b.DisplayName());
        }

        var cand = new System.Text.StringBuilder();
        foreach (var b in floorSet)
        {
            if (cand.Length > 0) cand.Append(" · ");
            cand.Append(b.DisplayName());
        }

        Debug.Log("[바이옴] " + w + "x" + h + " **" + data.Floor + "층** 배치를 깔았다 — " +
                  (drawn != null ? "**그린 그림 그대로**(`BiomeMapGenerator.Floor1`)"
                                 : "굴렸다 · 씨앗 " + roll) + "\n" +
                  "(후보: " + cand + " — `BiomeContentData.Tier`가 정한다).\n" +
                  sb + "\n" +
                  (gone.Length > 0
                      ? "**안 나온 것**: " + gone + " — 다시 누르면 다시 굴린다.\n"
                      : "") +
                  "`World Map Editor`로 다듬고, `5. Place Sites From World Map`을 다시 돌릴 것.");
    }

    // ── 손으로 그린 층은 그대로 찍는다 (2026-09-15) ────────────────────────
    //
    // 사용자가 1층 배치를 **그려서 보냈다.** 그러면 그것이 저작이고, **데이터로 옮기는
    // 것은 도구의 일이다** — *"칠하세요"*라고 되돌려 주는 것은 그린 것을 두 번 그리라는
    // 말이다(사용자: *"왜 나한테 굳이 칠하라고 하는거야?"*).
    //
    // **그림을 그림으로 적는다.** 열여섯 줄 × 열여섯 글자라 **코드에서 지도가 그대로
    // 보이고**, 고칠 때도 글자 하나를 바꾸면 된다. 숫자 배열이나 다각형 식으로 적으면
    // *무엇을 그렸는지*를 읽는 사람이 머릿속에서 다시 그려야 한다.
    //
    // **첫 줄이 북쪽이다**(지도를 위에서 본 그대로). 안 맞추면 남북이 뒤집히는데
    // 화면에서는 *"비슷한데 뭔가 다르다"*로만 보인다.
    //
    // **없는 층은 `null`** — 그 층은 예전처럼 굴린다(`SeedPoints`). 그려 주면 여기 붙인다.
    //
    // **격자는 64m라 여기서는 계단이지만 게임에서는 굽는다** — `TerrainGrid.BiomeSample`이
    // 읽는 자리를 굽히기 때문이다(근거는 거기). **칠하는 해상도와 경계 해상도는 다른 축이다.**
    static readonly System.Collections.Generic.Dictionary<char, Biome> Legend = new()
    {
        ['F'] = Biome.Fungal,      // 균사림
        ['M'] = Biome.Mossgrotto,  // 이끼굴
        ['C'] = Biome.Cavern,      // 동굴
        ['P'] = Biome.Mine,        // 폐광
        ['S'] = Biome.Swamp,       // 지저늪
        ['O'] = Biome.Ossuary,     // 납골굴
        ['D'] = Biome.Desert,      // 모래굴
        ['G'] = Biome.Glacier,     // 빙하굴
        ['Y'] = Biome.Crystal,     // 수정동
        ['L'] = Biome.Lava,        // 용암층
        ['A'] = Biome.Abyss,       // 심연
        ['J'] = Biome.Jungle,      // 지저밀림
        ['B'] = Biome.Blight,      // 기생림
        ['R'] = Biome.Irradiated,  // 방사굴
    };

    // 1층 — 사용자가 그린 그림. 북서 균사림 · 북동 이끼굴 · 남서 동굴 · **한복판과 남동 폐광**.
    //
    // **시작 칸이 폐광이어야 한다**(2026-09-15, 사용자: *"시작칸쪽을 폐광으로 폐광을 더
    // 크게 그려"*). 시작 지점은 지도 한가운데(`WorldMapData.StartNode` = 8,8)인데,
    // 균사림·이끼굴은 **벽이 돌뿐**이라 거기서 시작하면 **구리 한 톨 없이** 판이 열린다.
    // 폐광은 1층에서 석탄이 나는 유일한 곳이고 구리·주석도 두터우니, **거점을 세우는
    // 자리로 맞는 곳**이다.
    //
    // 폐광 96칸 · 균사림 56 · 동굴 53 · 이끼굴 51.
    static readonly string[] Floor1 =
    {
        "FFFFFMMMMMMMMMMM",
        "FFFFFFMMMMMMMMMM",
        "FFFFFFFMMMMMMMMM",
        "FFFFFFFMMMMMMMMM",
        "FFFFFFFFMMMMMMMM",
        "FFFFFFFFMMMMPPPP",
        "FFFFFFFPPPPPPPPP",
        "FFFFFPPPPPPPPPPP",   // <- 여기 한복판(8,8)이 시작 칸이다
        "FFFCPPPPPPPPPPPP",
        "CCCCCPPPPPPPPPPP",
        "CCCCCCPPPPPPPPPP",
        "CCCCCCCPPPPPPPPP",
        "CCCCCCCCPPPPPPPP",
        "CCCCCCCCPPPPPPPP",
        "CCCCCCCCCPPPPPPP",
        "CCCCCCCCCPPPPPPP",
    };

    static string[] AuthoredFloor(int floor) => floor switch
    {
        1 => Floor1,
        _ => null,     // 아직 안 그렸다 — 굴린다
    };

    // **순서는 둘레를 도는 차례다**(2026-09-15). `i`번째 바이옴이 `i`번째 씨를 받고,
    // 씨는 `360/n`으로 고르게 놓인다(`SeedPoints`) — 그래서 **이웃하는 두 줄은 지도에서도
    // 이웃한다.** 젖은 순서로 적어 두면 *물가 옆이 물가*가 되어 경계가 덜 튄다.
    //
    // **어느 방위에 앉을지는 굴림이 정한다** — 돌림각이 굴림에서 나오므로 다시 누를 때마다
    // 돈다. *"동쪽이 꼭 균사림이어야 한다"*가 되면 그건 저작이고, 그때는 이 표가 아니라
    // 지도를 손으로 칠하는 것이 맞다(`World Map Editor`).
    //
    // **한 층 안의 순서만 뜻이 있다** — 층을 넘는 자리는 아무것도 안 정한다(티어가 이미 가른다).
    static readonly Biome[] ByWetness =
    {
        Biome.Fungal,     // 1층 — 균사가 자란다, 지하의 숲. **1층의 한복판**
        Biome.Swamp,      // 2층 — 고인 물과 진흙. **2층의 한복판**
        Biome.Jungle,     // 2층 — 덥고 젖었다. 늪 곁이다
        Biome.Mossgrotto, // 1층 — 물이 스며 이끼가 덮였다
        Biome.Glacier,    // 3층 — 얼어붙은 물. **3층의 한복판**
        Biome.Cavern,     // 1층 — 그 밖의 평범한 굴
        Biome.Blight,     // 2층 — 시체에서 버섯이 자란다. **밀림과 납골굴 사이**(둘레에서 이웃이 된다)
        Biome.Ossuary,    // 2층 — 마른 무덤, 물길에서 비켜 있다
        Biome.Crystal,    // 3층 — 차고 마른 곳
        Biome.Mine,       // 1층 — 사람이 팠던 자리
        Biome.Desert,     // 2층 — 물이 말랐다
        Biome.Irradiated, // 3층 — 마르고 병들었다
        Biome.Lava,       // 3층 — 마르고 뜨겁다
        Biome.Abyss,      // 4층 — 가장 깊은 곳. 지도 전체가 여기다
    };

    // 이 층의 바이옴을 **젖은 순서로** 모은다. **후보는 티어가 답한다** — 여기에 층별
    // 목록을 들면 표가 두 벌이 되어, 티어를 옮기는 날 지도가 안 따라온다.
    static readonly System.Collections.Generic.List<Biome> _floorSet = new();

    static System.Collections.Generic.List<Biome> FloorBiomes(int floor)
    {
        _floorSet.Clear();
        foreach (var b in ByWetness)
            if (BiomeTier.Of(b) == floor) _floorSet.Add(b);
        return _floorSet;
    }

    // ── 구역은 씨가 정한다 — 노이즈가 아니다 (2026-09-15) ────────────────
    //
    // 사용자: *"지금 군데군데 말고 동쪽에 뭐 남쪽에 뭐 이렇게 크게 지형을 갈랐으면
    // 좋겠어."* **노이즈로는 안 되는 주문이다.**
    //
    // 2026-09-06에 같은 말을 듣고(*"어느 한쪽에 몰아서 크게"*) **주기를 키웠다.** 덩어리는
    // 커졌지만 *군데군데*는 그대로였다 — **노이즈의 성질이 그것이기 때문이다**: 문턱을
    // 넘는 자리가 지도 여기저기에 생기므로 **같은 바이옴이 세 군데 네 군데에 난다.**
    // 주기를 키우면 덩어리가 **커질 뿐 개수는 안 준다.** 손잡이를 아무리 돌려도 못 간다.
    // **고칠 것이 값이 아니라 방식이었다.**
    //
    // **바이옴마다 씨를 하나 놓고 가장 가까운 씨에 붙인다**(보로노이). 그러면
    // **한 바이옴 = 한 덩어리**가 구조적으로 보장된다 — 문턱이 아니라 *소속*이기 때문이다.
    // 켄시의 구역이 이 모양이고, 사용자가 든 예가 그것이다.
    //
    // ## 씨는 둘레에 고르게, 반지름만 굴린다
    //
    // 각도는 `360/n`으로 **고르게** 나눈다 — 그래야 구역 크기가 비슷하다. 흔드는 것은
    // **반지름**뿐이고, 그 하나가 두 가지를 한다:
    //
    //   ① **가장 안쪽 씨가 한복판을 먹는다** — 시작 지점(`WorldMapData.StartNode`, 지도
    //      한가운데)이 그 구역 안에 들어간다. 반지름을 안 흔들면 한복판이 **n갈래로 갈리는
    //      파이의 꼭짓점**이 되고, 거점을 세우는 자리가 바이옴 서넛의 경계에 떨어진다.
    //   ② 구역 크기가 **똑같지 않게** 된다 — 자로 잰 듯 같으면 사람이 나눈 것으로 보인다.
    //
    // **한복판에 씨를 따로 두는 길은 안 된다**(2026-09-15에 두 번 해 봤다). 거리에 배율을
    // 주면 한복판이 **지도를 통째로 삼키고**(가중 보로노이는 경계가 안 닫힌다), 거리에서
    // 값을 빼면 덜하지만 여전히 절반을 넘는다(151/256). **각도를 고르게 두고 반지름만
    // 흔드는 쪽이 셈도 단순하고 결과도 고르다**(78·56·70·52 — 균등이 64다).
    //
    // ## 어기면 조용히 깨지는 것
    //
    // - **경계를 흔들지 않으면 자로 잰 부채꼴**이 된다(구역이 아니라 파이 조각으로 보인다).
    //   **거리를 흔들지 좌표를 흔들지 않는다** — 좌표를 흔들면 구역이 갈라져 *군데군데*가
    //   되돌아온다(고치려던 바로 그것이다).
    // - **흔드는 폭이 크면 섬이 생긴다.** ±15%면 경계가 구불거리고 덩어리는 안 쪼개진다.
    // - **돌림각도 반지름도 굴림에서 낸다** — 안 그러면 몇 번을 다시 눌러도 **늘 같은
    //   지도**다(덩어리를 키우던 시절 씨앗을 지도 크기에서 내서 겪은 그 자리).
    // - **한 층에 하나면 씨도 하나다**(4층 심연) — 지도 전체가 그 바이옴이다.
    // ── 방위를 못 박으면 굴림이 안 건드린다 (2026-09-15) ──────────────────
    //
    // 사용자가 1층 배치를 **그려 줬다**(균사림 북서 · 이끼굴 북동 · 폐광 남동 · 동굴 남서).
    // 그 순간 방위는 굴림의 것이 아니라 **저작**이 된다 — 다시 누를 때마다 도는 것은
    // *아직 안 정한 것*에나 맞는 규칙이다.
    //
    // **`BiomeContentData.MapDirection`이 답한다**(`MapDir.Auto`면 예전처럼 굴린다).
    // 한 층의 일부만 못 박아도 된다 — 못 박은 것은 제자리에 서고, 나머지가 **남은 자리를
    // 나눠 갖는다**(`Auto`끼리만 둘레를 고르게 돈다).
    //
    // **방위가 정해진 것도 반지름은 조금씩 다르게** 둔다. 넷을 같은 반지름에 놓으면
    // 경계 셋이 **지도 한가운데 한 점에서 만나고**, 시작 지점이 거기다 —
    // *여기가 어디인지*가 성립하지 않는다. 바이옴 이름에서 낸 값이라 **굴려도 안 변한다**
    // (저작한 것이 굴림에 흔들리면 그린 그림이 아니다).
    static Vector2[] SeedPoints(System.Collections.Generic.List<Biome> set, int w, int h, int roll)
    {
        int n = set.Count;
        var seeds = new Vector2[n];
        var mid   = new Vector2(w * 0.5f, h * 0.5f);

        if (n == 1) { seeds[0] = mid; return seeds; }

        float span = Mathf.Min(w, h);

        // `Auto`가 몇인지 먼저 세야 **남은 자리를 고르게** 나눈다.
        int autos = 0;
        foreach (var b in set) if (DirOf(b) == MapDir.Auto) autos++;

        float rot  = (roll * 47f % 360f) * Mathf.Deg2Rad;
        int   slot = 0;

        for (int i = 0; i < n; i++)
        {
            var dir = DirOf(set[i]);

            // **못 박은 반지름은 이름에서 낸다** — 굴려도 안 변한다.
            float t = dir == MapDir.Auto
                    ? Frac(i * 0.618f + roll * 0.37f)
                    : Frac(NameHash(set[i]) * 0.618f);

            // **폭이 넓어야 사분면이 똑같이 안 잘린다**(0.18~0.44 → 0.16~0.46).
            // 좁으면 네 귀퉁이가 대칭이라 경계가 정확히 가로세로 반이 된다.
            float r = span * (0.16f + 0.30f * t);

            if (dir == MapDir.Center) { seeds[i] = mid; continue; }

            float a;
            if (dir == MapDir.Auto)
            {
                // **황금비로 흩는다** — `i`에 그냥 비례시키면 반지름이 각도를 따라 **한
                // 방향으로 커져** 구역이 나선처럼 쏠린다. 무리수 간격이면 고루 퍼진다.
                a = rot + Mathf.PI * 2f * slot / Mathf.Max(1, autos);
                slot++;
            }
            else a = AngleOf(dir);

            seeds[i] = mid + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
        }
        return seeds;
    }

    static MapDir DirOf(Biome b)
    {
        var d = BiomeContent.Get(b);
        return d != null ? d.MapDirection : MapDir.Auto;
    }

    // **`+Z`가 북, 시계 방향** — `EventManager.Compass8`이 읽는 각도와 같은 규약이다.
    // 갈리면 로그가 *"북"*이라 적은 구역이 지도에서는 남쪽에 있게 된다.
    static float AngleOf(MapDir d) => d switch
    {
        MapDir.N  => 0f,
        MapDir.NE => 45f,
        MapDir.E  => 90f,
        MapDir.SE => 135f,
        MapDir.S  => 180f,
        MapDir.SW => 225f,
        MapDir.W  => 270f,
        MapDir.NW => 315f,
        _         => 0f,
    } * Mathf.Deg2Rad;

    // **`string.GetHashCode`를 안 쓴다** — 실행마다 달라져서 판을 다시 열 때마다 지도가
    // 미묘하게 움직인다(`PawnSpecData`가 먼저 겪은 함정).
    static float NameHash(Biome b)
    {
        unchecked
        {
            int h = 17;
            string s = b.ToString();
            for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
            return (Mathf.Abs(h) % 997) / 997f;
        }
    }

    static float Frac(float v) => v - Mathf.Floor(v);

    // ── 경계를 굽히는 법 — 거리가 아니라 **묻는 자리**를 흔든다 (2026-09-15) ──
    //
    // 처음엔 **모든 씨의 거리에 같은 배율**을 곱했다(`d * wob`). 주석에는 *"칸 하나에 한
    // 값을 써야 경계가 한 줄로 물결친다"*라고 적어 두었는데, **그 곱셈은 아무 일도 안
    // 한다** — 전부에 같은 수를 곱하면 *가장 가까운 씨*가 안 바뀐다(`argmin`은 양의 상수
    // 배에 불변이다). 화면에는 **자로 잰 십자**로 나타났고, 값을 아무리 키워도 그대로였다
    // (사용자: *"이거랑 이게 너무 다르지않아?"* — 그린 그림은 경계가 굽어 있었다).
    //
    // **비교를 바꾸는 흔들기여야 한다.** 그래서 거리가 아니라 **묻는 자리**를 옮긴다
    // (도메인 워프): 칸의 좌표에 저주파 노이즈를 더해 *그 자리에서* 가장 가까운 씨를
    // 묻는다. 평면을 통째로 구부리는 것이라 **구역은 이어진 채로** 경계만 물결친다.
    //
    // **폭은 세계에 비례한다**(한 변의 0.42배 × 노이즈 진폭 ≈ 두 칸 남짓). 키우면 평면이
    // 접혀 **섬이 생기고**(그게 고치려던 *군데군데*다), 줄이면 다시 십자가 된다.
    // 남는 한두 칸짜리 섬은 뒤의 다수결(`Despeckle`)이 걷어낸다.
    static float Warp(int w) => w * 0.42f;

    // 가장 가까운 씨. **받는 자리는 이미 흔들린 좌표다.**
    static int NearestSeed(Vector2[] seeds, float px, float py)
    {
        var p = new Vector2(px, py);

        int   best  = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < seeds.Length; i++)
        {
            float d = Vector2.SqrMagnitude(p - seeds[i]);   // 제곱으로 비교한다 — 순서가 같다
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
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
