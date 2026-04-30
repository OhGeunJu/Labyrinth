using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class StorageUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    [MenuItem("DungeonColony/Create Storage Building UI")]
    static void Create()
    {
        var canvas = GetOrCreateCanvas();

        // ── 루트 패널 ─────────────────────────────────────────────────────
        var panel = MakeRect("StorageBuildingPanel", canvas.transform);
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1, 0);
        rt.anchorMax        = new Vector2(1, 0);
        rt.pivot            = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(-16, 16);
        rt.sizeDelta        = new Vector2(380, 600);
        panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        panel.SetActive(false);

        // ── 타이틀 바 (상단 고정 50px) ────────────────────────────────────
        var titleBar = MakeRect("TitleBar", panel.transform);
        var titleRT  = titleBar.GetComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0, 1);
        titleRT.anchorMax        = new Vector2(1, 1);
        titleRT.pivot            = new Vector2(0.5f, 1);
        titleRT.anchoredPosition = Vector2.zero;
        titleRT.sizeDelta        = new Vector2(0, 50);
        titleBar.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 1f);

        // 타이틀 텍스트
        var titleGO  = MakeRect("TitleText", titleBar.transform);
        var titleTRT = titleGO.GetComponent<RectTransform>();
        titleTRT.anchorMin        = new Vector2(0, 0);
        titleTRT.anchorMax        = new Vector2(1, 1);
        titleTRT.offsetMin        = new Vector2(12, 0);
        titleTRT.offsetMax        = new Vector2(-50, 0);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "창고";
        titleTMP.fontSize  = 16;
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

        // ── 그리드 컨테이너 (타이틀 바 아래 ~ 패널 하단) ─────────────────
        var gridContainer = MakeRect("GridContainer", panel.transform);
        var gridRT        = gridContainer.GetComponent<RectTransform>();
        gridRT.anchorMin        = new Vector2(0, 0);
        gridRT.anchorMax        = new Vector2(1, 1);
        gridRT.offsetMin        = new Vector2(8, 8);
        gridRT.offsetMax        = new Vector2(-8, -58);  // 상단 50+여백 확보

        // ── StorageBuildingUI 연결 ────────────────────────────────────────
        var ui = panel.AddComponent<StorageBuildingUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue         = panel;
        so.FindProperty("titleText").objectReferenceValue     = titleTMP;
        so.FindProperty("gridContainer").objectReferenceValue = gridRT;
        so.ApplyModifiedProperties();

        // CloseButton
        var closeBtnComp = closeGO.AddComponent<CloseButton>();
        var closeSO      = new SerializedObject(closeBtnComp);
        closeSO.FindProperty("Target").objectReferenceValue = panel.GetComponent<RectTransform>();
        closeSO.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(panel, "Create StorageBuildingUI");
        Selection.activeGameObject = panel;
        Debug.Log("[StorageUICreator] 창고 UI 생성 완료");
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

    static void SetStretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }
}
