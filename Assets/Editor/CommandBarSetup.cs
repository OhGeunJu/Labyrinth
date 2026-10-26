using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// **명령 — 씬을 고치는 도구**(2026-09-19, 사용자: *"채굴 채집 운반 세 버튼을 명령으로 합쳐서 한번에 뜨게"*
// · *"원래 있던 취소 버튼도 달아"* · *"캐릭터 정보를 U로"*).
//
// 모드 바·도구 바는 씬에 세운 것이다(*개수가 정해진 것은 씬에 세운다* — 런타임이 만들지 않는다). 그래서
// 바꾸는 것도 이 도구가 씬에서 한다. **여러 번 돌려도 된다** — 이미 된 것은 건너뛴다.
//
//   1. **모드 바** — `채굴` 칸을 `명령`으로(이름 · `ModeBarUI.Mode.Command`), `운반` 칸을 지우고,
//      **빈 틈을 당겨 닫는다.** 첫 판은 가로로만 당겨서 **세로 바에 구멍이 남았다**(사용자: *"모양이 이상해"*) —
//      이제 칸들이 어느 축으로 늘어섰는지를 **자리에서 잰다.**
//   2. **도구 바** — 칸 목록이 `ModeToolBarUI.CommandTiles`(일곱)보다 적으면 마지막 칸을 복제해 **한 칸씩 뒤에**
//      세운다. 판이 점에 앵커돼 있으면 그만큼 늘린다(첫 칸 쪽 모서리는 그대로).
//   3. **스탯 창 키** — C는 명령의 `취소`가 가져갔다. 씬이 값을 들고 있어 여기서 U로 바꾼다.
//   4. **하단 가운데로**(사용자: *"UI 위치가 좀 거슬리네 … 하단 가운데에 만들자"* — 켄시의 아이콘 줄) — 모드 바를
//      화면 아래 가운데 **가로 한 줄**로, 도구 바를 그 **바로 위 가운데**로. 칸 크기는 지금 칸에서 잰다.
//   5. **도구 줄을 모드 바에 붙인다**(2026-09-28) — 도구 칸을 모드 버튼과 같은 납작한 글자 버튼으로, 모드 바
//      윗변에 틈 없이, 켜진 칸만큼 줄어드는 판으로(`Dock`). 모드 바 순서는 키 줄과 같게(명령 C · 밭 V · 건설 B)
//      하고 버튼 이름에 키를 적는다(`ModeOrder` · `ModeLabels`).
//   6. **지휘소·상단을 모드 바 끝에**(2026-10-09, 사용자: *"지휘소랑 상단은 왜 왼쪽 상단에 있는거야? 차라리 가운데 하단에
//      버튼들이랑 같이"*) — 마지막 모드 칸을 복제해 `Posts`·`Caravan` 칸을 세우고, 거점 바에 있던 두 버튼은 지운다(`WindowButtons`).
//      같은 날 무리 관리 · 세력 · 지도도(*"PLM 정도?"*).
//   7. **살펴보기 창을 바 위로**(2026-10-23) — 왼쪽 아래 창 둘이 바의 왼쪽 끝을 덮었다. 바는 그대로, 창을 올린다(`LiftInspect`).
//
// 칸 크기와 틈은 **이미 있는 칸에서 잰다** — 상수로 적으면 씬을 손본 날 어긋난다. 끝나면 씬을 저장할 것.
public static class CommandBarSetup
{
    const int Mine = 1, Haul = 2, Command = 4, Posts = 5, Caravan = 6, Colony = 7, Factions = 8, Map = 9;   // `ModeBarUI.Mode` — 정수로 직렬화된다

    [MenuItem("DungeonColony/UI/Setup Command Bar (명령 버튼 · 취소 칸 · 스탯 U키 · 하단 가운데)")]
    static void Run()
    {
        var log = new List<string>();
        Wake<ModeBarUI>("모드 바", log);
        Wake<ModeToolBarUI>("도구 바", log);
        HoistOffPanel(log);
        ModeBar(log);
        WindowButtons(log);
        ToolBar(log);
        StatsKey(log);
        ModeOrder(log);
        ModeLabels(log);
        BottomCenter(log);

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[명령] " + (log.Count > 0 ? string.Join(" · ", log) : "이미 다 돼 있다") + " — **씬을 저장할 것.**");
    }

    // ── 0. 뿌리를 켠다 ───────────────────────────────────────────────────────
    //
    // **뿌리는 늘 켜져 있고 판만 열리고 닫힌다**(창 규약) — 도구 바는 `Update`에서 명령 모드를
    // 물어 판을 켜므로, 뿌리가 꺼지면 **명령을 눌러도 아무것도 안 뜨고 경고도 없다**(2026-09-25,
    // 사용자: *"명령 눌러도 뭐가 안보이는데 … 패널들을 다 꺼놔서 그런건지"*). 씬이 바이너리라 밖에서
    // 들여다볼 수도 없어서, 여기서 켜고 **무엇을 켰는지 말한다.** 판(자식)은 안 건드린다 — 그건 런타임이 켠다.
    static void Wake<T>(string label, List<string> log) where T : Behaviour
    {
        var ui = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (ui == null) return;

        for (var t = ui.transform; t != null; t = t.parent)
        {
            if (t.GetComponent<Canvas>() != null && t.parent == null) break;   // 캔버스 자신은 안 건드린다
            if (t.gameObject.activeSelf) continue;
            Undo.RecordObject(t.gameObject, "명령");
            t.gameObject.SetActive(true);
            log.Add($"{label} `{t.name}` 켬");
        }
        if (!ui.enabled)
        {
            Undo.RecordObject(ui, "명령");
            ui.enabled = true;
            log.Add($"{label} 컴포넌트 켬");
        }
    }

