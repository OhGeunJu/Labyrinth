using UnityEngine;

// **등급이 얼굴을 바꾼다**(2026-09-17, 사용자: *"작업대나 연구대 등급에 따라 모델링이
// 달라지면 좋을 거 같긴해 업그레이드하는 느낌이 들어야지"*).
//
// ## 부품을 붙이는 것이 아니라 한 벌을 통째로 켠다
//
// 사용자 판단이다(*"부품이 붙은걸 모델을 통째로 갈아끼우는게 맞겠지?"*). 런타임에 소품을
// 하나씩 붙이면 **무엇이 붙어 있나가 상태가 되어** 저장·복원·다시 짓기마다 그 목록을
// 들고 다녀야 한다. 등급마다 한 벌을 통째로 켜면 **등급 하나가 곧 얼굴 하나**이고,
// 되돌리는 것도 다른 자식을 켜는 일로 끝난다.
//
// 보이는 결과는 부품이 붙는 것과 같다 — `[Art 3]`은 `[Art 2]`에 하나 더 얹힌 모습이고,
// **그 누적은 에디터에서 굽는다**(`BenchArtCreator`).
//
// ## 어기면 조용히 깨지는 것
//
// - **없으면 아무 일도 안 한다** — 얼굴을 안 입힌 작업대(프리미티브 그대로)가 아직 여럿이라,
//   여기서 옛 렌더러를 끄면 **그 작업대만 통째로 안 보인다.**
// - **가장 가까운 아래 등급으로 떨어진다** — `[Art 4]`만 굽고 5를 안 구웠으면 V로 올린 순간
//   **아무것도 안 켜져 투명해진다.** 없는 등급은 있는 것 중 가장 높은 것으로 내려앉는다.
// - **자식만 켜고 끈다 — 지우지 않는다.** 등급은 오르내리지 않지만(내리는 길이 없다)
//   지우면 다시 켤 방법이 없고, 무엇보다 **프리팹이 든 것을 런타임이 헐면 안 된다.**
[DisallowMultipleComponent]
public class BenchVisual : MonoBehaviour
{
    public const string Prefix = "[Art ";   // `[Art 1]` … `[Art 5]`

    // **런타임에 붙는다** — 프리팹 열둘에 손으로 달면 새 작업대를 만드는 날 하나가 빠지고,
    // 그건 *그 작업대만 등급이 안 보인다*로만 나타난다(`BenchUpgrade.Ensure`와 같은 규칙).
    public static BenchVisual Ensure(Component bench)
    {
        if (bench == null) return null;
        return bench.GetComponent<BenchVisual>() ?? bench.gameObject.AddComponent<BenchVisual>();
    }

    void Start() => Apply();

    // 등급이 오른 뒤 불린다(`CraftingTable.SetTier` · `ResearchTable.SetTier`).
    public void Apply()
    {
        var bench = GetComponent<IUpgradableBench>();
        if (bench == null) return;

        // 구워진 세트가 하나도 없으면 **손대지 않는다** — 얼굴을 안 입힌 작업대다.
        int highest = 0;
        for (int t = BenchTier.Min; t <= BenchTier.Max; t++)
            if (transform.Find(Prefix + t + "]") != null) highest = t;
        if (highest == 0) return;

        int show = Mathf.Min(bench.Tier, highest);

        for (int t = BenchTier.Min; t <= BenchTier.Max; t++)
        {
            var art = transform.Find(Prefix + t + "]");
            if (art != null) art.gameObject.SetActive(t == show);
        }
    }
}
