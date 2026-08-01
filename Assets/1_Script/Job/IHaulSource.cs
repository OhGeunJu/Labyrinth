using UnityEngine;

// 폰이 짐을 실어 갈 수 있는 곳.
//
// `HaulJob`은 출처를 이것으로만 안다 — **인벤토리가 있고 위치가 있으면 같은 운반**이기 때문이다.
// 전에는 `ResourceNode`로 못 박혀 있어서 작업대 산출물을 옮기려면 같은 일을 하는 잡을 하나 더
// 써야 했다. 출처의 종류가 늘 때마다 운반 규칙(잔량 보존·취소 시 내려놓기)을 베껴 쓰면
// 그중 하나는 반드시 어긋난다.
public interface IHaulSource
{
    Inventory  Inventory { get; }
    Vector2Int TileCoord { get; }
    Transform  transform { get; }   // MonoBehaviour가 이미 갖고 있다
}
