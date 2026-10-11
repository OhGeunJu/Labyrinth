using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// **상단 창 — `T`**(2026-10-01, `Caravan` · `CaravanManager`). 왼쪽은 상단 목록, 오른쪽은 고른 상단의 설정:
// 출발 거점 · 목적지(지도에서 고른다) · 대표 행상인 · 일행 · 주기 · 예산 · 판매/구매/우선 구매 규칙 · [지금 출발].
//
// **모양은 씬이 든다**(`UI → Create Caravan Window`) — 줄은 씬의 템플릿을 복제하고 여기서는 글자·목록·콜백만 건다.
// **드롭다운은 고를 때만 다시 짓는다** — 박자마다 다시 지으면 펼친 목록이 그 자리에서 닫힌다(`WorkPostsUI`가 적어 둔 것).
// 박자(`UiRefresh`)에 태우는 것은 상태 글자 · 출발 버튼 · 보고서뿐이다.
public class CaravanUI : MonoBehaviour
{
    public static CaravanUI Instance { get; private set; }

    [SerializeField] GameObject panel;

    [Header("목록")]
    [SerializeField] Transform listContent;
    [SerializeField] Button    listRowTemplate;
    [SerializeField] Button    addButton;
    [SerializeField] Button    removeButton;
    [SerializeField] TextMeshProUGUI lockedText;

