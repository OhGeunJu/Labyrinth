using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 광맥 열셋을 연구로 잠근다(2026-08-24, 사용자 결정 "광맥들도 연구로 뚫게 해줘").
//
// **광맥마다 연구 하나다** — 묶으면 순차적으로 발전해 가는 계단이 사라진다(`CLAUDE.md`).
// 그래서 열셋 + 뿌리 하나 = **연구 열넷**이고, 그 모양은 사슬이 아니라 **그래프**다
// (`ResearchData.Prerequisites`가 리스트라 *"둘을 다 끝내야 열리는 것"*을 적을 수 있다 —
// 08-23 (6)에 그 자리를 만들어 놓고 정작 저작이 없었다).
//
// ## 재료가 "가져와야 한다"를 그대로 표현한다
//
// 각 연구의 재료는 **그 광맥이 내놓는 자원 그 자체**다. 그래서 규칙이 한 줄로 읽힌다 —
// *철을 가져와야 철 광맥을 연구하고, 연구해야 철 광맥을 심는다.* 한때 `Discovery`라는
// 별도 축이 그 일을 했는데 **연구 재료가 이미 같은 말을 하고 있어서 걷어냈다**(08-23 (5)).
//
// **재료 목록을 코드에 적지 않는다** — `BuildingData.CostItem`을 그대로 읽는다. 표를 따로 두면
// 심는 값과 연구하는 값이 두 벌이 되어 한쪽만 고쳐진다.
//
// ## 다시 돌려도 안전하다
//
// 있으면 **값만 맞춘다**. 지우고 새로 만들면 guid가 바뀌어 이미 물려 둔 참조가 끊긴다
// (`TauntAbilityCreator`와 같은 규약). 선행은 **매번 새로 채운다** — 표를 고쳤을 때
// 옛 선행이 남으면 그래프가 저작과 어긋난다.
public static class VeinResearchCreator
{
    const string BuildingDir = "Assets/Resources/Data/Buildings";
    const string ResearchDir = "Assets/Resources/Data/Research";

    // 광맥 에셋 이름 → (연구 에셋 이름, 표시명, 설명, 연구 시간, 선행 연구 이름들)
    //
    // **뿌리가 하나 있어야 한다** — 없으면 열셋이 전부 처음부터 열려 있어 *"광맥을 심는다"*는
    // 발상 자체를 배우는 자리가 없다. 뿌리만 재료가 없다(배우는 것이지 가져오는 것이 아니다).
    const string Root = "Res_VeinTransplant";

    readonly struct Node
    {
        public readonly string Research, Name, Desc;
        public readonly float  Time;
        public readonly string[] Prereq;
        public Node(string research, string name, string desc, float time, params string[] prereq)
        { Research = research; Name = name; Desc = desc; Time = time; Prereq = prereq; }
    }

    // 값의 근거: 하루 1800초. 뿌리는 한 나절, 끝단(공허석)은 이틀 남짓이다.
    static readonly Dictionary<string, Node> Table = new()
    {
        ["Node_CoalSeam"]     = new("Res_Vein_Coal",     "석탄층 이식",     "탄층이 어느 결을 따라 뻗는지 읽어 낸다.",                       200f, Root),
        ["Node_CopperVein"]   = new("Res_Vein_Copper",   "구리 광맥 이식",  "무른 붉은 금속이 모이는 자리를 짚는다. 광맥을 심는 첫걸음이다.", 200f, Root),
        ["Node_TinVein"]      = new("Res_Vein_Tin",      "주석 광맥 이식",  "구리 곁에 나는 것을 따로 갈라 심는다.",                         350f, "Res_Vein_Copper"),
        ["Node_IronVein"]     = new("Res_Vein_Iron",     "철 광맥 이식",    "불과 구리를 다뤄 본 손만이 철의 결을 읽는다.",                   450f, "Res_Vein_Coal", "Res_Vein_Tin"),
        ["Node_SilverVein"]   = new("Res_Vein_Silver",   "은 광맥 이식",    "철보다 무르고 훨씬 드물다. 찾는 법이 다르다.",                   550f, "Res_Vein_Iron"),
        ["Node_GoldVein"]     = new("Res_Vein_Gold",     "금 광맥 이식",    "은을 좇다 보면 금이 어디 숨는지도 알게 된다.",                   700f, "Res_Vein_Silver"),
        ["Node_MagicCrystal"] = new("Res_Vein_Magic",    "마력석 결정 배양", "광물이 아니라 자란다 — 심는 것이 아니라 기른다.",                600f, "Res_Vein_Iron"),
        ["Node_EtherCrystal"] = new("Res_Vein_Ether",    "에테르석 결정 배양", "마력석에 금을 물려야 결정이 더 맑은 것을 붙든다.",             880f, "Res_Vein_Magic", "Res_Vein_Gold"),
        ["Node_VoidCrystal"]  = new("Res_Vein_Void",     "공허석 결정 배양", "아무것도 없는 쪽으로 자라는 결정. 기르는 자리가 곧 위험이다.",  1100f, "Res_Vein_Ether"),
        // **보석 셋은 나란히 선다 — 사슬이 아니다.** 루비 다음에 사파이어를 배운다는 것은
        // 아무것도 뜻하지 않는다(서로 배울 것이 없는 남남이다). 사슬로 엮으면 그래프가 아홉
        // 단으로 뻗어 창만 넓어지고, 무엇보다 **고를 것이 사라진다** — 순서가 정해져 있으면
        // 그건 계단이 아니라 줄이다. 나란히 두면 *어느 보석부터 여느냐*가 실제 결정이 된다.
        ["Node_RubyVein"]     = new("Res_Vein_Ruby",     "루비 광맥 이식",  "금을 캐던 자리에서 붉은 알을 골라내는 법.",                 800f, "Res_Vein_Gold"),
        ["Node_SapphireVein"] = new("Res_Vein_Sapphire", "사파이어 광맥 이식", "같은 돌이 다른 빛을 내는 까닭을 알면 갈라 심을 수 있다.", 820f, "Res_Vein_Gold"),
        ["Node_TopazVein"]    = new("Res_Vein_Topaz",    "토파즈 광맥 이식", "결이 곧아 깨기 쉽다. 심는 자리를 고르는 눈이 든다.",        840f, "Res_Vein_Gold"),
        // **셋을 다 끝내야 열린다** — 선행이 리스트인 까닭이 여기서 처음으로 값을 갖는다.
        ["Node_EmeraldVein"]  = new("Res_Vein_Emerald",  "에메랄드 광맥 이식", "빛깔이 다른 세 알을 다 다뤄 본 손만이 이 결을 읽는다.",  1000f, "Res_Vein_Ruby", "Res_Vein_Sapphire", "Res_Vein_Topaz"),
    };

