using UnityEngine;

// **갱도 버팀목 — 둘레의 판 굴이 다시 메워지지 않게 지킨다**
// (2026-09-18에 *천장을 받친다*로 들어왔다가, 2026-09-28에 붕괴를 걷어내며 뜻이 바뀌었다 — 핵 밖의
//  판 굴은 이틀 뒤 메워지고(`RockRegrow`) 버팀목 둘레만 남는다. 판정은 `RockSupport.IsHeld`).
//
// **건물로 짓는다** — 파낸 자리에 저절로 서게 두면 *얼마나 쓸까*라는 결정이 없어진다.
// 나무를 먹고 자리를 고르는 일이라야 **넓게 파는 값**이 실제로 치러진다.
//
// **칸을 안 막는다** — 통로 한복판에 세우는 물건이라, 막으면 지지하려던 그 길이 끊긴다
// (벽걸이 횃불과 같은 규칙). 그래서 콜라이더도 트리거다.
[DisallowMultipleComponent]
public class MineSupport : MonoBehaviour
{
    // 이 버팀목이 지키는 반경(칸). 기본값은 `RockSupport.SafeRadius`(9×9).
    [Tooltip("지키는 반경(칸). 체비셰프 거리라 정사각으로 퍼진다.")]
    [SerializeField] int radius = RockSupport.SafeRadius;

    public int Radius => Mathf.Max(1, radius);

    public Vector2Int Tile =>
        MapManager.Instance != null
            ? MapManager.Instance.WorldToTile(transform.position)
            : Vector2Int.zero;

    // **휴면·철거로 꺼지면 빠진다** — 남아 있으면 없는 버팀목이 굴을 지킨다
    // (`ResourceNode._all`과 같은 규칙).
    void OnEnable()  => RockSupport.Register(this);
    void OnDisable() => RockSupport.Unregister(this);
}
