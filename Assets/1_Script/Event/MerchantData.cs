using System;
using System.Collections.Generic;
using UnityEngine;

public enum MerchantType { General, Weapon, Armor }

public static class MerchantTypeExt
{
    public static string DisplayName(this MerchantType t) => t switch
    {
        MerchantType.Weapon  => "무기 상인",
        MerchantType.Armor   => "방어구 상인",
        MerchantType.General => "일반 상인",
        _                    => t.ToString(),
    };
}

[Serializable]
public class MerchantPoolEntry
{
    public ItemData     Item;
    [Tooltip("0이면 Item.BaseValue 사용")]
    [Min(0)] public int         BasePrice = 0;
    [Min(1)] public int         MinStock  = 1;
    [Min(1)] public int         MaxStock  = 3;
    [Min(0)] public float       Weight    = 1f;
    public           ItemQuality Quality   = ItemQuality.Common;
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
    [Tooltip("골드 아이템 (구매 시 Storage에서 차감)")]
    public ItemData GoldItem;

    [Header("가격 조정")]
    [Tooltip("마을 전체 구매가 배율. 1.0=기본, 1.5=비싼 마을, 0.8=싼 마을")]
    [Range(0.1f, 5f)] public float PriceMultiplier = 1f;

    [Tooltip("아이템별 개별 배율 오버라이드. 여기 등록된 아이템은 PriceMultiplier 대신 이 배율 적용.")]
    public List<ItemPriceOverride> ItemOverrides = new();

    [Tooltip("플레이어 판매가 배율. 0.5 = 기준가의 50%로 매입")]
    [Range(0.1f, 1f)] public float SellMultiplier = 0.5f;

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
        int basePrice = entry.BasePrice > 0 ? entry.BasePrice
                      : (entry.Item != null ? entry.Item.BaseValue : 10);
        if (basePrice <= 0) basePrice = 10;
        return Mathf.Max(1, Mathf.RoundToInt(basePrice * GetItemMultiplier(entry.Item)));
    }

    // 판매가: item.BaseValue × quality 배율 × SellMultiplier
    public int CalcSellPrice(ItemData item, ItemQuality quality)
    {
        if (item == null || item.BaseValue <= 0) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(item.BaseValue * quality.StatMultiplier() * SellMultiplier));
    }
}
