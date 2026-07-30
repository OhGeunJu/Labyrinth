using System.IO;
using UnityEditor;
using UnityEngine;

// DungeonColony → World Map Editor
//
// 월드맵의 **논리 격자**(`WorldMapData`)를 칠해서 저작한다. `WorldMap.mapData`에 연결하면 그대로 실린다.
//
// 여기서 칠하는 건 **지형이 아니다.** 지형(높이·생김새)은 씬에 직접 만들어 붙인 Terrain이고,
// 이 격자는 그 위에 얹히는 논리 레이어다 — 칸마다 바이옴(콘텐츠 게이팅)과 내용물(마을·소굴·자원).
// 그래서 여기 색은 저작자가 구역을 알아보기 위한 표시색이지 게임 화면의 지형색이 아니다.
// 색 정의는 `BiomeExt.Color` / `WorldNodeContentExt.Color` — POI 마커 색과 공유한다.
public class WorldMapEditorWindow : EditorWindow
{
    const string DefaultDir = "Assets/5_Data/World";

    enum Brush { Biome, Content, StartNode }

    WorldMapData _data;

    Brush            _brush        = Brush.Biome;
    Biome            _biomeBrush   = Biome.Plains;
    WorldNodeContent _contentBrush = WorldNodeContent.Town;

    float     _cellSize = 26f;
    Vector2   _scroll;
    Vector2Int _hover = new Vector2Int(-1, -1);

    // 드래그 페인트 1회를 Undo 한 덩어리로 묶기 위한 상태.
    bool _painting;

    [MenuItem("DungeonColony/World Map Editor")]
    static void Open()
    {
        var w = GetWindow<WorldMapEditorWindow>("World Map");
        w.minSize = new Vector2(520f, 480f);
        w.TryAdoptSceneData();
    }

    void OnEnable() => TryAdoptSceneData();

    // 씬에 이미 연결된 월드맵 데이터가 있으면 그걸 연다 — 매번 고르지 않아도 되게.
    void TryAdoptSceneData()
    {
        if (_data != null) return;
        var wm = FindFirstObjectByType<WorldMap>();
        if (wm == null) return;
        var prop = new SerializedObject(wm).FindProperty("mapData");
        if (prop != null) _data = prop.objectReferenceValue as WorldMapData;
    }

    void OnGUI()
    {
        DrawAssetBar();
        if (_data == null)
        {
            EditorGUILayout.HelpBox(
                "편집할 WorldMapData가 없습니다. 위에서 기존 에셋을 고르거나 [새로 만들기]를 누르세요.",
                MessageType.Info);
            return;
        }

        if (!_data.IsSized) _data.Resize(_data.Width, _data.Height);

        DrawSizeBar();
        DrawBrushBar();
        DrawToolBar();
        EditorGUILayout.Space(4f);
        DrawGrid();
        DrawStatus();
    }

    // ── 상단: 에셋 ────────────────────────────────────────────────────────────

    void DrawAssetBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            var next = (WorldMapData)EditorGUILayout.ObjectField(
                _data, typeof(WorldMapData), false, GUILayout.MinWidth(180f));
            if (EditorGUI.EndChangeCheck()) _data = next;

