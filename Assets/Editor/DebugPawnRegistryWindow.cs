using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **폰 기록을 들여다본다**(2026-11-18, `PawnRegistry` — 설계 `docs/design_world_history.md` 11절 1단계).
//
// **시험 마을 사람** — 지금 세계에는 저작된 자리에 선 폰이 없어서(마을 · 소굴 자리뿐) 카메라 앞에 하나 세운다
// (`WorldContentStreamer.DebugPlace` — 진짜와 같은 길이라 신원이 `place:debug_…`이고, 청크와 함께 내려가고 다시 선다).
//
// 기록 수 · 처지별 개수 · 기록 목록(이름 · 처지 · 세력 · 마지막 자리)을 보여 준다. 처지로 거를 수 있고, 줄을 누르면
// 그 자리로 카메라를 옮긴다(`CameraController.FocusOn`). "콘솔에 요약" 단추는 같은 줄을 `[폰 기록]` 로그로 남긴다.
// 확인할 것: 마을 사람을 죽이면 `place:` 기록이 `Dead`가 되고, 청크를 벗어났다 돌아와도 그 줄이 `Live`로 안 바뀐다.
// `Unknown`이 하나라도 생기면 이유를 안 넘긴 파괴 경로가 있다는 뜻이다(콘솔 경고에 그 폰 이름과 자리가 있다).
public class DebugPawnRegistryWindow : EditorWindow
{
    [MenuItem("DungeonColony/Debug/폰 기록")]
    static void Open()
    {
        var w = GetWindow<DebugPawnRegistryWindow>("폰 기록");
        w.minSize = new Vector2(420f, 300f);
        w.Show();
    }

    Vector2 _scroll;
    int     _filter = -1;          // -1 = 전부
    bool    _placedOnly;
    readonly List<PawnRecord> _rows = new();
    GameObject _testPrefab;
    float      _ahead = 15f;
    const string DefaultTestPrefab = "Assets/3_Prefab/Pawn/Human/Body/Npc_Freefolk_Guard_Male.prefab";

