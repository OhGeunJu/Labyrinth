using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// 씬에 하나 배치. EventData 목록을 인스펙터에서 연결.
public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }
    public static event Action<GameEventData> OnEventTriggered;

    [SerializeField] List<GameEventData> eventPool = new();
    [Tooltip("이벤트 롤 주기 (초)")]
    [SerializeField] float checkInterval = 300f;
    [Tooltip("맵 가장자리 스폰 오프셋 (Terrain 절반 크기 - 여백)")]
    [SerializeField] float mapEdgeOffset = 10f;

    float _timer;
    float _elapsed;

    void Awake() => Instance = this;

    void Update()
    {
        _elapsed += Time.deltaTime;
        _timer   += Time.deltaTime;
        if (_timer >= checkInterval)
        {
            _timer = 0f;
            TryRollEvent();
        }
    }

    void TryRollEvent()
    {
        var candidates = new List<GameEventData>();
        float totalWeight = 0f;
        foreach (var e in eventPool)
        {
            if (e == null || _elapsed < e.MinGameTime) continue;
            candidates.Add(e);
            totalWeight += e.Weight;
        }
        if (candidates.Count == 0) return;

        float roll = Random.value * totalWeight;
        float acc  = 0f;
        foreach (var e in candidates)
        {
            acc += e.Weight;
            if (roll <= acc) { TriggerEvent(e); return; }
        }
        TriggerEvent(candidates[candidates.Count - 1]);
    }

    void TriggerEvent(GameEventData data)
    {
        OnEventTriggered?.Invoke(data);
        switch (data.EventType)
        {
            case GameEventType.Raid:          SpawnRaid(data);          break;
            case GameEventType.Merchant:      SpawnMerchant(data);      break;
            case GameEventType.Reinforcement: /* 뼈대 — 추후 구현 */   break;
            case GameEventType.Discovery:     SpawnDiscovery(data);     break;
        }
    }

    // ── 침공 ─────────────────────────────────────────────────────────────────

    void SpawnRaid(GameEventData data)
    {
        if (data.EnemyPrefabs == null || data.EnemyPrefabs.Count == 0) return;
        Vector3 edge = GetMapEdgePosition();
        for (int i = 0; i < data.EnemyCount; i++)
        {
            var prefab = data.EnemyPrefabs[Random.Range(0, data.EnemyPrefabs.Count)];
            if (prefab == null) continue;
            Vector3 pos = edge + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            pos.y = SampleHeight(pos);
            Instantiate(prefab, pos, Quaternion.identity);
        }
    }

    // ── 상인 ─────────────────────────────────────────────────────────────────

    void SpawnMerchant(GameEventData data)
    {
        if (data.MerchantPrefab == null) return;
        Vector3 edge = GetMapEdgePosition();
        edge.y = SampleHeight(edge);
        Instantiate(data.MerchantPrefab, edge, Quaternion.identity);
    }

    // ── 발견 ─────────────────────────────────────────────────────────────────

    void SpawnDiscovery(GameEventData data)
    {
        if (data.ResourceNodePrefab == null) return;
        var core = DungeonCore.Instance;
        Vector3 origin = core != null ? core.transform.position : Vector3.zero;
        float angle = Random.value * 360f * Mathf.Deg2Rad;
        float dist  = Random.Range(data.DiscoveryMinDist, data.DiscoveryMaxDist);
        Vector3 pos = origin + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        pos.y = SampleHeight(pos);
        Instantiate(data.ResourceNodePrefab, pos, Quaternion.identity);
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────────

    Vector3 GetMapEdgePosition()
    {
        Terrain terrain = Terrain.activeTerrain;
        float halfW, halfH;
        Vector3 origin = Vector3.zero;
        if (terrain != null)
        {
            var size = terrain.terrainData.size;
            halfW  = size.x * 0.5f - mapEdgeOffset;
            halfH  = size.z * 0.5f - mapEdgeOffset;
            origin = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        }
        else
        {
            halfW = halfH = 100f;
        }

        int side = Random.Range(0, 4);
        Vector3 pos = side switch
        {
            0 => origin + new Vector3(Random.Range(-halfW, halfW),  0f,  halfH),
            1 => origin + new Vector3(Random.Range(-halfW, halfW),  0f, -halfH),
            2 => origin + new Vector3( halfW, 0f, Random.Range(-halfH, halfH)),
            _ => origin + new Vector3(-halfW, 0f, Random.Range(-halfH, halfH)),
        };
        pos.y = SampleHeight(pos);
        return pos;
    }

    static float SampleHeight(Vector3 pos)
    {
        Terrain t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(pos) : 0f;
    }

    // 디버그용 — 인스펙터 버튼 대용
    [ContextMenu("Force Roll Event")]
    void ForceRoll() => TryRollEvent();
}
