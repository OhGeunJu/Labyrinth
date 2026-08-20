using UnityEngine;

// **먹는 일의 규칙은 여기 하나뿐이다.**
//
// 길이 둘이다 — 제 발로 창고에 가서 먹는 것(`EatNode`)과 남이 가져다 먹여 주는 것
// (`FeedJob`). 무엇을 고르고 얼마나 채우는지를 양쪽에 따로 적으면 반드시 어긋난다
// (이 저장소가 `CarryCapacity`에서 이미 3.3배 어긋난 두 벌을 겪었다).
//
// 눈금(감소·임계·바닥 벌칙)은 여기가 아니라 **`Needs`**가 든다. 여기가 답하는 것은
// "지금 있는 것 중 무엇을 먹나" 하나다.
public static class Feeding
{
    public const float Full = Needs.Full;

    // **남을 먹이는 것은 제 손으로 먹는 것보다 오래 걸린다.** 떠먹여야 하고, 갇힌 사람은
    // 대개 받아 먹을 형편이 아니다 — 간수의 시간이 곧 포로를 살려 두는 값이다.
    public const float FeedTimeMult = 2f;

    // 누가 이 축을 도는가 · 임계 · 남은 자리는 **`Needs`가 답한다**(축이 셋이라 여기 적으면
    // 축마다 한 벌씩 생긴다). 부르는 쪽이 두 이름을 외우지 않게 그대로 흘려보낸다.
    public static bool  Simulates(UnitBrain brain)            => Needs.Simulates(brain);
    public static bool  IsHungry(UnitBrain brain) => Needs.IsLow(brain, NeedKind.Hunger);
    public static float Room(float current)       => Needs.Room(current);

    // ── 무엇을 먹나 ──────────────────────────────────────────────────────
    //
    // **낭비가 가장 적은 것을 고른다.** 허기는 100에서 잘리므로(`Eat`의 `Min`), 허기 80인
    // 폰에게 영양 50짜리를 먹이면 30이 그냥 사라진다. 그래서 **넘치지 않는 것 중 가장 큰
    // 것**을 고르고, 전부 넘치면 **가장 작은 것**을 고른다.
    //
    // 규칙 하나가 두 가지를 한다: 낭비가 줄고, **애써 만든 요리가 저절로 아껴진다**
    // (잡곡부터 나가고 좋은 것은 허기가 깊을 때만 쓰인다). 치료가 "가장 좋은 것을 쓴다"인
    // 것과 반대로 보이지만 축이 다르다 — 약은 넘쳐도 버려지지 않는다.
    static bool Better(FoodData candidate, FoodData best, float room)
    {
        if (candidate == null) return false;

        float mine = candidate.NutritionValue;
        if (mine <= 0f) return false;
        if (best == null) return true;

        float theirs  = best.NutritionValue;
        bool  fits    = mine   <= room;
        bool bestFits = theirs <= room;
        if (fits != bestFits) return fits;

        return fits ? mine > theirs    // 둘 다 맞으면 큰 것
                    : mine < theirs;   // 둘 다 넘치면 작은 것
    }

    // **격자 하나를 훑는 규칙은 여기 한 번뿐이다.** 배낭이든 창고든 "이 안에서 무엇을
    // 먹을까"는 같은 물음이라, 따로 적으면 배낭과 창고가 다른 것을 고르게 된다.
    //
    // **잠근 뭉치는 안 먹는다**(`Treatment`와 같은 규칙). 플레이어가 "이건 건드리지 마라"고
    // 못 박은 것을 자동 판단이 조용히 먹으면 잠근 뜻이 없다. 잠긴 것뿐이면 창고로 간다 —
    // 세상의 모든 음식을 잠갔을 때만 굶는다. (창고에는 잠글 수단이 없어 아무 차이가 없다.)
    static FoodData BestIn(Inventory inv, float room)
    {
        if (inv == null) return null;

        FoodData best = null;
        foreach (var inst in inv.Items)
        {
            if (inst == null || inst.Locked || inst.Quantity <= 0) continue;
            if (inst.Data is not FoodData food) continue;
            if (Better(food, best, room)) best = food;
        }
        return best;
    }

