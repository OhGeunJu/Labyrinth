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
}
