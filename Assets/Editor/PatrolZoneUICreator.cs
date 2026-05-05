using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class PatrolZoneUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    [MenuItem("DungeonColony/Create PatrolZone UI")]
    static void Create()
    {
        var canvas = GetOrCreateCanvas();

        // ── 루트 패널 ─────────────────────────────────────────────────────
        var panel = MakeRect("PatrolZonePanel", canvas.transform);
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(160, 80);
        rt.sizeDelta        = new Vector2(260, 280);
        panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        panel.SetActive(false);

        // ── 타이틀 바 ─────────────────────────────────────────────────────
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
        titleTMP.text = "순찰 구역"; titleTMP.fontSize = 15;
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

        // ── 모드 버튼 행 ──────────────────────────────────────────────────
        var modeRow = MakeRect("ModeRow", panel.transform);
        var mrt     = modeRow.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0, 1); mrt.anchorMax = new Vector2(1, 1);
        mrt.pivot     = new Vector2(0.5f, 1);
        mrt.anchoredPosition = new Vector2(0, -52);
        mrt.sizeDelta = new Vector2(-16, 32);
        var hlg = modeRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4; hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        var btnWP   = MakeButton("ModeWaypoint", "웨이포인트", modeRow.transform);
        var btnArea = MakeButton("ModeArea",     "무작위",     modeRow.transform);
        var btnIdle = MakeButton("ModeIdle",     "대기",       modeRow.transform);

        // ── 구분선 ────────────────────────────────────────────────────────
        var sep = MakeRect("Separator", panel.transform);
        var srt = sep.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot     = new Vector2(0.5f, 1);
        srt.anchoredPosition = new Vector2(0, -90); srt.sizeDelta = new Vector2(-16, 1);
        sep.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // ── 유닛 목록 ScrollView ──────────────────────────────────────────
        var scroll     = MakeRect("UnitScroll", panel.transform);
        var scrollRT   = scroll.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0, 0); scrollRT.anchorMax = new Vector2(1, 1);
        scrollRT.offsetMin = new Vector2(8,  48);
        scrollRT.offsetMax = new Vector2(-8, -98);
        var scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        var viewport = MakeRect("Viewport", scroll.transform);
        SetStretch(viewport);
        viewport.AddComponent<Image>().color = Color.clear;
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();

        var content = MakeRect("Content", viewport.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot     = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero; contentRT.sizeDelta = new Vector2(0, 0);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2; vlg.childForceExpandWidth = true;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRT;

        // ── 배정 버튼 ─────────────────────────────────────────────────────
        var assignBtn = MakeButton("AssignButton", "선택 유닛 배정", panel.transform);
        var abrt      = assignBtn.GetComponent<RectTransform>();
        abrt.anchorMin = new Vector2(0, 0); abrt.anchorMax = new Vector2(1, 0);
        abrt.pivot     = new Vector2(0.5f, 0);
        abrt.anchoredPosition = new Vector2(0, 8); abrt.sizeDelta = new Vector2(-16, 32);

        // ── CloseButton 연결 ──────────────────────────────────────────────
        var cb = closeGO.AddComponent<CloseButton>();
        var cbSO = new SerializedObject(cb);
        cbSO.FindProperty("Target").objectReferenceValue = panel.GetComponent<RectTransform>();
        cbSO.ApplyModifiedProperties();

        // ── PatrolZoneUI 연결 ─────────────────────────────────────────────
        var ui   = panel.AddComponent<PatrolZoneUI>();
        var uiSO = new SerializedObject(ui);
        uiSO.FindProperty("panel").objectReferenceValue             = panel;
        uiSO.FindProperty("titleText").objectReferenceValue         = titleTMP;
        uiSO.FindProperty("modeWaypoint").objectReferenceValue      = btnWP;
        uiSO.FindProperty("modeArea").objectReferenceValue          = btnArea;
        uiSO.FindProperty("modeIdle").objectReferenceValue          = btnIdle;
        uiSO.FindProperty("unitListContainer").objectReferenceValue = contentRT;
        uiSO.FindProperty("assignButton").objectReferenceValue      = assignBtn;
        uiSO.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(panel, "Create PatrolZoneUI");
        Selection.activeGameObject = panel;
        Debug.Log("[PatrolZoneUICreator] 순찰 UI 생성 완료 — SelectionManager를 인스펙터에서 연결해주세요.");
    }

    // ── 유틸 ──────────────────────────────────────────────────────────────

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

    static void SetStretch(GameObject go, Vector2 min = default, Vector2 max = default)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = min; rt.offsetMax = max;
        rt.anchoredPosition = Vector2.zero;
    }

    static Button MakeButton(string name, string label, Transform parent)
    {
        var go = MakeRect(name, parent);
        go.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.22f, 1f);
        var btn = go.AddComponent<Button>();
        var tGO = MakeRect("Text", go.transform);
        SetStretch(tGO);
        var tmp = tGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 12;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        ApplyFont(tmp);
        return btn;
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }
}