            if (GUILayout.Button("새로 만들기", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                CreateAsset();

            GUI.enabled = _data != null;
            if (GUILayout.Button("씬에 연결", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                AssignToScene();
            GUI.enabled = true;
        }
    }

    void CreateAsset()
    {
        Directory.CreateDirectory(DefaultDir);
        AssetDatabase.Refresh();

        string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultDir}/WorldMapData.asset");
        var so = CreateInstance<WorldMapData>();
        so.Resize(so.Width, so.Height);   // 전부 미지정(Unknown) — 아직 안 칠한 칸이 눈에 띄게
        AssetDatabase.CreateAsset(so, path);
        AssetDatabase.SaveAssets();

        _data = so;
        Selection.activeObject = so;
        Debug.Log($"[WorldMapEditor] 생성: {path}");
    }

    void AssignToScene()
    {
        var wm = FindFirstObjectByType<WorldMap>();
        if (wm == null)
        {
            Debug.LogError("[WorldMapEditor] 씬에 WorldMap이 없습니다 — " +
                           "`DungeonColony → Setup World Map Scene`을 먼저 실행하세요.");
            return;
        }
        var so = new SerializedObject(wm);
        so.FindProperty("mapData").objectReferenceValue = _data;

        // 크기·시작 노드도 함께 맞춘다 — 런타임엔 데이터가 이기지만, 인스펙터 표시와
        // `Setup World Map Scene`의 카메라 프레이밍이 이 필드를 읽으므로 어긋나면 헷갈린다.
        so.FindProperty("width").intValue          = _data.Width;
        so.FindProperty("height").intValue         = _data.Height;
        so.FindProperty("startNode").vector2IntValue = _data.StartNode;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(wm);
        Debug.Log($"[WorldMapEditor] '{_data.name}'({_data.Width}×{_data.Height})을(를) 씬 WorldMap에 " +
                  "연결했습니다. 크기를 크게 바꿨다면 월드 카메라 위치를 다시 맞추세요. 씬 저장 필요.");
    }

    // ── 크기 / 브러시 / 도구 ──────────────────────────────────────────────────

    void DrawSizeBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("크기", GUILayout.Width(30f));
            int w = EditorGUILayout.IntField(_data.Width,  GUILayout.Width(46f));
            EditorGUILayout.LabelField("×", GUILayout.Width(12f));
            int h = EditorGUILayout.IntField(_data.Height, GUILayout.Width(46f));

            if (GUILayout.Button("적용", GUILayout.Width(46f)) && (w != _data.Width || h != _data.Height))
            {
                Undo.RecordObject(_data, "Resize World Map");
                _data.Resize(w, h);   // 좌하단 기준으로 기존 내용 보존
                Save();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("칸 크기", GUILayout.Width(48f));
            _cellSize = EditorGUILayout.Slider(_cellSize, 14f, 44f, GUILayout.Width(140f));
        }
    }

    void DrawBrushBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            _brush = (Brush)GUILayout.Toolbar((int)_brush,
                new[] { "바이옴", "내용물", "시작 노드" }, GUILayout.Width(220f));

