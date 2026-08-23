using System.Collections.Generic;
using UnityEngine;

// **도발은 새 전투 규칙이 아니다.** 표적 선정이 `DetectNode` 한 곳이라, 도발은 **그 답을
// 덮어쓰는 것** 하나다 — `FinishTarget`이 자동 표적 규칙에 예외를 뚫은 것과 **같은 자리**다.
//
// 무엇을 하는지는 여기 한 곳이 답한다. 거는 쪽(`AbilityUser`)과 읽는 쪽(`DetectNode`)이
// 각자 규칙을 적으면 **"도발했는데 안 온다"**가 생기고 그 원인은 화면에 안 남는다.
public static class Taunt
{
    static readonly List<GameObject> _buffer = new();

    // 거는 쪽 — 범위 안의 적대 중 **가장 가까운 N명**의 표적을 나로 돌린다.
    //
    // **"가장 위험한 N명"이 아니라 가까운 N명이다**(설계 1차안). 가까운 쪽이 **예측 가능**하고,
    // 위험도를 재려면 그 정의가 또 하나 늘어난다(전력? 남은 체력? 나를 노리는 중인가?).
    public static int Apply(UnitBrain user, AbilityData ability)
    {
        if (user == null || ability == null) return 0;

        var bb = user.Blackboard;
        var faction = bb != null ? bb.Faction : null;
        if (faction == null) return 0;

        float stat  = user.Stats != null ? user.Stats.Toughness : 0f;
        float range = ability.RangeFor(stat);
        int   want  = ability.TargetsFor(stat);
        float until = Time.time + ability.Duration;

        // 훑는 일은 `FactionScan`이 한다 — 탐지·도주와 **같은 한 벌**이라 "누가 적인가"의
        // 답이 하나뿐이다(쓰러진 놈은 거기서 이미 빠진다 — 도발은 *때리러 오게 하는 것*이라
        // 못 오는 상대에게 걸면 인원만 축낸다).
        //
        // **그릇을 돌려 쓴다** — 도발은 누를 때만 도는데도, 새 리스트를 만드는 습관이 이
        // 저장소에서 세 번 문제가 됐다.
        if (FactionScan.HostilesWithin(user.gameObject, faction, range, false, _buffer) == 0)
            return 0;

        Vector3 from = user.transform.position;
        _buffer.Sort((a, b) =>
            (a.transform.position - from).sqrMagnitude
            .CompareTo((b.transform.position - from).sqrMagnitude));

        int taken = 0;
        foreach (var go in _buffer)
        {
            if (taken >= want) break;

            var brain = go.GetComponent<UnitBrain>();
            var theirs = brain != null ? brain.Blackboard : null;
            if (theirs == null) continue;

            theirs.TauntedBy    = user.gameObject;
            theirs.TauntedUntil = until;
            theirs.Target       = user.gameObject;   // 다음 틱을 기다리지 않고 바로 돌아선다
            taken++;
        }
        return taken;
    }

    // 읽는 쪽 — 지금 이 폰이 누구에게 붙들려 있나. 없으면 `null`.
    //
    // **때린 자가 사라지면 저절로 풀린다** — 죽거나 쓰러지거나 시간이 지나면. 따로 지우는
    // 코드를 두면 그 경로 중 하나를 반드시 빠뜨린다.
    public static GameObject HolderOf(UnitBlackboard bb)
    {
        if (bb == null || bb.TauntedBy == null) return null;
        if (Time.time >= bb.TauntedUntil) { bb.TauntedBy = null; return null; }

        if (!BTTargetUtil.IsAttackable(bb.TauntedBy)) { bb.TauntedBy = null; return null; }
        return bb.TauntedBy;
    }
}
