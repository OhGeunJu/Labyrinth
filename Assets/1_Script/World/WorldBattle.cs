using System.Collections.Generic;
using UnityEngine;

// 지도 위에서 벌어진 전투 하나의 결과.
public class WorldBattleResult
{
    public bool PlayerWon;

    public int BandLosses;            // 무리가 잃은 인원
    public readonly List<UnitBrain> Down    = new();   // 크게 당한 원정대원
    public readonly List<UnitBrain> Wounded = new();   // 긁힌 원정대원

    // 진 쪽이 빼앗긴 등짐. 이겼으면 전리품, 졌으면 잃은 것.
    public readonly Dictionary<ItemData, int> Loot = new();

    public string Summary;
}

// **지도 위의 자동 전투.**
//
// 왜 자동인가: 필드맵을 그 자리에 만드는 축이 아직 없다("직접 건설"이 막혀 있는 것과 같은
// 이유). 다만 자동 전투는 임시 방편이 아니라 **마운트앤블레이드에도 있는 정식 선택지**다 —
// 나중에 [직접 지휘한다] 한 줄이 조우 메뉴에 늘 뿐, 이 판정이 사라지지는 않는다.
//
// **전력은 스탯에서 나온다.** 51개를 다 세지 않는다 — 요리 실력이 전투 결과를 바꾸면
// 무엇을 키워야 하는지가 흐려진다. 공격·방어·강인함·회피 넷만 본다.
//
// **장비는 양쪽 다 세되 표현이 다르다.** 원정대는 실제로 무기를 들고 있으므로 그것을 읽고,
// 무리는 실제 장비가 없으므로 `WorldBandData.PowerMultiplier`가 그 자리를 대신한다.
// 한쪽만 세면 무장한 쪽이 늘 이겨서 판정이 굴림이 아니라 결론이 된다.
public static class WorldBattle
{
    // ── 전력 ──────────────────────────────────────────────────────────────────

    // 사람 한 명 몫. 스탯은 0~130이므로 결과도 대략 그 범위다.
    static float Power(System.Func<StatName, float> stat)
    {
        // 원거리형도 제 몫을 하게 공격은 **둘 중 큰 쪽**을 쓴다 — 궁수의 근접공격이 낮다고
        // 전력이 0에 가까워지면 활을 든 폰을 데려갈 이유가 없어진다.
        float atk = Mathf.Max(stat(StatName.MeleeAttack), stat(StatName.PrecisionShooting));
        float def = (stat(StatName.MeleeDefense) + stat(StatName.RangedDefense)) * 0.5f;

        return atk * 0.45f + def * 0.30f + stat(StatName.Toughness) * 0.15f
             + stat(StatName.Evasion) * 0.10f;
    }

    // 무리 멤버 — 명세의 **등급**에서 값을 낸다(실제 폰이 없으므로).
    public static float PowerOf(BandMember m)
    {
        if (m == null || m.Spec == null) return 0f;
        return Power(s => m.Spec.Grades.TryGetValue(s, out var g) ? g.ToValue() : 1f);
    }

    // 원정대원 — 실제 스탯 + **실제로 들고 있는 장비**.
    public static float PowerOf(UnitBrain u)
    {
        if (u == null || u.Stats == null) return 0f;

        // 쓰러져 있는 사람은 싸우지 못한다. 데려는 왔어도 전력에 세면 안 된다.
        if (u.Blackboard != null && u.Blackboard.IsKnockedOut) return 0f;

        float p = Power(u.Stats.GetStat);

        var eq = u.Equipment;
        if (eq != null)
        {
            // 무기의 초당 피해와 갑옷의 감소량을 스탯과 같은 눈금으로 환산한다.
            // 정밀한 환산이 아니라 **비교 가능한 크기**가 목적이다 — 맨손과 철검의 차이가
            // 판정에 나타나기만 하면 된다.
            if (eq.MainHand != null)
                p += eq.MainHand.BaseDamage / Mathf.Max(0.1f, eq.MainHand.BaseAttackInterval) * 6f;

            foreach (ArmorSlot slot in System.Enum.GetValues(typeof(ArmorSlot)))
            {
                var armor = eq.GetArmor(slot);
                if (armor != null) p += armor.DamageReduction * eq.GetArmorQualityMult(slot) * 1.5f;
            }
        }

        return p;
    }

