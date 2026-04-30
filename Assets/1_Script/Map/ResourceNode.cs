using System.Collections.Generic;
using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    [SerializeField] ResourceNodeData data;
    public Inventory Inventory { get; private set; }

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
        Debug.Log($"[ResourceNode] {brain.name} 배정됨");
    }

    public void Unassign(UnitBrain brain)
    {
        if (!_assigned.Remove(brain)) return;
        if (brain.Blackboard.AssignedNode == this)
            brain.Blackboard.AssignedNode = null;
        Debug.Log($"[ResourceNode] {brain.name} 배정 해제");
    }

    void OnDestroy()
    {
        foreach (var brain in _assigned)
            if (brain != null && brain.Blackboard.AssignedNode == this)
                brain.Blackboard.AssignedNode = null;
        _assigned.Clear();
    }

    // 월드 위치에서 타일 좌표 자동 파생
    public Vector2Int TileCoord =>
        MapManager.Instance.WorldToTile(transform.position);

    float _gauge;
    int _totalYielded;

    void Awake()
    {
        Inventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        // 빌드존 안일 때만 타일 막힘 표시 (시각화용). 실제 이동 차단은 콜라이더+NavMesh 담당.
        if (MapManager.Instance != null && MapManager.Instance.IsInBuildZone(TileCoord))
            MapManager.Instance.SetTile(TileCoord, TileData.MakeImpassable());
    }

    public void Init(ResourceNodeData nodeData) => data = nodeData;

    // 매 작업 틱마다 호출. 게이지가 차면 아이템 1회 드롭.
    public void Work(float power)
    {
        if (data == null) return;

        _gauge += power;
        Debug.Log($"[ResourceNode] 채굴 중 {_gauge:F1} / {data.GaugeRequired} ({_gauge / data.GaugeRequired * 100f:F0}%)");

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

        int amount = Random.Range(data.DropMin, data.DropMax + 1);
        _totalYielded++;

        // 노드 인벤토리에 쌓아둠
        Inventory.TryAdd(data.DropItem, amount);
        Debug.Log($"[ResourceNode] {data.DropItem.ItemName} ×{amount} 적재 (누적: {Inventory.GetTotalQuantity(data.DropItem)})");

        // 운반 가능한 창고가 있으면 HaulJob 등록
        var storage = Storage.FindNearest(transform.position, data.DropItem);
        if (storage != null && JobManager.Instance != null)
            JobManager.Instance.RegisterJob(new HaulJob(this, data.DropItem, amount, storage));
    }
}
