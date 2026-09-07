using System.Collections.Generic;
using UnityEngine;

// **채우는 자리 — 충전대와 제단**(2026-09-04).
//
// 둘은 **같은 물건이고 저작만 다르다**: 마력 충전대는 마력석을 써서 **에너지**를, 망자의
// 제단은 뼈를 바쳐 **사기**를 채운다. 컴포넌트를 둘로 나누면 *가장 가까운 것 찾기 · 재료
// 세기 · 소모 · 채우기*가 두 벌이 되고, 몸이 하나 더 늘 때 세 벌이 된다.
//
// ## 레지스트리다
//
// `Storage.All`·`LightSource.All`과 같은 자리 — 타입이 제 인스턴스를 들고 있을 뿐이라
// 구독을 놓쳐 새는 데가 없다. **휴면이 그대로 걸린다**: `BuildingDormancy`가 컴포넌트를
// `enabled = false`로 재우면 `OnDisable`에서 빠지므로, **핵 영역 밖 충전대는 후보가 아니다**
// (안 그러면 골렘이 16km 밖 충전대로 걸어간다).
//
// ## 재료는 창고에서 바로 뺀다
//
// 화장터와 같은 길이다(`MaterialPool`) — 폰이 **그 자리에 서 있는 것**이 값이고, 마력석을
// 등에 지고 오는 왕복까지 만들면 운반 일감이 한 벌 더 는다. 대신 그 폰의 배낭도 같이
// 본다(`MaterialPool.Consume`이 배낭 먼저다) — 주워 온 뼈로 그 자리에서 채울 수 있다.
public class Recharger : MonoBehaviour, IWorldClickable
{
    public static readonly List<Recharger> All = new();

    [Tooltip("무슨 축을 채우나. 에너지 = 골렘, 사기 = 해골. 허기·피로는 여기 자리가 아니다.")]
    [SerializeField] NeedKind fills = NeedKind.Energy;

    [Tooltip("한 번 채울 때 드는 재료. 비우면 공짜다 — **저작 실수로 비면 유지비가 통째로 사라진다.**")]
    [SerializeField] ItemData fuel;

    [SerializeField] int fuelAmount = 1;

    public NeedKind Fills      => fills;
    public ItemData Fuel       => fuel;
    public int      FuelAmount => Mathf.Max(1, fuelAmount);

    void Awake()
    {
        // **비면 조용히 공짜가 된다** — 화면에는 *"골렘이 저절로 충전된다"*로만 보이고,
        // 원인이 코드가 아니라 에셋에 있어 찾기 어렵다.
        if (fuel == null)
            Debug.LogWarning($"[충전] {name}: 재료가 비어 있습니다 — {Needs.Name(fills)}가 공짜로 찹니다.", this);
    }

    // 유지 시설이 먹는 힘 — 이식 광맥(2)보다 비싸다. 사람 없이 도는 것 중 **폰의 상태를
    // 되돌리는 것**이라, 광맥 하나보다 값이 커야 *어느 쪽에 힘을 쓸까*가 결정이 된다.
    const int DefaultPowerCost = 3;

    void Start() => PowerUser.Ensure(this, DefaultPowerCost);

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // **좌클릭에 열 창이 없다**(화톳불·침대와 같은 자리). 상태는 `InspectPanel`이 적고,
    // 조작이라 할 것이 없다 — 폰이 알아서 온다.
    public void OnWorldClick() { }
}
