using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **건물은 무엇으로 지어졌는가 — 돌과 나무 둘 다다**
// (2026-09-17, 사용자 결정: *"건물들도 나무랑 써야지"*).
//
// ## 왜 필요했나
//
// 두 가지가 겹쳐 있었다.
//
// **하나 — 재료가 한 칸이었다.** `BuildingData`가 `CostItem` + `CostAmount` 하나라
// *돌이냐 나무냐*를 골라야 했고, 지하로 내려올 때 `0d. Swap Wood Costs To Stone`이
// **목재 비용을 전부 돌로 갈아엎었다**(그때는 지하에 나무가 없었다). 이제 `Costs`가
// 목록이라 **돌 벽에 나무 문**을 적을 자리가 있다.
//
// **둘 — 스물여섯 개가 `돌 1`이었다.** 그건 저작이 아니라 **저작이 안 된 상태**다
// (2G짜리 오두막). 값이 없으면 *무엇을 먼저 지을까*가 결정이 아니게 된다.
//
// ## 무엇이 무엇으로 지어지나 — 규칙 셋
//
// - **거친 구조물은 돌과 원목**(집·탑·복도·울타리·바리케이드). 쌓고 세우는 것이다.
// - **짜 맞춘 것은 판자**(침대·작업대·연구대·창고). 켜서 맞춘 것이라 원목으로는 못 만든다.
//   **그래서 판자에 쓰임이 하나 는다** — 여태 판자는 *무기 자루*에만 쓰였다.
// - **불과 물을 다루는 것은 돌**(화톳불·제련소·화장터·우물). 나무로 지으면 탄다.
//
// ## 눈금은 이미 저작된 것에서 낸다
//
// 값이 매겨져 있던 열넷이 기준이다 — 화톳불 20G · 벽 10G · 무덤 36G · 화장터 80G ·
// 제련소 80G · 석공대 70G · 충전대 120G · 보석 연마대 150G · 서브코어 240G.
// **작업대는 70~150G**이 그 띠라, 새로 매기는 것도 거기 선다.
//
// **표에 없는 것은 안 건드린다** — 이식 광맥 열셋은 *그 광석 25개*이고 그건 다른 축이다
// (무엇을 캐는 광맥인가가 곧 재료다).
//
// **다시 돌려도 된다** — 표가 곧 답이라 매번 같은 값이 된다.
public static class BuildingCostCreator
{
    const string BuildingDir = "Assets/Resources/Data/Buildings";
    const string ItemDir     = "Assets/Resources/Data/Items";

    // 재료 약칭 — 표를 읽을 수 있게.
    const string Stone = "Res_Stone";      //  2G  벽을 파면 나온다
    const string Cut   = "Res_CutStone";   //  6G  돌 2 → 1 (석공술)
    const string Wood  = "Res_Wood";       //  3G  거대버섯 · 이끼 낀 나무 (1층)
    const string Plank = "Res_Lumber";     //  8G  원목 3 → 2 (목공술)
    const string Bone  = "Res_Bone";       //  8G

