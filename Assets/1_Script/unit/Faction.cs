using UnityEngine;

public enum FactionType
{
    Player,
    Enemy,
}

public class Faction : MonoBehaviour
{
    [SerializeField] FactionType factionType;
    public FactionType Type => factionType;

    public bool IsHostileTo(Faction other)
    {
        if (other == null) return false;
        return factionType != other.Type;
    }
}