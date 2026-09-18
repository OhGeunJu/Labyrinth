using System.Collections.Generic;
using UnityEngine;

// **무너진 자리에 있던 것은 다친다**
// (2026-09-18 — `RockSupport`가 *언제 무너지나*를 답하고, 여기는 *무너지면 무슨 일이
//  일어나나*를 답한다).
//
// **`RockMap`이 이 일을 하지 않는 이유**: 저쪽이 답하는 물음은 *무엇이 바위인가* 하나다.
// 거기에 피해와 알림을 넣으면 **지형이 전투와 화면을 알게 되고**, 무너짐을 다른 데서
// 일으키고 싶은 날(폭발·지진) 그 코드가 통째로 딸려 온다.
//
// 그래서 저쪽은 **사실만 알리고**(`OnCollapse`) 여기가 듣는다.
public static class CaveIn
{
    // 깔리면 받는 피해. **쓰러뜨리되 죽이지는 않는 값**으로 둔다 — 파다가 한 번 무너진
    // 것으로 폰을 잃으면 *넓게 파 보는 일* 자체를 안 하게 된다(그러면 규칙이 있으나 마나다).
    // 몸의 눈금은 부위 100·혈액 100이고, 40이면 대개 쓰러지고 살아남는다.
    const float Damage = 40f;

    // 칸 한복판에서 이만큼 안에 있으면 깔린 것으로 본다(칸은 1m다).
    const float Reach = 0.7f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        // **두 번 붙지 않게 먼저 뗀다** — 도메인 리로드를 끄면 정적 구독이 살아남는다.
        RockMap.OnCollapse -= OnCollapse;
        RockMap.OnCollapse += OnCollapse;
    }

    static void OnCollapse(List<Vector2Int> tiles)
    {
        if (tiles == null || tiles.Count == 0) return;

        var map = MapManager.Instance;
        if (map == null) return;

        int hurt = 0;

        // **폰을 찾는 것은 그 자리다** — 칸마다 물리 검사를 돌리면 무너진 칸 수만큼
        // 캐스트가 돈다. 폰 목록을 한 번 훑고 칸과 맞춰 보는 편이 싸다(폰은 수십, 칸은 수십).
        foreach (var brain in UnitBrain.All)
        {
            if (brain == null || !brain.isActiveAndEnabled) continue;

            var at = brain.transform.position;
            var t  = map.WorldToTile(at);
            if (!tiles.Contains(t)) continue;

            var c = map.TileToWorld(t);
            if (new Vector2(at.x - c.x, at.z - c.z).sqrMagnitude > Reach * Reach) continue;

            // **몸이 받는다** — 무너진 돌은 둔기다(`DamageType.Blunt`).
            brain.GetComponent<BodyPartSystem>()?.TakeDamage(
                new DamageInfo { Amount = Damage, Type = DamageType.Blunt });
            hurt++;
        }

        // **알린다** — 굴은 그대로 남으므로 **화면에 아무 변화가 없다.** 안 알리면 폰이
        // 왜 쓰러졌는지 알 길이 아예 없다(무너진 자리에 돌을 떨구지 않는 이유: 그러면
        // **무너뜨리는 것이 이득**이 된다).
        string where = tiles.Count == 1 ? "천장 한 칸이" : $"천장 {tiles.Count}칸이";
        string who   = hurt > 0 ? $" — **{hurt}명이 깔렸습니다**" : "";
        Debug.LogWarning($"[붕괴] 받치는 것이 없어 {where} 내려앉았습니다{who}. "
                       + "버팀목을 세우면 그만큼 넓게 팔 수 있습니다.");
    }
}
