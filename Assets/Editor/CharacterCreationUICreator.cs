using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// **캐릭터 생성 화면을 씬에 짓는다**(2026-09-20) — 규격은 `docs/editor_creators.md`.
//
//   Canvas
//    └ CharacterCreation_UI      ← `CharacterCreationUI`. 화면 전체 · 늘 켜져 있다
//       └ CharacterCreation_Panel ← 화면 전체를 덮는 어둠(뒤의 세상을 못 누르게) · 보였다 사라지는 것
//          └ Window               ← 가운데 창(자리는 여기가 든다 — 다시 돌려도 물려준다)
//             ├ TitleBar          ← #0D0D0D 풀폭 · 레이아웃 그룹 없음
//             ├ TabBar            ← 캐릭터 탭 — `TabTemplate`을 런타임이 인원 수만큼 복제한다(한 명이면 안 보인다)
//             ├ Preview           ← 폰 미리보기(RawImage) — 끌면 돈다
//             ├ Choices           ← 이름 칸 + 고르는 줄 여덟(`ChoiceRowUI`)
//             ├ Stats             ← 스탯 여섯 열(`StatColumnUI` — 스탯 창과 같은 표) · 열정 `+` `++`
//             ├ Story             ← 성장 배경 · 특성
//             └ BottomBar         ← [다시 굴리기] [시작]
//
// - **닫기 버튼(X)을 일부러 안 단다** — 이 화면은 `시작`으로만 나간다(닫으면 폰 없이 판이 흐른다).
// - **글자는 ASCII와 한글뿐**(`<` `>`) — 기호는 폰트에 없으면 조용히 네모가 된다.
// - 다 짓고 나면 모양은 씬이 정본이다 — 고칠 것은 인스펙터에서(생성기의 수명).
public static class CharacterCreationUICreator
{
    const string FontPath     = "Assets/NanumGothic SDF.asset";
    const string WandererPath = "Assets/3_Prefab/Pawn/Wanderer_Human.prefab";

    static readonly Color Dim      = new(0f, 0f, 0f, 0.86f);
    static readonly Color WindowBg = new(0.10f, 0.10f, 0.12f, 1f);
    static readonly Color Header   = new(0.05098f, 0.05098f, 0.05098f, 1f);   // #0D0D0D
    static readonly Color Btn      = new(0.18f, 0.18f, 0.21f, 1f);
    static readonly Color StartBtn = new(0.23f, 0.42f, 0.23f, 1f);
    static readonly Color Box      = new(0.07f, 0.07f, 0.08f, 1f);

    // **스탯은 읽혀야 한다**(2026-09-20, 사용자: *"지금 스탯들 너무 안보여 … 눈아프네"*) — 글자 12 · 줄 22였다.
    // 글자 14 · 줄 26 · 진행도(%) 없음 · 밝은 이름 · 진한 줄무늬로 올리고, 그만큼 창을 넓혔다.
    const float W = 1780f, H = 820f, HeadH = 48f, TabH = 40f, BarH = 60f;
    const float Top = HeadH + TabH + 8f;
    // 미리보기 칸은 넓게(사용자: *"화면을 좀더 늘려줘"*) — 300은 좁고 길쭉해 양손이 잘렸다.
    const float PrevW = 440f, ChoiceX = 472f, RightX = 808f, StatsH = 336f, RowH = 26f;

    // 줄 아홉 — 화면이 `Key`로 가른다(`CharacterCreationUI.Step`).
    static readonly (string key, string label)[] Rows =
    {
        ("race", "종족"), ("gender", "성별"), ("body", "체형"), ("head", "머리형"), ("hair", "머리카락"),
        ("eyes", "눈"), ("mouth", "입"), ("ear", "귀"), ("nose", "코"),
    };

