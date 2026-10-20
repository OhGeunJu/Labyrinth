using UnityEngine;

// ── 원거리는 거리를 벌리며 쏜다 ─────────────────────────────────────────
//
// 사용자(2026-10-20): *"적이 쫓으면 계속 물러나야 돼 — 어느 정도 거리가 될 때까지. 거리가 되면 쏘기
// 전까지는 물러나면 안 되는 거지. 상대가 나보다 빠르면 그냥 내가 맞는 거고, 느리면 내가 쏘는 거고,
// 똑같으면 평생 튀어 — 지형도 있으니 언젠간 차이가 나겠지."*
//
// 그래서 **빠르기를 따로 셈하지 않는다** — 쫓아오는 동안은 물러나고(`Retreat`), 벌어지면 한 발 쏠 때까지
// 선다(`Hold`). 빠른 적은 따라붙어 사수가 맞고, 느린 적은 벌어져 사수가 쏜다. 시간 상한은 없다.
//
// **물러나는 동안에도 전투는 켠 채다** — 장전은 걸으면서 찬다. 조준만 걸음에 풀리므로
// (`CombatHandler` "간격의 마지막 구간은 조준이다") 서는 순간 곧 쏜다. 끄면 물러날 때마다 장전이 0부터다.
//
// **하나만 남긴다 — 몰리면 제자리에서 쏜다.** 물러날 자리가 없거나(막다른 굴) 걸어도 안 벌어지면
// (다른 폰에 막힘) 그 자리에서 쏜다. 이게 없으면 벽에 붙은 채 쏘지도 않고 자리만 찾는다.
//
// 안 바뀌는 것: 소집한 폰(자리를 지킨다) · 근접 · 건물 표적 · 이동 명령(트리 위라 이긴다).
public static class Kiting
{
    public enum Mode { None, Retreat, Hold }

    public sealed class State
    {
        public Mode    Mode;
        public float   Timer;        // 이번 걸음(자리 하나)을 걸은 시간 — TickDelta로 잰다
        public Vector3 StepFrom;     // 이번 걸음을 시작한 자리 — 벌어졌나를 잰다
        public float   HoldSince;    // 선 순간(Time.time) — 이 뒤에 한 발이 나가야 다시 움직인다
        public float   RetryIn;      // 몰렸을 때 다시 찾기까지
    }

    public static bool Enabled;      // 진단 `[거리]`(`DungeonColony/Debug/Kiting Log`)

    // 이보다 가까우면 물러난다 · 이만큼 벌어지면 선다. 사거리에 비례 — 활 9m면 3.2m · 6.3m.
    const float TooCloseFrac = 0.35f, TooCloseMin = 3f;
    const float WantFrac     = 0.7f;

    const float RepickAfter = 1.5f;  // 걷는 동안 이만큼마다 자리를 다시 고른다(적이 돌아 들어온다)
    const float MinGain     = 0.5f;  // 그동안 이만큼도 못 움직였으면 막힌 것이다
    const float CorneredFor = 2f;    // 몰리면 이만큼 제자리에서 쏜 뒤 다시 찾는다

    static readonly float[] Angles = { 0f, 45f, -45f, 90f, -90f };   // 표적 반대쪽이 먼저

    // **이 싸움에 거리 유지를 쓰나.**
    public static bool Applies(UnitBlackboard bb, GameObject target) =>
        bb.RangedWeapon && !bb.IsDrafted && bb.Mover != null
        && target != null && target.TryGetComponent(out UnitBrain _);

