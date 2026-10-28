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
    // **[Serializable]이라야 씬에 남는다**(2026-08-25). 그전에는 런타임에만 채워져서
    // **에디터의 연구 창이 텅 비어 보였다** — 자리를 저작해 놓고도 플레이를 눌러야만
    // 결과를 볼 수 있었다.
    [System.Serializable]
    public struct Link
    {
        public Vector2 From;    // 선행 노드의 오른쪽 가장자리
        public Vector2 To;      // 뒤 연구의 왼쪽 가장자리
        public bool    Done;    // 선행이 끝났나 — 색이 갈린다
    }

    [SerializeField] float thickness = 2f;

    static readonly Color DoneColor   = new(0.45f, 0.80f, 0.45f, 0.95f);
    static readonly Color UndoneColor = new(0.45f, 0.45f, 0.45f, 0.70f);

    // **`readonly`면 유니티가 직렬화하지 않는다.** 구워 둔 선이 씬에 남아야 에디터에서
    // 그래프가 보인다(`ResearchGraphBaker`가 채운다). 런타임은 그대로 덮어쓴다.
    [SerializeField] List<Link> _links = new();

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

        // **곧게 잇는다**(2026-08-25, 사용자 *"선을 직각으로 하지말고 그냥 바로 이어봐 림월드는
        // 그렇게 하던데"*).
        //
        // 직각으로 꺾던 시절의 진짜 문제는 미관이 아니라 **거짓말**이었다: 꺾인 선의 세로 구간이
        // 두 열 사이 한가운데 서는데, 그 자리가 **왼쪽 열 노드의 바로 오른쪽**이라 상관없는
        // 노드에서 선이 나가는 것처럼 보였다. 실제로 `목공술 → 석공술` 선이 `가죽 세공` 옆을
        // 지나 **가죽 세공에서 석공술로 이어진 것처럼** 읽혔다.
        //
        // 곧게 이으면 그 착시가 통째로 없어진다 — **선은 반드시 제 부모의 오른쪽 끝에서
        // 출발한다.** 그리고 이웃 열끼리 잇는 선은 **어떤 노드도 가로지를 수 없다**(두 열
        // 사이에는 노드가 없다). 통로를 비워 두던 규칙도 그래서 사라진다.
        foreach (var link in _links)
            Segment(vh, link.From, link.To, link.Done ? DoneColor : UndoneColor);
    }

    // 두 점을 잇는 굵기 있는 사각형 하나. **어느 방향이든 된다** — 진행 방향의 법선으로
    // 두께를 밀어 낸다(가로·세로만 그리던 시절의 특수 처리는 필요 없어졌다).
    void Segment(VertexHelper vh, Vector2 a, Vector2 b, Color color)
    {
        Vector2 d   = b - a;
        float   len = d.magnitude;
        if (len < 0.001f) return;               // 겹쳐 있으면 방향이 없다

        Vector2 n = new Vector2(-d.y, d.x) * (thickness * 0.5f / len);

        int i = vh.currentVertCount;
        vh.AddVert(a - n, color, Vector2.zero);
        vh.AddVert(a + n, color, Vector2.zero);
        vh.AddVert(b + n, color, Vector2.zero);
        vh.AddVert(b - n, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }
}
