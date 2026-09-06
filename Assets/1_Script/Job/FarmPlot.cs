using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum FarmState { Empty, Planted, ReadyToHarvest }

public class FarmPlot : MonoBehaviour, IWorldClickable
{
    public static readonly List<FarmPlot> All = new();
    public static event Action<FarmPlot> OnSelected;

    [Tooltip("비우면 등록된 씨앗 **전부**를 쓴다. 이 밭만 일부만 심게 하고 싶을 때만 채운다.")]
    [SerializeField] SeedData[] availableSeeds;

    // **같은 밭인데 더 빨리 자란다**(2026-08-25, 온실). 씨앗의 `GrowthTime`을 나눈다 —
    // 작물마다 따로 저작하지 않아야 **씨앗을 하나 더할 때 온실 표까지 손보는 일**이 안 생긴다.
    //
    // 1.0이 보통이다. **아래로는 안 쓴다** — 느린 밭은 짓지 말라는 뜻이라 선택이 아니다.
    [Tooltip("성장 속도 배율. 보통 밭 1.0 · 온실 1.5")]
    [SerializeField] float growthMultiplier = 1f;

    public float GrowthMultiplier => Mathf.Max(0.1f, growthMultiplier);

    // 지금 물길이 닿고 있나 — 살펴보기·패널이 *"왜 안 마르지"*에 답할 수 있게 밖으로 낸다.
    public bool IsIrrigated => Irrigation.Covers(transform.position);

    // **비어 있으면 레지스트리에서 끌어온다.**
    // 프리팹 배열에만 의존하면 상인에게 씨앗을 사 와도 목록에 안 떠서 심을 수가 없다
    // — 런타임에 지은 밭은 특히 그렇다(아무도 배열을 채워 주지 않는다).
    // 채우면 그게 이긴다(큐레이션용) — `ResearchTable.availableResearch`와 같은 관례.
    public SeedData[] AvailableSeeds
    {
        get
        {
            if (availableSeeds != null && availableSeeds.Length > 0) return availableSeeds;

            _seedFallback.Clear();
            foreach (var kv in DataManager.AllItems)
                if (kv.Value is SeedData s) _seedFallback.Add(s);
            _seedFallback.Sort((a, b) => string.Compare(a.ItemName, b.ItemName, StringComparison.OrdinalIgnoreCase));
            return _seedFallback.ToArray();
        }
    }

    // 매번 새 배열을 만들면 패널이 열려 있는 동안 프레임마다 쓰레기가 생긴다 — 한 벌만 돌려 쓴다.
    static readonly List<SeedData> _seedFallback = new();
    public SeedData   SelectedSeed    { get; private set; }
    public FarmState  State           { get; private set; } = FarmState.Empty;
    public float      GrowthProgress  { get; private set; }
    public float      Moisture        { get; private set; }

    const float WaterThreshold   = 30f;
    const float JobCheckInterval = 2f;

    bool  _seedJobPending;
    bool  _waterJobPending;
    bool  _harvestJobPending;
    float _jobCheckTimer;

    public Vector2Int TileCoord => MapManager.Instance != null
        ? MapManager.Instance.WorldToTile(transform.position)
        : Vector2Int.zero;

    void OnEnable()  { All.Add(this); JoinNeighborGroup(); FarmZones.MarkDirty(); }
    void OnDisable() { All.Remove(this); FarmZones.MarkDirty(); }

    // **밭을 도는 곳은 여기 하나다**(2026-09-05에 되살렸다).
    //
    // 2026-09-02에 구역(`FarmZones`)을 붙이면서 `OnEnable`/`OnDisable`을 고치다 **`Update`가
    // 통째로 사라졌다.** `UpdateGrowth`와 `TryRegisterJobs`는 그대로 남아 있는데 **부르는
    // 곳이 없어져서**, 작물이 자라지도 않고 심기·물주기·수확 일감이 하나도 안 나갔다 —
    // **농사가 통째로 죽어 있었다.** 컴파일도 통과하고 밭도 놓이고 구역 테두리까지 그려지니
    // 화면에는 *"심었는데 안 자란다"*로만 보인다.
    //
    // (`private` 메서드에 부르는 곳이 없어도 C#은 경고를 안 준다 — 그래서 지우는 쪽이
    // 안 걸린다. 남는 교훈: **없어진 것은 `Update`인데 증상은 다른 자리에서 난다.**)
    void Update()
    {
        UpdateGrowth();

        _jobCheckTimer -= Time.deltaTime;
        if (_jobCheckTimer <= 0f)
        {
            _jobCheckTimer = JobCheckInterval;
            TryRegisterJobs();
        }
    }

