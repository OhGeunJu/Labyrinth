using System;
using System.Collections.Generic;
using UnityEngine;

// **한 마을에 종류당 하나씩 선다**(`TownMarket`). 그래서 이 enum은 "무엇을 파는가"인 동시에
// 마을 시장의 **줄 목록**이기도 하다 — 값을 늘리면 마을에 상인이 한 명 더 설 자리가 생긴다.
// 값을 뒤에 붙일 것(직렬화가 정수라 중간에 끼우면 기존 에셋의 종류가 밀린다).
public enum MerchantType { General, Weapon, Armor, Material, Food }

public static class MerchantTypeExt
{
    public static string DisplayName(this MerchantType t) => t switch
    {
        MerchantType.Weapon   => "무기 상인",
        MerchantType.Armor    => "방어구 상인",
        MerchantType.Material => "재료 상인",
        MerchantType.Food     => "식료 상인",
        MerchantType.General  => "일반 상인",
        _                     => t.ToString(),
    };
}

[Serializable]
public class MerchantPoolEntry
{
    [Tooltip("팔 아이템(에셋). 비우면 아래 ItemId로 찾는다.")]
    public ItemData     Item;

    [Tooltip("에셋이 아닌 아이템을 팔 때 쓰는 식별자. **JSON·모드 아이템은 이쪽뿐이다** — " +
             "그것들은 런타임에 만들어져 에셋 참조로 가리킬 방법이 없다.")]
    public string       ItemId;

    [Tooltip("0이면 아이템의 BaseValue 사용")]
    [Min(0)] public int         BasePrice = 0;
    [Min(1)] public int         MinStock  = 1;
    [Min(1)] public int         MaxStock  = 3;
    [Min(0)] public float       Weight    = 1f;
    public           ItemQuality Quality   = ItemQuality.Common;

    // 실제로 팔 아이템. 에셋이 먼저고, 없으면 레지스트리에서 Id로 찾는다.
    // **이 프로퍼티를 거치지 않고 `Item`을 직접 읽으면 JSON 아이템이 조용히 null이 된다.**
    public ItemData Resolve() => Item != null ? Item : DataManager.GetItem(ItemId);
}

// 아이템별 가격 오버라이드 — 특정 마을에서 이 아이템만 다른 배율 적용
[Serializable]
public class ItemPriceOverride
{
    public ItemData Item;
    [Tooltip("이 아이템만 적용할 배율. PriceMultiplier를 대체함.")]
    [Range(0.1f, 5f)] public float Multiplier = 1f;
}

// 당일 진열된 재고 1행
public class MerchantStockEntry
{
    public ItemData     Item;
    public int         Price;
    public int         Stock;
    public ItemQuality Quality;
}

[CreateAssetMenu(menuName = "DungeonColony/MerchantData")]
public class MerchantData : ScriptableObject
{
    public MerchantType MerchantType;
    [Tooltip("하루에 진열할 아이템 종류 수")]
    [Min(1)] public int DailySlotCount = 5;
    public List<MerchantPoolEntry> ItemPool = new();

    // **상품 묶음은 겹쳐 쓴다.** 고블린 방어구 상인은 자기 목록에 "고블린제"만 적고
    // 기본 방어구는 `Merchant_Armor`를 통째로 얹는다 — 공용 목록을 종족 수만큼 복붙하지
    // 않는다. 복붙하면 기본 방어구 하나를 고칠 때 종족 수만큼 고쳐야 하고, 반드시 어긋난다.
    //
    // 값·매입 배율·진열 칸 수는 **이 에셋 것**이 쓰인다. 얹은 것은 후보 목록일 뿐이라
    // 같은 물건이라도 어느 상인이 파느냐로 값이 달라진다(지역 시세와 같은 규칙).
    [Tooltip("함께 취급하는 다른 상품 묶음. 기본템 + 고블린템처럼 겹쳐 쓴다.")]
    public List<MerchantData> ExtraPools = new();

    // 실제로 뽑을 후보 전체 — 자기 목록 + 얹은 묶음들.
    // 같은 묶음을 두 번 세지 않고, 서로를 얹은 순환 참조에도 멈춘다.
    public List<MerchantPoolEntry> CollectPool()
    {
        var result = new List<MerchantPoolEntry>();
        Collect(result, new HashSet<MerchantData>());
        return result;
    }

