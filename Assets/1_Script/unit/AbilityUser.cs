using System.Collections.Generic;
using UnityEngine;

// **폰이 지금 쓸 수 있는 능동과 그 쿨다운.**
//
// 능동을 **갖고 있는지**는 장비가 답하고(`WeaponData.Ability`·`ArmorData.Ability`), 여기는
// *지금 쓸 수 있나*와 *써서 무슨 일이 일어나나*만 든다. 목록을 폰이 따로 들면 장비를 벗겨도
// 능력이 남는다.
[RequireComponent(typeof(UnitBrain))]
public class AbilityUser : MonoBehaviour
{
    public static event System.Action<AbilityUser> OnChanged;

    UnitBrain  _brain;
    Equipment  _equipment;

    readonly List<AbilityData> _abilities = new();
    readonly Dictionary<AbilityData, float> _readyAt = new();

    // 장비가 바뀔 때만 다시 모은다 — 매 프레임 여섯 칸을 훑을 이유가 없다.
    bool _dirty = true;

    public IReadOnlyList<AbilityData> Abilities { get { Refresh(); return _abilities; } }

    void Awake()
    {
        _brain     = GetComponent<UnitBrain>();
        _equipment = GetComponent<Equipment>();
    }

    void OnEnable()  => Equipment.OnEquipChanged += HandleEquipChanged;
    void OnDisable() => Equipment.OnEquipChanged -= HandleEquipChanged;

    void HandleEquipChanged(Equipment e)
    {
        if (e != _equipment) return;
        _dirty = true;
        OnChanged?.Invoke(this);
    }

    void Refresh()
    {
        if (!_dirty) return;
        _dirty = false;
        _abilities.Clear();
        if (_equipment == null) return;

        Add(_equipment.MainHand != null ? _equipment.MainHand.Ability : null);
        Add(_equipment.OffHand  != null ? _equipment.OffHand.Ability  : null);
        for (var slot = ArmorSlot.Head; slot <= ArmorSlot.Legs; slot++)
        {
            var armor = _equipment.GetArmor(slot);
            Add(armor != null ? armor.Ability : null);
        }
    }

    // **같은 능력을 두 번 담지 않는다** — 중갑 네 벌이 다 도발을 주면 칸이 넷이 되는데,
    // 쿨다운은 능력마다 하나라 **눌러도 세 칸이 회색인 채로 남는다.**
    void Add(AbilityData ability)
    {
        if (ability != null && !_abilities.Contains(ability)) _abilities.Add(ability);
    }

    // 남은 쿨다운(초). 0이면 지금 쓸 수 있다.
    public float CooldownLeft(AbilityData ability)
    {
        if (ability == null) return 0f;
        return _readyAt.TryGetValue(ability, out float at) ? Mathf.Max(0f, at - Time.time) : 0f;
    }

    // **쓸 수 있나** — 쿨다운 · 몸 상태. 여기 한 곳이 답하므로 입력·화면이 따로 재지 않는다.
    public bool CanUse(AbilityData ability)
    {
        if (ability == null || _brain == null) return false;
        Refresh();
        if (!_abilities.Contains(ability)) return false;
        if (CooldownLeft(ability) > 0f) return false;

        // 쓰러졌거나 갇혔거나 업힌 폰은 못 쓴다 — 명령을 받을 수 없는 상태와 **같은 물음**이다.
        var bb = _brain.Blackboard;
        if (bb != null && (bb.IsKnockedOut || bb.IsPrisoner)) return false;
        return _brain.CanObeyNow;
    }

    // 실제로 쓴다. **효과는 여기서 짜지 않는다** — 종류마다 제 주인이 있다(`Taunt`).
    // 여기서 분기하면 능력이 늘 때마다 이 파일이 전투 규칙을 알게 된다.
    public bool TryUse(AbilityData ability)
    {
        if (!CanUse(ability)) return false;

        int affected = ability.Kind switch
        {
            AbilityKind.Taunt => Taunt.Apply(_brain, ability),
            _                 => 0,
        };

        // **아무도 안 걸려도 쿨다운은 돈다**(사용자 결정, 2026-08-23). 한때 빈 허공에 쓴 것은
        // 안 물렸는데, 그러면 **걸릴 때까지 눌러 보는 것이 최선**이 되어 *언제 쓰느냐*가
        // 결정이 아니게 된다 — 그 결정을 만들려고 능동으로 둔 것이다. 헛치는 것도 선택이다.
        _readyAt[ability] = Time.time + ability.Cooldown;

        // **쓰면 는다** — 스킬 포인트가 없는 대신 이것이 성장로다.
        // **헛친 것으로는 안 는다** — 그건 아무 일도 안 일어난 것이라 배울 것도 없다.
        if (affected > 0 && ability.Kind == AbilityKind.Taunt && _brain.Stats != null)
            _brain.Stats.GrowStat(StatName.Toughness, CombatXp.Sol(_brain.Stats.Toughness) * 2f);

        OnChanged?.Invoke(this);
        return true;
    }
}