    [MenuItem("DungeonColony/UI/Create Character Creation (캐릭터 생성 화면)")]
    static void Create()
    {
        var canvas = FindCanvas();
        if (canvas == null) { Debug.LogError("[캐릭터 생성] 씬에 캔버스가 없다."); return; }

        // 다시 돌리면 **창의 자리를 물려준다**(사람이 옮긴 것은 판이다 — 뿌리는 늘 같은 값이다).
        Vector2? keepPos = null;
        var old = Object.FindFirstObjectByType<CharacterCreationUI>(FindObjectsInactive.Include);
        if (old != null)
        {
            var win = old.transform.Find("CharacterCreation_Panel/Window") as RectTransform;
            if (win != null) keepPos = win.anchoredPosition;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        var root = MakeRect("CharacterCreation_UI", canvas.transform);
        Undo.RegisterCreatedObjectUndo(root, "캐릭터 생성 화면");
        Stretch(root);
        root.transform.SetAsLastSibling();

        var panel = MakeRect("CharacterCreation_Panel", root.transform);
        Stretch(panel);
        panel.AddComponent<Image>().color = Dim;   // 뒤의 세상을 못 누르게 — 레이캐스트를 먹는다

        var window = MakeRect("Window", panel.transform);
        var wrt = window.GetComponent<RectTransform>();
        wrt.anchorMin = wrt.anchorMax = wrt.pivot = new Vector2(0.5f, 0.5f);
        wrt.sizeDelta = new Vector2(W, H);
        wrt.anchoredPosition = keepPos ?? Vector2.zero;
        window.AddComponent<Image>().color = WindowBg;

        // 머리줄 — 풀폭 · 레이아웃 그룹 없음.
        var head = MakeRect("TitleBar", window.transform);
        var hrt = head.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
        hrt.anchoredPosition = Vector2.zero; hrt.sizeDelta = new Vector2(0, HeadH);
        head.AddComponent<Image>().color = Header;
        var title = Text("Title", head.transform, "새 방랑자", 20, TextAlignmentOptions.MidlineLeft);
        Place(title, new Vector2(16, 0), new Vector2(400, HeadH), fromTopLeft: false, stretchY: true);

        // 캐릭터 탭 — 머리줄 바로 아래 풀폭. **개수가 설정에서 오므로 템플릿 하나만 짓고** 런타임이 복제한다.
        var tabs = MakeRect("TabBar", window.transform);
        var trt = tabs.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = new Vector2(0, -HeadH); trt.sizeDelta = new Vector2(0, TabH);
        var hlg = tabs.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 6, 4); hlg.spacing = 6;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        var tabTemplate = MakeButton("TabTemplate", tabs.transform, "1.", Btn, 14);
        var tle = tabTemplate.gameObject.AddComponent<LayoutElement>();
        tle.preferredWidth = 170; tle.preferredHeight = 30;

        // 미리보기 — 왼쪽.
        var prev = MakeRect("Preview", window.transform);
        Place(prev, new Vector2(16, Top), new Vector2(PrevW, H - Top - BarH - 16));
        var raw = prev.AddComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;

        // 가운데 — 이름 + 고르는 줄.
        var choices = MakeRect("Choices", window.transform);
        Place(choices, new Vector2(ChoiceX, Top), new Vector2(320, H - Top - BarH - 16));

        var nameLabel = Text("NameLabel", choices.transform, "이름", 15, TextAlignmentOptions.MidlineLeft);
        Place(nameLabel, new Vector2(0, 0), new Vector2(72, 34));
        var input = MakeInput(choices.transform);
        Place(input.gameObject, new Vector2(76, 0), new Vector2(160, 34));
        var randomName = MakeButton("RandomName", choices.transform, "무작위", Btn, 13);
        Place(randomName.gameObject, new Vector2(242, 0), new Vector2(78, 34));

        var rowComps = new List<ChoiceRowUI>();
        for (int i = 0; i < Rows.Length; i++)
            rowComps.Add(MakeRow(choices.transform, Rows[i].key, Rows[i].label, 52 + i * 44));

        // 오른쪽 위 — 스탯 여섯 열(스탯 창과 **같은 표**와 **같은 줄 부품** — 따로 그리면 두 창이 다른 말을 한다).
        float rightW = W - RightX - 16;
        var stats = MakeRect("Stats", window.transform);
        Place(stats, new Vector2(RightX, Top), new Vector2(rightW, StatsH));
        stats.AddComponent<Image>().color = Box;
        var cols = new List<StatColumnUI>();
        float colW = (rightW - 16) / StatColumns.All.Length;
        for (int c = 0; c < StatColumns.All.Length; c++)
            cols.Add(MakeStatColumn(stats.transform, c, new Vector2(8 + c * colW, 8), colW - 6));

        // 오른쪽 아래 — 사람(성장 배경 · 특성).
        var story = MakeRect("Story", window.transform);
        Place(story, new Vector2(RightX, Top + StatsH + 8),
              new Vector2(rightW, H - Top - BarH - 16 - StatsH - 8));
        story.AddComponent<Image>().color = Box;
        var storyText = Text("Text", story.transform, "", 14, TextAlignmentOptions.TopLeft);
        var srt = storyText.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(12, 12); srt.offsetMax = new Vector2(-12, -12);
        storyText.GetComponent<TextMeshProUGUI>().textWrappingMode = TextWrappingModes.Normal;

        // 아래 줄 — [다시 굴리기] [시작].
        var bar = MakeRect("BottomBar", window.transform);
        var brt = bar.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0);
        brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(0, BarH);
        var reroll = MakeButton("Reroll", bar.transform, "다시 굴리기", Btn, 15);
        Anchor(reroll.gameObject, new Vector2(1, 0.5f), new Vector2(-172, 0), new Vector2(140, 40));
        var start = MakeButton("Start", bar.transform, "시작", StartBtn, 16);
        Anchor(start.gameObject, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(140, 40));

