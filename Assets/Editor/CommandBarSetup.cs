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
//   2. **도구 바** — 넷째 타일(`취소`)이 없으면 셋째를 복제해 **한 칸 뒤에** 세우고 `fourthItem`에 물린다.
//      판이 점에 앵커돼 있으면 그만큼 늘린다(첫 칸 쪽 모서리는 그대로).
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
        ModeBar(log);
        ToolBar(log);
        StatsKey(log);
        BottomCenter(log);

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[명령] " + (log.Count > 0 ? string.Join(" · ", log) : "이미 다 돼 있다") + " — **씬을 저장할 것.**");
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

    // ── 2. 도구 바 — 넷째 타일(취소) ─────────────────────────────────────────

    static void ToolBar(List<string> log)
    {
        var bar = Object.FindFirstObjectByType<ModeToolBarUI>(FindObjectsInactive.Include);
        if (bar == null) { log.Add("도구 바 없음"); return; }

        var so     = new SerializedObject(bar);
        var fourth = so.FindProperty("fourthItem");
        if (fourth.objectReferenceValue != null) return;

        var first  = so.FindProperty("primaryItem").objectReferenceValue as BuildingItemUI;
        var second = so.FindProperty("extraItem").objectReferenceValue as BuildingItemUI;
        var third  = so.FindProperty("cancelItem").objectReferenceValue as BuildingItemUI;
        if (third == null) { log.Add("도구 바 셋째 타일 없음"); return; }

        var rt3    = (RectTransform)third.transform;
        var parent = rt3.parent as RectTransform;
        var prev   = (second != null ? second : first);
        Vector2 stepVec = prev != null ? rt3.anchoredPosition - ((RectTransform)prev.transform).anchoredPosition
                                       : new Vector2(rt3.rect.width, 0f);

        bool grouped = parent != null && parent.GetComponent<HorizontalOrVerticalLayoutGroup>() != null;
        if (!grouped && parent != null)
        {
            int axis = Mathf.Abs(stepVec.y) > Mathf.Abs(stepVec.x) ? 1 : 0;
            Resize(parent, axis, Mathf.Abs(stepVec[axis]));
        }

        var go = Object.Instantiate(third.gameObject, rt3.parent);
        Undo.RegisterCreatedObjectUndo(go, "명령");
        go.name = "Tile_Cancel";
        go.transform.SetSiblingIndex(third.transform.GetSiblingIndex() + 1);
        if (!grouped) ((RectTransform)go.transform).anchoredPosition = rt3.anchoredPosition + stepVec;
        if (grouped && parent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parent);

        fourth.objectReferenceValue = go.GetComponent<BuildingItemUI>();
        so.ApplyModifiedProperties();
        log.Add("도구 바에 `취소` 칸 세움");
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
            var so = new SerializedObject(tool);
            var items = new List<RectTransform>();
            foreach (var name in new[] { "primaryItem", "extraItem", "cancelItem", "fourthItem" })
                if (so.FindProperty(name).objectReferenceValue is Component c) items.Add((RectTransform)c.transform);
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
