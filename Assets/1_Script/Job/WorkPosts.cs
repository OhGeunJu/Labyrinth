using System;
using System.Collections.Generic;
using UnityEngine;

// **배치 — 폰을 건물에 붙박이로 둔다, 세 순위까지**(2026-09-29, 사용자: *"팰월드는 건물에 배치하는게 하나여서 그 하나를
//  더 못하는 상황이 오면 그냥 놀잖아 … 3순위까지 만들어서 하나를 끝내면 2순위를 하고 그것도 끝내면 3순위를"*).
//
// ## 규칙
//
// - **칸 셋**(1 · 2 · 3순위). 일꾼은 **위에서부터 할 일이 있는 첫 칸**을 한다(`WorkNode`). 아래 칸 일을 하는 중에도
//   몇 초마다 위를 다시 본다 — 1순위에 일이 생기면 돌아간다(하던 일감은 끝내고).
// - **셋 다 할 일이 없으면 논다**(사용자 결정) — 다른 일에 안 끌려가고 자리를 지킨다. 밥·잠은 그대로다(트리의 앞 가지).
// - **칸이 하나도 없으면 이 컴포넌트가 없는 것과 같다** — 작업 설정(`P`)대로 알아서 일한다(사용자: *"배정이 안되어있는
//   폰은 지금처럼"*).
// - **배치는 명령이다** — 켜고 끄기(`WorkSettings.IsEnabled`)는 안 보고, **못 하는 일**(`IsBlocked` — 배경 · 종족)만 막는다
//   (손으로 붙인 자원 노드 배정과 같은 규칙).
// - **자기 배낭 비우기는 늘 한다**(`Unload`) — 캔 것을 못 내려놓아 캐기가 멎는다.
//
// ## 칸에 넣을 수 있는 것 — 넷
//
//   `Node`    지은 자원 노드(광맥 · 버섯 밭) — 그 노드를 캔다(`ResourceNode.Assign`)
//   `Station` 작업대 · 연구대 · 도축대 — **그 대에 걸린 일감만**(`JobBase.Site`)
//   `Farm`    밭 구역 — 넣은 밭과 **붙은 구역 전체**의 씨 뿌리기 · 물 · 수확(`FarmZones.Of` — 구역에 번호가 따로 없다)
//   `Haul`    운반 그 자체 — 건물이 아니다(사용자: *"창고가 아니라 그냥 운반 그 자체"*)
public enum PostKind { None, Node, Station, Farm, Haul }

[Serializable]
public struct WorkPost
{
    public PostKind  Kind;
    public Component Target;   // Node · Station · Farm의 대상. Haul은 비운다.

    public static WorkPost Haul => new() { Kind = PostKind.Haul };

    public bool IsSet => Kind == PostKind.Haul || (Kind != PostKind.None && Target != null);

    // 이 칸이 이 일감을 받나 — 노드 칸은 일감이 아니라 노드를 직접 캐므로 여기서는 늘 거짓이다.
    public bool Takes(JobBase job)
    {
        if (job == null) return false;
        switch (Kind)
        {
            case PostKind.Station: return Target != null && job.Site == Target;
            case PostKind.Farm:
                if (Target is not FarmPlot mine || job.Site is not FarmPlot plot) return false;
                if (plot == mine) return true;
                foreach (var p in FarmZones.Of(mine)) if (p == plot) return true;
                return false;
            case PostKind.Haul: return job.Category == JobCategory.Hauling;
            default: return false;
        }
    }
}

[DisallowMultipleComponent]
public class WorkPosts : MonoBehaviour
{
    public const int Count = 3;

    [SerializeField] WorkPost[] slots = new WorkPost[Count];

    public static event Action<WorkPosts> OnChanged;

    public WorkPost this[int i] => slots != null && i >= 0 && i < slots.Length ? slots[i] : default;

    // 하나라도 칸이 섰나 — 아니면 평소처럼 일한다.
    public bool Any
    {
        get
        {
            if (slots == null) return false;
            foreach (var s in slots) if (s.IsSet) return true;
            return false;
        }
    }

    public void Set(int i, WorkPost post)
    {
        if (slots == null || slots.Length != Count) Array.Resize(ref slots, Count);
        if (i < 0 || i >= Count) return;
        slots[i] = post;
        OnChanged?.Invoke(this);
    }

    public void Clear(int i) => Set(i, default);

