using UnityEditor;
using UnityEngine;

// **뿌리기 규칙을 씬에 쓰는 문 — 여기 하나다**(2026-09-14).
//
// `ScatterRule`은 `WorldContentStreamer`의 직렬화 배열이라 **씬이 값을 든다** — 코드 기본값은
// 아무 힘이 없다(이 저장소가 `EventManager.checkInterval`에서 한 번 크게 겪은 규칙). 그래서
// 콘텐츠 도구가 씬에 써 넣는데, **쓰는 도구가 둘이 됐다**: 버섯·바닥 돌(`MushroomCreator`)과
// 바닥 광맥(`MetalCreator`).
//
// 각자 적으면 그중 하나는 반드시 뒤처진다 — 실제로 `MushroomCreator` 안의 `Rule`에는
// *"지하 높이에서 전부 걸러지지 않게 고도 범위를 넓게 쓴다"* 같은 **안 적으면 조용히 아무것도
// 안 나는** 규칙이 서너 개 박혀 있었다. 새 도구가 그걸 모르면 **에셋은 멀쩡한데 세계에는
// 아무것도 없는** 상태가 되고, 그 원인은 화면에서 안 보인다.
//
// ## 어기면 조용히 틀리는 것
//
// - **이름으로 찾아 값만 맞춘다** — 그냥 붙이면 도구를 다시 돌릴 때마다 규칙이 하나씩 늘어
//   버섯이 두 배, 세 배로 난다(`5. Place Sites`가 신원으로 맞추는 것과 같은 이유).
// - **고도·경사를 반드시 적는다** — 코드 기본값은 **지상 것**이라(설선 400m) 그대로 두면
//   지하 높이에서 전부 걸러져 **하나도 안 난다.** 지하에서 거르는 것은 고도가 아니라 바위다.
// - **`RequireOpenTile`을 켠다** — 끄면 바위 속에서 자란다.
public static class ScatterWiring
{
    // 씬의 `WorldContentStreamer`를 열어 규칙 배열을 준다. **없으면 말한다** — 에셋만
    // 만들어 두고 아무것도 안 나면 원인이 코드가 아니라 배선에 있고, 그건 화면에서 절대
    // 안 보인다.
    public static bool Open(string tag, out SerializedObject so, out SerializedProperty rules)
    {
        so = null; rules = null;

        var streamer = Object.FindFirstObjectByType<WorldContentStreamer>(FindObjectsInactive.Include);
        if (streamer == null)
        {
            Debug.LogWarning($"[{tag}] 씬에 `WorldContentStreamer`가 없습니다 — 에셋만 만들었고 " +
                             "뿌리기는 안 걸렸습니다. 판(씬)을 열고 다시 돌리세요.");
            return false;
        }

        so    = new SerializedObject(streamer);
        rules = so.FindProperty("scatter");
        return true;
    }

    // **씬을 더럽힌다 — 저장은 사람이 한다.** 여기서 `SaveScene`을 부르면 도구 하나가
    // 사람이 열어 둔 판을 제멋대로 덮어쓴다.
    public static void Commit(SerializedObject so)
    {
        so.ApplyModifiedProperties();

        var target = (Component)so.targetObject;
        EditorUtility.SetDirty(target);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
    }

    // 규칙 하나를 쓴다(없으면 만들고, 있으면 값만 맞춘다).
    //
    // `fromBiomePool`을 켜면 `prefab`·`biomes`를 안 본다 — 그 자리 바이옴의 자원 노드 풀이
    // 둘 다 답한다(`ScatterRule.FromBiomePool`).
    public static void Set(SerializedProperty rules, string name, GameObject prefab,
                           float perChunk, float spacing, float regrowDays, Biome[] biomes,
                           (int min, int max) cluster, int spread, bool fromBiomePool = false)
    {
        SerializedProperty e = null;
        for (int i = 0; i < rules.arraySize; i++)
        {
            var it = rules.GetArrayElementAtIndex(i);
            if (it.FindPropertyRelative("Name").stringValue == name) { e = it; break; }
        }
        if (e == null)
        {
            rules.InsertArrayElementAtIndex(rules.arraySize);
            e = rules.GetArrayElementAtIndex(rules.arraySize - 1);
        }

        e.FindPropertyRelative("Name").stringValue            = name;
        e.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        e.FindPropertyRelative("FromBiomePool").boolValue     = fromBiomePool;
        e.FindPropertyRelative("PerChunk").floatValue         = perChunk;
        e.FindPropertyRelative("MinSpacing").floatValue       = spacing;
        e.FindPropertyRelative("RegrowDays").floatValue       = regrowDays;
        e.FindPropertyRelative("RequireOpenTile").boolValue   = true;

        // 지하 바닥은 거의 평지라 경사·고도는 사실상 아무것도 안 거른다 — **거르는 것은
        // 바위다.** 그래도 값을 비워 두지 않는 이유: 코드 기본값(설선 400m)이 남아 있으면
        // 지하 높이에서 **전부 걸러져 하나도 안 난다.**
        e.FindPropertyRelative("MaxSlope").floatValue      = 35f;
        e.FindPropertyRelative("MinHeight").floatValue     = -1000f;
        e.FindPropertyRelative("MaxHeight").floatValue     =  1000f;
        e.FindPropertyRelative("ScaleRange").vector2Value  = new Vector2(0.85f, 1.25f);

        e.FindPropertyRelative("ClusterMin").intValue      = cluster.min;
        e.FindPropertyRelative("ClusterMax").intValue      = cluster.max;
        e.FindPropertyRelative("ClusterSpread").intValue   = spread;

        var arr = e.FindPropertyRelative("Biomes");
        arr.arraySize = biomes != null ? biomes.Length : 0;
        for (int i = 0; i < arr.arraySize; i++)
            arr.GetArrayElementAtIndex(i).enumValueIndex = (int)biomes[i];
    }
}
