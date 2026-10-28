using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **머리마다 눈·입·코 자리 — 사람이 고치는 표**(2026-09-18, 사용자: *"머리마다 눈 위치 수정이 필요해"*).
//
// 옆선은 그림에서 잰 값(`ChibiHeadProfiles`)이 곧 정답이지만, 눈은 **3D로 옮기면 그림과 다르게 읽힌다**
// — 게임 카메라가 위에서 내려다봐 얼굴 아래쪽이 눌리고, 곡면에 붙인 판은 옆으로 갈수록 좁아 보인다.
// 그래서 **처음 값은 측정에서 채우되 고치는 것은 사람이다.** 여기 적힌 값이 측정보다 이기고,
// 도구는 **없는 머리만 채운다**(있는 줄을 덮어쓰면 손으로 맞춘 자리가 다시 구울 때마다 날아간다).
//
// **고치는 즉시 바뀐다** — 눈·입 메시만 제자리에서 다시 굽는다(`ChibiPawnCreator.RebakeFaces`). 처음엔
// [다시 굽기]를 따로 눌러야 먹어서 *"설정해도 안바껴"*가 됐다. 슬라이더와 결과 사이에 버튼이 끼면 그 버튼은 잊힌다.
// [다시 굽기]는 머리 모양·몸을 바꿨을 때의 길이다(체형 여섯 + 리그).
public class ChibiFaceLayout : ScriptableObject
{
    public const string Path = "Assets/5_Data/Chibi/Chibi_FaceLayout.asset";

    [Serializable]
    public class Entry
    {
        [HideInInspector] public string Head;

        [Tooltip("눈 가운데 높이 — 0 = 턱 끝 · 1 = 정수리")]
        [Range(0.15f, 0.75f)] public float EyeHeight = 0.36f;

        [Tooltip("눈 가운데가 한복판에서 떨어진 거리 ÷ 머리 폭 (두 눈 사이 = 이것의 두 배)")]
        [Range(0.05f, 0.40f)] public float EyeSpacing = 0.20f;

        [Tooltip("눈 지름 ÷ 머리 폭")]
        [Range(0.05f, 0.35f)] public float EyeSize = 0.15f;

        [Tooltip("입 가운데 높이 — 0 = 턱 끝 · 1 = 정수리")]
        [Range(0.05f, 0.50f)] public float MouthHeight = 0.16f;

        [Tooltip("코 밑동 높이 — 0 = 턱 끝 · 1 = 정수리. 0이면 눈과 입 사이로 채운다. **코는 입체라 [다시 굽기]를 눌러야 옮겨진다.**")]
        [Range(0.0f, 0.60f)] public float NoseHeight = 0f;
    }

    // ── 전체에 한꺼번에 — 머리마다의 값 위에 얹는다 ──
    public const float DefaultEyeSizeScale = 0.7f, DefaultEyeAspect = 1.35f;

    [Tooltip("모든 머리의 눈 크기 배율. 측정값(2D 그림의 눈) 그대로면 3D에서 커 보여 0.7로 뒀다.")]
    [Range(0.3f, 1.5f)] public float EyeSizeScale = DefaultEyeSizeScale;

    [Tooltip("눈 가로 ÷ 세로. 1이면 동그랗다. 둥근 머리 옆에 붙으면 옆으로 눌려 세로로 길어 보이므로 조금 가로로 둔다.")]
    [Range(0.6f, 2.0f)] public float EyeAspect = DefaultEyeAspect;

    [Tooltip("모든 머리의 눈 높이에 더하는 값(+ 위 · - 아래).")]
    [Range(-0.15f, 0.15f)] public float EyeHeightOffset = 0f;

    [Tooltip("모든 머리의 눈 간격 배율.")]
    [Range(0.5f, 1.5f)] public float EyeSpacingScale = 1f;

    [Tooltip("모든 머리의 코 크기 배율.")]
    [Range(0.3f, 2.0f)] public float NoseSizeScale = 1f;

    // 코 높이를 안 적은 머리 — 눈과 입 사이(입 쪽에서 55%).
    public static float DefaultNose(Entry e) => Mathf.Lerp(e.MouthHeight, e.EyeHeight, 0.55f);

    public List<Entry> Entries = new();

    // **고치는 즉시 반영한다** — 눈·입 메시만 제자리에서 다시 굽는다(프리팹·폰은 그 메시를 가리킨다).
    void OnValidate() => ChibiPawnCreator.RequestFaceRebake();

    public Entry Of(string head) => Entries.Find(e => e.Head == head);

    // 측정값 — 새 머리를 채울 때와 "측정값으로" 버튼이 쓴다.
    public static Entry Measured(ChibiHeadProfiles.Ref r) => new()
    {
        Head        = r.Name,
        EyeHeight   = r.EyeT,
        EyeSpacing  = r.EyeDx,
        EyeSize     = r.EyeD / r.W,
        MouthHeight = r.EyeT * 0.45f,
        NoseHeight  = Mathf.Lerp(r.EyeT * 0.45f, r.EyeT, 0.55f),
    };

