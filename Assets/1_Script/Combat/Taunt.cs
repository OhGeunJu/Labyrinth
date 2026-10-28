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

    // **지금 도발에 걸린 폰들 — 후보 목록이다**(2026-09-07, 선으로 잇는 표시를 넣으면서).
    //
    // 도발은 폰마다 제 블랙보드(`TauntedBy`/`TauntedUntil`)에 사는데, **그리는 쪽은 "누가
    // 걸려 있나"를 물어야** 한다. 그것 때문에 세계의 모든 폰을 매 프레임 훑게 두면 표시 하나가
    // 폰 수만큼 비싸진다(`LightSource`·`Irrigation`이 같은 이유로 레지스트리다).
    //
    // **여기는 후보만 든다 — 진짜인지는 `HolderOf`가 답한다.** 만료·죽음·기절을 여기서 또
    // 재면 답이 두 벌이 되고, 그때부터 *"선은 그려지는데 안 끌려간다"*가 생긴다.
    static readonly List<UnitBlackboard> _held = new();

    public static IReadOnlyList<UnitBlackboard> Held => _held;

    // 걷어내는 문. **읽는 쪽이 부른다** — 도발은 시간으로 풀리므로 풀리는 순간을 아는
    // 코드가 없고, 그래서 *지울 때*가 아니라 *볼 때* 정리하는 것이 유일하게 새지 않는 길이다.
    public static void Forget(UnitBlackboard bb) => _held.Remove(bb);

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
            if (!_held.Contains(theirs)) _held.Add(theirs);
            taken++;
        }
        return taken;
    }

    // **쓸 만한가 — "쓸 수 있나"와 다른 물음이다**(2026-09-07, 적도 능동을 쓰게 하면서).
    //
    // `AbilityUser.CanUse`는 *쿨다운이 돌았나 · 몸이 성한가*를 답한다. 사람이 누를 때는 그것만
    // 있으면 된다 — **언제 쓸지는 사람이 정한다.** 스스로 도는 폰에게는 그 자리가 비어 있고,
    // 비워 두면 **뜨자마자 허공에 쓴다**(쿨다운은 헛쳐도 돈다 — 그렇게 정해 두었다).
    //
    // **답은 여기 있다 — `AbilityUser`가 아니다.** 거기서 분기하면 능력이 늘 때마다 그 파일이
    // 전투 규칙을 알게 된다(`Apply`를 여기 둔 것과 **같은 이유**).
    //
    // 규칙은 하나다: **끌어올 것이 하나라도 있으면 쓴다.** 범위 안의 적대 중 **나를 안 보고
    // 있는 놈**이 곧 끌어올 것이다 — 이미 전부 나를 노리고 있으면 걸어도 아무것도 안 바뀐다.
    //
    // *"둘 이상일 때만"*으로 문턱을 올리는 길도 있는데 안 올렸다. 쿨다운 25초에 지속 8초라
    // **아껴 두면 싸움이 먼저 끝난다**, 그리고 문턱은 화면에 안 보여서 *"왜 안 쓰지"*가 된다.
    // 표적을 가까운 순으로 고른 것과 같은 취지다 — **예측 가능한 쪽이 1차안이다.**
    //
    // **`_buffer`를 `Apply`와 같이 쓴다** — 재는 것과 거는 것이 이어서 돌지 겹쳐 돌지 않고,
    // `HostilesWithin`이 들어가면서 비운다.
    public static bool Worth(UnitBrain user, AbilityData ability)
    {
        if (user == null || ability == null) return false;

        var bb = user.Blackboard;
        var faction = bb != null ? bb.Faction : null;
        if (faction == null) return false;

        float stat  = user.Stats != null ? user.Stats.Toughness : 0f;
        float range = ability.RangeFor(stat);

        // **거는 쪽과 같은 훑기다** — 눈금을 따로 들면 *"쓸 만하다고 해 놓고 아무도 안 걸린다"*가
        // 생기고, 그건 쿨다운만 태운다.
        if (FactionScan.HostilesWithin(user.gameObject, faction, range, false, _buffer) == 0)
            return false;

        var self = user.gameObject;
        foreach (var go in _buffer)
        {
            var brain  = go.GetComponent<UnitBrain>();
            var theirs = brain != null ? brain.Blackboard : null;
            if (theirs == null) continue;
            if (theirs.Target != self) return true;   // 나를 안 보고 있다 = 끌어올 것이 있다
        }
        return false;
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
