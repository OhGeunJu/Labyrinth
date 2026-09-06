using UnityEditor;
using UnityEngine;

// **살아 있지 않은 하수인의 유지 시설**(2026-09-04, 사용자 결정: *"골렘 하수인 허기가
// 있는게 이상함 … 대신 에너지를 충전해야하게 따로 뭘 만들자 / 해골도 사기를 충전"*).
//
// | 건물 | 채우는 축 | 재료 | 여는 연구 |
// |---|---|---|---|
// | 마력 충전대 | 에너지(골렘) | 마력석 | 없음 |
// | 망자의 제단 | 사기(해골) | 뼈 | 뼈 세공 |
//
// ## 유지 시설은 그 하수인과 **같은 문으로 열린다**
//
// 해골은 `뼈 세공`으로 열리므로 제단도 그 연구다. 골렘은 처음부터 열려 있으므로 충전대도
// 연구가 없다. **어긋나면 소환은 되는데 유지할 수가 없고**, 해골은 사기가 바닥나면
// 무너지므로(`Needs.StarvePerSecond`) 그건 *부를 수는 있지만 반드시 죽는 하수인*이 된다.
// 새 하수인을 넣을 때 이 줄을 같이 볼 것.
//
// ## 재료는 그 하수인이 남기는 것으로
//
// 뼈는 **해골이 죽으면 나오는 것**이라(`UnitData.BoneYield`) 고리가 닫힌다 — 시체를 치우는
// 일이 곧 제단을 채우는 일이다(`MinionData`에 적힌 *"뼈 → 연구 → 해골 → 또 뼈"*의 마지막
// 마디). 마력석은 광맥에서만 나오므로 골렘은 **광산이 곧 유지비**다.
//
// ## 골렘 셋의 몸을 여기서 켠다
//
// `UnitData.IsConstruct`는 새 필드라 **에셋이 전부 꺼진 채로 태어난다** — 안 켜면 이 도구가
// 건물만 세우고 골렘은 여전히 밥을 먹는다(그리고 창고가 비면 굶어 죽는다). 손으로 셋을
// 켜게 두면 언젠가 둘만 켠다.
//
// **다시 돌려도 안전하다** — 있으면 값만 맞춘다.
public static class UpkeepCreator
{
    const string PrefabDir = "Assets/3_Prefab/Building";
    const string DataDir   = "Assets/Resources/Data/Buildings";
    const string ItemDir   = "Assets/Resources/Data/Items";
    const string UnitDir   = "Assets/Resources/Data/Units";
    const string ResDir    = "Assets/Resources/Data/Research";
    const string MatDir    = "Assets/2_Material";
    const string Bones     = "Assets/assetsss/Fantastic Dungeon Pack/prefabs/PROPS/natural/bones";

    static readonly string[] Golems = { "Golem_Stone", "Golem_Frost", "Golem_Lava" };

