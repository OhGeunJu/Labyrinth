using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **빈 곳을 채우는 잎 연구 다섯**(2026-08-24, 사용자 요청 *"다음 연구가 없는 그냥 한칸짜리를
// 넣어놓으면 좀더 채워지고 좋을 거 같은데"*).
//
// 자리를 저작으로 돌리고 나니 그래프가 **가로로만 길고 아래가 텅 비었다.** 채우는 값은
// 노드를 더 놓는 것이지만, **빈칸을 메우려고 아무 연구나 만들면 그건 저작이 아니라 벽지다.**
// 그래서 **이미 있는데 아무 조건 없이 열려 있던 레시피 스물다섯**에서 골랐다 — 만들 수 있는
// 것이 늘어나는 게 아니라 **언제부터 만들 수 있나가 생긴다.**
//
// ## 기본은 안 잠근다
//
// 다섯 다 **윗단만** 잠근다: 가죽은 그대로 무두질하되 **경화 가죽**만, 천·넝마·붕대는 그대로고
// **천 갑옷·밧줄**만, 붕대는 그대로고 **약초 습포·구급낭**만. 기본을 잠그면 그건 깊이가 아니라
// **초반에 아무것도 못 하는 것**이다(창고를 연구로 잠그는 것과 같은 사고).
//
// **다시 돌려도 안전하다** — 있으면 값만 맞춘다(지우고 새로 만들면 guid가 바뀐다).
// 자리는 `ResearchGraphPositions`가 따로 든다(답이 두 벌이 되지 않게).
public static class CraftResearchCreator
{
    const string Dir = "Assets/Resources/Data/Research";

    readonly struct Node
    {
        public readonly string Name, Desc;
        public readonly float  Time;
        public readonly string Prereq;                 // 없으면 null
        public readonly (string id, int amount)[] Cost;
        public Node(string name, string desc, float time, string prereq,
                    params (string, int)[] cost)
        { Name = name; Desc = desc; Time = time; Prereq = prereq; Cost = cost; }
    }

    static readonly Dictionary<string, Node> Table = new()
    {
        ["Res_Weaving"] = new("직조",
            "실을 꼬아 밧줄을 놓고 천을 짠다. 찢어 쓰던 것을 이제 짜서 쓴다.",
            250f, null, ("cloth", 10)),

        ["Res_Medicine"] = new("의술",
            "약초가 무엇에 듣는지, 얼마나 써야 하는지. 감는 것과 고치는 것은 다르다.",
            650f, "Res_Alchemy", ("herbs", 12), ("med_bandage", 4)),

        // **석공술 다음이다** — 돌을 쪼개 본 손이 원석의 결을 읽는다.
        ["Res_Gemcutting"] = new("보석 세공",
            "원석의 결을 읽어 깎아 낸다. 잘못 치면 가루가 되고, 잘 치면 힘을 머금는다.",
            650f, "Res_Stonecutting", ("Res_Ruby", 3), ("Res_MagicStone", 3)),

        // **재료에 밧줄을 안 넣는다** — 밧줄은 `직조`가 여는데, 그걸 요구하면 *활을 만들려면
        // 직조가 필요하고 직조를 하려면...* 하는 순환처럼 읽힌다. 실제 의존은 **레시피 재료**로
        // 이미 서 있다(활 셋이 전부 밧줄을 쓴다) — 연구까지 겹쳐 걸 이유가 없다.
        ["Res_Bowyer"] = new("궁술 공방",
            "휘어도 부러지지 않는 나무를 골라 시위를 건다.",
            400f, "Res_Woodworking", ("Res_Lumber", 12)),
    };

    [MenuItem("DungeonColony/Create Craft Research")]
    static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) { Debug.LogError($"[공방 연구] {Dir} 가 없습니다."); return; }

        var made = new Dictionary<string, ResearchData>();
        foreach (var kv in Table)
        {
            var r = Upsert(kv.Key);
            r.ResearchName      = kv.Value.Name;
            r.Description       = kv.Value.Desc;
            r.ResearchTime      = kv.Value.Time;
            r.SkillGrowthAmount = 1f;
            made[kv.Key] = r;
        }

        foreach (var kv in Table)
        {
            var r = made[kv.Key];

            // **에셋이면 참조로, 아니면 `ItemId`로 적는다.**
            //
            // 처음엔 `DataManager.GetItem(id)` 하나로 찾았는데 **에디터에서는 JSON 레지스트리가
            // 안 서 있어 전부 null이 됐다** — 재료가 통째로 빈 연구 다섯이 조용히 만들어졌고,
            // 화면에는 *"재료 없음"*으로 떠서 **틀린 줄도 몰랐다.**
            //
            // 이 다섯의 재료 일곱 중 셋만 SO고(`Res_Lumber`·`Res_Ruby`·`Res_MagicStone`)
            // 나머지 넷(천·약초·붕대·두꺼운 가죽)은 JSON이다. 그쪽은 `ItemId`로 적어 두면
            // **런타임에 `Resolve()`가 찾는다**(`MerchantPoolEntry`와 같은 규약).
            r.Ingredients.Clear();
            foreach (var (id, amount) in kv.Value.Cost)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/Resources/Data/Items/{id}.asset");
                if (asset != null)
                    r.Ingredients.Add(new ResearchData.Ingredient { Item = asset, Amount = amount });
                else
                    r.Ingredients.Add(new ResearchData.Ingredient { ItemId = id, Amount = amount });
            }

            r.Prerequisites.Clear();
            if (kv.Value.Prereq != null)
            {
                var pre = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{kv.Value.Prereq}.asset");
                if (pre != null) r.Prerequisites.Add(pre);
                else Debug.LogWarning($"[공방 연구] 선행을 못 찾았습니다 — {kv.Value.Prereq}");
            }
            EditorUtility.SetDirty(r);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[공방 연구] {made.Count}개를 만들었습니다. "
                + "자리는 `Setup Research Graph Positions`가 넣습니다.");
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
