using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// **연구 그래프를 씬에 굽는다**(2026-08-25, 사용자 요청 *"안에 연구내용을 런타임으로 만들지
// 말고 ResearchTable_Panel에 보이게 만들어줘"*).
//
// ## 왜
//
// `ResearchTableUI.RebuildGraph`는 창을 열 때마다 자식을 전부 지우고 다시 찍었다. 그래서
// **에디터의 `ResearchTable_Panel`은 언제나 빈 판**이었다 — 자리를 저작해 놓고도(`GraphColumn`·
// `GraphRow`) **플레이를 눌러야만 결과를 볼 수 있었다.** 저작한 것을 못 보면 그건 저작이 아니라
// 짐작이다.
//
// 이 저장소가 이미 그렇게 굴러간다: **규격은 씬이 갖고 런타임은 그 안을 채운다.** 자리가
// 저작물이 된 이상 노드도 저작물이다.
//
// ## 무엇을 굽나
//
// 노드(`ResearchEntryUI`)와 **선**(`ResearchLinkGraphic._links`) 둘 다. 선까지 구워야
// 에디터에서 그래프로 보인다 — 노드만 있으면 흩어진 상자들이다.
//
// ## 씬이 진짜다
//
// (2026-08-25, 사용자 *"런타임에서 쓰는 걸 굽는게 아니라 그냥 진짜 씬에 배치한걸 런타임에
// 썼으면 좋겠는데"*.)
//
// 처음엔 구운 노드를 **오브젝트로만** 쓰고 런타임이 자리를 다시 계산했다 — 그러면 **씬에서
// 노드를 옮겨 놔도 게임에서는 제자리로 돌아간다.** 굽는 뜻이 절반만 서는 것이다.
//
// 이제 런타임은 **노드 자리를 안 건드린다.** 선은 **씬에 놓인 노드의 자리에서 다시 긋는다** —
// 계산해서 긋는 것이 아니라 *거기 있는 것*을 읽는 것이라, 씬에서 노드를 끌어 옮기면
// **선이 따라온다.**
//
// **다시 구워도 옮긴 자리는 안 없어진다** — 있던 노드는 짝을 지어 다시 쓰고 **자리를 그대로
// 둔다**(자리를 주는 것은 새로 찍는 노드뿐이다). 그래서 연구를 더한 뒤 다시 굽는 것이
// 안전하고, 그때 **에디터 화면의 선도 지금 자리에 맞게 다시 그어진다.**
//
// 연구 수가 안 맞으면 런타임이 **콘솔에 적고** 통째로 다시 찍는다(조용히 반만 맞는 일이 없게).
//
// ## 다시 돌려도 안전하다
//
// 있던 노드는 **연구별로 짝을 지어 다시 쓰고**(자리 유지), 없던 것만 새로 찍고, 없어진 연구의
// 노드만 지운다. **선 판(`links`)은 안 지운다**(참조가 끊긴다).
public static class ResearchGraphBaker
{
    const string ResearchDir = "Assets/Resources/Data/Research";

