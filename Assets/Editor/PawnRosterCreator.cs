using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// DungeonColony/Create Pawn Roster UI 실행
// → "Portrait" 레이어 보장 + 씬의 PawnRosterPanel UI 생성 + 엔트리 프리팹 저장 + 필드 연결
public static class PawnRosterCreator
{
    const string FontPath    = "Assets/NanumGothic SDF.asset";
    const string EntryPath   = "Assets/3_Prefab/UI/PawnRosterEntry.prefab";
    const string PortraitLayer = "Portrait";
    const float  PanelH = 290f;   // 가로 바 높이 (카드 2줄)
    static readonly Vector2 CellSize = new Vector2(110f, 132f);

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

        // ── 엔트리 프리팹 생성/갱신
        var entryPrefab = BuildEntryPrefab(canvas.transform);

        // ── 패널 루트 (하단 가로 바, 좌우 스트레치)
        var panel   = MakeRect("PawnRoster_Panel", comp.transform != canvas.transform ? comp.transform : canvas.transform);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 0f);
        panelRT.anchorMax = new Vector2(1f, 0f);
        panelRT.pivot     = new Vector2(0.5f, 0f);
        panelRT.offsetMin = new Vector2(10f, 10f);            // 좌/하 여백
        panelRT.offsetMax = new Vector2(-10f, 10f + PanelH);  // 우 여백, 높이 = PanelH
        panel.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.92f);

        var hg = panel.AddComponent<HorizontalLayoutGroup>();
        hg.childControlWidth = hg.childControlHeight = true;
        hg.childForceExpandWidth = false; hg.childForceExpandHeight = true;
        hg.childAlignment = TextAnchor.MiddleLeft; hg.spacing = 10f;
        hg.padding = new RectOffset(10, 10, 8, 8);

        MakeHeader("대기중", panel.transform);
        var idleContainer = MakeSection("IdleContainer", panel.transform);

        MakeHeader("작업중", panel.transform);
        var workContainer = MakeSection("WorkContainer", panel.transform);

        // ── 필드 연결
        so.FindProperty("panel").objectReferenceValue          = panel;
        so.FindProperty("idleContainer").objectReferenceValue  = idleContainer.transform;
        so.FindProperty("workContainer").objectReferenceValue  = workContainer.transform;
        so.FindProperty("entryPrefab").objectReferenceValue    = entryPrefab;
        so.FindProperty("selectionManager").objectReferenceValue = Object.FindFirstObjectByType<SelectionManager>();
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(panel, "Create Pawn Roster UI");
        EditorUtility.SetDirty(comp);
        Debug.Log("[PawnRosterCreator] PawnRoster UI 생성 완료");
    }

    // 폰 엔트리 프리팹: [초상화] [이름] + 전체 클릭 버튼
    static PawnRosterEntryUI BuildEntryPrefab(Transform tempParent)
    {
        // 세로 카드: 초상화(위) + 이름(아래), 가로 바에 좌→우로 나열될 고정폭 셀
        var root = MakeRect("PawnRosterEntry", tempParent);
        root.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.19f, 1f);
        var btn = root.AddComponent<Button>();
        var le  = root.AddComponent<LayoutElement>();
        le.preferredWidth = 96;

        var vg = root.AddComponent<VerticalLayoutGroup>();
        vg.childControlWidth = vg.childControlHeight = true;
        vg.childForceExpandWidth = false; vg.childForceExpandHeight = false;
        vg.childAlignment = TextAnchor.UpperCenter; vg.spacing = 4f;
        vg.padding = new RectOffset(6, 6, 6, 6);

        var portraitGO = MakeRect("Portrait", root.transform);
        var pLE = portraitGO.AddComponent<LayoutElement>();
        pLE.preferredWidth = 96; pLE.preferredHeight = 96;
        var portrait = portraitGO.AddComponent<Image>();
        portrait.preserveAspect = true; portrait.raycastTarget = false;

        var nameGO  = MakeRect("NameText", root.transform);
        var nLE = nameGO.AddComponent<LayoutElement>();
        nLE.preferredWidth = 96; nLE.preferredHeight = 20;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "이름"; nameTMP.fontSize = 14; nameTMP.color = Color.white;
        nameTMP.alignment = TextAlignmentOptions.Center; nameTMP.raycastTarget = false;
        nameTMP.enableWordWrapping = false; nameTMP.overflowMode = TextOverflowModes.Ellipsis;
        ApplyFont(nameTMP);

        var entry = root.AddComponent<PawnRosterEntryUI>();
        var eso = new SerializedObject(entry);
        eso.FindProperty("portraitImage").objectReferenceValue = portrait;
        eso.FindProperty("nameText").objectReferenceValue      = nameTMP;
        eso.FindProperty("selectButton").objectReferenceValue  = btn;
        eso.ApplyModifiedProperties();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, EntryPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[PawnRosterCreator] 엔트리 프리팹 저장: {EntryPath}");
        return prefab.GetComponent<PawnRosterEntryUI>();
    }

    static void MakeHeader(string text, Transform parent)
    {
        var go  = MakeRect("Header_" + text, parent);
        var le  = go.AddComponent<LayoutElement>();
        le.preferredWidth = 40;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 12; tmp.color = new Color(0.7f, 0.7f, 0.75f);
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        ApplyFont(tmp);
    }

    // 2행 그리드 섹션. 세로(2행)부터 채우고 가로로 확장. GridLayoutGroup의 preferred width를
    // 부모 HorizontalLayoutGroup이 읽으므로 ContentSizeFitter는 불필요(충돌 방지).
    static GameObject MakeSection(string name, Transform parent)
    {
        var go = MakeRect(name, parent);
        var grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize        = CellSize;
        grid.spacing         = new Vector2(6f, 6f);
        grid.constraint      = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 2;
        grid.startAxis       = GridLayoutGroup.Axis.Vertical;
        grid.childAlignment  = TextAnchor.MiddleLeft;
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = 30;
        return go;
    }

    // ── "Portrait" 레이어 보장 ───────────────────────────
    static int EnsureLayer(string layerName)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning("[PawnRosterCreator] TagManager.asset을 찾을 수 없어 레이어를 보장하지 못했습니다.");
            return -1;
        }

        var tagManager = new SerializedObject(assets[0]);
        var layers = tagManager.FindProperty("layers");

        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                return i;

        for (int i = 8; i < layers.arraySize; i++) // 0-7은 빌트인 예약
        {
            var el = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(el.stringValue))
            {
                el.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[PawnRosterCreator] '{layerName}' 레이어를 인덱스 {i}에 생성했습니다.");
                return i;
            }
        }

        Debug.LogWarning($"[PawnRosterCreator] 빈 유저 레이어가 없어 '{layerName}'을 생성하지 못했습니다.");
        return -1;
    }

    // ── 공통 헬퍼 ────────────────────────────────────────
    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tmp.font = font;
    }
}
