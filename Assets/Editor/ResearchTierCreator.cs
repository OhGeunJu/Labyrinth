using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **어느 연구를 어느 등급의 연구대에서 배우나**
// (2026-09-17, 사용자 결정: *"하위 연구대에서는 상위 연구대의 연구들이 안 보여야 해"* ·
//  배분은 *"앞을 넣고 뒤를 비운다"*).
//
// ## 표로 적는다 — 선행 깊이에서 계산하지 않는다
//
// 초안은 연구 그래프의 열에서 냈지만 **값은 여기 적혀 있다.** 계산으로 두면 `GraphColumn`
// (화면의 것)이 규칙을 정하게 되어 **노드를 보기 좋게 옮기는 날 등급이 바뀐다** — 이
// 저장소가 연구 자리를 `ResearchGraphPositions`로 따로 뺀 것과 같은 이유다.
//
// ## 배분 — 앞이 넓고 뒤가 빈다
//
// | 등급 | 수 | 성격 |
// |---|---|---|
// | I   | 15 | 판이 열리자마자 — 목공·석공·요리·직조와 그 바로 뒤 |
// | II  | 13 | 제련과 연금이 열린다. **강철이 여기서 나온다** |
// | III | 10 | 주조·야금·정밀 단조 — 상위 금속의 층 |
// | IV  |  6 | 보석과 마법 광물 |
// | V   |  0 | **비워 둔다** — 올릴 곳이 남아 있다는 것이 끝이 아니라는 표시다 |
//
// **V가 빈 것은 실수가 아니다**(사용자 결정). 다섯째 등급은 있는데 거기서만 배우는 것이
// 아직 없다 — 넣을 자리가 준비돼 있다는 뜻이고, 없는 연구를 지어내 채우면 그건 저작이
// 아니라 벽지다(`CraftResearchCreator`가 같은 말을 한다).
//
// ## 순환을 반드시 확인할 것
//
// 등급을 올리는 재료를 **그 등급에서만 열리는 연구**가 쥐고 있으면 영영 못 올린다.
// 지금 배분은 그 검사를 통과한다:
//
// - 등급 II ← 판자      : 목공술이 **I**
// - 등급 III ← 강철      : 제련술이 **II**
// - 등급 IV ← 티타늄     : 제련술이 **II**(티타늄 주괴도 제련술이다)
// - 등급 V ← 미스릴      : 야금학이 **III**
//
// **재료 표(`BenchTier.CostTo`)를 고치면 이 검사를 다시 할 것.**
//
// **다시 돌려도 된다** — 표가 곧 답이라 매번 같은 값이 된다.
public static class ResearchTierCreator
{
    const string Dir = "Assets/Resources/Data/Research";

