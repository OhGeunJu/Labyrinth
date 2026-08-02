// 자원의 **묶음**. 창고 필터가 자원을 낱개로 늘어놓으면 못 쓰므로 묶음 단위로 다룬다.
//
// 여기 없는 것이 하나 있다: **자원의 종류**다.
// 2026-08-02까지는 `ResourceType` enum(22종)이 자원 하나하나를 가리켰다. 그런데 그 값들은
// `ResourceItemData` 에셋과 **정확히 1:1**이었다 — 한 자원이 이름을 셋(에셋 Id · enum 값 ·
// 표시명) 가지고 있었고, 표시명 테이블도 `ItemName`과 `ResourceTypeExt.DisplayName()`으로
// **두 벌**이었다. 08-02에 표시명과 Id를 갈랐던 것과 같은 병이 한 층 위에 있었던 것이다.
//
// 그래서 종류는 **아이템 자신**(`ItemData` 참조)이 맡고, 여기 남은 것은 묶음뿐이다.
//   · 건물 비용 · 소환 비용 · 노드 산출물 = `ItemData` 참조
//   · 창고 필터 · UI 분류                = `ResourceGroup`
//
// 묶음을 enum으로 남긴 이유: 이건 콘텐츠가 아니라 **화면을 나누는 축**이다. 여섯 칸은
// 창고 패널의 버튼 여섯 개와 같은 것이고, 모드가 여기에 일곱 번째를 더할 이유가 없다.
// 모드는 `"resourceGroup": "Ore"` 한 줄로 기존 칸에 들어온다.
public enum ResourceGroup { Stone, Ore, Gem, Ingot, Wood, Currency }

public static class ResourceGroupExt
{
    public static readonly ResourceGroup[] All =
    {
        ResourceGroup.Stone, ResourceGroup.Ore, ResourceGroup.Gem,
        ResourceGroup.Ingot, ResourceGroup.Wood, ResourceGroup.Currency,
    };

    public static string DisplayName(this ResourceGroup g) => g switch
    {
        ResourceGroup.Stone    => "석재",
        ResourceGroup.Ore      => "원석",
        ResourceGroup.Gem      => "보석",
        ResourceGroup.Ingot    => "주괴",
        ResourceGroup.Wood     => "목재",
        ResourceGroup.Currency => "화폐",
        _                      => g.ToString(),
    };

    // 그 묶음에 속한 자원 아이템 전부. **레지스트리에서 훑는다** —
    // 그래서 모드가 넣은 자원도 자동으로 딸려 온다(enum을 훑던 시절엔 불가능했다).
    public static System.Collections.Generic.List<ResourceItemData> ItemsIn(ResourceGroup g)
    {
        var list = new System.Collections.Generic.List<ResourceItemData>();
        foreach (var kv in DataManager.AllItems)
            if (kv.Value is ResourceItemData r && r.Group == g) list.Add(r);
        return list;
    }
}
