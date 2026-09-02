using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// **모드 도구 바를 씬에 세운다**(2026-09-02).
//
// **규격은 도구가 씬에 세우고 상태는 런타임이 읽는다**(`docs/editor_creators.md`) —
// `ModeToolBarUI`는 아무것도 안 들고 매 프레임 지정기에 물어본다.
//
// ── 자리는 손이 정한다 ────────────────────────────────────────────────────
//
// **도구가 자리를 계산하지 않는다.** 건설 칸을 재서 맞춰 보려고 했는데 두 가지가 걸렸다:
// 에디터에서는 레이아웃이 안 돌아 **잰 값이 0**이고, 무엇보다 **사용자가 이미 손으로 맞춰
// 놓는다.** 도구가 그 위에 제 계산을 덮으면 다시 돌릴 엄두가 안 난다.
//
// 그래서 **이미 있으면 그 자리를 그대로 물려준다**(모드 바가 쓰는 규칙 그대로).
// 처음 세울 때만 왼쪽 아래에 놓고, 그 뒤로는 씬에서 옮긴 것이 답이다.
//
// **크기는 물려주지 않는다** — 그건 타일 둘에서 나오는 값이지 손으로 정하는 값이 아니다
// (모드 바가 크기까지 물려줬다가 버튼이 하나 늘자 판 밖으로 삐져나왔다).
public static class ModeToolBarSetup
{
    const string BarName   = "ModeToolBar_UI";
    const string PanelName = "ModeToolBar_Panel";

    const float Gap    = 6f;
    const float Margin = 10f;

