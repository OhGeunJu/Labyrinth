using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 연구 그래프의 **자리를 저작한다**(2026-08-24, 사용자 결정).
//
// *"규격에 맞춰서 넣기보다는 직접 위치를 잡아서 해 — 노드 그래프처럼 만드는데 그걸 어떻게든
// 규격에 맞게 넣으려는게 문제야. 림월드도 보면 다 첫번째 시작하는 연구라고 제일 왼쪽에서
// 시작하지 않아."*
//
// 자동 배치는 *"열 = 가장 긴 선행 사슬"*이라 **선행 없는 연구를 전부 맨 왼쪽에 쌓는다.**
// 그건 규칙으로는 옳지만 읽히는 모양이 아니다 — 뿌리 여덟이 한 줄로 늘어서고 그 옆이 텅 빈다.
//
// **이 도구는 첫 벌을 깔아 줄 뿐이다.** 이후 조정은 각 `ResearchData`의 `GraphColumn`·
// `GraphRow`를 인스펙터에서 직접 만진다(−1이면 자동으로 돌아간다).
//
// ── 자리를 잡을 때 지킨 규칙 ─────────────────────────────────────────────────
//
// **선은 곧게 잇는다**(2026-08-25, `ResearchLinkGraphic`). 그래서 *"두 열 사이 한가운데에
// 세로 구간이 서니 통로를 비워라"*던 옛 규칙은 **없어졌다** — 대신 지킬 것이 하나 생겼다:
// **선이 남의 노드를 관통하면 안 된다.**
//
// 이웃 열끼리 잇는 선은 **어떤 노드도 못 가로지른다**(두 열 사이에는 노드가 없다). 그러니
// 실제로 볼 것은 **두 열 이상 건너뛰는 선**뿐이고, 지금 그런 것은 둘이다
// (`석탄층 → 철`, `마력석 → 에테르석`) — 둘 다 지나는 열의 노드를 안 스친다(검산 완료).
//
// ── 지킬 것 둘: 자식은 아래로, 순서는 부모를 따라 ────────────────────────────
//
// *"사슬세공이 제강술에만 달려있는데 왜 혼자 위에 올라가 있는거지? / 야금학은 제강술이랑도
// 연결이 되어있는데 왜 원소공학이 밑에 있고 야금학이 위에 있는거야?"*(사용자, 2026-08-25).
//
// 열만 맞고 행은 남는 자리에 밀어 넣었더니 **선이 거꾸로 올라갔다** — 사슬 세공은 유일한
// 부모(제강술)보다 **세 행 위**에 있었다. 선을 곧게 편 뒤로는 그 어긋남이 대각선으로 그대로
// 드러난다.
//
// - **자식은 부모의 행이거나 그 아래.** 위로 가는 것은 **한 부모가 자식 여럿을 위아래로
//   펼치는 자리**뿐이고 그때도 한 행까지다(지금 일곱 개, 전부 그 모양).
// - **한 열 안의 순서는 부모의 순서를 그대로 따른다.** 귀금속 제련(3,8)이 제강술(3,9)보다
//   위이므로 그 자식인 원소 공학(4,8)도 야금학(4,9)보다 위다. 안 따르면 선이 서로 넘어간다.
//
// ── 2026-08-25: 스물아홉에서 마흔여덟으로 ────────────────────────────────────
//
// *"지금은 너무 떨어져있고 비어있잖아"*(사용자, 림월드 연구 트리 스크린샷과 나란히).
//
// **빈 것은 간격이 아니라 칸이었다** — 8열 × 12행 96칸에 노드가 스물아홉(30%)이고, 오른쪽
// 아래 다섯 열 × 여섯 행이 통째로 비어 있었다. 간격(`nodeGap`)을 좁히면 덩어리가 작아질 뿐
// 빈 데는 그대로 빈다.
//
// 두 가지를 같이 했다: **노드를 열아홉 더 놓고**(`TechTreeCreator` — 전부 새 물건을 연다)
// **갈래를 양쪽으로 갈랐다**(제련·석공·가죽·뼈는 왼쪽 0~3열, 조리·연금·목공·직조는 오른쪽
// 4~6열). 왼쪽에 다 쌓으면 세로만 열일곱 행으로 길어지고 오른쪽이 그대로 빈다.
// 결과는 **8열 × 13행 104칸에 마흔여덟(46%)**이다.
public static class ResearchGraphPositions
{
    const string Dir = "Assets/Resources/Data/Research";