    // **컴포넌트는 판 위가 아니라 뿌리에 산다.** 판을 끄는 것이 기본 상태인데(창 규약), 도구 바가
    // 판 자신이나 판 안쪽에 붙어 있으면 **판을 끄는 순간 `Update`가 죽어 다시는 판을 못 켠다** —
    // 명령을 눌러도 아무것도 안 뜨고 경고도 없다. 판의 부모로 옮긴다(배선은 그대로 따라간다).
    // 어떻게 생겼는지를 먼저 적는다 — 씬이 바이너리라 **이 한 줄이 밖에서 보는 유일한 창이다.**
    static void HoistOffPanel(List<string> log)
    {
        var bar = Object.FindFirstObjectByType<ModeToolBarUI>(FindObjectsInactive.Include);
        if (bar == null) { log.Add("도구 바 컴포넌트가 씬에 없음"); return; }

        var so    = new SerializedObject(bar);
        var panel = so.FindProperty("panel").objectReferenceValue as GameObject;

        string Path(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Insert(0, t.name + (t.gameObject.activeSelf ? "" : "(꺼짐)"));
            return string.Join("/", parts);
        }
        Debug.Log($"[명령] 도구 바 컴포넌트: {Path(bar.transform)} · 판: {(panel != null ? Path(panel.transform) : "안 물림")}");

        if (panel == null) { log.Add("도구 바 `panel`이 안 물렸다"); return; }
        if (!bar.transform.IsChildOf(panel.transform)) return;   // 판 밖(뿌리)에 있다 — 정상

        var host = panel.transform.parent;
        if (host == null) { log.Add("판에 부모가 없어 도구 바를 못 옮김"); return; }

        UnityEditorInternal.ComponentUtility.CopyComponent(bar);
        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(host.gameObject);
        Undo.DestroyObjectImmediate(bar);
        log.Add($"도구 바 컴포넌트를 판(`{panel.name}`)에서 `{host.name}`로 옮김");
    }

    // ── 1. 모드 바 ───────────────────────────────────────────────────────────

    static void ModeBar(List<string> log)
    {
        var bar = Object.FindFirstObjectByType<ModeBarUI>(FindObjectsInactive.Include);
        if (bar == null) { log.Add("모드 바 없음"); return; }

        var so    = new SerializedObject(bar);
        var slots = so.FindProperty("slots");

        int mine = -1, haul = -1, command = -1;
        for (int i = 0; i < slots.arraySize; i++)
        {
            int m = slots.GetArrayElementAtIndex(i).FindPropertyRelative("Mode").enumValueIndex;
            if (m == Mine) mine = i; else if (m == Haul) haul = i; else if (m == Command) command = i;
        }

        if (mine >= 0 && command < 0)
        {
            var slot = slots.GetArrayElementAtIndex(mine);
            slot.FindPropertyRelative("Mode").enumValueIndex = Command;
            if (slot.FindPropertyRelative("Button").objectReferenceValue is Button btn)
            {
                foreach (var t in btn.GetComponentsInChildren<TMP_Text>(true)) { Undo.RecordObject(t, "명령"); t.text = "명령"; }
                foreach (var t in btn.GetComponentsInChildren<Text>(true))     { Undo.RecordObject(t, "명령"); t.text = "명령"; }
                Undo.RecordObject(btn.gameObject, "명령");
                btn.gameObject.name = "Btn_Command";
            }
            log.Add("`채굴` -> `명령`");
        }

        if (haul >= 0)
        {
            if (slots.GetArrayElementAtIndex(haul).FindPropertyRelative("Button").objectReferenceValue is Button btn)
                Undo.DestroyObjectImmediate(btn.gameObject);
            slots.DeleteArrayElementAtIndex(haul);
            log.Add("`운반` 지움");
        }
        so.ApplyModifiedProperties();

        // 남은 칸들의 틈을 닫는다.
        var items = new List<RectTransform>();
        for (int i = 0; i < slots.arraySize; i++)
            if (slots.GetArrayElementAtIndex(i).FindPropertyRelative("Button").objectReferenceValue is Button b)
                items.Add((RectTransform)b.transform);

        float closed = CloseGaps(items);
        if (closed > 0.5f) log.Add($"모드 바 틈 {closed:0}px 닫음");
    }