    // **면을 칠하지 않는다 — 테두리로 그린다**(`FarmZoneOverlay`, 2026-09-02).
    //
    // 처음엔 흙 색에 구역 색을 곱했는데 **애매했다**(사용자). 어두운 굴에서 옅은 곱셈은
    // *"저 밭이 좀 누런가"* 정도로만 보이고, 무엇보다 **어디서 끊기는지가 안 보인다** —
    // 구역을 표시하는 목적의 절반이 *경계*인데 면을 칠하면 경계가 제일 안 보이는 것이 된다.

    // SelectionManager의 단일 레이캐스트가 이 농지를 클릭 판정 시 호출.
    // **구역을 그리는 중에는 창을 안 연다**(2026-09-02, 사용자). 그 모드의 클릭은 *구역을
    // 긋는 조작*이라, 같은 클릭에 창까지 뜨면 **긋는 내내 창이 깜빡이고** 화면을 가린다.
    //
    // 창을 여는 쪽에서 막는다 — 지정기가 클릭을 삼키게 하면 **선택 해제 같은 다른 일까지
    // 같이 막힌다**(클릭 하나가 하는 일이 여럿이다).
    public void OnWorldClick()
    {
        if (FarmZoneDesignator.IsActive) return;
        OnSelected?.Invoke(this);
    }

    void UpdateGrowth()
    {
        if (State != FarmState.Planted || SelectedSeed == null) return;

        if (Moisture > 0f)
        {
            // **물길이 닿으면 안 마른다 — 채워 주지는 않는다**(`Irrigation`). 저절로 차오르게
            // 하면 물 긷는 일이 통째로 사라지고(우물도 물통도 뜻을 잃는다) 무엇보다 **심자마자
            // 자란다.** 첫 물은 사람이 대고, 그 뒤로 안 마르는 것이 관개가 사는 값이다.
            if (!IsIrrigated)
                Moisture = Mathf.Max(0f, Moisture - SelectedSeed.MoistureDecay * Time.deltaTime);

            GrowthProgress += Time.deltaTime * GrowthMultiplier / SelectedSeed.GrowthTime;

            if (GrowthProgress >= 1f)
            {
                GrowthProgress = 1f;
                State          = FarmState.ReadyToHarvest;
            }
        }
    }

    void TryRegisterJobs()
    {
        if (JobManager.Instance == null || SelectedSeed == null) return;

        if (State == FarmState.Empty && !_seedJobPending)
        {
            // **폰이 들고 있는 씨앗도 센다.** 창고만 보면 상점에서 막 사 온 씨앗(배낭에 있다)으로는
            // 일감이 영영 안 걸린다 — 씨앗이 있는데 아무 일도 안 일어나던 원인이 이것이었다.
            // 누가 심을지는 아직 모르므로 넓게 보고, 실제 배정은 `SeedJob.CanAssignTo`가 거른다.
            if (MaterialPool.AvailableNear(SelectedSeed, transform.position) > 0)
            {
                _seedJobPending = true;
                JobManager.Instance.RegisterJob(new SeedJob(this, SelectedSeed));
            }
        }
        else if (State == FarmState.Planted && !_waterJobPending && Moisture < WaterThreshold)
        {
            _waterJobPending = true;
            JobManager.Instance.RegisterJob(new WaterJob(this));
        }
        else if (State == FarmState.ReadyToHarvest && !_harvestJobPending)
        {
            _harvestJobPending = true;
            JobManager.Instance.RegisterJob(new FarmHarvestJob(this, SelectedSeed));
        }
    }

    // ── 구역 ────────────────────────────────────────────────────────────────
    //
    // **어느 밭들이 한 덩어리인가는 `FarmZones`가 답한다** — 규칙(번호 우선, 없으면 맞닿음)과
    // 색을 한 곳에 두려는 것이다. 여기서 또 세면 **패널이 세는 구역과 밭이 칠하는 색이
    // 갈린다.**

    // 손으로 묶은 번호. **0이면 자동**(맞닿은 것끼리) — 대부분은 여기 머문다.
    [SerializeField] int groupId;

    public int GroupId => groupId;

    public void SetGroup(int id)
    {
        if (groupId == id) return;
        groupId = id;
        FarmZones.MarkDirty();
    }

