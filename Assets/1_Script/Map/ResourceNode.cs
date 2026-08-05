using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceNode : MonoBehaviour, IHaulSource
{
    // 배정/해제 통보 — 노드 패널·무리 관리 패널이 즉시 갱신하도록.
    // 해제는 패널 밖(이동 명령·사망)에서도 일어나므로, 폴링만으로는 목록이 낡은 채로 남는다.
    public static event Action<ResourceNode> OnAssignmentChanged;

    [SerializeField] ResourceNodeData data;

    [Tooltip("이 수량 이상 쌓이면 운반 작업 등록. 한 번에 몇 개나 실려 가는지는 여기가 아니라 " +
             "폰의 적재 한도(CarryCapacity)가 정한다.")]
    [SerializeField] int haulThreshold = 10;

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

    // 동시에 붙을 수 있는 인원. 0이면 제한 없음(`ResourceNodeData`에 그렇게 적은 경우).
    public int MaxWorkers => data != null ? Mathf.Max(0, data.MaxWorkers) : 0;

    public int  AssignedCount => _assigned.Count;
    public bool IsFull        => MaxWorkers > 0 && _assigned.Count >= MaxWorkers;

    // 배정 성공 여부를 **돌려준다** — 자리가 없으면 부른 쪽이 그 사실을 말해 줘야 한다.
    // 조용히 실패하면 플레이어는 명령이 먹은 줄 알고 폰이 안 움직이는 것만 본다.
    public bool Assign(UnitBrain brain)
    {
        if (brain == null) return false;
        if (_assigned.Contains(brain)) return true;   // 이미 여기 있다 = 원하는 상태
        if (IsFull) return false;

        // 다른 노드에 배정돼 있었으면 **거기서 먼저 뗀다.** 폰이 들고 있는
        // `Blackboard.AssignedNode`는 하나뿐이라, 안 떼면 이전 노드의 목록에만
        // 유령 항목이 남는다 — 그 폰은 실제로는 여기서 일하는데 저쪽 패널엔 계속 보인다.
        //
        // **자리 확인을 먼저 한 뒤에 뗀다** — 순서가 반대면 꽉 찬 노드에 보내려다 실패했을 때
        // 폰이 원래 캐던 곳에서도 떨어져 나와 아무 데도 배정되지 않은 채로 남는다.
        var prev = brain.Blackboard.AssignedNode;
        if (prev != null && prev != this) prev.Unassign(brain);

        _assigned.Add(brain);
        brain.Blackboard.AssignedNode = this;
        OnAssignmentChanged?.Invoke(this);
        return true;
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

        // Awake에서 막아둔 점유 타일 해제 — 노드 소멸 자리가 영구 건설 불가로 남는 것 방지.
        // 막을 때와 **같은 목록**으로 푼다(그동안 노드가 움직였어도 어긋나지 않는다).
        if (MapManager.Instance != null)
            foreach (var tile in _blockedTiles)
                MapManager.Instance.SetTile(tile, TileData.MakePassable());
        _blockedTiles.Clear();
    }

    // 월드 위치에서 타일 좌표 자동 파생
    public Vector2Int TileCoord =>
        MapManager.Instance.WorldToTile(transform.position);

    [Tooltip("차지하는 타일 수 (가로×세로). 모델이 이 크기에 맞게 만들어져 있어야 한다.")]
    [SerializeField] Vector2Int size = Vector2Int.one;

    float _gauge;
    int _totalYielded;
    readonly List<Vector2Int> _blockedTiles = new();

    HaulDispatcher _haul;
    float _haulCheckTimer;
    const float HaulCheckInterval = 2f;

    // 차지하는 칸들.
    //
    // **월드 중심에서 되짚는다** — `TileCoord`(위치를 floor한 칸)에서 퍼뜨리면 안 된다.
    // 짝수 크기 건물은 배치할 때 칸 **경계**에 놓이므로(`BuildModeManager.FootprintCenter`),
    // floor한 칸은 실제 왼쪽 끝보다 한 칸 오른쪽이다. 그대로 퍼뜨리면 배치가 등록한 칸과
    // 한 칸 어긋나고, 둘의 합집합이 막혀 **2×1 광맥이 3칸을 막는다**(실제로 그랬다).
    //
    // 타일 중심은 `tile + 0.5`이므로(`MapManager.TileToWorld`), 맨 왼쪽 칸 중심은
    // `중심 - (w-1)/2`이고 그 칸 번호는 그것을 floor한 값이다.
    public IEnumerable<Vector2Int> Footprint
    {
        get
        {
            int w = Mathf.Max(1, size.x), h = Mathf.Max(1, size.y);
            var pos = transform.position;

            int minX = Mathf.FloorToInt(pos.x - (w - 1) * 0.5f);
            int minY = Mathf.FloorToInt(pos.z - (h - 1) * 0.5f);

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                yield return new Vector2Int(minX + x, minY + y);
        }
    }

    void Awake()
    {
        Inventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        _haul     = new HaulDispatcher(this);

        // 빌드존 안일 때만 타일 막힘 표시 (시각화용). 실제 이동 차단은 콜라이더+NavMesh 담당.
        // **차지한 칸 전부**를 막는다 — 한 칸만 막으면 큰 광맥의 나머지 칸에 건물이 겹쳐 지어진다.
        if (MapManager.Instance == null) return;
        foreach (var tile in Footprint)
        {
            if (!MapManager.Instance.IsInBuildZone(tile)) continue;
            _blockedTiles.Add(tile);
            MapManager.Instance.SetTile(tile, TileData.MakeImpassable());
        }
    }

    public void Init(ResourceNodeData nodeData) => data = nodeData;

    // 매 작업 틱마다 호출. 게이지가 차면 아이템 1회 드롭.
    //
    // `deltaTime`을 **호출자가 반드시 넘겨야 한다.** 예전엔 `Work(skill)`로 틱마다
    // 스킬 수치를 그대로 더했는데, BT는 tickInterval(0.1초)마다 도므로 실제 채굴 속도가
    // 초당 skill×10이 돼 버렸다(스킬 10이 돌 노드에서 초당 33개). 프레임/틱 수가
    // 속도를 좌우하면 배속·프레임레이트에 따라 산출이 달라진다.
    // 인자로 받는 이유: 호출부가 BT 노드(bb.TickDelta)와 Job(별도 틱)으로 갈려 있어
    // 여기서 Time.deltaTime을 쓸 수 없다 — 시그니처로 강제해 빠뜨리지 못하게 한다.
    public void Work(float power, float deltaTime)
    {
        if (data == null) return;

        _gauge += power * deltaTime;

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

        // 임계값 이상 쌓인 만큼 **몫으로 나눠** 등록 — 쌓인 양에 비례해 여러 폰이 붙는다.
        _haul?.Dispatch(data.DropItem, HaulThreshold);
    }

    // 쌓인 재고를 주기적으로 다시 본다.
    //
    // 산출(`DropYield`) 때만 등록하면, 폰 배낭이 차서 남긴 잔량은 **다음 산출이 있어야만**
    // 실려 간다 — 다 캔 노드에서는 그 다음이 없어 잔량이 영영 남았다.
    // 창고가 그때 없었거나 꽉 찼던 경우도 여기서 회복된다(작업대의 방식과 같다).
    void Update()
    {
        if (_haul == null || data == null || data.DropItem == null) return;
        if (Inventory == null || Inventory.Items.Count == 0) return;

        _haulCheckTimer -= Time.deltaTime;
        if (_haulCheckTimer > 0f) return;
        _haulCheckTimer = HaulCheckInterval;

        _haul.Dispatch(data.DropItem, HaulThreshold);
    }
}
