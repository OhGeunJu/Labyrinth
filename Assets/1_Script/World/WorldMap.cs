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
    ResourceSite, // 특수 자원 지대
}

// 월드맵의 한 노드(칸).
[Serializable]
public struct WorldTile
{
    public Biome Biome;
    public WorldNodeContent Content;
    public bool Discovered;   // 안개 해제 여부
}

// 추상 바둑판 월드맵 — 필드맵(MapManager)과 분리된 전략/탐험 레이어.
// - 1 노드 = 한 지역. 노드 진입 시 그 노드의 필드맵을 로드/생성(step 4).
// - 디테일 시뮬되는 필드맵은 항상 1개(전환형). 월드맵 데이터는 항상 메모리 상주.
// - 이동/거리는 노드 단위(일수). GameTimeManager 일수 축과 연동(step 4).
[DefaultExecutionOrder(-60)]
public class WorldMap : MonoBehaviour
{
    public static WorldMap Instance { get; private set; }

    [Header("월드맵 크기 (노드)")]
    [SerializeField] int width = 16;
    [SerializeField] int height = 16;

    [Header("생성")]
    [Tooltip("0이면 매 실행 랜덤 시드")]
    [SerializeField] int seed = 0;
    [SerializeField] Vector2Int startNode = new Vector2Int(8, 8);

    public int Width => width;
    public int Height => height;
    public Vector2Int StartNode => startNode;

    WorldTile[,] _tiles;

    // 내 정착지 노드 목록 (전환 UI/오프라인 생산 대상)
    readonly List<Vector2Int> _settlements = new();
    public IReadOnlyList<Vector2Int> Settlements => _settlements;

    // 노드 발견/내용 변경 통보 (뷰 갱신용)
    public static event Action<Vector2Int> OnNodeChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Generate();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Generate()
    {
        _tiles = new WorldTile[width, height];
        var rng = new System.Random(seed == 0 ? Environment.TickCount : seed);

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            _tiles[x, y] = new WorldTile
            {
                Biome      = RandomBiome(rng),
                Content    = WorldNodeContent.Empty,
                Discovered = false,
            };
        }

        // 시작 노드 = 내 첫 정착지. 평원 보정 + 발견 + 주변 노드 공개.
        if (InBounds(startNode))
        {
            _tiles[startNode.x, startNode.y].Biome   = Biome.Plains;
            _tiles[startNode.x, startNode.y].Content = WorldNodeContent.MySettlement;
            _settlements.Add(startNode);
            Discover(startNode);
            foreach (var n in Neighbors(startNode)) Discover(n);
        }
    }

    static Biome RandomBiome(System.Random rng)
    {
        // Unknown(0) 제외, Plains~Swamp 중 선택
        int count = (int)Biome.Swamp; // 1..count
        return (Biome)(rng.Next(count) + 1);
    }

    // ── 조회 ─────────────────────────────────────────────────────────────────

    public bool InBounds(Vector2Int node) =>
        node.x >= 0 && node.x < width && node.y >= 0 && node.y < height;

    public WorldTile GetTile(Vector2Int node) =>
        InBounds(node) ? _tiles[node.x, node.y] : default;

    public bool IsDiscovered(Vector2Int node) => InBounds(node) && _tiles[node.x, node.y].Discovered;

    // ── 변경 ─────────────────────────────────────────────────────────────────

    public void Discover(Vector2Int node)
    {
        if (!InBounds(node) || _tiles[node.x, node.y].Discovered) return;
        _tiles[node.x, node.y].Discovered = true;
        OnNodeChanged?.Invoke(node);
    }

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
        Discover(node);
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
