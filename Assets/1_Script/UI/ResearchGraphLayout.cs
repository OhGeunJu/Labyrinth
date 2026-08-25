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

        List<List<ResearchData>> columns = new();
        for (int i = 0; i <= maxCol; i++) columns.Add(new List<ResearchData>());
        foreach (var kv in depth) columns[kv.Value].Add(kv.Key);

        // ── 자리는 **저작이 먼저, 자동은 남는 칸에** ────────────────────────────
        //
        // 저작된 것(`HasAuthoredPos`)은 적힌 칸을 그대로 쓴다 — **열까지 저작이 이긴다.**
        // 그래야 *"선행이 없는데 한가운데에서 시작하는 연구"*가 표현된다(림월드가 그렇다).
        // 자동 배치는 열을 선행 깊이로 정하므로 그런 연구를 **전부 맨 왼쪽에 쌓는다.**
        //
        // 남은 것은 예전 규칙대로 놓되 **저작된 칸을 피한다**:
        //   ① 뿌리는 새 행을 하나씩 받는다.
        //   ② 자식은 **부모의 행을 물려받는다** → 사슬이 가로로 곧게 뻗는다.
        //   ③ 한 부모는 **한 번만 물려준다** → 둘째 자식부터 새 행으로 갈라진다.
        var row     = new Dictionary<ResearchData, int>();
        var donated = new HashSet<ResearchData>();
        var taken   = new HashSet<(int, int)>();   // 이미 찬 칸
        int nextRow = 0;

        // 저작된 것을 먼저 앉힌다 — 자동이 그 자리를 피할 수 있게.
        foreach (var r in known)
        {
            if (!r.HasAuthoredPos) continue;
            depth[r] = r.GraphColumn;             // **열도 저작이 이긴다**
            row[r]   = r.GraphRow;
            if (!taken.Add((r.GraphColumn, r.GraphRow)))
                Debug.LogWarning($"[연구] 저작된 자리가 겹칩니다 — `{Label(r)}` ({r.GraphColumn}, {r.GraphRow}).");
            nextRow = Mathf.Max(nextRow, r.GraphRow + 1);
            maxCol  = Mathf.Max(maxCol, r.GraphColumn);
        }

        // 저작으로 열이 바뀌었을 수 있으니 열 목록을 다시 만든다.
        columns = new List<List<ResearchData>>();
        for (int i = 0; i <= maxCol; i++) columns.Add(new List<ResearchData>());
        foreach (var r in known) columns[depth[r]].Add(r);

        for (int c = 0; c <= maxCol; c++)
        {
            // **부모가 앉은 자리 순으로 훑는다** — 위쪽 사슬의 자식이 위쪽에 남는다.
            var col = columns[c];
            col.Sort((x, y) =>
            {
                float kx = ParentRow(x, row), ky = ParentRow(y, row);
                if (!Mathf.Approximately(kx, ky)) return kx.CompareTo(ky);
                return string.CompareOrdinal(Label(x), Label(y));
            });

            foreach (var r in col)
            {
                if (r.HasAuthoredPos) continue;   // 이미 앉았다

                var heir = PrimaryParent(r, known, depth, row, donated);
                int want = heir != null ? row[heir] : nextRow;
                if (heir != null) donated.Add(heir);

                // 원하는 자리가 찼으면 **아래로 내려가며** 빈 칸을 찾는다.
                while (taken.Contains((c, want))) want++;

                row[r] = want;
                taken.Add((c, want));
                nextRow = Mathf.Max(nextRow, want + 1);
            }
        }

        // **거꾸로 가는 선은 저작 실수다** — 자리를 손으로 잡으면 선행을 오른쪽에 두는 일이
        // 생기고, 그러면 선이 왼쪽으로 흘러 어디서 어디로 가는지 못 읽는다. 화면만 보고는
        // *"원래 저런 건가"*와 구별이 안 되므로 콘솔에 적는다.
        foreach (var r in known)
        {
            if (r.Prerequisites == null) continue;
            foreach (var p in r.Prerequisites)
            {
                if (p == null || !known.Contains(p)) continue;
                if (depth[p] >= depth[r])
                    Debug.LogWarning($"[연구] `{Label(r)}`(열 {depth[r]})이 선행 `{Label(p)}`(열 {depth[p]})보다 " +
                                     "왼쪽이거나 같은 열입니다 — 선이 거꾸로 갑니다.");
            }
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

    // **누가 제 행을 물려주나** — 가장 가까운(열이 큰) 선행 중 아직 안 물려준 것.
    //
    // 가까운 쪽을 고르는 이유: 사슬은 바로 앞 마디에서 이어지는 것이 눈에 자연스럽고, 멀리
    // 있는 선행의 행을 물려받으면 **그 사이 열을 가로지르는 긴 선**이 생긴다.
    static ResearchData PrimaryParent(ResearchData r, HashSet<ResearchData> known,
                                      Dictionary<ResearchData, int> depth,
                                      Dictionary<ResearchData, int> row,
                                      HashSet<ResearchData> donated)
    {
        if (r.Prerequisites == null) return null;

        ResearchData best = null;
        int bestCol = -1, bestRow = int.MaxValue;
        foreach (var p in r.Prerequisites)
        {
            if (p == null || !known.Contains(p)) continue;
            if (donated.Contains(p) || !row.ContainsKey(p)) continue;

            int pc = depth[p], pr = row[p];
            // 열이 큰 쪽(가까운 쪽)이 먼저, 같으면 위쪽이 먼저 — 답이 흔들리지 않게.
            if (pc > bestCol || (pc == bestCol && pr < bestRow))
            {
                best = p; bestCol = pc; bestRow = pr;
            }
        }
        return best;
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