    [Header("상세")]
    [SerializeField] GameObject      detailRoot;
    [SerializeField] TMP_InputField  nameInput;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] TMP_Dropdown    homeDropdown;
    [SerializeField] TextMeshProUGUI destText;
    [SerializeField] Button          pickDestButton;
    [SerializeField] TMP_Dropdown    leaderDropdown;
    [SerializeField] Toggle          autoToggle;
    [SerializeField] TMP_InputField  intervalInput;
    [SerializeField] TMP_InputField  budgetInput;

    [Header("일행")]
    [SerializeField] Transform memberContent;
    [SerializeField] Toggle    memberTemplate;

    [Header("규칙")]
    [SerializeField] Transform        sellContent;
    [SerializeField] CaravanRuleRowUI sellTemplate;
    [SerializeField] Button           addSellButton;
    [SerializeField] Transform        buyContent;
    [SerializeField] CaravanRuleRowUI buyTemplate;
    [SerializeField] Button           addBuyButton;
    [SerializeField] Transform        priorityContent;
    [SerializeField] CaravanRuleRowUI priorityTemplate;
    [SerializeField] Button           addPriorityButton;

    [Header("출발")]
    [SerializeField] Button          departButton;
    [SerializeField] TextMeshProUGUI departNote;
    [SerializeField] TextMeshProUGUI reportText;

    [SerializeField] Color rowNormal   = new(0.16f, 0.16f, 0.19f, 1f);
    [SerializeField] Color rowSelected = new(0.30f, 0.45f, 0.62f, 1f);

    readonly List<Button> _listRows = new();
    readonly List<Toggle> _memberRows = new();
    readonly List<CaravanRuleRowUI> _sellRows = new(), _buyRows = new(), _priorityRows = new();

    Caravan _sel;
    int _beat = -1;
    List<ItemData> _items;
    List<string>   _itemLabels;
    readonly List<UnitBrain> _leaders = new();
    readonly List<UnitBrain> _crew = new();
    readonly List<DungeonCore> _cores = new();
    int _crewHash;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        Instance = this;
        if (listRowTemplate  != null) listRowTemplate.gameObject.SetActive(false);
        if (memberTemplate   != null) memberTemplate.gameObject.SetActive(false);
        if (sellTemplate     != null) sellTemplate.gameObject.SetActive(false);
        if (buyTemplate      != null) buyTemplate.gameObject.SetActive(false);
        if (priorityTemplate != null) priorityTemplate.gameObject.SetActive(false);
        if (panel != null) panel.SetActive(false);

        addButton?.onClick.AddListener(() => { var c = CaravanManager.Instance?.Create(); if (c != null) Select(c); });
        removeButton?.onClick.AddListener(() => { if (_sel != null) { CaravanManager.Instance?.Remove(_sel); _sel = null; RebuildAll(); } });
        pickDestButton?.onClick.AddListener(PickDestination);
        departButton?.onClick.AddListener(() => { if (_sel != null && _sel.Depart()) RefreshStatus(); });
        addSellButton?.onClick.AddListener(() => { if (_sel == null) return; _sel.Sell.Add(new CaravanSellRule { Item = FirstItem() }); RebuildRules(); });
        addBuyButton?.onClick.AddListener(() => { if (_sel == null) return; _sel.Buy.Add(new CaravanBuyRule { Item = FirstItem() }); RebuildRules(); });
        addPriorityButton?.onClick.AddListener(() => { if (_sel == null) return; _sel.Priority.Add(new CaravanPriorityRule { Item = FirstItem() }); RebuildRules(); });

        nameInput?.onEndEdit.AddListener(t => { if (_sel != null && !string.IsNullOrWhiteSpace(t)) { _sel.Name = t.Trim(); RebuildList(); } });
        autoToggle?.onValueChanged.AddListener(v => { if (_sel != null) _sel.Auto = v; });
        intervalInput?.onEndEdit.AddListener(t => { if (_sel != null && int.TryParse(t, out var n)) _sel.IntervalDays = Mathf.Clamp(n, 1, 60); BindScalars(); });
        budgetInput?.onEndEdit.AddListener(t => { if (_sel != null && int.TryParse(t, out var n)) _sel.Budget = Mathf.Max(0, n); BindScalars(); });
        homeDropdown?.onValueChanged.AddListener(v => { if (_sel != null && v >= 0 && v < _cores.Count) _sel.Home = _cores[v]; RefreshStatus(); });
        leaderDropdown?.onValueChanged.AddListener(v =>
        {
            if (_sel == null) return;
            _sel.Leader = v > 0 && v - 1 < _leaders.Count ? _leaders[v - 1] : null;
            _sel.Members.Remove(_sel.Leader);
            RebuildMembers();
            RefreshStatus();
        });
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.tKey.wasPressedThisFrame && !InputFocus.KeysBlocked()) Toggle();
            else if (IsOpen && kb.escapeKey.wasPressedThisFrame) WindowRaiser.Close(panel);
        }

        if (!IsOpen || !UiRefresh.Due(ref _beat, 2)) return;

        // 식구가 바뀌었으면(합류 · 사망 · 길들이기) 대표·일행 목록을 다시 — **펼친 드롭다운이 없을 때만**.
        int h = CrewHash();
        if (h != _crewHash && (leaderDropdown == null || !leaderDropdown.IsExpanded)) { _crewHash = h; RebuildPeople(); }

        RefreshStatus();
    }

    public void Toggle()
    {
        if (panel == null) return;
        if (IsOpen) { WindowRaiser.Close(panel); return; }
        Open();
    }

    public void Open()
    {
        if (panel == null) return;
        RebuildAll();
        WindowRaiser.Open(panel);
    }

    // ── 짓기 ────────────────────────────────────────────────────────────────

    void RebuildAll()
    {
        bool unlocked = Caravans.Unlocked;
        if (lockedText != null) lockedText.gameObject.SetActive(!unlocked);
        if (listContent != null) listContent.gameObject.SetActive(unlocked);
        if (addButton != null) addButton.gameObject.SetActive(unlocked);
        if (removeButton != null) removeButton.gameObject.SetActive(unlocked);

        var mgr = CaravanManager.Instance;
        if (_sel == null && mgr != null && mgr.Caravans.Count > 0) _sel = mgr.Caravans[0];
        if (_sel != null && (mgr == null || !mgr.Caravans.Contains(_sel))) _sel = null;

        if (detailRoot != null) detailRoot.SetActive(unlocked && _sel != null);

        EnsureItems();
        RebuildList();
        if (_sel != null && unlocked)
        {
            RebuildPeople();
            BindScalars();
            RebuildRules();
            RefreshStatus();
        }
    }

    void Select(Caravan c)
    {
        _sel = c;
        RebuildAll();
    }

    void RebuildList()
    {
        var mgr = CaravanManager.Instance;
        int n = mgr != null ? mgr.Caravans.Count : 0;
        while (_listRows.Count < n)
        {
            var b = Instantiate(listRowTemplate, listContent);
            int idx = _listRows.Count;
            b.onClick.AddListener(() => { var m = CaravanManager.Instance; if (m != null && idx < m.Caravans.Count) Select(m.Caravans[idx]); });
            _listRows.Add(b);
        }
        for (int i = 0; i < _listRows.Count; i++)
        {
            bool on = i < n;
            _listRows[i].gameObject.SetActive(on);
            if (!on) continue;
            var c = mgr.Caravans[i];
            var t = _listRows[i].GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null) t.text = Josa.Fix($"{c.Name}\n<size=80%><color=#9AB>{StateLabel(c)}</color></size>");
            if (_listRows[i].targetGraphic is Image img) img.color = c == _sel ? rowSelected : rowNormal;
        }
    }

    void BindScalars()
    {
        if (_sel == null) return;
        nameInput?.SetTextWithoutNotify(_sel.Name);
        autoToggle?.SetIsOnWithoutNotify(_sel.Auto);
        intervalInput?.SetTextWithoutNotify(_sel.IntervalDays.ToString());
        budgetInput?.SetTextWithoutNotify(_sel.Budget.ToString());

        _cores.Clear();
        foreach (var c in DungeonCore.All) if (c != null) _cores.Add(c);
        if (homeDropdown != null)
        {
            homeDropdown.ClearOptions();
            homeDropdown.AddOptions(_cores.Select((c, i) => $"{i + 1} 거점").ToList());
            homeDropdown.SetValueWithoutNotify(Mathf.Max(0, _cores.IndexOf(_sel.Home)));
            homeDropdown.RefreshShownValue();
            if (_sel.Home == null && _cores.Count > 0) _sel.Home = _cores[0];
        }
    }

    // 대표 후보 = 말할 줄 아는 내 사람(사교 높은 순). 일행 후보 = 내 사람 + 가축(하수인은 핵 곁을 안 떠난다).
    void RebuildPeople()
    {
        _leaders.Clear(); _crew.Clear();
        foreach (var b in UnitBrain.All)
        {
            if (b == null) continue;
            var f = b.GetComponent<Faction>();
            if (f == null || !f.IsPlayer || b.Data is MinionData) continue;
            if (b.Blackboard != null && b.Blackboard.IsPrisoner) continue;
            if (PawnCarry.IsCorpse(b)) continue;
            _crew.Add(b);
            if (!b.IsTamedBeast) _leaders.Add(b);
        }
        // 길 위에 있는 일행도 목록에 남긴다 — 꺼져 있어 `UnitBrain.All`에 없다.
        if (_sel != null) foreach (var a in _sel.Away) if (a != null && !_crew.Contains(a)) { _crew.Add(a); if (!a.IsTamedBeast) _leaders.Add(a); }

        _leaders.Sort((a, b) => (b.Stats != null ? b.Stats.Social : 0f).CompareTo(a.Stats != null ? a.Stats.Social : 0f));
        _crew.Sort(PawnOrder.Compare);
        _crewHash = CrewHash();

        if (leaderDropdown != null && _sel != null)
        {
            var opts = new List<string> { "(없음)" };
            foreach (var b in _leaders) opts.Add($"{b.UnitName} (사교 {(b.Stats != null ? b.Stats.Social : 0f):F0})");
            leaderDropdown.ClearOptions();
            leaderDropdown.AddOptions(opts);
            leaderDropdown.SetValueWithoutNotify(_sel.Leader != null ? _leaders.IndexOf(_sel.Leader) + 1 : 0);
            leaderDropdown.RefreshShownValue();
        }
        RebuildMembers();
    }

    void RebuildMembers()
    {
        if (_sel == null || memberTemplate == null) return;
        var list = _crew.Where(b => b != _sel.Leader).ToList();
        while (_memberRows.Count < list.Count)
        {
            var t = Instantiate(memberTemplate, memberContent);
            _memberRows.Add(t);
        }
        for (int i = 0; i < _memberRows.Count; i++)
        {
            bool on = i < list.Count;
            _memberRows[i].gameObject.SetActive(on);
            if (!on) continue;
            var b = list[i];
            var row = _memberRows[i];
            row.onValueChanged.RemoveAllListeners();
            row.SetIsOnWithoutNotify(_sel.Members.Contains(b));
            row.onValueChanged.AddListener(v => { if (v) { if (!_sel.Members.Contains(b)) _sel.Members.Add(b); } else _sel.Members.Remove(b); RefreshStatus(); });
            var label = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                string tag = b.IsTamedBeast ? (PawnCart.Has(b) ? " [가축 수레]" : " [가축]") : "";
                label.text = $"{b.UnitName}{tag}  <color=#9AB>{CarryCapacity.For(b):F0}kg</color>";
            }
        }
    }

    void RebuildRules()
    {
        if (_sel == null) return;
        EnsureItems();

        Fill(_sellRows, sellTemplate, sellContent, _sel.Sell.Count, (row, i) =>
        {
            var r = _sel.Sell[i];
            row.Bind(_items, _itemLabels, r.Item, (int)r.Mode, r.Amount,
                     it => r.Item = it, m => r.Mode = (CaravanSellMode)m, a => r.Amount = a,
                     () => { _sel.Sell.Remove(r); RebuildRules(); });
        });
        Fill(_buyRows, buyTemplate, buyContent, _sel.Buy.Count, (row, i) =>
        {
            var r = _sel.Buy[i];
            row.Bind(_items, _itemLabels, r.Item, 0, r.Target,
                     it => r.Item = it, null, a => r.Target = a,
                     () => { _sel.Buy.Remove(r); RebuildRules(); });
        });
        Fill(_priorityRows, priorityTemplate, priorityContent, _sel.Priority.Count, (row, i) =>
        {
            var r = _sel.Priority[i];
            row.Bind(_items, _itemLabels, r.Item, 0, r.Max,
                     it => r.Item = it, null, a => r.Max = a,
                     () => { _sel.Priority.Remove(r); RebuildRules(); });
        });
    }

    static void Fill(List<CaravanRuleRowUI> rows, CaravanRuleRowUI template, Transform parent, int n,
                     System.Action<CaravanRuleRowUI, int> bind)
    {
        if (template == null) return;
        while (rows.Count < n) rows.Add(Instantiate(template, parent));
        for (int i = 0; i < rows.Count; i++)
        {
            bool on = i < n;
            rows[i].gameObject.SetActive(on);
            if (on) bind(rows[i], i);
        }
    }

    // 고를 수 있는 물건 — 이름순. 값이 없는 것(동전 등)도 둔다: 구매 규칙은 *"없으면 사 온다"*라 값과 무관하다.
    void EnsureItems()
    {
        if (_items != null) return;
        _items = DataManager.AllItems.Values.Where(i => i != null && !string.IsNullOrEmpty(i.ItemName))
                                             .OrderBy(i => i.ItemName).ToList();
        _itemLabels = _items.Select(i => i.ItemName).ToList();
    }

    ItemData FirstItem() { EnsureItems(); return _items.Count > 0 ? _items[0] : null; }

    int CrewHash()
    {
        int h = 17;
        foreach (var b in UnitBrain.All)
        {
            if (b == null) continue;
            var f = b.GetComponent<Faction>();
            if (f != null && f.IsPlayer) h = h * 31 + b.GetInstanceID() + (PawnCart.Has(b) ? 7 : 0);
        }
        return h;
    }

    // ── 상태 ────────────────────────────────────────────────────────────────

    static string StateLabel(Caravan c)
    {
        float left = Mathf.Max(0f, c.StateEnds - GameTimeManager.Elapsed) / Mathf.Max(1f, GameTimeManager.DayDuration) * 24f;
        return c.State switch
        {
            CaravanState.Gathering => "핵에 모이는 중",
            CaravanState.Outbound  => $"{c.DestName}으로(로) 가는 중 - {left:F1}시간",
            CaravanState.Returning => $"돌아오는 중 - {left:F1}시간",
            _                      => c.Auto ? $"대기 - {c.IntervalDays}일마다 출발" : "대기",
        };
    }

    void RefreshStatus()
    {
        RebuildList();
        if (_sel == null) return;

        if (statusText != null) statusText.text = Josa.Fix(StateLabel(_sel));
        if (destText != null)
        {
            float leg = _sel.LegDays() * 24f;
            destText.text = _sel.HasDestination
                ? Josa.Fix($"{_sel.DestName}  <color=#9AB>(편도 {leg:F1}시간)</color>")
                : "<color=#888>정하지 않음</color>";
        }

        string why = _sel.WhyNotDepart();
        if (departButton != null) departButton.interactable = why == null;
        if (departNote != null)
            departNote.text = Josa.Fix(why != null
                ? $"<color=#F90>{why}</color>"
                : $"짐 한도 {_sel.Capacity:F0}kg, 예산 {_sel.Budget} G, 지갑 {Wallet.Balance} G");

        if (reportText != null)
            reportText.text = string.IsNullOrEmpty(_sel.LastReport)
                ? "<color=#888>아직 다녀온 적이 없다.</color>"
                : Josa.Fix(_sel.LastReport);
    }

    // ── 목적지 — 지도에서 ───────────────────────────────────────────────────

    void PickDestination()
    {
        if (_sel == null) return;
        var map = WorldMapPanel.Instance;
        if (map == null) { EventNotificationUI.Notify("상단", "지도 창을 찾지 못했습니다."); return; }

        var target = _sel;
        WindowRaiser.Close(panel);
        map.BeginPick("상단 목적지 - 가 본 마을이나 내 거점을 누르세요", world =>
        {
            if (!TryPick(target, world))
                EventNotificationUI.Notify("상단", "그 근처에 가 본 마을이나 내 거점이 없습니다.");
            _sel = target;
            Open();
        }, () => { _sel = target; Open(); });
    }

    // 누른 자리에서 가장 가까운 **가 본 마을** 또는 **내 핵**(반경 60m).
    static bool TryPick(Caravan c, Vector3 world)
    {
        const float R = 60f;
        float best = R * R;
        string town = null;
        DungeonCore core = null;

        foreach (var s in WorldSites.Of(WorldNodeContent.Town))
        {
            if (!TownSite.IsDiscovered(s.Id)) continue;
            float d = (s.Position - new Vector2(world.x, world.z)).sqrMagnitude;
            if (d < best) { best = d; town = s.Id; core = null; }
        }
        foreach (var k in DungeonCore.All)
        {
            if (k == null) continue;
            var p = k.transform.position;
            float d = (new Vector2(p.x - world.x, p.z - world.z)).sqrMagnitude;
            if (d < best) { best = d; core = k; town = null; }
        }

        if (town != null) { c.SetTown(town); return true; }
        if (core != null) { c.SetOutpost(core); return true; }
        return false;
    }
}
