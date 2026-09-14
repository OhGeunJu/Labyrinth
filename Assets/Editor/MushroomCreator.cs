using UnityEditor;
using UnityEngine;

// **지하의 초록 축 — 버섯**(2026-09-06, 사용자: *"바닥에서 버섯 채집(열매채집 같은 느낌)
// 버섯 자동 스폰 그리고 나무 대용으로 쓸 버섯도 만들어줘"*).
//
// 둘을 만든다. **갈리는 것은 무엇을 내놓느냐 하나이고 나머지는 거기서 파생된다:**
//
// | | 갓버섯 | 거대버섯 |
// |---|---|---|
// | 내놓는 것 | 음식 | 목재 |
// | 무엇인가 | **`Forageable`** — 채집 | **`ResourceNode`** — 벌목 |
// | 붙는 법 | **큐**(지나가다 집는다) | **배정**(붙이거나 `ClaimFreeFor`) |
// | 한 번 하면 | **없어진다** | 그 자리에 남는다 |
// | 어디에 | 어느 굴에나 | **균사림에만** |
//
// **작은 것을 큰 것의 축소판으로 만들면 작아지지 않는다**(2026-09-06에 그렇게 만들었다
// 되돌렸다 — 사용자: *"배정받아서 캐는 버섯이 아니라 그냥 한번 캐고 없어지는 작은 버섯"*).
// 갓버섯을 자원 노드로 두었더니 폰 하나가 버섯 한 무리에 **배정되어 눌러앉았고**, 다 따도
// 버섯이 그대로 서 있었다.
//
// ## 목재 축의 답이 이것이다
//
// `docs/design_underworld.md`가 남긴 미결 — *"목재를 균사로 앉히나 / 교역품으로 만드나 /
// 지운다"*. **균사로 앉힌다.** 근거 셋: ① 건물 비용에는 이미 목재가 **하나도 없다**(38개가
// 돌) ② 남은 소비는 합판·나무방패·합성궁 셋뿐 ③ `FungalWood` 노드가 이미 있었다 — 다만
// **프리팹이 비어 있어 세울 수가 없었다.** 있어도 없는 것이었다.
//
// 그리고 **거대버섯은 균사림에만 난다**(`Biome` enum의 주석이 이미 *"농사와 목재가 여기서
// 나온다"*고 적어 두었다). 목재가 희소해지고 **굴 밖으로 나갈 이유**가 하나 생긴다 —
// 지하에서 목재가 아무 데서나 나면 지상을 접은 뜻이 반쯤 사라진다.
//
// ## 자동 스폰은 뿌리기가 한다 — 새로 만들지 않는다
//
// `WorldContentStreamer`의 `ScatterRule`이 이미 **좌표 해시로 결정적으로** 뿌린다(나무·바위).
// 버섯에 없던 것 둘만 그쪽에 붙였다: **`RequireOpenTile`**(안 켜면 바위 속에서 자란다)과
// **`RegrowDays`**(딴 것을 *잊기만* 하면 같은 자리에 다시 선다 — 자리를 저장할 필요가 없다).
//
// **다시 돌려도 안전하다** — 있으면 값만 맞춘다.
public static class MushroomCreator
{
    const string PrefabDir = "Assets/3_Prefab/Node";
    const string NodeDir   = "Assets/5_Data/Resource";
    const string ItemDir   = "Assets/Resources/Data/Items";
    const string BiomeDir  = "Assets/Resources/Data/Biomes";
    const string MatDir    = "Assets/2_Material";

    // **빛나는 것은 균사림 것뿐이다**(2026-09-06, 사용자: *"큰 나무는 다른 바이옴에서 안
    // 나오는 거 같은데 작은 버섯은 다른 바이옴에서 나와 … 다른 바이옴에서 나오는 버섯을
    // 추가하던지 해. 그 버섯은 발광효과가 없게"*).
    //
    // 발광 버섯이 아무 굴에나 나면 **균사림이 특별할 이유가 없어진다** — 그 지대를 읽게
    // 만드는 것이 빛인데 그 빛이 어디에나 있으면 신호가 아니다. 그렇다고 먹을 것을 한
    // 바이옴에만 두면 *거기로 가는 길*이 유일한 공략이 되므로, **평범한 양송이**을 따로 낸다.
    static readonly Biome[] FungalOnly = { Biome.Fungal };
    static readonly Biome[] MossOnly   = { Biome.Mossgrotto };
    static readonly Biome[] SwampOnly  = { Biome.Swamp };
    // **전부에 체크하면 바이옴을 나눈 뜻이 없어진다.** 마르거나 독하거나 갈라진 곳
    // (소금굴·유황굴·균열대)은 애초에 뺐고, **이끼굴도 뺐다**(2026-09-15, 사용자:
    // *"이끼굴에 양송이가 날 필요는 없을 거 같은데"*) — 이끼굴은 이끼와 나무가 나는
    // 곳이라, 거기에 양송이까지 얹으면 **바닥에 나는 것이 다 있는 바이옴**이 된다.
    static readonly Biome[] PlainCaves =
    {
        Biome.Cavern, Biome.Glacier, Biome.Mine, Biome.Crystal, Biome.Ossuary,
    };

