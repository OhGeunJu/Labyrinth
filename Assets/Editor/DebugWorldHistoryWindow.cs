using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **세계 역사를 들여다본다**(2026-11-18, `WorldHistory` — 설계 `docs/design_world_history.md` 11절 2단계).
//
// 적힌 사건을 최근 것부터 — 날 · 종류 · 문장(`WorldHistory.Describe` — 볼 때마다 만든다) · 횟수 · 얹은 우호도.
// 종류 · 세력으로 거를 수 있고, 줄을 누르면 그 자리로 카메라를 옮긴다.
// 확인할 것: 몸값 · 놓아 주기 · 중립을 죽이면 줄이 생기고 오른쪽 우호도 칸에 값이 붙는다(예전 `FactionRelations.Add`의 값 그대로).
// 같은 상인과 여러 번 사고팔면 줄이 늘지 않고 횟수(×N)와 금액이 는다.
public class DebugWorldHistoryWindow : EditorWindow
{
    [MenuItem("DungeonColony/Debug/세계 역사")]
    static void Open()
    {
        var w = GetWindow<DebugWorldHistoryWindow>("세계 역사");
        w.minSize = new Vector2(520f, 300f);
        w.Show();
    }

    Vector2 _scroll;
    int     _type = -1;        // -1 = 전부
    string  _faction = "";     // "" = 전부
    readonly List<string> _factions = new();

    void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 중에만 사건이 있습니다.", MessageType.Info);
            return;
        }

        var events = WorldHistory.Events;
        EditorGUILayout.LabelField($"사건 {events.Count}줄 (다음 Id {WorldHistory.NextId})", EditorStyles.boldLabel);

        // 종류 · 세력 거르기
        var names = new List<string> { "전부" };
        names.AddRange(System.Enum.GetNames(typeof(WorldEventType)));
        _type = EditorGUILayout.Popup("종류", _type + 1, names.ToArray()) - 1;

        _factions.Clear();
        _factions.Add("전부");
        foreach (var e in events)
            if (!string.IsNullOrEmpty(e.TargetFaction) && !_factions.Contains(e.TargetFaction)) _factions.Add(e.TargetFaction);
        int fi = Mathf.Max(0, _factions.IndexOf(string.IsNullOrEmpty(_faction) ? "전부" : _faction));
        fi = EditorGUILayout.Popup("세력", fi, _factions.ToArray());
        _faction = fi <= 0 ? "" : _factions[fi];

        if (!string.IsNullOrEmpty(_faction))
        {
            var fd = FindFaction(_faction);
            if (fd != null)
                EditorGUILayout.LabelField($"지금 우호도 {FactionRelations.Of(fd):0.#} — {FactionRelations.Describe(fd)}");
        }

        DrawSites();

        EditorGUILayout.Space(4f);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = events.Count - 1; i >= 0; i--)
        {
            var e = events[i];
            if (_type >= 0 && (int)e.Type != _type) continue;
            if (!string.IsNullOrEmpty(_faction) && e.TargetFaction != _faction) continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{e.Day}일", GUILayout.Width(40f));
            EditorGUILayout.LabelField(e.Type.ToString(), GUILayout.Width(110f));
            if (GUILayout.Button(WorldHistory.Describe(e), EditorStyles.linkLabel)) Focus(e.Position);
            GUILayout.FlexibleSpace();
            if (e.Count > 1) EditorGUILayout.LabelField($"x{e.Count}", GUILayout.Width(40f));
            EditorGUILayout.LabelField(Mathf.Approximately(e.Favor, 0f) ? "" : $"{e.Favor:+0;-0}",
                                       GUILayout.Width(40f));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    // ── 장소 · 반응(5단계) ──────────────────────────────────────────────────
    //
    // 지금 소굴이 비는 길은 병력을 다 내보냈을 때뿐이라 기다리기 어렵다 — 카메라에서 가장 가까운 소굴을 바로 비우고
    // (`WorldLairs.Clear` — 진짜와 같은 문), 사흘 기다리는 대신 예약을 지금 푼다(`WorldReactions.ResolveAllNow`).
    bool _sitesOpen = true;

    void DrawSites()
    {
        _sitesOpen = EditorGUILayout.Foldout(_sitesOpen,
            $"장소 {SiteRegistry.States.Count}곳 · 예약된 반응 {WorldReactions.PendingList.Count}", true);
        if (!_sitesOpen) return;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("카메라에서 가장 가까운 소굴 비우기")) ClearNearestLair();
        using (new EditorGUI.DisabledScope(WorldReactions.PendingList.Count == 0))
            if (GUILayout.Button("예약된 반응 지금 처리"))
                Debug.Log($"[세계 역사] 반응 {WorldReactions.ResolveAllNow()}건을 처리했다.");
        EditorGUILayout.EndHorizontal();

        foreach (var p in WorldReactions.PendingList)
            EditorGUILayout.LabelField($"  {p.DueDay}일 예정 — {p.Kind} (사건 #{p.SourceEventId})");
        foreach (var st in SiteRegistry.States)
            EditorGUILayout.LabelField($"  {st.SiteId} — {st.Condition}" +
                                       (string.IsNullOrEmpty(st.OwnerFactionId) ? "" : $" · 주인 {WorldHistory.FactionName(st.OwnerFactionId)}") +
                                       $" · {st.SinceDay}일부터");
    }

    static void ClearNearestLair()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var at = new Vector2(cam.transform.position.x, cam.transform.position.z);
        WorldLair best = null;
        float bestSqr = float.MaxValue;
        foreach (var lair in WorldLairs.All)
        {
            if (lair == null) continue;
            float d = (lair.Position - at).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = lair; }
        }
        if (best == null) { Debug.LogWarning("[세계 역사] 남은 소굴이 없다."); return; }
        WorldLairs.Clear(best);
        Debug.Log($"[세계 역사] 소굴 {best.Name}({best.Id})을(를) 비웠다 — {Mathf.Sqrt(bestSqr):0}m.");
    }

    // 세력 Id → 에셋(우호도를 묻는 데만). 디버그 창이라 에셋을 훑는 것을 감수한다.
    static readonly Dictionary<string, FactionData> _found = new();
    static FactionData FindFaction(string id)
    {
        if (_found.TryGetValue(id, out var hit)) return hit;
        _found[id] = null;
        foreach (var guid in AssetDatabase.FindAssets("t:FactionData"))
        {
            var f = AssetDatabase.LoadAssetAtPath<FactionData>(AssetDatabase.GUIDToAssetPath(guid));
            if (f != null && WorldHistory.FactionId(f) == id) return _found[id] = f;
        }
        return null;
    }

    static void Focus(Vector3 at)
    {
        var cam = Object.FindFirstObjectByType<CameraController>();
        if (cam != null) cam.FocusOn(at);
    }
}
