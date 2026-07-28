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

    void OnEnable()
    {
        _zone = MapManager.GetForPosition(transform.position);
        Register();
    }

    // 배치 직후 위치를 보정(표면 스냅)한 경우처럼, 등록 좌표·높이를 다시 계산해야 할 때 호출.
    public void Refresh()
    {
        if (_zone == null) return;
        foreach (var t in _tiles) _zone.UnregisterFloor(t);
        _tiles.Clear();
        Register();
    }

    void Register()
    {
        if (_zone == null) return;
        ComputeTiles();
        float surfaceY = ComputeSurfaceY();
        foreach (var t in _tiles) _zone.RegisterFloor(t, surfaceY);
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
        if (_zone != null)
            foreach (var t in _tiles) _zone.UnregisterFloor(t);
        _tiles.Clear();
    }

    void ComputeTiles()
    {
        _tiles.Clear();

        if (autoFromRenderer && TryGetComponent<Renderer>(out var r))
        {
            var b = r.bounds;
            int x0 = Mathf.FloorToInt(b.min.x), x1 = Mathf.CeilToInt(b.max.x) - 1;
            int z0 = Mathf.FloorToInt(b.min.z), z1 = Mathf.CeilToInt(b.max.z) - 1;
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
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
