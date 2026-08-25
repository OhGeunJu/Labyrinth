using UnityEditor;
using UnityEngine;

// **농업·가구 건물 셋을 짓는다**(2026-08-25, 사용자 요청 *"농업·가구 모델링 만들어"*).
//
// ## 건물은 모델이 아니라 하는 일이 먼저다
//
// 셋 다 **이미 있는 축에 한 줄씩 얹는다** — 새 규칙을 만들지 않는다:
//
// | 건물 | 하는 일 | 얹은 자리 |
// |---|---|---|
// | 관개 수로 | 둘레 밭이 안 마른다 | `Irrigation` 레지스트리 → `FarmPlot.UpdateGrowth` |
// | 온실 밭 | 같은 씨앗이 더 빨리 여문다 | `FarmPlot.GrowthMultiplier` |
// | 좋은 침대 | 피로가 더 빨리 찬다 | `Bed.RestQuality` → `Needs.SleepPerSecond` |
//
// ## 이미 있는 프리팹은 베껴서 고친다
//
// 온실과 좋은 침대는 **밭·침대와 같은 물건**이다(자식 구조 · `WorkSpot` · 눕는 자리 ·
// 물들일 렌더러 · 콜라이더가 전부 거기 저작돼 있다). 새로 지으면 그것을 전부 다시 맞춰야 하고,
// 하나라도 빠뜨리면 **폰이 침대에 비스듬히 눕거나 밭을 클릭해도 안 열린다.**
//
// 원본을 **덮어쓰지 않는다** — `SaveAsPrefabAsset`으로 새 경로에 저장하므로 새 guid를 받고,
// 원본의 루트 fileID를 물려받을 이유도 없다(씬에 놓인 인스턴스가 없는 새 프리팹이다).
//
// ## 순서가 있다
//
// **`Create Tech Tree`를 먼저 돌릴 것** — 여기서 `Res_Architecture`(수로·온실)와
// `Res_Furniture`(좋은 침대)를 찾아 잠금에 물린다. 없으면 경고만 남기고 **안 잠긴 건물**이 나온다.
//
// **셋에 연구가 둘뿐이다**(2026-08-25) — 건축학 하나가 큰 창고·관개 수로·온실 밭을 같이
// 연다. 관개·온실로 쪼개 놨었는데 **그 사이에 고를 것이 없어** 세 번 눌러야 하는 한 줄이었다.
//
// **다시 돌려도 안전하다** — 있으면 값만 맞춘다.
public static class FarmFurnitureCreator
{
    const string PrefabDir = "Assets/3_Prefab/Building";
    const string DataDir   = "Assets/Resources/Data/Buildings";
    const string ItemDir   = "Assets/Resources/Data/Items";
    const string MatDir    = "Assets/2_Material";
    const string ResDir    = "Assets/Resources/Data/Research";

