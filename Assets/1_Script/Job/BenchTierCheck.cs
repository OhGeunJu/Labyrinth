using System.Collections.Generic;
using System.Text;
using UnityEngine;

// **작업대 등급의 재료가 아직 맞는 층에서 나나 — 그리고 그 등급에 닿을 수 있나**
// (2026-09-30, 사용자: *"문제는 이제 층마다 얻을 수 있는 물건이 정해져있지 않다는거지"*).
//
// `BenchTier.CostTo`는 아이템을 적는다 — 올릴 때 *무엇을 가져와야 하나*가 화면에 딱 적혀야
// 하기 때문이다. 그런데 **무엇이 몇 층에 나나는 저작이 정하고 바뀐다.** 표를 층에 묶어 두면
// 층을 고치는 날 표가 조용히 늙는다: 3층 재료였던 것이 1층에서도 나기 시작하면 *한 층
// 내려갔다 왔다*는 뜻이 사라지는데, 화면에는 아무 표시도 안 난다.
//
// 그래서 **표는 그대로 두고 게임이 대조한다** — Play를 시작할 때 바이옴 저작에서 *"이 물건이
// 처음 나는 층"*을 계산해 `BenchTier.FloorFor`와 어긋나면 경고한다. 표를 고칠지 층을 고칠지는
// 사람이 정한다.
//
// ## 무엇을 재나
//
// - **층** — 등급 N의 재료 중 **적어도 하나는 N층에서 처음 나야 한다**(그게 문이다) ·
//   **N층보다 깊은 것은 없어야 한다**(IV 재료가 5층에 있으면 IV가 V 뒤에 온다).
// - **순환** — 등급 N으로 올리는 재료를 **연구대 N-1까지로 만들 수 있어야 한다**. 재료의
//   레시피를 여는 연구가 N에서만 보이면 영영 못 올린다(`ResearchTierCreator` 머리말).
//   모든 작업대가 같이 N-1이라고 친다 — 작업대마다 등급이 따로지만, *가장 느린 길*이 아니라
//   *닿을 수 있는가*를 묻는 검사라 그것으로 충분하다.
//
// ## 층은 어디서 오나
//
// **벽 광맥**(`BiomeContentData.ResourceNodes`) · **묻힌 자원**(`Deposits`) · **바닥에 뿌리는
// 것**(`ScatterRule` — 씬이 들므로 Play에서만 보인다: 그래서 컴파일 때가 아니라 여기서 잰다).
// 만드는 물건은 **재료 중 가장 깊은 층**이고, 길이 여럿이면 **가장 얕은 길**을 친다.
// 바이옴의 층은 `BiomeContentData.Tier`(`BiomeTier.Of`와 같은 값)다.
//
// **빠진 출처** — 짐승 도축·상인. 그쪽으로 강철이 1층에서 팔리기 시작하면 이 검사는 못 본다.
public static class BenchTierCheck
{
    const int Unreachable = int.MaxValue;

    public static void Run(IEnumerable<ScatterRule> scatter)
    {
        var floor = FloorOfItems(scatter);
        var bench = BenchOfItems();

        var sb = new StringBuilder();
        foreach (var (tier, rows) in BenchTier.CostTo)
        {
            int want = BenchTier.FloorFor(tier);
            bool hasGate = false;

            foreach (var (id, _) in rows)
            {
                var item = DataManager.GetItem(id);
                if (item == null) continue;   // `BenchUpgrade.CostFor`가 따로 경고한다

                int f = floor.TryGetValue(item, out var v) ? v : Unreachable;
                if (f == want) hasGate = true;

                if (f == Unreachable)
                    sb.AppendLine($"- {BenchTier.Roman(tier)}: {item.ItemName} — 어느 층에서도 안 난다");
                else if (f > want)
                    sb.AppendLine($"- {BenchTier.Roman(tier)}: {item.ItemName} — {f}층에서 처음 난다(이 등급은 {want}층)");

                int b = bench.TryGetValue(item, out var bv) ? bv : Unreachable;
                if (b >= tier)
                    sb.AppendLine($"- {BenchTier.Roman(tier)}: {item.ItemName} — "
                                + (b == Unreachable ? "만드는 길이 없다"
                                                    : $"연구대 {BenchTier.Roman(b)}가 있어야 만든다(순환)"));
            }

            if (!hasGate)
                sb.AppendLine($"- {BenchTier.Roman(tier)}: {want}층에서 처음 나는 재료가 하나도 없다 — "
                            + "올리는 데 그 층에 갈 이유가 없다");
        }

        if (sb.Length > 0)
            Debug.LogWarning("[작업대 등급] 올리는 재료가 층 구성과 어긋납니다 — "
                           + "`BenchTier.CostTo`를 고치거나 층 저작을 확인하세요.\n" + sb);
    }

    // ── 물건이 처음 나는 층 ──────────────────────────────────────────────────