            GUILayout.Space(8f);
            switch (_brush)
            {
                case Brush.Biome:
                    _biomeBrush = (Biome)EditorGUILayout.EnumPopup(_biomeBrush, GUILayout.Width(120f));
                    DrawSwatch(_biomeBrush.Color());
                    break;
                case Brush.Content:
                    _contentBrush = (WorldNodeContent)EditorGUILayout.EnumPopup(_contentBrush, GUILayout.Width(120f));
                    DrawSwatch(_contentBrush.Color());
                    break;
                case Brush.StartNode:
                    EditorGUILayout.LabelField($"현재: {_data.StartNode} — 칸을 클릭해 지정",
                                               EditorStyles.miniLabel);
                    break;
            }
            GUILayout.FlexibleSpace();
        }
    }

    static void DrawSwatch(Color c)
    {
        var r = GUILayoutUtility.GetRect(20f, 16f, GUILayout.Width(20f));
        EditorGUI.DrawRect(r, c);
    }

    void DrawToolBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("전체 채우기", GUILayout.Width(90f)))
            {
                Undo.RecordObject(_data, "Fill World Map");
                _data.Fill(_biomeBrush);
                Save();
            }
            if (GUILayout.Button("내용물 지우기", GUILayout.Width(100f)))
            {
                Undo.RecordObject(_data, "Clear World Map Content");
                _data.ClearContent();
                Save();
            }
            GUILayout.FlexibleSpace();
        }
    }

    // ── 격자 ─────────────────────────────────────────────────────────────────

    void DrawGrid()
    {
        float w = _data.Width  * _cellSize;
        float h = _data.Height * _cellSize;

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        var area = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

        var symbolStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.black },
        };

        for (int y = 0; y < _data.Height; y++)
        for (int x = 0; x < _data.Width;  x++)
        {
            var cell = CellRect(area, x, y);
            var node = _data.Get(x, y);

            EditorGUI.DrawRect(cell, node.Biome.Color());

            if (node.Content != WorldNodeContent.Empty)
            {
                // 내용물은 안쪽 사각형 + 한 글자 — 바이옴 색을 덮지 않고 겹쳐 보이게.
                var inner = new Rect(cell.x + cell.width * 0.22f, cell.y + cell.height * 0.22f,
                                     cell.width * 0.56f, cell.height * 0.56f);
                EditorGUI.DrawRect(inner, node.Content.Color());
                if (_cellSize >= 20f) GUI.Label(cell, node.Content.Symbol(), symbolStyle);
            }

            if (new Vector2Int(x, y) == _data.StartNode) DrawBorder(cell, Color.white, 2f);
            if (new Vector2Int(x, y) == _hover)          DrawBorder(cell, new Color(1f, 1f, 1f, 0.5f), 1f);
        }

        HandleGridInput(area);
        EditorGUILayout.EndScrollView();
    }

    // 게임 좌표는 y가 위로, GUI는 아래로 간다 → 행을 뒤집어 그린다.
    Rect CellRect(Rect area, int x, int y) =>
        new Rect(area.x + x * _cellSize,
                 area.y + (_data.Height - 1 - y) * _cellSize,
                 _cellSize - 1f, _cellSize - 1f);

    static void DrawBorder(Rect r, Color c, float t)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    void HandleGridInput(Rect area)
    {
        var e = Event.current;
        if (!area.Contains(e.mousePosition))
        {
            if (_hover.x >= 0) { _hover = new Vector2Int(-1, -1); Repaint(); }
            return;
        }

        int x = Mathf.FloorToInt((e.mousePosition.x - area.x) / _cellSize);
        int y = _data.Height - 1 - Mathf.FloorToInt((e.mousePosition.y - area.y) / _cellSize);
        if (!_data.InBounds(x, y)) return;

        var cell = new Vector2Int(x, y);
        if (cell != _hover) { _hover = cell; Repaint(); }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            // 드래그 한 번 = Undo 한 덩어리.
            Undo.RecordObject(_data, "Paint World Map");
            _painting = true;
            Paint(x, y);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && _painting)
        {
            Paint(x, y);
            e.Use();
        }
        else if (e.type == EventType.MouseUp && _painting)
        {
            _painting = false;
            Save();
            e.Use();
        }
    }

    void Paint(int x, int y)
    {
        switch (_brush)
        {
            case Brush.Biome:
            {
                var n = _data.Get(x, y); n.Biome = _biomeBrush; _data.Set(x, y, n);
                break;
            }
            case Brush.Content:
            {
                var n = _data.Get(x, y); n.Content = _contentBrush; _data.Set(x, y, n);
                break;
            }
            case Brush.StartNode:
                _data.StartNode = new Vector2Int(x, y);
                break;
        }
        EditorUtility.SetDirty(_data);
        Repaint();
    }

    // ── 하단 ─────────────────────────────────────────────────────────────────

    void DrawStatus()
    {
        string cell = "—";
        if (_data.InBounds(_hover.x, _hover.y))
        {
            var n = _data.Get(_hover.x, _hover.y);
            cell = $"({_hover.x}, {_hover.y})  {n.Biome.DisplayName()} / {n.Content.DisplayName()}";
        }
        EditorGUILayout.LabelField(cell, EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            "흰 테두리 = 시작 노드(런타임에 반드시 '내 정착지') · " +
            "높이·생김새는 씬에 만든 지형이 갖는다 — 여기서 칠하는 건 논리 정보뿐",
            EditorStyles.miniLabel);
    }

    void Save()
    {
        EditorUtility.SetDirty(_data);
        AssetDatabase.SaveAssetIfDirty(_data);
        Repaint();
    }
}