    // 건물 에셋 이름 → 비용. **첫 재료가 그 건물의 몸이다**(`VeinResearchCreator`가 그렇게 읽는다).
    static readonly Dictionary<string, (string item, int amount)[]> Table = new()
    {
        // ── 짜 맞춘 것 — 판자 ────────────────────────────────────────────────
        ["Bed"]                   = new[] { (Plank, 8) },                  //  64G
        ["QualityBed"]            = new[] { (Plank, 16) },                 // 128G  더 좋은 잠자리
        ["PrisonBed"]             = new[] { (Plank, 8) },                  //  64G
        ["Storage"]               = new[] { (Plank, 10) },                 //  80G
        ["Granary"]               = new[] { (Plank, 12), (Stone, 6) },     // 108G  바닥을 돌로 깐다
        ["LargeStorage"]          = new[] { (Plank, 25), (Stone, 10) },    // 220G
        ["ResearchTable"]         = new[] { (Plank, 15), (Stone, 10) },    // 140G
        ["CraftingTable_Labor"]   = new[] { (Plank, 10) },                 //  80G  맨 처음 것이라 싸다
        ["CraftingTable_Woodwork"]= new[] { (Plank, 16) },                 // 128G
        ["CraftingTable_Tailor"]  = new[] { (Plank, 14) },                 // 112G
        ["CraftingTable_Cook"]    = new[] { (Plank, 12), (Stone, 8) },     // 112G  화덕이 돌이다
        ["CraftingTable_Weapon"]  = new[] { (Plank, 10), (Stone, 20) },    // 120G  모루가 돌이다
        ["CraftingTable_Armor"]   = new[] { (Plank, 10), (Stone, 20) },    // 120G
        ["ButcherTable"]          = new[] { (Plank, 8), (Stone, 6) },      //  76G
        ["Greenhouse"]            = new[] { (Plank, 15), (Stone, 10) },    // 140G

        // ── 거친 구조물 — 돌과 원목 ──────────────────────────────────────────
        ["Hut"]                   = new[] { (Wood, 25), (Stone, 15) },     // 105G
        ["Hall"]                  = new[] { (Wood, 40), (Stone, 30) },     // 180G
        ["Longhouse"]             = new[] { (Wood, 60), (Stone, 40) },     // 260G
        ["Workshop"]              = new[] { (Wood, 25), (Stone, 20) },     // 115G
        ["Tower"]                 = new[] { (Stone, 45), (Wood, 15) },     // 135G  쌓는 것이라 돌이 몸이다
        ["Corridor"]              = new[] { (Stone, 8) },                  //  16G
        ["TreeBuilding"]          = new[] { (Wood, 20) },                  //  60G
        ["Palisade"]              = new[] { (Wood, 4) },                   //  12G  줄로 긋는다 — 한 칸 값이다
        ["Barricade"]             = new[] { (Wood, 5) },                   //  15G
        ["Cell"]                  = new[] { (Stone, 15) },                 //  30G
        ["PrisonCage"]            = new[] { (Stone, 10), (Wood, 8) },      //  44G

        // ── 이미 저작돼 있던 것 — 여기 같이 적는다 ────────────────────────────
        //
        // **표가 답이라야 한다.** 절반만 적어 두면 *어느 쪽이 진짜인가*를 다음에 읽는
        // 사람이 재게 되고, 도구를 돌릴 때마다 나머지 절반은 손으로 맞춰야 한다.
        ["Campfire"]              = new[] { (Stone, 10) },                 //  20G
        ["WallTorch"]             = new[] { (Stone, 3) },                  //   6G
        ["Wall"]                  = new[] { (Stone, 5) },                  //  10G
        ["Grave"]                 = new[] { (Stone, 18) },                 //  36G
        ["Pyre"]                  = new[] { (Stone, 40) },                 //  80G
        ["CraftingTable_Smelt"]   = new[] { (Stone, 40) },                 //  80G
        ["CraftingTable_Stonecut"]= new[] { (Stone, 35) },                 //  70G
        ["Water"]                 = new[] { (Stone, 25) },                 //  50G
        ["IrrigationChannel"]     = new[] { (Stone, 8) },                  //  16G
        ["Charger"]               = new[] { (Cut, 20) },                   // 120G
        ["CraftingTable_Gemcut"]  = new[] { (Cut, 25) },                   // 150G
        ["CraftingTable_Alchemy"] = new[] { (Cut, 20) },                   // 120G
        ["SubCore"]               = new[] { (Cut, 40) },                   // 240G
        ["BoneAltar"]             = new[] { (Bone, 20) },                  // 160G
    };

    [MenuItem("DungeonColony/World/F. Wire Building Costs (건설 비용)")]
    static void Wire()
    {
        var items = new Dictionary<string, ItemData>();
        foreach (var id in new[] { Stone, Cut, Wood, Plank, Bone })
        {
            var a = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemDir}/{id}.asset");
            if (a == null) { Debug.LogError($"[건설 비용] {id} 아이템이 없습니다 — 아무것도 안 했습니다."); return; }
            items[id] = a;
        }

        int wired = 0, missing = 0;
        var lines = new List<string>();

        foreach (var kv in Table)
        {
            var b = AssetDatabase.LoadAssetAtPath<BuildingData>($"{BuildingDir}/{kv.Key}.asset");
            if (b == null) { Debug.LogWarning($"[건설 비용] 건물 에셋이 없습니다 — {kv.Key}"); missing++; continue; }

            Undo.RecordObject(b, "건설 비용");

            // **통째로 다시 쓴다 — 더하지 않는다.** 더하면 다시 돌릴 때마다 목록이 길어지고
            // 무엇이 의도이고 무엇이 잔재인지 못 가린다(`MetalCreator.WireBiomes`와 같은 규칙).
            b.Costs.Clear();

            int worth = 0;
            var parts = new List<string>();
            foreach (var (id, amount) in kv.Value)
            {
                b.Costs.Add(new RecipeData.Ingredient { Item = items[id], Amount = amount });
                worth += Mathf.RoundToInt(items[id].BaseValue * amount);
                parts.Add($"{items[id].ItemName}{amount}");
            }

            // **옛 한 칸을 비운다** — 안 비우면 `BuildingData.OnEnable`의 흡수 조건(`Costs`가
            // 비었나)이 언제 참이 될지가 두 값에 달리게 된다. 지금은 `Costs`가 채워져 있어
            // 흡수가 안 돌지만, **누가 `Costs`를 비우는 날 옛 값이 되살아난다.**
            var so = new SerializedObject(b);
            so.FindProperty("CostItem").objectReferenceValue = null;
            so.FindProperty("CostAmount").intValue           = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(b);
            lines.Add($"{b.BuildingName}: {string.Join(" + ", parts)} = {worth}G");
            wired++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[건설 비용] {wired}개에 비용을 적었습니다"
                + (missing > 0 ? $" (에셋을 못 찾은 것 {missing}개)" : "") + ".\n"
                + string.Join("\n", lines));
    }
}
