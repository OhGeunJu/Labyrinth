using System;
using UnityEngine;

// 적이 지니고 나오는 것을 저작하는 두 그릇. 둘 다 `LoadoutData`가 든다.
//
// **둘 다 태어날 때 정해진다** — 갈리는 것은 *어디에 놓이는가*뿐이다.
// - `LoadoutEntry`(장비)는 몸에 **찬다**(`Equipment`) — 무기가 공격을, 갑옷이 피해 감소를 바꾼다.
// - `LootEntry`(주머니)는 배낭에 **넣는다**(`Inventory`).
//
// 한때 주머니만 **죽을 때** 굴렸다. 근거는 *"잡동사니는 살아 있는 동안 아무 차이를 만들지
// 않으니, 미리 지고 다니면 적재 무게·짐내림에 공짜로 걸린다"*였는데 그 대가가 컸다 —
// **살아 있는 상대의 배낭이 늘 비어 있었다.** 붙잡은 포로를 뒤져도 아무것도 없어서
// *"무엇을 지녔는지 보고 정한다"*가 성립하지 않았다. 무게와 짐내림에 걸리는 것은
// 지고 다니는 물건에 대해 **옳은 동작**이다(2026-08-16).
//
// **`LootEntry`는 이제 "몸에서 나오는 것"에는 안 쓴다** — 늑대 가죽·고기는
// `UnitData.ButcherYield`/`BoneYield`이고 **도축해야 나온다**. 시점이 그 둘을 가른다:
// 지니던 것은 죽으면 그 자리에 남고, 몸은 갈라야 나온다.

[Serializable]
public class LootEntry
{
    [Tooltip("아이템(에셋). 비우면 아래 ItemId로 찾는다.")]
    public ItemData Item;

    [Tooltip("에셋이 아닌 아이템을 쓸 때의 식별자. **JSON·모드 아이템은 이쪽뿐이다** — " +
             "그것들은 런타임에 만들어져 에셋 참조로 가리킬 방법이 없다(`MerchantPoolEntry`와 같은 규약).")]
    public string ItemId;

    // 실제 아이템. 에셋이 먼저, 없으면 레지스트리에서 Id로.
    // **이 프로퍼티를 안 거치고 `Item`을 직접 읽으면 JSON 아이템이 조용히 null이 된다.**
    public ItemData Resolve() => Item != null ? Item : DataManager.GetItem(ItemId);

    [Tooltip("나올 확률. 1이면 반드시 나온다.")]
    [Range(0f, 1f)] public float Chance = 1f;

    [Tooltip("나올 개수 범위(양끝 포함). 둘 다 0이면 안 나온다.")]
    [Min(0)] public int MinCount = 1;
    [Min(0)] public int MaxCount = 1;

    // 이번에 몇 개인가. **확률을 먼저 굴리고 개수를 굴린다** — 개수 0을 확률로 쓰면
    // "10% 확률로 3~5개"를 적을 수가 없다(둘은 다른 물음이다).
    public int Roll()
    {
        if (Resolve() == null) return 0;
        if (UnityEngine.Random.value > Chance) return 0;

        int lo = Mathf.Max(0, Mathf.Min(MinCount, MaxCount));
        int hi = Mathf.Max(0, Mathf.Max(MinCount, MaxCount));
        return hi <= 0 ? 0 : UnityEngine.Random.Range(lo, hi + 1);
    }
}

[Serializable]
public class LoadoutEntry
{
    [Tooltip("무기(`WeaponData`) 또는 방어구(`ArmorData`). 그 밖의 것은 배낭에만 들어간다.\n" +
             "비우면 아래 ItemId로 찾는다.")]
    public ItemData Item;

    [Tooltip("에셋이 아닌 아이템을 쓸 때의 식별자. **장비는 대부분 JSON이라 이쪽을 쓰게 된다** " +
             "— 2026-08-02에 옛 장비 16종을 JSON으로 옮겼고, 그것들은 에셋 참조로 못 가리킨다.")]
    public string ItemId;

    public ItemData Resolve() => Item != null ? Item : DataManager.GetItem(ItemId);

    [Tooltip("차고 나올 확률. 1이면 반드시 찬다.")]
    [Range(0f, 1f)] public float Chance = 1f;

    public bool Roll() => Resolve() != null && UnityEngine.Random.value <= Chance;
}