    [MenuItem("DungeonColony/Setup Mode Tool Bar")]
    public static void Create()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError("[도구 바] 씬에 Canvas가 없다."); return; }

        // **타일 프리팹은 건설 목록이 쓰는 것을 그대로 가져온다** — 따로 만들면 여백·글꼴이
        // 언젠가 갈려 **같은 화면 안에 두 종류의 타일**이 보인다.
        var build = Object.FindFirstObjectByType<BuildModeUI>(FindObjectsInactive.Include);
        if (build == null)
        {
            Debug.LogError("[도구 바] 씬에 `BuildModeUI`가 없다 — 타일 프리팹을 거기서 가져온다.");
            return;
        }

        var prefab = new SerializedObject(build).FindProperty("buildingItemPrefab")
                                                .objectReferenceValue as BuildingItemUI;
        if (prefab == null)
        {
            Debug.LogError("[도구 바] `BuildModeUI.buildingItemPrefab`이 비었다.");
            return;
        }

        var cell = ((RectTransform)prefab.transform).sizeDelta;
        if (cell.x < 1f || cell.y < 1f) cell = new Vector2(92f, 110f);

        // ── 자리는 **건설 칸(`BuildingSection`) 규격 그대로** ────────────
        //
        // 절대 좌표를 베끼면 **해상도가 바뀔 때 둘이 갈린다**(사용자가 짚었다). 같이 움직이려면
        // **같은 앵커·같은 오프셋**을 써야 한다 — 그건 재는 값이 아니라 **적혀 있는 값**이라
        // 에디터에서도 정확하다(레이아웃이 안 돌아도 0이 안 나온다).
        //
        // 부모는 아래에서 정한다 — **꺼지는 패널의 형제**라 안 숨으면서 좌표계는 같다.
        var itemsProp = new SerializedObject(build).FindProperty("buildingContainer");
        var items     = itemsProp != null ? itemsProp.objectReferenceValue as Transform : null;
        // **`BuildingSection`을 이름으로 찾아 올라간다.** `buildingContainer`는 스크롤 안의
        // `Content`라 그 **부모가 `Viewport`**인 경우가 많다 — 부모를 그냥 쓰면 뷰포트 규격을
        // 베끼게 되고, 그러면 자리가 전혀 딴 데가 된다(2026-09-02에 그래서 계속 틀렸다).
        RectTransform section = null;
        for (var t = items; t != null; t = t.parent)
            if (t.name == "BuildingSection") { section = t as RectTransform; break; }

        section ??= (items != null ? (items.parent as RectTransform) ?? items as RectTransform : null);

        if (section == null)
        {
            Debug.LogError("[도구 바] `BuildModeUI.buildingContainer`가 비어 규격을 베낄 데가 없다.");
            return;
        }

        // **이미 있으면 그 규격을 통째로 물려받는다**(2026-09-02, 사용자: *"내가 해놨으니깐"*).
        // 자리도, 타일 칸의 여백·셀 크기도 **손으로 맞춘 것이 답이다** — 도구가 제 계산을
        // 덮으면 다시 돌릴 엄두가 안 난다. 처음 세울 때만 건설 쪽에서 베낀다.
        RectTransform      keepBar  = null;
        RectTransform      keepRow  = null;
        GridLayoutGroup    keepGrid = null;

        var oldGo = Object.FindFirstObjectByType<ModeToolBarUI>(FindObjectsInactive.Include)?.gameObject
                 ?? FindByName(BarName)
                 ?? FindByName("FarmZoneBar_UI");

        if (oldGo != null)
        {
            keepBar = oldGo.GetComponent<RectTransform>();

            var oldRow = FindChild(oldGo.transform, "Items");
            if (oldRow != null)
            {
                keepRow  = oldRow as RectTransform;
                keepGrid = oldRow.GetComponent<GridLayoutGroup>();
            }

            // **값을 먼저 베껴 둔다** — 오브젝트를 지우면 그 컴포넌트도 같이 죽는다.
            if (keepBar  != null) keepBar  = Snapshot(keepBar);
            if (keepRow  != null) keepRow  = Snapshot(keepRow);
            if (keepGrid != null) keepGrid = Snapshot(keepGrid);

            Undo.DestroyObjectImmediate(oldGo);
        }

        // ── 뿌리 (스크립트만) ────────────────────────────────────────────
        //
        // **캔버스 직속이다** — 건설 창 안에 넣으면 그 창이 꺼질 때 같이 숨고(정작 밭 구역
        // 모드에서 안 보인다), 남의 창을 손볼 때마다 이게 따라 어긋난다.
        // **부모는 `BuildMode_UI`다 — `BuildMode_Panel`의 형제**(2026-09-02).
        //
        //   BuildMode_UI          ← 늘 켜져 있다(스크립트만 든 뿌리)
        //   ├ BuildMode_Panel     ← 건설 모드일 때만 켜진다
        //   │   └ BuildingSection
        //   └ ModeToolBar_UI      ← 여기. **패널 밖이라 안 숨는다**
        //
        // 캔버스 직속으로 두면 `BuildMode_UI`가 가운데 앵커라 **해상도가 바뀔 때 갈린다.**
        // 형제로 두면 그 좌표계를 같이 쓰므로 `BuildingSection` 규격이 **그대로 맞는다**
        // (`BuildMode_Panel`이 부모를 꽉 채우면 보정도 0이다).
        var togglePanel = new SerializedObject(build).FindProperty("panel").objectReferenceValue as GameObject;
        var host = togglePanel != null && togglePanel.transform.parent != null
            ? togglePanel.transform.parent
            : canvas.transform;

        var bar = new GameObject(BarName, typeof(RectTransform));
        bar.transform.SetParent(host, false);
        bar.transform.SetAsLastSibling();
        Undo.RegisterCreatedObjectUndo(bar, "도구 바");

        var brt = (RectTransform)bar.transform;
        if (keepBar != null) { Apply(brt, keepBar); goto barDone; }

        brt.anchorMin        = section.anchorMin;
        brt.anchorMax        = section.anchorMax;
        brt.pivot            = section.pivot;
        brt.anchoredPosition = section.anchoredPosition;
        brt.sizeDelta        = section.sizeDelta;
        brt.offsetMin        = section.offsetMin;
        brt.offsetMax        = section.offsetMax;

        // 낀 조상만큼 한 번 보정 — 조상이 부모를 꽉 채우면 0이다.
        Canvas.ForceUpdateCanvases();
        var sc = new Vector3[4]; section.GetWorldCorners(sc);
        var bc = new Vector3[4]; brt.GetWorldCorners(bc);
        var drift = sc[0] - bc[0];
        if (drift.sqrMagnitude > 0.0001f) brt.position += drift;

        barDone:;

        // ── 창 (보였다 사라지는 자식) ────────────────────────────────────
        var panel = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(bar.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        // **뒤판은 꺼 둔다**(사용자). 타일이 이미 제 모양을 갖고 있어 그 뒤에 검은 판이
        // 한 겹 더 깔리면 **건설 목록과 다르게 보인다.** 컴포넌트는 남긴다.
        var back = panel.GetComponent<Image>();
        back.color   = new Color(0.08f, 0.08f, 0.09f, 0.85f);
        back.enabled = false;

        // ── 타일 칸: 건설 `Content`를 **그대로 베낀다** ──────────────────
        //
        // 자리(`RectTransform`)만 맞춰서는 안 된다 — **레이아웃 설정이 타일을 놓는다.**
        // 건설 쪽은 `GridLayoutGroup`(셀 92×110 · 간격 6 · 왼쪽 아래부터 · 여백 0)이고
        // 이쪽은 `HorizontalLayoutGroup`에 여백 10이었다. **같은 프리팹을 써도 놓는 규칙이
        // 다르면 다른 자리에 선다** — 그게 계속 어긋나던 마지막 이유다.
        var srcGrid = items.GetComponent<GridLayoutGroup>();

        var row = new GameObject("Items", typeof(RectTransform), typeof(GridLayoutGroup));
        row.transform.SetParent(panel.transform, false);

        var rrt = (RectTransform)row.transform;
        var grid = row.GetComponent<GridLayoutGroup>();

        if (keepRow != null && keepGrid != null)
        {
            Apply(rrt, keepRow);
            Apply(grid, keepGrid);
            goto rowDone;
        }

        var irt = (RectTransform)items;
        rrt.anchorMin        = irt.anchorMin;
        rrt.anchorMax        = irt.anchorMax;
        rrt.pivot            = irt.pivot;
        rrt.anchoredPosition = irt.anchoredPosition;
        rrt.sizeDelta        = irt.sizeDelta;
        rrt.offsetMin        = irt.offsetMin;
        rrt.offsetMax        = irt.offsetMax;

        if (srcGrid != null)
        {
            grid.padding         = new RectOffset(srcGrid.padding.left, srcGrid.padding.right,
                                                  srcGrid.padding.top,  srcGrid.padding.bottom);
            grid.cellSize        = srcGrid.cellSize;
            grid.spacing         = srcGrid.spacing;
            grid.startCorner     = srcGrid.startCorner;
            grid.startAxis       = srcGrid.startAxis;
            grid.childAlignment  = srcGrid.childAlignment;
            grid.constraint      = srcGrid.constraint;
            grid.constraintCount = srcGrid.constraintCount;
        }
        else
        {
            // 건설 쪽이 격자가 아니면 타일 크기로 어림한다 — 그래도 자리는 같은 칸이다.
            grid.cellSize = cell;
            grid.spacing  = new Vector2(Gap, Gap);
        }

        rowDone:;

        // **타일은 씬에 세워 둔다** — 런타임에 안 찍는다(사용자 결정). 그래야 씬에서 만진
        // 것이 그대로 게임에 나온다. 개수가 늘 둘이라 *목록을 채우는 일*이 아니라 **규격**이다.
        var primary = (BuildingItemUI)PrefabUtility.InstantiatePrefab(prefab, row.transform);
        primary.gameObject.name = "Tile_Primary";

        // **셋째는 밭 구역 전용이다**(`확장`) — 다른 모드에서는 런타임이 끈다.
        var extra   = (BuildingItemUI)PrefabUtility.InstantiatePrefab(prefab, row.transform);
        extra.gameObject.name = "Tile_Extra";

        var cancel  = (BuildingItemUI)PrefabUtility.InstantiatePrefab(prefab, row.transform);
        cancel.gameObject.name = "Tile_Cancel";

        Label(primary, "새 구역");
        Label(extra,   "확장");
        Label(cancel,  "삭제");

        var ui = bar.AddComponent<ModeToolBarUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue      = panel;
        so.FindProperty("primaryItem").objectReferenceValue = primary;
        so.FindProperty("extraItem").objectReferenceValue   = extra;
        so.FindProperty("cancelItem").objectReferenceValue  = cancel;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(ui);
        Selection.activeGameObject = bar;

        Debug.Log($"[도구 바] 타일 둘을 세웠다 — 건설 목록과 같은 프리팹({prefab.name}).\n" +
                  $"자리는 {section.name}의 규격을 그대로 베꼈다 — 해상도가 바뀌어도 같이 움직인다.\n" +
                  "씬을 저장할 것.");
    }

    // ── 규격 물려주기 ───────────────────────────────────────────────────────
    //
    // **오브젝트를 지우면 컴포넌트도 같이 죽는다** — 그래서 값을 담아 둘 그릇을 하나 만들어
    // 거기 베껴 두고, 새로 세운 뒤에 옮겨 붓는다. 숨겨 둔 임시 오브젝트라 씬에 안 남는다.
    static GameObject _scratch;

    static Transform Scratch()
    {
        if (_scratch == null)
            _scratch = new GameObject("~ModeToolBarScratch") { hideFlags = HideFlags.HideAndDontSave };
        return _scratch.transform;
    }

    static RectTransform Snapshot(RectTransform src)
    {
        var go = new GameObject("snap", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(Scratch(), false);
        var rt = (RectTransform)go.transform;
        Apply(rt, src);
        return rt;
    }

    static GridLayoutGroup Snapshot(GridLayoutGroup src)
    {
        var go = new GameObject("snapGrid", typeof(RectTransform), typeof(GridLayoutGroup))
                 { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(Scratch(), false);
        var g = go.GetComponent<GridLayoutGroup>();
        Apply(g, src);
        return g;
    }

    static void Apply(RectTransform dst, RectTransform src)
    {
        dst.anchorMin        = src.anchorMin;
        dst.anchorMax        = src.anchorMax;
        dst.pivot            = src.pivot;
        dst.anchoredPosition = src.anchoredPosition;
        dst.sizeDelta        = src.sizeDelta;
        dst.offsetMin        = src.offsetMin;
        dst.offsetMax        = src.offsetMax;
    }

    static void Apply(GridLayoutGroup dst, GridLayoutGroup src)
    {
        dst.padding         = new RectOffset(src.padding.left, src.padding.right,
                                             src.padding.top,  src.padding.bottom);
        dst.cellSize        = src.cellSize;
        dst.spacing         = src.spacing;
        dst.startCorner     = src.startCorner;
        dst.startAxis       = src.startAxis;
        dst.childAlignment  = src.childAlignment;
        dst.constraint      = src.constraint;
        dst.constraintCount = src.constraintCount;
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // 이름으로 온 씬을 훑는다 — `Transform.Find`는 직속 자식만 본다.
    static GameObject FindByName(string name)
    {
        foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include,
                                                                   FindObjectsSortMode.None))
            if (rt.name == name) return rt.gameObject;
        return null;
    }

    // 타일의 이름 칸을 씬에서 적고, **비용 칸은 끈다** — 도구에는 값이 없다.
    //
    // 런타임의 `Setup`이 어차피 이름을 적지만, 그것만 두면 **씬에서는 프리팹 기본값
    // (`이름`·`비용`)이 뜬다** — 자리를 맞추려고 씬을 보는데 화면이 딴말을 하는 셈이다.
    //
    // **배경은 안 켠다** — 켜면 어두운 테두리가 한 겹 더 생겨 **건설 타일과 다르게 보인다**.
    // 켜진 도구는 이름 글자색이 말한다(`ModeToolBarUI`).
    static void Label(BuildingItemUI item, string text)
    {
        var so = new SerializedObject(item);

        var name = so.FindProperty("nameLabel").objectReferenceValue as TMPro.TextMeshProUGUI;
        if (name != null)
        {
            var ns = new SerializedObject(name);
            ns.FindProperty("m_text").stringValue = text;
            ns.ApplyModifiedPropertiesWithoutUndo();
        }

        var cost = so.FindProperty("costLabel").objectReferenceValue as TMPro.TextMeshProUGUI;
        if (cost != null) cost.gameObject.SetActive(false);
    }
}
