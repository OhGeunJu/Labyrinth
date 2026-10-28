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

    // 남은 횟수. `MaxCharges`가 0이면 아무것도 안 든다.
    //
    // **비어 있음 = 가득이다.** 그래서 하루가 바뀌면 `Clear()` 한 줄이 곧 초기화다 —
    // 능력마다 시각을 들고 하나씩 채우면 **같은 답이 능력 수만큼 생긴다.**
    readonly Dictionary<AbilityData, int> _left = new();

    // `_left`가 **어느 날의 것인가**. 날짜가 다르면 통째로 버린다.
    int _leftDay = int.MinValue;

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
        foreach (ArmorSlot slot in System.Enum.GetValues(typeof(ArmorSlot)))
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

    // ── 횟수 ─────────────────────────────────────────────────────────────────
    //
    // **쿨다운과 다른 물음이다**: 쿨다운은 *연달아 못 쓴다*, 횟수는 *오래 못 버틴다*.
    // 근거는 `AbilityData`에 적어 두었다.
    //
    // **게임 시간으로 잰다**(`GameTimeManager.Elapsed`) — 허기·작물·연구가 서 있는 축이라,
    // 여기만 실시간이면 배속을 올렸을 때 **밥은 세 배로 먹는데 능력만 안 찬다.**
    //
    // **처음 물을 때 가득 채운다** — `Awake`에서 채우면 장비를 나중에 입은 폰이 0으로 남고,
    // 장비를 갈아입을 때마다 채우면 **방패를 벗었다 끼는 것이 곧 재충전**이 된다.
    public int ChargesLeft(AbilityData ability)
    {
        if (ability == null || ability.MaxCharges <= 0) return int.MaxValue;

        // **하루에 한 번 통째로 가득**(2026-09-08, 사용자 결정). 한 칸씩 차는 시계를
        // 들고 있었는데 그 값이 아무것도 안 정했다 — 근거는 `AbilityData`에 있다.
        //
        // **`Elapsed`가 아니라 날짜로 잰다.** 경과 초로 재면 *마지막으로 쓴 뒤 하루*가 되어
        // 폰마다 초기화되는 순간이 달라지고, 그러면 *오늘 몇 번 남았나*를 폰마다 따로 세야
        // 한다. 날짜면 **온 무리가 같은 아침에 가득**이다.
        int today = GameTimeManager.CurrentDay;
        if (_leftDay != today) { _left.Clear(); _leftDay = today; }

        // **안 든 것이 곧 가득이다** — 처음 물을 때 채워 넣지 않는다(그러면 장비를 나중에
        // 입은 폰이 0으로 남거나, 갈아입는 것이 곧 재충전이 된다).
        return _left.TryGetValue(ability, out int n) ? n : ability.MaxCharges;
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
        if (ChargesLeft(ability) <= 0) return false;

        // 쓰러졌거나 갇혔거나 업힌 폰은 못 쓴다 — 명령을 받을 수 없는 상태와 **같은 물음**이다.
        var bb = _brain.Blackboard;
        if (bb != null && (bb.IsKnockedOut || bb.IsPrisoner)) return false;
        return _brain.CanObeyNow;
    }

    // **지금 쓸 만한가** — `CanUse`와 **다른 물음이다**: 저쪽은 *쿨다운이 돌았나·몸이 성한가*이고
    // 이쪽은 *써서 뭔가 달라지나*다. 사람이 누를 때는 앞의 것만 있으면 된다(**언제 쓸지는 사람이
    // 정한다**). 스스로 도는 폰에게는 그 자리가 비어 있어서, 안 물으면 **쿨다운이 도는 족족
    // 허공에 쓴다**(헛쳐도 쿨다운은 돈다 — 그렇게 정해 두었다).
    //
    // **여기서 규칙을 짜지 않는다** — `TryUse`가 효과를 종류의 주인에게 넘기는 것과 **같은
    // 모양**이다. 안 그러면 능력이 늘 때마다 이 파일이 전투 규칙을 알게 된다.
    //
    // **모르는 종류는 거짓이다** — 참으로 두면 판단을 안 적은 새 능력이 뜨는 족족 자동으로
    // 나가고, 그건 *만들다 만 것이 저 혼자 도는 것*이다.
    public bool WorthUsing(AbilityData ability)
    {
        if (ability == null || _brain == null) return false;

        return ability.Kind switch
        {
            AbilityKind.Taunt => Taunt.Worth(_brain, ability),
            _                 => false,
        };
    }

    // **스스로 도는 폰이 부르는 문**(`UseAbilityNode`). 쓸 수 있고 쓸 만한 것 하나를 쓴다.
    //
    // **`CanUse`를 먼저 묻는 순서가 중요하다** — `WorthUsing`은 둘레를 훑으므로, 쿨다운이
    // 안 돈 능력에까지 그 훑기를 돌리면 **싸우는 폰마다 매 틱 헛스캔**이 붙는다.
    //
    // 여럿이면 **목록 순서대로 첫 번째**다. 지금은 도발 하나뿐이라 고를 것이 없고, 늘어나면
    // 그때 *무엇을 먼저*가 물음이 된다 — **없는 물음에 미리 답하지 않는다.**
    public bool TryUseAny()
    {
        Refresh();
        for (int i = 0; i < _abilities.Count; i++)
        {
            var ability = _abilities[i];
            if (CanUse(ability) && WorthUsing(ability) && TryUse(ability)) return true;
        }
        return false;
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

        // **횟수도 헛쳐도 준다** — 쿨다운과 같은 이유다(걸릴 때까지 눌러 보는 것이 최선이
        // 되면 *언제 쓰느냐*가 결정이 아니게 된다).
        if (ability.MaxCharges > 0)
        {
            // **`ChargesLeft`를 먼저 부른다** — 날이 바뀌었으면 거기서 가득으로 되돌린다.
            int n = ChargesLeft(ability);
            _left[ability] = Mathf.Max(0, n - 1);
        }

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
