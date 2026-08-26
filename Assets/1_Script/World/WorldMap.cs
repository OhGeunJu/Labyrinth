using System;
using System.Collections.Generic;
using UnityEngine;

// 월드맵 노드의 내용물.
public enum WorldNodeContent
{
    Empty,        // 빈 야생 — 진입 시 임시 원정 맵 생성, 코어 박으면 정착지화
    MySettlement, // 내 정착지 (코어 있음)
    Town,         // NPC 마을 (상인 등)
    EnemyLair,    // 적 소굴
    ResourceSite, // 특수 자원 지대 (매장량이 두터운 칸)
    Outpost,      // 내 채집기지 — 매장 자원을 매일 캐서 쌓아둔다(사람은 살지 않는다)
}

public static class WorldNodeContentExt
{
    public static string DisplayName(this WorldNodeContent c) => c switch
    {
        WorldNodeContent.Empty        => "빈 지역",
        WorldNodeContent.MySettlement => "내 정착지",
        WorldNodeContent.Town         => "마을",
        WorldNodeContent.EnemyLair    => "적 소굴",
        WorldNodeContent.ResourceSite => "자원 지대",
        WorldNodeContent.Outpost      => "채집기지",
        _                             => c.ToString(),
    };

    // POI 마커 색 — 런타임 지도와 에디터 미리보기 공용(단일 출처).
    public static UnityEngine.Color Color(this WorldNodeContent c) => c switch
    {
        WorldNodeContent.MySettlement => new Color(1f, 0.9f, 0.3f),
        WorldNodeContent.Town         => new Color(0.4f, 0.6f, 1f),
        WorldNodeContent.EnemyLair    => new Color(0.9f, 0.2f, 0.2f),
        WorldNodeContent.ResourceSite => new Color(0.3f, 0.85f, 0.85f),
        WorldNodeContent.Outpost      => new Color(0.85f, 0.65f, 0.35f),
        _                             => UnityEngine.Color.white,
    };

    // 지도 위 한 글자 표기(에디터 격자용).
    public static string Symbol(this WorldNodeContent c) => c switch
    {
        WorldNodeContent.MySettlement => "핵",
        WorldNodeContent.Town         => "마",
        WorldNodeContent.EnemyLair    => "적",
        WorldNodeContent.ResourceSite => "자",
        WorldNodeContent.Outpost      => "굴",
        _                             => "",
    };
}

// 월드맵의 한 노드(칸).
// 안개(Discovered)는 없다 — 2026-06-20 결정으로 폐기. 지형·내용물은 처음부터 전부 보인다.
// 발견의 긴장은 콘텐츠 은닉이 아니라 **거리 + 위험도 게이팅**("보이지만 못 감")으로 낸다.
// 높이/생김새는 여기 없다 — 지형은 씬에 직접 만들어 붙인 Terrain이고, 격자는 그 위에 얹힌 논리 레이어다.
// 표면 좌표가 필요하면 `WorldMapGrid.SurfacePoint(node)`가 Terrain을 샘플해 알려준다.
[Serializable]
public struct WorldTile
{
    public Biome Biome;
    public WorldNodeContent Content;
}

// 추상 바둑판 월드맵 — 필드맵(MapManager)과 분리된 전략/탐험 레이어.
// - 1 노드 = 한 지역. 노드 진입 시 그 노드의 필드맵을 로드/생성(step 4).
// - 디테일 시뮬되는 필드맵은 항상 1개. 월드맵 데이터는 항상 메모리 상주.
// - 이동/거리는 노드 단위. `GameTimeManager` 일수 축과 연동(step 4).
//
// 내용은 **저작한다. 절차 생성하지 않는다.** `mapData`(WorldMapData SO)를 연결하면 그걸 그대로 싣는다.
// 저작은 `DungeonColony → World Map Editor`에서 격자에 칠한다.
// 데이터가 없으면 빈 맵(전부 미지정)으로 뜨고 경고를 남긴다 — 조용히 아무 지도나 만들어내지 않는다.
[DefaultExecutionOrder(-60)]
public class WorldMap : MonoBehaviour
{
    public static WorldMap Instance { get; private set; }

