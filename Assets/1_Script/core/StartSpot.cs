using UnityEngine;

// **판이 시작되는 자리 — 씬에 하나 놓는 표식**(2026-09-20, 사용자: *"씬에 있는 폰 지워버리고 시작 자리 하나 만들"*).
//
// 전에는 **씬에 놓아 둔 내 폰의 자리**가 곧 시작 자리였다(`RockRenderer.Start`가 그 둘레를 판다). 캐릭터를 만드는
// 화면이 생기면서 판이 열릴 때 씬에는 폰이 없다 — 그래서 자리를 폰에서 떼어 **표식이 든다.**
//
// - **파는 쪽이 이것을 먼저 본다**(`RockRenderer.Start`) — 만든 폰은 화면을 닫은 뒤에 서므로, 폰을 기다리면
//   첫 화면이 통짜 바위다.
// - **만든 폰은 여기 선다**(`CharacterCreationUI`).
// - **하나만 둔다** — 둘이면 어느 쪽인지가 씬의 순서에 달린다(`Awake`가 경고한다).
// - 씬 배선은 `World → I. Place Start Spot`이 한다(씬의 방랑자를 치우고 첫 자리에 이것을 둔다).
[DisallowMultipleComponent]
public class StartSpot : MonoBehaviour
{
    public static StartSpot Current { get; private set; }

    void Awake()
    {
        if (Current != null && Current != this)
            Debug.LogWarning($"[시작 자리] 씬에 `StartSpot`이 둘 이상이다 — `{Current.name}`을 쓰고 `{name}`은 무시한다.", this);
        else
            Current = this;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
    }
}
