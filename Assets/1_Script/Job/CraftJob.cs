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
    bool  _timerStarted; // 유닛이 작업대에 도착해 실제 제작을 시작했는지
    bool  _consumed;   // 재료 차감됨 + 아직 완성 전 → 취소 시 환불 대상

    public override JobCategory Category => _recipe?.Category ?? JobCategory.Crafting;

    // 재료가 다 있어야 배정 — 없으면 큐에서 대기(주문 유지), 재료 입고 시 자동 재개.
    public override bool IsReady => _table != null && _recipe != null && _table.CanCraft(_recipe);

    public CraftJob(CraftingTable table, RecipeData recipe)
    {
        _table  = table;
        _recipe = recipe;
        Priority = 1;
        SetSite(table);

        if (MapManager.Instance != null)
            TileCoord = MapManager.Instance.WorldToTile(table.transform.position);
    }

    protected override void OnStart()
    {
        if (_recipe == null || _table == null) { Status = JobStatus.Done; return; }

        // 재료 재확인 후 즉시 소비 (예약 개념).
        // IsReady 게이트로 거르지만, 예약~시작 사이 타 제작자가 소진했을 수 있어 여기서 한 번 더 확인.
        // 부족하면 주문을 없애지 않고 큐에 되돌려(재등록) 재료 도착 시 자동 재개하게 한다.
        foreach (var ing in _recipe.Ingredients)
        {
            if (Storage.GetTotalByItem(ing.Item) < ing.Amount)
            {
                Debug.Log($"[CraftJob] 재료 부족 — {ing.Item.ItemName} ×{ing.Amount} 필요, 주문 대기열로 반환");
                _table.QueueOrder(_recipe);   // 동등 주문 재등록 (IsReady 게이트로 대기)
                Status = JobStatus.Done;      // 이 잡 인스턴스는 종료 → 유닛이 놓음
                return;
            }
        }
        foreach (var ing in _recipe.Ingredients)
            Storage.ConsumeByItem(ing.Item, ing.Amount);
        _consumed = true;

        Debug.Log($"[CraftJob] 제작 시작 — {_recipe.RecipeName} ({_recipe.CraftTime}초)");
        // 타이머는 유닛이 작업대에 도착한 첫 Execute에서 시작 (이동 시간이 제작 시간을 깎지 않게)
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_recipe == null || _table == null) { Status = JobStatus.Done; return; }

        // 연료 없으면 대기 (FuelJob이 채워줄 때까지)
        if (!_table.HasFuel) return;

        if (!_timerStarted)
        {
            _timerStarted = true;
            _craftEndTime = Time.time + _recipe.CraftTime;
        }
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
            DroppedItem.Spawn(_recipe.Result, leftover, _table.transform.position + Vector3.up * 0.3f, quality);

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
        if (_recipe == null) return;

        if (_consumed)
        {
            _consumed = false;
            Vector3 pos = _table != null ? _table.transform.position : Vector3.zero;
            foreach (var ing in _recipe.Ingredients)
                DroppedItem.Spawn(ing.Item, ing.Amount, pos + Vector3.up * 0.3f);
        }

        // 주문 자체는 잃지 않는다 — 큐에 되돌려 다른 폰(또는 나중에 이 폰)이 이어받게 한다.
        // 재료 부족 경로와 같은 정책. 취소가 사망뿐 아니라 **플레이어 이동 명령**으로도
        // 일어나게 됐으므로, 주문이 조용히 사라지면 안 된다.
        _table?.QueueOrder(_recipe);
    }
}
