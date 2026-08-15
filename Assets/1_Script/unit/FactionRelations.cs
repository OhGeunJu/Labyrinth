using System;
using System.Collections.Generic;
using UnityEngine;

// **세력이 나를 어떻게 보는가 — 저작한 값이 아니라 지금 값.**
//
// `FactionData.relations`는 `IReadOnlyList`이고 런타임에 바꾸는 코드가 **0건**이었다. 그래서
// 세력을 아홉으로 늘려도 관계표가 고정이라 **배경화면**이었다 — 마을 사람을 몰살해도 그 마을은
// 여전히 나와 거래했고, 붙잡은 자를 돈 받고 돌려보내도 그쪽은 고마워하지 않았다.
//
// ── 왜 전쟁 깃발이 아니라 우호도인가 ─────────────────────────────────────────
//
// "전쟁 중인가"를 켜고 끄면 **관계가 두 칸짜리**가 되어, 마을 사람 하나를 벤 것과 마을을
// 불태운 것이 같아진다. 수치로 두면 **쌓이고 식는다** — 한 번 실수는 넘어가 주고 계속하면
// 돌아선다. 그리고 되돌릴 길(몸값·풀어 주기)이 값을 갖는다.
//
// ── 저작한 값이 시작값이다 ─────────────────────────────────────────────────
//
// **저작한 태도를 숫자로 옮겨 시작한다** — 적대면 −70, 아군이면 +75, 중립이면 0.
// 전부 0에서 시작했더니 **고블린이 화면에 `적대 (0)`으로 떴다**: 판정은 저작한 값이 하니 맞게
// 굴러가는데 **표가 거짓말을 했다.** 시작값을 태도에서 뽑으면 보이는 수와 실제가 같아진다.
//
// 그래도 저작을 **지우지는 않는다** — 사이 구간(−50 ~ +60)은 `FactionData`가 그대로 이긴다
// (`TownMarket`의 "저작 > 주입 > 기본값"과 같은 줄).
//
// **플레이어와의 관계만 움직인다.** 세력끼리의 전쟁은 지도 위 사건이 정해야 하는데 그걸
// 지금 넣으면 무엇이 그것을 움직이는지가 없어 숫자만 도는 표가 된다.
public static class FactionRelations
{
    // −100 ~ +100. 0이 저작한 그대로다.
    public const float Min = -100f;
    public const float Max =  100f;

    // 이 아래로 내려가면 **적대**, 이 위로 올라가면 **아군**. 사이는 저작한 값을 그대로 쓴다.
    //
    // 문턱이 넓은 것은 일부러다 — 좁으면 사고 한 번에 세력이 돌아서서, 관계가 **쌓이는 것**이
    // 아니라 스위치가 된다.
    public const float HostileAt = -50f;
    public const float AllyAt    =  60f;

    static readonly Dictionary<FactionData, float> _favor = new();

    // 관계가 움직였다 — UI가 듣는다. `(세력, 새 우호도, 왜)`
    public static event Action<FactionData, float, string> OnChanged;

    public static float Of(FactionData f)
    {
        if (f == null) return 0f;
        if (_favor.TryGetValue(f, out float v)) return v;

        // **처음 물어볼 때 저작한 태도를 숫자로 옮긴다.** 전에는 전부 0에서 시작해서
        // **고블린이 화면에 `적대 (0)`으로 떴다** — 판정은 저작한 값이 하니 맞게 굴러가는데
        // 표가 거짓말을 했다. 시작값을 태도에서 뽑으면 **보이는 수와 실제가 같아진다.**
        var player = Faction.Player;
        if (player == null || player.Data == null) return 0f;   // 아직 모른다 — 캐시하지 않는다

        // **세력마다 다르게 저작한다**(`FactionData.StartingFavor`) — 적대라도 정도가 다르다:
        // 산적은 돈이 목적이라 무를 여지가 있고, 망자와는 애초에 말이 통하지 않는다.
        // 안 적어 둔 세력만 태도에서 뽑는다(폴백).
        float seed = !Mathf.Approximately(f.StartingFavor, 0f)
                   ? Mathf.Clamp(f.StartingFavor, Min, Max)
                   : f.StanceTo(player.Data) switch
                     {
                         FactionStance.Hostile => -70f,
                         FactionStance.Ally    =>  75f,
                         _                     =>   0f,
                     };

        _favor[f] = seed;
        return seed;
    }


