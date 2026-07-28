using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// DungeonColony/Create Colony Panel 실행
// → 무리 관리 패널(P) 생성 + 구버전 우선순위 UI(Priority_UI / PriorityManager) 정리.
//
// 행(폰)과 열 제목은 런타임에 ColonyPanel이 만든다(작업 카테고리 목록에서 파생).
// 이 스크립트는 껍데기(헤더·열 제목 행·스크롤·하단 안내)만 만들고 필드를 연결한다.
public static class ColonyPanelCreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    const float HeaderH    = 32f;
    const float ColHeaderH = 22f;
    const float FooterH    = 26f;
    const float PanelH     = 420f;

    static readonly Color HeaderColor = new Color(0.05098f, 0.05098f, 0.05098f, 1f); // #0D0D0D
    static readonly Color CloseColor  = new Color(0.50196f, 0.14902f, 0.14902f, 1f); // #802626

    [MenuItem("DungeonColony/Create Colony Panel")]
    static void Create()
    {
        var canvas = GetOrCreateCanvas();

        CleanUpLegacyPriorityUI();

        // 기존 무리 관리 패널이 있으면 통째로 새로 만든다
        var existing = Object.FindFirstObjectByType<ColonyPanel>();
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var root = MakeRect("Colony_UI", canvas.transform);
        SetStretch(root);

        float panelW = ColonyColumns.TotalWidth + 20f;

        // ── 패널 루트 (화면 중앙)
        var panel   = MakeRect("ColonyPanel", root.transform);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot     = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = new Vector2(panelW, PanelH);
        panel.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.97f);

        // ── 헤더 (상단 풀폭). 규칙 1: LayoutGroup 없이 앵커 배치
        var header   = MakeRect("Header", panel.transform);
        var headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f); headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot     = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0f, HeaderH);
        header.AddComponent<Image>().color = HeaderColor;

        var dragComp = header.AddComponent<DraggablePanel>();
        var dragSO   = new SerializedObject(dragComp);
        dragSO.FindProperty("target").objectReferenceValue = panelRT;
        dragSO.ApplyModifiedProperties();

        var titleTMP = MakeText("Title", header.transform, "무리 관리", 15, TextAlignmentOptions.MidlineLeft);
        var titleRT  = titleTMP.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(10f, 0f); titleRT.offsetMax = new Vector2(-34f, 0f);

        // 닫기 버튼 (규칙 2: 텍스트 "X" / 규칙 3: 공용 CloseButton)
        var closeGO = MakeRect("CloseButton", header.transform);
        var closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot     = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-2f, -2f);
        closeRT.sizeDelta = new Vector2(28f, 28f);
        closeGO.AddComponent<Image>().color = CloseColor;
        closeGO.AddComponent<Button>();
        var cLabel = MakeRect("Label", closeGO.transform);
        SetStretch(cLabel);
        var cTMP = cLabel.AddComponent<TextMeshProUGUI>();
        cTMP.text = "X"; cTMP.fontSize = 14; cTMP.color = Color.white;
        cTMP.alignment = TextAlignmentOptions.Center; cTMP.raycastTarget = false;
        ApplyFont(cTMP);
        var closeComp = closeGO.AddComponent<CloseButton>();
        var closeSO   = new SerializedObject(closeComp);
        closeSO.FindProperty("target").objectReferenceValue = panel;
        closeSO.ApplyModifiedProperties();

        // ── 열 제목 행 (헤더 바로 아래, 라벨은 런타임 생성)
        var colHeader   = MakeRect("ColumnHeader", panel.transform);
        var colHeaderRT = colHeader.GetComponent<RectTransform>();
        colHeaderRT.anchorMin = new Vector2(0f, 1f); colHeaderRT.anchorMax = new Vector2(1f, 1f);
        colHeaderRT.pivot     = new Vector2(0.5f, 1f);
        colHeaderRT.anchoredPosition = new Vector2(0f, -HeaderH);
        colHeaderRT.sizeDelta = new Vector2(0f, ColHeaderH);
        colHeader.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);

        // ── 스크롤 뷰 (열 제목 ~ 하단 안내 사이)
        var sv   = MakeRect("ScrollView", panel.transform);
        var svRT = sv.GetComponent<RectTransform>();
        svRT.anchorMin = Vector2.zero; svRT.anchorMax = Vector2.one;
        svRT.offsetMin = new Vector2(0f, FooterH);
        svRT.offsetMax = new Vector2(0f, -(HeaderH + ColHeaderH));

        var sr = sv.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 5f;   // 20은 한 노치에 목록이 통째로 넘어갈 만큼 과했다

        var vp = MakeRect("Viewport", sv.transform);
        SetStretch(vp);
        vp.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        vp.AddComponent<Mask>().showMaskGraphic = false;
        sr.viewport = vp.GetComponent<RectTransform>();

        var content   = MakeRect("Content", vp.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f); contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero; contentRT.sizeDelta = Vector2.zero;
        var vg = content.AddComponent<VerticalLayoutGroup>();
        vg.childControlWidth = vg.childControlHeight = true;
        vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
        vg.spacing = 2f; vg.padding = new RectOffset(6, 6, 4, 4);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = contentRT;

        // ── 하단 안내 (우선순위 모델을 화면에서 설명)
        var footer   = MakeRect("Footer", panel.transform);
        var footerRT = footer.GetComponent<RectTransform>();
        footerRT.anchorMin = new Vector2(0f, 0f); footerRT.anchorMax = new Vector2(1f, 0f);
        footerRT.pivot     = new Vector2(0.5f, 0f);
        footerRT.anchoredPosition = Vector2.zero;
        footerRT.sizeDelta = new Vector2(0f, FooterH);
        footer.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 1f);

        var hint = MakeText("Hint", footer.transform,
                            "왼쪽 열의 작업을 먼저 처리합니다 · 채굴/벌목/순찰은 대상에서 직접 배정",
                            11, TextAlignmentOptions.MidlineLeft);
        hint.color = new Color(0.62f, 0.62f, 0.66f);
        var hintRT = hint.GetComponent<RectTransform>();
        hintRT.anchorMin = Vector2.zero; hintRT.anchorMax = Vector2.one;
        hintRT.offsetMin = new Vector2(10f, 0f); hintRT.offsetMax = new Vector2(-10f, 0f);

        // ── 컴포넌트 연결
        var comp = root.AddComponent<ColonyPanel>();
        var so   = new SerializedObject(comp);
        so.FindProperty("panel").objectReferenceValue     = panel;
        so.FindProperty("headerRow").objectReferenceValue = colHeader.transform;
        so.FindProperty("content").objectReferenceValue   = content.transform;
        so.FindProperty("selectionManager").objectReferenceValue =
            Object.FindFirstObjectByType<SelectionManager>();
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(root, "Create Colony Panel");
        Selection.activeGameObject = root;
        Debug.Log("[ColonyPanelCreator] 무리 관리 패널 생성 완료 (P키로 토글)");
    }

    // 구버전 우선순위 UI 제거 — 스크립트가 삭제돼 스크립트 없는 오브젝트로 남아 있다.
    static void CleanUpLegacyPriorityUI()
    {
        string[] legacyNames = { "Priority_UI", "PriorityPanel", "PriorityManager" };

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,
                                                               FindObjectsSortMode.None))
        {
            if (go == null) continue;
            foreach (var name in legacyNames)
            {
                if (go.name != name) continue;
                Debug.Log($"[ColonyPanelCreator] 구버전 오브젝트 제거: {name}");
                Undo.DestroyObjectImmediate(go);
                break;
            }
        }
    }

    // ── 헬퍼 ──────────────────────────────────────────────
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

    static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size,
                                    TextAlignmentOptions align)
    {
        var go  = MakeRect(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = Color.white;
        tmp.alignment = align; tmp.raycastTarget = false;
        ApplyFont(tmp);
        return tmp;
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }
}
