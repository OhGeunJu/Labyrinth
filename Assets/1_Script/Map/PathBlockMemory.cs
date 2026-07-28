using System.Collections.Generic;
using UnityEngine;

// 길찾기 그래프에 없는 것(다른 폰 등)에 막혔던 자리를 잠깐 기억한다.
// 로컬 윈도우 베이크가 이 타일을 통행 불가로 보고 경로를 다시 뽑으므로,
// 폰이 같은 지점에 계속 부딪히며 좌우로 흔들리는 대신 **플래너가 우회로를 낸다.**
//
// 모든 폰이 공유한다 — 한 폰이 막힌 자리는 다른 폰도 잠시 피하는 게 맞다(정체 구간).
public static class PathBlockMemory
{
    // 이 시간이 지나면 자동으로 풀린다. 막은 원인(폰)이 대개 곧 비켜나므로 짧게 유지.
    public const float BlockDuration = 3f;

    static readonly Dictionary<Vector2Int, float> _blocked = new();

    public static void Block(Vector2Int tile) => _blocked[tile] = Time.time + BlockDuration;

    public static void Block(Vector3 world)
    {
        var map = MapManager.GetForPosition(world);
        if (map == null) return;
        Block(map.WorldToTile(world));
    }

    public static bool IsBlocked(Vector2Int tile)
    {
        if (_blocked.Count == 0) return false;   // 베이크가 셀마다 부르는 경로 — 비었으면 즉시 탈출
        if (!_blocked.TryGetValue(tile, out float until)) return false;
        if (Time.time < until) return true;

        _blocked.Remove(tile);
        return false;
    }

    public static void Clear() => _blocked.Clear();
}