    // 칸들이 늘어선 축을 자리에서 재고, 한 칸 보폭보다 넓은 틈을 당겨 닫는다. 판(칸들의 부모)이 그 축에서
    // 점에 앵커돼 있으면 닫은 만큼 줄인다 — **첫 칸 쪽 모서리는 그대로.** 돌려주는 값은 닫은 길이.
    static float CloseGaps(List<RectTransform> items)
    {
        if (items.Count < 2) return 0f;
        var parent = items[0].parent as RectTransform;
        if (parent == null || parent.GetComponent<HorizontalOrVerticalLayoutGroup>() != null) return 0f;

        int axis = Spread(items, 1) > Spread(items, 0) ? 1 : 0;
        items.Sort((a, b) => a.anchoredPosition[axis].CompareTo(b.anchoredPosition[axis]));

        float step = float.MaxValue;
        for (int i = 1; i < items.Count; i++)
        {
            float d = items[i].anchoredPosition[axis] - items[i - 1].anchoredPosition[axis];
            if (d > 0.5f && d < step) step = d;
        }
        if (step == float.MaxValue) return 0f;

        float total = 0f;
        for (int i = 1; i < items.Count; i++)
        {
            float d = items[i].anchoredPosition[axis] - items[i - 1].anchoredPosition[axis];
            if (d <= step * 1.5f) continue;
            float shift = d - step;
            for (int j = i; j < items.Count; j++)
            {
                Undo.RecordObject(items[j], "명령");
                var p = items[j].anchoredPosition; p[axis] -= shift; items[j].anchoredPosition = p;
            }
            total += shift;
        }

        if (total > 0.5f) Resize(parent, axis, -total);
        return total;
    }

