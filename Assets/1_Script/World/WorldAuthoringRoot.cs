using UnityEngine;

// **저작 씬에서 세계가 매달리는 자리**(2026-08-27).
//
// 굽기가 *"어디부터 훑나"*를 이 컴포넌트로 찾는다. **이름으로 찾지 않는다** — 이름은 바뀌라고
// 있는 것이고, 오타 하나에 굽기가 조용히 아무것도 안 굽는다.
//
// ── `LoadedWholeWorld`가 왜 있나 ────────────────────────────────────────────
//
// **굽기는 지우기까지 한다.** 씬에 없는 청크의 에셋은 *"저작자가 지운 것"*이라 없어져야 한다 —
// 안 그러면 씬에서 지운 마을이 게임에는 그대로 선다.
//
// 그런데 그 규칙은 **씬이 세계 전부를 담고 있을 때만** 참이다. 일부만 펼쳐 놓고 구우면 **안
// 펼친 청크가 통째로 지워진다.** 그래서 `4. Load World Into Scene`이 이 깃발을 세우고, 굽기는
// 깃발이 없으면 **아예 거절한다.**
//
// 모드를 둘로 나누지 않는 이유: *"지우는 굽기 / 안 지우는 굽기"*가 생기면 어느 쪽으로 구웠는지를
// 저작자가 기억해야 하고, 한 번 잘못 고르면 조용히 사라진다. **길이 하나면 틀릴 수가 없다.**
// **판이 열리면 스스로 꺼진다**(2026-08-27).
//
// 저작 씬은 게임 씬과 **같이 열어 두고** 작업한다(지형 프로필을 `TerrainStreamer`에서 읽어야
// 하므로). 그 상태로 Play를 누르면 유니티는 열린 씬을 전부 그대로 들고 들어가므로,
// **세계의 마을·소굴 220곳이 한꺼번에 살아난다** — 그 안의 NPC가 전부 지형 없는 허공에서
// 떨어지고(`PawnMover`의 낙하 복구가 수백 줄씩 경고를 뱉는다), 스트리밍이 세운 것과 **두
// 벌**이 되어 같은 마을이 두 번 선다.
//
// **빌드에서 빼는 것만으로는 안 막힌다** — 그건 빌드 이야기이고 에디터의 Play는 열린 씬을 쓴다.
// 그래서 여기서 끈다. 저작에는 아무 영향이 없다(에디터 도구는 `FindObjectsInactive.Include`로 찾는다).
[DisallowMultipleComponent]
[DefaultExecutionOrder(-30000)]   // 자식들이 깨어나기 전에 꺼야 한 프레임도 안 돈다
public class WorldAuthoringRoot : MonoBehaviour
{
    [SerializeField] bool loadedWholeWorld;

    void Awake()
    {
        if (!Application.isPlaying) return;

        gameObject.SetActive(false);
        Debug.Log($"[저작 씬] 판이 열려서 `{name}` 아래 {transform.childCount}곳을 껐다 — " +
                  "세계는 구운 청크 에셋으로 스트리밍된다. 저작은 Play를 나가면 그대로다.");
    }

    [Tooltip("마지막으로 펼친 때. 저작자가 보라고 적어 두는 것이고 코드는 안 읽는다.")]
    [SerializeField] string lastLoaded;

    public bool LoadedWholeWorld => loadedWholeWorld;

    // `4. Load World Into Scene`만 부른다.
    public void MarkLoaded(string when)
    {
        loadedWholeWorld = true;
        lastLoaded       = when;
    }
}