    void Collect(List<MerchantPoolEntry> into, HashSet<MerchantData> seen)
    {
        if (!seen.Add(this)) return;

        if (ItemPool != null) into.AddRange(ItemPool);
        if (ExtraPools == null) return;

        foreach (var p in ExtraPools)
            if (p != null) p.Collect(into, seen);
    }
    // **골드 아이템 필드는 없앴다**(2026-08-10). 값은 숫자고 지갑은 전역 잔고(`Wallet`)라
    // 상인이 "무엇이 돈인가"를 들고 있을 이유가 없다. 남겨 두면 안 채운 상인이
    // "이 상인에겐 골드 설정이 없다"로 조용히 거래 불가가 된다.

    [Header("가격 조정")]
    [Tooltip("마을 전체 구매가 배율. 1.0=기본, 1.5=비싼 마을, 0.8=싼 마을")]
    [Range(0.1f, 5f)] public float PriceMultiplier = 1f;

    [Tooltip("아이템별 개별 배율 오버라이드. 여기 등록된 아이템은 PriceMultiplier 대신 이 배율 적용.")]
    public List<ItemPriceOverride> ItemOverrides = new();

    [Tooltip("플레이어 판매가 배율. 0.5 = 기준가의 50%로 매입")]
    [Range(0.1f, 1f)] public float SellMultiplier = 0.5f;

    [Header("취급 조건")]
    // **"고블린 방어구 상인은 고블린 방어구를 판다"를 적는 자리.** 종족과 상품을 그냥 곱하면
    // 인간이 고블린제 물건을 들고 오는 조합이 나온다. 그렇다고 조합마다 프리팹을 만들면
    // 종족 × 종류의 곱셈으로 돌아간다 — 조건은 **상품 쪽이 드는 게 맞다.** 어느 종족이
    // 만드는 물건인가는 그 물건의 성질이지, 파는 사람의 스탯이나 이름 풀의 성질이 아니다.
    [Tooltip("이 물건을 취급하는 종족. **비우면 누구나** — 대부분은 비워 둔다.")]
    public List<UnitData> Races = new();

    // 이 종족이 이 물건을 취급하는가. 조건이 없으면 항상 참.
    public bool SoldBy(UnitData race)
    {
        if (Races == null || Races.Count == 0) return true;
        foreach (var r in Races)
            if (r != null && r == race) return true;
        return false;
    }

    // 아이템에 적용할 최종 배율 반환 (오버라이드 없으면 PriceMultiplier)
    public float GetItemMultiplier(ItemData item)
    {
        if (item == null) return PriceMultiplier;
        foreach (var o in ItemOverrides)
            if (o.Item == item) return o.Multiplier;
        return PriceMultiplier;
    }

    // 구매가: poolEntry.BasePrice(0이면 item.BaseValue) × 아이템 배율
    public int CalcPrice(MerchantPoolEntry entry)
    {
        var item = entry.Resolve();
        int basePrice = entry.BasePrice > 0 ? entry.BasePrice
                      : (item != null ? item.BaseValue : 10);
        if (basePrice <= 0) basePrice = 10;
        return Mathf.Max(1, Mathf.RoundToInt(basePrice * GetItemMultiplier(item)));
    }

    // 판매가: item.BaseValue × quality 배율 × **아이템 배율** × SellMultiplier × **되팔이 감가**
    //
    // **살 때와 팔 때 같은 지역 배율을 쓴다.** 전엔 배율이 구매가에만 붙어서, 생선을 2배로 치는
    // 마을도 매입은 다른 곳과 똑같이 했다 — 그러면 "이 마을은 생선이 귀하다"가 살 때만
    // 참이고, 싸게 사서 비싸게 파는 교역이 성립하지 않는다. 지역차가 값의 성질이라면
    // 방향과 무관하게 붙어야 한다.
    //
    // **되팔이 감가(`ItemData.ResaleFactor`)는 물건이 든다.** 상인 쪽에만 두면 "이 상인은
    // 인색하다"밖에 표현할 수 없어 그 상인에게 광석을 팔아도 똑같이 깎인다. 값이 안 나오는 건
    // 물건의 성질이다 — 광석은 시세대로 받지만 쓰던 검은 중고다.
    public int CalcSellPrice(ItemData item, ItemQuality quality)
    {
        if (item == null || item.BaseValue <= 0) return 0;
        float value = item.BaseValue * quality.StatMultiplier() * GetItemMultiplier(item);
        return Mathf.Max(1, Mathf.RoundToInt(value * SellMultiplier * item.ResaleFactor));
    }
}
