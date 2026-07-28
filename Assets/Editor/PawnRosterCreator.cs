using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// DungeonColony/Create Pawn Roster UI 실행
// → "Portrait" 레이어 보장 + 우측 하단 폰 명부 패널을 **씬 오브젝트로** 생성 + 필드 연결.
//
// 행 하나(`RowTemplate`)도 씬에 만든다. **런타임에는 이 템플릿을 복제해 목록을 채운다** —
// 편집 중 보이는 모양이 곧 게임에서 보이는 모양이고, 여백·폰트·색을 인스펙터에서 고치면
// 코드 수정 없이 그대로 반영된다. 코드는 데이터 연결만 한다(PawnRosterRowUI.Bind).
public static class PawnRosterCreator
{
    const string FontPath      = "Assets/NanumGothic SDF.asset";
    const string PortraitLayer = "Portrait";

    const float PanelW      = 340f;
    const float PanelH      = 760f;
    const float HeaderH     = 34f;
    const float RowHeight   = 52f;
    const float PortraitPad = 8f;
    const float StatusWidth = 78f;

    static readonly Color HeaderColor = new Color(0.05098f, 0.05098f, 0.05098f, 1f); // #0D0D0D
    static readonly Color RowColor    = new Color(1f, 1f, 1f, 0.04f);

    [MenuItem("DungeonColony/Create Pawn Roster UI")]
    static void Create()
    {
        EnsureLayer(PortraitLayer);

        var comp = Object.FindFirstObjectByType<PawnRosterPanel>();
        if (comp == null)
        {
            Debug.LogError("[PawnRosterCreator] 씬에 PawnRosterPanel 컴포넌트가 없습니다.");
            return;
        }

        var so = new SerializedObject(comp);

        var oldPanel = so.FindProperty("panel").objectReferenceValue as GameObject;
        if (oldPanel != null) Undo.DestroyObjectImmediate(oldPanel);

        var canvas = comp.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[PawnRosterCreator] 씬에 Canvas가 없습니다.");
            return;
        }

        // ── 패널 루트 (우측 하단 모서리에 딱 붙임)
        var parent  = comp.transform != canvas.transform ? comp.transform : canvas.transform;
        var panel   = MakeRect("PawnRoster_Panel", parent);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(1f, 0f);
        panelRT.pivot     = new Vector2(1f, 0f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = new Vector2(PanelW, PanelH);
        panel.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.97f);

