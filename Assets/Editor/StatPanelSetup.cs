using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 스탯 창의 **규격**을 씬에 세운다 — 켄시식 가로 배치 + **줄마다 오브젝트**(2026-08-23).
//
// **한 덩어리 글자에 자리를 흉내 내던 것을 그만뒀다.** 공백을 채워 열을 맞추던 방식은 글꼴이
// 비례라 늘 어긋났고, `<pos=64%>`로 바꿔도 **값이 오른쪽 정렬이 안 됐다**(비율 위치라 자릿수가
// 다르면 끝이 들쭉날쭉). 줄이 오브젝트면 이름은 왼쪽에 붙고 값은 오른쪽에 붙어 **정렬이 그냥
// 된다** — 간격도 `lineSpacing` 같은 간접적인 손잡이가 아니라 **줄 높이와 사이 간격**이다.
//
// **줄 구성은 `StatColumns`가 든다** — 씬을 짓는 여기와 값을 채우는 `UnitStatsUI`가 같은 표를
// 읽는다. 각자 목록을 들면 스탯을 더할 때 한쪽만 늘어 빈 줄이 남는다.
//
// **몇 번 돌려도 같다** — 열과 줄은 있으면 다시 쓰고 남으면 지운다.
public static class StatPanelSetup
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    // ── 규격 ─────────────────────────────────────────────────────────────
    const float FontSize = 14f;
    const float RowH     = 26f;   // 줄 높이
    const float RowGap   = 4f;    // 줄 사이
    // 등급이 앉는 오른쪽 칸. **넉넉하면 이름 자리를 먹는다** — `D+ 60%`가 실제로 45px쯤이라
    // 74는 과했고, 그만큼 이름 칸이 좁아 `원거리방어`가 등급을 침범했다.
    const float ValueW   = 60f;
    const float HeaderH  = 26f;

    // 속성 열의 오른쪽 여백 — 줄을 300에서 이만큼 줄여 이름과 등급 사이를 좁힌다.
    const int AttrPadRight = 120;

    const float TitleH = 36f, Pad = 10f, Gap = 8f;
    const float LeftW  = 300f, PanelW = 1160f;

    // 이름표 — 초상화 + 이름 한 줄.
    const float PortraitSize = 64f, IdentityH = 68f;

    // 줄로 못 나누는 글(든 것·성장 배경·특성)에 줄 자리를 이만큼 준다.
    // **넉넉히 잡지 않는다** — 남는 자리는 그냥 빈 화면이고, 창이 클수록 뒤가 더 많이 가려진다.
    // 줄로 못 나누는 글은 **줄 간격을 따로 잡는다**(줄 오브젝트는 높이로 정하지만 이건 한
    // 덩어리다). 안 잡으면 **씬에 남아 있던 옛 값을 그대로 쓴다** — 실제로 줄 오브젝트로
    // 갈아엎기 전의 `60`이 남아 글이 창 밖으로 밀려났다.
    const float ProseLineSpacing = 20f;

    // 최대 열세 줄(든 것 3 · 성장 배경 3 · 특성 4 · 사이 3)에 줄당 22px.
    const float ProseH = 300f;

    // 세로는 **왼쪽과 오른쪽 중 큰 쪽**이 정한다. 한쪽 높이에 다른 쪽 여유를 **더하면**
    // 둘을 합친 만큼 길어져 창이 화면을 넘는다(실제로 822가 나왔다 — 필요한 것은 562였다).
    static float PanelH
    {
        get
        {
            float skills = HeaderH + StatColumns.LongestColumn * (RowH + RowGap);
            float left   = IdentityH + HeaderH
                         + StatColumns.All[0].Stats.Length * (RowH + RowGap) + ProseH;
            // **딱 맞게 잰다.** 한때 740을 바닥으로 뒀는데 아래가 통째로 비었다 —
            // 남는 자리는 여백이 아니라 **뒤를 가리는 검은 판**이다.
            return TitleH + Pad * 2f + Mathf.Max(skills, left);
        }
    }

    static readonly Color HeaderColor = new(0.91f, 0.77f, 0.40f);
    static readonly Color NameColor   = new(0.85f, 0.85f, 0.88f);

    [MenuItem("DungeonColony/Setup Stat Panel")]
    static void Setup()
    {
        var ui = Object.FindFirstObjectByType<UnitStatsUI>(FindObjectsInactive.Include);
        if (ui == null) { Debug.LogError("[스탯 창] 씬에 UnitStatsUI가 없습니다."); return; }

        var so       = new SerializedObject(ui);
        var panel    = so.FindProperty("panel").objectReferenceValue as GameObject;
        var infoText = so.FindProperty("infoText").objectReferenceValue as TextMeshProUGUI;
        if (panel == null || infoText == null)
        {
            Debug.LogError("[스탯 창] panel 또는 infoText가 배선되지 않았습니다."); return;
        }

        float panelH = PanelH;
        float bodyH  = panelH - TitleH - Pad * 2f;
        float midY   = -(TitleH + Pad + bodyH * 0.5f);

        // **가운데로.** 가로로 길어져 오른쪽 위에 붙여 두면 화면을 가로지른다.
        if (panel.transform is RectTransform panelRt)
        {
            panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(PanelW, panelH);
        }

        // 제목 줄은 창 폭을 따라간다. **위에 매달린 것만** — 아래에 매달린 것까지 늘리면
        // 창 바닥을 가로지르는 띠가 된다.
        foreach (RectTransform child in panel.transform)
        {
            if (child.anchorMin.y < 1f || child.anchorMax.y < 1f) continue;
            child.anchorMin = new Vector2(0f, child.anchorMin.y);
            child.anchorMax = new Vector2(1f, child.anchorMax.y);
            child.sizeDelta = new Vector2(0f, child.sizeDelta.y);
        }

        // **스크롤은 걷어낸다** — 열로 벌리면 다 들어간다.
        var scroll = panel.GetComponentInChildren<ScrollRect>(true);
        if (scroll != null) Object.DestroyImmediate(scroll.gameObject);

        var built = new StatColumnUI[StatColumns.All.Length];

        // ── 왼쪽: 속성 열 + 줄로 못 나누는 글 ────────────────────────────
        var left = Ensure(panel.transform, "Left");
        Place(left, LeftW, bodyH, Pad + LeftW * 0.5f, midY);
        Stack(left, RowGap);

        // **이름표가 맨 위다.** 전에는 이름 줄이 `성장 배경` 글 안에 있어 **속성 아래**로
        // 밀렸다 — 속성 열이 왼쪽 상자의 첫 자식이었기 때문이다.
        BuildIdentity(left, infoText, so);

        built[0] = BuildColumn(left, 0, infoText);

        // **있던 글자를 그대로 옮긴다** — 새로 만들면 씬에서 손본 것이 사라진다.
        var infoRt = (RectTransform)infoText.transform;
        infoRt.SetParent(left, false);
        infoRt.SetAsLastSibling();
        infoText.alignment        = TextAlignmentOptions.TopLeft;
        infoText.fontSize         = FontSize;
        infoText.textWrappingMode = TextWrappingModes.Normal;
        infoText.lineSpacing      = ProseLineSpacing;

        // **`ContentSizeFitter`를 걷어낸다.** 옛 스크롤 시절에 붙어 있던 것인데, 이제 이 글자는
        // `VerticalLayoutGroup` 아래 산다 — **둘이 같은 높이를 서로 다르게 정해** 글이 속성 줄
        // 위로 올라와 겹친다(장비를 차서 `든 것` 절이 생기는 순간 드러났다). 크기를 정하는
        // 사람은 하나여야 한다.
        var fitter = infoText.GetComponent<ContentSizeFitter>();
        if (fitter != null) Object.DestroyImmediate(fitter);

        FlexibleHeight(infoRt);

        // ── 오른쪽: 기술 열들 ────────────────────────────────────────────
        float skillsW = PanelW - Pad * 2f - LeftW - Gap;

        var skills = Ensure(panel.transform, "Skills");
        Place(skills, skillsW, bodyH, Pad + LeftW + Gap + skillsW * 0.5f, midY);

        var row = skills.GetComponent<HorizontalLayoutGroup>();
        if (row == null) row = skills.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing                = Gap;
        row.padding                = new RectOffset(6, 6, 0, 0);
        row.childAlignment         = TextAnchor.UpperLeft;
        row.childControlWidth      = row.childControlHeight = true;
        row.childForceExpandWidth  = true;
        row.childForceExpandHeight = false;

        for (int i = 1; i < StatColumns.All.Length; i++)
            built[i] = BuildColumn(skills, i, infoText);

        // **표에서 빠진 열은 지운다.** 열은 이름으로 찾아 다시 쓰는데, 표에서 없어진 열은
        // 아무도 안 건드려 **머리글만 남은 빈 열**로 그대로 서 있는다(`노동`을 작업·제작으로
        // 가르고 나서 실제로 그랬다). 게다가 그 빈 열이 가로 자리를 나눠 가져 **남은 열이
        // 좁아지고, 좁아진 만큼 이름이 등급을 침범한다** — 한 번 안 지운 것이 두 증상으로 나왔다.
        Prune(skills, SkillNames());
        Prune(left, new[] { "Identity", StatColumns.All[0].Header, infoText.name });

        var arr = so.FindProperty("columns");
        arr.arraySize = built.Length;
        for (int i = 0; i < built.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = built[i];

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);

        // **씬에 사는 것은 씬을 직접 저장해야 남는다** — `SaveAssets`는 에셋만 저장한다.
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        int rows = 0;
        foreach (var c in StatColumns.All) rows += c.Stats.Length;
        Debug.Log($"[스탯 창] 규격 완료 — {PanelW}x{panelH:0}, 열 {StatColumns.All.Length}개 · 줄 {rows}개 " +
                  $"(줄 높이 {RowH}, 사이 {RowGap}). 값은 런타임이 채웁니다.");
    }

    // 지금 표가 쓰는 기술 열 이름들.
    static string[] SkillNames()
    {
        var names = new string[StatColumns.All.Length - 1];
        for (int i = 1; i < StatColumns.All.Length; i++) names[i - 1] = StatColumns.All[i].Header;
        return names;
    }

    // 알려진 이름이 아닌 자식을 지운다. **뒤에서 앞으로 돈다** — 앞에서 지우면 자식 목록이
    // 밀려 하나씩 건너뛴다.
    static void Prune(Transform parent, string[] keep)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            bool known = false;
            foreach (var k in keep) if (child.name == k) { known = true; break; }
            if (known) continue;

            Debug.Log($"[스탯 창] 표에 없는 `{child.name}`을(를) 지웠습니다.");
            Object.DestroyImmediate(child.gameObject);
        }
    }

    // 이름표 = 초상화(왼쪽 고정) + 이름(남는 자리).
    static void BuildIdentity(RectTransform parent, TextMeshProUGUI model, SerializedObject uiSo)
    {
        var rt = Ensure(parent, "Identity");
        rt.SetSiblingIndex(0);
        FixedHeight(rt, IdentityH);

        var portrait = Ensure(rt, "Portrait");
        portrait.anchorMin = new Vector2(0f, 0.5f);
        portrait.anchorMax = new Vector2(0f, 0.5f);
        portrait.pivot     = new Vector2(0f, 0.5f);
        portrait.sizeDelta = new Vector2(PortraitSize, PortraitSize);
        portrait.anchoredPosition = new Vector2(2f, 0f);

        var img = portrait.GetComponent<Image>();
        if (img == null) img = portrait.gameObject.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget  = false;

        var name = EnsureText(rt, "Name", model);
        var nrt  = (RectTransform)name.transform;
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot     = new Vector2(0.5f, 0.5f);
        nrt.offsetMin = new Vector2(PortraitSize + 10f, 0f);
        nrt.offsetMax = new Vector2(-2f, 0f);
        name.alignment        = TextAlignmentOptions.Left;
        name.fontSize         = FontSize + 3f;   // 이름은 조금 크다 — 창에서 처음 읽히는 글자다
        name.color            = new Color(0.92f, 0.92f, 0.95f);
        name.textWrappingMode = TextWrappingModes.Normal;

        uiSo.FindProperty("nameText").objectReferenceValue = name;
        uiSo.FindProperty("portrait").objectReferenceValue = img;
    }

    // 열 하나 = 머리글 + 스탯 수만큼의 줄.
    static StatColumnUI BuildColumn(Transform parent, int index, TextMeshProUGUI model)
    {
        var col = StatColumns.All[index];
        var rt  = Ensure(parent, col.Header);

        // 0번(속성)은 왼쪽 상자에 살아 폭이 300이다 — 기술 열(158)만큼으로 좁힌다.
        Stack(rt, RowGap, index == 0 ? AttrPadRight : 0);

        var le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;

        // 머리글 — 켄시처럼 `-전투-`. **특수문자를 안 쓴다**(글꼴에 없으면 조용히 네모가 된다).
        var header = EnsureText(rt, "Header", model);
        header.text      = "-" + col.Header + "-";
        header.color     = HeaderColor;
        header.alignment = TextAlignmentOptions.Left;
        FixedHeight((RectTransform)header.transform, HeaderH);
        header.transform.SetSiblingIndex(0);

        var rows = new StatRowUI[col.Stats.Length];
        for (int i = 0; i < col.Stats.Length; i++)
            rows[i] = BuildRow(rt, i, model);

        // **남는 줄은 지운다** — 스탯을 줄이고 다시 돌렸을 때 옛 줄이 남으면 값이 안 들어와
        // 빈 줄로 남는다.
        for (int i = col.Stats.Length; ; i++)
        {
            var extra = rt.Find("Row" + i);
            if (extra == null) break;
            Object.DestroyImmediate(extra.gameObject);
        }

        var comp = rt.GetComponent<StatColumnUI>();
        if (comp == null) comp = rt.gameObject.AddComponent<StatColumnUI>();

        var so = new SerializedObject(comp);
        so.FindProperty("columnIndex").intValue = index;
        var arr = so.FindProperty("rows");
        arr.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
        so.ApplyModifiedProperties();
        return comp;
    }

    // 줄 하나 = 이름(왼쪽 늘어남) + 등급(오른쪽 고정). **여기가 정렬의 전부다.**
    static StatRowUI BuildRow(Transform parent, int i, TextMeshProUGUI model)
    {
        var rt = Ensure(parent, "Row" + i);
        FixedHeight(rt, RowH);

        // **줄이 통째로 커서를 받는다** — 이름 글자에만 걸면 짧은 이름 옆 빈자리에서 설명이 끊긴다.
        //
        // **색은 없다(알파 0).** uGUI는 레이캐스트에 알파를 안 보므로 **완전히 투명해도 커서는
        // 그대로 받는다** — 줄마다 옅은 상자가 비쳐 목록이 표처럼 보이던 것만 사라진다.
        var hit = rt.GetComponent<Image>();
        if (hit == null) hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);

        var name = EnsureText(rt, "Name", model);
        var nrt  = (RectTransform)name.transform;
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot     = new Vector2(0.5f, 0.5f);
        nrt.offsetMin = new Vector2(2f, 0f);
        nrt.offsetMax = new Vector2(-ValueW, 0f);
        name.alignment        = TextAlignmentOptions.Left;
        name.color            = NameColor;
        name.textWrappingMode = TextWrappingModes.NoWrap;

        var value = EnsureText(rt, "Value", model);
        var vrt   = (RectTransform)value.transform;
        vrt.anchorMin = new Vector2(1f, 0f);
        vrt.anchorMax = new Vector2(1f, 1f);
        vrt.pivot     = new Vector2(1f, 0.5f);
        vrt.sizeDelta = new Vector2(ValueW, 0f);
        vrt.anchoredPosition = new Vector2(-2f, 0f);
        value.alignment        = TextAlignmentOptions.Right;
        value.textWrappingMode = TextWrappingModes.NoWrap;

        var comp = rt.GetComponent<StatRowUI>();
        if (comp == null) comp = rt.gameObject.AddComponent<StatRowUI>();

        var so = new SerializedObject(comp);
        so.FindProperty("nameText").objectReferenceValue  = name;
        so.FindProperty("valueText").objectReferenceValue = value;
        so.ApplyModifiedProperties();
        return comp;
    }

    // ── 손 ───────────────────────────────────────────────────────────────
    static RectTransform Ensure(Transform parent, string name)
    {
        if (parent.Find(name) is RectTransform found) return found;

        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Setup Stat Panel");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static TextMeshProUGUI EnsureText(Transform parent, string name, TextMeshProUGUI model)
    {
        var rt  = Ensure(parent, name);
        var tmp = rt.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();

        tmp.font          = model != null ? model.font : null;   // 한 창에 글자가 둘로 갈리면 안 된다
        tmp.fontSize      = FontSize;
        tmp.richText      = true;
        tmp.raycastTarget = false;                               // 커서는 줄이 받는다
        if (tmp.font == null) ApplyFont(tmp);
        return tmp;
    }

    // `padRight` — 줄을 그만큼 좁힌다. **왼쪽 상자의 속성 열에 쓴다**: 등급은 줄 오른쪽 끝에
    // 붙는데 그 상자가 300이나 되어 `근력`과 `S+` 사이가 텅 비었다. 기술 열은 폭이 158이라
    // 그 문제가 없다 — **같은 규칙인데 폭이 달라서 생긴 차이**라, 좁히는 것으로 맞춘다.
    static void Stack(RectTransform rt, float spacing, int padRight = 0)
    {
        var v = rt.GetComponent<VerticalLayoutGroup>();
        if (v == null) v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing                = spacing;
        v.padding                = new RectOffset(0, padRight, 0, 0);
        v.childAlignment         = TextAnchor.UpperLeft;
        v.childControlWidth      = v.childControlHeight = true;
        v.childForceExpandWidth  = true;
        v.childForceExpandHeight = false;
    }

    static void FixedHeight(RectTransform rt, float h)
    {
        var le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = h;
        le.flexibleHeight  = 0f;
    }

    // 남은 자리를 다 먹는다 — 왼쪽 상자의 글(배경·특성)은 길이가 저마다 다르다.
    static void FlexibleHeight(RectTransform rt)
    {
        var le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = -1f;
        le.minHeight       = -1f;
        le.flexibleHeight  = 1f;
    }

    static void Place(RectTransform rt, float w, float h, float cx, float cy)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(cx, cy);
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }
}