    public static float PowerOf(IEnumerable<UnitBrain> members)
    {
        float sum = 0f;
        if (members != null) foreach (var m in members) sum += PowerOf(m);
        return sum;
    }

    // ── 판정 ──────────────────────────────────────────────────────────────────

    // 원정대 ↔ 무리. **결과를 계산만 하지 않고 그 자리에서 적용한다** — 피해·손실·약탈이
    // 따로 놀면 호출자마다 빠뜨리는 것이 생긴다(조우에서 한 것과 소굴 습격에서 한 것이 달라진다).
    public static WorldBattleResult Resolve(ExpeditionParty party, WorldBand band)
    {
        var result = new WorldBattleResult();
        if (party == null || band == null) { result.Summary = "싸울 상대가 없다."; return result; }

        float mine   = PowerOf(party.Members);
        float theirs = band.Power;

        // **흔들림은 곱셈이다.** 덧셈으로 흔들면 전력이 클 때는 아무 영향이 없고 작을 때만
        // 판이 뒤집혀서, 강한 무리를 상대할수록 운이 사라진다.
        float roll  = Random.Range(0.80f, 1.25f);
        float ratio = mine <= 0f ? 0f : (mine * roll) / (mine * roll + Mathf.Max(1f, theirs));

        result.PlayerWon = ratio >= 0.5f;

        // 진 쪽은 크게 잃고, 이긴 쪽은 **상대가 셌던 만큼만** 잃는다.
        // 이겨도 공짜가 아니어야 "이길 수는 있지만 붙을 만한가"가 판단거리가 된다.
        float myLoss, theirLoss;
        if (result.PlayerWon)
        {
            myLoss    = Mathf.Clamp(theirs / Mathf.Max(1f, mine) * 0.35f, 0f, 0.6f);
            theirLoss = Random.Range(0.6f, 1f);
        }
        else
        {
            myLoss    = Random.Range(0.55f, 0.9f);
            theirLoss = Mathf.Clamp(mine / Mathf.Max(1f, theirs) * 0.35f, 0f, 0.6f);
        }

        ApplyToBand(band, theirLoss, result);
        ApplyToParty(party, myLoss, result);

        // 진 쪽이 턴다. 이겼으면 쓰러뜨린 수만큼 노획하고, 졌으면 등짐을 빼앗긴다.
        if (result.PlayerWon) LootFromBand(party, band, result);
        else                  LootFromParty(party, result);

        result.Summary = BuildSummary(band, result);
        return result;
    }

    // 무리끼리 붙었다 — 순찰대와 약탈대가 길에서 마주친 경우.
    //
    // **알림을 띄우지 않는다.** 지도 어디선가 남들끼리 싸울 때마다 창이 뜨면, 정작 내 원정대에
    // 관한 알림이 그 사이에 묻힌다. 결과는 지도에서 보인다 — 토큰이 사라지거나 작아진다.
    // 오가는 것은 인원뿐이라 원정대 판정(피해·약탈)보다 훨씬 단순하다.
    public static void ResolveBands(WorldBand a, WorldBand b)
    {
        if (a == null || b == null) return;

        float pa = a.Power, pb = b.Power;
        float roll = Random.Range(0.8f, 1.25f);
        bool aWon = pa * roll >= pb;

        var  winner      = aWon ? a  : b;
        var  loser       = aWon ? b  : a;
        // **손실은 싸우기 전의 전력으로 잰다.** 진 쪽을 먼저 덜어낸 뒤 그 전력으로 이긴 쪽
        // 손실을 계산하면, 상대를 크게 이길수록 내 피해도 줄어드는 이중 보너스가 된다.
        float winnerPower = aWon ? pa : pb;
        float loserPower  = aWon ? pb : pa;

        loser.TakeLosses(Mathf.Max(1, Mathf.RoundToInt(loser.Count * Random.Range(0.7f, 1f))));
        winner.TakeLosses(Mathf.RoundToInt(winner.Count
            * Mathf.Clamp(loserPower / Mathf.Max(1f, winnerPower) * 0.35f, 0f, 0.5f)));
    }

