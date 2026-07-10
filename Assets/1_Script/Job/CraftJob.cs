using UnityEngine;

public class CraftJob : JobBase
{
    // 스킬에 따라 중심 등급이 이동하는 슬라이딩 윈도우.
    // 스킬 0→일반 중심, 스킬 100→전설 중심. spread 범위 밖 등급은 확률 0.
    static ItemQuality RollQuality(float skill, bool skilled)
    {
        if (!skilled) return ItemQuality.Common;

        const int   COUNT  = 6;
        const float SPREAD = 1.5f;

        float center = skill / 100f * (COUNT - 1);   // 0.0(일반) ~ 5.0(전설)

        float[] w     = new float[COUNT];
        float   total = 0f;
        for (int i = 0; i < COUNT; i++)
        {
            w[i]   = Mathf.Max(0f, 1f - Mathf.Abs(i - center) / SPREAD);
            total += w[i];
        }

        float roll = UnityEngine.Random.value * total;
        float cum  = 0f;
        for (int i = 0; i < COUNT; i++)
        {
            cum += w[i];
            if (roll <= cum) return (ItemQuality)i;
        }
        return ItemQuality.Legendary;
    }

    readonly CraftingTable _table;
    readonly RecipeData    _recipe;
    float _craftEndTime;
    bool  _consumed;   // 재료 차감됨 + 아직 완성 전 → 취소 시 환불 대상

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
        _consumed = true;

        Debug.Log($"[CraftJob] 제작 시작 — {_recipe.RecipeName} ({_recipe.CraftTime}초)");
        _craftEndTime = Time.time + _recipe.CraftTime;
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_recipe == null || _table == null) { Status = JobStatus.Done; return; }

        // 연료 없으면 대기 (FuelJob이 채워줄 때까지)
        if (!_table.HasFuel) return;

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

        // 완성품 생성 시점 — 재료는 결과물로 전환됨, 환불 대상 아님
        _consumed = false;

        // ── 인벤토리 입고 / 드롭 ──────────────────────────────────────────────
        int added    = unit.Inventory.TryAdd(_recipe.Result, finalAmount, quality);
        int leftover = finalAmount - added;
        if (leftover > 0)
            DroppedItem.Spawn(_recipe.Result, leftover, _table.transform.position + Vector3.up * 0.3f);

        // ── 스킬 성장 (켄시식) ────────────────────────────────────────────────
        if (_recipe.SkillGrowthAmount > 0f)
        {
            float xp = _recipe.SkillGrowthAmount
                     * CombatXp.ChallengeMult(skill, required)
                     * CombatXp.Sol(skill);
            unit.Stats.GrowCraftSkill(_recipe.RequiredSkillType, xp);
        }

        // ── 로그 ──────────────────────────────────────────────────────────────
        string dest      = added > 0 && leftover == 0 ? "인벤토리"
                         : added > 0                   ? $"인벤토리 {added} + 드롭 {leftover}"
                         :                               "드롭";
        string qualLabel = isEquip ? $" [{quality.DisplayName()}]" : "";
        string penalty   = !skilled ? " (스킬 미달 -50%)" : "";
        Debug.Log($"[CraftJob] 제작 완료 — {_recipe.RecipeName}{qualLabel} ×{finalAmount}{penalty} → {dest}  ({_recipe.RequiredSkillType} {skill:F0})");
        Status = JobStatus.Done;
    }

    // 완성 전 취소(유닛 사망/재배정/연료 부재 등) 시 차감한 재료를 작업대에 드롭 환불.
    // 운반 작업이 창고로 회수 → 재료 손실 방지.
    protected override void OnCancelled()
    {
        if (!_consumed || _recipe == null) return;
        _consumed = false;

        Vector3 pos = _table != null ? _table.transform.position : Vector3.zero;
        foreach (var ing in _recipe.Ingredients)
            DroppedItem.Spawn(ing.Item, ing.Amount, pos + Vector3.up * 0.3f);

        Debug.Log($"[CraftJob] 제작 취소 — 재료 환불(드롭): {_recipe.RecipeName}");
    }
}
