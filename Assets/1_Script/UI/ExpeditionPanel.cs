using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 원정대 편성 패널 (G키) — 누구를 데려갈지 고르고 [출발]을 누른다.
//
// **누르는 순간 사라지지 않는다.** 고른 폰들은 던전 핵 앞으로 걸어와 모이고,
// 전원이 도착했을 때 비로소 필드맵에서 빠진다(`ExpeditionFormation`).
// 그동안 이 패널은 "모이는 중 2/4"를 보여주고 목록을 잠근다 — 걸어가는 중에 명단이 바뀌면
// 누가 가는지가 흔들린다. [취소]는 언제든 가능하고, 아직 아무도 안 떠났으므로 되돌릴 게 없다.
//
// 행 모양은 씬의 `rowTemplate`이 갖는다(→ "모양은 씬이, 코드는 데이터만").
public class ExpeditionPanel : MonoBehaviour
{
    [Header("씬 참조 (생성기가 연결)")]
    [SerializeField] GameObject      panel;
    [SerializeField] Transform       content;
    [SerializeField] ExpeditionRowUI rowTemplate;
    [SerializeField] TextMeshProUGUI summaryText;   // "선택 3명 · 적재 210"
    [SerializeField] TextMeshProUGUI statusText;    // "모이는 중 2/3"
    [SerializeField] Button          departButton;
    [SerializeField] Button          cancelButton;

    [SerializeField] Key toggleKey = Key.G;

    [Header("상태줄 색")]
    [SerializeField] Color statusNormal = new(0.65f, 0.65f, 0.70f);
    [SerializeField] Color statusWarn   = new(0.95f, 0.65f, 0.35f);

    readonly List<ExpeditionRowUI> _rows = new();
    bool _stuckNotified;   // 이번 편성에서 "못 옴"을 이미 알렸는가

    void OnEnable()
    {
        ExpeditionFormation.OnChanged    += Refresh;
        ExpeditionFormation.OnDeparted   += Close;
        UnitBrain.OnUnitSpawned          += OnRosterChanged;
        UnitBrain.OnUnitDied             += OnRosterChanged;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
        GameModeManager.OnModeChanged    += OnModeChanged;
    }

    void OnDisable()
    {
        ExpeditionFormation.OnChanged    -= Refresh;
        ExpeditionFormation.OnDeparted   -= Close;
        UnitBrain.OnUnitSpawned          -= OnRosterChanged;
        UnitBrain.OnUnitDied             -= OnRosterChanged;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
        GameModeManager.OnModeChanged    -= OnModeChanged;
    }

    // 출발하면 곧바로 월드맵으로 넘어간다 — 편성 패널이 열린 채 남아 있으면
    // 돌아왔을 때 이미 떠난 폰들의 낡은 목록이 보인다.
    void OnModeChanged(GameMode mode) { if (mode != GameMode.Field) Close(); }

    void Start()
    {
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);

        if (departButton != null) departButton.onClick.AddListener(Depart);
        if (cancelButton != null) cancelButton.onClick.AddListener(CancelFormation);

        if (panel != null) panel.SetActive(false);
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void OnRosterChanged(UnitBrain _) { if (IsOpen) Rebuild(); }

