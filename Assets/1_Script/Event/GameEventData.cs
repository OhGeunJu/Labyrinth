using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/GameEventData")]
public class GameEventData : ScriptableObject
{
    [Header("공통")]
    public string EventName;
    [TextArea] public string Description;
    public Sprite Icon;
    [Tooltip("랜덤 선택 가중치. 높을수록 자주 발생")]
    public float Weight = 1f;
    [Tooltip("게임 시작 후 이 이벤트가 발동될 수 있는 최소 경과 시간(초)")]
    public float MinGameTime = 0f;
    public GameEventType EventType;

    [Header("침공 (Raid)")]
    public List<GameObject> EnemyPrefabs = new();
    [Tooltip("스폰할 적 수")]
    public int EnemyCount = 5;

    [Header("상인 (Merchant)")]
    public GameObject MerchantPrefab;

    [Header("발견 (Discovery)")]
    public GameObject ResourceNodePrefab;
    [Tooltip("던전 코어 기준 스폰 최소/최대 거리")]
    public float DiscoveryMinDist = 20f;
    public float DiscoveryMaxDist = 60f;
}
