using UnityEngine;

// 재료를 **어디서 세고 어디서 꺼내는가**를 한 곳에서 정한다.
//
// 예전엔 창고 하나뿐이었다. 그런데 상점에서 산 물건이 폰 배낭으로 들어가게 되면서
// **손에 씨앗을 들고도 밭을 못 가는** 상태가 생겼다 — 밭은 창고만 봤기 때문이다.
//
// 규칙:
//  · 세는 곳   — 창고 + **그 일을 할 폰**의 배낭
//  · 꺼내는 곳 — **배낭 먼저**, 그다음 창고 (들고 있는 걸 두고 창고까지 갈 이유가 없다)
//
// **남의 배낭은 안 센다.** B를 조작하는데 A가 들고 있던 재료가 사라지면, 건드린 적 없는 폰의
// 짐이 줄어드는 셈이라 화면으로 설명할 방법이 없다.
//
// 다만 "이 일감을 등록할 가치가 있는가"를 볼 때는 **누가 할지 아직 모른다.** 그때는
// `AvailableAnywhere`로 넓게 보고, 실제 배정에서 `JobBase.CanAssignTo`가 폰별로 다시 거른다 —
// "일감이 준비됐나"와 "이 폰이 할 수 있나"는 원래 다른 축이다.
public static class MaterialPool
{
    // **잠근 뭉치는 재료가 아니다.** 플레이어가 "이건 이 폰 것"이라고 못 박은 것을 제작이
    // 조용히 먹으면 잠근 뜻이 없다 — 짐내림만 막고 소비는 되면, 안 실려 갈 뿐 사라지는 건
    // 똑같다. 창고에 있는 것은 잠글 수단이 없으므로 전량이 재료다(잠금은 배낭 얘기다).
    //
    // 세는 것과 꺼내는 것이 **같은 기준**이어야 한다 — `Available`이 세고 `Consume`이
    // 그보다 넓게 빼면 잠근 것이 빠지고, 좁게 빼면 총량은 맞는데 차감이 실패한다.

    // ── 어느 거점의 재료인가 ────────────────────────────────────────────────
    //
    // 세계가 29km로 이어지면서 거점이 **동시에** 살아 있게 됐다. 전부 더하면 **29km 밖
    // 창고의 목재로 여기서 집을 짓는다**(`Territory` 참조).
    //
    // **이름을 갈라 둔다** — 기본 인자를 두면 부르는 곳(스무 군데다)이 조용히 빠뜨리고,
    // 그 증상은 *"분명히 없는데 지어진다"*라 잡기 어렵다:
    //   · `Available(item, carrier)` — **그 폰이 선 자리**의 거점
    //   · `AvailableNear(item, pos)` — **그 자리**의 거점 (일감·건물이 쓴다)
    //   · `AvailableHere(item)`      — **지금 보는 곳**의 거점 (화면이 쓴다)

    // 지금 보는 곳 — 스트리밍이 켜져 있으면 뷰어, 아니면 조작 중인 핵.
    public static Vector3 Focus =>
        SimLocality.Active                ? SimLocality.Center
      : DungeonCore.Instance != null      ? DungeonCore.Instance.transform.position
      :                                     Vector3.zero;

    // 창고 + 이 폰의 배낭(잠긴 것 제외).
    public static int Available(ItemData item, UnitBrain carrier)
    {
        if (item == null) return 0;
        Vector3 near = carrier != null ? carrier.transform.position : Focus;
        int total = Storage.GetTotalByItem(item, near);
        if (carrier != null && carrier.Inventory != null)
            total += carrier.Inventory.GetUnlockedQuantity(item);
        return total;
    }

    // 그 자리의 창고 + **그 거점에 있는** 플레이어 폰들의 배낭(잠긴 것 제외).
    // 일감을 등록할지 말지 볼 때 쓴다 — 그때는 누가 할지 아직 모른다.
    //
    // **폰도 거점으로 거른다** — 29km 밖에 나가 있는 원정대의 배낭을 여기 재료로 세면
    // 일감이 서고, 정작 그 폰은 오지 않는다.
    public static int AvailableNear(ItemData item, Vector3 near)
    {
        if (item == null) return 0;
        int total = Storage.GetTotalByItem(item, near);

        foreach (var f in Faction.All)
        {
            if (f == null || !f.IsPlayer) continue;
            if (!Territory.Same(f.transform.position, near)) continue;
            var inv = f.GetComponent<Inventory>();
            if (inv != null) total += inv.GetUnlockedQuantity(item);
        }
        return total;
    }

    // 화면이 쓰는 것 — 지금 보는 곳의 거점.
    public static int AvailableHere(ItemData item) => AvailableNear(item, Focus);

    // 배낭 먼저, 그다음 창고. **총량을 먼저 확인해** 부분 차감이 남지 않게 한다 —
    // 절반만 빠지고 실패하면 재료가 조용히 증발한다.
    public static bool Consume(ItemData item, int amount, UnitBrain carrier)
    {
        if (item == null || amount <= 0) return false;
        if (Available(item, carrier) < amount) return false;

        int remaining = amount;

        // 뺄 때도 잠금을 지나친다 — 위에서 안 센 것을 여기서 빼면 잠근 것이 사라진다.
        if (carrier != null && carrier.Inventory != null)
            remaining -= carrier.Inventory.RemoveUnlocked(item, remaining);

        if (remaining > 0)
            return Storage.ConsumeByItem(item, remaining,
                                         carrier != null ? carrier.transform.position : Focus);
        return true;
    }

    // 차감했던 재료를 되돌린다(제작·연구 취소 환불). **창고로 먼저, 바닥은 최후 수단.**
    //
    // 예전엔 곧장 바닥에 떨궜다. 그러면 그 짐이 **다시 운반 일감**이 되어 폰이 나르러 가고,
    // 나르는 동안 재료는 창고 집계에서 빠져 있다 — 같은 작업이 다시 배정되면 "재료 부족"이
    // 되어 조용히 죽는다. 연구가 저절로 풀리면서 폰이 운반만 하던 것이 이 고리였다(2026-08-08).
    //
    // 되돌리는 것은 **원래 있던 곳(창고)으로** 가는 게 맞다 — 취소는 사건이 아니라 무효화다.
    public static void Refund(ItemData item, int amount, UnityEngine.Vector3 dropAt)
    {
        if (item == null || amount <= 0) return;

        int left = amount;
        while (left > 0)
        {
            var storage = Storage.FindNearest(dropAt, item, left);
            if (storage == null) break;

            int added = storage.TryAdd(item, left);
            if (added <= 0) break;      // 자리를 못 찾았다 — 더 돌아도 같은 답이다
            left -= added;
        }

        if (left > 0) DroppedItem.Spawn(item, left, dropAt);
    }
}
