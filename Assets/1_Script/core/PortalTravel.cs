using System.Collections.Generic;
using UnityEngine;

// **핵은 포탈이다**(2026-10-01, 사용자: *"핵은 포탈 기능을 하게 바꿔도 좋을 거 같아"*). 내 핵에서 내 다른 핵으로 —
// 폰을 핵 우클릭 [포탈 - N 거점으로]로 보내면 **걸어서 이 핵에 닿은 순간** 저 핵 옆에 선다. 상단도 같은 문을 지난다
// (`Caravan` — 거기서는 추상으로).
//
// **닿아야 넘어간다** — 누르는 즉시 옮기면 *"핵 둘만 있으면 세계 어디든 즉시"*가 되어 걷는 값이 사라진다. 핵까지는 걸어야
// 한다(거점에서 나가는 길이 곧 포탈까지 가는 길이다).
//
// 다른 명령이 덮으면(이동 명령의 `ManualMove`가 내려가면) 그 폰의 포탈 길은 거둔다 — 명령이 명령을 덮는 규칙 그대로.
public class PortalTravel : MonoBehaviour
{
    const float EnterRadius = 3.5f;   // 핵 칸(들) 옆에서 멈춘 자리까지 — 3×3 발자국 가장자리 + 몸

    static PortalTravel _inst;
    readonly Dictionary<UnitBrain, (Transform from, Transform to)> _going = new();
    readonly List<UnitBrain> _done = new();
    bool _followed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => _inst = SelfStanding.Create<PortalTravel>("PortalTravel");

    public static bool Send(IEnumerable<UnitBrain> pawns, DungeonCore from, DungeonCore to) =>
        from != null && to != null && Send(pawns, from.transform, to.transform);

    // **층 통로도 같은 문이다**(`FloorPassage`) — 걸어서 닿으면 짝 통로 옆에 선다.
    public static bool Send(IEnumerable<UnitBrain> pawns, FloorPassage from) =>
        from != null && from.Target != null && Send(pawns, from.transform, from.Target.transform);

    // **이 문과 같은 층에 선 폰인가.** 길찾기는 층이 다르면 길을 안 낸다(층을 넘는 길은 통로·포탈뿐이다) —
    // 다른 층의 폰을 보내면 성공처럼 받아 놓고 길이 0이라 곧 이동이 풀려 **아무 알림 없이 명령이 사라진다.**
    // 메뉴(`MoveController`)도 이것으로 보낼 폰을 거른다 — 둘이 따로 재면 *"메뉴엔 떴는데 안 간다"*가 생긴다.
    public static bool CanReach(UnitBrain b, Transform from) =>
        b != null && from != null && Floors.Same(b.transform.position, from.position);

    public static bool Send(IEnumerable<UnitBrain> pawns, Transform from, Transform to)
    {
        if (_inst == null || from == null || to == null || from == to) return false;
        bool any = false;
        foreach (var b in pawns)
        {
            if (b == null || !b.CanObeyNow || !CanReach(b, from)) continue;
            var bb = b.Blackboard;
            bb.Target     = null;
            bb.ManualMove = true;
            b.AbandonWork();
            if (b.TryGetComponent(out CombatHandler combat)) combat.ExitCombat();
            if (b.TryGetComponent(out PawnMover bPawnMover)) bPawnMover.SetMoveTarget(from.transform.position);
            _inst._going[b] = (from, to);
            any = true;
        }
        return any;
    }

    void Update()
    {
        if (_going.Count == 0) return;
        _done.Clear();
        _followed = false;

        foreach (var kv in _going)
        {
            var b = kv.Key;
            var (from, to) = kv.Value;
            if (b == null || from == null || to == null || !b.isActiveAndEnabled) { _done.Add(b); continue; }
            if (b.Blackboard == null) { _done.Add(b); continue; }

            // **닿았나를 먼저 본다** — 핵 칸은 막혀 있어 폰은 그 옆에서 멈추고, 멈추는 순간 이동 명령이 내려간다.
            // 명령이 내려간 것을 먼저 보면 핵 바로 옆에 와서 *"다른 명령이 덮었다"*로 읽힌다.
            var p = b.transform.position; var c = from.transform.position;
            if ((new Vector2(p.x - c.x, p.z - c.z)).sqrMagnitude > EnterRadius * EnterRadius)
            {
                if (!b.Blackboard.ManualMove) _done.Add(b);   // 닿기 전에 다른 명령이 덮었다
                continue;
            }

            // 넘어간다 — 저쪽 둘레 빈자리에.
            var dst = to.position;
            float a = Random.value * Mathf.PI * 2f;
            var spot = dst + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.4f;
            if (RockMap.IsRock(spot)) RockMap.TryNearestOpen(spot, 6f, out spot);
            if (b.TryGetComponent(out PawnMover mover)) mover.StopMovement();
            PawnPlacement.PlaceOnGround(b.gameObject, spot);
            b.Blackboard.ManualMove = false;
            _done.Add(b);

            // **층을 넘었고 지금 그 층을 보고 있었으면 카메라가 따라간다** — 층을 옮기는 것이 곧 카메라가 다른 사분면으로
            // 가는 것이다. 한 번만(여럿이 같은 프레임에 넘어도).
            if (!_followed && !Floors.Same(c, dst) && Floors.Current == Floors.At(c))
            {
                _followed = true;
                CameraController.Active?.FocusOn(spot);
            }
        }

        foreach (var b in _done) _going.Remove(b);
    }
}
