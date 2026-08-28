using System.Collections.Generic;
using UnityEngine;

// 런타임 배치된 건물의 메타(원본 BuildingData)를 보관하고 철거를 담당.
// 배치 시 BuildingDormancy.Ensure처럼 자동 부착(BuildModeManager.Place / BuildingBlueprint.Complete).
// 철거: 점유 해제 + 메소 재베이크 + 건설비 75% 환불(또는 코어 아이템 드랍, step C) + 파괴.
[DisallowMultipleComponent]
public class PlacedBuilding : MonoBehaviour, IFootprintSite
{
    public static readonly List<PlacedBuilding> All = new();

    [SerializeField] BuildingData data;
    // **전액 환불.** 75%였는데, 손실을 물리면 "지어 놓고 아니다 싶으면 옮긴다"가 벌칙이 된다 —
    // 배치는 되돌릴 수 있어야 시험해 볼 수 있고, 잘못 놓은 값은 자재가 아니라 **들인 시간**으로
    // 이미 치른다(폰이 자재를 지고 걸어왔고 그동안 다른 일을 못 했다).
    // 청사진 취소(`BuildingBlueprint.Cancel`)와 같은 값이라 둘을 헷갈릴 일도 없어졌다.
    [SerializeField, Range(0f, 1f)] float refundRatio = 1f;

    public BuildingData Data => data;

    // 이 건물이 점유(통행 불가) 등록한 타일들. 철거 시 정확히 이것만 해제 → 노드 등 타 점유를 건드리지 않음.
    readonly List<Vector2Int> _occupiedTiles = new();

    // 철거 예정 빨간 틴트용
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    static readonly int ColorId     = Shader.PropertyToID("_Color");     // Built-in
    static readonly Color PendingColor = new Color(1f, 0.3f, 0.3f, 1f);

    Renderer[] _renderers;
    bool _highlighted;

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // **씬에 손으로 놓아둔 건물도 자기 칸을 막는다.**
    //
    // 점유 등록은 `BuildModeManager.Place`만 했다. 그래서 에디터에서 끌어다 놓은 건물은
    // `MapManager`가 **전혀 모르는 상태**가 된다 — 길찾기가 아는 것은 콜라이더뿐인데
    // 그건 모델보다 훨씬 작을 수 있어(연구대: 모델 3×2, 콜라이더 1×1) **모델 한복판을
    // 가로지르는 경로**가 나온다. 폰은 그 경로를 따라가다 눈에 보이는 모서리에 처박히고,
    // 로그에는 "로컬경로 있음 / 목표칸 통행가능"만 남는다(2026-08-08 연구대 사건).
    //
    // 배치로 지은 건물은 이미 `OccupyTiles`가 불렸으므로 여기서 다시 하지 않는다.
    void Start()
    {
        if (_occupiedTiles.Count > 0) return;             // 배치 경로가 이미 등록했다
        OccupyFootprint();
    }

    // **자기 크기만큼 점유한다.** 배치·청사진·씬 저작이 같은 계산을 쓰도록 여기 모았다 —
    // 청사진은 한때 `WorldToTile(position)` 한 칸만 막아서, 3×2 건물이 지어지고 나면
    // 나머지 다섯 칸을 폰이 그냥 통과했다.
    public void OccupyFootprint()
    {
        if (data == null || !data.OccupiesTile) return;
        if (MapManager.Instance == null) return;

        OccupyTiles(BlockingAt(transform.position, data, transform.rotation));
    }

