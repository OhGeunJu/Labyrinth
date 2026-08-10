using UnityEngine;

// **치료에 쓰는 소모품.**
//
// 등급(`ItemQuality`)으로 가르지 않고 **별개 아이템**으로 둔다. 등급은 같은 물건이 잘 만들어진
// 것이냐를 말하는 축이고(희귀 붕대), 여기서 필요한 것은 **다른 물건**이다 — 찢은 천과 구급낭은
// 잘 만든 붕대와 못 만든 붕대의 관계가 아니다. 따로 두면 레시피·재료·값이 각자 붙고,
// "무엇을 만들어 둘 것인가"가 선택이 된다.
//
// **없어도 치료는 된다** — 맨손 값(`TreatJob.BareHands`)이 따로 있다. 도구를 요구하면
// 물자가 떨어진 순간 치료가 통째로 멈추는데, 그건 사람이 손을 놓는 것과 다르다.
public class MedicineData : ItemData
{
    public override string TypeName => "의료용품";

    [Tooltip("한 번 치료로 회복시키는 양의 배율. 맨손은 " + nameof(TreatJob) + ".BareHands 참고.")]
    public float Potency = 1f;

    [Tooltip("치료에 걸리는 시간의 배율(클수록 빠르다). 효능과 따로 두는 이유: " +
             "약초 습포처럼 **잘 듣지만 손이 많이 가는** 것이 있다.")]
    public float TreatSpeed = 1f;

    // 소모품이라 넉넉히 쌓인다. 붕대를 한 칸에 다섯 개씩 들고 다닐 수는 없다.
    public override int MaxStack => 25;
}
