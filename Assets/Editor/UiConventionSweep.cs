using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// DungeonColony → Sweep UI Convention
//
// 씬의 패널들을 훑어 세 가지를 규약에 맞춘다:
//   1) 제목 바 이름을 **TitleBar**로 통일 (기존: TitleBar / TopBar / Header 혼재)
//   2) 제목 바에 **DraggablePanel** 부착 + target = 그 패널 (비어 있으면 바 자신이 움직여 버린다)
//   3) 제목 바 안의 닫기 버튼 이름을 **CloseButton**으로 통일 + **CloseButton** 스크립트 부착 + target = 패널
//
// **제목 바의 판정 기준은 깊이다** — `Canvas > *_UI > *Panel > 바` 구조에서 캔버스 기준 깊이 3.
// 이름만 보면 `ItemInfoPanel/Header`(툴팁 안의 섹션 헤더, 깊이 2)까지 잡아 이름을 바꿔 버린다.
// 그건 제목 바가 아니라 아이콘+제목 컬럼을 담는 줄이므로 건드리면 안 된다.
//
// 이름으로 오브젝트를 찾는 코드는 없음을 확인하고 진행했다(`transform.Find("TopBar")` 류 없음).
// 여러 번 실행해도 안전하고, 바꾼 것만 로그에 남긴다. 다 쓰면 지운다(생성기의 수명).
public static class UiConventionSweep
{
    const string BarName   = "TitleBar";
    const string CloseName = "CloseButton";

    // 제목 바로 인정하는 기존 이름들.
    static readonly HashSet<string> BarAliases   = new() { "TitleBar", "TopBar", "Header" };
    // 닫기 버튼으로 인정하는 기존 이름들.
    static readonly HashSet<string> CloseAliases = new() { "CloseButton", "CloseBtn", "Close" };

    [MenuItem("DungeonColony/Sweep UI Convention")]
    static void Sweep()
    {
        var canvas = FindUiCanvas();
        if (canvas == null) { Debug.LogError("[UiSweep] 씬에 Canvas가 없습니다."); return; }

        var log = new List<string>();
        int barsTouched = 0, dragsAdded = 0, closesTouched = 0;

        // Canvas > UI루트 > 패널 > 바  (깊이 3)
        foreach (Transform uiRoot in canvas.transform)
        foreach (Transform panel in uiRoot)
        foreach (Transform bar in panel)
        {
            if (!BarAliases.Contains(bar.name)) continue;

            string path = $"{uiRoot.name}/{panel.name}";

            // 1) 바 이름 통일
            if (bar.name != BarName)
            {
                log.Add($"  이름  {path}/{bar.name} → {BarName}");
                Undo.RecordObject(bar.gameObject, "Rename bar");
                bar.name = BarName;
                barsTouched++;
            }

            // 2) DraggablePanel — target을 비워두면 바 자신이 움직인다(패널이 아니라).
            if (EnsureDraggable(bar, panel, path, log)) dragsAdded++;

            // 3) 바 안의 닫기 버튼
            foreach (Transform child in bar)
            {
                if (!CloseAliases.Contains(child.name)) continue;
                if (EnsureClose(child, panel, path, log)) closesTouched++;
            }
        }

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        var sb = new StringBuilder();
        sb.AppendLine($"[UiSweep] 완료 — 바 이름 {barsTouched}건 / DraggablePanel {dragsAdded}건 / 닫기 버튼 {closesTouched}건 수정.");
        sb.Append(log.Count == 0 ? "  (바꿀 게 없었습니다 — 이미 규약대로입니다)" : string.Join("\n", log));
        sb.Append("\n  ※ 씬 저장 필요. Undo로 한 번에 되돌릴 수 있습니다.");
        Debug.Log(sb.ToString());
    }

    // 바에 DraggablePanel을 붙이고 target을 패널로 맞춘다.
    static bool EnsureDraggable(Transform bar, Transform panel, string path, List<string> log)
    {
        var drag = bar.GetComponent<DraggablePanel>();
        bool added = false;

        if (drag == null)
        {
            drag = Undo.AddComponent<DraggablePanel>(bar.gameObject);
            log.Add($"  Drag  {path}/{bar.name} ← DraggablePanel 부착");
            added = true;
        }

        var so   = new SerializedObject(drag);
        var prop = so.FindProperty("target");
        if (prop != null && prop.objectReferenceValue != (Object)panel)
        {
            prop.objectReferenceValue = panel as RectTransform;
            so.ApplyModifiedProperties();
            if (!added) log.Add($"  Drag  {path}/{bar.name} ← target을 패널로 교정");
            added = true;
        }
        return added;
    }

    // 닫기 버튼 이름을 통일하고 CloseButton 스크립트 + target을 맞춘다.
    static bool EnsureClose(Transform btn, Transform panel, string path, List<string> log)
    {
        bool touched = false;

        if (btn.name != CloseName)
        {
            log.Add($"  이름  {path}/{btn.parent.name}/{btn.name} → {CloseName}");
            Undo.RecordObject(btn.gameObject, "Rename close button");
            btn.name = CloseName;
            touched = true;
        }

        // CloseButton은 [RequireComponent(typeof(Button))] — 버튼이 없으면 붙일 수 없다.
        if (btn.GetComponent<Button>() == null)
        {
            log.Add($"  경고  {path}/{btn.name} 에 Button이 없어 CloseButton을 붙이지 못했습니다");
            return touched;
        }

        var close = btn.GetComponent<CloseButton>();
        if (close == null)
        {
            close = Undo.AddComponent<CloseButton>(btn.gameObject);
            log.Add($"  Close {path}/{btn.name} ← CloseButton 스크립트 부착");
            touched = true;
        }

        var so   = new SerializedObject(close);
        var prop = so.FindProperty("target");
        if (prop != null && prop.objectReferenceValue == null)
        {
            prop.objectReferenceValue = panel.gameObject;
            so.ApplyModifiedProperties();
            log.Add($"  Close {path}/{btn.name} ← target을 패널로 연결");
            touched = true;
        }
        return touched;
    }

    // 월드 루트 아래 캔버스는 대상이 아니다(있다면 지도용).
    static Canvas FindUiCanvas()
    {
        var view = Object.FindFirstObjectByType<WorldMapView>();
        var mapRoot = view != null ? view.MapRoot : null;

        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None))
        {
            if (c == null) continue;
            if (mapRoot != null && c.transform.IsChildOf(mapRoot)) continue;
            return c;
        }
        return null;
    }
}
