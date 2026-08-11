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

    [Header("지배")]
    [Tooltip("갇힌 하수인 종족을 지배할 때의 성공 확률(0~1). **실패하면 그 포로는 죽는다.** " +
             "붙잡힌 쪽의 성질이 아니라 **지배하는 핵의 힘**이라 여기 둔다 — 등급·서브코어로 " +
             "올리고 싶어지면 `Conversion.DominateChance`만 고치면 된다.")]
    [Range(0f, 1f)] public float DominateChance = 0.7f;

    [Header("환경 효과")]
    public float MoveSpeedModifier = 0f;
    public float HungerRateModifier = 0f;
    public float DamagePerSecond = 0f;

    [Header("바닥 프리팹 (없으면 임시 색상 사용)")]
    public GameObject FloorPrefab;
    public Color FloorColor = Color.white;
}