    // 사거리 안에서 부른다. **참이면 이번 틱은 이것이 다룬다**(걷는 중) — 거짓이면 평소대로 서서 쏜다.
    public static bool Tick(UnitBlackboard bb, float dist)
    {
        var s = bb.Kite;
        float range = bb.AttackRange;
        float tooClose = Mathf.Max(TooCloseMin, range * TooCloseFrac);
        float want     = range * WantFrac;

        if (s.RetryIn > 0f) s.RetryIn -= bb.TickDelta;

        switch (s.Mode)
        {
            case Mode.Hold:
                // 한 발이 나갔으면 풀린다 — 그때도 붙어 있으면 아래에서 다시 물러난다.
                if (bb.Combat.LastStrikeTime < s.HoldSince) return false;
                s.Mode = Mode.None;
                Log(bb, $"쐈다 · {dist:0.0}m");
                break;

            case Mode.Retreat:
                if (dist >= want)
                {
                    Stand(bb, s);
                    Log(bb, $"벌어졌다 {dist:0.0}m — 쏠 때까지 선다");
                    return false;
                }
                s.Timer += bb.TickDelta;
                if (bb.Mover.HasTarget() && s.Timer < RepickAfter) return true;

                // 걸음 하나가 끝났다(닿았거나 시간이 됐다) — 막혀서 못 걸었으면 몰린 것이다.
                if (Flat(bb.Self.transform.position - s.StepFrom) < MinGain)
                {
                    Cornered(bb, s, "걸어도 안 벌어진다");
                    return false;
                }
                return Step(bb, s, want, dist);
        }

        if (dist >= tooClose || s.RetryIn > 0f) return false;
        return Step(bb, s, want, dist);
    }

    // 표적에서 멀어지는 자리 하나를 골라 걷기 시작한다. 없으면 몰렸다.
    static bool Step(UnitBlackboard bb, State s, float want, float dist)
    {
        if (!TryPick(bb, want, dist, out var spot))
        {
            Cornered(bb, s, "물러날 자리가 없다");
            return false;
        }
        if (s.Mode != Mode.Retreat) Log(bb, $"붙었다 {dist:0.0}m — 물러난다");
        s.Mode     = Mode.Retreat;
        s.Timer    = 0f;
        s.StepFrom = bb.Self.transform.position;
        bb.Mover.SetMoveTarget(spot);
        return true;
    }

    static void Stand(UnitBlackboard bb, State s)
    {
        s.Mode      = Mode.Hold;
        s.HoldSince = Time.time;
        bb.Mover.StopMovement();
    }

    static void Cornered(UnitBlackboard bb, State s, string why)
    {
        Log(bb, $"몰렸다({why}) — 제자리에서 쏜다");
        s.Mode    = Mode.None;
        s.RetryIn = CorneredFor;
        bb.Mover.StopMovement();
    }

    // **어디로 물러나나** — 표적 반대쪽부터 ±45° · ±90°. 설 수 있고(`FindWorkableNear` — 같은 높이),
    // 거기까지 곧게 뚫렸고(`LineIsClear` — 바위·얇은 벽), 지금보다 표적에서 멀고, 거기서 표적이 보이는 자리.
    // 곧게 뚫린 자리만 고르는 이유: 돌아가야 하는 자리는 걷는 동안 표적 쪽으로 다가갈 수 있다.
    static bool TryPick(UnitBlackboard bb, float want, float dist, out Vector3 spot)
    {
        spot = default;
        var map = MapManager.GetForPosition(bb.Self.transform.position);
        if (map == null) return false;

        Vector3 me = bb.Self.transform.position, foe = bb.Target.transform.position;
        Vector3 away = me - foe; away.y = 0f;
        if (away.sqrMagnitude < 1e-4f) away = -bb.Self.transform.forward;
        away.Normalize();

        float step = Mathf.Max(2f, want - dist + 1f);
        var from = map.WorldToTile(me);
        int probe = Mathf.CeilToInt(step) + 4;

        foreach (float a in Angles)
        {
            var p = me + Quaternion.Euler(0f, a, 0f) * away * step;
            var t = map.FindWorkableNear(map.WorldToTile(p), 2);
            if (t == from || !map.IsWorkable(t)) continue;
            if (!map.LineIsClear(from, t, probe)) continue;

            var w = map.TileToGround(t);
            if (Flat(w - foe) <= dist + MinGain) continue;     // 옆으로 비켜서기만 하는 자리
            if (!Vision.SightClear(w, foe)) continue;          // 숨어 버리면 못 쏜다

            spot = w;
            return true;
        }
        return false;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // 싸움이 끝나거나 사거리를 벗어나면 지운다 — 다음 싸움에 새지 않게.
    public static void Reset(UnitBlackboard bb)
    {
        if (bb.Kite.Mode == Mode.None && bb.Kite.RetryIn <= 0f) return;
        bb.Kite.Mode    = Mode.None;
        bb.Kite.RetryIn = 0f;
    }

    static void Log(UnitBlackboard bb, string msg)
    {
        if (!Enabled || bb?.Self == null) return;
        Debug.Log($"[거리] {Perception.NameOf(bb.Self)}: {msg}");
    }
}
