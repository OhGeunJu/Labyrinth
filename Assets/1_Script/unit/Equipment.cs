using System;
using System.Collections.Generic;
using UnityEngine;

public class Equipment : MonoBehaviour
{
    public static event Action<Equipment> OnEquipChanged;

    public WeaponData MainHand { get; private set; }
    public WeaponData OffHand  { get; private set; }

    public bool OffHandLocked => MainHand != null && MainHand.IsLargeWeapon;

    readonly Dictionary<ArmorSlot, ArmorData>     _armor    = new();
    readonly Dictionary<ArmorSlot, ItemInstance>  _armorInst = new();
    ItemInstance _mainHandInst;
    ItemInstance _offHandInst;

    public ArmorData GetArmor(ArmorSlot slot) =>
        _armor.TryGetValue(slot, out var a) ? a : null;

    // 차고 있는 **인스턴스**(등급을 들고 있다). 장비칸끼리 맞바꿀 때 `ItemData`만 넘기면
    // 희귀 철검이 옮기는 것만으로 일반이 된다.
    public ItemInstance MainHandInstance => _mainHandInst;
    public ItemInstance OffHandInstance  => _offHandInst;
    public ItemInstance ArmorInstance(ArmorSlot slot) =>
        _armorInst.TryGetValue(slot, out var i) ? i : null;

    // 장착 아이템의 등급 배율 (갑옷 방어력에 곱함)
    public float GetArmorQualityMult(ArmorSlot slot) =>
        _armorInst.TryGetValue(slot, out var inst) ? inst.Quality.StatMultiplier() : 1f;

    Inventory _inventory;
    UnitBlackboard _bb;
    UnitData _unitData;

    void Start() => Bind();

    // **필요할 때 스스로 묶는다.** `Start`에만 두면 컴포넌트 실행 순서에 기대게 되는데,
    // `UnitData.Loadout`처럼 **다른 컴포넌트의 `Start`에서** 장비를 채우는 길이 생기면
    // 그쪽이 먼저 돌 때 `_inventory`가 null이라 장착이 조용히 실패한다(에러도 안 난다).
    void Bind()
    {
        if (_inventory == null) _inventory = GetComponent<Inventory>();

        var brain = GetComponent<UnitBrain>();
        if (brain == null) return;

        if (_bb == null)       _bb       = brain.Blackboard;
        if (_unitData == null) _unitData = brain.Data;
    }

    // **이 폰이 장비를 낄 수 있나 — 종족이 답한다**(`UnitData.CanEquip`).
    //
    // 골렘에게 검을 쥐여 주는 것이 이상해서 넣었다(2026-08-28, 사용자 지적). 막는 자리는
    // **`Wear*` 셋** — 배낭 경로(`Equip`)만 막으면 밖에서 건너오는 길(상자·시체·다른 폰의
    // 장비칸)이 그대로 열려 있다. 이 저장소가 `OffHandLocked`를 두고 이미 배운 것과 같은
    // 자리다: **판단은 `Wear*` 한 곳에.**
    public bool CanEquip
    {
        get
        {
            Bind();
            return _unitData == null || _unitData.CanEquip;
        }
    }

    // **이 무기를 쥘 수 있나 — 종족이 답한다**(`UnitData.ForbiddenWeapons`, 2026-10-28). 막는 자리는 `CanEquip`과 같은
    // `Wear*`다(배낭 · 상자 · 시체 · 다른 폰의 칸 — 어느 길로 와도 여기를 지난다).
    public bool CanWield(WeaponData weapon)
    {
        Bind();
        return weapon == null || _unitData == null || !_unitData.Forbids(weapon.Type);
    }

    // ── 인스턴스 직접 장착 ───────────────────────────────────────────────────
    //
    // **배낭을 안 거친다.** 밖(상자·시체·다른 폰의 장비칸)에서 건너온 것은 내 배낭에 자리가
    // 없어도 차야 한다 — 빈 칸에 넣는 데 배낭 자리를 요구하면 **"칸은 비었는데 왜 안 들어가지"**
    // 가 된다. 벗겨지는 쪽은 `ReturnToInventory`가 배낭으로, 자리가 없으면 발밑으로 보낸다.
    //
    // 아래 `Equip*`(배낭에서 꺼내 차는 평소 경로)도 전부 이 셋으로 모인다 — 장착 지점이
    // 여럿이면 하나가 반드시 `SyncBlackboard`나 `OnEquipChanged`를 빠뜨린다.

