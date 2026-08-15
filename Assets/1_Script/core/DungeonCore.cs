using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DungeonCore : MonoBehaviour
{
    public static DungeonCore Instance { get; private set; }

    [SerializeField] DungeonCoreData data;
    [SerializeField] Color rangeColor = new Color(0.4f, 0.8f, 1f, 0.6f);
    [SerializeField] int rangeSegments = 64;

    public DungeonCoreData Data => data;
    public bool IsSelected { get; private set; }

    public static event Action<DungeonCore> OnSelected;

    // 영역(핵 union) 변경 시 발동 — 핵 이동/서브코어 등록·해제. 건물 휴면 재평가 트리거.
    public static event Action OnTerritoryChanged;
    public static void RaiseTerritoryChanged() => OnTerritoryChanged?.Invoke();

    // ── 하수인 캡 ────────────────────────────────────────────────────────────
    readonly List<SubCore> _subCores = new();

    // **셀 때마다 다시 센다.** 예전엔 `OnUnitSpawned`/`OnUnitDied`로 더하고 뺐는데, 그러면
    // **그 두 문을 안 지나는 변화**를 놓친다:
    //  · 회유해서 우리 편이 된 하수인은 스폰된 적이 없다 — 영영 안 세어진다.
    //  · 원정 나간 하수인은 꺼지기만 하고 죽지는 않는다 — 카운트에 남는다.
    //
    // 세력 레지스트리(`Faction.All`)가 이미 "지금 살아 있고 켜져 있는 것"을 들고 있으므로
    // 거기서 세면 두 구멍이 함께 사라진다. 폰 수는 적고 읽는 곳은 UI·소환뿐이라 값도 싸다.
    public int CurrentMinionCount
    {
        get
        {
            int n = 0;
            var all = Faction.All;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || !f.IsPlayer) continue;
                if (f.GetComponent<UnitBrain>()?.Data is MinionData) n++;
            }
            return n;
        }
    }
    public int TotalMinionCap => (data != null ? data.BaseMinionCap : 5)
                                 + GetSubCoreMinionBonus();

    int GetSubCoreMinionBonus()
    {
        int bonus = 0;
        foreach (var sc in _subCores) if (sc != null) bonus += sc.MinionCapBonus;
        return bonus;
    }

    public void RegisterSubCore(SubCore sc)   { if (!_subCores.Contains(sc)) { _subCores.Add(sc); RaiseTerritoryChanged(); } }
    public void UnregisterSubCore(SubCore sc) { if (_subCores.Remove(sc)) RaiseTerritoryChanged(); }

    public IReadOnlyList<SubCore> SubCores => _subCores;

    [SerializeField] Color selectedColor = new Color(1f, 0.9f, 0.2f, 0.8f);

    Material _mat;

    void Awake()
    {
        Instance = this;
        _mat = new Material(Shader.Find("Hidden/Internal-Colored"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_Cull",    (int)CullMode.Off);
        _mat.SetInt("_ZWrite", 0);
    }

    void OnEnable()  => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

    // ── 갇힌 하수인 종족을 지배한다 ──────────────────────────────────────────
    //
    // **폰이 갈 필요가 없다** — 핵이 하는 일이다. 영역 안 우리에 가둬 두면 시간이 지나며
    // 회유 게이지가 찬다(`Conversion.TickCore`). 사람 종족은 이 길로 안 온다(말로 설득한다).
    //
    // 갇힌 폰만 훑으므로 대개 0~2명이다 — 세력 레지스트리를 도는 값이 아깝지 않다.
    // 1초에 한 번이면 충분하다(게이지는 분 단위로 찬다).
    const float DominateInterval = 1f;
    float _nextDominate;

    void Update()
    {
        if (Time.time < _nextDominate) return;
        float dt = Time.time - _nextDominate + DominateInterval;
        _nextDominate = Time.time + DominateInterval;

        var all = Faction.All;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var f = all[i];
            if (f == null || f.IsPlayer) continue;                 // 이미 우리 편이면 볼 것 없다

            var brain = f.GetComponent<UnitBrain>();
            if (brain?.Blackboard == null || !brain.Blackboard.IsPrisoner) continue;

            Conversion.TickCore(brain, dt);
        }
    }

    void OnDestroy()
    {
        Destroy(_mat);
        Release();   // 막아 두었던 칸을 돌려준다 — 안 풀면 핵이 사라진 자리가 영영 막힌다
    }

    // 씬에 저작된 부양 높이(표면 위로 얼마나 떠 있는가). 핵 이전 시 이 높이를 유지한다.
    float _hoverOffset;

    void Start()
    {
        // 핵의 현재 위치에 빌드존을 정렬
        if (MapManager.Instance != null)
            MapManager.Instance.RecenterAt(transform.position);

        // Floor는 OnEnable에서 등록되므로 이 시점엔 바닥 높이가 준비돼 있다.
        _hoverOffset = transform.position.y - SurfaceHeightAt(transform.position);

        StopBeingAWall();
        Occupy(transform.position);

        // 초기 영역 확정 → 핵 생성 전에 OnEnable한 건물들이 휴면 여부 재평가하도록.
        RaiseTerritoryChanged();
    }

    // ── 통행 차단은 **한 칸만** ───────────────────────────────────────────────
    //
    // 핵도 건물처럼 **자기 타일 하나**만 막는다(서브코어가 `PlacedBuilding`으로 하는 것과 같다).
    // 핵은 씬에 미리 놓인 오브젝트라 그 경로를 안 타서, 전에는 **콜라이더로만** 막혔다 —
    // 그런데 길찾기의 장애물 검사는 셀마다 높이 ±2짜리 상자를 던지므로, **떠 있는 구슬**
    // (y=2, 반지름 0.5)이 발밑 지면 칸에 걸리고 셀 경계에 걸쳐 **2×2가 통째로** 막혔다.
    // 보이는 것은 공중의 구슬인데 그 밑을 못 지나가니 설명할 길이 없었다.
    Vector2Int? _occupiedTile;

    void Occupy(Vector3 worldPosition)
    {
        var map = MapManager.Instance;
        if (map == null) return;

        var tile = map.WorldToTile(worldPosition);
        if (_occupiedTile == tile) return;

        Release();
        map.SetTile(tile, TileData.MakeImpassable());
        _occupiedTile = tile;
    }

    void Release()
    {
        var map = MapManager.Instance;
        if (map != null && _occupiedTile.HasValue)
            map.SetTile(_occupiedTile.Value, TileData.MakePassable());
        _occupiedTile = null;
    }

    // 점유로 막게 됐으므로 콜라이더까지 장애물이면 **같은 것을 두 번 막는다** — 그리고
    // 그쪽이 더 넓게 번진다. 장애물 레이어(`Wall`)에 있으면 내려놓는다.
    //
    // 씬 값을 코드가 덮는 건 보통 피하지만, 여기선 그대로 두면 위 점유가 아무 효과가 없다
    // (넓은 쪽이 이긴다). 무엇을 왜 바꿨는지 한 번 찍어 둔다.
    void StopBeingAWall()
    {
        var finder = FindFirstObjectByType<HierarchicalPathfinder>();
        if (finder == null) return;

        int mask = finder.ObstacleLayer.value;
        if ((mask & (1 << gameObject.layer)) == 0) return;

        string was = LayerMask.LayerToName(gameObject.layer);
        gameObject.layer = 0;   // Default — 클릭 판정은 레이어 마스크를 안 쓰므로 그대로 된다
        Debug.Log($"[던전 핵] 레이어를 '{was}'에서 'Default'로 내렸습니다 — " +
                  "장애물 레이어에 있으면 떠 있는 콜라이더가 발밑 여러 칸을 막습니다. " +
                  "통행 차단은 점유 타일 한 칸이 맡습니다.");
    }

    // 메인 핵 이전(relocate). 정착지 필드맵은 고정(200×200)이므로 그리드를 recenter하지 않는다.
    // 코어는 고정 필드 안에서만 이동하며, 영역(핵 radius union)만 바뀐다 → 건물 휴면 자동 재평가.
    // 점유(_occupied)는 절대좌표라 이동해도 건물·점유가 리셋되지 않는다.
    // (UI/입력 트리거는 후속 — 이 메서드만 호출하면 됨)
    public void RelocateTo(Vector3 worldPosition)
    {
        // Y는 목적지 표면 + 저작 부양 높이 — 일부러 띄워둔 핵이 이전 후 바닥에 박히지 않게.
        float y = SurfaceHeightAt(worldPosition) + _hoverOffset;
        transform.position = new Vector3(worldPosition.x, y, worldPosition.z);
        Occupy(transform.position);   // 떠난 칸은 풀고 새 칸을 막는다
        RaiseTerritoryChanged();
    }

    // 해당 위치 타일의 표면 높이(바닥 윗면 ∥ 지형). 맵이 없으면 그 위치의 Y를 그대로 본다.
    static float SurfaceHeightAt(Vector3 worldPos)
    {
        var map = MapManager.Instance;
        return map != null ? map.SurfaceHeight(map.WorldToTile(worldPos)) : worldPos.y;
    }

    // 정착지 전환 시 이 코어를 활성 메인 코어로 재지정 (SettlementManager가 호출).
    // MapManager.SetAsPlayerZone과 짝 — 활성 정착지의 코어/존 싱글톤을 일치시킨다.
    public void SetAsActive()
    {
        Instance = this;
        RaiseTerritoryChanged();
    }

    public void Select()
    {
        IsSelected = true;
        OnSelected?.Invoke(this);
    }

    public void Deselect() { IsSelected = false; }

    [System.Serializable]
    public struct MinionEntry
    {
        public MinionData Data;
        public GameObject Prefab;
    }

    [Header("소환 가능한 하수인 목록")]
    [Tooltip("비워 두는 게 기본 — 그러면 Resources/Data/Units/ 의 MinionData가 그대로 목록이 된다. " +
             "채우면 그게 이기는 큐레이션 오버라이드(BuildModeManager.placeableBuildings와 같은 관례).")]
    [SerializeField] MinionEntry[] minionEntries;

    // 소환 목록. **씬 배열이 비면 레지스트리**를 쓴다 — 건설 목록과 같은 관례다.
    // 씬에 사는 목록은 저장을 빠뜨리면 조용히 날아가고, 무엇보다 **모드가 종족을 추가할 진입점이 없다.**
    readonly List<MinionData> _minionCache = new();

    // **연구로 잠긴 것은 목록에서 빠진다**(`MinionData.RequiredResearch`). 회색으로 남기지
    // 않는 이유: 소환 메뉴는 "지금 부를 수 있는 것"의 목록이고, 못 부르는 이유가 자원인지
    // 연구인지를 그 좁은 줄에 적기 시작하면 건설 목록의 규칙과 갈린다.
    public IReadOnlyList<MinionData> Minions
    {
        get
        {
            _minionCache.Clear();

            if (minionEntries == null || minionEntries.Length == 0)
            {
                foreach (var data in DataManager.AllMinions)
                    if (data != null && data.IsUnlocked) _minionCache.Add(data);
                return _minionCache;
            }

            foreach (var entry in minionEntries)
                if (entry.Data != null && entry.Data.IsUnlocked) _minionCache.Add(entry.Data);
            return _minionCache;
        }
    }

    // 프리팹 찾기 — **씬 배열이 이긴다.** 에셋(`MinionData.Prefab`)은 씬 오브젝트를 참조할 수 없어서,
    // 씬 오브젝트를 원본으로 쓰던 기존 배선은 오버라이드로만 표현된다.
    GameObject FindPrefab(MinionData data)
    {
        if (minionEntries != null)
            foreach (var entry in minionEntries)
                if (entry.Data == data && entry.Prefab != null) return entry.Prefab;

        return data.Prefab;
    }

    // 하수인 소환 — 캡 확인 → 자원 소비 → 프리팹 스폰
    public bool Summon(MinionData minionData, Vector3 spawnPosition)
    {
        if (minionData == null) return false;

        if (CurrentMinionCount >= TotalMinionCap)
        {
            Debug.Log($"[DungeonCore] 하수인 한도 초과 ({CurrentMinionCount}/{TotalMinionCap})");
            return false;
        }

        GameObject prefab = FindPrefab(minionData);

        if (prefab == null)
        {
            Debug.LogWarning($"[DungeonCore] {minionData.RaceName}에 연결된 프리팹이 없습니다 — " +
                             $"'{minionData.name}' 에셋의 Prefab 칸을 채우거나 " +
                             "씬의 DungeonCore.minionEntries에 등록하세요.");
            return false;
        }

        // 바이옴 콘텐츠 게이팅(step 7). BiomeContentData 미설정 바이옴은 제한 없음 → 기존 씬 무영향.
        if (SettlementManager.Instance != null &&
            !BiomeContent.IsMinionAllowed(SettlementManager.Instance.ActiveBiome, minionData))
        {
            Debug.Log($"[DungeonCore] {minionData.RaceName}은(는) 현재 바이옴에서 소환 불가");
            return false;
        }

        if (minionData.SummonCostAmount > 0 &&
            !Storage.ConsumeByItem(minionData.SummonCostItem, minionData.SummonCostAmount))
        {
            Debug.Log($"[DungeonCore] 자원 부족 — {minionData.SummonCostItem?.ItemName} ×{minionData.SummonCostAmount} 필요");
            return false;
        }

        // **반드시 켜서 내보낸다.** `Instantiate`는 원본의 활성 상태를 그대로 복제하는데,
        // 씬에 꺼진 채로 둔 템플릿(또는 모드가 꺼진 채로 구운 프리팹)을 원본으로 쓰면
        // 소환된 하수인이 꺼진 채 나와 `Awake`조차 안 돈다 — 아무 일도 안 일어난 것처럼 보인다.
        var spawned = Instantiate(prefab, spawnPosition, Quaternion.identity);
        spawned.SetActive(true);

        Debug.Log($"[DungeonCore] {minionData.RaceName} 소환 ({CurrentMinionCount}/{TotalMinionCap})");
        return true;
    }

    // 메인 코어 또는 서브 코어 범위 내 여부
    public bool IsWithinAnyRange(Vector2Int tile)
    {
        if (IsWithinRange(tile)) return true;
        foreach (var sc in _subCores)
            if (sc != null && sc.IsWithinRange(tile)) return true;
        return false;
    }

    public bool IsWithinAnyRangeWorld(Vector3 worldPos)
    {
        if (MapManager.Instance == null) return false;
        var tile = MapManager.Instance.WorldToTile(worldPos);
        if (IsWithinRange(tile)) return true;
        foreach (var sc in _subCores)
            if (sc != null && sc.IsWithinRangeWorld(worldPos)) return true;
        return false;
    }

    // 메인 핵이 candidateCorePos에 있다고 가정했을 때 worldPos가 영역(메인+서브 union) 안인지.
    // 핵 이전 미리보기(CoreRelocator)에서 휴면/재가동 예정 건물을 세는 데 쓴다.
    // 판정은 IsWithinAnyRangeWorld와 동일하게 타일 중심 기준으로 맞춘다(경계에서 미리보기 ≠ 실제 방지).
    public bool WouldBeInRangeAt(Vector3 candidateCorePos, Vector3 worldPos)
    {
        if (data == null) return true;                 // 반경 미설정 = 무제한 (IsWithinRange 관례)
        if (MapManager.Instance == null) return false;

        var wp = MapManager.Instance.TileToWorld(MapManager.Instance.WorldToTile(worldPos));
        var dx = wp.x - candidateCorePos.x;
        var dz = wp.z - candidateCorePos.z;
        if (dx * dx + dz * dz <= data.BaseRadius * data.BaseRadius) return true;

        foreach (var sc in _subCores)
            if (sc != null && sc.IsWithinRangeWorld(worldPos)) return true;
        return false;
    }

    // 코어 주변 랜덤 위치 반환
    public Vector3 GetSpawnPosition()
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float dist  = 2f;
        return transform.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
    }

    public bool IsWithinRange(Vector2Int tile)
    {
        if (data == null) return true;
        if (MapManager.Instance == null) return false;
        var wp = MapManager.Instance.TileToWorld(tile);
        var dx = wp.x - transform.position.x;
        var dz = wp.z - transform.position.z;
        return dx * dx + dz * dz <= data.BaseRadius * data.BaseRadius;
    }

    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != Camera.main) return;
        DrawRange();
    }

    void OnRenderObject()
    {
        if (GraphicsSettings.currentRenderPipeline != null) return;
        DrawRange();
    }

    void DrawRange()
    {
        if (data == null) return;

        float radius = data.BaseRadius;
        Vector3 center = new Vector3(transform.position.x, 0.02f, transform.position.z);

        _mat.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINE_STRIP);
        GL.Color(IsSelected ? selectedColor : rangeColor);

        for (int i = 0; i <= rangeSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / rangeSegments;
            GL.Vertex3(center.x + Mathf.Cos(angle) * radius,
                       center.y,
                       center.z + Mathf.Sin(angle) * radius);
        }

        GL.End();
        GL.PopMatrix();
    }
}
