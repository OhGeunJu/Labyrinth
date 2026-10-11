using UnityEngine;

// **돈을 받고 며칠 동안만 싸워 주는 사람.**
//
// 합류(떠돌이 고용)와 **다른 축이다**: 합류는 값을 한 번 치르고 **우리 사람이 되는 것**이라
// 일도 시키고 성장도 하고 영영 남는다. 계약은 **기간을 사는 것**이라 끝나면 간다.
//
// 그래서 값도 다르게 매겨진다 — 합류는 사람 값(`PawnSpec.Price`)이고 계약은 **하루치 × 날수**다.
// 오래 붙들수록 비싸지는 것이 계약이고, 그게 "위기에 잠깐 부른다"를 만든다.
//
// ── 싸움만 한다 — 세력을 안 바꾸기 때문이다 ────────────────────────────────
//
// 고용해도 **`Faction_Mercenary` 그대로**다. 그것 하나로 세 가지가 저절로 된다:
//   · **곁에서 싸운다** — 그 세력은 플레이어와 아군이고 산적·고블린·망자·교단·야수에 적대다.
//     역할이 없고 플레이어도 아니라 `UsesEnemyAi`가 참이라 **적 트리(감지 → 전투)**를 쓴다.
//   · **명령을 못 받는다** — 선택·우클릭이 "플레이어 세력인가"로 거른다.
//   · **잡일을 못 한다** — 명부(P)도 작업 큐도 같은 게이트를 쓴다.
//
// 처음엔 플레이어 세력으로 바꿨다가 **명부에 뜨고 조종까지 됐다**(2026-08-14). 막을 것을
// 하나씩 만드는 것보다 **애초에 우리 편이 아니게 두는 편**이 적게 만들고 덜 어긋난다.
[DisallowMultipleComponent]
public class Hireling : MonoBehaviour
{
    // 계약이 끝나는 시각(`GameTimeManager.Elapsed` 축).
    public float ExpiresAt { get; private set; }

    // 어느 용병단에서 왔나. **세력을 안 바꾸므로 되돌리려고 드는 것이 아니다** —
    // 고용 중에도 `Faction_Mercenary` 그대로이고, 이 값은 **연장 선택지를 띄울지**를
    // 가리는 데 쓴다(`Conversation.AddContract`).
    public FactionData HomeFaction { get; private set; }

    public float DaysLeft =>
        Mathf.Max(0f, (ExpiresAt - GameTimeManager.Elapsed) / GameTimeManager.DayDuration);

    // 일을 시킬 수 없는 사람인가. **`JobManager`가 묻는 단 하나의 질문**이다.
    public static bool IsHired(UnitBrain unit) =>
        unit != null && unit.GetComponent<Hireling>() != null;

    // 계약을 건다. 이미 걸려 있으면 **기간이 늘어난다**(다시 부르는 것이 아니라 연장이다) —
    // 새로 갈아 끼우면 남은 날이 사라져 더 짧게 사는 일이 생긴다.
    public static void Sign(UnitBrain unit, int days, FactionData home)
    {
        if (unit == null || days <= 0) return;

        if (!unit.TryGetComponent(out Hireling h)) h = unit.gameObject.AddComponent<Hireling>();
        float span = days * GameTimeManager.DayDuration;

        h.ExpiresAt   = Mathf.Max(h.ExpiresAt, GameTimeManager.Elapsed) + span;
        h.HomeFaction ??= home;
    }

    void Update()
    {
        if (GameTimeManager.Elapsed < ExpiresAt) return;
        Expire();
    }

    // 계약 만료 — 남이 되어 떠난다.
    //
    // **`Imprisonment.Release`를 쓰지 않는다.** 그건 붙잡혀 있던 사람을 푸는 길이라 도주
    // 성향까지 씌운다 — 용병은 도망치는 것이 아니라 **일이 끝나서 가는 것**이다. 나가는 길만
    // 같은 자를 쓴다(`EventManager.TryFindExit` → `LeaveMapNode`).
    void Expire()
    {
        var brain = GetComponent<UnitBrain>();
        if (brain == null) { Destroy(this); return; }

        string who = brain.UnitName;

        // **세력은 안 건드린다** — 애초에 안 바꿨으므로 되돌릴 것이 없다.
        // `HomeFaction`은 "이 사람이 어느 용병단에서 왔나"를 기억할 뿐이다(연장 판정에 쓴다).
        var bb = brain.Blackboard;
        if (bb != null && EventManager.TryFindExit(transform.position, out var exit))
        {
            bb.LeavingMap  = true;
            bb.LeaveTarget = exit;
        }

        EventNotificationUI.Notify("계약 종료", $"{who}의 고용 기간이 끝나 떠납니다.");

        // **컴포넌트를 뗀다** — 남겨 두면 다시 고용했을 때 만료 시각이 옛것과 섞인다.
        Destroy(this);
    }
}
