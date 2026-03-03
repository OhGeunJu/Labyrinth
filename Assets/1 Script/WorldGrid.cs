using UnityEngine;

public class WorldGrid
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float CellSize { get; private set; }
    public Vector3 Origin { get; private set; }

    public WorldGrid(int width, int height, float cellSize, Vector3 origin)
    {
        Width = width;
        Height = height;
        CellSize = cellSize;
        Origin = origin;
    }

    public Vector3 TileToWorld(int x, int y)
    {
        return Origin + new Vector3(x * CellSize, 0f, y * CellSize);
    }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }
}