using System;
using System.Collections.Generic;
using UnityEngine;

// 월드맵에 세운 채집기지 하나.
// **사람이 살지 않는다** — 거점(정착지)과 달리 들어가서 지내는 곳이 아니라, 매일 알아서 캐서 쌓아두는 곳이다.
public class WorldOutpost
{
    public Vector2Int Node;

    // 아직 땅에 남은 것. 다 캐면 고갈된다.
    public readonly List<WorldDeposit> Remaining = new();

    // 캐서 쌓아둔 것. 원정대가 들러 실어간다.
    public readonly Dictionary<ItemData, int> Stock = new();

    public bool IsDepleted
    {
        get
        {
            foreach (var d in Remaining) if (d.Amount > 0) return false;
            return true;
        }
    }

    public int StockCount
    {
        get { int n = 0; foreach (var kv in Stock) n += kv.Value; return n; }
    }

    public int RemainingCount
    {
        get { int n = 0; foreach (var d in Remaining) n += d.Amount; return n; }
    }
}

// 채집기지의 단일 소유자 — 세우기 / 매일 캐기 / 비축분 넘기기.
//
// **필드맵을 만들지 않는다.** "자동 건설"은 플레이어가 들어가 배치하지 않아도 굴러가는 길이고,
// 그래서 지도 위의 추상 시설로만 존재한다. 들어가서 직접 짓는 길(직접 건설)은 별도이며
// 그건 필드맵 생성이 전제라 아직 없다.
//
// 자원은 **기지에 쌓인다** — 본거지로 순간이동하지 않는다. 원정대가 들러 등짐에 싣고 돌아와야 한다
// (설계 `design_world_field.md`의 "거점 간 물류: 1차는 파티 등짐만"과 같은 규칙).
[DefaultExecutionOrder(-25)]
public class OutpostManager : MonoBehaviour
{
    public static OutpostManager Instance { get; private set; }

    [Serializable]
    public class CostEntry
    {
        public ItemData Item;
        [Min(0)] public int Amount = 10;
    }

    [Header("건설 비용 (비우면 무료)")]
    [Tooltip("자동 건설에 드는 자원. 활성 거점 창고에서 빠져나간다.")]
    [SerializeField] List<CostEntry> buildCost = new();

    [Header("채굴")]
    [Tooltip("기지 하나가 하루에 캐는 총량.")]
    [Min(1)] [SerializeField] int dailyYield = 5;

    readonly List<WorldOutpost> _outposts = new();

    public static event Action OnChanged;

    public IReadOnlyList<CostEntry> BuildCost => buildCost;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable()  => GameTimeManager.OnDayChanged += MineAll;
    void OnDisable() => GameTimeManager.OnDayChanged -= MineAll;

    public WorldOutpost At(Vector2Int node)
    {
        foreach (var o in _outposts) if (o.Node == node) return o;
        return null;
    }

    // ── 건설 ─────────────────────────────────────────────────────────────────

    // 비용을 낼 수 있는가. 창고가 없거나 모자라면 못 짓는다.
    public bool CanAfford(out string missing)
    {
        missing = null;
        foreach (var c in buildCost)
        {
            if (c == null || c.Item == null || c.Amount <= 0) continue;
            if (MaterialPool.AvailableHere(c.Item) < c.Amount)
            {
                missing = ItemName(c.Item);
                return false;
            }
        }
        return true;
    }

    // 비용 문구 — 메뉴에서 "무엇이 드는지"를 그대로 보여주기 위한 것.
    public string CostSummary()
    {
        if (buildCost.Count == 0) return "비용 없음";

        var sb = new System.Text.StringBuilder();
        foreach (var c in buildCost)
        {
            if (c == null || c.Item == null || c.Amount <= 0) continue;
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append($"{ItemName(c.Item)} {MaterialPool.AvailableHere(c.Item)}/{c.Amount}");
        }
        return sb.Length == 0 ? "비용 없음" : sb.ToString();
    }

    // 그 칸에 채집기지를 세운다. 매장량을 실어 두고 지도 내용물을 바꾼다.
    public WorldOutpost Build(Vector2Int node)
    {
        var wm = WorldMap.Instance;
        if (wm == null || At(node) != null) return null;

        if (!CanAfford(out var missing))
        {
            Debug.LogWarning($"[채집기지] 자원이 모자랍니다 — {missing}");
            return null;
        }

        foreach (var c in buildCost)
        {
            if (c == null || c.Item == null || c.Amount <= 0) continue;
            Storage.ConsumeByItem(c.Item, c.Amount, MaterialPool.Focus);
        }

        var outpost = new WorldOutpost { Node = node };
        outpost.Remaining.AddRange(wm.DepositsAt(node));
        _outposts.Add(outpost);

        // 지도에 표시 — `SetContent`가 `OnNodeChanged`를 쏴서 마커가 새로 선다.
        wm.SetContent(node, WorldNodeContent.Outpost);

        Debug.Log($"[채집기지] ({node.x},{node.y})에 세웠습니다 — 매장 {outpost.RemainingCount}");
        OnChanged?.Invoke();
        return outpost;
    }

    // ── 매일 채굴 ────────────────────────────────────────────────────────────

    // 하루치를 남은 매장에서 **골고루** 덜어 비축으로 옮긴다.
    // 한 자원만 먼저 파내면 "이 기지는 무엇을 주는가"가 날마다 달라져 읽기 어렵다.
    void MineAll()
    {
        bool changed = false;

        foreach (var o in _outposts)
        {
            if (o.IsDepleted) continue;

            int budget = dailyYield;
            // 남은 것이 있는 항목을 돌며 1씩 — 예산이 소진되거나 더 캘 게 없을 때까지.
            while (budget > 0)
            {
                bool tookAny = false;
                for (int i = 0; i < o.Remaining.Count && budget > 0; i++)
                {
                    var d = o.Remaining[i];
                    if (d.Amount <= 0) continue;

                    d.Amount--;
                    o.Remaining[i] = d;

                    o.Stock.TryGetValue(d.Item, out int cur);
                    o.Stock[d.Item] = cur + 1;

                    budget--;
                    tookAny = true;
                    changed = true;
                }
                if (!tookAny) break;   // 전부 0 — 고갈
            }

            if (o.IsDepleted)
                Debug.Log($"[채집기지] ({o.Node.x},{o.Node.y}) 매장이 고갈됐습니다.");
        }

        if (changed) OnChanged?.Invoke();
    }

    // ── 비축분 싣기 ──────────────────────────────────────────────────────────

    // 기지에 쌓인 것을 원정대 등짐으로. **등짐 한도까지만** 옮기고 나머지는 기지에 남는다.
    public int LoadInto(WorldOutpost outpost, ExpeditionParty party)
    {
        if (outpost == null || party == null) return 0;

        int moved = 0;
        var items = new List<ItemData>(outpost.Stock.Keys);

        foreach (var item in items)
        {
            int have  = outpost.Stock[item];
            int taken = party.TryAdd(item, have);
            if (taken <= 0) continue;

            moved += taken;
            if (taken >= have) outpost.Stock.Remove(item);
            else               outpost.Stock[item] = have - taken;
        }

        if (moved > 0)
        {
            Debug.Log($"[채집기지] 비축분 {moved}개를 실었습니다. 등짐 {party.CarriedWeight:0}/{party.Capacity:0}kg");
            OnChanged?.Invoke();
        }
        return moved;
    }

    public static string ItemName(ItemData item) =>
        item == null ? "?" : (string.IsNullOrEmpty(item.ItemName) ? item.name : item.ItemName);
}
