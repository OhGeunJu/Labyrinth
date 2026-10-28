using UnityEngine;

// **붙잡은 사람을 우리 편으로 돌린다.**
//
// 길이 둘인데 **게이지는 하나**다(`UnitBlackboard.ConvertProgress`). 무엇이 되는지는
// **종족이 정한다** — 하수인 종족이면 하수인, 사람 종족이면 방랑자(2026-08-12 결정).
//
// ── 왜 길이 둘인가 ─────────────────────────────────────────────────────────
//
// **사람은 사람이 설득한다**(`PersuadeJob`). 폰이 가서 말을 붙이고, 회차마다 게이지가 오른다.
// 아무도 안 가면 아무 일도 안 일어난다 — 사람을 얻는 일이라 사람을 써야 한다.
// 다 차면 **그대로 합류**한다: 설득은 쌓아 온 것이 곧 결과다.
//
// **하수인 종족은 던전 핵이 지배한다.** 폰이 갈 필요가 없다 — 핵 영역 안 우리에 가둬 두면
// 시간이 지나며 저절로 찬다. 애초에 핵이 하수인을 **소환하는** 물건이므로, 같은 종족을
// 붙들어 두면 그 아래로 들어오는 것이 자연스럽다.
//
// **다만 하수인 쪽은 게이지가 곧 결과가 아니다.** 다 차면 **한 번 시도할 수 있게** 되고,
// 성공하면 하수인이 되지만 **실패하면 머리가 버티지 못해 죽는다**. 그래서 둘의 값이 다르다 —
// 설득은 "공들이면 얻는다"이고 지배는 **도박**이다: 사람은 시간과 일손을 걸고, 하수인은
// 우리 자리와 목숨을 건다.
public static class Conversion
{
    // 게이지가 이만큼 차면 넘어온다. 0~1이라 UI가 그대로 %로 쓴다.
    public const float Complete = 1f;

    // ── 핵의 지배 (하수인 종족) ──────────────────────────────────────────
    //
    // **게이지는 "준비"지 "설득"이 아니다.** 다 차면 저절로 넘어오는 것이 아니라, 그때
    // **한 번 시도할 수 있게** 된다 — 성공하면 하수인이 되고 **실패하면 뇌가 터져 죽는다**.
    // 그래서 가둬 두는 시간은 안전하고, 위험은 **누르는 순간 한 번**에 몰린다.
    //
    // 확률이 여기 있는 것은 앞서 "확률로 굴리지 말자"고 한 것과 어긋나지 않는다:
    // 그때 막은 것은 **플레이어 모르게** 굴리는 것이었다. 이건 확률을 보여 주고 누를지
    // 말지를 사람이 정한다 — 굴림이 곧 결정이다.
    // 다 차기까지. 누구든 같다.
    //
    // **하루다 — 4분이 아니었다.** 설득이 이틀 남짓인데(아래) 핵이 4분이면, 하수인 종족을
    // 잡아 오는 것이 사실상 즉시였다. 사람보다 빠른 것은 맞다(폰이 갈 필요가 없다) —
    // 대신 다 차도 **도박 한 번**이 남고 실패하면 죽는다. 그 값이 시간 차이를 갚는다.
    const float CoreSeconds = 1800f;

    // ── 회유는 **우리에서만** 한다 ────────────────────────────────────────
    //
    // 죄수 침대에서도 되게 했더니 **두 건물이 같은 물건**이 됐다 — 침대가 회복까지 얹어 주니
    // 우리를 지을 이유가 사라진다. 갈라 두면 둘 다 뜻이 생긴다:
    //
    //   **우리 = 돌려세우는 자리 / 죄수 침대 = 살려 두는 자리.**
    //
    // 그래서 다친 포로는 침대에 눕혀 낫게 한 **다음 우리로 옮기는** 걸음이 생긴다.
    // (옮기는 길은 이미 있다 — 우클릭 [끌어낸다].)
    static bool InCage(UnitBrain brain) => PrisonCage.HolderOf(brain) != null;