    const int IngredientAmount = 15;   // 심는 값은 25 — **연구가 더 싸다**(배우는 것이 먼저다)

    [MenuItem("DungeonColony/Create Vein Research")]
    static void Create()
    {
        if (!AssetDatabase.IsValidFolder(ResearchDir))
        {
            Debug.LogError($"[광맥 연구] {ResearchDir} 가 없습니다."); return;
        }

        var made = new Dictionary<string, ResearchData>();

        // ── 뿌리 ────────────────────────────────────────────────────────────
        var root = Upsert(Root);
        root.ResearchName = "광맥 이식";
        root.Description  = "가져온 광석 조각을 땅에 묻어 다시 자라게 하는 법. "
                          + "이것부터 배워야 어떤 광맥이든 심을 수 있다.";
        root.ResearchTime = 120f;
        root.SkillGrowthAmount = 1.5f;
        root.Ingredients.Clear();
        root.Prerequisites.Clear();
        made[Root] = root;

        // ── 열셋 ────────────────────────────────────────────────────────────
        int linked = 0, missing = 0;
        foreach (var kv in Table)
        {
            var node = kv.Value;
            var r = Upsert(node.Research);
            r.ResearchName      = node.Name;
            r.Description       = node.Desc;
            r.ResearchTime      = node.Time;
            r.SkillGrowthAmount = 1f;
            made[node.Research] = r;
        }

        // 재료·선행은 **에셋이 다 생긴 뒤에** 채운다(선행이 서로를 가리킨다).
        foreach (var kv in Table)
        {
            string buildingName = kv.Key;
            var node = kv.Value;
            var r    = made[node.Research];

            // **선행부터 채운다 — 건물이 없어도.**
            //
            // 전에는 건물을 못 찾으면 `continue`로 빠져나가 **선행 갱신까지 건너뛰었다.**
            // 그러면 위에 적어 둔 *"선행은 매번 새로 채운다"*가 거짓이 된다: 노드 에셋을
            // 옮기거나 이름을 바꾼 뒤 표의 선행을 고치면 **옛 선행이 그대로 남고 `SetDirty`도
            // 안 걸려** 그래프가 저작과 조용히 어긋난다.
            r.Prerequisites.Clear();
            foreach (var p in node.Prereq)
            {
                if (made.TryGetValue(p, out var pr)) r.Prerequisites.Add(pr);
                else Debug.LogWarning($"[광맥 연구] 선행을 못 찾았습니다 — {p}");
            }

            // **재료는 그 광맥이 내놓는 자원**(= 심는 값과 같은 아이템). 표를 두지 않는다.
            var building = AssetDatabase.LoadAssetAtPath<BuildingData>($"{BuildingDir}/{buildingName}.asset");
            if (building == null)
            {
                Debug.LogWarning($"[광맥 연구] 건물 에셋이 없습니다 — {buildingName}"); missing++;
            }
            else
            {
                r.Ingredients.Clear();
                if (building.CostItem != null)
                    r.Ingredients.Add(new ResearchData.Ingredient { Item = building.CostItem, Amount = IngredientAmount });
                else
                    Debug.LogWarning($"[광맥 연구] {buildingName} 에 CostItem이 없어 재료를 비웠습니다.");

                building.RequiredResearch = r;
                EditorUtility.SetDirty(building);
                linked++;
            }

            EditorUtility.SetDirty(r);   // 건물이 없어도 선행은 고쳤으므로 늘 표시한다
        }
        EditorUtility.SetDirty(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[광맥 연구] 연구 {made.Count}개 · 광맥 {linked}개에 연결했습니다"
                + (missing > 0 ? $" (건물 {missing}개를 못 찾았습니다)" : "")
                + ". 뿌리는 `광맥 이식`이고 나머지 열셋이 그 아래로 뻗습니다.");
    }

    // 있으면 그대로 쓰고 없으면 만든다 — **지우고 새로 만들지 않는다**(guid가 바뀐다).
    static ResearchData Upsert(string assetName)
    {
        string path = $"{ResearchDir}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ResearchData>(path);
        if (existing != null) return existing;

        var created = ScriptableObject.CreateInstance<ResearchData>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }
}
