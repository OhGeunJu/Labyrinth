using UnityEngine;

namespace DungeonColony.Map
{
    public enum TileType
    {
        Bedrock,      // 암반 (파괴 불가, 맵 경계)
        Rock,         // 암석 (채굴 가능)
        Floor,        // 바닥 (개방)
        Wall,         // 건설된 벽
        Door,         // 문
        CoreSlot,     // 미궁핵 설치 가능 위치
    }

    [System.Flags]
    public enum TileFlags
    {
        None = 0,
        Passable = 1 << 0,  // 이동 가능
        Mineable = 1 << 1,  // 채굴 가능
        Buildable = 1 << 2,  // 건설 가능
        Designated = 1 << 3,  // 작업 지정됨
        HasItem = 1 << 4,  // 아이템 존재
    }

    [System.Serializable]
    public struct TileData
    {
        public TileType Type;
        public TileFlags Flags;
        public int Hardness;    // 채굴 난이도
        public float Fertility;   // 재배 가능도 (0 = 불가)

        public bool IsPassable => Flags.HasFlag(TileFlags.Passable);
        public bool IsMineable => Flags.HasFlag(TileFlags.Mineable);
        public bool IsBuildable => Flags.HasFlag(TileFlags.Buildable);
        public bool IsDesignated => Flags.HasFlag(TileFlags.Designated);
        public bool HasItem => Flags.HasFlag(TileFlags.HasItem);

        public static TileData Rock(int hardness = 100) => new TileData
        {
            Type = TileType.Rock,
            Flags = TileFlags.Mineable,
            Hardness = hardness,
            Fertility = 0f
        };

        public static TileData Floor(float fertility = 0f) => new TileData
        {
            Type = TileType.Floor,
            Flags = TileFlags.Passable | TileFlags.Buildable,
            Hardness = 0,
            Fertility = fertility
        };

        public static TileData Bedrock() => new TileData
        {
            Type = TileType.Bedrock,
            Flags = TileFlags.None,
            Hardness = int.MaxValue,
            Fertility = 0f
        };
    }

    /// <summary>
    /// 25×25 타일 청크
    /// ChunkCoord (0,0) → 월드 타일 (0~24, 0~24)
    /// ChunkCoord (1,0) → 월드 타일 (25~49, 0~24)
    /// </summary>
    public class Chunk
    {
        public const int SIZE = 25;

        public Vector2Int ChunkCoord { get; }
        public TileData[,] Tiles { get; } = new TileData[SIZE, SIZE];
        public Vector2Int WorldOffset => ChunkCoord * SIZE;

        public Chunk(Vector2Int coord) { ChunkCoord = coord; }

        // ── 로컬 좌표 접근 ──────────────────────────────
        public TileData GetTile(int lx, int ly) =>
            InBounds(lx, ly) ? Tiles[lx, ly] : default;

        public void SetTile(int lx, int ly, TileData tile)
        {
            if (InBounds(lx, ly)) Tiles[lx, ly] = tile;
        }

        public bool InBounds(int lx, int ly) =>
            (uint)lx < SIZE && (uint)ly < SIZE;

        // ── 초기화 헬퍼 ────────────────────────────────
        /// <summary>청크 전체를 암석으로 채움</summary>
        public void FillRock(int hardness = 100)
        {
            for (int x = 0; x < SIZE; x++)
                for (int y = 0; y < SIZE; y++)
                    Tiles[x, y] = TileData.Rock(hardness);
        }

        /// <summary>테두리 1칸을 암반(파괴 불가)으로 설정</summary>
        public void ApplyBedrockBorder()
        {
            for (int i = 0; i < SIZE; i++)
            {
                Tiles[i, 0] = TileData.Bedrock();
                Tiles[i, SIZE - 1] = TileData.Bedrock();
                Tiles[0, i] = TileData.Bedrock();
                Tiles[SIZE - 1, i] = TileData.Bedrock();
            }
        }
    }
}