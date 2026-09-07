using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// **자원 노드 창을 규격대로 다시 짓는다**(2026-09-06).
//
// 사용자: *"Resource Node UI를 좀더 크고 깔끔하게 바꿔봐. 지금은 좀 옹기종기 모여있는
// 느낌이라서 별로네."* — 맞는 말이었다. 원래 창은 좁은 판에 이름·게이지·재고·목록·버튼이
// 여백 없이 붙어 있었고, 거기에 내가 상태 글과 버튼 둘을 **아래에 덧붙이면서** 더 빽빽해졌다.
//
// **덧붙이기를 그만두고 다시 짓는다.** 규칙은 `docs/editor_creators.md`에 있다:
// `Xxx_UI` 루트 · #0D0D0D 풀폭 헤더 · 28×28 #802626 닫기(`CloseButton` 컴포넌트) ·
// NanumGothic · 특수문자 금지.
//
// ## 짜임
//
// 세로로 **네 덩이**이고 사이를 벌린다 — *무엇인가 / 어떻게 되고 있나 / 무엇이 쌓였나 /
// 누가 하고 있나*. 덩이의 순서가 곧 **읽는 순서**다: 위에서부터 보다가 원하는 답이 나오면
// 멈춘다.
//
//   헤더            이름 + 닫기
//   재고            아이콘 + 수량 + [옮긴다] + 운반 임계 [-][+]
//   진행            게이지 막대 + %
//   붙은 폰         목록 + [배정]
//   상태            전력 · 사람 · 산출 추정 (여러 줄)
//   하단 바         [자동운반] [전력]
//
// **순서는 사용자가 씬에서 손으로 맞춰 본 것이다**(2026-09-06) — *무엇이 얼마나 쌓였나*가
// 먼저고, 그다음이 *지금 얼마나 찼나*, 그다음이 *누가 하고 있나*, 마지막이 *왜 이런가*다.
// 위 셋은 **한눈에 보는 값**이고 상태는 **막혔을 때만 읽는 글**이라, 글이 위에 있으면
// 매번 지나쳐야 한다.
//
// **`pawnEntryPrefab`은 안 건드린다** — 도구가 만들 수 없는 참조라(프리팹), 이미 물려 있는
// 것을 그대로 쓴다. 다시 지어도 그 칸만은 살아남는다.
//
// **다시 돌려도 안전하다** — 있으면 값만 맞춘다.
public static class ResourceNodePanelSetup
{
    const string FontPath = "Assets/NanumGothic SDF.asset";

    const float PanelW  = 460f;
    // **창은 담은 것만큼만 크다**(2026-09-06, 사용자: *"전체적으로 좀 위에 쏠려 있다는
    // 느낌이 들어"*). 720이었을 때는 내용이 위쪽 절반에 몰리고 아래 200px이 **빈 판**이었다 —
    // 세로 레이아웃은 위에서부터 쌓으므로 남는 높이가 전부 아래로 간다.
    //
    // 가운데 정렬로 옮기는 길도 있지만 그러면 **위아래가 다 떠서** 헤더와 하단 바에서
    // 멀어진다. 빈 자리를 나눠 갖는 것보다 **없애는 편**이 맞다.
    //
    //   덩이 합 382(재고 76 + 진행 46 + 붙은 폰 164 + 상태 96) + 사이 42(14 x 3)
    //   + 위 60(헤더 48 + 12) + 아래 54(바 44 + 10) = 538
    //
    // 거기에 **`Slack`을 더한다** — 딱 맞추니 이번엔 *짧뚱하다*는 말이 나왔다(사용자).
    // **끝을 딱 맞추는 것과 숨 쉴 자리를 두는 것은 다른 일이다**: 상태 글은 줄 수가
    // 노드마다 다르므로(전력이 없으면 한 줄, 있으면 셋) 딱 맞추면 **긴 쪽이 하단 바에
    // 닿는다.** 남는 자리는 전부 상태 아래로 간다(세로 레이아웃이 위에서부터 쌓는다).
    //
    // **덩이 높이를 고치면 이 값도 같이 고칠 것** — 안 고치면 다시 빈 판이 생긴다.
    const float Slack   = 150f;
    const float PanelH  = 538f + Slack;
    const float HeaderH = 48f;
    const float BarH    = 44f;   // 하단 버튼 줄
    const float Pad     = 16f;

