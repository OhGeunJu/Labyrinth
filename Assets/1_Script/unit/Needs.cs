using UnityEngine;

// **몸이 요구하는 것들** — 허기 · 피로 · 에너지 · 사기.
//
// **넷이지만 한 몸이 넷을 다 요구하지는 않는다** — 산 몸은 허기·피로, 골렘은 에너지,
// 해골은 사기다(`BodyKind`). 무엇을 요구하는지는 몸이 답하고(`Runs`), 그 답을 쓰는 자리는
// `Simulates` 하나다.
//
// 축들은 기계적으로 같은 물건이다: 0~100 사이에서 **저절로 줄고**, 임계 아래로 내려가면 폰이
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
//
// **에너지·사기는 그 함정을 피한다** — 하는 일은 같아 보여도 **채우는 자리가 창고가 아니라
// 지은 건물**이라(충전대·제단) *어디에 몇 개 세우나*라는 결정이 딸려 온다.

// **축은 꼬리에 붙인다** — 블랙보드가 `(int)kind`로 색인하는 `float[]`이라, 중간에 끼우면
// 살아 있는 폰의 허기 칸에 에너지가 들어간다.
public enum NeedKind { Hunger, Rest, Energy, Morale }

// **몸의 종류 — 무엇을 요구하는지가 여기서 갈린다**(2026-09-04, 사용자: *"골렘 하수인
// 허기가 있는게 이상함"*).
//
// 그전에는 *"플레이어 세력인가"* 하나가 축을 정해서 **골렘이 밥을 먹고 침대에서 잤다.**
// 그러다 창고가 비면 돌덩이가 굶어 죽었다 — 규칙이 아니라 사고다.
//
// | 몸 | 요구하는 것 | 바닥에 닿으면 | 채우는 자리 |
// |---|---|---|---|
// | 산 몸 | 허기 · 피로 | 굶어 죽는다 / 쓰러져 잔다 | 음식 · 침대 |
// | 구축물(골렘) | **에너지** | **일이 멎는다** | 마력 충전대 |
// | 언데드(해골) | **사기** | **무너진다** | 망자의 제단 |
//
// **비대칭이 곧 성격이다.** 골렘은 기계라 꺼졌다 다시 켜면 되고(멈출 뿐 안 죽는다),
// 해골은 붙들어 둔 의지라 놓치면 흩어진다. 그래서 골렘은 비싸고 안전하며, 해골은
// 싸고 손이 간다.
//
// **"멈춘다"는 일·순찰·배회가 멎는다는 뜻이다** — 명령과 반격은 남는다(`RechargeNode`가
// 전투·명령 **아래**에 있다). 완전히 굳게 하면 에너지 떨어진 골렘이 공짜 표적이 되고,
// 무엇보다 **재료가 생겨도 제 발로 못 가는 데드락**이 된다.
public enum BodyKind { Living, Construct, Undead }

public static class Needs
{
    public const float Full  = 100f;
    public const int   Count = 4;

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

    // **에너지·사기는 하루에 한 번 바닥나는 눈금이다** — 임계(40)까지가 1,080초라 실제로는
    // **하루 1.67번** 채우러 간다(밥은 3.3번이다). 유지비가 밥보다 성긴 대신 **재료가 비싸다**
    // (마력석·뼈는 밀밭처럼 자라지 않는다) — 그것이 하수인을 늘리는 값이다.
    //
    // **한 번에 가득 찬다**(`Recharge.Fill`) — 음식처럼 영양이 갈리지 않으므로 임계~가득
    // 사이를 재는 `Band` 셈이 필요 없다. 그래서 `Full`을 그대로 나눈다.
    const float ChargesPerDay = 1f;

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
            NeedKind.Rest   => Band / (day * WakeDays),
            _               => Full / (day / ChargesPerDay),
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

    // **잠자리의 질이 곧 배율이다**(2026-08-25). 전에는 `bool inBed` 하나라 침대는 전부
    // 똑같았고, 그래서 *더 좋은 침대*라는 것이 표현될 자리가 없었다 — 가구를 지을 이유가
    // 없다는 뜻이기도 하다.
    //
    // 눈금은 그대로다: **바닥 0.5 · 보통 침대 1.0**(`Bed.RestQuality` 기본값). 그 위는
    // 침대가 저작한다(좋은 침대 1.4 → 임계에서 가득까지 600초가 **429초**).
    public const float FloorQuality = 0.5f;

