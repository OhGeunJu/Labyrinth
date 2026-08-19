using UnityEngine;

// **치료의 규칙은 여기 하나뿐이다.**
//
// 치료하는 길이 둘이다 — 의사가 와서 하는 것(`TreatJob`)과 누워서 스스로 하는 것(`RestNode`).
// 회복량·속도·약 고르기를 양쪽에 따로 적으면 반드시 어긋난다(이 저장소가 `CarryCapacity`에서
// 이미 3.3배 어긋난 두 벌을 겪었다).
public static class Treatment
{
    // ── 맨손 ─────────────────────────────────────────────────────────────
    // **도구가 없어도 치료는 된다.** 요구하면 물자가 떨어진 순간 치료가 통째로 멎는데,
    // 그건 사람이 손을 놓는 것과 다르다. 대신 느리고 덜 듣는다.
    public const float BareHandsPotency = 0.4f;
    public const float BareHandsSpeed   = 0.55f;

    public const float BaseSeconds  = 8f;    // 의료 0·붕대 기준 한 회차
    // **부위 회복만 몸의 눈금과 같이 올렸다**(부위 ×3, `BodyPartSystem`) — 안 올리면 붕대 한
    // 번이 긁힌 자국만큼도 못 메워, 약을 쓰는 자리 자체가 사라진다. **혈액 상한은 100 그대로라
    // 예전 값 그대로다.**
    public const float BasePartHeal = 38f;   // 한 회차 부위 HP 회복(배율 곱하기 전)
    public const float BaseCapHeal  = 8f;    // 한 회차 혈액 상한 회복

    // **스스로 하는 것은 덜 듣는다.** 한 손으로, 안 보이는 곳을, 아픈 채로 싸맨다 —
    // 그리고 이 값이 없으면 의사를 둘 이유가 없어진다(자가치유가 늘 켜져 있으므로).
    public const float SelfPenalty = 0.7f;

    // 의료 0이면 절반, 130이면 1.8배.
    public static float SkillMult(float medicine) => 0.5f + medicine / 100f;

    public static float Seconds(MedicineData med, float medicine, bool self)
    {
        float speed = (med != null ? med.TreatSpeed : BareHandsSpeed) * SkillMult(medicine);
        if (self) speed *= SelfPenalty;
        return BaseSeconds / Mathf.Max(0.15f, speed);
    }

    public static float Power(MedicineData med, float medicine, bool self)
    {
        float p = (med != null ? med.Potency : BareHandsPotency) * SkillMult(medicine);
        return self ? p * SelfPenalty : p;
    }

    // 한 회차. 약은 **부르는 쪽이 이미 소모한 뒤**에 넘긴다 — 여기서 빼면 자가치유와 의사가
    // 서로 다른 인벤토리를 봐야 해서 인자가 늘고, 결국 판단이 두 곳으로 갈린다.
    public static void Apply(BodyPartSystem body, float power)
    {
        if (body == null) return;
        body.Treat(BasePartHeal * power, BaseCapHeal * power);
    }

    // ── 약 고르기 ────────────────────────────────────────────────────────
    //
    // **배낭에 든 것만 쓸 수 있다.** 창고에 있는 것을 원격으로 소모하면 붕대가 손에 없는데
    // 감기는 꼴이 된다 — 의사는 챙겨서 가야 한다(`TreatJob`의 수거 단계).
    //
    // **가장 좋은 것을 쓴다.** 아껴 쓰는 규칙(나쁜 것부터)도 생각했지만, 그러면 애써 만든
    // 구급낭이 영영 상자에 남는다. 무엇을 쓸지는 **무엇을 쟁여 두느냐**로 이미 조절된다.
    public static MedicineData BestInBackpack(UnitBrain pawn)
    {
        var inv = pawn != null ? pawn.Inventory : null;
        if (inv == null) return null;

        MedicineData best = null;
        foreach (var inst in inv.Items)
        {
            if (inst?.Data is not MedicineData med) continue;
            if (inst.Locked) continue;   // 잠근 것은 게임이 알아서 처분하지 않는다
            if (best == null || med.Potency > best.Potency) best = med;
        }
        return best;
    }

    // 창고에 있는 것 중 가장 좋은 것과, 그것이 든 **가장 가까운** 창고.
    //
    // **여기서는 효능이 거리를 이긴다** — 밥(`Feeding.BestInStorage`)은 반대로 가까운 창고를
    // 먼저 고르는데, 축이 다르다: 음식은 어느 것을 먹든 허기가 차고 하루 두 끼씩 모두가
    // 하는 일이라 왕복이 값이지만, 약은 효능 차가 크고(맨손 0.4 ↔ 구급낭 2.3) 드물게 쓴다.
    // 구급낭을 두고 옆 창고의 넝마를 감으면 그게 더 큰 손해다.
    //
    // 거리는 **같은 약이 여러 창고에 있을 때** 갈린다(`FindStorageWithItem`이 가까운 쪽을 준다).
    public static MedicineData BestInStorage(Vector3 from, out Storage source)
    {
        source = null;
        MedicineData best = null;

        foreach (var kv in DataManager.AllItems)
        {
            if (kv.Value is not MedicineData med) continue;
            if (best != null && med.Potency <= best.Potency) continue;

            var storage = Storage.FindStorageWithItem(med, from);   // **실제로 든** 창고
            if (storage == null) continue;

            best   = med;
            source = storage;
        }
        return best;
    }

    // 배낭에서 한 개 뺀다. 못 빼면(그새 없어졌으면) false — 부르는 쪽은 맨손으로 친다.
    // **`RemoveUnlocked`** — 치료는 자동이므로 잠근 뭉치는 안 건드린다(`BestInBackpack`과 짝).
    public static bool ConsumeFromBackpack(UnitBrain pawn, MedicineData med)
        => med != null && pawn?.Inventory != null && pawn.Inventory.RemoveUnlocked(med, 1) > 0;
}
