using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Storage : MonoBehaviour
{
    public static readonly List<Storage> All = new();
    public static event Action OnChanged;
    public static event Action<Storage> OnSelected;

    [Header("이름")]
    public string BuildingName = "창고";

    [Header("허용 아이템 (비어있으면 모두 허용)")]
    [SerializeField] List<ResourceType> allowedTypes = new();

    public Inventory Inventory { get; private set; }
    public IReadOnlyList<ResourceType> AllowedTypes => allowedTypes;

    Camera _cam;
    BuildingDormancy _dormancy;

    // 핵 영역 밖(휴면) 창고는 집계/탐색에서 제외 → "작동 정지". 영역 복구 시 자동 재참여.
    public bool IsActive => _dormancy == null || !_dormancy.IsDormant;

    void Awake()
    {
        Inventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        Inventory.OnChanged += () => OnChanged?.Invoke();
        _cam = Camera.main;
    }

    // 레지스트리는 OnEnable/OnDisable 기준 — 비활성 정착지(루트 SetActive(false))의 창고는
    // All에서 빠져 집계/탐색에서 자동 제외된다(정착지 전환 격리). 항상 활성이면 기존과 동일.
    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // BuildingDormancy는 배치 시 AddComponent될 수 있어 Awake 이후에 붙음 → Start에서 해석.
    void Start() => _dormancy = GetComponent<BuildingDormancy>();

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit) && hit.collider.gameObject == gameObject)
            OnSelected?.Invoke(this);
    }

    public void Init(BuildingData data)
    {
        if (data != null) BuildingName = data.BuildingName;
    }

    // 필터 설정
    public void SetAllowAll()               => allowedTypes.Clear();
    public void AddFilter(ResourceType t)   { if (!allowedTypes.Contains(t)) allowedTypes.Add(t); }
    public void RemoveFilter(ResourceType t) => allowedTypes.Remove(t);
    public bool HasFilter(ResourceType t)   => allowedTypes.Contains(t);

    // ── 입고 필터 ─────────────────────────────────────────────────────
    public bool CanAccept(ItemData item)
    {
        if (allowedTypes.Count == 0) return true;
        // 자원 아이템만 ResourceType으로 필터링. 무기/방어구는 필터가 비어있을 때만 허용.
        return item is ResourceItemData r && allowedTypes.Contains(r.ResourceType);
    }

    // 필터 통과 시에만 입고. 성공 여부 반환.
    public bool TryAdd(ItemData item, int amount)
    {
        if (!CanAccept(item)) return false;
        return Inventory.TryAdd(item, amount) > 0;
    }

    // ── 정적 집계 (ResourceType 기반) ────────────────────────────────
    public static int GetTotal(ResourceType type)
    {
        int total = 0;
        foreach (var sb in All)
        {
            if (!sb.IsActive) continue;
            total += sb.Inventory.GetTotalByType(type);
        }
        return total;
    }

    public static bool Consume(ResourceType type, int amount)
    {
        int remaining = amount;
        foreach (var sb in All)
        {
            if (remaining <= 0) break;
            if (!sb.IsActive) continue;
            remaining -= sb.Inventory.RemoveByType(type, remaining);
        }
        return remaining <= 0;
    }

    // ── ItemData 기반 집계 (제작 재료용) ──────────────────────────────
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
    // ResourceType 기반으로 재료가 있는 창고와 해당 ItemData를 반환
    public static (Storage building, ItemData itemData) FindStorageWithType(ResourceType type)
    {
        foreach (var sb in All)
        {
            if (!sb.IsActive) continue;
            foreach (var inst in sb.Inventory.Items)
                if (inst.Data is ResourceItemData r && r.ResourceType == type && inst.Quantity > 0)
                    return (sb, inst.Data);
        }
        return (null, null);
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