    // ── 놓는 순간 이웃의 번호를 물려받는다 ──────────────────────────────────
    //
    // 구역은 **번호가 정하고 맞닿았다고 합쳐지지 않는다**(`FarmZones`). 그대로 두면
    // **한 번의 드래그로 깐 밭 스물다섯 장이 구역 스물다섯 개**가 되어 씨앗을 스물다섯 번
    // 골라야 한다 — 갈라 놓을 수 있게 만든 규칙이 정작 처음 놓는 일을 못 쓰게 만든다.
    //
    // 그래서 **놓일 때 한 번만** 이웃을 본다: 붙어 있는 밭이 있으면 그 번호를, 없으면 새 번호.
    // **그 뒤로는 안 본다** — 도구로 가른 두 구역은 붙어 있어도 그대로 남는다.
    static readonly Vector2Int[] Around =
        { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

    void JoinNeighborGroup()
    {
        if (groupId != 0) return;

        var here = TileCoord;
        for (int i = 0; i < Around.Length; i++)
        {
            var t = here + Around[i];
            foreach (var p in All)
            {
                if (p == null || p == this || p.groupId == 0) continue;
                if (p.TileCoord != t) continue;
                groupId = p.groupId;
                return;
            }
        }

        int max = 0;
        foreach (var p in All) if (p != null && p.groupId > max) max = p.groupId;
        groupId = max + 1;
    }

    public IReadOnlyList<FarmPlot> Zone() => FarmZones.Of(this);

    // ── 씨앗 ────────────────────────────────────────────────────────────────

    // **구역 전체에 건다.** 밭 하나를 눌러도 이어진 밭이 다 따라온다 — 그게 구역의 뜻이다.
    public void SetSeed(SeedData seed)
    {
        foreach (var p in Zone()) p.SetSeedHere(seed);
    }

    public void ClearSeed()
    {
        foreach (var p in Zone()) p.SetSeedHere(null);
    }

    // 이 칸 하나에만. **자라는 중이면 지금 것을 뽑지 않는다** — 다 키운 작물을 씨앗을
    // 바꿨다는 이유로 버리면, 바꾸는 것 자체가 값이 큰 조작이 된다. **다음 사이클부터** 바꾼다
    // (`OnHarvested`가 물려받는다). 그래서 *"바꿨는데 왜 아직 밀이지"*가 안 생기게 패널이
    // 그 상태를 같이 띄운다(`NextSeed`).
    void SetSeedHere(SeedData seed)
    {
        if (State == FarmState.Empty)
        {
            SelectedSeed    = seed;
            NextSeed        = null;
            _clearNext      = false;
            _seedJobPending = false;
            return;
        }

        NextSeed   = seed;
        _clearNext = seed == null;
    }

    // **"안 바꾼다"와 "비운다"는 다른 뜻이다**(2026-09-05). `NextSeed = null` 하나로 둘을
    // 적었더니 자라는 밭에서 [지우기]가 **아무 일도 안 했다** — 거두고 나서 `NextSeed`가
    // 비어 있으니 *"바꿀 것이 없다"*로 읽혀 `SelectedSeed`가 그대로 남고, 밭이 **영원히
    // 다시 심었다**(화면에는 버튼이 안 먹는 것으로만 보인다).
    bool _clearNext;

    // 거두고 나면 비워지나 — 패널이 그 상태를 띄울 수 있게 열어 둔다(`NextSeed`와 같은 자리).
    public bool ClearQueued => _clearNext;

    // 지금 자라는 것을 거둔 뒤에 바뀔 씨앗. 없으면 그대로 간다.
    public SeedData NextSeed { get; private set; }

    // ── Job 콜백 ─────────────────────────────────────────────────────────────

    public void OnSeeded()
    {
        State           = FarmState.Planted;
        GrowthProgress  = 0f;
        Moisture        = 0f;
        _seedJobPending = false;
    }

    public void OnSeedJobCancelled()    => _seedJobPending    = false;

    public void OnWatered()
    {
        Moisture         = SelectedSeed != null ? SelectedSeed.WaterFillAmount : 100f;
        _waterJobPending = false;
    }

    public void OnWaterJobCancelled()   => _waterJobPending   = false;

    public void OnHarvested()
    {
        State              = FarmState.Empty;
        GrowthProgress     = 0f;
        Moisture           = 0f;
        _harvestJobPending = false;

        // **바꿔 둔 것이 있으면 이제 바뀐다** — 자라는 중에 구역의 씨앗을 바꿔도 다 큰 작물을
        // 버리지 않고, 거두고 나서 갈아탄다. 없으면 `SelectedSeed` 유지 → 다음 사이클 자동 시작.
        if (_clearNext)
        {
            SelectedSeed = null;
            NextSeed     = null;
            _clearNext   = false;
        }
        else if (NextSeed != null)
        {
            SelectedSeed = NextSeed;
            NextSeed     = null;
        }
    }

    public void OnHarvestJobCancelled() => _harvestJobPending = false;

    public void Init(BuildingData data) { }
}
