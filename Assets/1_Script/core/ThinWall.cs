using UnityEngine;

// **얇은 벽 — 칸 한쪽 변에 붙어 선다**(2026-10-01, `EdgeWalls` · 사용자: *"건물 벽은 얇고 한쪽에 딱 붙는 느낌으로"*).
//
// 막는 것은 **건물 앞(+Z)이 가리키는 변 하나**다 — 칸은 비어 있어 그 칸에 침대·작업대가 같이 선다. 모양(프리팹의
// 몸통)은 그 변에 붙은 얇은 판이고, 콜라이더는 `Default` 층이다: 몸은 부딪히되 칸마다 쏘는 장애물 검사(`Wall` 층)에는
// 안 걸린다 — 걸리면 판이 든 칸이 통째로 막혀 얇은 벽을 둔 뜻이 사라진다.
//
// 등록은 `Start` · 해제는 `OnDestroy`(`FortWall`과 같은 이유 — 휴면으로 꺼질 때 빼면 영역 밖 벽이 길에서 사라진다).
// 청사진 고스트는 `Start` 전에 스크립트가 떨어지므로 길을 안 막는다 — **다 지어져야 막는다.**
[DisallowMultipleComponent]
public class ThinWall : MonoBehaviour
{
    Vector2Int _tile;
    int _dir;
    bool _registered;

    void Start() => Register();

    void OnDestroy() => Unregister();

    // 옮겨 갔을 때(`PlacedBuilding.MoveTo`) — 옛 변을 풀고 새 변을 막는다.
    public void Reregister()
    {
        Unregister();
        Register();
    }

    void Register()
    {
        var map = MapManager.Instance;
        if (map == null || _registered) return;

        _tile = map.WorldToTile(transform.position);
        _dir  = EdgeWalls.DirFromYaw(transform.eulerAngles.y);
        EdgeWalls.Add(_tile, _dir);
        _registered = true;
    }

    void Unregister()
    {
        if (!_registered) return;
        EdgeWalls.Remove(_tile, _dir);
        _registered = false;
    }
}
