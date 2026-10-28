using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **씬에 손으로 놓은 것을 지금 지면에 다시 앉힌다**(2026-09-24).
//
// `6. Resnap Sites To Terrain`의 **형제이고 다른 물음에 답한다**:
//
//   · `6. Resnap` — **저작 씬의 장소**(마을·소굴). 신원이 있고 **구운 오프셋이 기록으로 남아**
//     있어서 *"저작자가 얼마나 띄웠나"*를 되읽을 수 있다.
//   · 여기 — **게임 씬에 손으로 놓은 아무 오브젝트**. 기록이 없다. 그래서 되읽을 것이 없고,
//     기준을 **지금 보이는 몸**에서 낸다.
//
// ── 기준은 밑면이다 (두 번 돌려도 같아야 한다) ─────────────────────────────
//
// 처음엔 *"지금 y를 지면 위 높이로 보고 유지"*로 두려 했다 — 바닥이 여태 **전부 0**이었으니
// 이번 한 번은 정확하다. 그런데 그건 **한 번만 맞는 도구**다: 두 번 돌리면 높이가 두 배로
// 올라가고, 그 사실이 화면에 안 적힌다. **일회성 이행을 상시 도구에 남기면 그것이 규칙이 된다**
// (`0d. Swap Wood Costs To Stone`이 묶음에 남아 건물의 나무를 매번 돌로 되돌리던 그 사고).
//
// 그래서 **몸의 밑면을 지면에 맞춘다** — 렌더러·콜라이더를 합친 경계의 아래쪽이다. 두 번
// 돌려도 답이 같고, 피벗이 어디든(발밑이든 몸 한가운데든) 맞는다. 일부러 띄워 둔 것은
// **고르는 쪽에서 빼면 된다** — 그래서 기본이 *고른 것만*이다.
//
// ── 무엇을 안 건드리나 ─────────────────────────────────────────────────────
//
// **렌더러도 콜라이더도 없는 것은 몸이 없다** — 매니저·시스템 오브젝트가 전부 그렇고, 그것들을
// 움직이면 아무 그림도 안 바뀌면서 씬만 더러워진다. 카메라·캔버스·조명은 **자리의 뜻이 다르고**
// (화면의 것이다), `DontSave`는 **에디터가 세운 지형·바위**다(다음 틱에 다시 선다).
public class SnapToGround : EditorWindow
{
    enum Scope { Selected, WholeScene }

    Scope _scope = Scope.Selected;
    bool  _dryRun;
    bool  _includeChildren;   // 고른 것의 자식 중 몸을 가진 것까지

    [MenuItem("DungeonColony/World/6b. Snap Scene Objects To Ground (씬 오브젝트 앉히기)")]
    static void Open()
    {
        var w = GetWindow<SnapToGround>("Snap To Ground");
        w.minSize = new Vector2(400f, 280f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("씬 오브젝트를 지금 지면에 앉힌다", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "**몸의 밑면**을 그 자리 지면에 맞춘다(XZ는 안 건드린다). 밑면 기준이라 " +
            "두 번 돌려도 답이 같다 — 대신 일부러 띄워 둔 것은 내려앉으므로 고르는 쪽에서 뺄 것.",
            EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(6f);
        _scope = (Scope)EditorGUILayout.EnumPopup("대상", _scope);
        if (_scope == Scope.Selected)
        {
            EditorGUILayout.LabelField($"    고른 것 {Selection.gameObjects.Length}개",
                                       EditorStyles.miniLabel);
            _includeChildren = EditorGUILayout.ToggleLeft(
                "고른 것의 자식까지 (묶음을 골랐을 때)", _includeChildren);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "씬의 **최상위** 오브젝트를 훑는다. 몸(렌더러·콜라이더)이 없는 것 · 카메라 · " +
                "캔버스 · 조명 · 저작 루트 · 에디터가 세운 것은 건너뛴다.", MessageType.Info);
        }

        EditorGUILayout.Space(4f);
        _dryRun = EditorGUILayout.ToggleLeft("세어만 본다 (안 고친다)", _dryRun);

        EditorGUILayout.Space(10f);
        if (GUILayout.Button(_dryRun ? "세어 본다" : "앉힌다", GUILayout.Height(28f))) Run();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "바닥에 계단이 생겨(단 1m · ±6m) 여태 y=0에 놓아 둔 것이 뜨거나 묻힌다.\n\n" +
            "**저작 씬의 마을·소굴은 여기가 아니라 `6. Resnap Sites To Terrain`이 맡는다** — " +
            "그쪽은 구운 오프셋을 되읽어 *일부러 띄운 높이*를 지킨다.", MessageType.Warning);
    }

