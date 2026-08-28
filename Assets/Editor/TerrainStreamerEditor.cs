using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// **"여기에 산"의 *여기*를 화면에 놓는다**(2026-08-27).
//
// `TerrainFeature`는 좌표로 저작하므로 저장할 것이 없다 — 그건 옳다. 문제는 **저작하는 손**이
// 인스펙터의 `Center (X, Y)` 칸이었다는 것이다. 산 하나를 옮기려면 숫자를 찍고, 지형을 다시
// 띄워 보고, 다시 찍는다. 세계가 16,384m라 그 왕복이 저작을 사실상 막았다.
//
// **그릇은 그대로 두고 손잡이만 붙인다** — 데이터는 여전히 `TerrainStreamer.features` 배열
// 하나이고, 여기는 그것을 씬에서 잡을 수 있게 할 뿐이다. `Bake Delta`도 `Place Sites`도
// 아무것도 안 바뀐다.
//
// 쓰는 법:
//   1. `DungeonColony → Terrain → A. Show Terrain Here` 로 땅을 띄운다(없어도 되지만 있으면 보인다)
//   2. 씬에서 `TerrainStreamer`가 붙은 오브젝트를 고른다 (`D. Edit Mountains`가 골라 준다)
//   3. 구를 끌어 옮기고 · 화살표로 높이 · 상자로 반경과 고원을 잡는다
//   4. 손을 떼면 미리보기가 그 자리에서 다시 그려진다
[CustomEditor(typeof(TerrainStreamer))]
public class TerrainStreamerEditor : Editor
{
    SerializedProperty _features;

    // **한 번에 하나만 잡는다.** 손잡이를 전부 띄우면 산 열 개에 손잡이 마흔 개가 겹쳐
    // 무엇을 집었는지 알 수 없다 — 나머지는 윤곽과 점만 그리고, 점을 누르면 그것이 잡힌다.
    int _active = -1;

    bool _autoRefresh = true;
    bool _dirty;                       // 이번 드래그에서 값이 바뀌었나

    // **끄는 동안 바닥 높이를 얼린다.** 옮기는 손잡이는 XZ 평면 위에서 마우스 광선과 만나
    // 자리를 잡는데, 그 평면을 *지금 지면 높이*에 두면 **경사에서 되먹임이 생긴다** —
    // 조금 옮기면 땅이 높아지고, 평면이 올라가면 교점이 더 밀리고, 다시 더 높아진다.
    // 잡은 순간의 높이에 묶어 두면 끄는 동안 평면이 가만히 있는다.
    float _dragY;

    static readonly Color Mountain = new(1f,   0.55f, 0.15f);
    static readonly Color Basin    = new(0.3f, 0.65f, 1f);