    static Dictionary<ItemData, int> FloorOfItems(IEnumerable<ScatterRule> scatter)
    {
        var floor = new Dictionary<ItemData, int>();

        void Offer(ItemData item, int f)
        {
            if (item == null) return;
            if (!floor.TryGetValue(item, out var have) || f < have) floor[item] = f;
        }

        foreach (var biome in BiomeContent.All)
        {
            if (biome == null) continue;
            int f = biome.Tier;

            foreach (var node in biome.ResourceNodes)
                if (node != null) Offer(node.DropItem, f);

            foreach (var d in biome.Deposits)
                if (d != null) Offer(d.Item, f);
        }

        if (scatter != null)
            foreach (var rule in scatter)
            {
                var item = ScatterItem(rule);
                if (item == null) continue;

                // **바이옴을 비운 규칙은 어디에나 난다** — 1층에도 난다.
                if (rule.Biomes == null || rule.Biomes.Length == 0) { Offer(item, 1); continue; }
                foreach (var b in rule.Biomes)
                {
                    var data = BiomeContent.Get(b);
                    if (data != null) Offer(item, data.Tier);
                }
            }

        // 만드는 물건 — 재료가 가장 깊은 층. 사슬이 몇 단이든 닿도록 바뀌지 않을 때까지 돈다.
        Relax(floor, (recipe, ingredientMax) => ingredientMax);
        return floor;
    }

    static ItemData ScatterItem(ScatterRule rule)
    {
        if (rule == null || rule.Prefab == null) return null;
        var node = rule.Prefab.GetComponent<ResourceNode>();
        if (node != null && node.Data != null) return node.Data.DropItem;
        var forage = rule.Prefab.GetComponent<Forageable>();
        return forage != null ? forage.Item : null;
    }

    // ── 물건을 만들려면 연구대가 몇 등급이어야 하나 ─────────────────────────
    //
    // 캐는 것은 0(연구가 필요 없다). 만드는 것은 **레시피를 여는 연구가 보이는 등급**과
    // 재료의 등급 중 큰 것이다.
    static Dictionary<ItemData, int> BenchOfItems()
    {
        var bench = new Dictionary<ItemData, int>();

        // 레시피의 결과가 아닌 것은 캐거나 줍는 것이다.
        var crafted = new HashSet<ItemData>();
        foreach (var r in DataManager.AllRecipes.Values)
            if (r != null && r.Result != null) crafted.Add(r.Result);
        foreach (var item in DataManager.AllItems.Values)
            if (item != null && !crafted.Contains(item)) bench[item] = 0;

        // **메모는 부를 때마다 새로** — 연구에 드는 재료가 앞 바퀴에서 아직 닿지 않았으면
        // 그 *닿지 않음*이 메모에 굳어 버린다.
        Relax(bench, (recipe, ingredientMax) => Mathf.Max(ingredientMax,
                                                          recipe.RequiredBenchTier,
                                                          ResearchTier(recipe.RequiredResearch, bench,
                                                                       new Dictionary<ResearchData, int>())));
        return bench;
    }

    // 연구가 보이는 등급 — 제 등급 · 선행 연구 · 연구에 드는 재료 중 가장 높은 것.
    static int ResearchTier(ResearchData r, Dictionary<ItemData, int> bench,
                            Dictionary<ResearchData, int> memo)
    {
        if (r == null) return 0;
        if (memo.TryGetValue(r, out var have)) return have;

        memo[r] = Unreachable;   // 선행이 돌아서 저를 가리키면 닿을 수 없다
        int t = r.RequiredBenchTier;
        foreach (var pre in r.Prerequisites)
            t = Mathf.Max(t, ResearchTier(pre, bench, memo));
        foreach (var ing in r.Ingredients)
        {
            var item = ing.Resolve();
            if (item == null) continue;
            t = Mathf.Max(t, bench.TryGetValue(item, out var b) ? b : Unreachable);
        }

        memo[r] = t;
        return t;
    }

    // **레시피를 따라 값을 퍼뜨린다** — 결과 = min(지금 값, combine(레시피, 재료 중 최대)).
    // 재료가 하나라도 닿지 않으면 그 레시피는 길이 아니다.
    static void Relax(Dictionary<ItemData, int> value,
                      System.Func<RecipeData, int, int> combine)
    {
        for (int pass = 0; pass < 32; pass++)
        {
            bool changed = false;
            foreach (var r in DataManager.AllRecipes.Values)
            {
                if (r == null || r.Result == null) continue;

                int worst = 0;
                foreach (var ing in r.Ingredients)
                {
                    if (ing.Item == null) continue;
                    worst = Mathf.Max(worst, value.TryGetValue(ing.Item, out var v) ? v : Unreachable);
                }
                if (worst == Unreachable) continue;

                int got = combine(r, worst);
                if (got == Unreachable) continue;
                if (!value.TryGetValue(r.Result, out var cur) || got < cur)
                {
                    value[r.Result] = got;
                    changed = true;
                }
            }
            if (!changed) return;
        }
    }
}
