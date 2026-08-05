using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;   // Scene

// 창 오브젝트 이름을 **컴포넌트에서 유도해** 통일한다.
//
//     <기준>_UI      ← 컴포넌트가 붙은, 늘 켜져 있는 부모
//     <기준>_Panel   ← 보였다 사라지는 자식 (`panel` 필드가 가리키는 것)
//
// 기준 = 컴포넌트 클래스 이름에서 꼬리 `UI`를 뗀 것.
//   `StorageUI`         → `Storage_UI` / `Storage_Panel`
//   `CraftingTableUI`   → `CraftingTable_UI` / `CraftingTable_Panel`
//   `PawnContextMenuUI` → `PawnContextMenu_UI` / `PawnContextMenu_Panel`
//
// 2026-08-04에 **클래스 이름을 씬의 짧은 이름 쪽으로 맞췄다**(`StorageBuildingUI`→`StorageUI`,
// `PawnInventoryPanel`→`InventoryUI` …). 그래서 컴포넌트에서 유도한 것이 곧 짧은 이름이다.
// 예외 둘은 반대로 씬을 늘렸다 — `Crafting_UI`→`CraftingTable_UI`(`ResearchTable_UI`와 짝),
// `Corpse_UI`→`CorpseInventory_UI`(`Inventory_UI`와 짝).
//
// **표를 손으로 적지 않는다.** 어느 루트가 어느 패널의 짝인지 이름으로 짐작하면 반드시
// 틀린다(`Storage_UI`의 짝이 `StorageBuildingPanel`이었다). `panel` 필드가 짝을 정확히
// 알고 있으므로 거기서 읽는다.
//
// 이름은 `fileID` 참조에 영향을 주지 않고, 이 프로젝트에는 창을 문자열로 찾는 코드가 없다
// (2026-08-04 전수 확인 — `transform.Find`는 전부 창 안쪽 부품용).
public static class UiObjectRenamer
{
    [MenuItem("DungeonColony/Rename UI Objects (Preview)")]
    static void Preview() => Run(false);

    [MenuItem("DungeonColony/Rename UI Objects (Apply)")]
    static void Apply() => Run(true);

    struct Plan
    {
        public MonoBehaviour Comp;
        public GameObject    Root;
        public GameObject    Panel;
        public string        RootName;
        public string        PanelName;
        public string        Skip;      // 비어 있지 않으면 건너뛴 이유
    }

    static void Run(bool apply)
    {
        // `using System;`(StringComparison)이 있어 `Object`만 쓰면 `System.Object`와 겹친다.
        var comps = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        var plans = new List<Plan>();
        foreach (var c in comps)
        {
            if (c == null) continue;
            var plan = Examine(c);
            if (plan.Comp != null) plans.Add(plan);
        }

        if (plans.Count == 0)
        {
            Debug.LogWarning("[UiObjectRenamer] `panel` 필드를 가진 창을 찾지 못했습니다.");
            return;
        }

        plans.Sort((a, b) => string.CompareOrdinal(a.RootName, b.RootName));

        var sb = new StringBuilder();
        sb.AppendLine(apply ? "[UiObjectRenamer] 적용" : "[UiObjectRenamer] 미리보기 (아무것도 바꾸지 않음)");

        int changed = 0, skipped = 0, already = 0;
        Scene? scene = null;

        foreach (var p in plans)
        {
            if (!string.IsNullOrEmpty(p.Skip))
            {
                sb.AppendLine($"  건너뜀  {p.Root.name}  — {p.Skip}");
                skipped++;
                continue;
            }

            bool rootOk  = p.Root.name  == p.RootName;
            bool panelOk = p.Panel.name == p.PanelName;
            if (rootOk && panelOk) { already++; continue; }

            sb.AppendLine($"  {p.Root.name}/{p.Panel.name}".PadRight(48) +
                          $"→  {p.RootName}/{p.PanelName}");
            changed++;

            if (!apply) continue;

            scene ??= p.Root.scene;
            if (!rootOk)  p.Root.name  = p.RootName;
            if (!panelOk) p.Panel.name = p.PanelName;
            EditorUtility.SetDirty(p.Root);
        }

        sb.AppendLine($"  — 바꿀 것 {changed} · 이미 맞음 {already} · 건너뜀 {skipped}");
        if (!apply) sb.AppendLine("  적용하려면 DungeonColony → Rename UI Objects (Apply)");

        Debug.Log(sb.ToString());

        if (apply && scene.HasValue)
        {
            EditorSceneManager.MarkSceneDirty(scene.Value);
            EditorSceneManager.SaveOpenScenes();
        }
    }