    // 밑동에서 꼭대기로 긋는 옆모습 네 줄 — 원 둘만 그리면 높이가 화면에 안 보인다.
    static readonly Vector3[] Skirt =
        { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

    static GUIStyle _label;

    void OnEnable() => _features = serializedObject.FindProperty("features");

    // ── 인스펙터 ────────────────────────────────────────────────────────────

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("산·분지 — 씬에서 저작", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "이 오브젝트를 고른 채로 씬 뷰에서 잡는다. 땅이 안 보이면 " +
                "DungeonColony → Terrain → A. Show Terrain Here.",
                EditorStyles.wordWrappedMiniLabel);

            serializedObject.Update();
            if (_active >= _features.arraySize) _active = -1;

            for (int i = 0; i < _features.arraySize; i++)
            {
                var f = _features.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool on   = i == _active;
                    bool next = GUILayout.Toggle(on, Summary(f, i), EditorStyles.miniButton);
                    if (next != on) { _active = next ? i : -1; SceneView.RepaintAll(); }

                    if (GUILayout.Button("보기", EditorStyles.miniButton, GUILayout.Width(38f)))
                    { _active = i; Frame(f); }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 산"))   Add(120f);
                if (GUILayout.Button("+ 분지")) Add(-45f);
                using (new EditorGUI.DisabledScope(_active < 0))
                    if (GUILayout.Button("지우기")) Delete(_active);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _autoRefresh = GUILayout.Toggle(_autoRefresh, "손 떼면 미리보기 갱신", EditorStyles.miniButton);
                if (GUILayout.Button("지금 갱신", EditorStyles.miniButton)) Refresh();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    string Summary(SerializedProperty f, int i)
    {
        string name = f.FindPropertyRelative("Name").stringValue;
        if (string.IsNullOrEmpty(name)) name = "(이름 없음) " + i;

        var c = f.FindPropertyRelative("Center").vector2Value;
        int h = Mathf.RoundToInt(f.FindPropertyRelative("Height").floatValue);
        int r = Mathf.RoundToInt(f.FindPropertyRelative("Radius").floatValue);
        return name + "   " + h + "m · 반경 " + r + "m ("
                    + Mathf.RoundToInt(c.x) + ", " + Mathf.RoundToInt(c.y) + ")";
    }

    // ── 씬 ──────────────────────────────────────────────────────────────────

    void OnSceneGUI()
    {
        var streamer = (TerrainStreamer)target;

        // **값만 넣는다 — 캐시는 안 비운다.** 여기는 프레임마다 도는 자리라, 델타를 비우면
        // 손잡이를 그릴 때마다 `Resources`를 다시 뒤진다.
        TerrainHeight.Profile  = streamer.Profile;
        TerrainHeight.Features = streamer.Features;

        serializedObject.Update();
        if (_active >= _features.arraySize) _active = -1;

        // **지형에 가려도 보여야 한다** — 산 반대편 사면에 선 손잡이가 안 보이면 그쪽은
        // 카메라를 돌려야만 만질 수 있다.
        var zt = Handles.zTest;
        Handles.zTest = CompareFunction.Always;

        EditorGUI.BeginChangeCheck();
        for (int i = 0; i < _features.arraySize; i++) DrawFeature(i);
        if (EditorGUI.EndChangeCheck()) _dirty = true;

        Handles.zTest = zt;
        serializedObject.ApplyModifiedProperties();

        DrawOverlay();

        // **손을 뗄 때 다시 그린다** — 드래그 중에 매 프레임 다시 채우면 청크 아홉 개가
        // 14만 정점이라 손잡이가 끈적해진다.
        if (_dirty && Event.current.type == EventType.MouseUp)
        {
            _dirty = false;
            if (_autoRefresh) TerrainAuthoring.RefreshPreview();
        }
    }

    void DrawFeature(int i)
    {
        var p  = _features.GetArrayElementAtIndex(i);
        var pc = p.FindPropertyRelative("Center");
        var pr = p.FindPropertyRelative("Radius");
        var ph = p.FindPropertyRelative("Height");
        var pp = p.FindPropertyRelative("Plateau");

        Vector2 c       = pc.vector2Value;
        float   radius  = Mathf.Max(1f, pr.floatValue);
        float   height  = ph.floatValue;
        float   plateau = Mathf.Clamp(pp.floatValue, 0f, 0.95f);

        // **이 특징을 뺀 땅 높이**가 밑동이다 — `Contribution`이 그 답을 이미 알고 있다.
        // 빼지 않으면 원이 꼭대기에 붙어 산의 높이가 화면에 안 보인다.
        var f = new TerrainFeature
        {
            Center = c, Radius = radius, Height = height, Plateau = plateau,
            Ridge       = p.FindPropertyRelative("Ridge").floatValue,
            RidgePeriod = p.FindPropertyRelative("RidgePeriod").floatValue,
        };
        float top    = TerrainHeight.Procedural(c.x, c.y);
        float floorY = top - f.Contribution(c.x, c.y);

        var basePos = new Vector3(c.x, floorY, c.y);
        var topPos  = new Vector3(c.x, top,    c.y);

        bool  active = i == _active;
        Color col    = height >= 0f ? Mountain : Basin;
        Handles.color = active ? col : col * 0.6f;

        Handles.DrawWireDisc(basePos, Vector3.up, radius);
        if (plateau > 0.01f) Handles.DrawWireDisc(topPos, Vector3.up, radius * plateau);
        foreach (var d in Skirt) Handles.DrawLine(basePos + d * radius, topPos + d * (radius * plateau));

        Handles.Label(topPos + Vector3.up * (HandleUtility.GetHandleSize(topPos) * 0.4f),
                      Summary(p, i), Label(active));

        if (!active)
        {
            // 안 고른 것은 점 하나 — **누르면 잡힌다.** 목록으로 돌아가지 않아도 되게.
            float s = HandleUtility.GetHandleSize(topPos) * 0.06f;
            if (Handles.Button(topPos, Quaternion.identity, s, s * 2f, Handles.DotHandleCap))
            { _active = i; Repaint(); }
            return;
        }

        float size = HandleUtility.GetHandleSize(topPos);

        // 옮기기 — XZ 평면. **Y는 안 받는다**(`Center`가 Vector2다. 세로로 끌 수 있게 두면
        // 화면에서는 움직이는데 값이 안 바뀌어, 놓는 순간 제자리로 튄다).
        if (GUIUtility.hotControl == 0) _dragY = topPos.y;   // 안 끄는 동안에는 지면을 따라간다
        var moveAt = new Vector3(topPos.x, _dragY, topPos.z);

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider2D(moveAt, Vector3.up, Vector3.right, Vector3.forward,
                                         size * 0.13f, Handles.SphereHandleCap, 0f);
        if (EditorGUI.EndChangeCheck()) pc.vector2Value = new Vector2(moved.x, moved.z);

        // 높이 — 세로 화살표. 구와 안 겹치게 조금 띄운다.
        Vector3 hAt = topPos + Vector3.up * (size * 0.45f);
        EditorGUI.BeginChangeCheck();
        Vector3 hTo = Handles.Slider(hAt, Vector3.up, size * 0.16f, Handles.ArrowHandleCap, 0f);
        if (EditorGUI.EndChangeCheck()) ph.floatValue = height + (hTo.y - hAt.y);

        // 반경 — 밑동 원의 +X 쪽 상자.
        Vector3 rimAt = basePos + Vector3.right * radius;
        EditorGUI.BeginChangeCheck();
        Vector3 rimTo = Handles.Slider(rimAt, Vector3.right,
                                       HandleUtility.GetHandleSize(rimAt) * 0.07f,
                                       Handles.CubeHandleCap, 0f);
        if (EditorGUI.EndChangeCheck()) pr.floatValue = Mathf.Max(1f, rimTo.x - basePos.x);

        // 고원 — 꼭대기 원의 +Z 쪽 상자. **비율이라 반경으로 나눈다.**
        Vector3 plAt = topPos + Vector3.forward * (radius * plateau);
        EditorGUI.BeginChangeCheck();
        Vector3 plTo = Handles.Slider(plAt, Vector3.forward,
                                      HandleUtility.GetHandleSize(plAt) * 0.06f,
                                      Handles.CubeHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
            pp.floatValue = Mathf.Clamp((plTo.z - topPos.z) / radius, 0f, 0.95f);
    }

    void DrawOverlay()
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(8f, 28f, 214f, 92f), GUI.skin.box);

        GUILayout.Label(_active >= 0 && _active < _features.arraySize
                            ? "잡은 것 — " + Summary(_features.GetArrayElementAtIndex(_active), _active)
                            : "산·분지 " + _features.arraySize + "개 — 점을 눌러 잡는다",
                        EditorStyles.wordWrappedMiniLabel);

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ 산"))   Add(120f);
            if (GUILayout.Button("+ 분지")) Add(-45f);
            using (new EditorGUI.DisabledScope(_active < 0))
                if (GUILayout.Button("지우기")) Delete(_active);
        }

        using (new GUILayout.HorizontalScope())
        {
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "자동 갱신", EditorStyles.miniButton);
            if (GUILayout.Button("지금 갱신", EditorStyles.miniButton)) Refresh();
        }

