using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **캔버스의 형제 순서를 층으로 정리한다**(2026-08-29).
//
// uGUI에서 **그리는 순서는 형제 순서**다 — 뒤에 있는 것이 위에 그려진다. 그런데 씬의 순서는
// 창을 만든 차례대로 쌓여 있어서, *"무엇이 무엇을 덮는가"*가 **저작 순서라는 우연**으로 정해진다.
// 실제로 살펴보기 창이 모드 바를 덮고 있었다.
//
// ── 세 층이다 ──────────────────────────────────────────────────────────────
//
// `WindowRaiser`가 이미 그 규약을 들고 있다(*"늘 위에 있어야 하는 것들"*). 여기서는 그것을
// **씬의 순서에도** 적용한다 — 런타임에 되올리는 것과 처음 순서가 어긋나면, 창을 한 번도
// 안 연 상태에서는 규약이 안 지켜진다.
//
//   ① 상시 HUD    — 시계 · 살펴보기 · 모드 바 · 무리 목록. **사람이 여닫는 창이 아니다.**
//                    맨 아래에 깔린다 — 창이 열리면 당연히 그 위를 덮어야 한다.
//   ② 창          — 배낭 · 창고 · 작업대 · 상점 · 대화… 사이에서의 순서는 `WindowRaiser`가
//                    클릭에 따라 계속 바꾸므로 **여기서 정할 것이 없다.**
//   ③ 늘 위       — 우클릭 창 · 툴팁 둘. 가려지면 **정작 설명을 보려고 올린 창이 그 설명을 덮는다.**
//
// ── 이름이 아니라 컴포넌트로 가른다 ────────────────────────────────────────
//
// 이름으로 찾으면 이름을 바꾸는 순간 조용히 층이 뒤바뀐다. 컴포넌트는 그 창이 **무엇인지**를
// 들고 있으므로, 새 창을 만들어도 타입만 여기 적으면 따라온다.
public static class HudOrderSetup
{
    // ① 맨 아래 — 상시 HUD. **적힌 순서대로** 깔린다(뒤가 위).
    static readonly System.Type[] Hud =
    {
        typeof(TimeHUD),
        typeof(InspectPanelUI),
        typeof(PawnRosterUI),
        typeof(ModeBarUI),
    };

    // ③ 맨 위 — 가려지면 안 되는 것. **`WindowRaiser.KeepOverlaysOnTop`과 같은 목록이어야 한다.**
    static readonly System.Type[] Overlay =
    {
        typeof(PawnContextMenuUI),
        typeof(ItemInfoUI),
        typeof(HoverTooltipUI),
    };

    [MenuItem("DungeonColony/Setup HUD Order")]
    static void Run()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) { Debug.LogError("[HUD] 씬에 Canvas가 없다."); return; }

        var root = canvas.transform;

        // **캔버스의 직속 자식만 다룬다** — 이 저장소가 "창"을 세는 단위가 그것이다
        // (`GameModeManager`가 필드/월드를 갈아 끼울 때도, `WindowRaiser`가 창을 올릴 때도).
        var children = new List<Transform>();
        for (int i = 0; i < root.childCount; i++) children.Add(root.GetChild(i));

        var hud     = Pick(children, Hud);
        var overlay = Pick(children, Overlay);

        // 나머지가 ② — 순서를 안 건드린다. **클릭이 계속 바꾸는 자리**라 여기서 정하면
        // 첫 프레임에만 맞고 그 뒤로는 뜻이 없다.
        var windows = new List<Transform>();
        foreach (var t in children)
            if (!hud.Contains(t) && !overlay.Contains(t)) windows.Add(t);

        int order = 0;
        foreach (var t in hud)     { Undo.RecordObject(t, "HUD 순서"); t.SetSiblingIndex(order++); }
        foreach (var t in windows) { Undo.RecordObject(t, "HUD 순서"); t.SetSiblingIndex(order++); }
        foreach (var t in overlay) { Undo.RecordObject(t, "HUD 순서"); t.SetSiblingIndex(order++); }

        EditorUtility.SetDirty(canvas);

        Debug.Log($"[HUD] 형제 순서를 정리했다 — 상시 HUD {hud.Count} · 창 {windows.Count} · 늘 위 {overlay.Count}.\n" +
                  $"아래 → 위: {Names(hud)} | (창들) | {Names(overlay)}\n" +
                  "**씬을 저장할 것.**");
    }

    // 적힌 타입 순서대로 고른다. **없으면 건너뛴다** — 씬마다 있는 창이 다르다.
    static List<Transform> Pick(List<Transform> pool, System.Type[] types)
    {
        var found = new List<Transform>();
        foreach (var type in types)
        {
            foreach (var t in pool)
            {
                if (found.Contains(t)) continue;
                if (t.GetComponentInChildren(type, true) == null) continue;
                found.Add(t);
                break;
            }
        }
        return found;
    }

    static string Names(List<Transform> list)
    {
        var parts = new List<string>();
        foreach (var t in list) parts.Add(t.name);
        return parts.Count == 0 ? "(없음)" : string.Join(" → ", parts);
    }
}