    public bool WearMainHand(ItemInstance inst)
    {
        if (!CanEquip) return false;
        if (inst?.Data is not WeaponData weapon) return false;
        if (!CanWield(weapon)) return false;

        // **방패는 주손에 들지 않는다.** 주손 칸에 떨어뜨려도 보조손으로 보낸다 —
        // 방패를 들고 때릴 수는 없는데, 막지 않으면 주손에 끼워 무기를 못 드는 상태가 된다.
        // **횃불도 같다**(2026-10-15) — 보조손에 드는 불이다.
        if (BelongsInOffHand(weapon)) return WearOffHand(inst);

        Bind();

        // **양손 무기는 보조손을 비운다.** 안 보면 순서에 따라 규칙이 갈렸다 — 대검을 든
        // 채로는 방패가 안 껴지는데(`OffHandLocked`), **방패를 먼저 끼고 대검을 끼우면 둘 다
        // 찬 상태**가 만들어졌다. 게다가 그 뒤로는 `OffHandLocked`가 참이라 방패를 바꿀 수도
        // 없다. 순서로 갈리는 것은 규칙이 아니다.
        if (weapon.IsLargeWeapon && !StowOffHand()) return false;

        if (MainHand != null)
            ReturnToInventory(MainHand, _mainHandInst);

        MainHand      = weapon;
        _mainHandInst = inst;
        SyncBlackboard();
        Changed();
        Debug.Log($"[Equipment] {gameObject.name} 장착: {weapon.ItemName} [{inst.Quality.DisplayName()}]");
        return true;
    }

    // 차고 있던 것을 **배낭으로** 옮긴다. 넣을 자리가 없으면 `false`이고 **아무것도 안 바뀐다** —
    // 발밑에 떨구지 않는다: 바닥 물건은 이제 저절로 안 실려 가므로(체크한 것만 나른다,
    // 2026-08-13) 무기를 바꾼 값이 **치우는 일**로 돌아온다. 못 바꾸면 안 바꾸는 편이 낫다.
    bool StowOffHand()
    {
        if (OffHand == null) return true;
        if (_inventory == null) return false;

        if (!PutInBackpack(OffHand, _offHandInst)) return false;

        OffHand      = null;
        _offHandInst = null;
        return true;   // `OnEquipChanged`는 부르는 쪽이 이어서 낸다
    }

    bool StowMainHand()
    {
        if (MainHand == null) return true;
        if (_inventory == null) return false;

        if (!PutInBackpack(MainHand, _mainHandInst)) return false;

        MainHand      = null;
        _mainHandInst = null;
        SyncBlackboard();
        return true;
    }

    public bool WearOffHand(ItemInstance inst)
    {
        if (!CanEquip) return false;
        if (inst?.Data is not WeaponData item) return false;
        if (!CanWield(item)) return false;

        Bind();

        // **방패를 끼면 양손 무기가 벗겨진다.** 거절만 하면 "왜 안 껴지지"가 되고, 무엇보다
        // 반대 방향(대검을 끼면 방패가 벗겨진다)과 규칙이 갈린다.
        // 하나로 적으면: **한 손짜리와 두 손짜리는 같이 못 든다 — 나중에 찬 쪽이 이긴다.**
        if (OffHandLocked && !StowMainHand()) return false;

        if (OffHand != null)
            ReturnToInventory(OffHand, _offHandInst);

        OffHand      = item;
        _offHandInst = inst;
        _burnClock   = -1f;
        Changed();
        Debug.Log($"[Equipment] {gameObject.name} 오프핸드 장착: {item.ItemName} [{inst.Quality.DisplayName()}]");
        return true;
    }

