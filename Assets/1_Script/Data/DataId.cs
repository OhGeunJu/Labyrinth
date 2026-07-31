using UnityEngine;

// 데이터 에셋의 **식별자 규칙**을 한 곳에 못 박는다.
//
// 왜 표시명을 식별자로 쓰면 안 되나:
//   · 표시명은 **번역 대상**이다. "철검"을 "Iron Sword"로 바꾸는 순간 그걸 가리키던
//     모드 JSON·레시피 참조가 전부 끊긴다.
//   · 모드 둘이 같은 표시명을 쓰면 **조용히 덮어쓴다**(경고도 없이 하나가 사라진다).
//   · 표시명은 저작 중에 자주 바뀐다. 식별자는 안 바뀌어야 한다.
//
// 규칙: **`Id`가 비어 있으면 에셋 파일명을 쓴다.**
//   파일명은 이미 프로젝트에서 유일하고 ASCII이며(`Res_IronOre`, `Rec_Bronze`) 잘 안 바뀐다.
//   그래서 대부분의 에셋은 `Id`를 비워 두면 되고, **파일명과 다르게 하고 싶을 때만** 채운다
//   — "데이터가 없으면 기본 동작"이라는 이 프로젝트의 관례와 같다.
public static class DataId
{
    public static string Resolve(Object asset, string explicitId)
    {
        if (!string.IsNullOrWhiteSpace(explicitId)) return explicitId.Trim();
        return asset != null ? asset.name : null;
    }
}
