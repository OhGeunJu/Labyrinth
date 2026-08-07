using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ItemData")]
public class ItemData : ScriptableObject
{
    [Tooltip("식별자. 비우면 에셋 파일명을 쓴다(권장). 표시명과 달리 번역되지 않고 바뀌지 않아야 한다.")]
    public string Id;

    [Tooltip("화면에 보이는 이름. 번역 대상이므로 **식별자로 쓰지 말 것** — Id를 쓴다.")]
    public string ItemName;
    [TextArea(2, 5)] public string Description;
    public Sprite Icon;
    [Tooltip("바닥에 드랍될 때 쓰는 3D 모델 프리팹. 비면 기본 큐브로 폴백.")]
    public GameObject WorldModel;
    [Min(0)] public float Weight = 0f;

    [Header("그리드 크기 (셀 단위)")]
    [Min(1)] public int Width  = 1;
    [Min(1)] public int Height = 1;

    [Header("경제")]
    [Tooltip("기준 가격 (G). 상인 풀에서 BasePrice가 0이면 이 값 사용. 인벤토리 툴팁에도 표시.")]
    [Min(0)] public int BaseValue = 0;

    [Tooltip("한 칸에 몇 개까지 쌓이는가. 1이면 한 칸에 하나(무기·방어구). " +
             "**자원만의 속성이 아니다** — 밀·빵·씨앗도 쌓여야 한다.")]
    [Min(1)] public int Stack = 1;

    // 예전엔 `ResourceItemData`만 스택을 들고 있어서, `ItemData`·`FoodData`인 밀·빵·고기는
    // **한 칸에 하나씩** 들어갔다(창고에 `밀 밀 밀 밀`이 늘어서던 원인). 스택은 자원만의
    // 성질이 아니므로 여기로 올렸다. 파생 클래스가 다르게 정하고 싶으면 오버라이드한다.
    public virtual int MaxStack => Mathf.Max(1, Stack);

    // ── 되팔 때의 감가 ────────────────────────────────────────────────────────
    //
    // **1 = 시세대로, 낮을수록 되팔 때 손해.** 상인의 매입 인심(`MerchantData.SellMultiplier`)과
    // 곱해져 최종 매입가가 된다.
    //
    // 왜 상인이 아니라 **물건**이 드는가: 상인 쪽에만 두면 "이 상인은 인색하다"밖에 표현할 수
    // 없어서, 그 상인에게 광석을 팔아도 똑같이 깎인다. 실제로 값이 안 나오는 건 **물건의
    // 성질**이다 — 광석은 시세가 정해져 있어 거의 그대로 받지만, 쓰던 검은 중고가 된다.
    //
    // 자원·식량은 1(기본). 무기·방어구는 낮게 잡는다(각 클래스에서 오버라이드) —
    // 그래야 "장비는 사서 쓰는 것이지 되팔아 돈 버는 수단이 아니다"가 값으로 드러난다.
    public virtual float ResaleFactor => 1f;

    // ── 종류 ──────────────────────────────────────────────────────────────────
    //
    // 화면에 보이는 **분류 이름**("두손 검", "몸통 방어구", "원석"…). 툴팁이 등급 옆에 붙인다.
    //
    // 클래스가 답하게 두는 이유: 무엇인지 아는 건 그 자신뿐이다. 밖에서 `is WeaponData`로
    // 갈라 쓰면 종류가 늘 때마다 그 분기를 **전부** 찾아 고쳐야 하고, 하나 빠뜨리면 조용히
    // "잡화"가 된다. 모드가 새 아이템 종류를 넣어도 여기만 오버라이드하면 된다.
    public virtual string TypeName => "잡화";
}
