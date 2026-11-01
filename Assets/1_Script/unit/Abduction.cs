using System.Collections.Generic;
using UnityEngine;

// **쓰러진 사람을 굴로 끌고 가 먹는다**(2026-11-01, 사용자: *"고블린이랑 트롤은 습격이오면 플레이어 캐릭들을 납치해서
// 잡아먹는 것도 나쁘지않아보여 … 굳이 습격이 아니더라도 야생에서도"* → *"굴로 끌고 가 며칠에 걸쳐 먹음"* ·
// *"곁에 깨어 있는 내 편이 없을 때"*).
//
// **누가** — 종족이 답한다(`UnitData.Devours` — 고블린 · 트롤). 습격대든 떠도는 무리든 같은 노드다.
// **언제** — 쓰러진 내 폰 둘레 `GuardRadius` 안에 깨어 있는 내 편이 없을 때. 지키고 서 있으면 못 멘다 —
//   싸움에서 졌거나 동료가 달아난 뒤에 일어나는 일이고, 그래서 *곁을 지키는 것*이 막는 길이다.
// **어디로** — 돌아갈 굴(`TryDenOf`): 떠도는 무리는 태어난 자리(`WildSpawner.TryHomeOf`), 습격대는 같은 세력의 가장
//   가까운 소굴(같은 층 · `LairRange` 안). **굴이 없으면 안 멘다** — 메고 갈 곳이 없는 납치는 어디서 끝날지 모른다.
// **그 뒤** — 굴에 기둥을 세우고 묶는다(`CaptivePost` — 갇힘은 한 벌 `Imprisonment.Bind`). 먹히는 것과 탈출은 묶인 사람에게
//   붙는 `Devoured`가 든다(끼니마다 먹는 놈이 와서 부위를 차례로 뜯는다 — `DevourNode`).
// **되찾는 길** — 메고 가는 놈을 쓰러뜨리면 떨어뜨린다(`BodyPartSystem.KnockOut` · `UnitBrain.Die`). 굴에서는 [끌어낸다]
//   (`PawnCarry.TryPickUp`이 갇힘을 푼다 — 우리 편은 늘 업는다)로 데려온다.
public static class Abduction
{
    public const float GuardRadius = 12f;   // 쓰러진 사람 둘레에 깨어 있는 그 편이 있으면 안 멘다(m)
    public const float SeekRange   = 25f;   // 이만큼 안의 쓰러진 사람만 노린다(m)
    public const float ArriveDist  = 2.5f;  // 굴에 닿았다고 치는 거리(m)
    const float LairRange = 400f;           // 습격대가 돌아갈 소굴 — 같은 층 · 이 안(m)

    // 쓰러진 사람 → 메러 가는 놈. 둘이 한 사람에게 달려가 하나는 헛걸음하는 일을 막는다.
    static readonly Dictionary<UnitBrain, UnitBrain> _claims = new();
    // 메고 가는 놈 → 굴. 메는 순간 정해 두고 끝까지 간다(가는 길에 무리 기준점이 움직여도).
    static readonly Dictionary<UnitBrain, Vector3> _hauls = new();

    public static bool Devours(UnitBrain b) => b != null && b.Data != null && b.Data.Devours;

    // ── 굴 ──────────────────────────────────────────────────────────────────

    public static bool TryDenOf(UnitBrain self, out Vector3 den)
    {
        den = default;
        if (self == null) return false;
        if (WildSpawner.TryHomeOf(self, out den)) return true;

        var mine = self.TryGetComponent(out Faction f) ? f.Data : null;
        if (mine == null) return false;

        var   at    = self.transform.position;
        float best  = LairRange * LairRange;
        bool  found = false;
        foreach (var lair in WorldLairs.All)
        {
            if (lair == null || lair.IsCleared || lair.Band == null || lair.Band.Faction != mine) continue;
            var p = new Vector3(lair.Position.x, 0f, lair.Position.y);
            if (!Floors.Same(p, at)) continue;
            float d = (p.x - at.x) * (p.x - at.x) + (p.z - at.z) * (p.z - at.z);
            if (d >= best) continue;
            best = d; den = p; found = true;
        }
        return found && RockMap.TryNearestOpen(den, 12f, out den);
    }

    // ── 먹잇감 ───────────────────────────────────────────────────────────────

    // 쓰러진 내 폰 — 묶이지도 업히지도 않았고 살아 있다.
    public static bool IsPrey(UnitBrain victim)
    {
        if (victim == null || !victim.isActiveAndEnabled || PawnCarry.IsCorpse(victim)) return false;
        var bb = victim.Blackboard;
        if (bb == null || !bb.IsKnockedOut || bb.IsPrisoner || bb.CarriedBy != null) return false;
        return victim.TryGetComponent(out Faction f) && f.IsPlayer;
    }

