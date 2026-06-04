using System.Collections.Generic;
using UnityEngine;

// 좌측(또는 하단) 폰 명부 패널
// - 대기중 / 작업중 두 섹션으로 자동 분류
// - 항목 클릭 → 해당 폰 선택 (배정 흐름으로 연결)
// - 증분 갱신: 소환/사망 시에만 엔트리 생성/제거, 매 틱은 섹션 변경 시에만 reparent
public class PawnRosterPanel : MonoBehaviour
{
    [Header("패널 루트")]
    [SerializeField] GameObject panel;

    [Header("섹션 컨테이너")]
    [SerializeField] Transform idleContainer;   // "대기중" 목록 부모
    [SerializeField] Transform workContainer;   // "작업중" 목록 부모

    [Header("레퍼런스")]
    [SerializeField] PawnRosterEntryUI entryPrefab;
    [SerializeField] SelectionManager selectionManager;

    [Header("새로고침 간격 (초)")]
    [SerializeField] float refreshInterval = 0.5f;

    readonly Dictionary<UnitBrain, PawnRosterEntryUI> _entries = new();
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
            AddEntry(brain);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < refreshInterval) return;
        _timer = 0f;
        ReassignSections();
    }

    void OnSpawned(UnitBrain brain) => AddEntry(brain);

    void OnDied(UnitBrain brain)
    {
        if (_entries.TryGetValue(brain, out var entry))
        {
            if (entry != null) Destroy(entry.gameObject);
            _entries.Remove(brain);
        }
    }

    void AddEntry(UnitBrain brain)
    {
        if (brain == null || entryPrefab == null) return;
        if (_entries.ContainsKey(brain)) return;

        var faction = brain.GetComponent<Faction>();
        if (faction == null || faction.Type != FactionType.Player) return;

        var container = SectionFor(brain);
        var entry = Instantiate(entryPrefab, container);
        entry.Setup(brain, SelectPawn);
        _entries[brain] = entry;
    }

    // 매 틱: 죽은 참조 정리 + 섹션이 바뀐 엔트리만 reparent
    void ReassignSections()
    {
        List<UnitBrain> dead = null;
        foreach (var kv in _entries)
        {
            var brain = kv.Key;
            var entry = kv.Value;
            if (brain == null || entry == null) { (dead ??= new()).Add(brain); continue; }

            var target = SectionFor(brain);
            if (entry.transform.parent != target)
                entry.transform.SetParent(target, false);
        }

        if (dead != null)
            foreach (var b in dead)
            {
                if (_entries.TryGetValue(b, out var e) && e != null) Destroy(e.gameObject);
                _entries.Remove(b);
            }
    }

    Transform SectionFor(UnitBrain brain)
    {
        bool working = brain.Blackboard != null && brain.Blackboard.AssignedNode != null;
        return working ? workContainer : idleContainer;
    }

    void SelectPawn(UnitBrain brain)
    {
        var mover = brain.GetComponent<PawnMover>();
        if (mover != null && selectionManager != null)
            selectionManager.SelectPawn(mover);
    }
}
