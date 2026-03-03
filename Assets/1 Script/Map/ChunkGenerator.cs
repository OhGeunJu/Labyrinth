using UnityEngine;

namespace DungeonColony.Map
{
    /// <summary>
    /// 청크 내용 생성 (절차적 암석 배치)
    /// </summary>
    public static class ChunkGenerator
    {
        /// <summary>
        /// 기본 생성: 전체 암석 + 암반 테두리
        /// seed는 청크 좌표 기반으로 자동 결정
        /// </summary>
        public static Chunk Generate(Vector2Int chunkCoord, int globalSeed = 0)
        {
            var chunk = new Chunk(chunkCoord);
            chunk.FillRock();
            chunk.ApplyBedrockBorder();

            // 청크별 고유 시드로 변형 적용
            int seed = globalSeed ^ (chunkCoord.x * 73856093) ^ (chunkCoord.y * 19349663);
            ApplyNoiseVariation(chunk, seed);

            return chunk;
        }

        /// <summary>시작 청크: 중앙에 초기 개방 공간 생성</summary>
        public static Chunk GenerateStartChunk(Vector2Int chunkCoord, int globalSeed = 0)
        {
            var chunk = new Chunk(chunkCoord);
            chunk.FillRock();
            chunk.ApplyBedrockBorder();

            // 중앙 5×5 개방 (초기 거점)
            int center = Chunk.SIZE / 2;  // 12
            for (int x = center - 2; x <= center + 2; x++)
                for (int y = center - 2; y <= center + 2; y++)
                    chunk.SetTile(x, y, TileData.Floor());

            // 중앙 타일에 미궁핵 슬롯 표시
            chunk.SetTile(center, center, new TileData
            {
                Type = TileType.CoreSlot,
                Flags = TileFlags.Passable | TileFlags.Buildable,
                Hardness = 0,
                Fertility = 0f
            });

            ApplyNoiseVariation(chunk, globalSeed, skipOpenArea: true);
            return chunk;
        }

        // ── 내부 ────────────────────────────────────────
        static void ApplyNoiseVariation(Chunk chunk, int seed,
                                         bool skipOpenArea = false)
        {
            var rng = new System.Random(seed);
            int center = Chunk.SIZE / 2;

            for (int x = 1; x < Chunk.SIZE - 1; x++)
                for (int y = 1; y < Chunk.SIZE - 1; y++)
                {
                    // 개방 구역 스킵
                    if (skipOpenArea &&
                        Mathf.Abs(x - center) <= 3 &&
                        Mathf.Abs(y - center) <= 3) continue;

                    var tile = chunk.GetTile(x, y);
                    if (tile.Type != TileType.Rock) continue;

                    // 10% 확률로 단단한 암석
                    if (rng.NextDouble() < 0.10)
                        chunk.SetTile(x, y, TileData.Rock(hardness: 300));
                }
        }
    }
}