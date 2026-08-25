using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 연구대 창의 **규격을 씬에 다시 세운다**(2026-08-25, 사용자 *"연구대 UI도 새로 다시 만들어줘
// 지금 크기를 건들여서 뭔가 틀어졌어"*).
//
// ## 왜 틀어졌나
//
// 창이 **1240 × 700**인데 그래프는 연구가 마흔일곱으로 늘면서 **1732 × 698**이 됐다 —
// 스크롤 칸이 820폭이라 **그래프의 절반도 안 보인다.** 손으로 크기를 만지면 스크롤 칸과
// 디테일 판이 서로 겹치거나 창 밖으로 밀려나는데, 그 셋(창·스크롤·디테일)은 **한 벌로
// 계산해야 맞는 값**이라 하나만 고치면 반드시 어긋난다.
//
// ## 규격은 여기 하나가 든다
//
// `StatPanelSetup`과 같은 자리다 — **몇 번 돌려도 같다**(자리 값이 절대값이라 상대로 옮기면
// 두 번째 실행이 두 번 민다). 오브젝트를 지우고 새로 만들지 **않는다**: `ResearchTableUI`가
// 들고 있는 배선(제목·설명·재료·버튼·게이지 열두 개)이 거기 매달려 있어서, 다시 지으면
// 그것을 전부 다시 물려야 하고 하나만 빠뜨려도 **조용히 반만 도는 창**이 된다.
// 여기서 하는 일은 **자리와 크기와 스크롤 설정을 규격으로 되돌리는 것**뿐이다.
//
// ## 값
//
// 화면 기준은 1920 × 1080(`CanvasScaler`). 창을 **1800 × 900**으로 잡으면 세로는 그래프가
// 통째로 들어가고(698 < 830) 가로만 조금 스크롤한다(1732 > 1378). **가로를 다 담으려면
// 노드를 좁히거나 간격을 줄여야 하는데**, 그러면 잠긴 이름(`[잠김] 에메랄드 광맥 이식`)이
// 잘린다 — 긴 기술 트리가 가로로 스크롤되는 것은 정상이다(림월드도 그렇다).
public static class ResearchPanelSetup
{
    // ── 규격 ─────────────────────────────────────────────────────────────
    const float PanelW = 1800f, PanelH = 900f;
    const float Pad    = 10f;    // 창 가장자리 여백
    const float TitleH = 40f;    // 제목 바
    const float Gap    = 10f;    // 칸 사이
    const float DetailW = 392f;  // 오른쪽 디테일 판 (씬에 있던 값 그대로)

    [MenuItem("DungeonColony/Setup Research Panel")]
    static void Setup()
    {
        // **꺼진 것까지 찾는다** — 연구 창은 평소 꺼져 있다.
        var found = Object.FindObjectsByType<ResearchTableUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found.Length == 0) { Debug.LogError("[연구대 창] 씬에 ResearchTableUI가 없습니다."); return; }
        var ui = found[0];

        var so        = new SerializedObject(ui);
        var panel     = so.FindProperty("panel").objectReferenceValue as GameObject;
        var container = so.FindProperty("entryContainer").objectReferenceValue as Transform;
        var detail    = so.FindProperty("detailPanel").objectReferenceValue as GameObject;
        var links     = so.FindProperty("links").objectReferenceValue as ResearchLinkGraphic;

        if (panel == null || container == null)
        { Debug.LogError("[연구대 창] panel 또는 entryContainer가 배선되지 않았습니다."); return; }

        var panelRt = panel.transform as RectTransform;
        var content = container as RectTransform;
        var scroll  = content.GetComponentInParent<ScrollRect>(true);
        if (scroll == null) { Debug.LogError("[연구대 창] Content 위에 ScrollRect가 없습니다."); return; }

        var scrollRt   = scroll.transform as RectTransform;
        var viewportRt = scroll.viewport != null ? scroll.viewport : content.parent as RectTransform;

        Undo.RecordObjects(new Object[]{ panelRt, scrollRt, content, scroll }, "Setup Research Panel");

        // ── 창 ────────────────────────────────────────────────────────────
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta        = new Vector2(PanelW, PanelH);
        panelRt.anchoredPosition = Vector2.zero;

        // ── 제목 바 — 있으면 자리만 맞춘다 ────────────────────────────────
        var title = panelRt.Find("TitleBar") as RectTransform;
        if (title != null)
        {
            Undo.RecordObject(title, "Setup Research Panel");
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot     = new Vector2(0f, 1f);
            title.sizeDelta        = new Vector2(-Pad * 2f, TitleH);
            title.anchoredPosition = new Vector2(Pad, -Pad);
        }

        // ── 스크롤 칸과 디테일 판은 **한 벌로** 계산한다 ──────────────────
        //
        // 하나만 손으로 고치면 반드시 어긋난다 — 창 폭에서 여백·간격·디테일 폭을 빼고 남는
        // 것이 스크롤 폭이다. 손으로 만지면 그 뺄셈이 안 맞는다.
        float top      = Pad + TitleH + Gap;
        float innerH   = PanelH - top - Pad;
        float scrollW  = PanelW - Pad * 2f - DetailW - Gap;

        SetTopLeft(scrollRt, new Vector2(Pad, -top), new Vector2(scrollW, innerH));

        if (detail != null)
            SetTopLeft(detail.transform as RectTransform,
                       new Vector2(Pad + scrollW + Gap, -top), new Vector2(DetailW, innerH));

        // ── 스크롤 설정 ───────────────────────────────────────────────────
        //
        // **가로도 켠다** — 그래프가 창보다 넓다(1732 > 1378). 세로만 켜 두면 오른쪽 절반을
        // 영영 못 본다. **탄성은 끈다**: 튕기면 노드를 누르려던 클릭이 미끄러진다.
        scroll.horizontal   = true;
        scroll.vertical     = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.content      = content;
        if (viewportRt != null) scroll.viewport = viewportRt;

        if (viewportRt != null)
        {
            Undo.RecordObject(viewportRt, "Setup Research Panel");
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.pivot     = new Vector2(0.5f, 0.5f);
            viewportRt.sizeDelta        = Vector2.zero;
            viewportRt.anchoredPosition = Vector2.zero;
        }

        // ── Content — 왼쪽 위 고정 ────────────────────────────────────────
        //
        // 크기는 **그래프가 정한다**(런타임/굽기가 `graph.Size`를 넣는다). 여기서는 기준점만
        // 못 박는다 — 피벗이 가운데면 노드 좌표가 통째로 어긋난다.
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot     = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;

        // **레이아웃 그룹은 없어야 한다** — 있으면 자식의 `anchoredPosition`을 매 프레임
        // 덮어써서 **저작한 자리가 통째로 무시된다**(그래프가 한 줄로 늘어선다).
        int stripped = 0;
        foreach (var lg in content.GetComponents<LayoutGroup>())       { Undo.DestroyObjectImmediate(lg); stripped++; }
        foreach (var f  in content.GetComponents<ContentSizeFitter>())  { Undo.DestroyObjectImmediate(f);  stripped++; }

        // ── 선 판 — Content와 같은 기준, 그리고 맨 뒤 ─────────────────────
        if (links != null)
        {
            var lrt = links.transform as RectTransform;
            Undo.RecordObject(lrt, "Setup Research Panel");
            lrt.anchorMin = new Vector2(0f, 1f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot     = new Vector2(0f, 1f);
            lrt.anchoredPosition = Vector2.zero;
            // **첫 자식이라야 노드 뒤에 깔린다** — 뒤에 두면 선이 글자를 덮는다.
            lrt.SetAsFirstSibling();
            links.raycastTarget = false;   // 선이 클릭을 먹으면 노드를 못 누른다
        }

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);

        Debug.Log($"[연구대 창] 규격을 세웠습니다 — 창 {PanelW}×{PanelH} · "
                + $"스크롤 {scrollW}×{innerH} · 디테일 {DetailW}×{innerH}"
                + (stripped > 0 ? $" · 레이아웃 그룹 {stripped}개 제거" : "")
                + ". 씬을 저장하고 `Bake Research Graph`를 다시 돌리세요.");
    }

    // 왼쪽 위 기준으로 자리와 크기를 못 박는다. **절대값이라 몇 번 돌려도 같다** —
    // 상대로 옮기면 두 번째 실행이 두 번 민다(건설 메뉴에서 겪은 그 함정).
    static void SetTopLeft(RectTransform rt, Vector2 pos, Vector2 size)
    {
        if (rt == null) return;
        Undo.RecordObject(rt, "Setup Research Panel");
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;
    }
}
