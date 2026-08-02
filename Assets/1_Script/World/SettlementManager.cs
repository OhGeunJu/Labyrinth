using System;
using System.Collections.Generic;
using UnityEngine;

// 정착지 전환형 관리.
// 정착지 = 필드맵 루트 GameObject(그 아래 MapManager / DungeonCore / 건물 / 유닛 포함).
// 한 번에 하나만 활성(Root.SetActive). 비활성 정착지는 GameObject 비활성 상태로
// 모든 상태를 인메모리 보존 → 디스크 직렬화 불필요(세이브는 후속 과제).
//
// 전환 시 활성 정착지의 싱글톤을 일치시킨다:
//   - MapManager.SetAsPlayerZone (활성 존 = 길찾기/배치 기준)
//   - DungeonCore.SetAsActive   (활성 메인 코어)
// Storage.All은 OnEnable/OnDisable 기준이라 루트 토글 시 자동 격리됨.
//
// step 4: 사전 배치/런타임 정착지를 WorldMap 노드에 등록(RegisterSettlement)해 월드맵과 동기화.
// step 5: 비활성 정착지는 매일(OnDayChanged) 오프라인 생산 틱으로 자원을 누적한다.
[DefaultExecutionOrder(-58)]
public class SettlementManager : MonoBehaviour
{
    public static SettlementManager Instance { get; private set; }

    // 비활성 정착지가 하루당 생산하는 자원(추상 틱). 자원 노드/농장 시뮬 대신 데이터로 지정.
    [Serializable]
    public class OfflineYieldEntry
    {
        public ItemData Item;
        [Min(0)] public int PerDay = 1;
    }

    [Serializable]
    public class Settlement
    {
        public string Name = "정착지";
        public Vector2Int WorldNode;   // 월드맵 노드 좌표
        public GameObject Root;        // 필드맵 루트 (MapManager/DungeonCore/건물/유닛 포함)

        [Header("오프라인 생산 (비활성일 때 하루당)")]
        public List<OfflineYieldEntry> OfflineYield = new();
    }

    [Tooltip("씬에 사전 배치된 정착지들. 인덱스 0 = 시작 정착지.")]
    [SerializeField] List<Settlement> settlements = new();

    public IReadOnlyList<Settlement> Settlements => settlements;
    public Settlement Active { get; private set; }

    public static event Action<Settlement> OnActiveChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnEnable()  => GameTimeManager.OnDayChanged += TickOfflineProduction;
    void OnDisable() => GameTimeManager.OnDayChanged -= TickOfflineProduction;

    void Start()
    {
        // 시작 시 0번만 활성, 나머지 비활성.
        for (int i = 0; i < settlements.Count; i++)
        {
            var root = settlements[i].Root;
            if (root != null) root.SetActive(i == 0);
        }
        // 사전 배치 정착지들을 월드맵 노드에 등록(step 4) — 월드맵이 내 거점을 인지.
        SyncToWorldMap();

        if (settlements.Count > 0) Activate(settlements[0]);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // 다른 정착지로 전환. 이전 거점 끄기 → 대상 켜기 → 싱글톤 재지정.
    // **루트 활성화는 이 클래스의 몫**이다 — 게임 모드는 루트를 건드리지 않는다(월드맵을 봐도 거점은 돈다).
    // 한 번에 하나만 시뮬되는 이유는 모드가 아니라 **"활성 정착지는 하나"**라는 규칙이다.
    public void SwitchTo(Settlement target)
    {
        if (target == null || target == Active) return;

        if (Active?.Root != null) Active.Root.SetActive(false);
        if (target.Root  != null) target.Root.SetActive(true);

        Activate(target);
    }

    public void SwitchToNode(Vector2Int node)
    {
        var s = FindByNode(node);
        if (s != null) SwitchTo(s);
    }

    public Settlement FindByNode(Vector2Int node) =>
        settlements.Find(s => s.WorldNode == node);

    // 새 정착지 등록 (코어 박기로 생성된 경우 — step 4 런타임 진입점).
    // 월드맵 노드에도 즉시 등록해 동기화한다.
    public void Register(Settlement s)
    {
        if (s == null) return;
        if (!settlements.Contains(s)) settlements.Add(s);
        WorldMap.Instance?.RegisterSettlement(s.WorldNode);
    }

    // 런타임 코어 박기용 편의 진입점: 노드+루트로 정착지를 만들어 등록.
    public Settlement RegisterAt(Vector2Int node, GameObject root, string name = "정착지")
    {
        var existing = FindByNode(node);
        if (existing != null) return existing;

        var s = new Settlement { Name = name, WorldNode = node, Root = root };
        Register(s);
        return s;
    }

    // 현재 활성 정착지의 바이옴 (바이옴 콘텐츠 게이팅용, step 7).
    public Biome ActiveBiome =>
        (WorldMap.Instance != null && Active != null)
            ? WorldMap.Instance.GetTile(Active.WorldNode).Biome
            : Biome.Unknown;

    // 대상을 활성 정착지로 확정하고 싱글톤을 일치.
    void Activate(Settlement target)
    {
        Active = target;
        if (target?.Root != null)
        {
            var map  = target.Root.GetComponentInChildren<MapManager>(true);
            map?.SetAsPlayerZone();
            var core = target.Root.GetComponentInChildren<DungeonCore>(true);
            core?.SetAsActive();
        }
        OnActiveChanged?.Invoke(target);
    }

    void SyncToWorldMap()
    {
        if (WorldMap.Instance == null) return;
        foreach (var s in settlements)
            if (s != null) WorldMap.Instance.RegisterSettlement(s.WorldNode);
    }

    // ── step 5: 오프라인 추상 생산 ─────────────────────────────────────────────
    // 시뮬되지 않는 정착지는 매일 데이터로 지정된 자원을 그 정착지 창고에 누적.
    // 겨냥 대상은 **지금 안 보고 있는 다른 거점**이다 — 활성 거점은 월드맵을 보는 동안에도
    // 실제로 시뮬되므로 여기 걸리지 않는다(`Root.activeSelf`가 걸러낸다).
    // 창고 필터/격자 여유가 없으면 그만큼만 입고.
    void TickOfflineProduction()
    {
        foreach (var s in settlements)
        {
            if (s == null || s.Root == null) continue;
            if (s.Root.activeSelf) continue;   // 실제로 시뮬 중이면 스킵
            ProduceOffline(s);
        }
    }

    static void ProduceOffline(Settlement s)
    {
        if (s.OfflineYield == null || s.OfflineYield.Count == 0) return;

        // 비활성 루트라 Storage.All 레지스트리엔 없음 → 자식에서 직접 수집(비활성 포함).
        var storages = s.Root.GetComponentsInChildren<Storage>(true);
        if (storages.Length == 0) return;

        foreach (var y in s.OfflineYield)
        {
            if (y == null || y.PerDay <= 0) continue;
            var item = y.Item;
            if (item == null) continue;

            int remaining = y.PerDay;
            foreach (var st in storages)
            {
                if (remaining <= 0) break;
                if (st == null || !st.CanAccept(item)) continue;
                remaining -= st.Inventory.TryAdd(item, remaining);
            }
        }
    }
}