        // 배선.
        var comp = root.AddComponent<CharacterCreationUI>();
        var so = new SerializedObject(comp);
        so.FindProperty("panel").objectReferenceValue            = panel;
        so.FindProperty("preview").objectReferenceValue          = raw;
        so.FindProperty("nameInput").objectReferenceValue        = input;
        so.FindProperty("randomNameButton").objectReferenceValue = randomName;
        so.FindProperty("storyText").objectReferenceValue        = storyText.GetComponent<TextMeshProUGUI>();
        so.FindProperty("rerollButton").objectReferenceValue     = reroll;
        so.FindProperty("startButton").objectReferenceValue      = start;
        so.FindProperty("wandererPrefab").objectReferenceValue   = AssetDatabase.LoadAssetAtPath<GameObject>(WandererPath);
        so.FindProperty("tabTemplate").objectReferenceValue      = tabTemplate;
        var colsProp = so.FindProperty("statColumns");
        colsProp.arraySize = cols.Count;
        for (int i = 0; i < cols.Count; i++) colsProp.GetArrayElementAtIndex(i).objectReferenceValue = cols[i];
        var rowsProp = so.FindProperty("rows");
        rowsProp.arraySize = rowComps.Count;
        for (int i = 0; i < rowComps.Count; i++) rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rowComps[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[캐릭터 생성] 화면을 지었다 — `CharacterCreation_UI`. 판이 열릴 때 씬에 `StartSpot`이 있으면 뜬다.");
    }

    // ── 한 줄 ────────────────────────────────────────────────────────────────

