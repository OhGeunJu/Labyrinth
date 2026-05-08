using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/NpcData")]
public class NpcData : UnitData
{
    public NpcType NpcType = NpcType.Villager;

    [Tooltip("배회 반경 (Villager, Guard)")]
    public float WanderRadius = 10f;

    [Tooltip("도주 감지 범위 (Villager)")]
    public float FleeDetectRange = 8f;

    [Tooltip("EventMerchant 머무는 시간(초)")]
    public float StayDuration = 120f;

    [Tooltip("EventMerchant 목적지 — 비우면 던전 코어 위치 사용")]
    public Vector3 ArrivalOffset = Vector3.zero;
}