    // 곁에 깨어 있는 내 편이 있나 — 있으면 못 멘다.
    public static bool Guarded(UnitBrain victim)
    {
        var at = victim.transform.position;
        foreach (var f in Faction.All)
        {
            if (f == null || !f.IsPlayer || !f.TryGetComponent(out UnitBrain b) || b == victim) continue;
            if (!b.isActiveAndEnabled || PawnCarry.IsCorpse(b)) continue;
            var bb = b.Blackboard;
            if (bb == null || bb.IsKnockedOut || bb.IsPrisoner || bb.CarriedBy != null) continue;
            if ((b.transform.position - at).sqrMagnitude <= GuardRadius * GuardRadius) return true;
        }
        return false;
    }

    // 지금 노릴 만한가 — 먹잇감이고 · 지키는 이가 없고 · 남이 먼저 찜하지 않았다.
    public static bool StillPrey(UnitBrain self, UnitBrain victim) =>
        IsPrey(victim) && !Guarded(victim) && (!_claims.TryGetValue(victim, out var by) || by == self || !Alive(by));

    public static UnitBrain FindPrey(UnitBrain self)
    {
        if (self == null || self.Blackboard == null || !TryDenOf(self, out _)) return null;
        var   at   = self.transform.position;
        float best = SeekRange * SeekRange;
        UnitBrain pick = null;
        foreach (var f in Faction.All)
        {
            if (f == null || !f.IsPlayer || !f.TryGetComponent(out UnitBrain v)) continue;
            if (!Floors.Same(v.transform.position, at)) continue;
            float d = (v.transform.position - at).sqrMagnitude;
            if (d >= best || !StillPrey(self, v)) continue;
            if (!Vision.CanSee(self.Blackboard, v.gameObject)) continue;
            best = d; pick = v;
        }
        return pick;
    }

    public static void Claim(UnitBrain self, UnitBrain victim) { if (victim != null) _claims[victim] = self; }

    public static void Unclaim(UnitBrain self)
    {
        _drop.Clear();
        foreach (var kv in _claims) if (kv.Value == self || kv.Key == null || !Alive(kv.Value)) _drop.Add(kv.Key);
        foreach (var k in _drop) _claims.Remove(k);
    }
    static readonly List<UnitBrain> _drop = new();

    static bool Alive(UnitBrain b) =>
        b != null && b.isActiveAndEnabled && b.Blackboard != null && !b.Blackboard.IsKnockedOut && !PawnCarry.IsCorpse(b);

    // ── 메고 가기 ────────────────────────────────────────────────────────────

    // 메었다 — 갈 굴을 정해 둔다. 굴이 없으면 내려놓는다(메기 전에 물었지만 그새 소굴이 사라졌을 수 있다).
    public static bool BeginHaul(UnitBrain self)
    {
        Unclaim(self);
        if (!TryDenOf(self, out var den)) { PawnCarry.PutDown(self); return false; }
        _hauls[self] = den;
        Debug.Log($"[납치] {self.UnitName}이(가) {self.Blackboard.Carrying?.UnitName}을(를) 메고 굴로 간다");
        return true;
    }

    // 지금 메고 가는 굴. 메는 순간을 놓쳤으면(다시 불러온 판 등) 지금 정한다.
    public static bool TryHaulDen(UnitBrain self, out Vector3 den)
    {
        if (_hauls.TryGetValue(self, out den)) return true;
        if (!TryDenOf(self, out den)) return false;
        _hauls[self] = den;
        return true;
    }

    // 굴에 닿았다 — 내려놓고 묶는다. 메고 온 놈은 굴에 남는다(먹는 놈이 곁에 있어야 먹는다).
    public static void Deliver(UnitBrain self, Vector3 den)
    {
        _hauls.Remove(self);
        var victim = self.Blackboard.Carrying;
        PawnCarry.PutDown(self, den);
        if (victim == null || PawnCarry.IsCorpse(victim)) return;

        CaptivePost.Raise(den, self.TryGetComponent(out Faction f) ? f.Data : null).Tie(victim);

        // **습격대에서 빠져 굴에 남는다** — 후퇴 명령을 받으면 먹을 놈 없이 사람만 묶여 남는다.
        RaidLord.Detach(self);
        var bb = self.Blackboard;
        bb.LeavingMap   = false;
        bb.Routed       = false;
        bb.HomePosition = den;
        bb.HomeSet      = true;

        Debug.Log($"[납치] {self.UnitName}이(가) {victim.UnitName}을(를) 굴로 데려왔다");
    }

    public static void ForgetHaul(UnitBrain self) => _hauls.Remove(self);

    // 이 세력이 이 굴 둘레에 누군가를 묶어 두고 있나 — 떠도는 무리는 그동안 굴을 안 떠난다(`WildSpawner`).
    public static bool HoldsCaptive(FactionData faction, Vector3 den, float radius = 20f)
        => CaptivePost.HoldsCaptive(faction, den, radius);
}
