using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// 폰 명부 — 화면 우측 하단 세로 목록. 한 줄에 [초상화][이름][현재 상태].
// 항목 클릭 → 해당 폰 선택. 인원이 넘치면 스크롤.
//
// **패널도 행도 씬에 실제 오브젝트로 있다.** 편집 중에 보이는 `rowTemplate`을 런타임에
// 그대로 복제해 쓴다 — 코드가 모양을 만들지 않으므로 미리보기와 실제가 어긋날 수 없고,
// 여백·폰트·색을 인스펙터에서 바꾸면 바로 게임에 반영된다.
//
// 행 순서는 **고정**(방랑자 → 하수인, 이름순)이다. 상태별로 묶으면 일하는 폰이 계속
// 자리를 옮겨 다녀 클릭하기 어렵다 — 상태는 자리 이동 대신 텍스트·색으로 보여준다.
//
// **여기 있는 건 지금 이 거점에 있는 폰뿐이다.** 원정 나간 폰은 목록에서 빠지고 돌아오면 다시 붙는다
// (나가 있는 동안은 선택도 명령도 안 되므로 남겨두면 죽은 줄이 된다). 밖에 나간 팀은 월드맵에서 본다.
public class PawnRosterUI : MonoBehaviour
{
    [Header("씬 참조 (생성기가 연결)")]
    [SerializeField] GameObject panel;
    [SerializeField] Transform  content;
    [SerializeField] TextMeshProUGUI titleText;

    [Tooltip("편집 중 보이는 행. 런타임엔 숨기고 이걸 복제해 목록을 만든다")]
    [SerializeField] PawnRosterRowUI rowTemplate;

    [Header("레퍼런스")]
    [SerializeField] SelectionManager selectionManager;

    [Header("새로고침 간격 (초)")]
    [SerializeField] float refreshInterval = 0.3f;

    readonly Dictionary<UnitBrain, PawnRosterRowUI> _rows = new();
    float _timer;

    void OnEnable()
    {
        UnitBrain.OnUnitSpawned          += OnSpawned;
        UnitBrain.OnFactionChanged       += OnSpawned;
        UnitBrain.OnUnitDied             += OnDied;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnDisable()
    {
        UnitBrain.OnUnitSpawned          -= OnSpawned;
        UnitBrain.OnFactionChanged       -= OnSpawned;
        UnitBrain.OnUnitDied             -= OnDied;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void Start()
    {
        if (panel != null) panel.SetActive(true);

        // 템플릿은 편집 중 모양 확인용 — 게임에선 숨긴다(비활성 자식은 레이아웃에서도 빠진다)
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);

        foreach (var brain in FindObjectsByType<UnitBrain>(FindObjectsSortMode.None))
            AddRow(brain);
        SortRows();
        RefreshAll();
    }

    void OnBuildModeChanged(bool active) { if (panel != null) panel.SetActive(!active); }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        EndDragIfMouseReleased();

        _timer += Time.deltaTime;
        if (_timer < refreshInterval) return;
        _timer = 0f;
        RefreshAll();
    }

    // ── 행 관리 ──────────────────────────────────────────────────────
    void OnSpawned(UnitBrain brain) { if (AddRow(brain)) { SortRows(); RefreshAll(); } }

    void OnDied(UnitBrain brain)
    {
        if (!_rows.TryGetValue(brain, out var row)) return;
        if (row != null) Destroy(row.gameObject);
        _rows.Remove(brain);
        UpdateTitle();
    }

    bool AddRow(UnitBrain brain)
    {
        if (brain == null || content == null || rowTemplate == null) return false;
        if (_rows.ContainsKey(brain)) return false;

        var faction = brain.GetComponent<Faction>();
        if (faction == null || !faction.IsPlayer) return false;

        // 편집 중 보이던 그 행을 그대로 복제한다 — 모양은 씬이 갖고, 코드는 데이터만 붙인다.
        var row = Instantiate(rowTemplate, content);
        row.gameObject.name = $"Row_{brain.UnitName}";
        row.gameObject.SetActive(true);
        row.Bind(brain);

        row.RowPointerDown  += OnRowDown;
        row.RowPointerEnter += OnRowEnter;
        row.RowPointerUp    += OnRowUp;

        _rows[brain] = row;
        return true;
    }

