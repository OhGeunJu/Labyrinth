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
