using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class FarmingCreator
{
    const string FontPath    = "Assets/NanumGothic SDF.asset";
    const string PrefabDir   = "Assets/3_Prefab";

    // ═══════════════════════════════════════════════════════════════
    // 1. FarmPlot 프리팹
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("DungeonColony/Create FarmPlot Prefab")]
    static void CreateFarmPlotPrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "FarmPlot";
        go.transform.localScale = new Vector3(1f, 0.1f, 1f);

        // 머티리얼 — 흙 느낌 갈색
        var renderer = go.GetComponent<MeshRenderer>();
        var mat      = new Material(Shader.Find("Standard"));
        mat.color    = new Color(0.45f, 0.28f, 0.12f);
        renderer.sharedMaterial = mat;

        // Collider는 이미 포함됨 (CreatePrimitive)
        go.AddComponent<FarmPlot>();

        var path   = $"{PrefabDir}/FarmPlot.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        Debug.Log($"[FarmingCreator] FarmPlot 프리팹 생성 → {path}\n" +
                  "인스펙터에서 Available Seeds 배열을 채워주세요.");
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. WaterSource (우물) 프리팹
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("DungeonColony/Create WaterSource Prefab")]
    static void CreateWaterSourcePrefab()
    {
        var root = new GameObject("WaterSource");

        // 우물 외형 — 원통 + 작은 원통 뚜껑
        var base_ = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        base_.name = "Base";
        base_.transform.SetParent(root.transform, false);
        base_.transform.localScale    = new Vector3(0.8f, 0.4f, 0.8f);
        base_.transform.localPosition = new Vector3(0, 0.4f, 0);
        var baseMat = new Material(Shader.Find("Standard"));
        baseMat.color = new Color(0.55f, 0.55f, 0.55f);
        base_.GetComponent<MeshRenderer>().sharedMaterial = baseMat;
        Object.DestroyImmediate(base_.GetComponent<Collider>());

        var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        top.name = "Top";
        top.transform.SetParent(root.transform, false);
        top.transform.localScale    = new Vector3(0.85f, 0.05f, 0.85f);
        top.transform.localPosition = new Vector3(0, 0.85f, 0);
        var topMat = new Material(Shader.Find("Standard"));
        topMat.color = new Color(0.35f, 0.22f, 0.1f);
        top.GetComponent<MeshRenderer>().sharedMaterial = topMat;
        Object.DestroyImmediate(top.GetComponent<Collider>());

        // 콜라이더 (루트에)
        var col        = root.AddComponent<BoxCollider>();
        col.center     = new Vector3(0, 0.45f, 0);
        col.size       = new Vector3(0.9f, 0.9f, 0.9f);

        root.AddComponent<WaterSource>();

        var path   = $"{PrefabDir}/WaterSource.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        Debug.Log($"[FarmingCreator] WaterSource 프리팹 생성 → {path}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. FarmPlot UI
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("DungeonColony/Create FarmPlot UI")]
    static void CreateFarmPlotUI()
    {
        var canvas = GetOrCreateCanvas();

        // ── 루트 패널 ─────────────────────────────────────────────
        var panel = MakeRect("FarmPlotPanel", canvas.transform);
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(200, 80);
        rt.sizeDelta        = new Vector2(300, 240);
        panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        panel.SetActive(false);

        // ── 타이틀 바 ─────────────────────────────────────────────
        var titleBar = MakeRect("TitleBar", panel.transform);
        var trt      = titleBar.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot     = new Vector2(0.5f, 1);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta = new Vector2(0, 44);
        titleBar.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 1f);

        var titleGO = MakeRect("TitleText", titleBar.transform);
        SetStretch(titleGO, new Vector2(12, 0), new Vector2(-50, 0));
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "농장"; titleTMP.fontSize = 15;
        titleTMP.fontStyle = FontStyles.Bold; titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Left;
        ApplyFont(titleTMP);

        var closeGO = MakeRect("CloseButton", titleBar.transform);
        var crt     = closeGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(1, 0); crt.anchorMax = new Vector2(1, 1);
        crt.pivot     = new Vector2(1, 0.5f);
        crt.anchoredPosition = new Vector2(-6, 0); crt.sizeDelta = new Vector2(38, -10);
        closeGO.AddComponent<Image>().color = new Color(0.5f, 0.15f, 0.15f, 1f);
        closeGO.AddComponent<Button>();
        var cTxtGO = MakeRect("Text", closeGO.transform);
        SetStretch(cTxtGO);
        var cTMP = cTxtGO.AddComponent<TextMeshProUGUI>();
        cTMP.text = "✕"; cTMP.fontSize = 14;
        cTMP.alignment = TextAlignmentOptions.Center; cTMP.color = Color.white;
        ApplyFont(cTMP);

        // ── 상태 텍스트 ───────────────────────────────────────────
        var stateGO = MakeRect("StateText", panel.transform);
        var srt     = stateGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot     = new Vector2(0.5f, 1);
        srt.anchoredPosition = new Vector2(0, -52); srt.sizeDelta = new Vector2(-20, 22);
        var stateTMP = stateGO.AddComponent<TextMeshProUGUI>();
        stateTMP.text = "상태: 비어있음"; stateTMP.fontSize = 13;
        stateTMP.color = new Color(0.8f, 0.8f, 0.8f); stateTMP.alignment = TextAlignmentOptions.Left;
        ApplyFont(stateTMP);

        // ── 성장 / 수분 행 ────────────────────────────────────────
        var statsRow = MakeRect("StatsRow", panel.transform);
        var srRT     = statsRow.GetComponent<RectTransform>();
        srRT.anchorMin = new Vector2(0, 1); srRT.anchorMax = new Vector2(1, 1);
        srRT.pivot     = new Vector2(0.5f, 1);
        srRT.anchoredPosition = new Vector2(0, -78); srRT.sizeDelta = new Vector2(-20, 20);
        var hlg = statsRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8; hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        var growthGO  = MakeRect("GrowthText",   statsRow.transform);
        var moistGO   = MakeRect("MoistureText", statsRow.transform);
        var growthTMP = growthGO.AddComponent<TextMeshProUGUI>();
        var moistTMP  = moistGO.AddComponent<TextMeshProUGUI>();
        growthTMP.text  = "성장: 0%";    growthTMP.fontSize  = 12;
        moistTMP.text   = "수분: 0/100"; moistTMP.fontSize   = 12;
        growthTMP.color = moistTMP.color = new Color(0.75f, 0.75f, 0.75f);
        growthTMP.alignment = moistTMP.alignment = TextAlignmentOptions.Left;
        ApplyFont(growthTMP); ApplyFont(moistTMP);

        // ── 구분선 ────────────────────────────────────────────────
        var sep = MakeRect("Separator", panel.transform);
        var sepRT = sep.GetComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0, 1); sepRT.anchorMax = new Vector2(1, 1);
        sepRT.pivot     = new Vector2(0.5f, 1);
        sepRT.anchoredPosition = new Vector2(0, -104); sepRT.sizeDelta = new Vector2(-16, 1);
        sep.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // ── 씨앗 버튼 영역 레이블 ─────────────────────────────────
        var seedLabelGO = MakeRect("SeedLabel", panel.transform);
        var slRT        = seedLabelGO.GetComponent<RectTransform>();
        slRT.anchorMin = new Vector2(0, 1); slRT.anchorMax = new Vector2(1, 1);
        slRT.pivot     = new Vector2(0.5f, 1);
        slRT.anchoredPosition = new Vector2(0, -112); slRT.sizeDelta = new Vector2(-20, 18);
        var slTMP = seedLabelGO.AddComponent<TextMeshProUGUI>();
        slTMP.text = "씨앗 선택"; slTMP.fontSize = 11;
        slTMP.color = new Color(0.5f, 0.5f, 0.5f); slTMP.alignment = TextAlignmentOptions.Left;
        ApplyFont(slTMP);

        // ── 씨앗 버튼 부모 (HorizontalLayoutGroup) ────────────────
        var seedParent = MakeRect("SeedButtonParent", panel.transform);
        var spRT       = seedParent.GetComponent<RectTransform>();
        spRT.anchorMin = new Vector2(0, 1); spRT.anchorMax = new Vector2(1, 1);
        spRT.pivot     = new Vector2(0.5f, 1);
        spRT.anchoredPosition = new Vector2(0, -136); spRT.sizeDelta = new Vector2(-16, 36);
        var seedHLG = seedParent.AddComponent<HorizontalLayoutGroup>();
        seedHLG.spacing = 4; seedHLG.childForceExpandWidth = false; seedHLG.childForceExpandHeight = true;
        seedHLG.childAlignment = TextAnchor.MiddleLeft;

        // ── 씨앗 초기화 버튼 ──────────────────────────────────────
        var clearGO  = MakeRect("ClearSeedButton", panel.transform);
        var clearRT  = clearGO.GetComponent<RectTransform>();
        clearRT.anchorMin = new Vector2(0, 0); clearRT.anchorMax = new Vector2(1, 0);
        clearRT.pivot     = new Vector2(0.5f, 0);
        clearRT.anchoredPosition = new Vector2(0, 10); clearRT.sizeDelta = new Vector2(-16, 28);
        clearGO.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.22f, 1f);
        var clearBtn = clearGO.AddComponent<Button>();
        var clearTxtGO = MakeRect("Text", clearGO.transform);
        SetStretch(clearTxtGO);
        var clearTMP = clearTxtGO.AddComponent<TextMeshProUGUI>();
        clearTMP.text = "씨앗 초기화"; clearTMP.fontSize = 12;
        clearTMP.alignment = TextAlignmentOptions.Center; clearTMP.color = Color.white;
        ApplyFont(clearTMP);

        // ── CloseButton 연결 ──────────────────────────────────────
        var cb   = closeGO.AddComponent<CloseButton>();
        var cbSO = new SerializedObject(cb);
        cbSO.FindProperty("Target").objectReferenceValue = panel.GetComponent<RectTransform>();
        cbSO.ApplyModifiedProperties();

        // ── FarmPlotUI 연결 ───────────────────────────────────────
        var ui   = panel.AddComponent<FarmPlotUI>();
        var uiSO = new SerializedObject(ui);
        uiSO.FindProperty("panel").objectReferenceValue            = panel;
        uiSO.FindProperty("titleText").objectReferenceValue        = titleTMP;
        uiSO.FindProperty("stateText").objectReferenceValue        = stateTMP;
        uiSO.FindProperty("growthText").objectReferenceValue       = growthTMP;
        uiSO.FindProperty("moistureText").objectReferenceValue     = moistTMP;
        uiSO.FindProperty("seedButtonParent").objectReferenceValue = seedParent.transform;
        uiSO.FindProperty("clearSeedButton").objectReferenceValue  = clearBtn;
        uiSO.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(panel, "Create FarmPlotUI");
        Selection.activeGameObject = panel;
        Debug.Log("[FarmingCreator] FarmPlot UI 생성 완료");
    }

    // ── 유틸 ──────────────────────────────────────────────────────

    static Canvas GetOrCreateCanvas()
    {
        var c = Object.FindFirstObjectByType<Canvas>();
        if (c != null) return c;
        var go = new GameObject("Canvas");
        c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static void SetStretch(GameObject go, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;    rt.offsetMax = offsetMax;
        rt.anchoredPosition = Vector2.zero;
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }
}
