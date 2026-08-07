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
    [Tooltip("지형 가장자리에서 안쪽으로 남길 여백 (m)")]
    [SerializeField] float mapEdgeOffset = 10f;
    [Tooltip("핵에서 바깥으로 훑어 볼 최대 거리 (타일)")]
    [SerializeField] float maxSpawnRadius = 150f;
    [Tooltip("핵에서 이만큼은 떨어뜨린다 — 못 채우면 찾은 칸 중 가장 먼 곳")]
    [SerializeField] float minSpawnDistance = 25f;
    [Tooltip("가장자리를 찾을 때 굴려 볼 방향 수")]
    [SerializeField] int spawnDirections = 16;
    [Tooltip("찾은 가장자리에서 안쪽으로 물러설 칸 수 — 낭떠러지 바로 위에 세우지 않는다")]
    [SerializeField] int spawnEdgeInset = 3;
    [Tooltip("발견한 물건을 떨굴 거리(핵 기준, m)")]
    [SerializeField] float discoveryDropRadius = 8f;

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
        // **설 자리를 먼저 찾고 알린다.** 알림이 앞서면 "고블린이 몰려온다"고 해 놓고
        // 아무도 오지 않는(공허에 떨어져 길을 못 찾는) 일이 생긴다.
        Vector3 edge = Vector3.zero;
        bool needsSpawn = data.EventType is GameEventType.Raid
                                         or GameEventType.Merchant
                                         or GameEventType.Reinforcement;
        if (needsSpawn && !HasSpawnContent(data))
        {
            Debug.LogWarning($"[이벤트] '{data.EventName}' — 부를 프리팹이 비어 있어 건너뜁니다.");
            return;
        }

        if (needsSpawn && !TryFindEdgeSpawn(out edge))
        {
            Debug.LogWarning($"[이벤트] '{data.EventName}' — 유닛이 설 수 있는 맵 가장자리를 " +
                             "못 찾아 건너뜁니다(바닥이 너무 좁거나 전부 막혔습니다).");
            return;
        }

        OnEventTriggered?.Invoke(data);
        switch (data.EventType)
        {
            case GameEventType.Raid:          SpawnRaid(data, edge);          break;
            case GameEventType.Merchant:      SpawnMerchant(data, edge);      break;
            case GameEventType.Reinforcement: SpawnReinforcement(data, edge); break;
            case GameEventType.Discovery:     SpawnDiscovery(data);           break;
        }

        // **어디에 나타났는지 알린다.** 방향이 없으면 "고블린이 몰려온다"를 읽고도 화면을
        // 훑어 찾아야 한다 — 습격은 어느 쪽을 막을지가 곧 대응이다.
        // `Notify`는 같은 창을 다시 쓰므로 위에서 뜬 설명 위에 방향까지 얹힌다
        // (`SpawnDiscovery`가 얻은 물건을 알리는 것과 같은 길).
        if (needsSpawn) NotifyDirection(data, edge);
    }

    void NotifyDirection(GameEventData data, Vector3 edge)
    {
        var core = DungeonCore.Instance;
        if (core == null) return;

        string dir = DirectionFrom(core.transform.position, edge);
        if (dir == null) return;

        // 상인은 **무엇을 파는지**가 갈 이유를 만든다 — 무기 상인이면 가고 재료 상인이면
        // 만다. 종류 이름은 전부 "…상인"으로 끝나므로 조사는 "이"로 고정이다.
        string line = data.EventType == GameEventType.Merchant && _lastMerchant != null
            ? $"{_lastMerchant.MerchantType.DisplayName()}이 {dir}쪽에 도착했습니다."
            : $"{dir}쪽에 나타났습니다.";

        string desc = string.IsNullOrWhiteSpace(data.Description)
            ? line
            : $"{data.Description}\n{line}";
        // 원정 중이라 필드 UI가 꺼져 있어도 들고 기다린다 — 돌아와서야 알게 되더라도
        // 어느 쪽에서 들어왔는지는 남아야 한다.
        EventNotificationUI.Notify(data.EventName, desc, holdWhenHidden: true);
    }

    static readonly string[] Compass = { "북", "북동", "동", "남동", "남", "남서", "서", "북서" };

    // 핵에서 본 8방위. +Z가 북, 시계 방향.
    static string DirectionFrom(Vector3 origin, Vector3 point)
    {
        Vector3 d = point - origin;
        d.y = 0f;
        if (d.sqrMagnitude < 1f) return null;   // 핵 위 — 방향이라 할 것이 없다

        float ang = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;
        return Compass[Mathf.RoundToInt(ang / 45f) % 8];
    }

    // ── 침공 ─────────────────────────────────────────────────────────────────

    void SpawnRaid(GameEventData data, Vector3 edge)
    {
        if (data.EnemyPrefabs == null || data.EnemyPrefabs.Count == 0) return;

        var core = DungeonCore.Instance;
        var used = new HashSet<Vector2Int>();   // 한 칸에 둘을 낳지 않는다

        for (int i = 0; i < data.EnemyCount; i++)
        {
            var prefab = data.EnemyPrefabs[Random.Range(0, data.EnemyPrefabs.Count)];
            if (prefab == null) continue;

            var go = Instantiate(prefab, ScatterNear(edge, 3f, used), Quaternion.identity);

            // **핵을 향해 걷게 한다.** 적 트리는 탐지(수 미터)와 배회(12m)뿐이라,
            // 명령이 없으면 침공대가 맵 가장자리에서 서성이다 끝난다 — 습격이 일어나지 않는 것과 같다.
            // `WanderNode`는 이동 중인 유닛의 목적지를 덮지 않으므로 도착까지 이 명령이 산다.
            var mover = go.GetComponent<PawnMover>();
            if (mover != null && core != null) mover.SetMoveTarget(ScatterNear(core.transform.position, 5f));
        }
    }

    // ── 지원군 ───────────────────────────────────────────────────────────────

    void SpawnReinforcement(GameEventData data, Vector3 edge)
    {
        if (data.ReinforcementPrefabs == null || data.ReinforcementPrefabs.Count == 0) return;

        var core = DungeonCore.Instance;
        Vector3 rally = core != null ? core.transform.position : edge;
        var used = new HashSet<Vector2Int>();

        for (int i = 0; i < data.ReinforcementCount; i++)
        {
            var prefab = data.ReinforcementPrefabs[Random.Range(0, data.ReinforcementPrefabs.Count)];
            if (prefab == null) continue;

            var go = Instantiate(prefab, ScatterNear(edge, 3f, used), Quaternion.identity);

            // 던전 코어 방향으로 초기 이동 (랜덤 오프셋으로 뭉치지 않게)
            var mover = go.GetComponent<PawnMover>();
            if (mover != null) mover.SetMoveTarget(ScatterNear(rally, 5f));
        }
    }

    // ── 상인 ─────────────────────────────────────────────────────────────────

    // 이번에 온 상인이 무엇을 파는가. 알림에 종류를 적으려고 밖에서도 읽는다.
    MerchantData _lastMerchant;

    // 사람과 등짐을 따로 뽑아 여기서 합친다 — 종족 풀 × 상품 풀.
    void SpawnMerchant(GameEventData data, Vector3 edge)
    {
        _lastMerchant = null;

        var prefab = PickOne(data.MerchantPrefabs);
        if (prefab == null) return;

        var go       = Instantiate(prefab, edge, Quaternion.identity);
        var merchant = go.GetComponent<Merchant>();

        // **저작한 상인은 건드리지 않는다.** 프리팹에 손으로 상품을 물려 뒀다면 그건
        // "이 사람은 늘 이것만 판다"는 뜻이다 — 이벤트가 덮으면 고정 상인을 세울 길이 없어진다.
        if (merchant != null && merchant.HasAuthoredData)
        {
            _lastMerchant = merchant.Data;
            return;
        }

        // **상품은 그 종족이 취급하는 것 중에서 뽑는다.** 그냥 곱하면 인간이 고블린제 물건을
        // 들고 오는 조합이 나온다(`MerchantData.Races`). 뽑을 것이 없으면 — 이 종족을 받아
        // 주는 상품이 목록에 없으면 — 프리팹이 들고 온 것(역할 기본값)을 그대로 판다.
        var race    = go.GetComponent<UnitBrain>()?.Data;
        var variant = PickVariantFor(data, race);
        if (variant == null) { _lastMerchant = merchant?.Data; return; }

        merchant ??= go.AddComponent<Merchant>();
        merchant.Data = variant;      // setter가 그 상인만의 재고를 새로 뽑는다
        _lastMerchant = variant;
    }

    static MerchantData PickVariantFor(GameEventData data, UnitData race)
    {
        var pool = data.MerchantVariants;
        if (pool == null || pool.Count == 0) return null;

        var eligible = new List<MerchantData>();
        foreach (var m in pool)
            if (m != null && m.SoldBy(race)) eligible.Add(m);

        return PickOne(eligible);
    }

    // 목록에서 하나. 균등 추첨이고, 비운 칸(null)이 섞여 있어도 건너뛴다.
    // 누가 더 자주 오는가는 아직 저작할 축이 아니다 — 필요해지면 가중치를 붙이면 된다.
    static T PickOne<T>(List<T> pool) where T : UnityEngine.Object
    {
        if (pool == null || pool.Count == 0) return null;
        for (int i = 0; i < pool.Count; i++)
        {
            var pick = pool[Random.Range(0, pool.Count)];
            if (pick != null) return pick;
        }
        return null;
    }

    // 부를 프리팹이 아예 없는 이벤트는 발동해 봐야 알림만 뜨고 아무도 안 온다.
    static bool HasSpawnContent(GameEventData data) => data.EventType switch
    {
        GameEventType.Raid          => PickOne(data.EnemyPrefabs)         != null,
        GameEventType.Merchant      => PickOne(data.MerchantPrefabs)      != null,
        GameEventType.Reinforcement => PickOne(data.ReinforcementPrefabs) != null,
        _                           => true,
    };

    // ── 발견 ─────────────────────────────────────────────────────────────────

    // **물건을 줍는다.** 예전엔 거점 근처에 광맥을 세웠는데, 광맥은 자리를 차지하고 영구히
    // 남는 것이라 이벤트가 몇 번 돌면 거점 둘레가 광맥 밭이 되고 건설할 땅이 사라졌다.
    // 발견은 한 번의 사건이지 지형 변화가 아니다.
    //
    // **바닥에 떨군다 — 창고로 순간이동시키지 않는다.** 창고에 바로 넣으면 사건이 아니라
    // 숫자가 조용히 오르는 일이 되고, 알림을 놓치면 무슨 일이 있었는지도 모른다.
    // 떨어뜨려 두면 `DroppedItem`이 운반 일감을 걸어 폰이 가지러 가고, 받을 창고가 없으면
    // 그 자리에 남아 5초마다 다시 시도한다(창고를 비우면 저절로 실려 온다).
    void SpawnDiscovery(GameEventData data)
    {
        var item = PickDiscoveryItem(data);
        if (item == null)
        {
            Debug.LogWarning("[이벤트] 발견할 아이템이 없습니다 — DiscoveryItems를 채우거나 " +
                             "아이템 레지스트리를 확인하세요.");
            return;
        }

        int lo = Mathf.Min(data.DiscoveryMinAmount, data.DiscoveryMaxAmount);
        int hi = Mathf.Max(data.DiscoveryMinAmount, data.DiscoveryMaxAmount);
        int amount = Random.Range(lo, hi + 1);

        var core   = DungeonCore.Instance;
        var origin = core != null ? core.transform.position : Vector3.zero;

        // 거점 둘레에 떨군다 — 가지러 갈 만하되 발밑은 아닌 거리.
        var spot = ScatterNear(origin, discoveryDropRadius);
        DroppedItem.Spawn(item, amount, spot);

        string dir  = DirectionFrom(origin, spot);
        string line = dir != null ? $" {dir}쪽에 떨어져 있습니다." : "";
        EventNotificationUI.Notify("발견", $"{item.ItemName} {amount}개를 찾았습니다.{line}",
                                   holdWhenHidden: true);
    }

    // 후보가 비면 **레지스트리 전체**에서 고른다 — 모드가 넣은 아이템도 딸려 온다.
    // 화폐는 뺀다: 골드가 하늘에서 떨어지면 경제가 무너진다.
    static ItemData PickDiscoveryItem(GameEventData data)
    {
        if (data.DiscoveryItems != null && data.DiscoveryItems.Count > 0)
        {
            var picked = data.DiscoveryItems[Random.Range(0, data.DiscoveryItems.Count)];
            if (picked != null) return picked;
        }

        var pool = new List<ItemData>();
        foreach (var kv in DataManager.AllItems)
        {
            var it = kv.Value;
            if (it == null || it.BaseValue <= 0) continue;
            if (it is ResourceItemData r && r.Group == ResourceGroup.Currency) continue;
            pool.Add(it);
        }
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }

    // ── 스폰 자리 ────────────────────────────────────────────────────────────

    // **가장자리는 지형이 아니라 맵이 정한다.** 예전엔 `Terrain` 크기로만 가장자리를 잡고
    // 높이도 `Terrain.SampleHeight`만 봐서 `MapManager`의 바닥(`_floored`)·점유를 전혀 몰랐다 —
    // 던전형 맵에서는 공허 한복판에 유닛이 떨어져 길찾기가 아예 안 나왔다(2026-08-06 실측).
    //
    // 그래서 **핵에서 바깥으로 걸어 나가며** 설 수 있는 마지막 칸을 가장자리로 삼는다.
    // 설 수 없는 칸을 만나면 거기서 멈추므로 고른 칸은 **핵과 이어져 있는 것이 보장된다** —
    // 좌표를 굴린 뒤 유효성만 검사하는 방식엔 이 보장이 없다(바닥 밖 섬도 통과한다).
    bool TryFindEdgeSpawn(out Vector3 spawn)
    {
        spawn = Vector3.zero;

        var core = DungeonCore.Instance;
        var map  = core != null ? MapManager.GetForPosition(core.transform.position)
                                : MapManager.Instance;
        if (map == null)
        {
            spawn = TerrainEdgeFallback();   // 맵이 없으면 지형밖에 볼 것이 없다
            return true;
        }

        // 기준점은 핵. 핵이 없으면 빌드존 한복판에서 나간다.
        Vector3 origin = core != null
            ? core.transform.position
            : map.TileToGround(map.ZoneOriginTile + new Vector2Int(map.Width / 2, map.Height / 2));

        // 방향을 고르게 나눠 굴린다 — 한쪽이 막혀도 반대쪽엔 길이 있다.
        int tries = Mathf.Max(1, spawnDirections);
        float baseAngle = Random.value * 360f;

        Vector2Int fallback = default;
        bool hasFallback = false;

        for (int i = 0; i < tries; i++)
        {
            float rad = (baseAngle + 360f * i / tries) * Mathf.Deg2Rad;
            var dir   = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            if (!WalkToEdge(map, origin, dir, out var tile, out float dist)) continue;

            if (dist >= minSpawnDistance) { spawn = map.TileToGround(tile); return true; }
            if (!hasFallback) { fallback = tile; hasFallback = true; }
        }

        // 최소 거리를 채우는 방향이 하나도 없으면(좁은 던전) 찾아 둔 칸이라도 쓴다 —
        // 여기서 포기하면 작은 맵에서는 습격이 영영 일어나지 않는다.
        if (hasFallback) { spawn = map.TileToGround(fallback); return true; }
        return false;
    }

    // origin에서 dir 방향으로 한 칸씩 나아가며 **설 수 있는 마지막 칸**을 찾는다.
    // 설 수 없는 칸(공허)이나 지형 밖을 만나면 멈춘다. 건물에 점유된 칸은 지나치기만 한다 —
    // 벽 하나가 그 방향을 통째로 지우면 안 되고, 우회는 길찾기 몫이다.
    bool WalkToEdge(MapManager map, Vector3 origin, Vector3 dir, out Vector2Int found, out float dist)
    {
        found = default;
        dist  = 0f;

        // **가장자리 위가 아니라 그 안쪽에 세운다.** 마지막 칸은 낭떠러지 바로 위다 —
        // 거기에 넷을 흩어 놓으면 서로 비켜서다 한둘이 공허로 밀려난다.
        // (`ExpeditionFormation.approachInset`이 집결 원에서 배운 것과 같다:
        //  판정선 위에 세우면 경계에서 아슬아슬해진다.)
        // 그래서 걸어 나가며 **최근 몇 칸을 기억**했다가 그중 가장 안쪽 것을 쓴다.
        int inset = Mathf.Max(0, spawnEdgeInset);
        var recent = new List<(Vector2Int tile, float dist)>();
        var last   = map.WorldToTile(origin);

        for (float d = 1f; d <= maxSpawnRadius; d += 1f)
        {
            Vector3 point = origin + dir * d;
            if (!InsideTerrain(point)) break;               // 지형 밖 — 여기가 가장자리

            var tile = map.WorldToTile(point);
            if (tile == last) continue;                     // 같은 칸을 두 번 보지 않는다
            last = tile;

            if (!map.IsStandable(tile)) break;              // 공허 — 여기가 가장자리
            if (!map.GetTile(tile).IsPassable) continue;    // 건물 — 지나친다

            recent.Add((tile, d));
            if (recent.Count > inset + 1) recent.RemoveAt(0);
        }

        if (recent.Count == 0) return false;

        // 물러설 만큼 못 걸었으면(좁은 바닥) 있는 것 중 가장 안쪽.
        var pick = recent[0];
        found = pick.tile;
        dist  = pick.dist;
        return true;
    }

    const int ScatterTries = 12;

    // 한 점 둘레에 흩되 **설 수 있는 칸으로 스냅**하고, 두 가지를 더 피한다:
    //  ① 이미 이번에 쓴 칸 — 같은 칸에 둘을 낳으면 겹친 채 태어나 서로 밀어내며 갈라지고,
    //     그게 바닥 끝이면 밀려난 쪽이 떨어진다.
    //  ② 이웃이 공허인 칸 — 설 수는 있어도 낭떠러지 끝이라 비켜서는 한 걸음이 곧 추락이다.
    // 예전엔 ±3m를 좌표에 그냥 더해서, 자리를 제대로 골라도 무리의 절반이 공허로 새어 나갔다.
    static Vector3 ScatterNear(Vector3 center, float radius, HashSet<Vector2Int> used = null)
    {
        var map = MapManager.GetForPosition(center);
        if (map == null)
        {
            var p = center + new Vector3(Random.Range(-radius, radius), 0f, Random.Range(-radius, radius));
            p.y = SampleHeight(p);
            return p;
        }

        Vector2Int fallback = map.WorldToTile(center);
        bool hasFallback = false;

        for (int i = 0; i < ScatterTries; i++)
        {
            var offset = new Vector3(Random.Range(-radius, radius), 0f, Random.Range(-radius, radius));
            var tile   = map.FindWorkableNear(map.WorldToTile(center + offset), Mathf.CeilToInt(radius));
            if (!map.IsWorkable(tile)) continue;

            if (!hasFallback) { fallback = tile; hasFallback = true; }

            if (used != null && used.Contains(tile)) continue;
            if (!SurroundedByFloor(map, tile)) continue;

            used?.Add(tile);
            return map.TileToGround(tile);
        }

        // 까다롭게 굴 자리가 없으면 설 수 있기만 하면 된다 — 아예 못 나오는 것보다 낫다.
        used?.Add(fallback);
        return map.TileToGround(fallback);
    }

    // 이웃 네 칸이 다 설 수 있는가 — 낭떠러지 끝 칸을 걸러낸다.
    static bool SurroundedByFloor(MapManager map, Vector2Int t) =>
        map.IsStandable(t + Vector2Int.up)   && map.IsStandable(t + Vector2Int.down) &&
        map.IsStandable(t + Vector2Int.left) && map.IsStandable(t + Vector2Int.right);

    // 지형이 있으면 그 안쪽(여백 `mapEdgeOffset`)인가. 지형이 없는 씬은 항상 참 —
    // 그때는 바닥(`IsStandable`)과 `maxSpawnRadius`가 경계를 대신한다.
    bool InsideTerrain(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        if (t == null) return true;

        var o = t.transform.position;
        var s = t.terrainData.size;
        return p.x >= o.x + mapEdgeOffset && p.x <= o.x + s.x - mapEdgeOffset
            && p.z >= o.z + mapEdgeOffset && p.z <= o.z + s.z - mapEdgeOffset;
    }

    // MapManager가 아예 없을 때만 쓰는 옛 경로.
    Vector3 TerrainEdgeFallback()
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
