using UnityEngine;

// **자재를 받아 무언가를 완성하는 자리.**
//
// 2026-09-17까지 그런 자리는 `BuildingBlueprint` 하나였고 `ConstructionJob`이 그 타입을
// 직접 들고 있었다. 작업대 **제자리 업그레이드**가 생기면서 둘이 됐다(사용자: *"새로
// 설치할 필요 없이 원래 쓰던 거에서 바로 업글"*).
//
// **일감을 복제하지 않는다** — `ConstructionJob`은 *창고에서 자재를 챙겨 지고 와 내려놓는다*는
// 두 단계를 다루는데, 그 안에는 **남이 먼저 가져갔을 때 · 배낭에 다 못 넣었을 때 · 도중에
// 짐을 내려놓았을 때**의 처리가 들어 있다. 복사하면 그중 하나가 반드시 뒤처진다.
//
// 그래서 **받는 타입만 인터페이스로 벌린다.** 구현체는 둘 다 `MonoBehaviour`라
// `SetSite`·`WorkSpot.TileFor`가 그대로 통한다.
public interface IConstructionSite
{
    // 자리 — `Component`라야 `WorkSpot.TileFor`가 설 칸을 낸다.
    Transform transform { get; }

    // **여기서 무슨 동작을 하나.** `ConstructionJob` 안에서 `BuildingData.LevelFloor`를
    // 물어보던 자리다 — 자리가 둘이 된 이상 **그 자리가 답해야 한다**(작업대 업그레이드를
    // 바닥 깔기 동작으로 두들기면 허공을 친다).
    WorkAnim BuildAnim { get; }

    // 실제로 들어온 만큼만 넘어온다 — 폰이 도중에 내려놓았을 수 있다.
    void Deliver(ItemData item, int amount);

    // 일감이 끝났다(성공·실패·취소 전부). 다음 재료로 넘어갈지는 자리가 정한다.
    void OnJobEnded();
}
