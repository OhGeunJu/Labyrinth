using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// **살펴보기 창 하나 — 폰이면 켄시 상태표, 아니면 글**(2026-09-19 — `PawnSheetUI` + `InspectPanelUI`).
//
// 사용자: *"원래 있던 창은 없애고 상태창에 … 광석 정보라던지 벽 정보라던지 원래 뜨던걸 옮겨야해 · X표는 없애"*.
// 그래서 이 도구는 판을 세우고 **살펴보기 창(`InspectPanelUI`)을 이 판에 다시 물린 뒤 옛 창을 지운다.**
// 살펴보기의 판단(무엇을 보여 주나 · 글 · 동작 버튼)은 한 줄도 안 바뀐다 — **그리는 자리만 옮긴다.**
//
//   PawnSheet_UI (전체 스트레치 · `PawnSheetUI` + `InspectPanelUI`)
//    └ PawnSheet_Panel (왼쪽 아래)
//       ├ PawnView  — 이름 · 상태·능력치 · 배경·장비(폰일 때만 · **아래까지 늘어난다**)
//       ├ Info      — 살펴보기 글(폰이면 두 칸 아래 · 아니면 판 전체)
//       └ ActionRow — 동작 버튼 줄(아래) + ActionTemplate
//
// 규격은 `docs/editor_creators.md` — NanumGothic · 특수문자 없음. **헤더와 닫기(X)는 없다**(사용자 결정 —
// 고른 것이 없으면 판이 저절로 꺼진다). **줄 수가 정해진 왼쪽은 다 세우고** 몸마다 개수가 다른 막대는 템플릿 한 줄.
//
// **다시 돌리면 판의 자리를 물려준다**(사람이 옮긴 것은 판이다 — 크기는 안 물려준다: 줄 수에서 나온다).
// 다 쓰면 지운다(생성기의 수명).
public static class PawnSheetCreator
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    // **두 창의 높이는 같다**(2026-09-19, 사용자: *"두 창의 높이가 동일해야해 그만큼 초록색 바를 키워야겠지 글씨"*).
    // 왼쪽 창이 이름 · 상태·능력치 · 배경·장비 줄을 다 담는 높이가 곧 오른쪽 창의 높이이고, 막대는 그 높이를 **나눠
    // 채운다**(레이아웃이 늘린다 — 막대 수가 몸마다 달라도 바닥까지 찬다).
    //
    // **낮고 넓게**(사용자: *"너무 길잖아 화면을 다 채우겠어 저기 가슴이라고 써져있는데가 마지노선"* — 화면 높이의
    // 3분의 1쯤). 줄을 빼지 않고 **상태·능력치 칸과 배경·장비 칸을 옆으로 나란히** 둔다(켄시도 그렇게 넓다).
    // 너비는 380 — 520으로 넓혔더니 *"능력치 있는 쪽이 가로로 너무 길어졌다"*(사용자). 칸 하나가 177쯤이면
    // 능력치 줄은 넉넉하고 긴 장비 이름은 말줄임된다.
    const float W = 380f, RowH = 20f, Pad = 10f;
    const float NameH = 40f, StatsH = 204f, GearH = 204f, Gap = 6f, NotesH = 30f, ActionH = 34f;
    const float ViewH = NameH + Gap + StatsH;                               // 폰일 때만 켜는 칸들(두 칸은 옆으로)
    const float H     = Pad + ViewH + 6f + NotesH + 6f + ActionH + Pad;     // 왼쪽 창 = 오른쪽 창
    const float HW = 300f, HH = H;

    static readonly Color Bg      = new(0.10f, 0.10f, 0.11f, 0.96f);
    static readonly Color Box     = new(0.14f, 0.14f, 0.15f, 1f);
    static readonly Color Line    = new(0.30f, 0.30f, 0.32f, 1f);
    static readonly Color BarBg   = new(0.09f, 0.16f, 0.07f, 1f);
    static readonly Color BarFill = new(0.20f, 0.45f, 0.13f, 1f);
    static readonly Color Label   = new(0.62f, 0.62f, 0.64f, 1f);

    [MenuItem("DungeonColony/UI/Create Inspect Sheet (살펴보기 + 켄시 상태표)")]
    static void Create()
    {
        // **살펴보기 창이 사는 캔버스**에 세운다 — 첫 캔버스를 잡으면 월드 공간 캔버스(머리 위 체력 막대 등)가 걸릴 수 있다.
        var insp   = Object.FindFirstObjectByType<InspectPanelUI>(FindObjectsInactive.Include);
        var canvas = insp != null ? insp.GetComponentInParent<Canvas>(true)?.rootCanvas : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[상태표] 캔버스가 없다."); return; }

        var selection = Object.FindFirstObjectByType<SelectionManager>(FindObjectsInactive.Include);
        if (selection == null) { Debug.LogError("[상태표] `SelectionManager`가 없다."); return; }

        // 옛 살펴보기 창 — 이 판이 아닌 곳에 있는 것. 판을 다시 세우기 전에 잡아 둔다.
        InspectPanelUI oldInsp = null; GameObject oldPanelGo = null;
        foreach (var c in Object.FindObjectsByType<InspectPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.gameObject.name != "PawnSheet_UI")
            {
                oldInsp = c;
                oldPanelGo = new SerializedObject(c).FindProperty("panel").objectReferenceValue as GameObject;
                break;
            }

        // 다시 돌릴 때 — 판의 자리를 물려준다.
        bool keep = false; Vector2 aMin = default, aMax = default, piv = default, pos = default;
        bool keepH = false; Vector2 hMin = default, hMax = default, hPiv = default, hPos = default;
        var old = canvas.transform.Find("PawnSheet_UI");
        if (old != null)
        {
            var oldPanel = old.Find("PawnSheet_Panel") as RectTransform;
            if (oldPanel != null) { keep = true; aMin = oldPanel.anchorMin; aMax = oldPanel.anchorMax; piv = oldPanel.pivot; pos = oldPanel.anchoredPosition; }
            var oldHealth = old.Find("PawnHealth_Panel") as RectTransform;
            if (oldHealth != null) { keepH = true; hMin = oldHealth.anchorMin; hMax = oldHealth.anchorMax; hPiv = oldHealth.pivot; hPos = oldHealth.anchoredPosition; }
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        var root = Rect("PawnSheet_UI", canvas.transform);
        Stretch(root);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "상태표");

        var panel = Rect("PawnSheet_Panel", root);
        panel.anchorMin = panel.anchorMax = keep ? aMin : Vector2.zero;
        if (keep) panel.anchorMax = aMax;
        panel.pivot = keep ? piv : Vector2.zero;
        panel.sizeDelta = new Vector2(W, H);
        panel.anchoredPosition = keep ? pos : new Vector2(16f, 16f);   // 왼쪽 아래 — 켄시의 자리
        panel.gameObject.AddComponent<Image>().color = Bg;

        // 폰일 때만 켜는 칸들 — 이름 · 상태·능력치 · 배경·장비(왼쪽 창 위쪽).
        var body = Rect("PawnView", panel);
        // **아래까지 늘린다**(사용자: *"밑에가 좀 비는데 채워줘"*) — 아랫변은 런타임이 정한다(글·동작이 있으면 그만큼만 비운다).
        body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one; body.pivot = new Vector2(0.5f, 1);
        body.offsetMin = new Vector2(Pad, Pad); body.offsetMax = new Vector2(-Pad, -Pad);

        // **이름이 먼저다**(사용자: *"이름이랑 순서가 먼저 보여야해"*) — 이름 한 줄 + 유형·세력 한 줄.
        var nameBox = Box_("NameBox", body, 0f, NameH);
        // 한 줄로 — 이름 왼쪽 · [유형] (세력) 오른쪽.
        var nameT = Text("Name", nameBox, "이름", 17, TextAlignmentOptions.MidlineLeft, new Color(0.91f, 0.77f, 0.40f));
        Stretch(nameT.rectTransform); nameT.rectTransform.offsetMin = new Vector2(8, 0); nameT.rectTransform.offsetMax = new Vector2(-8, 0);
        nameT.fontStyle = FontStyles.Bold;
        var subT = Text("Sub", nameBox, "[방랑자]", 13, TextAlignmentOptions.MidlineRight, Label);
        Stretch(subT.rectTransform); subT.rectTransform.offsetMin = new Vector2(8, 0); subT.rectTransform.offsetMax = new Vector2(-8, 0);

        // 상태·능력치 — 회색 이름 · 흰 값.
        var left = Fill_("Stats", body, NameH + Gap, 0f, 0.5f);
        float y = -6f;
        var state = Row(left, "상태", ref y);
        var goal  = Row(left, "목표", ref y);
        var load  = Row(left, "짐",   ref y);
        Divider(left, ref y);
        var atk = Row(left, StatName.MeleeAttack.ToDisplay(), ref y, 14);
        var def = Row(left, StatName.Defense.ToDisplay(),     ref y, 15);
        var str = Row(left, StatName.Strength.ToDisplay(),    ref y, 15);
        var dex = Row(left, StatName.Dexterity.ToDisplay(),   ref y, 15);
        var tou = Row(left, StatName.Toughness.ToDisplay(),   ref y, 15);
        var eva = Row(left, StatName.Evasion.ToDisplay(),     ref y, 15);

        // 배경·특성·장비 — **같은 색 규칙**(사용자: *"색 구분이 있을 거면 이름 장비들도 동일하게"*).
        var gear = Fill_("Gear", body, NameH + Gap, 0.5f, 1f);
        y = -6f;
        var story  = Row(gear, "배경", ref y);
        var traits = Row(gear, "특성", ref y);
        Divider(gear, ref y);
        var gMain  = Row(gear, "주손",   ref y);
        var gOff   = Row(gear, "보조손", ref y);
        var gHead  = Row(gear, "머리",   ref y);
        var gChest = Row(gear, "몸통",   ref y);
        var gHands = Row(gear, "손",     ref y);
        var gLegs  = Row(gear, "다리",   ref y);

        // ── 오른쪽 ──
        // **오른쪽은 제 창이다**(2026-09-19, 사용자: *"두 창을 나눠 Left Right를"*) — 왼쪽 창 바로 오른쪽에 붙여
        // 세우되 자리는 따로 옮길 수 있다. 폰일 때만 뜬다(`PawnSheetUI.healthPanel`).
        // 물려받은 오른쪽 창이 **왼쪽 창 곁(600px 안)**이면 왼쪽 창 바로 오른쪽에 다시 붙인다 — 왼쪽 창 너비가 바뀌면
        // 겹치거나 틈이 벌어진다. 멀리 옮겨 둔 것은 사람이 정한 자리라 그대로 둔다(같은 앵커일 때만 잰다).
        var leftPos = keep ? pos : new Vector2(16f, 16f);
        if (keepH && (!keep || (hMin == aMin && hPiv == piv)) && hPos.x >= leftPos.x && hPos.x < leftPos.x + 600f)
            hPos.x = leftPos.x + W + 8f;

        var health = Rect("PawnHealth_Panel", root);
        health.anchorMin = keepH ? hMin : Vector2.zero; health.anchorMax = keepH ? hMax : Vector2.zero;
        health.pivot = keepH ? hPiv : Vector2.zero;
        health.sizeDelta = new Vector2(HW, HH);
        health.anchoredPosition = keepH ? hPos : new Vector2(16f + W + 8f, 16f);
        health.gameObject.AddComponent<Image>().color = Bg;

        var right = Rect("Right", health);
        right.anchorMin = Vector2.zero; right.anchorMax = Vector2.one;
        right.offsetMin = new Vector2(Pad, Pad); right.offsetMax = new Vector2(-Pad, -Pad);

        var condBox = Rect("ConditionBox", right);
        condBox.anchorMin = new Vector2(0, 1); condBox.anchorMax = new Vector2(1, 1); condBox.pivot = new Vector2(0.5f, 1);
        condBox.sizeDelta = new Vector2(0, 30f); condBox.anchoredPosition = Vector2.zero;
        condBox.gameObject.AddComponent<Image>().color = Box;
        var cond = Text("Text", condBox, "이상 없음", 14, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(cond.rectTransform); cond.rectTransform.offsetMin = new Vector2(8, 0); cond.rectTransform.offsetMax = new Vector2(-8, 0);

        var bars = Rect("Bars", right);
        bars.anchorMin = Vector2.zero; bars.anchorMax = Vector2.one;
        bars.offsetMin = Vector2.zero; bars.offsetMax = new Vector2(0, -36f);
        bars.gameObject.AddComponent<Image>().color = Box;
        var vg = bars.gameObject.AddComponent<VerticalLayoutGroup>();
        vg.padding = new RectOffset(6, 6, 6, 6); vg.spacing = 2f;
        vg.childControlWidth = true; vg.childForceExpandWidth = true;
        vg.childControlHeight = true; vg.childForceExpandHeight = true;   // 막대가 창 높이를 나눠 채운다

        // 막대 템플릿 — Fill(자식, 앵커로 길이) · Label(가운데) · Value(오른쪽).
        var bar = Rect("BarTemplate", bars);
        bar.gameObject.AddComponent<LayoutElement>().minHeight = 18f;
        bar.gameObject.AddComponent<Image>().color = BarBg;
        var fill = Rect("Fill", bar);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0.8f, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = BarFill;
        var bl = Text("Label", bar, "머리", 14, TextAlignmentOptions.Center, Color.white);
        Stretch(bl.rectTransform); bl.rectTransform.offsetMax = new Vector2(-44f, 0);
        var bv = Text("Value", bar, "100", 14, TextAlignmentOptions.MidlineRight, Color.white);
        Stretch(bv.rectTransform); bv.rectTransform.offsetMax = new Vector2(-6f, 0);

        // 살펴보기 글 — 폰이면 두 칸 아래로 내려간다(`PawnSheetUI.infoTopPawn`).
        var info = Text("Info", panel, "", 13, TextAlignmentOptions.TopLeft, Color.white);
        info.textWrappingMode = TextWrappingModes.Normal;
        info.richText = true;
        var irt = info.rectTransform;
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(Pad + 4f, Pad + ActionH + 6f);
        irt.offsetMax = new Vector2(-Pad - 4f, -Pad);

        // 동작 버튼 줄 — 판 아래. 템플릿 하나를 살펴보기 창이 복제한다.
        var row = Rect("ActionRow", panel);
        row.anchorMin = new Vector2(0, 0); row.anchorMax = new Vector2(1, 0); row.pivot = new Vector2(0.5f, 0);
        row.offsetMin = new Vector2(Pad, Pad); row.offsetMax = new Vector2(-Pad, Pad + ActionH);
        var hg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hg.spacing = 6f; hg.childAlignment = TextAnchor.MiddleLeft;
        hg.childControlWidth = false; hg.childControlHeight = false;
        hg.childForceExpandWidth = false; hg.childForceExpandHeight = false;

        var tpl = Rect("ActionTemplate", row);
        tpl.sizeDelta = new Vector2(96f, 28f);
        tpl.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.24f, 1f);
        var tplBtn = tpl.gameObject.AddComponent<Button>();
        var tl = Text("Text", tpl, "동작", 12, TextAlignmentOptions.Center, Color.white);
        Stretch(tl.rectTransform);

        // 컴포넌트 배선.
        var ui = root.gameObject.AddComponent<PawnSheetUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue       = panel.gameObject;
        so.FindProperty("pawnView").objectReferenceValue    = body.gameObject;
        so.FindProperty("healthPanel").objectReferenceValue = health.gameObject;
        so.FindProperty("info").objectReferenceValue        = irt;
        so.FindProperty("actionRow").objectReferenceValue   = row;
        so.FindProperty("pad").floatValue                   = Pad;
        so.FindProperty("notesH").floatValue                = NotesH;
        so.FindProperty("actionH").floatValue               = ActionH;
        so.FindProperty("nameText").objectReferenceValue    = nameT;
        so.FindProperty("subText").objectReferenceValue     = subT;
        so.FindProperty("story").objectReferenceValue       = story;
        so.FindProperty("traits").objectReferenceValue      = traits;
        so.FindProperty("mainHand").objectReferenceValue    = gMain;
        so.FindProperty("offHand").objectReferenceValue     = gOff;
        so.FindProperty("head").objectReferenceValue        = gHead;
        so.FindProperty("chest").objectReferenceValue       = gChest;
        so.FindProperty("hands").objectReferenceValue       = gHands;
        so.FindProperty("legs").objectReferenceValue        = gLegs;
        so.FindProperty("state").objectReferenceValue       = state;
        so.FindProperty("goal").objectReferenceValue        = goal;
        so.FindProperty("load").objectReferenceValue        = load;
        so.FindProperty("attack").objectReferenceValue      = atk;
        so.FindProperty("defense").objectReferenceValue     = def;
        so.FindProperty("strength").objectReferenceValue    = str;
        so.FindProperty("dexterity").objectReferenceValue   = dex;
        so.FindProperty("toughness").objectReferenceValue   = tou;
        so.FindProperty("evasion").objectReferenceValue     = eva;
        so.FindProperty("condition").objectReferenceValue   = cond;
        so.FindProperty("barRoot").objectReferenceValue     = bars;
        so.FindProperty("barTemplate").objectReferenceValue = bar.gameObject;
        so.ApplyModifiedProperties();

        // 살펴보기 창을 이 판에 물린다 — **옛 창은 지운다**(같은 것을 그리는 창이 둘이면 안 된다).
        var inspUi = root.gameObject.AddComponent<InspectPanelUI>();
        var iso  = new SerializedObject(inspUi);
        iso.FindProperty("panel").objectReferenceValue            = panel.gameObject;
        iso.FindProperty("infoText").objectReferenceValue         = info;
        iso.FindProperty("selectionManager").objectReferenceValue = selection;
        iso.FindProperty("actionRow").objectReferenceValue        = row;
        iso.FindProperty("actionTemplate").objectReferenceValue   = tplBtn;
        iso.ApplyModifiedProperties();

        if (oldInsp != null)
        {
            // 옛 창의 뿌리가 캔버스 바로 아래의 제 것이면 통째로, 아니면 그 판과 컴포넌트만.
            var oldRoot = oldInsp.transform;
            bool ownRoot = oldRoot.parent == canvas.transform && oldRoot.childCount <= 1;
            if (ownRoot) Undo.DestroyObjectImmediate(oldRoot.gameObject);
            else
            {
                if (oldPanelGo != null && oldPanelGo != panel.gameObject) Undo.DestroyObjectImmediate(oldPanelGo);
                Undo.DestroyObjectImmediate(oldInsp);
            }
        }

        panel.gameObject.SetActive(false);   // 고른 것이 생기면 살펴보기 창이 켠다
        health.gameObject.SetActive(false);  // 폰일 때 `PawnSheetUI`가 켠다

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log("[상태표] `PawnSheet_UI`를 세웠다(왼쪽 아래) — 살펴보기 창이 여기에 그리고 " +
                  (oldInsp != null ? "옛 창은 지웠다. " : "") + "자리는 판(`PawnSheet_Panel`)을 옮겨 맞출 것. **씬을 저장할 것.**");
    }

    // 칸 하나 — 위에서 `top`만큼 내려가 `h` 높이. 바탕은 모두 같은 색(칸끼리 같은 규칙).
    // `x0`~`x1` — 가로로 차지하는 몫(나란히 둘 때 가운데 틈 `Gap`을 반씩 나눠 뺀다).
    static RectTransform Box_(string name, RectTransform parent, float top, float h, float x0 = 0f, float x1 = 1f)
    {
        var r = Rect(name, parent);
        r.anchorMin = new Vector2(x0, 1); r.anchorMax = new Vector2(x1, 1); r.pivot = new Vector2(0.5f, 1);
        float l = x0 > 0f ? Gap * 0.5f : 0f, rr = x1 < 1f ? Gap * 0.5f : 0f;
        r.offsetMin = new Vector2(l, -top - h); r.offsetMax = new Vector2(-rr, -top);
        r.gameObject.AddComponent<Image>().color = Box;
        return r;
    }

    // 칸 하나 — 위에서 `top`만큼 내려가 **아래 끝까지.** 줄은 세로 묶음이 **칸 높이를 나눠** 늘어놓는다 —
    // 칸이 커져도 아래가 비지 않는다(`Row`가 늘어나는 몫을 갖는다).
    static RectTransform Fill_(string name, RectTransform parent, float top, float x0, float x1)
    {
        var r = Rect(name, parent);
        r.anchorMin = new Vector2(x0, 0); r.anchorMax = new Vector2(x1, 1); r.pivot = new Vector2(0.5f, 1);
        float l = x0 > 0f ? Gap * 0.5f : 0f, rr = x1 < 1f ? Gap * 0.5f : 0f;
        r.offsetMin = new Vector2(l, 0f); r.offsetMax = new Vector2(-rr, -top);
        r.gameObject.AddComponent<Image>().color = Box;

        var vg = r.gameObject.AddComponent<VerticalLayoutGroup>();
        vg.padding = new RectOffset(8, 8, 6, 6); vg.spacing = 0f;
        vg.childControlWidth = true;  vg.childForceExpandWidth = true;
        vg.childControlHeight = true; vg.childForceExpandHeight = false;
        return r;
    }

    // 구분선 — 늘어나지 않는 틈(10) 한가운데에 2px 선.
    static void Divider(RectTransform parent, ref float y)
    {
        var div = Rect("Divider", parent);
        var le = div.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 10f; le.flexibleHeight = 0f;
        var line = Rect("Line", div);
        line.anchorMin = new Vector2(0, 0.5f); line.anchorMax = new Vector2(1, 0.5f);
        line.sizeDelta = new Vector2(0f, 2f); line.anchoredPosition = Vector2.zero;
        line.gameObject.AddComponent<Image>().color = Line;
        y -= 12f;
    }

    // 한 줄 — 이름(왼쪽 · 흐린 색) + 값(오른쪽 · 흰색). 돌려주는 것은 값 칸. 값은 이름 칸을 비켜 선다(길면 말줄임).
    static TextMeshProUGUI Row(RectTransform parent, string label, ref float y, float size = 13)
    {
        var row = Rect("Row_" + label, parent);
        // 세로 묶음 안에서 **남는 높이를 나눠 갖는다** — 칸이 커지면 줄 사이가 벌어져 아래가 안 빈다.
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 16f; le.preferredHeight = RowH; le.flexibleHeight = 1f;
        row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1); row.pivot = new Vector2(0.5f, 1);
        row.sizeDelta = new Vector2(-16f, RowH); row.anchoredPosition = new Vector2(0, y);
        y -= RowH;

        var l = Text("Label", row, label, size, TextAlignmentOptions.MidlineLeft, Label);
        Stretch(l.rectTransform);
        var v = Text("Value", row, "-", size, TextAlignmentOptions.MidlineRight, Color.white);
        Stretch(v.rectTransform);
        v.rectTransform.offsetMin = new Vector2(64f, 0);
        v.overflowMode = TextOverflowModes.Ellipsis;
        return v;
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string text, float size,
                                TextAlignmentOptions align, Color color)
    {
        var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.alignment = align; t.color = color;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }
}