    // 에셋 이름 → (열, 행)
    static readonly Dictionary<string, Vector2Int> Layout = new()
    {
        // ── 광맥 사슬 ────────────────────────────────────────────────────
        ["Res_VeinTransplant"]     = new(0, 1),
        ["Res_Vein_Copper"]        = new(1, 0),
        ["Res_Vein_Coal"]          = new(1, 2),
        ["Res_Vein_Tin"]           = new(2, 0),
        ["Res_Vein_Iron"]          = new(3, 1),
        ["Res_Vein_Silver"]        = new(4, 0),
        ["Res_Vein_Magic"]         = new(4, 2),
        ["Res_Vein_Gold"]          = new(5, 0),
        ["Res_Vein_Ruby"]          = new(6, 0),
        ["Res_Vein_Sapphire"]      = new(6, 1),
        ["Res_Vein_Topaz"]         = new(6, 2),
        ["Res_Vein_Ether"]         = new(6, 3),
        ["Res_Vein_Emerald"]       = new(7, 1),
        ["Res_Vein_Void"]          = new(7, 3),

        // ── 뿌리 넷은 0열에 나란히 선다 ──────────────────────────────────
        ["Res_Woodworking"]        = new(0, 5),
        ["Res_Bonecraft"]          = new(0, 8),
        ["Res_Weaving"]            = new(0, 9),
        ["Res_Cooking"]            = new(0, 10),

        // ── 나무 → 돌 ───────────────────────────────────────────────────
        ["Res_Bowyer"]             = new(1, 5),
        ["Res_CompositeBow"]       = new(2, 5),
        ["Res_Plywood"]            = new(1, 6),
        ["Res_Furniture"]          = new(2, 6),
        ["Res_Stonecutting"]       = new(1, 7),
        ["Res_Architecture"]       = new(2, 7),
        ["Res_Gemcutting"]         = new(2, 9),

        // ── 쇠 — 자식은 부모 순서를 그대로 따라 아래로 퍼진다 ────────────
        ["Res_Smelting"]           = new(2, 8),
        ["Res_PreciousMetals"]     = new(3, 7),
        ["Res_Casting"]            = new(3, 8),
        ["Res_WeaponSmithing"]     = new(3, 9),
        ["Res_ElementalCraft"]     = new(4, 7),
        ["Res_Metallurgy"]         = new(4, 8),
        ["Res_Platemail"]          = new(4, 9),
        ["Res_Chainmail"]          = new(4, 10),
        ["Res_FineForging"]        = new(4, 11),
        ["Res_CrystalWorking"]     = new(5, 7),
        ["Res_MasterSmithing"]     = new(5, 11),

        // ── 뼈·섬유 ─────────────────────────────────────────────────────
        ["Res_BoneArmor"]          = new(1, 8),
        ["Res_Tailoring"]          = new(1, 9),

        // ── 불 → 밥과 약 ───────────────────────────────────────────────
        ["Res_Baking"]             = new(1, 10),
        ["Res_Preserving"]         = new(1, 11),
        ["Res_Alchemy"]            = new(1, 12),
        ["Res_Medicine"]           = new(2, 11),
        ["Res_Refining"]           = new(2, 12),
        ["Res_Surgery"]            = new(3, 12),
    };

    [MenuItem("DungeonColony/Setup Research Graph Positions")]
    static void Apply()
    {
        int set = 0, missing = 0;
        var used = new Dictionary<Vector2Int, string>();

        foreach (var kv in Layout)
        {
            var r = AssetDatabase.LoadAssetAtPath<ResearchData>($"{Dir}/{kv.Key}.asset");
            if (r == null) { Debug.LogWarning($"[연구 자리] 없는 에셋 — {kv.Key}"); missing++; continue; }

            if (used.TryGetValue(kv.Value, out var other))
                Debug.LogWarning($"[연구 자리] 자리가 겹칩니다 — {kv.Key} ↔ {other} ({kv.Value.x}, {kv.Value.y})");
            used[kv.Value] = kv.Key;

            r.GraphColumn = kv.Value.x;
            r.GraphRow    = kv.Value.y;
            EditorUtility.SetDirty(r);
            set++;
        }

        // **표에 없는 연구는 −1로 두지 않는다** — 이미 적힌 자리를 지우면 손으로 옮겨 둔 것이
        // 도구 한 번에 날아간다. 표에 없으면 그냥 안 건드리고, 자동으로 남아 있으면 자동이다.
        AssetDatabase.SaveAssets();
        Debug.Log($"[연구 자리] {set}개에 자리를 넣었습니다"
                + (missing > 0 ? $" (없는 에셋 {missing}개)" : "")
                + ". 이후 조정은 각 ResearchData의 GraphColumn·GraphRow에서 (−1 = 자동).");
    }
}
