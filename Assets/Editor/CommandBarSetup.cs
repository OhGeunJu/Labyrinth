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
//
// 칸 크기와 틈은 **이미 있는 칸에서 잰다** — 상수로 적으면 씬을 손본 날 어긋난다. 끝나면 씬을 저장할 것.
public static class CommandBarSetup
{
    const int Mine = 1, Haul = 2, Command = 4;   // `ModeBarUI.Mode` — 정수로 직렬화된다

    [MenuItem("DungeonColony/UI/Setup Command Bar (명령 버튼 · 취소 칸 · 스탯 U키 · 하단 가운데)")]
    static void Run()
    {
        var log = new List<string>();
        Wake<ModeBarUI>("모드 바", log);
        Wake<ModeToolBarUI>("도구 바", log);
        HoistOffPanel(log);
        ModeBar(log);
        ToolBar(log);
        StatsKey(log);
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
        if (tool != null)
        {
            var items = new List<RectTransform>();
            foreach (var c in tool.Tiles)
                if (c != null) items.Add((RectTransform)c.transform);
            if (Row(items, top + Margin * 0.5f, out _)) log.Add("도구 바 그 위 가운데");
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
