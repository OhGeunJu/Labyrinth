using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **연구를 스물아홉에서 마흔다섯으로 늘린다**(2026-08-25, 사용자 요청 *"관련 없는 연구끼리
// 연결해놓지 말고 차라리 템을 더 만들어서 연구를 더 만들어"*).
//
// ## 왜 노드를 더 놓는가
//
// 08-24에 자리를 저작으로 돌리고 나니 그래프가 **가로로만 길고 아래가 텅 비었다**(8열 × 12행
// 96칸 중 29칸 = 30%). 간격을 좁히는 것으로는 안 풀린다 — **빈 것은 간격이 아니라 칸**이다.
//
// 그렇다고 빈칸을 메우려고 아무 연구나 만들면 **그건 저작이 아니라 벽지다**(08-24에 잎 연구
// 다섯을 만들며 적어 둔 그대로다). 그래서 여기 열일곱은 **전부 새 물건을 연다** — 아이템
// 스물다섯 · 레시피 스물여섯 · **새 건물 셋**(`FarmFurnitureCreator`)이 같이 들어왔고,
// 여는 것이 없는 연구는 만들지 않았다.
//
// ## 관련된 것끼리만 잇는다
//
// - **무기 제작을 제련술 아래로** 넣었다. 철 장비를 만드는 연구가 **뿌리로 떠 있어서** 선이
//   하나도 안 붙었다 — 화면에서 붕 떠 보이던 셋 중 하나다.
// - **골각 세공 → 가죽 세공을 끊었다.** 뼈를 다루는 손과 가죽을 다루는 손은 같은 짐승에서
//   나올 뿐 같은 기술이 아니다. 가죽 세공은 뿌리가 되고, 대신 **사슬 세공이 그 자식**이다
//   (사슬은 가죽 받침 위에 입는다 — 레시피 재료가 그대로 그렇다).
// - **광맥 이식은 뿌리로 둔다.** 제련술 아래로 넣으면 두 덩어리가 이어져 보기는 좋지만, 선이
//   **네 열을 건너뛰어** 그 사이 열의 노드를 세로 선이 관통한다. 자식을 열넷 거느리므로
//   떠 보이지도 않는다.
//
// **다시 돌려도 안전하다** — 있으면 값만 맞춘다(지우고 새로 만들면 guid가 바뀐다).
// 자리는 `ResearchGraphPositions`가 따로 든다(답이 두 벌이 되지 않게).
public static class TechTreeCreator
{
    const string Dir      = "Assets/Resources/Data/Research";
    const string ItemDir  = "Assets/Resources/Data/Items";
    const string BuildDir = "Assets/Resources/Data/Buildings";

    readonly struct Node
    {
        public readonly string   Name, Desc;
        public readonly float    Time;
        public readonly string[] Prereqs;
        public readonly (string id, int amount)[] Cost;
        public Node(string name, string desc, float time, string[] prereqs,
                    params (string, int)[] cost)
        { Name = name; Desc = desc; Time = time; Prereqs = prereqs; Cost = cost; }
    }

    static readonly string[] None = new string[0];

    // ── 새 연구 열일곱 ───────────────────────────────────────────────────────
    static readonly Dictionary<string, Node> New = new()
    {
        // 제련 갈래
        // **제강술을 여기 흡수했다**(2026-08-25, 사용자 *"주조랑 제강술을 하나로 합쳐서 그냥
        // 주조라고 네이밍을 해도 되나? 거기에 방어구 넣으면 딱인데"*).
        //
        // 제강술이 실제로 잠그던 것은 **셋뿐**이었다(강철 흉갑·투구·대검) — 강철 주괴는 원래
        // 무연구였고, 대검은 무기라 `정밀 단조`로 갔다. 남은 둘이 갑옷이니 **주조가 곧
        // 금속 갑옷의 자리**가 되고, `갑주 제작대`가 여기 달린다.
        ["Res_Casting"] = new("주조",
            "쇳물을 다룬다 — 틀에 부어 굳히고, 두드려 펴고, 강철로 벼린다. 몸에 두르는 쇠는 전부 여기서 나온다.",
            850f, new[]{ "Res_Smelting" }, ("Res_Bronze", 10), ("Res_Steel", 6), ("Res_Coal", 12)),

        ["Res_Platemail"] = new("판금 갑옷",
            "가슴과 머리만으로는 부족하다. 팔다리까지 판으로 덮는다.",
            1000f, new[]{ "Res_Casting" }, ("Res_Steel", 12), ("res_hardleather", 4)),

        ["Res_FineForging"] = new("정밀 단조",
            "같은 강철이라도 두드린 횟수가 다르면 다른 물건이 된다. 짧은 날과 무거운 머리를 벼른다.",
            1050f, new[]{ "Res_WeaponSmithing", "Res_Casting" }, ("Res_Steel", 10), ("Res_Coal", 12)),

        ["Res_Metallurgy"] = new("야금학",
            "무엇을 얼마나 섞으면 어떻게 되는지. 쇠를 다루는 일이 손재주에서 셈으로 넘어간다.",
            1150f, new[]{ "Res_PreciousMetals", "Res_Casting" },
            ("Res_SilverIngot", 6), ("Res_Steel", 8), ("Res_Coal", 10)),

        ["Res_MasterSmithing"] = new("명장 단조",
            "합금을 정밀하게 벼른다. 한 자루에 광산 하나가 들어가고, 그만한 값을 한다.",
            1500f, new[]{ "Res_Metallurgy", "Res_FineForging" }, ("res_alloy", 6), ("Res_GoldIngot", 3)),

        // 사슬은 **가죽 받침 위에** 입는다 — 레시피 재료가 그대로 그렇다(강철 + 질긴 가죽).
        ["Res_Chainmail"] = new("사슬 세공",
            "고리를 하나씩 이어 짠다. 베는 것에는 강하고 두들기는 것에는 약하다.",
            1000f, new[]{ "Res_Casting" }, ("Res_Steel", 8), ("res_hardleather", 6)),

        // 목공 갈래
        ["Res_Plywood"] = new("합판",
            "결을 엇갈려 붙이고 눌러 굳힌다. 통나무보다 얇은데 잘 안 부러진다.",
            400f, new[]{ "Res_Woodworking" }, ("Res_Lumber", 20), ("rope", 4)),

        ["Res_CompositeBow"] = new("복합궁",
            "뿔과 나무와 힘줄을 겹쳐 감는다. 짧은데 장궁만큼 나간다.",
            650f, new[]{ "Res_Bowyer", "Res_Plywood" }, ("res_plywood", 8), ("Res_Bone", 6)),

        // 석공 갈래
        // **하나가 셋을 다 연다**(2026-08-25, 사용자 *"굳이 여러개로 나눌 필요 없이 하나를
        // 하면 다 완성되도 되잖아 / 쓸데없이 여러개로 나누지마"*).
        //
        // 건축학 → 관개 → 온실로 쪼개 놨었는데, **셋이 건물 하나씩을 열 뿐 그 사이에 고를
        // 것이 없었다** — 순서가 정해져 있으니 세 번 눌러야 하는 한 줄이지 갈래가 아니다.
        // 깊이는 *다음에 무엇을 할까*가 갈릴 때만 값이 있다.
        ["Res_Architecture"] = new("건축학",
            "무엇을 어디에 받쳐야 무너지지 않는가. 큰 창고를 올리고, 물길을 내고, 밭에 천을 씌운다.",
            750f, new[]{ "Res_Stonecutting" }, ("res_brick", 24), ("Res_Stone", 20), ("cloth", 10)),

        ["Res_CrystalWorking"] = new("결정 세공",
            "결정 여럿을 물려 서로를 울리게 한다. 하나만 박은 것과는 다른 물건이 나온다.",
            1300f, new[]{ "Res_ElementalCraft" }, ("crystal_arcane", 4), ("Res_Emerald", 3)),

        // 섬유·가죽·뼈
        // **가죽 세공을 여기 흡수했다**(2026-08-25, 사용자 *"재봉이랑 가죽세공을 합쳐도 될 거
        // 같고"*). 천을 누비는 것과 가죽을 눌러 굳히는 것은 **둘 다 실과 바늘이 닿는 일**이고,
        // 갈라 두니 가죽 세공이 자식 하나(골각 방어구)만 거느린 뿌리로 떠 있었다.
        ["Res_Tailoring"] = new("재봉",
            "천을 겹쳐 누비고 가죽을 눌러 굳힌다. 실과 바늘이 닿는 것은 전부 여기서 나온다.",
            500f, new[]{ "Res_Weaving" }, ("cloth", 16), ("leather", 8)),

        ["Res_BoneArmor"] = new("골각 방어구",
            "굵은 뼈를 잘라 가죽에 덧댄다. 값은 산 사람이 아니라 시체가 낸다.",
            450f, new[]{ "Res_Bonecraft" }, ("Res_Bone", 18), ("leather", 6)),

        // 연금 갈래
        ["Res_Refining"] = new("정제술",
            "정수를 다시 졸여 낸다. 한 방울이 한 병을 대신한다.",
            700f, new[]{ "Res_Alchemy" }, ("alch_essence", 12), ("herbs", 20)),

        ["Res_Surgery"] = new("수술",
            "감는 것으로 안 되는 것이 있다. 열고, 꺼내고, 다시 꿰맨다.",
            900f, new[]{ "Res_Medicine", "Res_Refining" }, ("res_refined_essence", 3), ("med_kit", 4)),

        // 조리 갈래
        ["Res_Baking"] = new("제빵",
            "가루를 반죽해 굽는다. 낟알을 그냥 씹던 것에서 한 걸음.",
            350f, new[]{ "Res_Cooking" }, ("flour", 12)),

        // **여기도 하나로 모았다** — 절임·맥주·건량이 각각 한 칸씩이었는데 셋 다 *오래 가게
        // 만드는 법*이고, 사이에 고를 것이 없었다. **원정이 이 연구의 이유다**: 등짐은 무게로
        // 세므로(`CarryCapacity`) 가벼운 고영양 식량이 그만큼 다른 것을 실을 자리가 된다.
        ["Res_Preserving"] = new("보존식",
            "소금물에 담그고, 띄우고, 말려 눌러 둔다. 맛을 잃는 대신 시간을 얻는다.",
            550f, new[]{ "Res_Cooking" }, ("vegetables", 20), ("flour", 12)),

        // 가구는 **합판 아래** — 널을 켤 줄 알아야 짜는 물건이다.
        // (관개 수로·온실 밭은 위 `건축학`이 같이 연다.)
        ["Res_Furniture"] = new("가구 공방",
            "널을 켜서 짜 맞춘다. 사람이 눕는 자리부터 달라진다.",
            600f, new[]{ "Res_Plywood" }, ("res_plywood", 10), ("cloth", 8)),

    };

    // ── 이미 있는 것의 선행을 고쳐 잡는다 ────────────────────────────────────
    //
    // 값을 통째로 덮으므로 **비운 것은 뿌리가 된다**.
    static readonly Dictionary<string, string[]> Rewire = new()
    {
        ["Res_WeaponSmithing"] = new[]{ "Res_Smelting" },     // 떠 있던 뿌리를 제자리로

        // ── 등뼈: 나무 → 돌 → 쇠 · 불 → 약 (2026-08-25) ──────────────────────
        //
        // *"제련술 석공술 세공 이런 것들은 좀 뒤에 있고 조리법 목공술 직조 이런 것들이 앞에
        // 있어야 하는 거 아니야"*(사용자). 맞다 — **뿌리가 여덟이라 전부 첫날부터 열렸다.**
        // 나무를 켤 줄도 모르는데 제련술을 시작할 수 있었고, 그러면 연구가 스물아홉이든
        // 쉰하나든 **순서라는 것이 없다.**
        //
        // 세 줄로 축이 선다: **자루를 끼운 도구라야 돌을 쫀다**(석공술 ← 목공술) ·
        // **가마를 쌓아야 쇠를 녹인다**(제련술 ← 석공술) · **끓이고 졸이는 데서 약이
        // 나온다**(연금술 ← 조리법).
        //
        // **뿌리는 여섯만 남는다** — 목공·조리·직조·가죽·뼈(손과 불만 있으면 되는 것)와
        // 광맥 이식. 나머지는 전부 무언가의 다음이다.
        ["Res_Stonecutting"]   = new[]{ "Res_Woodworking" },
        ["Res_Smelting"]       = new[]{ "Res_Stonecutting" },
        ["Res_Alchemy"]        = new[]{ "Res_Cooking" },
    };

    // ── 뜻이 바뀐 기존 연구의 이름·설명 ──────────────────────────────────────
    //
    // 무언가를 흡수하면 **설명이 거짓말이 된다** — 석공술이 벽돌까지 굽는데 설명은 여전히
    // *"돌을 쪼갠다"*뿐이면, 벽돌을 어디서 여는지 화면 어디에도 안 나온다.
    // `Rewire`가 선행을 고치듯 여기는 글을 고친다(선행은 안 건드린다).
    static readonly Dictionary<string, (string Name, string Desc)> Retouch = new()
    {
        // **석공술을 알면 벽돌은 딸려 온다**(2026-08-25, 사용자 *"석공술을 알면 벽돌 굽기같은
        // 거는 같이 아는 게 당연한 거 아닐까"*) — 둘 다 *돌과 흙을 다루는 한 가지 손*이다.
        ["Res_Stonecutting"] = ("석공술",
            "돌을 결대로 쪼개고, 흙을 다져 굽는다. 다듬은 돌과 벽돌이 여기서 나온다."),

        // **조리법이 아니라 불 다루기다**(2026-08-25, 사용자 *"조리법에서 연금술이 가는게
        // 뭔가 말이 안돼 — 차라리 화로? 이런거였으면 그다음에 조리법이랑 연금술이 있으면
        // 이해가 되잖아"*).
        //
        // 맞는 지적인데, **노드를 하나 더 놓는 대신 이 노드의 뜻을 넓혔다** — 어제 *"쓸데없이
        // 여러개로 나누지마"*라고 한 것과 안 부딪히게. 불을 다룰 줄 아는 것이 먼저고 그다음이
        // 끓이는 일(제빵·보존식)과 졸이는 일(연금술)이다. **여는 것은 그대로**(요리대와 기본
        // 요리 일곱) — 불로 하는 가장 기본적인 일이라 자리가 맞다.
        ["Res_Cooking"] = ("불 다루기",
            "불을 지피고 다스린다. 끓이고 굽고 졸이는 것이 전부 여기서 시작한다."),
    };

    // ── 도구가 안 만든 연구의 시간 ───────────────────────────────────────────
    //
    // *"원소 공학 90초 띡 할거면 연구에 왜 있어 그냥 되게 해야지"*(사용자, 2026-08-25).
    //
    // 맞다 — 지팡이 일곱을 여는 깊이 4짜리가 **90초**였다. 그리고 그것만이 아니었다:
    // **골각 세공이 뿌리인데 720초**(다른 뿌리는 250~300)고, **루비·사파이어·토파즈가
    // 부모인 금 광맥보다 쌌다.**
    //
    // ## 규칙 하나: 자식은 부모보다 비싸다
    //
    // 어기면 **깊은 것이 더 싸다** — 그러면 그건 연구가 아니라 클릭이다. 곡선은 깊이를 따른다:
    // 뿌리 250~350 · 1단 350~550 · 2단 600~750 · 3단 800~900 · 4단 1000~1150 · 5단 1300~1500.
    // (광맥은 일곱 단이라 제 곡선을 쓴다 — 120에서 1100까지.)
    //
    // 여기는 **도구가 만들지 않는 연구**의 시간만 든다(`New`에 있는 것은 거기 인라인 값이
    // 답한다 — 답이 두 벌이 되지 않게).
    static readonly Dictionary<string, float> Retime = new()
    {
        ["Res_Cooking"]        = 250f,   // 뿌리
        ["Res_Woodworking"]    = 300f,   // 뿌리
        ["Res_Bonecraft"]      = 350f,   // 뿌리 — 해골 소환까지 여므로 뿌리 중 제일 비싸다
        ["Res_Stonecutting"]   = 400f,
        ["Res_Alchemy"]        = 550f,
        ["Res_Smelting"]       = 600f,
        ["Res_WeaponSmithing"] = 800f,
        ["Res_PreciousMetals"] = 850f,
        ["Res_ElementalCraft"] = 1000f,  // 지팡이 일곱을 연다 — 90초였다
    };

    // ── 연구가 여는 건물 ─────────────────────────────────────────────────────
    //
    // 레시피는 잠갔는데 **그 레시피를 쓰는 작업대가 안 잠겨 있으면** 지어 놓고 아무것도 못
    // 만드는 건물이 된다. 보석 연마대가 정확히 그 꼴이었다 — 원소 결정 일곱이 전부 보석
    // 세공에 잠겨 있는데 연마대는 처음부터 지어졌다.
    static readonly Dictionary<string, string> BuildingGates = new()
    {
        ["CraftingTable_Gemcut"] = "Res_Gemcutting",
        ["LargeStorage"]         = "Res_Architecture",

        // **작업대 하나가 가죽 모자부터 강철 투구까지 만들던 것을 갈랐다**(2026-08-25).
        // 하나였을 때는 **무엇으로 잠글지가 성립하지 않았다** — 재봉으로 열면 강철 투구가
        // 딸려 오고, 주조로 열면 초반에 가죽옷을 못 만든다.
        ["CraftingTable_Armor"]  = "Res_Casting",      // 갑주 제작대 — 금속 갑옷
        ["CraftingTable_Tailor"] = "Res_Weaving",      // 재봉대 — 가죽·천 갑옷
    };

    [MenuItem("DungeonColony/Create Tech Tree")]
    static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) { Debug.LogError($"[기술 트리] {Dir} 가 없습니다."); return; }

        foreach (var kv in New)
        {
            var r = Upsert(kv.Key);
            r.ResearchName      = kv.Value.Name;
            r.Description       = kv.Value.Desc;
            r.ResearchTime      = kv.Value.Time;
            r.SkillGrowthAmount = 1f;
        }
        AssetDatabase.SaveAssets();

        int wired = 0;
        foreach (var kv in New)
        {
            var r = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{kv.Key}.asset");
            if (r == null) continue;

            // **에셋이면 참조로, 아니면 ItemId로 적는다** — 에디터에서는 JSON 레지스트리가
            // 안 서 있어 `DataManager.GetItem`이 전부 null을 준다(08-25에 재료가 통째로
            // 비어 있던 그 자리다. 값이 0이면 화면에 *"재료 없음"*으로 떠서 안 드러난다).
            r.Ingredients.Clear();
            foreach (var (id, amount) in kv.Value.Cost)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemDir}/{id}.asset");
                r.Ingredients.Add(asset != null
                    ? new ResearchData.Ingredient { Item = asset, Amount = amount }
                    : new ResearchData.Ingredient { ItemId = id,  Amount = amount });
            }

            r.Prerequisites.Clear();
            foreach (var p in kv.Value.Prereqs)
            {
                var pre = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{p}.asset");
                if (pre != null) { r.Prerequisites.Add(pre); wired++; }
                else Debug.LogWarning($"[기술 트리] 선행을 못 찾았습니다 — {kv.Key} ← {p}");
            }
            EditorUtility.SetDirty(r);
        }

        foreach (var kv in Rewire)
        {
            var r = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{kv.Key}.asset");
            if (r == null) { Debug.LogWarning($"[기술 트리] 고칠 연구가 없습니다 — {kv.Key}"); continue; }
            r.Prerequisites.Clear();
            foreach (var p in kv.Value)
            {
                var pre = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{p}.asset");
                if (pre != null) r.Prerequisites.Add(pre);
                else Debug.LogWarning($"[기술 트리] 선행을 못 찾았습니다 — {kv.Key} ← {p}");
            }
            EditorUtility.SetDirty(r);
        }

        foreach (var kv in Retime)
        {
            var r = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{kv.Key}.asset");
            if (r == null) { Debug.LogWarning($"[기술 트리] 시간을 고칠 연구가 없습니다 — {kv.Key}"); continue; }
            r.ResearchTime = kv.Value;
            EditorUtility.SetDirty(r);
        }

        foreach (var kv in Retouch)
        {
            var r = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{kv.Key}.asset");
            if (r == null) { Debug.LogWarning($"[기술 트리] 글을 고칠 연구가 없습니다 — {kv.Key}"); continue; }
            r.ResearchName = kv.Value.Name;
            r.Description  = kv.Value.Desc;
            EditorUtility.SetDirty(r);
        }

        int gated = 0;
        foreach (var kv in BuildingGates)
        {
            var b = AssetDatabase.LoadAssetAtPath<BuildingData>($"{BuildDir}/{kv.Key}.asset");
            var r = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{kv.Value}.asset");
            if (b == null || r == null)
            { Debug.LogWarning($"[기술 트리] 건물 잠금 실패 — {kv.Key} ← {kv.Value}"); continue; }
            b.RequiredResearch = r;
            EditorUtility.SetDirty(b);
            gated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[기술 트리] 새 연구 {New.Count}개 · 선행 {wired}개 · 다시 이은 것 {Rewire.Count}개 · "
                + $"건물 잠금 {gated}개. 자리는 `Setup Research Graph Positions`가 넣습니다.");
    }

    static ResearchData Upsert(string assetName)
    {
        string path = $"{Dir}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ResearchData>(path);
        if (existing != null) return existing;
        var created = ScriptableObject.CreateInstance<ResearchData>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }
}
