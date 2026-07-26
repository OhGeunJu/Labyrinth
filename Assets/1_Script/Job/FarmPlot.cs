using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum FarmState { Empty, Planted, ReadyToHarvest }

public class FarmPlot : MonoBehaviour, IWorldClickable
{
    public static readonly List<FarmPlot> All = new();
    public static event Action<FarmPlot> OnSelected;

    [SerializeField] SeedData[] availableSeeds;

    public SeedData[] AvailableSeeds  => availableSeeds;
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
            if (Storage.GetTotalByItem(SelectedSeed) > 0)
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
