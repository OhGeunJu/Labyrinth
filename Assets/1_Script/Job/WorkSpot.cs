using System.Collections.Generic;
using UnityEngine;

// **폰이 서서 일하는 자리.** 건물 프리팹의 자식으로 두고 위치를 잡는다.
//
// 왜 필요한가: 일감의 목표 칸이 건물 **자기 타일**이었다. 그 칸은 대개 통행 불가라
// 폰은 주변에서 빈 칸을 찾아야 했는데, 건물·노드가 몰려 있으면 그 탐색이 실패하고
// **도달 불가 목적지**가 만들어졌다(`[Work] … 갈 수 없어 놓습니다`가 반복되는 원인).
//
// 자리를 데이터로 못 박으면 두 가지가 생긴다:
//   ① 어디에 서는지가 **저작 대상**이 된다 — 작업대 앞이라는 자연스러운 자리를 지정할 수 있다.
//   ② **건설할 때 보인다** — 건설 모드에서 모든 작업 자리가 표시되므로, 그 칸을 막는 배치를
//      플레이어가 눈으로 피할 수 있다. 막고 나서 폰이 멈추는 걸 보고 원인을 추측할 필요가 없다.
//
// 이 컴포넌트가 없는 건물은 예전처럼 주변에서 자동으로 찾는다(`MapManager.FindWorkableNear`).
public class WorkSpot : MonoBehaviour
{
    // 건설 모드에서만 보이는 표식. 평소엔 꺼 둔다 — 게임 화면에 상시로 떠 있을 것은 아니다.
    [SerializeField] GameObject marker;

    static readonly List<WorkSpot> All = new();

    public Vector2Int Tile =>
        MapManager.Instance != null ? MapManager.Instance.WorldToTile(transform.position) : default;

    void OnEnable()
    {
        All.Add(this);
        BuildModeManager.OnActiveChanged += SetMarkerVisible;
        SetMarkerVisible(BuildModeManager.IsActive);
    }

    void OnDisable()
    {
        All.Remove(this);
        BuildModeManager.OnActiveChanged -= SetMarkerVisible;
    }

    void SetMarkerVisible(bool visible)
    {
        if (marker != null) marker.SetActive(visible);
    }

    // ── 조회 ──────────────────────────────────────────────────────────────
    // 이 건물에서 폰이 서야 할 칸. 순서:
    //   ① 저작된 `WorkSpot` 자식이 있고 **그 칸에 설 수 있으면** 그 칸
    //   ② 아니면 건물 주변에서 설 수 있는 가장 가까운 칸 (반경 2까지)
    //
    // ①에서도 "설 수 있으면"을 확인하는 이유: 저작해 둔 자리를 나중에 다른 건물이 막을 수 있다.
    // 그때 고집하면 폰이 영영 못 간다 — 저작은 **선호**지 보장이 아니다.
    //
    // **건물이 깔고 앉을 칸은 후보에서 뺀다**(`IFootprintSite`). 다 지어진 건물은 그 칸이 이미
    // 통행 불가라 저절로 빠지지만, **청사진은 일부러 안 막는다** — 짓는 동안 폰이 지나다녀야
    // 하기 때문이다. 그래서 발자국을 따로 묻지 않으면 3×2 건물의 한복판이 건설 자리로 뽑히고,
    // 완성되는 순간 그 칸이 막히며 방금 지은 폰이 벽 안에 갇힌다.
    public static Vector2Int TileFor(Component site)
    {
        if (site == null || MapManager.Instance == null) return default;

        var footprint = FootprintOf(site);

        var spot = site.GetComponentInChildren<WorkSpot>(true);
        if (spot != null)
        {
            var t = spot.Tile;
            if (MapManager.Instance.IsWorkable(t) && (footprint == null || !footprint.Contains(t)))
                return t;
        }

        var center = MapManager.Instance.WorldToTile(site.transform.position);

        // 발자국이 크면 반경 2로는 못 빠져나온다 — 절반 + 한 칸이면 어느 방향으로든 밖이다.
        int radius = 2;
        if (footprint != null)
            foreach (var t in footprint)
                radius = Mathf.Max(radius, Mathf.Max(Mathf.Abs(t.x - center.x), Mathf.Abs(t.y - center.y)) + 1);

        return MapManager.Instance.FindWorkableNear(center, radius, footprint);
    }

    // 부모까지 타고 올라가 찾는다 — 일감이 넘기는 것은 `ResearchTable`처럼 건물에 붙은
    // 컴포넌트고, 발자국을 아는 것은 그 위의 `PlacedBuilding` / `BuildingBlueprint`다.
    static HashSet<Vector2Int> FootprintOf(Component site)
    {
        var owner = site.GetComponentInParent<IFootprintSite>();
        if (owner == null) return null;

        var set = new HashSet<Vector2Int>(owner.Footprint);
        return set.Count > 0 ? set : null;
    }
}