    public static FoodData BestInBackpack(UnitBrain pawn, float current)
        => BestIn(pawn != null ? pawn.Inventory : null, Room(current));

    // **가장 가까운 창고**에서, 그 안에 든 것 중 가장 알맞은 것.
    //
    // **어디로 갈지가 먼저, 무엇을 먹을지가 그다음이다.** 낭비를 아끼자고 더 먼 창고까지
    // 걷게 하면 아낀 것보다 걸은 값이 크다 — 낭비는 영양 몇 점이지만 왕복은 그 폰의
    // 시간 전부다. `Better`는 **고른 창고 안에서** 고르는 규칙으로 남는다.
    //
    // 창고를 훑는 것은 `Storage.NearestWhere`가 한다(`FindStorageWithItem`은 아이템 하나만
    // 물을 수 있어 "아무 음식이나"를 못 묻는다 — 그래서 조건을 넘기는 쪽을 쓴다).
    public static FoodData BestInStorage(float current, Vector3 from, out Storage source)
    {
        float room = Room(current);

        source = Storage.NearestWhere(from, s => BestIn(s.Inventory, room) != null);
        return source != null ? BestIn(source.Inventory, room) : null;
    }

    // **먹이러 갈 수 있는 밥이 있는가** — 일감을 걸 값어치가 있는지 볼 때만 쓴다.
    //
    // **창고만 센다.** `MaterialPool.AvailableAnywhere`를 흉내 내 폰들의 배낭까지 셌더니
    // 헛돌았다: `FeedJob`이 쓸 수 있는 것은 **그 폰의 배낭과 창고**뿐인데, 세는 쪽은 **남의
    // 배낭**까지 봤다. 온 콜로니의 밥이 다른 폰 배낭에만 있으면(짐내림이 기본 꺼짐이 되면서
    // 흔해진 상태다) 자리가 1초마다 일감을 내고, 간수가 가서 빈손으로 접고, 다시 나고 —
    // 그동안 포로는 굶는다. 세는 것과 쓰는 것이 어긋나면 일감이 영원히 돈다.
    //
    // 들고 있는 폰이 그대로 가는 경우는 `FeedJob.OnStart`의 `BestInBackpack`이 잡는다 —
    // 그건 일감이 배정된 **뒤**의 이야기라 여기서 볼 것이 아니다.
    //
    // **자리가 얼마나 남았는지는 안 본다**(`Full`) — 있기만 하면 된다.
    public static bool ExistsAnywhere()
    {
        foreach (var storage in Storage.All)
        {
            if (storage == null || !storage.IsActive) continue;
            if (BestIn(storage.Inventory, Full) != null) return true;
        }
        return false;
    }

    // 한 개 뺀다. 못 빼면(그새 없어졌으면) false — 부르는 쪽이 그때 접는다.
    //
    // **격자를 가리지 않는다** — 배낭에서 먹을 수도, 창고 앞에 서서 거기서 바로 먹을 수도
    // 있다(배낭에 자리가 없을 때). 두 길이 같은 함수를 쓰지 않으면 한쪽만 잠금을 존중한다.
    public static bool ConsumeFrom(Inventory inv, FoodData food)
        => food != null && inv != null && inv.RemoveUnlocked(food, 1) > 0;

    public static bool ConsumeFromBackpack(UnitBrain pawn, FoodData food)
        => ConsumeFrom(pawn != null ? pawn.Inventory : null, food);

    // ── 먹는다 ───────────────────────────────────────────────────────────
    //
    // 채우는 곳은 여기 하나다. 넘친 만큼은 버려진다 — `Better`가 그걸 줄이려고 있다.
    //
    public static void Eat(UnitBrain eater, FoodData food)
    {
        var bb = eater != null ? eater.Blackboard : null;
        if (bb == null || food == null) return;

        Needs.Fill(bb, NeedKind.Hunger, food.NutritionValue);
    }

    public static float FeedSeconds(FoodData food)
        => (food != null ? food.EatTime : 2f) * FeedTimeMult;
}
