using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 좌측(또는 하단) 폰 명부 패널
// - 대기중 / 작업중 두 섹션으로 자동 분류
// - 항목 클릭 → 해당 폰 선택 (배정 흐름으로 연결)
public class PawnRosterPanel : MonoBehaviour
{
    [Header("패널 루트")]
    [SerializeField] GameObject panel;

    [Header("섹션 컨테이너")]
    [SerializeField] Transform idleContainer;   // "대기중" 목록 부모
    [SerializeField] Transform workContainer;   // "작업중" 목록 부모

    [Header("레퍼런스")]
    [SerializeField] GameObject entryPrefab;    // Button + TMP Text 프리팹
    [SerializeField] SelectionManager selectionManager;

    [Header("새로고침 간격 (초)")]
    [SerializeField] float refreshInterval = 0.5f;

    readonly List<UnitBrain> _pawns = new();
    readonly List<GameObject> _entries = new();
    float _timer;

    void OnEnable()
    {
        UnitBrain.OnUnitSpawned          += OnSpawned;
        UnitBrain.OnUnitDied             += OnDied;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnDisable()
    {
        UnitBrain.OnUnitSpawned          -= OnSpawned;
        UnitBrain.OnUnitDied             -= OnDied;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (panel != null) panel.SetActive(!active); }

    void Start()
    {
        if (panel != null) panel.SetActive(true);
        foreach (var brain in FindObjectsByType<UnitBrain>(FindObjectsSortMode.None))
            TryRegister(brain);
        Refresh();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < refreshInterval) return;
        _timer = 0f;
        Refresh();
    }

    void OnSpawned(UnitBrain brain)
    {
        if (TryRegister(brain))
            Refresh();
    }

    void OnDied(UnitBrain brain)
    {
        if (_pawns.Remove(brain))
            Refresh();
    }

    bool TryRegister(UnitBrain brain)
    {
        if (_pawns.Contains(brain)) return false;
        var faction = brain.GetComponent<Faction>();
        if (faction == null || faction.Type != FactionType.Player) return false;
        _pawns.Add(brain);
        return true;
    }

    void Refresh()
    {
        foreach (var e in _entries) Destroy(e);
        _entries.Clear();

        _pawns.RemoveAll(b => b == null);

        foreach (var brain in _pawns)
        {
            bool working = brain.Blackboard.AssignedNode != null;
            var container = working ? workContainer : idleContainer;

            var entry = Instantiate(entryPrefab, container);

            var text = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = brain.Data != null ? brain.Data.RaceName : brain.name;

            var btn = entry.GetComponentInChildren<Button>();
            if (btn != null)
            {
                var captured = brain;
                btn.onClick.AddListener(() => SelectPawn(captured));
            }

            _entries.Add(entry);
        }
    }

    void SelectPawn(UnitBrain brain)
    {
        var mover = brain.GetComponent<PawnMover>();
        if (mover != null)
            selectionManager.SelectPawn(mover);
    }
}