    // 지도를 새로 짓거나 판을 다시 시작할 때. static은 도메인 리로드를 꺼 두면 살아남는다.
    public static void Clear() => _favor.Clear();

    // ── 움직인다 ──────────────────────────────────────────────────────────────

    // **이유를 같이 받는다.** 우호도가 왜 움직였는지 화면에 적으려면 부르는 쪽이 아는 것을
    // 그대로 넘겨야 한다 — 여기서 짐작하면 "무엇 때문에 미움받는지"를 영영 알 수 없다.
    public static void Add(FactionData f, float delta, string reason)
    {
        if (f == null || f.PlayerControlled || Mathf.Approximately(delta, 0f)) return;

        float before = Of(f);
        float after  = Mathf.Clamp(before + delta, Min, Max);
        if (Mathf.Approximately(before, after)) return;

        _favor[f] = after;

        // **문턱을 넘을 때만 알린다.** 숫자가 1 움직일 때마다 띄우면 전투 중에 알림이 도배되고,
        // 그러면 정작 "돌아섰다"는 소식이 묻힌다.
        var wasStance = StanceFrom(before);
        var nowStance = StanceFrom(after);
        if (wasStance != nowStance)
            EventNotificationUI.Notify("관계 변화",
                $"{f.FactionName}이(가) 이제 {Label(nowStance)}입니다. ({reason})");

        OnChanged?.Invoke(f, after, reason);
    }

    // 죽인다 — 그 세력이 나를 미워한다. **중립·아군을 죽였을 때만 뜻이 있다**:
    // 이미 적대인 상대를 죽이는 것은 전쟁이지 사건이 아니다.
    public const float KillPenalty    = -12f;
    public const float ExecutePenalty = -25f;   // 붙잡아 둔 자를 처형하는 것은 더 나쁘다
    public const float RansomBonus    =   8f;   // 돈 받고 돌려보냈다 — 거래는 거래다
    public const float ReleaseBonus   =  14f;   // 그냥 놓아 줬다 — 값을 안 받은 만큼 더 산다
    public const float RaidPenalty    = -20f;   // 중립 무리를 먼저 덮쳤다 — 죽인 것과 별개로 든다

    // ── 우호도가 무엇을 바꾸는가 ──────────────────────────────────────────────
    //
    // **태도만 정하면 우호도는 관리할 것이 못 된다.** 올려 봐야 "안 덤빈다" 하나뿐이고,
    // 그건 이미 적대가 아닌 세력에겐 아무 변화도 아니다 — 마을 사람(+75)과 변방민(0)이
    // 화면의 숫자만 다르고 하는 일이 똑같았다.
    //
    // **그런데 값에는 안 붙인다.** 한 번 붙여 보고 걷어냈다(2026-08-14 (16)→(17)):
    //
    //   상대별 flat %는 **선택이 아니라 순위를 만든다.** 지역차(`ItemOverrides` 0.5~2.0)는
    //   품목마다 방향이 달라 "무엇을 어디서 사서 어디에 파나"를 묻지만, 우호도는 그 상인의
    //   **모든 품목을 같은 방향으로** 민다. 그래서 오버라이드가 없는 품목(무기·방어구·약 —
    //   대다수)에서는 우호도가 유일한 차이가 되어 평원·숲 마을로 쏠렸다. 게다가 마을 사람
    //   +75는 **저작한 시작값**이라 그 20%는 "관계를 쌓아 얻은 것"이 아니라 그냥
    //   *평원이 원래 싸다*로 나타났다 — 우호도를 넣은 취지와 정반대다.
    //
    // 무역을 다루는 게임들이 대체로 같은 답을 낸다: **가격은 지역·수요공급이, 평판은
    // 접근권과 위험이** 맡는다(마운트앤블레이드·Pirates!·X4·엘리트). flat %를 쓰는 쪽은
    // 그걸 **상대가 아니라 플레이어에게** 붙인다(림월드의 Social, 스카이림의 화술) —
    // 모든 상인에 똑같이 걸리므로 어디로 갈지를 왜곡하지 않는다. 여기서도 값 배율은
    // `Trading`(사교 스탯)이 든다.
    //
    // 남은 파생값 둘은 **순위가 아니라 결과**라 방향이 하나여도 왜곡이 없다:
    // 진열 종류 수와 습격 추첨.