    public static float SleepPerSecond(float quality)
        => Band / (GameTimeManager.DayDuration * SleepDays) * Mathf.Max(0f, quality);

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
    // **사기는 허기와 같은 자리다** — 다 하면 무너진다(언데드는 기절하지 않으므로
    // `BodyPartSystem`이 곧장 죽인다). **에너지는 피로와 같은 자리다** — 안 죽고 멈춘다.
    // 멈춘 골렘을 되살리는 길은 재료가 생기는 것뿐이고, 그때는 제 발로 간다
    // (`RechargeNode`가 멈춘 채로도 계속 돌기 때문이다 — 안 그러면 **데드락**이다).
    public static float StarvePerSecond(NeedKind kind) => kind switch
    {
        NeedKind.Hunger => 2f,
        NeedKind.Morale => 2f,
        _               => 0f,
    };

    // ── 이름 ─────────────────────────────────────────────────────────────
    // **여기 한 벌뿐이다** — 살펴보기 창·우클릭 줄·로그가 같은 말을 쓰게.
    public static string Name(NeedKind kind) => kind switch
    {
        NeedKind.Hunger => "허기",
        NeedKind.Rest   => "피로",
        NeedKind.Energy => "에너지",
        _               => "사기",
    };

    // ── 무슨 몸인가 ──────────────────────────────────────────────────────
    //
    // **답이 갈리는 길이 둘이다** — 종족이 애초에 그런 몸이거나(`UnitData.IsConstruct`),
    // 한 번 죽어 일어났거나(`UnitBrain.IsUndead` — 해골 종족 ∥ `Rise`로 세운 시체).
    // 언데드를 여기서 따로 세지 않는 이유는 그 물음의 답이 이미 한 벌이기 때문이다.
    //
    // **언데드가 먼저다** — 일으켜 세운 골렘 같은 것은 없지만, 순서를 안 정해 두면
    // 둘 다 참인 에셋이 하나 생기는 날 답이 자리에 따라 갈린다.
    public static BodyKind BodyOf(UnitBrain brain)
    {
        if (brain == null) return BodyKind.Living;
        if (brain.IsUndead) return BodyKind.Undead;
        return brain.Data != null && brain.Data.IsConstruct ? BodyKind.Construct : BodyKind.Living;
    }

    // 이 몸이 그 축을 요구하는가. **`Simulates`가 이걸 부른다**(반대가 아니다) — 밖에서
    // `IsConstruct`를 다시 물으면 몸이 늘 때 그 자리가 뒤처진다.
    public static bool Runs(BodyKind body, NeedKind kind) => body switch
    {
        BodyKind.Construct => kind == NeedKind.Energy,
        BodyKind.Undead    => kind == NeedKind.Morale,
        _                  => kind == NeedKind.Hunger || kind == NeedKind.Rest,
    };

    public static bool Runs(UnitBrain brain, NeedKind kind) => Runs(BodyOf(brain), kind);

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

    // **축마다 다시 묻는다** — 무슨 몸인가(`Runs`)와, 갇혔는가 둘이다.
    //
    // **갇힌 사람은 허기만 돈다.** 먹이는 것은 남이 해 줄 수 있지만(`FeedJob`) **잠은 대신
    // 자 줄 수 없고**, 충전대와 제단은 **갇힌 채로는 갈 수가 없다** — 도는데 채울 길이 없는
    // 축은 관리할 것이 아니라 화면의 거짓말이다. 갇히면 BT가 안 돌아 `RestNode`도
    // `RechargeNode`도 안 돈다는 것이 그 이유의 전부다.
    //
    // **사기는 안 막으면 가두는 것이 곧 처형이 된다** — 바닥나면 무너지는데(`StarvePerSecond`)
    // 제단에 갈 수가 없으니 **가둔 해골은 반드시 죽는다.**
    //
    // 딸려 오는 것 하나: **골렘과 해골은 굶겨서 회유할 수 없다**(도는 축이 없다). 우리에
    // 넣어 두는 값이 사람에게만 든다는 뜻이고, 그건 맞다 — 돌덩이를 굶기는 그림이 애초에
    // 이 물음의 출발이었다.
    //
    // (우리에 세워 두든 침대에 눕히든 같다. 자리로 갈라 봐야 "왜 이 포로만 피로가 도나"가
    // 되고, 침대 포로만 저절로 차는 것은 그 자리를 회복하는 자리로 만들자는 뜻인데 그건
    // 이미 부상 회복이 하고 있다.)
    public static bool Simulates(UnitBrain brain, NeedKind kind)
    {
        if (!Simulates(brain))  return false;
        if (!Runs(brain, kind)) return false;

        var bb = brain.Blackboard;
        if (bb != null && bb.IsPrisoner) return kind == NeedKind.Hunger;
        return true;
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
