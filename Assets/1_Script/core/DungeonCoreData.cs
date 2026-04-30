using UnityEngine;

public enum CoreType
{
    Cave,
    Meadow,
    Desert,
    Swamp,
    Ice,
    Lava,
    Jungle,
    Cursed,
}

[CreateAssetMenu(fileName = "DungeonCoreData", menuName = "DungeonColony/DungeonCoreData")]
public class DungeonCoreData : ScriptableObject
{
    [Header("기본 정보")]
    public string CoreName;
    public CoreType Type;

    [Header("반경")]
    public float BaseRadius = 10f;
    public float RadiusPerGrade = 2f;

    [Header("등급")]
    public int MaxGrade = 5;

    [Header("하수인 기본 캡")]
    public int BaseMinionCap = 5;

    [Header("환경 효과")]
    public float MoveSpeedModifier = 0f;
    public float HungerRateModifier = 0f;
    public float DamagePerSecond = 0f;

    [Header("바닥 프리팹 (없으면 임시 색상 사용)")]
    public GameObject FloorPrefab;
    public Color FloorColor = Color.white;
}