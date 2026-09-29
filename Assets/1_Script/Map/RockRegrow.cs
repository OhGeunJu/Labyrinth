using System.Collections.Generic;
using UnityEngine;

// **판 굴은 이틀 뒤 바위로 되돌아온다 — 핵 영역과 버팀목 둘레만 빼고**
// (2026-09-28, 사용자: *"붕괴 시스템이 고장난 거 같아 그냥 붕괴를 만들지 말고 던전핵이나 건설목을 세운곳이
//  아니면 하루뒤? 이틀 뒤에 재생되는 걸로 바꾸는게 좋을 거 같네"* → 이틀 · 버팀목).
//
// ## 무엇이 결정이 되나
//
// 붕괴(09-18)가 하던 일 — *얼마나 팔까에 값을 매긴다* — 을 이것이 이어받는다. 핵 밖으로 판 길은
// **내버려 두면 닫히고**, 남겨 두려면 핵 영역을 넓히거나 **버팀목**(나무)을 세운다. 원정길이 닫히는
// 것은 다시 파면 되는 일이라 폰을 잃는 붕괴보다 벌이 가볍고, 무엇보다 **화면에서 무슨 일이 일어났는지
// 보인다**(붕괴는 굴을 그대로 두고 천장만 없애 화면에 아무 변화가 없었다).
//
// ## 언제 메우나
//
// - 판 순간 시계가 선다(`RockMap.Mine` → `RockMap.Dug`). **이틀 뒤에 한 번 본다.**
// - 그때 **지켜지는 칸이면 이틀을 더 준다** — 핵 영역을 옮기거나 버팀목을 뜯으면 그로부터 **길어야 이틀
//   뒤** 닫힌다. 매번 지키는지를 다시 재지 않으니 판 칸이 수천이어도 값이 거의 안 든다.
// - **그 자리나 바로 옆에 무엇이 있으면 미룬다** — 폰 · 바닥 물건 · 자원 노드 · 채집물 · 시체 · 건물 ·
//   청사진 · 바닥. 메우면 **벽 속에 묻힌다**(폰이면 갇히고, 물건이면 영영 못 찾는다).
//
// **메우는 문은 `RockMap.Refill` 하나다** — 길찾기·그리기에 알리는 것이 거기 있다.
public class RockRegrow : MonoBehaviour
{
    // 판 뒤 다시 메워질 때까지(게임 일). `RockMap.Mine`이 시계를 세울 때 읽는다.
    public const float Days = 2f;

    // 무엇이 있어 미뤘을 때 다시 볼 때까지(게임 초) — 폰은 곧 비킨다.
    const float BlockedRetry = 60f;

    // 한 번에 메우는 칸 수. **한꺼번에 판 굴은 한꺼번에 닫히려 한다** — 칸마다 길찾기를 다시 굽으므로
    // 한 프레임에 몰리면 멈칫한다. 나머지는 다음 박에.
    const int PerPass = 32;

    const float CheckSeconds = 2f;   // 실시간 — 이틀짜리 시계라 촘촘할 필요가 없다
    float _next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => SelfStanding.Create<RockRegrow>("RockRegrow");

    // **이 칸은 판 뒤에도 안 메워지나** — 메우는 쪽과 커서 옆 안내(`MineDesignator`)가 같이 묻는다.
    public static bool Keeps(Vector2Int tile)
    {
        if (RockSupport.IsHeld(tile)) return true;

        var map = MapManager.Instance;
        if (map == null) return false;
        var at = map.TileToWorld(tile);
        foreach (var core in DungeonCore.All)
            if (core != null && core.IsWithinAnyRangeWorld(at)) return true;
        return false;
    }

    static readonly List<Vector2Int>                _due      = new();
    static readonly List<(Vector2Int t, float at)>  _postpone = new();
    static readonly HashSet<Vector2Int>             _built    = new();
    bool _builtReady;

    void Update()
    {
        if (!RockMap.Enabled || Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + CheckSeconds;

        var dug = RockMap.Dug;
        if (dug.Count == 0) return;

        float now = GameTimeManager.Elapsed;
        _due.Clear();
        foreach (var kv in dug)
            if (kv.Value <= now) { _due.Add(kv.Key); if (_due.Count >= PerPass) break; }
        if (_due.Count == 0) return;

        var map = MapManager.Instance;
        if (map == null) return;

        _postpone.Clear();
        _builtReady = false;
        int refilled = 0;

        foreach (var t in _due)
        {
            if (Keeps(t))             { _postpone.Add((t, now + Days * GameTimeManager.DayDuration)); continue; }
            if (Blocked(map, t))      { _postpone.Add((t, now + BlockedRetry)); continue; }
            if (RockMap.Refill(t)) refilled++;
        }

        foreach (var (t, at) in _postpone) RockMap.Postpone(t, at);
    }

    // 메우면 묻히는 것이 있나.
    bool Blocked(MapManager map, Vector2Int t)
    {
        if (map.IsOccupied(t) || map.IsFloored(t)) return true;

        // 건물·청사진의 **발자국 전체** — 점유는 *길을 막나*의 답이라 안 막는 건물과 청사진이 빠진다.
        if (!_builtReady) { CollectBuilt(); _builtReady = true; }
        if (_built.Contains(t)) return true;

        var ground = map.TileToGround(t);

        // **폰은 옆 칸까지 본다** — 그 칸에 딱 서 있지 않아도 몸이 걸쳐 있거나, 막 들어서는 참일 수 있다.
        foreach (var brain in UnitBrain.All)
        {
            if (brain == null) continue;
            var p = brain.transform.position;
            if (new Vector2(p.x - ground.x, p.z - ground.z).sqrMagnitude < 1.5f * 1.5f) return true;
        }

        // 바닥의 것들 — 물리로 묻는다(그 칸 안, 폰 키만큼).
        int n = Physics.OverlapBoxNonAlloc(ground + Vector3.up * 1f, new Vector3(0.45f, 1f, 0.45f),
                                           _hits, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var c = _hits[i];
            if (c == null) continue;
            if (c.GetComponentInParent<DroppedItem>()  != null) return true;
            if (c.GetComponentInParent<ResourceNode>() != null) return true;
            if (c.GetComponentInParent<Forageable>()   != null) return true;
            if (c.GetComponentInParent<Corpse>()       != null) return true;
            if (c.GetComponentInParent<UnitBrain>()    != null) return true;
        }
        return false;
    }

    static readonly Collider[] _hits = new Collider[32];

    static void CollectBuilt()
    {
        _built.Clear();
        foreach (var pb in PlacedBuilding.All)
            if (pb != null && pb.Data != null)
                foreach (var t in PlacedBuilding.FootprintAt(pb.transform.position, pb.Data.Size, pb.transform.rotation))
                    _built.Add(t);
        foreach (var bp in BuildingBlueprint.All)
            if (bp != null)
                foreach (var t in bp.Area) _built.Add(t);
    }
}
