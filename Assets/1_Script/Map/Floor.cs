using System.Collections.Generic;
using UnityEngine;

// 바닥(floor) — 코어와 무관한 건설 substrate (모델 A). 자기 footprint 타일을 MapManager._floored에 등록.
// 씬 사전 배치 바닥·런타임 배치 바닥 공용. ResourceNode의 점유 등록 패턴과 동일 구조.
// 건물은 바닥 위(floored)에만 배치된다(MapManager.IsBuildable). 바닥은 통행을 막지 않는다(점유 X).
[DefaultExecutionOrder(-45)] // MapManager(-50) 이후 → Instance/존 준비됨
public class Floor : MonoBehaviour
{
    [Tooltip("바닥이 덮는 타일 크기(칸). Renderer가 있으면 그 XZ 범위를 자동 사용.")]
    [SerializeField] Vector2Int sizeTiles = new(1, 1);
    [Tooltip("Renderer 바운드로 덮는 타일을 자동 산출(권장). 끄면 sizeTiles + 위치 기준.")]
    [SerializeField] bool autoFromRenderer = true;

    readonly List<Vector2Int> _tiles = new();
    MapManager _zone;

    // **손으로 놓은 바닥은 제 메시가 있다.** `FloorRenderer`가 `_floored`를 통째로 그리므로,
    // 여기 등록한 칸까지 그리면 슬래브가 두 장 겹쳐 z-파이팅이 난다. 어느 칸이 저작물인지는
    // 이 집합 하나가 답한다 — 그리는 쪽이 씬을 뒤지게 하면 안 된다.
    static readonly HashSet<Vector2Int> _authored = new();

    public static bool IsAuthored(Vector2Int tile) => _authored.Contains(tile);

    void OnEnable()
    {
        _zone = MapManager.GetForPosition(transform.position);
        Register();
    }

    // 배치 직후 위치를 보정(표면 스냅)한 경우처럼, 등록 좌표·높이를 다시 계산해야 할 때 호출.
    public void Refresh()
    {
        if (_zone == null) return;
        foreach (var t in _tiles) { _authored.Remove(t); _zone.UnregisterFloor(t); }
        _tiles.Clear();
        Register();
    }

    void Register()
    {
        if (_zone == null) return;
        ComputeTiles();
        float surfaceY = ComputeSurfaceY();
        foreach (var t in _tiles) { _authored.Add(t); _zone.RegisterFloor(t, surfaceY); }
    }

    // 바닥 윗면 월드 Y — 건물이 이 위에 얹힌다. 렌더러가 있으면 바운드 상단(슬래브 두께 반영),
    // 없으면 피벗 높이. 경사 바닥은 지원하지 않음(타일당 단일 높이).
    float ComputeSurfaceY()
    {
        if (TryGetComponent<Renderer>(out var r)) return r.bounds.max.y;
        return transform.position.y;
    }

    void OnDisable()
    {
        foreach (var t in _tiles)
        {
            _authored.Remove(t);
            if (_zone != null) _zone.UnregisterFloor(t);
        }
        _tiles.Clear();
    }

    void ComputeTiles()
    {
        _tiles.Clear();

        if (autoFromRenderer && TryGetComponent<Renderer>(out var r))
        {
            var b = r.bounds;

            // **칸을 묻는 것은 `WorldToTile` 하나다**(2026-08-28). 전에는 월드 좌표를
            // 그대로 `Floor`/`Ceil`했는데, 그것은 **격자가 세계 원점에 박혀 있을 때만**
            // 맞는 계산이다. 08-27에 위상이 첫 핵으로 옮겨 간 뒤로 1×1 슬래브 하나가
            // **네 칸을 등록했고**(위상이 0이 아니면 `Ceil(x+1+위상)-1 = x+1`), 그래서
            // 면을 드래그로 깔면 **한 칸 건너 하나씩만** 깔렸다(나머지는 "이미 바닥").
            //
            // 모서리에서 반 칸 안으로 들어와 묻는다 — 경계에 정확히 걸린 값이 부동소수
            // 오차로 옆 칸에 떨어지는 것도 같이 막는다.
            var lo = _zone.WorldToTile(new Vector3(b.min.x + 0.5f, 0f, b.min.z + 0.5f));
            var hi = _zone.WorldToTile(new Vector3(b.max.x - 0.5f, 0f, b.max.z - 0.5f));
            for (int x = lo.x; x <= hi.x; x++)
            for (int z = lo.y; z <= hi.y; z++)
                _tiles.Add(new Vector2Int(x, z));
            return;
        }

        var c = _zone.WorldToTile(transform.position);
        int hx = sizeTiles.x / 2, hz = sizeTiles.y / 2;
        for (int dx = -hx; dx < sizeTiles.x - hx; dx++)
        for (int dz = -hz; dz < sizeTiles.y - hz; dz++)
            _tiles.Add(new Vector2Int(c.x + dx, c.y + dz));
    }
}
