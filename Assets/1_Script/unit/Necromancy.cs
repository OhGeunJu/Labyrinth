using System.Collections.Generic;
using UnityEngine;

// **죽은 자를 일으키는 일 한 곳.**
//
// 시체는 저절로 사라지지 않고 **뼈는 영영 남는다**(`CorpseStage`). 그래서 치우는 길을 셋
// 두었는데(도축·매장·화장), 치우지 않아도 아쉬울 것이 없으면 그것들은 잡일 목록일 뿐이다.
// **여기가 그 값을 만든다** — 교단·망자가 쳐들어오면 널브러진 시체가 **그 자리에서** 일어난다.
// 습격이 가장자리에서 걸어 들어오는 것이 아니라 **거점 한복판에서** 시작되므로, 치우는 것이
// 잡일이 아니라 **방어**가 된다.
//
// **새 이벤트를 만들지 않는다.** 기존 습격에 얹으므로 습격 빈도(`EventManager`의 전역 시계)가
// 안 늘고, 밸런스 축이 하나 더 생기지도 않는다.
//
// ── 갈리는 것은 시체의 단계다 ────────────────────────────────────────────────
//
// **살점이 남았으면(신선·썩음) 그 몸이 그대로 일어난다** — 이름·스탯·특성·차고 있던 장비까지
// (`UnitBrain.Rise`). 프리팹이 필요 없고, 무엇보다 **죽은 동료가 내가 입혀 준 갑옷을 입고
// 걸어온다**. **뼈만 남았으면 해골이 선다**(`FactionData.RisenBones`) — 그 모양은 더 이상
// 그 사람이 아니므로 유품은 그 자리에 떨어진다.
//
// ── 누가 일으키나 ────────────────────────────────────────────────────────────
//
// **세력이 답한다**(`FactionData.RaisesDead`). 습격이 오는 길이 둘인데(이벤트 추첨 · 지도에서
// 걸어온 무리) 이벤트 에셋에 적으면 **지도에서 온 교단은 시체를 안 일으킨다** — 플레이어에게
// 그 둘은 똑같이 '교단이 몰려온 것'이다. 실제로 쳐들어온 폰들의 소속을 물으면 두 길이 저절로
// 같아지고, 배선을 하나 빠뜨려 조용히 아무 일도 안 일어나는 자리도 안 생긴다.
public static class Necromancy
{
    // 일어난 몸에 남는 피. 갓 일어난 것이 만신창이면 첫 한 대에 도로 눕는다 — 그러면
    // 시체를 안 치운 값이 안 치러진다. 그렇다고 멀쩡하면 죽었던 것이 아무 뜻이 없다.
    const float RisenBloodRatio = 0.7f;

    // 일어난 몸의 빛깔. 시체가 삭을 때 쓰는 것과 같은 계열의 죽은 색이되, 썩은 갈색보다
    // 창백하다 — 이제 서서 걸어 다니는 것이므로 멀리서도 산 것과 갈려야 한다.
    static readonly Color RisenTint = new(0.62f, 0.68f, 0.66f);

    // ── 진입점 ───────────────────────────────────────────────────────────────
    //
    // **습격이 도착한 자리에서 부른다**(`EventManager`의 두 길 모두). 쳐들어온 폰들의 소속을
    // 훑어 시체를 일으키는 세력이 있으면 그 세력의 것으로 일으킨다.
    public static int OnRaidArrived(IEnumerable<GameObject> arrivals)
    {
        var raiser = RaiserAmong(arrivals);
        return raiser == null ? 0 : RaiseAll(raiser);
    }

    // 쳐들어온 것들 중 죽은 자를 일으키는 세력. **먼저 걸리는 하나**를 쓴다 — 교단과 망자가
    // 섞여 와도 일으키는 규칙은 같고, 나온 것이 누구 편이냐만 갈린다(둘은 서로 아군이다).
    public static FactionData RaiserAmong(IEnumerable<GameObject> arrivals)
    {
        if (arrivals == null) return null;

        foreach (var go in arrivals)
        {
            if (go == null) continue;
            var data = go.TryGetComponent(out Faction faction) ? faction.Data : null;
            if (data != null && data.RaisesDead) return data;
        }
        return null;
    }

