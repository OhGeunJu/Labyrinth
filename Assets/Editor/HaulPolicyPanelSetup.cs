using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// **자동운반 품목 창**(`O`) — 규격은 씬이 갖고 내용만 런타임이 채운다(`HaulPolicyPanel`).
//
// **규칙은 `docs/editor_creators.md`에 있다 — 그것부터 읽고 짓는다.**
// 2026-09-06에 안 읽고 지었다가 넷을 한꺼번에 어겼다(사용자: *"내가 패널위에 부모로 UI가
// 필요하다고 했지? 이거 자꾸 안지키네"*):
//
//  · **`Xxx_UI` 루트가 없었다** — 창을 캔버스 **직속 자식**으로 붙였다. 그 자리는
//    `GameModeManager`가 *"필드 UI 묶음"*으로 세는 자리라(캔버스 직속 자식을 켜고 끈다),
//    거기에 창 자체를 두면 **모드가 바뀔 때 이 창만 규칙 밖에서 논다.** 루트는 늘 켜져 있고
//    창(자식)만 열리고 닫힌다 — 그래서 `Update`를 도는 컴포넌트도 **루트**에 산다.
//  · **헤더가 없었다**(#0D0D0D 풀폭) · **닫기 버튼이 규격이 아니었다**(28×28 · #802626 ·
//    글자 `X` · 공용 `CloseButton` 컴포넌트) · **폰트를 안 물렸다**(NanumGothic SDF).
//
// **다시 돌려도 안전하다** — 있으면 값만 맞추고, 옛 자리에 지은 것은 걷어낸다.
public static class HaulPolicyPanelSetup
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    const float PanelW  = 460f;
    const float PanelH  = 720f;
    const float HeaderH = 48f;
    const float HintH   = 34f;
    const float SearchH = 32f;
    const float RowH    = 30f;
    const float BarW    = 10f;

    [MenuItem("DungeonColony/UI/Setup Haul Policy Panel (자동운반 품목)")]
    static void Setup()
    {
        var canvas = GetOrCreateCanvas();
        DropLegacy(canvas);

        // ── 루트: 늘 켜져 있고, 창만 열리고 닫힌다
        var root = MakeRect("HaulPolicy_UI", canvas.transform);
        SetStretch(root);

        var panel = MakeRect("HaulPolicy_Panel", root.transform);
        var prt   = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(PanelW, PanelH);
        Img(panel).color = new Color(0.08f, 0.09f, 0.11f, 0.97f);

        // ── 헤더 (#0D0D0D 고정 · 풀폭 · 레이아웃 그룹 없이 앵커로)
        var header = MakeRect("Header", panel.transform);
        var hrt = header.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = new Vector2(0f, HeaderH);
        Img(header).color = new Color(0.05098f, 0.05098f, 0.05098f, 1f);

        // **헤더를 잡으면 창이 움직인다**(`DraggablePanel` — 씬의 다른 창들과 같은 규격).
        // 안 붙이면 이 창만 제자리에 못 박힌 채가 된다.
        var drag = header.GetComponent<DraggablePanel>() ?? Undo.AddComponent<DraggablePanel>(header);
        var dso  = new SerializedObject(drag);
        dso.FindProperty("target").objectReferenceValue = prt;
        dso.ApplyModifiedPropertiesWithoutUndo();

        var titleGo = MakeRect("Title", header.transform);
        var trt = titleGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12f, 0f); trt.offsetMax = new Vector2(-42f, 0f);
        var title = Tmp(titleGo, 16f, TextAlignmentOptions.MidlineLeft);
        title.text  = "자동으로 나를 품목";
        title.color = new Color(0.92f, 0.92f, 0.94f);

        // 닫기 — 28×28 · #802626 · 글자 `X` · 공용 `CloseButton`
        var closeGo = MakeRect("CloseButton", header.transform);
        var crt2 = closeGo.GetComponent<RectTransform>();
        crt2.anchorMin = crt2.anchorMax = crt2.pivot = new Vector2(1f, 1f);
        crt2.anchoredPosition = new Vector2(-6f, -6f);
        crt2.sizeDelta = new Vector2(28f, 28f);
        Img(closeGo).color = new Color(0.50196f, 0.14902f, 0.14902f, 1f);
        if (closeGo.GetComponent<Button>() == null) Undo.AddComponent<Button>(closeGo);

        var cTxt = MakeRect("Text", closeGo.transform); SetStretch(cTxt);
        var cTmp = Tmp(cTxt, 13f, TextAlignmentOptions.Center);
        cTmp.text = "X"; cTmp.color = Color.white;

        var closer = closeGo.GetComponent<CloseButton>() ?? Undo.AddComponent<CloseButton>(closeGo);
        var cso = new SerializedObject(closer);
        cso.FindProperty("target").objectReferenceValue = panel;
        cso.ApplyModifiedPropertiesWithoutUndo();

        // ── 안내 한 줄 (헤더 아래) — 런타임이 개수까지 여기에 적는다
        var hintGo = MakeRect("Hint", panel.transform);
        var nrt = hintGo.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0.5f, 1f);
        nrt.anchoredPosition = new Vector2(0f, -HeaderH - 2f);
        nrt.offsetMin = new Vector2(14f, nrt.offsetMin.y);
        nrt.offsetMax = new Vector2(-14f, nrt.offsetMax.y);
        nrt.sizeDelta = new Vector2(nrt.sizeDelta.x, HintH);
        var hint = Tmp(hintGo, 13f, TextAlignmentOptions.TopLeft);
        hint.color = new Color(0.62f, 0.62f, 0.66f);

        // ── 검색
        var searchGo = MakeRect("Search", panel.transform);
        var srt = searchGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = new Vector2(0f, -(HeaderH + HintH + 2f));
        srt.offsetMin = new Vector2(12f, srt.offsetMin.y);
        srt.offsetMax = new Vector2(-12f, srt.offsetMax.y);
        srt.sizeDelta = new Vector2(srt.sizeDelta.x, SearchH);
        Img(searchGo).color = new Color(0.14f, 0.14f, 0.17f);

        // **`TMP_InputField`는 부품 셋을 손으로 물려야 한다** — 텍스트 영역(마스크) · 글자 ·
        // 안내 글자. 하나라도 비면 글자가 안 보이거나 칸 밖으로 넘친다.
        var area = MakeRect("TextArea", searchGo.transform);
        var art = area.GetComponent<RectTransform>();
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(10f, 2f); art.offsetMax = new Vector2(-10f, -2f);
        if (area.GetComponent<RectMask2D>() == null) Undo.AddComponent<RectMask2D>(area);

        var sTextGo = MakeRect("Text", area.transform); SetStretch(sTextGo);
        var sText = Tmp(sTextGo, 15f, TextAlignmentOptions.MidlineLeft);
        sText.color = new Color(0.94f, 0.94f, 0.96f);

        var holderGo = MakeRect("Placeholder", area.transform); SetStretch(holderGo);
        var hold = Tmp(holderGo, 15f, TextAlignmentOptions.MidlineLeft);
        hold.color = new Color(0.55f, 0.55f, 0.60f);
        hold.text  = "이름으로 찾기";

        var input = searchGo.GetComponent<TMP_InputField>() ?? Undo.AddComponent<TMP_InputField>(searchGo);
        input.targetGraphic = Img(searchGo);
        input.textViewport  = art;
        input.textComponent = sText;
        input.placeholder   = hold;
        input.lineType      = TMP_InputField.LineType.SingleLine;
        input.SetTextWithoutNotify("");

        // ── 스크롤 뷰 (헤더+안내+검색 아래를 채운다)
        float top = HeaderH + HintH + SearchH + 10f;

        var sv = MakeRect("ScrollView", panel.transform);
        var svrt = sv.GetComponent<RectTransform>();
        svrt.anchorMin = Vector2.zero; svrt.anchorMax = Vector2.one;
        svrt.offsetMin = new Vector2(12f, 12f);
        svrt.offsetMax = new Vector2(-12f, -top);

        var sr = sv.GetComponent<ScrollRect>() ?? Undo.AddComponent<ScrollRect>(sv);
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 24f;

        var vp = MakeRect("Viewport", sv.transform);
        var vprt = vp.GetComponent<RectTransform>();
        vprt.anchorMin = Vector2.zero; vprt.anchorMax = Vector2.one;
        vprt.offsetMin = Vector2.zero; vprt.offsetMax = new Vector2(-(BarW + 4f), 0f);
        Img(vp).color = new Color(0f, 0f, 0f, 0.01f);
        var mask = vp.GetComponent<Mask>() ?? Undo.AddComponent<Mask>(vp);
        mask.showMaskGraphic = false;
        sr.viewport = vprt;

        var content = MakeRect("Content", vp.transform);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero; crt.sizeDelta = Vector2.zero;

        var vg = content.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(content);
        vg.childControlWidth = true;  vg.childControlHeight = false;
        vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
        vg.spacing = 2f; vg.padding = new RectOffset(4, 4, 4, 4);

        var fit = content.GetComponent<ContentSizeFitter>() ?? Undo.AddComponent<ContentSizeFitter>(content);
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = crt;

        // ── 스크롤바. **`ScrollView` 안, `Viewport`의 형제**로 둔다 — 뷰포트 안에 두면
        // 마스크에 잘리고 스크롤을 따라 같이 움직인다.
        //
        // **손잡이는 `SlidingArea` 안에 있어야 한다**: `Scrollbar`는 손잡이의 **부모를
        // 트랙으로 보고** 거기서 앵커를 매 프레임 다시 잡는다. 직접 붙이면 그 계산이 트랙
        // 없이 돌아 **손잡이가 트랙만 한 판**으로 남아 목록을 덮는다(2026-09-06에 겪었다).
        var barGo = MakeRect("Scrollbar", sv.transform);
        var brt = barGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1f, 0f); brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(BarW, 0f);
        Img(barGo).color = new Color(1f, 1f, 1f, 0.07f);   // 웹처럼 — 트랙은 옅은 홈

        var slide = MakeRect("SlidingArea", barGo.transform); SetStretch(slide);
        var slideImg = slide.GetComponent<Image>(); if (slideImg != null) Object.DestroyImmediate(slideImg);

        var handle = MakeRect("Handle", slide.transform);
        var hdrt = handle.GetComponent<RectTransform>();
        hdrt.offsetMin = hdrt.offsetMax = Vector2.zero;   // 나머지는 `Scrollbar`가 잡는다
        Img(handle).color = new Color(0.55f, 0.57f, 0.63f);

        var bar = barGo.GetComponent<Scrollbar>() ?? Undo.AddComponent<Scrollbar>(barGo);
        bar.direction     = Scrollbar.Direction.BottomToTop;
        bar.handleRect    = hdrt;
        bar.targetGraphic = Img(handle);

        sr.verticalScrollbar = bar;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // ── 줄 원본. **꺼 둔다** — 켜 두면 빈 줄이 늘 하나 붙어 있다.
        var row = MakeRect("RowTemplate", content.transform);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, RowH);
        Img(row).color = new Color(0.18f, 0.18f, 0.20f);

        var btn = row.GetComponent<Button>() ?? Undo.AddComponent<Button>(row);
        btn.targetGraphic = Img(row);

        var le = row.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(row);
        le.preferredHeight = RowH; le.minHeight = RowH;

        var lbl = MakeRect("Label", row.transform);
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(10f, 0f); lrt.offsetMax = new Vector2(-10f, 0f);
        Tmp(lbl, 15f, TextAlignmentOptions.MidlineLeft);
        row.SetActive(false);

        // ── 컴포넌트는 **늘 켜져 있는 루트**에 산다(창에 붙이면 `O`키가 한 번도 안 먹는다)
        var stray = panel.GetComponent<HaulPolicyPanel>();
        if (stray != null) Object.DestroyImmediate(stray);

        var comp = root.GetComponent<HaulPolicyPanel>() ?? Undo.AddComponent<HaulPolicyPanel>(root);
        var so = new SerializedObject(comp);
        so.FindProperty("panel").objectReferenceValue       = panel;
        so.FindProperty("content").objectReferenceValue     = crt;
        so.FindProperty("rowTemplate").objectReferenceValue = btn;
        so.FindProperty("headline").objectReferenceValue    = hint;
        so.FindProperty("search").objectReferenceValue      = input;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(comp);

        panel.SetActive(false);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log("[자동운반] `HaulPolicy_UI/HaulPolicy_Panel`을 규격대로 다시 세웠다 — `O`로 연다.\n" +
                  "헤더 #0D0D0D · CloseButton 컴포넌트 · 스크롤바 · 검색 · NanumGothic. **씬을 저장할 것.**");
    }

    // 옛 자리(캔버스 직속 `HaulPolicy_Panel`)와 캔버스에 붙은 컴포넌트를 걷어낸다.
    // **안 걷으면 창이 둘이 되고, `O`를 누를 때 어느 쪽이 열리는지가 순서에 달린다.**
    static void DropLegacy(Canvas canvas)
    {
        var old = canvas.transform.Find("HaulPolicy_Panel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var comp = canvas.GetComponent<HaulPolicyPanel>();
        if (comp != null) Undo.DestroyObjectImmediate(comp);
    }

    // ── 공통 헬퍼 (`docs/editor_creators.md`) ────────────────────────────────

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

    // **있으면 그것을 쓴다** — 다시 돌릴 때마다 새로 만들면 손으로 옮긴 자리가 사라진다.
    static GameObject MakeRect(string name, Transform parent)
    {
        var t = parent != null ? parent.Find(name) : null;
        if (t != null) return t.gameObject;

        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        if (parent != null) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, name);
        return go;
    }

    static void SetStretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
    }

    static TextMeshProUGUI Tmp(GameObject go, float size, TextAlignmentOptions align)
    {
        var img = go.GetComponent<Image>(); if (img != null) Object.DestroyImmediate(img);

        var tmp = go.GetComponent<TextMeshProUGUI>() ?? Undo.AddComponent<TextMeshProUGUI>(go);
        tmp.fontSize  = size;
        tmp.alignment = align;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
        return tmp;
    }

    static Image Img(GameObject go)
        => go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
}