    void Run()
    {
        // **씬의 지형 프로필을 함수에 밀어 넣는다** — 안 넣으면 코드 기본값으로 재서
        // *"에디터가 앉힌 자리"*와 *"게임이 그리는 땅"*이 갈린다.
        TerrainAuthoring.SyncFromScene();

        var targets = new List<GameObject>();
        Collect(targets);

        if (targets.Count == 0)
        {
            Debug.LogWarning("[앉히기] 앉힐 것이 없다 — " +
                             (_scope == Scope.Selected
                                 ? "씬에서 오브젝트를 고르고 다시 누를 것."
                                 : "씬 최상위에 몸을 가진 오브젝트가 없다."));
            return;
        }

        int moved = 0, skipped = 0, outside = 0;
        float worst = 0f;
        string worstName = null;

        foreach (var go in targets)
        {
            if (!Bottom(go, out float bottom)) { skipped++; continue; }

            var p = go.transform.position;

            // **세계 밖은 안 건드린다** — 높이 함수는 밖에서도 값을 내지만 그건 유령 지형이다.
            if (p.x < 0f || p.z < 0f || p.x >= TerrainGrid.WorldSize || p.z >= TerrainGrid.WorldSize)
            { outside++; continue; }

            float ground = TerrainHeight.At(p.x, p.z);

            // 밑면을 지면에 — **피벗이 어디든 맞는다**(몸과 피벗의 차이를 그대로 유지한다).
            float dy = ground - bottom;
            if (Mathf.Abs(dy) < 0.02f) continue;

            if (Mathf.Abs(dy) > worst) { worst = Mathf.Abs(dy); worstName = go.name; }
            moved++;

            if (_dryRun) continue;

            Undo.RecordObject(go.transform, "Snap To Ground");
            go.transform.position = new Vector3(p.x, p.y + dy, p.z);
            EditorUtility.SetDirty(go.transform);
        }

        string head = _dryRun ? "[앉히기·세어봄]" : "[앉히기]";
        Debug.Log($"{head} {targets.Count}개 중 **{moved}개**를 앉혔다" +
                  (worstName != null ? $" (가장 크게 어긋난 것 `{worstName}` {worst:0.##}m)" : "") + ". " +
                  (skipped > 0 ? $"몸이 없어 건너뛴 것 {skipped}개. " : "") +
                  (outside > 0 ? $"세계 밖 {outside}개. " : ""));

        if (!_dryRun && moved > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    // ── 무엇을 앉히나 ───────────────────────────────────────────────────────

    void Collect(List<GameObject> into)
    {
        if (_scope == Scope.Selected)
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;

                // **고른 것은 거의 안 거른다** — 사람이 콕 집은 것이라 *"왜 이건 안 움직이지"*가
                // 되면 안 된다. 다만 몸이 없으면 앉힐 자리가 없어 `Bottom`이 거른다.
                if (_includeChildren)
                {
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                        if (HasBody(t.gameObject) && !into.Contains(t.gameObject)) into.Add(t.gameObject);
                }
                else if (!into.Contains(go)) into.Add(go);
            }
            return;
        }

        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!Eligible(root)) continue;
            into.Add(root);
        }
    }

    // 씬 전체를 훑을 때의 체. **고른 것에는 안 쓴다**(사람이 집은 것을 말없이 빼면 안 된다).
    static bool Eligible(GameObject go)
    {
        if (go == null) return false;

        // **에디터가 세운 것** — 지형·바위 청크. 다음 틱에 다시 서므로 움직여도 헛일이다.
        if ((go.hideFlags & HideFlags.DontSave) != 0) return false;

        // **화면의 것은 자리의 뜻이 다르다.**
        if (go.GetComponent<Camera>() != null) return false;
        if (go.GetComponent<Canvas>() != null) return false;
        if (go.GetComponent<Light>()  != null) return false;
        if (go.transform is RectTransform)     return false;

        // **저작 루트와 스트리밍 루트** — 통째로 옮기면 그 아래 세계가 다 기운다.
        if (go.GetComponent<WorldAuthoringRoot>() != null) return false;
        if (go.GetComponent<TerrainStreamer>()    != null) return false;

        // **몸이 없으면 앉힐 것이 없다** — 매니저·시스템 오브젝트가 전부 그렇다.
        return HasBody(go);
    }

    static bool HasBody(GameObject go) =>
        go.GetComponentInChildren<Renderer>(true) != null ||
        go.GetComponentInChildren<Collider>(true) != null;

    // ── 몸의 밑면 ───────────────────────────────────────────────────────────
    //
    // **렌더러와 콜라이더를 합친다.** 렌더러만 보면 콜라이더만 있는 것(트리거·발판)이 빠지고,
    // 콜라이더만 보면 그림만 있는 장식이 빠진다.
    //
    // **꺼진 것도 센다** — 휴면·모드 전환으로 꺼져 있는 것이 흔하고, 꺼진 동안 자리가 틀려도
    // 켜면 그대로 틀리다. 다만 **파티클은 뺀다**(경계가 매 프레임 바뀌어 기준이 안 된다).
    static bool Bottom(GameObject go, out float bottom)
    {
        bottom = 0f;
        bool any = false;
        float lo = float.MaxValue;

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            lo = Mathf.Min(lo, r.bounds.min.y);
            any = true;
        }

        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            lo = Mathf.Min(lo, c.bounds.min.y);
            any = true;
        }

        if (!any) return false;
        bottom = lo;
        return true;
    }
}