    // **번호를 7로 옮겼다**(2026-09-06) — `World/6`에 이미 셋이 있어(`Resnap` ·
    // `Terrain In Authoring Scene`) *"6번 돌리세요"*가 어느 것인지 안 가려졌다.
    [MenuItem("DungeonColony/World/7. Create Flora (버섯 · 바닥 돌 · 단단한 나무)")]
    static void Create()
    {
        var wood = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/Res_Wood.asset");
        if (wood == null)
        {
            Debug.LogError("[버섯] Res_Wood를 못 찾았습니다 — 목재 아이템이 있어야 거대버섯이 섭니다.");
            return;
        }

        var glowFood  = Food("Food_CapMushroom",  "갓버섯", 4, 18f,
                             "균사림 바닥에 돋는 발광 버섯. 푸른 빛을 내며 배를 조금 채운다.");
        var caveFood  = Food("Food_CaveMushroom", "양송이", 3, 14f,
                             "어느 굴에나 돋는 흔한 버섯. 맛은 없지만 거저 얻는다.");

        var capPrefab   = BuildCap(glowFood, "Node_CapMushroom",
                                   new Color(0.18f, 0.38f, 0.85f), glow: true);
        var cavePrefab  = BuildCap(caveFood, "Node_CaveMushroom",
                                   new Color(0.52f, 0.42f, 0.32f), glow: false);

        // **노드 데이터를 먼저 만든다 — 프리팹이 그것을 들어야 하기 때문이다.**
        //
        // 2026-09-06에 이 순서를 뒤집어 놨다가 겪었다: 프리팹의 `ResourceNode.data`가 빈 채로
        // 구워져서, 살펴보기 창이 **`Node_GiantMushroom(Clone)`**이라는 날 이름과 빈 몸을
        // 띄웠다(이름을 파생시킬 `DropItem`이 없으니 `node.name`으로 떨어진다).
        // **둘이 서로를 가리키므로 한쪽을 먼저 만들고 나머지를 채운다.**
        var giant = Node("FungalWood", wood, prefab: null, gauge: 240f, min: 4, max: 8, workers: 2,
                         totalYields: 6,
                         displayName: "거대버섯",
                         description: "사람 키를 넘는 균사 기둥. 베면 목재 대신 쓸 질긴 균사가 나온다. " +
                                      "균사림에서만 자란다. **여섯 번 베면 없어진다** — " +
                                      "계속 쓰려면 기지에 심어야 한다.");

        var giantPrefab = BuildGiant(giant);
        giant.Prefab = giantPrefab;
        EditorUtility.SetDirty(giant);

        // **바닥에 굴러다니는 돌도 채집물이다**(2026-09-06, 사용자: *"버섯처럼 돌들도 바닥에
        // 좀 떨어져 있으면 좋겠어. 컨셉을 약간 자연에서 떨어져있는 돌 버섯들은 그냥 채집으로
        // 가고"*).
        //
        // **벽을 파는 것과 다른 축이다** — 벽은 *지형을 바꾸는 일*이고 이건 *줍는 일*이다.
        // 굴에 들어서자마자 돌이 조금 필요한데 그걸 위해 벽을 파야 하면, **첫 삽이 늘
        // 채굴**이 된다. 주울 것이 바닥에 있으면 시작이 부드러워진다.
        var stoneItem = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/Res_Stone.asset");
        GameObject pebblePrefab = null;
        if (stoneItem != null)
            pebblePrefab = BuildPebbles(stoneItem);
        else
            Debug.LogWarning("[버섯] Res_Stone을 못 찾아 바닥 돌은 안 만들었습니다.");

        // ── 단단한 나무 — **원목의 다음 티어**(2026-09-15, 사용자 결정) ──────
        //
        // 사용자: *"단단한 나무는 팰월드 처럼 나무 다음 트리야"* — 쓰임이 새로운 것이
        // 아니라 **같은 쓰임의 더 좋은 것**이고, 2층(지저늪)에서만 난다.
        //
        // **무엇이 먹는지는 아직 안 정했다**(사용자가 적기로 했다). 그래서 **재료와 나무만**
        // 만들어 두고 레시피·연구는 한 줄도 안 건드린다 — 지금 이건 *팔면 돈이 되는 목재*다.
        var hardItem   = Hardwood();
        var hardNode   = Node("HardwoodTrunk", hardItem, prefab: null,
                              gauge: 320f, min: 3, max: 6, workers: 2, totalYields: 5,
                              displayName: "물먹은 둥치",
                              description: "지저늪에 잠겼다 굳은 검은 나무. 가지는 삭아 없어지고 " +
                                           "둥치만 남았다. 물을 먹고 굳어 **원목보다 단단하다**. " +
                                           "**다섯 번 베면 없어진다.**");
        // ── 1층의 약초 사슬 (2026-09-15, 사용자 결정) ────────────────────────
        //
        // **허브가 지하에 안 났다.** 열 개 레시피의 뿌리인데(정수·치유 물약·해독제·습포·
        // 구급낭·약초차·에일·치즈·천) 노드에도 밭에도 뿌리기에도 없어서, **사는 것과
        // 뺏는 것**뿐이었다 — 1층에서 치료가 자급이 안 됐다.
        //
        // **이끼가 그 자리다**(사용자: *"이끼로는 뭐해? 물약 만들어야하나?"*). 새 아이템도
        // 새 레시피도 안 만든다 — **기다리고 있던 재료에 지하 원천을 주는 것**이 전부다.
        //
        // **푸른 곰팡이는 정수 그 자체다**(사용자: *"정수를 푸른곰팡이로 바꿔 페니실린"*).
        // `alch_essence`가 이미 *"모든 조제의 바탕"*이라 **역할이 정확히 겹쳤다** — 이름과
        // 설명만 바꾸고 **id는 그대로 뒀다**(레시피 여섯과 연구 하나가 문자열로 가리킨다).
        //
        // **둘은 사슬이 아니라 나란히 선다**(2026-09-15, 사용자: *"허브 → 배양 → 푸른곰팡이로
        // 가도 되긴하지만 굳이 갈 필요 없지않나? 두 템의 역할을 다르게 잡으면 되잖아"*).
        //
        // 처음엔 *허브를 배양해 곰팡이를 만든다*로 이었는데, **그러면 이끼굴 하나만 가도
        // 된다** — 균사림은 *한 단계 건너뛰는 지름길*일 뿐이라 안 가도 그만이다.
        // 이으면 바이옴이 **대체재**가 되고, 가르면 **둘 다 가야 한다.**
        //
        // 저작을 보니 **이미 갈려 있었다** — 손댈 것은 잇던 레시피를 **지우는 것**뿐이었다:
        //
        //   · **허브**(이끼굴) = 식물 — 천 짜기 · 약초차 · 에일 · 치즈 · 습포
        //   · **푸른 곰팡이**(균사림) = 약효 — 치유 물약 · 해독제 · 활력 물약 · 구급낭
        //
        // 그리고 **물약 대부분이 둘을 같이 먹는다**(치유 = 곰팡이 2 + 허브 1). 사슬로 이어
        // 두면 그게 *같은 것을 두 모양으로 넣는 것*이 되어 뜻이 없었다.
        var mossPatch  = BuildPatch("Node_MossPatch",  "herbs",       2, 4,
                                    new Color(0.26f, 0.52f, 0.22f), glow: false);
        var mouldPatch = BuildPatch("Node_BlueMould",  "alch_essence", 2, 3,
                                    new Color(0.22f, 0.44f, 0.92f), glow: true);

        // ── 이끼굴의 나무 (2026-09-15, 사용자: *"이끼굴은 흙컨셉으로 해서 나무를 여기에"*) ──
        //
        // **둘 다 원목을 낸다**(거대버섯과 이 나무). 앞서 내가 *"목재원이 둘이면 한쪽은
        // 다른 것을 내야 한다"*고 적었는데 **그건 내가 과하게 세운 규칙**이다 — 구리도 1층
        // 세 바이옴에서 나고, 아무도 그걸 문제 삼지 않는다. 같은 것이 여러 곳에서 나는 것은
        // *어디로 갈지*를 좁혀 주지 않을 뿐 **틀린 것이 아니다.**
        //
        // 갈리는 것은 **생김새와 곁들이**다: 균사림은 갓버섯(빛·식량)과 곰팡이(약)가 같이
        // 나고, 이끼굴은 이끼(약초)가 같이 난다. 목재만 보고 고를 일이 아니다.
        var mossTree = Node("MossTree", wood, prefab: null,
                            gauge: 200f, min: 3, max: 6, workers: 2, totalYields: 6,
                            displayName: "이끼 낀 나무",
                            description: "흙이 두터운 굴에 뿌리를 내린 나무. 잎은 없고 " +
                                         "가지에 이끼가 앉았다. **여섯 번 베면 없어진다.**");
        var treePrefab = BuildTree(mossTree);
        mossTree.Prefab = treePrefab;
        EditorUtility.SetDirty(mossTree);

        var hardPrefab = BuildTrunk(hardNode);
        hardNode.Prefab = hardPrefab;
        EditorUtility.SetDirty(hardNode);

        CleanBiomePools();
        DropStaleCapNode();
        WireScatter(capPrefab, cavePrefab, giantPrefab, pebblePrefab, hardPrefab,
                    mossPatch, mouldPatch, treePrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[초목] 갓버섯·양송이(채집) · 거대버섯(벌목, 균사림) · 바닥 돌 · " +
                  "**물먹은 둥치**(벌목, 지저늪)를 만들고 뿌리기에 물렸습니다.\n" +
                  "**단단한 나무를 먹는 레시피는 아직 없습니다** — 무엇이 먹을지 정하면 거기 걸 것.");
    }

