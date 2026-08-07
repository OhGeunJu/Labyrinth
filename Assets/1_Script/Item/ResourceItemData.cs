using UnityEngine;

// 자원 아이템 — 스택 가능 + `ResourceGroup` 분류 (창고 필터, UI 묶음).
//
// **자원의 정체성은 이 에셋 자신이다.** 예전엔 `ResourceType` enum이 종류를 따로 들고 있었지만
// 그 값은 이 에셋과 1:1이라 이름만 하나 더 만드는 꼴이었다(`ResourceGroup.cs` 주석 참조).
// 이제 "무슨 자원인가"는 이 에셋을 가리켜 말하고, `Group`은 묶음만 답한다.
[CreateAssetMenu(menuName = "DungeonColony/ResourceItemData")]
public class ResourceItemData : ItemData
{
    [Tooltip("창고 필터·UI 분류에 쓰는 묶음. 종류가 아니라 묶음이다 — 종류는 이 에셋 자신.")]
    public ResourceGroup Group;

    // 묶음 이름이 곧 종류 이름이다 — "원석"·"주괴"·"보석".
    public override string TypeName => Group.DisplayName();

}
