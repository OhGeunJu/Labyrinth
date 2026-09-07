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
    static readonly Biome[] PlainCaves =
    {
        Biome.Limestone, Biome.Cavern, Biome.Flooded, Biome.Mine, Biome.Crystal,
    };

    // **번호를 7로 옮겼다**(2026-09-06) — `World/6`에 이미 셋이 있어(`Resnap` ·
    // `Terrain In Authoring Scene`) *"6번 돌리세요"*가 어느 것인지 안 가려졌다.
    [MenuItem("DungeonColony/World/7. Create Mushrooms (버섯 · 바닥 돌)")]
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

        CleanBiomePools();
        DropStaleCapNode();
        WireScatter(capPrefab, cavePrefab, giantPrefab, pebblePrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[버섯] 갓버섯(채집) · 거대버섯(벌목, 균사림)을 만들고 뿌리기에 물렸습니다.");
    }

    // ── 아이템 ──────────────────────────────────────────────────────────────

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
        FireArt.Set(node, so => so.FindProperty("data").objectReferenceValue = data);

        return Save(root, "Node_GiantMushroom");
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
        FireArt.Set(src, so =>
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
                            GameObject pebblePrefab)
    {
        var streamer = Object.FindFirstObjectByType<WorldContentStreamer>(FindObjectsInactive.Include);
        if (streamer == null)
        {
            Debug.LogWarning("[버섯] 씬에 WorldContentStreamer가 없습니다 — 에셋만 만들었고 " +
                             "뿌리기는 안 걸렸습니다. 판(씬)을 열고 다시 돌리세요.");
            return;
        }

        var so    = new SerializedObject(streamer);
        var rules = so.FindProperty("scatter");

        // **밀도는 "자리 수 × 무리 크기"다.** 갓버섯은 청크(64m)에 **네 자리, 자리마다
        // 셋~다섯이 맞닿은 칸에** — 굴을 걷다 보면 몇 번은 버섯밭을 만나고, 만나면 폰이
        // 거기 서서 몇 번을 딴다. 자리 사이는 넉넉히 벌린다(12m): 무리끼리 붙으면 무리로
        // 안 보인다.
        //
        // **퍼짐은 1칸이다** — 제 칸과 맞닿은 여덟. 다섯이면 한 칸도 안 비고 뭉친다.
        // 2로 올리면 3×3을 넘어서서 *한 무더기*가 아니라 *느슨한 밭*이 된다.
        Rule(rules, "갓버섯", capPrefab, perChunk: 5f, spacing: 12f, regrowDays: 3f,
             biomes: FungalOnly, cluster: (3, 5), spread: 1);

        // **양송이는 안 빛난다** — 그래서 더 흔해도 된다(빛이 아니라 먹을 것이다).
        Rule(rules, "양송이", cavePrefab, perChunk: 4f, spacing: 14f, regrowDays: 3f,
             biomes: PlainCaves, cluster: (2, 4), spread: 1);

        // **거대버섯은 균사림에만** — 그리고 균사림 안에서는 흔하다(청크당 넷). 바이옴으로
        // 걸러 놓고 밀도까지 낮추면 *"목재가 나는 곳"*에 가서도 목재를 못 본다.
        // **무리는 안 짓는다** — 나무 자리라 하나씩 서는 것이 맞다.
        Rule(rules, "거대버섯", giantPrefab, perChunk: 4f, spacing: 14f, regrowDays: 0f,
             biomes: FungalOnly, cluster: (1, 1), spread: 0);

        // **돌은 어디에나, 그리고 흔하다** — 버섯보다 눈에 안 띄는 물건이라 드물면 없는 것과
        // 같다. **다시 안 난다**(0): 자연에 떨어져 있던 것이지 자라는 것이 아니다.
        if (pebblePrefab != null)
            Rule(rules, "바닥 돌", pebblePrefab, perChunk: 7f, spacing: 9f, regrowDays: 0f,
                 biomes: null, cluster: (2, 4), spread: 1);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(streamer);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(streamer.gameObject.scene);
    }

    // 이름으로 찾아 값만 맞춘다 — **그냥 붙이면 다시 돌릴 때마다 규칙이 하나씩 늘어** 버섯이
    // 두 배, 세 배로 난다(`5. Place Sites`가 신원으로 맞추는 것과 같은 이유다).
    static void Rule(SerializedProperty rules, string name, GameObject prefab,
                     float perChunk, float spacing, float regrowDays, Biome[] biomes,
                     (int min, int max) cluster, int spread)
    {
        SerializedProperty e = null;
        for (int i = 0; i < rules.arraySize; i++)
        {
            var it = rules.GetArrayElementAtIndex(i);
            if (it.FindPropertyRelative("Name").stringValue == name) { e = it; break; }
        }
        if (e == null)
        {
            rules.InsertArrayElementAtIndex(rules.arraySize);
            e = rules.GetArrayElementAtIndex(rules.arraySize - 1);
        }

        e.FindPropertyRelative("Name").stringValue            = name;
        e.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        e.FindPropertyRelative("PerChunk").floatValue         = perChunk;
        e.FindPropertyRelative("MinSpacing").floatValue       = spacing;
        e.FindPropertyRelative("RegrowDays").floatValue       = regrowDays;
        e.FindPropertyRelative("RequireOpenTile").boolValue   = true;

        // 지하 바닥은 거의 평지라 경사·고도는 사실상 아무것도 안 거른다 — **거르는 것은
        // 바위다.** 그래도 값을 비워 두지 않는 이유: 코드 기본값(설선 400m)이 남아 있으면
        // 지하 높이에서 **전부 걸러져 하나도 안 난다.**
        e.FindPropertyRelative("MaxSlope").floatValue      = 35f;
        e.FindPropertyRelative("MinHeight").floatValue     = -1000f;
        e.FindPropertyRelative("MaxHeight").floatValue     =  1000f;
        e.FindPropertyRelative("ScaleRange").vector2Value  = new Vector2(0.85f, 1.25f);

        e.FindPropertyRelative("ClusterMin").intValue      = cluster.min;
        e.FindPropertyRelative("ClusterMax").intValue      = cluster.max;
        e.FindPropertyRelative("ClusterSpread").intValue   = spread;

        var arr = e.FindPropertyRelative("Biomes");
        arr.arraySize = biomes != null ? biomes.Length : 0;
        for (int i = 0; i < arr.arraySize; i++)
            arr.GetArrayElementAtIndex(i).enumValueIndex = (int)biomes[i];
    }
}
