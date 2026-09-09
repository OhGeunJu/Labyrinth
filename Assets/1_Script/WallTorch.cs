using UnityEngine;

// **벽걸이 횃불 — 벽이 있으면 붙고, 없으면 선다**(2026-09-04, 사용자 결정:
// *"벽에 닿으면 공중에 떠서 붙어있고 아무 벽도 없으면 바닥 가운데에 우뚝 서있게 마크처럼"*).
//
// ## 왜 건물을 둘로 안 나누나
//
// `벽걸이 횃불`과 `세운 횃불`을 따로 만들면 **플레이어가 놓기 전에 그 칸이 벽에 닿는지를
// 알아야 한다** — 통로를 따라 스무 개를 놓는 동안 칸마다 그걸 재게 되고, 잘못 고르면
// 벽에 안 붙거나 아예 안 놓인다. 마인크래프트의 횃불이 하나인 것과 같은 이유다:
// **놓는 사람은 "여기 불"만 말하고, 어떻게 붙을지는 자리가 답한다.**
//
// 그래서 여기가 답하는 것은 **어떤 모양으로 설 것인가** 하나다. 언제 켜지는지는
// `Campfire`가, 얼마나 밝은지는 `LightSource`가 이미 답한다 — 불을 새로 만들지 않는다.
//
// ## 칸을 안 막는다 — 두 모양 다
//
// 벽에 붙은 것은 공중에 떠 있으니 당연하고, **바닥에 선 것도 안 막는다**(`OccupiesTile`
// 꺼짐 · 콜라이더는 트리거). 그것이 이 물건이 화톳불과 갈리는 자리다 — 화톳불은 칸을
// 먹는 넓은 불이고, 횃불은 **통로를 밝히되 좁히지 않는다.** 막으면 한 칸짜리 굴에
// 불을 켜는 순간 그 길이 끊긴다.
//
// ## 벽이 캐여 없어지면 내려앉는다
//
// 지하에서는 벽이 **없어지는 물건**이다(채굴). 붙을 때 한 번만 재고 말면 벽을 판 뒤에도
// 횃불이 **허공에 떠 있는다.** 그래서 `RockMap.OnChanged`·`MapManager.OnTileChanged`를
// 듣고, **맞닿은 넷과 제 칸이 바뀔 때만** 다시 잰다(세계의 모든 칸에 반응하면 굴을 파는
// 동안 횃불 수만큼 헛일이 돈다).
// **`[RequireComponent]`를 안 단다**(2026-09-04). 이 스크립트는 `LightSource`를 **코드에서
// 한 번도 안 부른다** — 불은 `Campfire`가 켜고 여기는 모양만 정한다. 그런데 달아 두었더니
// `BuildModeManager.EnsureGhost`가 미리보기에서 스크립트를 뗄 때 **유니티가 거절했다**
// (*"Can't remove LightSource because WallTorch depends on it"*). **안 쓰는 의존은 선언하지
// 않는다** — 그것이 지키는 것은 없고 막는 것만 있다.
public class WallTorch : MonoBehaviour, IPlacementPose
{
    [Tooltip("위치·회전을 옮기는 자리. 불꽃과 등이 여기 매달려 있어야 벽에 붙을 때 같이 올라간다.")]
    [SerializeField] Transform mount;

    [Tooltip("벽이 없을 때 켜는 모형 — 바닥에 우뚝 선 횃불.")]
    [SerializeField] GameObject standing;

    [Tooltip("벽이 있을 때 켜는 모형 — 벽에 거는 횃불.")]
    [SerializeField] GameObject wall;

    [Tooltip("불꽃과 등이 달린 자리. **두 모형은 키가 달라서** 여기가 모양마다 다시 놓인다.")]
    [SerializeField] Transform head;

    [Tooltip("바닥에 섰을 때 불꽃 높이(m). 모형 꼭대기 언저리여야 자루 끝에서 타는 것으로 보인다.")]
    [SerializeField] float standHead = 1.35f;

    [Tooltip("벽에 걸었을 때 불꽃 높이(m) — **걸린 자리에서 잰다**(이미 wallHeight만큼 올라가 있다).")]
    [SerializeField] float wallHead = 0.35f;

    [Tooltip("벽 쪽으로 얼마나 밀어붙이나(m). 칸 반이 0.5라 그보다 조금 작아야 벽면에 닿는다.")]
    [SerializeField] float wallOffset = 0.42f;

    [Tooltip("벽에 걸 높이(m). 바닥이 아니라 눈높이 위여야 '걸었다'로 보인다.")]
    [SerializeField] float wallHeight = 1.1f;

    [Tooltip("벽걸이 모형이 벽을 등지도록 돌리는 보정각. 모형이 반대로 보면 180을 넣는다.")]
    [SerializeField] float wallYaw;

