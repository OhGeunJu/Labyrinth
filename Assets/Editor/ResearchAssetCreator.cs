using UnityEditor;
using UnityEngine;

// 연구 에셋을 만든다. **없는 것만 만든다** — 이미 있으면 손대지 않는다(값을 손봤을 수 있다).
//
// 잠금은 `RecipeData.RequiredResearch` ∥ `ResearchData.UnlockedRecipes` **어느 쪽으로 적어도**
// 걸린다. JSON 레시피는 `requiredResearchId`로 자기가 가리키므로 여기서는 `UnlockedRecipes`를
// 채우지 않는다 — 양쪽에 적으면 목록이 어긋났을 때 어느 쪽이 맞는지 알 수 없다.
public static class ResearchAssetCreator
{
    const string Dir = "Assets/Resources/Data/Research";

    [MenuItem("DungeonColony/Create Research Assets")]
    static void Create()
    {
        int made = 0;

        // **시간은 기존 연구 축에 맞춘다** — 제강술이 900초다(하루 1800초의 절반).
        // 60초 같은 값을 넣으면 연구 하나가 순식간에 끝나 진행 곡선이 무너진다.

        // 연금술 — **선행 연구가 없다.** 약초를 다루는 일이라 금속 다루는 제련과 계보가 다르다.
        // 재료(마력석·은)가 이미 문턱 노릇을 하므로 연구까지 막을 이유가 없다.
        made += Make("Res_Alchemy", "연금술",
                     "약초에서 유효한 것만 뽑아내는 법. 정수와 물약을 조제할 수 있게 된다.",
                     prerequisite: null, time: 900f,
                     ("Res_MagicStone", 3), ("Res_SilverIngot", 2)) ? 1 : 0;

        // 원소 공학 — 보석 세공(귀금속)을 딛는다. 결정을 무기에 물리는 기술.
        made += Make("Res_ElementalCraft", "원소 공학",
                     "결정에 갇힌 힘을 무기로 흘려보내는 법. 원소 지팡이를 만들 수 있게 된다.",
                     prerequisite: "Res_PreciousMetals", time: 1200f,
                     ("Res_MagicStone", 5), ("Res_Ruby", 2), ("Res_GoldIngot", 2)) ? 1 : 0;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ResearchAssetCreator] 연구 {made}종 생성 (이미 있는 것은 건너뜀). " +
                  "레시피의 requiredResearchId를 새 Id로 바꿔야 실제로 잠깁니다.");
    }

    // 재료는 **SO 아이템만** 가리킬 수 있다 — `ResearchData.Ingredients`가 `ItemData` 참조라
    // JSON으로 만든 아이템(허브 등)은 에셋이 아니어서 인스펙터에 꽂을 수 없다.
    static bool Make(string assetName, string display, string description,
                     string prerequisite, float time,
                     params (string item, int amount)[] ingredients)
    {
        string path = $"{Dir}/{assetName}.asset";
        if (AssetDatabase.LoadAssetAtPath<ResearchData>(path) != null) return false;

        var data = ScriptableObject.CreateInstance<ResearchData>();
        data.ResearchName      = display;
        data.Description       = description;
        data.ResearchTime      = time;
        data.SkillGrowthAmount = 1.5f;
        // Id는 비워 둔다 — `DataId.Resolve`가 에셋 파일명을 쓴다(프로젝트 관례).

        if (!string.IsNullOrEmpty(prerequisite))
        {
            var pre = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{prerequisite}.asset");
            if (pre != null) data.Prerequisite = pre;
            else Debug.LogWarning($"[ResearchAssetCreator] 선행 연구 '{prerequisite}'를 못 찾았습니다 — 기본 개방으로 둡니다.");
        }

        foreach (var (itemName, amount) in ingredients)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(
                $"Assets/Resources/Data/Items/{itemName}.asset");
            if (item == null)
            {
                Debug.LogWarning($"[ResearchAssetCreator] {assetName}: 재료 '{itemName}'을(를) 못 찾았습니다.");
                continue;
            }
            data.Ingredients.Add(new ResearchData.Ingredient { Item = item, Amount = amount });
        }

        AssetDatabase.CreateAsset(data, path);
        return true;
    }
}
