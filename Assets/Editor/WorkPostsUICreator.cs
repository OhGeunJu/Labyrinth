using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// **배치 창을 씬에 세운다**(2026-09-29, `WorkPostsUI` · `WorkPosts`) — 한 번 돌리면 씬이 정본이다(생성기의 수명).
//
// 규격(`docs/editor_creators.md`): 뿌리 `WorkPosts_UI`는 화면 전체 · 자리는 판 `WorkPosts_Panel`이 든다 · 헤더 #0D0D0D 풀폭
// (레이아웃 그룹 없음) · 닫기 28×28 #802626 `X` + 공용 `CloseButton` · 폰트 NanumGothic · 특수문자 없음 · 줄은 씬의 템플릿.
// **다시 돌리면 판의 자리를 물려준다**(사람이 옮긴 것은 판이다).
public static class WorkPostsUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    const float W = 820f, H = 460f, HeadH = 40f, ColH = 26f, RowH = 34f;
    const float NameW = 170f, SlotW = 190f, Gap = 10f, Pad = 12f;

    static readonly Color PanelBg = new(0.11f, 0.11f, 0.13f, 0.97f);
    static readonly Color Header  = new(0.05098f, 0.05098f, 0.05098f, 1f);   // #0D0D0D
    static readonly Color CloseBg = new(0.50196f, 0.14902f, 0.14902f, 1f);   // #802626
    static readonly Color RowBg   = new(0.16f, 0.16f, 0.19f, 1f);
    static readonly Color DdBg    = new(0.22f, 0.22f, 0.26f, 1f);
    static readonly Color Muted   = new(0.65f, 0.65f, 0.70f, 1f);

    [MenuItem("DungeonColony/UI/Create Work Posts Window (배치 창 · 1~3순위)")]
    static void Create()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError("[배치 창] 씬에 캔버스가 없다."); return; }

        Vector2? keepPos = null;
        var old = Object.FindFirstObjectByType<WorkPostsUI>(FindObjectsInactive.Include);
        if (old != null)
        {
            var p = old.transform.Find("WorkPosts_Panel") as RectTransform;
            if (p != null) keepPos = p.anchoredPosition;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        var root = MakeRect("WorkPosts_UI", canvas.transform);
        Undo.RegisterCreatedObjectUndo(root, "배치 창");
        Stretch(root);

        var panel = MakeRect("WorkPosts_Panel", root.transform);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(W, H);
        prt.anchoredPosition = keepPos ?? Vector2.zero;
        panel.AddComponent<Image>().color = PanelBg;

        // 헤더 — 풀폭 · 레이아웃 그룹 없음.
        var head = MakeRect("Header", panel.transform);
        var hrt = head.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
        hrt.anchoredPosition = Vector2.zero; hrt.sizeDelta = new Vector2(0, HeadH);
        head.AddComponent<Image>().color = Header;
        // 제목은 이름 하나만(2026-09-29, 사용자: *"배치 - 1순위 뭐 이런 거 쓰지 말고 그런 이름으로만"*) — 규칙 설명은 제목 자리가 아니다.
        var title = Text("Title", head.transform, "지휘소", 15,
                         TextAlignmentOptions.MidlineLeft, Color.white);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 1);
        trt.offsetMin = new Vector2(Pad, 0); trt.offsetMax = new Vector2(-48, 0);

        var close = MakeRect("CloseButton", head.transform);
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-6, -6); crt.sizeDelta = new Vector2(28, 28);
        close.AddComponent<Image>().color = CloseBg;
        close.AddComponent<Button>();
        var x = Text("Text", close.transform, "X", 13, TextAlignmentOptions.Center, Color.white);
        StretchRt(x.rectTransform);
        var cb = close.AddComponent<CloseButton>();
        var cso = new SerializedObject(cb);
        cso.FindProperty("target").objectReferenceValue = panel;
        cso.ApplyModifiedPropertiesWithoutUndo();

        // 열 머리 — 이름 · 1순위 · 2순위 · 3순위.
        var cols = MakeRect("Columns", panel.transform);
        var colrt = cols.GetComponent<RectTransform>();
        colrt.anchorMin = new Vector2(0, 1); colrt.anchorMax = new Vector2(1, 1); colrt.pivot = new Vector2(0.5f, 1);
        colrt.anchoredPosition = new Vector2(0, -HeadH); colrt.sizeDelta = new Vector2(0, ColH);
        PlaceLeft(Text("Name", cols.transform, "폰", 13, TextAlignmentOptions.MidlineLeft, Muted).rectTransform, Pad + 8, NameW);
        for (int i = 0; i < WorkPosts.Count; i++)
            PlaceLeft(Text($"Slot{i + 1}", cols.transform, $"{i + 1}순위", 13, TextAlignmentOptions.MidlineLeft, Muted).rectTransform,
                      SlotX(i) + 8, SlotW);

        // 스크롤 — 열 머리 아래를 채운다.
        var sv = MakeRect("ScrollView", panel.transform);
        var svrt = sv.GetComponent<RectTransform>();
        svrt.anchorMin = Vector2.zero; svrt.anchorMax = Vector2.one;
        svrt.offsetMin = new Vector2(0, Pad); svrt.offsetMax = new Vector2(0, -(HeadH + ColH));
        var sr = sv.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;
        var vp = MakeRect("Viewport", sv.transform);
        Stretch(vp);
        vp.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        vp.AddComponent<Mask>().showMaskGraphic = false;
        sr.viewport = vp.GetComponent<RectTransform>();
        var content = MakeRect("Content", vp.transform);
        var ctrt = content.GetComponent<RectTransform>();
        ctrt.anchorMin = new Vector2(0, 1); ctrt.anchorMax = new Vector2(1, 1); ctrt.pivot = new Vector2(0.5f, 1);
        ctrt.anchoredPosition = Vector2.zero; ctrt.sizeDelta = Vector2.zero;
        var vg = content.AddComponent<VerticalLayoutGroup>();
        vg.childControlWidth = vg.childControlHeight = true;
        vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
        vg.spacing = 4f; vg.padding = new RectOffset((int)Pad, (int)Pad, 4, 4);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = ctrt;

        var row = MakeRow(content.transform);
        var empty = Text("Empty", content.transform, "일꾼이 없다.", 14, TextAlignmentOptions.Center, Muted);
        empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

        var ui = root.AddComponent<WorkPostsUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue       = panel;
        so.FindProperty("content").objectReferenceValue     = content.transform;
        so.FindProperty("rowTemplate").objectReferenceValue = row;
        so.FindProperty("emptyText").objectReferenceValue   = empty;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);   // 판은 닫힌 채로 — `H`키가 연다
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[배치 창] `WorkPosts_UI`를 지었다 — `H`키나 거점 바의 [지휘소]로 연다.");
    }

    static float SlotX(int i) => Pad + NameW + Gap + i * (SlotW + Gap);

    // 한 줄 — 이름 + 드롭다운 셋. 런타임이 복제한다(`WorkPostsUI.rowTemplate`).
    static WorkPostRowUI MakeRow(Transform parent)
    {
        var row = MakeRect("RowTemplate", parent);
        row.AddComponent<Image>().color = RowBg;
        row.AddComponent<LayoutElement>().preferredHeight = RowH;

        var name = Text("Name", row.transform, "폰 이름", 14, TextAlignmentOptions.MidlineLeft, Color.white);
        PlaceLeft(name.rectTransform, 8, NameW - 8);

        var dds = new TMP_Dropdown[WorkPosts.Count];
        for (int i = 0; i < WorkPosts.Count; i++)
        {
            var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            go.name = $"Slot{i + 1}";
            go.transform.SetParent(row.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(SlotX(i) - Pad, 0); rt.sizeDelta = new Vector2(SlotW, RowH - 6);

            foreach (var img in go.GetComponentsInChildren<Image>(true))
                if (img.gameObject == go || img.name == "Template" || img.name == "Item Background") img.color = DdBg;
            foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                ApplyFont(t);
                t.fontSize = 13;
                t.color = Color.white;
            }
            dds[i] = go.GetComponent<TMP_Dropdown>();
        }

        var comp = row.AddComponent<WorkPostRowUI>();
        var so = new SerializedObject(comp);
        so.FindProperty("nameText").objectReferenceValue = name;
        var arr = so.FindProperty("slots");
        arr.arraySize = dds.Length;
        for (int i = 0; i < dds.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = dds[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    // ── 헬퍼(규격 문서의 것) ─────────────────────────────────────────────────

    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent != null ? parent.gameObject.layer : 5;
        return go;
    }

    static void Stretch(GameObject go) => StretchRt(go.GetComponent<RectTransform>());

    static void StretchRt(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void PlaceLeft(RectTransform rt, float x, float w)
    {
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0); rt.sizeDelta = new Vector2(w, 0);
    }

    static TextMeshProUGUI Text(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color color)
    {
        var go = MakeRect(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.alignment = align; t.color = color;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        ApplyFont(t);
        return t;
    }

    static void ApplyFont(TextMeshProUGUI t)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) t.font = font;
    }
}
