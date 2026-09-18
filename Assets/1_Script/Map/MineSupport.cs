using UnityEngine;

// **갱도 버팀목 — 천장을 받쳐 더 넓게 팔 수 있게 한다**
// (2026-09-18, 사용자 결정: *"cave kit에 wooden에 supports보면 동굴을 지지해주는 나무
//  기둥같은게 있는데 … 그거에 제한을 두는 시스템"*).
//
// **건물로 짓는다** — 파낸 자리에 저절로 서게 두면 *얼마나 쓸까*라는 결정이 없어진다.
// 나무를 먹고 자리를 고르는 일이라야 **넓게 파는 값**이 실제로 치러진다.
//
// **칸을 안 막는다** — 통로 한복판에 세우는 물건이라, 막으면 지지하려던 그 길이 끊긴다
// (벽걸이 횃불과 같은 규칙). 그래서 콜라이더도 트리거다.
[DisallowMultipleComponent]
public class MineSupport : MonoBehaviour
{
    // 이 버팀목이 받치는 반경(칸). **`RockSupport.SafeRadius`와 같은 눈금**이라, 하나를
    // 세우면 *바위 하나가 거기 있는 것*과 같아진다 — 규칙이 하나로 읽힌다.
    [Tooltip("받치는 반경(칸). 체비셰프 거리라 정사각으로 퍼진다.")]
    [SerializeField] int radius = RockSupport.SafeRadius;

    public int Radius => Mathf.Max(1, radius);

    public Vector2Int Tile =>
        MapManager.Instance != null
            ? MapManager.Instance.WorldToTile(transform.position)
            : Vector2Int.zero;

    // **휴면·철거로 꺼지면 빠진다** — 남아 있으면 없는 버팀목이 천장을 받친다
    // (`ResourceNode._all`과 같은 규칙).
    void OnEnable()  => RockSupport.Register(this);
    void OnDisable() => RockSupport.Unregister(this);
}
