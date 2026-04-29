using UnityEngine;

public class CraftJob : JobBase
{
    readonly CraftingTable _table;
    readonly RecipeData    _recipe;
    float _craftEndTime;

    public override JobCategory Category => _recipe?.Category ?? JobCategory.Crafting;

    public CraftJob(CraftingTable table, RecipeData recipe)
    {
        _table  = table;
        _recipe = recipe;
        Priority = 1;

        if (MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(table.transform.position);
    }

    protected override void OnStart()
    {
        if (_recipe == null || _table == null) { Status = JobStatus.Done; return; }

        // 재료 확인 후 즉시 소비 (예약 개념)
        foreach (var ing in _recipe.Ingredients)
        {
            if (StorageBuilding.GetTotalByItem(ing.Item) < ing.Amount)
            {
                Status = JobStatus.Done;
                return;
            }
        }
        foreach (var ing in _recipe.Ingredients)
            StorageBuilding.ConsumeByItem(ing.Item, ing.Amount);

        _craftEndTime = Time.time + _recipe.CraftTime;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_recipe == null || _table == null) { Status = JobStatus.Done; return; }

        if (Time.time < _craftEndTime) return;

        // 제작 완료 — 항상 테이블 위에 드롭 (운반 작업으로 창고로 이동됨)
        DroppedItem.Spawn(_recipe.Result, _recipe.ResultAmount,
            _table.transform.position + Vector3.up * 0.3f);

        Status = JobStatus.Done;
    }
}