    // −100 ~ +100을 −1 ~ +1로. 파생값은 전부 이 하나에서 나온다.
    static float Normalized(FactionData f) => f == null ? 0f : Mathf.Clamp(Of(f) / Max, -1f, 1f);

    // **진열이 몇 종류나 서는가**(`Shop.Refresh`). 관계가 좋으면 고를 것이 많고 나쁘면
    // 빈약하다 — 값과 달리 **쏠림을 안 만든다**: 평원에서 종류를 더 많이 볼 수 있어도
    // 광물을 사려면 여전히 동굴에 가야 한다. 지역차가 "어디로 가나"를 온전히 도맡는다.
    //
    // 폭이 작은 것은 일부러다(−2 ~ +2). 크게 잡으면 관계 나쁜 마을의 진열이 텅 비어
    // 사실상 거래 불가가 되는데, 그건 태도(적대)가 이미 하는 일이다.
    public const int SlotSwing = 2;

    public static int SlotBonus(FactionData f) => Mathf.RoundToInt(Normalized(f) * SlotSwing);

    // 습격 추첨에서 이 세력이 갖는 무게. **미워할수록 자주 온다** — 우호도를 떨어뜨리는 것에
    // 값이 붙어야 "죽이고 다니는" 것이 공짜가 아니게 된다.
    //
    // 빈도 자체는 전역 시계(`WorldBandManager.raidIntervalDays`)가 그대로 정한다. 여기서
    // 갈리는 건 **누가 올 차례인가**뿐이다 — 우호도로 빈도까지 흔들면 세력 하나를 미워한
    // 값이 습격 횟수의 곱셈이 되어, 소굴 수로 곱해지던 그 사고(2026-08-14 (5))가 축만
    // 바뀌어 돌아온다.
    public static float RaidWeight(FactionData f)
    {
        float t = Normalized(f);
        return t <= 0f ? 1f - t                      //  0 → ×1,  −100 → ×2
                       : Mathf.Lerp(1f, 0.25f, t);   // +100 → ×0.25 (거의 안 온다)
    }

    // ── 판정 ──────────────────────────────────────────────────────────────────

