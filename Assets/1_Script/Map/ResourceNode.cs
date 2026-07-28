using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    // 배정/해제 통보 — 노드 패널·무리 관리 패널이 즉시 갱신하도록.
    // 해제는 패널 밖(이동 명령·사망)에서도 일어나므로, 폴링만으로는 목록이 낡은 채로 남는다.
    public static event Action<ResourceNode> OnAssignmentChanged;

    [SerializeField] ResourceNodeData data;
    [SerializeField] int haulThreshold = 10;  // 이 수량 이상 쌓이면 운반 작업 등록
    public Inventory Inventory { get; private set; }

    public int HaulThreshold
    {
        get => haulThreshold;
        set => haulThreshold = Mathf.Max(1, value);
    }

    public ResourceNodeData Data => data;
    public bool IsDesignated { get; set; }
    public float GaugeProgress => data != null ? _gauge / data.GaugeRequired : 0f;

    readonly List<UnitBrain> _assigned = new();
    public IReadOnlyList<UnitBrain> AssignedPawns => _assigned;

    public void Assign(UnitBrain brain)
    {
        if (_assigned.Contains(brain)) return;
        _assigned.Add(brain);
        brain.Blackboard.AssignedNode = this;
        OnAssignmentChanged?.Invoke(this);
    }

    public void Unassign(UnitBrain brain)
    {
        if (!_assigned.Remove(brain)) return;
        if (brain.Blackboard.AssignedNode == this)
            brain.Blackboard.AssignedNode = null;
        OnAssignmentChanged?.Invoke(this);
    }

    void OnDestroy()
    {
        foreach (var brain in _assigned)
            if (brain != null && brain.Blackboard.AssignedNode == this)
                brain.Blackboard.AssignedNode = null;

        bool had = _assigned.Count > 0;
        _assigned.Clear();
        if (had) OnAssignmentChanged?.Invoke(this);

        // Awake에서 막아둔 점유 타일 해제 — 노드 소멸 자리가 영구 건설 불가로 남는 것 방지
        if (_tileBlocked && MapManager.Instance != null)
            MapManager.Instance.SetTile(_blockedTile, TileData.MakePassable());
    }

    // 월드 위치에서 타일 좌표 자동 파생
    public Vector2Int TileCoord =>
        MapManager.Instance.WorldToTile(transform.position);

    float _gauge;
    int _totalYielded;
    bool _haulJobPending;
    bool _tileBlocked;
    Vector2Int _blockedTile;

    void Awake()
    {
        Inventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        // 빌드존 안일 때만 타일 막힘 표시 (시각화용). 실제 이동 차단은 콜라이더+NavMesh 담당.
        if (MapManager.Instance != null && MapManager.Instance.IsInBuildZone(TileCoord))
        {
            _blockedTile = TileCoord;
            _tileBlocked = true;
            MapManager.Instance.SetTile(_blockedTile, TileData.MakeImpassable());
        }
    }

    public void Init(ResourceNodeData nodeData) => data = nodeData;

    // 매 작업 틱마다 호출. 게이지가 차면 아이템 1회 드롭.
    public void Work(float power)
    {
        if (data == null) return;

        _gauge += power;

        if (_gauge >= data.GaugeRequired)
        {
            _gauge = 0f;
            DropYield();
        }
    }

    void DropYield()
    {
        if (data.DropItem == null)
        {
            Debug.LogWarning($"[ResourceNode] {name}: DropItem이 설정되지 않았습니다.");
            return;
        }

        int amount = UnityEngine.Random.Range(data.DropMin, data.DropMax + 1);
        _totalYielded++;

        // 노드 인벤토리에 쌓아둠
        Inventory.TryAdd(data.DropItem, amount);

        // 임계값 이상 쌓이고 운반 작업이 없을 때만 등록
        int total = Inventory.GetTotalQuantity(data.DropItem);
        if (!_haulJobPending && total >= haulThreshold)
        {
            var storage = Storage.FindNearest(transform.position, data.DropItem);
            if (storage != null && JobManager.Instance != null)
            {
                _haulJobPending = true;
                JobManager.Instance.RegisterJob(new HaulJob(this, data.DropItem, total, storage, () => _haulJobPending = false));
            }
        }
    }
}