    public bool WearArmor(ItemInstance inst)
    {
        if (!CanEquip) return false;
        if (inst?.Data is not ArmorData armor) return false;

        Bind();
        if (_armor.TryGetValue(armor.Slot, out var prev))
            ReturnToInventory(prev, _armorInst.TryGetValue(armor.Slot, out var pi) ? pi : null);

        _armor[armor.Slot]     = armor;
        _armorInst[armor.Slot] = inst;
        Changed();
        Debug.Log($"[Equipment] {gameObject.name} 갑옷 장착: {armor.ItemName} ({armor.Slot}) [{inst.Quality.DisplayName()}]");
        return true;
    }

    // 차고 있던 것을 **배낭에 안 돌려주고 그대로 빼낸다.** 장비칸끼리 교환할 때 쓴다 —
    // 배낭을 거치면 배낭이 꽉 찼을 때 교환이 실패하거나 물건이 발밑에 떨어진다.
    public ItemInstance TakeOff(ArmorSlot slot)
    {
        if (!_armor.TryGetValue(slot, out _)) return null;
        _armorInst.TryGetValue(slot, out var inst);
        _armor.Remove(slot);
        _armorInst.Remove(slot);
        Changed();
        return inst;
    }

    public ItemInstance TakeOffMainHand()
    {
        if (MainHand == null) return null;
        var inst = _mainHandInst;
        MainHand = null; _mainHandInst = null;
        SyncBlackboard();
        Changed();
        return inst;
    }

    public ItemInstance TakeOffOffHand()
    {
        if (OffHand == null) return null;
        var inst = _offHandInst;
        OffHand = null; _offHandInst = null;
        Changed();
        return inst;
    }

    // ── 배낭에서 꺼내 차기 (평소 경로) ───────────────────────────────────────

    // 인벤토리에서 무기를 꺼내 MainHand에 장착
    public bool Equip(WeaponData weapon)
    {
        Bind();
        if (weapon == null) return false;
        if (BelongsInOffHand(weapon)) return EquipOffHand(weapon);

        var inst = PullOne(weapon);
        return inst != null && (WearMainHand(inst) || PutBack(inst));
    }

    // **못 찼으면 도로 넣는다 — 늘 `false`를 돌려준다.**
    //
    // `PullOne`은 이미 배낭에서 뺐다. 예전엔 `Wear*`가 실패할 길이 사실상 없어서 그냥
    // 두어도 됐는데, **양손 무기가 보조손을 못 비울 때 거절하도록 바꾸면서**(2026-08-13)
    // 그 길이 생겼다 — 그대로 두면 무기가 조용히 사라진다.
    bool PutBack(ItemInstance inst)
    {
        if (inst?.Data != null) _inventory?.TryAdd(inst.Data, 1, inst.Quality);
        return false;
    }

    // 배낭에서 **한 개만** 빼내 독립된 인스턴스로 만든다.
    //
    // 전엔 `RemoveOne` 뒤에 그 인스턴스를 그대로 차고 있었는데, 스택이 남아 있으면
    // (철검 3개 중 하나를 차면) **배낭에 남은 뭉치와 같은 객체를 차고 있는 셈**이 된다.
    // 등급만 읽는 동안은 티가 안 나지만, 인스턴스가 잠금·수량을 들고 있어 언제든 어긋난다.
    ItemInstance PullOne(ItemData data)
    {
        var found = FindInInventory(data);

        // **열린 것부터 든다** — 반쯤 탄 횃불을 두고 새것을 켜면 배낭에 타다 만 것만 쌓인다.
        if (found != null && found.IsPartial)
        {
            var q    = found.Quality;
            float left = _inventory.TakeOpened(found);
            return new ItemInstance(data, 1, 0, 0, false, q) { Left = left };
        }

        // 찾은 인스턴스를 정확히 소모 — Remove(data)는 다른 등급 인스턴스를 뺄 수 있음
        if (found == null || !_inventory.RemoveOne(found)) return null;

        return new ItemInstance(data, 1, 0, 0, false, found.Quality);
    }

    // MainHand 해제 → 인벤토리로 반환
    public void Unequip()
    {
        if (MainHand == null) return;

        ReturnToInventory(MainHand, _mainHandInst);
        Debug.Log($"[Equipment] {gameObject.name} 해제: {MainHand.ItemName}");
        MainHand      = null;
        _mainHandInst = null;
        SyncBlackboard();
        Changed();
    }

