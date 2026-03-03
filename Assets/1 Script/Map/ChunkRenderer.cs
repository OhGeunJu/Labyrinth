using UnityEngine;

namespace DungeonColony.Map
{
    public class ChunkRenderer : MonoBehaviour
    {
        float _tileSize;
        Chunk _chunk;

        public void SetPrefabs(GameObject rock, GameObject floor,
                                GameObject bedrock, GameObject coreSlot, float tileSize)
        {
            _tileSize = tileSize;
        }

        public void Init(Chunk chunk)
        {
            _chunk = chunk;
            Render();
        }

        void Render()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            for (int x = 0; x < Chunk.SIZE; x++)
                for (int y = 0; y < Chunk.SIZE; y++)
                {
                    var tile = _chunk.GetTile(x, y);
                    var wo = _chunk.WorldOffset;
                    var pos = new Vector3((wo.x + x) * _tileSize, 0f, (wo.y + y) * _tileSize);

                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(transform);
                    go.transform.position = pos;
                    go.transform.localScale = new Vector3(_tileSize * 0.95f, 0.2f, _tileSize * 0.95f);
                    go.name = $"{tile.Type}_{x}_{y}";

                    // 타입별 색상 구분
                    var mat = go.GetComponent<Renderer>().material;
                    mat.color = tile.Type switch
                    {
                        TileType.Bedrock => new Color(0.2f, 0.2f, 0.2f),
                        TileType.Rock => new Color(0.5f, 0.4f, 0.3f),
                        TileType.Floor => new Color(0.8f, 0.7f, 0.5f),
                        TileType.CoreSlot => new Color(0.3f, 0.8f, 0.9f),
                        _ => Color.white,
                    };
                }
        }

        public void RefreshTile(int lx, int ly) => Render();
    }
}