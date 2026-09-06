using UnityEngine;

// **바위 한 칸을 판다**(2026-08-29).
//
// 기존 채굴(`ResourceNode`)과 **다른 축이다**: 저쪽은 세워 둔 노드에 폰을 붙이는 **직접 배정**
// 이고(플레이어가 콕 집는다), 이쪽은 찍어 둔 칸이 **큐로 흐른다**(찍고 나면 누가 언제 파든
// 상관없다). 지하에서 파는 칸은 수백이라 하나씩 붙일 수가 없다.
//
// **광맥은 같은 일감이 낸다.** 파낸 칸에 광맥이 박혀 있으면 돌 대신 그 광석이 나온다 —
// 광맥을 노드로 세우면 *"바위를 파다가 노드를 만나면 다른 일감으로 갈아타는"* 이음매가
// 생기고, 그 이음매에서 폰이 일감을 놓는다.
public class MineJob : JobBase
{
    public override JobCategory Category => JobCategory.Mining;

    // **둘러서서 판다** — 파는 칸 위에 설 수는 없다(거기가 바위다). 자원 노드를 `NodeReach`로
    // 둘러싸게 한 것과 같은 축이고, 값도 같아야 한다.
    public override float Reach => 2.0f;

    readonly Vector2Int _tile;
    readonly ResourceNodeData _vein;

    float _work;

    // **광맥이든 맨바위든 같은 모양이다** — 둘 다 `ResourceNodeData`라 나오는 것·개수·
    // 걸리는 시간을 **한 곳에서** 읽는다. 광맥이 없으면 기본 바위(`RockMap.PlainRock`).
    ResourceNodeData Node => _vein != null ? _vein : RockMap.PlainRock;

    // 데이터가 아예 없을 때의 눈금. **경고는 `RockRenderer`가 한 번만 한다.**
    const float FallbackWork = 60f;

    public MineJob(Vector2Int tile)
    {
        _tile = tile;
        _vein = RockMap.VeinAt(tile);

        // **목표는 그 칸 자신이 아니다** — 거기는 바위라 못 선다. 둘레에서 설 수 있는 칸을
        // 겨눈다(`Reach`가 2m라 옆 칸에서 닿는다).
        TileCoord = tile;
        Priority  = 0;
    }

    public Vector2Int Tile => _tile;

    float Required => Node != null ? Mathf.Max(1f, Node.GaugeRequired) : FallbackWork;

    // **이미 파였으면 할 일이 없다** — 두 폰이 같은 칸을 겨눴거나, 찍어 둔 사이에 다른
    // 경로로 파였을 수 있다.
    //
    // **그리고 둘러설 자리가 없으면 아직 일감이 아니다**(2026-09-05).
    //
    // 채굴은 **바위 칸 자체를 겨눈다**(`TileCoord`) — 다른 일감이 *폰이 설 수 있는 칸*을
    // 겨누는 것과 다른 자리다. 그래서 덩이를 넓게 찍으면 **속에 있는 칸**은 둘레가 전부
    // 바위라 **닿을 방법이 없는데**, 그것도 큐에 그대로 올라갔다. 폰은 배정을 받아 수십
    // 미터를 걸어가서 못 닿고 놓고, 그 일감은 잠시 쉬었다 다시 올라오고 — **폰 열여섯이
    // 그 왕복만 되풀이했다**(10초에 포기 711건, 정작 겉면은 아무도 안 팠다).
    //
    // 겉면부터 파면 속이 저절로 겉면이 된다 — **파 들어가는 순서가 규칙에서 나온다.**
    // (`Reach`가 2m라 대각선 이웃에서도 닿으므로 여덟을 다 본다.)
    public override bool IsReady =>
        RockMap.IsRock(_tile) && RockMap.IsMarked(_tile) && HasFace(_tile);

    static readonly Vector2Int[] Around =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };

    // 둘러설 수 있는 칸이 하나라도 있나. **`MapManager`가 없으면 참**(격자 없는 씬에서
    // 채굴이 통째로 막히지 않게 — 다른 폴백과 같은 규칙).
    static bool HasFace(Vector2Int tile)
    {
        var map = MapManager.Instance;
        if (map == null) return true;

        foreach (var d in Around)
            if (map.IsWorkable(tile + d)) return true;

        return false;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (!RockMap.IsRock(_tile) || !RockMap.IsMarked(_tile))
        {
            Status = JobStatus.Done;     // 누가 먼저 팠거나 지정이 풀렸다
            return;
        }

        // **`UnitStats`는 컴포넌트가 아니다** — `UnitBrain`이 필드로 든다(`Stats`).
        // `GetComponent`로 찾으면 컴파일은 되는데 **런타임에 `ArgumentException`으로 죽고**,
        // 그러면 그 폰의 BT가 통째로 멎는다.
        var stats = unit != null ? unit.Stats : null;
        float power = stats != null ? Mathf.Max(0.1f, stats.Mining) : 1f;

        // **틱 델타를 곱한다.** 안 곱하면 초당 `skill × 10`이 되어 배속·프레임레이트가
        // 밸런스를 바꾼다 — `ResourceNode.Work`가 시그니처로 못 박은 그 사고다.
        float dt = unit != null && unit.Blackboard != null ? unit.Blackboard.TickDelta : Time.deltaTime;
        _work += power * dt;

        // 캐는 만큼 는다. `BTLeafNodes`가 노동에 쓰는 것과 같은 축이다.
        stats?.GrowStat(StatName.Mining, dt * 0.05f);

        if (_work < Required) return;

        Finish(unit);
    }

    void Finish(UnitBrain unit)
    {
        Vector3 drop = unit != null ? unit.transform.position : Vector3.zero;

        // **판다 → 그다음 떨군다.** 순서가 반대면 떨어진 물건이 아직 바위인 칸에 놓여
        // 아무도 못 줍는다.
        RockMap.Mine(_tile);

        // **저작된 것을 그대로 쓴다.** 전에는 `ResourceGroup.Stone` 묶음의 첫 번째를 집었는데,
        // 그 묶음에 **뼈**가 같이 들어 있어서 **벽을 파면 뼈가 나왔다** — 묶음은 *"창고 필터와
        // 화면 분류"*의 축이지 *"이 자원이 어디서 나오나"*의 축이 아니다.
        var node = Node;
        if (node != null && node.DropItem != null)
        {
            int amount = Random.Range(node.DropMin, node.DropMax + 1);
            if (amount > 0)
            {
                // **캐낸 것은 저절로 실려 간다**(2026-09-05, 사용자 요청).
                //
                // 바닥의 물건은 원래 **체크한 것만** 운반된다(`DroppedItem.HaulAllowed`,
                // 기본 꺼짐) — 널린 전리품을 폰이 알아서 줍줍하지 않게 하려는 규칙이다.
                // 그런데 **설비가 내놓는 것은 그 축과 무관하다**(자원 노드·작업대 산출물은
                // 아예 바닥을 안 거친다 — 생산 라인은 그대로 자동이다).
                //
                // 벽을 파서 나온 돌은 **전리품이 아니라 생산물**이다. 바닥을 거치는 것은
                // 구현 사정일 뿐인데, 그것 때문에 **캘 때마다 사람이 따로 찍어 줘야 했다.**
                DroppedItem.Spawn(node.DropItem, amount, drop)?.SetHaulAllowed(true);
            }
        }

        Status = JobStatus.Done;
    }
}