    static void ApplyToBand(WorldBand band, float lossRate, WorldBattleResult result)
    {
        int lose = Mathf.RoundToInt(band.Count * lossRate);
        // 이겼는데 상대가 멀쩡하면 싸운 것 같지가 않다 — 최소 한 명은 쓰러진다.
        if (result.PlayerWon) lose = Mathf.Max(1, lose);
        result.BandLosses = band.TakeLosses(lose);
    }

    // 원정대 쪽 손실은 **인원 삭제가 아니라 피해**다. 기절할지 죽을지는 `BodyPartSystem`이
    // 정한다 — 갑옷·강인함이 지도 위에서도 제 몫을 해야 필드에서 갖춘 것이 뜻을 갖는다.
    static void ApplyToParty(ExpeditionParty party, float lossRate, WorldBattleResult result)
    {
        var alive = new List<UnitBrain>();
        foreach (var m in party.Members)
            if (m != null && !(m.Blackboard != null && m.Blackboard.IsKnockedOut)) alive.Add(m);
        if (alive.Count == 0) return;

        // **약한 사람부터 크게 당한다.** 무리 쪽(`WorldBand.TakeLosses`)과 같은 규칙이다 —
        // 누가 앞에 서느냐를 무작위로 두면 강한 폰을 데려갈 이유가 흐려진다.
        alive.Sort((a, b) => PowerOf(a).CompareTo(PowerOf(b)));

        int down = Mathf.Clamp(Mathf.RoundToInt(alive.Count * lossRate), 0, alive.Count);

        for (int i = 0; i < alive.Count; i++)
        {
            var u    = alive[i];
            var body = u.GetComponent<BodyPartSystem>();
            if (body == null) continue;

            if (i < down)
            {
                // 여러 번 나눠 때린다 — 한 방에 몰면 부위 하나만 상하고 갑옷도 한 번만 일한다.
                int hits = Random.Range(5, 9);
                for (int h = 0; h < hits; h++) body.TakeDamage(Random.Range(10f, 20f));
                result.Down.Add(u);
            }
            else
            {
                body.TakeDamage(Random.Range(3f, 9f));
                result.Wounded.Add(u);
            }
        }
    }

    // **항복.** 싸우지 않고 등짐을 통째로 내주고 풀려난다.
    //
    // 싸움의 패배와 다른 점이 요점이다: 패배는 **등짐의 절반쯤**을 잃고 사람이 크게 다치지만,
    // 항복은 **등짐 전부**를 잃는 대신 **아무도 안 다친다.** 그래서 "무엇을 지키느냐"가 갈린다 —
    // 짐이 가벼우면 항복이 싸고, 한몫 챙겨 돌아가는 길이면 차라리 싸운다.
    //
    // **빈손이면 항복이 공짜다**(가져갈 것이 없어 그냥 놓아준다). 그건 예외가 아니라 결과다 —
    // 아무것도 안 지고 다니는 원정은 애초에 아무것도 못 가져오므로, 그 자체가 이미 대가다.
    public static WorldBattleResult Surrender(ExpeditionParty party, WorldBand band)
    {
        var result = new WorldBattleResult { PlayerWon = false };
        if (party == null || band == null) { result.Summary = "항복할 상대가 없다."; return result; }

        foreach (var kv in party.Cargo)
            if (kv.Key != null && kv.Value > 0) result.Loot[kv.Key] = kv.Value;
        party.ClearCargo();

        var sb = new System.Text.StringBuilder();
        if (result.Loot.Count == 0)
        {
            sb.Append($"{band.Name}에게 항복했다. 가져갈 것이 없어 그냥 놓아주었다.");
        }
        else
        {
            sb.Append($"{band.Name}에게 항복하고 짐을 내주었다. 다친 사람은 없다.\n빼앗긴 것 — ");
            bool first = true;
            foreach (var kv in result.Loot)
            {
                if (!first) sb.Append(" · ");
                sb.Append($"{kv.Key.ItemName} {kv.Value}");
                first = false;
            }
        }

        result.Summary = sb.ToString();
        return result;
    }

