using UnityEngine;

// **몸이 요구하는 것들** — 허기 · 피로.
//
// 둘은 기계적으로 같은 물건이다: 0~100 사이에서 **저절로 줄고**, 임계 아래로 내려가면 폰이
// **채우러 가고**, 바닥에 닿으면 **몸이 상한다**. 갈리는 것은 눈금과 채우는 방법뿐이다.
//
// 그래서 축마다 따로 적지 않는다. 허기 하나뿐이던 시절에도 그 규칙은 여섯 군데에 흩어져
// 있었다(블랙보드 · 감소 · 임계 · `EatNode` · 포로 급식 · 살펴보기 창). 축이 늘 때마다 그
// 여섯이 통째로 복사되고, 그중 하나가 어긋나는 것은 시간 문제다 — 이 저장소가 `CarryCapacity`
// (3.3배) · 표시명 · `Storage.Deliver`에서 이미 세 번 겪은 모양이다.
//
// **한때 갈증이 셋째 축이었다**(2026-08-20에 넣었다 같은 날 걷어냈다). 걷어낸 이유는 눈금이
// 아니라 **하는 일이 허기와 같아서**다: 둘 다 *창고에서 소모품을 찾아 먹기*이고 우물이 서는
// 순간 물은 사실상 무한이라, **축은 둘인데 플레이어가 내리는 결정은 늘지 않았다** — 폰이
// 하루 세 번 더 걸어갈 뿐이었다. 되살릴 일이 있으면 그날 worklog에 설계가 통째로 남아 있다.
public enum NeedKind { Hunger, Rest }

public static class Needs
{
    public const float Full  = 100f;
    public const int   Count = 2;

    // ── 눈금 ─────────────────────────────────────────────────────────────
    //
    // **기준 축은 하루(`GameTimeManager.DayDuration`, 1800초)다.** 초당 감소량을 상수로 박으면
    // 하루 길이를 바꿨을 때 따라오지 않는다 — "하루에 몇 번"으로 적고 거기서 나눈다.
    //
    // | 축 | 하루에 | 100 → 0 | 임계까지 |
    // |---|---|---|---|
    // | 허기 | 2끼 | 900초 | 360초 |
    // | 피로 | (깨어 있는 내내) | 1200초 | 480초 |
    //
    // **피로는 하루의 2/3(1200초) 깨어 있고 1/3(600초) 잔다.** 그 600초는 어두운 때(밤+새벽
    // 8시간)와 **정확히 같아서**, 잘 굴러가는 콜로니는 저절로 *밤에 자고 낮에 일하는* 모양이
    // 된다 — 밤이 견디는 시간이 아니라 **자는 시간**이 되는 자리다.
    //
    // **그 시간은 0과 100 사이가 아니라 임계(`Threshold`)와 가득(100) 사이를 건너는 시간이다.**
    // 폰은 바닥이 아니라 **임계에서 자러 가고 가득 차면 일어나므로**, 0~100으로 재면 실제
    // 주기가 그 40%로 줄어 **하루에 두세 번 눕는다**(720초 주기 — 4분 자고 8분 일한다).
    // 그러면 "밤에 잔다"가 성립하지 않는다. 허기는 이 셈이 다르다(아래).
    const float MealsPerDay = 2f;
    const float WakeDays    = 2f / 3f;

    // 폰이 오가는 구간 — 임계에서 자러 가서 가득 채우고 일어난다.
    const float Band = Full - Threshold;

    public static float DecayPerSecond(NeedKind kind)
    {
        float day = GameTimeManager.DayDuration;
        return kind switch
        {
            // **허기는 0까지 쓴다** — 굶어 죽는 자리가 실제로 있고(혈액 2/s), 임계는 *먹으러
            // 갈까*를 정할 뿐이라 밥은 있을 때 자주 먹는 것이 맞다. 그래서 하루 2끼는
            // *한 번 채워 바닥날 때까지*의 눈금이고, 실제로는 임계에서 먹으므로 더 자주 간다.
            NeedKind.Hunger => Full / (day / MealsPerDay),
            _               => Band / (day * WakeDays),
        };
    }