    // 이 폰을 핵이 지배할 수 있나 — **하수인 종족 · 우리에 갇힘 · 핵 영역 안**.
    public static bool CoreCanDominate(UnitBrain brain)
    {
        if (brain == null || brain.Blackboard == null) return false;
        if (!brain.Blackboard.IsPrisoner) return false;
        if (!(brain.Data is MinionData)) return false;
        if (!InCage(brain)) return false;

        var core = DungeonCore.Instance;
        return core != null && core.IsWithinAnyRangeWorld(brain.transform.position);
    }

    // **왜 안 차는지 한 줄로 답한다.** 게이지가 멈춰 있는데 이유가 화면에 없으면
    // "고장인가"가 된다 — 자리가 없어서 멈춘 것과 영역 밖이라 안 도는 것은 할 일이 다르다.
    public static string CoreBlockedReason(UnitBrain brain)
    {
        if (brain == null || brain.Blackboard == null || !brain.Blackboard.IsPrisoner) return null;
        if (!(brain.Data is MinionData)) return null;
        if (!InCage(brain)) return "우리에 가둬야 지배할 수 있다";

        var core = DungeonCore.Instance;
        if (core == null) return "던전 핵이 없다";
        if (!core.IsWithinAnyRangeWorld(brain.transform.position)) return "핵 영역 밖이라 지배가 닿지 않는다";
        if (core.CurrentMinionCount >= core.TotalMinionCap)
            return $"하수인 자리가 없다 ({core.CurrentMinionCount}/{core.TotalMinionCap})";
        return null;
    }

    // 핵이 조금씩 채운다. `DungeonCore.Update`가 갇힌 폰들을 돌며 부른다.
    // **다 차면 거기서 멈춘다** — 넘어오는 것은 사람이 누를 때다(`TryDominate`).
    public static void TickCore(UnitBrain brain, float deltaTime)
    {
        if (!CoreCanDominate(brain)) return;
        if (brain.Blackboard.ConvertProgress >= Complete) return;

        // **자리가 없으면 멈춘다.** 다 채워 놓고 자리가 없어 못 받으면 그 시간이 통째로
        // 버려진다 — 멈춰 두면 자리를 비우는 순간 이어서 찬다.
        var core = DungeonCore.Instance;
        if (core.CurrentMinionCount >= core.TotalMinionCap) return;

        brain.Blackboard.ConvertProgress =
            Mathf.Min(Complete, brain.Blackboard.ConvertProgress + deltaTime / CoreSeconds);
    }

    // 지금 지배를 시도할 수 있나 — 게이지가 다 찼고, 조건이 그대로일 때.
    public static bool CanDominateNow(UnitBrain brain) =>
        CoreCanDominate(brain)
        && brain.Blackboard.ConvertProgress >= Complete
        && CoreBlockedReason(brain) == null;

    // 성공 확률. **핵이 정한다** — 지배하는 쪽의 힘이지 붙잡힌 쪽의 성질이 아니다.
    // (등급·서브코어로 올리고 싶어지면 여기 한 곳만 고치면 된다.)
    public static float DominateChance()
    {
        var core = DungeonCore.Instance;
        return core != null && core.Data != null
            ? Mathf.Clamp01(core.Data.DominateChance) : 0.7f;
    }

    // **한 번의 굴림.** 성공하면 하수인, 실패하면 죽는다.
    //
    // 실패했다고 게이지를 남겨 두지 않는다 — 죽었으므로 남길 것이 없다. 그게 이 도박의
    // 값이다: 붙잡아 온 시간과 우리 자리를 걸고 하수인 하나를 얻는다.
    public static void TryDominate(UnitBrain brain)
    {
        if (!CanDominateNow(brain)) return;

        if (Random.value <= DominateChance()) { Join(brain); return; }

        EventNotificationUI.Notify("지배 실패",
            $"{brain.UnitName}의 머리가 버티지 못했습니다.", holdWhenHidden: true);

        // `Die`가 우리 자리와 갇힘까지 정리한다(`HolderOf(this)?.Release()`).
        brain.Blackboard.ConvertProgress = 0f;
        brain.Die();
    }