    [Header("저작 데이터 (World Map Editor로 편집)")]
    [SerializeField] WorldMapData mapData;

    [Header("데이터가 없을 때의 빈 맵 크기")]
    [SerializeField] int width = 16;
    [SerializeField] int height = 16;
    [SerializeField] Vector2Int startNode = new Vector2Int(8, 8);

    [Header("매장 자원")]
    [Tooltip("칸별 매장량을 뽑는 시드. 바꾸면 배치는 그대로 두고 묻힌 양만 다시 굴린다.")]
    [SerializeField] int depositSeed = 20260731;

    public int DepositSeed => depositSeed;

    public int Width => width;
    public int Height => height;
    public Vector2Int StartNode => startNode;

    WorldTile[,] _tiles;

    // 내 정착지 노드 목록 (전환 UI/오프라인 생산 대상)
    readonly List<Vector2Int> _settlements = new();
    public IReadOnlyList<Vector2Int> Settlements => _settlements;

    // 노드 내용 변경 통보 (뷰 갱신용)
    public static event Action<Vector2Int> OnNodeChanged;

    // 조사(탐험)를 마친 칸. **안개는 없다** — 지형·바이옴·내용물은 처음부터 다 보인다.
    // 가려지는 건 **세부 수치(매장량)**뿐이고, 그건 가서 조사해야 드러난다.
    // 그래야 "어디에 전초기지를 세울까"를 정하려고 발품을 팔 이유가 생긴다.
    readonly HashSet<Vector2Int> _surveyed = new();

    public bool IsSurveyed(Vector2Int node) => _surveyed.Contains(node);

    public void MarkSurveyed(Vector2Int node)
    {
        if (InBounds(node)) _surveyed.Add(node);
    }

    // 그 칸에 묻힌 자원. 조사 여부와 무관하게 **값 자체는 항상 같다**(결정적) —
    // 조사는 그 값을 보여줄지 말지를 정할 뿐이다.
    public List<WorldDeposit> DepositsAt(Vector2Int node) =>
        WorldDeposits.At(node, GetTile(node), depositSeed);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Generate();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Generate()
    {
        // 마을 시장은 칸 좌표를 열쇠로 static에 남는다 — 지도를 새로 지으면 옛 좌표의
        // 상점이 엉뚱한 마을에 붙는다(도메인 리로드를 끄면 플레이 정지 뒤에도 살아남는다).
        TownMarket.Clear();
        TownRecruits.Clear();
        WorldLairs.Clear();

        if (mapData != null && mapData.IsSized) LoadAuthored();
        else                                    BuildEmpty();

        // 시작 노드는 반드시 내 정착지 — `SettlementManager.settlements[0]`이 여기 앉는다.
        if (InBounds(startNode))
        {
            _tiles[startNode.x, startNode.y].Content = WorldNodeContent.MySettlement;
            if (!_settlements.Contains(startNode)) _settlements.Add(startNode);
        }
        else
        {
            Debug.LogWarning($"[WorldMap] 시작 노드 {startNode}가 맵 범위(0~{width - 1}, 0~{height - 1}) 밖입니다.");
        }
    }

    // 저작 데이터를 그대로 싣는다. 크기·시작 노드도 데이터가 정한다(인스펙터 값은 무시).
    void LoadAuthored()
    {
        width     = mapData.Width;
        height    = mapData.Height;
        startNode = mapData.StartNode;

        _tiles = new WorldTile[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            var n = mapData.Get(x, y);
            _tiles[x, y] = new WorldTile { Biome = n.Biome, Content = n.Content };

            // 저작자가 찍어둔 정착지도 전부 내 거점으로 등록.
            if (n.Content == WorldNodeContent.MySettlement)
                _settlements.Add(new Vector2Int(x, y));
        }
    }

