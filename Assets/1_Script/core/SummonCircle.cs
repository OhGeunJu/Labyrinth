using UnityEngine;

// **소환진 — 하수인을 부르는 자리는 핵이 아니라 건물이다**(2026-09-29, 사용자: *"던전 코어에 있는 소환을 다른 건축물로
//  새로 만들고 배치를 핵을 누르면 열리는 걸로"*).
//
// 핵을 누르면 **배치 창**이 열린다(`WorkPostsUI`). 소환은 이 건물을 눌러 연다 — 창은 그대로 `DungeonCoreUI`이고
// **어느 핵의 하수인인가는 자리가 답한다**(이 소환진을 덮는 영역의 핵). 한도 · 비용 · 목록은 그 핵이 든다.
// **하수인은 소환진 곁에 선다** — 핵 곁이 아니다(부른 자리에서 나와야 무엇을 눌렀는지와 결과가 한 자리에 선다).
//
// **휴면이면 안 열린다** — 영역 밖 건물은 전면 사용 불가다(`BuildingDormancy`). 클릭은 사라지지 않고 다음 처리로
// 넘어간다(`IClickGate` — 건물 정보가 뜬다).
[DisallowMultipleComponent]
public class SummonCircle : MonoBehaviour, IWorldClickable, IClickGate
{
    public bool CanClick => !BuildingDormancy.IsDormantAt(this) && CoreHere() != null;

    public void OnWorldClick()
    {
        var core = CoreHere();
        if (core == null) return;
        DungeonCoreUI.OpenFor(core, SpawnPoint());
    }

    // 이 자리를 덮는 핵. 여럿이면 가까운 것(부핵 영역은 제 본핵이 답한다 — `IsWithinAnyRangeWorld`).
    DungeonCore CoreHere()
    {
        DungeonCore best = null;
        float bestD = float.MaxValue;
        var at = transform.position;
        foreach (var c in DungeonCore.All)
        {
            if (c == null || !c.IsWithinAnyRangeWorld(at)) continue;
            float d = (c.transform.position - at).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    // 소환진 둘레의 설 자리 — 벽 속이면 가장 가까운 트인 칸(`RockMap.TryNearestOpen`).
    Vector3 SpawnPoint()
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        var p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2f;
        if (RockMap.TryNearestOpen(p, 6f, out var open)) p = open;
        var map = MapManager.Instance;
        p.y = map != null ? map.SampleGroundHeight(p.x, p.z) : p.y;
        return p;
    }
}