    // ── 일으키기 ─────────────────────────────────────────────────────────────
    //
    // **`Corpse.All` 같은 레지스트리를 새로 만들지 않는다**(`ButcherTable`이 시체를 찾는 것과
    // 같은 자리) — 습격이 도착할 때 한 번 도는 일이라 매 틱 도는 훑기가 아니다.
    public static int RaiseAll(FactionData raiser)
    {
        if (raiser == null || !raiser.RaisesDead) return 0;

        var core  = DungeonCore.Instance;
        int risen = 0;

        // **목록을 그대로 돌지 않는다** — 일으키는 동안 `Corpse`가 파괴되거나(해골로 갈릴 때)
        // 새로 생겨 `All`이 바뀐다. 훑는 중에 바뀌면 `InvalidOperationException`이다.
        foreach (var corpse in new List<Corpse>(Corpse.All))
        {
            if (corpse == null) continue;

            var brain = corpse.GetComponent<UnitBrain>();
            if (brain == null || InSomeonesHands(brain, corpse)) continue;

            // **묻힌 것은 무덤을 파는 세력만**(교단). 그것이 무덤에 값을 준다 — 싸고 즉시지만
            // 미루는 것이고, 화장은 값을 치르고 끝내는 것이다. 파낸 뒤에 일으키는 것은
            // 무덤이 알아서 한다(`Exhume`이 흙 위로 올려 세운다).
            if (corpse.Buried)
            {
                if (!raiser.ExhumesGraves) continue;
                Grave.HolderOf(corpse)?.Exhume();
                if (corpse.Buried) continue;   // 무덤을 못 찾았다 — 땅속에 둔다
            }

            var go = corpse.Stage == CorpseStage.Bones
                ? RaiseAsBones(corpse, raiser)
                : RaiseInPlace(corpse, brain, raiser);

            if (go == null) continue;
            risen++;

            // 적 트리는 탐지·배회(12m)뿐이라, 멀리서 일어난 것은 명령이 없으면 그 자리에서
            // 서성이다 끝난다 — 쳐들어온 무리에게 핵으로 걷게 하는 것과 같은 규칙이다.
            if (core != null)
                if (go.TryGetComponent(out PawnMover mover)) mover.SetMoveTarget(core.transform.position);
        }

        if (risen > 0)
        {
            EventNotificationUI.Notify("죽은 자가 일어난다",
                $"{raiser.FactionName}이(가) 시체 {risen}구를 일으켰습니다.");
            Debug.Log($"[Necromancy] {raiser.FactionName} — 시체 {risen}구를 일으켰습니다.");
        }

        return risen;
    }

    // **살점이 남은 몸 — 그 사람이 그대로 일어난다.** 유품을 안 떨군다: 몸이 없어지지 않으므로
    // 지녔던 것은 그대로 그 몸에 있고, 다시 눕히면 그때 시체가 되어 도로 나온다.
    static GameObject RaiseInPlace(Corpse corpse, UnitBrain brain, FactionData raiser)
    {
        // 시체는 기절 자세로 누워 있고 `CharacterController`가 꺼져 있을 수 있다
        // (업어다 내려놓은 자리라면 켜져 있다). 세우는 규약은 하나를 쓴다.
        var at = corpse.transform.position;
        if (!brain.Rise(raiser, RisenBloodRatio)) return null;

        PawnPlacement.PlaceAndEnable(brain.gameObject, at);

        // **물들인다 — 모양이 그대로이기 때문이다.** 일어난 것이 내가 입혀 준 갑옷을 입은
        // 내 폰과 눈으로 구별되지 않으면, 그것이 적이라는 사실을 체력 창을 열어야 안다.
        // 시체가 삭을 때 색으로 알리는 것과 **같은 자리 같은 방법**이다(`Corpse.Tint`).
        Corpse.Tint(brain.gameObject, RisenTint);
        return brain.gameObject;
    }

    // **뼈만 남은 몸 — 해골이 선다.** 모양이 더 이상 그 사람이 아니므로 몸을 없애고 프리팹을
    // 세운다. 몸이 없어지는 길이니 **유품은 `Corpse.SpillBelongings`가 내놓는다**(도축·화장과
    // 같은 규칙) — 안 부르면 차고 있던 무기·갑옷이 몸과 함께 조용히 사라진다.
    static GameObject RaiseAsBones(Corpse corpse, FactionData raiser)
    {
        var prefab = raiser.RisenBones;
        if (prefab == null) return null;   // 뼈를 일으키지 않는 세력 — 그냥 둔다

        var at = corpse.transform.position;
        corpse.SpillBelongings(at);

        var go = PawnSpawn.Instantiate(prefab, at, Quaternion.identity);
        PawnPlacement.PlaceAndEnable(go, at);

        // **일으킨 세력의 것이 된다.** 해골 프리팹은 망자 소속으로 저작돼 있지만, 교단이
        // 일으켰으면 교단의 것이다 — *누가 일으켰는지가 무리 구성에 보인다*.
        if (go.TryGetComponent(out UnitBrain goBrain)) goBrain.ChangeFaction(raiser);

        Object.Destroy(corpse.gameObject);
        return go;
    }

    // **사람 손에 있는 것은 안 일어난다** — 업고 가는 중이거나, 도축대에 눕혀 놓았거나,
    // 화장터에 올려 불을 붙인 것. 그 자리들은 저마다 몸을 붙들고 있어(부모로 매달거나 상판에
    // 눕히거나) 여기서 일으키면 자세·자리·일감이 한꺼번에 어긋난다. 무엇보다 **불 위에서
    // 일어나는 것**은 화장을 값을 치르고 끝내는 길로 둔 취지와 정반대다.
    static bool InSomeonesHands(UnitBrain brain, Corpse corpse)
    {
        if (PawnCarry.IsCarried(brain)) return true;

        foreach (var table in ButcherTable.All)
            if (table != null && table.Laid == brain) return true;

        foreach (var pyre in Pyre.All)
            if (pyre != null && pyre.Burning == corpse) return true;

        return false;
    }
}
