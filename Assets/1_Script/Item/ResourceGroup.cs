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
// **값은 반드시 꼬리에 붙일 것** — 정수로 직렬화되므로 중간에 끼우면 저작한 아이템 에셋이
// 통째로 밀린다(돌이 원석이 되고 원석이 보석이 된다).
//
// `Bone`을 뒤에 더했다(2026-08-29). 그전에는 뼈가 **석재 묶음**에 있었는데 — 단단한 것이라
// 그렇게 뒀던 모양이지만 — 그러면 **돌 창고에 뼈가 섞이고**, 무엇보다 *"돌 묶음의 아무거나"*를
// 집는 코드가 **뼈를 돌로 안다**(벽을 팠는데 뼈가 나왔다). 묶음은 화면을 나누는 축이라
// **눈으로 봤을 때 같은 칸에 있어야 할 것**끼리 묶여야 한다.
// **값은 꼬리에 붙인다** — 정수 직렬화라 중간에 끼우면 저작한 아이템이 통째로 밀린다.
// `Hide`·`Cloth`는 2026-09-06에 붙였다: 그전에는 생가죽·가죽·두꺼운가죽·천·두꺼운천이
// **전부 `Wood`로 저작돼 있었다.** 묶음은 창고 필터와 인벤토리 분류의 축이라, 창고를
// *"목재"*로 걸어 두면 생가죽이 들어갔다 — **화면이 거짓말을 하고 있었다.** 자리가 없어서
// 가장 가까운 칸에 던져 넣은 것인데, 묶음은 콘텐츠가 아니라 **화면을 나누는 축**이라
// 자리를 내는 편이 싸다.
public enum ResourceGroup { Stone, Ore, Gem, Ingot, Wood, Currency, Bone, Hide, Cloth }

public static class ResourceGroupExt
{
    public static readonly ResourceGroup[] All =
    {
        ResourceGroup.Stone, ResourceGroup.Ore, ResourceGroup.Gem,
        ResourceGroup.Ingot, ResourceGroup.Wood, ResourceGroup.Currency,
        ResourceGroup.Bone, ResourceGroup.Hide, ResourceGroup.Cloth,
    };

    public static string DisplayName(this ResourceGroup g) => g switch
    {
        ResourceGroup.Stone    => "석재",
        ResourceGroup.Ore      => "원석",
        ResourceGroup.Gem      => "보석",
        ResourceGroup.Ingot    => "주괴",
        ResourceGroup.Wood     => "목재",
        ResourceGroup.Currency => "화폐",
        ResourceGroup.Bone     => "뼈",
        ResourceGroup.Hide     => "가죽",
        ResourceGroup.Cloth    => "천",
        _                      => g.ToString(),
    };

}
