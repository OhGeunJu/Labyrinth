using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMesh 기반 경로 탐색.
/// 오픈월드 전체에서 동작 — MapManager 타일 그리드와 무관.
/// 사용 전 씬에 NavMesh가 베이크되어 있어야 함.
/// </summary>
public static class Pathfinder
{
    const float SnapRadius = 5f;

    /// <summary>
    /// NavMesh 위에서 fromWorld → toWorld 경로 탐색. 월드 좌표 웨이포인트 리스트 반환.
    /// 완전한 경로가 없으면 null (PawnMover가 직선 이동으로 폴백).
    /// </summary>
    public static List<Vector3> FindPath(Vector3 fromWorld, Vector3 toWorld)
    {
        if (!NavMesh.SamplePosition(fromWorld, out var fromHit, SnapRadius, NavMesh.AllAreas)) return null;
        if (!NavMesh.SamplePosition(toWorld,   out var toHit,   SnapRadius, NavMesh.AllAreas)) return null;

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, path))
            return null;
        if (path.status != NavMeshPathStatus.PathComplete) return null;

        var corners = path.corners;
        if (corners == null || corners.Length < 2) return null;

        var result = new List<Vector3>(corners.Length);
        foreach (var c in corners) result.Add(c);
        result[result.Count - 1] = toWorld; // 마지막은 정확한 목표 좌표
        return result;
    }
}
