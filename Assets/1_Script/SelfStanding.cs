using UnityEngine;

// **씬 배선 없이 스스로 서는 것**을 세우는 문 하나(2026-09-09).
//
// `DraftController`·`HaulDesignator`·`WindowRaiser`·`RaidLords`… 열둘이 같은 네 줄을
// 각자 들고 있었고 **`hideFlags`가 절반만 `DontSave`였다** — 세우는 규칙이 한 벌이면 그런
// 갈림은 애초에 안 생긴다. 쓰는 쪽은 `[RuntimeInitializeOnLoadMethod]`만 제 손으로 단다
// (그 속성은 클래스마다 있어야 유니티가 부른다).
public static class SelfStanding
{
    public static T Create<T>(string name) where T : Component
    {
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        Object.DontDestroyOnLoad(go);
        return go.AddComponent<T>();
    }
}
