using System.Collections.Generic;
using UnityEngine;

// **작업대·연구대의 등급 — 속도와 해금을 한 축이 답한다**
// (2026-09-17, 사용자 결정: *"연구대1(로마숫자) 연구대2 345 이렇게"* ·
//  *"연구 속도가 빨라지고 상위 레시피를 열어"* · *"하위 연구대에서는 상위 연구대의 연구들이
//  안 보여야 해"*).
//
// 속도 배율과 *무엇을 만들 수 있나*를 **따로 저작하면 반드시 어긋난다** — 빠른데 상위
// 레시피는 안 열리는 작업대가 조용히 생기고, 그게 어느 쪽 실수인지 화면에 안 적힌다.
// **등급 하나를 적고 나머지는 거기서 낸다.**
//
// ## 등급은 새 건물이 아니라 같은 건물의 상태다
//
// 처음엔 `개량 연구대`를 **별도 `BuildingData`**로 냈는데 사용자가 뒤집었다:
// *"새로 설치할 필요 없이 원래 쓰던 거에서 바로 업글할 수 있게"*. 별도 건물이면
// **헐고 다시 짓는 일**이 되어 자리·산출물·배정이 통째로 날아가고, 무엇보다 건설 목록이
// 등급 수만큼 부푼다(작업대 열하나 × 다섯 = 쉰다섯 줄).
//
// 그래서 등급은 **인스턴스 상태**이고 올리는 문은 `BenchUpgrade` 하나다.
//
// ## 정수다 — enum이 아니다
//
// 등급이 다섯으로 늘 수 있고, **`RequiredBenchTier`가 *"이 등급 이상"*이라는 부등호로
// 묻는 값**이라 순서가 뜻을 갖는다. enum이면 사이에 하나 끼우는 날 값이 밀린다(정수 직렬화).
public static class BenchTier
{
    public const int Min = 1;
    public const int Max = 5;

    // **등급마다 +25%다.** 크게 잡으면 아래 등급을 쓸 이유가 사라지고(다음 등급이 나올
    // 때까지 기다리는 편이 낫다), 10%쯤이면 **올리고도 달라진 것을 못 느낀다.**
    // V가 2배라 *끝까지 올리면 절반 시간*이고, 그건 다섯 단계를 밟은 값으로 읽힌다.
    public const float SpeedStep = 0.25f;

    public static int Clamp(int tier) => Mathf.Clamp(tier, Min, Max);

    public static float SpeedOf(int tier) => 1f + SpeedStep * (Clamp(tier) - Min);

    static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };

    public static string Roman(int tier) => Numerals[Clamp(tier) - Min];

    // **이름은 등급이 만든다** — `BuildingData.BuildingName`에 다섯 벌을 저작하면
    // 이름을 고치는 날 그중 하나가 반드시 뒤처진다.
    public static string NameOf(string baseName, int tier) => $"{baseName} {Roman(tier)}";

    // **이름의 근거는 놓인 건물이다** — 작업대와 연구대가 각자 문자열을 들면
    // `BuildingData.BuildingName`을 고치는 날 한쪽만 따라온다.
    public static string BaseNameOf(Component bench)
    {
        var pb = bench != null ? bench.GetComponent<PlacedBuilding>() : null;
        if (pb != null && pb.Data != null && !string.IsNullOrWhiteSpace(pb.Data.BuildingName))
            return pb.Data.BuildingName;
        return bench != null ? bench.name : "작업대";
    }

    // ── 올리는 값 — 다음 층의 물건으로 ──────────────────────────────────────
    //
    // (2026-09-30, 사용자: *"업그레이드마다 다음 지역에서 열리는 아이템을 가지고 해야하긴해
    //  거의 모든게임이 그런 구조긴 하거든"* · *"다음 층 방식으로 해"*)
    //
    // **등급 N으로 올리려면 N층에서 처음 나는 것이 든다**(`FloorFor`). 올리는 일이 곧
    // *한 층 더 내려갔다 왔다*는 표시다 — 지금 층 것으로 올리면 작업대가 탐험과 따로 논다.
    //
    // | 올릴 등급 | 층 | 금속 | 목재 |
    // |---|---|---|---|
    // | II  | 2 | 강철   | 단단한 판자(지저늪) |
    // | III | 3 | 티타늄 | 서리 판자(빙하굴) |
    // | IV  | 4 | 미스릴 | 흑단 판자(심연) |
    // | V   | 5 | **비워 둔다** — 5층이 아직 없다(연구 V가 빈 것과 같은 자리) | |
    //
    // **금속 하나 · 목재 하나, 둘 다 그 층의 것이다.** 처음엔 목재를 단단한 판자로 셋 다
    // 채웠는데 사용자가 뒤집었다(*"단단한 판자도 좀 다른 걸로 바꿔야지 계속 쓸 거야?"* ·
    // *"나무 류가 층마다 새로운 나무가 나는 느낌"*) — 그래서 3·4층에 나무를 새로 심었다
    // (`WoodCreator` — 서리나무 · 흑단). **금속은 층 하나에 하나**라는 규칙이 이미 있고
    // (`docs/design_metals.md`) 목재도 이제 같은 모양이다.
    //
    // ## 어기면 조용히 깨지는 것
    //
    // - **층 구성이 바뀌면 이 표가 늙는다** — 그래서 `BenchTierCheck`가 Play 시작 때
    //   바이옴 저작에서 *"이 물건이 처음 나는 층"*을 계산해 어긋나면 경고한다.
    // - **올리는 재료를 그 등급에서만 열리는 연구가 쥐면 영영 못 올린다** — 같은 검사가 잰다.
    //   II가 강철을 먹으므로 **제련술이 연구대 I에 있어야 한다**(`ResearchTierCreator`).
    // - **줄이 없는 등급은 못 올린다**(`CanRaiseTo`) — 예전엔 *비어 있으면 누르는 즉시 오른다*
    //   였는데, V를 비워 두는 순간 그게 **공짜 V 등급**이 된다.
    //
    // **재료 이름은 문자열이 아니라 여기서 한 번만 적는다** — 오타는 *"재료가 없다"*로만
    // 나타난다(`ItemId` 규약과 같은 함정).
    public static readonly Dictionary<int, (string item, int amount)[]> CostTo = new()
    {
        [2] = new[] { ("Res_HardLumber",  20), ("Res_Steel",    6) },
        [3] = new[] { ("Res_FrostLumber", 20), ("Res_Titanium", 6) },
        [4] = new[] { ("Res_EbonyLumber", 20), ("Res_Mithril",  6) },
    };

    // 등급 N의 재료는 N층 것이다 — 검사가 이 값과 대조한다.
    public static int FloorFor(int tier) => tier;

    public static bool CanRaiseTo(int tier) =>
        tier > Min && tier <= Max && CostTo.ContainsKey(tier);
}