    public bool EquipOffHand(WeaponData item)
    {
        Bind();
        if (item == null) return false;

        // **`OffHandLocked`를 여기서 안 막는다** — 막으면 배낭 경로만 옛 규칙을 쓴다.
        // 아래 `WearOffHand`가 양손 무기를 배낭으로 치우고(못 치우면 거절하고) 끼운다.
        // 규칙이 문마다 갈리지 않게 **판단은 `Wear*` 한 곳**에만 둔다.
        var inst = PullOne(item);
        return inst != null && (WearOffHand(inst) || PutBack(inst));
    }

    public void UnequipOffHand()
    {
        if (OffHand == null) return;

        ReturnToInventory(OffHand, _offHandInst);
        Debug.Log($"[Equipment] {gameObject.name} 오프핸드 해제: {OffHand.ItemName}");
        OffHand      = null;
        _offHandInst = null;
        Changed();
    }

    public bool EquipArmor(ArmorData armor)
    {
        Bind();
        if (armor == null) return false;

        var inst = PullOne(armor);
        return inst != null && (WearArmor(inst) || PutBack(inst));
    }

    public void UnequipArmor(ArmorSlot slot)
    {
        if (!_armor.TryGetValue(slot, out var armor)) return;

        _armorInst.TryGetValue(slot, out var inst);
        ReturnToInventory(armor, inst);
        _armor.Remove(slot);
        _armorInst.Remove(slot);
        Changed();
        Debug.Log($"[Equipment] {gameObject.name} 갑옷 해제: {armor.ItemName} ({slot})");
    }

    // 주손이 아니라 보조손에 가는 것 — 방패와 횃불.
    public static bool BelongsInOffHand(WeaponData w) =>
        w != null && (w.Type == WeaponType.Shield || w.Type == WeaponType.Torch);

    // ── 들고 다니는 불빛 (2026-10-15) ──────────────────────────────────────────
    //
    // **어느 것이 비추나** — 보조손 횃불과 허리 램프 중 **넓은 쪽 하나**다. 둘을 겹쳐 더 밝게 하지 않는다
    // (`LightSource.BrightestAt`이 가장 밝은 하나만 보는 것과 같은 규칙). 세우는 것은 `CarriedLight`.
    public ItemData LightItem
    {
        get
        {
            ItemData best = OffHand != null && OffHand.GivesLight ? OffHand : null;
            var belt = GetArmor(ArmorSlot.Belt);
            if (belt != null && belt.GivesLight && (best == null || belt.LightRadius > best.LightRadius))
                best = belt;
            return best;
        }
    }

    // **걸음 배율** — 찬 갑옷 전부의 `MoveSpeedMult`를 곱한다(2026-10-18, 신발). `PawnMover`가 읽는다.
    public float MoveSpeedMult
    {
        get
        {
            float m = 1f;
            foreach (var a in _armor.Values) if (a != null) m *= a.MoveSpeedMult;
            return m;
        }
    }

    // **장비가 바뀌는 문은 하나다** — 화면(`OnEquipChanged`)과 빛(`CarriedLight`)이 같이 따라온다.
    void Changed()
    {
        CarriedLight.Sync(this);
        OnEquipChanged?.Invoke(this);
    }

    // ── 횃불이 타는 시간 ──────────────────────────────────────────────────────
    //
    // **남은 시간은 횃불이 든다**(`ItemInstance.Left`, 2026-10-16). 처음(10-15)엔 폰이 들었는데 사용자: *"다른 폰이
    // 그 횃불을 집었을때는?"* — 벗어서 넘기면 새것이 됐다. 이제 반쯤 탄 횃불은 배낭에서도 바닥에서도 다른 손에서도
    // 반쯤 탄 횃불이다. 다 타면 그 하나가 없어지고 **배낭의 같은 횃불을 이어 든다**(열린 것부터 — `PullOne`).
    //
    // 시계는 게임 시각(`GameTimeManager.Elapsed`)이라 배속을 그대로 탄다.
    float _burnClock = -1f;