    // 자리 한 칸. **폰 줄 프리팹의 크기와 같아야 한다**(100x100) — 갈리면 빈 칸만
    // 크기가 달라 줄이 어긋난다.
    const float SlotSize = 100f;

    static readonly Color PanelBg  = new(0.08f, 0.09f, 0.11f, 0.97f);
    static readonly Color HeaderBg = new(0.05098f, 0.05098f, 0.05098f, 1f);
    static readonly Color CloseBg  = new(0.50196f, 0.14902f, 0.14902f, 1f);
    static readonly Color BtnBg    = new(0.20f, 0.20f, 0.22f);
    static readonly Color Ink      = new(0.90f, 0.90f, 0.93f);
    static readonly Color Dim      = new(0.62f, 0.62f, 0.66f);

    [MenuItem("DungeonColony/UI/Setup Resource Node Panel (자원 노드 창)")]
    static void Setup()
    {
        var canvas = GetOrCreateCanvas();

        // ── 옛 창을 **컴포넌트에게 물어본다**(2026-09-06에 두 번 틀렸다).
        //
        // 처음에는 `Find("ResourceNodePanel")`로 지웠는데 씬의 옛 창은 **다른 이름**이었다.
        // 그다음엔 *루트의 자식 중 새 것만 남기고 지우기*로 바꿨는데, 옛 창이 **루트 밖에**
        // 있으면 그것도 안 걸린다. 화면에는 옛 위젯과 새 위젯이 **겹쳐서** 보였다.
        //
        // **`ResourceNodeUI.panel`이 옛 창이 무엇인지 이미 알고 있다** — 이름을 맞히거나
        // 자리를 짐작할 이유가 없다. 참조가 답이다.
        //
        // **새 것을 짓기 전에 묻는다** — `MakeRect`가 이름이 같으면 그것을 다시 쓰므로,
        // 순서가 뒤집히면 방금 지은 창을 옛 창으로 알고 지운다.
        var ui = Object.FindFirstObjectByType<ResourceNodeUI>(FindObjectsInactive.Include);

        Object carriedEntry = null;
        GameObject oldPanel = null;
        if (ui != null)
        {
            var prev = new SerializedObject(ui);
            var e = prev.FindProperty("pawnEntryPrefab");
            if (e != null) carriedEntry = e.objectReferenceValue;

            var op = prev.FindProperty("panel");
            if (op != null) oldPanel = op.objectReferenceValue as GameObject;
        }

        var root = MakeRect("ResourceNode_UI", canvas.transform);
        SetStretch(root);

        // **옛 창이 루트 자신이면 지우면 안 된다** — 그 안에 지금 컴포넌트가 산다.
        // 그때는 자식만 비운다(아래 갈래가 그 일을 한다).
        if (oldPanel != null && oldPanel != root)
            Undo.DestroyObjectImmediate(oldPanel);

        // **옛 창은 이름으로 찾지 않는다**(2026-09-06에 겪었다).
        //
        // `Find("ResourceNodePanel")`로 지웠는데 씬의 옛 창은 **다른 이름**이었다. 그래서
        // 안 지워졌고, 새 창이 그 위에 하나 더 서서 **머리띠가 둘로 보였다**(사용자:
        // *"왜 헤더랑 타이틀바 두개가 있어"*).
        //
        // **남길 것 하나만 이름으로 알고 나머지는 전부 지운다** — 이름을 맞히는 쪽이 아니라
        // **남길 것을 아는 쪽**이 맞다(맞히는 쪽은 이름이 바뀔 때마다 조용히 실패한다).
        // 루트 아래에 남은 옛 조각도 걷는다 — **남길 것 하나만 이름으로 알고 나머지는
        // 전부 지운다**(이름을 맞히는 쪽은 이름이 바뀔 때마다 조용히 실패한다).
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i);
            if (child.name != "ResourceNode_Panel") Undo.DestroyObjectImmediate(child.gameObject);
        }

        var panel = MakeRect("ResourceNode_Panel", root.transform);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0.5f);
        prt.anchoredPosition = new Vector2(-24f, 0f);   // 오른쪽 — 살펴보기 창과 안 겹치게
        prt.sizeDelta = new Vector2(PanelW, PanelH);
        Img(panel).color = PanelBg;

        // ── 헤더
        var header = Band(panel.transform, "Header", 1f, 0f, HeaderH);
        Img(header).color = HeaderBg;

        // **헤더가 곧 타이틀바다** — 이 저장소에 바가 둘인 창은 없다(`docs/editor_creators.md`).
        // 그리고 **헤더를 잡아 끌면 창이 움직인다**(`DraggablePanel`, 씬의 다른 창 스물한 개가
        // 같은 규격이다). 안 붙이면 **이 창만 제자리에 못 박힌 채**가 되고, 그건 다른 창을
        // 옮겨 본 사람에게 *고장*으로 읽힌다.
        Drag(header, panel);

        var nameText = Label(header.transform, "Title", 17f, TextAlignmentOptions.MidlineLeft, Ink);
        Inset(nameText.gameObject, 14f, 46f);
        nameText.text = "자원";

        MakeClose(header.transform, panel);

        // ── 하단 바(버튼 둘) — 아래에서부터 자리를 잡아야 가운데가 남는다
        var bar = Band(panel.transform, "BottomBar", 0f, 0f, BarH);
        Img(bar).color = new Color(0.06f, 0.06f, 0.08f);

        var haulPolicy = BarButton(bar.transform, "HaulPolicyButton", "자동운반 불가",
                                   new Vector2(0f, 0f), new Vector2(0.5f, 1f), Pad, 4f);
        var power      = BarButton(bar.transform, "PowerButton", "전력 끈다",
                                   new Vector2(0.5f, 0f), new Vector2(1f, 1f), 4f, Pad);

        // ── 가운데: 세로로 쌓는다
        var body = MakeRect("Body", panel.transform);
        var brt = body.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(Pad, BarH + 10f);
        brt.offsetMax = new Vector2(-Pad, -(HeaderH + 12f));
        StripImage(body);

        var col = body.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(body);
        col.spacing = 14f;                       // **덩이 사이를 벌린다** — 이것이 "깔끔"의 절반이다
        col.childControlWidth  = true;  col.childControlHeight  = false;
        col.childForceExpandWidth = true; col.childForceExpandHeight = false;

        // 진행
        var prog = Section(body.transform, "Progress", 46f);
        var gaugeBg = MakeRect("Bar", prog.transform);
        var gbrt = gaugeBg.GetComponent<RectTransform>();
        gbrt.anchorMin = new Vector2(0f, 0.5f); gbrt.anchorMax = new Vector2(1f, 0.5f);
        gbrt.pivot = new Vector2(0.5f, 0.5f);
        gbrt.anchoredPosition = Vector2.zero;
        // **벽에서 뗀다**(사용자: *"초록색 게이지가 너무 왼쪽에 딱 달라붙어 있어"*) —
        // 막대는 **채워지는 것**이라 끝이 판에 닿아 있으면 *어디까지가 막대인지*가 안 보인다.
        gbrt.offsetMin = new Vector2(6f, -9f); gbrt.offsetMax = new Vector2(-62f, 9f);
        // 막대의 빈 부분 — **판이 아니라 홈이다.** 창 바탕보다 살짝 어두워야
        // *"여기까지 찬다"*가 보인다.
        Img(gaugeBg).color = new Color(0f, 0f, 0f, 0.28f);

        var fillGo = MakeRect("Fill", gaugeBg.transform);
        SetStretch(fillGo);
        var gaugeFill = Img(fillGo);
        gaugeFill.color = new Color(0.40f, 0.80f, 0.55f);
        gaugeFill.type  = Image.Type.Filled;
        gaugeFill.fillMethod = Image.FillMethod.Horizontal;
        gaugeFill.fillAmount = 0f;

        var gaugeText = Label(prog.transform, "Percent", 14f, TextAlignmentOptions.MidlineRight, Dim);
        var gtrt = gaugeText.GetComponent<RectTransform>();
        gtrt.anchorMin = new Vector2(1f, 0f); gtrt.anchorMax = new Vector2(1f, 1f);
        gtrt.pivot = new Vector2(1f, 0.5f);
        gtrt.sizeDelta = new Vector2(52f, 0f);
        gtrt.anchoredPosition = Vector2.zero;
        gaugeText.text = "0%";

        // 상태 — 멈춘 이유가 여기 적힌다.
        //
        // **판을 안 깐다**(2026-09-06, 사용자: *"붙은폰을 제외하고는 다 뒤가 하나의 배경으로
        // 통일되어 있으면 좋겠어"*). 칸마다 판을 깔면 **창 하나에 사각형이 넷**이 되어,
        // 무엇이 묶음이고 무엇이 그냥 글인지가 안 읽힌다. **판은 목록에만 둔다** — 거기만
        // *여러 개가 담기는 그릇*이고 나머지는 한 줄짜리 값이다.
        var statusBox = Section(body.transform, "Status", 96f);
        var statusText = Label(statusBox.transform, "Text", 14f, TextAlignmentOptions.TopLeft, Ink);
        Inset(statusText.gameObject, 4f, 4f, 4f, 4f);

        // 재고 — 판을 안 깐다(위 "상태" 참조).
        var stock = Section(body.transform, "Stock", 76f);

        var iconGo = MakeRect("Icon", stock.transform);
        var irt = iconGo.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0f, 0.5f); irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(4f, 0f);
        irt.sizeDelta = new Vector2(56f, 56f);
        var stockIcon = Img(iconGo);
        stockIcon.preserveAspect = true;

        var stockCount = Label(stock.transform, "Count", 16f, TextAlignmentOptions.MidlineLeft, Ink);
        var scrt = stockCount.GetComponent<RectTransform>();
        scrt.anchorMin = new Vector2(0f, 0f); scrt.anchorMax = new Vector2(1f, 1f);
        scrt.offsetMin = new Vector2(72f, 0f); scrt.offsetMax = new Vector2(-190f, 0f);
        stockCount.text = "0";

        var transfer = SmallButton(stock.transform, "TransferButton", "옮긴다",
                                   new Vector2(1f, 0.5f), new Vector2(-4f, 0f), 78f);

        var thrDown = SmallButton(stock.transform, "ThresholdDown", "-",
                                  new Vector2(1f, 0.5f), new Vector2(-90f, 0f), 30f);
        var thrText = Label(stock.transform, "ThresholdText", 13f, TextAlignmentOptions.Center, Dim);
        var ttrt = thrText.GetComponent<RectTransform>();
        ttrt.anchorMin = ttrt.anchorMax = new Vector2(1f, 0.5f);
        ttrt.pivot = new Vector2(1f, 0.5f);
        ttrt.anchoredPosition = new Vector2(-122f, 0f);
        ttrt.sizeDelta = new Vector2(58f, 26f);
        thrText.text = "운반:10";
        var thrUp = SmallButton(stock.transform, "ThresholdUp", "+",
                                new Vector2(1f, 0.5f), new Vector2(-182f, 0f), 30f);

        // 붙은 폰 — **여기도 판을 안 깐다**(2026-09-06).
        //
        // 판을 깔았더니 폰 줄 뒤로 **큰 회색 사각형**이 서서, 사용자가 그것을 *폰 줄 자체의
        // 배경*으로 읽었다(*"그걸 왜 저렇게 크게 만들어놨어 한명 가릴만큼만 유지시켰어야지"*).
        // **한 명이 붙어 있는데 네 명 자리가 비어 보이면 그 여백이 뜻을 갖는 것처럼 읽힌다.**
        //
        // 배경은 **창 하나**여야 하고, 그 위에 도드라지는 것은 **실제로 있는 것**뿐이다 —
        // 폰 줄은 제 밝은 판을 이미 갖고 있다(`pawnEntryPrefab`). 빈 자리는 그냥 빈 자리다.
        var crew = Section(body.transform, "Crew", 164f);

        var crewTitle = Label(crew.transform, "Title", 13f, TextAlignmentOptions.TopLeft, Dim);
        var ctrt = crewTitle.rectTransform;
        ctrt.anchorMin = new Vector2(0f, 1f); ctrt.anchorMax = new Vector2(1f, 1f);
        ctrt.pivot = new Vector2(0.5f, 1f);
        ctrt.sizeDelta = new Vector2(-8f, 18f);
        ctrt.anchoredPosition = Vector2.zero;
        crewTitle.text = "붙은 폰";

        // 자리를 **가로로 늘어놓는다**(2026-09-06, 사용자가 그려 준 그림 그대로).
        //
        // 세로 목록이었을 때는 **몇 자리짜리 광맥인지가 화면에 없었다** — 한 명이 붙어 있으면
        // 줄 하나만 보이고, 자리가 넷인지 하나인지는 버튼의 `1/1`을 읽어야 알 수 있었다.
        // 가로로 **빈 칸까지 그리면 그 숫자가 그림이 된다**(인벤토리 칸과 같은 규칙).
        //
        // **크기를 안 건드린다**(`childControl*` 전부 꺼짐) — 폰 줄 프리팹이 이미 100x100
        // 정사각 타일이라, 여기서 늘리면 프리팹의 저작이 창마다 달라진다.
        var list = MakeRect("List", crew.transform);
        var lrt = list.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.sizeDelta = new Vector2(0f, SlotSize);
        lrt.anchoredPosition = new Vector2(0f, -22f);
        StripImage(list);

        // 다시 돌릴 때 옛 세로 레이아웃을 걷는다 — 한 오브젝트에 레이아웃 그룹이 둘이면
        // 유니티가 경고만 하고 **어느 쪽이 이기는지는 순서에 달린다.**
        var oldCol = list.GetComponent<VerticalLayoutGroup>();
        if (oldCol != null) Object.DestroyImmediate(oldCol);

        var lrow = list.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(list);
        lrow.spacing = 9f;
        lrow.childAlignment = TextAnchor.UpperLeft;
        lrow.childControlWidth  = false; lrow.childControlHeight  = false;
        lrow.childForceExpandWidth = false; lrow.childForceExpandHeight = false;

        // **버튼은 글자만큼만 넓다** — `배정 3/3`이 들어가면 그만이다. 넓게 잡으면
        // 줄 아래에 판이 하나 더 있는 것처럼 보인다.
        var assign = SmallButton(crew.transform, "AssignButton", "배정",
                                 new Vector2(0.5f, 0f), new Vector2(0f, 4f), 84f);

        // **차례를 코드가 못 박는다** — `MakeRect`는 이름이 같으면 **있던 것을 다시 쓰므로**,
        // 만드는 순서를 바꿔도 **이미 씬에 있는 것들의 차례는 안 바뀐다**(다시 돌려도 옛 차례
        // 그대로다). 세로 레이아웃은 형제 순서로 쌓으므로 여기서 명시한다.
        stock.transform.SetSiblingIndex(0);
        prog.transform.SetSiblingIndex(1);
        crew.transform.SetSiblingIndex(2);
        statusBox.transform.SetSiblingIndex(3);

        // ── 배선
        //
        // **도구가 못 만드는 참조는 옮겨 담는다**(`pawnEntryPrefab` — 프리팹이라 여기서 지을
        // 수가 없다. 위에서 미리 받아 두었다). 컴포넌트를 옮기려고 지웠다 다시 붙이면 **그 칸이
        // 같이 사라지고**, 화면에는 *"아무도 안 붙었다"*로만 보인다.
        //
        // 도구가 세울 수 있는 것은 다시 세우면 그만이지만, **사람이 물려 둔 것은 한 번 잃으면
        // 도구로는 못 되돌린다.**
        if (ui == null) ui = Undo.AddComponent<ResourceNodeUI>(root);
        else if (ui.gameObject != root)
        {
            // 옛 창에 붙어 있던 컴포넌트는 옮긴다 — 둘이 남으면 어느 쪽이 여는지가 순서에 달린다.
            Object.DestroyImmediate(ui);
            ui = Undo.AddComponent<ResourceNodeUI>(root);
        }

        var so = new SerializedObject(ui);
        Set(so, "panel", panel);
        Set(so, "nameText", nameText);
        Set(so, "gaugeText", gaugeText);
        Set(so, "gaugeFill", gaugeFill);
        Set(so, "stockIcon", stockIcon);
        Set(so, "stockCount", stockCount);
        Set(so, "assignButton", assign);
        Set(so, "assignedListRoot", list.transform);
        Set(so, "transferButton", transfer);
        Set(so, "haulThresholdText", thrText);
        Set(so, "haulThresholdUpButton", thrUp);
        Set(so, "haulThresholdDownButton", thrDown);
        Set(so, "statusText", statusText);
        Set(so, "haulPolicyButton", haulPolicy);
        Set(so, "powerButton", power);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);

        NormalizePawnEntry(entryAfter(so));

        // 받아 둔 것을 되돌려 놓는다. **덮어쓰지는 않는다** — 이미 물려 있으면 그것이 답이다.
        var entry = so.FindProperty("pawnEntryPrefab");
        if (entry != null && entry.objectReferenceValue == null && carriedEntry != null)
        {
            entry.objectReferenceValue = carriedEntry;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // **비었으면 말한다** — 비면 목록이 통째로 안 그려지는데 화면에는 *"아무도 안 붙었다"*
        // 로만 보인다(원인이 창에 있다는 표시가 아무 데도 없다).
        if (entry != null && entry.objectReferenceValue == null)
            Debug.LogWarning("[노드 창] `pawnEntryPrefab`이 비어 있다 — 붙은 폰 목록이 안 그려진다. " +
                             "`3_Prefab/UI`의 폰 줄 프리팹을 인스펙터에서 물릴 것.");

        panel.SetActive(false);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log($"[노드 창] `ResourceNode_UI/ResourceNode_Panel`을 {PanelW:0}x{PanelH:0}으로 " +
                  "다시 세웠다 — 헤더 · 진행 · 상태 · 재고 · 붙은 폰 · 하단 바. **씬을 저장할 것.**");
    }

    static PawnEntryUI entryAfter(SerializedObject so)
    {
        var p = so.FindProperty("pawnEntryPrefab");
        return p != null ? p.objectReferenceValue as PawnEntryUI : null;
    }

    // 폰 줄 프리팹의 **속을 가운데로 모은다**(2026-09-06, 사용자: *"폰 대가리가 너무
    // 천장에 딱 붙어 있잖아"*).
    //
    // 속(초상화 60 + 이름 14 + 사이·여백)이 84라 100짜리 타일에서 **16px이 남는데**,
    // `childAlignment`가 `UpperCenter`라 그 16이 **전부 아래로** 갔다 — 머리는 천장에
    // 붙고 이름 밑만 비었다.
    //
    // **프리팹을 여기서 고치는 이유**: 이 값은 *창의 저작*이 아니라 **타일 자체의 규격**이고,
    // 창을 다시 지을 때마다 손으로 맞추게 하면 언젠가 한 번은 안 맞춘다. 이미 맞으면
    // 아무것도 안 한다(다시 돌려도 프리팹이 안 더러워진다).
    static void NormalizePawnEntry(PawnEntryUI prefab)
    {
        if (prefab == null) return;

        var col = prefab.GetComponent<VerticalLayoutGroup>();
        if (col == null || col.childAlignment == TextAnchor.MiddleCenter) return;

        col.childAlignment = TextAnchor.MiddleCenter;
        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();

        Debug.Log("[노드 창] 폰 줄 프리팹의 속을 가운데로 모았다 — 초상화가 천장에 안 붙는다.");
    }

    // ── 조각 ────────────────────────────────────────────────────────────────

    // 위(anchorY=1) 또는 아래(0)에 붙는 풀폭 띠.
    static GameObject Band(Transform parent, string name, float anchorY, float offset, float h)
    {
        var go = MakeRect(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, anchorY); rt.anchorMax = new Vector2(1f, anchorY);
        rt.pivot = new Vector2(0.5f, anchorY);
        rt.anchoredPosition = new Vector2(0f, anchorY > 0.5f ? -offset : offset);
        rt.sizeDelta = new Vector2(0f, h);
        return go;
    }

    // 세로 레이아웃의 한 칸.
    //
    // **높이를 두 곳에 적는다**(2026-09-06에 겪었다). `VerticalLayoutGroup.childControlHeight`가
    // 꺼져 있으면 레이아웃이 **`LayoutElement`를 안 보고 자식의 `sizeDelta`를 그대로 쓴다** —
    // 그래서 `preferredHeight`만 준 칸들이 전부 높이 0으로 쌓여 **붙은 폰과 상태가 겹쳤다.**
    //
    // 켜는 쪽으로 고칠 수도 있지만, 그러면 **칸 안의 자식들이 늘어난다**(앵커로 잡아 둔 것들이
    // 레이아웃에 끌려간다). 높이만 두 곳에 적는 편이 손대는 데가 적다.
    static GameObject Section(Transform parent, string name, float h)
    {
        var go = MakeRect(name, parent);

        var le = go.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(go);
        le.minHeight = le.preferredHeight = h;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);

        StripImage(go);
        return go;
    }

    static Button BarButton(Transform parent, string name, string label,
                            Vector2 aMin, Vector2 aMax, float padL, float padR)
    {
        var go = MakeRect(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = new Vector2(padL, 7f);
        rt.offsetMax = new Vector2(-padR, -7f);
        Img(go).color = BtnBg;

        var b = go.GetComponent<Button>() ?? Undo.AddComponent<Button>(go);
        b.targetGraphic = Img(go);

        var t = Label(go.transform, "Text", 14f, TextAlignmentOptions.Center, Ink);
        SetStretch(t.gameObject);
        t.text = label;
        return b;
    }

    static Button SmallButton(Transform parent, string name, string label,
                              Vector2 anchor, Vector2 pos, float w)
    {
        var go = MakeRect(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x, anchor.y);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, 28f);
        Img(go).color = BtnBg;

        var b = go.GetComponent<Button>() ?? Undo.AddComponent<Button>(go);
        b.targetGraphic = Img(go);

        var t = Label(go.transform, "Text", 13f, TextAlignmentOptions.Center, Ink);
        SetStretch(t.gameObject);
        t.text = label;
        return b;
    }

    // 헤더를 잡아 끌면 창이 움직인다. **`target`을 반드시 물릴 것** — 비우면 헤더만
    // 떨어져 나간다(제 자신을 옮긴다).
    static void Drag(GameObject header, GameObject panel)
    {
        var d = header.GetComponent<DraggablePanel>() ?? Undo.AddComponent<DraggablePanel>(header);
        var so = new SerializedObject(d);
        so.FindProperty("target").objectReferenceValue = panel.GetComponent<RectTransform>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void MakeClose(Transform header, GameObject panel)
    {
        var go = MakeRect("CloseButton", header);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-6f, -6f);
        rt.sizeDelta = new Vector2(28f, 28f);
        Img(go).color = CloseBg;
        if (go.GetComponent<Button>() == null) Undo.AddComponent<Button>(go);

        var t = Label(go.transform, "Text", 13f, TextAlignmentOptions.Center, Color.white);
        SetStretch(t.gameObject);
        t.text = "X";

        var closer = go.GetComponent<CloseButton>() ?? Undo.AddComponent<CloseButton>(go);
        var so = new SerializedObject(closer);
        so.FindProperty("target").objectReferenceValue = panel;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── 공통 헬퍼 (`docs/editor_creators.md`) ────────────────────────────────

    static void Set(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[노드 창] `{field}` 칸이 없다 — 이름이 바뀌었나?");
    }

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

    static void Inset(GameObject go, float left, float right, float top = 0f, float bottom = 0f)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static TextMeshProUGUI Label(Transform parent, string name, float size,
                                 TextAlignmentOptions align, Color color)
    {
        var go = MakeRect(name, parent);
        StripImage(go);

        var tmp = go.GetComponent<TextMeshProUGUI>() ?? Undo.AddComponent<TextMeshProUGUI>(go);
        tmp.fontSize  = size;
        tmp.alignment = align;
        tmp.color     = color;
        tmp.richText  = true;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
        return tmp;
    }

    static void StripImage(GameObject go)
    {
        var img = go.GetComponent<Image>();
        if (img != null) Object.DestroyImmediate(img);
    }

    static Image Img(GameObject go)
        => go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
}
