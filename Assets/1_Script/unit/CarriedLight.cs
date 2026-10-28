using UnityEngine;

// **폰이 들고 다니는 불빛 — 허리 램프 · 손 횃불**(2026-10-15).
//
// 사용자: *"허리에 램프, 횃불 장착 가능하게 and 손에도 횃불"* → *"허리 램프는 영구지속 … 빛이 비추는 범위도 큰 차이
// 없었으면 · 손 횃불은 허리 램프를 얻기 전에 사용하는 느낌 · 공격 가능 · 시간 타면 꺼지는 소모품 · 보조손"*.
//
// ## 불을 새로 만들지 않는다
//
// 빛은 **`LightSource` 하나**를 폰에 매단다 — 화톳불·벽 횃불·채광 구멍이 쓰는 그것이다. 그래서 시야(`Vision`)는
// 아무것도 안 고쳐도 따라온다: 램프를 찬 폰 둘레에서는 밤이 걷히고, 그것은 **누구에게나**다(우리 램프 곁의 산적도
// 잘 본다 — `LightSource`가 세력을 안 가리는 규칙 그대로).
//
// ## 무엇을 비추나는 장비가 답한다
//
// 고르는 것은 `Equipment.LightItem`(보조손 횃불과 허리 램프 중 넓은 쪽 하나), 얼마나는 `ItemData.LightRadius`.
// 여기는 그 답을 등으로 세우기만 한다. 장비가 바뀔 때마다 `Equipment.Changed`가 부른다.
//
// ## 그림자를 안 던진다
//
// 그림자를 던지는 등은 여덟 개까지다(`ShadowBudget`). 폰마다 던지면 굴의 횃불·화톳불 몫을 폰이 다 먹는다.
//
// 손에 든 횃불의 모형은 `PawnProps`가 보조손에 쥐여 준다(표는 `HandPropSet` — 모형의 팩 등은 떼고 빛은 여기 하나).
// 허리에 단 등의 모형은 아직 없다 — 빛만 선다.
public class CarriedLight : MonoBehaviour
{
    // 횃불은 벽 횃불과 같은 불빛(`WallTorch.prefab`의 등 — 색 · 세기 2.4). 램프는 야광석이라 차고 고르다.
    static readonly Color FlameColor = new(1.00f, 0.62f, 0.28f);
    static readonly Color LampColor  = new(0.86f, 0.96f, 0.78f);
    const float FlameIntensity = 2.4f;
    const float LampIntensity  = 2.0f;

    // 등이 닿는 거리 = 시야가 걷히는 거리 × 이것. 벽 횃불이 반경 5에 등 6이다.
    const float RangePerRadius = 1.2f;

    // 횃불은 손높이에서, 램프는 허리에서.
    const float FlameHeight = 1.5f;
    const float LampHeight  = 1.0f;

    ItemData   _item;
    GameObject _lamp;

    public static void Sync(Equipment eq)
    {
        if (eq == null) return;
        var item = eq.LightItem;

        if (!eq.TryGetComponent(out CarriedLight cl))
        {
            if (item == null) return;
            cl = eq.gameObject.AddComponent<CarriedLight>();
        }
        cl.Set(item);
    }

    void Set(ItemData item)
    {
        if (item == _item) return;
        _item = item;

        // **바뀌면 다시 세운다** — `LightSource`는 `Awake`에서 등의 범위와 기준 세기를 한 번 잡는다. 고쳐 쓰면
        // 그 기준이 옛 물건의 것으로 남는다. 바뀌는 일은 장비를 갈 때뿐이라 값이 없다.
        if (_lamp != null) Destroy(_lamp);
        _lamp = null;
        if (item == null) return;

        bool flame = item is WeaponData;   // 보조손에 드는 것은 불이다

        _lamp = new GameObject(flame ? "CarriedLight_Torch" : "CarriedLight_Lamp");
        _lamp.transform.SetParent(transform, false);
        _lamp.transform.localPosition = new Vector3(0f, flame ? FlameHeight : LampHeight, 0f);

        var l = _lamp.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = flame ? FlameColor : LampColor;
        l.intensity = flame ? FlameIntensity : LampIntensity;
        l.range     = item.LightRadius * RangePerRadius;   // `LightSource.Awake`가 화면 몫(`Reach`)을 곱한다
        l.shadows   = LightShadows.None;

        var src = _lamp.AddComponent<LightSource>();
        src.Radius    = item.LightRadius;
        src.Flicker   = flame ? 0.15f : 0.03f;
        src.AlwaysLit = true;
        src.SetLit(true);
    }

    void OnDestroy()
    {
        if (_lamp != null) Destroy(_lamp);
    }
}
