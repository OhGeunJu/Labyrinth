using UnityEngine;

// **저작 씬의 인스턴스에 붙는 표식**(2026-08-27).
//
// 세계를 씬에서 저작하면 물건 하나가 **프리팹 인스턴스 하나**가 된다. 그런데 `WorldObject`가
// 담는 것 중에는 트랜스폼이 답할 수 없는 것이 있다 — 신원 · 안 내리는지 · 도구가 낸 것인지 ·
// 폰 명세. 그것만 여기 든다.
//
// **자리와 방향은 여기 안 적는다.** 트랜스폼이 답한다 — 여기 또 적으면 씬에서 끌어 옮겼을 때
// 둘이 갈리고, 어느 쪽이 맞는지가 또 규칙이 된다. 굽기가 `transform`에서 읽는다.
//
// **이름도 여기 없다** — `gameObject.name`이 답한다. 하이어라키에서 이름을 고치는 것이 곧
// 저작이어야 한다.
//
// 런타임에는 아무도 안 읽는다(`ScatterTag`처럼 씬 쪽 물건이다). 게임이 읽는 것은 구운
// `WorldChunkContent`뿐이다.
[DisallowMultipleComponent]
public class WorldObjectTag : MonoBehaviour
{
    // **굽기가 발급하고 유지한다.** 비어 있으면 굽기가 새로 발급해 여기 되쓴다 —
    // 굽을 때마다 새로 발급하면 신원이 매번 바뀌어, `Persistent` 추적과 (나중의) 세이브가
    // *"이 마을은 이미 털렸다"*를 기억할 수 없게 된다.
    public string Id;

    [Tooltip("무엇인가 — 마을·소굴·자원 지대. `빈 지역`으로 두면 그냥 물건이라 지도에 안 뜬다.")]
    public WorldNodeContent Kind;

    [Tooltip("켜면 한 번 뜬 뒤로는 안 사라진다. 마을처럼 상태를 들고 있는 곳에.")]
    public bool Persistent = true;

    [Tooltip("자동 배치(`5. Populate World`)가 낸 것. 끄면 손으로 놓은 것이라 도구가 안 건드린다.")]
    [UnityEngine.Serialization.FormerlySerializedAs("FromMap")]
    public bool Generated;

    [Tooltip("폰이면 — 비우면 태어날 때 굴린다.")]
    public PawnSpecData Spec;

    [Tooltip("퀘스트 훅. 아직 아무도 안 읽는다.")]
    public string QuestId;
}
