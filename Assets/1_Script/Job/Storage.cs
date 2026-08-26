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

    // ── 자리 판정 ─────────────────────────────────────────────────────
    // **필터를 통과한다고 들어갈 수 있는 건 아니다.** 만적인 창고는 `CanAccept`엔 걸리지만
    // `TryAdd`가 0을 돌려준다 — 그 둘을 같은 것으로 보면 운반이 끝나지 않는다:
    // 폰이 창고에 못 넣어 바닥에 떨구면 `DroppedItem.Spawn`이 **또 그 만적 창고를 가리키는**
    // 운반 일감을 걸고, 폰이 다시 주워 와 또 떨군다. 창고는 언젠가 반드시 차므로 반드시 온다.
    //
    // 그래서 "받아 줄 곳"을 찾을 땐 자리까지 본다. 자리가 없으면 일감을 아예 만들지 않고
    // 그 자리에 쌓아 둔다(작업대가 이미 그렇게 한다 — "받아 줄 창고가 없으면 그대로 쌓아 둔다").
    //
    // `quality`는 **`TryAdd`에 넘길 그 등급**을 준다. 스택은 같은 등급끼리만 합쳐지므로,
    // 등급을 빠뜨리면 `일반` 스택의 여유를 `희귀` 짐이 쓸 수 있다고 잘못 답한다.
    public bool HasRoomFor(ItemData item, int amount = 1, ItemQuality quality = ItemQuality.Common)
    {
        if (!CanAccept(item)) return false;
        return Inventory != null && Inventory.CanFit(item, amount, quality);
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
    //
    // **`near`는 선택 인자가 아니다**(2026-08-26). 세계가 29km로 이어지면서 거점이 동시에
    // 살아 있게 됐는데, 전부 더하면 **29km 밖 창고의 목재로 여기서 집을 짓는다.** 기본값을
    // 두면 부르는 쪽이 조용히 빠뜨리고, 그 증상은 *"분명히 없는데 지어진다"*라 잡기 어렵다
    // (`Storage.FindNearest`의 `from`을 필수로 둔 것과 같은 이유).
    //
    // 가르는 것은 **핵 영역**이다(`Territory`) — 거리로 자르면 *"얼마나 멀면 다른 거점인가"*라는
    // 새 숫자가 생기는데, 핵은 이미 영역을 갖고 있고 `BuildingDormancy`가 그 답을 쓰고 있다.
    // **핵이 하나뿐인 판에서는 전부 같은 영역**이라 예전과 똑같이 돈다.
    public static int GetTotalByItem(ItemData item, Vector3 near)
    {
        int total = 0;
        foreach (var sb in All)
        {
            if (!sb.IsActive) continue;
            if (!Territory.Same(sb.transform.position, near)) continue;
            total += sb.Inventory.GetTotalQuantity(item);
        }
        return total;
    }

    public static bool ConsumeByItem(ItemData item, int amount, Vector3 near)
    {
        if (GetTotalByItem(item, near) < amount) return false;
        int remaining = amount;
        foreach (var sb in All)
        {
            if (remaining <= 0) break;
            if (!sb.IsActive) continue;
            if (!Territory.Same(sb.transform.position, near)) continue;
            remaining -= sb.Inventory.Remove(item, remaining);
        }
        return remaining <= 0;
    }

    // **창고에서 배낭으로 챙긴다** — 실제로 실은 개수를 돌려준다.
    //
    // **넣을 자리를 먼저 보고 꺼낸다.** 꺼내고 나서 `TryAdd`가 실패하면 되돌리게 되는데,
    // 부르는 쪽은 대개 **주기적으로 다시 시도**하므로 그 되돌림이 되풀이된다 — 창고에서
    // 물건이 나왔다 들어갔다 하는 것으로 보인다(`EatNode`가 BT 틱마다, `FeedJob`이 1초마다
    // 그랬다). 몇 개까지 실을 수 있는지는 **한 개씩 줄여 가며** 묻는다: 무게가 아니라
    // 격자 자리가 정하므로 나눗셈으로는 못 맞힌다.
    //
    // 되돌리기는 **안전망으로만** 남긴다 — 자리를 확인한 뒤라 여기까지 오면 안 된다.
    //
    // **등급 없는 소모품용이다**(약·음식·자재). 등급을 안 넘기므로 장비에 쓰면 등급이
    // 일반으로 내려앉는다 — 다만 등급을 갖는 것은 장비뿐이고(`ItemData.HasQuality`)
    // 장비를 창고에서 뭉치로 챙기는 일감은 없다.
    public int FetchInto(Inventory into, ItemData item, int want)
    {
        if (into == null || item == null || want <= 0) return 0;

        int take = Mathf.Min(want, Inventory.GetUnlockedQuantity(item));
        while (take > 0 && !into.CanFit(item, take)) take--;
        if (take <= 0) return 0;

        if (Inventory.RemoveUnlocked(item, take) <= 0) return 0;

        int got = into.TryAdd(item, take);
        if (got < take) Inventory.TryAdd(item, take - got);
        return got;
    }

    // ── 위치/탐색 유틸 ───────────────────────────────────────────────
    // 그 아이템이 실제로 들어 있는 **가장 가까운** 창고. 자재·씨앗·약을 가지러 갈 곳이다.
    //
    // **`FindNearest`의 짝이다 — 방향만 반대다.** 저쪽은 "받아 줄" 곳(필터+빈자리)이라
    // 가득 든 창고가 탈락하는데, 가지러 갈 때는 정확히 그 창고에 가야 한다.
    //
    // **`from`은 선택 인자가 아니다.** 예전엔 위치를 아예 안 받아 `All`에서 **처음 걸리는**
    // 창고를 돌려줬다 — 창고가 둘 이상이면 폰이 목재 한 짐에 맵을 가로지른다. 기본값을
    // 두면 부르는 쪽이 조용히 빠뜨리므로(`ResourceNode.Work`에 델타를 못 빠뜨리게 한 것과
    // 같은 이유) 반드시 적게 한다.
    public static Storage FindStorageWithItem(ItemData item, Vector3 from)
        => item == null ? null
         : NearestWhere(from, sb => sb.Inventory.GetTotalQuantity(item) > 0);

    // **조건에 맞는 가장 가까운 창고.** 훑는 규칙(휴면 제외 · 거리 비교 · 조기 탈출)을
    // 여기 한 번만 적는다 — 부르는 쪽은 "어떤 창고인가"만 답한다.
    //
    // 이게 없으면 "가장 가까운 창고 고르기"가 물음마다 한 벌씩 생긴다. 실제로 그랬다:
    // `Feeding`이 "음식이 든 가장 가까운 창고"를 찾으려고 같은 루프를 통째로 다시 썼다
    // (`FindStorageWithItem`은 아이템 하나만 물을 수 있어 "아무 음식이나"를 못 물었다).
    public static Storage NearestWhere(Vector3 from, Func<Storage, bool> accepts)
    {
        if (accepts == null) return null;

        Storage best     = null;
        float   bestDist = float.MaxValue;

        foreach (var sb in All)
        {
            if (sb == null || !sb.IsActive || sb.Inventory == null) continue;

            // 거리를 먼저 본다 — 이미 더 가까운 데서 찾았으면 **뒤져 볼 것도 없다.**
            float dist = (sb.transform.position - from).sqrMagnitude;
            if (dist >= bestDist) continue;
            if (!accepts(sb)) continue;

            bestDist = dist;
            best     = sb;
        }
        return best;
    }

    // item을 **실제로 받아 줄** 창고 중 가장 가까운 것. 필터뿐 아니라 **자리까지** 본다 —
    // 만적인 곳을 돌려주면 폰이 거기까지 갔다가 짐을 떨구고, 그 짐이 다시 같은 곳으로 가는
    // 일감이 되어 줍고 떨구기를 반복한다(`HasRoomFor` 주석 참조).
    //
    // `amount`·`quality`는 **실제로 넣을 것**을 그대로 넘길 것. 기본값(1개·일반)으로 물으면
    // "한 개는 들어간다"만 확인하고 27개를 지고 가게 된다.
    //
    // **두 번 훑는다.** 통째로 받아 줄 곳이 먼저고, 없으면 한 개라도 받는 곳으로 물러선다.
    // 물러서지 않으면 30자리 남은 창고와 50짜리 짐 앞에서 일감이 아예 안 뜬다 —
    // 자리가 있는데 없다고 하는 쪽이 만적인 곳을 고르는 것보다 낫다고 할 수는 없다.
    // (부분 입고는 이미 모든 운반이 처리한다: 다른 창고 → 배낭 복귀 → 드롭 순.)
    public static Storage FindNearest(Vector3 position, ItemData item = null, int amount = 1,
                                      ItemQuality quality = ItemQuality.Common)
    {
        var full = FindNearestFitting(position, item, amount, quality);
        if (full != null || amount <= 1) return full;

        return FindNearestFitting(position, item, 1, quality);
    }

    // **부린 짐이 증발하지 않게 하는 사슬 — 한 곳에서 돈다.**
    //
    // `HaulJob`·`GroundHaulJob`·`FarmHarvestJob`이 이걸 각자 적어 두었고, **셋이 조금씩
    // 달랐다.** 다른 것이 규칙이었으면 어딘가에 적혀 있어야 하는데 아무 데도 없었고,
    // 실제로는 하나가 문서화된 규칙을 어기고 있었다:
    //
    //  · `FarmHarvestJob`은 `FindNearest`에 **수량·등급을 안 넘겼다.** CLAUDE.md가 콕 집어
    //    막아 둔 것이다 — 기본값(1개·일반)으로 물으면 "한 개는 들어간다"만 확인하고 50을
    //    지고 가서, 도착해 못 넣고 줍고 떨구기를 반복한다.
    //  · `HaulJob`은 `storage.Inventory.TryAdd`로 **창고 필터를 우회**했다(`CanAccept`를 손으로
    //    한 번 더 물어 가려 놓았지만, 폴백 창고에는 그 검사도 없었다).
    //  · 마지막 처리도 셋이 달랐다 — 배낭 복귀만, 바닥 드롭만, 둘 다.
    //
    // **순서: 지정한 창고 → 받아 줄 다른 창고 → 배낭 → 바닥.** 뒤로 갈수록 나쁜 결과라
    // 이 차례가 곧 "덜 나쁜 쪽부터"다. 배낭이 바닥보다 앞인 이유는, 들고 있으면 짐내림이
    // 나중에 옮겨 주지만 바닥에 떨구면 그것이 다시 운반 일감이 되기 때문이다.
    //
    // 반환값은 **끝내 아무 데도 못 넣은 수량**(정상이면 0).
    public static int Deliver(Storage preferred, ItemData item, int amount, ItemQuality quality,
                              UnitBrain carrier, Vector3 dropAt)
    {
        if (item == null || amount <= 0) return 0;

        int left = amount;

        // ① 지정한 창고. **`Storage.TryAdd`로 넣는다** — 필터는 그쪽이 본다.
        if (preferred != null) left -= preferred.TryAdd(item, left, quality);

        // ② 받아 줄 다른 창고. `FindNearest`가 필터와 **자리까지** 본다.
        if (left > 0)
        {
            var fallback = FindNearest(dropAt, item, left, quality);
            if (fallback != null && fallback != preferred) left -= fallback.TryAdd(item, left, quality);
        }

        // ③ 배낭. 방금 뺀 자리라 대개 그대로 들어간다.
        if (left > 0 && carrier != null && carrier.Inventory != null)
            left -= carrier.Inventory.TryAdd(item, left, quality);

        // ④ 바닥. 여기까지 왔으면 창고가 다 찼다는 뜻이라 알린다 —
        //    조용히 떨구면 "물건이 왜 밖에 있지"의 답이 화면에 없다.
        if (left > 0)
        {
            DroppedItem.Spawn(item, left, dropAt, quality);
            Debug.Log($"[Storage] {item.ItemName} {left}개를 받아 줄 곳이 없어 바닥에 두었습니다.");
            left = 0;
        }

        return left;
    }

    // **운반 목적지는 도중에 사라진다.** 일감을 만들 땐 있던 창고가 폰이 걸어가는 사이에
    // 철거될 수 있다 — 그걸 안 보고 `_storage.transform`을 만지면 `MissingReferenceException`으로
    // BT가 통째로 멎는다(`GroundHaulJob`·`HaulJob`이 둘 다 이랬다. 후자는 부릴 때만 봤고
    // 목적지 칸을 정할 때는 안 봤다).
    //
    // 살아 있으면 그대로, 아니면 받아 줄 다른 창고로 갈아탄다. 그마저 없으면 null —
    // 부르는 쪽이 "가져갈 데가 없다"를 처리해야 한다(짐은 출처나 배낭에 남긴다).
    public static Storage Rebind(Storage current, Vector3 from, ItemData item, int amount,
                                 ItemQuality quality = ItemQuality.Common)
        => current != null ? current : FindNearest(from, item, amount, quality);

    static Storage FindNearestFitting(Vector3 position, ItemData item, int amount, ItemQuality quality)
    {
        Storage nearest = null;
        float minDist = float.MaxValue;
        foreach (var sb in All)
        {
            if (!sb.IsActive) continue;
            if (item != null && !sb.HasRoomFor(item, amount, quality)) continue;
            float d = Vector3.Distance(position, sb.transform.position);
            if (d < minDist) { minDist = d; nearest = sb; }
        }
        return nearest;
    }
}