    [MenuItem("DungeonColony/Create Farm & Furniture Buildings")]
    static void Create()
    {
        var channel = BuildIrrigationChannel();
        var green   = BuildGreenhouse();
        var bed     = BuildQualityBed();

        Data("IrrigationChannel", "관개 수로",
             "물길을 내어 둘레 밭이 마르지 않게 한다. 첫 물은 사람이 대야 한다 — 그 뒤로 안 마른다.",
             BuildCategory.Resource, sortOrder: 22, size: new Vector2Int(1, 1),
             costItem: "Res_Stone", costAmount: 8, research: "Res_Architecture", prefab: channel,
             // **물길은 그어서 놓는다** — 한 칸씩 찍게 하면 밭 한 뙈기를 대는 데 열 번을 누른다
             // (벽·목책·바닥이 같은 이유로 드래그다).
             dragBuild: true);

        Data("Greenhouse", "온실 밭",
             "천을 씌워 볕과 물기를 가둔 밭. 같은 씨앗이 절반 더 빨리 여문다.",
             BuildCategory.Resource, sortOrder: 12, size: new Vector2Int(1, 1),
             costItem: "Res_Wood", costAmount: 20, research: "Res_Architecture", prefab: green);

        Data("QualityBed", "좋은 침대",
             "널을 켜서 짜 맞추고 두툼하게 깔았다. 같은 밤을 자도 더 개운하다.",
             BuildCategory.Living, sortOrder: 32, size: new Vector2Int(1, 2),
             costItem: "Res_Wood", costAmount: 30, research: "Res_Furniture", prefab: bed);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[농업·가구] 건물 셋을 만들었습니다 — 관개 수로 · 온실 밭 · 좋은 침대. "
                + "건설 메뉴는 레지스트리를 훑으므로 씬 배선은 없습니다.");
    }

    // ── 관개 수로 — 기본 도형으로 새로 짓는다 ────────────────────────────────
    //
    // 베낄 원본이 없다(물길은 처음이다). `PrisonCage`가 그랬듯 **기본 도형이고 갈아 끼울
    // 자리다** — 하는 일은 `Irrigation`이 이미 다 한다.
    static GameObject BuildIrrigationChannel()
    {
        var stone = Mat("Bld_57524F");
        var water = Mat("Bld_5CADCC");

        var root = new GameObject("IrrigationChannel");

        // 바닥 슬래브 · 양쪽 턱 · 그 사이의 물
        Box(root, "Base",      new Vector3(0f,     0.05f, 0f), new Vector3(0.9f,  0.1f,  0.9f), stone);
        Box(root, "WallLeft",  new Vector3(-0.39f, 0.20f, 0f), new Vector3(0.12f, 0.3f,  0.9f), stone);
        Box(root, "WallRight", new Vector3( 0.39f, 0.20f, 0f), new Vector3(0.12f, 0.3f,  0.9f), stone);
        Box(root, "Water",     new Vector3(0f,     0.17f, 0f), new Vector3(0.66f, 0.14f, 0.9f), water);

        root.AddComponent<Irrigation>();

        // **콜라이더는 루트에, 그리고 트리거다.** 자식에 있으면 `hit.collider.GetComponent`로
        // 묻는 클릭이 안 잡히고, 트리거가 아니면 `OccupiesTile`이 꺼져 있는데 몸이 부딪혀
        // **길찾기가 통과 경로를 내고 폰이 처박힌다**(침대·밭이 겪은 그 자리).
        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center    = new Vector3(0f, 0.2f, 0f);
        col.size      = new Vector3(0.8f, 0.4f, 0.8f);

        root.AddComponent<PlacedBuilding>();

        return SaveNew(root, "IrrigationChannel");
    }

    // ── 온실 밭 — 밭을 베껴 틀을 씌운다 ──────────────────────────────────────
    static GameObject BuildGreenhouse()
    {
        var contents = PrefabUtility.LoadPrefabContents($"{PrefabDir}/FarmPlot.prefab");
        if (contents == null) { Debug.LogError("[농업·가구] FarmPlot.prefab 을 못 찾았습니다."); return null; }

        contents.name = "Greenhouse";

        var plot = contents.GetComponent<FarmPlot>();
        if (plot != null) SetFloat(plot, "growthMultiplier", 1.5f);
        else Debug.LogWarning("[농업·가구] 베낀 밭에 FarmPlot이 없습니다 — 성장 배율을 못 넣었습니다.");

        // **틀은 열려 있다** — 지붕을 덮어 버리면 흙과 작물이 안 보여서 *심었는지 아닌지*가
        // 화면에서 사라진다. 기둥 넷과 위 테두리, 그리고 비스듬한 덮개 두 장만 얹는다.
        var wood  = Mat("Bld_574530");
        var cover = Mat("Bld_D9DEE6");

        var frame = new GameObject("Frame");
        frame.transform.SetParent(contents.transform, false);

        const float h = 0.55f;
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0) ? -0.4f : 0.4f;
            float z = (i < 2)      ? -0.4f : 0.4f;
            Box(frame, $"Post{i}", new Vector3(x, h * 0.5f, z), new Vector3(0.07f, h, 0.07f), wood);
        }
        Box(frame, "RimFront", new Vector3(0f, h, -0.4f), new Vector3(0.87f, 0.06f, 0.07f), wood);
        Box(frame, "RimBack",  new Vector3(0f, h,  0.4f), new Vector3(0.87f, 0.06f, 0.07f), wood);
        Box(frame, "CoverA",   new Vector3(0f, h + 0.06f, -0.2f), new Vector3(0.86f, 0.03f, 0.34f), cover);
        Box(frame, "CoverB",   new Vector3(0f, h + 0.06f,  0.2f), new Vector3(0.86f, 0.03f, 0.34f), cover);

        // **클릭 상자를 새 실루엣에 맞춘다.** 밭의 콜라이더는 흙만 덮는 납작한 판(높이 0.18)이라,
        // 틀을 씌우고 나면 위쪽을 눌렀을 때 아무것도 안 잡힌다. **가로세로는 안 건드린다** —
        // 그건 `Size − 0.2` 규약이라 칸 크기를 뜻한다.
        RaiseCollider(contents, height: 0.70f);

        return SaveNew(contents, "Greenhouse", fromContents: true);
    }

    // ── 좋은 침대 — 침대를 베껴 두툼하게 ─────────────────────────────────────
    static GameObject BuildQualityBed()
    {
        var contents = PrefabUtility.LoadPrefabContents($"{PrefabDir}/Bed.prefab");
        if (contents == null) { Debug.LogError("[농업·가구] Bed.prefab 을 못 찾았습니다."); return null; }

        contents.name = "QualityBed";

        var bed = contents.GetComponent<Bed>();
        if (bed != null) SetFloat(bed, "restQuality", 1.4f);
        else Debug.LogWarning("[농업·가구] 베낀 침대에 Bed가 없습니다 — 회복 배율을 못 넣었습니다.");

        // **색으로 가르지 않는다** — 매트리스 재질은 `Bed`가 런타임에 갈아 끼우므로
        // (`normalTint`/`prisonTint`, 죄수용이냐가 그 축이다) 여기서 칠해 봐야 덮인다.
        // 그래서 **생김새로 가른다**: 요를 두껍게, 베개를 크게, 머리판을 세운다.
        var mattress = contents.transform.Find("Mattress");
        if (mattress != null)
        {
            mattress.localScale    = new Vector3(0.84f, 0.26f, 1.72f);
            mattress.localPosition = new Vector3(0f, 0.42f, 0f);
        }
        var pillow = contents.transform.Find("Pillow");
        if (pillow != null)
        {
            pillow.localScale    = new Vector3(0.68f, 0.16f, 0.36f);
            pillow.localPosition = new Vector3(0f, 0.59f, -0.66f);
        }

        var frame = contents.transform.Find("Frame");
        var wood  = frame != null ? frame.GetComponent<Renderer>()?.sharedMaterial : null;
        Box(contents, "Headboard", new Vector3(0f, 0.52f, -0.96f), new Vector3(0.9f, 0.6f, 0.08f),
            wood != null ? wood : Mat("Bld_574530"));

        // **머리판을 세웠으면 눕는 자리를 못 박아야 한다.**
        //
        // `Bed.SleepPosition`은 `sleepAnchor`가 없으면 **자식 전체를 감싼 상자의 윗면**에서
        // `lieHeight`만큼 띄운다. 머리판(윗면 0.82)이 요(0.55)보다 높으므로 그대로 두면
        // **폰이 요에서 0.28m 떠서 잔다** — CLAUDE.md가 침대에 표식 quad를 얹었다 걷어낸
        // 그 자리와 **같은 함정**이다(자식이 늘면 상자가 커진다).
        //
        // 값은 원본 침대와 맞춘다: 원본은 베개 윗면 0.54 + `lieHeight` 0.35 = **0.89**.
        // 여기 요가 두꺼워 윗면이 0.55이므로 0.90 — 눈으로도 같은 높이다.
        var anchor = new GameObject("SleepAnchor");
        anchor.transform.SetParent(contents.transform, false);
        anchor.transform.localPosition = new Vector3(0f, 0.90f, 0f);
        if (bed != null)
        {
            var so = new SerializedObject(bed);
            so.FindProperty("sleepAnchor").objectReferenceValue = anchor.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 머리판(윗면 0.82)이 원래 상자(0~0.6) 밖으로 나온다 — 여기도 맞춘다.
        RaiseCollider(contents, height: 0.85f);

        return SaveNew(contents, "QualityBed", fromContents: true);
    }

    // ── 공통 ─────────────────────────────────────────────────────────────────

    // 기본 도형 하나. **콜라이더는 떼어 낸다** — 자식마다 붙어 있으면 클릭 레이가 자식에
    // 걸려 `hit.collider.GetComponent<IWorldClickable>()`가 빈손이 된다(루트에만 둔다).
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

    // 클릭 상자의 **높이만** 바꾼다. 가로세로는 `Size − 0.2` 규약이 든 값이라 손대면
    // *어디를 눌러야 잡히나*가 칸 크기와 어긋난다.
    static void RaiseCollider(GameObject root, float height)
    {
        if (!root.TryGetComponent<BoxCollider>(out var col)) return;
        var size = col.size;
        col.size   = new Vector3(size.x, height, size.z);
        col.center = new Vector3(col.center.x, height * 0.5f, col.center.z);
    }

    static Material Mat(string name)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");
        if (m == null) Debug.LogWarning($"[농업·가구] 재질을 못 찾았습니다 — {name}");
        return m;
    }

    // private [SerializeField]는 밖에서 못 만지므로 직렬화 창구로 넣는다.
    static void SetFloat(Component c, string field, float value)
    {
        var so = new SerializedObject(c);
        var p  = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[농업·가구] {c.GetType().Name}.{field} 가 없습니다."); return; }
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject SaveNew(GameObject root, string name, bool fromContents = false)
    {
        string path  = $"{PrefabDir}/{name}.prefab";
        var    saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        if (fromContents) PrefabUtility.UnloadPrefabContents(root);
        else              Object.DestroyImmediate(root);
        if (saved == null) Debug.LogError($"[농업·가구] 프리팹 저장 실패 — {path}");
        return saved;
    }

    // BuildingData를 만들고, **프리팹의 `PlacedBuilding.data`와 서로 물린다.**
    //
    // 한쪽만 물리면 조용히 반쪽이 된다: 데이터에 프리팹이 없으면 건설 메뉴에서 눌러도 아무것도
    // 안 서고, 프리팹에 데이터가 없으면 **씬에 놓인 것이 발자국을 등록 안 해** 모델 한복판을
    // 가로지르는 경로가 나오고 좌클릭해도 정보 패널이 안 뜬다.
    static void Data(string assetName, string name, string desc, BuildCategory cat, int sortOrder,
                     Vector2Int size, string costItem, int costAmount, string research, GameObject prefab,
                     bool dragBuild = false)
    {
        string path = $"{DataDir}/{assetName}.asset";
        var d = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<BuildingData>();
            AssetDatabase.CreateAsset(d, path);
        }

        d.BuildingName  = name;
        d.Description   = desc;
        d.Category      = cat;
        d.SortOrder     = sortOrder;
        d.Size          = size;
        d.CostAmount    = costAmount;
        d.UsesBlueprint = true;   // 값이 붙은 건물은 청사진이다 — 즉시 배치면 자재가 모자랄 때
                                  // 클릭이 그냥 씹혀 *안 지어지는 것*과 *못 놓는 것*이 안 갈린다.
        d.OccupiesTile  = false;  // 밭·침대와 같다. 그래서 콜라이더도 트리거여야 한다.
        d.DragBuild     = dragBuild;
        d.Prefab        = prefab;

        d.CostItem = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemDir}/{costItem}.asset");
        if (d.CostItem == null) Debug.LogWarning($"[농업·가구] 비용 아이템을 못 찾았습니다 — {costItem}");

        d.RequiredResearch = AssetDatabase.LoadAssetAtPath<ResearchData>($"{ResDir}/{research}.asset");
        if (d.RequiredResearch == null)
            Debug.LogWarning($"[농업·가구] 연구를 못 찾았습니다 — {research}. "
                           + "`Create Tech Tree`를 먼저 돌리세요(안 그러면 안 잠긴 건물이 됩니다).");

        EditorUtility.SetDirty(d);

        // 프리팹 쪽에 데이터를 물린다.
        if (prefab == null) return;
        var contents = PrefabUtility.LoadPrefabContents($"{PrefabDir}/{assetName}.prefab");
        var pb = contents.GetComponent<PlacedBuilding>() ?? contents.AddComponent<PlacedBuilding>();
        var so = new SerializedObject(pb);
        so.FindProperty("data").objectReferenceValue = d;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, $"{PrefabDir}/{assetName}.prefab");
        PrefabUtility.UnloadPrefabContents(contents);
    }
}
