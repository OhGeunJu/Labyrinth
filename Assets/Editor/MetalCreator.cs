using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **금속 사다리와 광맥 배선**(2026-09-12, 사용자 결정 — *"금속 체계를 다시 잡아야해"*).
//
// ## 왜 다시 잡나
//
// 증상이 셋이었는데 원인이 하나였다. **"철"에는 주괴가 없었다** — 다른 금속은 전부
// `광석 → 주괴 → 장비` 세 단계인데 철만 `광석 → 장비` 두 단계라, 제련 한 단계를
// 건너뛰면서 재료비가 1/5로 떨어졌다. 그래서 철 투구가 **강철 투구의 1/6 재료로**
// 방어 5/6을 줬고(재료비/방어 12 대 69), 청동은 주괴를 거치느라 옆에 서는 순간
// 바보가 됐으며(101), 철 갑옷(방어 9)이 강철 흉갑(방어 9)을 통째로 잡아먹었다.
//
// **그래서 철과 강철을 합친다** — 숫자만 다른 두 티어는 고를 이유가 없다.
//
// ## 규칙은 한 줄이다 — 광석 → 주괴 → 장비
//
// 상위 금속을 *아래 금속 + 마법 광물*의 합금으로 두는 길도 있었는데 접었다(사용자:
// *"미스릴이면 미스릴 광석 미스릴 주괴 이렇게 가야지"*). 합금으로 두면 **아래층에서
// 캔 것만으로 최고 금속이 나와** *깊이가 곧 티어*라는 축이 무너진다 — 새 층에 갈
// 이유가 사라진다. **금속마다 제 광석이 있고 그 광석은 제 층에서 난다.**
//
// 예외는 **청동 하나**이고 그건 정당하다 — 구리 + 주석은 실제로 합금이고, 첫 금속에
// *광석 둘을 모아야 한다*는 결이 붙는다.
//
// | 티어 | 층 | 광석 | 주괴 |
// |---|---|---|---|
// | 1 | 석회굴 · 동굴 | 구리 + 주석 | 청동 |
// | 2 | 폐광 · 균사림 | 철 | 강철 |
// | 3 | 수몰층 · 수정동 | **티타늄 광석** | **티타늄** |
// | 5 | 심연 | **미스릴 광석** | **미스릴** |
//
// **이름 셋을 갈아 끼웠다**(2026-09-12). 처음에 지어낸 *비전강·에테르강*은 사용자가
// **한 번도 들어 본 적이 없는 말**이었고(*"조금 들어봤을 만한걸로 해줘"*), 그다음 후보였던
// **플라스틸은 이 세계의 말이 아니었다** — 판에 있는 것이 *제련술·주조·야금학·명장 단조*
// 뿐인데 plastic + steel은 **없는 기술을 전제한다.** 지금 이름은 **청동 · 강철 · 티타늄 ·
// 미스릴**이고, 티타늄은 실제로 캐는 광석인 데다 이름이 **티탄**에서 왔다.
//
// **미스릴이 맨 위다**(사용자: *"플라스틸보다 미스릴이 좋은 거 아냐?"*).
// 처음엔 미스릴을 3층에 뒀는데 뒤집힌 순서였다 — 미스릴은 *금의 열 배 값*에 강철보다
// 단단하고 깃털처럼 가벼운 전설 금속이고, 티타늄은 SF의 **산업 자재**다. 그리고 맞바꾸니
// 결이 더 맞는다: **미스릴은 원래 가장 깊은 갱도에서 나왔고, 너무 깊이 판 것이 화를 불렀다.**
// (**테라리아는 반대다** — 거기선 티타늄이 미스릴 위다. 티어가 열넷이라 그런 것이고,
//  넷짜리 사다리에서는 제일 유명한 이름이 맨 위에 서는 편이 읽힌다.)
//
// **티타늄이 3층인 것에도 이유가 있다** — 실제로 *바닷물에 안 상하는* 금속이라
// 잠수함·해양 설비에 쓴다. 3층이 **수몰층**이다.
//
// **4층(용암층)에는 금속을 안 둔다.** 층마다 금속을 하나씩 두면 부위별 장비 한 벌이
// 다섯 벌이 되는데, 지금 데이터는 **부위를 다 채운 티어가 하나도 없다**(청동 4 · 은 1 ·
// 합금 2). 대신 용암층은 **석탄과 마법 광물의 층**이다 — 금속 사다리로는 안 내려가도
// 되지만 지팡이·결정 방패를 만들려면 가야 한다. 축이 둘로 갈린다.
//
// **마력석·에테르석·공허석은 금속에서 뺐다.** 그쪽은 이미 *결정 → 지팡이·결정 방패*라는
// 제 축을 갖고 있다(마력석 하나가 결정 여섯을 만든다). **금속은 장비의 몸, 마법 광물은
// 마법 장비의 심장** — 섞으면 지팡이 만들 마력석을 갑옷에 쓰게 된다.
//
// ## 그런데 먼저 고칠 것이 있었다 — 광맥이 지상 시절 배선 그대로다
//
// `BiomeContentData.ResourceNodes`가 *"이 바이옴의 벽을 파면 무엇이 나오나"*인데
// (`RockMap.VeinAt`), 지상 바이옴(Plains·Desert·Forest·Swamp·Tundra)에 걸려 있었다.
// 지하로 내려온 뒤로:
//
// - **구리·주석이 Plains·Desert에만** 붙어 있어 **지하에서 안 난다 → 청동을 아예 못 만들었다**
// - **금·에테르결정도 안 난다**(Desert·Swamp뿐)
// - **사파이어·토파즈·에메랄드는 어느 바이옴에도 없다**
// - 지하 여덟 층 중 **다섯(석회굴·폐광·수몰층·수정동·심연)은 풀이 비어 있어** 벽을 파도
//   **광맥이 아예 안 나온다**(`VeinAt`이 `pool.Count == 0`이면 `null`을 돌려준다)
//
// 그래서 **광맥 배선이 첫 삽이다.** 안 하면 새로 만든 미스릴·티타늄도 청동과 똑같이
// *"있는데 못 만드는 것"*이 된다.
//
// ## 어기면 조용히 틀리는 것
//
// - **석탄은 모든 층에 난다** — 제련 넷이 전부 석탄을 먹는데 지금은 **용암층(4층)에만**
//   났다. 2층에서 강철을 만들려고 4층까지 내려가야 하는 꼴이었다.
// - **아래 금속의 광석이 위층에도 난다** — 철이 2층에서 처음 나지만 3·5층에도 나야 계속
//   만들 수 있다. 깊이 들어갈수록 기본 재료가 끊기면 **내려가는 것이 벌칙**이 된다.
// - **구리와 주석은 반드시 같은 층에** — 둘 중 하나만 나는 층에서는 청동을 못 만든다.
// - **풀은 균등 추첨이다**(`pool[hash % pool.Count]`) — 목록에 **두 번 넣으면 두 배로**
//   나온다. 흔해야 하는 것(석탄·철)은 그렇게 적는다.
// - **버섯을 풀에 넣지 않는다** — 거기는 *벽 속*이라 바닥에 나야 할 것이 바위에 박힌다
//   (`MushroomCreator`가 먼저 겪고 빼냈다). 바닥에 나는 것은 뿌리기(`ScatterRule`)가 한다.
// - **은과 금은 장비를 안 만든다 — 교역품이다.** 장비 금속에서는 뺐지만(은 단검을 지웠다)
//   **광맥은 수정동에 남긴다**: 안 남기면 은 주괴가 *있는데 만들 재료가 안 나는 것*이 되어,
//   방금 청동에서 고친 그 함정을 다시 판다. 값비싼 물건이 하나는 있어야 상인이 성립한다.
//
// **다시 돌려도 된다** — 있으면 값만 맞춘다(guid 유지).
public static class MetalCreator
{
    const string PrefabDir = "Assets/3_Prefab/Node";
    const string NodeDir   = "Assets/5_Data/Resource";
    const string ItemDir   = "Assets/Resources/Data/Items";
    const string BiomeDir  = "Assets/Resources/Data/Biomes";
    const string MatDir    = "Assets/2_Material";

