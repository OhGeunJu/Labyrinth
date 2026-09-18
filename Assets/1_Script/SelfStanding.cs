using UnityEngine;

// **씬 배선 없이 스스로 서는 것**을 세우는 문 하나(2026-09-09).
//
// `DraftController`·`HaulDesignator`·`WindowRaiser`·`RaidLords`… 열둘이 같은 네 줄을
// 각자 들고 있었고 **`hideFlags`가 절반만 `DontSave`였다** — 세우는 규칙이 한 벌이면 그런
// 갈림은 애초에 안 생긴다. 쓰는 쪽은 `[RuntimeInitializeOnLoadMethod]`만 제 손으로 단다
// (그 속성은 클래스마다 있어야 유니티가 부른다).
public static class SelfStanding
{
    // **런타임에 만든 오브젝트의 플래그는 여기 하나다**(2026-09-18).
    //
    // **`HideFlags.DontSave`를 쓰면 안 된다** — 그것은 *씬에 저장 안 함*에 더해
    // ***씬을 다시 읽어도 안 죽음***이다. 그래서 플레이 중에 만든 것이 **플레이를 나가도
    // 에디터 씬에 그대로 남는다**(사용자: *"게임 씬을 종료하고 나서 게임 씬이 그대로 씬에
    // 남아있어"*). 바라는 것은 *저장만 안 되는 것*이므로 **`DontSaveInEditor`**다.
    //
    // (같은 플래그의 다른 얼굴을 바로 앞에서 겪었다 — 에디터가 세운 바위 청크가 도메인
    // 리로드를 넘어 살아남아 **보이지 않는 벽**이 됐다. **`DontSave`는 안 죽는다**가 그
    // 둘의 공통 원인이다.)
    //
    // **에셋(`Material`·`Mesh`)은 예외다** — 거기서는 `HideAndDontSave`가 *안 쓰는 것으로
    // 보고 버리지 마라*라는 뜻이라 그대로 둔다.
    public const HideFlags RuntimeObject = HideFlags.DontSaveInEditor;

    // 계층 창에 안 보여야 하는 것(진단 틱·초상화 장비처럼 사람이 만질 일이 없는 것).
    public const HideFlags HiddenRuntimeObject = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;

    public static T Create<T>(string name) where T : Component
    {
        var go = new GameObject(name) { hideFlags = RuntimeObject };
        Object.DontDestroyOnLoad(go);
        return go.AddComponent<T>();
    }
}