    static float Spread(List<RectTransform> items, int axis)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var r in items) { lo = Mathf.Min(lo, r.anchoredPosition[axis]); hi = Mathf.Max(hi, r.anchoredPosition[axis]); }
        return hi - lo;
    }

    // 판을 한 축으로 늘이거나 줄인다 — **작은 쪽 모서리(첫 칸 쪽)를 그대로 두고, 자식은 제자리에 남긴다.**
    // 늘어나 있는 판(앵커가 벌어진 축)은 크기를 화면이 정하므로 안 건드린다.
    static void Resize(RectTransform panel, int axis, float delta)
    {
        if (!Mathf.Approximately(panel.anchorMin[axis], panel.anchorMax[axis])) return;

        var kids = new List<(RectTransform t, Vector3 pos)>();
        foreach (RectTransform c in panel) kids.Add((c, c.position));

        var corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        float minBefore = corners[0][axis];

        Undo.RecordObject(panel, "명령");
        var size = panel.sizeDelta; size[axis] += delta; panel.sizeDelta = size;

        panel.GetWorldCorners(corners);
        var pos = panel.position; pos[axis] += minBefore - corners[0][axis]; panel.position = pos;

        foreach (var (t, p) in kids) { Undo.RecordObject(t, "명령"); t.position = p; }
    }

    // ── 2. 도구 바 — 모자란 칸을 세운다 ─────────────────────────────────────
    //
    // **칸은 목록이다**(2026-09-25 — 도구가 여섯이 되며 서수 필드를 넘었다). 옛 필드 다섯은
    // `ModeToolBarUI.AbsorbLegacy`가 목록으로 옮기고(런타임과 **같은 함수**다), 모자란 만큼
    // *"마지막 칸 다음에 하나"*를 되풀이한다. 무엇이 어느 칸인지는 런타임이 순서로 정한다.
    static void ToolBar(List<string> log)
    {
        var bar = Object.FindFirstObjectByType<ModeToolBarUI>(FindObjectsInactive.Include);
        if (bar == null) { log.Add("도구 바 없음"); return; }

        Undo.RecordObject(bar, "명령");
        if (bar.AbsorbLegacy()) { EditorUtility.SetDirty(bar); log.Add("도구 바 칸을 목록으로 옮김"); }

        var so    = new SerializedObject(bar);
        var list  = so.FindProperty("tiles");

        for (int i = list.arraySize - 1; i >= 0; i--)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == null) list.DeleteArrayElementAtIndex(i);

        if (list.arraySize == 0) { so.ApplyModifiedProperties(); log.Add("도구 바에 칸이 하나도 없다"); return; }

        while (list.arraySize < ModeToolBarUI.CommandTiles)
        {
            if (!AppendTile(list, $"Tile_{list.arraySize + 1}", out var added)) { log.Add("도구 바 칸을 못 세웠다"); break; }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = added;
            log.Add($"도구 바에 `{added.name}` 칸 세움");
        }
        so.ApplyModifiedProperties();
    }

    // **목록의 마지막 칸 다음에 하나를 복제해 놓는다.** 보폭은 마지막 두 칸 사이에서
    // 재고(레이아웃 묶음이면 순서만 맞춘다), 판이 점에 앵커돼 있으면 그만큼 늘인다.
    static bool AppendTile(SerializedProperty list, string goName, out BuildingItemUI added)
    {
        added = null;
        int n = list.arraySize;
        if (n == 0) return false;

        var last   = (RectTransform)((Component)list.GetArrayElementAtIndex(n - 1).objectReferenceValue).transform;
        var parent = last.parent as RectTransform;

        Vector2 stepVec = n >= 2
            ? last.anchoredPosition - ((RectTransform)((Component)list.GetArrayElementAtIndex(n - 2).objectReferenceValue).transform).anchoredPosition
            : new Vector2(last.rect.width, 0f);

        bool grouped = parent != null && parent.GetComponent<HorizontalOrVerticalLayoutGroup>() != null;
        if (!grouped && parent != null)
        {
            int axis = Mathf.Abs(stepVec.y) > Mathf.Abs(stepVec.x) ? 1 : 0;
            Resize(parent, axis, Mathf.Abs(stepVec[axis]));
        }

        var go = Object.Instantiate(last.gameObject, last.parent);
        Undo.RegisterCreatedObjectUndo(go, "명령");
        go.name = goName;
        go.transform.SetSiblingIndex(last.transform.GetSiblingIndex() + 1);
        if (!grouped) ((RectTransform)go.transform).anchoredPosition = last.anchoredPosition + stepVec;
        if (grouped && parent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parent);

        added = go.GetComponent<BuildingItemUI>();
        return added != null;
    }

    // ── 3½. 모드 바 순서 — 키보드 줄과 같게 ─────────────────────────────────
    //
    // (2026-09-28, 사용자: *"명령C밭V로 하고 순서도 그렇게 바꿔줘"*.) 키가 `C` · `V` · `B`로 한 줄에
    // 붙어 있어 **버튼도 그 순서로 선다** — 명령 · 밭 구역 · 건설. 목록에 없는 모드는 뒤에 원래 순서대로.
    // **목록 순서가 곧 화면 순서다** — 바로 다음 `BottomCenter`가 목록 순서대로 가로로 늘어놓는다.
    static readonly int[] KeyRow = { Command, 3 /* FarmZone */, 0 /* Build */, Posts, Caravan, Colony, Factions, Map };

    static void ModeOrder(List<string> log)
    {
        var bar = Object.FindFirstObjectByType<ModeBarUI>(FindObjectsInactive.Include);
        if (bar == null) return;

        var so    = new SerializedObject(bar);
        var slots = so.FindProperty("slots");

        int Rank(int i)
        {
            int m = slots.GetArrayElementAtIndex(i).FindPropertyRelative("Mode").enumValueIndex;
            int r = System.Array.IndexOf(KeyRow, m);
            return r >= 0 ? r : KeyRow.Length + i;
        }

        // 삽입 정렬 — `MoveArrayElement`로 한 칸씩 옮긴다(배선은 원소째 따라간다).
        bool moved = false;
        for (int i = 1; i < slots.arraySize; i++)
            for (int j = i; j > 0 && Rank(j) < Rank(j - 1); j--)
            {
                slots.MoveArrayElement(j, j - 1);
                moved = true;
            }

        if (!moved) return;
        so.ApplyModifiedProperties();
        log.Add("모드 바 순서: 명령 · 구역 · 건설 · 지휘소 · 상단 · 무리 · 세력 · 지도");
    }

    // ── 6. 지휘소 · 상단 ─────────────────────────────────────────────────────
    //
    // 둘은 모드가 아니라 창이지만 **모드 바 끝에 같은 모양으로** 선다 — 칸은 이미 있는 모드 칸을 복제한다(크기·색·글꼴이
    // 그대로 따라온다). 이름과 글자는 `ModeLabels`가, 자리는 `BottomCenter`가 이어서 맞춘다. 거점 바에 붙어 있던 옛 두
    // 버튼(`CommandButton`·`CaravanButton`)은 지운다 — `OutpostBarUI`에서 그 필드가 빠졌다.
    static void WindowButtons(List<string> log)
    {
        var bar = Object.FindFirstObjectByType<ModeBarUI>(FindObjectsInactive.Include);
        if (bar == null) return;

        var so    = new SerializedObject(bar);
        var slots = so.FindProperty("slots");
        var have = new HashSet<int>();
        Button sample = null;
        for (int i = 0; i < slots.arraySize; i++)
        {
            var s = slots.GetArrayElementAtIndex(i);
            have.Add(s.FindPropertyRelative("Mode").enumValueIndex);
            if (s.FindPropertyRelative("Button").objectReferenceValue is Button b) sample = b;
        }

        void Add(int mode, string goName)
        {
            if (sample == null) return;
            var go = Object.Instantiate(sample.gameObject, sample.transform.parent);
            go.name = goName;
            Undo.RegisterCreatedObjectUndo(go, "모드 바");
            var btn = go.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            int at = slots.arraySize;
            slots.InsertArrayElementAtIndex(at);
            var el = slots.GetArrayElementAtIndex(at);
            el.FindPropertyRelative("Mode").enumValueIndex = mode;
            el.FindPropertyRelative("Button").objectReferenceValue = btn;
        }
        foreach (var (mode, goName, label) in new[] { (Posts, "Btn_Posts", "지휘소"), (Caravan, "Btn_Caravan", "상단"),
                                                     (Colony, "Btn_Colony", "무리"), (Factions, "Btn_Factions", "세력"), (Map, "Btn_Map", "지도") })
            if (!have.Contains(mode)) { Add(mode, goName); log.Add($"모드 바에 `{label}` 칸"); }
        so.ApplyModifiedProperties();

        // 거점 바의 옛 두 버튼.
        var outpost = Object.FindFirstObjectByType<OutpostBarUI>(FindObjectsInactive.Include);
        if (outpost != null)
            foreach (var n in new[] { "CommandButton", "CaravanButton" })
            {
                var old = FindDeep(outpost.transform, n);
                if (old == null) continue;
                Undo.DestroyObjectImmediate(old.gameObject);
                log.Add($"거점 바 `{n}` 지움");
            }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }

    // ── 4. 하단 가운데 ───────────────────────────────────────────────────────

    const float Margin = 12f, Pad = 6f, Gap = 8f;

    static void BottomCenter(List<string> log)
    {
        var mode = Object.FindFirstObjectByType<ModeBarUI>(FindObjectsInactive.Include);
        float top = 0f;
        if (mode != null)
        {
            var so = new SerializedObject(mode);
            var slots = so.FindProperty("slots");
            var items = new List<RectTransform>();
            for (int i = 0; i < slots.arraySize; i++)
                if (slots.GetArrayElementAtIndex(i).FindPropertyRelative("Button").objectReferenceValue is Button b)
                    items.Add((RectTransform)b.transform);
            if (Row(items, Margin, out top)) log.Add("모드 바 하단 가운데");
        }

        var tool = Object.FindFirstObjectByType<ModeToolBarUI>(FindObjectsInactive.Include);
        if (tool != null && mode != null && Dock(tool, mode, top, log)) log.Add("도구 바를 모드 바 위에 붙임");
        if (mode != null && RaiseBuildBand(mode)) log.Add("건설 띠를 모드 바 위로 올림");
        if (mode != null && LiftInspect(mode, tool)) log.Add("살펴보기 창을 바 위로 올림");
    }

    // ── 7. 살펴보기 창도 바 위에 선다 (2026-10-23) ──────────────────────────
    //
    // 사용자: *"이거 밑에 UI 겹치는 건 어케할까"*(스크린샷 — 왼쪽 아래 살펴보기 창 둘이 모드 바·도구 줄의 왼쪽 끝을
    // 덮었다). 바는 가운데에 두고 **창 둘(`PawnSheet_Panel` · `PawnHealth_Panel`)의 바닥을 도구 줄 윗변 + 칸 간격에**
    // 맞춘다(사용자가 고른 안). 도구 줄까지 넘는 이유: 명령의 칸 일곱이 펼쳐지면 그 줄도 창 밑으로 들어간다.
    //
    // **올리기만 한다** — 손으로 더 위에 옮겨 둔 창은 그대로 둔다. 창 자리는 `Create Inspect Sheet`를 다시 돌려도
    // 물려받으므로 한 번 올리면 남는다.
    static bool LiftInspect(ModeBarUI mode, ModeToolBarUI tool)
    {
        var insp = Object.FindFirstObjectByType<InspectPanelUI>(FindObjectsInactive.Include);
        if (insp == null) return false;

        var slots = new SerializedObject(mode).FindProperty("slots");
        Button sample = null;
        for (int i = 0; i < slots.arraySize && sample == null; i++)
            sample = slots.GetArrayElementAtIndex(i).FindPropertyRelative("Button").objectReferenceValue as Button;
        var modePanel = sample != null ? sample.transform.parent as RectTransform : null;
        if (modePanel == null) return false;

        var c = new Vector3[4];
        modePanel.GetWorldCorners(c);
        float top = c[1].y;
        float scale = modePanel.lossyScale.y;

        if (tool != null)
            foreach (var t in tool.Tiles)
                if (t != null && t.transform.parent is RectTransform toolPanel)
                {
                    toolPanel.GetWorldCorners(c);
                    top = Mathf.Max(top, c[1].y);
                    break;
                }

        float target = top + Gap * scale;
        bool moved = false;
        foreach (var name in new[] { "PawnSheet_Panel", "PawnHealth_Panel" })
        {
            if (!(insp.transform.Find(name) is RectTransform rt)) continue;
            rt.GetWorldCorners(c);
            if (c[0].y >= target - 0.5f * scale) continue;
            if (ShiftBottomTo(rt, target, c)) moved = true;
        }
        return moved;
    }

    // ── 6. 건설 띠도 모드 바 위에 선다 (2026-09-29) ─────────────────────────
    //
    // 사용자: *"기니깐 밑에가 좀 가리네"*(스크린샷 — 다리 스물일곱이 두 줄을 채우자 **아랫줄 타일을 모드 바가 덮었다**).
    // 건설 띠는 화면 바닥에 붙어 있고 모드 바도 바닥 가운데라 **처음부터 겹쳐 있었는데**, 타일이 적을 땐 가운데까지
    // 안 닿아 안 보였다. 띠의 바닥을 **모드 바 윗변 + 칸 간격**에 맞춘다 — 갈래 판은 띠의 왼쪽 아래에 걸려 있어
    // 같이 올라간다. **세계 좌표로 재서 옮긴다** — 둘은 부모가 달라 앵커 값끼리는 못 비교한다. 몇 번 돌려도 같다.
    static bool RaiseBuildBand(ModeBarUI mode)
    {
        var build = Object.FindFirstObjectByType<BuildModeUI>(FindObjectsInactive.Include);
        if (build == null) return false;
        var band = (new SerializedObject(build).FindProperty("panel").objectReferenceValue as GameObject)?.transform as RectTransform;
        if (band == null) return false;

        var slots = new SerializedObject(mode).FindProperty("slots");
        Button sample = null;
        for (int i = 0; i < slots.arraySize && sample == null; i++)
            sample = slots.GetArrayElementAtIndex(i).FindPropertyRelative("Button").objectReferenceValue as Button;
        var modePanel = sample != null ? sample.transform.parent as RectTransform : null;
        if (modePanel == null) return false;

        var c = new Vector3[4];
        modePanel.GetWorldCorners(c);
        float modeTop = c[1].y;
        band.GetWorldCorners(c);
        float bandBottom = c[0].y;

        modePanel.GetWorldCorners(c);
        float modeBottom = c[0].y;

        float scale = Mathf.Max(1e-4f, band.lossyScale.y);
        float delta = (modeTop + Gap * scale - bandBottom) / scale;   // 띠의 부모 단위
        bool moved = false;
        if (Mathf.Abs(delta) >= 0.5f)
        {
            Undo.RecordObject(band, "건설 띠");
            band.anchoredPosition += new Vector2(0f, delta);
            EditorUtility.SetDirty(band);
            moved = true;
        }

        // **갈래 판은 바닥에 남는다**(같은 날, 사용자: *"카테고리가 있는 쪽은 전처럼 밑에 붙어 있어도 될 거 같아"*).
        // 판은 띠의 왼쪽 끝이라 모드 바(가운데)와 안 겹친다 — 띠와 같이 올라가면 아래에 빈 자리만 남는다.
        // **모드 바와 같은 바닥선**에 맞춘다(띠의 자식이지만 레이아웃을 안 탄다 — `ignoreLayout`).
        //
        // **옮기는 것은 배경 판(`CategorySection`)이다** — 버튼 칸(`CategoryGrid`, `categoryContainer`)은 그 자식이다.
        // 처음엔 버튼 칸만 옮겨서 **배경이 띠와 같이 위에 남고 버튼만 내려왔다**(사용자: *"뒤에 배경은 왜 안 옮겼어"*).
        // 판을 바닥선에 맞춘 뒤 **버튼 칸을 판 안에 다시 앉힌다** — 아래 여백은 왼쪽 여백과 같게(판이 사방 같은 여백으로
        // 섰다). 그러면 그 한 번의 실수로 어긋난 자리도 여기서 돌아온다.
        var grid = new SerializedObject(build).FindProperty("categoryContainer").objectReferenceValue as RectTransform;
        if (grid != null)
        {
            var section = grid.parent as RectTransform;
            if (section == null || section == band) section = grid;

            if (ShiftBottomTo(section, modeBottom, c)) moved = true;

            if (section != grid)
            {
                section.GetWorldCorners(c);
                float secLeft = c[0].x, secBottom = c[0].y;
                grid.GetWorldCorners(c);
                float inset = Mathf.Max(0f, c[0].x - secLeft);
                if (ShiftBottomTo(grid, secBottom + inset, c)) moved = true;
            }
        }
        return moved;
    }

    // 그 판의 바닥(세계 y)을 `worldY`에 맞춘다. 옮겼으면 true.
    static bool ShiftBottomTo(RectTransform rt, float worldY, Vector3[] c)
    {
        rt.GetWorldCorners(c);
        float d = (worldY - c[0].y) / Mathf.Max(1e-4f, rt.lossyScale.y);
        if (Mathf.Abs(d) < 0.5f) return false;
        Undo.RecordObject(rt, "갈래 판");
        rt.anchoredPosition += new Vector2(0f, d);
        EditorUtility.SetDirty(rt);
        return true;
    }

    // ── 5. 도구 줄은 모드 바 위에 붙은 한 덩어리 (2026-09-28) ────────────────
    //
    // 사용자: *"위치가 좀 애매한데"*. 도구 칸이 건설 목록 타일을 그대로 써서 **빈 아이콘 사각형**이었고
    // 모드 바와 틈을 두고 따로 떠 있어 한 묶음으로 안 읽혔다. 그래서:
    //
    //   · **칸을 모드 버튼과 같은 납작한 글자 버튼으로** — 아이콘을 끄고 이름을 칸 가득 가운데에,
    //     크기·글자 크기·바탕색은 **모드 버튼에서 잰다**(상수로 적으면 씬을 손본 날 어긋난다).
    //   · **모드 바 바로 위에 칸 간격만큼 띄워 얹고 바탕을 같은 색으로** — 모드를 고르면 그 위에 도구가 펼쳐진다.
    //   · **판이 켜진 칸만큼 줄어든다**(가로 묶음 + 크기 맞춤) — 명령은 일곱, 밭은 셋이라 고정 폭이면
    //     밭일 때 오른쪽에 빈 칸 넷이 남아 가운데가 어긋난다. 꺼진 칸은 묶음이 안 센다.
    static bool Dock(ModeToolBarUI tool, ModeBarUI mode, float top, List<string> log)
    {
        var tiles = new List<BuildingItemUI>();
        foreach (var t in tool.Tiles) if (t != null) tiles.Add(t);
        if (tiles.Count == 0) return false;

        // 모드 버튼 하나를 본으로 잰다.
        var modeSo = new SerializedObject(mode);
        var slots  = modeSo.FindProperty("slots");
        Button sample = null;
        for (int i = 0; i < slots.arraySize && sample == null; i++)
            sample = slots.GetArrayElementAtIndex(i).FindPropertyRelative("Button").objectReferenceValue as Button;
        if (sample == null) { log.Add("모드 버튼이 없어 도구 칸을 못 맞춤"); return false; }

        var sampleRt   = (RectTransform)sample.transform;
        var sampleText = sample.GetComponentInChildren<TMP_Text>(true);
        var idle       = modeSo.FindProperty("idle").colorValue;
        var modePanel  = sampleRt.parent as RectTransform;
        var modeBg     = modePanel != null ? modePanel.GetComponent<Image>() : null;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/NanumGothic SDF.asset");

        // ── 칸 ──
        foreach (var tile in tiles)
        {
            var rt = (RectTransform)tile.transform;

            // **칸이 제 크기를 스스로 정하지 못하게 한다.** 건설 목록 타일이라 아이콘과 이름을 쌓는
            // 묶음·크기 맞춤이 칸 자신에 붙어 있을 수 있고, 그러면 아래에서 준 크기를 **다음 레이아웃
            // 계산이 도로 덮는다**(2026-09-28 — 두 번 돌려도 칸이 그대로 컸다).
            foreach (var c in tile.GetComponents<Component>())
                if (c is LayoutGroup || c is ContentSizeFitter || c is AspectRatioFitter)
                    Undo.DestroyObjectImmediate(c);
            var le = tile.GetComponent<LayoutElement>();
            if (le != null)
            {
                Undo.RecordObject(le, "도구 바");
                le.minWidth  = le.preferredWidth  = sampleRt.rect.width;
                le.minHeight = le.preferredHeight = sampleRt.rect.height;
                le.flexibleWidth = le.flexibleHeight = -1f;
            }

            Undo.RecordObject(rt, "도구 바");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sampleRt.rect.size;

            var so = new SerializedObject(tile);

            if (so.FindProperty("icon").objectReferenceValue is Image icon && icon.gameObject.activeSelf)
            { Undo.RecordObject(icon.gameObject, "도구 바"); icon.gameObject.SetActive(false); }

            if (so.FindProperty("background").objectReferenceValue is Image bg)
            { Undo.RecordObject(bg, "도구 바"); bg.color = idle; bg.enabled = true; }

            if (so.FindProperty("nameLabel").objectReferenceValue is TextMeshProUGUI label)
            {
                var lr = label.rectTransform;
                Undo.RecordObject(lr, "도구 바");
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(4f, 0f); lr.offsetMax = new Vector2(-4f, 0f);

                Undo.RecordObject(label, "도구 바");
                label.alignment          = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode       = TextOverflowModes.Ellipsis;
                if (sampleText != null) { label.fontSize = sampleText.fontSize; label.enableAutoSizing = sampleText.enableAutoSizing; }
                if (font != null) label.font = font;
            }
        }

        // ── 판 ── 칸들의 부모(창 규약의 `_Panel`)
        var panel = (RectTransform)tiles[0].transform.parent;
        Undo.RecordObject(panel, "도구 바");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot     = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, top);   // 아래에서 모드 바 기준으로 다시 놓는다

        var img = panel.GetComponent<Image>();
        if (img == null) img = Undo.AddComponent<Image>(panel.gameObject);
        Undo.RecordObject(img, "도구 바");
        img.enabled = true;
        img.color   = modeBg != null ? modeBg.color : new Color(0.05f, 0.05f, 0.05f, 0.85f);

        // **묶음은 하나만 붙는다 — 격자(`GridLayoutGroup`)까지 걷는다.** 처음엔 가로·세로 묶음만 봤는데
        // 도구 칸은 건설 목록처럼 **격자에 담겨 있었다**: 격자가 칸 크기를 강제로 정해 크기가 안 바뀌었고,
        // 가로 묶음을 더하려다 실패해 판이 반쯤 고쳐진 채 남았다(2026-09-28 스크린샷).
        foreach (var g in panel.GetComponents<LayoutGroup>())
            if (!(g is HorizontalLayoutGroup)) Undo.DestroyObjectImmediate(g);
        var row = panel.GetComponent<HorizontalLayoutGroup>();   // `??` 금지 — 에디터의 가짜 null
        if (row == null) row = Undo.AddComponent<HorizontalLayoutGroup>(panel.gameObject);
        Undo.RecordObject(row, "도구 바");
        int pad = Mathf.RoundToInt(Pad);
        row.padding = new RectOffset(pad, pad, pad, pad);
        row.spacing = Gap;
        row.childAlignment         = TextAnchor.MiddleCenter;
        row.childControlWidth      = row.childControlHeight      = false;
        row.childForceExpandWidth  = row.childForceExpandHeight  = false;

        var fit = panel.GetComponent<ContentSizeFitter>();
        if (fit == null) fit = Undo.AddComponent<ContentSizeFitter>(panel.gameObject);
        Undo.RecordObject(fit, "도구 바");
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

        // **자리는 모드 바에서 잰다 — 뿌리의 앵커를 믿지 않는다.** 도구 바의 뿌리(`ModeToolBar_UI`)는
        // 건설 창 안에 들어 있어(`docs/editor_creators.md`) 화면 전체가 아닐 수 있고, 그러면 `(0.5, 0)` 앵커가
        // 화면 가운데가 아니다(두 번째 스크린샷: 판이 오른쪽으로 80px 밀렸다). 모드 바 윗변 가운데의
        // **월드 점**에 판의 피벗을 놓으면 뿌리가 어떻게 생겼든 맞는다.
        if (modePanel != null)
        {
            var c = new Vector3[4];
            modePanel.GetWorldCorners(c);                    // 0 좌하 · 1 좌상 · 2 우상 · 3 우하
            Undo.RecordObject(panel, "도구 바");
            panel.position = (c[1] + c[2]) * 0.5f;

            // **틈은 칸 사이와 같은 간격** — 딱 붙이니 두 줄이 한 덩이로 뭉쳐 보였다(2026-09-28, 사용자:
            // *"너무 붙어 있는 거 같아"*). 월드에 놓은 뒤 로컬 단위로 올린다(캔버스 배율을 안 탄다).
            panel.anchoredPosition += new Vector2(0f, Gap);
        }

        // **무엇이 어떻게 섰는지 적는다** — 씬이 바이너리라 밖에서 볼 창이 이 로그뿐이다.
        Debug.Log("[도구 바] 고친 뒤\n" + Dump(panel) + "\n" + Dump(modePanel));
        return true;
    }

    // 판과 자식 한 겹: 이름 · 켜짐 · 앵커 · 피벗 · 자리 · 크기 · 붙은 레이아웃 부품.
    static string Dump(RectTransform rt)
    {
        if (rt == null) return "(없음)";

        string Line(RectTransform r) =>
            $"{r.name}{(r.gameObject.activeSelf ? "" : "(꺼짐)")} 앵커 {r.anchorMin}-{r.anchorMax} 피벗 {r.pivot} " +
            $"자리 {r.anchoredPosition} 크기 {r.rect.size} [{string.Join(",", Parts(r))}]";

        IEnumerable<string> Parts(RectTransform r)
        {
            foreach (var c in r.GetComponents<Component>())
                if (c is LayoutGroup || c is ContentSizeFitter || c is LayoutElement || c is Image)
                    yield return c.GetType().Name;
        }

        var sb = new System.Text.StringBuilder();
        var path = new List<string>();
        for (var t = rt.parent; t != null; t = t.parent) path.Insert(0, t.name);
        sb.AppendLine($"  {string.Join("/", path)}/");
        sb.AppendLine("  " + Line(rt));
        foreach (RectTransform c in rt) sb.AppendLine("    " + Line(c));
        for (var p = rt.parent as RectTransform; p != null; p = p.parent as RectTransform)
            sb.AppendLine("  위: " + Line(p));   // 뿌리가 화면 전체인가 — 가운데가 어긋나면 여기서 보인다
        return sb.ToString();
    }

    // ── 모드 버튼 이름에 키를 적는다 ─────────────────────────────────────────
    //
    // `건설 (B)`에만 키가 있고 명령·밭 구역에는 없었다(사용자가 본 스크린샷). 키는 코드가 듣는 것 —
    // `CommandMode`(C) · `FarmZoneDesignator`(V) · `BuildModeManager`(B).
    static void ModeLabels(List<string> log)
    {
        var bar = Object.FindFirstObjectByType<ModeBarUI>(FindObjectsInactive.Include);
        if (bar == null) return;

        var slots = new SerializedObject(bar).FindProperty("slots");
        for (int i = 0; i < slots.arraySize; i++)
        {
            var slot = slots.GetArrayElementAtIndex(i);
            string name = slot.FindPropertyRelative("Mode").enumValueIndex switch
            {
                Command => "명령 (C)",
                3       => "구역 (V)",   // 밭 + 저장(2026-10-06)
                0       => "건설 (B)",
                Posts   => "지휘소 (H)",
                Caravan => "상단 (T)",
                Colony   => "무리 (P)",
                Factions => "세력 (L)",
                Map      => "지도 (M)",
                _       => null,
            };
            if (name == null) continue;
            if (!(slot.FindPropertyRelative("Button").objectReferenceValue is Button btn)) continue;

            foreach (var t in btn.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.text == name) continue;
                Undo.RecordObject(t, "모드 바");
                t.text = name;
                log.Add($"모드 버튼 `{name}`");
            }
        }
    }

    // 칸들을 **부모(판) 안에 가로 한 줄로** 늘어놓고, 판을 화면 아래 가운데 `bottom` 높이에 앵커한다.
    // 판이 캔버스 바로 아래가 아니면(판의 부모가 전체 스트레치 뿌리 — 창 규약) 그 뿌리 기준이다.
    // 돌려주는 `topY`는 판의 윗변(아래에서) — 다음 줄을 그 위에 얹는다.
    static bool Row(List<RectTransform> items, float bottom, out float topY)
    {
        topY = bottom;
        if (items.Count == 0) return false;
        var panel = items[0].parent as RectTransform;
        if (panel == null) return false;

        var group = panel.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (group != null && !(group is HorizontalLayoutGroup))
        { Undo.DestroyObjectImmediate(group); group = null; }   // 세로 묶음이면 가로로 다시 놓는다

        float w = 0f, h = 0f;
        foreach (var r in items) { w = Mathf.Max(w, r.rect.width); h = Mathf.Max(h, r.rect.height); }

        Undo.RecordObject(panel, "명령");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot     = new Vector2(0.5f, 0f);
        panel.sizeDelta = new Vector2(items.Count * w + (items.Count - 1) * Gap + Pad * 2f, h + Pad * 2f);
        panel.anchoredPosition = new Vector2(0f, bottom);

        if (group == null)
            for (int i = 0; i < items.Count; i++)
            {
                var r = items[i];
                Undo.RecordObject(r, "명령");
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot     = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(Pad + i * (w + Gap), 0f);
            }
        else LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

        topY = bottom + panel.sizeDelta.y;
        return true;
    }

    // ── 3. 스탯 창 키 C -> U ─────────────────────────────────────────────────

    static void StatsKey(List<string> log)
    {
        var ui = Object.FindFirstObjectByType<UnitStatsUI>(FindObjectsInactive.Include);
        if (ui == null) return;
        var so  = new SerializedObject(ui);
        var key = so.FindProperty("toggleKey");
        int u   = (int)UnityEngine.InputSystem.Key.U;
        if (key == null || key.intValue == u) return;
        key.intValue = u;
        so.ApplyModifiedProperties();
        log.Add("스탯 창 키 U");
    }
}