    // ── 발자국과 "막는 칸"은 다른 물음이다 ──────────────────────────────────
    //
    // **발자국** = 이 건물이 깔고 앉은 칸 전부(겹쳐 짓지 못하게 하는 축).
    // **막는 칸** = 그중 길을 막는 칸(길찾기가 보는 축).
    //
    // 여태 둘이 같았다 — `OccupiesTile` 하나가 사각형 전체에 걸렸으니까. 걸어 들어가는
    // 건물에서는 갈린다: **벽은 막고 안쪽과 문간은 튼다.** 마스크가 없으면 옛날 그대로다.
    public static IEnumerable<Vector2Int> BlockingAt(Vector3 center, BuildingData data, Quaternion rot)
    {
        if (data == null || !data.OccupiesTile) yield break;

        int W = Mathf.Max(1, data.Size.x);
        int H = Mathf.Max(1, data.Size.y);

        int quarter = Mathf.RoundToInt(rot.eulerAngles.y / 90f) & 3;
        int w = W, h = H;
        if (quarter % 2 == 1) (w, h) = (h, w);

        var lo = LowerLeftTile(center, w, h);

        for (int x = 0; x < w; x++)
        for (int z = 0; z < h; z++)
        {
            // 세계 칸 → 회전 전 칸. **90°와 270°는 서로의 역이다** — 한쪽 식을 베껴
            // 부호만 바꾸면 문이 반대편에 뚫린다.
            int lx, lz;
            switch (quarter)
            {
                case 1:  lx = W - 1 - z; lz = x;             break;
                case 2:  lx = W - 1 - x; lz = H - 1 - z;     break;
                case 3:  lx = z;         lz = H - 1 - x;     break;
                default: lx = x;         lz = z;             break;
            }

            if (data.BlocksLocal(lx, lz)) yield return new Vector2Int(lo.x + x, lo.y + z);
        }
    }

    // 폰이 서는 자리를 고를 때 후보에서 빠지는 칸 (`IFootprintSite`).
    //
    // **깔고 앉은 칸이 아니라 막는 칸이다** — 걸어 들어가는 건물의 안쪽은 트여 있고,
    // 거기가 곧 침대·작업대 앞에 서는 자리다. 발자국 전체를 빼면 **제 건물 안에서
    // 일할 자리가 없어진다.**
    public IEnumerable<Vector2Int> Footprint =>
        data != null ? BlockingAt(transform.position, data, transform.rotation)
                     : System.Array.Empty<Vector2Int>();

    // 중심에서 크기만큼 펼친 칸들. `BuildModeManager.GetFootprint`가 만드는 것과 같은 칸이
    // 나오고, 격자에서 반 칸쯤 어긋나게 놓인 건물도 반올림으로 제자리를 찾는다.
    public static IEnumerable<Vector2Int> FootprintAt(Vector3 center, Vector2Int size, Quaternion rot)
    {
        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);

        // 90°/270°면 축이 바뀐다(`BuildModeManager.FootprintSize`와 같은 규칙).
        int quarter = Mathf.RoundToInt(rot.eulerAngles.y / 90f) & 3;
        if (quarter % 2 == 1) (w, h) = (h, w);

        var lo = LowerLeftTile(center, w, h);

