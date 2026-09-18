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
    float _startTime;
    float _carriedSeconds;   // 이번에 붙기 전까지 이미 만들어 둔 몫
    bool  _timerStarted; // 유닛이 작업대에 도착해 실제 제작을 시작했는지
    bool  _consumed;   // 재료 차감됨 + 아직 완성 전 → 취소 시 환불 대상

    // 실제로 만들고 있던 폰. **`Cancel()`이 `ReservedBy`를 먼저 비우므로** `OnCancelled`에서는
    // 예약을 읽을 수 없다 — 여기에 붙들어 둔다(`HaulJob._carrier`와 같은 이유).
    UnitBrain _crafter;

    // 등급이 있는 결과물(무기·방어구)은 **만들던 폰이 이어서** 해야 한다.
    // **무엇이 등급을 갖는지는 물건이 답한다**(`ItemData.HasQuality`) — 여기서 `is WeaponData`로
    // 갈라 쓰면 종류가 늘 때 이 분기를 찾아 고쳐야 하고, 빠뜨리면 조용히 등급이 사라진다.
    bool IsEquipment => _recipe != null && _recipe.Result != null && _recipe.Result.HasQuality;

    // **흐른 시간이 아니라 진척시킨 몫이다**(2026-09-17 — 작업대에 등급이 생겼다).
    //
    // 개량 작업대는 같은 1초에 1.5초어치를 한다. 그래서 여기서 **표준 초로 환산해** 돌려주면
    // 진행 게이지도(`Progress`) 이어 하기도(`SavePartial`) **한 줄도 안 바뀐다** — 그 둘이
    // 재는 것은 *얼마나 남았나*이지 *얼마나 앉아 있었나*가 아니다.
    //
    // **저장하는 값이 표준 초라야 작업대를 옮겨도 이어진다** — 실제 앉은 초로 저장하면
    // 개량 작업대에서 반쯤 만들다 기본으로 옮긴 순간 진행도가 조용히 부풀거나 줄어든다.
    float BenchSpeed => _table != null ? _table.Speed : 1f;

    float WorkedThisSession => _timerStarted ? Mathf.Max(0f, Time.time - _startTime) * BenchSpeed : 0f;

    public override JobCategory Category => _recipe?.Category ?? JobCategory.Crafting;
    public override WorkAnim    Anim     => WorkAnim.HammerTable;

    // ── 진행 상황 (UI 게이지용) — `ResearchJob`과 같은 모양 ────────────────
    public RecipeData Recipe => _recipe;

    // 폰이 작업대에 붙어 타이머가 도는 동안에만 true. 걸어오는 중엔 false.
    public bool IsRunning => _timerStarted && Status == JobStatus.InProgress;

    // 이전에 만들어 둔 몫을 포함한다 — 이어서 하는 것이므로 게이지가 0부터 다시 차면 안 된다.
    public float Progress
    {
        get
        {
            if (_recipe == null) return 0f;
            float total = Mathf.Max(0.01f, _recipe.CraftTime);
            return Mathf.Clamp01((_carriedSeconds + WorkedThisSession) / total);
        }
    }

    // 준비는 됐는데 **이 폰은 안 되는** 경우들.
    public override bool CanAssignTo(UnitBrain unit)
    {
        if (_table == null || _recipe == null) return true;

        // ⓪ 재료를 **이 폰이** 댈 수 있는가(창고 + 자기 배낭).
        // 남의 배낭에 있는 재료로는 이 폰이 만들 수 없다 — 조작한 적 없는 폰의 짐이
        // 줄어드는 일이 없어야 한다.
        foreach (var ing in _recipe.Ingredients)
            if (MaterialPool.Available(ing.Item, unit) < ing.Amount) return false;

        // ① 플레이어가 지정한 제작자
        var assigned = _table.GetAssignedCrafter(_recipe);
        if (assigned != null && assigned != unit) return false;

        // ② 이미 만들다 만 장비 — 그 폰만 이어받는다(등급이 만든 사람의 솜씨이므로).
        //    만들던 폰이 죽었으면 `GetPartialOwner`가 진행을 버리고 null을 준다.
        var owner = _table.GetPartialOwner(_recipe);
        if (owner != null && owner != unit) return false;

        return true;
    }

    // 재료가 다 있어야 배정 — 없으면 큐에서 대기(주문 유지), 재료 입고 시 자동 재개.
    //
    // **연료도 같은 게이트에 넣는다.** 전에는 연료 없이도 배정돼서, 폰이 작업대에 붙은 뒤
    // `Execute`의 연료 검사에 걸려 영원히 서 있었다. 폰이 하나뿐이면 그 폰이 연료를 채우러
    // 갈 수 없으므로 **교착**이다 — 여기서 막으면 폰은 연료 보충 작업을 먼저 집어 간다.
    public override bool IsReady =>
        _table != null && _recipe != null && _table.CanCraft(_recipe) && _table.HasFuel;

    public CraftJob(CraftingTable table, RecipeData recipe)
    {
        _table  = table;
        _recipe = recipe;
        Priority = 1;
        SetSite(table);   // 목표 칸(WorkSpot)도 여기서 정해진다
    }

    protected override void OnStart()
    {
        if (_recipe == null || _table == null) { Status = JobStatus.Done; return; }

        // **이어서 하는 제작은 재료를 다시 받지 않는다.** 재료는 처음 시작할 때 한 번만 들어간다 —
        // 아니면 중단·재개를 반복하는 것만으로 재료가 계속 빨려 들어간다(연구와 같은 정책).
        if (_table.GetPartialSeconds(_recipe) > 0f)
        {
            Debug.Log($"[CraftJob] 이어서 제작 — {_recipe.RecipeName}");
            return;
        }

        // 재료 재확인 후 즉시 소비 (예약 개념).
        // IsReady 게이트로 거르지만, 예약~시작 사이 타 제작자가 소진했을 수 있어 여기서 한 번 더 확인.
        // 부족하면 주문을 없애지 않고 큐에 되돌려(재등록) 재료 도착 시 자동 재개하게 한다.
        // 재료는 **창고 + 이 폰의 배낭**에서 센다(`MaterialPool`). 들고 있는 걸 두고
        // 창고 것만 쓰면, 방금 사 온 재료로는 아무것도 못 만든다.
        foreach (var ing in _recipe.Ingredients)
        {
            if (MaterialPool.Available(ing.Item, ReservedBy) < ing.Amount)
            {
                Debug.Log($"[CraftJob] 재료 부족 — {ing.Item.ItemName} ×{ing.Amount} 필요, 주문 대기열로 반환");
                _table.OrderFinished(_recipe);       // 이 인스턴스는 끝 — 바로 아래 재등록이 다시 +1
                _table.QueueOrder(_recipe);   // 동등 주문 재등록 (IsReady 게이트로 대기)
                Status = JobStatus.Done;      // 이 잡 인스턴스는 종료 → 유닛이 놓음
                return;
            }
        }
        foreach (var ing in _recipe.Ingredients)
            MaterialPool.Consume(ing.Item, ing.Amount, ReservedBy);   // 배낭 먼저, 그다음 창고
        _consumed = true;

        Debug.Log($"[CraftJob] 제작 시작 — {_recipe.RecipeName} ({_recipe.CraftTime}초)");
        // 타이머는 유닛이 작업대에 도착한 첫 Execute에서 시작 (이동 시간이 제작 시간을 깎지 않게)
    }

    protected override void Execute(UnitBrain unit)
    {
        if (_recipe == null || _table == null) { Status = JobStatus.Done; return; }
        _crafter = unit;

        // 연료 없으면 대기 (FuelJob이 채워줄 때까지)
        if (!_table.HasFuel) return;

        if (!_timerStarted)
        {
            _timerStarted   = true;
            _startTime      = Time.time;
            _carriedSeconds = _table.GetPartialSeconds(_recipe);

            // **남은 만큼만** 잰다. 매 틱 누적하지 않는 이유는 BT가 `tickInterval`마다 돌아
            // 프레임 델타를 더하면 시간이 6배 이상 느려지기 때문이다(알려진 이슈).
            // **남은 표준 초를 작업대 속도로 나눈다** — 개량 작업대는 그만큼 일찍 끝난다.
            _craftEndTime = Time.time
                          + Mathf.Max(0.01f, (_recipe.CraftTime - _carriedSeconds) / BenchSpeed);
            _table.SetActiveJob(this);   // 이때부터 패널 게이지가 이 잡을 읽는다
        }
        if (Time.time < _craftEndTime) return;

        float skill    = unit.Stats.GetCraftSkill(_recipe.RequiredSkillType);
        float required = _recipe.RequiredSkillLevel;
        bool  skilled  = skill >= required;

        // ── 장비(무기/방어구)는 등급 판정, 나머지는 수량 배율 ─────────────────
        bool isEquip = IsEquipment;

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

        // ── 산출물 입고 ───────────────────────────────────────────────────────
        // **작업대 산출물 칸이 먼저다.** 폰 인벤토리에 바로 넣으면 그 폰이 완성품을 들고
        // 돌아다니게 되고(다음 일감을 하러 가 버린다), 넘치는 만큼은 바닥에 떨어져
        // 어디로 갔는지 알 수 없다. 산출물은 작업대에서 기다리다 운반으로 창고에 간다.
        int toTable  = _table.Inventory != null ? _table.Inventory.TryAdd(_recipe.Result, finalAmount, quality) : 0;
        int leftover = finalAmount - toTable;

        // 산출물 칸이 꽉 찼을 때만 폰이 받고, 그마저 안 되면 바닥에 떨군다(증발 방지).
        int toUnit = 0;
        if (leftover > 0)
        {
            toUnit    = unit.Inventory.TryAdd(_recipe.Result, leftover, quality);
            leftover -= toUnit;
        }
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
        var parts = new System.Collections.Generic.List<string>(3);
        if (toTable  > 0) parts.Add($"산출물 {toTable}");
        if (toUnit   > 0) parts.Add($"인벤토리 {toUnit}");
        if (leftover > 0) parts.Add($"드롭 {leftover}");
        string dest = string.Join(" + ", parts);
        string qualLabel = isEquip ? $" [{quality.DisplayName()}]" : "";
        string penalty   = !skilled ? " (스킬 미달 -50%)" : "";
        Debug.Log($"[CraftJob] 제작 완료 — {_recipe.RecipeName}{qualLabel} ×{finalAmount}{penalty} → {dest}  ({_recipe.RequiredSkillType} {skill:F0})");
        _table.ClearActiveJob(this);
        _table.ClearPartial(_recipe);   // 완성됐으니 부분 진행은 지운다
        _table.OrderFinished(_recipe);
        Status = JobStatus.Done;
    }

    // 완성 전 취소(유닛 사망/재배정/연료 부재 등) 시 차감한 재료를 작업대에 드롭 환불.
    // 운반 작업이 창고로 회수 → 재료 손실 방지.
    protected override void OnCancelled()
    {
        _table?.ClearActiveJob(this);   // 게이지가 완료되지 않은 채 남아 있지 않게
        _table?.OrderFinished(_recipe);        // 아래 QueueOrder가 다시 +1 한다 → 합계는 유지
        if (_recipe == null) return;

        // **만들던 것을 버리지 않는다.** 폰이 밥을 먹으러 가거나 죽거나 이동 명령을 받아도
        // 진행이 남는다 — 그러지 않으면 긴 제작은 한 번도 안 끊길 수가 없어 영영 못 끝낸다.
        // 장비는 `Owner`를 함께 저장해 **만들던 폰만 이어받게** 한다(등급이 그 폰의 솜씨이므로).
        float done = _carriedSeconds + WorkedThisSession;
        if (done > 0f && _table != null)
            _table.SavePartial(_recipe, done, IsEquipment ? _crafter : null);

        // 진행이 있었으면 재료는 돌려주지 않는다 — 이미 물건에 들어갔고,
        // 돌려주면서 진행까지 남기면 중단·재개로 재료를 복사할 수 있다(연구와 같은 정책).
        if (_consumed && done <= 0f)
        {
            _consumed = false;
            Vector3 pos = _table != null ? _table.transform.position : Vector3.zero;
            // 창고로 되돌린다 — 바닥에 떨구면 그것이 다시 운반 일감이 되고, 나르는 동안
            // 창고 집계에서 빠져 있어 같은 주문이 "재료 부족"으로 다시 죽는다(연구와 같은 정책).
            foreach (var ing in _recipe.Ingredients)
                MaterialPool.Refund(ing.Item, ing.Amount, pos + Vector3.up * 0.3f);
        }

        // 주문 자체는 잃지 않는다 — 큐에 되돌려 다른 폰(또는 나중에 이 폰)이 이어받게 한다.
        _table?.QueueOrder(_recipe);
    }
}
