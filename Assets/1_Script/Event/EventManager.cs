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
            case GameEventType.Reinforcement: SpawnReinforcement(data); break;
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

    // ── 지원군 ───────────────────────────────────────────────────────────────

    void SpawnReinforcement(GameEventData data)
    {
        if (data.ReinforcementPrefabs == null || data.ReinforcementPrefabs.Count == 0) return;

        Vector3 edge = GetMapEdgePosition();
        var core = DungeonCore.Instance;
        Vector3 rally = core != null
            ? core.transform.position
            : edge;

        for (int i = 0; i < data.ReinforcementCount; i++)
        {
            var prefab = data.ReinforcementPrefabs[Random.Range(0, data.ReinforcementPrefabs.Count)];
            if (prefab == null) continue;

            Vector3 pos = edge + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            pos.y = SampleHeight(pos);
            var go = Instantiate(prefab, pos, Quaternion.identity);

            // 던전 코어 방향으로 초기 이동 (랜덤 오프셋으로 뭉치지 않게)
            var mover = go.GetComponent<PawnMover>();
            if (mover != null)
            {
                Vector3 dest = rally + new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
                dest.y = SampleHeight(dest);
                mover.SetMoveTarget(dest);
            }
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
        var core = DungeonCore.Instance;
        Vector3 origin = core != null ? core.transform.position : Vector3.zero;
        float angle = Random.value * 360f * Mathf.Deg2Rad;
        float dist  = Random.Range(data.DiscoveryMinDist, data.DiscoveryMaxDist);
        Vector3 pos = origin + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        pos.y = SampleHeight(pos);

        // 바이옴 게이팅(step 7): 현재 바이옴 자원 풀에서 노드를 뽑는다.
        // 풀이 없으면(게이팅 미설정) 이벤트 기본 프리팹으로 폴백 → 기존 동작 유지.
        var nodeData = PickBiomeNode();
        if (nodeData != null)
        {
            var prefab = nodeData.Prefab != null ? nodeData.Prefab : data.ResourceNodePrefab;
            if (prefab == null) return;
            var go = Instantiate(prefab, pos, Quaternion.identity);
            var node = go.GetComponent<ResourceNode>();
            if (node != null) node.Init(nodeData);
            return;
        }

        if (data.ResourceNodePrefab == null) return;
        Instantiate(data.ResourceNodePrefab, pos, Quaternion.identity);
    }

    // 활성 정착지 바이옴의 자원 노드 풀에서 랜덤 1개. 게이팅 미설정이면 null.
    static ResourceNodeData PickBiomeNode()
    {
        var sm = SettlementManager.Instance;
        if (sm == null) return null;
        var pool = BiomeContent.ResourceNodes(sm.ActiveBiome);
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
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