    // ── 설득 (사람 종족) ─────────────────────────────────────────────────
    //
    // **한 회차 = 말을 붙이는 시간 + 굴림 + 쉬는 시간.** 셋을 다 두는 이유가 각각 있다.

    // 회차 **사이**의 쉬는 시간(초). `PersuadeSeconds`(말하는 시간)와 다른 축이다 —
    // 그쪽만 있으면 설득이 **쉼 없이 이어지는 한 줄**이라, 게이지가 차는 동안 그 폰은
    // 다른 아무 일도 못 한다(간수를 켜 둔 폰 하나가 우리 앞에 붙박인다).
    //
    // 쿨타임이 있으면 회차 사이에 다른 일감이 끼어들 수 있고, **결과가 회차 단위로 읽힌다** —
    // 성공·실패가 뜨고 잠깐 쉬었다 다시 온다.
    // **3분이다 — 20초가 아니다.** 20초일 때는 간수 하나가 우리 앞을 거의 떠나지 않았고,
    // 무엇보다 **회유 한 판이 몇 분 만에 끝나서** 붙잡아 두는 일에 아무 무게가 없었다.
    // 3분이면 하루(1800초)에 열 번 남짓 — 간수가 다른 일을 하다 들르는 그림이 된다.
    //
    // 이 값이 **회유 전체 길이의 축**이다(회차 수 × 이 값 ≈ 이틀). 바꾸면 `PersuadeGain`도
    // 같이 옮겨야 한다.
    public const float PersuadeCooldown = 180f;

    // 다음 회차를 걸 수 있는 시각은 **포로가 들고 있다**(`UnitBlackboard.NextPersuadeTime`).
    // 여기 정적 표로 두면 죽거나 넘어온 폰의 항목이 남아 새고, 무엇보다 **누가 설득하든
    // 같은 쿨타임**이어야 한다 — 사람을 바꿔 가며 누르면 쉬지 않고 도는 것이 되면 안 된다.
    public static bool PersuadeReady(UnitBrain target) =>
        target?.Blackboard == null || Time.time >= target.Blackboard.NextPersuadeTime;

    public static float PersuadeCooldownLeft(UnitBrain target) =>
        target?.Blackboard == null ? 0f
            : Mathf.Max(0f, target.Blackboard.NextPersuadeTime - Time.time);

    // ── 한 회차에 오르는 양 ──────────────────────────────────────────────
    //
    // **설득하는 쪽의 인지력은 여기 안 들어간다 — 성공률에만 든다**(`PersuadeChance`).
    // 한 스탯이 "얼마나 오르나"와 "먹히나"를 둘 다 정하면 **같은 값이 두 번 곱해져** 잘하는
    // 폰과 못하는 폰의 차이가 쓸데없이 벌어진다(다섯 배가 넘었다). 하나만 고르는 편이
    // 화면에 적기도 쉽다: "말이 먹히느냐"가 인지력이다.
    //
    // **버티는 쪽의 인지력은 든다.** 머리가 좋은 사람은 좀처럼 안 넘어온다 — 설득하는 쪽과
    // 같은 스탯을 마주 세우는 것이 대칭이라 읽기 쉽다.
    //
    // 한때 강인함으로 버티게 했다가 걷어냈었다("저 놈은 왜 느리지"를 스탯 창을 열어야
    // 알게 될 뿐이었다). 지금은 **포로 패널이 회유·탈출 게이지를 둘 다 띄우고** 갇힌 사람도
    // 눌러서 스탯을 볼 수 있으므로(2026-08-13), 그때의 반대 이유가 사라졌다.
    const float BaseGain      = 0.11f;   // 저항 0일 때 아홉 회차쯤
    const float MindResist    = 1f;      // 포로 인지 130이면 배로 걸린다

