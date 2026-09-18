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

    // ── 올리는 값 ────────────────────────────────────────────────────────────
    //
    // **여기가 손잡이다**(2026-09-17, 사용자: *"이름만 정하고 재료는 나중에"*).
    // 지금 값은 **돌려 보라고 넣어 둔 임시값**이다 — 비워 두면 값이 0이라 누르는 즉시
    // 완성되어 **업그레이드 절차 자체를 화면에서 못 본다.**
    //
    // 층과 나란히 세워 두었다(판자 1층 · 단단한 판자 2층 · 티타늄 3층 · 미스릴 4층).
    // **재료 이름은 문자열이 아니라 여기서 한 번만 적는다** — 오타는 *"재료가 없다"*로만
    // 나타난다(`ItemId` 규약과 같은 함정).
    public static readonly Dictionary<int, (string item, int amount)[]> CostTo = new()
    {
        [2] = new[] { ("Res_Lumber",     20), ("Res_Stone", 15) },
        [3] = new[] { ("Res_HardLumber", 20), ("Res_Steel",  6) },
        [4] = new[] { ("Res_HardLumber", 25), ("Res_Titanium", 6) },
        [5] = new[] { ("Res_HardLumber", 30), ("Res_Mithril",  6) },
    };
}
