using UnityEngine;

// **수레 — 짐승이 끄는 짐칸**(2026-10-01, `PawnCart`). 가축(`UnitBrain.IsTamedBeast`)에 달면 그 짐승이 지는 양이
// `CarryBonus`만큼 늘고 걸음이 `SpeedMult`만큼 느려진다 — 그리고 **사람이 끌 수 있게 된다**(`AcceptsOrders`).
//
// 장비 칸이 아니다 — 짐승은 손이 없다(`UnitData.CanEquip`). 칸을 하나 더 만들면 사람 장비창까지 갈라야 하므로,
// 수레는 **창고에서 꺼내 다는 물건**이다(`PawnCart.Attach` — 핵 영역 안에서).
[CreateAssetMenu(menuName = "DungeonColony/CartData")]
public class CartData : ItemData
{
    public override string TypeName => "수레";

    [Tooltip("이 수레가 더해 주는 적재(kg). 사람 근력 130이 75kg이다.")]
    public float CarryBonus = 120f;

    [Tooltip("수레를 끄는 동안의 걸음 배율.")]
    [Range(0.3f, 1f)] public float SpeedMult = 0.85f;

    [Tooltip("짐승 뒤에 붙일 모양. 크기는 `ModelLength`에 맞춰 재서 줄인다(팩마다 단위가 달라서).")]
    public GameObject Model;

    [Tooltip("모양의 길이(m) — 가장 긴 수평 변을 이 길이로 맞춘다.")]
    public float ModelLength = 1.6f;

    [Tooltip("모양에 입힐 재질(비우면 팩의 것 그대로). 팩 재질이 렌더 파이프라인과 안 맞으면 분홍으로 뜬다.")]
    public Material Material;

    public override int MaxStack => 1;
}
