using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Storage : MonoBehaviour, IWorldClickable
{
    public static readonly List<Storage> All = new();
    public static event Action OnChanged;
    public static event Action<Storage> OnSelected;

    [Header("이름")]
    public string BuildingName = "창고";

    [Header("허용 아이템 (둘 다 비어있으면 모두 허용)")]
    [Tooltip("묶음 단위 허용. 창고 패널의 버튼이 다루는 것이 이것이다.")]
    [SerializeField] List<ResourceGroup> allowedGroups = new();

    [Tooltip("묶음과 별개로 콕 집어 허용할 자원. '석탄 전용 창고'처럼 묶음보다 좁게 잡을 때.")]
    [SerializeField] List<ItemData> allowedItems = new();

    public Inventory Inventory { get; private set; }
    public IReadOnlyList<ResourceGroup> AllowedGroups => allowedGroups;
    public IReadOnlyList<ItemData>      AllowedItems  => allowedItems;

    // 필터가 하나도 없으면 "전부 허용"이다. 이 정규형이 여러 곳에서 쓰이므로 한 곳에서 답한다.
    public bool AllowsEverything => allowedGroups.Count == 0 && allowedItems.Count == 0;

    BuildingDormancy _dormancy;

    // 핵 영역 밖(휴면) 창고는 집계/탐색에서 제외 → "작동 정지". 영역 복구 시 자동 재참여.
    public bool IsActive => _dormancy == null || !_dormancy.IsDormant;

    void Awake()
    {
        Inventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        Inventory.OnChanged += () => OnChanged?.Invoke();
    }

    // 레지스트리는 OnEnable/OnDisable 기준 — 비활성 정착지(루트 SetActive(false))의 창고는
    // All에서 빠져 집계/탐색에서 자동 제외된다(정착지 전환 격리). 항상 활성이면 기존과 동일.
    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // BuildingDormancy는 배치 시 AddComponent될 수 있어 Awake 이후에 붙음 → Start에서 해석.
    void Start() => _dormancy = GetComponent<BuildingDormancy>();

    // SelectionManager의 단일 레이캐스트가 이 건물을 클릭 판정 시 호출.
    public void OnWorldClick() => OnSelected?.Invoke(this);

    public void Init(BuildingData data)
    {
        if (data != null) BuildingName = data.BuildingName;
    }

    // 필터 설정
    public void SetAllowAll() { allowedGroups.Clear(); allowedItems.Clear(); }

    public void AddFilter(ResourceGroup g)    { if (!allowedGroups.Contains(g)) allowedGroups.Add(g); }
    public void RemoveFilter(ResourceGroup g) => allowedGroups.Remove(g);
    public bool HasFilter(ResourceGroup g)    => allowedGroups.Contains(g);

    public void AddItemFilter(ItemData item)    { if (item != null && !allowedItems.Contains(item)) allowedItems.Add(item); }
    public void RemoveItemFilter(ItemData item) => allowedItems.Remove(item);

    // ── 입고 필터 ─────────────────────────────────────────────────────
    public bool CanAccept(ItemData item)
    {
        if (AllowsEverything) return true;
        if (item == null) return false;

        // 콕 집어 허용한 것이 먼저다 — 묶음에 없는 자원(또는 자원이 아닌 것)도 이걸로 받을 수 있다.
        if (allowedItems.Contains(item)) return true;

        // 자원 아이템만 묶음으로 걸러진다. 무기·방어구는 필터가 비어 있을 때만 들어온다.
        return item is ResourceItemData r && allowedGroups.Contains(r.Group);
    }

    // 필터 통과 시에만 입고. 실제 입고된 수량 반환 (0=거부/만적, amount 미만=부분 입고).
    // 부분 입고 시 호출자가 남은 양을 폴백 라우팅해야 증발하지 않는다.
    public int TryAdd(ItemData item, int amount)
        => TryAdd(item, amount, ItemQuality.Common);

    // 등급 보존 입고 (땅 아이템 운반 등 장비 등급 유지용).
    public int TryAdd(ItemData item, int amount, ItemQuality quality)
    {
        if (!CanAccept(item)) return 0;
        return Inventory.TryAdd(item, amount, quality);
    }

    // ── 집계는 **아이템 기준 하나뿐**이다 ─────────────────────────────
    // 예전엔 `GetTotal(ResourceType)`과 `GetTotalByItem(ItemData)`가 같은 일을 두 벌로 했다.
    // 종류가 아이템으로 흡수되면서 앞엣것은 뒤엣것과 완전히 같은 뜻이 됐다 — 그래서 지웠다.
    public static int GetTotalByItem(ItemData item)
    {
        int total = 0;
        foreach (var sb in All)
        {
            if (!sb.IsActive) continue;
            total += sb.Inventory.GetTotalQuantity(item);
        }
        return total;
    }

    public static bool ConsumeByItem(ItemData item, int amount)
    {
        if (GetTotalByItem(item) < amount) return false;
        int remaining = amount;
        foreach (var sb in All)
        {
            if (remaining <= 0) break;
            if (!sb.IsActive) continue;
            remaining -= sb.Inventory.Remove(item, remaining);
        }
        return remaining <= 0;
    }

    // ── 위치/탐색 유틸 ───────────────────────────────────────────────
    // 그 아이템이 실제로 들어 있는 창고. 건설 자재를 가지러 갈 곳을 고를 때 쓴다.
    public static Storage FindStorageWithItem(ItemData item)
    {
        if (item == null) return null;
        foreach (var sb in All)
        {
            if (!sb.IsActive) continue;
            if (sb.Inventory.GetTotalQuantity(item) > 0) return sb;
        }
        return null;
    }

    // item을 받을 수 있는 창고 중 가장 가까운 것 반환
    public static Storage FindNearest(Vector3 position, ItemData item = null)
    {
        Storage nearest = null;
        float minDist = float.MaxValue;
        foreach (var sb in All)
        {
            if (!sb.IsActive) continue;
            if (item != null && !sb.CanAccept(item)) continue;
            float d = Vector3.Distance(position, sb.transform.position);
            if (d < minDist) { minDist = d; nearest = sb; }
        }
        return nearest;
    }
}
