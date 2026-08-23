using System.Collections.Generic;
using UnityEngine;

// **연구 그래프의 자리를 정하는 단일 출처**(2026-08-23).
//
// 노드를 놓는 곳(`ResearchTableUI`)과 선을 긋는 곳(`ResearchLinkGraphic`)이 각자 좌표를
// 계산하면 **선이 노드에 안 붙는다** — 이 저장소가 여러 번 겪은 *"답이 두 벌이면 어긋난다"*의
// 자리다. 여기서 한 번 계산하고 둘 다 그 결과를 읽는다.
public static class ResearchGraphLayout
{
    // **크기는 여기서 정하지 않는다.** 노드 크기는 **노드 프리팹**이, 간격·여백은 **인스펙터**가
    // 든다(`ResearchTableUI`) — 이 저장소의 *"모양은 씬이, 코드는 데이터만"*이다. 상수로 박으면
    // 노드를 조금 키우는 일에도 코드를 고치고 다시 빌드해야 한다.
    //
    // 다만 **선을 노드 가장자리에서 시작하려면** 그 크기를 알아야 하므로, 계산 결과에
    // 자리(`TopLeft`·`Center`)를 실어 노드와 선이 **같은 답**을 읽게 한다.

    public struct Slot
    {
        public int     Col, Row;
        public Vector2 TopLeft;   // Content 기준(오른쪽 +x, 아래쪽 −y)
        public Vector2 Center;
    }

    public class Result
    {
        public readonly Dictionary<ResearchData, Slot> Slots = new();
        public Vector2 Size;
        public Vector2 NodeSize;   // 선이 가장자리를 찾는 데 쓴다
    }

    // **깊이는 가장 긴 선행 사슬이다.** 선행 중 하나만 보면 여러 갈래가 합쳐지는 연구가
    // 제 선행보다 왼쪽에 놓여 선이 거꾸로 간다.
    public static Result Build(IReadOnlyList<ResearchData> all,
                               Vector2 nodeSize, Vector2 gap, Vector2 pad)
    {
        var result = new Result();
        if (all == null || all.Count == 0) return result;

        // 0이 들어오면 나누기가 아니라 **겹쳐 그리기**가 된다 — 노드가 전부 한 자리에 쌓인다.
        nodeSize = new Vector2(Mathf.Max(1f, nodeSize.x), Mathf.Max(1f, nodeSize.y));

        var known = new HashSet<ResearchData>();
        foreach (var r in all) if (r != null) known.Add(r);

        var depth    = new Dictionary<ResearchData, int>();
        var visiting = new HashSet<ResearchData>();
        foreach (var r in known) Depth(r, known, depth, visiting);

        // 열별로 모은다.
        int maxCol = 0;
        foreach (var kv in depth) maxCol = Mathf.Max(maxCol, kv.Value);

        var columns = new List<List<ResearchData>>();
        for (int i = 0; i <= maxCol; i++) columns.Add(new List<ResearchData>());
        foreach (var kv in depth) columns[kv.Value].Add(kv.Key);

        // 뒤 연구 목록 — 되돌아오는 패스가 쓴다.
        var children = new Dictionary<ResearchData, List<ResearchData>>();
        foreach (var r in known)
        {
            if (r.Prerequisites == null) continue;
            foreach (var p in r.Prerequisites)
            {
                if (p == null || !known.Contains(p)) continue;
                if (!children.TryGetValue(p, out var list))
                    children[p] = list = new List<ResearchData>();
                list.Add(r);
            }
        }

        // **행은 두 번 정한다.**
        //
        // ① 왼쪽부터 — 선행이 이미 자리를 잡은 뒤에야 *"부모 옆에 놓는다"*가 된다.
        // ② 오른쪽부터 되돌아오며 — ①만 하면 **부모가 제 자식보다 한참 아래**에 남는다
        //    (뿌리들의 순서는 이름이 정하는데 그 순서와 자식 자리는 아무 상관이 없다).
        //    실제로 제련술이 제 자식 둘보다 세 줄 아래 놓였고, 선이 길게 위로 꺾여 어느 선이
        //    어디로 가는지 읽을 수가 없었다. 되돌아오는 패스 한 번이면 부모가 자식 쪽으로 붙는다.
        var row = new Dictionary<ResearchData, int>();
        for (int c = 0; c <= maxCol; c++)
        {
            Order(columns[c], a => ParentRow(a, row), row);
        }
        for (int c = maxCol - 1; c >= 0; c--)
        {
            Order(columns[c], a => ChildRow(a, children, row), row);
        }

        int maxRow = 0;
        foreach (var kv in row) maxRow = Mathf.Max(maxRow, kv.Value);

        foreach (var r in known)
        {
            int c = depth[r], w = row[r];
            var tl = new Vector2(pad.x + c * (nodeSize.x + gap.x),
                               -(pad.y + w * (nodeSize.y + gap.y)));
            result.Slots[r] = new Slot
            {
                Col     = c,
                Row     = w,
                TopLeft = tl,
                Center  = tl + new Vector2(nodeSize.x * 0.5f, -nodeSize.y * 0.5f),
            };
        }

        result.NodeSize = nodeSize;
        result.Size = new Vector2(
            pad.x * 2 + (maxCol + 1) * nodeSize.x + maxCol * gap.x,
            pad.y * 2 + (maxRow + 1) * nodeSize.y + maxRow * gap.y);
        return result;
    }

