using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **납치 · 먹기를 시험한다**(2026-11-01, 사용자: *"실제 그거를 실험하는데 필요한 트롤 배치나 고블린 부락? 같은게 없잖아"*).
//
// 고블린 떼는 바이옴 표(`WildSpawns`)에 있어 저절로 나지만 청크당 5%라 마주치기를 기다려야 하고, 트롤 · 오크 · 오우거는
// 어디에도 안 놓였다(어디에 두나는 사용자가 정한다). 그래서 **무리를 지금 카메라 앞에 떠도는 떼로 세운다**
// (`WildSpawner.SpawnHere` — 세운 자리가 태어난 자리 = 굴이 된다). 기다림을 줄이는 버튼 둘을 같이 둔다:
// 고른 폰을 바로 쓰러뜨리기(`BodyPartSystem.KnockOutNow`) · 묶인 사람의 다음 끼니를 당기기(`Devoured.MealNow`).
//
// 무엇이 일어나는지는 콘솔의 `[납치]` 로그와 아래 "묶인 사람" 칸으로 본다. `Assets/Editor/`라 빌드에 안 들어간다.
public class DebugAbductionWindow : EditorWindow
{
    [MenuItem("DungeonColony/Debug/납치 시험")]
    static void Open()
    {
        var w = GetWindow<DebugAbductionWindow>("납치 시험");
        w.minSize = new Vector2(320f, 300f);
        w.Show();
    }

    readonly List<WorldBandData> _bands = new();
    Vector2 _scroll;
    float _ahead = 25f;   // 카메라 가운데에서 이만큼 — 0이면 가운데 그 자리

    void OnEnable() => Reload();
    void OnFocus()  => Reload();
    void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

    void Reload()
    {
        _bands.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:WorldBandData"))
        {
            var b = AssetDatabase.LoadAssetAtPath<WorldBandData>(AssetDatabase.GUIDToAssetPath(guid));
            if (b != null && b.Behavior == WorldBandBehavior.Roam) _bands.Add(b);
        }
        _bands.Sort((a, b) => Devours(b).CompareTo(Devours(a)));   // 먹는 떼를 위로
    }

    static bool Devours(WorldBandData band)
    {
        foreach (var m in band.Members)
            if (m?.Prefab != null && m.Prefab.TryGetComponent(out UnitBrain b) && b.Data != null && b.Data.Devours) return true;
        return false;
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (AssetDatabase.LoadAssetAtPath<WorldBandData>("Assets/5_Data/World/Band/Band_OgreFlame.asset") == null)
        {
            EditorGUILayout.HelpBox("큰 종족 무리 에셋이 아직 다 없습니다(파랑 · 붉은 오우거는 `Pawn/9`가 몸을 만든 뒤에).", MessageType.Info);
            if (GUILayout.Button("큰 종족 무리 에셋 만들기")) { BigRaceCreator.EnsureBands(); Reload(); }
            EditorGUILayout.Space(6f);
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("세우기 · 쓰러뜨리기는 Play Mode에서 됩니다.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawSpawn();
        EditorGUILayout.Space(8f);
        DrawKnockOut();
        EditorGUILayout.Space(8f);
        DrawCaptives();

        EditorGUILayout.EndScrollView();
    }

    // ── 세우기 ──────────────────────────────────────────────────────────────

    void DrawSpawn()
    {
        EditorGUILayout.LabelField("떼 세우기 — 카메라 가운데 앞", EditorStyles.boldLabel);
        _ahead = EditorGUILayout.Slider("앞으로(m)", _ahead, 0f, 60f);

        foreach (var band in _bands)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string tag = Devours(band) ? "  [먹는다]" : "";
                EditorGUILayout.LabelField(band.BandName + tag, GUILayout.MinWidth(140f));
                using (new EditorGUI.DisabledScope(!band.IsUsable))
                    if (GUILayout.Button("세운다", GUILayout.Width(60f)))
                    {
                        if (!TryAim(out var at)) Debug.LogWarning("[납치 시험] 카메라가 가리키는 땅을 못 찾았다.");
                        else if (!WildSpawner.SpawnHere(band, at)) Debug.LogWarning($"[납치 시험] {band.BandName}을 못 세웠다 — 둘레에 빈칸이 없거나 무리가 비었다.");
                    }
            }
        }
        EditorGUILayout.HelpBox(
            "세운 자리가 그 떼의 굴입니다(메고 돌아가 기둥을 세우는 곳).\n" +
            $"쓰러진 내 폰 둘레 {Abduction.GuardRadius:F0}m 안에 깨어 있는 내 편이 있으면 안 멥니다 — 한 명만 보내 볼 것.",
            MessageType.None);
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

    // ── 쓰러뜨리기 ─────────────────────────────────────────────────────────

    void DrawKnockOut()
    {
        EditorGUILayout.LabelField("고른 폰", EditorStyles.boldLabel);
        var sel = SelectionManager.Instance;
        var list = sel != null ? sel.Selected : null;
        int n = list?.Count ?? 0;
        EditorGUILayout.LabelField(n == 0 ? "고른 폰 없음" : $"{n}명 골랐음");
        using (new EditorGUI.DisabledScope(n == 0))
            if (GUILayout.Button("고른 폰을 지금 쓰러뜨린다"))
                foreach (var m in list)
                    if (m != null && m.TryGetComponent(out BodyPartSystem body)) body.KnockOutNow();
    }

    // ── 묶인 사람 ──────────────────────────────────────────────────────────

    void DrawCaptives()
    {
        EditorGUILayout.LabelField("묶인 사람", EditorStyles.boldLabel);
        var all = Devoured.All;
        if (all.Count == 0) { EditorGUILayout.LabelField("없음", EditorStyles.miniLabel); return; }

        foreach (var d in all)
        {
            string state = d.Eater != null ? $"먹히는 중 ({d.Eater.DisplayTitle} · {d.MealLeft:F0}초 남음)"
                         : d.MealDue  ? "끼니 — 먹을 놈을 기다린다"
                         : $"다음 끼니까지 {d.NextMealIn:F0}초";
            if (d.Untying) state += " · 줄 푸는 중";
            if (d.Loose)   state += " · 풀림";

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{d.name}: {state}", GUILayout.MinWidth(180f));
                if (GUILayout.Button("끼니 당기기", GUILayout.Width(80f))) d.MealNow();
            }
        }
    }
}
