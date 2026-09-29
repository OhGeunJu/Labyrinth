using System.Collections.Generic;
using UnityEngine;

// **건물도 부서진다**(2026-09-30, 사용자: *"나무 바리게이트나 목책 벽 이런 거는 부술 수 있으면 좋겠는데"* →
// 누가: 적 + 내 폰 명령 · 무엇이: 모든 건물 · 수리: 폰이 자동으로 · 부서지면: 짓는 비용의 75%가 떨어진다).
//
// ## 체력은 짓는 값에서 나온다
//
// 건물 여든다섯에 하나씩 적으면 **값을 바꿀 때마다 체력이 뒤처진다**(비용은 `BuildingCostCreator`가 다시 쓴다).
// 그래서 기본은 **비용의 시세**에서 뽑는다 — `40 + 3 × 값(G)`: 나무 바리케이드(목재 5 = 15G) 85 · 돌 벽 100 남짓 ·
// 큰 건물은 수백. 따로 정하고 싶으면 `BuildingData.MaxHealth`(0이면 비용에서).
//
// ## 무엇으로 치나가 갈린다
//
// **둔기가 잘 부순다** — 벽을 칼로 베는 것은 말이 안 되고, 그래야 뼈 몽둥이·철퇴가 *공성 무기*로 값을 갖는다.
// 둔기 ×1.5 · 베기·찌르기 ×0.6 · 원소는 그대로. 막기·회피는 없다(건물은 안 피한다). **경험치도 없다** — 벽을
// 두드려 무기 솜씨를 올리는 것은 사냥이 아니라 착취다.
//
// ## 부서지면
//
// **짓는 비용의 75%가 그 자리에 떨어진다**(철거는 전액 — `PlacedBuilding.Refund`). 칸을 풀고 길을 다시 굽는 것은
// 철거와 같은 길(`PlacedBuilding.Break`)이다.
//
// **붙는 자리는 `PlacedBuilding.Start`** — 청사진 고스트는 컴포넌트를 끄므로 `Start`가 안 돌아 안 붙는다.
// **던전 핵·서브코어는 안 붙인다** — 핵이 부서지는 것은 건물 하나가 아니라 거점이 끝나는 일이라 따로 정할 문제다.
[DisallowMultipleComponent]
public class BuildingHealth : MonoBehaviour
{
    public static readonly List<BuildingHealth> All = new();

    public const float DropRatio = 0.75f;

    float _max = -1f, _hp;

    public float Max     { get { EnsureInit(); return _max; } }
    public float Current { get { EnsureInit(); return _hp; } }
    public bool  Damaged => Current < Max - 0.01f;
    public float Ratio   => Max > 0f ? Current / Max : 1f;

    PlacedBuilding _pb;
    public PlacedBuilding Building => _pb != null ? _pb : (_pb = GetComponent<PlacedBuilding>());

    // 체력이 바뀌었다 — 수리 일감(`RepairDispatcher`)·화면이 듣는다.
    public static event System.Action<BuildingHealth> OnChanged;

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    void EnsureInit()
    {
        if (_max > 0f) return;
        _max = MaxFor(Building != null ? Building.Data : null);
        _hp  = _max;
    }

    // 최대 체력 — 위 머리글. **답은 여기 하나다**(살펴보기·수리 눈금이 같이 읽는다).
    public static float MaxFor(BuildingData data)
    {
        if (data == null) return 100f;
        if (data.MaxHealth > 0f) return data.MaxHealth;

        float value = 0f;
        foreach (var c in data.Costs)
            if (c.Item != null && c.Amount > 0) value += Mathf.Max(1, c.Item.BaseValue) * c.Amount;
        return Mathf.Round(40f + 3f * value);
    }

    // 무기 종류마다 건물에 얼마나 드나.
    public static float TypeMult(DamageType t) => t switch
    {
        DamageType.Blunt => 1.5f,
        DamageType.Slash => 0.6f,
        DamageType.Pierce => 0.6f,
        _ => 1f,
    };

    public void TakeDamage(float amount, DamageType type, GameObject from)
    {
        EnsureInit();
        if (_hp <= 0f || amount <= 0f) return;

        _hp = Mathf.Max(0f, _hp - amount * TypeMult(type));
        OnChanged?.Invoke(this);

        if (_hp <= 0f) Break(from);
    }

    // 고친다 — 수리 일감이 부른다. 넘치지 않는다.
    public void Repair(float amount)
    {
        EnsureInit();
        if (amount <= 0f || _hp <= 0f) return;
        _hp = Mathf.Min(_max, _hp + amount);
        OnChanged?.Invoke(this);
    }

    void Break(GameObject from)
    {
        var pb = Building;
        string name = pb != null && pb.Data != null ? pb.Data.BuildingName : gameObject.name;
        EventNotificationUI.Notify("건물", $"{name}이(가) 부서졌습니다.");
        if (pb != null) pb.Break(DropRatio);
        else Destroy(gameObject);
    }

    // 폰에서 이 건물까지 — **한가운데가 아니라 가장 가까운 면까지**(큰 건물은 가운데까지 재면 영영 사거리에 안 든다).
    public float DistanceFrom(Vector3 from)
    {
        var p = ClosestPoint(from);
        p.y = from.y;
        return Vector3.Distance(from, p);
    }

    public Vector3 ClosestPoint(Vector3 from)
    {
        var cols = GetComponentsInChildren<Collider>();
        bool any = false;
        var b = new Bounds(transform.position, Vector3.zero);
        foreach (var c in cols)
        {
            if (c == null || !c.enabled || c.isTrigger) continue;
            if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
        }
        if (!any && Building != null && Building.Data != null)
        {
            var s = Building.Data.Size;
            b = new Bounds(transform.position, new Vector3(Mathf.Max(1, s.x), 2f, Mathf.Max(1, s.y)));
        }
        return b.ClosestPoint(from);
    }
}
