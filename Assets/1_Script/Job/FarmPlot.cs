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

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

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

    // SelectionManager의 단일 레이캐스트가 이 농지를 클릭 판정 시 호출.
    public void OnWorldClick() => OnSelected?.Invoke(this);

    void UpdateGrowth()
    {
        if (State != FarmState.Planted || SelectedSeed == null) return;

        if (Moisture > 0f)
        {
            Moisture       = Mathf.Max(0f, Moisture - SelectedSeed.MoistureDecay * Time.deltaTime);
            GrowthProgress += Time.deltaTime / SelectedSeed.GrowthTime;

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
            if (MaterialPool.AvailableAnywhere(SelectedSeed) > 0)
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

    public void SetSeed(SeedData seed)
    {
        if (State != FarmState.Empty) return;
        SelectedSeed    = seed;
        _seedJobPending = false;
    }

    public void ClearSeed()
    {
        if (State != FarmState.Empty) return;
        SelectedSeed    = null;
        _seedJobPending = false;
    }

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
        // SelectedSeed 유지 → 다음 사이클 자동 시작
    }

    public void OnHarvestJobCancelled() => _harvestJobPending = false;

    public void Init(BuildingData data) { }
}