    public static float PersuadeGain(UnitBrain speaker, UnitBrain target)
    {
        float mind = target != null && target.Stats != null ? target.Stats.Cognition : 0f;
        return BaseGain / (1f + mind / 130f * MindResist);
    }

    // **설득하는 쪽은 간수 솜씨로 잰다**(`StatName.Social`).
    //
    // 인지력을 빌려 쓰고 있었는데, 그러면 **학자가 곧 간수가 된다** — 연구를 시키려고 올린
    // 스탯이 설득까지 잘하게 만들고, 반대로 간수를 키우면 연구 속도가 딸려 온다. 작업
    // 카테고리마다 노동 스탯이 하나씩 있는데(치료=의료, 연구=연구) 간수만 없었던 것이다.
    //
    // **버티는 쪽은 여전히 인지력이다**(`PersuadeGain`) — 설득은 **기술**이고 버티는 것은
    // **머리**다. 같은 스탯을 마주 세우면 대칭이 예쁘지만, 포로에게 "간수 솜씨"를 물을 수는 없다.
    static float SocialSkill(UnitBrain speaker)
        => speaker != null && speaker.Stats != null ? speaker.Stats.Social : 0f;

    // 한 회차에 걸리는 시간(초) — 말을 붙이는 데 드는 시간. 솜씨가 좋으면 짧다.
    public static float PersuadeSeconds(UnitBrain speaker)
        => 8f / (0.5f + SocialSkill(speaker) / 100f);

    // **말이 안 먹힐 수도 있다.** 간수 솜씨 0이면 40%, 130이면 90%쯤 통한다.
    //
    // 여기 확률을 둔 것은 앞서 "확률로 굴리지 말자"고 한 것과 안 어긋난다 — 그때 막은 것은
    // **모르게** 굴리는 것이었다. 이건 굴린 결과가 그 자리에서 글자로 뜨고(성공/실패),
    // 실패해도 **잃는 것은 시간뿐**이라 무엇이 일어났는지도 무엇을 잃었는지도 분명하다.
    // (지배 실패는 죽지만 그건 도박이라고 적어 두고 사람이 누르는 것이다.)
    public static float PersuadeChance(UnitBrain speaker)
        => Mathf.Clamp01(0.40f + SocialSkill(speaker) / 130f * 0.50f);

    // **한 회차를 치른다.** 굴리고, 알리고, 쿨타임을 건다.
    //
    // `PersuadeJob`이 결과를 직접 계산하지 않는다 — 성공/실패의 값과 알림이 일감 안에 있으면,
    // 나중에 다른 길(월드맵 포로 설득 등)이 생겼을 때 반드시 다르게 적힌다.
    public static void PersuadeRound(UnitBrain speaker, UnitBrain target)
    {
        if (target?.Blackboard == null) return;

        target.Blackboard.NextPersuadeTime = Time.time + PersuadeCooldown;

        bool ok = Random.value <= PersuadeChance(speaker);
        GradeUpNotificationUI.Popup(target.transform,
                                    ok ? "설득 성공" : "설득 실패",
                                    ok ? new Color(0.45f, 0.9f, 0.5f) : new Color(0.9f, 0.45f, 0.4f));

        if (ok) Add(target, PersuadeGain(speaker, target));
    }

    // 설득으로 회유할 수 있는 대상인가 — **우리에 갇힌 사람 종족**만.
    // 하수인 종족은 말이 안 통한다(핵이 대신한다). 침대는 살려 두는 자리지 돌려세우는
    // 자리가 아니다 — 그쪽에 있으면 먼저 우리로 옮겨야 한다(위 `InCage` 주석).
    public static bool CanPersuade(UnitBrain brain) =>
        brain != null && brain.Blackboard != null && brain.Blackboard.IsPrisoner
        && !(brain.Data is MinionData) && !IsBeast(brain) && InCage(brain);