    // ── 잠 ───────────────────────────────────────────────────────────────
    //
    // **침대에서 자면 600초에 임계에서 가득까지 채운다** — 깨어 있는 1200초와 합쳐 **하루가
    // 딱 맞는다**(빈 몸에서 100까지는 1500초다 — 쓰러졌을 때가 그 값이다).
    // 그리고 그 600초는 어두운 때(밤+새벽)와 **정확히 같은 길이**라, 잘 굴러가는 콜로니는
    // 저절로 *밤에 자고 낮에 일하는* 모양이 된다.
    //
    // **바닥에서 자면 절반이다.** 그래서 침대가 값을 갖는다 — 안 그러면 침대는 다친 사람을
    // 눕히는 물건일 뿐이고, 지어야 할 이유가 방 하나만큼도 안 된다. 절반이면 밤새 자도
    // 다 못 채워서 **다음 날 낮에 또 졸린다.**
    public const  float SleepDays      = 1f / 3f;
    public static float SleepPerSecond(bool inBed)
        => Band / (GameTimeManager.DayDuration * SleepDays) * (inBed ? 1f : 0.5f);

    // **자러 가는 문턱과 일어나는 문턱은 다르다**(히스테리시스). 같으면 문턱 위에서 **떤다** —
    // 자러 가서 조금 차면 "이제 안 졸리다"가 되어 일어나고, 걸어 나가면 다시 졸려서 돌아온다.
    // 실제로 그랬다(2026-08-20): 침대로 갔다 바로 일어나기를 되풀이했다.
    //
    // 그래서 **가는 것은 `Threshold`(40)가 정하고 나오는 것은 `WakeAt`(100)이 정한다.**
    // 읽는 쪽이 둘을 헷갈리면 그 진동이 그대로 돌아오므로, `RestNode`는 *이미 누우러 갔는가*로
    // 어느 문턱을 볼지 가른다.

    // 이 아래면 쓰러져 잔다 — **자리를 안 가린다.** 임계에서 자러 가는 것은 *판단*이고
    // 여기는 *한계*라, 습격 한복판이든 광산 바닥이든 몸이 먼저 무너진다. 그것이 피로가
    // 안 죽이면서도 아픈 이유다.
    public const float CollapseAt = 0f;

    // 잠에서 깨는 지점. **가득 채우고 일어난다** — 임계에서 깨우면 하루 종일 자다 깨다
    // 하고, 그러면 밤에 자는 리듬이 안 생긴다.
    public const float WakeAt = Full;

    // 이 아래면 채우러 간다. **축이 같이 쓴다** — 축마다 다르면 "왜 얘는 아직 안 가지"를
    // 축별로 외워야 한다.
    //
    // **40이다**(2026-08-20에 60에서 내렸다). 60은 *절반도 안 쓰고 움직이는* 값이라 특히
    // 허기에서 헛걸음이 많았다 — **하루 다섯 번** 창고를 왕복했는데, 그건 "하루 2끼"라고
    // 적어 둔 눈금과 너무 멀다. 40이면 **3.3번**이다.
    //
    // **피로 주기는 이 값과 무관하다** — 눈금을 임계~가득 사이로 재기 때문에(`Band`) 문턱을
    // 어디에 두든 1200 + 600 = 하루 한 번이다. 대신 갈리는 것은 **여유**다:
    //
    // | | 임계 60 | 임계 40 |
    // |---|---|---|
    // | 끼니 간격 | 360초(하루 5번) | **540초(하루 3.3번)** |
    // | 먹으러 간 뒤 아사까지 | 540초 | **360초** |
    // | 안 자고 버티다 쓰러지기까지 | 1.67일 | **1.11일** |
    // | 밤새 바닥잠으로 차는 양 | 20점 | **30점** |
    //
    // 즉 **덜 자주 움직이는 대신 늦게 움직인다** — 물자가 끊겼을 때 손쓸 시간이 그만큼 짧다.
    public const float Threshold = 40f;

    // ── 바닥에 닿으면 ────────────────────────────────────────────────────
    //
    // 허기는 **혈액을 깎는다**(`BodyPartSystem.Starve`) — 화면에 보이는 파이프라인으로
    // 기절 → 사망시키려고. 숨은 제2 체력으로 죽이던 것을 2026-07-26에 여기로 옮겼다.
    //
    // **피로는 안 죽인다.** 잠을 못 자서 죽는 것은 이 게임의 시간 눈금(하루 30분)에서 성립하지
    // 않고, 무엇보다 **못 쓰게 만드는 것이 더 아프다** — 바닥에 닿으면 그 자리에 쓰러져 잔다.
    public static float StarvePerSecond(NeedKind kind) => kind switch
    {
        NeedKind.Hunger => 2f,
        _               => 0f,
    };

    // ── 이름 ─────────────────────────────────────────────────────────────
    // **여기 한 벌뿐이다** — 살펴보기 창·우클릭 줄·로그가 같은 말을 쓰게.
    public static string Name(NeedKind kind) => kind switch
    {
        NeedKind.Hunger => "허기",
        _               => "피로",
    };