    // 저작 데이터가 없을 때의 빈 맵. **절차 생성이 아니다** — 전부 미지정으로 두고 경고를 남겨
    // "저작을 아직 안 했다"가 화면에서도 로그에서도 드러나게 한다.
    void BuildEmpty()
    {
        _tiles = new WorldTile[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            _tiles[x, y] = new WorldTile { Biome = Biome.Unknown, Content = WorldNodeContent.Empty };

        if (InBounds(startNode)) _tiles[startNode.x, startNode.y].Biome = Biome.Plains;

        string why = mapData == null ? "mapData 미연결" : "mapData의 배열 크기가 Width×Height와 어긋남";
        Debug.LogWarning($"[WorldMap] 저작 데이터가 없어 빈 맵({width}×{height})으로 시작합니다 — {why}. " +
                         "`DungeonColony → World Map Editor`에서 저작하고 [씬에 연결]하세요.");
    }

    // ── 조회 ─────────────────────────────────────────────────────────────────

    public bool InBounds(Vector2Int node) =>
        node.x >= 0 && node.x < width && node.y >= 0 && node.y < height;

    public WorldTile GetTile(Vector2Int node) =>
        InBounds(node) ? _tiles[node.x, node.y] : default;

    // **저작한 바이옴을 인스턴스 없이 읽는다** — 에디터에서 지형을 조각할 때는 `Awake`가
    // 안 돌아 `Instance`도 `_tiles`도 없다. 그때 기본값(평원)으로 떨어지면 **도구가 게임과
    // 다른 지형을 조각하고**, 구운 델타가 통째로 어긋난다(그 함정은 연구 재료에서 이미 겪었다).
    // 저작한 지도의 한 변 — `TerrainGrid.RegionsPerAxis`가 이걸 읽는다.
    // **지역의 크기는 세계가 아니라 저작한 해상도가 정한다.**
    public static int AuthoredWidth
    {
        get
        {
            var inst = Instance;
            if (inst != null && inst.width > 0) return inst.width;

            var src = inst != null ? inst.mapData
                    : UnityEngine.Object.FindFirstObjectByType<WorldMap>(FindObjectsInactive.Include)?.mapData;
            return src != null && src.Width > 0 ? src.Width : 16;
        }
    }

    // 저작한 칸 내용물 — 바이옴과 같은 이유로 인스턴스 없이도 읽힌다(도구가 쓴다).
    public static WorldNodeContent AuthoredContent(int x, int y)
    {
        var inst = Instance;
        if (inst != null && inst._tiles != null && inst.InBounds(new Vector2Int(x, y)))
            return inst._tiles[x, y].Content;

        var src = inst != null ? inst.mapData
                : UnityEngine.Object.FindFirstObjectByType<WorldMap>(FindObjectsInactive.Include)?.mapData;
        if (src != null && src.IsSized && src.InBounds(x, y)) return src.Get(x, y).Content;

        return WorldNodeContent.Empty;
    }

    public static Biome AuthoredBiome(int x, int y)
    {
        var inst = Instance;
        if (inst != null && inst._tiles != null && inst.InBounds(new Vector2Int(x, y)))
            return inst._tiles[x, y].Biome;

        var src = inst != null ? inst.mapData
                : UnityEngine.Object.FindFirstObjectByType<WorldMap>(FindObjectsInactive.Include)?.mapData;
        if (src != null && src.IsSized && src.InBounds(x, y)) return src.Get(x, y).Biome;

        return Biome.Plains;
    }

    // ── 변경 ─────────────────────────────────────────────────────────────────

    public void SetContent(Vector2Int node, WorldNodeContent content)
    {
        if (!InBounds(node)) return;
        _tiles[node.x, node.y].Content = content;
        OnNodeChanged?.Invoke(node);
    }

    // 코어를 박아 정착지로 승격 (step 4의 코어 배치에서 호출).
    public void RegisterSettlement(Vector2Int node)
    {
        if (!InBounds(node)) return;
        if (!_settlements.Contains(node)) _settlements.Add(node);
        _tiles[node.x, node.y].Content = WorldNodeContent.MySettlement;
        OnNodeChanged?.Invoke(node);
    }

    // ── 이동/인접 ─────────────────────────────────────────────────────────────

    // 4방향 인접 노드 (대각 제외 — 바둑판 이동).
    public IEnumerable<Vector2Int> Neighbors(Vector2Int node)
    {
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            var n = node + d;
            if (InBounds(n)) yield return n;
        }
    }

    // 두 노드 간 이동 거리(맨해튼) — 파티 이동 일수 계산 기준(step 4).
    public static int Distance(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
}