    // 이긴 쪽이 노획한다 — **쓰러뜨린 수에 비례**한다. 무리 전체를 잡든 절반만 잡고 놓치든
    // 같은 양이 나오면, 끝까지 싸울 이유가 없어진다.
    //
    // **등짐 한도를 그대로 지킨다**(`ExpeditionParty.TryAdd`). 못 실은 것은 조용히 사라진다 —
    // 지도 위에는 떨어뜨려 둘 바닥이 없고, 무게를 넘겨 받아 주면 적재 한도가 뜻을 잃는다.
    static void LootFromBand(ExpeditionParty party, WorldBand band, WorldBattleResult result)
    {
        int kills = result.BandLosses;
        if (kills <= 0 || band.Data == null) return;

        foreach (var e in band.Data.Loot)
        {
            if (e == null || e.Item == null) continue;

            int total = 0;
            for (int k = 0; k < kills; k++)
            {
                if (Random.value > e.Chance) continue;
                total += Random.Range(Mathf.Min(e.MinPerKill, e.MaxPerKill),
                                      Mathf.Max(e.MinPerKill, e.MaxPerKill) + 1);
            }
            if (total <= 0) continue;

            int got = party.TryAdd(e.Item, total);
            if (got <= 0) continue;

            result.Loot.TryGetValue(e.Item, out int cur);
            result.Loot[e.Item] = cur + got;
        }

        // 상단은 **들고 다니던 것**도 나온다. 진열 재고가 곧 짐이므로, 털면 그만큼 준다 —
        // 다음에 만났을 때 멀쩡한 진열대를 보면 턴 것이 아무 일도 아니게 된다.
        if (band.Shop == null) return;

        foreach (var entry in band.Shop.Stock)
        {
            if (entry == null || entry.Item == null || entry.Stock <= 0) continue;

            int take = Mathf.Max(1, Mathf.RoundToInt(entry.Stock * Random.Range(0.4f, 0.8f)));
            take = Mathf.Min(take, entry.Stock);

            int got = party.TryAdd(entry.Item, take);
            if (got <= 0) continue;

            entry.Stock -= got;
            result.Loot.TryGetValue(entry.Item, out int cur);
            result.Loot[entry.Item] = cur + got;
        }
    }

    // 진 원정대는 **등짐을 통째로** 빼앗긴다. 붙잡히는 마당에 짐만 지키고 있을 수는 없다.
    //
    // 그래서 항복과의 차이가 짐이 아니라 **사람**에서 난다: 항복은 같은 짐을 내주고 아무도
    // 안 다치고 풀려나지만, 싸워서 지면 크게 다치고 **포로가 되어 끌려간다.**
    // 전에는 절반쯤만 가져갔는데, 그러면 항복이 늘 손해라서 고를 이유가 없었다.
    static void LootFromParty(ExpeditionParty party, WorldBattleResult result)
    {
        foreach (var kv in party.Cargo)
            if (kv.Key != null && kv.Value > 0) result.Loot[kv.Key] = kv.Value;
        party.ClearCargo();
    }

    static string BuildSummary(WorldBand band, WorldBattleResult r)
    {
        var sb = new System.Text.StringBuilder();

        sb.Append(r.PlayerWon
            ? $"{band.Name}을(를) 물리쳤다."
            : $"{band.Name}에게 밀렸다.");

        if (r.BandLosses > 0) sb.Append($" 적 {r.BandLosses}명이 쓰러졌다.");
        if (band.IsAlive)     sb.Append($" 남은 적 {band.Count}명.");

        if (!r.PlayerWon)
            sb.Append("\n<color=#FF8080>살아남은 이들은 붙잡혀 끌려갔다.</color>");

        if (r.Down.Count > 0)
        {
            sb.Append($"\n우리 쪽 {r.Down.Count}명이 크게 다쳤다 — ");
            for (int i = 0; i < r.Down.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(r.Down[i].UnitName);
            }
        }

        if (r.Loot.Count > 0)
        {
            sb.Append(r.PlayerWon ? "\n노획 — " : "\n빼앗긴 것 — ");
            bool first = true;
            foreach (var kv in r.Loot)
            {
                if (!first) sb.Append(" · ");
                sb.Append($"{kv.Key.ItemName} {kv.Value}");
                first = false;
            }
        }

        return sb.ToString();
    }
}
