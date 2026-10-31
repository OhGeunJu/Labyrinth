using System.Collections.Generic;
using UnityEngine;

// **몸집 — 큰 종족은 보이는 몸과 팔이 크다**(2026-10-28, 사용자: *"오크 1.25배 큰 크기? 오우거 트롤 2배 큰 크기"*).
//
// 값은 종족이 든다(`UnitData.BodyScale`). 여기는 그 값이 닿는 자리를 한 벌로 모은다:
//   · **보이는 몸** — `Model` 자식의 크기를 종족 값으로 **맞춘다**(`Apply` — 곱하지 않는다). 루트를 키우면 컨트롤러·콜라이더·
//     머리 위 표식까지 같이 커진다. 프리팹도 같은 값으로 구워 둔다(`Pawn/9` — 에디터에서 큰 몸으로 보이게 · 10-31).
//   · **팔 길이** — 근접 사거리에 얹는다(`Reach` → `UnitBlackboard.AttackRange`).
//   · **몸 둘레** — 큰 표적은 중심에서 재면 배 속까지 걸어 들어온다. 거리를 잴 때 빼고(`BTTargetUtil.DistanceTo`)
//     둘러설 자리를 그만큼 넓힌다(`MeleeSlots`).
//
// **부딪히는 몸은 그대로 한 칸이다**(`PawnMover.BodyRadius`) — 굵히면 1m 굴에 끼고, 길찾기는 한 칸 몸으로 굽는다.
// 큰 몸이 좁은 굴에서 벽에 조금 묻혀 보이는 것은 그 값이다.
//
// ── 휘두르기 ─────────────────────────────────────────────────────────────────
//
// 사용자: *"큰 캐릭들이 범위가 아닌 단일 공격만 하는 것은 어색함 하지만 범위 공격만 하면 안됌"*. 그래서 **둘 다**다 —
// 표적은 온전히 맞고, 같은 휘두름에 걸린 둘레의 적(`UnitData.CleaveTargets`)이 몫(`CleaveDamage`)만큼 덜 맞는다.
// 휩쓸린 쪽도 막고 피할 수 있다(`CombatHandler.Sweep`).
public static class BodySize
{
    // 1배를 넘는 만큼 몸 반지름이 는다(m). 2배 몸이면 0.45m 더 굵다 — 사람 몸(0.35)보다 조금 더.
    const float GirthPerScale = 0.45f;

    // 1배를 넘는 만큼 팔이 는다(m). 2배 몸이면 근접 사거리가 1m 늘어 둘레의 적까지 닿는다.
    const float ReachPerScale = 1.0f;

    // 휘두르기가 쓰는 앞쪽 부채꼴의 반각(도). 등 뒤의 놈까지 치면 휘두르기가 아니라 회오리다.
    public const float SweepHalfAngle = 75f;

    public static float ScaleOf(UnitData d) => d != null ? d.BodyScale : 1f;

    public static float ScaleOf(GameObject go) =>
        go != null && go.TryGetComponent(out UnitBrain b) ? ScaleOf(b.Data) : 1f;

    // 사람보다 굵은 만큼(m). 건물·사람이면 0.
    public static float Girth(GameObject go) => Mathf.Max(0f, ScaleOf(go) - 1f) * GirthPerScale;

    // 사람보다 긴 팔(m).
    public static float Reach(UnitData d) => Mathf.Max(0f, ScaleOf(d) - 1f) * ReachPerScale;

    // **`UnitBrain.Awake`에서 한 번** — 곱하지 않고 값을 넣는다. 프리팹이 이미 키워 두었어도(`Pawn/9`) 두 번 걸리지 않고,
    // 종족 값을 고친 뒤 프리팹을 다시 굽지 않아도 판에서는 종족 값을 따른다.
    // 큰 종족의 `Model`은 1배 기준으로 지어진다(사람 몸을 베낀다) — 다른 크기로 지은 모델을 큰 종족에 쓰면 그 크기는 덮인다.
    public static void Apply(GameObject go, UnitData d)
    {
        float s = ScaleOf(d);
        if (Mathf.Approximately(s, 1f) || go == null) return;

        var model = go.transform.Find("Model");
        if (model == null)
        {
            Debug.LogWarning($"[몸집] {go.name}에 `Model` 자식이 없다 — {s}배를 못 입혔다.", go);
            return;
        }
        model.localScale = Vector3.one * s;
    }

    // 휘두르기의 그릇 — 한 판에 한 벌을 돌려 쓴다(`FactionScan.HostilesWithin`이 담을 그릇을 받는 이유와 같다).
    internal static readonly List<GameObject> Swept = new();
}
