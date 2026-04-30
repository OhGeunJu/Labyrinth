using UnityEngine;

public enum TileType
{
    Impassable,
    Passable,
}

[System.Serializable]
public struct TileData
{
    public TileType Type;
    public float MovePenalty;
    public float Height;    // 지형 레이캐스트로 수집한 월드 Y
    public Vector3 Normal;  // 경사 판정용

    public bool IsPassable => Type == TileType.Passable;

    public static TileData MakePassable()   => new TileData { Type = TileType.Passable,   Normal = Vector3.up };
    public static TileData MakeImpassable() => new TileData { Type = TileType.Impassable, Normal = Vector3.up };
}