    // ── 누가 이 축을 도는가 ──────────────────────────────────────────────
    //
    // **플레이어 세력, 그리고 갇힌 사람은 세력을 안 가린다**(업힌 동안은 아무도 안 돈다).
    //
    // 붙잡아 온 적이 안 굶던 시절에는 우리에 넣어 두기만 하면 회유가 저절로 끝났다 — 붙들어
    // 두는 데 값이 들어야 한다. 화면(`InspectPanelUI`의 바)도 **이 답을 쓴다**: 도는 것과
    // 보이는 것이 갈리면 가장 나쁜 쪽으로 나타난다(포로는 마르는데 바가 안 떠서 왜 죽는지
    // 알 길이 없다).
    //
    // **죽은 자는 아무것도 필요하지 않다.** 시체는 `UnitBrain`을 끄지만 컴포넌트는 남으므로,
    // 갇힌 채 죽으면 침대·우리가 계속 "여기 목마른 사람이 있다"고 답한다.
    public static bool Simulates(UnitBrain brain)
    {
        var bb = brain != null ? brain.Blackboard : null;
        if (bb == null || brain.IsDead) return false;
        if (bb.CarriedBy != null) return false;
        if (bb.IsPrisoner) return true;

        // **매 프레임 `GetComponent`를 다시 하지 않는다** — 블랙보드가 이미 들고 있다.
        return bb.Faction != null && bb.Faction.IsPlayer;
    }

    // **축마다 다시 묻는다** — 갇힌 사람은 **피로가 안 돈다.**
    //
    // 먹이고 물 주는 것은 남이 해 줄 수 있지만(`FeedJob`) **잠은 대신 자 줄 수 없다.**
    // 갇히면 BT가 안 돌아 `RestNode`도 안 도니, 피로만 도는 것은 *채울 길이 없는 바*를
    // 하나 만드는 일이다 — 그건 관리할 것이 아니라 화면의 거짓말이다. 가둬 두는 값은
    // 허기로 이미 든다.
    //
    // (우리에 세워 두든 침대에 눕히든 같다. 자리로 갈라 봐야 "왜 이 포로만 피로가 도나"가
    // 되고, 침대 포로만 저절로 차는 것은 그 자리를 회복하는 자리로 만들자는 뜻인데 그건
    // 이미 부상 회복이 하고 있다.)
    public static bool Simulates(UnitBrain brain, NeedKind kind)
    {
        if (!Simulates(brain)) return false;
        if (kind != NeedKind.Rest) return true;

        var bb = brain.Blackboard;
        return bb != null && !bb.IsPrisoner;
    }

    // **줄고 있는가** — 도는 축이라고 늘 주는 것은 아니다.
    //
    // **자는 동안에는 피로가 안 준다.** 피로는 *깨어 있는 값*이라 그게 맞고, 안 그러면
    // 채우는 속도에서 주는 속도가 그대로 상쇄된다: 침대는 두 배 느려지고 **바닥잠은 영영
    // 안 찬다**(바닥 회복 0.083/s가 감소 0.083/s와 정확히 같아 net 0이다 — 쓰러진 폰이
    // 영원히 못 일어난다).
    public static bool Decays(UnitBrain brain, NeedKind kind)
    {
        if (!Simulates(brain, kind)) return false;
        if (kind != NeedKind.Rest)   return true;

        var bb = brain.Blackboard;
        return bb == null || bb.Mover == null || !bb.Mover.IsSleeping;
    }

    public static bool IsLow(UnitBrain brain, NeedKind kind)
    {
        var bb = brain != null ? brain.Blackboard : null;
        return bb != null && bb.Need(kind) < Threshold;
    }

    // 채울 수 있는 양. **넘치는 만큼은 버려진다** — 무엇을 고를지가 이 값에서 나온다.
    public static float Room(float current) => Mathf.Max(0f, Full - current);

    // **길이는 여기서 나온다.** 블랙보드가 `{ Full, Full }`처럼 손으로 적어 두면 축을 하나
    // 더할 때 **컴파일은 통과하고 첫 틱에 `IndexOutOfRangeException`이 난다**(`Need`/`SetNeed`가
    // `(int)kind`로 색인하고 `UpdateNeeds`는 `Count`까지 돈다). 갈증을 넣었다 뺀 자리라
    // 다시 넣을 일이 실제로 있다.
    public static float[] NewFull()
    {
        var a = new float[Count];
        for (int i = 0; i < a.Length; i++) a[i] = Full;
        return a;
    }

    public static void Fill(UnitBlackboard bb, NeedKind kind, float amount)
    {
        if (bb == null || amount <= 0f) return;
        bb.SetNeed(kind, Mathf.Min(Full, bb.Need(kind) + amount));
    }
}
