using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// **턱 · 머리를 인스펙터에서 넘겨 본다**
// (2026-09-18, 사용자: *"인스펙터로 바꿀 수 있게 만들자"*).
//
// 구운 벌은 프리팹 안에 전부 들어 있고 `PawnLook`이 이름으로 묶는다 — 여기는 그 목록을
// 드롭다운으로 내고, 고르면 **`Override`에 적고 바로 켠다.** 그래서 씬 뷰에서 즉시 보이고,
// 프리팹 모드에서 고르면 그 프리팹의 저작이 된다(비우면 다시 씨앗이 고른다).
//
// **되돌리기가 먹는다** — 렌더러 켜고 끄기까지 계층 전체를 기록한다. 안 그러면 Ctrl+Z가
// 문자열만 되돌리고 얼굴은 그대로라 *"되돌렸는데 안 바뀐다"*가 된다.
[CustomEditor(typeof(PawnLook))]
public class PawnLookEditor : Editor
{
    const string Seeded = "(씨앗이 고른다)";

    public override void OnInspectorGUI()
    {
        var look = (PawnLook)target;

        // 처음 열 때 목록을 채운다 — 플레이 중이 아니면 `Start`가 안 돌아 비어 있다.
        if (!look.ShapeNames.Any() && !look.PickAxes.Any()) look.Regather();

        // **해골처럼 고르는 축으로 된 머리**(`Head.Pick.*`) — 사람 머리 줄은 비어 있으니 축 줄만 낸다.
        if (look.PickAxes.Any())
        {
            EditorGUILayout.LabelField("고르는 축 — 비우면 씨앗이 고른다", EditorStyles.boldLabel);
            foreach (var axis in look.PickAxes.ToList())
            {
                var a = axis;
                Row($"{a} ({look.Picked(a)})", look.PickOptions(a).ToList(), look.PickOverride(a), v => look.SetPickOverride(a, v), look);
            }
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전부 씨앗으로")) Change(look, look.ClearPickOverrides);
                if (GUILayout.Button("다시 모으기"))   Change(look, look.Regather);
            }
            EditorGUILayout.HelpBox("축마다 씨앗(명세 Seed · 없으면 이름 해시)이 따로 고른다. `@축.벌`이 붙은 벌은 그 축이 맞을 때만 켜진다" +
                                    "(이는 턱이 있을 때만). 뼈 색 · 눈구멍 속은 `BoneLook`.", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("지금", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("머리형", look.Shape);
            EditorGUILayout.TextField("머리",   look.Hair);
            EditorGUILayout.TextField("귀",     look.Ear);
            EditorGUILayout.TextField("코",     look.Nose);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("못 박기 — 비우면 씨앗이 고른다", EditorStyles.boldLabel);

        Row("머리형", look.ShapeNames.ToList(), look.ShapeOverride, v => look.ShapeOverride = v, look);

        var hairs = look.HairNames.ToList();
        hairs.Insert(0, PawnLook.Bald);
        Row("머리", hairs, look.HairOverride, v => look.HairOverride = v, look);

        var ears = look.EarNames.ToList();
        ears.Insert(0, PawnLook.None);
        Row("귀", ears, look.EarOverride, v => look.EarOverride = v, look, "(일반 귀)");

        var noses = look.NoseNames.ToList();
        Row("코", noses, look.NoseOverride, v => look.NoseOverride = v, look, "(없음)");

        // 이목구비 — 아틀라스의 이름을 그대로 드롭다운에 낸다. 첫 칸은 "기본", 둘째는 "없음".
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("이목구비 — 비우면 기본", EditorStyles.boldLabel);
        // **눈 줄은 눈 아틀라스만, 입 줄은 입 아틀라스만** 낸다 — 한 목록에 섞으면 눈 자리에 입이 들어간다.
        foreach (var (feature, label) in new[] { ("EyeL", "왼눈"), ("EyeR", "오른눈"), ("Mouth", "입") })
        {
            var atlas = look.FaceAtlasOf(feature);
            if (atlas == null)
            {
                EditorGUILayout.LabelField(label, "아틀라스가 없다 — `Pawn/3. Create Chibi Pawns`로 다시 세울 것");
                continue;
            }
            var names = new List<string> { PawnLook.None };
            names.AddRange(atlas.Names);
            var f = feature;
            Row(label, names, look.FaceOverride(f), v => look.SetFaceOverride(f, v), look, "(기본)");
        }


        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("전부 씨앗으로"))
                Change(look, () =>
                {
                    look.ShapeOverride = ""; look.HairOverride = ""; look.EarOverride = ""; look.NoseOverride = "";
                    foreach (var f in PawnLook.FaceFeatures) look.SetFaceOverride(f, "");
                });
            if (GUILayout.Button("다시 모으기"))
                Change(look, look.Regather);
        }

        EditorGUILayout.HelpBox(
            "턱·머리는 씨앗(명세 Seed · 없으면 이름 해시)이 고른다. 여기서 고른 것은 그보다 " +
            "앞선다 — 이 사람은 이 얼굴이다. 눈·입은 각자의 아틀라스 칸이고(2_Material/Chibi/Chibi_EyesAtlas · MouthsAtlas), " +
            "표정은 코드가 `SetFace`로 잠깐 얹는다(여기서 고른 것보다 앞선다).",
            MessageType.None);
    }

    // 드롭다운 + 이전/다음. 목록이 비어 있으면(치비 머리가 아닌 폰) 말만 남긴다.
    static void Row(string label, List<string> names, string current, System.Action<string> set, PawnLook look,
                    string unsetLabel = Seeded)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (names.Count == 0)
            {
                EditorGUILayout.LabelField(label, "구운 벌이 없다 — `Pawn/3. Create Chibi Pawns`로 세운 폰이 아니다");
                return;
            }

            var options = new List<string> { unsetLabel };
            options.AddRange(names);
            int idx = string.IsNullOrEmpty(current) ? 0 : Mathf.Max(0, names.IndexOf(current) + 1);

            int picked = EditorGUILayout.Popup(label, idx, options.ToArray());
            if (picked != idx) Change(look, () => set(picked == 0 ? "" : names[picked - 1]));

            // 이전/다음 — 씨앗 자리는 건너뛰고 벌만 돈다(눌러 가며 견주는 용도라).
            if (GUILayout.Button("<", GUILayout.Width(24)))
            {
                int at = idx == 0 ? names.Count - 1 : (idx - 2 + names.Count) % names.Count;
                Change(look, () => set(names[at]));
            }
            if (GUILayout.Button(">", GUILayout.Width(24)))
            {
                int at = idx == 0 ? 0 : idx % names.Count;
                Change(look, () => set(names[at]));
            }
        }
    }

    // **계층 전체를 기록한다** — 바뀌는 것이 문자열 하나가 아니라 렌더러 수십 개의 켜짐이다.
    static void Change(PawnLook look, System.Action act)
    {
        Undo.RegisterFullObjectHierarchyUndo(look.gameObject, "폰 얼굴 바꾸기");
        act();
        EditorUtility.SetDirty(look);
        foreach (var r in look.GetComponentsInChildren<Renderer>(true)) { EditorUtility.SetDirty(r); EditorUtility.SetDirty(r.transform); }
        foreach (var p in look.GetComponentsInChildren<ChibiFacePart>(true)) EditorUtility.SetDirty(p);
        if (!Application.isPlaying)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(look);
            foreach (var r in look.GetComponentsInChildren<Renderer>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(r.transform);
        }
        SceneView.RepaintAll();
    }
}