    bool IsOpen => panel != null && panel.activeSelf;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 월드맵을 보는 동안엔 편성할 게 없다(폰이 화면에 없다).
        if (kb[toggleKey].wasPressedThisFrame && GameModeManager.IsField) Toggle();
        if (IsOpen && kb.escapeKey.wasPressedThisFrame) Close();
    }

    void Toggle() { if (IsOpen) Close(); else Open(); }

    void Open()
    {
        if (panel == null) return;
        panel.SetActive(true);
        Rebuild();
    }

    void Close() { if (panel != null) panel.SetActive(false); }

    // ── 목록 ─────────────────────────────────────────────────────────────────

    void Rebuild()
    {
        foreach (var r in _rows) if (r != null) Destroy(r.gameObject);
        _rows.Clear();

        if (content == null || rowTemplate == null) return;

        var brains = new List<UnitBrain>();
        foreach (var b in FindObjectsByType<UnitBrain>(FindObjectsSortMode.None))
        {
            if (b == null) continue;
            var faction = b.GetComponent<Faction>();
            if (faction == null || !faction.IsPlayer) continue;
            if (b.Blackboard != null && b.Blackboard.IsKnockedOut) continue;   // 기절한 폰은 못 간다
            brains.Add(b);
        }

        // 로스터·무리 관리와 같은 순서 — 방랑자 먼저, 그다음 하수인, 각각 이름순.
        brains.Sort((a, b) =>
        {
            bool am = a.Data is MinionData, bm = b.Data is MinionData;
            if (am != bm) return am ? 1 : -1;
            return string.CompareOrdinal(a.UnitName, b.UnitName);
        });

        var formation = ExpeditionFormation.Instance;

        foreach (var b in brains)
        {
            var row = Instantiate(rowTemplate, content);
            row.gameObject.name = $"Row_{b.UnitName}";
            row.gameObject.SetActive(true);
            row.Bind(b, Refresh);

            // 집결 중에 패널을 열면 **누가 가고 있는지** 체크로 보여야 한다.
            if (InFormation(formation, b)) row.SetChecked(true);

            _rows.Add(row);
        }

        Refresh();
    }

    static bool InFormation(ExpeditionFormation f, UnitBrain brain)
    {
        if (f == null || !f.IsGathering) return false;
        var members = f.Members;
        for (int i = 0; i < members.Count; i++)
            if (members[i] == brain) return true;
        return false;
    }

    // ── 상태 갱신 ────────────────────────────────────────────────────────────

    void Refresh()
    {
        var formation = ExpeditionFormation.Instance;
        bool gathering = formation != null && formation.IsGathering;

        if (!gathering) _stuckNotified = false;

        // 패널을 닫아둔 채로 집결시켰다면 못 오는 폰이 생겨도 볼 방법이 없다 → 한 번 띄운다.
        // 매번 띄우면 사용자가 닫는 것과 싸우게 되므로 편성당 한 번만.
        if (gathering && formation.StuckCount > 0 && !_stuckNotified)
        {
            _stuckNotified = true;
            // 여기서 Open()을 부르면 Rebuild → Refresh로 되돌아온다. 보여주기만 하면 된다.
            if (!IsOpen && panel != null) panel.SetActive(true);
        }

        int   picked = 0;
        float load   = 0f;
        foreach (var r in _rows)
        {
            if (r == null || !r.IsChecked || r.Brain == null) continue;
            picked++;
            if (r.Brain.Stats != null) load += r.Brain.Stats.Strength * 5f;
        }

        foreach (var r in _rows) if (r != null) r.SetInteractable(!gathering);

        if (summaryText != null)
            summaryText.text = $"선택 {picked}명 · 적재 {Mathf.FloorToInt(load)}";

        if (statusText != null)
        {
            // 못 오는 폰이 있으면 **그걸 먼저 말한다.** 몰래 두고 출발하지 않으므로
            // 플레이어가 알아야 결정할 수 있다 — 취소하거나, 길을 뚫어 주거나.
            bool stuck = gathering && formation.StuckCount > 0;

            statusText.text = !gathering
                ? (picked == 0 ? "데려갈 폰을 고르세요" : "출발하면 핵 앞으로 모입니다")
                : stuck
                    ? $"{formation.StuckSummary} 이(가) 집결 지점에 갈 수 없습니다 — 취소하거나 길을 확보하세요"
                    : $"핵 앞으로 모이는 중 {formation.ArrivedCount}/{formation.TotalCount}";

            statusText.color = stuck ? statusWarn : statusNormal;
        }

        if (departButton != null) departButton.interactable = !gathering && picked > 0;
        if (cancelButton != null) cancelButton.gameObject.SetActive(gathering);
    }

    // ── 동작 ─────────────────────────────────────────────────────────────────

    void Depart()
    {
        var formation = ExpeditionFormation.Instance;
        if (formation == null)
        {
            Debug.LogWarning("[편성] 씬에 ExpeditionFormation이 없습니다.");
            return;
        }

        var picked = new List<UnitBrain>();
        foreach (var r in _rows)
            if (r != null && r.IsChecked && r.Brain != null) picked.Add(r.Brain);

        if (picked.Count == 0) return;
        formation.Begin(picked);
        Refresh();
    }

    void CancelFormation()
    {
        ExpeditionFormation.Instance?.Cancel();
        Rebuild();   // 체크를 비우고 목록을 되살린다
    }
}
