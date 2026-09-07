using UnityEditor;
using UnityEngine;

// **습격을 지금 부른다 — 그리고 무리가 무엇을 하는지 본다**(2026-09-07).
//
// 원래 습격은 추첨으로만 오고 하루 두 번 남짓이라, 하나를 확인하려면 앉아서 기다려야 한다
// (`EventManager`의 `Force Roll Event`는 **아무거나** 뽑아서 상인이 오기도 한다).
//
// **부르기만 하는 버튼으로는 부족하다.** `RaidLord`가 든 것은 *접근 → 돌격 → 후퇴*라는
// **시간에 걸친 상태**인데, 화면에서는 폰이 걷는 것만 보여 **지금이 접근인지 돌격인지
// 구별이 안 된다.** 그래서 부르는 자리와 보는 자리를 한 창에 둔다.
//
// `Assets/Editor/`에 있으므로 **빌드에 아예 안 들어간다**.
public class DebugRaidWindow : EditorWindow
{
    [MenuItem("DungeonColony/Debug: 습격 부르기")]
    static void Open()
    {
        var w = GetWindow<DebugRaidWindow>("습격");
        w.minSize = new Vector2(300f, 220f);
        w.Show();
    }

    Vector2 _scroll;

    // 상태가 시간에 걸쳐 바뀌므로 **가만히 두어도 갱신돼야 한다** — 안 그러면 창을 클릭할
    // 때만 숫자가 움직여서, 멈춘 것인지 안 그린 것인지 구별이 안 된다.
    void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play Mode에서만 됩니다 — 습격은 실제 폰을 세우는 일이라 " +
                "에디터 상태에서는 부를 것도 지휘할 것도 없습니다.",
                MessageType.Info);
            return;
        }

        var em = EventManager.Instance;
        if (em == null)
        {
            EditorGUILayout.HelpBox("씬에 EventManager가 없습니다.", MessageType.Warning);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawPool(em);
        EditorGUILayout.Space(8f);
        DrawLords();

        EditorGUILayout.EndScrollView();
    }

    // **씬의 추첨 풀에서 고른다** — 에셋 폴더를 훑으면 씬이 안 들고 있는 이벤트까지 부르게
    // 되어, **실제 판에서는 절대 안 나오는 습격**을 시험하게 된다.
    void DrawPool(EventManager em)
    {
        EditorGUILayout.LabelField("부르기", EditorStyles.boldLabel);

        var pool = em.Pool;
        int shown = 0;

        for (int i = 0; i < pool.Count; i++)
        {
            var data = pool[i];
            if (data == null || data.EventType != GameEventType.Raid) continue;
            shown++;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(data.EventName, GUILayout.MinWidth(120f));

                // **막지 않고 알린다** — `MinGameTime`이 안 찼어도 부를 수 있어야 한다
                // (그걸 기다리는 것이 곧 시험을 못 하는 이유다). 다만 지금 추첨에서는
                // 안 나오는 것이라는 사실은 보여 준다.
                bool early = GameTimeManager.Elapsed < data.MinGameTime;
                if (early) EditorGUILayout.LabelField("아직 안 열림", GUILayout.Width(70f));

                if (GUILayout.Button("부른다", GUILayout.Width(60f)))
                    em.TriggerNow(data);
            }
        }

        if (shown == 0)
            EditorGUILayout.HelpBox(
                "씬 EventManager의 추첨 풀에 습격(Raid) 이벤트가 없습니다.",
                MessageType.Warning);
    }

    // **여기가 이 창의 값이다.** 폰이 걷는 것만 보면 접근인지 돌격인지 모른다.
    void DrawLords()
    {
        EditorGUILayout.LabelField("지휘 중인 무리", EditorStyles.boldLabel);

        var lords = RaidLords.Active;
        if (lords.Count == 0)
        {
            EditorGUILayout.LabelField("없음", EditorStyles.miniLabel);
            return;
        }

        for (int i = 0; i < lords.Count; i++)
        {
            var lord = lords[i];
            if (lord == null) continue;

            // **지휘관을 같이 적는다** — 안 보이면 스탯만 조용히 오른 것이라 뜻이 없다.
            string boss = lord.Leader != null ? $"   지휘관: {lord.Leader.name}" : "";
            EditorGUILayout.LabelField(
                $"{i + 1}. {Label(lord.Current)}   {lord.StandingCount} / {lord.Initial}명{boss}");
        }

        EditorGUILayout.HelpBox(
            "접근: 집결지에 모인다 (산 멤버 6할이 오면 돌격)\n" +
            "돌격: 핵으로 간다\n" +
            "후퇴: 서 있는 놈이 처음의 1/3 이하 — 걸어 나간다 (다시 안 온다)",
            MessageType.None);
    }

    static string Label(RaidLord.Phase p) => p switch
    {
        RaidLord.Phase.Approach => "접근",
        RaidLord.Phase.Assault  => "돌격",
        RaidLord.Phase.Retreat  => "후퇴",
        _                       => p.ToString(),
    };
}