    // **플레이어와의 태도는 우호도가 정한다 — 사이 구간도 포함해서.**
    //
    // 처음엔 문턱을 넘을 때만 덮고 사이 구간은 저작한 값이 이기게 했는데, **시작값을 저작하고
    // 나니 그게 무르는 길을 막았다**: 적대로 저작된 세력은 우호도가 −40까지 올라와도 여전히
    // 적대라, 아군(+60)에 닿기 전에는 아무 변화가 없다. 그러면 **놓아 주고 몸값을 치른 것이
    // 화면의 숫자만 움직이고 세상은 그대로**다.
    //
    // 시작값이 이미 저작한 뜻을 담고 있으므로(적대 세력은 −로 시작한다), 그 뒤로는 우호도
    // 하나가 답이면 된다 — **저작은 시작점이지 판결이 아니다.**
    //
    // **아직 모를 때는 거짓을 돌려준다**(플레이어 세력이 안 잡힌 프레임) — 그때 중립이라고
    // 답해 버리면 판이 시작되는 순간 적이 안 덤빈다.
    // **세력 데이터끼리의 태도 — 컴포넌트 없이 묻는 자리.**
    //
    // `Faction.StanceTo`는 컴포넌트를 받는데, **지도 위의 무리에는 `Faction` 컴포넌트가 없다**
    // (`WorldBand`는 논리 데이터다). 그래서 월드맵 코드가 `FactionData.IsHostileTo`를 직접
    // 불렀고, 그건 **저작한 관계표만 보므로 우호도가 통째로 무시됐다** — 산적 포로를 풀어 줘
    // 중립까지 끌어올려도 지도에서는 여전히 쫓아오고, 조우 메뉴는 `싸운다`만 띄우고, 이미
    // 적대인 무리를 칠 때마다 `RaidPenalty`가 또 붙었다.
    //
    // **판정의 단일 출처를 여기로 내린다** — `Faction.StanceTo`도 이걸 부른다. 필드와 지도가
    // 같은 답을 내야 "화면에는 중립인데 지도에서는 덤빈다"가 안 생긴다.
    public static FactionStance StanceBetween(FactionData a, FactionData b)
    {
        if (a == null || b == null) return FactionStance.Neutral;

        // **자기 세력끼리는 우호도를 안 거친다.** 우호도는 "저 세력이 **나를** 어떻게 보는가"라
        // 플레이어가 자신을 보는 값이라는 것이 없다(`Add`도 플레이어 세력은 튕겨낸다).
        //
        // 안 가르면 내 폰끼리의 판정이 `StanceFrom(Of(플레이어))`로 떨어지는데, 그게 지금
        // 아군으로 나오는 건 `Faction_Player`에 `StartingFavor`가 안 적혀 있어 폴백이
        // `StanceTo(self)`(= 아군 → 75)를 씨앗으로 잡아 주기 때문이다. **인스펙터에서 그 값을
        // 0~59 사이로 한 번 건드리면 내 폰끼리 중립이 되고**, 둘이 조용히 깨진다 —
        // `BodyPartSystem`이 오사를 원한으로 적어 **내 폰끼리 반격**하고,
        // `CombatHandler.HolyAoEHeal`이 아무도 안 고친다. 저작 한 번에 걸릴 함정을 남기지 않는다.
        if (a == b) return a.StanceTo(b);

        // 움직이는 것은 **플레이어와의 관계뿐**이다(세력끼리는 저작한 그대로).
        if (b.PlayerControlled && TryOverride(a, out var mine)) return mine;
        if (a.PlayerControlled && TryOverride(b, out var his))  return his;

        return a.StanceTo(b);
    }

    public static bool IsHostileBetween(FactionData a, FactionData b)
        => StanceBetween(a, b) == FactionStance.Hostile;

    public static bool TryOverride(FactionData f, out FactionStance stance)
    {
        stance = FactionStance.Neutral;
        if (f == null) return false;

        var player = Faction.Player;
        if (player == null || player.Data == null) return false;

        stance = StanceFrom(Of(f));
        return true;
    }

    static FactionStance StanceFrom(float favor) =>
        favor <= HostileAt ? FactionStance.Hostile :
        favor >= AllyAt    ? FactionStance.Ally    : FactionStance.Neutral;

    static string Label(FactionStance s) => s switch
    {
        FactionStance.Hostile => "적대",
        FactionStance.Ally    => "아군",
        _                     => "중립",
    };

    // 화면에 적을 한 줄. 숫자만 띄우면 그것이 좋은지 나쁜지 매번 재야 한다.
    public static string Describe(FactionData f)
    {
        float favor = Of(f);
        string s  = favor <= HostileAt ? "적대"
                  : favor >= AllyAt    ? "아군"
                  : favor <= -20f      ? "껄끄럽다"
                  : favor >=  20f      ? "우호적"
                  :                    "무덤덤";
        return $"{f.FactionName}  {s} ({favor:+0;-0;0})";
    }
}
