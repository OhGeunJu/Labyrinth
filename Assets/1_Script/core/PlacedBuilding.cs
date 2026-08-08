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
    [SerializeField, Range(0f, 1f)] float refundRatio = 0.75f;

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

        OccupyTiles(FootprintAt(transform.position, data.Size, transform.rotation));
    }

    // 이 건물이 깔고 앉는 칸들 (`IFootprintSite`) — 폰이 서는 자리를 고를 때 후보에서 빠진다.
    public IEnumerable<Vector2Int> Footprint =>
        data != null ? FootprintAt(transform.position, data.Size, transform.rotation)
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

        int x0 = Mathf.RoundToInt(center.x - w * 0.5f);
        int z0 = Mathf.RoundToInt(center.z - h * 0.5f);

        for (int x = 0; x < w; x++)
        for (int z = 0; z < h; z++)
            yield return new Vector2Int(x0 + x, z0 + z);
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

    // 표면 **위에** 내려놓는다. `TileToGround`는 바닥 윗면이라 그대로 쓰면 `CharacterController`의
    // 아래 절반이 파묻히고, 유니티가 겹침을 풀며 옆으로 밀어낸다 — 그 자리가 가장자리면 그대로
    // 떨어진다(`EventManager.SpawnPawn`이 스폰에서 같은 보정을 한다, 2026-08-08).
    static void Teleport(UnitBrain brain, Vector3 groundPos)
    {
        var cc     = brain.GetComponent<CharacterController>();
        float lift = cc != null ? cc.height * 0.5f - cc.center.y : 1f;

        // 켜진 채로 옮기면 콜라이더가 제자리로 되민다.
        if (cc != null) cc.enabled = false;
        brain.transform.position = new Vector3(groundPos.x, groundPos.y + lift + 0.05f, groundPos.z);
        if (cc != null) cc.enabled = true;

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

        // 자기가 등록한 점유 타일만 해제 → 같은 타일을 막고 있는 자원노드 등 타 점유는 건드리지 않음.
        if (MapManager.Instance != null)
            foreach (var t in _occupiedTiles)
                MapManager.Instance.SetTile(t, TileData.MakePassable());

        Destroy(gameObject);

        // 벽 등 철거 후 메소 격자 복구
        HierarchicalPathfinder.Instance?.RebakeObstacleAt(pos);
    }

    void Refund()
    {
        if (data == null || data.CostAmount <= 0) return;
        int refund = Mathf.FloorToInt(data.CostAmount * refundRatio);
        if (refund <= 0) return;

        var item = data.CostItem;
        if (item == null) return; // 비용 아이템이 없는 건물(무료) → 환불할 것도 없다

        // 철거 위치에 드랍 → 자동으로 가장 가까운 창고로 운반.
        DroppedItem.Spawn(item, refund, transform.position);
    }

    // ── 철거 예정 빨간 표시 ─────────────────────────────────────────────────
    public void SetPendingHighlight(bool on)
    {
        if (on == _highlighted) return;
        _highlighted = on;

        _renderers ??= GetComponentsInChildren<Renderer>();
        var mpb = new MaterialPropertyBlock();
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            if (on)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, PendingColor);
                mpb.SetColor(ColorId,     PendingColor);
                r.SetPropertyBlock(mpb);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }
}
