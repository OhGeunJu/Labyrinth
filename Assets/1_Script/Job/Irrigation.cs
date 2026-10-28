using System.Collections.Generic;
using UnityEngine;

// **물길이 닿는 밭은 마르지 않는다.**
//
// 밭은 수분이 있어야 자란다(`FarmPlot.UpdateGrowth`) — 마르면 성장이 **멎고**, 폰이 물을
// 길어다 부어야 다시 돈다(`WaterJob`). 그건 밭을 짓는 값으로는 맞는데, **밭이 늘어날수록
// 늘어나는 것이 결정이 아니라 잡일**이다: 열 칸을 심으면 열 칸에 물을 대는 일이 매일 돈다.
//
// 관개 수로는 그 잡일을 **자리로 바꾼다** — 어디에 놓느냐가 어느 밭이 마르지 않느냐를
// 정하고, 반경이 있으므로 밭을 어떻게 모아 놓을지가 그대로 문제가 된다.
//
// ## 물을 채우지는 않는다 — 마르지 않게 할 뿐이다
//
// 수분을 저절로 **올려 주면** 물 긷는 일이 통째로 사라지고(우물도 물통도 뜻을 잃는다),
// 무엇보다 **심자마자 자라기 시작한다.** 지금은 첫 물은 사람이 대야 하고, 그 뒤로 안 마른다 —
// *한 번 대 놓으면 되는 밭*이라는 것이 관개가 사는 값이다.
//
// ## 레지스트리다
//
// `LightSource.All`·`Bed._all`·`ResourceNode._all`과 같은 자리 — 타입이 제 인스턴스를 들고
// 있을 뿐이라 구독을 놓쳐 새는 데가 없고, **휴면도 저절로 걸린다**: `BuildingDormancy`가
// `enabled = false`로 재우면 `OnDisable`이 목록에서 빼므로, 핵 영역 밖 수로는 아무것도
// 적시지 않는다(화톳불이 혼자 타던 그 자리와 같은 규칙).
//
// ## 반경은 가장자리에서 뚝 끊긴다
//
// 불빛과 같은 판단이다(`LightSource`) — 부드럽게 줄이면 **어디까지가 물길인지를 눈으로 못 재**
// 어디에 밭을 놓을지가 계산이 아니라 짐작이 된다. 여기 답은 예/아니오 하나뿐이라 더욱 그렇다.
public class Irrigation : MonoBehaviour
{
    public static readonly List<Irrigation> All = new();

    // **반경이 곧 배치 결정이다.** 1칸 = 1m이므로 6m면 한 칸이 **113칸**을 적신다 —
    // 하나 놓으면 밭 전체가 끝나서 *어디에 몇 개 놓나*라는 물음이 사라진다.
    // 2.5m는 **약 19칸**(4×4 남짓)이라, 밭을 넓히면 수로도 같이 끼워 넣어야 한다.
    [Tooltip("물이 닿는 거리(m). 1칸 = 1m. 이 안의 밭은 수분이 안 마른다.")]
    [SerializeField] float radius = 2.5f;

    public float Radius => radius;

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // 그 자리가 물길 안인가. **평면으로 잰다** — 밭과 수로는 같은 바닥에 서므로 높이 차이는
    // 모델의 두께일 뿐인데, 그것까지 세면 같은 칸인데도 판정이 갈린다.
    public static bool Covers(Vector3 worldPos)
    {
        for (int i = 0; i < All.Count; i++)
        {
            var it = All[i];
            if (it == null) continue;

            Vector3 d = it.transform.position - worldPos;
            d.y = 0f;
            if (d.sqrMagnitude <= it.radius * it.radius) return true;
        }
        return false;
    }

#if UNITY_EDITOR
    // 건설할 때 어디까지 닿는지 보이게 — 반경이 안 보이면 배치가 짐작이 된다.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