    // 맞닿은 넷. **대각선은 안 본다** — 모서리만 닿은 벽에는 걸 면이 없다.
    static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0),
    };

    bool _onWall;
    bool _applied;

    void OnEnable()
    {
        RockMap.OnChanged        += OnTile;
        MapManager.OnTileChanged += OnTile;
        Apply();
    }

    void OnDisable()
    {
        RockMap.OnChanged        -= OnTile;
        MapManager.OnTileChanged -= OnTile;
    }

    // **`Start`에서 한 번 더 잰다** — `OnEnable`은 `MapManager`보다 먼저 올 수 있고, 그때는
    // 벽을 물어볼 곳이 없어 **전부 바닥에 선 모양**으로 굳는다.
    void Start() => Apply();

    void OnTile(Vector2Int tile)
    {
        if (!_applied) return;
        var map = MapManager.Instance;
        if (map == null) return;

        var here = map.WorldToTile(transform.position);
        if (tile == here) { Apply(); return; }
        foreach (var d in Dirs)
            if (tile == here + d) { Apply(); return; }
    }

    // 지금 서 있는 자리로. **런타임의 문**이고, 미리보기·청사진은 `PoseAt`을 직접 부른다.
    void Apply()
    {
        var map = MapManager.Instance;
        if (map == null) return;

        PoseAt(map.WorldToTile(transform.position));
    }

    // **이 칸에 놓인다면 어떤 모양인가**(`IPlacementPose`). 미리보기 고스트·청사진·완성품이
    // 전부 여기로 온다 — 갈리면 **미리보기는 벽에 붙는다고 해 놓고 완성되면 바닥에 선다.**
    //
    // 칸을 인자로 받는 이유는 고스트가 **아직 제 자리에 없을 수도** 있기 때문이다(줄 긋기의
    // 여벌은 한 프레임 안에 여러 칸을 돈다). 회전만은 트랜스폼에서 읽는다 — 부르는 쪽이
    // 이미 잡아 둔 값이라 따로 받으면 두 벌이 된다.
    public void PoseAt(Vector2Int tile)
    {
        var map = MapManager.Instance;
        if (map == null) return;

        _onWall  = TryFindWall(map, tile, out var dir);
        _applied = true;

        if (standing != null) standing.SetActive(!_onWall);
        if (wall     != null) wall.SetActive(_onWall);
        if (mount    == null) return;

        if (!_onWall)
        {
            mount.localPosition = Vector3.zero;
            mount.localRotation = Quaternion.identity;
            SetHead(standHead);
            return;
        }

        // **방향은 월드에서 냈으니 부모 기준으로 되돌린다** — 건물이 회전한 채 놓이면
        // 그대로 쓴 값이 그 회전만큼 돌아가서 **옆 칸 벽에 붙는다.**
        var local = transform.InverseTransformDirection(dir);
        mount.localPosition = local * wallOffset + Vector3.up * wallHeight;
        mount.localRotation = Quaternion.LookRotation(-local, Vector3.up) * Quaternion.Euler(0f, wallYaw, 0f);
        SetHead(wallHead);
    }

    // **불꽃과 등은 같이 옮긴다** — 등만 옮기면 불꽃이 자루 밑에서 타고, 불꽃만 옮기면
    // 밝은 자리와 타는 자리가 갈린다(화면과 판정이 어긋나는 그 자리의 작은 판이다).
    void SetHead(float y)
    {
        if (head != null) head.localPosition = new Vector3(0f, y, 0f);
    }

    // 맞닿은 넷 중 벽인 것. **놓을 때 돌린 방향에 가장 가까운 것을 고른다** — 구석에서는
    // 두 면이 다 벽인데, 아무거나 고르면 같은 자리에 다시 놓아도 다른 데 붙어서
    // *어느 벽에 걸지*가 플레이어의 손을 떠난다(회전 키가 곧 그 선택이 된다).
    bool TryFindWall(MapManager map, Vector2Int here, out Vector3 dir)
    {
        dir = Vector3.zero;

        var fwd   = transform.forward;
        float best = float.NegativeInfinity;

        foreach (var d in Dirs)
        {
            var tile = here + d;
            if (!IsWall(map, tile)) continue;

            var w = new Vector3(d.x, 0f, d.y);
            float score = Vector3.Dot(w, fwd);
            if (score <= best) continue;

            best = score;
            dir  = w;
        }

        return best > float.NegativeInfinity;
    }

    // **벽은 바위만이 아니다** — 지은 벽·오두막 껍데기도 걸 면이다. 통행을 막는 칸이면
    // 그 자리에 무언가 서 있다는 뜻이고, 그것이 곧 면이다.
    static bool IsWall(MapManager map, Vector2Int tile)
        => RockMap.IsRock(tile) || map.IsOccupied(tile);
}
