using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// 무리 관리 패널 (P) — 플레이어 세력 폰 전체를 한 표에서 관리한다.
//   행 = 폰(방랑자·하수인) / 열 = 작업 on·off + 현재 배정 + 전투 성향
//
// 작업은 폰별 숫자 우선순위가 아니라 **한다/안 한다**만 정한다. 무엇을 먼저 할지는
// JobCategoryInfo.QueueOrder(왼쪽 열이 먼저)가 전역으로 결정한다.
// 채굴·벌목·순찰은 대상에 폰을 직접 배정하는 방식이라 토글이 아니라 상태로만 표시한다.
public class ColonyPanel : MonoBehaviour
{
    [Header("패널 루트")]
    [SerializeField] GameObject panel;

    [Header("컨테이너")]
    [Tooltip("열 제목이 놓일 행")]
    [SerializeField] Transform headerRow;

    [Tooltip("폰 행이 쌓이는 스크롤 Content")]
    [SerializeField] Transform content;

    [Header("레퍼런스")]
    [SerializeField] SelectionManager selectionManager;

    [Header("새로고침 간격 (초)")]
    [SerializeField] float refreshInterval = 0.5f;

    readonly Dictionary<UnitBrain, ColonyRowUI> _rows = new();
    float _timer;

    void OnEnable()
    {
        UnitBrain.OnUnitSpawned          += OnSpawned;
        UnitBrain.OnUnitDied             += OnDied;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
        ResourceNode.OnAssignmentChanged += OnAssignmentChanged;
    }

    void OnDisable()
    {
        UnitBrain.OnUnitSpawned          -= OnSpawned;
        UnitBrain.OnUnitDied             -= OnDied;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
        ResourceNode.OnAssignmentChanged -= OnAssignmentChanged;
    }

    // 배정/해제는 패널 밖(이동 명령·사망)에서도 일어난다 — 주기 갱신을 기다리지 않고 바로 반영.
    void OnAssignmentChanged(ResourceNode node)
    {
        if (panel != null && panel.activeSelf) RefreshAll();
    }

    void Start()
    {
        BuildHeader();
        if (panel != null) panel.SetActive(false);

        foreach (var brain in FindObjectsByType<UnitBrain>(FindObjectsSortMode.None))
            AddRow(brain);
        SortRows();
    }

    void OnBuildModeChanged(bool active)
    {
        if (active && panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame && panel != null)
            panel.SetActive(!panel.activeSelf);

        if (panel == null || !panel.activeSelf) return;

        _timer += Time.deltaTime;
        if (_timer < refreshInterval) return;
        _timer = 0f;
        RefreshAll();
    }

    // ── 행 관리 ──────────────────────────────────────────────────────
    void OnSpawned(UnitBrain brain) { if (AddRow(brain)) SortRows(); }

    void OnDied(UnitBrain brain)
    {
        if (!_rows.TryGetValue(brain, out var row)) return;
        if (row != null) Destroy(row.gameObject);
        _rows.Remove(brain);
    }

    bool AddRow(UnitBrain brain)
    {
        if (brain == null || content == null) return false;
        if (_rows.ContainsKey(brain)) return false;

        // 작업 설정이 있는 폰 = 일꾼(방랑자·하수인). 적/NPC는 표에 넣지 않는다.
        if (brain.Work == null) return false;

        // 세력 확인 — 로스터/선택과 같은 게이트(플레이어 조작 세력만).
        var faction = brain.GetComponent<Faction>();
        if (faction == null || !faction.IsPlayer) return false;

        var go = new GameObject($"Row_{brain.UnitName}", typeof(RectTransform));
        go.transform.SetParent(content, false);

        var row = go.AddComponent<ColonyRowUI>();
        row.Build(brain, SelectPawn);
        _rows[brain] = row;
        return true;
    }

    // 방랑자 먼저, 그다음 하수인. 같은 종류끼리는 이름순.
    void SortRows()
    {
        var list = new List<ColonyRowUI>(_rows.Values);
        list.Sort((a, b) =>
        {
            if (a == null || b == null || a.Brain == null || b.Brain == null) return 0;
            bool aMinion = a.Brain.Data is MinionData;
            bool bMinion = b.Brain.Data is MinionData;
            if (aMinion != bMinion) return aMinion ? 1 : -1;
            return string.CompareOrdinal(a.Brain.UnitName, b.Brain.UnitName);
        });

        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) list[i].transform.SetSiblingIndex(i);
    }

    void RefreshAll()
    {
        List<UnitBrain> dead = null;
        foreach (var kv in _rows)
        {
            if (kv.Key == null || kv.Value == null) { (dead ??= new()).Add(kv.Key); continue; }
            kv.Value.Refresh();
        }

        if (dead == null) return;
        foreach (var brain in dead)
        {
            if (_rows.TryGetValue(brain, out var row) && row != null) Destroy(row.gameObject);
            _rows.Remove(brain);
        }
    }

    void SelectPawn(UnitBrain brain)
    {
        var mover = brain != null ? brain.GetComponent<PawnMover>() : null;
        if (mover != null && selectionManager != null) selectionManager.SelectPawn(mover);
    }

    // ── 열 제목 ──────────────────────────────────────────────────────
    // 열 구성은 JobCategoryInfo.QueueOrder에서 나오므로 런타임에 만든다(카테고리를 늘려도 따라온다).
    void BuildHeader()
    {
        if (headerRow == null) return;

        for (int i = headerRow.childCount - 1; i >= 0; i--)
            Destroy(headerRow.GetChild(i).gameObject);

        MakeHeaderLabel("이름", ColonyColumns.NameX, ColonyColumns.NameW, TextAlignmentOptions.Left);

        for (int i = 0; i < JobCategoryInfo.QueueOrder.Length; i++)
            MakeHeaderLabel(JobCategoryInfo.DisplayName(JobCategoryInfo.QueueOrder[i]),
                            ColonyColumns.JobX(i), ColonyColumns.JobW, TextAlignmentOptions.Center);

        MakeHeaderLabel("배정", ColonyColumns.AssignX, ColonyColumns.AssignW, TextAlignmentOptions.Left);
        MakeHeaderLabel("성향", ColonyColumns.StanceX, ColonyColumns.StanceW, TextAlignmentOptions.Center);
    }

    void MakeHeaderLabel(string text, float x, float w, TextAlignmentOptions align)
    {
        var label = UIHelper.MakeLabel(headerRow, $"H_{text}", text, Vector2.zero, Vector2.zero, 11f);
        ColonyRowUI.Place(label.GetComponent<RectTransform>(), x, w);
        label.alignment = align;
        label.color     = new Color(0.70f, 0.70f, 0.75f);
        if (align == TextAlignmentOptions.Left) label.margin = new Vector4(6f, 0f, 0f, 0f);
    }
}