    void Update()
    {
        var torch = OffHand;
        if (torch == null || torch.BurnDays <= 0f || _offHandInst == null) { _burnClock = -1f; return; }

        float now = GameTimeManager.Elapsed;
        if (_burnClock < 0f) { _burnClock = now; return; }
        float dt = now - _burnClock;
        _burnClock = now;
        if (dt <= 0f) return;

        _offHandInst.Left -= dt / (torch.BurnDays * GameTimeManager.DayDuration);
        if (_offHandInst.Left <= ItemInstance.Epsilon) BurnOut(torch);
    }

    void BurnOut(WeaponData torch)
    {
        Bind();
        OffHand      = null;
        _offHandInst = null;

        bool relit = _inventory != null && FindInInventory(torch) != null && EquipOffHand(torch);
        if (relit) return;   // 새 횃불을 들면서 `Changed`가 이미 났다

        Changed();
        if (TryGetComponent(out Faction f) && f.IsPlayer && TryGetComponent(out UnitBrain b))
            EventNotificationUI.Notify("횃불", $"{b.UnitName}의 {torch.ItemName}이(가) 다 탔습니다.");
    }

    // Blackboard 상태를 장착 상태에 맞게 갱신 (무기 등급 배율 반영)
    void SyncBlackboard()
    {
        _bb.IsUnarmed = MainHand == null;
        float qualMult = _mainHandInst?.Quality.StatMultiplier() ?? 1f;
        _bb.AttackDamage = MainHand != null
            ? MainHand.BaseDamage * qualMult
            : (_unitData != null ? _unitData.AttackDamage : 10f);
        _bb.AttackRange = MainHand != null
            ? MainHand.AttackRange
            : (_unitData != null ? _unitData.AttackRange : 1.5f);

        // **인지력 배수가 붙을지는 무기가 정한다**(2026-08-26) — 맨손·근접에는 안 붙는다.
        // 안 넣으면 칼을 든 폰이 인지력만으로 두 칸 밖에서 휘두른다.
        _bb.RangedWeapon = MainHand != null && MainHand.IsRanged;
    }

    // 벗은 것을 배낭에 돌려준다. **자리가 없으면 발밑에 내려놓는다.**
    //
    // 전에는 `TryAdd`의 반환값을 안 보고 넣어서, 격자가 꽉 차면 **차고 있던 장비가 조용히
    // 사라졌다.** 하나를 빼면 하나가 들어갈 자리가 생길 것 같지만 그렇지 않다 — 새로 차는
    // 물건이 스택 안에 있었으면(철검 3개 중 하나) 빼도 칸이 안 비고, 벗은 것은 새 칸을 찾는다.
    //
    // 교체를 거절하는 길도 있었지만, 그러면 "왜 안 입지"가 되고 원인이 화면에 안 남는다.
    // 내려놓으면 눈에 보이고 운반이 알아서 창고로 옮긴다 — 이 프로젝트의 증발 방지 관례다.
    void ReturnToInventory(ItemData item, ItemInstance inst)
    {
        if (item == null) return;
        var   quality = inst != null ? inst.Quality : ItemQuality.Common;
        float left    = inst != null ? inst.Left    : 1f;
        if (PutInBackpack(item, inst)) return;

        DroppedItem.Spawn(item, 1, transform.position + Vector3.up * 0.15f, quality, left);
        Debug.Log($"[Equipment] {gameObject.name}: 배낭에 자리가 없어 {item.ItemName}을 발밑에 내려놓았습니다.");
    }

    // 배낭에 넣는다 — **남은 양째**(반쯤 탄 횃불은 반쯤 탄 채로, `Inventory.TryAddOpened`).
    bool PutInBackpack(ItemData item, ItemInstance inst)
    {
        if (_inventory == null || item == null) return false;
        var quality = inst != null ? inst.Quality : ItemQuality.Common;
        return inst != null && inst.IsPartial
            ? _inventory.TryAddOpened(item, quality, inst.Left)
            : _inventory.TryAdd(item, 1, quality) > 0;
    }

    // **열린 뭉치를 먼저 준다** — 꺼내 드는 것도 쓰다 만 것부터다(`PullOne`).
    ItemInstance FindInInventory(ItemData data)
    {
        if (_inventory == null) return null;
        ItemInstance first = null;
        foreach (var inst in _inventory.Items)
        {
            if (inst.Data != data) continue;
            if (inst.IsPartial) return inst;
            first ??= inst;
        }
        return first;
    }
}