    // 한 열을 키로 다시 줄 세우고 행 번호를 매긴다. **키가 같으면 지금 자리를 지킨다** —
    // `List.Sort`는 안정 정렬이 아니라, 동점끼리 매번 뒤바뀌면 열 때마다 그래프가 달라진다.
    static void Order(List<ResearchData> col, System.Func<ResearchData, float> key,
                      Dictionary<ResearchData, int> row)
    {
        var now = new Dictionary<ResearchData, int>();
        for (int i = 0; i < col.Count; i++) now[col[i]] = i;

        col.Sort((a, b) =>
        {
            float ka = key(a), kb = key(b);
            if (!Mathf.Approximately(ka, kb)) return ka.CompareTo(kb);
            int ra = now[a], rb = now[b];
            if (ra != rb) return ra.CompareTo(rb);
            return string.CompareOrdinal(Label(a), Label(b));
        });

        for (int i = 0; i < col.Count; i++) row[col[i]] = i;
    }

    // 뒤 연구들의 평균 행. 뒤가 없으면 **지금 자리를 그대로 키로 쓴다** — 잎을 위나 아래로
    // 몰면 되돌아오는 패스가 ①이 잡아 둔 것을 통째로 흐트러뜨린다.
    static float ChildRow(ResearchData r, Dictionary<ResearchData, List<ResearchData>> children,
                          Dictionary<ResearchData, int> row)
    {
        if (!children.TryGetValue(r, out var list) || list.Count == 0)
            return row.TryGetValue(r, out int self) ? self : 0f;

        float sum = 0f; int n = 0;
        foreach (var c in list)
            if (row.TryGetValue(c, out int v)) { sum += v; n++; }
        return n > 0 ? sum / n : (row.TryGetValue(r, out int s) ? s : 0f);
    }

    // 선행들의 평균 행. 없으면 맨 위로 보낸다(뿌리는 위에서부터 차례로).
    static float ParentRow(ResearchData r, Dictionary<ResearchData, int> row)
    {
        if (r.Prerequisites == null) return -1f;
        float sum = 0f; int n = 0;
        foreach (var p in r.Prerequisites)
            if (p != null && row.TryGetValue(p, out int v)) { sum += v; n++; }
        return n > 0 ? sum / n : -1f;
    }

    // **순환은 조용히 넘기지 않는다** — 저작으로 A→B→A를 만들 수 있고, 그러면 무한 재귀다.
    // 끊고 콘솔에 남긴다(안 남기면 그 연구가 왜 맨 왼쪽에 있는지 알 길이 없다).
    static int Depth(ResearchData r, HashSet<ResearchData> known,
                     Dictionary<ResearchData, int> memo, HashSet<ResearchData> visiting)
    {
        if (r == null) return 0;
        if (memo.TryGetValue(r, out int d)) return d;

        if (!visiting.Add(r))
        {
            Debug.LogWarning($"[연구] 선행 연구가 순환합니다 — `{Label(r)}`에서 끊었습니다.");
            memo[r] = 0;
            return 0;
        }

        int best = 0;
        if (r.Prerequisites != null)
            foreach (var p in r.Prerequisites)
            {
                // **이 연구대가 안 다루는 선행은 깊이에 안 센다** — 큐레이션된 목록에서
                // 그 선행만 빠지면 남은 것들이 통째로 오른쪽으로 밀린다.
                if (p == null || !known.Contains(p)) continue;
                best = Mathf.Max(best, Depth(p, known, memo, visiting) + 1);
            }

        visiting.Remove(r);
        memo[r] = best;
        return best;
    }

    static string Label(ResearchData r) =>
        r == null ? "?" : (string.IsNullOrEmpty(r.ResearchName) ? r.name : r.ResearchName);
}