    // ── 아이템 ──────────────────────────────────────────────────────────────

    // **원목의 다음 티어.** 묶음이 `Wood`라는 것이 요점이다 — `ResourceNodeData.IsTree`가
    // 그걸 보고 **채굴이 아니라 벌목**으로 가른다(노드마다 손으로 적지 않는다).
    //
    // 값은 원목(4)의 세 배쯤. 2층에서만 나고 다섯 번이면 마르므로 **원목과 같은 값이면
    // 굳이 캐러 갈 이유가 없다.** 무게는 더 나간다 — 물을 먹고 굳은 나무다.
    static ResourceItemData Hardwood()
    {
        const string id = "Res_Hardwood";
        string path = ItemDir + "/" + id + ".asset";

        var r = AssetDatabase.LoadAssetAtPath<ResourceItemData>(path);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<ResourceItemData>();
            AssetDatabase.CreateAsset(r, path);
        }

        r.Id          = id;
        r.ItemName    = "단단한 나무";
        r.Description = "물에 잠겼다 굳은 검은 나무. 원목보다 무겁고 잘 쪼개지지 않는다.";
        r.Weight      = 1.4f;
        r.Width       = 1;
        r.Height      = 1;
        r.BaseValue   = 12;
        r.Group       = ResourceGroup.Wood;
        r.Stack       = 200;
        EditorUtility.SetDirty(r);
        return r;
    }

    // **음식이라 `FoodData`다.** 영양은 빵(30)보다 낮게 둔다: 주우면 나오는 것이 밥이 되면 농사를 지을 이유가 없다.
    static FoodData Food(string id, string name, int value, float nutrition, string desc)
    {
        string path = ItemDir + "/" + id + ".asset";
        var f = AssetDatabase.LoadAssetAtPath<FoodData>(path);
        if (f == null)
        {
            f = ScriptableObject.CreateInstance<FoodData>();
            AssetDatabase.CreateAsset(f, path);
        }

        f.Id          = id;
        f.ItemName    = name;
        f.Description = desc;
        f.BaseValue   = value;
        f.Weight      = 0.15f;
        f.Stack       = 20;
        f.NutritionValue = nutrition;
        f.EatTime        = 2f;
        EditorUtility.SetDirty(f);
        return f;
    }

    // ── 노드 데이터 ─────────────────────────────────────────────────────────

    static ResourceNodeData Node(string name, ItemData drop, GameObject prefab,
                                 float gauge, int min, int max, int workers,
                                 int totalYields = 0,
                                 string displayName = null, string description = null)
    {
        string path = NodeDir + "/" + name + ".asset";
        var n = AssetDatabase.LoadAssetAtPath<ResourceNodeData>(path);
        if (n == null)
        {
            n = ScriptableObject.CreateInstance<ResourceNodeData>();
            AssetDatabase.CreateAsset(n, path);
        }

        n.DropItem      = drop;
        n.Prefab        = prefab;   // **비면 세울 수가 없다** — `FungalWood`가 그 상태였다
        n.GaugeRequired = gauge;
        n.DropMin       = min;
        n.DropMax       = max;
        n.RequiredSkill = 0f;
        n.MaxWorkers    = workers;
        n.TotalYields   = totalYields;   // **야생은 유한하다** — 지은 노드는 이 값을 안 본다
        if (displayName != null) n.DisplayName = displayName;
        if (description != null) n.Description = description;
        EditorUtility.SetDirty(n);
        return n;
    }

    // 갓버섯이 자원 노드이던 시절의 데이터를 지운다 — **아무도 안 읽는 에셋을 남기면**
    // 다음에 읽는 사람이 *"둘 중 어느 쪽이 진짜인가"*를 재게 된다(옛 `sites` 배열과 같은 자리).
    static void DropStaleCapNode()
    {
        string path = NodeDir + "/CapMushroom.asset";
        if (AssetDatabase.LoadAssetAtPath<ResourceNodeData>(path) == null) return;

        AssetDatabase.DeleteAsset(path);
        Debug.Log("[버섯] 옛 CapMushroom 노드 데이터를 지웠습니다 — 갓버섯은 이제 채집물입니다.");
    }

    // ── 프리팹 ──────────────────────────────────────────────────────────────

    // 갓버섯 — 무릎께의 버섯 **둘**이 한 포기.
    //
    // 처음에는 다섯을 한 오브젝트에 그렸다(*"하나만 세우면 화면에서 안 보인다"*). 그런데
    // **갓을 늘리는 것과 물건을 늘리는 것은 다른 물음이다** — 갓만 늘리면 여전히 **한 번
    // 따면 무리째 사라진다.** 이제 뿌리기가 **한 자리에 서넛을 앉히므로**(`ClusterMin/Max`)
    // 포기 하나는 작아도 된다. 서넛 × 둘 = 갓 예닐곱이 2.5m 안에 흩어진다 — 화면에서는
    // 버섯밭이고 **따는 것은 하나씩**이다.
    static GameObject BuildCap(ItemData drop, string name, Color capColor, bool glow)
    {
        var root = new GameObject(name);

        // **자리를 굴리되 시드를 못 박는다** — 프리팹은 한 번 구워 두는 것이라, 다시 돌릴
        // 때마다 모양이 달라지면 **버전 관리에 매번 diff가 뜬다.**
        var rng = new System.Random(9061);
        for (int i = 0; i < 2; i++)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float r = 0.08f + (float)rng.NextDouble() * 0.16f;
            float s = 0.70f + (float)rng.NextDouble() * 0.60f;
            Cap(root.transform, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), s,
                name, capColor, glow);
        }

        // **트리거다 — 칸을 안 막는다.** 버섯이 통로를 막으면 한 칸짜리 굴이 통째로 끊긴다
        // (벽걸이 횃불과 같은 규칙). 그래도 콜라이더는 **있어야 한다**: 좌클릭으로 노드를
        // 고르는 길이 그것뿐이다.
        var col = root.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.height    = 0.55f;
        col.radius    = 0.35f;
        col.center    = new Vector3(0f, 0.27f, 0f);

        // **은은해야 버섯이다.** 처음에는 세기 0.8에 발광 2.2배로 넣었더니 화면에서
        // **흰 덩어리**가 됐다(사용자: *"이건 버섯이 아니라 태양이야"*) — 발광이 세면 색이
        // 흰색으로 날아가고, 그러면 *파란 버섯*이 아니라 *빛나는 무언가*가 된다.
        // **바탕색은 진하게, 발광은 옅게.**
        if (glow) Glow(root, radius: 3.5f, strength: 0.30f, intensity: 0.22f, range: 3.5f);

        // **자원 노드가 아니다** — 한 번 따면 없어지는 것이라 게이지도 배정도 없다.
        root.AddComponent<Forageable>().Configure(drop, 1, 3);
        return Save(root, name);
    }

    // 거대버섯 — 나무 자리를 대신하므로 **나무만큼 크다**(갓 지름 2.4m · 키 3.2m).
    static GameObject BuildGiant(ResourceNodeData data)
    {
        var root = new GameObject("Node_GiantMushroom");

        var stalk = Prim(PrimitiveType.Cylinder, "Stalk", root.transform,
                         new Vector3(0.50f, 1.30f, 0.50f), new Vector3(0f, 1.30f, 0f));
        Paint(stalk, "Mdl_Stalk_Giant", new Color(0.42f, 0.48f, 0.62f));

        var cap = Prim(PrimitiveType.Sphere, "Cap", root.transform,
                       new Vector3(2.40f, 1.10f, 2.40f), new Vector3(0f, 2.70f, 0f));
        Paint(cap, "Mdl_Cap_Giant", new Color(0.16f, 0.34f, 0.78f), emissive: true);

        // **여기는 트리거가 아니다.** 나무 자리를 대신하니 몸으로 막는 것이 맞다.
        var col = root.AddComponent<CapsuleCollider>();
        col.height = 3.2f;
        col.radius = 0.6f;
        col.center = new Vector3(0f, 1.6f, 0f);

        // **거대버섯이 그 지대의 등이다** — 갓이 크고 높아서 둘레를 넓게 밝힌다.
        // 거대버섯이 그 지대의 등이지만 **태양은 아니다** — 세기를 낮추고 반경으로 넓힌다
        // (밝기를 올리면 갓이 하얗게 날아가고, 반경은 *어디까지가 버섯 지대인가*를 그린다).
        Glow(root, radius: 11f, strength: 0.55f, intensity: 0.7f, range: 12f, height: 2.7f);

        // **데이터를 물려서 굽는다** — 뿌리기는 이 프리팹을 그대로 세우므로, 비어 있으면
        // 나오는 것도 게이지도 이름도 없는 노드가 세계에 선다.
        var node = root.AddComponent<ResourceNode>();
        SerializedWrite.Set(node, so => so.FindProperty("data").objectReferenceValue = data);

        return Save(root, "Node_GiantMushroom");
    }

    // ── 물먹은 둥치 ─────────────────────────────────────────────────────────
    //
    // **가지가 없다**(2026-09-15, 사용자 결정: *"물에 절인 검은 둥치"*). 거대버섯과
    // **확실히 갈려야 한다** — 둘 다 목재를 내는데 층이 다르므로, 멀리서 *어느 쪽을 캐는
    // 중인지*가 눈으로 읽혀야 한다. 버섯은 갓이 있고 이건 기둥뿐이다.
    //
    // **위가 부러져 있다**(원뿔이 아니라 잘린 원기둥) — 삭아 없어진 가지를 그리는 대신
    // *부러진 자리*로 말한다. 그리고 **조금 기울인다**: 곧게 선 기둥은 사람이 세운 것처럼
    // 보이고, 기울면 **물에 밀려 자빠지다 굳은 것**으로 읽힌다.
    //
    // **밑동이 굵다** — 물에 잠긴 나무의 뿌리목이 부푼 모양이고, 무엇보다 **한 칸을
    // 채워야** 칸에 놓인 물건으로 보인다(바닥 돌에서 배운 것과 같다).
    static GameObject BuildTrunk(ResourceNodeData data)
    {
        var root = new GameObject("Node_HardwoodTrunk");

        // 밑동 — 굵고 낮게.
        var flare = Prim(PrimitiveType.Cylinder, "Flare", root.transform,
                         new Vector3(0.95f, 0.22f, 0.95f), new Vector3(0f, 0.20f, 0f));
        Paint(flare, "Mdl_Trunk_Wet", new Color(0.13f, 0.12f, 0.11f));

        // 둥치 — 두 토막으로 나눠 **위로 갈수록 가늘게**. 한 토막이면 파이프로 보인다.
        var low = Prim(PrimitiveType.Cylinder, "TrunkLow", root.transform,
                       new Vector3(0.62f, 0.75f, 0.62f), new Vector3(0f, 1.00f, 0f));
        Paint(low, "Mdl_Trunk_Wet", new Color(0.13f, 0.12f, 0.11f));

        var high = Prim(PrimitiveType.Cylinder, "TrunkHigh", root.transform,
                        new Vector3(0.46f, 0.70f, 0.46f), new Vector3(0.10f, 2.35f, 0.05f));
        high.transform.localRotation = Quaternion.Euler(6f, 0f, 4f);   // 기울었다
        Paint(high, "Mdl_Trunk_Wet", new Color(0.11f, 0.10f, 0.10f));

        // 부러진 단면 — **한 톤 밝게.** 속살이 드러난 자리라 겉보다 밝아야 *부러졌다*로
        // 읽힌다(같은 색이면 그냥 잘린 원기둥이다).
        var broke = Prim(PrimitiveType.Cylinder, "Break", root.transform,
                         new Vector3(0.44f, 0.04f, 0.44f), new Vector3(0.18f, 3.02f, 0.09f));
        broke.transform.localRotation = Quaternion.Euler(6f, 0f, 4f);
        Paint(broke, "Mdl_Trunk_Core", new Color(0.34f, 0.28f, 0.22f));

        // **트리거가 아니다** — 나무 자리라 몸으로 막는 것이 맞다(거대버섯과 같은 규칙).
        var col = root.AddComponent<CapsuleCollider>();
        col.height = 3.1f;
        col.radius = 0.55f;
        col.center = new Vector3(0f, 1.55f, 0f);

        // **빛을 안 단다** — 거대버섯은 그 지대의 등이지만 이건 죽은 나무다. 늪이 어두운
        // 것이 그 바이옴의 성격이고, 여기에 등을 달면 균사림과 구별이 흐려진다.

        var node = root.AddComponent<ResourceNode>();
        SerializedWrite.Set(node, so => so.FindProperty("data").objectReferenceValue = data);

        return Save(root, "Node_HardwoodTrunk");
    }

    // ── 이끼 낀 나무 ───────────────────────────────────────────────────────
    //
    // 1층에 목재를 내는 것이 셋이 됐다 — 셋이 **눈으로 갈려야** 어느 굴에 있는지가 읽힌다:
    //
    //   · 거대버섯(균사림) — **갓**이 있다. 푸르고 빛난다
    //   · 이끼 낀 나무(이끼굴) — **가지**가 있다. 갈색에 이끼가 앉았다
    //   · 물먹은 둥치(지저늪, 2층) — **가지가 없다**. 검고 위가 부러졌다
    //
    // **가지가 이 나무의 표시다.** 잎은 안 단다 — 해가 없는 곳이라 잎이 있으면 *지상에서
    // 떠온 것*으로 보인다. 대신 **가지 끝에 이끼**를 얹어 어느 굴의 것인지 말한다.
    static GameObject BuildTree(ResourceNodeData data)
    {
        var root = new GameObject("Node_MossTree");
        var rng  = new System.Random(NameSeed("Node_MossTree"));

        var low = Prim(PrimitiveType.Cylinder, "TrunkLow", root.transform,
                       new Vector3(0.52f, 0.85f, 0.52f), new Vector3(0f, 0.85f, 0f));
        Paint(low, "Mdl_Bark", new Color(0.30f, 0.22f, 0.15f));

        var high = Prim(PrimitiveType.Cylinder, "TrunkHigh", root.transform,
                        new Vector3(0.38f, 0.80f, 0.38f), new Vector3(0f, 2.45f, 0f));
        Paint(high, "Mdl_Bark", new Color(0.30f, 0.22f, 0.15f));

        // 가지 셋 — **위로 벌어진다.** 아래로 처지면 죽은 나무로 보인다.
        for (int i = 0; i < 3; i++)
        {
            float a = (float)(i * 2.0 * System.Math.PI / 3.0 + rng.NextDouble() * 0.5);
            float dx = Mathf.Cos(a), dz = Mathf.Sin(a);

            var br = Prim(PrimitiveType.Cylinder, "Branch" + i, root.transform,
                          new Vector3(0.14f, 0.55f, 0.14f),
                          new Vector3(dx * 0.55f, 3.05f, dz * 0.55f));
            br.transform.localRotation = Quaternion.Euler(dz * 42f, 0f, -dx * 42f);
            Paint(br, "Mdl_Bark", new Color(0.30f, 0.22f, 0.15f));

            // **가지 끝의 이끼** — 이 나무가 어느 굴의 것인지를 말하는 한 점이다.
            var moss = Prim(PrimitiveType.Sphere, "Moss" + i, root.transform,
                            new Vector3(0.40f, 0.22f, 0.40f),
                            new Vector3(dx * 1.05f, 3.62f, dz * 1.05f));
            Paint(moss, "Mdl_TreeMoss", new Color(0.26f, 0.52f, 0.22f));
        }

        // **트리거가 아니다** — 나무라 몸으로 막는 것이 맞다(거대버섯과 같은 규칙).
        var col = root.AddComponent<CapsuleCollider>();
        col.height = 3.4f;
        col.radius = 0.5f;
        col.center = new Vector3(0f, 1.7f, 0f);

        var node = root.AddComponent<ResourceNode>();
        SerializedWrite.Set(node, so => so.FindProperty("data").objectReferenceValue = data);

        return Save(root, "Node_MossTree");
    }

    // ── 바닥에 깔린 것 — 이끼 · 곰팡이 ─────────────────────────────────────
    //
    // **버섯이 아니라 깔린 것이다.** 갓버섯은 대와 갓이 있어 *서 있는 물건*인데, 이끼와
    // 곰팡이는 **바닥에 붙어 번진 자국**이다 — 대를 세우면 작은 버섯으로 읽힌다.
    // 그래서 납작한 덩이 몇을 바닥에 눌러 붙인다.
    //
    // **트리거다**(바닥 돌과 같은 규칙) — 칸을 막으면 이끼 한 무더기가 통로를 끊는다.
    // 그래도 콜라이더는 **있어야 한다**: 좌클릭으로 고르는 길이 그것뿐이다.
    //
    // **JSON 아이템을 낸다** — 허브도 곰팡이도 `StreamingAssets`라 guid로 못 가리킨다.
    // `Forageable.itemId`가 그 길이다(`MerchantPoolEntry`와 같은 규약).
    static GameObject BuildPatch(string name, string itemId, int min, int max,
                                 Color tone, bool glow)
    {
        var root = new GameObject(name);

        // **시드를 이름에서 낸다 — `GetHashCode`가 아니다**(실행마다 달라지면 프리팹을
        // 다시 구울 때마다 얼룩 모양이 바뀌어 **버전 관리에 매번 diff가 뜬다**).
        var rng = new System.Random(NameSeed(name));

        // **한 칸을 덮는다** — 주울 물건은 칸만 해야 칸에 놓인 것으로 보인다(바닥 돌에서
        // 배운 것). 납작한 구 다섯을 겹쳐 얼룩을 만든다.
        for (int i = 0; i < 5; i++)
        {
            float a  = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float r  = (float)rng.NextDouble() * 0.32f;
            float sc = 0.34f + (float)rng.NextDouble() * 0.24f;

            var c = Prim(PrimitiveType.Sphere, "Clump" + i, root.transform,
                         new Vector3(sc, sc * 0.30f, sc * 0.92f),
                         new Vector3(Mathf.Cos(a) * r, sc * 0.08f, Mathf.Sin(a) * r));

            c.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            Paint(c, "Mdl_" + name, tone, emissive: glow);
        }

        var sph = root.AddComponent<SphereCollider>();
        sph.isTrigger = true;
        sph.radius    = 0.5f;
        sph.center    = new Vector3(0f, 0.12f, 0f);

        // **곰팡이는 아주 약하게 빛난다** — 균사림이 푸른 지대라 바닥에 붙은 얼룩은
        // 그냥 어둠에 묻힌다. 갓버섯만큼 밝히면 *등*이 되어 버리니 반경을 짧게 둔다.
        if (glow) Glow(root, radius: 3.5f, strength: 0.28f, intensity: 0.35f, range: 4f, height: 0.3f);

        var f = root.AddComponent<Forageable>();
        SerializedWrite.Set(f, so =>
        {
            so.FindProperty("itemId").stringValue = itemId;
            so.FindProperty("minAmount").intValue = min;
            so.FindProperty("maxAmount").intValue = max;
        });

        return Save(root, name);
    }

    static int NameSeed(string s)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
            return h != 0 ? h : 1;
        }
    }

    // 바닥에 굴러다니는 돌 서넛. **버섯과 같은 물건이다**(`Forageable`) — 모양만 다르다.
    static GameObject BuildPebbles(ItemData drop)
    {
        var root = new GameObject("Node_Pebbles");
        var rng  = new System.Random(4412);

        // **한 칸을 채운다**(2026-09-06, 사용자: *"진짜 그냥 칸 1칸을 채울만큼"*). 칸이 1m라
        // 무더기의 지름도 1m다 — 그전 26~46cm짜리는 위에서 내려다보는 화면에서 여전히
        // **바닥의 얼룩**이었다. **주울 물건은 칸만 해야 칸에 놓인 것으로 보인다.**
        //
        // **구가 아니라 육면체다**(사용자: *"너무 동그래"*). 구를 눌러 놓으면 **조약돌**이
        // 되는데 굴 바닥에 굴러다니는 것은 **깨진 돌**이다 — 모서리가 있어야 그렇게 읽힌다.
        // 세 축을 다 돌려서 같은 상자가 같은 각도로 서지 않게 한다.
        for (int i = 0; i < 4; i++)
        {
            float a  = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float r  = (float)rng.NextDouble() * 0.30f;
            float sc = 0.30f + (float)rng.NextDouble() * 0.22f;

            var c = Prim(PrimitiveType.Cube, "Rock" + i, root.transform,
                         new Vector3(sc, sc * (0.55f + (float)rng.NextDouble() * 0.35f), sc * 0.85f),
                         new Vector3(Mathf.Cos(a) * r, sc * 0.30f, Mathf.Sin(a) * r));

            c.transform.localRotation = Quaternion.Euler(
                (float)rng.NextDouble() * 26f - 13f,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 26f - 13f);

            Paint(c, "Mdl_Pebble", new Color(0.46f, 0.45f, 0.44f));
        }

        // **트리거다 — 칸을 안 막는다.** 통로에 굴러다니는 돌이 길을 끊으면 안 된다.
        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = 0.55f;
        col.center    = new Vector3(0f, 0.22f, 0f);

        root.AddComponent<Forageable>().Configure(drop, 1, 2);
        return Save(root, "Node_Pebbles");
    }

    // **`LightSource`로 단다 — 그냥 `Light`가 아니다.** `Vision`이 그 레지스트리를 읽으므로,
    // 발광 지대에 들어서면 **화면이 밝아지는 만큼 폰도 실제로 더 본다.** 순수 장식 조명으로
    // 두면 *"밝아 보이는데 안 보인다"*가 생긴다 — 화면과 판정은 같은 수를 봐야 한다.
    //
    // **켜는 사람이 없다** — `Campfire`가 때를 보고 켜는 불과 달리 버섯은 그냥 빛난다
    // (`LightSource.AlwaysLit`). **깜빡이지도 않는다**(`flicker` 0): 불이 아니라 빛이다.
    static void Glow(GameObject root, float radius, float strength,
                     float intensity, float range, float height = 0.35f)
    {
        var lampGo = new GameObject("Glow");
        lampGo.transform.SetParent(root.transform, false);
        lampGo.transform.localPosition = new Vector3(0f, height, 0f);

        var lamp = lampGo.AddComponent<Light>();
        lamp.type      = LightType.Point;
        lamp.color     = new Color(0.35f, 0.55f, 1f);
        lamp.intensity = intensity;
        lamp.range     = range;
        lamp.shadows   = LightShadows.None;   // 수백 개가 그림자를 던지면 프레임이 죽는다

        var src = root.AddComponent<LightSource>();
        src.AlwaysLit = true;
        SerializedWrite.Set(src, so =>
        {
            so.FindProperty("radius").floatValue   = radius;
            so.FindProperty("strength").floatValue = strength;
            so.FindProperty("flicker").floatValue  = 0f;
            so.FindProperty("lamp").objectReferenceValue = lamp;
        });
    }

    // **재질 이름을 종류마다 가른다** — 하나로 쓰면 나중에 만든 쪽이 앞엣것의 색을 덮어써서
    // **양송이를 넣는 순간 갓버섯까지 갈색이 된다**(색조 캐시에서 겪은 것과 같은 자리).
    static void Cap(Transform parent, Vector3 at, float scale,
                    string kind, Color capColor, bool glow)
    {
        var stalk = Prim(PrimitiveType.Cylinder, "Stalk", parent,
                         new Vector3(0.06f, 0.13f, 0.06f) * scale,
                         at + new Vector3(0f, 0.13f * scale, 0f));
        Paint(stalk, "Mdl_Stalk_" + kind, glow ? new Color(0.42f, 0.48f, 0.62f)
                                               : new Color(0.72f, 0.66f, 0.54f));

        var head = Prim(PrimitiveType.Sphere, "Cap", parent,
                        new Vector3(0.26f, 0.15f, 0.26f) * scale,
                        at + new Vector3(0f, 0.30f * scale, 0f));
        Paint(head, "Mdl_Cap_" + kind, capColor, emissive: glow);
    }

    // 프리미티브는 콜라이더를 달고 나온다 — **루트가 든 하나만 남긴다.** 자식마다 달려 있으면
    // 커서 레이캐스트가 갓 하나를 짚고, 그러면 노드가 아니라 그 조각이 잡힌다.
    static GameObject Prim(PrimitiveType type, string name, Transform parent,
                           Vector3 scale, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale    = scale;
        go.transform.localPosition = pos;
        return go;
    }

    // **갓은 스스로 빛난다**(2026-09-06, 사용자가 든 예: 테라리아의 발광 버섯 지대).
    // 어두운 굴에서 *"여기는 버섯 지형이구나"*를 만드는 것은 바닥 색이 아니라 **빛**이다 —
    // 바닥은 조명이 어두우면 무슨 색이든 검게 눌린다.
    static void Paint(GameObject go, string matName, Color fallback, bool emissive = false)
    {
        string path = MatDir + "/" + matName + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            // **재질 에셋이 셰이더를 가리키는 순간 빌드에도 들어간다** — `RuntimeShaders`가
            // 적어 둔 그 규칙의 뒷면이다(코드의 `Shader.Find`는 참조가 아니라서 잘려 나간다).
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            m.SetColor("_BaseColor", fallback);
            m.SetColor("_Color",     fallback);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(m, path);
        }

        // **다시 돌려도 값을 맞춘다** — 색을 바꿨는데 에셋이 이미 있으면 옛 색 그대로다
        // (*"있으면 다시 만들지 않는다"*가 여기서는 *"고쳐도 안 바뀐다"*가 된다).
        m.SetColor("_BaseColor", fallback);
        m.SetColor("_Color",     fallback);

        if (emissive)
        {
            // **URP는 발광을 키워드로 켠다** — 색만 넣고 키워드를 안 켜면 조용히 안 빛난다.
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            // **발광은 바탕색보다 어둡게.** 1을 넘기면 색이 흰색으로 날아가 *빛나는 파란
            // 버섯*이 아니라 *빛나는 덩어리*가 된다.
            m.SetColor("_EmissionColor", fallback * 0.45f);
        }
        else
        {
            // **끄는 것도 여기서 한다** — 갓버섯 재질을 양송이가 물려받는 일은 없지만,
            // 값을 맞추는 자리가 켜기만 하면 **한 번 켜진 것은 영영 안 꺼진다.**
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            m.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(m);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
    }

    static GameObject Save(GameObject root, string name)
    {
        var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
        Object.DestroyImmediate(root);
        return saved;
    }

    // ── 배선 ────────────────────────────────────────────────────────────────

    // **버섯을 `BiomeContentData.ResourceNodes`에서 뺀다** — 거기는 *"이 바이옴의 벽을 파면
    // 무엇이 나오나"*(광맥 풀, `RockMap.VeinAt`)이지 *"이 바이옴 바닥에 무엇이 나나"*가
    // 아니다. 넣어 두면 **버섯이 바위 속 광맥으로 나온다** — 바닥에 나야 할 것이 벽에 박힌다.
    //
    // 이름이 *"이 바이옴에서 등장하는 자원 노드 풀"*이라 그렇게 읽힐 만했고 09-06에 실제로
    // 그렇게 넣었다(`FungalWood`는 그 전부터 거기 있었다). **바닥에 나는 것은 뿌리기가
    // 답한다**(`ScatterRule.Biomes`).
    static void CleanBiomePools()
    {
        int removed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:BiomeContentData", new[] { BiomeDir }))
        {
            var b = AssetDatabase.LoadAssetAtPath<BiomeContentData>(AssetDatabase.GUIDToAssetPath(guid));
            if (b == null) continue;

            int n = b.ResourceNodes.RemoveAll(
                r => r != null && (r.name == "CapMushroom" || r.name == "FungalWood"));
            if (n == 0) continue;

            removed += n;
            EditorUtility.SetDirty(b);
        }
        if (removed > 0)
            Debug.Log($"[버섯] 광맥 풀에서 버섯 {removed}개를 걷어냈습니다 — 버섯은 벽이 아니라 바닥에 납니다.");
    }

    // **씬이 값을 든다** — 뿌리기 규칙은 `WorldContentStreamer`의 직렬화 배열이라, 코드
    // 기본값으로 적어 두면 아무 힘이 없다. 그래서 도구가 씬에 쓴다(`0. Setup World
    // Streaming`과 같은 자리).
    //
    // **없으면 조용히 넘어가지 않고 말한다** — 에셋만 만들어 두고 아무것도 안 나면 원인이
    // 코드가 아니라 배선에 있고, 그건 화면에서 절대 안 보인다.
    static void WireScatter(GameObject capPrefab, GameObject cavePrefab, GameObject giantPrefab,
                            GameObject pebblePrefab, GameObject trunkPrefab,
                            GameObject mossPrefab, GameObject mouldPrefab,
                            GameObject treePrefab)
    {
        // **쓰는 문은 `ScatterWiring` 하나다**(2026-09-14) — 바닥 광맥을 쓰는 도구가 하나 더
        // 생기면서, 여기 박혀 있던 *"안 적으면 조용히 아무것도 안 나는"* 규칙들(지하 고도
        // 범위 · `RequireOpenTile`)을 저쪽이 모르면 그대로 사고가 된다.
        if (!ScatterWiring.Open("버섯", out var so, out var rules)) return;

        // **밀도는 "자리 수 × 무리 크기"다.** 갓버섯은 청크(64m)에 **네 자리, 자리마다
        // 셋~다섯이 맞닿은 칸에** — 굴을 걷다 보면 몇 번은 버섯밭을 만나고, 만나면 폰이
        // 거기 서서 몇 번을 딴다. 자리 사이는 넉넉히 벌린다(12m): 무리끼리 붙으면 무리로
        // 안 보인다.
        //
        // **퍼짐은 1칸이다** — 제 칸과 맞닿은 여덟. 다섯이면 한 칸도 안 비고 뭉친다.
        // 2로 올리면 3×3을 넘어서서 *한 무더기*가 아니라 *느슨한 밭*이 된다.
        ScatterWiring.Set(rules, "갓버섯", capPrefab, perChunk: 5f, spacing: 12f, regrowDays: 3f,
             biomes: FungalOnly, cluster: (3, 5), spread: 1);

        // **양송이는 안 빛난다** — 그래서 더 흔해도 된다(빛이 아니라 먹을 것이다).
        ScatterWiring.Set(rules, "양송이", cavePrefab, perChunk: 4f, spacing: 14f, regrowDays: 3f,
             biomes: PlainCaves, cluster: (2, 4), spread: 1);

        // **거대버섯은 균사림에만** — 그리고 균사림 안에서는 흔하다(청크당 넷). 바이옴으로
        // 걸러 놓고 밀도까지 낮추면 *"목재가 나는 곳"*에 가서도 목재를 못 본다.
        // **무리는 안 짓는다** — 나무 자리라 하나씩 서는 것이 맞다.
        ScatterWiring.Set(rules, "거대버섯", giantPrefab, perChunk: 4f, spacing: 14f, regrowDays: 0f,
             biomes: FungalOnly, cluster: (1, 1), spread: 0);


        // **물먹은 둥치는 지저늪에만** — 2층의 목재다. 거대버섯(청크당 넷)보다 드물게
        // 둔다: 원목은 *늘 쓰는 것*이고 이건 **상위 재료**라, 같은 밀도면 티어가 안 읽힌다.
        // **다시 안 난다**(0) — 죽은 나무는 자라지 않는다.
        ScatterWiring.Set(rules, "물먹은 둥치", trunkPrefab, perChunk: 2.5f, spacing: 16f,
             regrowDays: 0f, biomes: SwampOnly, cluster: (1, 1), spread: 0);

        // **이끼 낀 나무는 이끼굴에만** — 1층 목재의 두 원천 중 하나다(다른 하나가
        // 균사림의 거대버섯). 거대버섯(청크당 넷)보다 조금 성기게 둔다: 나무 한 그루가
        // 칸을 막고 서 있으므로, 같은 밀도면 굴이 **숲이 아니라 장애물밭**이 된다.
        ScatterWiring.Set(rules, "이끼 낀 나무", treePrefab, perChunk: 3f, spacing: 13f,
             regrowDays: 0f, biomes: MossOnly, cluster: (1, 1), spread: 0);

        // **이끼는 이끼굴에 흔하다** — 바이옴 이름이 그것이고, 무엇보다 **약초가 여기서만
        // 난다**(1층 전체의 치료가 이 한 바이옴에 달렸으니 드물면 병목이 된다).
        // **다시 난다**(3일) — 약초는 이어지는 수입이어야 한다(갓버섯과 같은 규칙).
        ScatterWiring.Set(rules, "이끼 무더기", mossPrefab, perChunk: 5f, spacing: 11f,
             regrowDays: 3f, biomes: MossOnly, cluster: (3, 5), spread: 1);

        // **푸른 곰팡이는 균사림에만.** 이제 **만들 길이 없으므로**(배양 레시피를 지웠다)
        // 여기가 세계에서 유일한 원천이다 — 드물게 두면 물약이 통째로 막힌다.
        // 이끼보다 조금 성기되(허브가 먹는 레시피가 더 많다) **양을 늘려** 맞춘다(2~3).
        ScatterWiring.Set(rules, "푸른 곰팡이", mouldPrefab, perChunk: 4f, spacing: 13f,
             regrowDays: 4f, biomes: FungalOnly, cluster: (2, 4), spread: 1);

        // **돌은 어디에나, 그리고 흔하다** — 버섯보다 눈에 안 띄는 물건이라 드물면 없는 것과
        // 같다. **다시 안 난다**(0): 자연에 떨어져 있던 것이지 자라는 것이 아니다.
        if (pebblePrefab != null)
            ScatterWiring.Set(rules, "바닥 돌", pebblePrefab, perChunk: 7f, spacing: 9f, regrowDays: 0f,
                 biomes: null, cluster: (2, 4), spread: 1);

        ScatterWiring.Commit(so);
    }

}