    [MenuItem("DungeonColony/Bake Research Graph")]
    static void Bake()
    {
        var ui = Object.FindObjectsByType<ResearchTableUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
        if (ui == null) { Debug.LogError("[연구 그래프] 씬에 ResearchTableUI가 없습니다."); return; }

        var so        = new SerializedObject(ui);
        var container = so.FindProperty("entryContainer").objectReferenceValue as Transform;
        var prefab    = so.FindProperty("entryPrefab").objectReferenceValue as ResearchEntryUI;
        var links     = so.FindProperty("links").objectReferenceValue as ResearchLinkGraphic;
        var gap       = so.FindProperty("nodeGap").vector2Value;
        var padding   = so.FindProperty("graphPadding").vector2Value;

        if (container == null || prefab == null)
        {
            Debug.LogError("[연구 그래프] entryContainer 또는 entryPrefab이 배선되지 않았습니다.");
            return;
        }
        if (links == null)
            Debug.LogWarning("[연구 그래프] 선 판(links)이 배선되지 않았습니다 — 노드만 굽습니다.");

        // **레지스트리가 아니라 `AssetDatabase`로 읽는다** — 에디터에서는 `DataManager`가
        // 안 서 있어 `AllResearch`가 빈손이다(08-25에 재료가 통째로 비었던 그 자리).
        var all = AssetDatabase.FindAssets("t:ResearchData", new[] { ResearchDir })
                               .Select(AssetDatabase.GUIDToAssetPath)
                               .Select(AssetDatabase.LoadAssetAtPath<ResearchData>)
                               .Where(r => r != null)
                               .OrderBy(r => r.name, System.StringComparer.Ordinal)
                               .ToList();
        if (all.Count == 0) { Debug.LogError($"[연구 그래프] {ResearchDir} 에 연구가 없습니다."); return; }

        var nodeSize = NodeSize(prefab);
        var graph    = ResearchGraphLayout.Build(all, nodeSize, gap, padding);

        // ── 이미 있는 노드는 **자리를 지킨다** ──────────────────────────────
        //
        // 지우고 새로 찍으면 **손으로 옮겨 둔 자리가 날아간다** — 씬을 진짜로 만들어 놓고
        // 도구를 한 번 돌리면 저작이 사라지는 꼴이라, 다시 구울 엄두가 안 난다.
        //
        // 그래서 **연구별로 짝을 지어 다시 쓴다**: 있던 것은 그 자리에 두고, 없던 것만 새로
        // 찍고, 없어진 연구의 노드만 지운다. **선 판은 건드리지 않는다**(지우면 참조가 끊긴다).
        var existing = new Dictionary<ResearchData, ResearchEntryUI>();
        var orphans  = new List<GameObject>();

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i);
            if (links != null && child == links.transform) continue;