    [MenuItem("DungeonColony/Building/4. Create Upkeep Sites (충전대 · 제단)")]
    static void Create()
    {
        var charger = BuildCharger();
        var altar   = BuildAltar();

        Data("Charger", "마력 충전대",
             "골렘이 여기 서서 마력석을 태워 에너지를 채운다. 에너지가 바닥난 골렘은 그 자리에 멈춘다.",
             sortOrder: 40, costItem: "Res_CutStone", costAmount: 20, research: null, prefab: charger);

        Data("BoneAltar", "망자의 제단",
             "해골이 여기 뼈를 바치고 사기를 채운다. 사기가 다한 해골은 무너져 내린다.",
             sortOrder: 41, costItem: "Res_Bone", costAmount: 20, research: "Res_Bonecraft", prefab: altar);

        MarkGolems();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[유지] 마력 충전대 · 망자의 제단을 만들고 골렘 셋을 구축물로 표시했습니다. "
                + "건설 메뉴는 레지스트리를 훑으므로 씬 배선은 없습니다.");
    }

    // ── 마력 충전대 — 기본 도형이고 갈아 끼울 자리다 ──────────────────────────
    //
    // 팩에 결정 소품이 없다(광맥은 구리·철·금·은뿐). 하는 일은 `Recharger`가 다 하므로
    // 모양은 나중에 `PropDresser` 표에 한 줄 얹으면 된다.
    static GameObject BuildCharger()
    {
        var stone   = Mat("Mdl_StoneDark");
        var crystal = Mat("Node_MagicCrystal");

        var root = new GameObject("Charger");

        // 받침 · 기둥 넷 · 가운데 뜬 결정
        Box(root, "Base",   new Vector3(0f, 0.08f, 0f),  new Vector3(0.9f,  0.16f, 0.9f),  stone);
        Box(root, "PillarA", new Vector3(-0.3f, 0.4f, -0.3f), new Vector3(0.14f, 0.6f, 0.14f), stone);
        Box(root, "PillarB", new Vector3( 0.3f, 0.4f, -0.3f), new Vector3(0.14f, 0.6f, 0.14f), stone);
        Box(root, "PillarC", new Vector3(-0.3f, 0.4f,  0.3f), new Vector3(0.14f, 0.6f, 0.14f), stone);
        Box(root, "PillarD", new Vector3( 0.3f, 0.4f,  0.3f), new Vector3(0.14f, 0.6f, 0.14f), stone);

        var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = "Crystal";
        Object.DestroyImmediate(shard.GetComponent<Collider>());
        shard.transform.SetParent(root.transform, false);
        shard.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        shard.transform.localScale    = new Vector3(0.22f, 0.42f, 0.22f);
        shard.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        if (crystal != null) shard.GetComponent<Renderer>().sharedMaterial = crystal;

        Finish(root, NeedKind.Energy, "Res_MagicStone", height: 0.9f);
        return SaveNew(root, "Charger");
    }

    // ── 망자의 제단 — 팩 소품을 그대로 쓴다 ──────────────────────────────────
    static GameObject BuildAltar()
    {
        var root = new GameObject("BoneAltar");

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Bones + "/P_PROP_altar_dungeon_01.prefab");
        if (src != null)
        {
            var art = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            art.name = "Art";
            art.transform.localPosition = Vector3.zero;

            // **콜라이더는 루트에만** — 자식에 있으면 클릭 레이가 자식에 걸려
            // `hit.collider.GetComponent<IWorldClickable>()`가 빈손이 된다.
            foreach (var c in art.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }
        else
        {
            Debug.LogWarning("[유지] 제단 소품을 못 찾았습니다 — 기본 도형으로 세웁니다.");
            var slab = Mat("Mdl_StoneDark");
            var bone = Mat("Bld_CEC798");
            Box(root, "Slab",  new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.7f, 0.7f), slab);
            Box(root, "Bones", new Vector3(0f, 0.76f, 0f), new Vector3(0.7f, 0.12f, 0.5f), bone);
        }

        Finish(root, NeedKind.Morale, "Res_Bone", height: 0.9f);
        return SaveNew(root, "BoneAltar");
    }

    // ── 공통 ─────────────────────────────────────────────────────────────────

    // 두 건물이 갈리는 것은 **축과 재료 둘뿐**이다 — 나머지(콜라이더·배선·발자국)는 같다.
    static void Finish(GameObject root, NeedKind fills, string fuelId, float height)
    {
        var rc = root.AddComponent<Recharger>();
        var so = new SerializedObject(rc);
        so.FindProperty("fills").enumValueIndex = (int)fills;
        so.FindProperty("fuelAmount").intValue  = 1;

        var fuel = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/" + fuelId + ".asset");
        if (fuel == null)
            Debug.LogWarning($"[유지] 재료를 못 찾았습니다 — {fuelId}. 그러면 {Needs.Name(fills)}가 공짜로 찹니다.");
        so.FindProperty("fuel").objectReferenceValue = fuel;
        so.ApplyModifiedPropertiesWithoutUndo();

        // **칸을 막는다**(`OccupiesTile` 켜짐) — 폰이 그 앞에 서서 쓰는 설비라 몸이 부딪혀야
        // 하고, 그래서 콜라이더는 **트리거가 아니다**(작업대·화톳불과 같은 자리).
        // 크기는 `Size − 0.2` 규약.
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, height * 0.5f, 0f);
        col.size   = new Vector3(0.8f, height, 0.8f);

        root.AddComponent<PlacedBuilding>();
    }

    static void Box(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material Mat(string name)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + name + ".mat");
        if (m == null) Debug.LogWarning("[유지] 재질을 못 찾았습니다 — " + name);
        return m;
    }

    static GameObject SaveNew(GameObject root, string name)
    {
        string path  = PrefabDir + "/" + name + ".prefab";
        var    saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        if (saved == null) Debug.LogError("[유지] 프리팹 저장 실패 — " + path);
        return saved;
    }

    static void Data(string assetName, string name, string desc, int sortOrder,
                     string costItem, int costAmount, string research, GameObject prefab)
    {
        string path = DataDir + "/" + assetName + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<BuildingData>();
            AssetDatabase.CreateAsset(d, path);
        }

        d.BuildingName  = name;
        d.Description   = desc;
        d.Category      = BuildCategory.Living;
        d.SortOrder     = sortOrder;
        d.Size          = Vector2Int.one;
        d.CostAmount    = costAmount;
        d.UsesBlueprint = true;
        d.OccupiesTile  = true;
        d.Prefab        = prefab;

        d.CostItem = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/" + costItem + ".asset");
        if (d.CostItem == null) Debug.LogWarning("[유지] 비용 아이템을 못 찾았습니다 — " + costItem);

        // **연구가 없는 것도 저작이다** — 골렘이 처음부터 열려 있으므로 충전대도 그렇다.
        d.RequiredResearch = research == null
            ? null
            : AssetDatabase.LoadAssetAtPath<ResearchData>(ResDir + "/" + research + ".asset");
        if (research != null && d.RequiredResearch == null)
            Debug.LogWarning($"[유지] 연구를 못 찾았습니다 — {research}. 안 잠긴 건물이 됩니다.");

        EditorUtility.SetDirty(d);

        if (prefab == null) return;
        var contents = PrefabUtility.LoadPrefabContents(PrefabDir + "/" + assetName + ".prefab");
        var pb = contents.GetComponent<PlacedBuilding>() ?? contents.AddComponent<PlacedBuilding>();
        var so = new SerializedObject(pb);
        so.FindProperty("data").objectReferenceValue = d;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, PrefabDir + "/" + assetName + ".prefab");
        PrefabUtility.UnloadPrefabContents(contents);
    }

    // 골렘 셋을 구축물로. **해골은 안 건드린다** — 언데드는 `IsUndead`가 이미 답한다
    // (`Needs.BodyOf`). 둘 다 켜면 같은 물음에 답이 둘이 된다.
    static void MarkGolems()
    {
        foreach (var id in Golems)
        {
            var u = AssetDatabase.LoadAssetAtPath<UnitData>(UnitDir + "/" + id + ".asset");
            if (u == null) { Debug.LogWarning("[유지] 골렘을 못 찾았습니다 — " + id); continue; }
            if (u.IsConstruct) continue;

            u.IsConstruct = true;
            EditorUtility.SetDirty(u);
            Debug.Log($"[유지] {id} → 구축물(먹지도 자지도 않고 에너지를 채운다)");
        }
    }
}
