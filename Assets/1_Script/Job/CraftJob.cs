using UnityEngine;

public class CraftJob : JobBase
{
    // 스킬 100 기준 확률: 전설 2% / 에픽 5% / 유니크 10% / 희귀 20% / 고급 35% / 일반 28%
    static ItemQuality RollQuality(float skill, bool skilled)
    {
        if (!skilled) return ItemQuality.Common;

        float t    = Mathf.Clamp01(skill / 100f);
        float roll = UnityEngine.Random.value;

        if (roll < t * t * t * t * t * 0.02f) return ItemQuality.Legendary;
        if (roll < t * t * t * t       * 0.05f) return ItemQuality.Epic;
        if (roll < t * t * t           * 0.10f) return ItemQuality.Unique;
        if (roll < t * t               * 0.20f) return ItemQuality.Rare;
        if (roll < t                   * 0.35f) return ItemQuality.Uncommon;
        return ItemQuality.Common;
    }

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
            if (Storage.GetTotalByItem(ing.Item) < ing.Amount)
            {
                Debug.Log($"[CraftJob] 재료 부족 — {ing.Item.ItemName} ×{ing.Amount} 필요");
                Status = JobStatus.Done;
                return;
            }
        }
        foreach (var ing in _recipe.Ingredients)
            Storage.ConsumeByItem(ing.Item, ing.Amount);

        Debug.Log($"[CraftJob] 제작 시작 — {_recipe.RecipeName} ({_recipe.CraftTime}초)");
        _craftEndTime = Time.time + _recipe.CraftTime;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_recipe == null || _table == null) { Status = JobStatus.Done; return; }

        if (Time.time < _craftEndTime) return;

        float skill    = unit.Stats.GetCraftSkill(_recipe.RequiredSkillType);
        float required = _recipe.RequiredSkillLevel;
        bool  skilled  = skill >= required;

        // ── 장비(무기/방어구)는 등급 판정, 나머지는 수량 배율 ─────────────────
        bool isEquip = _recipe.Result is WeaponData || _recipe.Result is ArmorData;

        int         finalAmount;
        ItemQuality quality;

        if (isEquip)
        {
            quality     = RollQuality(skill, skilled);
            finalAmount = _recipe.ResultAmount;   // 장비는 수량 고정
        }
        else
        {
            quality     = ItemQuality.Common;
            float ratio = Mathf.Clamp01(skill / 100f);
            float mult  = skilled ? 1f + ratio * 0.5f : 0.5f;
            finalAmount = Mathf.Max(1, Mathf.RoundToInt(_recipe.ResultAmount * mult));
        }

        // ── 인벤토리 입고 / 드롭 ──────────────────────────────────────────────
        int added    = unit.Inventory.TryAdd(_recipe.Result, finalAmount, quality);
        int leftover = finalAmount - added;
        if (leftover > 0)
            DroppedItem.Spawn(_recipe.Result, leftover, _table.transform.position + Vector3.up * 0.3f);

        // ── 스킬 성장 ─────────────────────────────────────────────────────────
        if (_recipe.SkillGrowthAmount > 0f)
            unit.Stats.GrowCraftSkill(_recipe.RequiredSkillType, _recipe.SkillGrowthAmount);

        // ── 로그 ──────────────────────────────────────────────────────────────
        string dest      = added > 0 && leftover == 0 ? "인벤토리"
                         : added > 0                   ? $"인벤토리 {added} + 드롭 {leftover}"
                         :                               "드롭";
        string qualLabel = isEquip ? $" [{quality.DisplayName()}]" : "";
        string penalty   = !skilled ? " (스킬 미달 -50%)" : "";
        Debug.Log($"[CraftJob] 제작 완료 — {_recipe.RecipeName}{qualLabel} ×{finalAmount}{penalty} → {dest}  ({_recipe.RequiredSkillType} {skill:F0})");
        Status = JobStatus.Done;
    }
}
