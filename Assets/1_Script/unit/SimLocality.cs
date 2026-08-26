using UnityEngine;

// **세계가 29km면 전부 시뮬할 수 없다**(2026-08-26).
//
// 필드맵이 200m 한 장이던 시절에는 *"맵에 있는 것은 다 돈다"*가 공짜였다. 이어진 세계에서는
// 지도 반대편의 고블린 무리가 BT를 돌리고 길을 찾는다 — **화면에 아무 영향도 없는데
// 프레임만 먹는다.**
//
// **켄시도 전부 시뮬하지 않는다.** 플레이어 주변만 실제로 돌리고 나머지는 추상화한다. 이 게임의
// `SettlementManager`(활성 하나 + 나머지 오프라인 추상)가 이미 같은 생각이고, 갈리는 것은
// **경계가 보이느냐**뿐이었다.
//
// ── 가르는 축은 거리가 아니라 소속이다 ────────────────────────────────────
//
// **플레이어 세력은 언제나 돈다.** 수가 적고(수십), 무엇보다 **원정 나간 폰이 얼어붙으면 안
// 된다** — 지도 반대편에 보낸 팀이 멈춰 서 있으면 그건 최적화가 아니라 고장이다.
//
// **그 밖(적·NPC·야생)은 활성 반경 안에서만 돈다.** 수가 많고(장소마다 주둔지가 서면 수백),
// 멀리서 배회하는 것은 화면에 안 나타난다. 가까이 가면 그때부터 돈다.
public static class SimLocality
{
    // **꺼져 있으면 전부 돈다.** 절차 지형을 안 쓰는 씬(손으로 만든 200m 한 장)은 예전 그대로여야
    // 한다 — 거기서 폰을 얼리면 이유 없이 고장 난 것이 된다. `TerrainStreamer`가 켜 준다.
    public static bool Active;

    public static float Radius = 1200f;

    // 활성의 중심 — 보는 곳. `TerrainStreamer`가 매 프레임 넣는다(청크 반경과 같은 축이라
    // **지형이 서 있는 곳 = 폰이 도는 곳**이 저절로 맞는다).
    public static Vector3 Center;

    public static bool ShouldTick(UnitBrain brain)
    {
        if (!Active || brain == null) return true;

        // **플레이어 세력은 거리를 안 본다.** `Faction.IsPlayer`가 판정의 단일 출처다.
        var f = brain.Blackboard?.Faction;
        if (f == null || f.IsPlayer) return true;

        float r2 = Radius * Radius;
        return (brain.transform.position - Center).sqrMagnitude <= r2;
    }

    // 일감이 이 폰에게 너무 먼가. **거리 컷오프가 없으면 지도 반대편 일감을 집는다** —
    // `JobManager`가 점수를 먼저 보고 거리를 동점 처리에만 쓰기 때문이다(200m 맵에서는
    // 그 차이가 안 보였다).
    public static float MaxJobDistance = 600f;

    public static bool JobTooFar(Vector3 from, Vector3 site)
    {
        if (!Active || MaxJobDistance <= 0f) return false;
        return (site - from).sqrMagnitude > MaxJobDistance * MaxJobDistance;
    }
}