        // ── 헤더 (#0D0D0D 풀폭). 규칙 1: LayoutGroup 없이 앵커 배치
        var header   = MakeRect("Header", panel.transform);
        var headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f); headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot     = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0f, HeaderH);
        header.AddComponent<Image>().color = HeaderColor;

        var titleTMP = MakeText("Title", header.transform, "무리 (0)", 15, TextAlignmentOptions.MidlineLeft);
        var titleRT  = titleTMP.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(12f, 0f); titleRT.offsetMax = new Vector2(-12f, 0f);

        // ── 스크롤 뷰 (헤더 아래 전체)
        var sv   = MakeRect("ScrollView", panel.transform);
        var svRT = sv.GetComponent<RectTransform>();
        svRT.anchorMin = Vector2.zero; svRT.anchorMax = Vector2.one;
        svRT.offsetMin = Vector2.zero;
        svRT.offsetMax = new Vector2(0f, -HeaderH);

        var sr = sv.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 5f;

        var vp = MakeRect("Viewport", sv.transform);
        SetStretch(vp);
        vp.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        vp.AddComponent<Mask>().showMaskGraphic = false;
        sr.viewport = vp.GetComponent<RectTransform>();

        var content   = MakeRect("Content", vp.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f); contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero; contentRT.sizeDelta = Vector2.zero;

        var vg = content.AddComponent<VerticalLayoutGroup>();
        vg.childControlWidth = vg.childControlHeight = true;
        vg.childForceExpandWidth = true; vg.childForceExpandHeight = false;
        vg.spacing = 3f; vg.padding = new RectOffset(6, 6, 6, 6);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = contentRT;

        // ── 행 템플릿 (편집 중 보이는 그 행 = 런타임에 복제되는 원본)
        var rowTemplate = BuildRowTemplate(content.transform);

        // 런타임 생성물이 아니므로 한글 폰트를 직접 박아둔다
        ApplyFontToChildren(panel);

        // ── 필드 연결
        so.FindProperty("panel").objectReferenceValue       = panel;
        so.FindProperty("content").objectReferenceValue     = content.transform;
        so.FindProperty("titleText").objectReferenceValue   = titleTMP;
        so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
        so.FindProperty("selectionManager").objectReferenceValue =
            Object.FindFirstObjectByType<SelectionManager>();
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(panel, "Create Pawn Roster UI");
        EditorUtility.SetDirty(comp);
        Selection.activeGameObject = rowTemplate.gameObject;
        Debug.Log("[PawnRosterCreator] 폰 로스터 생성 완료 — " +
                  "RowTemplate을 인스펙터에서 고치면 게임 목록에 그대로 반영됩니다");
    }

    // 행 하나. 요소는 앵커로 붙여서 패널 폭을 바꾸면 이름 칸이 알아서 늘어난다.
    static PawnRosterRowUI BuildRowTemplate(Transform parent)
    {
        float portrait = RowHeight - PortraitPad * 2f;

        var row   = MakeRect("RowTemplate", parent);
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = rowLE.preferredHeight = RowHeight;

        var rowBg = row.AddComponent<Image>();
        rowBg.color = RowColor;

        var btn = row.AddComponent<Button>();
        btn.targetGraphic = rowBg;
        btn.transition    = Selectable.Transition.None;
        btn.navigation    = new Navigation { mode = Navigation.Mode.None };

        // 초상화 — 좌측 고정
        var iconGO = MakeRect("Portrait", row.transform);
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot     = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(PortraitPad, 0f);
        iconRT.sizeDelta = new Vector2(portrait, portrait);
        var icon = iconGO.AddComponent<Image>();
        icon.raycastTarget  = false;
        icon.preserveAspect = true;
        icon.color = new Color(1f, 1f, 1f, 0.12f);

        // 이름 — 초상화 오른쪽부터 상태 칸 앞까지 **스트레치**(패널을 넓히면 같이 넓어짐)
        var nameTMP = MakeText("Name", row.transform, "폰 이름", 19, TextAlignmentOptions.MidlineLeft);
        nameTMP.textWrappingMode = TextWrappingModes.NoWrap;
        nameTMP.overflowMode     = TextOverflowModes.Ellipsis;
        var nameRT = nameTMP.GetComponent<RectTransform>();
        nameRT.anchorMin = Vector2.zero; nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = new Vector2(PortraitPad * 2f + portrait, 0f);
        nameRT.offsetMax = new Vector2(-(StatusWidth + 12f), 0f);

        // 상태 — 우측 고정
        var statusTMP = MakeText("Status", row.transform, "대기", 16, TextAlignmentOptions.MidlineRight);
        var statusRT  = statusTMP.GetComponent<RectTransform>();
        statusRT.anchorMin = new Vector2(1f, 0f); statusRT.anchorMax = new Vector2(1f, 1f);
        statusRT.pivot     = new Vector2(1f, 0.5f);
        statusRT.anchoredPosition = new Vector2(-10f, 0f);
        statusRT.sizeDelta = new Vector2(StatusWidth, 0f);

        var comp = row.AddComponent<PawnRosterRowUI>();
        var rso  = new SerializedObject(comp);
        rso.FindProperty("background").objectReferenceValue    = rowBg;
        rso.FindProperty("portraitImage").objectReferenceValue = icon;
        rso.FindProperty("nameText").objectReferenceValue      = nameTMP;
        rso.FindProperty("statusText").objectReferenceValue    = statusTMP;
        rso.FindProperty("selectButton").objectReferenceValue  = btn;
        rso.ApplyModifiedProperties();

        return comp;
    }

    // ── 헬퍼 ──────────────────────────────────────────────
    static void EnsureLayer(string layerName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");

        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;

        // 8번부터가 사용자 레이어
        for (int i = 8; i < layers.arraySize; i++)
        {
            var element = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(element.stringValue)) continue;
            element.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"[PawnRosterCreator] '{layerName}' 레이어 생성 (index {i})");
            return;
        }

        Debug.LogWarning($"[PawnRosterCreator] 빈 레이어 슬롯이 없어 '{layerName}'을 만들지 못했습니다.");
    }

    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static void SetStretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size,
                                    TextAlignmentOptions align)
    {
        var go  = MakeRect(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = Color.white;
        tmp.alignment = align; tmp.raycastTarget = false;
        ApplyFont(tmp);
        return tmp;
    }

    static void ApplyFontToChildren(GameObject root)
    {
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            ApplyFont(tmp);
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }
}