    // 방랑자 먼저, 그다음 하수인. 같은 종류끼리는 이름순 (무리 관리 패널과 같은 규칙).
    void SortRows()
    {
        var list = new List<PawnRosterRowUI>(_rows.Values);
        // 순서 규칙은 `PawnOrder` 하나다 — 로스터·무리 관리·자동 선택이 갈리면
        // **명부 맨 위인 폰과 자동으로 골리는 폰이 달라진다.**
        list.Sort((a, b) => (a == null || b == null) ? 0 : PawnOrder.Compare(a.Brain, b.Brain));

        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) list[i].transform.SetSiblingIndex(i);
    }

    void RefreshAll()
    {
        List<UnitBrain> gone = null;
        foreach (var kv in _rows)
        {
            // **원정 나간 폰은 명부에서 뺀다.** 나간 폰은 GameObject가 꺼져 있어(`ExpeditionFormation`)
            // 클릭해도 선택·명령이 안 된다 — 남겨두면 죽은 줄이 되고, 상태 표시도 떠날 때 그대로 멈춘다.
            // 돌아오면 아래 재탐색이 다시 붙인다.
            if (kv.Key == null || kv.Value == null || !kv.Key.gameObject.activeInHierarchy)
            { (gone ??= new()).Add(kv.Key); continue; }

            kv.Value.Refresh(IsSelected(kv.Key));
        }

        if (gone != null)
            foreach (var brain in gone)
            {
                if (_rows.TryGetValue(brain, out var row) && row != null) Destroy(row.gameObject);
                _rows.Remove(brain);
            }

        // 원정에서 **돌아온 폰**을 다시 줍는다. `OnUnitSpawned`는 최초 스폰 때만 오므로
        // 복귀는 이 재탐색이 아니면 잡히지 않는다(비활성은 기본적으로 검색에서 빠진다).
        bool added = false;
        foreach (var brain in FindObjectsByType<UnitBrain>(FindObjectsSortMode.None))
            if (AddRow(brain)) added = true;
        if (added) SortRows();

        UpdateTitle();
    }

    bool IsSelected(UnitBrain brain)
    {
        if (selectionManager == null) return false;
        foreach (var pawn in selectionManager.Selected)
            if (pawn != null && pawn.gameObject == brain.gameObject) return true;
        return false;
    }

    void UpdateTitle()
    {
        if (titleText != null) titleText.text = $"무리 ({_rows.Count})";
    }

    // ── 선택 (단일 / Ctrl 추가 / 드래그 범위) ─────────────────────────────
    // 목록에서 여러 폰을 한 번에 잡을 수 있어야 명령을 묶어 내린다.
    //  · 그냥 클릭      → 그 폰만
    //  · Ctrl+클릭      → 하나씩 더하거나 뺀다
    //  · 눌러서 드래그  → 시작 행부터 지금 행까지 **범위**
    //
    // 범위를 "지나간 행을 계속 더하기"가 아니라 **앵커~현재 구간**으로 잡는 이유:
    // 그래야 드래그를 되돌렸을 때 선택도 같이 줄어든다(더하기만 하면 못 되돌린다).
    PawnRosterRowUI _dragAnchor;
    bool            _dragging;

    void OnRowDown(PawnRosterRowUI row, bool ctrl)
    {
        if (row == null || row.Brain == null || selectionManager == null) return;

        if (ctrl)
        {
            var m = row.Brain.GetComponent<PawnMover>();
            if (m != null) selectionManager.TogglePawn(m);
            _dragging   = false;      // Ctrl은 한 번에 하나 — 드래그로 이어지지 않는다
            _dragAnchor = null;
        }
        else
        {
            _dragging   = true;
            _dragAnchor = row;
            SelectRange(row, row);
        }
        RefreshAll();
    }

    void OnRowEnter(PawnRosterRowUI row)
    {
        if (!_dragging || _dragAnchor == null || row == null) return;
        SelectRange(_dragAnchor, row);
        RefreshAll();
    }

    void OnRowUp()
    {
        _dragging   = false;
        _dragAnchor = null;
    }

    // 마우스를 목록 밖에서 떼면 행의 PointerUp이 안 온다 — 버튼 상태로 직접 끊는다.
    void EndDragIfMouseReleased()
    {
        if (_dragging && Mouse.current != null && !Mouse.current.leftButton.isPressed)
            OnRowUp();
    }

    // 화면에 보이는 순서(형제 순서)가 곧 목록 순서다.
    void SelectRange(PawnRosterRowUI a, PawnRosterRowUI b)
    {
        if (selectionManager == null) return;

        int lo = Mathf.Min(a.transform.GetSiblingIndex(), b.transform.GetSiblingIndex());
        int hi = Mathf.Max(a.transform.GetSiblingIndex(), b.transform.GetSiblingIndex());

        var picked = new List<PawnMover>();
        foreach (var row in _rows.Values)
        {
            if (row == null || row.Brain == null) continue;
            int i = row.transform.GetSiblingIndex();
            if (i < lo || i > hi) continue;
            var m = row.Brain.GetComponent<PawnMover>();
            if (m != null) picked.Add(m);
        }
        selectionManager.SelectOnly(picked);
    }
}