            if (child.TryGetComponent<ResearchEntryUI>(out var e) && e.Data != null
                && !existing.ContainsKey(e.Data))
                existing[e.Data] = e;
            else
                orphans.Add(child.gameObject);   // 연구가 없어졌거나 같은 것이 둘
        }
        foreach (var o in orphans) Undo.DestroyObjectImmediate(o);

        int kept = 0, added = 0;
        foreach (var data in all)
        {
            if (!graph.Slots.TryGetValue(data, out var slot)) continue;

            bool isNew = !existing.TryGetValue(data, out var entry);
            if (isNew)
            {
                entry = (ResearchEntryUI)PrefabUtility.InstantiatePrefab(prefab, container);
                entry.gameObject.name = data.name;
                Undo.RegisterCreatedObjectUndo(entry.gameObject, "Bake Research Graph");

                // private [SerializeField]라 직렬화 창구로 넣는다. **프리팹 오버라이드로 남으므로**
                // 프리팹을 손봐도 각 노드가 제 연구를 계속 기억한다.
                var eso = new SerializedObject(entry);
                eso.FindProperty("_data").objectReferenceValue = data;
                eso.ApplyModifiedPropertiesWithoutUndo();
                added++;
            }
            else kept++;

            entry.Setup(data);   // 이름·색을 지금 넣어야 **에디터에서 글자가 보인다**

            if (entry.transform is RectTransform rt)
            {
                Undo.RecordObject(rt, "Bake Research Graph");
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.sizeDelta = graph.NodeSize;

                // **새로 찍은 것에만 자리를 준다** — 있던 것은 씬에 놓인 그대로가 답이다.
                if (isNew) rt.anchoredPosition = slot.TopLeft;
            }
        }

        // 스크롤 범위는 **넉넉한 쪽으로** 잡는다 — 계산한 크기와, 지금 노드가 실제로 차지하는
        // 크기 중 큰 것. 손으로 오른쪽·아래로 끌어 옮긴 노드가 **스크롤 밖으로 나가지 않게** 한다.
        if (container is RectTransform content) content.sizeDelta = ContentSize(container, graph, padding);

        int lines = 0;
        if (links != null)
        {
            (links.transform as RectTransform).sizeDelta = graph.Size;
            var list = BuildLinksFromScene(container);
            lines = list.Count;

            // `SetLinks`가 `_links`에 담고 메시를 다시 만든다 — 이제 `[SerializeField]`라
            // **씬에 남는다**(그래서 에디터에서 선이 보인다).
            Undo.RecordObject(links, "Bake Research Graph");
            links.SetLinks(list);
            EditorUtility.SetDirty(links);
        }

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Debug.Log($"[연구 그래프] 노드 {kept + added}개(있던 것 {kept} · 새로 {added}) · 선 {lines}개 "
                + $"({graph.Size.x:F0} × {graph.Size.y:F0}). 씬을 저장하세요.");
    }

    // 노드가 실제로 차지하는 넓이(손으로 옮긴 것까지)와 계산한 크기 중 **큰 쪽**.
    static Vector2 ContentSize(Transform container, ResearchGraphLayout.Result graph, Vector2 pad)
    {
        float w = graph.Size.x, h = graph.Size.y;
        foreach (Transform child in container)
        {
            if (child is not RectTransform rt || !child.TryGetComponent<ResearchEntryUI>(out _)) continue;
            w = Mathf.Max(w, rt.anchoredPosition.x + rt.rect.width  + pad.x);
            h = Mathf.Max(h, -rt.anchoredPosition.y + rt.rect.height + pad.y);
        }
        return new Vector2(w, h);
    }

    // `ResearchTableUI.NodeSize()`와 **같은 식이어야 한다** — 다르면 구운 자리와 런타임
    // 자리가 어긋나 선이 노드에 안 붙는다.
    static Vector2 NodeSize(ResearchEntryUI prefab)
    {
        var rt   = prefab.transform as RectTransform;
        var size = rt != null ? rt.rect.size : Vector2.zero;
        return new Vector2(size.x > 1f ? size.x : 156f, size.y > 1f ? size.y : 38f);
    }

    // **씬에 놓인 노드에서 긋는다 — 계산한 자리가 아니라.**
    //
    // 노드는 `anchoredPosition`을 지켜 주면서 선만 계산 자리에 그으면, **손으로 옮긴 노드의
    // 선이 옛 자리에 떠 있는다**(세 개 옮기고 연구 하나 더한 뒤 다시 구우면 바로 그 꼴이다).
    // `ResearchTableUI.BuildLinksFromScene`과 **같은 식**이어야 에디터와 게임이 같은 그림이다.
    //
    // 에디터에는 `ResearchManager`가 없으므로 완료 표시(`Done`)는 늘 false — 게임에서는
    // 런타임이 다시 긋는다.
    static List<ResearchLinkGraphic.Link> BuildLinksFromScene(Transform container)
    {
        var nodes = new Dictionary<ResearchData, RectTransform>();
        foreach (Transform child in container)
            if (child is RectTransform rt && child.TryGetComponent<ResearchEntryUI>(out var e)
                && e.Data != null && !nodes.ContainsKey(e.Data))
                nodes[e.Data] = rt;

        var result = new List<ResearchLinkGraphic.Link>();
        foreach (var kv in nodes)
        {
            var data = kv.Key;
            if (data.Prerequisites == null) continue;

            foreach (var pre in data.Prerequisites)
            {
                if (pre == null || !nodes.TryGetValue(pre, out var from)) continue;

                result.Add(new ResearchLinkGraphic.Link
                {
                    From = from.anchoredPosition + new Vector2(from.rect.width, -from.rect.height * 0.5f),
                    To   = kv.Value.anchoredPosition + new Vector2(0f, -kv.Value.rect.height * 0.5f),
                    Done = false,
                });
            }
        }
        return result;
    }
}
