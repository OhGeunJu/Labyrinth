using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// **선을 한 장의 메시로 긋는다**(2026-08-23).
//
// 잇는 선마다 `Image`를 하나씩 만들면 연구가 스물 몇이 될 때 오브젝트가 그만큼 늘고, 목록을
// 다시 지을 때마다 전부 지웠다 만든다. `Graphic` 하나가 사각형들을 직접 뱉으면 **드로우콜도
// 하나고 다시 그리는 것도 정점만 갈아 끼우는 일**이다(`GridOverlay`가 즉시 모드를 버리고
// 메시로 간 것과 같은 자리).
//
// **레이캐스트를 안 받는다** — 선이 노드 위를 지나므로 클릭을 먹으면 그 노드를 못 누른다.
[RequireComponent(typeof(CanvasRenderer))]
public class ResearchLinkGraphic : MaskableGraphic
{
    public struct Link
    {
        public Vector2 From;    // 선행 노드의 오른쪽 가장자리
        public Vector2 To;      // 뒤 연구의 왼쪽 가장자리
        public bool    Done;    // 선행이 끝났나 — 색이 갈린다
    }

    [SerializeField] float thickness = 2f;

    static readonly Color DoneColor   = new(0.45f, 0.80f, 0.45f, 0.95f);
    static readonly Color UndoneColor = new(0.45f, 0.45f, 0.45f, 0.70f);

    readonly List<Link> _links = new();

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetLinks(List<Link> links)
    {
        _links.Clear();
        if (links != null) _links.AddRange(links);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_links.Count == 0) return;

        foreach (var link in _links)
        {
            var color = link.Done ? DoneColor : UndoneColor;

            // **직각으로 꺾어 잇는다**(림월드식). 곧은 대각선은 열이 늘어날수록 서로 겹쳐
            // 어느 선이 어디로 가는지 못 읽는다. 꺾으면 세로 구간이 열과 열 사이에 모인다.
            float midX = (link.From.x + link.To.x) * 0.5f;

            Segment(vh, link.From, new Vector2(midX, link.From.y), color);
            Segment(vh, new Vector2(midX, link.From.y), new Vector2(midX, link.To.y), color);
            Segment(vh, new Vector2(midX, link.To.y), link.To, color);
        }
    }

    // 두 점을 잇는 굵기 있는 사각형 하나. **가로·세로만 들어오므로** 일반 회전을 안 쓴다 —
    // 법선을 구해 두께를 밀면 짧은 구간에서 모서리가 벌어진다.
    void Segment(VertexHelper vh, Vector2 a, Vector2 b, Color color)
    {
        float half = thickness * 0.5f;
        Vector2 min, max;

        if (Mathf.Approximately(a.y, b.y))
        {
            min = new Vector2(Mathf.Min(a.x, b.x), a.y - half);
            max = new Vector2(Mathf.Max(a.x, b.x), a.y + half);
        }
        else
        {
            // 꺾이는 자리를 반 두께만큼 늘려 이음매에 구멍이 안 나게 한다.
            min = new Vector2(a.x - half, Mathf.Min(a.y, b.y) - half);
            max = new Vector2(a.x + half, Mathf.Max(a.y, b.y) + half);
        }

        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(min.x, min.y), color, Vector2.zero);
        vh.AddVert(new Vector3(min.x, max.y), color, Vector2.zero);
        vh.AddVert(new Vector3(max.x, max.y), color, Vector2.zero);
        vh.AddVert(new Vector3(max.x, min.y), color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }
}
