using System.Collections.Generic;
using UnityEngine;

// **고른 폰이 지금 쓸 수 있는 능동.**
//
// **규격은 씬이 갖는다** — 바의 자리·칸 크기·색은 `DungeonColony → Create Ability Bar`가
// 세워 둔 것이고, 여기는 **그 안을 채운다**(칸을 몇 개 켜고 무엇을 그릴지). 칸 하나의 모양은
// 씬의 `SlotTemplate`을 복제해 쓴다 — 코드가 모양을 만들면 **편집 중에 보이는 것과 게임의
// 모양이 갈린다.**
//
// **첫 번째로 고른 폰만 본다.** 여럿을 골라도 칸에 뜨는 것은 하나여야 한다 — 폰마다 능력이
// 다르면 무엇을 그릴지 정할 수가 없고, 단축키는 **고른 폰 전부**에게 나간다
// (`AbilityController`). 화면은 대표 한 명, 명령은 전원 — 소집이 이미 그 모양이다.
public class AbilityBarUI : MonoBehaviour
{
    [Header("씬 참조")]
    [Tooltip("바 전체. 쓸 능력이 없으면 통째로 꺼진다")]
    [SerializeField] GameObject bar;
    [Tooltip("칸 하나의 모양. 런타임에 꺼지고 복제 원본이 된다")]
    [SerializeField] AbilityBarSlotUI slotTemplate;

    readonly List<AbilityBarSlotUI> _slots = new();

    SelectionManager _selection;
    UnitBrain        _bound;
    AbilityUser      _user;

    void Awake()
    {
        if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
        if (bar != null) bar.SetActive(false);
    }

    void OnEnable()  => AbilityUser.OnChanged += HandleAbilityChanged;
    void OnDisable() => AbilityUser.OnChanged -= HandleAbilityChanged;

    void HandleAbilityChanged(AbilityUser user)
    {
        if (user == _user) Rebuild();
    }

    void Update()
    {
        if (_selection == null) _selection = FindFirstObjectByType<SelectionManager>();

        // **고른 폰이 바뀌면 다시 짓는다.** 매 프레임 다시 지으면 커서를 올려 둔 툴팁이
        // 깜빡이고 클릭이 씹힌다 — 바뀔 때만.
        var brain = FirstSelected();
        if (brain != _bound) { _bound = brain; Rebuild(); }

        // 쿨다운만 매 프레임 갱신한다 — 그건 계속 변하는 값이다.
        for (int i = 0; i < _slots.Count; i++) _slots[i].RefreshCooldown();
    }

    UnitBrain FirstSelected()
    {
        if (_selection == null) return null;
        foreach (var mover in _selection.Selected)
        {
            if (mover == null) continue;
            var brain = mover.GetComponent<UnitBrain>();
            if (brain != null) return brain;
        }
        return null;
    }

    void Rebuild()
    {
        foreach (var slot in _slots) if (slot != null) Destroy(slot.gameObject);
        _slots.Clear();

        _user = _bound != null ? _bound.GetComponent<AbilityUser>() : null;
        var abilities = _user != null ? _user.Abilities : null;

        // **쓸 것이 없으면 바를 통째로 끈다.** 빈 칸 넷이 늘 떠 있으면 화면만 먹고, 무엇보다
        // *"이 폰은 능동이 없다"*가 **빈 칸**과 **못 쓰는 칸**으로 나뉘어 보인다.
        int count = abilities != null ? abilities.Count : 0;
        if (bar != null) bar.SetActive(count > 0);
        if (count == 0 || slotTemplate == null) return;

        // 단축키가 넷까지라 칸도 넷까지다 — 누를 수는 있는데 안 보이는 칸,
        // 또는 보이는데 못 누르는 칸이 생기면 그건 기능이 아니라 숨은 조작이다.
        count = Mathf.Min(count, AbilityController.SlotCount);

        for (int i = 0; i < count; i++)
        {
            var slot = Instantiate(slotTemplate, slotTemplate.transform.parent);
            slot.gameObject.SetActive(true);
            slot.Bind(_user, abilities[i], i + 1);
            _slots.Add(slot);
        }
    }
}