        for (int x = 0; x < w; x++)
        for (int z = 0; z < h; z++)
            yield return new Vector2Int(lo.x + x, lo.y + z);
    }

    // 발자국의 좌하단 **칸**. 월드 좌표를 칸으로 바꾸는 일은 `MapManager.WorldToTile` 하나가
    // 답한다 — **여기서 반올림으로 직접 구하면 안 된다.**
    //
    // 반올림은 격자가 세계 원점에 박혀 있을 때만 맞는 계산이다. 08-27에 위상이 첫 조작으로
    // 옮겨 간 뒤로 위상이 0이 아니게 됐고, 그때부터 **발자국이 통째로 한 칸 밀렸다.**
    // 1×1에서는 티가 안 났지만(옆 칸을 막을 뿐) 5×4 오두막에서 드러났다 — **문간이 한 칸
    // 옆으로 등록돼 폰이 벽으로 걸어갔고**, 안쪽도 한 줄이 잘려 침대를 놓을 자리가
    // 여섯 칸에서 두 칸으로 줄었다.
    //
    // 중심에서 반 칸 물러난 **좌하단 칸의 한복판**을 물어본다(모서리를 물으면 부동소수
    // 오차로 옆 칸에 떨어진다).
    static Vector2Int LowerLeftTile(Vector3 center, int w, int h)
    {
        var map = MapManager.Instance;
        var probe = new Vector3(center.x - w * 0.5f + 0.5f, 0f, center.z - h * 0.5f + 0.5f);

        return map != null
            ? map.WorldToTile(probe)
            : new Vector2Int(Mathf.FloorToInt(probe.x), Mathf.FloorToInt(probe.z));
    }

    // 배치 시 부착 + BuildingData 연결.
    public static PlacedBuilding Attach(GameObject go, BuildingData data)
    {
        if (go == null) return null;
        var pb = go.GetComponent<PlacedBuilding>() ?? go.AddComponent<PlacedBuilding>();
        pb.data = data;
        return pb;
    }

    // footprint 타일들을 통행 불가로 등록하고 소유 목록에 보관 (BuildingData.OccupiesTile일 때만 호출).
    public void OccupyTiles(IEnumerable<Vector2Int> tiles)
    {
        if (MapManager.Instance == null) return;

        var claimed = new List<Vector2Int>();
        foreach (var t in tiles)
        {
            MapManager.Instance.SetTile(t, TileData.MakeImpassable());
            _occupiedTiles.Add(t);
            claimed.Add(t);
        }

        EvictPawns(claimed);
    }

    // **막은 칸 위에 서 있던 폰을 밖으로 내보낸다.**
    //
    // 건물 대부분(33종 중 28종)은 즉시 배치고, 배치는 폰을 전혀 안 본다 — 폰이 선 자리에 3×2
    // 연구대가 그냥 생긴다. 그냥 두면 **못 나온다**: 길찾기는 출발 칸이 막힌 것을 풀어 주지
    // 않으므로 어떤 목적지를 줘도 경로가 안 나오고, 폰은 건물 안에서 일감을 집었다 놓기만 한다.
    //
    // **걷는 중인 폰은 건드리지 않는다.** 그 폰은 이미 계산해 둔 경로를 타고 제 발로 빠져나가고,
    // 지나가는 것뿐인데 순간이동시키면 가던 길이 끊긴다. 순간이동은 스스로 못 나오는 폰,
    // 즉 **거기 서 있던 폰**에게만 필요하다.
    static void EvictPawns(List<Vector2Int> tiles)
    {
        if (tiles.Count == 0) return;
        var map = MapManager.Instance;
        if (map == null) return;

        var blocked = new HashSet<Vector2Int>(tiles);

        foreach (var brain in FindObjectsByType<UnitBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var from = map.WorldToTile(brain.transform.position);
            if (!blocked.Contains(from)) continue;

            // 목표만 있고 경로가 없으면 걷는 게 아니라 **막혀 선 것**이다 — 그건 내보낸다.
            var mover = brain.GetComponent<PawnMover>();
            if (mover != null && mover.HasTarget() && mover.HasLocalPath) continue;

            var to = map.FindWorkableNear(from, 4, blocked);
            if (blocked.Contains(to)) continue;   // 나갈 데가 없으면 그대로 둔다 — 옮기는 시늉만 하면 더 나쁘다

            Teleport(brain, map.TileToGround(to));
        }
    }

    // 표면 **위에** 내려놓는다 — 파묻힘·되밀림 두 함정은 `PawnPlacement`가 한 곳에서 다룬다.
    static void Teleport(UnitBrain brain, Vector3 groundPos)
    {
        PawnPlacement.PlaceOnGround(brain.gameObject, groundPos);

        // 걸어오던 경로는 여기서 쓸모없다. 목표를 지우면 BT가 다음 틱에 새 자리에서 다시 계획한다.
        brain.GetComponent<PawnMover>()?.StopMovement();
        Debug.Log($"[PlacedBuilding] {brain.name} — 건물이 들어선 칸에서 밖으로 옮김");
    }

    // 마우스 위치의 건물 PlacedBuilding 반환 (없으면 null).
    public static PlacedBuilding PickUnderCursor(Camera cam, Vector2 screenPos)
    {
        if (cam == null) return null;
        var ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 500f))
            return hit.collider.GetComponentInParent<PlacedBuilding>();
        return null;
    }

    // ── 철거 ────────────────────────────────────────────────────────────────
    public void Demolish()
    {
        var pos = transform.position;

        Refund();
        ReleaseTiles();
        Destroy(gameObject);

        // 벽 등 철거 후 메소 격자 복구
        HierarchicalPathfinder.Instance?.RebakeObstacleAt(pos);
    }

    // 자기가 등록한 점유 타일만 해제 → 같은 타일을 막고 있는 자원노드 등 타 점유는 건드리지 않음.
    // 철거와 **이전**이 같은 것을 쓴다 — 옮기면서 옛 칸을 안 풀면 그 자리가 영영 막힌다.
    public void ReleaseTiles()
    {
        if (MapManager.Instance != null)
            foreach (var t in _occupiedTiles)
                MapManager.Instance.SetTile(t, TileData.MakePassable());
        _occupiedTiles.Clear();
    }

    // ── 이전 ────────────────────────────────────────────────────────────────
    //
    // **자재를 안 받고 안 돌려준다** — 옮기는 것은 짓는 것도 부수는 것도 아니다. 값을 물리면
    // 배치를 고쳐 보는 일에 벌칙이 붙고, 환불이 전액인 마당에 "철거 후 재건축"이 늘 더 싸지므로
    // 값을 받아 봐야 조작만 늘어난다(부수고 다시 짓기).
    //
    // 안에 든 것도 그대로 간다 — 창고 내용물·작업대 산출물·연구 진행은 컴포넌트가 들고 있고
    // 우리는 `transform`만 옮긴다.
    public void MoveTo(Vector3 pos, Quaternion rot)
    {
        var from = transform.position;

        ReleaseTiles();
        transform.SetPositionAndRotation(pos, rot);
        OccupyFootprint();          // 새 칸을 막고, 거기 서 있던 폰을 밖으로 내보낸다

        // 벽처럼 장애물인 건물은 **떠난 자리와 새 자리 둘 다** 다시 구워야 한다.
        HierarchicalPathfinder.Instance?.RebakeObstacleAt(from);
        HierarchicalPathfinder.Instance?.RebakeObstacleAt(pos);

        // 영역은 그대로고 건물만 움직였다 — `OnTerritoryChanged`가 안 뜨므로 스스로 다시 본다.
        GetComponent<BuildingDormancy>()?.Reevaluate();

        Debug.Log($"[PlacedBuilding] {(data != null ? data.BuildingName : name)} 이전 — {from} → {pos}");
    }

    void Refund()
    {
        if (data == null || data.CostAmount <= 0) return;
        int refund = Mathf.FloorToInt(data.CostAmount * refundRatio);
        if (refund <= 0) return;

        var item = data.CostItem;
        if (item == null) return; // 비용 아이템이 없는 건물(무료) → 환불할 것도 없다

        // **창고로 먼저, 바닥은 최후 수단**(`MaterialPool.Refund`) — 청사진 취소와 같은 길이다.
        // 전엔 곧장 바닥에 떨궜는데, 그러면 그 짐이 다시 운반 일감이 되고 나르는 동안 창고
        // 집계에서 빠져 있어 같은 자재를 기다리던 작업이 "재료 부족"으로 죽는다.
        MaterialPool.Refund(item, refund, transform.position);
    }

    // ── 철거 예정 빨간 표시 ─────────────────────────────────────────────────
    public void SetPendingHighlight(bool on)
    {
        if (on == _highlighted) return;
        _highlighted = on;

        _renderers ??= GetComponentsInChildren<Renderer>();
        ApplyTint(_renderers, on, PendingColor);
    }

    // **청사진도 같은 표시를 쓴다** — 철거 모드가 둘을 같이 집으므로 색이 다르면 무엇이 잡혔는지
    // 눈으로 못 가른다. 색만 인자로 받는다(청사진 고스트는 반투명이라 알파가 다르다).
    public static void ApplyTint(Renderer[] renderers, bool on, Color color)
    {
        if (renderers == null) return;

        var mpb = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (on)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, color);
                mpb.SetColor(ColorId,     color);
                r.SetPropertyBlock(mpb);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }
}
