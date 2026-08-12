using UnityEngine;

// **붙잡아 둔 사람은 스스로 걸어 나간다 — 지키지 않으면.**
//
// 여기 오기 전까지 갇힘은 **영구**였다: 한 번 가두면 회유가 끝날 때까지 아무 일도 안 일어났고,
// 그래서 붙잡는 것이 공짜였다(우리 하나만 지어 두면 그 뒤로는 시간만 지나면 됐다).
//
// ── 확률이 아니라 게이지다 ────────────────────────────────────────────────────
//
// 처음엔 1분마다 굴렸다. 그러면 **어느 날 포로가 사라지고, 그게 관리를 안 해서인지 운인지
// 알 길이 없다** — 이 저장소가 "모르게 굴리지 말자"고 여러 번 적어 둔 바로 그 꼴이다.
//
// 게이지로 두면 셋이 한꺼번에 풀린다: 얼마나 위험한지 **눈에 보이고**, 간수를 켜면 느려지는
// 것이 **보이며**, 회유 게이지와 **경주**가 된다. 관리가 그 경주를 정한다.
//
// **`ConvertProgress`와 나란한 축**이다 — 하나는 넘어오는 쪽으로, 하나는 나가는 쪽으로 찬다.
//
// ── 되잡는 길은 이미 있다 ─────────────────────────────────────────────────────
//
// 도망치는 포로는 `LeavingMap`이라 자동 표적에서 빠지지만 우클릭 [공격한다]로 지목해
// 때려눕힐 수 있고(`FinishTarget`), 눕히면 `PawnCarry.CanCarry`가 통과해 다시 업어 가둘 수
// 있다 — 규칙을 새로 만들지 않아도 고리가 닫힌다.
public static class PrisonBreak
{
    // ── 얼마나 빨리 차나 ──────────────────────────────────────────────────────
    //
    // **기준: 아무도 안 지키고 배는 부른 포로가 1.5일이면 나간다.**
    //
    // 회유가 2일 남짓이므로(`Conversion`) 그대로 두면 **잃는 쪽이 이긴다** — 간수를 켜야
    // 회유가 먼저 끝난다. 그게 이 게이지의 존재 이유다: 붙잡아 두는 것 자체가 결정이 된다.
    // (하루 = `GameTimeManager` 축의 1800초.)
    public const float DaysToEscape = 1.5f;
    public const float FillPerSecond = 1f / (DaysToEscape * 1800f);

    // **간수를 켜 두면 7.5일.** 회유(≈2일)가 넉넉히 먼저 끝난다.
    //
    // "누가 지금 우리 앞에 서 있나"는 안 잰다 — 간수는 회차마다 왔다 가는 일이라 어느 순간을
    // 재느냐로 답이 흔들리고, 그러면 화면에 이유를 적을 수가 없다("지금은 아무도 없어서
    // 도망갔습니다"는 설명이 아니다). **켜고 끄는 것이 곧 "지킬 사람을 두겠다"는 결정**이다.
    public const float WardenFactor = 0.2f;

    // **굶기면 빨라진다.** 먹이기를 게을리하는 것도 관리를 안 하는 것이다 — 굶주린 사람이
    // 필사적이 되는 편이, 약해져서 못 도망치는 것보다 값이 분명하다(후자면 굶기는 것이
    // 최적 전략이 되어 먹이기를 넣은 취지와 정반대가 된다).
    public const float HungryFactor = 2f;

    public const float Complete = 1f;

    // 지키는 사람이 있나 — 화면도 이 답을 쓴다.
    public static bool Watched => PrisonEscort.AnyWarden();

    // 지금 초당 얼마나 차나. **화면이 "며칠 남았나"를 이 값으로 낸다** — 계산이 두 곳에
    // 있으면 보이는 수와 실제가 어긋난다.
    public static float RateFor(UnitBrain prisoner)
    {
        if (!CanFill(prisoner)) return 0f;

        float rate = FillPerSecond;
        if (Watched) rate *= WardenFactor;
        if (Feeding.IsHungry(prisoner)) rate *= HungryFactor;
        return rate;
    }

    // **기절해 있으면 안 찬다.** 못 걷는 사람이 도망칠 궁리를 하지는 않는다 — 속도를 깎는
    // 것보다 규칙으로 두는 편이 화면에 적기도 쉽다.
    //
    // **다친 정도는 안 본다.** 넣으면 치료가 벌칙이 된다("살려 놓으면 도망간다").
    //
    // **우리 폰은 안 도망친다**(`PrisonEscort.IsCaptive` — "붙잡은 사람"의 단일 정의).
    // 도망칠 곳도 없고(`Imprisonment.Release`가 우리 폰에겐 도주 성향도 맵 이탈도 안 씌운다)
    // 알림만 거짓말로 남는다.
    public static bool CanFill(UnitBrain prisoner)
    {
        var bb = prisoner != null ? prisoner.Blackboard : null;
        return bb != null && bb.IsPrisoner && !bb.IsKnockedOut
            && PrisonEscort.IsCaptive(prisoner);
    }

    public static void Clear(UnitBrain prisoner)
    {
        var bb = prisoner != null ? prisoner.Blackboard : null;
        if (bb == null) return;

        bb.EscapeProgress = 0f;
        bb.LastEscapeTick = 0f;
    }

    // 자리(`PrisonCage`·`Bed`)가 주기 점검에서 부른다 — `Imprisonment.TickHeld` 한 줄로 묶여 있다.
    public static void Tick(UnitBrain prisoner)
    {
        var bb = prisoner != null ? prisoner.Blackboard : null;
        if (bb == null || !bb.IsPrisoner) return;

        // **델타는 스스로 잰다.** 부르는 간격을 인자로 받으면 자리마다 주기가 달라질 때
        // 속도가 조용히 갈린다. 첫 틱은 흘려보낸다(갇힌 순간부터 재기 시작).
        float now  = Time.time;
        float last = bb.LastEscapeTick;
        bb.LastEscapeTick = now;
        if (last <= 0f) return;

        float rate = RateFor(prisoner);
        if (rate <= 0f) return;   // 기절·우리 폰 — 시각은 위에서 이미 갱신했다

        bb.EscapeProgress += rate * (now - last);
        if (bb.EscapeProgress < Complete) return;

        Escape(prisoner);
    }

    static void Escape(UnitBrain prisoner)
    {
        string who = prisoner.UnitName;

        // **놓아 주는 길과 같은 문을 쓴다**(`Imprisonment.Release`) — 자리를 비우고, 갇힘을
        // 풀고, 도주 성향을 씌워 맵 밖으로 내보내는 일이 전부 거기 있다. 여기서 따로 적으면
        // "탈출한 놈만 구석에서 서성인다"가 된다. (`Unbind`가 게이지도 지운다.)
        Imprisonment.Release(prisoner, "탈출했다");

        // **화면에 뜬다.** 조용히 사라지면 사람이 잃은 것이 아니라 없어진 것이 되고,
        // 무엇보다 **쫓아갈 기회**가 사라진다 — 지금 때려눕히면 다시 업어 가둘 수 있다.
        GradeUpNotificationUI.Popup(prisoner.transform, "탈출!", new Color(0.9f, 0.45f, 0.4f));
        EventNotificationUI.Notify("포로 탈출",
            $"{who}이(가) 도망칩니다 — 쫓아가 쓰러뜨리면 다시 붙잡을 수 있습니다.");
    }
}