    static ChoiceRowUI MakeRow(Transform parent, string key, string label, float y)
    {
        var row = MakeRect("Row_" + key, parent);
        Place(row, new Vector2(0, y), new Vector2(320, 36));

        var l = Text("Label", row.transform, label, 15, TextAlignmentOptions.MidlineLeft);
        Place(l, new Vector2(0, 0), new Vector2(72, 36));
        var p = MakeButton("Prev", row.transform, "<", Btn, 16);
        Place(p.gameObject, new Vector2(76, 2), new Vector2(32, 32));
        var v = Text("Value", row.transform, "", 15, TextAlignmentOptions.Center);
        Place(v, new Vector2(112, 0), new Vector2(172, 36));
        var n = MakeButton("Next", row.transform, ">", Btn, 16);
        Place(n.gameObject, new Vector2(288, 2), new Vector2(32, 32));

        var comp = row.AddComponent<ChoiceRowUI>();
        comp.Key = key;
        var so = new SerializedObject(comp);
        so.FindProperty("label").objectReferenceValue = l.GetComponent<TextMeshProUGUI>();
        so.FindProperty("value").objectReferenceValue = v.GetComponent<TextMeshProUGUI>();
        so.FindProperty("prev").objectReferenceValue  = p;
        so.FindProperty("next").objectReferenceValue  = n;
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    // ── 스탯 열 하나 — 머리글 + 줄(`StatRowUI`) ──────────────────────────────────

    static StatColumnUI MakeStatColumn(Transform parent, int index, Vector2 topLeft, float width)
    {
        var col = StatColumns.All[index];
        var go  = MakeRect("Col_" + col.Header, parent);
        Place(go, topLeft, new Vector2(width, 28 + col.Stats.Length * RowH));

        var head = Text("Header", go.transform, col.Header, 16, TextAlignmentOptions.MidlineLeft);
        Place(head, Vector2.zero, new Vector2(width, 26));
        head.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        head.GetComponent<TextMeshProUGUI>().color = new Color(0.91f, 0.77f, 0.40f);

        var rows = new StatRowUI[col.Stats.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = MakeRect("Row", go.transform);
            Place(row, new Vector2(0, 28 + i * RowH), new Vector2(width, RowH - 2));
            row.AddComponent<Image>().color = new Color(1, 1, 1, i % 2 == 0 ? 0.07f : 0.025f);   // 줄무늬 · 툴팁을 받는 면
            var n = Text("Name",  row.transform, "", 14, TextAlignmentOptions.MidlineLeft);
            var v = Text("Value", row.transform, "", 14, TextAlignmentOptions.MidlineRight);
            n.GetComponent<TextMeshProUGUI>().color = new Color(0.96f, 0.96f, 0.97f);
            var nrt = n.GetComponent<RectTransform>(); nrt.anchorMin = Vector2.zero; nrt.anchorMax = new Vector2(0.74f, 1); nrt.offsetMin = new Vector2(6, 0); nrt.offsetMax = Vector2.zero;
            var vrt = v.GetComponent<RectTransform>(); vrt.anchorMin = new Vector2(0.70f, 0); vrt.anchorMax = Vector2.one; vrt.offsetMin = Vector2.zero; vrt.offsetMax = new Vector2(-6, 0);
            n.GetComponent<TextMeshProUGUI>().raycastTarget = false;
            v.GetComponent<TextMeshProUGUI>().raycastTarget = false;

            rows[i] = row.AddComponent<StatRowUI>();
            var rso = new SerializedObject(rows[i]);
            rso.FindProperty("nameText").objectReferenceValue  = n.GetComponent<TextMeshProUGUI>();
            rso.FindProperty("valueText").objectReferenceValue = v.GetComponent<TextMeshProUGUI>();
            rso.FindProperty("showPercent").boolValue          = false;   // 전부 최하라 0%만 줄줄이 뜬다
            rso.ApplyModifiedPropertiesWithoutUndo();
        }

        var comp = go.AddComponent<StatColumnUI>();
        var so = new SerializedObject(comp);
        so.FindProperty("columnIndex").intValue = index;
        var rp = so.FindProperty("rows");
        rp.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++) rp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    // ── 부품 ────────────────────────────────────────────────────────────────

    static Canvas FindCanvas()
    {
        // 게임 UI가 사는 캔버스 — 살펴보기 창이 있는 곳(초상화 등 다른 캔버스를 안 짚게).
        var inspect = Object.FindFirstObjectByType<InspectPanelUI>(FindObjectsInactive.Include);
        if (inspect != null) return inspect.GetComponentInParent<Canvas>(true).rootCanvas;
        return Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
    }

    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
    }

    // 부모의 왼쪽 위에서 잰 자리(y는 아래로).
    static void Place(GameObject go, Vector2 topLeft, Vector2 size, bool fromTopLeft = true, bool stretchY = false)
    {
        var rt = go.GetComponent<RectTransform>();
        if (stretchY)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(topLeft.x, 0); rt.sizeDelta = new Vector2(size.x, 0);
            return;
        }
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
        rt.sizeDelta = size;
    }

    static void Anchor(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static GameObject Text(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = MakeRect(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.alignment = align; t.color = new Color(0.9f, 0.9f, 0.92f);
        t.richText = true;
        ApplyFont(t);
        return go;
    }

    static Button MakeButton(string name, Transform parent, string label, Color bg, float size)
    {
        var go = MakeRect(name, parent);
        go.AddComponent<Image>().color = bg;
        var b = go.AddComponent<Button>();
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        var t = Text("Text", go.transform, label, size, TextAlignmentOptions.Center);
        Stretch(t);
        t.GetComponent<TextMeshProUGUI>().raycastTarget = false;
        return b;
    }

    static TMP_InputField MakeInput(Transform parent)
    {
        var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = "NameInput";
        go.transform.SetParent(parent, false);
        var input = go.GetComponent<TMP_InputField>();
        go.GetComponent<Image>().color = Box;
        foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            ApplyFont(t);
            t.fontSize = 15;
            t.color = t.gameObject.name == "Placeholder" ? new Color(1, 1, 1, 0.35f) : new Color(0.95f, 0.95f, 0.95f);
            if (t.gameObject.name == "Placeholder") t.text = "이름";
        }
        input.characterLimit = 16;
        return input;
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }
}
