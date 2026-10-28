using System.Collections.Generic;
using UnityEngine;

// **핵이 내는 힘을 건물들이 나눠 쓴다**(2026-09-06, 사용자 결정: *"핵에 에너지 총량을 둬서
// 그 에너지만큼 자원건물이나 충전소 등등 건물들이 그 에너지를 할당받게"*).
//
// ## 왜 이 축이 필요한가
//
// *자연은 찍고 기지는 배정한다*로 갈라 놓고 나니, **기지 쪽에 값이 없었다** — 이식 광맥을
// 몇 개든 심으면 그만큼 무한히 나왔다. 그러면 *굴에 나가는 이유*가 초반 한 번으로 끝난다.
// **힘의 총량이 곧 기지의 크기이고**, 그것을 늘리려면 다시 나가야 한다(핵·부핵).
//
// ## 배분은 순서다 — 저작한 우선순위가 아니다
//
// **먼저 지은 것이 먼저 받는다.** 우선순위를 저작하게 하면 건물마다 숫자를 하나씩 더 적어야
// 하고, 그 숫자는 **화면에 안 보이므로** *"왜 저건 도는데 이건 안 도나"*가 뽑기가 된다.
// 등록 순서는 플레이어가 **직접 만든 순서**라 설명이 필요 없고, 바꾸고 싶으면 **끄면 된다**
// (`PowerUser.SwitchedOn`) — 끄는 순간 그만큼이 뒤엣것에게 간다.
//
// ## 영역 단위다 — 창고·재료와 같은 규칙
//
// 전부 더하면 **16km 밖 부핵의 힘으로 여기 광맥이 돈다.** `Territory.CoreAt`이 답한다.
public static class PowerGrid
{
    static readonly List<PowerUser> _users = new();

    // **바뀌었다는 사실만 알린다** — 무엇이 바뀌었는지는 듣는 쪽이 제 것만 다시 보면 된다
    // (`HaulPolicy`와 같은 모양).
    public static event System.Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _users.Clear(); OnChanged = null; }

    public static void Register(PowerUser u)
    {
        if (u == null || _users.Contains(u)) return;
        _users.Add(u);        // **꼬리에 붙는다** — 그것이 곧 "먼저 지은 것이 먼저"다
        Recompute();
    }

    public static void Unregister(PowerUser u)
    {
        if (u == null || !_users.Remove(u)) return;
        Recompute();
    }

    // ── 총량 ────────────────────────────────────────────────────────────────
    //
    // 핵 하나와 그 영역의 부핵들이 낸다. **부핵을 세는 자리가 여기 하나여야 한다** —
    // 밖에서 또 더하면 화면의 숫자와 실제 배분이 갈린다.
    public static int CapacityOf(DungeonCore core)
    {
        if (core == null) return 0;

        int cap = core.EnergyOutput;
        foreach (var sc in SubCore.All)
            if (sc != null && Territory.Same(sc.transform.position, core)) cap += sc.EnergyOutput;

        return cap;
    }

    public static int DemandOf(DungeonCore core)
    {
        int sum = 0;
        foreach (var u in _users)
            if (u != null && u.SwitchedOn && CoreOf(u) == core) sum += u.Cost;
        return sum;
    }

    public static int UsedOf(DungeonCore core)
    {
        int sum = 0;
        foreach (var u in _users)
            if (u != null && u.Powered && CoreOf(u) == core) sum += u.Cost;
        return sum;
    }

    // ── 배분 ────────────────────────────────────────────────────────────────
    //
    // **핵마다 따로 센다.** 그리고 **끈 것은 아예 안 센다** — 끄는 것이 힘을 돌려주는
    // 유일한 길이라, 껐는데도 자리를 차지하면 그 스위치가 아무것도 안 하는 것이 된다.
    //
    // **영역 밖은 힘을 못 받는다**(핵이 없다) — 휴면(`BuildingDormancy`)과 같은 방향이다.
    static readonly Dictionary<DungeonCore, int> _left = new();

    public static void Recompute()
    {
        _left.Clear();

        bool changed = false;
        foreach (var u in _users)
        {
            if (u == null) continue;

            var core = CoreOf(u);
            bool ok;

            if (core == null || !u.SwitchedOn) ok = false;
            else
            {
                if (!_left.TryGetValue(core, out int rest))
                {
                    rest = CapacityOf(core);
                    _left[core] = rest;
                }

                ok = rest >= u.Cost;
                if (ok) _left[core] = rest - u.Cost;
            }

            if (u.Powered != ok) { u.SetPowered(ok); changed = true; }
        }

        if (changed) OnChanged?.Invoke();
    }

    static DungeonCore CoreOf(PowerUser u)
        => u != null ? Territory.CoreAt(u.transform.position) : null;
}
