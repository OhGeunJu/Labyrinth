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
    // 창고 + 이 폰의 배낭.
    public static int Available(ItemData item, UnitBrain carrier)
    {
        if (item == null) return 0;
        int total = Storage.GetTotalByItem(item);
        if (carrier != null && carrier.Inventory != null)
            total += carrier.Inventory.GetTotalQuantity(item);
        return total;
    }

    // 창고 + 플레이어 세력 폰 **전부**의 배낭. 일감을 등록할지 말지 볼 때만 쓴다.
    public static int AvailableAnywhere(ItemData item)
    {
        if (item == null) return 0;
        int total = Storage.GetTotalByItem(item);

        foreach (var f in Faction.All)
        {
            if (f == null || !f.IsPlayer) continue;
            var inv = f.GetComponent<Inventory>();
            if (inv != null) total += inv.GetTotalQuantity(item);
        }
        return total;
    }

    // 배낭 먼저, 그다음 창고. **총량을 먼저 확인해** 부분 차감이 남지 않게 한다 —
    // 절반만 빠지고 실패하면 재료가 조용히 증발한다.
    public static bool Consume(ItemData item, int amount, UnitBrain carrier)
    {
        if (item == null || amount <= 0) return false;
        if (Available(item, carrier) < amount) return false;

        int remaining = amount;

        if (carrier != null && carrier.Inventory != null)
            remaining -= carrier.Inventory.Remove(item, remaining);

        if (remaining > 0) return Storage.ConsumeByItem(item, remaining);
        return true;
    }
}
