using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class BedUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    [MenuItem("DungeonColony/Create Bed UI")]
    static void Create()
    {
        var canvas = GetOrCreateCanvas();

        // ── 루트 패널 ─────────────────────────────────────────────────────
        var panel = MakeRect("BedPanel", canvas.transform);
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 80);
        rt.sizeDelta        = new Vector2(280, 160);
        panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        panel.SetActive(false);

        // ── 타이틀 바 ────────────────────────────────────────────────────
        var titleBar = MakeRect("TitleBar", panel.transform);
        var titleRT  = titleBar.GetComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0, 1);
        titleRT.anchorMax        = new Vector2(1, 1);
        titleRT.pivot            = new Vector2(0.5f, 1);
        titleRT.anchoredPosition = Vector2.zero;
        titleRT.sizeDelta        = new Vector2(0, 44);
        titleBar.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 1f);

        // 타이틀 텍스트
        var titleGO  = MakeRect("TitleText", titleBar.transform);
        SetStretch(titleGO, new Vector2(12, 0), new Vector2(-50, 0));
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "침대";
        titleTMP.fontSize  = 15;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color     = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Left;
        ApplyFont(titleTMP);

        // 닫기 버튼
        var closeGO  = MakeRect("CloseButton", titleBar.transform);
        var closeRT  = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin        = new Vector2(1, 0);
        closeRT.anchorMax        = new Vector2(1, 1);
        closeRT.pivot            = new Vector2(1, 0.5f);
        closeRT.anchoredPosition = new Vector2(-6, 0);
        closeRT.sizeDelta        = new Vector2(38, -10);
        closeGO.AddComponent<Image>().color = new Color(0.5f, 0.15f, 0.15f, 1f);
        closeGO.AddComponent<Button>();
        var closeTxtGO = MakeRect("Text", closeGO.transform);
        SetStretch(closeTxtGO);
        var closeTMP = closeTxtGO.AddComponent<TextMeshProUGUI>();
        closeTMP.text      = "✕";
        closeTMP.fontSize  = 14;
        closeTMP.alignment = TextAlignmentOptions.Center;
        closeTMP.color     = Color.white;
        ApplyFont(closeTMP);

        // ── 배정 유닛 표시 ────────────────────────────────────────────────
        var assignedLabelGO = MakeRect("AssignedLabel", panel.transform);
        var aLabelRT        = assignedLabelGO.GetComponent<RectTransform>();
        aLabelRT.anchorMin        = new Vector2(0, 1);
        aLabelRT.anchorMax        = new Vector2(1, 1);
        aLabelRT.pivot            = new Vector2(0.5f, 1);
        aLabelRT.anchoredPosition = new Vector2(0, -52);
        aLabelRT.sizeDelta        = new Vector2(-20, 24);
        var aLabelTMP = assignedLabelGO.AddComponent<TextMeshProUGUI>();
        aLabelTMP.text      = "배정 유닛:";
        aLabelTMP.fontSize  = 12;
        aLabelTMP.color     = new Color(0.7f, 0.7f, 0.7f, 1f);
        aLabelTMP.alignment = TextAlignmentOptions.Left;
        ApplyFont(aLabelTMP);

        var assignedTextGO = MakeRect("AssignedUnitText", panel.transform);
        var aTextRT        = assignedTextGO.GetComponent<RectTransform>();
        aTextRT.anchorMin        = new Vector2(0, 1);
        aTextRT.anchorMax        = new Vector2(1, 1);
        aTextRT.pivot            = new Vector2(0.5f, 1);
        aTextRT.anchoredPosition = new Vector2(0, -76);
        aTextRT.sizeDelta        = new Vector2(-20, 28);
        var aTextTMP = assignedTextGO.AddComponent<TextMeshProUGUI>();
        aTextTMP.text      = "미배정";
        aTextTMP.fontSize  = 14;
        aTextTMP.fontStyle = FontStyles.Bold;
        aTextTMP.color     = Color.white;
        aTextTMP.alignment = TextAlignmentOptions.Center;
        ApplyFont(aTextTMP);

        // ── 버튼 행 ───────────────────────────────────────────────────────
        var assignBtn   = MakeButton("AssignButton",   "선택 유닛 배정", panel.transform,
                                     new Vector2(-72, -125), new Vector2(118, 30));
        var unassignBtn = MakeButton("UnassignButton", "배정 해제",       panel.transform,
                                     new Vector2(72,  -125), new Vector2(110, 30));

        // ── CloseButton 연결 ──────────────────────────────────────────────
        var closeBtnComp = closeGO.AddComponent<CloseButton>();
        var closeSO      = new SerializedObject(closeBtnComp);
        closeSO.FindProperty("Target").objectReferenceValue = panel.GetComponent<RectTransform>();
        closeSO.ApplyModifiedProperties();

        // ── BedUI 연결 ────────────────────────────────────────────────────
        var ui = panel.AddComponent<BedUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue            = panel;
        so.FindProperty("titleText").objectReferenceValue        = titleTMP;
        so.FindProperty("assignedUnitText").objectReferenceValue = aTextTMP;
        so.FindProperty("assignButton").objectReferenceValue     = assignBtn;
        so.FindProperty("unassignButton").objectReferenceValue   = unassignBtn;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(panel, "Create BedUI");
        Selection.activeGameObject = panel;
        Debug.Log("[BedUICreator] 침대 UI 생성 완료 — SelectionManager를 인스펙터에서 연결해주세요.");
    }

    // ── 유틸 ──────────────────────────────────────────────────────────────

    static Canvas GetOrCreateCanvas()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null) return canvas;
        var go = new GameObject("Canvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        return canvas;
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
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = offsetMin;
        rt.offsetMax        = offsetMax;
        rt.anchoredPosition = Vector2.zero;
    }

    static Button MakeButton(string name, string label, Transform parent,
                              Vector2 anchoredPos, Vector2 size)
    {
        var go = MakeRect(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1);
        rt.anchorMax        = new Vector2(0.5f, 1);
        rt.pivot            = new Vector2(0.5f, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;
        go.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.22f, 1f);
        var btn = go.AddComponent<Button>();

        var textGO = MakeRect("Text", go.transform);
        SetStretch(textGO);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 12;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        ApplyFont(tmp);

        return btn;
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }
}