    // ── 조련 (짐승) ──────────────────────────────────────────────────────
    //
    // **짐승은 말이 안 통한다 — 다룬다**(2026-10-01). 설득과 같은 그릇이다(우리 · 회차 일감 · 같은 게이지 ·
    // 같은 쉬는 시간) — 다른 것은 **누가 잘하나**(사교가 아니라 조련)와 **무엇이 버티나**(인지가 아니라 근력 —
    // 사나운 것은 몸이 크다)뿐이다. 다 차면 그대로 넘어온다(가축 — `UnitBrain.IsTamedBeast`).
    public static bool IsBeast(UnitBrain brain) =>
        brain != null && brain.Data != null && brain.Data.Tameable && !(brain.Data is MinionData);

    public static bool CanTame(UnitBrain brain) =>
        brain != null && brain.Blackboard != null && brain.Blackboard.IsPrisoner
        && IsBeast(brain) && InCage(brain);

    static float TamingSkill(UnitBrain tamer)
        => tamer != null && tamer.Stats != null ? tamer.Stats.Taming : 0f;

    // 한 회차의 시간 · 성공률 — 설득과 같은 눈금에 조련을 넣는다(갈라 두면 둘 중 하나만 손볼 때 어긋난다).
    public static float TameSeconds(UnitBrain tamer) => 8f / (0.5f + TamingSkill(tamer) / 100f);
    public static float TameChance(UnitBrain tamer)  => Mathf.Clamp01(0.35f + TamingSkill(tamer) / 130f * 0.55f);

    public static float TameGain(UnitBrain target)
    {
        float wild = target != null && target.Stats != null ? target.Stats.Strength : 0f;
        return BaseGain / (1f + wild / 130f);
    }

    public static void TameRound(UnitBrain tamer, UnitBrain target)
    {
        if (target?.Blackboard == null) return;

        target.Blackboard.NextPersuadeTime = Time.time + PersuadeCooldown;   // 쉬는 시간은 한 벌(누가 다루든 같다)

        bool ok = Random.value <= TameChance(tamer);
        GradeUpNotificationUI.Popup(target.transform,
                                    ok ? "길들이기 성공" : "길들이기 실패",
                                    ok ? new Color(0.45f, 0.9f, 0.5f) : new Color(0.9f, 0.45f, 0.4f));

        if (ok) Add(target, TameGain(target));
    }

    // ── 공통 ─────────────────────────────────────────────────────────────

    public static void Add(UnitBrain brain, float amount)
    {
        if (brain?.Blackboard == null || amount <= 0f) return;

        var bb = brain.Blackboard;
        bb.ConvertProgress = Mathf.Min(Complete, bb.ConvertProgress + amount);
        if (bb.ConvertProgress >= Complete) Join(brain);
    }

    // 넘어온다. **갇힘을 먼저 푼다** — 안 풀면 우리 편이 된 채로 우리에 묶여 있다.
    static void Join(UnitBrain brain)
    {
        var player = Faction.Player;
        if (player == null || player.Data == null)
        {
            Debug.LogWarning($"[회유] {brain.UnitName}: 플레이어 세력을 못 찾아 합류시키지 못했습니다.");
            return;
        }

        brain.Blackboard.ConvertProgress = 0f;

        // 자리에서 꺼내고(우리·죄수 침대) 갇힘을 푼다. `Imprisonment.Release`는 **적이면
        // 도망 성향을 씌우므로** 쓰지 않는다 — 이쪽은 나가는 게 아니라 들어오는 것이다.
        PrisonCage.HolderOf(brain)?.Release();
        Bed.ReleaseFrom(brain);
        Imprisonment.Unbind(brain);

        brain.ChangeFaction(player.Data);

        bool minion = brain.Data is MinionData;
        string what = minion ? "하수인" : IsBeast(brain) ? "가축" : "방랑자";
        EventNotificationUI.Notify(IsBeast(brain) ? "길들이기" : "회유",
            $"{brain.UnitName}이(가) 우리 편이 되었습니다 ({what}).",
            holdWhenHidden: true);
    }
}
