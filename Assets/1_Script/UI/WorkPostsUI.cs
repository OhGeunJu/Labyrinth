using System.Collections.Generic;
using TMPro;
using UnityEngine;

// **배치 창(지휘소) — `H`키 · 거점 바의 [지휘소] 버튼으로 연다**(2026-10-01). 09-29에는 핵을 누르면 열렸는데
// (사용자: *"배치를 핵을 누르면 열리는 걸로"*), 10-01에 핵이 포탈이 되면서 걷어냈다(사용자: *"지휘소를 … 키로 여는
// 버튼으로 바꾸고 핵은 포탈 기능을 하게"*). 소환은 소환진(`SummonCircle` → `DungeonCoreUI`).
//
// 폰 한 줄에 **1 · 2 · 3순위** 드롭다운 셋. 고르는 것은 *핵 영역 안의 배치할 수 있는 건물*과 `운반`이다
// (`WorkPosts.Candidates` — 목록을 여기서 따로 들면 일꾼이 받는 것과 창이 보여 주는 것이 갈린다).
// **비어 있음**을 고르면 그 칸을 비운다 — 셋 다 비면 그 폰은 작업 설정(`P`)대로 알아서 일한다.
//
// **모양은 씬이 든다**(`UI → Create Work Posts Window`) — 줄은 씬의 템플릿(`rowTemplate`)을 복제하고 여기서는
// 글자와 목록만 채운다("모양은 씬이, 코드는 데이터만"). 뿌리는 늘 켜져 있고 판만 열리고 닫힌다(창 규약).
public class WorkPostsUI : MonoBehaviour
{
    public static WorkPostsUI Instance { get; private set; }

    [SerializeField] GameObject       panel;
    [SerializeField] Transform        content;
    [SerializeField] WorkPostRowUI    rowTemplate;
    [SerializeField] TextMeshProUGUI  emptyText;

    readonly List<WorkPostRowUI> _rows = new();
    List<(string label, WorkPost post)> _options = new();

    void Awake()
    {
        Instance = this;
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (panel == null) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        if (kb.hKey.wasPressedThisFrame && !InputFocus.KeysBlocked()) { Toggle(); return; }
        if (panel.activeSelf && kb.escapeKey.wasPressedThisFrame) WindowRaiser.Close(panel);
    }

    public static bool IsOpen => Instance != null && Instance.panel != null && Instance.panel.activeSelf;

    public static void Toggle()
    {
        if (Instance != null && Instance.panel != null && Instance.panel.activeSelf) WindowRaiser.Close(Instance.panel);
        else Open();
    }

    public static void Open()
    {
        if (Instance == null) { Debug.LogWarning("[배치] 씬에 배치 창이 없다 — `UI → Create Work Posts Window`를 돌릴 것."); return; }
        Instance.Rebuild();
        WindowRaiser.Open(Instance.panel);
    }

    // **열 때마다 다시 짓는다** — 건물은 지어지고 부서지며, 폰은 합류하고 죽는다. 열려 있는 동안 바뀌는 일은
    // 드물고, 바뀌어도 다시 열면 맞는다(매 박 다시 지으면 드롭다운이 펼쳐진 채 닫힌다).
    void Rebuild()
    {
        _options = WorkPosts.Candidates();

        var pawns = new List<UnitBrain>();
        foreach (var b in UnitBrain.All)
        {
            if (b == null || b.Work == null) continue;             // 일꾼만(작업 설정이 붙는 폰)
            var f = b.GetComponent<Faction>();
            if (f == null || !f.IsPlayer) continue;
            if (b.GetComponent<Corpse>() != null) continue;
            pawns.Add(b);
        }
        pawns.Sort(PawnOrder.Compare);

        while (_rows.Count < pawns.Count)
        {
            var row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            _rows.Add(row);
        }
        for (int i = 0; i < _rows.Count; i++)
        {
            bool on = i < pawns.Count;
            _rows[i].gameObject.SetActive(on);
            if (on) _rows[i].Bind(pawns[i], _options);
        }

        if (emptyText != null) emptyText.gameObject.SetActive(pawns.Count == 0);
    }
}
