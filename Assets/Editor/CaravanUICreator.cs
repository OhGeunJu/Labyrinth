using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// **상단 창을 씬에 세운다**(2026-10-01, `CaravanUI`) — 한 번 돌리면 씬이 정본이다(생성기의 수명).
//
// 규격(`docs/editor_creators.md`): 뿌리 `Caravan_UI`는 화면 전체 · 자리는 판 `Caravan_Panel` · 헤더 #0D0D0D 풀폭(레이아웃
// 그룹 없음) · 닫기 28×28 #802626 `X` + 공용 `CloseButton` · 폰트 NanumGothic · 특수문자 없음 · 줄은 씬의 템플릿.
// **다시 돌리면 판의 자리를 물려준다.**
public static class CaravanUICreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    const float W = 1000f, H = 680f, HeadH = 40f, Pad = 12f, ListW = 220f;
    const float RowH = 30f, RuleColW = 240f;

    static readonly Color PanelBg = new(0.11f, 0.11f, 0.13f, 0.97f);
    static readonly Color Header  = new(0.05098f, 0.05098f, 0.05098f, 1f);   // #0D0D0D
    static readonly Color CloseBg = new(0.50196f, 0.14902f, 0.14902f, 1f);   // #802626
    static readonly Color PaneBg  = new(0.08f, 0.08f, 0.10f, 1f);
    static readonly Color RowBg   = new(0.16f, 0.16f, 0.19f, 1f);
    static readonly Color FieldBg = new(0.22f, 0.22f, 0.26f, 1f);
    static readonly Color BtnBg   = new(0.24f, 0.30f, 0.38f, 1f);
    static readonly Color GoBg    = new(0.26f, 0.40f, 0.24f, 1f);
    static readonly Color Muted   = new(0.65f, 0.65f, 0.70f, 1f);

    [MenuItem("DungeonColony/UI/Create Caravan Window (상단 창 · T)")]
    static void Create()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError("[상단 창] 씬에 캔버스가 없다."); return; }

        Vector2? keepPos = null;
        var old = Object.FindFirstObjectByType<CaravanUI>(FindObjectsInactive.Include);
        if (old != null)
        {
            var p = old.transform.Find("Caravan_Panel") as RectTransform;
            if (p != null) keepPos = p.anchoredPosition;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        var root = MakeRect("Caravan_UI", canvas.transform);
        Undo.RegisterCreatedObjectUndo(root, "상단 창");
        Stretch(root);

        var panel = MakeRect("Caravan_Panel", root.transform);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(W, H);
        prt.anchoredPosition = keepPos ?? Vector2.zero;
        panel.AddComponent<Image>().color = PanelBg;

        // ── 헤더 ────────────────────────────────────────────────────────────
        var head = MakeRect("Header", panel.transform);
        var hrt = head.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
        hrt.anchoredPosition = Vector2.zero; hrt.sizeDelta = new Vector2(0, HeadH);
        head.AddComponent<Image>().color = Header;
        var title = Text("Title", head.transform, "상단", 15, TextAlignmentOptions.MidlineLeft, Color.white);
        var trt = title.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(Pad, 0); trt.offsetMax = new Vector2(-48, 0);

        var close = MakeRect("CloseButton", head.transform);
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-6, -6); crt.sizeDelta = new Vector2(28, 28);
        close.AddComponent<Image>().color = CloseBg;
        close.AddComponent<Button>();
        StretchRt(Text("Text", close.transform, "X", 13, TextAlignmentOptions.Center, Color.white).rectTransform);
        var cb = close.AddComponent<CloseButton>();
        var cso = new SerializedObject(cb);
        cso.FindProperty("target").objectReferenceValue = panel;
        cso.ApplyModifiedPropertiesWithoutUndo();

        // ── 왼쪽 — 목록 ─────────────────────────────────────────────────────
        var listPane = MakeRect("ListPane", panel.transform);
        Place(listPane, Pad, HeadH + Pad, ListW, H - HeadH - Pad * 2);
        listPane.AddComponent<Image>().color = PaneBg;
        var listContent = Scroll("ListScroll", listPane.transform, 0, 0, ListW, H - HeadH - Pad * 2 - 44, out _);
        var listRow = MakeButton("RowTemplate", listContent, "1번 상단", BtnBg, 13);
        listRow.GetComponent<LayoutElement>().preferredHeight = 44;
        listRow.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
        listRow.GetComponentInChildren<TextMeshProUGUI>().rectTransform.offsetMin = new Vector2(8, 0);
        var addBtn = MakeButton("AddButton", listPane.transform, "새 상단", GoBg, 13);
        PlaceBottomLeft(addBtn.gameObject, 6, 6, (ListW - 18) / 2, 32);
        var removeBtn = MakeButton("RemoveButton", listPane.transform, "해산", CloseBg, 13);
        PlaceBottomLeft(removeBtn.gameObject, 12 + (ListW - 18) / 2, 6, (ListW - 18) / 2, 32);

        var locked = Text("LockedText", panel.transform,
                          "아직 교역할 곳을 모른다.\n마을을 찾으면 상단을 꾸릴 수 있다.", 16, TextAlignmentOptions.Center, Muted);
        StretchRt(locked.rectTransform);
        locked.textWrappingMode = TextWrappingModes.Normal;

        // ── 오른쪽 — 상세 ───────────────────────────────────────────────────
        float dx = Pad + ListW + Pad, dw = W - dx - Pad, dy = HeadH + Pad, dh = H - dy - Pad;
        var detail = MakeRect("Detail", panel.transform);
        Place(detail, dx, dy, dw, dh);
        var d = detail.transform;

        // 줄 A — 이름 · 상태
        var nameInput = Input("NameInput", d, "상단 이름");
        Place(nameInput.gameObject, 0, 0, 220, RowH);
        var status = Text("StatusText", d, "대기", 14, TextAlignmentOptions.MidlineLeft, Muted);
        Place(status.gameObject, 232, 0, dw - 232, RowH);

        // 줄 B — 출발 · 목적지
        float yB = RowH + 8;
        Place(Label("HomeLabel", d, "출발").gameObject, 0, yB, 40, RowH);
        var home = Dropdown("HomeDropdown", d);
        Place(home.gameObject, 44, yB, 120, RowH);
        Place(Label("DestLabel", d, "목적지").gameObject, 180, yB, 56, RowH);
        var dest = Text("DestText", d, "정하지 않음", 14, TextAlignmentOptions.MidlineLeft, Color.white);
        Place(dest.gameObject, 238, yB, 330, RowH);
        var pick = MakeButton("PickDestButton", d, "지도에서 고르기", BtnBg, 13);
        Place(pick.gameObject, dw - 140, yB, 140, RowH);

        // 줄 C — 대표 · 주기 · 예산
        float yC = yB + RowH + 8;
        Place(Label("LeaderLabel", d, "대표").gameObject, 0, yC, 40, RowH);
        var leader = Dropdown("LeaderDropdown", d);
        Place(leader.gameObject, 44, yC, 220, RowH);
        var auto = MakeToggle("AutoToggle", d, "주기 출발");
        Place(auto.gameObject, 280, yC, 110, RowH);
        var interval = Input("IntervalInput", d, "3");
        Place(interval.gameObject, 392, yC, 44, RowH);
        Place(Label("IntervalLabel", d, "일마다").gameObject, 440, yC, 56, RowH);
        Place(Label("BudgetLabel", d, "예산").gameObject, 512, yC, 40, RowH);
        var budget = Input("BudgetInput", d, "500");
        Place(budget.gameObject, 554, yC, 80, RowH);
        Place(Label("BudgetUnit", d, "G").gameObject, 638, yC, 20, RowH);

        // 일행 — 세 줄짜리 격자
        float yM = yC + RowH + 10;
        Place(Label("CrewLabel", d, "일행 (대표 말고 데려갈 사람과 가축)").gameObject, 0, yM, dw, 20);
        var crewBox = MakeRect("CrewBox", d);
        Place(crewBox, 0, yM + 22, dw, 96);
        crewBox.AddComponent<Image>().color = PaneBg;
        var crewContent = Scroll("CrewScroll", crewBox.transform, 0, 0, dw, 96, out _, grid: true);
        var member = MakeToggle("MemberTemplate", crewContent, "이름");

        // 규칙 — 판매 · 구매 · 우선 구매
        float yR = yM + 22 + 96 + 12, rh = 236;
        var sell  = RuleColumn("Sell",     d, 0,                       yR, rh, "판매 (창고 수량 기준)", true,  out var sellTpl,  out var addSell);
        var buy   = RuleColumn("Buy",      d, RuleColW + 12,           yR, rh, "구매 (이 수까지 채운다)", false, out var buyTpl,   out var addBuy);
        var prio  = RuleColumn("Priority", d, (RuleColW + 12) * 2,     yR, rh, "우선 구매 (있으면 먼저)", false, out var prioTpl,  out var addPrio);

        // 출발 · 보고
        float yF = yR + rh + 10;
        var depart = MakeButton("DepartButton", d, "지금 출발", GoBg, 14);
        Place(depart.gameObject, 0, yF, 140, 34);
        var note = Text("DepartNote", d, "", 13, TextAlignmentOptions.MidlineLeft, Muted);
        Place(note.gameObject, 150, yF, dw - 150, 34);
        note.textWrappingMode = TextWrappingModes.Normal;
        var report = Text("ReportText", d, "", 12, TextAlignmentOptions.TopLeft, Muted);
        Place(report.gameObject, 0, yF + 40, dw, dh - (yF + 40));
        report.textWrappingMode = TextWrappingModes.Normal;
        report.overflowMode = TextOverflowModes.Truncate;

        // ── 배선 ────────────────────────────────────────────────────────────
        var ui = root.AddComponent<CaravanUI>();
        var so = new SerializedObject(ui);
        void Set(string f, Object v) => so.FindProperty(f).objectReferenceValue = v;
        Set("panel", panel);
        Set("listContent", listContent); Set("listRowTemplate", listRow); Set("addButton", addBtn); Set("removeButton", removeBtn);
        Set("lockedText", locked);
        Set("detailRoot", detail); Set("nameInput", nameInput); Set("statusText", status);
        Set("homeDropdown", home); Set("destText", dest); Set("pickDestButton", pick);
        Set("leaderDropdown", leader); Set("autoToggle", auto); Set("intervalInput", interval); Set("budgetInput", budget);
        Set("memberContent", crewContent); Set("memberTemplate", member);
        Set("sellContent", sell); Set("sellTemplate", sellTpl); Set("addSellButton", addSell);
        Set("buyContent", buy); Set("buyTemplate", buyTpl); Set("addBuyButton", addBuy);
        Set("priorityContent", prio); Set("priorityTemplate", prioTpl); Set("addPriorityButton", addPrio);
        Set("departButton", depart); Set("departNote", note); Set("reportText", report);
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[상단 창] `Caravan_UI`를 지었다 - T로 연다.");
    }

    // ── 규칙 한 갈래 — 제목 · 스크롤 · 줄 템플릿 · [추가] ─────────────────────

    static Transform RuleColumn(string key, Transform parent, float x, float y, float h, string title, bool withMode,
                                out CaravanRuleRowUI template, out Button add)
    {
        var col = MakeRect(key + "Column", parent);
        Place(col, x, y, RuleColW, h);
        col.AddComponent<Image>().color = PaneBg;
        var t = Text("Title", col.transform, title, 13, TextAlignmentOptions.MidlineLeft, Muted);
        Place(t.gameObject, 6, 0, RuleColW - 12, 22);

        var content = Scroll(key + "Scroll", col.transform, 0, 24, RuleColW, h - 24 - 36, out _);

        var row = MakeRect("RowTemplate", content);
        row.AddComponent<Image>().color = RowBg;
        row.AddComponent<LayoutElement>().preferredHeight = 28;
        var item = Dropdown("Item", row.transform);
        PlaceMid(item.gameObject, 2, withMode ? 106 : 160, 24);
        TMP_Dropdown mode = null;
        if (withMode)
        {
            mode = Dropdown("Mode", row.transform);
            PlaceMid(mode.gameObject, 110, 58, 24);
        }
        var amount = Input("Amount", row.transform, "0");
        PlaceMid(amount.gameObject, withMode ? 170 : 164, 44, 24);
        var x2 = MakeButton("Remove", row.transform, "X", CloseBg, 12);
        PlaceMid(x2.gameObject, RuleColW - 26, 22, 22);

        template = row.AddComponent<CaravanRuleRowUI>();
        var so = new SerializedObject(template);
        so.FindProperty("itemDropdown").objectReferenceValue = item;
        so.FindProperty("modeDropdown").objectReferenceValue = mode;
        so.FindProperty("amountInput").objectReferenceValue  = amount;
        so.FindProperty("removeButton").objectReferenceValue = x2;
        so.ApplyModifiedPropertiesWithoutUndo();

        add = MakeButton("AddButton", col.transform, "추가", BtnBg, 13);
        PlaceBottomLeft(add.gameObject, 6, 4, RuleColW - 12, 28);
        return content;
    }

    // ── 부품 ────────────────────────────────────────────────────────────────

    // 스크롤 — 세로. `grid`면 세 칸짜리 격자(일행).
    static Transform Scroll(string name, Transform parent, float x, float y, float w, float h, out ScrollRect sr, bool grid = false)
    {
        var sv = MakeRect(name, parent);
        Place(sv, x, y, w, h);
        sr = sv.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        var vp = MakeRect("Viewport", sv.transform);
        Stretch(vp);
        vp.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        vp.AddComponent<Mask>().showMaskGraphic = false;
        sr.viewport = vp.GetComponent<RectTransform>();
        var content = MakeRect("Content", vp.transform);
        var ct = content.GetComponent<RectTransform>();
        ct.anchorMin = new Vector2(0, 1); ct.anchorMax = new Vector2(1, 1); ct.pivot = new Vector2(0.5f, 1);
        ct.anchoredPosition = Vector2.zero; ct.sizeDelta = Vector2.zero;
        if (grid)
        {
            var g = content.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2((w - 24) / 3f, 26); g.spacing = new Vector2(4, 2);
            g.padding = new RectOffset(6, 6, 4, 4);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 3;
        }
        else
        {
            var vg = content.AddComponent<VerticalLayoutGroup>();
            vg.childControlWidth = vg.childControlHeight = true;
            vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
            vg.spacing = 2; vg.padding = new RectOffset(4, 4, 4, 4);
        }
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = ct;
        return content.transform;
    }

    static Button MakeButton(string name, Transform parent, string label, Color bg, float size)
    {
        var go = MakeRect(name, parent);
        var img = go.AddComponent<Image>(); img.color = bg;
        var b = go.AddComponent<Button>(); b.targetGraphic = img;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        go.AddComponent<LayoutElement>().preferredHeight = 30;
        var t = Text("Text", go.transform, label, size, TextAlignmentOptions.Center, Color.white);
        StretchRt(t.rectTransform);
        return b;
    }

    static TMP_Dropdown Dropdown(string name, Transform parent)
    {
        var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        foreach (var img in go.GetComponentsInChildren<Image>(true))
            if (img.gameObject == go || img.name == "Template" || img.name == "Item Background") img.color = FieldBg;
        foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true)) { ApplyFont(t); t.fontSize = 13; t.color = Color.white; }
        var dd = go.GetComponent<TMP_Dropdown>();
        dd.ClearOptions();
        // 물건 목록이 길다 — 펼친 목록을 넉넉히.
        var tpl = dd.template;
        if (tpl != null) tpl.sizeDelta = new Vector2(tpl.sizeDelta.x, 240);
        return dd;
    }

    static TMP_InputField Input(string name, Transform parent, string placeholder)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        go.GetComponent<Image>().color = FieldBg;
        foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true)) { ApplyFont(t); t.fontSize = 13; t.color = Color.white; }
        var f = go.GetComponent<TMP_InputField>();
        if (f.placeholder is TextMeshProUGUI ph) { ph.text = placeholder; ph.color = Muted; }
        return f;
    }

    // 체크 상자 + 글자. 토글 하나에 그림 둘(바탕 · 표시) — 글리프가 아니라 `Image`로(특수문자 금지).
    static Toggle MakeToggle(string name, Transform parent, string label)
    {
        var go = MakeRect(name, parent);
        var tg = go.AddComponent<Toggle>();
        var bg = MakeRect("Background", go.transform);
        var brt = bg.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0, 0.5f); brt.pivot = new Vector2(0, 0.5f);
        brt.anchoredPosition = new Vector2(4, 0); brt.sizeDelta = new Vector2(18, 18);
        var bimg = bg.AddComponent<Image>(); bimg.color = FieldBg;
        var ck = MakeRect("Checkmark", bg.transform);
        var ckrt = ck.GetComponent<RectTransform>();
        ckrt.anchorMin = Vector2.zero; ckrt.anchorMax = Vector2.one; ckrt.offsetMin = new Vector2(4, 4); ckrt.offsetMax = new Vector2(-4, -4);
        var cimg = ck.AddComponent<Image>(); cimg.color = new Color(0.55f, 0.85f, 0.45f);
        tg.targetGraphic = bimg; tg.graphic = cimg; tg.isOn = false;
        var t = Text("Label", go.transform, label, 13, TextAlignmentOptions.MidlineLeft, Color.white);
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(28, 0); trt.offsetMax = Vector2.zero;
        return tg;
    }

    static TextMeshProUGUI Label(string name, Transform parent, string text) =>
        Text(name, parent, text, 13, TextAlignmentOptions.MidlineLeft, Muted);

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

    // 부모의 좌상단에서 (x, y)만큼 들어간 자리 · 크기 (w, h). y는 아래로 잰다.
    static void Place(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void PlaceBottomLeft(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 줄 안 — 왼쪽에서 x, 세로 가운데.
    static void PlaceMid(GameObject go, float x, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0);
        rt.sizeDelta = new Vector2(w, h);
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
