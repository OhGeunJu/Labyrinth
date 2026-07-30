using UnityEngine;

// 월드맵 3D 타일이 자기가 어느 노드인지 들고 있게 하는 표식.
// 클릭 레이캐스트가 맞은 콜라이더에서 **좌표를 역산하지 않고 바로 읽도록** 하는 용도 —
// 타일이 프리팹(경사·불규칙 형상)으로 바뀌어도 판정이 어긋나지 않는다.
public class WorldTileRef : MonoBehaviour
{
    public Vector2Int Node;
}
