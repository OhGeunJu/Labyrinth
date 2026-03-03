using System.Collections.Generic;
using UnityEngine;

namespace DungeonColony.Map
{
    public class MapManager : MonoBehaviour
    {
        [Header("맵 설정")]
        [SerializeField] int chunkCountX = 3;
        [SerializeField] int chunkCountY = 3;
        [SerializeField] int globalSeed = 0;

        [Header("청크 렌더러 설정")]
        [SerializeField] GameObject rockPrefab;
        [SerializeField] GameObject floorPrefab;
        [SerializeField] GameObject bedrockPrefab;
        [SerializeField] GameObject coreSlotPrefab;
        [SerializeField] float tileSize = 1f;

        readonly Dictionary<Vector2Int, Chunk> _chunks = new();

        public int TotalTilesX => chunkCountX * Chunk.SIZE;
        public int TotalTilesY => chunkCountY * Chunk.SIZE;

        void Awake()
        {
            if (globalSeed == 0) globalSeed = Random.Range(1, int.MaxValue);
            GenerateMap();
        }

        void GenerateMap()
        {
            _chunks.Clear();
            Vector2Int centerChunk = new(chunkCountX / 2, chunkCountY / 2);

            for (int cx = 0; cx < chunkCountX; cx++)
                for (int cy = 0; cy < chunkCountY; cy++)
                {
                    var coord = new Vector2Int(cx, cy);
                    Chunk chunk = (coord == centerChunk)
                        ? ChunkGenerator.GenerateStartChunk(coord, globalSeed)
                        : ChunkGenerator.Generate(coord, globalSeed);

                    _chunks[coord] = chunk;
                    SpawnChunkRenderer(chunk);
                }

            Debug.Log($"[MapManager] 생성 완료: {chunkCountX}×{chunkCountY} 청크, seed={globalSeed}");
        }

        void SpawnChunkRenderer(Chunk chunk)
        {
            // 청크 오브젝트 생성 후 MapManager 하위에 배치
            var go = new GameObject($"Chunk_{chunk.ChunkCoord.x}_{chunk.ChunkCoord.y}");
            go.transform.SetParent(transform);

            var renderer = go.AddComponent<ChunkRenderer>();
            renderer.SetPrefabs(rockPrefab, floorPrefab, bedrockPrefab, coreSlotPrefab, tileSize);
            renderer.Init(chunk);
        }

        // ── 월드 좌표 접근 ──────────────────────────────
        public TileData GetTile(int wx, int wy)
        {
            ToChunkLocal(wx, wy, out var coord, out int lx, out int ly);
            return _chunks.TryGetValue(coord, out var chunk) ? chunk.GetTile(lx, ly) : default;
        }

        public void SetTile(int wx, int wy, TileData tile)
        {
            ToChunkLocal(wx, wy, out var coord, out int lx, out int ly);
            if (_chunks.TryGetValue(coord, out var chunk))
                chunk.SetTile(lx, ly, tile);
        }

        public bool Mine(int wx, int wy, int power)
        {
            var tile = GetTile(wx, wy);
            if (!tile.IsMineable) return false;

            tile.Hardness -= power;
            if (tile.Hardness <= 0)
            {
                SetTile(wx, wy, TileData.Floor());
                return true;
            }
            SetTile(wx, wy, tile);
            return false;
        }

        public bool IsInBounds(int wx, int wy) =>
            wx >= 0 && wx < TotalTilesX && wy >= 0 && wy < TotalTilesY;

        static void ToChunkLocal(int wx, int wy, out Vector2Int coord, out int lx, out int ly)
        {
            int cx = wx >= 0 ? wx / Chunk.SIZE : (wx - Chunk.SIZE + 1) / Chunk.SIZE;
            int cy = wy >= 0 ? wy / Chunk.SIZE : (wy - Chunk.SIZE + 1) / Chunk.SIZE;
            coord = new Vector2Int(cx, cy);
            lx = wx - cx * Chunk.SIZE;
            ly = wy - cy * Chunk.SIZE;
        }
    }
}