    // 폰의 배치 — 없으면 `null`(평소처럼 일한다). **붙이는 것은 쓰는 쪽이다**(`For`) — 칸을 한 번도 안 만진 폰에는 안 붙는다.
    public static WorkPosts Of(UnitBrain brain) =>
        brain != null && brain.TryGetComponent<WorkPosts>(out var p) ? p : null;

    public static WorkPosts For(UnitBrain brain) =>
        brain == null ? null : Of(brain) ?? brain.gameObject.AddComponent<WorkPosts>();

    // ── 이름 ────────────────────────────────────────────────────────────────

    public static string Label(WorkPost p) => p.Kind switch
    {
        PostKind.Haul    => "운반",
        PostKind.Node    => p.Target is ResourceNode n ? NodeName(n) : "(없어짐)",
        PostKind.Station => p.Target != null ? BuildingName(p.Target) : "(없어짐)",
        PostKind.Farm    => p.Target != null ? "밭 구역" : "(없어짐)",
        _                => "비어 있음",
    };

    static string NodeName(ResourceNode n)
    {
        var pb = n.GetComponentInParent<PlacedBuilding>();
        if (pb != null && pb.Data != null) return pb.Data.BuildingName;
        var d = n.Data;
        return d != null && !string.IsNullOrEmpty(d.DisplayName) ? d.DisplayName : n.name;
    }

    static string BuildingName(Component c)
    {
        var pb = c.GetComponentInParent<PlacedBuilding>();
        return pb != null && pb.Data != null ? pb.Data.BuildingName : c.name;
    }

    // ── 넣을 수 있는 건물 목록 — 핵 창이 드롭다운을 채운다 ─────────────────
    //
    // **핵 영역 안의 것만** — 배치는 거점의 일이다(영역 밖 건물은 휴면이라 일감도 안 난다).
    // 같은 이름이 여럿이면 **가까운 순으로 번호**를 붙인다(`석공 작업대 2`) — 목록에서 무엇이 어느 것인지 가를 길이 그것뿐이다.
    public static List<(string label, WorkPost post)> Candidates()
    {
        var list = new List<(string, WorkPost)> { ("운반", WorkPost.Haul) };
        var core = DungeonCore.Instance;
        bool In(Component c) => c != null && (core == null || core.IsWithinAnyRangeWorld(c.transform.position));

        var named = new List<(string name, WorkPost post, Vector3 at)>();

        foreach (var n in ResourceNode.All)
            if (n != null && n.IsBuilt && In(n))
                named.Add((NodeName(n), new WorkPost { Kind = PostKind.Node, Target = n }, n.transform.position));

        foreach (var pb in PlacedBuilding.All)
        {
            if (pb == null || !In(pb)) continue;
            Component station = pb.GetComponent<CraftingTable>();
            if (station == null) station = pb.GetComponent<ResearchTable>();
            if (station == null) station = pb.GetComponent<ButcherTable>();
            if (station != null)
                named.Add((BuildingName(station), new WorkPost { Kind = PostKind.Station, Target = station }, pb.transform.position));
        }

        // 밭은 **구역마다 하나** — 붙은 밭은 한 구역이다(`FarmZones`).
        var seen = new HashSet<FarmPlot>();
        foreach (var plot in FindObjectsByType<FarmPlot>(FindObjectsSortMode.None))
        {
            if (plot == null || !In(plot) || seen.Contains(plot)) continue;
            foreach (var p in FarmZones.Of(plot)) seen.Add(p);
            named.Add(("밭 구역", new WorkPost { Kind = PostKind.Farm, Target = plot }, plot.transform.position));
        }

        // 같은 이름은 핵에서 가까운 순으로 번호.
        var from = core != null ? core.transform.position : Vector3.zero;
        named.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.name, b.name);
            return c != 0 ? c : (a.at - from).sqrMagnitude.CompareTo((b.at - from).sqrMagnitude);
        });
        var counts = new Dictionary<string, int>();
        foreach (var e in named) counts[e.name] = counts.TryGetValue(e.name, out int k) ? k + 1 : 1;
        var index = new Dictionary<string, int>();
        foreach (var e in named)
        {
            int i = index.TryGetValue(e.name, out int k) ? k + 1 : 1;
            index[e.name] = i;
            list.Add((counts[e.name] > 1 ? $"{e.name} {i}" : e.name, e.post));
        }
        return list;
    }
}
