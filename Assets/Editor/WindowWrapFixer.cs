using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

// 창 컴포넌트를 **자기 전용 오브젝트**로 옮긴다.
//
//     <기준>_UI      ← 컴포넌트가 붙은, 늘 켜져 있는 부모
//        └ <기준>_Panel   ← 보였다 사라지는 자식 (`panel` 필드)
//
// 어긋나는 꼴이 둘 있었고, 둘 다 여기서 편다:
//
//   (1) 컴포넌트가 **패널 자신에** 붙어 있다 — `ItemInfoUI`가 그랬다. 씬에 꺼진 채 저장돼
//       `Awake`가 영영 안 돌았고 `Instance`가 null이라 **툴팁이 한 번도 안 떴다.**
//       → 부모를 새로 세우고 옛 오브젝트를 그 자식으로 내린다.
//
//   (2) 컴포넌트가 **남의 오브젝트에 얹혀** 있다 — `WorldMenuUI`가 `Canvas/WorldUI`에 붙어
//       있다. 그런데 `WorldUI`는 창이 아니라 **월드 모드에서 켜지는 UI 전부를 담는 묶음**이고
//       `GameModeManager`가 이것만 반대로 켠다. 이름을 `WorldMenu_UI`로 바꿔 버리면 나중에
//       추가되는 월드 전용 UI가 "월드메뉴" 밑에 사는 꼴이 된다.
//       → 그 오브젝트는 그대로 두고 **안쪽에** 전용 부모를 세운 뒤 패널을 그리로 옮긴다.
//
// **대상은 손으로 적는다.** "이 오브젝트가 다른 역할도 하는가"는 자동으로 알 수 없다 —
// 이름이나 자식 수로는 구분되지 않는다(`WorldUI`도 자식이 하나뿐이다). 사람만 아는 사실이라
// 목록으로 두고, 이미 제 모양인 것은 건너뛴다(여러 번 실행해도 안전).
public static class WindowWrapFixer
{
    static readonly string[] Targets = { "ItemInfoUI", "WorldMenuUI" };

    [MenuItem("DungeonColony/Fix Window Structure")]
    static void Fix()
    {
        var comps = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int moved = 0, already = 0;
        Scene? scene = null;

        foreach (var c in comps)
        {
            if (c == null || Array.IndexOf(Targets, c.GetType().Name) < 0) continue;

            scene ??= c.gameObject.scene;   // 컴포넌트가 파괴되기 **전에** 잡아 둔다
            if (Wrap(c)) moved++;
            else         already++;
        }

        if (scene == null)
        {
            Debug.LogError("[WindowWrapFixer] 대상 컴포넌트를 씬에서 찾지 못했습니다.");
            return;
        }

        // **옮긴 것이 없어도 저장한다.** 앞선 실행이 옮기고 저장 직전에 터졌다면 그 결과가
        // 메모리에만 남아 있다 — 여기서 건너뛰고 저장까지 안 하면 씬을 닫을 때 날아간다.
        EditorSceneManager.MarkSceneDirty(scene.Value);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[WindowWrapFixer] 옮김 {moved} · 이미 맞음 {already} — 씬 저장 완료");
    }

    static bool Wrap(MonoBehaviour comp)
    {
        var so    = new SerializedObject(comp);
        var prop  = so.FindProperty("panel");
        var panel = prop != null ? prop.objectReferenceValue as GameObject : null;
        var host  = comp.gameObject;

        string baseName  = comp.GetType().Name.EndsWith("UI", StringComparison.Ordinal)
                         ? comp.GetType().Name.Substring(0, comp.GetType().Name.Length - 2)
                         : comp.GetType().Name;
        string wrapName  = baseName + "_UI";
        string panelName = baseName + "_Panel";

        if (panel == null)
        {
            Debug.LogError($"[WindowWrapFixer] {host.name}: panel 필드가 비어 있습니다.");
            return false;
        }

        // 이미 제 모양 — 전용 부모에 살고 패널이 따로 있다.
        if (host.name == wrapName && panel != host) { panel.name = panelName; return false; }

        // (1) 패널 자신에 붙어 있으면 **바깥에**, (2) 남의 것에 얹혀 있으면 **안쪽에** 세운다.
        bool onPanel = (panel == host);
        var  parent  = onPanel ? host.transform.parent : host.transform;
        if (parent == null)
        {
            Debug.LogError($"[WindowWrapFixer] {host.name}: 부모가 없습니다(캔버스 아래여야 함).");
            return false;
        }

        // **부모와 같은 크기로 펼친다.** 자식 패널의 `anchoredPosition`이 그대로 뜻을
        // 유지해야 한다 — 중간에 낀 부모의 rect가 다르면 창이 어긋난 자리에 뜬다.
        var wrapper = new GameObject(wrapName, typeof(RectTransform));
        wrapper.transform.SetParent(parent, false);
        if (onPanel) wrapper.transform.SetSiblingIndex(host.transform.GetSiblingIndex());

        var wrapRT = (RectTransform)wrapper.transform;
        wrapRT.anchorMin = Vector2.zero;
        wrapRT.anchorMax = Vector2.one;
        wrapRT.offsetMin = Vector2.zero;
        wrapRT.offsetMax = Vector2.zero;

        // 컴포넌트는 **복사해 붙인다** — 인스펙터로 이어 둔 참조를 그대로 들고 가야 한다.
        if (!ComponentUtility.CopyComponent(comp) || !ComponentUtility.PasteComponentAsNew(wrapper))
        {
            Debug.LogError($"[WindowWrapFixer] {host.name}: 컴포넌트 복사 실패");
            UnityEngine.Object.DestroyImmediate(wrapper);
            return false;
        }

        var moved = wrapper.GetComponent(comp.GetType()) as MonoBehaviour;
        if (moved == null)
        {
            Debug.LogError($"[WindowWrapFixer] {host.name}: 붙여넣은 컴포넌트를 못 찾았습니다.");
            UnityEngine.Object.DestroyImmediate(wrapper);
            return false;
        }

        // 옛 컴포넌트를 **먼저** 지운다 — 남겨 두면 둘이 `Instance`를 다툰다.
        UnityEngine.Object.DestroyImmediate(comp);

        var panelGO = onPanel ? host : panel;
        panelGO.transform.SetParent(wrapper.transform, false);
        panelGO.name = panelName;
        panelGO.SetActive(false);          // 보였다 사라지는 쪽은 이제 이 자식이다

        var movedSO = new SerializedObject(moved);
        movedSO.FindProperty("panel").objectReferenceValue = panelGO;
        movedSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(moved);

        Debug.Log($"[WindowWrapFixer] {comp.GetType().Name}: {wrapper.transform.parent.name}/" +
                  $"{wrapName}/{panelName} 로 정리했습니다.");
        return true;
    }
}