    [MenuItem("DungeonColony/World/B. Create Metal Ladder (금속 사다리 · 광맥 배선)")]
    static void Create()
    {
        // ── 광석과 주괴 ──────────────────────────────────────────────────
        //
        // **값은 사다리다.** 광석 12/14(구리·주석) → 20(철) → 45(티타늄) → 90(미스릴).
        // 주괴 값은 제련 배수 ~1.6에서 나온다(청동 91 · 강철 130이 이미 그 자리라 안 건드린다):
        // 티타늄 = (45x3 + 10x2) x 1.61 = 250 · 미스릴 = (90x3 + 10x2) x 1.62 = 470.
        var titaniumOre = Ore("Res_TitaniumOre", "티타늄 광석", 45,
            "물에 잠긴 층의 벽에 박힌 잿빛 광석. 물에 잠긴 채로도 녹이 슬지 않는다.");
        var mithrilOre = Ore("Res_MithrilOre", "미스릴 광석", 90,
            "은빛이 도는 무른 광석. 손에 얹으면 무게가 손을 속인다. 가장 깊은 곳에서만 나온다.");

        Ingot("Res_Titanium", "티타늄 주괴", 250, 1.6f,
            "티타늄 주괴. 강철보다 가볍고 단단한데, 무엇보다 물에도 습기에도 상하지 않는다.");
        var mithril = Ingot("Res_Mithril", "미스릴 주괴", 470, 1.0f,
            "미스릴 주괴. 이만한 강철의 반도 안 나가는데 더 단단하다. 끝에 있는 금속이다.");

        // **구리와 주석을 내린다 — 청동이 여기서 조였다**(2026-09-13).
        //
        // 청동 주괴 91은 강철 130의 0.70배인데 **나오는 방어는 5 대 9로 0.56배**였다.
        // 그래서 청동 장비가 방어 1점당 재료비 101로 **게임에서 가장 비쌌고**(강철 78),
        // 값을 올리면 *성능 대비 비싸고* 내리면 *만들수록 손해*가 되어 **둘 사이에 답이
        // 없었다.** 조인 것은 장비가 아니라 **입력**이다 — 광석 둘을 모아야 하는 최초의
        // 금속인데 그 둘이 철보다 비쌌다.
        //
        // 구리 12→8 · 주석 14→10이면 청동 주괴가 70이 되고(재료 44 x1.59),
        // 청동 흉갑의 재료비/방어가 80이 되어 **강철 78과 나란해진다.**
        Ore("Res_CopperOre", "구리 광석", 8,
            "붉은 기가 도는 무른 광석. 주석과 함께 녹이면 청동이 된다.");
        Ore("Res_TinOre", "주석 광석", 10,
            "잿빛의 무른 광석. 혼자서는 쓸 데가 없고 구리와 만나야 쓸모가 생긴다.");
        Ingot("Res_Bronze", "청동 주괴", 70, 2.0f,
            "구리와 주석을 녹여 부은 것. 사람이 처음 손에 쥔 금속이다.");

        RewireMasterSmithing(mithril);

        // ── 광맥 ─────────────────────────────────────────────────────────
        //
        // **노드 데이터를 먼저 만들고 프리팹을 뒤에 물린다** — 둘이 서로를 가리키기
        // 때문이다(`MushroomCreator`가 이 순서를 뒤집었다가 겪었다: 프리팹의
        // `ResourceNode.data`가 빈 채로 구워져 살펴보기 창이 날 프리팹 이름을 띄웠다).
        //
        // **게이지와 요구 숙련이 깊이를 따라 오른다** — 철 광맥이 150/8이다.
        var titaniumVein = Vein("Node_TitaniumVein", titaniumOre, gauge: 260f, skill: 18f,
            displayName: "티타늄 광맥",
            description: "물에 잠긴 층과 수정동의 벽에 잿빛 줄기로 뻗어 있다. 캐려면 솜씨가 든다.");
        var mithrilVein = Vein("Node_MithrilVein", mithrilOre, gauge: 400f, skill: 30f,
            displayName: "미스릴 광맥",
            description: "심연의 벽에서만 은빛 실처럼 뻗어 있다. 곡괭이가 튕겨 나가 오래 걸린다.");

        // **모양을 여기서 안 짓는다 — `VeinArt`가 답한다.** 갈라 두면 새 광맥만 다르게
        // 생기고, 그 차이가 *일부러 그런 것*인지 *빠뜨린 것*인지 화면에서 구별이 안 된다.
        // 색·모양은 `VeinArt.All` 표가 든다(둘 다 금속이라 둥근 바위에 알갱이).
        titaniumVein.Prefab = VeinArt.BuildOne("Node_TitaniumVein", titaniumVein);
        mithrilVein.Prefab  = VeinArt.BuildOne("Node_MithrilVein",  mithrilVein);
        EditorUtility.SetDirty(mithrilVein);
        EditorUtility.SetDirty(titaniumVein);

        WireBiomes(titaniumVein, mithrilVein);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[금속] 미스릴·티타늄을 만들고 지하 여덟 층의 광맥 풀을 다시 배선했습니다. " +
                  "이어서 `World → 9. Set Wild Yields`를 돌려 야생 매장량을 규칙에서 다시 내세요.");
    }

    // ── 옛 합금을 티타늄로 ────────────────────────────────────────────────

    // **명장 단조가 아직 옛 합금 주괴를 재료로 물고 있다.**
    //
    // `res_alloy`(강철2 + 은1 + 석탄2)는 지웠다 — 제 광석이 없는 금속이었고, *광석 → 주괴 →
    // 장비*로 규칙을 한 줄로 만들면서 **미스릴이 그 자리를 받았다**(합금 흉갑·대검 둘이
    // 이미 최상위 장비로 있어서, 새로 만드는 대신 개명했다).
    //
    // **문자열 `ItemId`가 아니라 SO 참조로 바꾼다** — `Res_Titanium`은 이제 SO라 guid로
    // 가리킬 수 있고, 그러면 이름을 바꾸는 날 조용히 끊기지 않는다(`ItemId` 오타는 화면에
    // *"재료 없음"*으로만 나타난다). **둘 다 비워 두면 재료가 통째로 사라지므로
    // 반드시 한쪽은 채울 것.**
    static void RewireMasterSmithing(ItemData top)
    {
        const string path = "Assets/Resources/Data/Research/Res_MasterSmithing.asset";
        var r = AssetDatabase.LoadAssetAtPath<ResearchData>(path);
        if (r == null) { Debug.LogWarning("[금속] 명장 단조를 못 찾았습니다."); return; }

        bool touched = false;
        for (int i = 0; i < r.Ingredients.Count; i++)
        {
            var e = r.Ingredients[i];
            if (e.ItemId != "res_alloy") continue;

            e.ItemId        = null;
            e.Item          = top;
            r.Ingredients[i] = e;   // 구조체라 되돌려 넣어야 한다
            touched = true;
        }

        if (!touched) return;
        EditorUtility.SetDirty(r);
        Debug.Log("[금속] 명장 단조의 재료를 합금 주괴 → 미스릴 주괴로 옮겼습니다.");
    }

    // ── 바이옴 배선 ─────────────────────────────────────────────────────────

    // **풀을 통째로 다시 쓴다 — 더하지 않는다.** 더하면 지상 시절의 배선(은 광맥 등)이
    // 남아 다시 돌릴 때마다 목록이 길어지고, 무엇이 의도이고 무엇이 잔재인지 못 가린다.
    static void WireBiomes(ResourceNodeData titanium, ResourceNodeData mithril)
    {
        var stone  = Node("Stone");
        var coal   = Node("Node_CoalSeam");
        var copper = Node("Node_CopperVein");
        var tin    = Node("Node_TinVein");
        var iron   = Node("Node_IronVein");
        var gold   = Node("Node_GoldVein");
        var silver = Node("Node_SilverVein");
        var ruby   = Node("Node_RubyVein");
        var sapph  = Node("Node_SapphireVein");
        var topaz  = Node("Node_TopazVein");
        var emer   = Node("Node_EmeraldVein");
        var magic  = Node("Node_MagicCrystal");
        var ether  = Node("Node_EtherCrystal");
        var voidc  = Node("Node_VoidCrystal");

        // **석탄과 돌이 어느 층에나 있다** — 제련 전부가 석탄을 먹고 돌은 건물 서른여덟
        // 개의 재료다. 깊이로 잠글 것이 아니다.
        //
        // **같은 것을 두 번 적으면 두 배로 나온다**(균등 추첨) — 흔해야 하는 것을 그렇게 적는다.
        Pool(Biome.Limestone, stone, stone, coal, copper, tin);
        Pool(Biome.Cavern,    stone, coal, copper, tin, iron);

        Pool(Biome.Mine,      iron, iron, coal, coal, stone, copper);
        Pool(Biome.Fungal,    iron, coal, stone, sapph);

        Pool(Biome.Flooded,   titanium, iron, coal, stone, sapph, emer);
        Pool(Biome.Crystal,   titanium, titanium, magic, ruby, sapph, topaz, emer, gold, silver);

        // 4층은 금속이 없다 — **연료와 마법 광물의 층**이다.
        Pool(Biome.Lava,      coal, coal, ether, magic, ruby, gold, iron);

        // 5층 — 미스릴은 여기서만 난다. 티타늄도 나야 **내려와서도 계속 만들 수 있다.**
        Pool(Biome.Abyss,     mithril, mithril, voidc, ether, titanium, coal);
    }

    static void Pool(Biome b, params ResourceNodeData[] nodes)
    {
        string path = BiomeDir + "/Biome_" + b + ".asset";
        var data = AssetDatabase.LoadAssetAtPath<BiomeContentData>(path);
        if (data == null) { Debug.LogWarning($"[금속] {path}를 못 찾았습니다."); return; }

        var list = new List<ResourceNodeData>();
        foreach (var n in nodes)
        {
            // **못 찾은 노드를 조용히 건너뛰지 않는다** — 빠진 광맥은 화면에 아무 표시도
            // 안 나고 *"저 층에서는 그게 안 나오네"*로만 보인다.
            if (n == null) { Debug.LogWarning($"[금속] {b}: 못 찾은 광맥이 있어 건너뜁니다."); continue; }
            list.Add(n);
        }

        data.ResourceNodes = list;
        EditorUtility.SetDirty(data);
    }

    static ResourceNodeData Node(string name) =>
        AssetDatabase.LoadAssetAtPath<ResourceNodeData>(NodeDir + "/" + name + ".asset");

    // ── 에셋 ────────────────────────────────────────────────────────────────

    // **광석·주괴는 SO다** — `ResourceNodeData.DropItem`과 제련 레시피(`Rec_*.asset`)가
    // **guid로** 가리키므로 JSON 아이템으로 두면 가리킬 방법이 없다(`CLAUDE.md`의
    // *"기준은 다른 에셋이 GUID로 가리켜야 하는가"*).
    static ResourceItemData Ore(string id, string name, int value, string desc) =>
        Res(id, name, value, weight: 1f, group: ResourceGroup.Ore, desc: desc);

    static ResourceItemData Ingot(string id, string name, int value, float weight, string desc) =>
        Res(id, name, value, weight, group: ResourceGroup.Ingot, desc: desc);

    static ResourceItemData Res(string id, string name, int value, float weight,
                                ResourceGroup group, string desc)
    {
        string path = ItemDir + "/" + id + ".asset";
        var r = AssetDatabase.LoadAssetAtPath<ResourceItemData>(path);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<ResourceItemData>();
            AssetDatabase.CreateAsset(r, path);
        }

        // **`Id`를 적어 둔다.** 비우면 에셋 이름이 답하지만(`Res_Steel`이 그렇다),
        // 그러면 파일을 옮기는 날 조용히 신원이 바뀐다.
        r.Id          = id;
        r.ItemName    = name;
        r.Description = desc;
        r.Weight      = weight;
        r.Width       = 1;
        r.Height      = 1;
        r.BaseValue   = value;
        r.Group       = group;
        r.Stack       = 200;
        EditorUtility.SetDirty(r);
        return r;
    }

    static ResourceNodeData Vein(string name, ItemData drop, float gauge, float skill,
                                 string displayName, string description)
    {
        string path = NodeDir + "/" + name + ".asset";
        var n = AssetDatabase.LoadAssetAtPath<ResourceNodeData>(path);
        if (n == null)
        {
            n = ScriptableObject.CreateInstance<ResourceNodeData>();
            AssetDatabase.CreateAsset(n, path);
        }

        n.DropItem      = drop;
        n.GaugeRequired = gauge;
        n.DropMin       = 1;
        n.DropMax       = 2;
        n.RequiredSkill = skill;
        n.MaxWorkers    = 3;
        // **야생은 유한하다.** 여기 적는 것은 임시값이고 **규칙이 정본이다** —
        // `World → 9. Set Wild Yields`가 `900/게이지`로 다시 낸다(손으로 적으면
        // 새 노드를 만드는 날 그것만 무한으로 남는다).
        n.TotalYields   = Mathf.Clamp(Mathf.RoundToInt(900f / gauge), 3, 12);
        n.DisplayName   = displayName;
        n.Description   = description;
        EditorUtility.SetDirty(n);
        return n;
    }
}