    void OnEnable()
    {
        if (_testPrefab == null) _testPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTestPrefab);
    }

    void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 중에만 기록이 있습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField(PawnRegistry.Summary(), EditorStyles.wordWrappedLabel);

        // 처지별 단추 — 누르면 그 처지만
        var counts = PawnRegistry.CountByStatus();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(_filter < 0, $"전부 {PawnRegistry.Count}", EditorStyles.miniButtonLeft)) _filter = -1;
        for (int i = 0; i < counts.Length; i++)
        {
            bool on = GUILayout.Toggle(_filter == i, $"{(PawnStatus)i} {counts[i]}", EditorStyles.miniButtonMid);
            if (on) _filter = i;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _placedOnly = GUILayout.Toggle(_placedOnly, "저작된 자리(place:)만", GUILayout.Width(160f));
        if (GUILayout.Button("콘솔에 요약", GUILayout.Width(100f))) Debug.Log(PawnRegistry.Summary());
        EditorGUILayout.EndHorizontal();

        DrawTestPlace();

        _rows.Clear();
        foreach (var r in PawnRegistry.Records)
        {
            if (_filter >= 0 && (int)r.Status != _filter) continue;
            if (_placedOnly && (r.Id == null || !r.Id.StartsWith(PawnRegistry.PlacePrefix))) continue;
            _rows.Add(r);
        }
        _rows.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

        EditorGUILayout.Space(4f);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var r in _rows)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(r.Status.ToString(), GUILayout.Width(70f));
            EditorGUILayout.LabelField(r.Spec != null ? r.Spec.Name : "?", GUILayout.Width(110f));
            EditorGUILayout.LabelField(r.FactionId ?? "-", GUILayout.Width(110f));
            EditorGUILayout.LabelField(r.Body != null ? "몸 기록" : "", GUILayout.Width(50f));
            float op = WorldHistory.Opinion(r);   // 개인 호감(3단계) — 0 근처면 비운다
            EditorGUILayout.LabelField(Mathf.Abs(op) < 0.5f ? "" : $"호감 {op:+0;-0}", GUILayout.Width(60f));
            if (GUILayout.Button(r.Id, EditorStyles.linkLabel)) Focus(r.LastPosition);
            // 4단계 시험 — 떠난 사람을 카메라 앞에 떠돌이 몸으로 다시 세운다(`PawnRegistry.SpecOf` → `Attach`가 몸을 입힌다).
            // 진짜 길은 떠돌이 이벤트(`EventManager.SpawnReinforcement` — 플레이어와 얽힌 사람만 · 확률)다.
            if (r.Status == PawnStatus.Departed && GUILayout.Button("다시 부르기", GUILayout.Width(80f))) Recall(r);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    // ── 시험 마을 사람 ──────────────────────────────────────────────────────

    void DrawTestPlace()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("시험 마을 사람(저작된 자리와 같은 길)", EditorStyles.boldLabel);
        _testPrefab = (GameObject)EditorGUILayout.ObjectField("프리팹", _testPrefab, typeof(GameObject), false);
        _ahead      = EditorGUILayout.Slider("앞으로(m)", _ahead, 0f, 60f);
        bool usable = _testPrefab != null && _testPrefab.TryGetComponent(out UnitBrain b) && PawnRegistry.Tracks(b);
        using (new EditorGUI.DisabledScope(!usable))
            if (GUILayout.Button("카메라 앞에 세우기"))
            {
                if (!TryAim(out var at)) Debug.LogWarning("[폰 기록] 카메라가 가리키는 땅을 못 찾았다.");
                else
                {
                    if (RockMap.TryNearestOpen(at, 24f, out var open)) at = open;
                    string id = WorldContentStreamer.DebugPlace(_testPrefab, at, "시험 마을 사람");
                    if (id == null) Debug.LogWarning("[폰 기록] 세우지 못했다 — `WorldContentStreamer`가 씬에 없다.");
                    else Debug.Log($"[폰 기록] 시험 마을 사람을 세웠다 — {id} · {at}");
                }
            }
        if (_testPrefab != null && !usable)
            EditorGUILayout.HelpBox("기록하는 몸이 아닙니다(`UnitData.UsesBackstory`가 꺼져 있다).", MessageType.Warning);
        EditorGUILayout.HelpBox("세운 뒤 죽이고 250m 넘게 멀어졌다가 돌아오면 다시 서지 않아야 합니다(처지 `Dead`). " +
                                "죽이지 않고 다녀오면 `AtSite` → 돌아와서 `Live`, 부상 · 지닌 것이 그대로여야 합니다.", MessageType.None);
    }

    bool TryAim(out Vector3 at)
    {
        at = default;
        var cam = Camera.main;
        if (cam == null) return false;
        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, 2000f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
        else if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d)) at = ray.GetPoint(d);
        else return false;
        var fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (fwd.sqrMagnitude > 1e-4f) at += fwd.normalized * _ahead;
        return true;
    }

    void Recall(PawnRecord r)
    {
        if (_testPrefab == null || !TryAim(out var at)) { Debug.LogWarning("[폰 기록] 다시 부를 몸(프리팹)이나 자리가 없다."); return; }
        if (RockMap.TryNearestOpen(at, 24f, out var open)) at = open;
        var spec = PawnRegistry.SpecOf(r);
        var go = PawnSpawn.Instantiate(_testPrefab, at, Quaternion.identity, null, spec);
        go.transform.position = PawnPlacement.StandingPos(go, at);
        if (go.TryGetComponent(out UnitBrain b))
        {
            b.ApplySpec(spec);
            b.Recruitable = true;
        }
        Debug.Log($"[폰 기록] {r.Spec?.Name}({r.Id})을(를) 떠돌이로 다시 세웠다 — 플레이어와 얽힌 일 " +
                  $"{(WorldHistory.SharesHistoryWithPlayer(r.Id) ? "있음" : "없음(이벤트로는 안 온다)")}.");
    }

    static void Focus(Vector3 at)
    {
        var cam = Object.FindFirstObjectByType<CameraController>();   // 디버그 창 한 번 누름 — 훑기를 감수한다
        if (cam != null) cam.FocusOn(at);
    }
}