    // 에셋 이름 → 요구 등급. **표에 없는 연구는 안 건드린다**(0으로 남아 어디서나 보인다).
    static readonly Dictionary<string, int> Table = new()
    {
        // ── I — 판이 열리자마자 ──────────────────────────────────────────────
        ["Res_VeinTransplant"] = 1,
        ["Res_Woodworking"]    = 1,
        ["Res_Bonecraft"]      = 1,
        ["Res_Weaving"]        = 1,
        ["Res_Cooking"]        = 1,
        ["Res_Vein_Copper"]    = 1,
        ["Res_Vein_Coal"]      = 1,
        ["Res_Bowyer"]         = 1,
        ["Res_Plywood"]        = 1,
        ["Res_Stonecutting"]   = 1,
        ["Res_BoneArmor"]      = 1,
        ["Res_Tailoring"]      = 1,
        ["Res_Baking"]         = 1,
        ["Res_Preserving"]     = 1,
        ["Res_Alchemy"]        = 1,

        // ── II — 제련과 연금 ─────────────────────────────────────────────────
        //
        // **`Res_Smelting`이 여기 있어야 한다** — 등급 III로 올리는 데 강철이 들고,
        // 강철을 여는 것이 제련술이다. 여기를 III로 옮기면 **III 연구대가 있어야 강철을
        // 만들고 강철이 있어야 III로 올리는** 순환이 된다.
        ["Res_Vein_Tin"]       = 2,
        ["Res_CompositeBow"]   = 2,
        ["Res_Furniture"]      = 2,
        ["Res_Architecture"]   = 2,
        ["Res_Smelting"]       = 2,
        ["Res_Gemcutting"]     = 2,
        ["Res_Medicine"]       = 2,
        ["Res_Refining"]       = 2,
        ["Res_Vein_Iron"]      = 2,
        ["Res_PreciousMetals"] = 2,
        ["Res_Casting"]        = 2,
        ["Res_WeaponSmithing"] = 2,
        ["Res_Surgery"]        = 2,

        // ── III — 상위 금속 ──────────────────────────────────────────────────
        //
        // **`Res_Metallurgy`가 여기 있어야 한다** — 등급 V에 미스릴이 들고 미스릴 주괴를
        // 여는 것이 야금학이다(V로 올릴 때 연구대는 IV이므로 III에서 이미 보인다).
        ["Res_Vein_Silver"]    = 3,
        ["Res_Vein_Magic"]     = 3,
        ["Res_ElementalCraft"] = 3,
        ["Res_Metallurgy"]     = 3,
        ["Res_Platemail"]      = 3,
        ["Res_Chainmail"]      = 3,
        ["Res_FineForging"]    = 3,
        ["Res_Vein_Gold"]      = 3,
        ["Res_CrystalWorking"] = 3,
        ["Res_MasterSmithing"] = 3,

        // ── IV — 보석과 마법 광물 ────────────────────────────────────────────
        ["Res_Vein_Ruby"]      = 4,
        ["Res_Vein_Sapphire"]  = 4,
        ["Res_Vein_Topaz"]     = 4,
        ["Res_Vein_Ether"]     = 4,
        ["Res_Vein_Emerald"]   = 4,
        ["Res_Vein_Void"]      = 4,
    };

    [MenuItem("DungeonColony/World/H. Wire Research Tiers (연구대 등급)")]
    static void Wire()
    {
        int wired = 0, missing = 0;
        var byTier = new Dictionary<int, List<string>>();

        foreach (var kv in Table)
        {
            var r = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{kv.Key}.asset");
            if (r == null) { Debug.LogWarning($"[연구대 등급] 연구 에셋이 없습니다 — {kv.Key}"); missing++; continue; }

            Undo.RecordObject(r, "연구대 등급");
            r.RequiredBenchTier = kv.Value;
            EditorUtility.SetDirty(r);

            if (!byTier.TryGetValue(kv.Value, out var list)) byTier[kv.Value] = list = new List<string>();
            list.Add(r.ResearchName);
            wired++;
        }

        // **표에 없는 연구를 센다** — 새 연구를 만들고 여기 안 적으면 조용히 등급 0이 되어
        // **가장 낮은 연구대에서도 보인다.** 그건 저작이 아니라 빠뜨린 것이다.
        int unlisted = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:ResearchData", new[] { Dir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string key  = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!Table.ContainsKey(key)) unlisted++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[연구대 등급] {wired}개에 등급을 적었습니다"
                    + (missing > 0 ? $" (에셋을 못 찾은 것 {missing}개)" : "") + ".");
        for (int t = BenchTier.Min; t <= BenchTier.Max; t++)
        {
            byTier.TryGetValue(t, out var list);
            int n = list?.Count ?? 0;
            sb.AppendLine($"  {BenchTier.Roman(t)} — {n}개"
                        + (n > 0 ? $": {string.Join(" · ", list)}" : " (비어 있음)"));
        }
        if (unlisted > 0)
            sb.AppendLine($"**표에 없는 연구가 {unlisted}개입니다** — 등급 0이라 가장 낮은 "
                        + "연구대에서도 보입니다. 새로 만든 연구라면 이 표에 적을 것.");

        Debug.Log(sb.ToString());
    }
}