        GUILayout.EndArea();
        Handles.EndGUI();
    }

    // ── 손질 ────────────────────────────────────────────────────────────────

    // **씬 뷰가 보는 자리에 낸다** — 좌표를 물어보면 다시 숫자 타이핑이 된다.
    void Add(float height)
    {
        var view   = SceneView.lastActiveSceneView;
        Vector3 at = view != null ? view.pivot : Vector3.zero;

        if (!TerrainGrid.InWorld(TerrainGrid.ChunkAt(at)))
        {
            Debug.LogWarning("[지형] 씬 뷰가 보는 곳 (" + Mathf.RoundToInt(at.x) + ", " +
                             Mathf.RoundToInt(at.z) + ")은 세계 밖이다 (0 ~ " +
                             TerrainGrid.WorldSize + "m).");
            return;
        }

        serializedObject.Update();
        int i = _features.arraySize;
        _features.arraySize = i + 1;

        var p = _features.GetArrayElementAtIndex(i);
        p.FindPropertyRelative("Name").stringValue       = height >= 0f ? "산" : "분지";
        p.FindPropertyRelative("Center").vector2Value    = new Vector2(at.x, at.z);
        p.FindPropertyRelative("Radius").floatValue      = 600f;
        p.FindPropertyRelative("Height").floatValue      = height;
        p.FindPropertyRelative("Plateau").floatValue     = 0.25f;
        p.FindPropertyRelative("Ridge").floatValue       = height >= 0f ? 15f : 0f;
        p.FindPropertyRelative("RidgePeriod").floatValue = 180f;
        serializedObject.ApplyModifiedProperties();

        _active = i;
        Refresh();
        SceneView.RepaintAll();
    }

    void Delete(int i)
    {
        if (i < 0 || i >= _features.arraySize) return;

        serializedObject.Update();
        _features.DeleteArrayElementAtIndex(i);
        serializedObject.ApplyModifiedProperties();

        _active = -1;
        Refresh();
        SceneView.RepaintAll();
    }

    void Refresh()
    {
        if (TerrainAuthoring.RefreshPreview() == 0)
            Debug.Log("[지형] 띄워 둔 미리보기가 없다 — `E. World Preview`(세계 한 장) 또는 " +
                      "`A. Show Terrain Here (3x3)`로 땅을 띄우면 산이 화면에서 자란다.");
    }

    void Frame(SerializedProperty f)
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) return;

        var c = f.FindPropertyRelative("Center").vector2Value;
        float r = Mathf.Max(1f, f.FindPropertyRelative("Radius").floatValue);
        view.LookAt(new Vector3(c.x, TerrainHeight.Procedural(c.x, c.y), c.y),
                    view.rotation, r * 1.6f);
    }

    static GUIStyle Label(bool active)
    {
        _label ??= new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
        _label.normal.textColor = active ? Color.white : new Color(0.75f, 0.75f, 0.75f);
        return _label;
    }

    // ── 들어가는 문 ─────────────────────────────────────────────────────────
    //
    // **메뉴가 이 저장소의 저작 입구다**(0~8 · A~C). 손잡이는 오브젝트를 골라야 뜨는데,
    // `TerrainStreamer`가 씬 어디에 있는지는 아무 데도 안 적혀 있다.
    [MenuItem("DungeonColony/Terrain/D. Edit Mountains (산·분지)")]
    static void Select()
    {
        var s = Object.FindFirstObjectByType<TerrainStreamer>();
        if (s == null)
        {
            Debug.LogWarning("[지형] 씬에 TerrainStreamer가 없다 — `0. Setup World Streaming` 먼저.");
            return;
        }

        Selection.activeGameObject = s.gameObject;
        Debug.Log("[지형] 산·분지 " + s.Features.Length + "개 — 씬 뷰에서 잡는다. " +
                  "땅이 안 보이면 `A. Show Terrain Here (3x3)`.");
    }
}
