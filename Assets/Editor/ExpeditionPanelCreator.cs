using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// DungeonColony → Create Expedition Panel
//
// 원정대 편성 패널(G키)을 씬에 짓는다. 화면 중앙, 목록 + 요약 + [출발]/[취소].
// 필드 UI이므로 **캔버스 직속**에 둔다(`Canvas/WorldUI` 아래가 아니다 — 그건 월드 전용 묶음).
//
// 행 모양은 씬의 `RowTemplate`이 갖는다 — 런타임은 복제해 데이터만 붙인다.
// 다 쓰면 지운다(생성기의 수명). 이후 수정은 인스펙터에서.
public static class ExpeditionPanelCreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";
    const string RootName = "Expedition_UI";

    static readonly Vector2 PanelSize = new(420f, 520f);
    const float HeaderH = 40f;
    const float FooterH = 52f;

    static readonly Color PanelBg  = new(0.10f, 0.10f, 0.11f, 0.96f);
    static readonly Color HeaderBg = new(0.05098f, 0.05098f, 0.05098f, 1f);   // #0D0D0D 고정
    static readonly Color CloseBg  = new(0.50196f, 0.14902f, 0.14902f, 1f);   // #802626 고정
    static readonly Color FooterBg = new(0.06f, 0.06f, 0.08f, 1f);
    static readonly Color RowBg    = new(1f, 1f, 1f, 0.04f);
    static readonly Color BtnBg    = new(0.25f, 0.30f, 0.38f, 1f);
    static readonly Color CancelBg = new(0.34f, 0.22f, 0.22f, 1f);

    [MenuItem("DungeonColony/Create Expedition Panel")]
    static void Create()
    {
        var canvas = FindFieldCanvas();
        if (canvas == null) { Debug.LogError("[편성패널] 씬에 Canvas가 없습니다."); return; }

        var old = canvas.transform.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = MakeRect(RootName, canvas.transform);
        Undo.RegisterCreatedObjectUndo(root, "Create Expedition Panel");
        SetStretch(root);

        var panel = MakeRect("ExpeditionPanel", root.transform);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = PanelSize;
        panel.AddComponent<Image>().color = PanelBg;

        BuildTitleBar(panel);
        var content = BuildList(panel);
        var template = BuildRowTemplate(content);
        BuildFooter(panel, out var summary, out var status, out var depart, out var cancel);

        var comp = root.AddComponent<ExpeditionPanel>();
        var so   = new SerializedObject(comp);
        so.FindProperty("panel").objectReferenceValue        = panel;
        so.FindProperty("content").objectReferenceValue      = content;
        so.FindProperty("rowTemplate").objectReferenceValue  = template;
        so.FindProperty("summaryText").objectReferenceValue  = summary;
        so.FindProperty("statusText").objectReferenceValue   = status;
        so.FindProperty("departButton").objectReferenceValue = depart;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.ApplyModifiedProperties();

        EnsureFormation();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = panel;

        Debug.Log("[편성패널] 생성 완료 — G키로 열립니다.\n" +
                  "  [출발]을 누르면 고른 폰이 던전 핵 앞으로 걸어와 모이고, 전원 도착 시 함께 떠납니다.\n" +
                  "  행 모양은 RowTemplate에서, 패널 크기·색은 ExpeditionPanel에서 고치세요. 씬 저장 필요.");
    }

    // 집결 로직은 씬 어딘가에 하나 있어야 한다. 없으면 만들어 붙인다.
    static void EnsureFormation()
    {
        if (Object.FindFirstObjectByType<ExpeditionFormation>() != null) return;

        var host = Object.FindFirstObjectByType<WorldMapView>();
        if (host != null)
        {
            Undo.AddComponent<ExpeditionFormation>(host.gameObject);
            Debug.Log("[편성패널] ExpeditionFormation을 WorldMapView에 붙였습니다.");
            return;
        }

        var go = new GameObject("ExpeditionFormation");
        Undo.RegisterCreatedObjectUndo(go, "Create ExpeditionFormation");
        go.AddComponent<ExpeditionFormation>();
        Debug.Log("[편성패널] ExpeditionFormation 오브젝트를 새로 만들었습니다.");
    }

    // ── 조각 ─────────────────────────────────────────────────────────────────

    static void BuildTitleBar(GameObject panel)
    {
        var bar = MakeRect("TitleBar", panel.transform);
        var rt  = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, HeaderH);
        bar.AddComponent<Image>().color = HeaderBg;
        bar.AddComponent<DraggablePanel>();

        var so = new SerializedObject(bar.GetComponent<DraggablePanel>());
        so.FindProperty("target").objectReferenceValue = panel.GetComponent<RectTransform>();
        so.ApplyModifiedProperties();

        var title = MakeRect("Title", bar.transform);
        var trt = title.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12, 0); trt.offsetMax = new Vector2(-40, 0);
        var tmp = title.AddComponent<TextMeshProUGUI>();
        tmp.text = "원정대 편성"; tmp.fontSize = 16; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left; tmp.color = Color.white;
        ApplyFont(tmp);

        var close = MakeRect("CloseButton", bar.transform);
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-6, -6);
        crt.sizeDelta = new Vector2(28, 28);
        close.AddComponent<Image>().color = CloseBg;
        close.AddComponent<Button>();

        var ct = MakeRect("Text", close.transform); SetStretch(ct);
        var ctmp = ct.AddComponent<TextMeshProUGUI>();
        ctmp.text = "X"; ctmp.fontSize = 13;
        ctmp.alignment = TextAlignmentOptions.Center; ctmp.color = Color.white;
        ApplyFont(ctmp);

        var cc = close.AddComponent<CloseButton>();
        var cso = new SerializedObject(cc);
        cso.FindProperty("target").objectReferenceValue = panel;
        cso.ApplyModifiedProperties();
    }

    static Transform BuildList(GameObject panel)
    {
        var sv = MakeRect("ScrollView", panel.transform);
        var svRT = sv.GetComponent<RectTransform>();
        svRT.anchorMin = Vector2.zero; svRT.anchorMax = Vector2.one;
        svRT.offsetMin = new Vector2(0, FooterH);
        svRT.offsetMax = new Vector2(0, -HeaderH);

        var sr = sv.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;

        var vp = MakeRect("Viewport", sv.transform); SetStretch(vp);
        vp.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        vp.AddComponent<Mask>().showMaskGraphic = false;
        sr.viewport = vp.GetComponent<RectTransform>();

        var content = MakeRect("Content", vp.transform);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(0.5f, 1);
        crt.anchoredPosition = Vector2.zero; crt.sizeDelta = Vector2.zero;

        var vg = content.AddComponent<VerticalLayoutGroup>();
        vg.childControlWidth = vg.childControlHeight = true;
        vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
        vg.spacing = 3f; vg.padding = new RectOffset(8, 8, 6, 6);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = crt;

        return content.transform;
    }

    // 편집 중 보이는 행 하나. 런타임은 이걸 복제한다.
    static ExpeditionRowUI BuildRowTemplate(Transform content)
    {
        var row = MakeRect("RowTemplate", content);
        row.AddComponent<Image>().color = RowBg;
        row.AddComponent<LayoutElement>().minHeight = 36;

        // 체크박스 — Toggle은 배경/체크마크 두 그래픽이 있어야 눈에 보인다.
        var tg = MakeRect("Check", row.transform);
        var trt = tg.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 0.5f); trt.anchorMax = new Vector2(0, 0.5f);
        trt.pivot = new Vector2(0, 0.5f);
        trt.anchoredPosition = new Vector2(10, 0);
        trt.sizeDelta = new Vector2(20, 20);
        var bg = tg.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.18f);

        var mark = MakeRect("Checkmark", tg.transform);
        var mrt = mark.GetComponent<RectTransform>();
        mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
        mrt.offsetMin = new Vector2(4, 4); mrt.offsetMax = new Vector2(-4, -4);
        var markImg = mark.AddComponent<Image>();
        markImg.color = new Color(0.55f, 0.80f, 0.45f);

        var toggle = tg.AddComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = markImg;
        toggle.isOn = false;

        var nameGO = MakeRect("Name", row.transform);
        var nrt = nameGO.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 1);
        nrt.offsetMin = new Vector2(40, 0); nrt.offsetMax = new Vector2(-110, 0);
        var ntmp = nameGO.AddComponent<TextMeshProUGUI>();
        ntmp.text = "폰 이름"; ntmp.fontSize = 14;
        ntmp.alignment = TextAlignmentOptions.Left; ntmp.color = Color.white;
        ApplyFont(ntmp);

        var loadGO = MakeRect("Load", row.transform);
        var lrt = loadGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(1, 0); lrt.anchorMax = new Vector2(1, 1);
        lrt.pivot = new Vector2(1, 0.5f);
        lrt.anchoredPosition = new Vector2(-10, 0);
        lrt.sizeDelta = new Vector2(100, 0);
        var ltmp = loadGO.AddComponent<TextMeshProUGUI>();
        ltmp.text = "적재 +50"; ltmp.fontSize = 12;
        ltmp.alignment = TextAlignmentOptions.Right;
        ltmp.color = new Color(0.72f, 0.72f, 0.76f);
        ApplyFont(ltmp);

        var comp = row.AddComponent<ExpeditionRowUI>();
        var so = new SerializedObject(comp);
        so.FindProperty("check").objectReferenceValue    = toggle;
        so.FindProperty("nameText").objectReferenceValue = ntmp;
        so.FindProperty("loadText").objectReferenceValue = ltmp;
        so.ApplyModifiedProperties();

        row.SetActive(false);
        return comp;
    }

    static void BuildFooter(GameObject panel, out TextMeshProUGUI summary, out TextMeshProUGUI status,
                            out Button depart, out Button cancel)
    {
        var bar = MakeRect("Footer", panel.transform);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, FooterH);
        bar.AddComponent<Image>().color = FooterBg;

        summary = MakeText(bar.transform, "Summary", "선택 0명 · 적재 0", 13,
                           new Color(0.88f, 0.88f, 0.92f), TextAlignmentOptions.Left);
        var srt = summary.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 0.5f); srt.anchorMax = new Vector2(0, 1);
        srt.pivot = new Vector2(0, 0.5f);
        srt.anchoredPosition = new Vector2(12, -4);
        srt.sizeDelta = new Vector2(220, 20);

        status = MakeText(bar.transform, "Status", "데려갈 폰을 고르세요", 11,
                          new Color(0.65f, 0.65f, 0.70f), TextAlignmentOptions.Left);
        var strt = status.GetComponent<RectTransform>();
        strt.anchorMin = new Vector2(0, 0); strt.anchorMax = new Vector2(0, 0.5f);
        strt.pivot = new Vector2(0, 0.5f);
        strt.anchoredPosition = new Vector2(12, 6);
        strt.sizeDelta = new Vector2(220, 18);

        depart = MakeButton(bar.transform, "DepartButton", "출발", BtnBg,
                            new Vector2(-12, 0), new Vector2(92, 32));
        cancel = MakeButton(bar.transform, "CancelButton", "취소", CancelBg,
                            new Vector2(-112, 0), new Vector2(80, 32));
        cancel.gameObject.SetActive(false);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────

    static Button MakeButton(Transform parent, string name, string label, Color bg,
                             Vector2 pos, Vector2 size)
    {
        var go = MakeRect(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot = new Vector2(1, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = bg;

        var btn = go.AddComponent<Button>();
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        var t = MakeRect("Label", go.transform); SetStretch(t);
        var tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        ApplyFont(tmp);
        return btn;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size,
                                    Color color, TextAlignmentOptions align)
    {
        var go = MakeRect(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size;
        tmp.alignment = align; tmp.color = color;
        ApplyFont(tmp);
        return tmp;
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

    // 월드 루트 아래 캔버스는 필드 UI가 아니다.
    static Canvas FindFieldCanvas()
    {
        var view = Object.FindFirstObjectByType<WorldMapView>();
        var mapRoot = view != null ? view.MapRoot : null;

        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None))
        {
            if (c == null) continue;
            if (mapRoot != null && c.transform.IsChildOf(mapRoot)) continue;
            return c;
        }
        return null;
    }
}