    // 에셋을 찾거나 만들고, **없는 머리만** 측정값으로 채운다.
    public static ChibiFaceLayout LoadOrCreate()
    {
        var layout = AssetDatabase.LoadAssetAtPath<ChibiFaceLayout>(Path);
        if (layout == null)
        {
            string dir = System.IO.Path.GetDirectoryName(Path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(dir).Replace('\\', '/'), System.IO.Path.GetFileName(dir));
            layout = CreateInstance<ChibiFaceLayout>();
            AssetDatabase.CreateAsset(layout, Path);
        }

        bool changed = false;

        // **옛 이름(림월드 이름)의 항목을 새 이름으로 옮긴다**(`ChibiNames`) — 안 옮기면 사람이 맞춘 눈 자리가
        // 조용히 버려지고 새 이름으로 측정값이 새로 채워진다.
        foreach (var e in layout.Entries)
        {
            string now = ChibiNames.Migrate(e.Head);
            if (now != e.Head && layout.Of(now) == null) { e.Head = now; changed = true; }
        }

        foreach (var r in ChibiHeadProfiles.All)
            if (layout.Of(r.Name) == null) { layout.Entries.Add(Measured(r)); changed = true; }

        // **코가 생기기 전에 적은 줄**은 코 높이가 0이다 — 눈과 입 사이로 채운다(손으로 맞춘 눈·입은 그대로).
        foreach (var e in layout.Entries)
            if (e.NoseHeight <= 0f) { e.NoseHeight = DefaultNose(e); changed = true; }
        if (changed) { EditorUtility.SetDirty(layout); AssetDatabase.SaveAssets(); }
        return layout;
    }

    [MenuItem("DungeonColony/Pawn/5. Chibi Face Layout (머리마다 눈 · 입 자리)")]
    static void Open()
    {
        var layout = LoadOrCreate();
        Selection.activeObject = layout;
        EditorGUIUtility.PingObject(layout);
    }
}

[CustomEditor(typeof(ChibiFaceLayout))]
public class ChibiFaceLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var layout = (ChibiFaceLayout)target;
        serializedObject.Update();

        EditorGUILayout.HelpBox("움직이는 즉시 눈 · 입이 바뀐다(씬 · 프리팹 모두). 머리 모양이나 몸을 바꿨을 때만 [다시 굽기].\n" +
                                "높이는 0 = 턱 끝 · 1 = 정수리, 간격 · 크기는 머리 폭에 대한 몫이다.", MessageType.Info);

        EditorGUILayout.LabelField("전체", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("EyeSizeScale"),    new GUIContent("눈 크기 배율"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("EyeAspect"),       new GUIContent("눈 가로 비율"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("EyeHeightOffset"), new GUIContent("눈 높이 보정"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("EyeSpacingScale"), new GUIContent("눈 간격 배율"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("NoseSizeScale"),   new GUIContent("코 크기 배율"));
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("머리마다", EditorStyles.boldLabel);

        var list = serializedObject.FindProperty("Entries");
        for (int i = 0; i < list.arraySize; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            string head = e.FindPropertyRelative("Head").stringValue;
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(head, EditorStyles.boldLabel);
                if (GUILayout.Button("측정값으로", GUILayout.Width(80)))
                {
                    foreach (var r in ChibiHeadProfiles.All)
                    {
                        if (r.Name != head) continue;
                        var m = ChibiFaceLayout.Measured(r);
                        e.FindPropertyRelative("EyeHeight").floatValue   = m.EyeHeight;
                        e.FindPropertyRelative("EyeSpacing").floatValue  = m.EyeSpacing;
                        e.FindPropertyRelative("EyeSize").floatValue     = m.EyeSize;
                        e.FindPropertyRelative("MouthHeight").floatValue = m.MouthHeight;
                        e.FindPropertyRelative("NoseHeight").floatValue  = m.NoseHeight;
                    }
                }
            }
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(e.FindPropertyRelative("EyeHeight"),   new GUIContent("눈 높이"));
            EditorGUILayout.PropertyField(e.FindPropertyRelative("EyeSpacing"),  new GUIContent("눈 간격"));
            EditorGUILayout.PropertyField(e.FindPropertyRelative("EyeSize"),     new GUIContent("눈 크기"));
            EditorGUILayout.PropertyField(e.FindPropertyRelative("MouthHeight"), new GUIContent("입 높이"));
            EditorGUILayout.PropertyField(e.FindPropertyRelative("NoseHeight"),  new GUIContent("코 높이"));
            EditorGUI.indentLevel--;
        }
        if (serializedObject.ApplyModifiedProperties()) ChibiPawnCreator.RequestFaceRebake();

        EditorGUILayout.Space(10);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("다시 굽기 (체형 여섯 + 리그)", GUILayout.Height(28)))
            {
                AssetDatabase.SaveAssets();
                ChibiPawnCreator.Create();
                PawnRigBuilder.BuildRigs();
            }
            if (GUILayout.Button("전부 측정값으로", GUILayout.Width(120), GUILayout.Height(28)))
            {
                Undo.RecordObject(layout, "눈 자리 되돌리기");
                layout.Entries.Clear();
                foreach (var r in ChibiHeadProfiles.All) layout.Entries.Add(ChibiFaceLayout.Measured(r));
                layout.EyeSizeScale = ChibiFaceLayout.DefaultEyeSizeScale; layout.EyeAspect = ChibiFaceLayout.DefaultEyeAspect;
                layout.EyeHeightOffset = 0f; layout.EyeSpacingScale = 1f; layout.NoseSizeScale = 1f;
                EditorUtility.SetDirty(layout);
                ChibiPawnCreator.RequestFaceRebake();
            }
        }
    }
}