    // 이 컴포넌트가 "창"인가 — `panel` 필드를 가졌으면 그렇다. 아니면 빈 Plan.
    static Plan Examine(MonoBehaviour comp)
    {
        var so   = new SerializedObject(comp);
        var prop = so.FindProperty("panel");
        if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference)
            return default;

        var root  = comp.gameObject;
        var panel = prop.objectReferenceValue as GameObject;

        var plan = new Plan
        {
            Comp      = comp,
            Root      = root,
            Panel     = panel,
            RootName  = BaseName(comp.GetType().Name) + "_UI",
            PanelName = BaseName(comp.GetType().Name) + "_Panel",
        };

        // **구조용 묶음은 창이 아니다.** `Canvas/WorldUI`는 월드 모드에서 켜지는 UI 전부를
        // 담는 자리이고(`GameModeManager`가 이것만 반대로 켠다 — `CLAUDE.md`), 지금 마침
        // `WorldMenuUI` 컴포넌트가 여기 얹혀 있을 뿐이다. `WorldMenu_UI`로 바꾸면 나중에
        // 추가되는 월드 전용 UI가 "월드메뉴" 밑에 사는 꼴이 된다.
        //
        // 제대로 된 답은 이름이 아니라 구조다 — `WorldUI` 안에 `WorldMenu_UI`를 세우고
        // 컴포넌트를 그리로 옮기는 것 — `DungeonColony → Fix Window Structure`가 그 일을 한다.
        // 그걸 돌리고 나면 이 예외에 안 걸린다(컴포넌트가 `WorldMenu_UI`로 내려가므로).
        if (root.name == "WorldUI")
            plan.Skip = "월드 UI 묶음 — 창이 아니라 GameModeManager가 토글하는 구조 오브젝트";
        // 프리팹에 사는 것은 손대지 않는다 — 인스턴스 이름만 바꾸면 프리팹과 어긋난 채
        // 오버라이드로 남고, 프리팹을 다시 적용하는 순간 되돌아간다.
        else if (PrefabUtility.IsPartOfPrefabInstance(root) || PrefabUtility.IsPartOfPrefabAsset(root))
            plan.Skip = "프리팹 소속 — 프리팹에서 직접 고칠 것";
        else if (panel == null)
            plan.Skip = "panel 필드가 비어 있음";
        else if (panel == root)
            plan.Skip = "컴포넌트가 패널 자신에 붙어 있음 — 부모를 세우는 것이 먼저 " +
                        "(docs/ui.md \"늘 켜진 부모 + 꺼지는 자식\")";

        return plan;
    }

    // 클래스 이름에서 꼬리 `UI`를 뗀다. **창 컴포넌트는 전부 `XxxUI`다**(2026-08-04 통일) —
    // 예전엔 `XxxPanel`도 섞여 있어서 여기서 둘 다 벗겨야 했다.
    // 꼬리가 없으면 그대로 쓴다(규약을 안 지킨 새 컴포넌트가 들어와도 멈추지 않게).
    static string BaseName(string typeName) =>
        typeName.EndsWith("UI", StringComparison.Ordinal) && typeName.Length > 2
            ? typeName.Substring(0, typeName.Length - 2)
            : typeName;
}
