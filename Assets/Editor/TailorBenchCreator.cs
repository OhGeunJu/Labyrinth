using UnityEditor;
using UnityEngine;

// **방어구 제작대를 둘로 가른다**(2026-08-25, 사용자 *"재봉에 방어구 제작대를 넣는게 좋지
// 않을까? 근데 재봉에서 여는 방어구 제작대에 강철 투구 이런게 있는 건 말이 안되긴하지"*).
//
// ## 하나였을 때는 무엇으로 잠글지가 성립하지 않았다
//
// `방어구 제작대` 하나가 **가죽 모자부터 강철 투구까지** 다 만들었다. 그래서:
//
// - **재봉으로 열면** 실과 바늘만 배운 사람이 강철 투구를 두들긴다.
// - **주조로 열면** 쇳물을 다룰 줄 알기 전에는 **가죽옷 한 벌 못 만든다**(초반이 통째로 막힌다).
//
// 갈라야 둘 다 풀린다:
//
// | 작업대 | 만드는 것 | 여는 연구 |
// |---|---|---|
// | **재봉대**(새로) | 가죽·천·뼈 갑옷 | 직조 |
// | **갑주 제작대**(개명) | 청동·철·강철·사슬·합금 | 주조 |
//
// 축은 `CraftSkill.Tailoring`이 든다 — 레시피의 `requiredSkillType`이 어느 작업대에 걸릴지를
// 이미 정하고 있어서(`CraftingTable.tableType`), **스킬을 하나 내는 것으로 갈라진다.**
//
// ## 베껴서 고친다
//
// 작업대 프리팹에는 `WorkSpot`(폰이 서는 자리)·연료 설정·산출물 통·콜라이더가 저작돼 있다.
// 새로 지으면 그걸 전부 다시 맞춰야 하고 하나만 빠뜨려도 **폰이 허공에 대고 제작하거나
// 완성품이 갈 곳을 잃는다.**
//
// **다시 돌려도 같다.**
public static class TailorBenchCreator
{
    const string PrefabDir = "Assets/3_Prefab/Building";
    const string DataDir   = "Assets/Resources/Data/Buildings";
    const string ItemDir   = "Assets/Resources/Data/Items";

    [MenuItem("DungeonColony/Create Tailor Bench")]
    static void Create()
    {
        // ── ① 원본(방어구 제작대)을 갑주 제작대로 고쳐 부른다 ────────────────
        //
        // **프리팹은 안 건드린다** — 하는 일(금속 갑옷)이 그대로고 `tableType`도
        // `ArmorCrafting` 그대로다. 바뀐 것은 **이름과 설명**뿐이다.
        var armor = AssetDatabase.LoadAssetAtPath<BuildingData>($"{DataDir}/CraftingTable_Armor.asset");
        if (armor == null) { Debug.LogError("[재봉대] CraftingTable_Armor.asset 을 못 찾았습니다."); return; }

        armor.BuildingName = "갑주 제작대";
        armor.Description  = "쇠를 두들겨 몸에 두를 것을 만든다. 청동·철·강철·사슬 갑옷.";
        EditorUtility.SetDirty(armor);

        // ── ② 재봉대 프리팹 — 원본을 베껴 `tableType`만 바꾼다 ───────────────
        string src = $"{PrefabDir}/CraftingTable_Armor.prefab";
        var contents = PrefabUtility.LoadPrefabContents(src);
        if (contents == null) { Debug.LogError($"[재봉대] {src} 를 못 찾았습니다."); return; }

        contents.name = "CraftingTable_Tailor";

        var table = contents.GetComponent<CraftingTable>();
        if (table == null)
        {
            Debug.LogError("[재봉대] 베낀 프리팹에 CraftingTable이 없습니다.");
            PrefabUtility.UnloadPrefabContents(contents);
            return;
        }
        var tso = new SerializedObject(table);
        tso.FindProperty("tableType").enumValueIndex = (int)CraftSkill.Tailoring;
        tso.ApplyModifiedPropertiesWithoutUndo();

        string path  = $"{PrefabDir}/CraftingTable_Tailor.prefab";
        var    saved = PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        if (saved == null) { Debug.LogError($"[재봉대] 프리팹 저장 실패 — {path}"); return; }

        // ── ③ BuildingData ───────────────────────────────────────────────────
        string dataPath = $"{DataDir}/CraftingTable_Tailor.asset";
        var d = AssetDatabase.LoadAssetAtPath<BuildingData>(dataPath);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<BuildingData>();
            AssetDatabase.CreateAsset(d, dataPath);
        }

        // **값과 크기는 원본과 같게** — 같은 종류의 물건이고, 다르게 하면 *왜 이쪽이 비싼가*가
        // 또 하나의 규칙이 된다.
        d.BuildingName  = "재봉대";
        d.Description   = "실과 바늘로 몸에 걸치는 것을 짓는다. 가죽·천 갑옷과 뼈를 덧댄 것.";
        d.Category      = armor.Category;
        d.SortOrder     = armor.SortOrder + 1;   // 갑주 제작대 바로 옆에 선다
        d.Size          = armor.Size;
        d.CostItem      = armor.CostItem;
        d.CostAmount    = armor.CostAmount;
        d.UsesBlueprint = armor.UsesBlueprint;
        d.OccupiesTile  = armor.OccupiesTile;
        d.Prefab        = saved;
        // 잠금은 `TechTreeCreator.BuildingGates`가 넣는다 — **답이 두 벌이 되지 않게**
        // (거기서 갑주 제작대 ← 주조 · 재봉대 ← 직조를 같이 든다).
        EditorUtility.SetDirty(d);

        // 프리팹 쪽에 데이터를 물린다 — 한쪽만 물리면 조용히 반쪽이 된다
        // (씬에 놓은 것이 발자국을 등록 안 해 모델 한복판을 가로지르는 경로가 나온다).
        var again = PrefabUtility.LoadPrefabContents(path);
        var pb = again.GetComponent<PlacedBuilding>() ?? again.AddComponent<PlacedBuilding>();
        var pso = new SerializedObject(pb);
        pso.FindProperty("data").objectReferenceValue = d;
        pso.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(again, path);
        PrefabUtility.UnloadPrefabContents(again);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[재봉대] 만들었습니다 — 재봉대(가죽·천) + 갑주 제작대로 개명(금속). "
                + "잠금은 `Create Tech Tree`가 넣습니다(재봉대 ← 직조 · 갑주 제작대 ← 주조).");
    }
}
